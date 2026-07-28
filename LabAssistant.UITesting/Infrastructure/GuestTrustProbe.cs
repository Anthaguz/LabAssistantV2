using System.Text.RegularExpressions;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>What a promoted domain controller reports about one forest trust over PowerShell Direct.</summary>
public sealed record GuestTrustInfo(string Target, string TrustType, string Direction, bool ForestTransitive)
{
    /// <summary>
    /// True when this trust is a forest trust. Get-ADTrust reports a forest trust as the explicit
    /// TrustType "Forest" on Server 2025+, or as "Uplevel" WITH ForestTransitive set on Windows Server
    /// 2022 - where a plain external (non-forest) trust ALSO reports "Uplevel" but with
    /// ForestTransitive=false. So an "Uplevel" trust only counts as a forest trust when it is forest-
    /// transitive; otherwise the probe would accept an external trust as a forest trust on exactly the
    /// server version this scenario runs against.
    /// </summary>
    public bool IsForestTrust =>
        string.Equals(TrustType, "Forest", StringComparison.OrdinalIgnoreCase) ||
        (string.Equals(TrustType, "Uplevel", StringComparison.OrdinalIgnoreCase) && ForestTransitive);

    /// <summary>True when the trust is bidirectional (Get-ADTrust reports "BiDirectional").</summary>
    public bool IsBidirectional =>
        string.Equals(Direction, "Bidirectional", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Host-side guest validator that authenticates into a promoted domain controller over PowerShell
/// Direct (VMBus) and reads the real forest-trust state the DC actually holds - the prize of a
/// forest-trust deploy, which the harness never infers from the UI's success text. A trust that
/// silently failed to establish (DNS could not resolve the peer, the cross-forest credential was
/// wrong, CreateTrustRelationship threw) shows up here as an absent trust or the wrong type/direction,
/// even when the deploy reported success. The scenario probes BOTH DCs so it proves each side of the
/// bidirectional trust exists, not just the source anchor's view.
///
/// Like <see cref="GuestDirectoryProbe"/>, this authenticates as NETBIOS\Administrator: after
/// promotion the local SAM account no longer resolves on a DC, and reading trusts needs domain-admin.
/// The password is read from the LABASSISTANT_SMOKE_ADMIN_PASSWORD environment variable INSIDE the
/// spawned PowerShell child, so the secret never appears in a script string, a process argument, or a
/// harness log. Reuses the same PowerShell Direct invocation pattern proven by the other guest probes.
/// </summary>
public sealed class GuestTrustProbe
{
    /// <summary>
    /// Polls the DC over PowerShell Direct until it reports a forest trust to
    /// <paramref name="expectedTrustedDomainDns"/> (trust replication can still be settling right after
    /// the deploy reports done) or the timeout elapses, then returns the trust's target, type and
    /// direction as the guest sees them.
    ///
    /// The readiness gate is a present forest+bidirectional trust to the expected domain: a bare
    /// Get-ADTrust that answers "not found" is not enough, because the AD module answers the moment
    /// PowerShell Direct is reachable - well before the runtime has created the trust. So this keeps
    /// polling until the guest reports the expected trusted domain AND it is a forest+bidirectional
    /// trust, and only then returns.
    ///
    /// Returns null when the guest never answered at all (or the poll was aborted). When the guest
    /// answered but never reported the expected trust within the timeout, it returns the LAST snapshot
    /// it read (or null if it only ever reported "no trust"), so the caller can report exactly what the
    /// guest ended up with - itself the failure signal for a trust that never established.
    ///
    /// <paramref name="abortIf"/> is checked before every attempt: when it returns true the poll stops
    /// immediately and returns null. The scenario passes a "did the app roll the VM back" probe here so
    /// a failed deploy fails fast instead of polling a deleted VM for the full timeout.
    /// </summary>
    public GuestTrustInfo? QueryTrust(
        string vmName,
        string netBiosName,
        string expectedTrustedDomainDns,
        TimeSpan timeout,
        Func<bool>? abortIf = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastError = "(none)";
        GuestTrustInfo? lastSnapshot = null;

        while (DateTime.UtcNow < deadline)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine($"GuestTrustProbe: aborting trust poll on '{vmName}' - the deploy target is gone (rolled back).");
                return null;
            }

            var result = PowerShellRunner.Run(BuildScript(vmName, netBiosName, expectedTrustedDomainDns), TimeSpan.FromSeconds(90));
            if (result.Success)
            {
                var found = Extract(result.StdOut, "TRUST_FOUND");
                if (string.Equals(found, "True", StringComparison.OrdinalIgnoreCase))
                {
                    var trust = TryParse(result.StdOut);
                    if (trust is not null)
                    {
                        lastSnapshot = trust;
                        if (string.Equals(trust.Target.TrimEnd('.'), expectedTrustedDomainDns.TrimEnd('.'), StringComparison.OrdinalIgnoreCase) &&
                            trust.IsForestTrust &&
                            trust.IsBidirectional)
                        {
                            return trust;
                        }

                        lastError = $"guest answered with Target='{trust.Target}', TrustType='{trust.TrustType}', " +
                                    $"Direction='{trust.Direction}' but the expected forest+bidirectional trust to " +
                                    $"'{expectedTrustedDomainDns}' is not present yet.";
                    }
                    else
                    {
                        lastError = "PowerShell Direct succeeded and reported a trust but its fields could not be parsed.";
                    }
                }
                else
                {
                    lastError = $"no trust to '{expectedTrustedDomainDns}' is present on the DC yet.";
                }
            }
            else
            {
                lastError = result.StdErr.Trim();
            }

            Thread.Sleep(15000);
        }

        Console.WriteLine(
            $"GuestTrustProbe: forest trust to '{expectedTrustedDomainDns}' never appeared on '{vmName}'. Last state: {lastError}");
        // Non-null when the guest reported some trust at least once (so the caller can report the actual
        // type/direction); null when it never reported a trust or never answered.
        return lastSnapshot;
    }

    private static string BuildScript(string vmName, string netBiosName, string trustedDomainDns)
    {
        string vm = vmName.Replace("'", "''");
        string netbios = netBiosName.Replace("'", "''");
        string trusted = trustedDomainDns.Replace("'", "''");
        // The password is pulled from the environment INSIDE this child process; it is never
        // interpolated into the script text. Build the SecureString with core .NET types only
        // (ConvertTo-SecureString lives in Microsoft.PowerShell.Security, which does not reliably
        // autoload in a spawned -NoProfile host).
        return $@"
$p = $env:{GuestDirectoryProbe.PasswordEnvVar}
if ([string]::IsNullOrEmpty($p)) {{ Write-Error 'admin password env var is not set'; exit 3 }}
$sec = New-Object System.Security.SecureString
foreach ($ch in $p.ToCharArray()) {{ $sec.AppendChar($ch) }}
$sec.MakeReadOnly()
$cred = New-Object System.Management.Automation.PSCredential('{netbios}\Administrator', $sec)
$out = Invoke-Command -VMName '{vm}' -Credential $cred -ScriptBlock {{
    Import-Module ActiveDirectory -ErrorAction Stop
    $t = Get-ADTrust -Identity '{trusted}' -ErrorAction SilentlyContinue
    if ($null -eq $t) {{
        'TRUST_FOUND=False'
    }} else {{
        'TRUST_FOUND=True'
        'TRUST_TARGET=' + $t.Target
        'TRUST_TYPE=' + $t.TrustType
        'TRUST_DIRECTION=' + $t.Direction
        'TRUST_FOREST_TRANSITIVE=' + $t.ForestTransitive
    }}
}} -ErrorAction Stop
$out | ForEach-Object {{ $_ }}
";
    }

    /// <summary>
    /// Parses the TRUST_* lines a successful probe prints into a <see cref="GuestTrustInfo"/>. Returns
    /// null when the required target/type/direction fields are absent (an unparseable or "not found"
    /// answer). Internal so the parse can be unit-tested against synthetic guest output without a VM.
    /// </summary>
    internal static GuestTrustInfo? TryParse(string stdout)
    {
        var target = Extract(stdout, "TRUST_TARGET");
        var type = Extract(stdout, "TRUST_TYPE");
        var direction = Extract(stdout, "TRUST_DIRECTION");
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(direction))
        {
            return null;
        }

        bool forestTransitive = string.Equals(Extract(stdout, "TRUST_FOREST_TRANSITIVE"), "True", StringComparison.OrdinalIgnoreCase);
        return new GuestTrustInfo(target!, type!, direction!, forestTransitive);
    }

    private static string? Extract(string stdout, string key)
    {
        var match = Regex.Match(stdout, $@"^{Regex.Escape(key)}=(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}

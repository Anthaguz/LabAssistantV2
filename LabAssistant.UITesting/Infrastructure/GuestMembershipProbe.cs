using System.Text.RegularExpressions;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>What a running VM reports about its domain membership over PowerShell Direct.</summary>
public sealed record GuestDomainMembership(bool PartOfDomain, string Domain);

/// <summary>
/// Host-side guest validator that authenticates into a running VM over PowerShell Direct (VMBus)
/// and reads the real domain-membership state the guest actually holds - the prize of a
/// domain-join deploy, which the harness never infers from the UI's success text. A member that
/// silently failed to join (DNS could not find the DC, wrong credentials, join timed out) shows up
/// here as PartOfDomain=false or the wrong Domain, even when the deploy reported success.
///
/// A domain member keeps its LOCAL Administrator account after joining, so we authenticate as
/// ".\Administrator" - a bare local login (unlike a promoted DC, which no longer resolves a bare
/// local account and needs NETBIOS\Administrator; see <see cref="GuestDirectoryProbe"/>). The
/// password is read from the LABASSISTANT_SMOKE_ADMIN_PASSWORD environment variable INSIDE the
/// spawned PowerShell child, so the secret never appears in a script string, a process argument, or
/// a harness log. Reuses the same PowerShell Direct invocation pattern proven by
/// <see cref="GuestNetworkProbe"/>.
/// </summary>
public sealed class GuestMembershipProbe
{
    /// <summary>
    /// Polls the guest over PowerShell Direct until it reports membership in the expected domain (a
    /// member still applies its network config, joins, and reboots after the deploy reports done) or
    /// the timeout elapses, then returns the guest's reported PartOfDomain flag and domain name.
    ///
    /// The readiness gate is <paramref name="expectedDomainDnsName"/>: a bare successful connection
    /// is NOT enough, because Win32_ComputerSystem answers the moment PowerShell Direct is reachable
    /// and reports the pre-join WORKGROUP - well before the join has completed. Returning on the
    /// first read would therefore snapshot the guest before it joined and misreport a
    /// still-in-progress deploy. So this keeps polling until the guest reports PartOfDomain=true AND
    /// the expected domain, and only then returns.
    ///
    /// Returns null when the guest never answered at all (or the poll was aborted). When the guest
    /// answered but never reported the expected domain within the timeout, it returns the LAST
    /// snapshot it read, so the caller can report exactly which membership the guest ended up with -
    /// which is itself the failure signal for a join that never completed or landed on a wrong domain.
    ///
    /// <paramref name="abortIf"/> is checked before every attempt: when it returns true the poll
    /// stops immediately and returns null. The member scenario passes a "did the app roll the VM
    /// back" probe here so a failed deploy fails fast instead of polling a deleted VM for the timeout.
    /// </summary>
    public GuestDomainMembership? QueryDomainMembership(
        string vmName,
        string expectedDomainDnsName,
        TimeSpan timeout,
        Func<bool>? abortIf = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastError = "(none)";
        GuestDomainMembership? lastSnapshot = null;

        while (DateTime.UtcNow < deadline)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine($"GuestMembershipProbe: aborting domain poll on '{vmName}' - the deploy target is gone (rolled back).");
                return null;
            }

            var result = PowerShellRunner.Run(BuildScript(vmName), TimeSpan.FromSeconds(90));
            if (result.Success)
            {
                var partOf = Extract(result.StdOut, "PART_OF_DOMAIN");
                var domain = Extract(result.StdOut, "DOMAIN");
                if (partOf is not null && domain is not null)
                {
                    bool joined = string.Equals(partOf, "True", StringComparison.OrdinalIgnoreCase);
                    lastSnapshot = new GuestDomainMembership(joined, domain);
                    if (joined && string.Equals(domain, expectedDomainDnsName, StringComparison.OrdinalIgnoreCase))
                    {
                        return lastSnapshot;
                    }

                    lastError = $"guest answered with PartOfDomain={partOf}, Domain='{domain}' but the expected domain " +
                                $"'{expectedDomainDnsName}' has not been joined yet.";
                }
                else
                {
                    lastError = "PowerShell Direct succeeded but Win32_ComputerSystem returned no membership data.";
                }
            }
            else
            {
                lastError = result.StdErr.Trim();
            }

            Thread.Sleep(15000);
        }

        Console.WriteLine(
            $"GuestMembershipProbe: '{expectedDomainDnsName}' membership never appeared on '{vmName}'. Last state: {lastError}");
        // Non-null when the guest answered at least once (so the caller can report the actual
        // membership); null when it never answered.
        return lastSnapshot;
    }

    private static string BuildScript(string vmName)
    {
        string vm = vmName.Replace("'", "''");
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
$cred = New-Object System.Management.Automation.PSCredential('.\Administrator', $sec)
$out = Invoke-Command -VMName '{vm}' -Credential $cred -ScriptBlock {{
    $cs = Get-CimInstance -ClassName Win32_ComputerSystem
    'PART_OF_DOMAIN=' + $cs.PartOfDomain
    'DOMAIN=' + $cs.Domain
}} -ErrorAction Stop
$out | ForEach-Object {{ $_ }}
";
    }

    private static string? Extract(string stdout, string key)
    {
        var match = Regex.Match(stdout, $@"^{Regex.Escape(key)}=(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}

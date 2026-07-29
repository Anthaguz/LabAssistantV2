using System.Text.RegularExpressions;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// What a domain controller reports about the route it uses to reach a peer DC on ANOTHER subnet:
/// the next hop the guest's own routing table selects for that destination, and whether the peer is
/// actually reachable. For a routed cross-forest topology this is the evidence that the trust traffic
/// crossed the ROUTER rather than taking a same-subnet L2 shortcut: an off-link peer must be reached
/// via the DC's default gateway, which is the router's LAN leg on this DC's subnet.
/// </summary>
public sealed record GuestRouteInfo(string NextHop, bool Reachable);

/// <summary>
/// Host-side guest validator that authenticates into a promoted domain controller over PowerShell
/// Direct (VMBus) and reads, from the guest itself, the next hop it uses to reach a peer DC on a
/// different subnet plus whether that peer answers. This is the routed-boundary corroboration for the
/// cross-forest capstone: it proves the two forests reach each other THROUGH the router (next hop =
/// the router's LAN IP on this DC's subnet), not over a shared L2 segment. A same-subnet shortcut - or
/// a topology that silently collapsed both DCs onto one switch - shows up here as a next hop that is
/// not the router (e.g. 0.0.0.0/on-link) or an unreachable peer, even when the trust itself validated.
///
/// Like <see cref="GuestTrustProbe"/>, it authenticates as NETBIOS\Administrator (after promotion a DC
/// has no local SAM account) and reads the password from the LABASSISTANT_SMOKE_ADMIN_PASSWORD
/// environment variable INSIDE the spawned PowerShell child, so the secret never appears in a script
/// string, a process argument, or a harness log. Reuses the PowerShell Direct pattern the other guest
/// probes are proven against.
/// </summary>
public sealed class GuestRouteProbe
{
    /// <summary>
    /// Polls the DC over PowerShell Direct until it selects an off-link next hop to
    /// <paramref name="peerIpAddress"/> AND reports the peer reachable (routing can still be settling
    /// right after the deploy reports done), then returns that next hop + reachability as the guest sees
    /// them. Returns null when the guest never answered (or the poll was aborted); when it answered but
    /// never reached the "reachable via an off-link next hop" state within the timeout it returns the
    /// LAST snapshot it read, so the caller can report exactly what the guest ended up with.
    ///
    /// <paramref name="abortIf"/> is checked before every attempt: when it returns true the poll stops
    /// immediately and returns null (the scenario passes a "did the app roll the VM back" probe so a
    /// failed deploy fails fast instead of polling a deleted VM for the full timeout).
    /// </summary>
    public GuestRouteInfo? QueryRoute(
        string vmName,
        string netBiosName,
        string peerIpAddress,
        TimeSpan timeout,
        Func<bool>? abortIf = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastError = "(none)";
        GuestRouteInfo? lastSnapshot = null;

        while (DateTime.UtcNow < deadline)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine($"GuestRouteProbe: aborting route poll on '{vmName}' - the deploy target is gone (rolled back).");
                return null;
            }

            var result = PowerShellRunner.Run(BuildScript(vmName, netBiosName, peerIpAddress), TimeSpan.FromSeconds(90));
            if (result.Success)
            {
                var info = TryParse(result.StdOut);
                if (info is not null)
                {
                    lastSnapshot = info;
                    if (info.Reachable)
                    {
                        return info;
                    }

                    lastError = $"guest selected next hop '{info.NextHop}' to '{peerIpAddress}' but the peer is not reachable yet.";
                }
                else
                {
                    lastError = "PowerShell Direct succeeded but no off-link next hop to the peer was reported yet.";
                }
            }
            else
            {
                lastError = result.StdErr.Trim();
            }

            Thread.Sleep(10000);
        }

        Console.WriteLine(
            $"GuestRouteProbe: a reachable off-link route to '{peerIpAddress}' never appeared on '{vmName}'. Last state: {lastError}");
        return lastSnapshot;
    }

    private static string BuildScript(string vmName, string netBiosName, string peerIpAddress)
    {
        string vm = vmName.Replace("'", "''");
        string netbios = netBiosName.Replace("'", "''");
        string peer = peerIpAddress.Replace("'", "''");
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
    $peer = '{peer}'
    $route = Find-NetRoute -RemoteIPAddress $peer -ErrorAction Stop |
        Where-Object {{ $_.NextHop -and $_.NextHop -ne '0.0.0.0' -and $_.NextHop -ne '::' }} |
        Select-Object -First 1
    if ($null -eq $route) {{
        'ROUTE_FOUND=False'
    }} else {{
        'ROUTE_FOUND=True'
        'ROUTE_NEXTHOP=' + $route.NextHop
    }}
    $reach = Test-NetConnection -ComputerName $peer -InformationLevel Quiet -WarningAction SilentlyContinue
    'ROUTE_REACHABLE=' + $reach
}} -ErrorAction Stop
$out | ForEach-Object {{ $_ }}
";
    }

    /// <summary>
    /// Parses the ROUTE_* lines a successful probe prints into a <see cref="GuestRouteInfo"/>. Returns
    /// null when no off-link next hop was reported (ROUTE_FOUND=False or the next hop line is absent),
    /// which is itself the "no cross-router path" signal. Internal so the parse can be unit-tested
    /// against synthetic guest output without a VM.
    /// </summary>
    internal static GuestRouteInfo? TryParse(string stdout)
    {
        var found = Extract(stdout, "ROUTE_FOUND");
        if (!string.Equals(found, "True", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var nextHop = Extract(stdout, "ROUTE_NEXTHOP");
        if (string.IsNullOrWhiteSpace(nextHop))
        {
            return null;
        }

        bool reachable = string.Equals(Extract(stdout, "ROUTE_REACHABLE"), "True", StringComparison.OrdinalIgnoreCase);
        return new GuestRouteInfo(nextHop!, reachable);
    }

    private static string? Extract(string stdout, string key)
    {
        var match = Regex.Match(stdout, $@"^{Regex.Escape(key)}=(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}

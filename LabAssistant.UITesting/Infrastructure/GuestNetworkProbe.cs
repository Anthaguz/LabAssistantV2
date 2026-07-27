using System.Text.RegularExpressions;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Host-side guest validator that authenticates into a running VM over PowerShell Direct (VMBus)
/// and reads the real IPv4 addresses the guest actually holds - the prize of a router / multi-NIC
/// deploy, which the harness never infers from the UI's success text. A static IP landing on the
/// wrong adapter (the multi-NIC MAC-mapping bug) shows up here as the expected address being absent.
///
/// A router keeps its LOCAL Administrator account (it never joins a domain), so we authenticate as
/// ".\Administrator" - a bare local login. The password is read from the
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD environment variable INSIDE the spawned PowerShell child, so the
/// secret never appears in a script string, a process argument, or a harness log. Reuses the same
/// PowerShell Direct invocation pattern proven by <see cref="GuestDirectoryProbe"/>.
/// </summary>
public sealed class GuestNetworkProbe
{
    /// <summary>
    /// Polls the guest over PowerShell Direct until it reports the expected IPv4 address (a router
    /// still applies its network config and can reboot right after the deploy reports done) or the
    /// timeout elapses, then returns every IPv4 address the guest reports across all adapters.
    ///
    /// The readiness gate is <paramref name="expectedAddress"/>: a bare successful connection is NOT
    /// enough, because Get-NetIPAddress answers the moment PowerShell Direct is reachable and always
    /// includes loopback (127.0.0.1) plus any DHCP address on the egress NIC - well before the app has
    /// applied the static LAN IP. Returning on the first non-empty read would therefore snapshot the
    /// guest before configuration finished and misreport a correctly-working deploy. So this keeps
    /// polling until the expected address appears, and only then returns.
    ///
    /// Returns null when the guest never answered at all (or the poll was aborted). When the guest
    /// answered but the expected address never appeared within the timeout, it returns the LAST
    /// snapshot it read, so the caller can report exactly which addresses the guest ended up with -
    /// which is itself the failure signal for a static IP that landed on the wrong adapter.
    ///
    /// <paramref name="abortIf"/> is checked before every attempt: when it returns true the poll
    /// stops immediately and returns null. The router scenario passes a "did the app roll the VM
    /// back" probe here so a failed deploy fails fast instead of polling a deleted VM for the timeout.
    /// </summary>
    public IReadOnlyList<string>? QueryIpv4Addresses(
        string vmName,
        string expectedAddress,
        TimeSpan timeout,
        Func<bool>? abortIf = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastError = "(none)";
        IReadOnlyList<string>? lastSnapshot = null;

        while (DateTime.UtcNow < deadline)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine($"GuestNetworkProbe: aborting IP poll on '{vmName}' - the deploy target is gone (rolled back).");
                return null;
            }

            var result = PowerShellRunner.Run(BuildScript(vmName), TimeSpan.FromSeconds(90));
            if (result.Success)
            {
                var addresses = Regex.Matches(result.StdOut, @"^IPV4=(.+)$", RegexOptions.Multiline)
                    .Select(m => m.Groups[1].Value.Trim())
                    .Where(a => a.Length > 0)
                    .ToList();
                if (addresses.Count > 0)
                {
                    lastSnapshot = addresses;
                    if (addresses.Any(a => string.Equals(a, expectedAddress, StringComparison.OrdinalIgnoreCase)))
                    {
                        return addresses;
                    }

                    lastError = $"guest answered with [{string.Join(", ", addresses)}] but the expected address " +
                                $"'{expectedAddress}' is not present yet.";
                }
                else
                {
                    lastError = "PowerShell Direct succeeded but Get-NetIPAddress returned no IPv4 data.";
                }
            }
            else
            {
                lastError = result.StdErr.Trim();
            }

            Thread.Sleep(15000);
        }

        Console.WriteLine(
            $"GuestNetworkProbe: '{expectedAddress}' never appeared on '{vmName}'. Last state: {lastError}");
        // Non-null when the guest answered at least once (so the caller can report the actual
        // addresses); null when it never answered.
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
    Get-NetIPAddress -AddressFamily IPv4 | ForEach-Object {{ 'IPV4=' + $_.IPAddress }}
}} -ErrorAction Stop
$out | ForEach-Object {{ $_ }}
";
    }
}

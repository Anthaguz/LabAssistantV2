using System.Text.RegularExpressions;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>What a promoted domain controller reports about its forest/domain over PowerShell Direct.</summary>
public sealed record GuestForestInfo(string ForestRootDomain, string DomainDnsName, string DomainNetBios);

/// <summary>
/// Host-side guest validator that authenticates into a promoted domain controller over
/// PowerShell Direct (VMBus) and reads the real Active Directory state - the actual prize
/// of a DC deploy scenario, which the harness never infers from the UI's success text.
///
/// The bootstrap credential is the local Administrator baked into the base image; after
/// promotion that account becomes the domain administrator, so we authenticate as
/// NETBIOS\Administrator (a bare local login no longer resolves on a promoted DC - see the
/// post-promotion credential-identity fix in the runtime). The password is read from the
/// LABASSISTANT_SMOKE_ADMIN_PASSWORD environment variable INSIDE the spawned PowerShell, so
/// the secret never appears in a script string, a process argument, or a harness log.
/// </summary>
public sealed class GuestDirectoryProbe
{
    public const string PasswordEnvVar = "LABASSISTANT_SMOKE_ADMIN_PASSWORD";

    /// <summary>True when the admin-password env var is set, i.e. a live guest deploy + validation can run.</summary>
    public static bool HasAdminPassword =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PasswordEnvVar));

    /// <summary>
    /// Polls the DC over PowerShell Direct until Active Directory answers (promotion can still
    /// be settling right after the deploy reports done) or the timeout elapses, then returns the
    /// forest root domain, domain DNS name, and NetBIOS name. Returns null if AD never answered.
    /// </summary>
    public GuestForestInfo? QueryForest(string vmName, string netBiosName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastError = "(none)";

        while (DateTime.UtcNow < deadline)
        {
            var result = PowerShellRunner.Run(BuildScript(vmName, netBiosName), TimeSpan.FromSeconds(90));
            if (result.Success)
            {
                var forest = Extract(result.StdOut, "FOREST_ROOT");
                var dns = Extract(result.StdOut, "DOMAIN_DNS");
                var netbios = Extract(result.StdOut, "DOMAIN_NETBIOS");
                if (!string.IsNullOrWhiteSpace(forest) && !string.IsNullOrWhiteSpace(dns))
                {
                    return new GuestForestInfo(forest!, dns!, netbios ?? string.Empty);
                }

                lastError = "PowerShell Direct succeeded but Get-ADForest/Get-ADDomain returned no data.";
            }
            else
            {
                lastError = result.StdErr.Trim();
            }

            Thread.Sleep(15000);
        }

        Console.WriteLine($"GuestDirectoryProbe: AD never answered on '{vmName}'. Last error: {lastError}");
        return null;
    }

    private static string BuildScript(string vmName, string netBiosName)
    {
        string vm = vmName.Replace("'", "''");
        string netbios = netBiosName.Replace("'", "''");
        // The password is pulled from the environment INSIDE this child process; it is never
        // interpolated into the script text.
        return $@"
$p = $env:{PasswordEnvVar}
if ([string]::IsNullOrEmpty($p)) {{ Write-Error 'admin password env var is not set'; exit 3 }}
$sec = ConvertTo-SecureString $p -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential('{netbios}\Administrator', $sec)
$out = Invoke-Command -VMName '{vm}' -Credential $cred -ScriptBlock {{
    Import-Module ActiveDirectory -ErrorAction Stop
    $f = Get-ADForest
    $d = Get-ADDomain
    'FOREST_ROOT=' + $f.RootDomain
    'DOMAIN_DNS=' + $d.DNSRoot
    'DOMAIN_NETBIOS=' + $d.NetBIOSName
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

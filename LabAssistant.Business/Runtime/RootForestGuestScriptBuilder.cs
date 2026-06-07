using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime;

internal static class RootForestGuestScriptBuilder
{
    public static string BuildInstallAdDomainServicesFeatureScript()
    {
        return """
Import-Module ServerManager -ErrorAction Stop
$feature = Get-WindowsFeature -Name 'AD-Domain-Services' -ErrorAction Stop
if ($feature.InstallState -ne 'Installed') {
    Install-WindowsFeature -Name 'AD-Domain-Services' -IncludeManagementTools -ErrorAction Stop | Out-Null
}
Write-Output 'AD-Domain-Services ready'
""";
    }

    public static string BuildPromoteRootForestScript(V2ResolvedDomainPlanningContext domain, string dsrmPassword)
    {
        return string.Join(
            Environment.NewLine,
            "Import-Module ADDSDeployment -ErrorAction Stop",
            $"$secureDsrmPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(dsrmPassword)}' -AsPlainText -Force",
            "Install-ADDSForest `",
            "    -CreateDnsDelegation:$false `",
            "    -DatabasePath 'C:\\Windows\\NTDS' `",
            "    -DomainMode 'WinThreshold' `",
            $"    -DomainName '{EscapeSingleQuotedLiteral(domain.DnsName)}' `",
            $"    -DomainNetbiosName '{EscapeSingleQuotedLiteral(domain.NetBiosName)}' `",
            "    -ForestMode 'WinThreshold' `",
            "    -InstallDns:$true `",
            "    -LogPath 'C:\\Windows\\NTDS' `",
            "    -NoRebootOnCompletion:$false `",
            "    -SysvolPath 'C:\\Windows\\SYSVOL' `",
            "    -SafeModeAdministratorPassword $secureDsrmPassword `",
            "    -Force:$true");
    }

    public static string BuildVerifyDomainControllerScript(string expectedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            "$computerSystem = Get-CimInstance Win32_ComputerSystem",
            "if ($computerSystem.DomainRole -notin 4, 5) {",
            "    throw 'Machine is not yet a domain controller.'",
            "}",
            "Import-Module ActiveDirectory -ErrorAction Stop",
            "$domain = Get-ADDomain -ErrorAction Stop",
            $"if ($domain.DNSRoot -ine '{EscapeSingleQuotedLiteral(expectedDomainName)}') {{",
            "    throw \"Expected domain does not match local domain root.\"",
            "}",
            "Write-Output $domain.DNSRoot");
    }

    public static string BuildDomainReadyProbeScript(string expectedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            $"Resolve-DnsName '{EscapeSingleQuotedLiteral(expectedDomainName)}' -ErrorAction Stop | Out-Null",
            $"Resolve-DnsName '_ldap._tcp.dc._msdcs.{EscapeSingleQuotedLiteral(expectedDomainName)}' -Type SRV -ErrorAction Stop | Out-Null",
            "Import-Module ActiveDirectory -ErrorAction Stop",
            "$domain = Get-ADDomain -ErrorAction Stop",
            $"if ($domain.DNSRoot -ine '{EscapeSingleQuotedLiteral(expectedDomainName)}') {{",
            "    throw \"Expected domain does not match local domain root.\"",
            "}",
            "Write-Output $domain.DNSRoot");
    }

    private static string EscapeSingleQuotedLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

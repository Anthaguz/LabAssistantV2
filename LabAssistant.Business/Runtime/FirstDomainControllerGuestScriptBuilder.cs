using LabAssistant.Models.Templates;
using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Runtime;

internal static class FirstDomainControllerGuestScriptBuilder
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

    public static string BuildPromoteFirstDomainControllerScript(
        V2ResolvedDomainPlanningContext domain,
        string dsrmPassword,
        V2RuntimeCredential? parentDomainAdminCredential = null,
        V2ResolvedDomainPlanningContext? parentDomain = null)
    {
        return domain.RelationKind switch
        {
            V2DomainRelationKind.Child => BuildPromoteChildDomainScript(domain, dsrmPassword, parentDomainAdminCredential, parentDomain),
            V2DomainRelationKind.Tree => BuildPromoteTreeDomainScript(domain, dsrmPassword, parentDomainAdminCredential, parentDomain),
            _ => BuildPromoteRootForestScript(domain, dsrmPassword)
        };
    }

    public static string BuildWaitForParentDomainDnsScript(string parentDomainName)
    {
        return string.Join(
            Environment.NewLine,
            $"Resolve-DnsName '{EscapeSingleQuotedLiteral(parentDomainName)}' -ErrorAction Stop | Out-Null",
            $"Resolve-DnsName '_ldap._tcp.dc._msdcs.{EscapeSingleQuotedLiteral(parentDomainName)}' -Type SRV -ErrorAction Stop | Out-Null",
            $"Write-Output '{EscapeSingleQuotedLiteral(parentDomainName)}'");
    }

    private static string BuildPromoteRootForestScript(V2ResolvedDomainPlanningContext domain, string dsrmPassword)
    {
        return string.Join(
            Environment.NewLine,
            BuildAlreadyDomainControllerGuard(domain.DnsName),
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

    private static string BuildPromoteChildDomainScript(
        V2ResolvedDomainPlanningContext domain,
        string dsrmPassword,
        V2RuntimeCredential? parentDomainAdminCredential,
        V2ResolvedDomainPlanningContext? parentDomain)
    {
        ArgumentNullException.ThrowIfNull(parentDomainAdminCredential);
        ArgumentNullException.ThrowIfNull(parentDomain);

        return string.Join(
            Environment.NewLine,
            BuildAlreadyDomainControllerGuard(domain.DnsName),
            "Import-Module ADDSDeployment -ErrorAction Stop",
            $"$parentDomainPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(parentDomainAdminCredential.Password)}' -AsPlainText -Force",
            $"$parentDomainCredential = New-Object System.Management.Automation.PSCredential ('{EscapeSingleQuotedLiteral(parentDomainAdminCredential.Username)}', $parentDomainPassword)",
            $"$secureDsrmPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(dsrmPassword)}' -AsPlainText -Force",
            "Install-ADDSDomain `",
            "    -CreateDnsDelegation:$true `",
            "    -Credential $parentDomainCredential `",
            "    -DatabasePath 'C:\\Windows\\NTDS' `",
            "    -DomainMode 'WinThreshold' `",
            "    -DomainType ChildDomain `",
            "    -InstallDns:$true `",
            "    -LogPath 'C:\\Windows\\NTDS' `",
            $"    -NewDomainName '{EscapeSingleQuotedLiteral(GetChildLabel(domain.DnsName, parentDomain.DnsName))}' `",
            $"    -NewDomainNetbiosName '{EscapeSingleQuotedLiteral(domain.NetBiosName)}' `",
            "    -NoRebootOnCompletion:$false `",
            $"    -ParentDomainName '{EscapeSingleQuotedLiteral(parentDomain.DnsName)}' `",
            "    -SysvolPath 'C:\\Windows\\SYSVOL' `",
            "    -SafeModeAdministratorPassword $secureDsrmPassword `",
            "    -Force:$true");
    }

    private static string BuildPromoteTreeDomainScript(
        V2ResolvedDomainPlanningContext domain,
        string dsrmPassword,
        V2RuntimeCredential? parentDomainAdminCredential,
        V2ResolvedDomainPlanningContext? parentDomain)
    {
        ArgumentNullException.ThrowIfNull(parentDomainAdminCredential);
        ArgumentNullException.ThrowIfNull(parentDomain);

        return string.Join(
            Environment.NewLine,
            BuildAlreadyDomainControllerGuard(domain.DnsName),
            "Import-Module ADDSDeployment -ErrorAction Stop",
            $"$parentDomainPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(parentDomainAdminCredential.Password)}' -AsPlainText -Force",
            $"$parentDomainCredential = New-Object System.Management.Automation.PSCredential ('{EscapeSingleQuotedLiteral(parentDomainAdminCredential.Username)}', $parentDomainPassword)",
            $"$secureDsrmPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(dsrmPassword)}' -AsPlainText -Force",
            "Install-ADDSDomain `",
            "    -CreateDnsDelegation:$false `",
            "    -Credential $parentDomainCredential `",
            "    -DatabasePath 'C:\\Windows\\NTDS' `",
            "    -DomainMode 'WinThreshold' `",
            "    -DomainType TreeDomain `",
            "    -InstallDns:$true `",
            "    -LogPath 'C:\\Windows\\NTDS' `",
            $"    -NewDomainName '{EscapeSingleQuotedLiteral(domain.DnsName)}' `",
            $"    -NewDomainNetbiosName '{EscapeSingleQuotedLiteral(domain.NetBiosName)}' `",
            "    -NoRebootOnCompletion:$false `",
            $"    -ParentDomainName '{EscapeSingleQuotedLiteral(parentDomain.DnsName)}' `",
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

    // Idempotency guard: if the machine is already a domain controller (DomainRole 4 or 5)
    // for the intended domain, short-circuit before re-running promotion, which would otherwise fail.
    private static string BuildAlreadyDomainControllerGuard(string expectedDomainName) =>
        string.Join(
            Environment.NewLine,
            "$existing = Get-CimInstance Win32_ComputerSystem",
            $"if ($existing.DomainRole -in 4, 5 -and $existing.Domain -ieq '{EscapeSingleQuotedLiteral(expectedDomainName)}') {{",
            "    Write-Output $existing.Domain",
            "    return",
            "}");

    private static string GetChildLabel(string childDomainName, string parentDomainName)
    {
        if (childDomainName.EndsWith("." + parentDomainName, StringComparison.OrdinalIgnoreCase))
        {
            return childDomainName[..^(parentDomainName.Length + 1)];
        }

        return childDomainName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries)[0];
    }
}

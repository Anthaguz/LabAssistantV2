using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime;

internal static class DomainProgressionGuestScriptBuilder
{
    public static string BuildPromoteReplicaDomainControllerScript(
        V2ResolvedDomainPlanningContext domain,
        V2RuntimeCredential domainJoinCredential,
        string dsrmPassword)
    {
        return string.Join(
            Environment.NewLine,
            BuildAlreadyDomainControllerGuard(domain.DnsName),
            "Import-Module ADDSDeployment -ErrorAction Stop",
            $"$domainPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(domainJoinCredential.Password)}' -AsPlainText -Force",
            $"$domainCredential = New-Object System.Management.Automation.PSCredential ('{EscapeSingleQuotedLiteral(domainJoinCredential.Username)}', $domainPassword)",
            $"$secureDsrmPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(dsrmPassword)}' -AsPlainText -Force",
            "Install-ADDSDomainController `",
            $"    -Credential $domainCredential `",
            $"    -DomainName '{EscapeSingleQuotedLiteral(domain.DnsName)}' `",
            "    -InstallDns:$true `",
            "    -NoGlobalCatalog:$false `",
            "    -NoRebootOnCompletion:$false `",
            "    -DatabasePath 'C:\\Windows\\NTDS' `",
            "    -LogPath 'C:\\Windows\\NTDS' `",
            "    -SysvolPath 'C:\\Windows\\SYSVOL' `",
            "    -SafeModeAdministratorPassword $secureDsrmPassword `",
            "    -Force:$true");
    }

    public static string BuildVerifyJoinedDomainScript(string expectedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            "$computerSystem = Get-CimInstance Win32_ComputerSystem",
            "if (-not $computerSystem.PartOfDomain) { throw 'Machine is not yet joined to a domain.' }",
            $"if ($computerSystem.Domain -ine '{EscapeSingleQuotedLiteral(expectedDomainName)}') {{",
            "    throw 'Machine is joined to an unexpected domain.'",
            "}",
            "Write-Output $computerSystem.Domain");
    }

    public static string BuildJoinDomainScript(
        string expectedDomainName,
        V2RuntimeCredential joinCredential)
    {
        return string.Join(
            Environment.NewLine,
            "$computerSystem = Get-CimInstance Win32_ComputerSystem",
            "if ($computerSystem.PartOfDomain -and $computerSystem.Domain -ieq " + $"'{EscapeSingleQuotedLiteral(expectedDomainName)}'" + ") {",
            "    Write-Output $computerSystem.Domain",
            "    return",
            "}",
            $"$joinPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(joinCredential.Password)}' -AsPlainText -Force",
            $"$joinCredential = New-Object System.Management.Automation.PSCredential ('{EscapeSingleQuotedLiteral(joinCredential.Username)}', $joinPassword)",
            $"Add-Computer -DomainName '{EscapeSingleQuotedLiteral(expectedDomainName)}' -Credential $joinCredential -Force -Restart");
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
}

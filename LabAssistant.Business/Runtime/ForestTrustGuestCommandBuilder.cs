using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime;

internal static class ForestTrustGuestCommandBuilder
{
    public static string BuildPrepareDnsForwarderCommand(string targetDomainName, IReadOnlyList<string> targetDnsServers)
    {
        var escapedServers = targetDnsServers
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Select(server => $"'{EscapeSingleQuotedLiteral(server)}'")
            .ToArray();

        return string.Join(
            Environment.NewLine,
            "Import-Module DnsServer -ErrorAction Stop",
            $"$targetDomain = '{EscapeSingleQuotedLiteral(targetDomainName)}'",
            "$masterServers = @(" + string.Join(", ", escapedServers) + ")",
            "if ($masterServers.Count -eq 0) { throw 'No DNS master servers were provided for the trust forwarder.' }",
            "$existing = Get-DnsServerConditionalForwarderZone -Name $targetDomain -ErrorAction SilentlyContinue",
            "if ($null -eq $existing) {",
            "    Add-DnsServerConditionalForwarderZone -Name $targetDomain -MasterServers $masterServers -ReplicationScope Forest -ErrorAction Stop | Out-Null",
            "} else {",
            "    Set-DnsServerConditionalForwarderZone -Name $targetDomain -MasterServers $masterServers -ErrorAction Stop | Out-Null",
            "}",
            "Resolve-DnsName $targetDomain -ErrorAction Stop | Out-Null",
            "Write-Output \"DNS forwarder ready for $targetDomain\"");
    }

    public static string BuildCreateBidirectionalForestTrustCommand(
        V2ResolvedTrustPlanningContext trust,
        V2RuntimeCredential targetDomainAdminCredential)
    {
        return string.Join(
            Environment.NewLine,
            "Import-Module ActiveDirectory -ErrorAction Stop",
            $"$targetPassword = ConvertTo-SecureString '{EscapeSingleQuotedLiteral(targetDomainAdminCredential.Password)}' -AsPlainText -Force",
            $"$targetCredential = New-Object System.Management.Automation.PSCredential ('{EscapeSingleQuotedLiteral(targetDomainAdminCredential.Username)}', $targetPassword)",
            $"$sourceForest = Get-ADForest -Identity '{EscapeSingleQuotedLiteral(trust.SourceDomainDnsName)}' -ErrorAction Stop",
            $"$targetForest = Get-ADForest -Identity '{EscapeSingleQuotedLiteral(trust.TargetDomainDnsName)}' -Credential $targetCredential -ErrorAction Stop",
            "$existing = Get-ADTrust -Identity $targetForest.Name -ErrorAction SilentlyContinue",
            "if ($null -eq $existing) {",
            "    New-ADTrust -Name $targetForest.Name -SourceForest $sourceForest.Name -TargetForest $targetForest.Name -TrustType Forest -Direction Bidirectional -TargetCredential $targetCredential -ErrorAction Stop | Out-Null",
            "}",
            "Write-Output \"Forest trust ready for $($targetForest.Name)\"");
    }

    public static string BuildValidateForestTrustCommand(string trustedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            "Import-Module ActiveDirectory -ErrorAction Stop",
            $"$trustedDomain = '{EscapeSingleQuotedLiteral(trustedDomainName)}'",
            "$trust = Get-ADTrust -Identity $trustedDomain -ErrorAction Stop",
            "if ($trust.TrustType -ine 'Uplevel' -and $trust.TrustType -ine 'Forest') { throw 'Resolved trust is not a forest trust.' }",
            "if ($trust.Direction -ine 'Bidirectional') { throw 'Resolved trust is not bidirectional.' }",
            "Write-Output \"Forest trust validated for $trustedDomain\"");
    }

    public static string BuildCleanupForestTrustCommand(string trustedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            "Import-Module ActiveDirectory -ErrorAction Stop",
            $"$trustedDomain = '{EscapeSingleQuotedLiteral(trustedDomainName)}'",
            "$trust = Get-ADTrust -Identity $trustedDomain -ErrorAction SilentlyContinue",
            "if ($null -ne $trust) {",
            "    Remove-ADTrust -Identity $trustedDomain -Confirm:$false -ErrorAction Stop",
            "}",
            "Write-Output \"Forest trust cleanup complete for $trustedDomain\"");
    }

    private static string EscapeSingleQuotedLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

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
            "$existing = Get-DnsServerZone -Name $targetDomain -ErrorAction SilentlyContinue | Where-Object { $_.ZoneType -eq 'Forwarder' }",
            "if ($null -eq $existing) {",
            "    Add-DnsServerConditionalForwarderZone -Name $targetDomain -MasterServers $masterServers -ReplicationScope Forest -ErrorAction Stop | Out-Null",
            "} else {",
            "    Set-DnsServerConditionalForwarderZone -Name $targetDomain -MasterServers $masterServers -ErrorAction Stop | Out-Null",
            "}",
            // A conditional forwarder to a freshly-promoted target DC often SERVFAILs for the first several
            // seconds after promotion even though DNS is up, so prove the forwarder resolves end to end with a
            // bounded retry instead of a single-shot check before we let trust creation depend on it.
            "$resolved = $false",
            "for ($attempt = 0; $attempt -lt 30; $attempt++) {",
            "    try { Resolve-DnsName $targetDomain -ErrorAction Stop | Out-Null; $resolved = $true; break }",
            "    catch { Start-Sleep -Seconds 5 }",
            "}",
            "if (-not $resolved) { throw \"Conditional forwarder for $targetDomain did not resolve within timeout.\" }",
            "Write-Output \"DNS forwarder ready for $targetDomain\"");
    }

    // Creates the bidirectional forest trust via the version-agnostic
    // System.DirectoryServices.ActiveDirectory .NET API rather than the New-ADTrust cmdlet: New-ADTrust does not
    // exist in the ActiveDirectory PowerShell module on Windows Server 2022 (it was introduced in Server 2025),
    // so the cmdlet form fails on our base image. Forest.CreateTrustRelationship builds BOTH sides of the trust
    // in a single call from the source anchor, using a target-forest DirectoryContext authenticated with the
    // (already NetBIOS-qualified) target domain-admin credential.
    public static string BuildCreateBidirectionalForestTrustCommand(
        V2ResolvedTrustPlanningContext trust,
        V2RuntimeCredential targetDomainAdminCredential)
    {
        return string.Join(
            Environment.NewLine,
            "[void][System.Reflection.Assembly]::LoadWithPartialName('System.DirectoryServices.ActiveDirectory')",
            $"$sourceForestName = '{EscapeSingleQuotedLiteral(trust.SourceDomainDnsName)}'",
            $"$targetForestName = '{EscapeSingleQuotedLiteral(trust.TargetDomainDnsName)}'",
            $"$targetUser = '{EscapeSingleQuotedLiteral(targetDomainAdminCredential.Username)}'",
            $"$targetPassword = '{EscapeSingleQuotedLiteral(targetDomainAdminCredential.Password)}'",
            "$sourceContext = New-Object System.DirectoryServices.ActiveDirectory.DirectoryContext([System.DirectoryServices.ActiveDirectory.DirectoryContextType]::Forest, $sourceForestName)",
            "$sourceForest = [System.DirectoryServices.ActiveDirectory.Forest]::GetForest($sourceContext)",
            "$targetContext = New-Object System.DirectoryServices.ActiveDirectory.DirectoryContext([System.DirectoryServices.ActiveDirectory.DirectoryContextType]::Forest, $targetForestName, $targetUser, $targetPassword)",
            "$targetForest = [System.DirectoryServices.ActiveDirectory.Forest]::GetForest($targetContext)",
            "$existingTrust = $null",
            "try { $existingTrust = $sourceForest.GetTrustRelationship($targetForestName) } catch { $existingTrust = $null }",
            "if ($null -eq $existingTrust) {",
            "    $sourceForest.CreateTrustRelationship($targetForest, [System.DirectoryServices.ActiveDirectory.TrustDirection]::Bidirectional)",
            "}",
            "Write-Output \"Forest trust ready for $targetForestName\"");
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

    // Removes the local side of the forest trust via the version-agnostic
    // System.DirectoryServices.ActiveDirectory .NET API rather than Remove-ADTrust: like New-ADTrust, the
    // Remove-ADTrust cmdlet does not exist on Windows Server 2022. This runs on each anchor, so GetCurrentForest
    // returns that anchor's own forest and DeleteLocalSideOfTrustRelationship drops only its side of the trust;
    // the two anchors together remove both sides. DNS conditional forwarders are intentionally left in place.
    public static string BuildCleanupForestTrustCommand(string trustedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            "[void][System.Reflection.Assembly]::LoadWithPartialName('System.DirectoryServices.ActiveDirectory')",
            $"$trustedDomain = '{EscapeSingleQuotedLiteral(trustedDomainName)}'",
            "$localForest = [System.DirectoryServices.ActiveDirectory.Forest]::GetCurrentForest()",
            "$existingTrust = $null",
            "try { $existingTrust = $localForest.GetTrustRelationship($trustedDomain) } catch { $existingTrust = $null }",
            "if ($null -ne $existingTrust) {",
            "    $localForest.DeleteLocalSideOfTrustRelationship($trustedDomain)",
            "}",
            "Write-Output \"Forest trust cleanup complete for $trustedDomain\"");
    }

    private static string EscapeSingleQuotedLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

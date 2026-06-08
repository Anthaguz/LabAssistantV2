using System.Text;

namespace LabAssistant.Business.Runtime;

internal static class RouterGuestScriptBuilder
{
    public static string BuildPrepareRouterNetworkScript(IReadOnlyList<RouterNicPlan> nics)
    {
        var ordered = nics
            .OrderBy(nic => nic.SwitchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(nic => nic.MacAddress, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine("$targetNics = @(");
        for (var index = 0; index < ordered.Length; index++)
        {
            var nic = ordered[index];
            var dnsServers = nic.DnsServers.Count == 0
                ? "@()"
                : "@(" + string.Join(", ", nic.DnsServers.Select(value => $"'{EscapeSingleQuotedLiteral(value)}'")) + ")";
            sb.Append("    [pscustomobject]@{");
            sb.Append($" SwitchName = '{EscapeSingleQuotedLiteral(nic.SwitchName)}';");
            sb.Append($" MacAddress = '{EscapeSingleQuotedLiteral(nic.MacAddress)}';");
            sb.Append($" IsExternal = ${nic.IsExternal.ToString().ToLowerInvariant()};");
            sb.Append($" IpAddress = {ToPowerShellString(nic.IpAddress)};");
            sb.Append($" PrefixLength = {ToNullableInt(nic.PrefixLength)};");
            sb.Append($" DnsServers = {dnsServers}");
            sb.Append(" }");
            sb.AppendLine(index == ordered.Length - 1 ? string.Empty : ",");
        }

        sb.AppendLine(")");
        sb.AppendLine("foreach ($target in $targetNics) {");
        sb.AppendLine("    $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $target.MacAddress } | Select-Object -First 1");
        sb.AppendLine("    if (-not $adapter) { throw \"Router adapter with MAC $($target.MacAddress) for switch '$($target.SwitchName)' was not found.\" }");
        sb.AppendLine("    $interfaceIndex = $adapter.ifIndex");
        sb.AppendLine("    if ($target.IsExternal) {");
        sb.AppendLine("        Get-NetRoute -InterfaceIndex $interfaceIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Where-Object { $_.Protocol -ne 'Dhcp' } | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue");
        sb.AppendLine("        Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { ($_.IPAddress -notlike '169.254.*') -and ($_.PrefixOrigin -ne 'Dhcp') } | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue");
        sb.AppendLine("        Set-NetIPInterface -InterfaceIndex $interfaceIndex -Dhcp Enabled -AddressFamily IPv4 -ErrorAction SilentlyContinue");
        sb.AppendLine("        Set-DnsClientServerAddress -InterfaceIndex $interfaceIndex -ResetServerAddresses -ErrorAction SilentlyContinue");
        sb.AppendLine("    } else {");
        sb.AppendLine("        Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '169.254.*' } | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue");
        sb.AppendLine("        Get-NetRoute -InterfaceIndex $interfaceIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue");
        sb.AppendLine("        if ($target.IpAddress -and $target.PrefixLength) {");
        sb.AppendLine("            New-NetIPAddress -InterfaceIndex $interfaceIndex -IPAddress $target.IpAddress -PrefixLength ([int]$target.PrefixLength) -AddressFamily IPv4 -ErrorAction Stop | Out-Null");
        sb.AppendLine("        }");
        sb.AppendLine("        if ($target.DnsServers.Count -gt 0) {");
        sb.AppendLine("            Set-DnsClientServerAddress -InterfaceIndex $interfaceIndex -ServerAddresses $target.DnsServers -ErrorAction Stop");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("Write-Output 'Router network prepared'");
        return sb.ToString();
    }

    public static string BuildInstallRouterRemoteAccessFeatureScript()
    {
        return """
Import-Module ServerManager -ErrorAction Stop
$featureNames = @('RemoteAccess', 'Routing')
$features = Get-WindowsFeature -Name $featureNames -ErrorAction Stop
$missing = @($features | Where-Object { $_.InstallState -ne 'Installed' } | Select-Object -ExpandProperty Name)
if ($missing.Count -gt 0) {
    Install-WindowsFeature -Name $missing -IncludeManagementTools -ErrorAction Stop | Out-Null
}
Write-Output 'Router remote-access features ready'
""";
    }

    public static string BuildEnableRouterRoutingScript(string externalMacAddress)
    {
        return string.Join(
            Environment.NewLine,
            "Import-Module RemoteAccess -ErrorAction SilentlyContinue",
            "try {",
            "    Install-RemoteAccess -VpnType RoutingOnly -ErrorAction Stop",
            "} catch {",
            "    if ($_.Exception.Message -notmatch 'already|configured|installed') { throw }",
            "}",
            "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {",
            "    Set-NetIPInterface -InterfaceIndex $_.ifIndex -Forwarding Enabled -AddressFamily IPv4 -ErrorAction Stop",
            "}",
            $"$externalMac = '{EscapeSingleQuotedLiteral(externalMacAddress)}'",
            "$externalAdapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $externalMac } | Select-Object -First 1",
            "if (-not $externalAdapter) { throw 'External router adapter was not found in the guest OS.' }",
            "Write-Output $externalAdapter.Name");
    }

    public static string BuildConfigureRouterNatScript(string externalMacAddress, IReadOnlyList<string> internalMacAddresses)
    {
        var internalMacs = "@(" + string.Join(", ", internalMacAddresses.Select(value => $"'{EscapeSingleQuotedLiteral(value)}'")) + ")";
        return string.Join(
            Environment.NewLine,
            $"$externalMac = '{EscapeSingleQuotedLiteral(externalMacAddress)}'",
            $"$internalMacs = {internalMacs}",
            "function Invoke-NetshNatCommand {",
            "    param([string[]]$Arguments, [switch]$IgnoreMissing)",
            "    $output = & netsh @Arguments 2>&1",
            "    $exitCode = $LASTEXITCODE",
            "    $outputText = @($output) -join \"`n\"",
            "    if ($exitCode -eq 0) { return }",
            "    if ($IgnoreMissing -and ($outputText -match 'not found|does not exist|not configured|not installed')) { return }",
            "    throw \"netsh $($Arguments -join ' ') failed with exit code $exitCode. Output: $outputText\"",
            "}",
            "$externalAdapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $externalMac } | Select-Object -First 1",
            "if (-not $externalAdapter) { throw 'External NAT adapter was not found.' }",
            "$internalAdapters = foreach ($mac in $internalMacs) {",
            "    $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $mac } | Select-Object -First 1",
            "    if (-not $adapter) { throw \"Internal NAT adapter with MAC $mac was not found.\" }",
            "    $adapter",
            "}",
            "Invoke-NetshNatCommand -Arguments @('routing', 'ip', 'nat', 'install')",
            "Invoke-NetshNatCommand -Arguments @('routing', 'ip', 'nat', 'delete', 'interface', $externalAdapter.Name) -IgnoreMissing",
            "Invoke-NetshNatCommand -Arguments @('routing', 'ip', 'nat', 'add', 'interface', $externalAdapter.Name, 'mode=full')",
            "foreach ($adapter in $internalAdapters) {",
            "    Invoke-NetshNatCommand -Arguments @('routing', 'ip', 'nat', 'delete', 'interface', $adapter.Name) -IgnoreMissing",
            "    Invoke-NetshNatCommand -Arguments @('routing', 'ip', 'nat', 'add', 'interface', $adapter.Name, 'mode=private')",
            "}",
            "Write-Output 'Router NAT configured'");
    }

    public static string BuildProbeRouterExternalReadinessScript(string externalMacAddress)
    {
        return string.Join(
            Environment.NewLine,
            $"$externalMac = '{EscapeSingleQuotedLiteral(externalMacAddress)}'",
            "$adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $externalMac } | Select-Object -First 1",
            "if (-not $adapter) { throw 'External router adapter was not found for egress validation.' }",
            "if ($adapter.Status -ne 'Up') { Write-Output 'HOST_OFFLINE'; return }",
            "$defaultRoute = Get-NetRoute -InterfaceIndex $adapter.ifIndex -DestinationPrefix '0.0.0.0/0' -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1",
            "if (-not $defaultRoute) { Write-Output 'HOST_OFFLINE'; return }",
            "Write-Output 'READY'");
    }

    public static string BuildValidateCrossSwitchRoutingScript(string expectedDomainName)
    {
        return string.Join(
            Environment.NewLine,
            $"Resolve-DnsName '{EscapeSingleQuotedLiteral(expectedDomainName)}' -QuickTimeout -ErrorAction Stop | Out-Null",
            $"Resolve-DnsName '_ldap._tcp.dc._msdcs.{EscapeSingleQuotedLiteral(expectedDomainName)}' -Type SRV -QuickTimeout -ErrorAction Stop | Out-Null",
            "Write-Output 'Cross-switch routing validated'");
    }

    public static string BuildValidateRouterEgressScript()
    {
        return """
if (-not (Test-NetConnection -ComputerName '1.1.1.1' -Port 53 -InformationLevel Quiet -WarningAction SilentlyContinue)) {
    throw 'Outbound router egress validation failed.'
}
Write-Output 'Router egress validated'
""";
    }

    private static string ToPowerShellString(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "$null"
            : $"'{EscapeSingleQuotedLiteral(value)}'";

    private static string ToNullableInt(int? value)
        => value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "$null";

    private static string EscapeSingleQuotedLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

internal sealed class RouterNicPlan
{
    public string SwitchName { get; init; } = string.Empty;

    public string MacAddress { get; init; } = string.Empty;

    public bool IsExternal { get; init; }

    public string? IpAddress { get; init; }

    public int? PrefixLength { get; init; }

    public IReadOnlyList<string> DnsServers { get; init; } = Array.Empty<string>();
}

using System.Text;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Runtime;

internal static class GuestNetworkGuestScriptBuilder
{
    // Guest-side Get-NetAdapter returns MACs as separator-delimited hex (00-15-5D-..), while Hyper-V injects them
    // bare (00155D..). Reduce the guest value to the same canonical form as Services MacAddressNormalizer
    // (separators stripped, upper-cased) before comparing against the already-normalized injected MAC.
    private const string CanonicalMacFunction =
        "function ConvertTo-CanonicalMac { param([string]$Value) if ([string]::IsNullOrWhiteSpace($Value)) { return '' } return (($Value -replace '[-:. ]', '')).ToUpperInvariant() }";

    public static string BuildPrepareGuestNetworkScript(IReadOnlyList<GuestNicPlan> nics)
    {
        var orderedNics = nics.OrderBy(nic => nic.NicId, StringComparer.OrdinalIgnoreCase).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine("$targetNics = @(");
        for (var index = 0; index < orderedNics.Length; index++)
        {
            var nic = orderedNics[index];
            var dnsServers = nic.DnsServers.Count == 0
                ? "@()"
                : "@(" + string.Join(", ", nic.DnsServers.Select(value => $"'{EscapeSingleQuotedLiteral(value)}'")) + ")";
            sb.Append("    [pscustomobject]@{");
            sb.Append($" NicId = '{EscapeSingleQuotedLiteral(nic.NicId)}';");
            sb.Append($" MacAddress = '{EscapeSingleQuotedLiteral(MacAddressNormalizer.NormalizeMacAddress(nic.MacAddress))}';");
            sb.Append($" IpAddress = {ToPowerShellString(nic.IpAddress)};");
            sb.Append($" PrefixLength = {ToNullableInt(nic.PrefixLength)};");
            sb.Append($" DefaultGateway = {ToPowerShellString(nic.DefaultGateway)};");
            sb.Append($" DnsServers = {dnsServers}");
            sb.Append(" }");
            sb.AppendLine(index == orderedNics.Length - 1 ? string.Empty : ",");
        }

        sb.AppendLine(")");
        sb.AppendLine(CanonicalMacFunction);
        // Guest transport (VMBus/PowerShell Direct) becomes ready before the guest's synthetic NICs are all
        // enumerable, so wait (bounded) until every target MAC is present before configuring. Matching by MAC
        // rather than enumeration order guarantees each static IP binds to the adapter on its intended switch,
        // which is what fixes multi-NIC VMs binding the wrong address to the wrong network.
        sb.AppendLine("$__laNicDeadline = (Get-Date).AddSeconds(180)");
        sb.AppendLine("function Get-LaMatchedAdapters { param($targets) $all = @(Get-NetAdapter -Physical:$false -ErrorAction SilentlyContinue); $map = @{}; foreach ($t in $targets) { $map[$t.MacAddress] = ($all | Where-Object { (ConvertTo-CanonicalMac $_.MacAddress) -eq $t.MacAddress } | Select-Object -First 1) }; return $map }");
        sb.AppendLine("$adapterMap = Get-LaMatchedAdapters $targetNics");
        sb.AppendLine("while ((@($adapterMap.Values | Where-Object { $_ }).Count -lt $targetNics.Count) -and (Get-Date) -lt $__laNicDeadline) {");
        sb.AppendLine("    Start-Sleep -Seconds 3");
        sb.AppendLine("    $adapterMap = Get-LaMatchedAdapters $targetNics");
        sb.AppendLine("}");
        sb.AppendLine("$missing = @($targetNics | Where-Object { -not $adapterMap[$_.MacAddress] })");
        sb.AppendLine("if ($missing.Count -gt 0) {");
        sb.AppendLine("    $allAdapters = @(Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue | Sort-Object ifIndex)");
        sb.AppendLine("    $inventory = ($allAdapters | ForEach-Object { \"$($_.Name) [mac=$(ConvertTo-CanonicalMac $_.MacAddress); ifIndex=$($_.ifIndex); status=$($_.Status); hidden=$($_.Hidden)]\" }) -join '; '");
        sb.AppendLine("    if (-not $inventory) { $inventory = '(no adapters enumerated at all)' }");
        sb.AppendLine("    $wantedMacs = ($missing | ForEach-Object { $_.MacAddress }) -join ', '");
        sb.AppendLine("    throw \"Guest NIC(s) with MAC(s) $wantedMacs did not appear within 180s. All adapters seen (incl. hidden): $inventory\"");
        sb.AppendLine("}");
        sb.AppendLine("foreach ($target in $targetNics) {");
        sb.AppendLine("    $adapter = $adapterMap[$target.MacAddress]");
        sb.AppendLine("    Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue");
        sb.AppendLine("    Get-NetRoute -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue");
        sb.AppendLine("    if ($target.IpAddress -and $target.PrefixLength) {");
        sb.AppendLine("        $ipParams = @{ InterfaceIndex = $adapter.ifIndex; IPAddress = $target.IpAddress; PrefixLength = [int]$target.PrefixLength; AddressFamily = 'IPv4'; ErrorAction = 'Stop' }");
        sb.AppendLine("        if ($target.DefaultGateway) { $ipParams['DefaultGateway'] = $target.DefaultGateway }");
        sb.AppendLine("        New-NetIPAddress @ipParams | Out-Null");
        sb.AppendLine("    }");
        sb.AppendLine("    if ($target.DnsServers.Count -gt 0) {");
        sb.AppendLine("        Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses $target.DnsServers -ErrorAction Stop");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine("Write-Output 'Guest network prepared'");
        return sb.ToString();
    }

    public static string BuildStabilizeDomainDnsScript(IReadOnlyList<string> dnsServers)
    {
        var dnsList = "@(" + string.Join(", ", dnsServers.Select(value => $"'{EscapeSingleQuotedLiteral(value)}'")) + ")";
        return string.Join(
            Environment.NewLine,
            $"$dnsServers = {dnsList}",
            "$adapters = Get-NetAdapter -Physical:$false | Sort-Object ifIndex",
            "foreach ($adapter in $adapters) {",
            "    Set-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -ServerAddresses $dnsServers -ErrorAction Stop",
            "}",
            "Write-Output 'Domain DNS stabilized'");
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

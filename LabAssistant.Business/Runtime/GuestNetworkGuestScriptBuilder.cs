using System.Text;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime;

internal static class GuestNetworkGuestScriptBuilder
{
    public static string BuildPrepareGuestNetworkScript(IReadOnlyList<V2ResolvedVmNetworkInterface> nics)
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
            sb.Append($" IpAddress = {ToPowerShellString(nic.IpAddress)};");
            sb.Append($" PrefixLength = {ToNullableInt(nic.PrefixLength)};");
            sb.Append($" DefaultGateway = {ToPowerShellString(nic.DefaultGateway)};");
            sb.Append($" DnsServers = {dnsServers}");
            sb.Append(" }");
            sb.AppendLine(index == orderedNics.Length - 1 ? string.Empty : ",");
        }

        sb.AppendLine(")");
        // Guest transport (VMBus/PowerShell Direct) becomes ready before the guest's synthetic NIC is
        // enumerable, so a single-shot Get-NetAdapter can momentarily see fewer adapters than the template
        // intends and wrongly fail. Wait (bounded) for the expected adapter count to appear before configuring.
        sb.AppendLine("$__laNicDeadline = (Get-Date).AddSeconds(180)");
        sb.AppendLine("$adapters = @(Get-NetAdapter -Physical:$false -ErrorAction SilentlyContinue | Sort-Object ifIndex)");
        sb.AppendLine("while ($adapters.Count -lt $targetNics.Count -and (Get-Date) -lt $__laNicDeadline) {");
        sb.AppendLine("    Start-Sleep -Seconds 3");
        sb.AppendLine("    $adapters = @(Get-NetAdapter -Physical:$false -ErrorAction SilentlyContinue | Sort-Object ifIndex)");
        sb.AppendLine("}");
        sb.AppendLine("if ($adapters.Count -lt $targetNics.Count) {");
        sb.AppendLine("    $allAdapters = @(Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue | Sort-Object ifIndex)");
        sb.AppendLine("    $inventory = ($allAdapters | ForEach-Object { \"$($_.Name) [ifIndex=$($_.ifIndex); status=$($_.Status); hidden=$($_.Hidden); desc=$($_.InterfaceDescription)]\" }) -join '; '");
        sb.AppendLine("    if (-not $inventory) { $inventory = '(no adapters enumerated at all)' }");
        sb.AppendLine("    throw \"Not enough guest NICs available to satisfy template network intent. Wanted $($targetNics.Count), matched $($adapters.Count) after waiting 180s. All adapters seen (incl. hidden): $inventory\"");
        sb.AppendLine("}");
        sb.AppendLine("for ($index = 0; $index -lt $targetNics.Count; $index++) {");
        sb.AppendLine("    $target = $targetNics[$index]");
        sb.AppendLine("    $adapter = $adapters[$index]");
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

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.HyperV;

public sealed class HyperVMachineAdminService : IHyperVMachineAdminService
{
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;

    public HyperVMachineAdminService(Func<IPersistentPowerShellSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public async Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync()
    {
        const string script = """
            $items = Get-VM | ForEach-Object {
                $diskPaths = @(Get-VMHardDiskDrive -VMName $_.Name -ErrorAction SilentlyContinue | ForEach-Object { $_.Path })
                [PSCustomObject]@{
                    VmId = $_.Id.Guid
                    VmName = $_.Name
                    State = $_.State.ToString()
                    VmPath = $_.Path
                    DiskPaths = $diskPaths
                }
            }
            $items | ConvertTo-Json -Compress -Depth 4
            """;

        var (output, error) = await ExecuteWithFreshSessionAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);

        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return Array.Empty<HyperVHostMachineVmInfo>();
        }

        using var document = JsonDocument.Parse(cleanedOutput);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray()
                .Select(ParseVmInfo)
                .ToList(),
            JsonValueKind.Object => [ParseVmInfo(document.RootElement)],
            _ => Array.Empty<HyperVHostMachineVmInfo>()
        };
    }

    public Task<HyperVMachineActionResult> StartVmAsync(string vmName)
    {
        var script = $"Start-VM -Name {Quote(vmName)} -ErrorAction Stop";
        return ExecuteCommandAsync(script);
    }

    public Task<HyperVMachineActionResult> StopVmAsync(string vmName)
    {
        var script = $"Stop-VM -Name {Quote(vmName)} -Force -ErrorAction Stop";
        return ExecuteCommandAsync(script);
    }

    public Task<HyperVMachineActionResult> RestartVmAsync(string vmName)
    {
        var script = $"Restart-VM -Name {Quote(vmName)} -Force -ErrorAction Stop";
        return ExecuteCommandAsync(script);
    }

    public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName)
    {
        try
        {
            var psi = new ProcessStartInfo("vmconnect.exe", $"localhost \"{vmName}\"")
            {
                UseShellExecute = true
            };

            Process.Start(psi);
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HyperVMachineActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                FailureMetadata = RuntimeErrorMetadataNormalizer.FromException(ex)
            });
        }
    }

    public async Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName)
    {
        var script = $$"""
            $vm = Get-VM -Name {{Quote(vmName)}} -ErrorAction Stop
            $memory = Get-VMMemory -VMName {{Quote(vmName)}} -ErrorAction Stop
            $adapters = @(Get-VMNetworkAdapter -VMName {{Quote(vmName)}} -ErrorAction SilentlyContinue | ForEach-Object {
                [PSCustomObject]@{
                    AdapterName = $_.Name
                    SwitchName = $_.SwitchName
                }
            })
            [PSCustomObject]@{
                ProcessorCount = [int]$vm.ProcessorCount
                StartupMemoryBytes = [int64]$memory.Startup
                DynamicMemoryEnabled = [bool]$memory.DynamicMemoryEnabled
                MinimumMemoryBytes = [int64]$memory.Minimum
                MaximumMemoryBytes = [int64]$memory.Maximum
                MemoryBufferPercent = [int]$memory.Buffer
                NetworkAdapters = $adapters
            } | ConvertTo-Json -Compress -Depth 5
            """;

        var (output, error) = await ExecuteWithFreshSessionAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return null;
        }

        using var document = JsonDocument.Parse(cleanedOutput);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new HyperVMachineEditSnapshot
        {
            ProcessorCount = GetOptionalInt(document.RootElement, "ProcessorCount") ?? 1,
            StartupMemoryBytes = GetOptionalLong(document.RootElement, "StartupMemoryBytes") ?? 0,
            DynamicMemoryEnabled = GetOptionalBool(document.RootElement, "DynamicMemoryEnabled") ?? false,
            MinimumMemoryBytes = GetOptionalLong(document.RootElement, "MinimumMemoryBytes") ?? 0,
            MaximumMemoryBytes = GetOptionalLong(document.RootElement, "MaximumMemoryBytes") ?? 0,
            MemoryBufferPercent = GetOptionalInt(document.RootElement, "MemoryBufferPercent") ?? 20,
            NetworkAdapters = GetNetworkAdapters(document.RootElement)
        };
    }

    public async Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync()
    {
        const string script = "Get-VMSwitch | Select-Object -ExpandProperty Name | ConvertTo-Json -Compress";
        var (output, error) = await ExecuteWithFreshSessionAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return Array.Empty<string>();
        }

        using var document = JsonDocument.Parse(cleanedOutput);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray()
                .Select(element => element.GetString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            JsonValueKind.String when !string.IsNullOrWhiteSpace(document.RootElement.GetString()) => [document.RootElement.GetString()!],
            _ => Array.Empty<string>()
        };
    }

    public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage)
    {
        if (!includeStorage)
        {
            var vmOnlyScript = $"Remove-VM -Name {Quote(vmName)} -Force -ErrorAction Stop";
            return ExecuteCommandAsync(vmOnlyScript);
        }

        var deleteWithStorageScript = $$"""
            $vm = Get-VM -Name {{Quote(vmName)}} -ErrorAction Stop
            $targets = New-Object System.Collections.Generic.List[string]
            if (-not [string]::IsNullOrWhiteSpace($vm.Path)) {
                $targets.Add($vm.Path)
            }
            Get-VMHardDiskDrive -VMName {{Quote(vmName)}} -ErrorAction SilentlyContinue | ForEach-Object {
                if (-not [string]::IsNullOrWhiteSpace($_.Path)) {
                    $targets.Add($_.Path)
                }
            }
            Remove-VM -Name {{Quote(vmName)}} -Force -ErrorAction Stop
            $targets | Select-Object -Unique | ForEach-Object {
                if (Test-Path -LiteralPath $_) {
                    $item = Get-Item -LiteralPath $_ -ErrorAction SilentlyContinue
                    if ($null -ne $item -and $item.PSIsContainer) {
                        Remove-Item -LiteralPath $_ -Recurse -Force -ErrorAction SilentlyContinue
                    } else {
                        Remove-Item -LiteralPath $_ -Force -ErrorAction SilentlyContinue
                    }
                }
            }
            """;

        return ExecuteCommandAsync(deleteWithStorageScript);
    }

    private async Task<HyperVMachineActionResult> ExecuteCommandAsync(string script)
    {
        var (output, error) = await ExecuteWithFreshSessionAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);

        if (string.IsNullOrWhiteSpace(error))
        {
            return new HyperVMachineActionResult { Success = true };
        }

        var cleanedError = PowerShellOutputCleaner.Clean(error);
        return new HyperVMachineActionResult
        {
            Success = false,
            ErrorMessage = cleanedError,
            FailureMetadata = RuntimeErrorMetadataNormalizer.FromPowerShellErrorText(cleanedError)
        };
    }

    public async Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName)
    {
        var script = $$"""
            Get-VMNetworkAdapter -VMName {{Quote(vmName)}} -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty IPAddresses |
            ConvertTo-Json -Compress
            """;

        var (output, error) = await ExecuteWithFreshSessionAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return Array.Empty<string>();
        }

        using var document = JsonDocument.Parse(cleanedOutput);
        var values = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray()
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList(),
            JsonValueKind.String when !string.IsNullOrWhiteSpace(document.RootElement.GetString()) => [document.RootElement.GetString()!],
            _ => []
        };

        return values
            .Where(ipText => IPAddress.TryParse(ipText, out _))
            .ToList();
    }

    public Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4)
    {
        try
        {
            var psi = new ProcessStartInfo("mstsc.exe", $"/v:{targetIpv4}")
            {
                UseShellExecute = true
            };

            Process.Start(psi);
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HyperVMachineActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                FailureMetadata = RuntimeErrorMetadataNormalizer.FromException(ex)
            });
        }
    }

    public Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request)
    {
        var lines = new List<string>
        {
            $"Set-VMProcessor -VMName {Quote(vmName)} -Count {request.ProcessorCount} -ErrorAction Stop"
        };

        if (request.DynamicMemoryEnabled)
        {
            lines.Add(
                $"Set-VMMemory -VMName {Quote(vmName)} -StartupBytes {request.StartupMemoryBytes} -DynamicMemoryEnabled $true -MinimumBytes {request.MinimumMemoryBytes} -MaximumBytes {request.MaximumMemoryBytes} -Buffer {request.MemoryBufferPercent} -ErrorAction Stop");
        }
        else
        {
            lines.Add(
                $"Set-VMMemory -VMName {Quote(vmName)} -StartupBytes {request.StartupMemoryBytes} -DynamicMemoryEnabled $false -ErrorAction Stop");
        }

        foreach (var adapter in request.NetworkAdapterAssignments)
        {
            lines.Add(
                $"Connect-VMNetworkAdapter -VMName {Quote(vmName)} -Name {Quote(adapter.AdapterName)} -SwitchName {Quote(adapter.SwitchName)} -ErrorAction Stop");
        }

        var script = string.Join(Environment.NewLine, lines);
        return ExecuteCommandAsync(script);
    }

    private async Task<(string Output, string Error)> ExecuteWithFreshSessionAsync(string script)
    {
        using var session = _sessionFactory();
        return await session.ExecuteAsync(script);
    }

    private static HyperVHostMachineVmInfo ParseVmInfo(JsonElement vmElement)
    {
        return new HyperVHostMachineVmInfo
        {
            VmId = GetString(vmElement, "VmId"),
            VmName = GetString(vmElement, "VmName"),
            State = GetString(vmElement, "State"),
            VmPath = GetOptionalString(vmElement, "VmPath"),
            DiskPaths = GetDiskPaths(vmElement)
        };
    }

    private static IReadOnlyList<string> GetDiskPaths(JsonElement vmElement)
    {
        if (!vmElement.TryGetProperty("DiskPaths", out var diskPathsElement))
        {
            return Array.Empty<string>();
        }

        return diskPathsElement.ValueKind switch
        {
            JsonValueKind.Array => diskPathsElement.EnumerateArray()
                .Select(pathElement => pathElement.GetString())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .ToList(),
            JsonValueKind.String when !string.IsNullOrWhiteSpace(diskPathsElement.GetString()) => [diskPathsElement.GetString()!],
            _ => Array.Empty<string>()
        };
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return GetOptionalString(element, propertyName) ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.String => valueElement.GetString(),
            JsonValueKind.Number => valueElement.GetRawText(),
            _ => null
        };
    }

    private static long? GetOptionalLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.Number when valueElement.TryGetInt64(out var value) => value,
            JsonValueKind.String when long.TryParse(valueElement.GetString(), out var value) => value,
            _ => null
        };
    }

    private static int? GetOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.Number when valueElement.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(valueElement.GetString(), out var value) => value,
            _ => null
        };
    }

    private static bool? GetOptionalBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueElement))
        {
            return null;
        }

        return valueElement.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(valueElement.GetString(), out var value) => value,
            _ => null
        };
    }

    private static IReadOnlyList<HyperVMachineNetworkAdapterInfo> GetNetworkAdapters(JsonElement vmElement)
    {
        if (!vmElement.TryGetProperty("NetworkAdapters", out var adapterElement) ||
            adapterElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<HyperVMachineNetworkAdapterInfo>();
        }

        return adapterElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new HyperVMachineNetworkAdapterInfo
            {
                AdapterName = GetOptionalString(item, "AdapterName") ?? string.Empty,
                SwitchName = GetOptionalString(item, "SwitchName")
            })
            .Where(adapter => !string.IsNullOrWhiteSpace(adapter.AdapterName))
            .ToList();
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}

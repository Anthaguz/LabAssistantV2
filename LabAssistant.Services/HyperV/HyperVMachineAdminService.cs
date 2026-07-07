using System.Diagnostics;
using System.Net;
using System.IO;
using System.Text.Json;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.HyperV;

public sealed class HyperVMachineAdminService : IHyperVMachineAdminService
{
    private readonly IHyperVQueryExecutor _queryExecutor;
    private readonly IHyperVAdministrativeCommandExecutor _administrativeCommandExecutor;
    private readonly IAppSettingsStore _settingsStore;

    public HyperVMachineAdminService(
        IHyperVQueryExecutor queryExecutor,
        IHyperVAdministrativeCommandExecutor administrativeCommandExecutor,
        IAppSettingsStore settingsStore)
    {
        _queryExecutor = queryExecutor;
        _administrativeCommandExecutor = administrativeCommandExecutor;
        _settingsStore = settingsStore;
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

        var execution = await ExecuteQueryAsync("machines_list_host_vms", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);

        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
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
        return ExecuteCommandAsync("machines_start_vm", script);
    }

    public Task<HyperVMachineActionResult> StopVmAsync(string vmName)
    {
        var script = $"Stop-VM -Name {Quote(vmName)} -Force -ErrorAction Stop";
        return ExecuteCommandAsync("machines_stop_vm", script);
    }

    public Task<HyperVMachineActionResult> RestartVmAsync(string vmName)
    {
        var script = $"Restart-VM -Name {Quote(vmName)} -Force -ErrorAction Stop";
        return ExecuteCommandAsync("machines_restart_vm", script);
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

        var execution = await ExecuteQueryAsync("machines_get_edit_snapshot", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
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
        var execution = await ExecuteQueryAsync("machines_get_virtual_switch_names", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
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

    public async Task<IReadOnlyList<HyperVVirtualSwitchInfo>> ListVirtualSwitchesAsync()
    {
        const string script = """
            $items = Get-VMSwitch | ForEach-Object {
                [PSCustomObject]@{
                    Name = $_.Name
                    SwitchType = $_.SwitchType.ToString()
                    AdapterName = $_.NetAdapterInterfaceDescription
                }
            }
            $items | ConvertTo-Json -Compress -Depth 4
            """;

        var execution = await ExecuteQueryAsync("machines_list_virtual_switches", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return Array.Empty<HyperVVirtualSwitchInfo>();
        }

        using var document = JsonDocument.Parse(cleanedOutput);
        return document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => document.RootElement.EnumerateArray()
                .Select(ParseVirtualSwitchInfo)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            JsonValueKind.Object => [ParseVirtualSwitchInfo(document.RootElement)],
            _ => Array.Empty<HyperVVirtualSwitchInfo>()
        };
    }

    public async Task<IReadOnlyList<string>> GetAttachedVmNamesForSwitchAsync(string switchName)
    {
        var script = $$"""
            $items = Get-VMNetworkAdapter -All -ErrorAction SilentlyContinue |
                Where-Object { $_.SwitchName -eq {{Quote(switchName)}} -and -not [string]::IsNullOrWhiteSpace($_.VMName) } |
                ForEach-Object { $_.VMName } |
                Sort-Object -Unique
            $items | ConvertTo-Json -Compress
            """;

        var execution = await ExecuteQueryAsync("machines_get_attached_vm_names_for_switch", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
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

    public Task<HyperVMachineActionResult> CreateVirtualSwitchAsync(HyperVVirtualSwitchCreateRequest request)
    {
        var script = string.Equals(request.SwitchType, "External", StringComparison.OrdinalIgnoreCase)
            ? $"New-VMSwitch -Name {Quote(request.Name)} -NetAdapterName {Quote(request.AdapterName ?? string.Empty)} -AllowManagementOS $true -ErrorAction Stop"
            : $"New-VMSwitch -Name {Quote(request.Name)} -SwitchType {Quote(request.SwitchType)} -ErrorAction Stop";
        return ExecuteCommandAsync("machines_create_virtual_switch", script);
    }

    public Task<HyperVMachineActionResult> RenameVirtualSwitchAsync(string currentName, string newName)
    {
        var script = $"Rename-VMSwitch -Name {Quote(currentName)} -NewName {Quote(newName)} -ErrorAction Stop";
        return ExecuteCommandAsync("machines_rename_virtual_switch", script);
    }

    public Task<HyperVMachineActionResult> DeleteVirtualSwitchAsync(string switchName)
    {
        var script = $"Remove-VMSwitch -Name {Quote(switchName)} -Force -ErrorAction Stop";
        return ExecuteCommandAsync("machines_delete_virtual_switch", script);
    }

    public async Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(
        string vmName,
        IReadOnlyCollection<string> knownBaseDiskPaths,
        string? differencingDiskBasePath)
    {
        var vmInfo = await GetVmStorageInfoAsync(vmName);
        if (vmInfo is null)
        {
            return Array.Empty<HyperVMachineDiskClassificationResult>();
        }

        var knownBaseSet = new HashSet<string>(
            knownBaseDiskPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath),
            StringComparer.OrdinalIgnoreCase);
        var differencingRoot = NormalizeDirectoryPath(differencingDiskBasePath);

        var results = new List<HyperVMachineDiskClassificationResult>();
        foreach (var diskPath in vmInfo.DiskPaths)
        {
            var normalizedPath = NormalizePath(diskPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (knownBaseSet.Contains(normalizedPath))
            {
                results.Add(new HyperVMachineDiskClassificationResult
                {
                    DiskPath = normalizedPath,
                    Classification = HyperVMachineDiskSafetyClassification.KnownBaseOrFull,
                    Reason = "path_matches_catalog_base_disk"
                });
                continue;
            }

            var vhdInfo = await ProbeVhdInfoAsync(normalizedPath);
            if (vhdInfo is null)
            {
                results.Add(new HyperVMachineDiskClassificationResult
                {
                    DiskPath = normalizedPath,
                    Classification = HyperVMachineDiskSafetyClassification.PotentialBaseOrUncertain,
                    Reason = "vhd_probe_failed"
                });
                continue;
            }

            if (string.Equals(vhdInfo.VhdType, "Differencing", StringComparison.OrdinalIgnoreCase))
            {
                var inDifferencingRoot = !string.IsNullOrWhiteSpace(differencingRoot) &&
                    normalizedPath.StartsWith(differencingRoot, StringComparison.OrdinalIgnoreCase);
                results.Add(new HyperVMachineDiskClassificationResult
                {
                    DiskPath = normalizedPath,
                    Classification = HyperVMachineDiskSafetyClassification.DifferencingEligible,
                    Reason = inDifferencingRoot
                        ? "vhd_type_differencing_under_differencing_root"
                        : "vhd_type_differencing"
                });
                continue;
            }

            // Some hosts/reporting paths may not return a stable VhdType string, but ParentPath is a
            // reliable indicator that the disk is differencing-based.
            if (!string.IsNullOrWhiteSpace(vhdInfo.ParentPath))
            {
                results.Add(new HyperVMachineDiskClassificationResult
                {
                    DiskPath = normalizedPath,
                    Classification = HyperVMachineDiskSafetyClassification.DifferencingEligible,
                    Reason = "parent_path_present"
                });
                continue;
            }

            // Fall back to path-based heuristic for known differencing root.
            if (!string.IsNullOrWhiteSpace(differencingRoot) &&
                normalizedPath.StartsWith(differencingRoot, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new HyperVMachineDiskClassificationResult
                {
                    DiskPath = normalizedPath,
                    Classification = HyperVMachineDiskSafetyClassification.DifferencingEligible,
                    Reason = "path_under_differencing_root"
                });
                continue;
            }

            if (string.Equals(vhdInfo.VhdType, "Dynamic", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vhdInfo.VhdType, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new HyperVMachineDiskClassificationResult
                {
                    DiskPath = normalizedPath,
                    Classification = HyperVMachineDiskSafetyClassification.KnownBaseOrFull,
                    Reason = $"vhd_type_{vhdInfo.VhdType?.ToLowerInvariant() ?? "unknown"}"
                });
                continue;
            }

            results.Add(new HyperVMachineDiskClassificationResult
            {
                DiskPath = normalizedPath,
                Classification = HyperVMachineDiskSafetyClassification.PotentialBaseOrUncertain,
                Reason = "unknown_vhd_type"
            });
        }

        return results;
    }

    public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage)
    {
        if (!includeStorage)
        {
            var vmOnlyScript = $"Remove-VM -Name {Quote(vmName)} -Force -ErrorAction Stop";
            return ExecuteCommandAsync("machines_delete_vm_registration_only", vmOnlyScript);
        }
        return DeleteVmAndStorageAsync(vmName);
    }

    private async Task<HyperVMachineActionResult> ExecuteCommandAsync(string commandName, string script)
    {
        var execution = await ExecuteAdministrativeCommandAsync(commandName, script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);

        if (string.IsNullOrWhiteSpace(execution.Error))
        {
            return new HyperVMachineActionResult { Success = true };
        }

        var cleanedError = PowerShellOutputCleaner.Clean(execution.Error);
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

        var execution = await ExecuteQueryAsync("machines_get_vm_ip_addresses", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
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
        return ExecuteCommandAsync("machines_apply_vm_edit", script);
    }

    private async Task<HyperVMachineActionResult> DeleteVmAndStorageAsync(string vmName)
    {
        var vmStorage = await GetVmStorageInfoAsync(vmName);
        var removeVmResult = await ExecuteCommandAsync("machines_delete_vm_with_storage", $"Remove-VM -Name {Quote(vmName)} -Force -ErrorAction Stop");
        if (!removeVmResult.Success)
        {
            return removeVmResult;
        }

        if (vmStorage is null)
        {
            return new HyperVMachineActionResult { Success = true };
        }

        var deletedDisks = new List<string>();
        var diskDeleteFailures = new List<Dictionary<string, object?>>();
        foreach (var diskPath in vmStorage.DiskPaths)
        {
            try
            {
                if (!File.Exists(diskPath))
                {
                    continue;
                }

                File.Delete(diskPath);
                deletedDisks.Add(diskPath);
            }
            catch (Exception ex)
            {
                diskDeleteFailures.Add(new Dictionary<string, object?>
                {
                    ["path"] = diskPath,
                    ["error"] = ex.Message
                });
            }
        }

        string? deletedVmFolderPath = null;
        Dictionary<string, object?>? vmFolderDeleteFailure = null;
        var vmFolderPath = vmStorage.VmPath;
        var folderCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(vmFolderPath))
        {
            folderCandidates.Add(NormalizePath(vmFolderPath));
        }

        foreach (var diskPath in vmStorage.DiskPaths)
        {
            var parent = Path.GetDirectoryName(diskPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                folderCandidates.Add(NormalizePath(parent));
            }
        }

        foreach (var folderPath in folderCandidates.OrderByDescending(path => path.Length))
        {
            if (!ShouldAttemptFolderCleanup(folderPath, vmName))
            {
                continue;
            }

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    continue;
                }

                Directory.Delete(folderPath, recursive: true);
                deletedVmFolderPath ??= folderPath;
            }
            catch (Exception ex)
            {
                vmFolderDeleteFailure = new Dictionary<string, object?>
                {
                    ["path"] = folderPath,
                    ["error"] = ex.Message
                };
                break;
            }
        }

        var metadata = new Dictionary<string, object?>
        {
            ["deletedDiskPaths"] = deletedDisks.ToArray(),
            ["diskDeleteFailures"] = diskDeleteFailures.ToArray(),
            ["vmFolderPath"] = vmFolderPath,
            ["deletedVmFolderPath"] = deletedVmFolderPath,
            ["vmFolderDeleteFailure"] = vmFolderDeleteFailure
        };

        if (diskDeleteFailures.Count > 0 || vmFolderDeleteFailure is not null)
        {
            return new HyperVMachineActionResult
            {
                Success = false,
                ErrorMessage = BuildCleanupFailureMessage(diskDeleteFailures, vmFolderDeleteFailure),
                FailureMetadata = metadata
            };
        }

        return new HyperVMachineActionResult
        {
            Success = true,
            FailureMetadata = metadata
        };
    }

    private Task<HyperVPowerShellExecutionResult> ExecuteAdministrativeCommandAsync(string commandName, string script)
    {
        return _administrativeCommandExecutor.ExecuteAsync(commandName, script);
    }

    private Task<HyperVPowerShellExecutionResult> ExecuteQueryAsync(string queryName, string script)
    {
        return _queryExecutor.ExecuteAsync(queryName, script);
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

    private static HyperVVirtualSwitchInfo ParseVirtualSwitchInfo(JsonElement element)
    {
        return new HyperVVirtualSwitchInfo
        {
            Name = GetString(element, "Name"),
            SwitchType = GetString(element, "SwitchType"),
            AdapterName = GetOptionalString(element, "AdapterName")
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
        return PowerShellCommandBuilder.Quote(value);
    }

    private async Task<HyperVMachineStorageInfo?> GetVmStorageInfoAsync(string vmName)
    {
        var script = $$"""
            $vm = Get-VM -Name {{Quote(vmName)}} -ErrorAction Stop
            $diskPaths = @(Get-VMHardDiskDrive -VMName {{Quote(vmName)}} -ErrorAction SilentlyContinue | ForEach-Object { $_.Path })
            [PSCustomObject]@{
                VmPath = $vm.Path
                DiskPaths = $diskPaths
            } | ConvertTo-Json -Compress -Depth 4
            """;

        var execution = await ExecuteQueryAsync("machines_get_vm_storage_info", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            throw new InvalidOperationException(PowerShellOutputCleaner.Clean(execution.Error));
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return null;
        }

        using var document = JsonDocument.Parse(cleanedOutput);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new HyperVMachineStorageInfo
        {
            VmPath = GetOptionalString(document.RootElement, "VmPath"),
            DiskPaths = GetDiskPaths(document.RootElement)
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList()
        };
    }

    private async Task<HyperVhdProbeInfo?> ProbeVhdInfoAsync(string diskPath)
    {
        var script = $$"""
            Get-VHD -Path {{Quote(diskPath)}} -ErrorAction Stop |
            Select-Object VhdType, ParentPath |
            ConvertTo-Json -Compress
            """;
        var execution = await ExecuteQueryAsync("machines_probe_vhd_info", script);
        DebugLogger.LogPowerShellOutput(script, execution.Output, execution.Error);
        if (!string.IsNullOrWhiteSpace(execution.Error))
        {
            return null;
        }

        var cleanedOutput = PowerShellOutputCleaner.Clean(execution.Output);
        if (string.IsNullOrWhiteSpace(cleanedOutput))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(cleanedOutput);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new HyperVhdProbeInfo
            {
                VhdType = GetOptionalString(document.RootElement, "VhdType"),
                ParentPath = GetOptionalString(document.RootElement, "ParentPath")
            };
        }
        catch
        {
            return null;
        }
    }

    private bool ShouldAttemptFolderCleanup(string? folderPath, string vmName)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        var normalizedPath = NormalizePath(folderPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        // Never attempt to delete a drive root.
        var root = Path.GetPathRoot(normalizedPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            string.Equals(
                normalizedPath.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var leafName = Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar));
        if (string.Equals(leafName, vmName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var vmBasePath = NormalizeDirectoryPath(_settingsStore.Settings.VmBasePath);
        if (!string.IsNullOrWhiteSpace(vmBasePath))
        {
            var normalizedFolderPath = NormalizeDirectoryPath(normalizedPath);
            if (normalizedFolderPath.StartsWith(vmBasePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(path).Trim();
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.EndsWith(Path.DirectorySeparatorChar)
            ? normalized
            : normalized + Path.DirectorySeparatorChar;
    }

    private static string BuildCleanupFailureMessage(
        IReadOnlyCollection<Dictionary<string, object?>> diskDeleteFailures,
        Dictionary<string, object?>? vmFolderDeleteFailure)
    {
        var parts = new List<string> { "VM registration removed, but storage cleanup failed." };
        if (diskDeleteFailures.Count > 0)
        {
            parts.Add($"{diskDeleteFailures.Count} disk cleanup failure(s).");
        }

        if (vmFolderDeleteFailure is not null)
        {
            var path = vmFolderDeleteFailure.TryGetValue("path", out var value) ? value?.ToString() : null;
            parts.Add($"VM folder cleanup failed at '{path}'.");
        }

        return string.Join(" ", parts);
    }

    private sealed class HyperVMachineStorageInfo
    {
        public string? VmPath { get; init; }

        public IReadOnlyList<string> DiskPaths { get; init; } = Array.Empty<string>();
    }

    private sealed class HyperVhdProbeInfo
    {
        public string? VhdType { get; init; }

        public string? ParentPath { get; init; }
    }
}

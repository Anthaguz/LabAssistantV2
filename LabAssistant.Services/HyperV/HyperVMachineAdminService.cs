using System.Diagnostics;
using System.Text.Json;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.HyperV;

public sealed class HyperVMachineAdminService : IHyperVMachineAdminService
{
    private readonly IPersistentPowerShellSession _session;

    public HyperVMachineAdminService(IPersistentPowerShellSession session)
    {
        _session = session;
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

        var (output, error) = await _session.ExecuteAsync(script);
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
        var (output, error) = await _session.ExecuteAsync(script);
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

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }
}

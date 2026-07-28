using System.Text.Json;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>Ground-truth snapshot of a VM as Hyper-V actually sees it.</summary>
public sealed record VmGroundTruth(
    string Name,
    string State,
    long MemoryStartupBytes,
    int ProcessorCount,
    int Generation,
    IReadOnlyList<string> SwitchNames,
    IReadOnlyList<VmDiskGroundTruth> Disks);

/// <summary>Ground-truth of one attached virtual hard disk and its parent (differencing) chain.</summary>
public sealed record VmDiskGroundTruth(string Path, string? ParentPath, string VhdType);

/// <summary>
/// Reads the real Hyper-V state so scenarios can assert that what the app
/// claims it built actually exists on the host. This is the "did it really
/// happen" oracle: the harness never trusts the UI's own success message.
/// </summary>
public sealed class HyperVProbe
{
    /// <summary>Returns the VM's ground truth, or null when no such VM exists.</summary>
    public VmGroundTruth? GetVm(string name)
    {
        string script = $@"
$vm = Get-VM -Name '{Escape(name)}' -ErrorAction SilentlyContinue
if ($null -eq $vm) {{ '' ; exit 0 }}
$switches = @(Get-VMNetworkAdapter -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object {{ $_.SwitchName }} | Where-Object {{ $_ }})
$disks = @(Get-VMHardDiskDrive -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object {{
    $vhd = Get-VHD -Path $_.Path -ErrorAction SilentlyContinue
    [pscustomobject]@{{ Path = $_.Path; ParentPath = $vhd.ParentPath; VhdType = [string]$vhd.VhdType }}
}})
[pscustomobject]@{{
    Name = $vm.Name
    State = [string]$vm.State
    MemoryStartupBytes = [long]$vm.MemoryStartup
    ProcessorCount = [int]$vm.ProcessorCount
    Generation = [int]$vm.Generation
    SwitchNames = $switches
    Disks = $disks
}} | ConvertTo-Json -Depth 6 -Compress";

        var result = PowerShellRunner.Run(script);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Get-VM probe failed: {result.StdErr.Trim()}");
        }

        string json = result.StdOut.Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var switches = ReadStringArray(root, "SwitchNames");
        var disks = new List<VmDiskGroundTruth>();
        if (root.TryGetProperty("Disks", out var disksEl))
        {
            foreach (var d in EnumerateArrayOrSingle(disksEl))
            {
                disks.Add(new VmDiskGroundTruth(
                    d.TryGetProperty("Path", out var p) ? p.GetString() ?? string.Empty : string.Empty,
                    d.TryGetProperty("ParentPath", out var pp) && pp.ValueKind != JsonValueKind.Null ? pp.GetString() : null,
                    d.TryGetProperty("VhdType", out var vt) ? vt.GetString() ?? string.Empty : string.Empty));
            }
        }

        return new VmGroundTruth(
            root.GetProperty("Name").GetString() ?? name,
            root.GetProperty("State").GetString() ?? "Unknown",
            root.GetProperty("MemoryStartupBytes").GetInt64(),
            root.GetProperty("ProcessorCount").GetInt32(),
            root.GetProperty("Generation").GetInt32(),
            switches,
            disks);
    }

    /// <summary>Returns the names of all VMs whose name starts with the given prefix.</summary>
    public IReadOnlyList<string> ListVmNames(string prefix)
        => QueryNames($"Get-VM -ErrorAction SilentlyContinue | Where-Object {{ $_.Name -like '{Escape(prefix)}*' }} | ForEach-Object {{ $_.Name }}");

    /// <summary>Returns the names of all VM switches whose name starts with the given prefix.</summary>
    public IReadOnlyList<string> ListSwitchNames(string prefix)
        => QueryNames($"Get-VMSwitch -ErrorAction SilentlyContinue | Where-Object {{ $_.Name -like '{Escape(prefix)}*' }} | ForEach-Object {{ $_.Name }}");

    /// <summary>
    /// Returns the SwitchType of the named virtual switch (e.g. "Internal", "Private", "External"),
    /// or null when no such switch exists. Used to prove a deploy-created switch was made with the
    /// expected type without trusting the UI's success text.
    ///
    /// The query enumerates all switches and filters by name (rather than <c>Get-VMSwitch -Name</c>)
    /// on purpose: <see cref="PowerShellRunner"/> runs every script under
    /// <c>$ErrorActionPreference='Stop'</c>, and <c>Get-VMSwitch -Name '&lt;absent&gt;'</c> raises a
    /// terminating "unable to find a virtual switch" error there even with -ErrorAction
    /// SilentlyContinue, which would make this throw for a switch that simply does not exist yet. A
    /// deliberately-absent switch (e.g. the switch a deploy is expected to auto-create) must read back
    /// as null, not an exception, so enumerate-and-filter - the same shape <see cref="ListSwitchNames"/>
    /// uses - is the only reliable way. A genuine host/module failure still surfaces as a throw.
    /// </summary>
    public string? GetSwitchType(string name)
        => GetSwitchType(name, static script => PowerShellRunner.Run(script));

    /// <summary>
    /// Testable seam for <see cref="GetSwitchType(string)"/>: the caller supplies the PowerShell
    /// runner so the not-found-returns-null and genuine-failure-throws contract can be exercised
    /// without a Hyper-V host.
    /// </summary>
    internal static string? GetSwitchType(string name, Func<string, PowerShellResult> runner)
    {
        var result = runner(BuildSwitchTypeScript(name));
        if (!result.Success)
        {
            throw new InvalidOperationException($"Get-VMSwitch type probe failed: {result.StdErr.Trim()}");
        }

        var value = result.StdOut.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Builds the switch-type probe script. Enumerates all switches and filters by exact name so an
    /// absent switch yields empty output (and thus null) instead of the terminating not-found error a
    /// name-qualified <c>Get-VMSwitch -Name</c> raises under <c>$ErrorActionPreference='Stop'</c>.
    /// </summary>
    internal static string BuildSwitchTypeScript(string name)
        => $"Get-VMSwitch -ErrorAction SilentlyContinue | " +
           $"Where-Object {{ $_.Name -eq '{Escape(name)}' }} | " +
           "Select-Object -First 1 -ExpandProperty SwitchType";

    /// <summary>True when at least one host switch exists (borrowable in discover-existing mode).</summary>
    public bool AnySwitchExists()
        => QueryNames("Get-VMSwitch -ErrorAction SilentlyContinue | ForEach-Object { $_.Name }").Count > 0;

    private IReadOnlyList<string> QueryNames(string script)
    {
        var result = PowerShellRunner.Run(script);
        if (!result.Success)
        {
            throw new InvalidOperationException($"PowerShell name query failed: {result.StdErr.Trim()}");
        }

        return result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string property)
    {
        var list = new List<string>();
        if (root.TryGetProperty(property, out var el))
        {
            foreach (var item in EnumerateArrayOrSingle(el))
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        list.Add(s);
                    }
                }
            }
        }

        return list;
    }

    // ConvertTo-Json emits a bare object (not a 1-element array) when a collection has a single item.
    private static IEnumerable<JsonElement> EnumerateArrayOrSingle(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (el.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            yield return el;
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");
}

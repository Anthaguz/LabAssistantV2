using System.Text;

namespace LabAssistant.UITesting.Infrastructure.Cleanup;

/// <summary>Summary of what a sweep removed, for logging and findings.</summary>
public sealed class SweepReport
{
    public List<string> RemovedVms { get; } = new();
    public List<string> RemovedSwitches { get; } = new();
    public List<string> RemovedDiskFiles { get; } = new();
    public List<string> CatalogEntriesRemoved { get; } = new();
    public List<string> RemovedTemplateFiles { get; } = new();
    public List<string> Errors { get; } = new();

    public bool RemovedAnything =>
        RemovedVms.Count + RemovedSwitches.Count + RemovedDiskFiles.Count +
        CatalogEntriesRemoved.Count + RemovedTemplateFiles.Count > 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"VMs removed:      {string.Join(", ", RemovedVms)}");
        sb.AppendLine($"Switches removed: {string.Join(", ", RemovedSwitches)}");
        sb.AppendLine($"Disk files:       {RemovedDiskFiles.Count}");
        sb.AppendLine($"Catalog entries:  {string.Join(", ", CatalogEntriesRemoved)}");
        sb.AppendLine($"Template files:   {string.Join(", ", RemovedTemplateFiles)}");
        if (Errors.Count > 0)
        {
            sb.AppendLine($"Errors:           {string.Join(" | ", Errors)}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Removes Hyper-V resources the harness created, identified purely by the
/// harness tag prefix. Run at start (wipe leftovers from a prior crashed run)
/// and at end (tear down this run). It is deliberately conservative: it only
/// ever acts on names that carry the tag, so a borrowed switch or a real user
/// VM can never be caught in the sweep. This is the no-orphans guarantee.
/// </summary>
public sealed class TeardownSweeper
{
    private readonly ResourceTagger _tagger;
    private readonly HyperVProbe _probe;
    private readonly AppDataLocations _appData;
    private readonly CatalogSeeder _catalog;

    public TeardownSweeper(ResourceTagger tagger, HyperVProbe probe, AppDataLocations appData, CatalogSeeder catalog)
    {
        _tagger = tagger;
        _probe = probe;
        _appData = appData;
        _catalog = catalog;
    }

    /// <summary>Removes every harness-tagged VM, switch, catalog entry, and leftover disk file.</summary>
    public SweepReport SweepAll()
    {
        var report = new SweepReport();
        RemoveTaggedVms(report);
        RemoveTaggedSwitches(report);
        RemoveTaggedCatalogEntries(report);
        RemoveTaggedDiskFiles(report);
        RemoveTaggedTemplateFiles(report);
        return report;
    }

    private void RemoveTaggedVms(SweepReport report)
    {
        IReadOnlyList<string> names;
        try
        {
            names = _probe.ListVmNames(_tagger.GlobalPrefix + "-");
        }
        catch (Exception ex)
        {
            report.Errors.Add($"list VMs: {ex.Message}");
            return;
        }

        foreach (var name in names)
        {
            if (!_tagger.IsHarnessOwned(name))
            {
                continue; // paranoia: never touch anything not carrying the tag
            }

            // Turn the VM off, WAIT until it is actually Off (it may be in a
            // transitional Starting/Stopping state right after a deploy), then remove
            // with retries. Delete its disks after removal, also with retries because
            // the app process may still be releasing the file handle. This is the
            // no-orphans guarantee, so it must not give up on a transient lock.
            string script = $@"
$name = '{Escape(name)}'
$vm = Get-VM -Name $name -ErrorAction SilentlyContinue
if ($null -ne $vm) {{
    $paths = @(Get-VMHardDiskDrive -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object {{ $_.Path }})
    for ($i = 0; $i -lt 30; $i++) {{
        $vm = Get-VM -Name $name -ErrorAction SilentlyContinue
        if ($null -eq $vm) {{ break }}
        if ($vm.State -eq 'Off') {{ break }}
        Stop-VM -Name $name -TurnOff -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }}
    $removed = $false
    for ($i = 0; $i -lt 20; $i++) {{
        try {{ Remove-VM -Name $name -Force -ErrorAction Stop; $removed = $true; break }}
        catch {{ Start-Sleep -Milliseconds 750 }}
    }}
    if (-not $removed) {{ throw ""VM '$name' could not be removed after retries."" }}
    foreach ($p in $paths) {{
        if ($p -and (Test-Path $p)) {{
            for ($i = 0; $i -lt 20; $i++) {{
                try {{ Remove-Item -LiteralPath $p -Force -ErrorAction Stop; break }}
                catch {{ Start-Sleep -Milliseconds 750 }}
            }}
        }}
    }}
}}";
            var result = PowerShellRunner.Run(script);
            if (result.Success)
            {
                report.RemovedVms.Add(name);
            }
            else
            {
                report.Errors.Add($"remove VM '{name}': {result.StdErr.Trim()}");
            }
        }
    }

    private void RemoveTaggedSwitches(SweepReport report)
    {
        IReadOnlyList<string> names;
        try
        {
            names = _probe.ListSwitchNames(_tagger.GlobalPrefix + "-");
        }
        catch (Exception ex)
        {
            report.Errors.Add($"list switches: {ex.Message}");
            return;
        }

        foreach (var name in names)
        {
            if (!_tagger.IsHarnessOwned(name))
            {
                continue;
            }

            var result = PowerShellRunner.Run($"Remove-VMSwitch -Name '{Escape(name)}' -Force -ErrorAction Stop");
            if (result.Success)
            {
                report.RemovedSwitches.Add(name);
            }
            else
            {
                report.Errors.Add($"remove switch '{name}': {result.StdErr.Trim()}");
            }
        }
    }

    private void RemoveTaggedCatalogEntries(SweepReport report)
    {
        try
        {
            var removed = _catalog.RemoveTaggedEntries(_tagger, deleteBackingFiles: true);
            report.CatalogEntriesRemoved.AddRange(removed);
        }
        catch (Exception ex)
        {
            report.Errors.Add($"catalog cleanup: {ex.Message}");
        }
    }

    private void RemoveTaggedDiskFiles(SweepReport report)
    {
        foreach (var root in new[] { _appData.DifferencingDiskBasePath, _appData.VmBasePath })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, _tagger.GlobalPrefix + "-*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                report.Errors.Add($"enumerate '{root}': {ex.Message}");
                continue;
            }

            foreach (var file in files.ToList())
            {
                if (TryDeleteWithRetry(file, out var error))
                {
                    report.RemovedDiskFiles.Add(file);
                }
                else
                {
                    report.Errors.Add($"delete '{file}': {error}");
                }
            }

            // Remove tagged VM folders and their now-empty config sub-tree (Remove-VM deletes
            // the .vmcx but leaves the containing directories). The folder name carries the
            // harness tag and the disk files were already deleted above, so a recursive delete
            // is safe and never touches a borrowed path. Retry because the app process may still
            // be releasing a handle right after teardown.
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, _tagger.GlobalPrefix + "-*"))
                {
                    if (TryDeleteDirectoryWithRetry(dir, out var dirError))
                    {
                        report.RemovedDiskFiles.Add(dir);
                    }
                    else
                    {
                        report.Errors.Add($"delete dir '{dir}': {dirError}");
                    }
                }
            }
            catch (Exception ex)
            {
                report.Errors.Add($"enumerate dirs '{root}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Removes saved template files this run's tag owns. The app persists one JSON
    /// per template under the Templates folder; the harness seeds a tagged template
    /// there, so a file whose name carries the prefix is harness-owned and safe to
    /// delete. This closes the one no-orphans gap the VM/switch/catalog/disk sweeps
    /// leave open. Only ever matches the tag prefix, so a real user's template is
    /// never touched.
    /// </summary>
    private void RemoveTaggedTemplateFiles(SweepReport report)
    {
        var folder = _appData.TemplatesFolder;
        if (!Directory.Exists(folder))
        {
            return;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, _tagger.GlobalPrefix + "-*.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            report.Errors.Add($"enumerate templates '{folder}': {ex.Message}");
            return;
        }

        foreach (var file in files.ToList())
        {
            if (TryDeleteWithRetry(file, out var error))
            {
                report.RemovedTemplateFiles.Add(file);
            }
            else
            {
                report.Errors.Add($"delete template '{file}': {error}");
            }
        }
    }

    private static bool TryDeleteWithRetry(string file, out string error)
    {
        error = string.Empty;
        for (int i = 0; i < 20; i++)
        {
            try
            {
                File.Delete(file);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Thread.Sleep(750);
            }
        }

        return !File.Exists(file);
    }

    private static bool TryDeleteDirectoryWithRetry(string dir, out string error)
    {
        error = string.Empty;
        for (int i = 0; i < 20; i++)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Thread.Sleep(750);
            }
        }

        return !Directory.Exists(dir);
    }

    private static string Escape(string value) => value.Replace("'", "''");
}

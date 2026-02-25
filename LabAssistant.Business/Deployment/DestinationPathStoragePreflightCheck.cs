using LabAssistant.Models.Deployment;
using LabAssistant.Services.FileSystem;

namespace LabAssistant.Business.Deployment;

public sealed class DestinationPathStoragePreflightCheck : IDeploymentPreflightCheck
{
    private const long MinWarnFreeSpaceBytes = 10L * 1024 * 1024 * 1024;
    private const long WarnFreeSpacePerVmBytes = 2L * 1024 * 1024 * 1024;

    private readonly IDestinationPathFeasibilityProbe _pathProbe;
    private readonly IFreeSpaceInfoProvider _freeSpaceInfoProvider;

    public DestinationPathStoragePreflightCheck(
        IDestinationPathFeasibilityProbe pathProbe,
        IFreeSpaceInfoProvider freeSpaceInfoProvider)
    {
        _pathProbe = pathProbe;
        _freeSpaceInfoProvider = freeSpaceInfoProvider;
    }

    public string Key => "DestinationPathStorage";
    public int Order => 300;

    public bool SupportsMode(DeploymentPreflightMode mode) => true;

    public Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentContext);

        var results = new List<DeploymentReadinessCheckResult>();
        var verifyWriteAccess = mode == DeploymentPreflightMode.Full;

        AddDuplicatePathResults(deploymentContext.VmContexts, results);

        foreach (var vm in deploymentContext.VmContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(BuildPathResult(
                vm,
                "vm destination folder",
                vm.VmPath,
                DestinationPathTargetKind.Directory,
                "VM_PATH",
                verifyWriteAccess));

            results.Add(BuildPathResult(
                vm,
                "differencing disk path",
                vm.VhdPath,
                DestinationPathTargetKind.File,
                "VHD_PATH",
                verifyWriteAccess));
        }

        if (mode == DeploymentPreflightMode.Full)
        {
            results.AddRange(BuildFreeSpaceResults(deploymentContext.VmContexts));
        }

        return Task.FromResult<IReadOnlyList<DeploymentReadinessCheckResult>>(results);
    }

    private DeploymentReadinessCheckResult BuildPathResult(
        VmDeploymentContext vm,
        string pathLabel,
        string? path,
        DestinationPathTargetKind targetKind,
        string codePrefix,
        bool verifyWriteAccess)
    {
        var probe = _pathProbe.Probe(path, targetKind, verifyWriteAccess);

        var (status, codeSuffix, guidance) = probe.Status switch
        {
            DestinationPathFeasibilityStatus.Valid => (
                DeploymentReadinessStatus.Pass,
                verifyWriteAccess ? "READY" : "QUICK_OK",
                "No action required."),
            DestinationPathFeasibilityStatus.Missing => (
                DeploymentReadinessStatus.Fail,
                "MISSING",
                $"Set a {pathLabel} before deploying."),
            DestinationPathFeasibilityStatus.Invalid => (
                DeploymentReadinessStatus.Fail,
                "INVALID",
                $"Correct the {pathLabel} format or choose a valid local path."),
            DestinationPathFeasibilityStatus.RootUnavailable => (
                DeploymentReadinessStatus.Fail,
                "ROOT_UNAVAILABLE",
                $"Verify the drive/share for the {pathLabel} is available and reachable."),
            DestinationPathFeasibilityStatus.Unwritable => (
                DeploymentReadinessStatus.Fail,
                "UNWRITABLE",
                $"Choose a writable {pathLabel} or update permissions before deploying."),
            DestinationPathFeasibilityStatus.ExistingDirectoryConflict => (
                DeploymentReadinessStatus.Fail,
                "CONFLICT_DIR_EXISTS",
                $"Use a different {pathLabel} or remove the existing folder if it is safe to do so."),
            _ => (
                DeploymentReadinessStatus.Fail,
                "CONFLICT_FILE_EXISTS",
                $"Use a different {pathLabel} or remove the existing file if it is safe to do so.")
        };

        return new DeploymentReadinessCheckResult
        {
            Status = status,
            Category = DeploymentReadinessCategory.DestinationPathStorage,
            Code = $"DST.{codePrefix}.{codeSuffix}",
            Message = BuildPathMessage(vm, pathLabel, probe, verifyWriteAccess),
            ActionableGuidance = guidance,
            AffectedVmNames = string.IsNullOrWhiteSpace(vm.VmName) ? [] : [vm.VmName],
            ResourcePath = string.IsNullOrWhiteSpace(probe.Path) ? path : probe.Path,
            ResourceName = vm.VmName
        };
    }

    private static string BuildPathMessage(
        VmDeploymentContext vm,
        string pathLabel,
        DestinationPathFeasibilityResult probe,
        bool verifyWriteAccess)
    {
        var vmLabel = string.IsNullOrWhiteSpace(vm.VmName) ? "VM" : $"VM '{vm.VmName}'";
        return probe.Status switch
        {
            DestinationPathFeasibilityStatus.Valid when verifyWriteAccess =>
                $"{vmLabel} {pathLabel} is writable and ready: {probe.Path}",
            DestinationPathFeasibilityStatus.Valid =>
                $"{vmLabel} {pathLabel} passed quick path checks: {probe.Path}",
            DestinationPathFeasibilityStatus.Missing =>
                $"{vmLabel} {pathLabel} is missing.",
            _ => $"{vmLabel} {pathLabel} check failed: {probe.Path}"
        };
    }

    private void AddDuplicatePathResults(
        IReadOnlyList<VmDeploymentContext> vmContexts,
        ICollection<DeploymentReadinessCheckResult> results)
    {
        AddDuplicatePathResultsForSelector(vmContexts, results, vm => vm.VmPath, "VM_PATH", "VM destination folder");
        AddDuplicatePathResultsForSelector(vmContexts, results, vm => vm.VhdPath, "VHD_PATH", "differencing disk path");
    }

    private static void AddDuplicatePathResultsForSelector(
        IReadOnlyList<VmDeploymentContext> vmContexts,
        ICollection<DeploymentReadinessCheckResult> results,
        Func<VmDeploymentContext, string?> pathSelector,
        string codePrefix,
        string label)
    {
        var groups = vmContexts
            .Select(vm => new { Vm = vm, Path = NormalizePath(pathSelector(vm)) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => x.Path!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in groups)
        {
            var affected = group
                .Select(x => x.Vm.VmName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            results.Add(new DeploymentReadinessCheckResult
            {
                Status = DeploymentReadinessStatus.Fail,
                Category = DeploymentReadinessCategory.DestinationPathStorage,
                Code = $"DST.{codePrefix}.DUPLICATE_IN_DEPLOYMENT",
                Message = $"Multiple VMs are configured to use the same {label}: {group.Key}",
                ActionableGuidance = $"Assign a unique {label} for each VM before deploying.",
                AffectedVmNames = affected,
                ResourcePath = group.Key
            });
        }
    }

    private IEnumerable<DeploymentReadinessCheckResult> BuildFreeSpaceResults(IReadOnlyList<VmDeploymentContext> vmContexts)
    {
        var pathToVmNames = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var vm in vmContexts)
        {
            AddPathVmMapping(pathToVmNames, vm.VmPath, vm.VmName);
            AddPathVmMapping(pathToVmNames, vm.VhdPath, vm.VmName);
        }

        var groupedByRoot = new Dictionary<string, (string SamplePath, HashSet<string> VmNames)>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in pathToVmNames.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var probe = _freeSpaceInfoProvider.Probe(entry.Key);
            var root = string.IsNullOrWhiteSpace(probe.RootPath)
                ? $"UNKNOWN::{entry.Key}"
                : probe.RootPath;

            if (!groupedByRoot.TryGetValue(root, out var current))
            {
                groupedByRoot[root] = (entry.Key, new HashSet<string>(entry.Value, StringComparer.OrdinalIgnoreCase));
                continue;
            }

            foreach (var vmName in entry.Value)
            {
                current.VmNames.Add(vmName);
            }

            groupedByRoot[root] = current;
        }

        foreach (var item in groupedByRoot.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var freeSpace = _freeSpaceInfoProvider.Probe(item.Value.SamplePath);
            var vmNames = item.Value.VmNames.Where(n => !string.IsNullOrWhiteSpace(n)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

            if (freeSpace.Status == FreeSpaceProbeStatus.Unknown || freeSpace.AvailableBytes is null)
            {
                yield return new DeploymentReadinessCheckResult
                {
                    Status = DeploymentReadinessStatus.Warn,
                    Category = DeploymentReadinessCategory.DestinationPathStorage,
                    Code = "DST.STORAGE.FREE_SPACE_UNKNOWN",
                    Message = $"Free space could not be verified for destination root '{freeSpace.RootPath ?? item.Key}'.",
                    ActionableGuidance = "Deployment may proceed, but verify available disk space on the destination drive/share.",
                    AffectedVmNames = vmNames,
                    ResourcePath = freeSpace.RootPath ?? item.Value.SamplePath,
                    ResourceName = freeSpace.RootPath
                };
                continue;
            }

            var vmCount = Math.Max(1, vmNames.Length);
            var warningThreshold = Math.Max(MinWarnFreeSpaceBytes, vmCount * WarnFreeSpacePerVmBytes);
            if (freeSpace.AvailableBytes.Value < warningThreshold)
            {
                yield return new DeploymentReadinessCheckResult
                {
                    Status = DeploymentReadinessStatus.Warn,
                    Category = DeploymentReadinessCategory.DestinationPathStorage,
                    Code = "DST.STORAGE.LOW_FREE_SPACE",
                    Message = $"Low free space detected on destination root '{freeSpace.RootPath}': {FormatBytes(freeSpace.AvailableBytes.Value)} available.",
                    ActionableGuidance = $"Deployment may proceed in v1, but free additional space or choose another destination root (recommended threshold: {FormatBytes(warningThreshold)}).",
                    AffectedVmNames = vmNames,
                    ResourcePath = freeSpace.RootPath,
                    ResourceName = freeSpace.RootPath
                };
                continue;
            }

            yield return new DeploymentReadinessCheckResult
            {
                Status = DeploymentReadinessStatus.Pass,
                Category = DeploymentReadinessCategory.DestinationPathStorage,
                Code = "DST.STORAGE.FREE_SPACE_OK",
                Message = $"Destination root '{freeSpace.RootPath}' has sufficient free space for a basic v1 check.",
                ActionableGuidance = "No action required.",
                AffectedVmNames = vmNames,
                ResourcePath = freeSpace.RootPath,
                ResourceName = freeSpace.RootPath
            };
        }
    }

    private static void AddPathVmMapping(IDictionary<string, HashSet<string>> map, string? path, string? vmName)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!map.TryGetValue(normalized, out var names))
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            map[normalized] = names;
        }

        if (!string.IsNullOrWhiteSpace(vmName))
        {
            names.Add(vmName);
        }
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return System.IO.Path.GetFullPath(path.Trim());
        }
        catch
        {
            return path.Trim();
        }
    }

    private static string FormatBytes(long bytes)
    {
        const long gib = 1024L * 1024 * 1024;
        return $"{bytes / (double)gib:0.#} GiB";
    }
}

using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Minimal in-memory <see cref="IAppSettingsStore"/> used to construct the real
/// <c>DeployReferenceDataService</c> without touching disk. Only <see cref="Settings"/> is exercised
/// by the Quick Deploy reference-data path; the remaining members are unused mutation seams.
/// </summary>
internal sealed class FakeAppSettingsStore : IAppSettingsStore
{
    public FakeAppSettingsStore(AppSettings settings) => Settings = settings;

    public AppSettings Settings { get; }

    public string SettingsPath => "in-memory";

    public void LoadOrCreate()
    {
    }

    public void Reload()
    {
    }

    public void Save()
    {
    }

    public void ResetToDefault()
    {
    }

    public void SetTemplateFolder(string path)
    {
    }

    public void SetLogFolder(string path)
    {
    }

    public void SetVmBasePath(string path)
    {
    }

    public void SetDifferencingDiskBasePath(string path)
    {
    }
}

/// <summary>
/// In-memory VHDX catalog store returning a fixed item set. Only <see cref="Load"/> is used by the
/// reference-data path; write members are unsupported because tests never persist.
/// </summary>
internal sealed class FakeVhdxCatalogStore : IVhdxCatalogStore
{
    private readonly IReadOnlyList<VhdxCatalogItem> _items;

    public FakeVhdxCatalogStore(IReadOnlyList<VhdxCatalogItem> items) => _items = items;

    public VhdxCatalogLoadResult Load(string catalogPath)
    {
        var result = new VhdxCatalogLoadResult();
        result.Items.AddRange(_items);
        return result;
    }

    public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items) =>
        throw new NotSupportedException();

    public void EnsureCatalogFileExists(string catalogPath)
    {
    }
}

/// <summary>
/// Machines capability fake. The reference-data path only falls back to
/// <see cref="LoadVirtualSwitchesAsync"/> when the Hyper-V admin service throws, so it mirrors the
/// same switch names; all other members are irrelevant to Quick Deploy and throw if reached.
/// </summary>
internal sealed class FakeMachinesCapabilityService : IMachinesCapabilityService
{
    private readonly IReadOnlyList<string> _switches;

    public FakeMachinesCapabilityService(IReadOnlyList<string> switches) => _switches = switches;

    public Task<IReadOnlyList<string>> LoadVirtualSwitchesAsync() => Task.FromResult(_switches);

    public Task<IReadOnlyList<MachineInventoryItem>> LoadInventoryAsync() => throw new NotSupportedException();

    public Task<MachineEditSnapshot?> LoadEditSnapshotAsync(MachineInventoryItem vm) => throw new NotSupportedException();

    public Task<MachineDeletionPolicyMode> GetDeletionPolicyAsync() => throw new NotSupportedException();

    public Task SetDeletionPolicyAsync(MachineDeletionPolicyMode mode) => throw new NotSupportedException();

    public Task<MachineDeletePreview> GetDeletePreviewAsync(MachineInventoryItem vm) => throw new NotSupportedException();

    public Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm) => throw new NotSupportedException();

    public Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm) => throw new NotSupportedException();

    public Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm) => throw new NotSupportedException();

    public Task<MachineOperationResult> OpenConsoleAsync(MachineInventoryItem vm) => throw new NotSupportedException();

    public Task<MachineRdpReadinessResult> EvaluateRdpReadinessAsync(MachineInventoryItem vm, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MachineOperationResult> OpenRdpAsync(MachineInventoryItem vm, string targetIpv4) => throw new NotSupportedException();

    public Task<MachineOperationResult> ApplyEditsAsync(MachineInventoryItem vm, MachineEditDraft draft) => throw new NotSupportedException();

    public Task<MachineOperationResult> DeleteVmAsync(MachineInventoryItem vm, MachineDeleteScope scope) => throw new NotSupportedException();
}

/// <summary>
/// Templates capability fake. Only <see cref="LoadVhdxCatalogOptionsAsync"/> feeds the Quick Deploy
/// editor catalog; the rest are unused by the lane and throw if reached.
/// </summary>
internal sealed class FakeTemplatesCapabilityService : ITemplatesCapabilityService
{
    private readonly TemplatesVhdxCatalogLoadResult _catalogResult;

    public FakeTemplatesCapabilityService(IReadOnlyList<TemplatesVhdxCatalogItem> items) =>
        _catalogResult = new TemplatesVhdxCatalogLoadResult { Items = items };

    public Task<TemplatesVhdxCatalogLoadResult> LoadVhdxCatalogOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_catalogResult);

    public Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TemplateOperationResult> SaveAsync(
        TemplateEditorDocument document,
        string? targetFilePath = null,
        bool saveAs = false,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TemplateOperationResult> ExportAsync(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>
/// Hyper-V admin fake. The reference-data path prefers <see cref="ListVirtualSwitchesAsync"/>; the
/// remaining members are outside Quick Deploy readiness and throw if reached.
/// </summary>
internal sealed class FakeHyperVMachineAdminService : IHyperVMachineAdminService
{
    private readonly IReadOnlyList<HyperVVirtualSwitchInfo> _switches;

    public FakeHyperVMachineAdminService(IReadOnlyList<string> switchNames) =>
        _switches = switchNames
            .Select(name => new HyperVVirtualSwitchInfo { Name = name, SwitchType = "External" })
            .ToList();

    public Task<IReadOnlyList<HyperVVirtualSwitchInfo>> ListVirtualSwitchesAsync() => Task.FromResult(_switches);

    public Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync() =>
        Task.FromResult<IReadOnlyList<string>>(_switches.Select(item => item.Name).ToList());

    public Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync() => throw new NotSupportedException();

    public Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName) => throw new NotSupportedException();

    public Task<IReadOnlyList<string>> GetAttachedVmNamesForSwitchAsync(string switchName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> CreateVirtualSwitchAsync(HyperVVirtualSwitchCreateRequest request) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> RenameVirtualSwitchAsync(string currentName, string newName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> DeleteVirtualSwitchAsync(string switchName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> StartVmAsync(string vmName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> StopVmAsync(string vmName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> RestartVmAsync(string vmName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName) => throw new NotSupportedException();

    public Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4) => throw new NotSupportedException();

    public Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(
        string vmName,
        IReadOnlyCollection<string> knownBaseDiskPaths,
        string? differencingDiskBasePath) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request) => throw new NotSupportedException();

    public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage) => throw new NotSupportedException();
}

/// <summary>
/// Preflight fake that returns a configurable readiness result set and records how many times it ran.
/// </summary>
internal sealed class FakePreflightService : IDeploymentPreflightService
{
    public List<DeploymentReadinessCheckResult> Results { get; } = [];

    public int CallCount { get; private set; }

    public Task<DeploymentReadinessReport> RunAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new DeploymentReadinessReport
        {
            Mode = mode,
            Results = Results.ToList()
        });
    }
}

/// <summary>
/// Deployment coordinator fake. <see cref="OnDeploy"/> lets a test block the run to observe
/// navigate-away cancellation, and it captures the context passed by the workflow.
/// </summary>
internal sealed class FakeDeploymentCoordinator : IDeploymentCoordinator
{
    public int CallCount { get; private set; }

    public MultiVmDeploymentContext? LastContext { get; private set; }

    public Func<MultiVmDeploymentContext, Task>? OnDeploy { get; set; }

    public Task DeployAllAsync(MultiVmDeploymentContext multiContext)
    {
        CallCount++;
        LastContext = multiContext;
        return OnDeploy?.Invoke(multiContext) ?? Task.CompletedTask;
    }
}

/// <summary>
/// Outcome summary builder fake returning a fixed summary.
/// </summary>
internal sealed class FakeOutcomeSummaryBuilder : IDeploymentOutcomeSummaryBuilder
{
    public DeploymentOutcomeSummary Summary { get; set; } = new();

    public DeploymentOutcomeSummary Build(MultiVmDeploymentContext multiVmContext) => Summary;
}

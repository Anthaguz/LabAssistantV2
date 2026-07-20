using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
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
/// V2 planning fake. Returns a configurable <see cref="V2PlanBuildResult"/> and records the request the
/// workflow composed so tests can assert the Quick Deploy template was authored as a standalone V2 plan.
/// </summary>
internal sealed class FakeV2PlanningCapabilityService : IV2PlanningCapabilityService
{
    public int CallCount { get; private set; }

    public V2PlanBuildRequest? LastRequest { get; private set; }

    public V2PlanBuildResult Result { get; set; } = new() { Success = true };

    public Task<V2PlanBuildResult> BuildPlanAsync(V2PlanBuildRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        return Task.FromResult(Result);
    }

    /// <summary>
    /// Builds a minimal provisioning-only standalone plan (one ProvisionVm + StartVm node per VM name) that
    /// mirrors what the real planner emits for a bare Quick Deploy VM, so progress rows seed correctly.
    /// </summary>
    public static V2PlanBuildResult StandalonePlan(params string[] vmNames)
    {
        var nodes = new List<V2PlanNode>();
        foreach (var vmName in vmNames)
        {
            nodes.Add(new V2PlanNode
            {
                NodeId = $"{vmName}:provision",
                VmName = vmName,
                Kind = V2PlanNodeKind.ProvisionVm,
                DisplayName = "Provision VM"
            });
            nodes.Add(new V2PlanNode
            {
                NodeId = $"{vmName}:start",
                VmName = vmName,
                Kind = V2PlanNodeKind.StartVm,
                DisplayName = "Start VM"
            });
        }

        return new V2PlanBuildResult { Success = true, Nodes = nodes };
    }
}

/// <summary>
/// V2 runtime fake. <see cref="OnExecute"/> lets a test block the run to observe navigate-away cancellation;
/// it captures the request and deployment context and returns a configurable result.
/// </summary>
internal sealed class FakeV2RuntimeCapabilityService : IV2RuntimeCapabilityService
{
    public int CallCount { get; private set; }

    public V2RuntimeExecutionRequest? LastRequest { get; private set; }

    public MultiVmDeploymentContext? LastContext { get; private set; }

    public Func<V2RuntimeExecutionRequest, Task>? OnExecute { get; set; }

    public V2RuntimeExecutionResult Result { get; set; } = new() { Success = true };

    public async Task<V2RuntimeExecutionResult> ExecuteAsync(
        V2RuntimeExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;
        LastContext = request.DeploymentContext;

        if (OnExecute is not null)
        {
            await OnExecute(request);
        }

        return Result;
    }
}

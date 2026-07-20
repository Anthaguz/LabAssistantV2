using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Deploy;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Configurable in-memory <see cref="IDeployFromTemplateCompositionHost"/> for runtime-independent
/// From Template lane tests. It lets a test register template documents, control readiness and V2
/// plan results, resolve credential slots, and observe the deploy/execute seams (including blocking a
/// run to exercise navigate-away cancellation). No WinUI, Hyper-V, or disk access is involved.
/// </summary>
internal sealed class FakeFromTemplateCompositionHost : IDeployFromTemplateCompositionHost
{
    private readonly Dictionary<string, TemplateEditorDocument> _documentsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, V2RuntimeCredential> _credentialValues = new(StringComparer.OrdinalIgnoreCase);

    public List<TemplateLibraryItem> Templates { get; } = [];

    public AppSettings Settings { get; set; } = new() { VmBasePath = @"C:\Labs", CatalogPath = @"C:\catalog.json" };

    public List<LocalCredentialSlotDefinition> SlotDefinitions { get; } = [];

    public V2RuntimeExecutionResult V2Result { get; set; } = new() { Success = true };

    public int ResolveSuggestionsResult { get; set; }

    public int ResolveSuggestionsCallCount { get; private set; }

    public Func<LabTemplate, IReadOnlyCollection<string>, IReadOnlyDictionary<string, string>, V2PlanBuildResult>? V2PlanFactory { get; set; }

    // Observation / capture surfaces.
    public int RefreshSharedUiCount { get; private set; }

    public int RefreshResultsPanelCount { get; private set; }

    public int OpenResultsPanelCount { get; private set; }

    public int ShowEditorCount { get; private set; }

    public List<(string SlotKey, string Username, string Password)> Upserts { get; } = [];

    public IReadOnlyDictionary<string, string>? LastExternalSwitchAdapterMappings { get; private set; }

    public MultiVmDeploymentContext? LastV2DeployContext { get; private set; }

    public Func<MultiVmDeploymentContext, Task>? OnExecuteV2 { get; set; }

    /// <summary>Registers a template library item plus its editor document keyed by file path.</summary>
    public TemplateLibraryItem AddTemplate(
        string name,
        string filePath,
        TemplateExecutionEngine engine,
        LabTemplate? template = null)
    {
        var item = new TemplateLibraryItem { Name = name, FilePath = filePath, ExecutionEngine = engine };
        var labTemplate = template ?? new LabTemplate { Name = name, ExecutionEngine = engine };
        labTemplate.ExecutionEngine = engine;
        Templates.Add(item);
        _documentsByPath[filePath] = new TemplateEditorDocument { Template = labTemplate, SourceFilePath = filePath };
        return item;
    }

    public AppSettings DeploymentSettings => Settings;

    public IReadOnlyList<string> AvailableSwitches => [];

    public IReadOnlyList<V2AvailableSwitchInfo> AvailableSwitchInfo => [];

    public bool IsTemplatesLoading => false;

    public IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems => Templates;

    public IReadOnlyList<VhdxCatalogItem> LoadCatalogItems() => [];

    public Task EnsureReferenceDataAsync(bool forceRefresh) => Task.CompletedTask;

    public IReadOnlyList<LocalCredentialSlotDefinition> LoadLocalCredentialSlotDefinitions() => SlotDefinitions;

    public bool TryGetLocalCredentialSlotValue(string slotKey, out V2RuntimeCredential credential) =>
        _credentialValues.TryGetValue(slotKey, out credential!);

    public void UpsertLocalCredentialSlot(string slotKey, string username, string password)
    {
        Upserts.Add((slotKey, username, password));
        _credentialValues[slotKey] = new V2RuntimeCredential { Username = username, Password = password };
    }

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => Task.CompletedTask;

    public Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath) =>
        Task.FromResult(_documentsByPath[filePath]);

    public Task<int> ApplyResolveSuggestionsAsync(LabTemplate template)
    {
        ResolveSuggestionsCallCount++;
        return Task.FromResult(ResolveSuggestionsResult);
    }

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText)
    {
        ShowEditorCount++;
        return Task.CompletedTask;
    }

    public Task<V2PlanBuildResult> BuildV2PlanAsync(
        LabTemplate template,
        IReadOnlyCollection<string> resolvedCredentialSlotKeys,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings)
    {
        LastExternalSwitchAdapterMappings = externalSwitchAdapterMappings;
        var plan = V2PlanFactory?.Invoke(template, resolvedCredentialSlotKeys, externalSwitchAdapterMappings)
            ?? new V2PlanBuildResult { Success = true };
        return Task.FromResult(plan);
    }

    public void RefreshSharedUiState() => RefreshSharedUiCount++;

    public void RefreshResultsPanelState() => RefreshResultsPanelCount++;

    public void OnOpenResultsPanelRequested() => OpenResultsPanelCount++;

    public async Task<V2RuntimeExecutionResult> ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
        V2BaseRemoteAccessOptions baseRemoteAccessOptions,
        MultiVmDeploymentContext deploymentContext)
    {
        LastV2DeployContext = deploymentContext;
        if (OnExecuteV2 is not null)
        {
            await OnExecuteV2(deploymentContext);
        }

        return V2Result;
    }
}

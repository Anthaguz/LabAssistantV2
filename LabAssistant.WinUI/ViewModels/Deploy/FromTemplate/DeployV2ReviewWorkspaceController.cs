using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployV2ReviewWorkspaceController
{
    private readonly DeployV2ReviewWorkspaceViewModel _workspace;
    private readonly DeployV2ReviewProjectionService _projectionService;
    private readonly IDeployFromTemplateV2ReviewHost _host;

    public DeployV2ReviewWorkspaceController(
        DeployV2ReviewWorkspaceViewModel workspace,
        DeployV2ReviewProjectionService projectionService,
        IDeployFromTemplateV2ReviewHost host)
    {
        _workspace = workspace;
        _projectionService = projectionService;
        _host = host;
    }

    public async Task RefreshPlanAsync(LabTemplate template)
    {
        _workspace.BeginPlanning();
        _host.ApplyWorkspaceState();

        try
        {
            await _host.EnsureReferenceDataAsync(forceRefresh: false);
            var slotDefinitions = _host.LoadLocalCredentialSlotDefinitions();
            var slotValues = BuildResolvedSlotDictionary(slotDefinitions);
            var plan = await _host.BuildV2PlanAsync(
                template,
                slotValues.Keys.ToList(),
                _workspace.ExternalSwitchAdapterMappings);
            var projection = _projectionService.Build(template, plan, slotDefinitions, slotValues);

            _workspace.ApplyProjection(
                projection.Summary,
                projection.Blockers,
                projection.CredentialSlots,
                projection.Waves,
                projection.Diagnostics,
                plan,
                slotValues);

            _host.ApplyWorkspaceState();
        }
        catch (Exception ex)
        {
            _workspace.SetPlanningFailed($"V2 planning failed. {ex.Message}");
            _host.ApplyWorkspaceState();
        }
    }

    public void SelectCredentialSlot(string? slotKey)
    {
        _workspace.SelectCredentialSlot(slotKey);
        _host.ApplyWorkspaceState();
    }

    public void SetExternalSwitchAdapterMapping(string switchName, string adapterName)
    {
        _workspace.SetExternalSwitchAdapterMapping(switchName, adapterName);
        _host.ApplyWorkspaceState();
    }

    public async Task SaveCredentialSlotAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(_workspace.SelectedCredentialSlotKey))
        {
            _workspace.SetPlanningFailed("Select a credential slot before saving.");
            _host.ApplyWorkspaceState();
            return;
        }

        _host.UpsertLocalCredentialSlot(_workspace.SelectedCredentialSlotKey, username, password);
        if (_host.ActiveTemplateDocument is not null)
        {
            await RefreshPlanAsync(_host.ActiveTemplateDocument.Template);
        }
    }

    public async Task<V2RuntimeExecutionResult> StartDeployAsync(LabTemplate template)
    {
        if (_workspace.CurrentPlan is null)
        {
            throw new InvalidOperationException("V2 deployment cannot start until planning succeeds.");
        }

        return await _host.ExecuteV2DeployAsync(
            template,
            _workspace.CurrentPlan,
            _workspace.ResolvedCredentialSlotValues,
            _workspace.CreateBaseRemoteAccessOptions(),
            new MultiVmDeploymentContext());
    }

    private IReadOnlyDictionary<string, V2RuntimeCredential> BuildResolvedSlotDictionary(
        IReadOnlyList<LabAssistant.Models.Configuration.LocalCredentialSlotDefinition> slotDefinitions)
    {
        var resolved = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in slotDefinitions)
        {
            if (_host.TryGetLocalCredentialSlotValue(definition.SlotKey, out var credential))
            {
                resolved[definition.SlotKey] = credential;
            }
        }

        return resolved;
    }
}

internal interface IDeployFromTemplateV2ReviewHost
{
    TemplateEditorDocument? ActiveTemplateDocument { get; }

    Task EnsureReferenceDataAsync(bool forceRefresh);

    IReadOnlyList<LabAssistant.Models.Configuration.LocalCredentialSlotDefinition> LoadLocalCredentialSlotDefinitions();

    bool TryGetLocalCredentialSlotValue(string slotKey, out V2RuntimeCredential credential);

    void UpsertLocalCredentialSlot(string slotKey, string username, string password);

    Task<V2PlanBuildResult> BuildV2PlanAsync(
        LabTemplate template,
        IReadOnlyCollection<string> resolvedCredentialSlotKeys,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings);

    Task<V2RuntimeExecutionResult> ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
        V2BaseRemoteAccessOptions baseRemoteAccessOptions,
        MultiVmDeploymentContext deploymentContext);

    void ApplyWorkspaceState();
}

using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployFromTemplateCompositionHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    IReadOnlyList<V2AvailableSwitchInfo> AvailableSwitchInfo { get; }

    bool IsTemplatesLoading { get; }

    IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    IReadOnlyList<LocalCredentialSlotDefinition> LoadLocalCredentialSlotDefinitions();

    bool TryGetLocalCredentialSlotValue(string slotKey, out V2RuntimeCredential credential);

    void UpsertLocalCredentialSlot(string slotKey, string username, string password);

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath);

    Task<int> ApplyResolveSuggestionsAsync(LabTemplate template);

    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    Task<V2PlanBuildResult> BuildV2PlanAsync(LabTemplate template, IReadOnlyCollection<string> resolvedCredentialSlotKeys);

    void RefreshSharedUiState();

    void RefreshResultsPanelState();

    void OnOpenResultsPanelRequested();

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    Task<V2RuntimeExecutionResult> ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
        V2BaseRemoteAccessOptions baseRemoteAccessOptions,
        MultiVmDeploymentContext deploymentContext);

    void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated);
}

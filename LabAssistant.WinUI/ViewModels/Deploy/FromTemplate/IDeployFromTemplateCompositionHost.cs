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

    bool IsTemplatesLoading { get; }

    IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    Task EnsureTemplateSwitchesAsync(bool forceRefresh);

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath);

    Task<int> ApplyResolveSuggestionsAsync(LabTemplate template);

    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    void RefreshSharedUiState();

    void ApplyRightPanelState();

    void OnOpenResultsPanelRequested();

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated);
}

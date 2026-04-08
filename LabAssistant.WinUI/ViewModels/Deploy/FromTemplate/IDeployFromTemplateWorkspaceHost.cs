using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Exposes the non-shell external integrations that the From Template owner still needs.
/// Lane workflow ownership stays in the owner/controller pair; this seam is limited to shared Templates/Deploy service access.
/// </summary>
internal interface IDeployFromTemplateWorkspaceHost
{
    bool IsTemplatesLoading { get; }

    IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems { get; }

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath);

    Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    void RefreshSharedUiState();

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated);
}

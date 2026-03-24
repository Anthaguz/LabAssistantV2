using LabAssistant.Business.Templates;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Shell-facing contract for the Quick Deploy composition.
/// Keeps shell-owned route, panel, and cross-capability actions explicit while editor and workspace coordination stay inside capability-local seams.
/// </summary>
internal interface IDeployOnTheFlyCompositionHost
{
    int VmEntryCount { get; }

    DeploymentReadinessReport? ReadinessReport { get; }

    bool IsEvaluatingReadiness { get; }

    bool IsStarting { get; }

    string LifecycleState { get; }

    void EnsureSeeded();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    void UpdateUi();

    void SetActionStatus(string statusText);

    void ScheduleAutoEvaluate();

    LabTemplate BuildTemplate();

    void ReplaceVmEntriesFromTemplate(LabTemplate template);

    Task<int> ApplyResolveSuggestionsAsync(LabTemplate template);

    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    void RefreshSharedUiState();

    Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName);

    void OnVmEntriesSelectionChanged(DeployOnTheFlyVmEntryRow? selectedRow);

    void OnEditorInteractionChanged(DeployOnTheFlyEditorInteractionState interactionState);

    Task OnEvaluateRequestedAsync(DeploymentPreflightMode mode);

    Task OnStartRequestedAsync();

    void OnOpenResultsPanelRequested();
}

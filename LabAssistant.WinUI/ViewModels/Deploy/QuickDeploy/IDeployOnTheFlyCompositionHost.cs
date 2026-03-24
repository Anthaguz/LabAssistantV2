using LabAssistant.Business.Templates;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Temporary residual bridge between the Quick Deploy composition and shell/shared integration that has not yet converged behind a purely capability-local owner.
/// This contract exists to keep true shell boundaries and shared Deploy hooks explicit during cleanup; it is not the default long-term scaling model for capability-specific coordination.
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

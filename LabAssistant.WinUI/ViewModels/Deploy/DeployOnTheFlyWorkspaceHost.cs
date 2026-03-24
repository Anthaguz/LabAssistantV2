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

/// <summary>
/// Adapts shell-owned and cross-capability hooks for Quick Deploy so <c>MainWindow</c> no longer implements the composition host boundary directly.
/// The lambdas supplied here are the remaining explicit shell/shared integration points for the Quick Deploy composition.
/// </summary>
internal sealed class DeployOnTheFlyWorkspaceHost : IDeployOnTheFlyCompositionHost
{
    private readonly Func<int> _getVmEntryCount;
    private readonly Func<DeploymentReadinessReport?> _getReadinessReport;
    private readonly Func<bool> _isEvaluatingReadiness;
    private readonly Func<bool> _isStarting;
    private readonly Func<string> _getLifecycleState;
    private readonly Action _ensureSeeded;
    private readonly Func<bool, Task> _ensureReferenceDataAsync;
    private readonly Action _updateUi;
    private readonly Action<string> _setActionStatus;
    private readonly Action _scheduleAutoEvaluate;
    private readonly Func<LabTemplate> _buildTemplate;
    private readonly Action<LabTemplate> _replaceVmEntriesFromTemplate;
    private readonly Func<LabTemplate, Task<int>> _applyResolveSuggestionsAsync;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;
    private readonly Action _refreshSharedUiState;
    private readonly Func<string, Task<bool>> _showRemoveVmEntryConfirmationDialogAsync;
    private readonly Action<DeployOnTheFlyVmEntryRow?> _onVmEntriesSelectionChanged;
    private readonly Action<DeployOnTheFlyEditorInteractionState> _onEditorInteractionChanged;
    private readonly Func<DeploymentPreflightMode, Task> _onEvaluateRequestedAsync;
    private readonly Func<Task> _onStartRequestedAsync;
    private readonly Action _onOpenResultsPanelRequested;

    public DeployOnTheFlyWorkspaceHost(
        Func<int> getVmEntryCount,
        Func<DeploymentReadinessReport?> getReadinessReport,
        Func<bool> isEvaluatingReadiness,
        Func<bool> isStarting,
        Func<string> getLifecycleState,
        Action ensureSeeded,
        Func<bool, Task> ensureReferenceDataAsync,
        Action updateUi,
        Action<string> setActionStatus,
        Action scheduleAutoEvaluate,
        Func<LabTemplate> buildTemplate,
        Action<LabTemplate> replaceVmEntriesFromTemplate,
        Func<LabTemplate, Task<int>> applyResolveSuggestionsAsync,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Action refreshSharedUiState,
        Func<string, Task<bool>> showRemoveVmEntryConfirmationDialogAsync,
        Action<DeployOnTheFlyVmEntryRow?> onVmEntriesSelectionChanged,
        Action<DeployOnTheFlyEditorInteractionState> onEditorInteractionChanged,
        Func<DeploymentPreflightMode, Task> onEvaluateRequestedAsync,
        Func<Task> onStartRequestedAsync,
        Action onOpenResultsPanelRequested)
    {
        _getVmEntryCount = getVmEntryCount;
        _getReadinessReport = getReadinessReport;
        _isEvaluatingReadiness = isEvaluatingReadiness;
        _isStarting = isStarting;
        _getLifecycleState = getLifecycleState;
        _ensureSeeded = ensureSeeded;
        _ensureReferenceDataAsync = ensureReferenceDataAsync;
        _updateUi = updateUi;
        _setActionStatus = setActionStatus;
        _scheduleAutoEvaluate = scheduleAutoEvaluate;
        _buildTemplate = buildTemplate;
        _replaceVmEntriesFromTemplate = replaceVmEntriesFromTemplate;
        _applyResolveSuggestionsAsync = applyResolveSuggestionsAsync;
        _showTemplateEditorAsync = showTemplateEditorAsync;
        _refreshSharedUiState = refreshSharedUiState;
        _showRemoveVmEntryConfirmationDialogAsync = showRemoveVmEntryConfirmationDialogAsync;
        _onVmEntriesSelectionChanged = onVmEntriesSelectionChanged;
        _onEditorInteractionChanged = onEditorInteractionChanged;
        _onEvaluateRequestedAsync = onEvaluateRequestedAsync;
        _onStartRequestedAsync = onStartRequestedAsync;
        _onOpenResultsPanelRequested = onOpenResultsPanelRequested;
    }

    public int VmEntryCount => _getVmEntryCount();

    public DeploymentReadinessReport? ReadinessReport => _getReadinessReport();

    public bool IsEvaluatingReadiness => _isEvaluatingReadiness();

    public bool IsStarting => _isStarting();

    public string LifecycleState => _getLifecycleState();

    public void EnsureSeeded() => _ensureSeeded();

    public Task EnsureReferenceDataAsync(bool forceRefresh) => _ensureReferenceDataAsync(forceRefresh);

    public void UpdateUi() => _updateUi();

    public void SetActionStatus(string statusText) => _setActionStatus(statusText);

    public void ScheduleAutoEvaluate() => _scheduleAutoEvaluate();

    public LabTemplate BuildTemplate() => _buildTemplate();

    public void ReplaceVmEntriesFromTemplate(LabTemplate template) => _replaceVmEntriesFromTemplate(template);

    public Task<int> ApplyResolveSuggestionsAsync(LabTemplate template) => _applyResolveSuggestionsAsync(template);

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) => _showTemplateEditorAsync(document, statusText);

    public void RefreshSharedUiState() => _refreshSharedUiState();

    public Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName) => _showRemoveVmEntryConfirmationDialogAsync(vmName);

    public void OnVmEntriesSelectionChanged(DeployOnTheFlyVmEntryRow? selectedRow) => _onVmEntriesSelectionChanged(selectedRow);

    public void OnEditorInteractionChanged(DeployOnTheFlyEditorInteractionState interactionState) => _onEditorInteractionChanged(interactionState);

    public Task OnEvaluateRequestedAsync(DeploymentPreflightMode mode) => _onEvaluateRequestedAsync(mode);

    public Task OnStartRequestedAsync() => _onStartRequestedAsync();

    public void OnOpenResultsPanelRequested() => _onOpenResultsPanelRequested();
}

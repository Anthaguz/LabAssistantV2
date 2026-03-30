using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Owns the Quick Deploy-local host boundary so controller construction and controller-host implementation no longer terminate in <c>MainWindow</c>.
/// The delegates supplied here are limited to shared Deploy integration, deployment services, and narrow shell callbacks that still remain outside the Quick Deploy owner.
/// </summary>
internal sealed class DeployOnTheFlyWorkspaceHost : IDeployOnTheFlyCompositionHost, IDeployOnTheFlyWorkspaceControllerHost
{
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace;
    private readonly Func<AppSettings> _getDeploymentSettings;
    private readonly Func<IReadOnlyList<string>> _getAvailableSwitches;
    private readonly Func<IReadOnlyList<VhdxCatalogItem>> _loadCatalogItems;
    private readonly Action _refreshSharedUiState;
    private readonly Func<bool, Task> _ensureReferenceDataAsync;
    private readonly Func<LabTemplate, Task<int>> _applyResolveSuggestionsAsync;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;
    private readonly Func<string, Task<bool>> _showRemoveVmEntryConfirmationDialogAsync;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessChecksAsync;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<Action> _enqueueUiUpdate;
    private readonly Action _onOpenResultsPanelRequested;
    private readonly DeployOnTheFlyWorkspaceController _controller;
    private DeployOnTheFlyWorkspaceComposition? _composition;

    public DeployOnTheFlyWorkspaceHost(
        DeployOnTheFlyWorkspaceViewModel workspace,
        Func<AppSettings> getDeploymentSettings,
        Func<IReadOnlyList<string>> getAvailableSwitches,
        Func<IReadOnlyList<VhdxCatalogItem>> loadCatalogItems,
        Action refreshSharedUiState,
        Func<bool, Task> ensureReferenceDataAsync,
        Func<LabTemplate, Task<int>> applyResolveSuggestionsAsync,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Func<string, Task<bool>> showRemoveVmEntryConfirmationDialogAsync,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessChecksAsync,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<Action> enqueueUiUpdate,
        Action onOpenResultsPanelRequested)
    {
        _workspace = workspace;
        _getDeploymentSettings = getDeploymentSettings;
        _getAvailableSwitches = getAvailableSwitches;
        _loadCatalogItems = loadCatalogItems;
        _refreshSharedUiState = refreshSharedUiState;
        _ensureReferenceDataAsync = ensureReferenceDataAsync;
        _applyResolveSuggestionsAsync = applyResolveSuggestionsAsync;
        _showTemplateEditorAsync = showTemplateEditorAsync;
        _showRemoveVmEntryConfirmationDialogAsync = showRemoveVmEntryConfirmationDialogAsync;
        _runReadinessChecksAsync = runReadinessChecksAsync;
        _deployAllAsync = deployAllAsync;
        _enqueueUiUpdate = enqueueUiUpdate;
        _onOpenResultsPanelRequested = onOpenResultsPanelRequested;
        _controller = new DeployOnTheFlyWorkspaceController(_workspace, this);
    }

    /// <summary>
    /// Completes the local owner/composition cycle after the Quick Deploy composition is created.
    /// </summary>
    public void AttachComposition(DeployOnTheFlyWorkspaceComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        if (_composition is not null && !ReferenceEquals(_composition, composition))
        {
            throw new InvalidOperationException("Quick Deploy composition is already attached.");
        }

        _composition = composition;
    }

    public int VmEntryCount => _workspace.VmEntryCount;

    public DeploymentReadinessReport? ReadinessReport => _workspace.ReadinessReport;

    public bool IsEvaluatingReadiness => _workspace.IsEvaluatingReadiness;

    public bool IsStarting => _workspace.IsStarting;

    public string LifecycleState => _workspace.LifecycleState;

    public void EnsureSeeded()
    {
        _workspace.EnsureSeeded(_workspace.SelectedVmEntry?.VmId);
        _refreshSharedUiState();
        Composition.SelectVmEntry(_workspace.SelectedVmEntry);
    }

    public Task EnsureReferenceDataAsync(bool forceRefresh) => _ensureReferenceDataAsync(forceRefresh);

    public void UpdateUi() => Composition.UpdateUi();

    public void SetActionStatus(string statusText) => Composition.SetActionStatus(statusText);

    public void ScheduleAutoEvaluate() => _controller.ScheduleAutoEvaluate();

    public LabTemplate BuildTemplate()
    {
        return new LabTemplate
        {
            Name = "Quick Deploy Draft",
            Description = "Generated quick deploy input.",
            VmTemplates = _workspace.CreateTemplateSnapshot().ToList()
        };
    }

    public void ReplaceVmEntriesFromTemplate(LabTemplate template)
    {
        _workspace.ReplaceEntriesFromTemplate(template, _workspace.SelectedVmEntry?.VmId);
        _refreshSharedUiState();
        Composition.SelectVmEntry(_workspace.SelectedVmEntry);
        Composition.UpdateEditorPanel();
    }

    public Task<int> ApplyResolveSuggestionsAsync(LabTemplate template) => _applyResolveSuggestionsAsync(template);

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) => _showTemplateEditorAsync(document, statusText);

    public void RefreshSharedUiState() => _refreshSharedUiState();

    public Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName) => _showRemoveVmEntryConfirmationDialogAsync(vmName);

    public void OnVmEntriesSelectionChanged(DeployOnTheFlyVmEntryRow? selectedRow)
    {
        _workspace.SetSelectedVmEntry(selectedRow?.VmEntry);
        Composition.UpdateEditorPanel();
        Composition.UpdateUi();
    }

    public void OnEditorInteractionChanged(DeployOnTheFlyEditorInteractionState interactionState)
    {
        if (_workspace.IsSynchronizingEditorDraft)
        {
            return;
        }

        Composition.SyncEditorDraft(interactionState);
        Composition.UpdateUi();
        _controller.ScheduleAutoEvaluate();
    }

    public Task OnEvaluateRequestedAsync(DeploymentPreflightMode mode) => _controller.EvaluateReadinessAsync(mode);

    public Task OnStartRequestedAsync() => _controller.StartDeployAsync();

    public void OnOpenResultsPanelRequested() => _onOpenResultsPanelRequested();

    AppSettings IDeployOnTheFlyWorkspaceControllerHost.DeploymentSettings => _getDeploymentSettings();

    IReadOnlyList<string> IDeployOnTheFlyWorkspaceControllerHost.AvailableSwitches => _getAvailableSwitches();

    IReadOnlyList<VhdxCatalogItem> IDeployOnTheFlyWorkspaceControllerHost.LoadCatalogItems() => _loadCatalogItems();

    bool IDeployOnTheFlyWorkspaceControllerHost.TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors) =>
        Composition.TryApplyVmFields(showSuccessStatus, showValidationErrors);

    Task<DeploymentReadinessReport> IDeployOnTheFlyWorkspaceControllerHost.RunReadinessChecksAsync(
        MultiVmDeploymentContext context,
        DeploymentPreflightMode mode) =>
        _runReadinessChecksAsync(context, mode);

    void IDeployOnTheFlyWorkspaceControllerHost.EnqueueUiUpdate(Action updateAction)
    {
        _enqueueUiUpdate(updateAction);
    }

    Task<DeploymentOutcomeSummary> IDeployOnTheFlyWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context) =>
        _deployAllAsync(context);

    private DeployOnTheFlyWorkspaceComposition Composition =>
        _composition ?? throw new InvalidOperationException("Quick Deploy composition has not been attached.");
}

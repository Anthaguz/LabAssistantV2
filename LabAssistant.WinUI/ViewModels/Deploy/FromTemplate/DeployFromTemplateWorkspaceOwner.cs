using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Owns the long-lived From Template lane boundary, including workspace lifetime, controller lifetime,
/// template-library loading, shared Deploy helper coordination, and lane-local panel intent.
/// Shell routing, shell panel infrastructure, and shared Deploy composition remain outside this owner.
/// </summary>
internal sealed class DeployFromTemplateWorkspaceOwner : IDeployFromTemplateWorkspaceControllerHost
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();
    private readonly DeployFromTemplateWorkspaceComposition _composition;
    private readonly DeployFromTemplateWorkspaceController _controller;
    private readonly IDeployFromTemplateWorkspaceHost _host;
    private readonly DeployReferenceDataService _referenceDataService;
    private readonly DeployResolveSuggestionsService _resolveSuggestionsService;
    private readonly DeployTemplateEditorLauncher _templateEditorLauncher;
    private readonly DeployFromTemplateWorkspaceShellBridge _shellBridge;
    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];
    private DeploymentReadinessReport? _readinessReport;
    private bool _isLoadingTemplates;

    public DeployFromTemplateWorkspaceOwner(
        DeployFromTemplateView view,
        DeployFromTemplateRightPanelView rightPanelView,
        object? templateItemsSource,
        IDeployFromTemplateWorkspaceHost host,
        DeployReferenceDataService referenceDataService,
        DeployResolveSuggestionsService resolveSuggestionsService,
        DeployTemplateEditorLauncher templateEditorLauncher,
        DeployFromTemplateWorkspaceShellBridge shellBridge)
    {
        _view = view;
        _host = host;
        _referenceDataService = referenceDataService;
        _resolveSuggestionsService = resolveSuggestionsService;
        _templateEditorLauncher = templateEditorLauncher;
        _shellBridge = shellBridge;
        _composition = new DeployFromTemplateWorkspaceComposition(view, rightPanelView, templateItemsSource, _workspace);
        _controller = new DeployFromTemplateWorkspaceController(_workspace, this);
        WireHandlers();
    }

    public bool IsLoadingTemplates => _isLoadingTemplates;

    public bool ShouldAutoOpenResultsPanel =>
        _workspace.IsStarting || string.Equals(_workspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    public string ResultsPanelTitle => "From Template Progress / Results";

    public void ApplyShellState(bool isFromTemplateActive)
    {
        _composition.SetVisibility(isFromTemplateActive);
        if (isFromTemplateActive)
        {
            UpdateUi();
        }
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _composition.ApplyResultsPanelState(isActive, showPanel, panelUnavailable);
    }

    public void ResetPanelState()
    {
        _composition.ResetPanelState();
    }

    public Task EnsureTemplatesLoadedAsync(bool forceRefresh) => LoadTemplatesAsync(forceRefresh);

    public void RefreshUi() => UpdateUi();

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _workspace.ReconcileSelection(items);
        UpdateUi();
    }

    AppSettings IDeployFromTemplateWorkspaceControllerHost.DeploymentSettings => _referenceDataService.DeploymentSettings;

    IReadOnlyList<string> IDeployFromTemplateWorkspaceControllerHost.AvailableSwitches => _referenceDataService.AvailableSwitches;

    TemplateEditorDocument? IDeployFromTemplateWorkspaceControllerHost.ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    IReadOnlyList<VhdxCatalogItem> IDeployFromTemplateWorkspaceControllerHost.LoadCatalogItems() => _referenceDataService.CatalogItems;

    Task IDeployFromTemplateWorkspaceControllerHost.EnsureReferenceDataAsync(bool forceRefresh) => _referenceDataService.EnsureAsync(forceRefresh);

    Task<DeploymentReadinessReport> IDeployFromTemplateWorkspaceControllerHost.RunReadinessAsync(
        MultiVmDeploymentContext context,
        DeploymentPreflightMode mode) => _host.RunReadinessAsync(context, mode);

    void IDeployFromTemplateWorkspaceControllerHost.ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues)
    {
        _compatibilityIssues.Clear();
        _compatibilityIssues.AddRange(issues);
        UpdateUi();
    }

    DeploymentReadinessReport? IDeployFromTemplateWorkspaceControllerHost.CurrentReadinessReport
    {
        get => _readinessReport;
        set
        {
            _readinessReport = value;
            UpdateUi();
        }
    }

    Task<DeploymentOutcomeSummary> IDeployFromTemplateWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context) => _host.DeployAllAsync(context);

    void IDeployFromTemplateWorkspaceControllerHost.AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _host.AttachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    void IDeployFromTemplateWorkspaceControllerHost.ApplyWorkspaceState() => UpdateUi();

    private void WireHandlers()
    {
        _view.ReloadTemplatesRequested += ReloadTemplatesRequested;
        _view.EvaluateReadinessRequested += EvaluateReadinessRequested;
        _view.ResolveSuggestionsRequested += ResolveSuggestionsRequested;
        _view.OpenTemplateEditorRequested += OpenTemplateEditorRequested;
        _view.StartDeployRequested += StartDeployRequested;
        _view.TemplateSelectionChanged += TemplateSelectionChanged;
        _view.OpenResultsPanelRequested += OpenResultsPanelRequested;
    }

    private async void ReloadTemplatesRequested(object sender, RoutedEventArgs e)
    {
        await EnsureTemplatesLoadedAsync(forceRefresh: true);
    }

    private async void EvaluateReadinessRequested(object sender, RoutedEventArgs e)
    {
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async void ResolveSuggestionsRequested(object sender, RoutedEventArgs e)
    {
        await ResolveSuggestionsAsync();
    }

    private async void OpenTemplateEditorRequested(object sender, RoutedEventArgs e)
    {
        await OpenTemplateEditorAsync();
    }

    private async void StartDeployRequested(object sender, RoutedEventArgs e)
    {
        await _controller.StartDeployAsync();
    }

    private async void TemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await HandleTemplateSelectionChangedAsync();
    }

    private void OpenResultsPanelRequested(object sender, RoutedEventArgs e)
    {
        _shellBridge.RequestResultsPanelToggle();
    }

    private async Task ResolveSuggestionsAsync()
    {
        if (_workspace.ActiveTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            UpdateUi();
            return;
        }

        await _referenceDataService.EnsureAsync(forceRefresh: false);
        var applied = _resolveSuggestionsService.Apply(
            _workspace.ActiveTemplateDocument.Template,
            _referenceDataService.CatalogItems,
            _referenceDataService.AvailableSwitches);
        _workspace.SetActionStatus(
            applied == 0
                ? "No auto-resolve suggestions available for the current template state."
                : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        UpdateUi();
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async Task OpenTemplateEditorAsync()
    {
        var selectedTemplateLibraryItem = _workspace.SelectedTemplateLibraryItem;
        if (selectedTemplateLibraryItem is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            UpdateUi();
            return;
        }

        try
        {
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplateLibraryItem.FilePath);
            await _templateEditorLauncher.ShowEditorAsync(document, "Template loaded.");
            _workspace.SetActionStatus($"Opened '{selectedTemplateLibraryItem.Name}' in Templates editor.");
        }
        catch (Exception ex)
        {
            _workspace.SetActionStatus($"Failed to open template in editor. {ex.Message}");
        }

        UpdateUi();
    }

    private async Task HandleTemplateSelectionChangedAsync()
    {
        if (_isLoadingTemplates || _host.IsTemplatesLoading)
        {
            return;
        }

        _workspace.SetSelectedTemplateLibraryItem(_view.SelectedTemplateLibraryItem);
        if (_workspace.SelectedTemplateLibraryItem is null)
        {
            _workspace.ClearSelection("No template selected.");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Idle",
                progressPercent: 0,
                progressSummary: "No template selected.");
            UpdateUi();
            return;
        }

        try
        {
            var selectedTemplate = _workspace.SelectedTemplateLibraryItem;
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplate.FilePath);
            _workspace.SetLoadedTemplateDocument(
                document,
                $"Loaded '{selectedTemplate.Name}' for deploy readiness.");
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Ready",
                progressPercent: 0,
                progressSummary: $"Template '{selectedTemplate.Name}' loaded.");
            UpdateUi();
            await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
        }
        catch (Exception ex)
        {
            _workspace.SetSelectionLoadFailed($"Failed to load selected template. {ex.Message}");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            UpdateUi();
        }
    }

    private async Task LoadTemplatesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _host.TemplateLibraryItems.Count > 0)
        {
            _host.RefreshSharedUiState();
            UpdateUi();
            return;
        }

        _isLoadingTemplates = true;
        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: "Loading",
            progressPercent: 0,
            progressSummary: "Loading templates...");
        _host.RefreshSharedUiState();
        UpdateUi();
        _workspace.SetActionStatus("Loading templates for deploy...");

        try
        {
            await _host.EnsureTemplatesLibraryAsync(forceRefresh);

            if (_host.TemplateLibraryItems.Count == 0)
            {
                _workspace.ClearSelection("No templates available for deploy.");
                _readinessReport = null;
                _compatibilityIssues.Clear();
                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "No templates available.");
            }
            else
            {
                if (_workspace.SelectedTemplateLibraryItem is null)
                {
                    _workspace.SetSelectedTemplateLibraryItem(_host.TemplateLibraryItems[0]);
                }

                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "Template list loaded.");
                _workspace.SetActionStatus($"Loaded {_host.TemplateLibraryItems.Count} template(s) for deploy.");
            }
        }
        catch (Exception ex)
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            _workspace.SetActionStatus($"Failed to load deploy templates. {ex.Message}");
        }
        finally
        {
            _isLoadingTemplates = false;
            _host.RefreshSharedUiState();
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        _composition.UpdateUi(_isLoadingTemplates, _host.IsTemplatesLoading, _compatibilityIssues, _readinessReport);
        _shellBridge.RefreshResultsPanelState();
    }
}

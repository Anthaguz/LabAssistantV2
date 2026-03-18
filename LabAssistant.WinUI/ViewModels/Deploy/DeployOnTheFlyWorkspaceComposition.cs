using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

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

    void ScheduleAutoEvaluate();

    void OnVmEntriesSelectionChanged(object? selectedItem);

    Task OnVmRemoveRequestedAsync(VmTemplate vmEntry);

    void OnAddVmRequested();

    Task OnRemoveSelectedVmRequestedAsync();

    void OnApplyVmChangesRequested();

    void OnVmNameDraftChanged();

    void OnVmMemoryDraftChanged();

    void OnVmCpuDraftChanged();

    void OnVmSwitchSelectionChanged();

    void OnVmVhdxCatalogSelectionChanged();

    Task OnEvaluateRequestedAsync();

    Task OnResolveSuggestionsRequestedAsync();

    Task OnOpenTemplateEditorRequestedAsync();

    Task OnStartRequestedAsync();

    void OnOpenResultsPanelRequested();
}

internal sealed class DeployOnTheFlyWorkspaceComposition
{
    private readonly DeployOnTheFlyView _view;
    private readonly DeployOnTheFlyRightPanelView _rightPanelView;
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace;
    private readonly IDeployOnTheFlyCompositionHost _host;

    public DeployOnTheFlyWorkspaceComposition(
        DeployOnTheFlyView view,
        DeployOnTheFlyRightPanelView rightPanelView,
        DeployOnTheFlyWorkspaceViewModel workspace,
        IDeployOnTheFlyCompositionHost host)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _workspace = workspace;
        _host = host;

        _view.DeployOnTheFlyVmEntriesListViewControl.ItemsSource = _workspace.VmEntryRows;
        _rightPanelView.DeployOnTheFlyVmResultsListViewControl.ItemsSource = _workspace.ResultRows;
        WireHandlers();
    }

    public int ResultRowCount => _workspace.ResultRows.Count;

    public bool IsStarting => _workspace.IsStarting;

    public string LifecycleState => _workspace.LifecycleState;

    public void ApplyShellState(bool isActive)
    {
        _view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        if (!isActive)
        {
            return;
        }

        _host.EnsureSeeded();
        _ = _host.EnsureReferenceDataAsync(forceRefresh: false);
        _host.UpdateUi();

        if (_host.VmEntryCount > 0 &&
            _host.ReadinessReport is null &&
            !_host.IsEvaluatingReadiness &&
            !_host.IsStarting)
        {
            _host.ScheduleAutoEvaluate();
        }
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _rightPanelView.Visibility = isActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        _view.DeployOnTheFlyOpenResultsPanelButtonControl.Content = showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results";
        _view.DeployOnTheFlyOpenResultsPanelButtonControl.IsEnabled = isActive && !panelUnavailable;
        _view.DeployOnTheFlyResultsPanelSummaryTextBlockControl.Text = panelUnavailable
            ? "Expand the window to review the progress and results panel."
            : _host.IsStarting || string.Equals(_host.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : ResultRowCount > 0
                    ? $"{ResultRowCount} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.";
    }

    private void WireHandlers()
    {
        _view.DeployOnTheFlyVmEntriesListViewControl.SelectionChanged += (_, _) =>
            _host.OnVmEntriesSelectionChanged(_view.DeployOnTheFlyVmEntriesListViewControl.SelectedItem);
        _view.VmRemoveRequested += async vmEntry => await _host.OnVmRemoveRequestedAsync(vmEntry);
        _view.DeployOnTheFlyAddVmButtonControl.Click += (_, _) => _host.OnAddVmRequested();
        _view.DeployOnTheFlyRemoveVmButtonControl.Click += async (_, _) => await _host.OnRemoveSelectedVmRequestedAsync();
        _view.DeployOnTheFlyApplyVmChangesButtonControl.Click += (_, _) => _host.OnApplyVmChangesRequested();
        _view.DeployOnTheFlyVmNameTextBoxControl.TextChanged += (_, _) => _host.OnVmNameDraftChanged();
        _view.DeployOnTheFlyVmMemoryTextBoxControl.TextChanged += (_, _) => _host.OnVmMemoryDraftChanged();
        _view.DeployOnTheFlyVmCpuTextBoxControl.TextChanged += (_, _) => _host.OnVmCpuDraftChanged();
        _view.DeployOnTheFlyVmSwitchComboBoxControl.SelectionChanged += (_, _) => _host.OnVmSwitchSelectionChanged();
        _view.DeployOnTheFlyVmVhdxCatalogComboBoxControl.SelectionChanged += (_, _) => _host.OnVmVhdxCatalogSelectionChanged();
        _view.DeployOnTheFlyEvaluateButtonControl.Click += async (_, _) => await _host.OnEvaluateRequestedAsync();
        _view.DeployOnTheFlyResolveSuggestionsButtonControl.Click += async (_, _) => await _host.OnResolveSuggestionsRequestedAsync();
        _view.DeployOnTheFlyOpenTemplateEditorButtonControl.Click += async (_, _) => await _host.OnOpenTemplateEditorRequestedAsync();
        _view.DeployOnTheFlyStartButtonControl.Click += async (_, _) => await _host.OnStartRequestedAsync();
        _view.DeployOnTheFlyOpenResultsPanelButtonControl.Click += (_, _) => _host.OnOpenResultsPanelRequested();
    }
}

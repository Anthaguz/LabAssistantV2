using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.Views.Assets;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal interface IAssetsBaseDisksCompositionHost
{
    string? PickBaseDiskFilePath();

    Task<bool> ShowRemoveConfirmationDialogAsync(AssetsBaseDiskListRow row, AssetsBaseDiskRemovalAssessment assessment);
}

internal sealed class AssetsBaseDisksCompositionHost : IAssetsBaseDisksCompositionHost
{
    private readonly Func<string?> _pickBaseDiskFilePath;
    private readonly Func<AssetsBaseDiskListRow, AssetsBaseDiskRemovalAssessment, Task<bool>> _showRemoveConfirmationDialogAsync;

    public AssetsBaseDisksCompositionHost(
        Func<string?> pickBaseDiskFilePath,
        Func<AssetsBaseDiskListRow, AssetsBaseDiskRemovalAssessment, Task<bool>> showRemoveConfirmationDialogAsync)
    {
        _pickBaseDiskFilePath = pickBaseDiskFilePath;
        _showRemoveConfirmationDialogAsync = showRemoveConfirmationDialogAsync;
    }

    public string? PickBaseDiskFilePath() => _pickBaseDiskFilePath();

    public Task<bool> ShowRemoveConfirmationDialogAsync(AssetsBaseDiskListRow row, AssetsBaseDiskRemovalAssessment assessment) => _showRemoveConfirmationDialogAsync(row, assessment);
}

internal sealed class AssetsBaseDisksWorkspaceComposition : IAssetsBaseDisksWorkspaceHost
{
    private readonly AssetsBaseDisksView _view;
    private readonly AssetsBaseDisksWorkspaceViewModel _workspace = new();
    private readonly AssetsBaseDisksWorkspaceController _controller;
    private readonly IAssetsBaseDisksCompositionHost _host;

    public AssetsBaseDisksWorkspaceComposition(
        IAssetsBaseDisksCapabilityService capabilityService,
        AssetsBaseDisksView view,
        IAssetsBaseDisksCompositionHost host)
    {
        _view = view;
        _host = host;
        _controller = new AssetsBaseDisksWorkspaceController(capabilityService, _workspace, this);
        _view.AssetsBaseDisksListViewControl.ItemsSource = _workspace.Inventory;
        WireHandlers();
        _controller.ApplyWorkspaceState();
    }

    public bool IsLoading => _workspace.IsLoading;

    public int InventoryCount => _workspace.Inventory.Count;

    public Task EnsureInventoryAsync(bool forceRefresh) => _controller.EnsureInventoryAsync(forceRefresh);

    public void ApplyShellState()
    {
        _ = _controller.EnsureInventoryAsync(forceRefresh: false);
        _controller.ApplyWorkspaceState();
    }

    AssetsBaseDiskDraft? IAssetsBaseDisksWorkspaceHost.CaptureDraft(bool isNewOverride) => CaptureDraft(isNewOverride);

    void IAssetsBaseDisksWorkspaceHost.ApplyEditorDraft(AssetsBaseDiskDraft draft) => ApplyEditorDraft(draft);

    void IAssetsBaseDisksWorkspaceHost.ClearEditorFields() => ClearEditorFields();

    void IAssetsBaseDisksWorkspaceHost.SetSelectedRow(AssetsBaseDiskListRow? row) => _view.AssetsBaseDisksListViewControl.SelectedItem = row;

    void IAssetsBaseDisksWorkspaceHost.ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft) => ApplyWorkspaceState(workspace, canSaveDraft);

    string? IAssetsBaseDisksWorkspaceHost.PickBaseDiskFilePath() => _host.PickBaseDiskFilePath();

    void IAssetsBaseDisksWorkspaceHost.SetDraftPath(string path) => _view.AssetsBaseDisksPathTextBoxControl.Text = path;

    Task<bool> IAssetsBaseDisksWorkspaceHost.ShowRemoveConfirmationDialogAsync(AssetsBaseDiskListRow row, AssetsBaseDiskRemovalAssessment assessment) => _host.ShowRemoveConfirmationDialogAsync(row, assessment);

    private void WireHandlers()
    {
        _view.AssetsBaseDisksListViewControl.SelectionChanged += AssetsBaseDisksListView_SelectionChanged;
        _view.AssetsBaseDisksRefreshButtonControl.Click += AssetsBaseDisksRefreshButton_Click;
        _view.AssetsBaseDisksImportButtonControl.Click += AssetsBaseDisksImportButton_Click;
        _view.AssetsBaseDisksValidateButtonControl.Click += AssetsBaseDisksValidateButton_Click;
        _view.AssetsBaseDisksRemoveButtonControl.Click += AssetsBaseDisksRemoveButton_Click;
        _view.AssetsBaseDisksBrowsePathButtonControl.Click += AssetsBaseDisksBrowsePathButton_Click;
        _view.AssetsBaseDisksSaveMetadataButtonControl.Click += AssetsBaseDisksSaveMetadataButton_Click;
        _view.AssetsBaseDisksOsNameTextBoxControl.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
        _view.AssetsBaseDisksOsVersionTextBoxControl.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
        _view.AssetsBaseDisksGenerationTextBoxControl.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
        _view.AssetsBaseDisksNotesTextBoxControl.TextChanged += AssetsBaseDisksMetadataTextBox_TextChanged;
    }

    private AssetsBaseDiskDraft? CaptureDraft(bool isNewOverride)
    {
        if (!int.TryParse(_view.AssetsBaseDisksGenerationTextBoxControl.Text.Trim(), out var generation) || generation <= 0)
        {
            return null;
        }

        var path = _view.AssetsBaseDisksPathTextBoxControl.Text.Trim();
        var osName = _view.AssetsBaseDisksOsNameTextBoxControl.Text.Trim();
        var osVersion = _view.AssetsBaseDisksOsVersionTextBoxControl.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(osName) || string.IsNullOrWhiteSpace(osVersion))
        {
            return null;
        }

        return new AssetsBaseDiskDraft
        {
            Id = isNewOverride ? null : _workspace.SelectedRow?.Id,
            Path = path,
            OsName = osName,
            OsVersion = osVersion,
            Generation = generation,
            Notes = _view.AssetsBaseDisksNotesTextBoxControl.Text,
            IsNew = isNewOverride
        };
    }

    private void ApplyEditorDraft(AssetsBaseDiskDraft draft)
    {
        _workspace.IsUpdatingEditor = true;
        try
        {
            _view.AssetsBaseDisksOsNameTextBoxControl.Text = draft.OsName;
            _view.AssetsBaseDisksOsVersionTextBoxControl.Text = draft.OsVersion;
            _view.AssetsBaseDisksPathTextBoxControl.Text = draft.Path;
            _view.AssetsBaseDisksGenerationTextBoxControl.Text = draft.Generation > 0 ? draft.Generation.ToString() : string.Empty;
            _view.AssetsBaseDisksNotesTextBoxControl.Text = draft.Notes ?? string.Empty;
        }
        finally
        {
            _workspace.IsUpdatingEditor = false;
        }
    }

    private void ClearEditorFields()
    {
        _workspace.IsUpdatingEditor = true;
        try
        {
            _view.AssetsBaseDisksOsNameTextBoxControl.Text = string.Empty;
            _view.AssetsBaseDisksOsVersionTextBoxControl.Text = string.Empty;
            _view.AssetsBaseDisksPathTextBoxControl.Text = string.Empty;
            _view.AssetsBaseDisksGenerationTextBoxControl.Text = string.Empty;
            _view.AssetsBaseDisksNotesTextBoxControl.Text = string.Empty;
        }
        finally
        {
            _workspace.IsUpdatingEditor = false;
        }
    }

    private void ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft)
    {
        _view.AssetsBaseDisksRefreshButtonControl.IsEnabled = !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving;
        _view.AssetsBaseDisksImportButtonControl.IsEnabled = !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving;
        _view.AssetsBaseDisksValidateButtonControl.IsEnabled = !workspace.IsLoading && workspace.SelectedRow is not null;
        _view.AssetsBaseDisksRemoveButtonControl.IsEnabled = !workspace.IsLoading && !workspace.IsRemoving && workspace.SelectedRow is not null;
        _view.AssetsBaseDisksBrowsePathButtonControl.IsEnabled = !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving;
        _view.AssetsBaseDisksSaveMetadataButtonControl.IsEnabled = !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving && canSaveDraft;
        _view.AssetsBaseDisksLoadingStatePanelControl.Visibility = workspace.IsLoading ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        _view.AssetsBaseDisksEmptyStatePanelControl.Visibility = !workspace.IsLoading && workspace.Inventory.Count == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        _view.AssetsBaseDisksErrorStatePanelControl.Visibility = workspace.HasErrorState ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        _view.AssetsBaseDisksStatusTextBlockControl.Text = workspace.StatusText;
        _view.AssetsBaseDisksSelectedDiskSummaryTextBlockControl.Text = workspace.SelectedDiskSummaryText;
        _view.AssetsBaseDisksSelectedDiskValidationTextBlockControl.Text = workspace.SelectedDiskValidationText;
        _view.AssetsBaseDisksReferenceWarningTextBlockControl.Text = workspace.ReferenceWarningText;
        _view.AssetsBaseDisksErrorStateTextBlockControl.Text = workspace.ErrorStateText;
        _view.AssetsBaseDisksLoadingStateTextBlockControl.Text = workspace.IsLoading
            ? "Loading base disk catalog. Current details remain visible until refresh completes."
            : "Base disk catalog is idle.";
        _view.AssetsBaseDisksEmptyStateTextBlockControl.Text = "No base disks are registered. Use Import / Register to choose a VHDX and then save its metadata.";
        var showDetailsMessages = workspace.SelectedRow is not null || workspace.PendingDraft is not null;
        _view.AssetsBaseDisksSelectedDiskValidationTextBlockControl.Visibility = showDetailsMessages ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        _view.AssetsBaseDisksReferenceWarningTextBlockControl.Visibility = showDetailsMessages ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private void AssetsBaseDisksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _controller.HandleSelectionChanged(_view.AssetsBaseDisksListViewControl.SelectedItem as AssetsBaseDiskListRow);
    }

    private async void AssetsBaseDisksRefreshButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await _controller.EnsureInventoryAsync(forceRefresh: true);
    }

    private void AssetsBaseDisksImportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _controller.BeginImport();
    }

    private async void AssetsBaseDisksValidateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await _controller.ValidateAsync();
    }

    private async void AssetsBaseDisksRemoveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await _controller.RemoveSelectedAsync();
    }

    private void AssetsBaseDisksBrowsePathButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _controller.HandleBrowsePath();
    }

    private async void AssetsBaseDisksSaveMetadataButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await _controller.SaveDraftAsync();
    }

    private void AssetsBaseDisksMetadataTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _controller.HandleMetadataChanged();
    }
}

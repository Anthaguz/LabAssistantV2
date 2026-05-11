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
        _view.SetInventorySource(_workspace.Inventory);
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

    void IAssetsBaseDisksWorkspaceHost.ApplyEditorDraft(AssetsBaseDiskDraft draft) => _view.ApplyEditorDraft(draft);

    void IAssetsBaseDisksWorkspaceHost.ClearEditorFields() => _view.ClearEditor();

    void IAssetsBaseDisksWorkspaceHost.SetSelectedRow(AssetsBaseDiskListRow? row) => _view.SetSelectedBaseDisk(row);

    void IAssetsBaseDisksWorkspaceHost.ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft) => ApplyWorkspaceState(workspace, canSaveDraft);

    string? IAssetsBaseDisksWorkspaceHost.PickBaseDiskFilePath() => _host.PickBaseDiskFilePath();

    void IAssetsBaseDisksWorkspaceHost.SetDraftPath(string path) => _view.SetDraftPath(path);

    Task<bool> IAssetsBaseDisksWorkspaceHost.ShowRemoveConfirmationDialogAsync(AssetsBaseDiskListRow row, AssetsBaseDiskRemovalAssessment assessment) => _host.ShowRemoveConfirmationDialogAsync(row, assessment);

    private void WireHandlers()
    {
        _view.SelectedBaseDiskChanged += AssetsBaseDisksListView_SelectionChanged;
        _view.RefreshRequested += AssetsBaseDisksRefreshRequested;
        _view.ImportRequested += AssetsBaseDisksImportRequested;
        _view.ValidateRequested += AssetsBaseDisksValidateRequested;
        _view.RemoveRequested += AssetsBaseDisksRemoveRequested;
        _view.BrowsePathRequested += AssetsBaseDisksBrowsePathRequested;
        _view.SaveMetadataRequested += AssetsBaseDisksSaveMetadataRequested;
        _view.MetadataChanged += AssetsBaseDisksMetadataChanged;
    }

    private AssetsBaseDiskDraft? CaptureDraft(bool isNewOverride)
    {
        var formValues = _view.CaptureFormValues();
        if (!int.TryParse(formValues.GenerationText?.Trim(), out var generation) || generation <= 0)
        {
            return null;
        }

        var path = formValues.PathText?.Trim() ?? string.Empty;
        var osName = formValues.OsNameText?.Trim() ?? string.Empty;
        var osVersion = formValues.OsVersionText?.Trim() ?? string.Empty;
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
            Notes = formValues.NotesText,
            IsNew = isNewOverride
        };
    }

    private void ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft)
    {
        _view.UpdateWorkspaceState(BuildViewState(workspace, canSaveDraft));
    }

    private static AssetsBaseDisksViewState BuildViewState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft)
    {
        var showDetailsMessages = workspace.SelectedRow is not null || workspace.PendingDraft is not null;
        return new AssetsBaseDisksViewState(
            CanRefresh: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving,
            CanImport: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving,
            CanValidate: !workspace.IsLoading && workspace.SelectedRow is not null,
            CanRemove: !workspace.IsLoading && !workspace.IsRemoving && workspace.SelectedRow is not null,
            CanBrowsePath: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving,
            CanSaveMetadata: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsRemoving && canSaveDraft,
            IsLoadingVisible: workspace.IsLoading,
            IsEmptyVisible: !workspace.IsLoading && workspace.Inventory.Count == 0,
            IsErrorVisible: workspace.HasErrorState,
            ShowDetailsMessages: showDetailsMessages,
            StatusText: workspace.StatusText,
            SelectedDiskSummaryText: workspace.SelectedDiskSummaryText,
            SelectedDiskValidationText: workspace.SelectedDiskValidationText,
            ReferenceWarningText: workspace.ReferenceWarningText,
            ErrorStateText: workspace.ErrorStateText,
            LoadingStateText: workspace.IsLoading
                ? "Loading base disk catalog. Current details remain visible until refresh completes."
                : "Base disk catalog is idle.",
            EmptyStateText: "No base disks are registered. Use Import / Register to choose a VHDX and then save its metadata.");
    }

    private void AssetsBaseDisksListView_SelectionChanged(object? sender, EventArgs e)
    {
        _controller.HandleSelectionChanged(_view.SelectedBaseDisk);
    }

    private async void AssetsBaseDisksRefreshRequested(object? sender, EventArgs e)
    {
        await _controller.EnsureInventoryAsync(forceRefresh: true);
    }

    private void AssetsBaseDisksImportRequested(object? sender, EventArgs e)
    {
        _controller.BeginImport();
    }

    private async void AssetsBaseDisksValidateRequested(object? sender, EventArgs e)
    {
        await _controller.ValidateAsync();
    }

    private async void AssetsBaseDisksRemoveRequested(object? sender, EventArgs e)
    {
        await _controller.RemoveSelectedAsync();
    }

    private void AssetsBaseDisksBrowsePathRequested(object? sender, EventArgs e)
    {
        _controller.HandleBrowsePath();
    }

    private async void AssetsBaseDisksSaveMetadataRequested(object? sender, EventArgs e)
    {
        await _controller.SaveDraftAsync();
    }

    private void AssetsBaseDisksMetadataChanged(object? sender, EventArgs e)
    {
        _controller.HandleMetadataChanged();
    }
}

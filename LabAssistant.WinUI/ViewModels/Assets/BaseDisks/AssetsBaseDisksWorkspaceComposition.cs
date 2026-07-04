using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.Views.Assets;

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
    private readonly AssetsBaseDisksViewModel _viewModel;
    private readonly IAssetsBaseDisksCompositionHost _host;

    public AssetsBaseDisksWorkspaceComposition(
        IAssetsBaseDisksCapabilityService capabilityService,
        AssetsBaseDisksView view,
        IAssetsBaseDisksCompositionHost host)
    {
        _viewModel = view.ViewModel;
        _host = host;
        _viewModel.PickBaseDiskFilePath = _host.PickBaseDiskFilePath;
        _viewModel.ConfirmRemoveAsync = (item, assessment) =>
            _host.ShowRemoveConfirmationDialogAsync(
                new AssetsBaseDiskListRow(new AssetsBaseDiskRecord
                {
                    Id = item.Id,
                    Path = item.Path,
                    OsName = item.OsName,
                    OsVersion = item.OsVersion,
                    Generation = item.Generation,
                    Notes = item.Notes
                }),
                assessment);
    }

    public bool IsLoading => _viewModel.IsLoading;

    public int InventoryCount => _viewModel.BaseDisks.Count;

    public Task EnsureInventoryAsync(bool forceRefresh) => _viewModel.EnsureInventoryAsync(forceRefresh);

    public void ApplyShellState()
    {
        _ = _viewModel.EnsureInventoryAsync(forceRefresh: false);
    }

    AssetsBaseDiskDraft? IAssetsBaseDisksWorkspaceHost.CaptureDraft(bool isNewOverride) => CaptureDraft(isNewOverride);

    void IAssetsBaseDisksWorkspaceHost.ApplyEditorDraft(AssetsBaseDiskDraft draft)
    {
    }

    void IAssetsBaseDisksWorkspaceHost.ClearEditorFields()
    {
    }

    void IAssetsBaseDisksWorkspaceHost.SetSelectedRow(AssetsBaseDiskListRow? row)
    {
    }

    void IAssetsBaseDisksWorkspaceHost.ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft)
    {
    }

    string? IAssetsBaseDisksWorkspaceHost.PickBaseDiskFilePath() => _host.PickBaseDiskFilePath();

    void IAssetsBaseDisksWorkspaceHost.SetDraftPath(string path)
    {
    }

    Task<bool> IAssetsBaseDisksWorkspaceHost.ShowRemoveConfirmationDialogAsync(AssetsBaseDiskListRow row, AssetsBaseDiskRemovalAssessment assessment) => _host.ShowRemoveConfirmationDialogAsync(row, assessment);

    private AssetsBaseDiskDraft? CaptureDraft(bool isNewOverride)
    {
        if (!int.TryParse(_viewModel.GenerationText?.Trim(), out var generation) || generation <= 0)
        {
            return null;
        }

        var path = _viewModel.DiskPath?.Trim() ?? string.Empty;
        var osName = _viewModel.OsName?.Trim() ?? string.Empty;
        var osVersion = _viewModel.OsVersion?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(osName) || string.IsNullOrWhiteSpace(osVersion))
        {
            return null;
        }

        return new AssetsBaseDiskDraft
        {
            Id = isNewOverride ? null : _viewModel.SelectedDisk?.Id,
            Path = path,
            OsName = osName,
            OsVersion = osVersion,
            Generation = generation,
            Notes = _viewModel.Notes,
            IsNew = isNewOverride
        };
    }
}

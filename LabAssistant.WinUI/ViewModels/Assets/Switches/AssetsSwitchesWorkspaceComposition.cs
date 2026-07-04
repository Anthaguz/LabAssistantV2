using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.Views.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal interface IAssetsSwitchesCompositionHost
{
    Task<bool> ShowDeleteConfirmationDialogAsync(AssetsSwitchListRow row, AssetsSwitchDeleteAssessment assessment);
}

internal sealed class AssetsSwitchesCompositionHost : IAssetsSwitchesCompositionHost
{
    private readonly Func<AssetsSwitchListRow, AssetsSwitchDeleteAssessment, Task<bool>> _showDeleteConfirmationDialogAsync;

    public AssetsSwitchesCompositionHost(Func<AssetsSwitchListRow, AssetsSwitchDeleteAssessment, Task<bool>> showDeleteConfirmationDialogAsync)
    {
        _showDeleteConfirmationDialogAsync = showDeleteConfirmationDialogAsync;
    }

    public Task<bool> ShowDeleteConfirmationDialogAsync(AssetsSwitchListRow row, AssetsSwitchDeleteAssessment assessment)
        => _showDeleteConfirmationDialogAsync(row, assessment);
}

internal sealed class AssetsSwitchesWorkspaceComposition : IAssetsSwitchesWorkspaceHost
{
    private readonly AssetsSwitchesViewModel _viewModel;
    private readonly IAssetsSwitchesCompositionHost _host;

    public AssetsSwitchesWorkspaceComposition(
        IAssetsSwitchesCapabilityService capabilityService,
        AssetsSwitchesView view,
        IAssetsSwitchesCompositionHost host)
    {
        _viewModel = view.ViewModel;
        _host = host;
        _viewModel.ConfirmDeleteAsync = (item, assessment) =>
            _host.ShowDeleteConfirmationDialogAsync(
                new AssetsSwitchListRow(new AssetsSwitchRecord
                {
                    Name = item.Name,
                    SwitchType = item.Type,
                    AdapterName = item.AdapterName
                }),
                assessment);
    }

    public bool IsLoading => _viewModel.IsLoading;

    public int InventoryCount => _viewModel.Switches.Count;

    public Task EnsureInventoryAsync(bool forceRefresh) => _viewModel.EnsureInventoryAsync(forceRefresh);

    public void ApplyShellState()
    {
        _ = _viewModel.EnsureInventoryAsync(forceRefresh: false);
    }

    AssetsSwitchDraft? IAssetsSwitchesWorkspaceHost.CaptureDraft(bool isNewOverride) => CaptureDraft(isNewOverride);

    string IAssetsSwitchesWorkspaceHost.GetSelectedSwitchType() => _viewModel.SelectedType;

    void IAssetsSwitchesWorkspaceHost.ApplyEditorDraft(AssetsSwitchDraft draft)
    {
    }

    void IAssetsSwitchesWorkspaceHost.ClearEditorFields()
    {
    }

    void IAssetsSwitchesWorkspaceHost.SetSelectedRow(AssetsSwitchListRow? row)
    {
    }

    void IAssetsSwitchesWorkspaceHost.ApplyWorkspaceState(AssetsSwitchesWorkspaceViewModel workspace, bool canValidateOrApply, bool isExternalSwitchTypeSelected)
    {
    }

    Task<bool> IAssetsSwitchesWorkspaceHost.ShowDeleteConfirmationDialogAsync(AssetsSwitchListRow row, AssetsSwitchDeleteAssessment assessment)
        => _host.ShowDeleteConfirmationDialogAsync(row, assessment);

    private AssetsSwitchDraft CaptureDraft(bool isNewOverride)
    {
        return new AssetsSwitchDraft
        {
            IsNew = isNewOverride,
            OriginalName = isNewOverride ? null : _viewModel.SelectedSwitch?.Name,
            Name = _viewModel.SwitchName.Trim(),
            SwitchType = _viewModel.SelectedType,
            AdapterName = _viewModel.AdapterName
        };
    }
}

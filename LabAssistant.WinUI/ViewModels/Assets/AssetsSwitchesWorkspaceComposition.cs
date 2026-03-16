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
    private readonly AssetsSwitchesView _view;
    private readonly AssetsSwitchesWorkspaceViewModel _workspace = new();
    private readonly AssetsSwitchesWorkspaceController _controller;
    private readonly IAssetsSwitchesCompositionHost _host;

    public AssetsSwitchesWorkspaceComposition(
        IAssetsSwitchesCapabilityService capabilityService,
        AssetsSwitchesView view,
        IAssetsSwitchesCompositionHost host)
    {
        _view = view;
        _host = host;
        _controller = new AssetsSwitchesWorkspaceController(capabilityService, _workspace, this);
        _view.SetInventorySource(_workspace.Inventory);
        _view.SetAttachedVmSource(_workspace.AttachedVmNames);
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

    AssetsSwitchDraft? IAssetsSwitchesWorkspaceHost.CaptureDraft(bool isNewOverride) => CaptureDraft(isNewOverride);

    string IAssetsSwitchesWorkspaceHost.GetSelectedSwitchType() => _view.GetSelectedSwitchType();

    void IAssetsSwitchesWorkspaceHost.ApplyEditorDraft(AssetsSwitchDraft draft) => _view.ApplyEditorDraft(draft);

    void IAssetsSwitchesWorkspaceHost.ClearEditorFields() => _view.ClearEditor();

    void IAssetsSwitchesWorkspaceHost.SetSelectedRow(AssetsSwitchListRow? row) => _view.SetSelectedSwitch(row);

    void IAssetsSwitchesWorkspaceHost.ApplyWorkspaceState(AssetsSwitchesWorkspaceViewModel workspace, bool canValidateOrApply, bool isExternalSwitchTypeSelected)
        => ApplyWorkspaceState(workspace, canValidateOrApply, isExternalSwitchTypeSelected);

    Task<bool> IAssetsSwitchesWorkspaceHost.ShowDeleteConfirmationDialogAsync(AssetsSwitchListRow row, AssetsSwitchDeleteAssessment assessment)
        => _host.ShowDeleteConfirmationDialogAsync(row, assessment);

    private void WireHandlers()
    {
        _view.SelectedSwitchChanged += AssetsSwitchesListView_SelectionChanged;
        _view.RefreshRequested += AssetsSwitchesRefreshRequested;
        _view.CreateRequested += AssetsSwitchesCreateRequested;
        _view.ApplyRequested += AssetsSwitchesApplyRequested;
        _view.DeleteRequested += AssetsSwitchesDeleteRequested;
        _view.EditorChanged += AssetsSwitchesEditorChanged;
    }

    private AssetsSwitchDraft CaptureDraft(bool isNewOverride)
    {
        var formValues = _view.CaptureFormValues();
        return new AssetsSwitchDraft
        {
            IsNew = isNewOverride,
            OriginalName = isNewOverride ? null : _workspace.SelectedRow?.Name,
            Name = formValues.NameText.Trim(),
            SwitchType = formValues.SelectedSwitchType,
            AdapterName = formValues.AdapterNameText
        };
    }

    private void ApplyWorkspaceState(AssetsSwitchesWorkspaceViewModel workspace, bool canValidateOrApply, bool isExternalSwitchTypeSelected)
    {
        _view.UpdateWorkspaceState(BuildViewState(workspace, canValidateOrApply, isExternalSwitchTypeSelected));
    }

    private static AssetsSwitchesViewState BuildViewState(
        AssetsSwitchesWorkspaceViewModel workspace,
        bool canValidateOrApply,
        bool isExternalSwitchTypeSelected)
    {
        var isEditingExisting = workspace.SelectedRow is not null && workspace.PendingDraft is null;
        return new AssetsSwitchesViewState(
            CanRefresh: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsDeleting,
            CanCreate: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsDeleting,
            CanApply: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsDeleting && canValidateOrApply,
            CanDelete: !workspace.IsLoading && !workspace.IsSaving && !workspace.IsDeleting && workspace.SelectedRow is not null,
            CanEditSwitchType: !isEditingExisting && !workspace.IsSaving && !workspace.IsDeleting,
            CanEditAdapter: !isEditingExisting && !workspace.IsSaving && !workspace.IsDeleting && isExternalSwitchTypeSelected,
            IsLoadingVisible: workspace.IsLoading,
            IsEmptyVisible: !workspace.IsLoading && workspace.Inventory.Count == 0 && !workspace.HasErrorState,
            IsErrorVisible: workspace.HasErrorState,
            StatusText: workspace.StatusText,
            SelectedSwitchValidationText: workspace.SelectedSwitchValidationText,
            DeleteConstraintText: workspace.DeleteConstraintText,
            AttachedVmHintText: workspace.AttachedVmHintText,
            ErrorStateText: workspace.ErrorStateText,
            LoadingStateText: workspace.IsLoading
                ? "Loading current Hyper-V virtual switches. Current details remain visible until refresh completes."
                : "Virtual switch inventory is idle.",
            EmptyStateText: "No virtual switches were found on this host. Click Create to prepare a new switch.");
    }

    private async void AssetsSwitchesListView_SelectionChanged(object? sender, EventArgs e)
    {
        await _controller.HandleSelectionChangedAsync(_view.SelectedSwitch);
    }

    private async void AssetsSwitchesRefreshRequested(object? sender, EventArgs e)
    {
        await _controller.EnsureInventoryAsync(forceRefresh: true);
    }

    private async void AssetsSwitchesCreateRequested(object? sender, EventArgs e)
    {
        await _controller.BeginCreateAsync();
    }

    private async void AssetsSwitchesApplyRequested(object? sender, EventArgs e)
    {
        await _controller.SaveDraftAsync();
    }

    private async void AssetsSwitchesDeleteRequested(object? sender, EventArgs e)
    {
        await _controller.DeleteSelectedAsync();
    }

    private async void AssetsSwitchesEditorChanged(object? sender, EventArgs e)
    {
        await _controller.HandleEditorChangedAsync();
    }
}

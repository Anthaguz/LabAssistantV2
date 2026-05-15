using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal interface IAssetsSwitchesWorkspaceHost
{
    AssetsSwitchDraft? CaptureDraft(bool isNewOverride);

    string GetSelectedSwitchType();

    void ApplyEditorDraft(AssetsSwitchDraft draft);

    void ClearEditorFields();

    void SetSelectedRow(AssetsSwitchListRow? row);

    void ApplyWorkspaceState(AssetsSwitchesWorkspaceViewModel workspace, bool canValidateOrApply, bool isExternalSwitchTypeSelected);

    Task<bool> ShowDeleteConfirmationDialogAsync(AssetsSwitchListRow row, AssetsSwitchDeleteAssessment assessment);
}

internal sealed class AssetsSwitchesWorkspaceController
{
    private readonly IAssetsSwitchesCapabilityService _capabilityService;
    private readonly AssetsSwitchesWorkspaceViewModel _workspace;
    private readonly IAssetsSwitchesWorkspaceHost _host;
    private readonly AssetsSwitchesEditorWorkflow _editorWorkflow;

    public AssetsSwitchesWorkspaceController(
        IAssetsSwitchesCapabilityService capabilityService,
        AssetsSwitchesWorkspaceViewModel workspace,
        IAssetsSwitchesWorkspaceHost host)
    {
        _capabilityService = capabilityService;
        _workspace = workspace;
        _host = host;
        _editorWorkflow = new AssetsSwitchesEditorWorkflow(capabilityService, workspace, host);
    }

    public async Task EnsureInventoryAsync(bool forceRefresh)
    {
        if (_workspace.IsLoading)
        {
            return;
        }

        if (!forceRefresh && (_workspace.Inventory.Count > 0 || _workspace.PendingDraft is not null))
        {
            ApplyWorkspaceState();
            return;
        }

        _workspace.IsLoading = true;
        _workspace.StatusText = forceRefresh ? "Refreshing virtual switches..." : "Loading virtual switches...";
        ApplyWorkspaceState();

        try
        {
            var previousSelectionName = _workspace.SelectedRow?.Name;
            var result = await _capabilityService.LoadAsync(isRefresh: forceRefresh);

            _workspace.Inventory.Clear();
            foreach (var item in result.Items)
            {
                _workspace.Inventory.Add(new AssetsSwitchListRow(item));
            }

            if (result.Errors.Count > 0)
            {
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = $"Switch inventory load completed with issues:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors)}";
                _workspace.StatusText = $"Loaded {_workspace.Inventory.Count} switch(es) with {result.Errors.Count} issue(s). Review the error panel and refresh after correcting the host state.";
            }
            else
            {
                _editorWorkflow.ClearErrorState();
            }

            if (_workspace.PendingDraft is null)
            {
                var matchedSelection = !string.IsNullOrWhiteSpace(previousSelectionName)
                    ? _workspace.Inventory.FirstOrDefault(row => string.Equals(row.Name, previousSelectionName, StringComparison.OrdinalIgnoreCase))
                    : null;
                var selectedRow = matchedSelection ?? _workspace.Inventory.FirstOrDefault();
                _workspace.SelectedRow = selectedRow;
                _host.SetSelectedRow(selectedRow);
            }
            else
            {
                _workspace.SelectedRow = null;
                _host.SetSelectedRow(null);
            }

            await _editorWorkflow.RestoreAfterInventoryRefreshAsync(forceRefresh, previousSelectionName);
        }
        finally
        {
            _workspace.IsLoading = false;
            _editorWorkflow.ApplyWorkspaceState();
        }
    }

    public async Task HandleSelectionChangedAsync(AssetsSwitchListRow? selectedRow)
    {
        await _editorWorkflow.HandleSelectionChangedAsync(selectedRow);
    }

    public async Task BeginCreateAsync()
    {
        await _editorWorkflow.BeginCreateAsync();
    }

    public async Task SaveDraftAsync()
    {
        await _editorWorkflow.SaveDraftAsync(() => EnsureInventoryAsync(forceRefresh: true));
    }

    public async Task DeleteSelectedAsync()
    {
        if (_workspace.SelectedRow is null)
        {
            _workspace.StatusText = "Select a virtual switch to delete.";
            ApplyWorkspaceState();
            return;
        }

        _workspace.IsDeleting = true;
        ApplyWorkspaceState();

        try
        {
            var knownInventory = _workspace.Inventory
                .Select(row => new AssetsSwitchRecord
                {
                    Name = row.Name,
                    SwitchType = row.SwitchType,
                    AdapterName = row.AdapterName
                })
                .ToList();
            var assessment = await _capabilityService.AssessDeleteAsync(_workspace.SelectedRow.Name, knownInventory);
            _editorWorkflow.ApplyDeleteAssessment(assessment);

            if (!assessment.CanDelete)
            {
                _workspace.StatusText = "Delete blocked. Disconnect the attached VMs from this switch and refresh before trying again.";
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = $"Delete is blocked for '{_workspace.SelectedRow.Name}'.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", assessment.BlockingReasons.DefaultIfEmpty("At least one VM is attached to this switch."))}";
                ApplyWorkspaceState();
                return;
            }

            if (!await _host.ShowDeleteConfirmationDialogAsync(_workspace.SelectedRow, assessment))
            {
                _workspace.StatusText = "Virtual switch delete canceled.";
                ApplyWorkspaceState();
                return;
            }

            var result = await _capabilityService.DeleteAsync(_workspace.SelectedRow.Name, assessment);
            _workspace.StatusText = result.UserMessage;
            if (!result.Success)
            {
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = $"Delete failed. Review the details below before trying again.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors.DefaultIfEmpty(result.UserMessage))}";
            }
            else
            {
                _editorWorkflow.ClearErrorState();
                _workspace.SelectedRow = null;
                _workspace.PendingDraft = null;
            }

            await EnsureInventoryAsync(forceRefresh: true);
        }
        finally
        {
            _workspace.IsDeleting = false;
            _editorWorkflow.ApplyWorkspaceState();
        }
    }

    public async Task HandleEditorChangedAsync()
    {
        await _editorWorkflow.HandleEditorChangedAsync();
    }

    public void ApplyWorkspaceState() => _editorWorkflow.ApplyWorkspaceState();
}

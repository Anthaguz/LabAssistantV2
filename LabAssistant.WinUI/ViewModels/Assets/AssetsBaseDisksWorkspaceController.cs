using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal interface IAssetsBaseDisksWorkspaceHost
{
    AssetsBaseDiskDraft? CaptureDraft(bool isNewOverride);

    void ApplyEditorDraft(AssetsBaseDiskDraft draft);

    void ClearEditorFields();

    void SetSelectedRow(AssetsBaseDiskListRow? row);

    void ApplyWorkspaceState(AssetsBaseDisksWorkspaceViewModel workspace, bool canSaveDraft);

    string? PickBaseDiskFilePath();

    void SetDraftPath(string path);

    Task<bool> ShowRemoveConfirmationDialogAsync(AssetsBaseDiskListRow row, AssetsBaseDiskRemovalAssessment assessment);
}

internal sealed class AssetsBaseDisksWorkspaceController
{
    private readonly IAssetsBaseDisksCapabilityService _capabilityService;
    private readonly AssetsBaseDisksWorkspaceViewModel _workspace;
    private readonly IAssetsBaseDisksWorkspaceHost _host;
    private readonly AssetsBaseDisksEditorWorkflow _editorWorkflow;

    public AssetsBaseDisksWorkspaceController(
        IAssetsBaseDisksCapabilityService capabilityService,
        AssetsBaseDisksWorkspaceViewModel workspace,
        IAssetsBaseDisksWorkspaceHost host)
    {
        _capabilityService = capabilityService;
        _workspace = workspace;
        _host = host;
        _editorWorkflow = new AssetsBaseDisksEditorWorkflow(capabilityService, workspace, host);
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
        _workspace.StatusText = forceRefresh
            ? "Refreshing base disks..."
            : "Loading base disks...";
        ApplyWorkspaceState();

        try
        {
            var result = await _capabilityService.LoadAsync(isRefresh: forceRefresh);
            var previouslySelectedId = _workspace.SelectedRow?.Id;
            _workspace.Inventory.Clear();
            foreach (var item in result.Items)
            {
                _workspace.Inventory.Add(new AssetsBaseDiskListRow(item));
            }

            if (_workspace.PendingDraft is null)
            {
                var selectedRow = _workspace.Inventory.FirstOrDefault(row => string.Equals(row.Id, previouslySelectedId, StringComparison.OrdinalIgnoreCase))
                    ?? _workspace.Inventory.FirstOrDefault();
                _workspace.SelectedRow = selectedRow;
                _host.SetSelectedRow(selectedRow);
            }
            else
            {
                _workspace.SelectedRow = null;
                _host.SetSelectedRow(null);
            }

            _workspace.StatusText = result.Errors.Count > 0
                ? $"Loaded {_workspace.Inventory.Count} base disk(s) with {result.Errors.Count} issue(s). Review the error panel and use Refresh after correcting the catalog."
                : $"Loaded {_workspace.Inventory.Count} base disk(s).";
            _workspace.HasErrorState = result.Errors.Count > 0;
            _workspace.ErrorStateText = result.Errors.Count > 0
                ? $"Catalog load completed with issues:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors)}"
                : "No catalog load errors.";

            _editorWorkflow.RestoreAfterInventoryRefresh();
        }
        finally
        {
            _workspace.IsLoading = false;
            _editorWorkflow.ApplyWorkspaceState();
        }
    }

    public void HandleSelectionChanged(AssetsBaseDiskListRow? selectedRow) => _editorWorkflow.HandleSelectionChanged(selectedRow);

    public void BeginImport() => _editorWorkflow.BeginImport();

    public async Task ValidateAsync()
    {
        await _editorWorkflow.ValidateAsync();
    }

    public async Task RemoveSelectedAsync()
    {
        if (_workspace.SelectedRow is null)
        {
            _workspace.StatusText = "Select a base disk to remove.";
            ApplyWorkspaceState();
            return;
        }

        _workspace.IsRemoving = true;
        ApplyWorkspaceState();

        try
        {
            var assessment = await _capabilityService.AssessRemoveAsync(_workspace.SelectedRow.Id);
            _workspace.SelectedRow.ReferenceSummary = assessment.ReferenceSignalSummary;
            _workspace.ReferenceWarningText = string.Join(Environment.NewLine, assessment.WarningReasons.DefaultIfEmpty(assessment.ReferenceSignalSummary));
            if (!assessment.Exists || !assessment.CanRemove)
            {
                _workspace.StatusText = assessment.BlockingReasons.FirstOrDefault() ?? "Base disk removal is blocked. Resolve the issue and try again.";
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = string.Join(Environment.NewLine, assessment.BlockingReasons.DefaultIfEmpty("Base disk removal is blocked."));
                ApplyWorkspaceState();
                return;
            }

            if (!await _host.ShowRemoveConfirmationDialogAsync(_workspace.SelectedRow, assessment))
            {
                _workspace.StatusText = "Base disk removal canceled.";
                ApplyWorkspaceState();
                return;
            }

            var result = await _capabilityService.RemoveAsync(_workspace.SelectedRow.Id);
            _workspace.StatusText = result.UserMessage;
            if (!result.Success)
            {
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = $"Remove failed. {string.Join(Environment.NewLine, result.Errors.DefaultIfEmpty(result.UserMessage))}";
            }
            else
            {
                _workspace.HasErrorState = false;
                _workspace.ErrorStateText = "No catalog load errors.";
                _workspace.PendingDraft = null;
            }

            await EnsureInventoryAsync(forceRefresh: true);
        }
        finally
        {
            _workspace.IsRemoving = false;
            _editorWorkflow.ApplyWorkspaceState();
        }
    }

    public void HandleBrowsePath() => _editorWorkflow.HandleBrowsePath();

    public async Task SaveDraftAsync()
    {
        await _editorWorkflow.SaveDraftAsync(() => EnsureInventoryAsync(forceRefresh: true));
    }

    public void HandleMetadataChanged() => _editorWorkflow.HandleMetadataChanged();

    public void ApplyWorkspaceState() => _editorWorkflow.ApplyWorkspaceState();
}

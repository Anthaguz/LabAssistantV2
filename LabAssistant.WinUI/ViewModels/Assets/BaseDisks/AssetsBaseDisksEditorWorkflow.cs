using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal sealed class AssetsBaseDisksEditorWorkflow
{
    private readonly IAssetsBaseDisksCapabilityService _capabilityService;
    private readonly AssetsBaseDisksWorkspaceViewModel _workspace;
    private readonly IAssetsBaseDisksWorkspaceHost _host;

    public AssetsBaseDisksEditorWorkflow(
        IAssetsBaseDisksCapabilityService capabilityService,
        AssetsBaseDisksWorkspaceViewModel workspace,
        IAssetsBaseDisksWorkspaceHost host)
    {
        _capabilityService = capabilityService;
        _workspace = workspace;
        _host = host;
    }

    public void RestoreAfterInventoryRefresh()
    {
        if (_workspace.SelectedRow is not null)
        {
            LoadEditorFromRow(_workspace.SelectedRow);
            return;
        }

        if (_workspace.PendingDraft is not null)
        {
            LoadEditorFromDraft(_workspace.PendingDraft);
            _workspace.SelectedDiskSummaryText = "New base disk draft. Review metadata, validate, then save to register it.";
            _workspace.SelectedDiskValidationText = "Validation has not been evaluated for this draft yet.";
            _workspace.ReferenceWarningText = "Removal assessment is only available for registered base disks.";
            return;
        }

        ClearEditor();
    }

    public void HandleSelectionChanged(AssetsBaseDiskListRow? selectedRow)
    {
        _workspace.SelectedRow = selectedRow;
        if (selectedRow is not null)
        {
            LoadEditorFromRow(selectedRow);
        }
        else
        {
            ClearEditor();
        }

        ApplyWorkspaceState();
    }

    public void BeginImport()
    {
        var selectedPath = _host.PickBaseDiskFilePath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        _workspace.SelectedRow = null;
        _workspace.PendingDraft = null;
        _host.SetSelectedRow(null);
        LoadEditorFromDraft(new AssetsBaseDiskDraft
        {
            IsNew = true,
            Path = selectedPath,
            OsName = Path.GetFileNameWithoutExtension(selectedPath),
            OsVersion = string.Empty,
            Generation = 1,
            Notes = null
        });
        _workspace.PendingDraft = _host.CaptureDraft(isNewOverride: true);
        _workspace.StatusText = "Selected VHDX path. Review metadata, validate, and click Save Metadata to register the base disk.";
        _workspace.SelectedDiskValidationText = "Validation has not been evaluated for this new draft yet.";
        _workspace.ReferenceWarningText = "New base disk draft. Removal assessment is not applicable.";
        ApplyWorkspaceState();
    }

    public async Task ValidateAsync()
    {
        var draft = _host.CaptureDraft(isNewOverride: _workspace.SelectedRow is null);
        if (draft is null)
        {
            _workspace.StatusText = "Select or prepare a base disk draft before validating.";
            ApplyWorkspaceState();
            return;
        }

        var validation = await _capabilityService.ValidateAsync(draft);
        ApplyValidationResult(validation);
        _workspace.StatusText = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? "Validation passed. The base disk is ready to use."
            : "Validation blocked. Review the details and correct the metadata or path before saving.";
        ApplyWorkspaceState();
    }

    public void HandleBrowsePath()
    {
        var selectedPath = _host.PickBaseDiskFilePath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        _host.SetDraftPath(selectedPath);
        if (_workspace.SelectedRow is null)
        {
            _workspace.PendingDraft = _host.CaptureDraft(isNewOverride: true);
        }

        _workspace.StatusText = "Updated base disk path. Validate and save metadata to persist the change.";
        ApplyWorkspaceState();
    }

    public async Task SaveDraftAsync(Func<Task> refreshInventoryAsync)
    {
        var draft = _host.CaptureDraft(isNewOverride: _workspace.SelectedRow is null);
        if (draft is null)
        {
            _workspace.StatusText = "Complete required metadata before saving.";
            ApplyWorkspaceState();
            return;
        }

        _workspace.IsSaving = true;
        _workspace.StatusText = draft.IsNew
            ? "Registering base disk..."
            : "Saving base disk metadata...";
        ApplyWorkspaceState();

        try
        {
            var result = await _capabilityService.SaveAsync(draft);
            _workspace.StatusText = result.UserMessage;
            if (!result.Success)
            {
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = $"Save failed. {string.Join(Environment.NewLine, result.Errors.DefaultIfEmpty(result.UserMessage))}";
                ApplyWorkspaceState();
                return;
            }

            _workspace.HasErrorState = false;
            _workspace.ErrorStateText = "No catalog load errors.";
            _workspace.PendingDraft = null;
            await refreshInventoryAsync();
            if (result.Item is not null)
            {
                var selectedRow = _workspace.Inventory.FirstOrDefault(row => string.Equals(row.Id, result.Item.Id, StringComparison.OrdinalIgnoreCase));
                _workspace.SelectedRow = selectedRow;
                _host.SetSelectedRow(selectedRow);
            }

            var validationDraft = new AssetsBaseDiskDraft
            {
                Id = result.Item?.Id ?? draft.Id,
                Path = draft.Path,
                OsName = draft.OsName,
                OsVersion = draft.OsVersion,
                Generation = draft.Generation,
                Notes = draft.Notes,
                IsNew = false
            };
            var validation = await _capabilityService.ValidateAsync(validationDraft);
            ApplyValidationResult(validation);
        }
        finally
        {
            _workspace.IsSaving = false;
            ApplyWorkspaceState();
        }
    }

    public void HandleMetadataChanged()
    {
        if (_workspace.IsUpdatingEditor)
        {
            return;
        }

        _workspace.StatusText = _workspace.SelectedRow is null
            ? "Base disk draft changed. Validate and Save Metadata to register it."
            : "Base disk metadata changed. Validate and Save Metadata to persist changes.";
        if (_workspace.SelectedRow is null)
        {
            _workspace.PendingDraft = _host.CaptureDraft(isNewOverride: true);
        }

        ApplyWorkspaceState();
    }

    public void ApplyWorkspaceState()
    {
        var canSaveDraft = _host.CaptureDraft(isNewOverride: _workspace.SelectedRow is null) is not null;
        _host.ApplyWorkspaceState(_workspace, canSaveDraft);
    }

    private void LoadEditorFromRow(AssetsBaseDiskListRow row)
    {
        LoadEditorFromDraft(new AssetsBaseDiskDraft
        {
            Id = row.Id,
            Path = row.Path,
            OsName = row.OsName,
            OsVersion = row.OsVersion,
            Generation = row.Generation,
            Notes = row.Notes,
            IsNew = false
        });
        _workspace.PendingDraft = null;
        _workspace.SelectedDiskSummaryText = $"Catalog id: {row.Id}{Environment.NewLine}{row.Path}";
        _workspace.SelectedDiskValidationText = row.ValidationSummary;
        _workspace.ReferenceWarningText = row.ReferenceSummary;
    }

    private void LoadEditorFromDraft(AssetsBaseDiskDraft draft)
    {
        _workspace.IsUpdatingEditor = true;
        try
        {
            _host.ApplyEditorDraft(draft);
        }
        finally
        {
            _workspace.IsUpdatingEditor = false;
        }
    }

    private void ClearEditor()
    {
        _workspace.SelectedRow = null;
        _workspace.PendingDraft = null;
        _workspace.IsUpdatingEditor = true;
        try
        {
            _host.ClearEditorFields();
        }
        finally
        {
            _workspace.IsUpdatingEditor = false;
        }

        _workspace.SelectedDiskSummaryText = "Select a base disk or import a VHDX to begin.";
        _workspace.SelectedDiskValidationText = "Validation has not been evaluated.";
        _workspace.ReferenceWarningText = "No removal assessment has been performed.";
    }

    private void ApplyValidationResult(AssetsBaseDiskValidationResult validation)
    {
        var validationText = FormatValidationText(validation);
        _workspace.SelectedDiskValidationText = validationText;
        if (_workspace.SelectedRow is not null)
        {
            _workspace.SelectedRow.ValidationSummary = validationText;
        }
    }

    private static string FormatValidationText(AssetsBaseDiskValidationResult validation)
    {
        var label = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? "Ready"
            : string.Equals(validation.Severity, "Warn", StringComparison.OrdinalIgnoreCase)
                ? "Warning"
                : "Blocking";
        if (validation.Details.Count == 0)
        {
            return $"{label}: {validation.Summary}";
        }

        return $"{label}: {validation.Summary}{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", validation.Details)}";
    }
}

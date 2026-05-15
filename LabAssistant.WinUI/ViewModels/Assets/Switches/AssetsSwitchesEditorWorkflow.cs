using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal sealed class AssetsSwitchesEditorWorkflow
{
    private readonly IAssetsSwitchesCapabilityService _capabilityService;
    private readonly AssetsSwitchesWorkspaceViewModel _workspace;
    private readonly IAssetsSwitchesWorkspaceHost _host;

    public AssetsSwitchesEditorWorkflow(
        IAssetsSwitchesCapabilityService capabilityService,
        AssetsSwitchesWorkspaceViewModel workspace,
        IAssetsSwitchesWorkspaceHost host)
    {
        _capabilityService = capabilityService;
        _workspace = workspace;
        _host = host;
    }

    public async Task RestoreAfterInventoryRefreshAsync(bool forceRefresh, string? previousSelectionName)
    {
        if (_workspace.SelectedRow is not null)
        {
            var selectionName = _workspace.SelectedRow.Name;
            LoadEditorFromRow(_workspace.SelectedRow);
            await LoadAttachedVmNamesAsync(selectionName);
            await RefreshValidationAsync();
            _workspace.StatusText = forceRefresh
                ? "Virtual switch inventory refreshed."
                : $"Loaded {_workspace.Inventory.Count} switch(es).";
            if (!string.IsNullOrWhiteSpace(previousSelectionName)
                && !string.Equals(selectionName, previousSelectionName, StringComparison.OrdinalIgnoreCase))
            {
                _workspace.StatusText = $"Previously selected switch '{previousSelectionName}' is no longer available. Review the refreshed inventory.";
            }

            return;
        }

        if (_workspace.PendingDraft is not null)
        {
            LoadEditorFromDraft(_workspace.PendingDraft);
            _workspace.SelectedSwitchValidationText = "Enter a switch name, choose a type, and provide an adapter for External switches.";
            _workspace.DeleteConstraintText = string.Empty;
            SetAttachedVmState(Array.Empty<string>(), "Attached VMs are shown for existing switches.");
            _workspace.StatusText = forceRefresh
                ? "Virtual switch inventory refreshed. The current new-switch draft was preserved."
                : "Virtual switch inventory loaded. The current new-switch draft was preserved.";
            return;
        }

        ClearEditor();
        _workspace.StatusText = _workspace.Inventory.Count == 0
            ? "No virtual switches were found on this host. Click Create to prepare a new switch."
            : "Select a virtual switch or click New to prepare a new switch draft.";
    }

    public async Task HandleSelectionChangedAsync(AssetsSwitchListRow? selectedRow)
    {
        _workspace.SelectedRow = selectedRow;
        if (selectedRow is not null)
        {
            _workspace.PendingDraft = null;
            ClearErrorState();
            LoadEditorFromRow(selectedRow);
            await LoadAttachedVmNamesAsync(selectedRow.Name);
            await RefreshValidationAsync();
        }
        else if (_workspace.PendingDraft is null)
        {
            ClearEditor();
        }

        ApplyWorkspaceState();
    }

    public async Task BeginCreateAsync()
    {
        _workspace.SelectedRow = null;
        _host.SetSelectedRow(null);
        ClearErrorState();
        _workspace.PendingDraft = new AssetsSwitchDraft
        {
            IsNew = true,
            SwitchType = "External"
        };
        LoadEditorFromDraft(_workspace.PendingDraft);
        _workspace.StatusText = "Preparing a new virtual switch draft. Complete the fields and apply when the inline validation state is ready.";
        _workspace.SelectedSwitchValidationText = "Enter a switch name, choose a type, and provide an adapter for External switches.";
        _workspace.DeleteConstraintText = string.Empty;
        SetAttachedVmState(Array.Empty<string>(), "Attached VMs are shown for existing switches.");
        await RefreshValidationAsync();
        ApplyWorkspaceState();
    }

    public async Task SaveDraftAsync(Func<Task> refreshInventoryAsync)
    {
        var draft = _host.CaptureDraft(isNewOverride: _workspace.SelectedRow is null);
        if (draft is null)
        {
            _workspace.StatusText = "Complete required switch fields before applying.";
            ApplyWorkspaceState();
            return;
        }

        _workspace.IsSaving = true;
        _workspace.StatusText = draft.IsNew ? "Creating virtual switch..." : "Updating virtual switch...";
        ApplyWorkspaceState();

        try
        {
            var result = await _capabilityService.SaveAsync(draft);
            _workspace.StatusText = result.UserMessage;
            if (!result.Success)
            {
                _workspace.HasErrorState = true;
                _workspace.ErrorStateText = $"Switch save failed. Review the message below, correct the configuration, and try Apply again.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors.DefaultIfEmpty(result.UserMessage))}";
                ApplyWorkspaceState();
                return;
            }

            ClearErrorState();
            _workspace.PendingDraft = null;
            await refreshInventoryAsync();
            if (result.Item is not null)
            {
                var selectedRow = _workspace.Inventory.FirstOrDefault(row => string.Equals(row.Name, result.Item.Name, StringComparison.OrdinalIgnoreCase));
                _workspace.SelectedRow = selectedRow;
                _host.SetSelectedRow(selectedRow);
                if (selectedRow is not null)
                {
                    LoadEditorFromRow(selectedRow);
                    await LoadAttachedVmNamesAsync(selectedRow.Name);
                    await RefreshValidationAsync();
                }
            }
        }
        finally
        {
            _workspace.IsSaving = false;
            ApplyWorkspaceState();
        }
    }

    public async Task HandleEditorChangedAsync()
    {
        if (_workspace.IsUpdatingEditor)
        {
            return;
        }

        if (_workspace.SelectedRow is null)
        {
            _workspace.PendingDraft = _host.CaptureDraft(isNewOverride: true);
        }

        await RefreshValidationAsync();
        ApplyWorkspaceState();
    }

    public void ApplyWorkspaceState()
    {
        var canValidateOrApply = _host.CaptureDraft(isNewOverride: _workspace.SelectedRow is null) is not null;
        var isExternalSwitchTypeSelected = string.Equals(_host.GetSelectedSwitchType(), "External", StringComparison.OrdinalIgnoreCase);
        _host.ApplyWorkspaceState(_workspace, canValidateOrApply, isExternalSwitchTypeSelected);
    }

    public void ApplyDeleteAssessment(AssetsSwitchDeleteAssessment assessment)
    {
        SetAttachedVmState(
            assessment.AttachedVmNames,
            assessment.AttachedVmNames.Count > 0
                ? "Attached VMs currently using this switch."
                : "No attached VMs.");
        _workspace.DeleteConstraintText = assessment.CanDelete
            ? string.Empty
            : $"Delete blocked. {assessment.Summary}";
        if (_workspace.SelectedRow is not null)
        {
            _workspace.SelectedRow.DeleteSummary = _workspace.DeleteConstraintText;
        }
    }

    public void ClearErrorState()
    {
        _workspace.HasErrorState = false;
        _workspace.ErrorStateText = "No switch load or action errors.";
    }

    private void LoadEditorFromRow(AssetsSwitchListRow row)
    {
        LoadEditorFromDraft(new AssetsSwitchDraft
        {
            IsNew = false,
            OriginalName = row.Name,
            Name = row.Name,
            SwitchType = row.SwitchType,
            AdapterName = row.AdapterName
        });
        _workspace.SelectedSwitchValidationText = row.ValidationSummary;
        _workspace.DeleteConstraintText = string.Empty;
        SetAttachedVmState(Array.Empty<string>(), "Loading attached VMs...");
    }

    private void LoadEditorFromDraft(AssetsSwitchDraft draft)
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

        _workspace.SelectedSwitchValidationText = "Select a switch or click New to begin.";
        _workspace.DeleteConstraintText = string.Empty;
    }

    private void ApplyValidationResult(AssetsSwitchValidationResult validation)
    {
        var validationText = FormatValidationText(validation);
        _workspace.SelectedSwitchValidationText = validationText;
        if (_workspace.SelectedRow is not null)
        {
            _workspace.SelectedRow.ValidationSummary = validationText;
        }
    }

    private async Task RefreshValidationAsync()
    {
        var requestVersion = ++_workspace.ValidationRequestVersion;
        var draft = _host.CaptureDraft(isNewOverride: _workspace.SelectedRow is null);
        if (draft is null)
        {
            if (requestVersion != _workspace.ValidationRequestVersion)
            {
                return;
            }

            _workspace.SelectedSwitchValidationText = _workspace.SelectedRow is null
                ? "Complete the switch name, choose a type, and provide an adapter for External switches."
                : "Edit the switch name to validate changes. Type and adapter changes require creating a new switch.";
            return;
        }

        var validation = await _capabilityService.ValidateAsync(draft, GetKnownInventorySnapshot());
        if (requestVersion != _workspace.ValidationRequestVersion)
        {
            return;
        }

        ApplyValidationResult(validation);
    }

    private async Task LoadAttachedVmNamesAsync(string switchName)
    {
        var requestVersion = ++_workspace.AssessmentRequestVersion;
        var vmNames = await _capabilityService.GetAttachedVmNamesAsync(switchName);
        if (requestVersion != _workspace.AssessmentRequestVersion)
        {
            return;
        }

        if (_workspace.SelectedRow is null || !string.Equals(_workspace.SelectedRow.Name, switchName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetAttachedVmState(
            vmNames,
            vmNames.Count > 0
                ? "Attached VMs currently using this switch."
                : "No attached VMs.");
    }

    private void SetAttachedVmState(IEnumerable<string> vmNames, string hintText)
    {
        _workspace.AttachedVmNames.Clear();
        foreach (var vmName in vmNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            _workspace.AttachedVmNames.Add(vmName);
        }

        _workspace.AttachedVmHintText = hintText;
    }

    private IReadOnlyList<AssetsSwitchRecord> GetKnownInventorySnapshot()
    {
        return _workspace.Inventory
            .Select(row => new AssetsSwitchRecord
            {
                Name = row.Name,
                SwitchType = row.SwitchType,
                AdapterName = row.AdapterName
            })
            .ToList();
    }

    private static string FormatValidationText(AssetsSwitchValidationResult validation)
    {
        var heading = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? $"Ready: {validation.Summary}"
            : string.Equals(validation.Severity, "Warn", StringComparison.OrdinalIgnoreCase)
                ? $"Warning: {validation.Summary}"
                : $"Blocking: {validation.Summary}";
        if (validation.Details.Count == 0 || string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase))
        {
            return heading;
        }

        return string.Join(Environment.NewLine, new[] { heading }.Concat(validation.Details.Select(detail => "- " + detail)));
    }
}

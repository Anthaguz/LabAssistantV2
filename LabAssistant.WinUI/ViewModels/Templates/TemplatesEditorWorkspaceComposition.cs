using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesEditorWorkspaceHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyTemplatesWorkspaceUiState();

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    Task<bool> ShowRemoveTemplateVmConfirmationDialogAsync(string vmName);
}

internal sealed class TemplatesEditorWorkspaceHost : ITemplatesEditorWorkspaceHost
{
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Action<bool> _setTemplatesLoading;
    private readonly Action _applyTemplatesWorkspaceUiState;
    private readonly Func<bool, Task> _ensureTemplatesLibraryAsync;
    private readonly Func<string, Task<string?>> _pickTemplateFileForSaveAsync;
    private readonly Func<string, Task<bool>> _showRemoveTemplateVmConfirmationDialogAsync;

    public TemplatesEditorWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<string, Task<string?>> pickTemplateFileForSaveAsync,
        Func<string, Task<bool>> showRemoveTemplateVmConfirmationDialogAsync)
    {
        _isTemplatesLoading = isTemplatesLoading;
        _setTemplatesLoading = setTemplatesLoading;
        _applyTemplatesWorkspaceUiState = applyTemplatesWorkspaceUiState;
        _ensureTemplatesLibraryAsync = ensureTemplatesLibraryAsync;
        _pickTemplateFileForSaveAsync = pickTemplateFileForSaveAsync;
        _showRemoveTemplateVmConfirmationDialogAsync = showRemoveTemplateVmConfirmationDialogAsync;
    }

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public void SetTemplatesLoading(bool isLoading) => _setTemplatesLoading(isLoading);

    public void ApplyTemplatesWorkspaceUiState() => _applyTemplatesWorkspaceUiState();

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => _ensureTemplatesLibraryAsync(forceRefresh);

    public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName) => _pickTemplateFileForSaveAsync(suggestedFileName);

    public Task<bool> ShowRemoveTemplateVmConfirmationDialogAsync(string vmName) => _showRemoveTemplateVmConfirmationDialogAsync(vmName);
}

internal sealed class TemplatesEditorWorkspaceComposition : ITemplatesEditorWorkspaceControllerHost
{
    private readonly TemplatesEditorView _view;
    private readonly ITemplatesEditorWorkspaceHost _host;
    private readonly TemplatesEditorWorkspaceViewModel _workspace = new();
    private readonly TemplatesEditorWorkspaceController _controller;

    public TemplatesEditorWorkspaceComposition(
        ITemplatesCapabilityService templatesCapabilityService,
        TemplatesEditorView view,
        ITemplatesEditorWorkspaceHost host)
    {
        _view = view;
        _host = host;
        _controller = new TemplatesEditorWorkspaceController(templatesCapabilityService, _workspace, this);
        _view.DocumentHeaderChanged += TemplatesEditorView_DocumentHeaderChanged;
        _view.SelectedVmChanged += TemplatesEditorView_SelectedVmChanged;
        _view.VmDraftChanged += TemplatesEditorView_VmDraftChanged;
        _view.SetVmEntriesSource(_workspace.VmEntries);
        ApplyViewState();
        ApplyVmDraftState();
        ApplyActionState(isLoading: false);
    }

    public bool HasActiveDocument => _workspace.HasActiveDocument;

    public IReadOnlyList<VmTemplate> VmEntries => _workspace.VmEntries;

    public VmTemplate? SelectedVmEntry => _workspace.SelectedVmEntry;

    public TemplateEditorDocument? ActiveDocument => _workspace.ActiveDocument;

    public TemplatesEditorVmDraftSnapshot CaptureVmDraftState() => _workspace.CaptureVmDraftSnapshot();

    public void ApplyShellState(bool isEditorActive)
    {
        _view.Visibility = isEditorActive ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetDocument(TemplateEditorDocument? document)
    {
        if (document is null)
        {
            _workspace.ClearDocument();
            _workspace.ReplaceVmEntries(Array.Empty<VmTemplate>());
        }
        else
        {
            _workspace.SetDocument(document);
            _workspace.ReplaceVmEntries(document.Template.VmTemplates);
        }

        ApplyViewState();
        ApplyVmListState();
        ApplyVmDraftState();
    }

    public void ReplaceVmEntries(IReadOnlyList<VmTemplate> vmEntries)
    {
        _workspace.ReplaceVmEntries(vmEntries);
        ApplyVmListState();
    }

    public bool AddVmEntry() => _controller.AddVmEntry();

    public Task RemoveSelectedVmEntryAsync() => _controller.RemoveSelectedVmEntryAsync();

    public void RefreshVmEntries()
    {
        _view.RefreshVmEntries();
        ApplyVmListState();
    }

    public void SetVmReferenceData(
        IReadOnlyList<string> availableVmSwitches,
        IReadOnlyList<TemplateVhdxCatalogOption> vmVhdxCatalogOptions)
    {
        _workspace.SetVmReferenceData(availableVmSwitches, vmVhdxCatalogOptions);
        ApplyVmDraftState();
    }

    public void SetStatus(string statusText)
    {
        _workspace.SetStatusText(statusText);
        ApplyViewState();
    }

    public void SetVmCount(int vmCount)
    {
        _workspace.SetVmCount(vmCount);
        ApplyViewState();
    }

    public void SyncVmEntriesToDocument()
    {
        if (_workspace.ActiveDocument is null)
        {
            return;
        }

        _workspace.ActiveDocument.Template.VmTemplates = _workspace.VmEntries.ToList();
        _workspace.SetVmCount(_workspace.ActiveDocument.Template.VmTemplates.Count);
        ApplyViewState();
    }

    public bool ApplySelectedVmDraft(bool showSuccessStatus) => _controller.ApplySelectedVmDraft(showSuccessStatus);

    public Task SaveAsync() => _controller.SaveAsync();

    public Task SaveAsAsync() => _controller.SaveAsAsync();

    public Task ValidateAsync() => _controller.ValidateAsync();

    public TemplatesEditorDocumentHeaderInteractionState CaptureDocumentHeaderState()
    {
        return new TemplatesEditorDocumentHeaderInteractionState(
            _workspace.TemplateName,
            _workspace.TemplateDescription);
    }

    public void ApplyActionState(bool isLoading)
    {
        _view.UpdateActionState(new TemplatesEditorActionState(
            CanSave: _workspace.HasActiveDocument && !isLoading,
            CanSaveAs: _workspace.HasActiveDocument && !isLoading,
            CanValidate: _workspace.HasActiveDocument && !isLoading,
            CanBackToLibrary: !isLoading,
            CanAddTemplateVm: _workspace.HasActiveDocument && !isLoading,
            CanRemoveTemplateVm: _workspace.SelectedVmEntry is not null && !isLoading,
            CanAddTemplateVmSwitchRow: _workspace.SelectedVmEntry is not null && !isLoading,
            CanSelectTemplateVmVhdx: _workspace.SelectedVmEntry is not null && !isLoading,
            CanApplyTemplateVmChanges: _workspace.SelectedVmEntry is not null && !isLoading));
    }

    private void TemplatesEditorView_DocumentHeaderChanged(object? sender, EventArgs e)
    {
        var interactionState = _view.CaptureDocumentHeaderInteractionState();
        _workspace.SetDocumentHeaderDraft(interactionState.TemplateName, interactionState.TemplateDescription);
    }

    private void TemplatesEditorView_SelectedVmChanged(object? sender, EventArgs e)
    {
        var interactionState = _view.CaptureVmListInteractionState();
        _workspace.SetSelectedVmEntry(interactionState.SelectedVmEntry);
        ApplyVmListState();
    }

    private void TemplatesEditorView_VmDraftChanged(object? sender, EventArgs e)
    {
        ApplyVmDraftState(_view.CaptureVmDraftInteractionState());
    }

    bool ITemplatesEditorWorkspaceControllerHost.IsTemplatesLoading => _host.IsTemplatesLoading;

    void ITemplatesEditorWorkspaceControllerHost.SetTemplatesLoading(bool isLoading) => _host.SetTemplatesLoading(isLoading);

    void ITemplatesEditorWorkspaceControllerHost.ApplyWorkspaceState()
    {
        ApplyViewState();
        ApplyVmListState();
        _host.ApplyTemplatesWorkspaceUiState();
    }

    Task ITemplatesEditorWorkspaceControllerHost.EnsureTemplatesLibraryAsync(bool forceRefresh) => _host.EnsureTemplatesLibraryAsync(forceRefresh);

    Task<string?> ITemplatesEditorWorkspaceControllerHost.PickTemplateFileForSaveAsync(string suggestedFileName) => _host.PickTemplateFileForSaveAsync(suggestedFileName);

    Task<bool> ITemplatesEditorWorkspaceControllerHost.ShowRemoveTemplateVmConfirmationDialogAsync(string vmName) => _host.ShowRemoveTemplateVmConfirmationDialogAsync(vmName);

    private void ApplyViewState()
    {
        _view.UpdateDocumentHeaderState(new TemplatesEditorDocumentHeaderViewState(
            TemplateEditorContextText: _workspace.TemplateEditorContextText,
            TemplateIdText: _workspace.TemplateIdText,
            TemplateFilePathText: _workspace.TemplateFilePathText,
            TemplateVmCountText: _workspace.TemplateVmCountText,
            TemplateName: _workspace.TemplateName,
            TemplateDescription: _workspace.TemplateDescription,
            StatusText: _workspace.StatusText,
            IsStatusVisible: _workspace.HasStatusText));
    }

    private void ApplyVmListState()
    {
        _view.UpdateVmSelection(_workspace.SelectedVmEntry);
        ApplyVmDraftState();
    }

    private void ApplyVmDraftState(TemplatesEditorVmDraftInteractionState? interactionState = null)
    {
        var draftState = BuildVmDraftState(interactionState);
        _workspace.SetVmDraftState(draftState);
        _view.UpdateVmDraftState(new TemplatesEditorVmDraftViewState(
            VmIdText: draftState.VmIdText,
            VmName: draftState.VmName,
            VmMemoryText: draftState.VmMemoryText,
            VmCpuText: draftState.VmCpuText,
            VmVhdxIdText: draftState.VmVhdxIdText,
            VmVhdPathText: draftState.VmVhdPathText,
            VmVhdxSignatureText: draftState.VmVhdxSignatureText,
            AvailableSwitches: _workspace.AvailableVmSwitches,
            SelectedSwitches: draftState.SelectedSwitches,
            VhdxCatalogOptions: _workspace.VmVhdxCatalogOptions,
            SelectedVhdxCatalogOption: draftState.SelectedVhdxCatalogOption,
            VmSwitchGuidanceText: draftState.VmSwitchGuidanceText,
            VmVhdxGuidanceText: draftState.VmVhdxGuidanceText,
            UseUnresolvedVhdxSelection: draftState.RequiresVhdxResolution || draftState.SelectedVhdxCatalogOption is null));
    }

    private TemplatesEditorVmDraftState BuildVmDraftState(TemplatesEditorVmDraftInteractionState? interactionState)
    {
        if (_workspace.SelectedVmEntry is null)
        {
            return new TemplatesEditorVmDraftState(
                VmIdText: "VM ID: -",
                VmName: string.Empty,
                VmMemoryText: string.Empty,
                VmCpuText: string.Empty,
                VmVhdxIdText: string.Empty,
                VmVhdPathText: string.Empty,
                VmVhdxSignatureText: string.Empty,
                SelectedSwitches: Array.Empty<string>(),
                SelectedVhdxCatalogOption: null,
                VmSwitchGuidanceText: "Select a VM entry to configure switch assignments.",
                VmVhdxGuidanceText: "Select a VM entry to configure base disk.",
                RequiresVhdxResolution: false,
                IsEditingNewVmEntry: false,
                HasChanges: false);
        }

        var selectedVmEntry = _workspace.SelectedVmEntry;
        var selectedSwitches = interactionState?.SelectedSwitches?.ToList() ?? GetSelectedSwitches(selectedVmEntry);
        var selectedCatalogOption = interactionState?.SelectedVhdxCatalogOption;
        var normalization = selectedCatalogOption is null
            ? EvaluateTemplateVhdxNormalization(selectedVmEntry)
            : new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: selectedCatalogOption,
                Message: "User selected replacement catalog entry.",
                EffectiveSourceLabel: "Effective source: selected catalog.");

        var switchGuidanceText = BuildSwitchGuidanceText(selectedSwitches);
        var vhdxGuidanceText = BuildVhdxGuidanceText(selectedVmEntry, normalization, selectedCatalogOption is not null);
        return new TemplatesEditorVmDraftState(
            VmIdText: $"VM ID: {selectedVmEntry.VmId}",
            VmName: interactionState?.VmName ?? selectedVmEntry.Name,
            VmMemoryText: interactionState?.VmMemoryText ?? selectedVmEntry.MemoryMb.ToString(),
            VmCpuText: interactionState?.VmCpuText ?? selectedVmEntry.CpuCount.ToString(),
            VmVhdxIdText: selectedCatalogOption?.Id ?? (selectedVmEntry.VhdxId ?? string.Empty),
            VmVhdPathText: selectedCatalogOption?.Path ?? (selectedVmEntry.VhdPath ?? string.Empty),
            VmVhdxSignatureText: selectedCatalogOption?.Signature ?? (selectedVmEntry.VhdxSignature ?? string.Empty),
            SelectedSwitches: selectedSwitches,
            SelectedVhdxCatalogOption: normalization.EffectiveOption,
            VmSwitchGuidanceText: switchGuidanceText,
            VmVhdxGuidanceText: vhdxGuidanceText,
            RequiresVhdxResolution: selectedCatalogOption is null && normalization.RequiresUserResolution,
            IsEditingNewVmEntry: string.IsNullOrWhiteSpace(selectedVmEntry.VmId),
            HasChanges: HasVmDraftChanges(
                selectedVmEntry,
                interactionState,
                selectedSwitches,
                selectedCatalogOption));
    }

    private List<string> GetSelectedSwitches(VmTemplate vmEntry)
    {
        var switches = vmEntry.SwitchNames?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList() ?? [];

        if (switches.Count == 0 && !string.IsNullOrWhiteSpace(vmEntry.SwitchName))
        {
            switches.Add(vmEntry.SwitchName.Trim());
        }

        return switches;
    }

    private string BuildSwitchGuidanceText(IReadOnlyList<string> selectedSwitches)
    {
        if (_workspace.SelectedVmEntry is null)
        {
            return "Select a VM entry to configure switch assignments.";
        }

        if (_workspace.AvailableVmSwitches.Count == 0)
        {
            return "No host switches available. Add a host switch before assigning VM switch rows.";
        }

        if (selectedSwitches.Count == 0)
        {
            return "No switch rows. Optional for template VM.";
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selectedSwitch in selectedSwitches)
        {
            if (string.IsNullOrWhiteSpace(selectedSwitch))
            {
                return "Each switch row must have a selected host switch or be removed.";
            }

            if (!_workspace.AvailableVmSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
            {
                return $"Switch '{selectedSwitch}' is not available on this host.";
            }

            if (!seen.Add(selectedSwitch))
            {
                return $"Duplicate switch '{selectedSwitch}' is not allowed.";
            }
        }

        return "Switch rows configured.";
    }

    private string BuildVhdxGuidanceText(
        VmTemplate selectedVmEntry,
        TemplateVhdxNormalizationResult normalization,
        bool hasUserSelectedCatalogOption)
    {
        if (_workspace.VmVhdxCatalogOptions.Count == 0)
        {
            return "No catalog entries available. Import base disks in Assets > Base Disks.";
        }

        if (normalization.RequiresUserResolution)
        {
            return normalization.Message;
        }

        if (hasUserSelectedCatalogOption && normalization.EffectiveOption is not null)
        {
            return $"{normalization.EffectiveSourceLabel} Catalog entry selected. Save to persist.";
        }

        if (normalization.EffectiveOption is not null)
        {
            return $"{normalization.EffectiveSourceLabel} Effective disk: {normalization.EffectiveOption.DisplayLabel} ({normalization.EffectiveOption.Id}).";
        }

        if (!string.IsNullOrWhiteSpace(selectedVmEntry.VhdPath))
        {
            return "Legacy path-based reference loaded. Select a catalog entry to normalize.";
        }

        return "Catalog-backed selection is preferred.";
    }

    private bool HasVmDraftChanges(
        VmTemplate selectedVmEntry,
        TemplatesEditorVmDraftInteractionState? interactionState,
        IReadOnlyList<string> selectedSwitches,
        TemplateVhdxCatalogOption? selectedCatalogOption)
    {
        if (interactionState is null)
        {
            return false;
        }

        if (!string.Equals(interactionState.Value.VmName, selectedVmEntry.Name, StringComparison.Ordinal) ||
            !string.Equals(interactionState.Value.VmMemoryText, selectedVmEntry.MemoryMb.ToString(), StringComparison.Ordinal) ||
            !string.Equals(interactionState.Value.VmCpuText, selectedVmEntry.CpuCount.ToString(), StringComparison.Ordinal))
        {
            return true;
        }

        if (!selectedSwitches.SequenceEqual(GetSelectedSwitches(selectedVmEntry), StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (selectedCatalogOption is null)
        {
            return false;
        }

        return !string.Equals(selectedVmEntry.VhdxId, selectedCatalogOption.Id, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(selectedVmEntry.VhdPath, selectedCatalogOption.Path, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(selectedVmEntry.VhdxSignature, selectedCatalogOption.Signature, StringComparison.OrdinalIgnoreCase);
    }

    private TemplateVhdxNormalizationResult EvaluateTemplateVhdxNormalization(VmTemplate vmTemplate)
    {
        var idMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdxId)
            ? null
            : _workspace.VmVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, vmTemplate.VhdxId, StringComparison.OrdinalIgnoreCase));

        var signatureMatches = string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature)
            ? []
            : _workspace.VmVhdxCatalogOptions
                .Where(option => !string.IsNullOrWhiteSpace(option.Signature) &&
                                 string.Equals(option.Signature, vmTemplate.VhdxSignature, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var pathMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdPath)
            ? null
            : _workspace.VmVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, vmTemplate.VhdPath, StringComparison.OrdinalIgnoreCase));

        if (idMatch is not null)
        {
            if (pathMatch is not null && !string.Equals(pathMatch.Id, idMatch.Id, StringComparison.OrdinalIgnoreCase))
            {
                return new TemplateVhdxNormalizationResult(
                    RequiresUserResolution: true,
                    EffectiveOption: null,
                    Message: "VHD identity conflict detected. Select a catalog entry to resolve before saving.",
                    EffectiveSourceLabel: "Effective source: unresolved conflict.");
            }

            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: idMatch,
                Message: "Resolved from vhdxId.",
                EffectiveSourceLabel: "Effective source: vhdxId.");
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdxId))
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: true,
                EffectiveOption: null,
                Message: $"Catalog entry '{vmTemplate.VhdxId}' is missing. Select a replacement before saving.",
                EffectiveSourceLabel: "Effective source: unresolved missing catalog.");
        }

        if (signatureMatches.Count > 1)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: true,
                EffectiveOption: null,
                Message: "Multiple catalog entries match vhdxSignature. Select one entry before saving.",
                EffectiveSourceLabel: "Effective source: unresolved signature.");
        }

        if (signatureMatches.Count == 1)
        {
            if (pathMatch is not null &&
                !string.Equals(pathMatch.Id, signatureMatches[0].Id, StringComparison.OrdinalIgnoreCase))
            {
                return new TemplateVhdxNormalizationResult(
                    RequiresUserResolution: true,
                    EffectiveOption: null,
                    Message: "VHD identity conflict detected. Select a catalog entry to resolve before saving.",
                    EffectiveSourceLabel: "Effective source: unresolved conflict.");
            }

            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: signatureMatches[0],
                Message: "Resolved from vhdxSignature.",
                EffectiveSourceLabel: "Effective source: vhdxSignature.");
        }

        if (pathMatch is not null)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: pathMatch,
                Message: $"Catalog entry '{pathMatch.Id}' resolves current vhdPath.",
                EffectiveSourceLabel: "Effective source: vhdPath.");
        }

        return new TemplateVhdxNormalizationResult(
            RequiresUserResolution: false,
            EffectiveOption: null,
            Message: "Catalog-backed selection is preferred.",
            EffectiveSourceLabel: "Effective source: none.");
    }
}

using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public readonly record struct TemplatesEditorDocumentHeaderInteractionState(
    string TemplateName,
    string TemplateDescription);

public readonly record struct TemplatesEditorDocumentHeaderViewState(
    string TemplateEditorContextText,
    string TemplateIdText,
    string TemplateFilePathText,
    string TemplateVmCountText,
    string TemplateName,
    string TemplateDescription,
    string StatusText,
    bool IsStatusVisible);

public readonly record struct TemplatesEditorActionState(
    bool CanSave,
    bool CanSaveAs,
    bool CanValidate,
    bool CanBackToLibrary,
    bool CanAddTemplateVm,
    bool CanRemoveTemplateVm,
    bool CanAddTemplateVmSwitchRow,
    bool CanSelectTemplateVmVhdx,
    bool CanApplyTemplateVmChanges);

public readonly record struct TemplatesEditorVmListInteractionState(
    VmTemplate? SelectedVmEntry);

internal readonly record struct TemplatesEditorVmDraftInteractionState(
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    IReadOnlyList<string> SelectedSwitches,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption);

internal readonly record struct TemplatesEditorVmDraftViewState(
    string VmIdText,
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    string VmVhdxIdText,
    string VmVhdPathText,
    string VmVhdxSignatureText,
    IReadOnlyList<string> AvailableSwitches,
    IReadOnlyList<string> SelectedSwitches,
    IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption,
    string VmSwitchGuidanceText,
    string VmVhdxGuidanceText,
    bool UseUnresolvedVhdxSelection);

public sealed partial class TemplatesEditorView : UserControl
{
    private bool _isUpdatingDocumentHeader;
    private bool _isUpdatingVmSelection;
    private bool _isUpdatingVmDraft;
    private IReadOnlyList<VmTemplate> _vmEntrySource = Array.Empty<VmTemplate>();

    public event EventHandler? DocumentHeaderChanged;
    public event EventHandler? SelectedVmChanged;
    public event EventHandler? VmDraftChanged;
    public event EventHandler? AddVmRequested;
    public event EventHandler? RemoveVmRequested;
    public event EventHandler? ApplyVmChangesRequested;
    public event EventHandler? SaveRequested;
    public event EventHandler? SaveAsRequested;
    public event EventHandler? ValidateRequested;
    public event EventHandler? BackToLibraryRequested;

    partial void InitializeBridge()
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.AddSlotRequested = () => AddVmRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.RemoveSlotRequested = () => RemoveVmRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.ApplySlotChangesRequested = () => ApplyVmChangesRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.SaveRequested = () => SaveRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.SaveAsRequested = () => SaveAsRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.ValidateRequested = () => ValidateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.CancelRequested = () => BackToLibraryRequested?.Invoke(this, EventArgs.Empty);
    }

    public TemplatesEditorDocumentHeaderInteractionState CaptureDocumentHeaderInteractionState()
    {
        return new TemplatesEditorDocumentHeaderInteractionState(ViewModel.TemplateName, ViewModel.Description);
    }

    public TemplatesEditorVmListInteractionState CaptureVmListInteractionState()
    {
        return new TemplatesEditorVmListInteractionState(ViewModel.SelectedSlot?.SourceItem);
    }

    internal TemplatesEditorVmDraftInteractionState CaptureVmDraftInteractionState()
    {
        return new TemplatesEditorVmDraftInteractionState(
            ViewModel.SlotName,
            ViewModel.SlotMemoryText,
            ViewModel.SlotCpuText,
            CaptureSelectedSwitches(),
            ViewModel.SelectedCatalogOption);
    }

    public void SetVmEntriesSource(object? itemsSource)
    {
        _vmEntrySource = (itemsSource as IEnumerable<VmTemplate>)?.ToList() ?? [];
        var selectedVmId = ViewModel.SelectedSlot?.VmId;

        _isUpdatingVmSelection = true;
        try
        {
            ViewModel.Slots.Clear();
            foreach (var vmEntry in _vmEntrySource)
            {
                ViewModel.Slots.Add(TemplateSlotItem.FromVmTemplate(vmEntry));
            }

            ViewModel.SelectedSlot = ResolveSelectedSlot(selectedVmId, null);
        }
        finally
        {
            _isUpdatingVmSelection = false;
        }

        ViewModel.RefreshComputedState();
    }

    public void UpdateVmSelection(VmTemplate? selectedVmEntry)
    {
        _isUpdatingVmSelection = true;
        try
        {
            ViewModel.SelectedSlot = ResolveSelectedSlot(selectedVmEntry?.VmId, selectedVmEntry);
        }
        finally
        {
            _isUpdatingVmSelection = false;
        }

        ViewModel.RefreshComputedState();
    }

    public void RefreshVmEntries()
    {
        SetVmEntriesSource(_vmEntrySource);
    }

    public void UpdateDocumentHeaderState(TemplatesEditorDocumentHeaderViewState state)
    {
        _isUpdatingDocumentHeader = true;
        try
        {
            ViewModel.ContextText = state.TemplateEditorContextText;
            ViewModel.TemplateIdText = state.TemplateIdText;
            ViewModel.FilePathText = state.TemplateFilePathText;
            ViewModel.SlotCountText = state.TemplateVmCountText;
            ViewModel.TemplateName = state.TemplateName;
            ViewModel.Description = state.TemplateDescription;
            ViewModel.StatusMessage = state.StatusText;
            ViewModel.IsStatusVisible = state.IsStatusVisible;
        }
        finally
        {
            _isUpdatingDocumentHeader = false;
        }

        ViewModel.RefreshComputedState();
    }

    internal void UpdateVmDraftState(TemplatesEditorVmDraftViewState state)
    {
        _isUpdatingVmDraft = true;
        try
        {
            ViewModel.SlotIdText = state.VmIdText;
            ViewModel.SlotName = state.VmName;
            ViewModel.SlotMemoryText = state.VmMemoryText;
            ViewModel.SlotCpuText = state.VmCpuText;
            ViewModel.SlotSwitchesText = string.Join(", ", state.SelectedSwitches.Where(value => !string.IsNullOrWhiteSpace(value)));
            ViewModel.SlotBaseDiskId = state.VmVhdxIdText;
            ViewModel.SlotBaseDiskPath = state.VmVhdPathText;
            ViewModel.SlotVhdxSignature = state.VmVhdxSignatureText;
            ViewModel.SlotGenerationText = state.SelectedVhdxCatalogOption?.Generation.ToString() ?? string.Empty;
            ViewModel.SwitchGuidanceText = state.VmSwitchGuidanceText;
            ViewModel.BaseDiskGuidanceText = state.VmVhdxGuidanceText;
            ViewModel.SelectedCatalogOption = state.SelectedVhdxCatalogOption;

            if (ViewModel.SelectedSlot is not null)
            {
                ViewModel.SelectedSlot.ApplyDraftState(
                    state.VmIdText,
                    state.VmName,
                    state.VmMemoryText,
                    state.VmCpuText,
                    state.SelectedSwitches,
                    state.SelectedVhdxCatalogOption,
                    state.VmVhdxIdText,
                    state.VmVhdPathText,
                    state.VmVhdxSignatureText,
                    ViewModel.SlotGenerationText);
            }
        }
        finally
        {
            _isUpdatingVmDraft = false;
        }

        ViewModel.RefreshComputedState();
    }

    public void UpdateActionState(TemplatesEditorActionState state)
    {
        ViewModel.CanSave = state.CanSave;
        ViewModel.CanSaveAs = state.CanSaveAs;
        ViewModel.CanValidate = state.CanValidate;
        ViewModel.CanCancel = state.CanBackToLibrary;
        ViewModel.CanAddSlot = state.CanAddTemplateVm;
        ViewModel.CanRemoveSlot = state.CanRemoveTemplateVm;
        ViewModel.CanApplySlotChanges = state.CanApplyTemplateVmChanges;
    }

    private IReadOnlyList<string> CaptureSelectedSwitches() => ViewModel.SlotSwitchesText
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private TemplateSlotItem? ResolveSelectedSlot(string? vmId, VmTemplate? selectedVmEntry)
    {
        if (!string.IsNullOrWhiteSpace(vmId))
        {
            var idMatch = ViewModel.Slots.FirstOrDefault(slot => string.Equals(slot.VmId, vmId, StringComparison.Ordinal));
            if (idMatch is not null)
            {
                return idMatch;
            }
        }

        if (selectedVmEntry is not null)
        {
            var referenceMatch = ViewModel.Slots.FirstOrDefault(slot => ReferenceEquals(slot.SourceItem, selectedVmEntry));
            if (referenceMatch is not null)
            {
                return referenceMatch;
            }
        }

        return ViewModel.Slots.FirstOrDefault();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TemplatesEditorViewModel.TemplateName) or nameof(TemplatesEditorViewModel.Description))
        {
            if (!_isUpdatingDocumentHeader)
            {
                DocumentHeaderChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (e.PropertyName == nameof(TemplatesEditorViewModel.SelectedSlot))
        {
            if (!_isUpdatingVmSelection)
            {
                SelectedVmChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (_isUpdatingVmDraft)
        {
            return;
        }

        if (e.PropertyName is nameof(TemplatesEditorViewModel.SlotName)
            or nameof(TemplatesEditorViewModel.SlotMemoryText)
            or nameof(TemplatesEditorViewModel.SlotCpuText)
            or nameof(TemplatesEditorViewModel.SlotSwitchesText)
            or nameof(TemplatesEditorViewModel.SlotBaseDiskId)
            or nameof(TemplatesEditorViewModel.SlotBaseDiskPath)
            or nameof(TemplatesEditorViewModel.SlotVhdxSignature)
            or nameof(TemplatesEditorViewModel.SlotGenerationText))
        {
            VmDraftChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}


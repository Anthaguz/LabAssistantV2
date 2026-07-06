using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Templates Editor subview model. Full x:Bind MVVM: it owns the document header, the VM slot list,
/// the selected-slot draft, and the save/validate/add/remove commands, folding the logic of the
/// dissolved editor workspace controller/composition/bridge into a single testable view model. The
/// contractual VHDX normalization precedence and switch validation live in <see cref="TemplatesEditorLogic"/>.
/// Reference data and cross-subview navigation/dialogs are injected via <c>Attach</c>, so the model is
/// unit-testable without a dispatcher or Hyper-V. Registered transient; created and torn down with the
/// hosting <c>TemplatesPage</c>.
/// </summary>
public partial class TemplatesEditorViewModel : ViewModelBase
{
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private TemplatesReferenceDataService? _referenceDataService;
    private ITemplatesEditorHost? _host;

    private TemplateEditorDocument? _activeDocument;
    private IReadOnlyList<string> _availableVmSwitches = Array.Empty<string>();
    private IReadOnlyList<TemplateVhdxCatalogOption> _vhdxCatalogOptions = Array.Empty<TemplateVhdxCatalogOption>();
    private TemplateVhdxCatalogOption? _selectedCatalogOption;
    private bool _requiresVhdxResolution;
    private string? _selectedVmId;

    // Guards the draft recompute re-push so setting the display-only base-disk fields (and normalized
    // Name/Memory/Cpu/Switches values) does not re-enter the recompute pipeline.
    private bool _isRecomputing;

    // Guards the slot-list rebuild so programmatic selection changes do not trigger a field-edit recompute.
    private bool _isReplacingSlots;

    public TemplatesEditorViewModel(ITemplatesCapabilityService templatesCapabilityService)
    {
        _templatesCapabilityService = templatesCapabilityService;
        PropertyChanged += OnSelfPropertyChanged;
    }

    [ObservableProperty] private string _templateName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ObservableCollection<TemplateSlotItem> _slots = [];
    [ObservableProperty] private TemplateSlotItem? _selectedSlot;
    [ObservableProperty] private string _contextText = "No template selected.";
    [ObservableProperty] private string _templateIdText = "Template ID: -";
    [ObservableProperty] private string _filePathText = "File path: new template (not saved)";
    [ObservableProperty] private string _slotCountText = "VMs: 0";
    [ObservableProperty] private string _statusMessage = "No template loaded.";
    [ObservableProperty] private bool _isStatusVisible;
    [ObservableProperty] private string _slotIdText = "VM ID: -";
    [ObservableProperty] private string _slotName = string.Empty;
    [ObservableProperty] private string _slotMemoryText = string.Empty;
    [ObservableProperty] private string _slotCpuText = string.Empty;
    [ObservableProperty] private string _slotSwitchesText = string.Empty;
    [ObservableProperty] private string _slotBaseDiskId = string.Empty;
    [ObservableProperty] private string _slotBaseDiskPath = string.Empty;
    [ObservableProperty] private string _slotVhdxSignature = string.Empty;
    [ObservableProperty] private string _slotGenerationText = string.Empty;
    [ObservableProperty] private string _switchGuidanceText = "Select a VM slot to configure switch assignments.";
    [ObservableProperty] private string _baseDiskGuidanceText = "Select a VM slot to configure the base disk.";

    public bool HasActiveDocument => _activeDocument is not null;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasSelectedSlot => SelectedSlot is not null;
    public string ErrorStateText => string.IsNullOrWhiteSpace(ErrorMessage) ? "No template editor errors." : ErrorMessage!;
    public string SelectedSlotHeading => SelectedSlot is null ? "No VM slot selected" : $"Editing {SelectedSlot.Name}";
    public string SelectedSlotSummary => SelectedSlot?.Description ?? "Select a VM slot to edit its configuration.";

    public bool CanSave => HasActiveDocument && !IsLoading;
    public bool CanValidate => HasActiveDocument && !IsLoading;
    public bool CanCancel => !IsLoading;
    public bool CanAddSlot => HasActiveDocument && !IsLoading;
    public bool CanRemoveSlot => SelectedSlot is not null && !IsLoading;
    public bool CanApplySlotChanges => SelectedSlot is not null && !IsLoading;

    /// <summary>Attaches reference data and the cross-subview host. Called by the page on navigation.</summary>
    internal void Attach(TemplatesReferenceDataService referenceDataService, ITemplatesEditorHost host)
    {
        _referenceDataService = referenceDataService;
        _host = host;
    }

    /// <summary>Detaches the reference data service and host. Called by the page on leave.</summary>
    internal void Detach()
    {
        _referenceDataService = null;
        _host = null;
    }

    /// <summary>
    /// Surfaces <paramref name="statusText"/> on the editor status line without loading a document or
    /// routing. Preserves the legacy behavior where a failed "open in editor" from the Library reports
    /// its error on the editor status line while the user stays on the Library subview.
    /// </summary>
    internal void ReportStatus(string statusText)
    {
        SetStatus(statusText);
        RefreshComputedState();
    }

    public override Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }

    public override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        _referenceDataService = null;
        _host = null;
        _activeDocument = null;
        _availableVmSwitches = Array.Empty<string>();
        _vhdxCatalogOptions = Array.Empty<TemplateVhdxCatalogOption>();
        _selectedCatalogOption = null;
        _requiresVhdxResolution = false;
        _selectedVmId = null;
        Slots.Clear();
        SelectedSlot = null;
        TemplateName = string.Empty;
        Description = string.Empty;
        ContextText = "No template selected.";
        TemplateIdText = "Template ID: -";
        FilePathText = "File path: new template (not saved)";
        SlotCountText = "VMs: 0";
        StatusMessage = "No template loaded.";
        IsStatusVisible = false;
        IsLoading = false;
        ErrorMessage = null;
        RecomputeDraft(null);
        RefreshComputedState();
        IsInitialized = false;
    }

    /// <summary>
    /// Loads <paramref name="document"/> into the editor: refreshes the header, rebuilds the VM slot
    /// list, refreshes reference data, and re-seeds the selected-slot draft. Tab routing is handled by
    /// the hosting page.
    /// </summary>
    public async Task ShowDocumentAsync(TemplateEditorDocument document, string statusText)
    {
        ArgumentNullException.ThrowIfNull(document);

        var referenceData = _referenceDataService is null
            ? new TemplatesEditorReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>())
            : await _referenceDataService.LoadEditorReferenceDataAsync(forceRefresh: false);

        ApplyDocumentHeader(document);
        ReplaceSlots(document.Template.VmTemplates);
        _availableVmSwitches = referenceData.AvailableVmSwitches;
        _vhdxCatalogOptions = referenceData.VhdxCatalogOptions;
        SetStatus(statusText);
        RecomputeDraft(null);
        RefreshComputedState();
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_activeDocument is null)
        {
            SetStatus("No template loaded.");
            RefreshComputedState();
            return;
        }

        if (!TryApplyEditorFieldsToDocument(showSuccessStatus: false))
        {
            RefreshComputedState();
            return;
        }

        IsLoading = true;
        RefreshComputedState();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_activeDocument);
            SetStatus(result.UserMessage);
            if (result.Success)
            {
                ApplyDocumentHeader(new TemplateEditorDocument
                {
                    Template = _activeDocument.Template,
                    SourceFilePath = result.FilePath
                });
                if (_host is not null)
                {
                    await _host.ReloadLibraryAsync(forceRefresh: true);
                }
            }
        }
        finally
        {
            IsLoading = false;
            RecomputeDraft(null);
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private async Task Validate()
    {
        if (_activeDocument is null)
        {
            SetStatus("No template loaded.");
            RefreshComputedState();
            return;
        }

        if (TryApplyEditorFieldsToDocument(showSuccessStatus: false))
        {
            var result = await _templatesCapabilityService.ValidateAsync(_activeDocument);
            SetStatus(result.IsValid
                ? "Template validation passed."
                : "Validation failed: " + string.Join(" ", result.Errors));
        }

        RecomputeDraft(null);
        RefreshComputedState();
    }

    [RelayCommand]
    private void Cancel() => _host?.NavigateToLibrary();

    [RelayCommand]
    private void AddSlot()
    {
        if (_activeDocument is null)
        {
            SetStatus("Load or create a template first.");
            RefreshComputedState();
            return;
        }

        var nextVmNumber = Slots.Count + 1;
        var vmEntry = new VmTemplate
        {
            Name = $"VM-{nextVmNumber}",
            MemoryMb = 2048,
            CpuCount = 2
        };

        var slot = TemplateSlotItem.FromVmTemplate(vmEntry);
        _isReplacingSlots = true;
        try
        {
            Slots.Add(slot);
            SelectedSlot = slot;
        }
        finally
        {
            _isReplacingSlots = false;
        }

        SyncSlotsToDocument();
        SetStatus($"Added VM entry '{vmEntry.Name}'.");
        RecomputeDraft(null);
        RefreshComputedState();
    }

    [RelayCommand]
    private async Task RemoveSlot()
    {
        if (SelectedSlot is null)
        {
            SetStatus("Select a VM entry first.");
            RefreshComputedState();
            return;
        }

        var vmName = SelectedSlot.SourceItem?.Name ?? SelectedSlot.Name;
        if (_host is null || !await _host.ConfirmRemoveVmAsync(vmName))
        {
            return;
        }

        var removed = SelectedSlot;
        var removedIndex = Slots.IndexOf(removed);
        _isReplacingSlots = true;
        try
        {
            Slots.Remove(removed);
            TemplateSlotItem? nextSelection = null;
            if (Slots.Count > 0)
            {
                var nextIndex = Math.Clamp(removedIndex, 0, Slots.Count - 1);
                nextSelection = Slots[nextIndex];
            }

            SelectedSlot = nextSelection;
        }
        finally
        {
            _isReplacingSlots = false;
        }

        SyncSlotsToDocument();
        SetStatus($"Removed VM entry '{vmName}'.");
        RecomputeDraft(null);
        RefreshComputedState();
    }

    [RelayCommand]
    private void ApplySlotChanges()
    {
        if (SelectedSlot is null)
        {
            SetStatus("Select a VM entry first.");
            RefreshComputedState();
            return;
        }

        ApplySelectedVmDraft(showSuccessStatus: true);
        RecomputeDraft(null);
        RefreshComputedState();
    }

    private bool ApplySelectedVmDraft(bool showSuccessStatus)
    {
        if (_activeDocument is null)
        {
            SetStatus("No template loaded.");
            return false;
        }

        return TryApplyEditorFieldsToDocument(showSuccessStatus);
    }

    private bool TryApplyEditorFieldsToDocument(bool showSuccessStatus)
    {
        var document = _activeDocument;
        if (document is null)
        {
            return false;
        }

        if (!TryApplySelectedVmDraft(showSuccessStatus))
        {
            return false;
        }

        var template = document.Template;
        template.Name = TemplateName.Trim();
        var trimmedDescription = Description.Trim();
        template.Description = string.IsNullOrWhiteSpace(trimmedDescription) ? null : trimmedDescription;
        template.VmTemplates = Slots.Select(slot => slot.SourceItem!).ToList();
        SlotCountText = $"VMs: {template.VmTemplates.Count}";
        ApplyDocumentHeader(document);
        return true;
    }

    private bool TryApplySelectedVmDraft(bool showSuccessStatus)
    {
        if (SelectedSlot is null)
        {
            return true;
        }

        var vmName = SlotName.Trim();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            SetStatus("VM name is required.");
            return false;
        }

        if (!int.TryParse(SlotMemoryText, out var memoryMb) || memoryMb <= 0)
        {
            SetStatus("Memory must be a positive integer.");
            return false;
        }

        if (!int.TryParse(SlotCpuText, out var cpuCount) || cpuCount <= 0)
        {
            SetStatus("CPU count must be a positive integer.");
            return false;
        }

        var selectedSwitches = ParseSwitches(SlotSwitchesText);
        if (!TemplatesEditorLogic.TryValidateSwitches(selectedSwitches, _availableVmSwitches, out var switchValidationError))
        {
            SetStatus(switchValidationError ?? "Switch validation failed.");
            return false;
        }

        var entry = SelectedSlot.SourceItem!;
        entry.Name = vmName;
        entry.MemoryMb = memoryMb;
        entry.CpuCount = cpuCount;
        entry.SwitchNames = selectedSwitches.Count > 0 ? selectedSwitches : null;
        entry.SwitchName = selectedSwitches.Count > 0 ? selectedSwitches[0] : null;

        if (_selectedCatalogOption is TemplateVhdxCatalogOption selectedCatalogOption)
        {
            entry.VhdxId = selectedCatalogOption.Id;
            entry.VhdPath = selectedCatalogOption.Path;
            entry.VhdxSignature = selectedCatalogOption.Signature;
        }
        else if (_requiresVhdxResolution)
        {
            SetStatus(BaseDiskGuidanceText);
            return false;
        }

        if (showSuccessStatus)
        {
            SetStatus($"Updated VM entry '{vmName}'.");
        }

        return true;
    }

    private void SyncSlotsToDocument()
    {
        if (_activeDocument is null)
        {
            return;
        }

        _activeDocument.Template.VmTemplates = Slots.Select(slot => slot.SourceItem!).ToList();
        SlotCountText = $"VMs: {_activeDocument.Template.VmTemplates.Count}";
    }

    private void ApplyDocumentHeader(TemplateEditorDocument document)
    {
        _activeDocument = document;
        TemplateName = document.Template.Name ?? string.Empty;
        Description = document.Template.Description ?? string.Empty;
        ContextText = string.IsNullOrWhiteSpace(document.SourceFilePath)
            ? "Editing new template draft."
            : "Editing existing template.";
        TemplateIdText = $"Template ID: {document.Template.Id}";
        FilePathText = $"File path: {document.SourceFilePath ?? "new template (not saved)"}";
        SlotCountText = $"VMs: {document.Template.VmTemplates.Count}";
        RefreshComputedState();
    }

    private void ReplaceSlots(IReadOnlyList<VmTemplate> vmTemplates)
    {
        var selectedVmId = _selectedVmId;
        var selectedReference = SelectedSlot?.SourceItem;

        _isReplacingSlots = true;
        try
        {
            Slots.Clear();
            foreach (var vmTemplate in vmTemplates)
            {
                Slots.Add(TemplateSlotItem.FromVmTemplate(vmTemplate));
            }

            SelectedSlot = ResolveSelectedSlot(selectedVmId, selectedReference);
        }
        finally
        {
            _isReplacingSlots = false;
        }
    }

    private TemplateSlotItem? ResolveSelectedSlot(string? selectedVmId, VmTemplate? selectedReference)
    {
        if (!string.IsNullOrWhiteSpace(selectedVmId))
        {
            var idMatch = Slots.FirstOrDefault(slot =>
                string.Equals(slot.SourceItem?.VmId, selectedVmId, StringComparison.Ordinal));
            if (idMatch is not null)
            {
                return idMatch;
            }
        }

        if (selectedReference is not null)
        {
            var referenceMatch = Slots.FirstOrDefault(slot => ReferenceEquals(slot.SourceItem, selectedReference));
            if (referenceMatch is not null)
            {
                return referenceMatch;
            }
        }

        return Slots.FirstOrDefault();
    }

    private void RecomputeDraft(DraftInteraction? interaction)
    {
        _isRecomputing = true;
        try
        {
            if (SelectedSlot is null)
            {
                SlotIdText = "VM ID: -";
                SlotName = string.Empty;
                SlotMemoryText = string.Empty;
                SlotCpuText = string.Empty;
                SlotSwitchesText = string.Empty;
                SlotBaseDiskId = string.Empty;
                SlotBaseDiskPath = string.Empty;
                SlotVhdxSignature = string.Empty;
                SlotGenerationText = string.Empty;
                SwitchGuidanceText = "Select a VM entry to configure switch assignments.";
                BaseDiskGuidanceText = "Select a VM entry to configure base disk.";
                _selectedCatalogOption = null;
                _requiresVhdxResolution = false;
                return;
            }

            var entry = SelectedSlot.SourceItem!;
            var selectedSwitches = interaction?.SelectedSwitches?.ToList() ?? GetSelectedSwitches(entry);
            var selectedCatalogOption = interaction?.SelectedVhdxCatalogOption;
            var normalization = selectedCatalogOption is null
                ? TemplatesEditorLogic.EvaluateVhdxNormalization(entry, _vhdxCatalogOptions)
                : new TemplateVhdxNormalizationResult(
                    RequiresUserResolution: false,
                    EffectiveOption: selectedCatalogOption,
                    Message: "User selected replacement catalog entry.",
                    EffectiveSourceLabel: "Effective source: selected catalog.");

            var effectiveOption = normalization.EffectiveOption;
            var vmVhdxIdText = selectedCatalogOption?.Id ?? (entry.VhdxId ?? string.Empty);
            var vmVhdPathText = selectedCatalogOption?.Path ?? (entry.VhdPath ?? string.Empty);
            var vmVhdxSignatureText = selectedCatalogOption?.Signature ?? (entry.VhdxSignature ?? string.Empty);
            var generationText = effectiveOption?.Generation.ToString() ?? string.Empty;

            SlotIdText = $"VM ID: {entry.VmId}";
            SlotName = interaction?.VmName ?? entry.Name;
            SlotMemoryText = interaction?.VmMemoryText ?? entry.MemoryMb.ToString();
            SlotCpuText = interaction?.VmCpuText ?? entry.CpuCount.ToString();
            SlotSwitchesText = string.Join(", ", selectedSwitches.Where(value => !string.IsNullOrWhiteSpace(value)));
            SlotBaseDiskId = vmVhdxIdText;
            SlotBaseDiskPath = vmVhdPathText;
            SlotVhdxSignature = vmVhdxSignatureText;
            SlotGenerationText = generationText;
            SwitchGuidanceText = BuildSwitchGuidanceText(selectedSwitches);
            BaseDiskGuidanceText = BuildVhdxGuidanceText(entry, normalization, selectedCatalogOption is not null);
            _selectedCatalogOption = effectiveOption;
            _requiresVhdxResolution = selectedCatalogOption is null && normalization.RequiresUserResolution;

            SelectedSlot.ApplyDraftState(
                SlotIdText,
                SlotName,
                SlotMemoryText,
                SlotCpuText,
                selectedSwitches,
                effectiveOption,
                vmVhdxIdText,
                vmVhdPathText,
                vmVhdxSignatureText,
                generationText);
        }
        finally
        {
            _isRecomputing = false;
        }
    }

    private static List<string> GetSelectedSwitches(VmTemplate vmEntry)
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
        if (SelectedSlot is null)
        {
            return "Select a VM entry to configure switch assignments.";
        }

        if (_availableVmSwitches.Count == 0)
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

            if (!_availableVmSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
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
        if (_vhdxCatalogOptions.Count == 0)
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

    private static List<string> ParseSwitches(string switchesText) => switchesText
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private DraftInteraction CaptureInteraction() => new(
        SlotName,
        SlotMemoryText,
        SlotCpuText,
        ParseSwitches(SlotSwitchesText),
        _selectedCatalogOption);

    private void SetStatus(string statusText)
    {
        StatusMessage = statusText ?? string.Empty;
        IsStatusVisible = !string.IsNullOrWhiteSpace(StatusMessage);
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(HasActiveDocument));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsStatusVisible));
        OnPropertyChanged(nameof(HasSelectedSlot));
        OnPropertyChanged(nameof(ErrorStateText));
        OnPropertyChanged(nameof(SelectedSlotHeading));
        OnPropertyChanged(nameof(SelectedSlotSummary));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanValidate));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(CanAddSlot));
        OnPropertyChanged(nameof(CanRemoveSlot));
        OnPropertyChanged(nameof(CanApplySlotChanges));
    }

    private void OnDraftFieldChanged()
    {
        if (_isRecomputing || _isReplacingSlots || SelectedSlot is null)
        {
            return;
        }

        RecomputeDraft(CaptureInteraction());
    }

    partial void OnSelectedSlotChanged(TemplateSlotItem? value)
    {
        _selectedVmId = string.IsNullOrWhiteSpace(value?.SourceItem?.VmId) ? null : value.SourceItem.VmId;
        if (_isReplacingSlots)
        {
            return;
        }

        RecomputeDraft(null);
        RefreshComputedState();
    }

    partial void OnIsStatusVisibleChanged(bool value) => RefreshComputedState();

    partial void OnSlotNameChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotMemoryTextChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotCpuTextChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotSwitchesTextChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotBaseDiskIdChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotBaseDiskPathChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotVhdxSignatureChanged(string value) => OnDraftFieldChanged();
    partial void OnSlotGenerationTextChanged(string value) => OnDraftFieldChanged();

    private void OnSelfPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IsLoading) or nameof(ErrorMessage))
        {
            RefreshComputedState();
        }
    }

    private readonly record struct DraftInteraction(
        string VmName,
        string VmMemoryText,
        string VmCpuText,
        IReadOnlyList<string> SelectedSwitches,
        TemplateVhdxCatalogOption? SelectedVhdxCatalogOption);
}

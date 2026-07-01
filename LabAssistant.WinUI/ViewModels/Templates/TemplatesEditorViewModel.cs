using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.WinUI.Infrastructure;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

public partial class TemplatesEditorViewModel : ViewModelBase
{
    public TemplatesEditorViewModel()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsLoading) or nameof(ErrorMessage))
            {
                RefreshComputedState();
            }
        };
    }

    public Action? SaveRequested { get; set; }
    public Action? SaveAsRequested { get; set; }
    public Action? CancelRequested { get; set; }
    public Action? AddSlotRequested { get; set; }
    public Action? RemoveSlotRequested { get; set; }
    public Action? ApplySlotChangesRequested { get; set; }
    public Action? ValidateRequested { get; set; }

    [ObservableProperty] private string _templateName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ObservableCollection<TemplateSlotItem> _slots = [];
    [ObservableProperty] private TemplateSlotItem? _selectedSlot;
    [ObservableProperty] private bool _isDirty;
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
    [ObservableProperty] private bool _canSave;
    [ObservableProperty] private bool _canSaveAs;
    [ObservableProperty] private bool _canValidate;
    [ObservableProperty] private bool _canCancel = true;
    [ObservableProperty] private bool _canAddSlot;
    [ObservableProperty] private bool _canRemoveSlot;
    [ObservableProperty] private bool _canApplySlotChanges;

    internal TemplateVhdxCatalogOption? SelectedCatalogOption { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StatusVisibility => IsStatusVisible ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedSlotVisibility => SelectedSlot is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NoSelectionVisibility => SelectedSlot is null ? Visibility.Visible : Visibility.Collapsed;
    public string ErrorStateText => string.IsNullOrWhiteSpace(ErrorMessage) ? "No template editor errors." : ErrorMessage!;
    public string SelectedSlotHeading => SelectedSlot is null ? "No VM slot selected" : $"Editing {SelectedSlot.Name}";
    public string SelectedSlotSummary => SelectedSlot?.Description ?? "Select a VM slot to edit its configuration.";

    [RelayCommand] private void Save() => SaveRequested?.Invoke();
    [RelayCommand] private void SaveAs() => SaveAsRequested?.Invoke();
    [RelayCommand] private void Cancel() => CancelRequested?.Invoke();
    [RelayCommand] private void AddSlot() => AddSlotRequested?.Invoke();
    [RelayCommand] private void RemoveSlot() => RemoveSlotRequested?.Invoke();
    [RelayCommand] private void ApplySlotChanges() => ApplySlotChangesRequested?.Invoke();
    [RelayCommand] private void Validate() => ValidateRequested?.Invoke();

    public void RefreshComputedState()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(StatusVisibility));
        OnPropertyChanged(nameof(SelectedSlotVisibility));
        OnPropertyChanged(nameof(NoSelectionVisibility));
        OnPropertyChanged(nameof(ErrorStateText));
        OnPropertyChanged(nameof(SelectedSlotHeading));
        OnPropertyChanged(nameof(SelectedSlotSummary));
    }

    partial void OnSelectedSlotChanged(TemplateSlotItem? value) => RefreshComputedState();
    partial void OnIsStatusVisibleChanged(bool value) => RefreshComputedState();
}

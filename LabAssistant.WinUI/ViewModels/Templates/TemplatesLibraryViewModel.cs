using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.WinUI.Infrastructure;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

public partial class TemplatesLibraryViewModel : ViewModelBase
{
    public TemplatesLibraryViewModel()
    {
        StatusMessage = "Select a template to view details.";
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsLoading) or nameof(ErrorMessage))
            {
                RefreshComputedState();
            }
        };
    }

    public Action? ApplySearchRequested { get; set; }
    public Action? ClearSearchRequested { get; set; }
    public Action? LoadRequested { get; set; }
    public Action? CreateRequested { get; set; }
    public Action? CreateBuilderRequested { get; set; }
    public Action? RenameRequested { get; set; }
    public Action? DeleteRequested { get; set; }
    public Action? OpenInBuilderRequested { get; set; }
    public Action? OpenInEditorRequested { get; set; }
    public Action? ImportRequested { get; set; }
    public Action? ExportRequested { get; set; }

    [ObservableProperty] private ObservableCollection<TemplateListItem> _templates = [];
    [ObservableProperty] private TemplateListItem? _selectedTemplate;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _renameTemplateName = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _canApplySearch = true;
    [ObservableProperty] private bool _canClearSearch = true;
    [ObservableProperty] private bool _canLoad = true;
    [ObservableProperty] private bool _canCreate = true;
    [ObservableProperty] private bool _canCreateBuilder = true;
    [ObservableProperty] private bool _canRename;
    [ObservableProperty] private bool _canDelete;
    [ObservableProperty] private bool _canOpenInBuilder;
    [ObservableProperty] private bool _canOpenInEditor;
    [ObservableProperty] private bool _canImport = true;
    [ObservableProperty] private bool _canExport;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => !IsLoading && IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedTemplateVisibility => SelectedTemplate is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility NoSelectionVisibility => SelectedTemplate is null ? Visibility.Visible : Visibility.Collapsed;
    public string ErrorStateText => string.IsNullOrWhiteSpace(ErrorMessage) ? "No template library errors." : ErrorMessage!;
    public string EmptyStateText => "No templates are currently available. Create a new template or adjust the search filter.";
    public string SelectedTemplateTitle => SelectedTemplate?.Name ?? "No template selected";
    public string SelectedTemplateDescription => SelectedTemplate?.DescriptionOrFallback ?? "Select a template to review its details and actions.";
    public string SelectedTemplateMetadata => SelectedTemplate is null ? "No metadata available." : $"ID {SelectedTemplate.Id} • {SelectedTemplate.SlotCountDisplay}";
    public string SelectedTemplateLastModified => SelectedTemplate?.LastModifiedDisplay ?? "Last modified unavailable";
    public string RenameHintText => SelectedTemplate is null ? "Select a template to rename it." : "Rename the template display name and persist the change to the current file.";

    [RelayCommand] private void ApplySearch() => ApplySearchRequested?.Invoke();
    [RelayCommand] private void ClearSearch() { SearchText = string.Empty; ClearSearchRequested?.Invoke(); }
    [RelayCommand] private void Load() => LoadRequested?.Invoke();
    [RelayCommand] private void Create() => CreateRequested?.Invoke();
    [RelayCommand] private void CreateBuilder() => CreateBuilderRequested?.Invoke();
    [RelayCommand] private void Rename() => RenameRequested?.Invoke();
    [RelayCommand] private void Delete() => DeleteRequested?.Invoke();
    [RelayCommand] private void OpenInBuilder() => OpenInBuilderRequested?.Invoke();
    [RelayCommand] private void OpenInEditor() => OpenInEditorRequested?.Invoke();
    [RelayCommand] private void Import() => ImportRequested?.Invoke();
    [RelayCommand] private void Export() => ExportRequested?.Invoke();

    public void RefreshComputedState()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(SelectedTemplateVisibility));
        OnPropertyChanged(nameof(NoSelectionVisibility));
        OnPropertyChanged(nameof(ErrorStateText));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(SelectedTemplateTitle));
        OnPropertyChanged(nameof(SelectedTemplateDescription));
        OnPropertyChanged(nameof(SelectedTemplateMetadata));
        OnPropertyChanged(nameof(SelectedTemplateLastModified));
        OnPropertyChanged(nameof(RenameHintText));
    }

    partial void OnSelectedTemplateChanged(TemplateListItem? value)
    {
        RenameTemplateName = value?.Name ?? string.Empty;
        RefreshComputedState();
    }

    partial void OnIsEmptyChanged(bool value) => RefreshComputedState();
}

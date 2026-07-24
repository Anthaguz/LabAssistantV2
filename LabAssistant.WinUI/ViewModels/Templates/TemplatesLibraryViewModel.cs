using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Templates Library subview model. Full x:Bind MVVM: it owns the template inventory, search,
/// selection, and the library maintenance commands (open in editor/builder, create, rename, delete),
/// folding the logic of the dissolved library workspace controller/composition/bridge. It reaches
/// sibling subviews and dialogs through the injected <see cref="ITemplatesLibraryHost"/> seam, so it
/// is unit-testable without a dispatcher or Hyper-V. Registered transient; created and torn down with
/// the hosting <c>TemplatesPage</c>.
/// </summary>
public partial class TemplatesLibraryViewModel : ViewModelBase
{
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private ITemplatesLibraryHost? _host;
    private string? _selectedTemplateFilePath;
    private bool _isSyncingSelection;

    public TemplatesLibraryViewModel(ITemplatesCapabilityService templatesCapabilityService)
    {
        _templatesCapabilityService = templatesCapabilityService;
        StatusMessage = "Select a template to view details.";
        PropertyChanged += OnSelfPropertyChanged;
    }

    [ObservableProperty] private ObservableCollection<TemplateListItem> _templates = [];
    [ObservableProperty] private TemplateListItem? _selectedTemplate;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _renameTemplateName = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool ShowEmptyState => !IsLoading && IsEmpty;
    public bool HasSelectedTemplate => SelectedTemplate is not null;
    public string ErrorStateText => string.IsNullOrWhiteSpace(ErrorMessage) ? "No template library errors." : ErrorMessage!;
    public string EmptyStateText => "No templates are currently available. Create a new template or adjust the search filter.";
    public string SelectedTemplateTitle => SelectedTemplate?.Name ?? "No template selected";
    public string SelectedTemplateDescription => SelectedTemplate?.DescriptionOrFallback ?? "Select a template to review its details and actions.";
    public string SelectedTemplateMetadata => SelectedTemplate is null ? "No metadata available." : $"ID {SelectedTemplate.Id} • {SelectedTemplate.SlotCountDisplay}";
    public string SelectedTemplateLastModified => SelectedTemplate?.LastModifiedDisplay ?? "Last modified unavailable";
    public string RenameHintText => SelectedTemplate is null ? "Select a template to rename it." : "Rename the template display name and persist the change to the current file.";

    public bool CanApplySearch => !IsLoading;
    public bool CanClearSearch => !IsLoading;
    public bool CanLoad => !IsLoading;
    public bool CanCreate => !IsLoading;
    public bool CanCreateBuilder => !IsLoading;
    public bool CanOpenInEditor => SelectedTemplate is not null && !IsLoading;
    public bool CanRename => SelectedTemplate is not null && !IsLoading;
    public bool CanDelete => SelectedTemplate is not null && !IsLoading;
    public bool CanOpenInBuilder => SelectedTemplate?.ExecutionEngine == TemplateExecutionEngine.V2UnifiedPlanning && !IsLoading;

    /// <summary>Attaches the cross-subview host. Called by the page on navigation.</summary>
    internal void Attach(ITemplatesLibraryHost host) => _host = host;

    /// <summary>Detaches the cross-subview host. Called by the page on leave.</summary>
    internal void Detach() => _host = null;

    public override Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        IsInitialized = true;
        return ReloadLibraryAsync(forceRefresh: false);
    }

    public override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        _host = null;
        Templates.Clear();
        SelectedTemplate = null;
        _selectedTemplateFilePath = null;
        RenameTemplateName = string.Empty;
        SearchText = string.Empty;
        IsEmpty = true;
        IsLoading = false;
        ErrorMessage = null;
        StatusMessage = "Select a template to view details.";
        RefreshComputedState();
        IsInitialized = false;
    }

    /// <summary>
    /// Loads (or reloads) the template inventory using the current <see cref="SearchText"/>. When
    /// <paramref name="forceRefresh"/> is <see langword="false"/> an already-populated inventory is
    /// left untouched. Selection is preserved across reloads by file path.
    /// </summary>
    public async Task ReloadLibraryAsync(bool forceRefresh)
    {
        if (IsLoading && !forceRefresh)
        {
            return;
        }

        if (!forceRefresh && Templates.Count > 0)
        {
            RefreshComputedState();
            return;
        }

        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading templates...";
        RefreshComputedState();

        try
        {
            var result = await _templatesCapabilityService.LoadLibraryAsync(SearchText);
            var libraryStatusText = result.Items.Count == 0
                ? result.Errors.Count == 0
                    ? "No templates found in configured template folder."
                    : $"No templates loaded. {result.Errors[0]}"
                : result.Errors.Count == 0
                    ? $"Loaded {result.Items.Count} template(s)."
                    : $"Loaded {result.Items.Count} template(s) with warnings.";
            ApplyInventory(result.Items, libraryStatusText);
        }
        catch (Exception ex)
        {
            SetFailure($"Failed to load templates. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private Task ApplySearch() => ReloadLibraryAsync(forceRefresh: true);

    [RelayCommand]
    private Task ClearSearch()
    {
        SearchText = string.Empty;
        return ReloadLibraryAsync(forceRefresh: true);
    }

    [RelayCommand]
    private Task Load() => ReloadLibraryAsync(forceRefresh: true);

    [RelayCommand]
    private async Task OpenInEditor()
    {
        if (SelectedTemplate is null)
        {
            SetStatus("Select a template first.");
            return;
        }

        var filePath = SelectedTemplate.FilePath;
        IsLoading = true;
        RefreshComputedState();
        try
        {
            var document = await _templatesCapabilityService.LoadForEditorAsync(filePath);
            if (_host is not null)
            {
                await _host.ShowTemplateInEditorAsync(document, "Template loaded.");
            }
        }
        catch (Exception ex)
        {
            _host?.ReportEditorStatus($"Failed to open template. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private async Task OpenInBuilder()
    {
        if (SelectedTemplate is null)
        {
            SetStatus("Select a template first.");
            return;
        }

        if (SelectedTemplate.ExecutionEngine != TemplateExecutionEngine.V2UnifiedPlanning)
        {
            SetStatus("Only V2 templates open in Builder. Use the current Editor for V1/simple/legacy templates.");
            return;
        }

        var filePath = SelectedTemplate.FilePath;
        IsLoading = true;
        RefreshComputedState();
        try
        {
            var document = await _templatesCapabilityService.LoadForEditorAsync(filePath);
            if (_host is not null)
            {
                await _host.ShowTemplateInBuilderAsync(document);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to open V2 template in Builder. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private async Task Create()
    {
        IsLoading = true;
        RefreshComputedState();
        try
        {
            var document = await _templatesCapabilityService.CreateDraftAsync();
            if (_host is not null)
            {
                await _host.ShowTemplateInEditorAsync(document, "New template draft created.");
            }
        }
        catch (OperationCanceledException)
        {
            // Benign navigate-away cancellation; nothing to surface.
        }
        catch (Exception ex)
        {
            SetFailure($"Failed to create template draft. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private async Task CreateBuilder()
    {
        IsLoading = true;
        RefreshComputedState();
        try
        {
            if (_host is not null)
            {
                await _host.CreateTemplateBuilderDraftAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Benign navigate-away cancellation; nothing to surface.
        }
        catch (Exception ex)
        {
            SetFailure($"Failed to create Builder draft. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private async Task Rename()
    {
        if (SelectedTemplate is null)
        {
            SetStatus("Select a template first.");
            return;
        }

        var trimmedName = RenameTemplateName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            SetStatus("Enter a new template name first.");
            return;
        }

        var filePath = SelectedTemplate.FilePath;
        IsLoading = true;
        RefreshComputedState();
        try
        {
            var document = await _templatesCapabilityService.LoadForEditorAsync(filePath);
            document.Template.Name = trimmedName;
            var result = await _templatesCapabilityService.SaveAsync(document, filePath);
            SetStatus(result.UserMessage);
            if (result.Success)
            {
                await ReloadLibraryAsync(forceRefresh: true);
            }
        }
        catch (Exception ex)
        {
            SetFailure($"Failed to rename template. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedTemplate is null)
        {
            SetStatus("Select a template first.");
            return;
        }

        var selectedTemplate = SelectedTemplate;
        if (_host is null || !await _host.ConfirmDeleteTemplateAsync(selectedTemplate.SourceItem))
        {
            return;
        }

        IsLoading = true;
        RefreshComputedState();
        try
        {
            var result = await _templatesCapabilityService.DeleteAsync(selectedTemplate.FilePath);
            SetStatus(result.UserMessage);
            if (result.Success)
            {
                SelectedTemplate = null;
                _selectedTemplateFilePath = null;
                await ReloadLibraryAsync(forceRefresh: true);
            }
        }
        catch (OperationCanceledException)
        {
            // Benign navigate-away cancellation; nothing to surface.
        }
        catch (Exception ex)
        {
            SetFailure($"Failed to delete template. {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            RefreshComputedState();
        }
    }

    private void ApplyInventory(IReadOnlyList<TemplateLibraryItem> items, string statusText)
    {
        var selectedPath = _selectedTemplateFilePath;

        _isSyncingSelection = true;
        try
        {
            Templates.Clear();
            foreach (var item in items)
            {
                Templates.Add(TemplateListItem.FromLibraryItem(item));
            }

            SelectedTemplate = string.IsNullOrWhiteSpace(selectedPath)
                ? null
                : Templates.FirstOrDefault(item => string.Equals(item.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isSyncingSelection = false;
        }

        _selectedTemplateFilePath = SelectedTemplate?.FilePath;
        RenameTemplateName = SelectedTemplate?.Name ?? string.Empty;
        IsEmpty = Templates.Count == 0;
        ErrorMessage = null;
        StatusMessage = statusText;
        RefreshComputedState();
    }

    private void SetStatus(string statusText)
    {
        ErrorMessage = null;
        StatusMessage = statusText;
        RefreshComputedState();
    }

    private void SetFailure(string statusText)
    {
        ErrorMessage = statusText;
        StatusMessage = statusText;
        RefreshComputedState();
    }

    private void RefreshComputedState()
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(HasSelectedTemplate));
        OnPropertyChanged(nameof(ErrorStateText));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(SelectedTemplateTitle));
        OnPropertyChanged(nameof(SelectedTemplateDescription));
        OnPropertyChanged(nameof(SelectedTemplateMetadata));
        OnPropertyChanged(nameof(SelectedTemplateLastModified));
        OnPropertyChanged(nameof(RenameHintText));
        OnPropertyChanged(nameof(CanApplySearch));
        OnPropertyChanged(nameof(CanClearSearch));
        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(CanCreateBuilder));
        OnPropertyChanged(nameof(CanOpenInEditor));
        OnPropertyChanged(nameof(CanRename));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanOpenInBuilder));
    }

    partial void OnSelectedTemplateChanged(TemplateListItem? value)
    {
        if (!_isSyncingSelection)
        {
            _selectedTemplateFilePath = value?.FilePath;
            RenameTemplateName = value?.Name ?? string.Empty;
        }

        RefreshComputedState();
    }

    partial void OnIsEmptyChanged(bool value) => RefreshComputedState();

    private void OnSelfPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IsLoading) or nameof(ErrorMessage))
        {
            RefreshComputedState();
        }
    }
}

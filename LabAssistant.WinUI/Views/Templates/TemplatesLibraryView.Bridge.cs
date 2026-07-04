using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public readonly record struct TemplatesLibraryInteractionState(
    string SearchText,
    TemplateLibraryItem? SelectedTemplate,
    string RenameTemplateName);

public readonly record struct TemplatesLibraryViewState(
    string SearchText,
    string StatusText,
    TemplateLibraryItem? SelectedTemplate,
    bool CanApplySearch,
    bool CanClearSearch,
    bool CanLoad,
    bool CanOpenTemplate,
    bool CanCreateTemplate,
    bool CanRenameTemplate,
    bool CanOpenTemplateInBuilder,
    bool CanCreateBuilderTemplate,
    bool CanDeleteTemplate,
    bool CanImportTemplate,
    bool CanExportTemplate,
    bool IsLoading,
    bool IsEmpty,
    bool HasError);

public sealed partial class TemplatesLibraryView : UserControl
{
    private bool _isUpdatingSearchText;
    private bool _isUpdatingSelection;
    private bool _isBridgeAttached;

    public event EventHandler? SearchTextChanged;
    public event EventHandler? SelectedTemplateChanged;
    public event EventHandler? ApplySearchRequested;
    public event EventHandler? ClearSearchRequested;
    public event EventHandler? ReloadRequested;
    public event EventHandler? OpenTemplateRequested;
    public event EventHandler? CreateTemplateRequested;
    public event EventHandler? RenameTemplateRequested;
    public event EventHandler? OpenTemplateInBuilderRequested;
    public event EventHandler? CreateBuilderTemplateRequested;
    public event EventHandler? DeleteTemplateRequested;
    public event EventHandler? ImportTemplateRequested;
    public event EventHandler? ExportTemplateRequested;

    partial void InitializeBridge()
    {
        AttachBridge();
    }

    private void AttachBridge()
    {
        if (_isBridgeAttached)
        {
            return;
        }

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.ApplySearchRequested = () => ApplySearchRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.ClearSearchRequested = () => ClearSearchRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.LoadRequested = () => ReloadRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.CreateRequested = () => CreateTemplateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.CreateBuilderRequested = () => CreateBuilderTemplateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.RenameRequested = () => RenameTemplateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.DeleteRequested = () => DeleteTemplateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.OpenInBuilderRequested = () => OpenTemplateInBuilderRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.OpenInEditorRequested = () => OpenTemplateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.ImportRequested = () => ImportTemplateRequested?.Invoke(this, EventArgs.Empty);
        ViewModel.ExportRequested = () => ExportTemplateRequested?.Invoke(this, EventArgs.Empty);
        _isBridgeAttached = true;
    }

    private void DetachBridge()
    {
        if (!_isBridgeAttached)
        {
            return;
        }

        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _isBridgeAttached = false;
    }

    public void SetInventorySource(object? itemsSource)
    {
        var items = itemsSource as IEnumerable<TemplateLibraryItem> ?? Enumerable.Empty<TemplateLibraryItem>();
        var selectedPath = ViewModel.SelectedTemplate?.FilePath;
        ViewModel.Templates.Clear();
        foreach (var item in items)
        {
            ViewModel.Templates.Add(TemplateListItem.FromLibraryItem(item));
        }

        ViewModel.IsEmpty = ViewModel.Templates.Count == 0;
        SetSelectedTemplate(selectedPath);
        ViewModel.RefreshComputedState();
    }

    public TemplatesLibraryInteractionState CaptureInteractionState()
    {
        return new TemplatesLibraryInteractionState(
            ViewModel.SearchText,
            ViewModel.SelectedTemplate?.SourceItem,
            ViewModel.RenameTemplateName);
    }

    public void UpdateViewState(TemplatesLibraryViewState state)
    {
        _isUpdatingSearchText = true;
        try
        {
            ViewModel.SearchText = state.SearchText;
        }
        finally
        {
            _isUpdatingSearchText = false;
        }

        ViewModel.StatusMessage = state.StatusText;
        ViewModel.CanApplySearch = state.CanApplySearch;
        ViewModel.CanClearSearch = state.CanClearSearch;
        ViewModel.CanLoad = state.CanLoad;
        ViewModel.CanOpenInEditor = state.CanOpenTemplate;
        ViewModel.CanCreate = state.CanCreateTemplate;
        ViewModel.CanCreateBuilder = state.CanCreateBuilderTemplate;
        ViewModel.CanRename = state.CanRenameTemplate;
        ViewModel.CanOpenInBuilder = state.CanOpenTemplateInBuilder;
        ViewModel.CanDelete = state.CanDeleteTemplate;
        ViewModel.CanImport = state.CanImportTemplate;
        ViewModel.CanExport = state.CanExportTemplate;
        ViewModel.IsLoading = state.IsLoading;
        ViewModel.IsEmpty = state.IsEmpty;
        ViewModel.ErrorMessage = state.HasError ? state.StatusText : null;
        SetSelectedTemplate(state.SelectedTemplate?.FilePath);
        ViewModel.RefreshComputedState();
    }

    private void SetSelectedTemplate(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            if (ViewModel.SelectedTemplate is null)
            {
                return;
            }

            _isUpdatingSelection = true;
            try
            {
                ViewModel.SelectedTemplate = null;
            }
            finally
            {
                _isUpdatingSelection = false;
            }

            return;
        }

        var nextSelection = ViewModel.Templates.FirstOrDefault(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (ReferenceEquals(ViewModel.SelectedTemplate, nextSelection))
        {
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            ViewModel.SelectedTemplate = nextSelection;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplatesLibraryViewModel.SearchText) && !_isUpdatingSearchText)
        {
            SearchTextChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.PropertyName == nameof(TemplatesLibraryViewModel.SelectedTemplate) && !_isUpdatingSelection)
        {
            SelectedTemplateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

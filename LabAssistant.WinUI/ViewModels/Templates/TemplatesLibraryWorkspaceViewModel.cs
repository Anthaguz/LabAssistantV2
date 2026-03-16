using System.Collections.ObjectModel;
using LabAssistant.Business.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal sealed class TemplatesLibraryWorkspaceViewModel
{
    public ObservableCollection<TemplateLibraryItem> Items { get; } = [];

    public string SearchQuery { get; private set; } = string.Empty;

    public string StatusText { get; private set; } = "No templates loaded.";

    public TemplateLibraryItem? SelectedItem { get; private set; }

    public string? SelectedTemplateFilePath { get; private set; }

    public bool IsLoading { get; private set; }

    public bool HasErrorState { get; private set; }

    public void SetSearchQuery(string? searchQuery)
    {
        SearchQuery = searchQuery ?? string.Empty;
    }

    public void BeginLoading()
    {
        IsLoading = true;
        HasErrorState = false;
        StatusText = "Loading templates...";
    }

    public void ApplyInventory(IReadOnlyList<TemplateLibraryItem> items, string statusText)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        SelectedItem = string.IsNullOrWhiteSpace(SelectedTemplateFilePath)
            ? null
            : Items.FirstOrDefault(item => string.Equals(item.FilePath, SelectedTemplateFilePath, StringComparison.OrdinalIgnoreCase));
        SelectedTemplateFilePath = SelectedItem?.FilePath;
        IsLoading = false;
        HasErrorState = false;
        StatusText = statusText;
    }

    public void SetSelectedItem(TemplateLibraryItem? selectedItem)
    {
        SelectedItem = selectedItem;
        SelectedTemplateFilePath = selectedItem?.FilePath;
    }

    public void SetStatus(string statusText)
    {
        IsLoading = false;
        HasErrorState = false;
        StatusText = statusText;
    }

    public void SetFailure(string statusText)
    {
        IsLoading = false;
        HasErrorState = true;
        StatusText = statusText;
    }
}

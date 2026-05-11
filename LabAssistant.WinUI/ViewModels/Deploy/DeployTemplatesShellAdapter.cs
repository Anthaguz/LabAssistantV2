using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Exposes the narrow Templates-side interactions that Deploy may use without depending on Templates composition details.
/// </summary>
internal sealed class DeployTemplatesShellAdapter
{
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Func<IReadOnlyList<TemplateLibraryItem>> _getLibraryItems;
    private readonly Func<bool, Task> _ensureLibraryAsync;
    private readonly Func<string, Task<TemplateEditorDocument>> _loadTemplateForEditorAsync;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;

    public DeployTemplatesShellAdapter(
        object? itemsSource,
        Func<bool> isTemplatesLoading,
        Func<IReadOnlyList<TemplateLibraryItem>> getLibraryItems,
        Func<bool, Task> ensureLibraryAsync,
        Func<string, Task<TemplateEditorDocument>> loadTemplateForEditorAsync,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync)
    {
        ItemsSource = itemsSource;
        _isTemplatesLoading = isTemplatesLoading;
        _getLibraryItems = getLibraryItems;
        _ensureLibraryAsync = ensureLibraryAsync;
        _loadTemplateForEditorAsync = loadTemplateForEditorAsync;
        _showTemplateEditorAsync = showTemplateEditorAsync;
    }

    public object? ItemsSource { get; }

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public IReadOnlyList<TemplateLibraryItem> GetLibraryItems() => _getLibraryItems();

    public Task EnsureLibraryAsync(bool forceRefresh) => _ensureLibraryAsync(forceRefresh);

    public Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath) => _loadTemplateForEditorAsync(filePath);

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) =>
        _showTemplateEditorAsync(document, statusText);
}

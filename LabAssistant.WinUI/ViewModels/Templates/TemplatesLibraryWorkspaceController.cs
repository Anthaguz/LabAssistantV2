using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesLibraryWorkspaceControllerHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyWorkspaceState();

    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    void SetTemplateEditorStatus(string statusText);

    Task<string?> PickTemplateFileForOpenAsync();

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem);

    void ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items);
}

internal sealed class TemplatesLibraryWorkspaceController
{
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly TemplatesLibraryWorkspaceViewModel _workspace;
    private readonly ITemplatesLibraryWorkspaceControllerHost _host;

    public TemplatesLibraryWorkspaceController(
        ITemplatesCapabilityService templatesCapabilityService,
        TemplatesLibraryWorkspaceViewModel workspace,
        ITemplatesLibraryWorkspaceControllerHost host)
    {
        _templatesCapabilityService = templatesCapabilityService;
        _workspace = workspace;
        _host = host;
    }

    public async Task EnsureLibraryAsync(bool forceRefresh)
    {
        if (_host.IsTemplatesLoading && !forceRefresh)
        {
            return;
        }

        if (!forceRefresh && _workspace.Items.Count > 0)
        {
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        _workspace.BeginLoading();
        _host.ApplyWorkspaceState();

        try
        {
            var result = await _templatesCapabilityService.LoadLibraryAsync(_workspace.SearchQuery);
            var libraryStatusText = result.Items.Count == 0
                ? result.Errors.Count == 0
                    ? "No templates found in configured template folder."
                    : $"No templates loaded. {result.Errors[0]}"
                : result.Errors.Count == 0
                    ? $"Loaded {result.Items.Count} template(s)."
                    : $"Loaded {result.Items.Count} template(s) with warnings.";
            _workspace.ApplyInventory(result.Items, libraryStatusText);
            _host.ReconcileDeployTemplateSelection(_workspace.Items);
        }
        catch (Exception ex)
        {
            _workspace.SetFailure($"Failed to load templates. {ex.Message}");
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public void HandleSearchTextChanged(string? searchQuery)
    {
        _workspace.SetSearchQuery(searchQuery);
        _host.ApplyWorkspaceState();
    }

    public void HandleSelectionChanged(TemplateLibraryItem? selectedItem)
    {
        _workspace.SetSelectedItem(selectedItem);
        _host.ApplyWorkspaceState();
    }

    public Task ApplySearchAsync()
    {
        return EnsureLibraryAsync(forceRefresh: true);
    }

    public async Task ClearSearchAsync()
    {
        _workspace.SetSearchQuery(string.Empty);
        _host.ApplyWorkspaceState();
        await EnsureLibraryAsync(forceRefresh: true);
    }

    public Task ReloadAsync()
    {
        return EnsureLibraryAsync(forceRefresh: true);
    }

    public async Task OpenSelectedTemplateInEditorAsync()
    {
        if (_workspace.SelectedItem is null)
        {
            _workspace.SetStatus("Select a template first.");
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        try
        {
            var document = await _templatesCapabilityService.LoadForEditorAsync(_workspace.SelectedItem.FilePath);
            await _host.ShowTemplateEditorAsync(document, "Template loaded.");
        }
        catch (Exception ex)
        {
            _host.SetTemplateEditorStatus($"Failed to open template. {ex.Message}");
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public async Task CreateTemplateAsync()
    {
        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        try
        {
            var document = await _templatesCapabilityService.CreateDraftAsync();
            await _host.ShowTemplateEditorAsync(document, "New template draft created.");
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public async Task DeleteSelectedTemplateAsync()
    {
        if (_workspace.SelectedItem is null)
        {
            _workspace.SetStatus("Select a template first.");
            _host.ApplyWorkspaceState();
            return;
        }

        var selectedTemplate = _workspace.SelectedItem;
        if (!await _host.ShowDeleteTemplateConfirmationDialogAsync(selectedTemplate))
        {
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        try
        {
            var result = await _templatesCapabilityService.DeleteAsync(selectedTemplate.FilePath);
            _workspace.SetStatus(result.UserMessage);
            if (result.Success)
            {
                _workspace.SetSelectedItem(null);
                await EnsureLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public async Task ImportTemplateAsync()
    {
        var sourcePath = await _host.PickTemplateFileForOpenAsync();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            _workspace.SetStatus("Import cancelled.");
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        try
        {
            var result = await _templatesCapabilityService.ImportAsync(sourcePath);
            _workspace.SetStatus(result.UserMessage);
            if (result.Success)
            {
                await EnsureLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }

    public async Task ExportSelectedTemplateAsync()
    {
        if (_workspace.SelectedItem is null)
        {
            _workspace.SetStatus("Select a template first.");
            _host.ApplyWorkspaceState();
            return;
        }

        var suggestedName = Path.GetFileName(_workspace.SelectedItem.FilePath);
        var destinationPath = await _host.PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            _workspace.SetStatus("Export cancelled.");
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        try
        {
            var result = await _templatesCapabilityService.ExportAsync(_workspace.SelectedItem.FilePath, destinationPath);
            _workspace.SetStatus(result.UserMessage);
        }
        finally
        {
            if (ownsLoadingState)
            {
                _host.SetTemplatesLoading(false);
            }

            _host.ApplyWorkspaceState();
        }
    }
}

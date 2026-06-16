using LabAssistant.Business.Templates;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal interface ITemplatesBuilderWorkspaceControllerHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyWorkspaceState();

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh);

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    void NavigateToBuilder();

    void NavigateToLibrary();
}

internal sealed class TemplatesBuilderWorkspaceController
{
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly TemplatesBuilderWorkspaceViewModel _workspace;
    private readonly ITemplatesBuilderWorkspaceControllerHost _host;
    private TemplatesBuilderReferenceData _referenceData = new(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>());

    public TemplatesBuilderWorkspaceController(
        ITemplatesCapabilityService templatesCapabilityService,
        TemplatesBuilderWorkspaceViewModel workspace,
        ITemplatesBuilderWorkspaceControllerHost host)
    {
        _templatesCapabilityService = templatesCapabilityService;
        _workspace = workspace;
        _host = host;
    }

    public async Task CreateDraftAsync()
    {
        await EnsureReferenceDataAsync(forceRefresh: false);
        _workspace.LoadNewDraft(TemplatesBuilderDraftMapper.CreateSuggestedDraft(_referenceData));
        _host.NavigateToBuilder();
        _host.ApplyWorkspaceState();
    }

    public async Task ShowDocumentAsync(TemplateEditorDocument document)
    {
        await EnsureReferenceDataAsync(forceRefresh: false);
        _workspace.LoadDocument(document);
        _host.NavigateToBuilder();
        _host.ApplyWorkspaceState();
    }

    public async Task ApplySuggestionsAsync()
    {
        await EnsureReferenceDataAsync(forceRefresh: true);
        _workspace.ApplyDraft(TemplatesBuilderDraftMapper.CreateSuggestedDraft(_referenceData));
        _workspace.SetStatus("Deterministic suggestions applied. Review and confirm before saving.");
        _host.ApplyWorkspaceState();
    }

    public async Task ValidateAsync()
    {
        if (!_workspace.HasActiveDraft)
        {
            _workspace.SetStatus("Create or open a V2 Builder draft first.");
            _host.ApplyWorkspaceState();
            return;
        }

        var build = BuildCurrentDocument();
        if (build.Document is null)
        {
            _workspace.SetStatus("Validation failed: " + string.Join(" ", build.Errors));
            _host.ApplyWorkspaceState();
            return;
        }

        var result = await _templatesCapabilityService.ValidateAsync(build.Document);
        _workspace.SetStatus(result.IsValid
            ? "V2 Builder draft validation passed."
            : "Validation failed: " + string.Join(" ", result.Errors));
        _host.ApplyWorkspaceState();
    }

    public async Task SaveAsync()
    {
        if (!_workspace.HasActiveDraft)
        {
            _workspace.SetStatus("Create or open a V2 Builder draft first.");
            _host.ApplyWorkspaceState();
            return;
        }

        if (!_workspace.IsSaveConfirmed)
        {
            _workspace.SetStatus("Confirm the visible Builder draft before saving.");
            _host.ApplyWorkspaceState();
            return;
        }

        var build = BuildCurrentDocument();
        if (build.Document is null)
        {
            _workspace.SetStatus("Save blocked: " + string.Join(" ", build.Errors));
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        _host.ApplyWorkspaceState();

        try
        {
            var result = await _templatesCapabilityService.SaveAsync(build.Document);
            _workspace.SetStatus(result.UserMessage);
            if (result.Success && !string.IsNullOrWhiteSpace(result.FilePath))
            {
                _workspace.SetSavedDocument(build.Document, result.FilePath);
                await _host.EnsureTemplatesLibraryAsync(forceRefresh: true);
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

    public async Task SaveAsAsync()
    {
        if (!_workspace.HasActiveDraft)
        {
            _workspace.SetStatus("Create or open a V2 Builder draft first.");
            _host.ApplyWorkspaceState();
            return;
        }

        if (!_workspace.IsSaveConfirmed)
        {
            _workspace.SetStatus("Confirm the visible Builder draft before saving.");
            _host.ApplyWorkspaceState();
            return;
        }

        var build = BuildCurrentDocument();
        if (build.Document is null)
        {
            _workspace.SetStatus("Save As blocked: " + string.Join(" ", build.Errors));
            _host.ApplyWorkspaceState();
            return;
        }

        var suggestedName = string.IsNullOrWhiteSpace(build.Document.Template.Name)
            ? "v2-lab-template"
            : build.Document.Template.Name;
        var destinationPath = await _host.PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            _workspace.SetStatus("Save As cancelled.");
            _host.ApplyWorkspaceState();
            return;
        }

        var ownsLoadingState = !_host.IsTemplatesLoading;
        if (ownsLoadingState)
        {
            _host.SetTemplatesLoading(true);
        }

        _host.ApplyWorkspaceState();

        try
        {
            var result = await _templatesCapabilityService.SaveAsync(build.Document, destinationPath, saveAs: true);
            _workspace.SetStatus(result.UserMessage);
            if (result.Success && !string.IsNullOrWhiteSpace(result.FilePath))
            {
                _workspace.SetSavedDocument(build.Document, result.FilePath);
                await _host.EnsureTemplatesLibraryAsync(forceRefresh: true);
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

    private async Task EnsureReferenceDataAsync(bool forceRefresh)
    {
        _referenceData = await _host.LoadReferenceDataAsync(forceRefresh);
        _workspace.SetReferenceData(_referenceData);
    }

    private TemplatesBuilderDraftBuildResult BuildCurrentDocument()
    {
        return TemplatesBuilderDraftMapper.BuildDocument(
            _workspace.CaptureDraft(),
            _workspace.TemplateId,
            _workspace.TemplateRevision,
            _workspace.CreatedWithAppVersion,
            _workspace.SourceFilePath,
            _workspace.PreservedTrusts);
    }
}

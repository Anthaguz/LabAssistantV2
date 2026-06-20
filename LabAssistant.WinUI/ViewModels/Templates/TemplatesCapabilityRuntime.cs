using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesWorkspaceShellBridge
{
    bool IsTemplatesCapabilityActive { get; }

    bool IsTemplatesLibraryActive { get; }

    bool IsTemplatesEditorActive { get; }

    bool IsTemplatesBuilderActive { get; }
}

internal sealed class TemplatesWorkspaceShellBridge : ITemplatesWorkspaceShellBridge
{
    private readonly Func<bool> _isTemplatesCapabilityActive;
    private readonly Func<bool> _isTemplatesLibraryActive;
    private readonly Func<bool> _isTemplatesEditorActive;
    private readonly Func<bool> _isTemplatesBuilderActive;

    public TemplatesWorkspaceShellBridge(
        Func<bool> isTemplatesCapabilityActive,
        Func<bool> isTemplatesLibraryActive,
        Func<bool> isTemplatesEditorActive,
        Func<bool> isTemplatesBuilderActive)
    {
        _isTemplatesCapabilityActive = isTemplatesCapabilityActive;
        _isTemplatesLibraryActive = isTemplatesLibraryActive;
        _isTemplatesEditorActive = isTemplatesEditorActive;
        _isTemplatesBuilderActive = isTemplatesBuilderActive;
    }

    public bool IsTemplatesCapabilityActive => _isTemplatesCapabilityActive();

    public bool IsTemplatesLibraryActive => _isTemplatesLibraryActive();

    public bool IsTemplatesEditorActive => _isTemplatesEditorActive();

    public bool IsTemplatesBuilderActive => _isTemplatesBuilderActive();
}

internal sealed class TemplatesCapabilityRuntime
{
    private readonly FrameworkElement _workspaceHost;
    private readonly TemplatesLibraryWorkspaceComposition _libraryComposition;
    private readonly TemplatesEditorWorkspaceComposition _editorComposition;
    private readonly TemplatesBuilderWorkspaceComposition _builderComposition;
    private readonly ITemplatesWorkspaceShellBridge _shellBridge;
    private readonly Func<Task<IReadOnlyList<string>>> _loadAvailableVmSwitchesAsync;
    private readonly Func<Task<IReadOnlyList<V2AvailableSwitchInfo>>> _loadAvailableVmSwitchInfoAsync;
    private readonly Func<Task<IReadOnlyList<TemplateVhdxCatalogOption>>> _loadVhdxCatalogOptionsAsync;
    private readonly Action _refreshDeployTemplatesLoadingState;
    private readonly List<TemplateVhdxCatalogOption> _templateVhdxCatalogOptions = [];
    private IReadOnlyList<string> _templateAvailableSwitches = Array.Empty<string>();
    private IReadOnlyList<V2AvailableSwitchInfo> _templateAvailableSwitchInfo = Array.Empty<V2AvailableSwitchInfo>();
    private bool _isLoading;

    public TemplatesCapabilityRuntime(
        FrameworkElement workspaceHost,
        TemplatesLibraryWorkspaceComposition libraryComposition,
        TemplatesEditorWorkspaceComposition editorComposition,
        TemplatesBuilderWorkspaceComposition builderComposition,
        ITemplatesWorkspaceShellBridge shellBridge,
        Func<Task<IReadOnlyList<string>>> loadAvailableVmSwitchesAsync,
        Func<Task<IReadOnlyList<V2AvailableSwitchInfo>>> loadAvailableVmSwitchInfoAsync,
        Func<Task<IReadOnlyList<TemplateVhdxCatalogOption>>> loadVhdxCatalogOptionsAsync,
        Action refreshDeployTemplatesLoadingState)
    {
        _workspaceHost = workspaceHost;
        _libraryComposition = libraryComposition;
        _editorComposition = editorComposition;
        _builderComposition = builderComposition;
        _shellBridge = shellBridge;
        _loadAvailableVmSwitchesAsync = loadAvailableVmSwitchesAsync;
        _loadAvailableVmSwitchInfoAsync = loadAvailableVmSwitchInfoAsync;
        _loadVhdxCatalogOptionsAsync = loadVhdxCatalogOptionsAsync;
        _refreshDeployTemplatesLoadingState = refreshDeployTemplatesLoadingState;
    }

    public IList<TemplateLibraryItem> LibraryItems => _libraryComposition.LibraryItems;

    public string LibrarySearchQuery => _libraryComposition.SearchQuery;

    public TemplateLibraryItem? SelectedLibraryItem => _libraryComposition.SelectedItem;

    public bool IsLoading => _isLoading;

    public bool HasActiveEditorDocument => _editorComposition.HasActiveDocument;

    public TemplateEditorDocument? ActiveEditorDocument => _editorComposition.ActiveDocument;

    public IReadOnlyList<VmTemplate> EditorVmEntries => _editorComposition.VmEntries;

    public VmTemplate? SelectedEditorVmEntry => _editorComposition.SelectedVmEntry;

    public TemplatesEditorVmDraftSnapshot CaptureEditorVmDraftState() => _editorComposition.CaptureVmDraftState();

    public Task EnsureLibraryAsync(bool forceRefresh) => _libraryComposition.EnsureLibraryAsync(forceRefresh);

    public Task ShowEditorDocumentAsync(TemplateEditorDocument document, string statusText) => _editorComposition.ShowDocumentAsync(document, statusText);

    public Task ShowBuilderDocumentAsync(TemplateEditorDocument document) => _builderComposition.ShowDocumentAsync(document);

    public Task CreateBuilderDraftAsync() => _builderComposition.CreateDraftAsync();

    public void SetEditorStatus(string statusText) => _editorComposition.SetStatus(statusText);

    public TemplatesEditorDocumentHeaderInteractionState CaptureEditorDocumentHeaderState() => _editorComposition.CaptureDocumentHeaderState();

    public void ReplaceEditorVmEntries(IReadOnlyList<VmTemplate> vmEntries) => _editorComposition.ReplaceVmEntries(vmEntries);

    public bool AddEditorVmEntry() => _editorComposition.AddVmEntry();

    public Task RemoveSelectedEditorVmEntryAsync() => _editorComposition.RemoveSelectedVmEntryAsync();

    public bool ApplySelectedEditorVmDraft(bool showSuccessStatus) => _editorComposition.ApplySelectedVmDraft(showSuccessStatus);

    public Task SaveEditorAsync() => _editorComposition.SaveAsync();

    public Task SaveEditorAsAsync() => _editorComposition.SaveAsAsync();

    public Task ValidateEditorAsync() => _editorComposition.ValidateAsync();

    public void SetLoading(bool isLoading)
    {
        _isLoading = isLoading;
        ApplyUiState();
        _refreshDeployTemplatesLoadingState();
    }

    public async Task EnsureEditorReferenceDataAsync(bool forceRefresh)
    {
        await EnsureAvailableVmSwitchesAsync(forceRefresh);
        await EnsureVhdxCatalogOptionsAsync(forceRefresh);
    }

    public async Task<TemplatesEditorReferenceData> LoadEditorReferenceDataAsync(bool forceRefresh)
    {
        await EnsureEditorReferenceDataAsync(forceRefresh);
        return new TemplatesEditorReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);
    }

    public async Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh)
    {
        await EnsureEditorReferenceDataAsync(forceRefresh);
        return new TemplatesBuilderReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions, _templateAvailableSwitchInfo);
    }

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _editorComposition.ApplyShellState(_shellBridge.IsTemplatesEditorActive);
        _builderComposition.ApplyShellState(_shellBridge.IsTemplatesBuilderActive);
        _libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);
    }

    public void ApplyUiState()
    {
        _libraryComposition.ApplyUiState(_isLoading, _libraryComposition.SelectedItem is not null);
        _editorComposition.RefreshUiState();
        _builderComposition.RefreshUiState();
    }

    private async Task EnsureAvailableVmSwitchesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateAvailableSwitches.Count > 0)
        {
            return;
        }

        _templateAvailableSwitchInfo = NormalizeSwitchInfo(await _loadAvailableVmSwitchInfoAsync());
        if (_templateAvailableSwitchInfo.Count > 0)
        {
            _templateAvailableSwitches = _templateAvailableSwitchInfo.Select(item => item.Name).ToList();
        }
        else
        {
            _templateAvailableSwitches = await _loadAvailableVmSwitchesAsync();
            _templateAvailableSwitchInfo = _templateAvailableSwitches
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => new V2AvailableSwitchInfo { Name = name.Trim(), SwitchType = "Unknown" })
                .ToList();
        }

        ApplyEditorReferenceData();
    }

    private static IReadOnlyList<V2AvailableSwitchInfo> NormalizeSwitchInfo(IReadOnlyList<V2AvailableSwitchInfo>? switches)
        => switches?
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var item = group.First();
                return new V2AvailableSwitchInfo
                {
                    Name = item.Name.Trim(),
                    SwitchType = string.IsNullOrWhiteSpace(item.SwitchType) ? "Unknown" : item.SwitchType.Trim()
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private async Task EnsureVhdxCatalogOptionsAsync(bool forceRefresh)
    {
        if (!forceRefresh && _templateVhdxCatalogOptions.Count > 0)
        {
            return;
        }

        _templateVhdxCatalogOptions.Clear();
        var items = await _loadVhdxCatalogOptionsAsync();
        _templateVhdxCatalogOptions.AddRange(items);
        ApplyEditorReferenceData();
    }

    private void ApplyEditorReferenceData()
    {
        _editorComposition.SetVmReferenceData(_templateAvailableSwitches, _templateVhdxCatalogOptions);
    }
}

using System;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Capability page that owns the Templates surface while it is the active shell content. Hosts the
/// Library, Editor, and Builder subviews in a tab view, implements <see cref="ICapabilityPage"/> so
/// the shell can forward subview changes, and mediates the cross-subview links (Library opening a
/// template in the Editor or Builder, the Editor routing back to the Library) via the
/// <see cref="ITemplatesLibraryHost"/> and <see cref="ITemplatesEditorHost"/> seams the Library and
/// Editor view models depend on. It replaces the former app-lifetime <c>TemplatesCapabilityRuntime</c>
/// plus delegate-bag composition that lived in <c>MainWindow</c>.
/// </summary>
/// <remarks>
/// The Library and Editor lanes are full x:Bind MVVM: their view models own their state, commands, and
/// contractual logic, and are bound directly by the subviews (which resolve their own transient view
/// models from DI). The Builder lane is retained on its preserved workspace composition as an interim
/// adapter until it is migrated to MVVM; this page owns that composition for the Builder's lifetime.
/// Because the page is transient, the pending editor document handed off by Deploy is drained from the
/// app-lifetime <see cref="ITemplateEditorHandoff"/> mailbox on entry; a pending document always takes
/// precedence over the requested initial subview.
/// </remarks>
public sealed partial class TemplatesPage : Page, ICapabilityPage, ITemplatesLibraryHost, ITemplatesEditorHost, ITemplatesBuilderHost
{
    private IShellHost? _shellHost;
    private ITemplateEditorHandoff? _handoff;
    private TemplatesReferenceDataService? _referenceDataService;
    private bool _isUpdatingSubviewSelection;

    public TemplatesPage()
    {
        InitializeComponent();
    }

    private TemplatesLibraryViewModel LibraryViewModel => LibraryViewHost.ViewModel;

    private TemplatesEditorViewModel EditorViewModel => EditorViewHost.ViewModel;

    private TemplatesBuilderViewModel BuilderViewModel => BuilderViewHost.ViewModel;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var initialRoute = ShellRouteKeys.TemplatesLibrary;
        if (e.Parameter is ShellNavigationRequest request)
        {
            _shellHost = request.Host;
            initialRoute = request.RouteKey;
        }

        var services = App.Services;
        _referenceDataService = services.GetRequiredService<TemplatesReferenceDataService>();
        _handoff = services.GetRequiredService<ITemplateEditorHandoff>();

        LibraryViewModel.Attach(this);
        EditorViewModel.Attach(_referenceDataService, this);
        BuilderViewModel.Attach(this);

        // A document handed off by Deploy (possibly while no page was alive) always wins over the
        // requested initial subview: show it in the Editor and route there.
        if (_handoff.TryTakePendingDocument(out var pendingDocument, out var pendingStatusText))
        {
            SelectSubviewTab(ShellRouteKeys.TemplatesEditor);
            _ = EditorViewModel.ShowDocumentAsync(pendingDocument, pendingStatusText);
        }
        else
        {
            SelectSubviewTab(initialRoute);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        LibraryViewModel.Detach();
        EditorViewModel.Detach();
        BuilderViewModel.Detach();
        _referenceDataService = null;
        _handoff = null;
        _shellHost = null;
    }

    void ICapabilityPage.ShowSubview(string routeKey) => SelectSubviewTab(routeKey);

    async Task ITemplatesLibraryHost.ShowTemplateInEditorAsync(TemplateEditorDocument document, string statusText)
    {
        await EditorViewModel.ShowDocumentAsync(document, statusText);
        NavigateToSubview(ShellRouteKeys.TemplatesEditor);
    }

    void ITemplatesLibraryHost.ReportEditorStatus(string statusText) => EditorViewModel.ReportStatus(statusText);

    Task ITemplatesLibraryHost.ShowTemplateInBuilderAsync(TemplateEditorDocument document) =>
        BuilderViewModel.ShowDocumentAsync(document);

    Task ITemplatesLibraryHost.CreateTemplateBuilderDraftAsync() =>
        BuilderViewModel.CreateDraftAsync();

    Task<bool> ITemplatesLibraryHost.ConfirmDeleteTemplateAsync(TemplateLibraryItem templateItem) =>
        _shellHost?.Dialogs.ShowDeleteTemplateConfirmationDialogAsync(templateItem) ?? Task.FromResult(false);

    void ITemplatesEditorHost.NavigateToLibrary() => NavigateToSubview(ShellRouteKeys.TemplatesLibrary);

    Task ITemplatesEditorHost.ReloadLibraryAsync(bool forceRefresh) => LibraryViewModel.ReloadLibraryAsync(forceRefresh);

    Task<bool> ITemplatesEditorHost.ConfirmRemoveVmAsync(string vmName) =>
        _shellHost?.Dialogs.ShowRemoveTemplateVmConfirmationDialogAsync(vmName) ?? Task.FromResult(false);

    Task<TemplatesBuilderReferenceData> ITemplatesBuilderHost.LoadBuilderReferenceDataAsync(bool forceRefresh) =>
        _referenceDataService is null
            ? Task.FromResult(new TemplatesBuilderReferenceData(Array.Empty<string>(), Array.Empty<TemplateVhdxCatalogOption>()))
            : _referenceDataService.LoadBuilderReferenceDataAsync(forceRefresh);

    Task ITemplatesBuilderHost.ReloadLibraryAsync(bool forceRefresh) => LibraryViewModel.ReloadLibraryAsync(forceRefresh);

    Task<string?> ITemplatesBuilderHost.PickTemplateFileForSaveAsync(string suggestedFileName) =>
        _shellHost?.Dialogs.PickTemplateFileForSaveAsync(suggestedFileName) ?? Task.FromResult<string?>(null);

    void ITemplatesBuilderHost.NavigateToBuilder() => NavigateToSubview(ShellRouteKeys.TemplatesBuilder);

    void ITemplatesBuilderHost.NavigateToLibrary() => NavigateToSubview(ShellRouteKeys.TemplatesLibrary);

    private void NavigateToSubview(string routeKey) => _shellHost?.NavigateToRoute(routeKey);

    private void SubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSubviewSelection || SubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        var routeKey = ReferenceEquals(selectedTab, EditorTabViewItem)
            ? ShellRouteKeys.TemplatesEditor
            : ReferenceEquals(selectedTab, BuilderTabViewItem)
                ? ShellRouteKeys.TemplatesBuilder
                : ShellRouteKeys.TemplatesLibrary;

        _shellHost?.ReportActiveSubview(routeKey);
    }

    private void SelectSubviewTab(string routeKey)
    {
        var targetTab = string.Equals(routeKey, ShellRouteKeys.TemplatesEditor, StringComparison.Ordinal)
            ? EditorTabViewItem
            : string.Equals(routeKey, ShellRouteKeys.TemplatesBuilder, StringComparison.Ordinal)
                ? BuilderTabViewItem
                : LibraryTabViewItem;

        if (ReferenceEquals(SubviewTabView.SelectedItem, targetTab))
        {
            return;
        }

        _isUpdatingSubviewSelection = true;
        try
        {
            SubviewTabView.SelectedItem = targetTab;
        }
        finally
        {
            _isUpdatingSubviewSelection = false;
        }
    }
}

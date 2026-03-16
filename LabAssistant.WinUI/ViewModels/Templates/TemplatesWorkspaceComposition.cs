using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesWorkspaceShellBridge
{
    bool IsTemplatesCapabilityActive { get; }

    bool IsTemplatesLibraryActive { get; }

    bool IsTemplatesEditorActive { get; }
}

internal interface ITemplatesWorkspaceHost
{
    void EnsureTemplatesLibraryLoaded();
}

internal sealed class TemplatesWorkspaceShellBridge : ITemplatesWorkspaceShellBridge
{
    private readonly Func<bool> _isTemplatesCapabilityActive;
    private readonly Func<bool> _isTemplatesLibraryActive;
    private readonly Func<bool> _isTemplatesEditorActive;

    public TemplatesWorkspaceShellBridge(
        Func<bool> isTemplatesCapabilityActive,
        Func<bool> isTemplatesLibraryActive,
        Func<bool> isTemplatesEditorActive)
    {
        _isTemplatesCapabilityActive = isTemplatesCapabilityActive;
        _isTemplatesLibraryActive = isTemplatesLibraryActive;
        _isTemplatesEditorActive = isTemplatesEditorActive;
    }

    public bool IsTemplatesCapabilityActive => _isTemplatesCapabilityActive();

    public bool IsTemplatesLibraryActive => _isTemplatesLibraryActive();

    public bool IsTemplatesEditorActive => _isTemplatesEditorActive();
}

internal sealed class TemplatesWorkspaceHost : ITemplatesWorkspaceHost
{
    private readonly Action _ensureTemplatesLibraryLoaded;

    public TemplatesWorkspaceHost(Action ensureTemplatesLibraryLoaded)
    {
        _ensureTemplatesLibraryLoaded = ensureTemplatesLibraryLoaded;
    }

    public void EnsureTemplatesLibraryLoaded() => _ensureTemplatesLibraryLoaded();
}

internal sealed class TemplatesWorkspaceComposition
{
    private readonly FrameworkElement _workspaceHost;
    private readonly TemplatesLibraryView _libraryView;
    private readonly TemplatesEditorView _editorView;
    private readonly ITemplatesWorkspaceHost _host;
    private readonly ITemplatesWorkspaceShellBridge _shellBridge;

    public TemplatesWorkspaceComposition(
        FrameworkElement workspaceHost,
        TemplatesLibraryView libraryView,
        TemplatesEditorView editorView,
        ITemplatesWorkspaceHost host,
        ITemplatesWorkspaceShellBridge shellBridge)
    {
        _workspaceHost = workspaceHost;
        _libraryView = libraryView;
        _editorView = editorView;
        _host = host;
        _shellBridge = shellBridge;
    }

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _libraryView.Visibility = _shellBridge.IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;
        _editorView.Visibility = _shellBridge.IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;

        if (_shellBridge.IsTemplatesLibraryActive)
        {
            _host.EnsureTemplatesLibraryLoaded();
        }
    }
}

using LabAssistant.Business.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Single shell-level implementation of <see cref="IShellHost"/>. Delegates navigation to the
/// <see cref="ShellNavigationCoordinator"/> and exposes the shell <see cref="XamlRoot"/>. This
/// replaces the scattered per-capability navigation delegate bags with one shell-owned seam.
/// </summary>
internal sealed class ShellHost : IShellHost
{
    private readonly ShellNavigationCoordinator _coordinator;
    private readonly Func<XamlRoot?> _getXamlRoot;
    private readonly Func<TemplateEditorDocument, string, System.Threading.Tasks.Task> _showTemplateInEditorAsync;

    public ShellHost(
        ShellNavigationCoordinator coordinator,
        Func<XamlRoot?> getXamlRoot,
        ShellDialogService dialogs,
        IShellRightPanel rightPanel,
        Func<TemplateEditorDocument, string, System.Threading.Tasks.Task> showTemplateInEditorAsync)
    {
        _coordinator = coordinator;
        _getXamlRoot = getXamlRoot;
        Dialogs = dialogs;
        RightPanel = rightPanel;
        _showTemplateInEditorAsync = showTemplateInEditorAsync;
    }

    public void NavigateToRoute(string routeKey) => _coordinator.NavigateToRoute(routeKey);

    public void ReportActiveSubview(string routeKey) => _coordinator.ReportActiveSubview(routeKey);

    public XamlRoot? XamlRoot => _getXamlRoot();

    public IShellRightPanel RightPanel { get; }

    public ShellDialogService Dialogs { get; }

    public System.Threading.Tasks.Task ShowTemplateInEditorAsync(TemplateEditorDocument document, string statusText) =>
        _showTemplateInEditorAsync(document, statusText);
}

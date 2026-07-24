using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace LabAssistant.WinUI.Views.Diagnostics;

/// <summary>
/// Diagnostics Logs subview. Binds directly to <see cref="DiagnosticsLogsViewModel"/> via
/// <c>x:Bind</c>. The code-behind hosts only genuinely view-level concerns: the draggable splitter
/// that resizes the Selected Context panel, and the clipboard write for the copyable code chip.
/// </summary>
public sealed partial class DiagnosticsLogsView : UserControl
{
    private const double MinPanelWidth = 260d;
    private const double MaxPanelWidth = 720d;

    public DiagnosticsLogsViewModel ViewModel { get; }

    public DiagnosticsLogsView()
    {
        ViewModel = App.Services.GetRequiredService<DiagnosticsLogsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewLifecycle.Run(() => ViewModel.InitializeAsync(), "DiagnosticsLogsView.Initialize");
        Unloaded += (_, _) => ViewLifecycle.Run(() => ViewModel.CleanupAsync(), "DiagnosticsLogsView.Cleanup");
    }

    // Dragging the splitter left grows the panel, right shrinks it (the panel is the right-hand column).
    private void OnSplitterManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        var next = PanelColumn.Width.Value - e.Delta.Translation.X;
        next = Math.Clamp(next, MinPanelWidth, MaxPanelWidth);
        PanelColumn.Width = new Microsoft.UI.Xaml.GridLength(next);
    }

    private void OnCopyCodeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var code = ViewModel.SelectedCode;
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }
}

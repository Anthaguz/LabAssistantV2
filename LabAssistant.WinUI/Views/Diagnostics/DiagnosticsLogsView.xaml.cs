using LabAssistant.WinUI.ViewModels.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Diagnostics;

/// <summary>
/// Diagnostics Logs subview. Binds directly to <see cref="DiagnosticsLogsViewModel"/> via
/// <c>x:Bind</c>; filter inputs, the entry list, selection detail, and commands are all bound with
/// no imperative view-state marshalling. Replaces the former event-forwarding code-behind and its
/// separate composition/controller layers.
/// </summary>
public sealed partial class DiagnosticsLogsView : UserControl
{
    public DiagnosticsLogsViewModel ViewModel { get; }

    public DiagnosticsLogsView()
    {
        ViewModel = App.Services.GetRequiredService<DiagnosticsLogsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        Unloaded += async (_, _) => await ViewModel.CleanupAsync();
    }
}

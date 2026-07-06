using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Templates Editor subview. Binds directly to <see cref="TemplatesEditorViewModel"/> via
/// <c>x:Bind</c>; the header fields, VM slot list, selected-slot draft, and commands are all bound
/// with no imperative view-state marshalling. Resolves its own transient view model from DI and drives
/// its lifecycle from <c>Loaded</c>/<c>Unloaded</c>. Reference data, library reload, and confirmation
/// dialogs are provided by the hosting page via <see cref="ITemplatesEditorHost"/>.
/// </summary>
public sealed partial class TemplatesEditorView : UserControl
{
    public TemplatesEditorViewModel ViewModel { get; }

    public TemplatesEditorView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesEditorViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        Unloaded += async (_, _) => await ViewModel.CleanupAsync();
    }
}

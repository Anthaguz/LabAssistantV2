using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Templates Library subview. Binds directly to <see cref="TemplatesLibraryViewModel"/> via
/// <c>x:Bind</c>; the inventory list, search inputs, selection detail, and commands are all bound with
/// no imperative view-state marshalling. Resolves its own transient view model from DI and drives its
/// lifecycle from <c>Loaded</c>/<c>Unloaded</c>. Cross-subview navigation and dialogs are provided by
/// the hosting page via <see cref="ITemplatesLibraryHost"/>.
/// </summary>
public sealed partial class TemplatesLibraryView : UserControl
{
    public TemplatesLibraryViewModel ViewModel { get; }

    public TemplatesLibraryView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesLibraryViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        Unloaded += async (_, _) => await ViewModel.CleanupAsync();
    }
}

using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Templates Editor subview. Binds directly to <see cref="TemplatesEditorViewModel"/> via
/// <c>x:Bind</c>; the header fields, VM slot list, selected-slot draft, and commands are all bound
/// with no imperative view-state marshalling. Resolves its own transient view model from DI; the
/// hosting <c>TemplatesPage</c> owns the view-model lifecycle (initialize/cleanup) so switching between
/// the Templates tabs does not tear the view model down. Reference data, library reload, and
/// confirmation dialogs are provided by the hosting page via <see cref="ITemplatesEditorHost"/>.
/// </summary>
public sealed partial class TemplatesEditorView : UserControl
{
    public TemplatesEditorViewModel ViewModel { get; }

    public TemplatesEditorView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesEditorViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}

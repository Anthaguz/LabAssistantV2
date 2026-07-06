using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Templates Builder subview: the V2 template authoring wizard. Binds directly to
/// <see cref="TemplatesBuilderViewModel"/> via <c>x:Bind</c>; every step panel, navigator row, detail
/// field, and command is bound declaratively with no imperative view-state marshalling or visual-tree
/// scanning. Resolves its own transient view model from DI; the hosting <c>TemplatesPage</c> owns the
/// view-model lifecycle (initialize/cleanup) so switching between the Templates tabs does not tear the
/// view model down. Reference data, library reload, file dialogs, and cross-subview navigation are
/// provided by the hosting page via <see cref="ViewModels.Templates.Builder.ITemplatesBuilderHost"/>.
/// </summary>
public sealed partial class TemplatesBuilderView : UserControl
{
    public TemplatesBuilderViewModel ViewModel { get; }

    public TemplatesBuilderView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesBuilderViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }
}

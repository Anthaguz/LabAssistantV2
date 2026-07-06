using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Selects between a selectable chrome button and an inline placeholder text for a Builder navigator,
/// resource list, or forest-header row, reproducing the imperative engine's distinction between
/// <c>CreateResourceButton</c> rows and <c>CreateEmptyDetailText</c> placeholder markers.
/// </summary>
public sealed partial class BuilderNavRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ButtonTemplate { get; set; }

    public DataTemplate? PlaceholderTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is BuilderNavRowViewModel { IsPlaceholder: true } ? PlaceholderTemplate : ButtonTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}

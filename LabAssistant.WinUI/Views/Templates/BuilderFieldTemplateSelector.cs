using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Selects the editor template for a Builder detail field by its <see cref="BuilderFieldKind"/>,
/// replacing the imperative form engine's per-field control instantiation with declarative templates
/// bound two-way to the field view model.
/// </summary>
public sealed partial class BuilderFieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }

    public DataTemplate? ReadOnlyTextTemplate { get; set; }

    public DataTemplate? ComboTemplate { get; set; }

    public DataTemplate? SwitchPickerTemplate { get; set; }

    public DataTemplate? CheckBoxTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is BuilderFieldViewModel field
            ? field.Kind switch
            {
                BuilderFieldKind.Text => TextTemplate,
                BuilderFieldKind.ReadOnlyText => ReadOnlyTextTemplate,
                BuilderFieldKind.Combo => ComboTemplate,
                BuilderFieldKind.SwitchPicker => SwitchPickerTemplate,
                BuilderFieldKind.CheckBox => CheckBoxTemplate,
                _ => TextTemplate
            }
            : null;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}

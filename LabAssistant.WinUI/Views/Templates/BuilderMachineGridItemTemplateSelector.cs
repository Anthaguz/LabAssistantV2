using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

/// <summary>
/// Picks the tile template for the Level 2 machine grid: the machine-card template for a
/// <see cref="BuilderMachineCardViewModel"/>, or the dotted add-computer placeholder template for the
/// trailing <see cref="BuilderAddMachinePlaceholder"/> sentinel. Kept as a template selector (not a
/// value converter) so the single <c>MachineGridItems</c> source can carry both shapes in one wrap.
/// </summary>
public sealed class BuilderMachineGridItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MachineCardTemplate { get; set; }

    public DataTemplate? AddPlaceholderTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is BuilderAddMachinePlaceholder ? AddPlaceholderTemplate : MachineCardTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}

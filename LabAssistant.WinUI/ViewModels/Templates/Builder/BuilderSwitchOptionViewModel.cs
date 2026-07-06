namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// Bindable wrapper over a <see cref="TemplatesBuilderSwitchPickerOption"/> for the network switch
/// picker combo box. Exposes the display label while keeping the underlying option available to the
/// owning view model so it can apply the switch intent when the selection changes.
/// </summary>
public sealed class BuilderSwitchOptionViewModel
{
    internal BuilderSwitchOptionViewModel(TemplatesBuilderSwitchPickerOption option)
    {
        Option = option;
    }

    internal TemplatesBuilderSwitchPickerOption Option { get; }

    public string Label => Option.Label;

    public string OptionKey => Option.OptionKey;
}

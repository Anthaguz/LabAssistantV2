using System.Collections.ObjectModel;
using LabAssistant.WinUI.Theming;

namespace LabAssistant.WinUI.ViewModels;

public sealed class ShellViewModel
{
    public ShellViewModel()
    {
        Capabilities =
        [
            new ShellCapability(ShellIconToken.Machines, "Machines"),
            new ShellCapability(ShellIconToken.Deploy, "Deploy"),
            new ShellCapability(ShellIconToken.Templates, "Templates"),
            new ShellCapability(ShellIconToken.Assets, "Assets"),
            new ShellCapability(ShellIconToken.Diagnostics, "Diagnostics"),
            new ShellCapability(ShellIconToken.Settings, "Settings")
        ];
    }

    public ObservableCollection<ShellCapability> Capabilities { get; }
}

public sealed class ShellCapability
{
    public ShellCapability(string token, string displayName)
    {
        Token = token;
        DisplayName = displayName;
    }

    public string Token { get; }

    public string DisplayName { get; }

    public string Glyph => ShellIconCatalog.GetGlyph(Token);
}

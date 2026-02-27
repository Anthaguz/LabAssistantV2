using System.Collections.ObjectModel;
using LabAssistant.WinUI.Theming;

namespace LabAssistant.WinUI.ViewModels;

public sealed class ShellViewModel
{
    private readonly Dictionary<string, ShellCapability> _capabilityLookup;

    public ShellViewModel()
    {
        Capabilities =
        [
            new ShellCapability(
                ShellIconToken.Machines,
                "Machines",
                [new ShellSubview("overview", "Overview", ["Refresh", "Filter"])]),
            new ShellCapability(
                ShellIconToken.Deploy,
                "Deploy",
                [
                    new ShellSubview("on-the-fly", "On-the-fly", ["Start", "Validate"]),
                    new ShellSubview("from-template", "From Template", ["Select Template", "Preview"])
                ]),
            new ShellCapability(
                ShellIconToken.Templates,
                "Templates",
                [
                    new ShellSubview("library", "Library", ["Import", "Export"]),
                    new ShellSubview("editor", "Editor", ["Save Draft", "Validate"])
                ]),
            new ShellCapability(
                ShellIconToken.Assets,
                "Assets",
                [
                    new ShellSubview("disks", "Disks", ["Add Disk", "Validate"]),
                    new ShellSubview("virtual-switches", "Virtual Switches", ["Add Switch", "Refresh"])
                ]),
            new ShellCapability(
                ShellIconToken.Diagnostics,
                "Diagnostics",
                [new ShellSubview("overview", "Overview", ["Export Bundle", "Open Logs"])]),
            new ShellCapability(
                ShellIconToken.Settings,
                "Settings",
                [new ShellSubview("general", "General", ["Apply", "Reset"])])
        ];

        _capabilityLookup = Capabilities.ToDictionary(
            capability => capability.DisplayName,
            capability => capability,
            StringComparer.Ordinal);
    }

    public ObservableCollection<ShellCapability> Capabilities { get; }

    public ShellCapability GetCapability(string capabilityDisplayName)
    {
        if (_capabilityLookup.TryGetValue(capabilityDisplayName, out var capability))
        {
            return capability;
        }

        return Capabilities[0];
    }
}

public sealed class ShellCapability
{
    public ShellCapability(string token, string displayName, IReadOnlyList<ShellSubview> subviews)
    {
        Token = token;
        DisplayName = displayName;
        Subviews = subviews;
        DefaultSubview = subviews[0];
    }

    public string Token { get; }

    public string DisplayName { get; }

    public string Glyph => ShellIconCatalog.GetGlyph(Token);

    public IReadOnlyList<ShellSubview> Subviews { get; }

    public ShellSubview DefaultSubview { get; }
}

public sealed class ShellSubview
{
    public ShellSubview(string key, string displayName, IReadOnlyList<string> toolbarActions)
    {
        Key = key;
        DisplayName = displayName;
        ToolbarActions = toolbarActions;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public IReadOnlyList<string> ToolbarActions { get; }
}

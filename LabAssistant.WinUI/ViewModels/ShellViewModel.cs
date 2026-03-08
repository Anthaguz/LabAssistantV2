using System.Collections.ObjectModel;
using LabAssistant.WinUI.Theming;

namespace LabAssistant.WinUI.ViewModels;

public static class ShellRouteKeys
{
    public const string MachinesOverview = "machines.overview";
    public const string DeployOverview = "deploy.overview";
    public const string DeployOnTheFly = "deploy.on_the_fly";
    public const string DeployFromTemplate = "deploy.from_template";
    public const string TemplatesLibrary = "templates.library";
    public const string TemplatesEditor = "templates.editor";
    public const string AssetsOverview = "assets.overview";
    public const string AssetsBaseDisks = "assets.base_disks";
    public const string AssetsSwitches = "assets.switches";
    public const string DiagnosticsOverview = "diagnostics.overview";
    public const string DiagnosticsLogs = "diagnostics.logs";
    public const string SettingsGeneral = "settings.general";
    public const string SettingsMachines = "settings.machines";
}

public sealed class ShellViewModel
{
    private readonly Dictionary<string, ShellCapability> _capabilityLookup;
    private readonly Dictionary<string, (ShellCapability Capability, ShellSubview Subview)> _routeLookup;

    public ShellViewModel()
    {
        Capabilities =
        [
            new ShellCapability(
                key: "machines",
                token: ShellIconToken.Machines,
                displayName: "Machines",
                isFooter: false,
                [
                    new ShellSubview(ShellRouteKeys.MachinesOverview, "Overview", ["Refresh", "Filter"])
                ]),
            new ShellCapability(
                key: "deploy",
                token: ShellIconToken.Deploy,
                displayName: "Deploy",
                isFooter: false,
                [
                    new ShellSubview(ShellRouteKeys.DeployOverview, "Overview", ["Open Quick Deploy", "Open From Template"]),
                    new ShellSubview(ShellRouteKeys.DeployOnTheFly, "Quick Deploy", ["Start", "Validate"]),
                    new ShellSubview(ShellRouteKeys.DeployFromTemplate, "From Template", ["Select Template", "Preview"]),
                ]),
            new ShellCapability(
                key: "templates",
                token: ShellIconToken.Templates,
                displayName: "Templates",
                isFooter: false,
                [
                    new ShellSubview(ShellRouteKeys.TemplatesLibrary, "Library", ["Import", "Export"]),
                    new ShellSubview(ShellRouteKeys.TemplatesEditor, "Editor", ["Save Draft", "Validate"])
                ]),
            new ShellCapability(
                key: "assets",
                token: ShellIconToken.Assets,
                displayName: "Assets",
                isFooter: false,
                [
                    new ShellSubview(ShellRouteKeys.AssetsOverview, "Overview", ["Open Base Disks", "Open Switches"]),
                    new ShellSubview(ShellRouteKeys.AssetsBaseDisks, "Base Disks", ["Refresh", "Import", "Remove"]),
                    new ShellSubview(ShellRouteKeys.AssetsSwitches, "Virtual Switches", ["Add Switch", "Refresh"])
                ]),
            new ShellCapability(
                key: "diagnostics",
                token: ShellIconToken.Diagnostics,
                displayName: "Diagnostics",
                isFooter: false,
                [
                    new ShellSubview(ShellRouteKeys.DiagnosticsOverview, "Overview", ["Export Bundle"]),
                    new ShellSubview(ShellRouteKeys.DiagnosticsLogs, "Logs", ["Reload", "Open Raw JSONL"])
                ]),
            new ShellCapability(
                key: "settings",
                token: ShellIconToken.Settings,
                displayName: "Settings",
                isFooter: true,
                [
                    new ShellSubview(ShellRouteKeys.SettingsMachines, "Machines", ["Save Policy"]),
                    new ShellSubview(ShellRouteKeys.SettingsGeneral, "General", ["Apply"])
                ])
        ];

        _capabilityLookup = Capabilities.ToDictionary(capability => capability.Key, StringComparer.Ordinal);
        _routeLookup = Capabilities
            .SelectMany(capability => capability.Subviews.Select(subview => (capability, subview)))
            .ToDictionary(entry => entry.subview.RouteKey, entry => (entry.capability, entry.subview), StringComparer.Ordinal);
    }

    public ObservableCollection<ShellCapability> Capabilities { get; }

    public string StartupRoute => ShellRouteKeys.MachinesOverview;

    public bool TryResolveRoute(string routeKey, out ShellCapability capability, out ShellSubview subview)
    {
        if (_routeLookup.TryGetValue(routeKey, out var resolved))
        {
            capability = resolved.Capability;
            subview = resolved.Subview;
            return true;
        }

        capability = Capabilities[0];
        subview = capability.DefaultSubview;
        return false;
    }

    public bool TryResolveCapability(string capabilityKey, out ShellCapability capability)
    {
        return _capabilityLookup.TryGetValue(capabilityKey, out capability!);
    }
}

public sealed class ShellCapability
{
    public ShellCapability(string key, string token, string displayName, bool isFooter, IReadOnlyList<ShellSubview> subviews)
    {
        Key = key;
        Token = token;
        DisplayName = displayName;
        IsFooter = isFooter;
        Subviews = subviews;
        DefaultSubview = subviews[0];
    }

    public string Key { get; }

    public string Token { get; }

    public string DisplayName { get; }

    public bool IsFooter { get; }

    public string Glyph => ShellIconCatalog.GetGlyph(Token);

    public IReadOnlyList<ShellSubview> Subviews { get; }

    public ShellSubview DefaultSubview { get; }

    public bool HasOverview => string.Equals(DefaultSubview.DisplayName, "Overview", StringComparison.Ordinal);
}

public sealed class ShellSubview
{
    public ShellSubview(string routeKey, string displayName, IReadOnlyList<string> toolbarActions)
    {
        RouteKey = routeKey;
        DisplayName = displayName;
        ToolbarActions = toolbarActions;
    }

    public string RouteKey { get; }

    public string DisplayName { get; }

    public IReadOnlyList<string> ToolbarActions { get; }
}

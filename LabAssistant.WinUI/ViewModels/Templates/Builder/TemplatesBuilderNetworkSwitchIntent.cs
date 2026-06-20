using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal enum TemplatesBuilderSwitchIntentKind
{
    Existing,
    CreateNew
}

internal readonly record struct TemplatesBuilderSwitchPickerOption(
    string OptionKey,
    TemplatesBuilderSwitchIntentKind Kind,
    string Label,
    string SwitchName,
    string SwitchType)
{
    public bool IsExisting => Kind == TemplatesBuilderSwitchIntentKind.Existing;

    public bool IsCreateNew => Kind == TemplatesBuilderSwitchIntentKind.CreateNew;
}

internal readonly record struct TemplatesBuilderNetworkSwitchIntentProjection(
    IReadOnlyList<TemplatesBuilderSwitchPickerOption> Options,
    TemplatesBuilderSwitchPickerOption SelectedOption,
    bool IsExistingSwitchSelected,
    bool IsCreateNewSelected,
    bool IsSwitchNameEditable,
    bool IsSwitchTypeEditable,
    string SwitchName,
    string SwitchType);

internal static class TemplatesBuilderNetworkSwitchIntent
{
    public const string CreateNewOptionKey = "__create_new_switch__";

    public static TemplatesBuilderNetworkSwitchIntentProjection Project(
        TemplatesBuilderLabNetworkDraft network,
        IReadOnlyList<V2AvailableSwitchInfo>? switchInventory)
    {
        var options = BuildOptions(switchInventory);
        var selected = options.FirstOrDefault(option =>
            option.IsExisting &&
            !string.IsNullOrWhiteSpace(network.SwitchName) &&
            string.Equals(option.SwitchName, network.SwitchName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(selected.OptionKey))
        {
            selected = options.First(option => option.IsCreateNew);
        }

        var isExisting = selected.IsExisting;
        return new TemplatesBuilderNetworkSwitchIntentProjection(
            options,
            selected,
            isExisting,
            selected.IsCreateNew,
            !isExisting,
            !isExisting,
            isExisting ? selected.SwitchName : network.SwitchName,
            isExisting ? DisplaySwitchType(selected.SwitchType) : network.SwitchType);
    }

    public static TemplatesBuilderLabNetworkDraft ApplySelectedOption(
        TemplatesBuilderLabNetworkDraft network,
        TemplatesBuilderSwitchPickerOption selectedOption)
    {
        if (selectedOption.IsCreateNew)
        {
            return network with
            {
                SwitchName = string.Empty,
                SwitchType = string.Empty
            };
        }

        return network with
        {
            SwitchName = selectedOption.SwitchName,
            SwitchType = NormalizeSupportedSwitchType(selectedOption.SwitchType) ?? network.SwitchType
        };
    }

    public static IReadOnlyList<TemplatesBuilderSwitchPickerOption> BuildOptions(
        IReadOnlyList<V2AvailableSwitchInfo>? switchInventory)
    {
        var options = NormalizeInventory(switchInventory)
            .Select(item => new TemplatesBuilderSwitchPickerOption(
                OptionKey: $"existing:{item.Name}",
                Kind: TemplatesBuilderSwitchIntentKind.Existing,
                Label: FormatExistingSwitchLabel(item),
                SwitchName: item.Name,
                SwitchType: item.SwitchType))
            .ToList();

        options.Add(new TemplatesBuilderSwitchPickerOption(
            CreateNewOptionKey,
            TemplatesBuilderSwitchIntentKind.CreateNew,
            "Create new switch",
            string.Empty,
            string.Empty));

        return options;
    }

    private static IReadOnlyList<V2AvailableSwitchInfo> NormalizeInventory(IReadOnlyList<V2AvailableSwitchInfo>? switchInventory)
        => switchInventory?
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var item = group.First();
                return new V2AvailableSwitchInfo
                {
                    Name = item.Name.Trim(),
                    SwitchType = string.IsNullOrWhiteSpace(item.SwitchType) ? "Unknown" : item.SwitchType.Trim()
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private static string FormatExistingSwitchLabel(V2AvailableSwitchInfo switchInfo)
    {
        var type = DisplaySwitchType(switchInfo.SwitchType);
        return string.IsNullOrWhiteSpace(type)
            ? switchInfo.Name
            : $"{switchInfo.Name} ({type})";
    }

    private static string DisplaySwitchType(string switchType)
        => string.IsNullOrWhiteSpace(switchType) ? "Unknown" : switchType.Trim();

    private static string? NormalizeSupportedSwitchType(string switchType)
    {
        if (string.Equals(switchType, V2SwitchTypeCatalog.External, StringComparison.OrdinalIgnoreCase))
        {
            return V2SwitchTypeCatalog.External;
        }

        if (string.Equals(switchType, V2SwitchTypeCatalog.Internal, StringComparison.OrdinalIgnoreCase))
        {
            return V2SwitchTypeCatalog.Internal;
        }

        if (string.Equals(switchType, V2SwitchTypeCatalog.Private, StringComparison.OrdinalIgnoreCase))
        {
            return V2SwitchTypeCatalog.Private;
        }

        return null;
    }
}

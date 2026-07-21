namespace LabAssistant.WinUI.Theming;

public static class ShellIconToken
{
    public const string Machines = "machines";
    public const string Deploy = "deploy";
    public const string Templates = "templates";
    public const string Assets = "assets";
    public const string Diagnostics = "diagnostics";
    public const string Settings = "settings";
    public const string Menu = "menu";
    public const string Insights = "insights";
}

public static class ShellIconCatalog
{
    private static readonly IReadOnlyDictionary<string, string> TokenToGlyph = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ShellIconToken.Machines] = "\uE80F",
        [ShellIconToken.Deploy] = "\uE8A7",
        [ShellIconToken.Templates] = "\uE8A5",
        [ShellIconToken.Assets] = "\uED43",
        [ShellIconToken.Diagnostics] = "\uE9D9",
        [ShellIconToken.Settings] = "\uE713",
        [ShellIconToken.Menu] = "\uE700",
        [ShellIconToken.Insights] = "\uE7BA"
    };

    public static string GetGlyph(string token)
    {
        if (TokenToGlyph.TryGetValue(token, out var glyph))
        {
            return glyph;
        }

        return "?";
    }
}

using System.Text.Json;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Machine-local harness configuration loaded from Fixtures/testenv.json.
/// Kept intentionally small; scenario-specific inputs live in their own fixtures.
/// </summary>
public sealed class HarnessConfig
{
    public string ResourceProviderMode { get; init; } = "discover-existing";
    public string RunTagPrefix { get; init; } = "LAT";
    public AppConfig App { get; init; } = new();
    public DiscoverExistingConfig DiscoverExisting { get; init; } = new();
    public DedicatedConfig Dedicated { get; init; } = new();

    public sealed class AppConfig
    {
        public string ExePath { get; init; } = string.Empty;
    }

    public sealed class DiscoverExistingConfig
    {
        public string PreferredBaseDiskName { get; init; } = string.Empty;
        public string PreferredSwitchName { get; init; } = string.Empty;
    }

    public sealed class DedicatedConfig
    {
        public string BaseDiskPath { get; init; } = string.Empty;
        public string IsoPath { get; init; } = string.Empty;
        public string SwitchName { get; init; } = "LAT-switch";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads testenv.json from the given path, or returns defaults when absent.
    /// The "//" comment keys in the JSON are ignored by the deserializer.
    /// </summary>
    public static HarnessConfig Load(string testEnvPath)
    {
        if (!File.Exists(testEnvPath))
        {
            return new HarnessConfig();
        }

        var json = File.ReadAllText(testEnvPath);
        return JsonSerializer.Deserialize<HarnessConfig>(json, JsonOptions) ?? new HarnessConfig();
    }

    /// <summary>
    /// Resolves the app exe to drive: the configured path if set, otherwise the
    /// newest LabAssistant.exe found under the repo's Debug output.
    /// </summary>
    public string ResolveAppExePath(string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(App.ExePath) && File.Exists(App.ExePath))
        {
            return App.ExePath;
        }

        var searchRoot = Path.Combine(repoRoot, "LabAssistant.WinUI", "bin");
        if (!Directory.Exists(searchRoot))
        {
            throw new FileNotFoundException(
                $"Could not auto-locate LabAssistant.exe: '{searchRoot}' does not exist. " +
                "Build LabAssistant.WinUI first, or set app.exePath in testenv.json.");
        }

        var newest = Directory
            .EnumerateFiles(searchRoot, "LabAssistant.exe", SearchOption.AllDirectories)
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        if (newest is null)
        {
            throw new FileNotFoundException(
                $"No LabAssistant.exe found under '{searchRoot}'. Build LabAssistant.WinUI first.");
        }

        return newest.FullName;
    }
}

namespace LabAssistant.UITesting.Infrastructure.Cleanup;

/// <summary>
/// Owns the naming convention that makes every harness-created Hyper-V resource
/// unambiguously identifiable and therefore safe to delete. Nothing outside this
/// prefix is ever touched by the sweeper, which is the core guard against the
/// harness destroying a real VM the user cares about.
/// </summary>
public sealed class ResourceTagger
{
    public ResourceTagger(string globalPrefix, string runId)
    {
        GlobalPrefix = globalPrefix;
        RunId = runId;
        RunPrefix = $"{globalPrefix}-{runId}-";
    }

    /// <summary>The prefix shared by every run (e.g. "LAT"). Used by the cross-run leftover sweep.</summary>
    public string GlobalPrefix { get; }

    /// <summary>The unique id for this run.</summary>
    public string RunId { get; }

    /// <summary>The prefix applied to every resource this run creates (e.g. "LAT-20260725-...-").</summary>
    public string RunPrefix { get; }

    /// <summary>Builds a tagged name for a resource this run creates.</summary>
    public string Name(string suffix) => $"{RunPrefix}{suffix}";

    /// <summary>True when a resource name belongs to ANY harness run (safe to sweep).</summary>
    public bool IsHarnessOwned(string resourceName)
        => resourceName is not null && resourceName.StartsWith(GlobalPrefix + "-", StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a tagger with a fresh, sortable run id.</summary>
    public static ResourceTagger CreateNew(string globalPrefix)
        => new(globalPrefix, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
}

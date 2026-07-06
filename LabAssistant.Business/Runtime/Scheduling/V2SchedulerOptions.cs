using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Concurrency and gate-retry policy for a scheduler run.
/// The Conservative/Balanced/Aggressive presets differ only in these numbers, not in code paths.
/// </summary>
/// <param name="MaxConcurrencyByClass">
/// Maximum simultaneously in-flight nodes per <see cref="V2WorkloadClass"/>.
/// A class absent from the map is treated as unbounded (still bounded by <paramref name="GlobalMaxConcurrency"/>).
/// </param>
/// <param name="GlobalMaxConcurrency">Global cap on simultaneously in-flight nodes across all classes.</param>
/// <param name="GateRetryDelay">
/// Reserved for scheduler-driven gate probes. Today's re-hosted gate executors retry internally, so this is unused
/// until dedicated probe executors exist (an O4 concern).
/// </param>
/// <param name="GateMaxRetries">Reserved for scheduler-driven gate probes. See <paramref name="GateRetryDelay"/>.</param>
public sealed record V2SchedulerOptions(
    IReadOnlyDictionary<V2WorkloadClass, int> MaxConcurrencyByClass,
    int GlobalMaxConcurrency,
    TimeSpan GateRetryDelay,
    int GateMaxRetries);

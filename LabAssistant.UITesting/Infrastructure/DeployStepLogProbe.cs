using System.Text.Json;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>The terminal state a deploy step reached, as reported by the app's structured event log.</summary>
public enum DeployStepOutcome
{
    /// <summary>The step ran and completed (<c>deploy.step.run.end</c> with <c>result=success</c>).</summary>
    Success,

    /// <summary>The step was terminally skipped (<c>result=skipped</c>) - e.g. a router validation with no dependent guests.</summary>
    Skipped,

    /// <summary>The step failed (<c>result=failed</c>), which in a live deploy is followed by a rollback.</summary>
    Failed
}

/// <summary>
/// Host-side reader of the app's structured event log (<c>structured-events*.jsonl</c>) that proves a
/// specific deploy STEP reached a terminal state, scoped to one VM. Where <see cref="GuestNetworkProbe"/>
/// reads the guest to prove an outcome, this reads the app's own per-step account
/// (<c>deploy.step.run.end</c> carries <c>result</c> in {success, skipped, failed} plus
/// <c>context.stepKey</c> / <c>context.vmName</c>), so a scenario can assert the deploy actually drove a
/// step to completion instead of tearing down the moment an earlier signal (like a static IP) appears.
///
/// This exists for the router tail: <c>prepareRouterNetwork</c> sets the LAN IP early, but the
/// RRAS/NAT/routerReady steps that follow it only emit their completion events later. Reading them here
/// is the difference between "the guest has an IP" and "the app finished installing RRAS, enabling
/// routing, configuring NAT, and marking the router ready".
///
/// Every read is failure-tolerant: a locked file, a parse error, or a missing log degrades to "no
/// terminal seen yet" (keep polling / return null) rather than throwing into a scenario's assertion path.
/// </summary>
public sealed class DeployStepLogProbe
{
    // The app rotates the active file at ~5 MB and keeps history files, all named
    // structured-events*.jsonl. A single deploy is far smaller than one rotation, but scanning every
    // matching file keeps the read correct even if a rotation lands mid-deploy.
    private const string LogGlob = "structured-events*.jsonl";

    // The app's terminal per-step event. Mirrors the contract emitted by the runtime; the harness
    // references no app project (it drives the built exe), so the event/field names are pinned here.
    private const string StepEndEvent = "deploy.step.run.end";

    // The app's per-step START event (deploy.step.run phase=start). It carries the same
    // context.stepKey / context.vmName as the end event but no result field. Reading it lets a scenario
    // anchor an ORDERING assertion at the moment a step began (e.g. the first prepareForestTrustDns
    // start) rather than only at completion.
    private const string StepStartEvent = "deploy.step.run.start";

    private readonly string _logsFolder;

    public DeployStepLogProbe(AppDataLocations locations)
    {
        _logsFolder = locations.LogsFolder;
    }

    /// <summary>Marks "now" as the start of a deploy's log window. Open it BEFORE Start Deploy.</summary>
    public AppLogWindow OpenWindow() => new(DateTimeOffset.UtcNow);

    /// <summary>
    /// Polls the log until the given step reaches a terminal state on <paramref name="vmName"/> (at or
    /// after <paramref name="window"/>), then returns that outcome. Returns null if no terminal event
    /// appears within <paramref name="timeout"/>, or as soon as <paramref name="abortIf"/> reports true
    /// (the router scenario passes a "did the app roll the VM back" probe so a failed step - which never
    /// emits routerReady - fails fast instead of polling for the full timeout).
    /// </summary>
    public DeployStepOutcome? WaitForStepTerminal(
        string vmName,
        string stepKey,
        AppLogWindow window,
        TimeSpan timeout,
        Func<bool>? abortIf = null,
        TimeSpan? pollInterval = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        var interval = pollInterval ?? TimeSpan.FromSeconds(5);

        while (true)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine(
                    $"DeployStepLogProbe: aborting wait for step '{stepKey}' on '{vmName}' - the deploy target is gone (rolled back).");
                return null;
            }

            DeployStepOutcome? outcome = ReadStepTerminal(vmName, stepKey, window.StartUtc);
            if (outcome is not null)
            {
                return outcome;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Console.WriteLine(
                    $"DeployStepLogProbe: step '{stepKey}' on '{vmName}' never reached a terminal state within the timeout.");
                return null;
            }

            Thread.Sleep(interval);
        }
    }

    /// <summary>
    /// Reads the current terminal outcome of <paramref name="stepKey"/> for <paramref name="vmName"/>
    /// from the log without waiting. Returns null when no terminal event is present yet. Use this to
    /// read the earlier tail steps once a later terminal step (e.g. routerReady) has been observed - by
    /// then every prior step's <c>run.end</c> is already on disk.
    /// </summary>
    public DeployStepOutcome? ReadStepTerminal(string vmName, string stepKey, AppLogWindow window)
        => ReadStepTerminal(vmName, stepKey, window.StartUtc);

    private DeployStepOutcome? ReadStepTerminal(string vmName, string stepKey, DateTimeOffset windowStart)
    {
        if (!Directory.Exists(_logsFolder))
        {
            return null;
        }

        try
        {
            var lines = new List<string>();
            foreach (string file in Directory.EnumerateFiles(_logsFolder, LogGlob))
            {
                lines.AddRange(ReadLinesShared(file));
            }

            return FindStepTerminalOutcome(lines, vmName, stepKey, windowStart);
        }
        catch
        {
            // Reading evidence must never throw into a scenario's assertion path.
            return null;
        }
    }

    /// <summary>
    /// Pure parse over structured-event lines: returns the terminal outcome of the given step for the
    /// given VM at or after <paramref name="windowStart"/>, or null when no terminal event is present.
    /// When several terminal events match (a step re-run across the window), the latest by timestamp
    /// wins so the final outcome is authoritative. Malformed lines are skipped.
    /// </summary>
    internal static DeployStepOutcome? FindStepTerminalOutcome(
        IEnumerable<string> lines,
        string vmName,
        string stepKey,
        DateTimeOffset windowStart)
    {
        DateTimeOffset bestTs = default;
        DeployStepOutcome? best = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryReadStepEnd(line, out DateTimeOffset ts, out string? lineVm, out string? lineStep, out DeployStepOutcome? outcome))
            {
                continue;
            }

            if (ts < windowStart ||
                outcome is null ||
                !string.Equals(lineVm, vmName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(lineStep, stepKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (best is null || ts >= bestTs)
            {
                best = outcome;
                bestTs = ts;
            }
        }

        return best;
    }

    /// <summary>
    /// Parses a <c>deploy.step.run.end</c> line into its timestamp, VM name, step key, and mapped
    /// outcome. Returns false for any other event, a malformed line, or an unrecognized result value.
    /// </summary>
    private static bool TryReadStepEnd(
        string line,
        out DateTimeOffset ts,
        out string? vmName,
        out string? stepKey,
        out DeployStepOutcome? outcome)
    {
        ts = default;
        vmName = null;
        stepKey = null;
        outcome = null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("event", out JsonElement eventElement) ||
                eventElement.ValueKind != JsonValueKind.String ||
                !string.Equals(eventElement.GetString(), StepEndEvent, StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetProperty("ts", out JsonElement tsElement) ||
                tsElement.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(
                    tsElement.GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out ts))
            {
                return false;
            }

            if (!root.TryGetProperty("result", out JsonElement resultElement) ||
                resultElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            outcome = MapOutcome(resultElement.GetString());
            if (outcome is null)
            {
                return false;
            }

            if (root.TryGetProperty("context", out JsonElement context) &&
                context.ValueKind == JsonValueKind.Object)
            {
                if (context.TryGetProperty("vmName", out JsonElement vmElement) &&
                    vmElement.ValueKind == JsonValueKind.String)
                {
                    vmName = vmElement.GetString();
                }

                if (context.TryGetProperty("stepKey", out JsonElement stepElement) &&
                    stepElement.ValueKind == JsonValueKind.String)
                {
                    stepKey = stepElement.GetString();
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DeployStepOutcome? MapOutcome(string? result) => result switch
    {
        "success" => DeployStepOutcome.Success,
        "skipped" => DeployStepOutcome.Skipped,
        "failed" => DeployStepOutcome.Failed,
        _ => null
    };

    /// <summary>
    /// Returns the EARLIEST start timestamp of <paramref name="stepKey"/> on <paramref name="vmName"/>
    /// (the <c>deploy.step.run.start</c> event) at or after <paramref name="window"/>, or null when the
    /// step never started. Earliest wins so a retried step reports when it FIRST began - the correct
    /// lower bound for an ordering assertion ("router routing finished before trust-DNS prep started").
    /// </summary>
    public DateTimeOffset? ReadStepStartTimestamp(string vmName, string stepKey, AppLogWindow window)
        => ReadStepEventTimestamp(vmName, stepKey, window.StartUtc, StepStartEvent, expectedOutcome: null, earliest: true);

    /// <summary>
    /// Returns the timestamp of the terminal <c>deploy.step.run.end</c> event for <paramref name="stepKey"/>
    /// on <paramref name="vmName"/> whose result maps to <paramref name="expectedOutcome"/>, at or after
    /// <paramref name="window"/>, or null when no such terminal is present. The LATEST matching terminal
    /// wins so a re-run's final outcome is authoritative - the correct upper bound for the predecessor
    /// side of an ordering assertion (when the step actually COMPLETED with the expected result).
    /// </summary>
    public DateTimeOffset? ReadStepTerminalTimestamp(string vmName, string stepKey, AppLogWindow window, DeployStepOutcome expectedOutcome)
        => ReadStepEventTimestamp(vmName, stepKey, window.StartUtc, StepEndEvent, expectedOutcome, earliest: false);

    private DateTimeOffset? ReadStepEventTimestamp(
        string vmName,
        string stepKey,
        DateTimeOffset windowStart,
        string eventName,
        DeployStepOutcome? expectedOutcome,
        bool earliest)
    {
        if (!Directory.Exists(_logsFolder))
        {
            return null;
        }

        try
        {
            var lines = new List<string>();
            foreach (string file in Directory.EnumerateFiles(_logsFolder, LogGlob))
            {
                lines.AddRange(ReadLinesShared(file));
            }

            return FindStepEventTimestamp(lines, vmName, stepKey, windowStart, eventName, expectedOutcome, earliest);
        }
        catch
        {
            // Reading evidence must never throw into a scenario's assertion path.
            return null;
        }
    }

    /// <summary>
    /// Pure parse over structured-event lines: returns the timestamp of the <paramref name="eventName"/>
    /// event for the given VM + step at or after <paramref name="windowStart"/>. When
    /// <paramref name="expectedOutcome"/> is non-null, only end events whose result maps to it are
    /// considered (start events carry no result, so pass null for those). When several match,
    /// <paramref name="earliest"/> selects the first-by-timestamp (step-start lower bound) versus the
    /// last-by-timestamp (final terminal). Returns null when no matching event is present. Malformed
    /// lines are skipped.
    /// </summary>
    internal static DateTimeOffset? FindStepEventTimestamp(
        IEnumerable<string> lines,
        string vmName,
        string stepKey,
        DateTimeOffset windowStart,
        string eventName,
        DeployStepOutcome? expectedOutcome,
        bool earliest)
    {
        DateTimeOffset? bestTs = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryReadStepEvent(line, eventName, out DateTimeOffset ts, out string? lineVm, out string? lineStep, out DeployStepOutcome? outcome))
            {
                continue;
            }

            if (ts < windowStart ||
                !string.Equals(lineVm, vmName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(lineStep, stepKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (expectedOutcome is not null && outcome != expectedOutcome)
            {
                continue;
            }

            if (bestTs is null || (earliest ? ts < bestTs.Value : ts >= bestTs.Value))
            {
                bestTs = ts;
            }
        }

        return bestTs;
    }

    /// <summary>
    /// Parses a deploy.step event of the given <paramref name="eventName"/> into its timestamp, VM name,
    /// step key, and (when present) mapped result. Returns false for any other event or a malformed line.
    /// The result field is optional: start events omit it, so callers that read starts pass
    /// <c>expectedOutcome: null</c> and ignore the out outcome.
    /// </summary>
    private static bool TryReadStepEvent(
        string line,
        string eventName,
        out DateTimeOffset ts,
        out string? vmName,
        out string? stepKey,
        out DeployStepOutcome? outcome)
    {
        ts = default;
        vmName = null;
        stepKey = null;
        outcome = null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("event", out JsonElement eventElement) ||
                eventElement.ValueKind != JsonValueKind.String ||
                !string.Equals(eventElement.GetString(), eventName, StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetProperty("ts", out JsonElement tsElement) ||
                tsElement.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(
                    tsElement.GetString(),
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out ts))
            {
                return false;
            }

            if (root.TryGetProperty("result", out JsonElement resultElement) &&
                resultElement.ValueKind == JsonValueKind.String)
            {
                outcome = MapOutcome(resultElement.GetString());
            }

            if (root.TryGetProperty("context", out JsonElement context) &&
                context.ValueKind == JsonValueKind.Object)
            {
                if (context.TryGetProperty("vmName", out JsonElement vmElement) &&
                    vmElement.ValueKind == JsonValueKind.String)
                {
                    vmName = vmElement.GetString();
                }

                if (context.TryGetProperty("stepKey", out JsonElement stepElement) &&
                    stepElement.ValueKind == JsonValueKind.String)
                {
                    stepKey = stepElement.GetString();
                }
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a file the app may still hold open for writing. Opening with
    /// <see cref="FileShare.ReadWrite"/> avoids a sharing violation against the live logger.
    /// </summary>
    private static IEnumerable<string> ReadLinesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }
}

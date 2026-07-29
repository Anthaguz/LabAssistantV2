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
/// The per-anchor honest-telemetry carried in the context of a <c>deploy.forest-trust.cleanup.end</c> terminal
/// with <c>result=skipped</c> (the findings-86/87 moot-skip terminal). Each anchor outcome is one of
/// <c>skipped</c> / <c>success</c> / <c>failed</c>; <see cref="SkipReason"/> states why the in-guest delete was
/// skipped (the anchor's own VM is being torn down in the same cancel). For the #921 both-run-created case the
/// authoritative honest-skip proof is <see cref="SourceAnchorOutcome"/>=<c>skipped</c> AND
/// <see cref="TargetAnchorOutcome"/>=<c>skipped</c> AND a non-empty <see cref="SkipReason"/> - which
/// distinguishes "correctly skipped as moot" from "cleanup silently did nothing". Any field absent reads null.
/// </summary>
public sealed record TrustCleanupSkipDetail(
    string? SourceAnchorOutcome,
    string? TargetAnchorOutcome,
    string? SkipReason);

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

    // The forest-trust lifecycle markers (facility deploy.forest-trust) are NOT deploy.step.run.end
    // events: the runtime emits them via EmitTrustEvent as code-based events whose dotted name is
    // facility.operation.phase and whose context carries trustId / stepKey but NO vmName. The cleanup
    // wrap in particular (CleanupFailedOrCancelledAsync) emits ONLY these markers - it never routes
    // through the per-step deploy.step.run.end path - so proving "cleanupForestTrust actually ran" on
    // the rollback path can only be read here. The dotted names are pinned to match the app's status
    // registry (status-codes.yaml facility 0x46 = deploy.forest-trust, phase start/end).
    private const string TrustEventPrefix = "deploy.forest-trust.";

    /// <summary>The create-trust step started (<c>result=started</c>) - emitted just before the long
    /// guest CreateBidirectionalForestTrust call, after the trust objects are marked created. The
    /// rollback scenario keys its cancel off this so real trust artifacts are in place; the cancel is then
    /// observed at the following validate stage (the atomic create attempt completes first).</summary>
    public const string TrustCreateStartEvent = "deploy.forest-trust.create.start";

    /// <summary>The create-trust step reached a terminal end (<c>result=success</c> when the trust was
    /// fully created, <c>result=failed</c> when it threw/was interrupted).</summary>
    public const string TrustCreateEndEvent = "deploy.forest-trust.create.end";

    /// <summary>The trust-validate step reached a terminal end (<c>result=success</c> means the trust
    /// was validated / TrustReady). Its absence is what the rollback scenario asserts: a cancelled
    /// create must never reach a validated trust.</summary>
    public const string TrustValidateEndEvent = "deploy.forest-trust.validate.end";

    /// <summary>The cleanup wrap started (<c>result=started</c>) - the app began removing the local side
    /// of the trust on each anchor because the deploy was cancelled or failed.</summary>
    public const string TrustCleanupStartEvent = "deploy.forest-trust.cleanup.start";

    /// <summary>The cleanup wrap reached its single terminal end. <c>result=success</c> = a surviving anchor's
    /// local side was removed; <c>result=skipped</c> = the in-guest delete was correctly skipped as moot because
    /// the anchor's own VM is being torn down in the same cancel (findings 86/87 - the trust dies with the disk);
    /// <c>result=failed</c> = residual left behind (a real finding). Exactly one terminal is emitted per
    /// cleanup.start. This is the AUTHORITATIVE proof that cleanupForestTrust executed and reached an honest
    /// terminal rather than hanging or the trust merely dying with the torn-down VM.</summary>
    public const string TrustCleanupEndEvent = "deploy.forest-trust.cleanup.end";

    /// <summary>The single RUN-LEVEL orchestration terminal (facility deploy.orchestration, operation run,
    /// phase end - status-codes.yaml facility 0x40 / op 0x02). Emitted once per deploy from
    /// <c>EmitDeployTerminalEvent</c> off <c>multiContext.OperationState</c>, its <c>result</c> is the
    /// authoritative run outcome: <c>success</c> (deploy completed - cancel landed too late / nothing rolled
    /// back), <c>cancelled</c> (clean cancel, ZERO residuals - the headline no-orphans signal), or
    /// <c>cancelled_with_residuals</c> (cancel that left residuals - findings 86/87 not fully closed). The
    /// rollback verdict keys its headline pass/fail criterion directly off this rather than inferring the run
    /// outcome from navigated-away + VMs-gone.</summary>
    public const string RunTerminalEvent = "deploy.orchestration.run.end";

    // Context sub-fields the moot-skip cleanup terminal (cleanup.end result=skipped) carries, pinned to the
    // locked #924 event contract: each anchor's outcome plus the reason the in-guest delete was skipped. The
    // runtime adds these to the same nested context object EmitTrustEvent already writes (trustId/stepKey/...).
    private const string SourceAnchorOutcomeField = "sourceAnchorOutcome";
    private const string TargetAnchorOutcomeField = "targetAnchorOutcome";
    private const string SkipReasonField = "skipReason";

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
    /// Polls the log until a forest-trust lifecycle event named <paramref name="eventName"/> with
    /// <paramref name="result"/> appears at or after <paramref name="window"/>, then returns true. Returns
    /// false if none appears within <paramref name="timeout"/>, or as soon as <paramref name="abortIf"/>
    /// reports true. Used by the rollback scenario to wait for <see cref="TrustCreateStartEvent"/> before
    /// injecting the cancel that lands during the following validate stage.
    /// </summary>
    public bool WaitForTrustEvent(
        string eventName,
        string result,
        AppLogWindow window,
        TimeSpan timeout,
        Func<bool>? abortIf = null,
        TimeSpan? pollInterval = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        var interval = pollInterval ?? TimeSpan.FromSeconds(2);

        while (true)
        {
            if (abortIf is not null && abortIf())
            {
                Console.WriteLine(
                    $"DeployStepLogProbe: aborting wait for trust event '{eventName}' (result={result}) - abort signalled.");
                return false;
            }

            if (ReadTrustEvent(eventName, result, window))
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Console.WriteLine(
                    $"DeployStepLogProbe: trust event '{eventName}' (result={result}) never appeared within the timeout.");
                return false;
            }

            Thread.Sleep(interval);
        }
    }

    /// <summary>
    /// Returns true when a forest-trust lifecycle event named <paramref name="eventName"/> with
    /// <paramref name="result"/> is present at or after <paramref name="window"/>, without waiting. Reading
    /// evidence is failure-tolerant: a locked/missing log or a parse error degrades to "not seen".
    /// </summary>
    public bool ReadTrustEvent(string eventName, string result, AppLogWindow window)
        => ReadTrustEventTimestamp(eventName, result, window) is not null;

    /// <summary>
    /// Returns the latest timestamp of a forest-trust lifecycle event named <paramref name="eventName"/>
    /// with <paramref name="result"/> at or after <paramref name="window"/>, or null when none is present.
    /// The timestamp lets callers assert ORDERING between trust markers (e.g. cleanup started after create
    /// started); the rollback scenario only needs presence, but the timestamp seam is reused for ordering
    /// proofs. Never throws into a scenario's assertion path.
    /// </summary>
    public DateTimeOffset? ReadTrustEventTimestamp(string eventName, string result, AppLogWindow window)
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

            return FindTrustEventTimestamp(lines, eventName, result, window.StartUtc);
        }
        catch
        {
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
    /// Pure parse over structured-event lines: returns the latest timestamp of a forest-trust lifecycle
    /// event whose <c>event</c> equals <paramref name="eventName"/> and whose <c>result</c> equals
    /// <paramref name="result"/> at or after <paramref name="windowStart"/>, or null when none matches.
    /// Malformed lines and events outside the deploy.forest-trust facility are skipped.
    /// </summary>
    internal static DateTimeOffset? FindTrustEventTimestamp(
        IEnumerable<string> lines,
        string eventName,
        string result,
        DateTimeOffset windowStart)
    {
        DateTimeOffset? best = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryReadTrustEvent(line, out DateTimeOffset ts, out string? lineEvent, out string? lineResult))
            {
                continue;
            }

            if (ts < windowStart ||
                !string.Equals(lineEvent, eventName, StringComparison.Ordinal) ||
                !string.Equals(lineResult, result, StringComparison.Ordinal))
            {
                continue;
            }

            if (best is null || ts >= best.Value)
            {
                best = ts;
            }
        }

        return best;
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
    /// Returns the latest <c>result</c> of the run-level orchestration terminal
    /// (<see cref="RunTerminalEvent"/>) at or after <paramref name="window"/>, or null when none is present.
    /// The value is one of <c>success</c> / <c>cancelled</c> / <c>cancelled_with_residuals</c> (or the failure
    /// variants); the rollback verdict maps it to its headline run outcome. Never throws into an assertion path.
    /// </summary>
    public string? ReadRunTerminalResult(AppLogWindow window)
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

            return FindLatestEventResult(lines, RunTerminalEvent, window.StartUtc);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Pure parse over structured-event lines: returns the <c>result</c> of the latest event whose
    /// <c>event</c> equals <paramref name="eventName"/> at or after <paramref name="windowStart"/>, or null
    /// when none matches. Malformed lines are skipped. Used to read the single run-level orchestration terminal.
    /// </summary>
    internal static string? FindLatestEventResult(
        IEnumerable<string> lines,
        string eventName,
        DateTimeOffset windowStart)
    {
        DateTimeOffset bestTs = default;
        string? best = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryReadEventResult(line, out DateTimeOffset ts, out string? lineEvent, out string? lineResult))
            {
                continue;
            }

            if (ts < windowStart || !string.Equals(lineEvent, eventName, StringComparison.Ordinal))
            {
                continue;
            }

            if (best is null || ts >= bestTs)
            {
                best = lineResult;
                bestTs = ts;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns the per-anchor honest-telemetry from the latest <c>deploy.forest-trust.cleanup.end</c> terminal
    /// with <c>result=skipped</c> at or after <paramref name="window"/>, or null when no skipped terminal is
    /// present. Lets the rollback scenario assert the moot-skip terminal is honest (both anchor outcomes
    /// <c>skipped</c> + a stated reason) rather than a silent no-op. Never throws into an assertion path.
    /// </summary>
    public TrustCleanupSkipDetail? ReadTrustCleanupSkipDetail(AppLogWindow window)
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

            return FindTrustCleanupSkipDetail(lines, window.StartUtc);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Pure parse over structured-event lines: returns the per-anchor skip detail of the latest
    /// <c>deploy.forest-trust.cleanup.end</c> (result=skipped) at or after <paramref name="windowStart"/>, or
    /// null when none matches. Malformed lines are skipped; a matching terminal with no anchor context yields a
    /// detail whose fields are null (so the caller can tell "skipped terminal, but incomplete telemetry" apart
    /// from "no skipped terminal").
    /// </summary>
    internal static TrustCleanupSkipDetail? FindTrustCleanupSkipDetail(
        IEnumerable<string> lines,
        DateTimeOffset windowStart)
    {
        DateTimeOffset bestTs = default;
        TrustCleanupSkipDetail? best = null;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryReadTrustCleanupSkip(line, out DateTimeOffset ts, out TrustCleanupSkipDetail? detail))
            {
                continue;
            }

            if (ts < windowStart)
            {
                continue;
            }

            if (best is null || ts >= bestTs)
            {
                best = detail;
                bestTs = ts;
            }
        }

        return best;
    }

    /// <summary>
    /// Parses a <c>deploy.forest-trust.cleanup.end</c> line with <c>result=skipped</c> into its timestamp and
    /// per-anchor context detail. Returns false for any other event/result, a malformed line, or a line missing
    /// ts/result. A skipped terminal with no anchor context still parses (detail fields null) so the caller can
    /// distinguish an honest full-detail skip from an incomplete-telemetry one.
    /// </summary>
    private static bool TryReadTrustCleanupSkip(
        string line,
        out DateTimeOffset ts,
        out TrustCleanupSkipDetail? detail)
    {
        ts = default;
        detail = null;

        if (!TryReadEventResult(line, out ts, out string? eventName, out string? result))
        {
            return false;
        }

        if (!string.Equals(eventName, TrustCleanupEndEvent, StringComparison.Ordinal) ||
            !string.Equals(result, "skipped", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            string? source = null;
            string? target = null;
            string? reason = null;

            if (root.TryGetProperty("context", out JsonElement context) &&
                context.ValueKind == JsonValueKind.Object)
            {
                source = ReadStringField(context, SourceAnchorOutcomeField);
                target = ReadStringField(context, TargetAnchorOutcomeField);
                reason = ReadStringField(context, SkipReasonField);
            }

            detail = new TrustCleanupSkipDetail(source, target, reason);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadStringField(JsonElement context, string field)
        => context.TryGetProperty(field, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    /// <summary>
    /// Parses any structured-event line into its <c>event</c> name, <c>ts</c>, and <c>result</c>. Returns
    /// false for a malformed line or one missing event/ts/result. This is the general reader; the
    /// forest-trust reader (<see cref="TryReadTrustEvent"/>) layers the facility-prefix filter on top.
    /// </summary>
    private static bool TryReadEventResult(
        string line,
        out DateTimeOffset ts,
        out string? eventName,
        out string? result)
    {
        ts = default;
        eventName = null;
        result = null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("event", out JsonElement eventElement) ||
                eventElement.ValueKind != JsonValueKind.String)
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

            eventName = eventElement.GetString();
            result = resultElement.GetString();
            return eventName is not null && result is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a forest-trust lifecycle line (event name under <see cref="TrustEventPrefix"/>) into its
    /// timestamp, event name, and result. Delegates to <see cref="TryReadEventResult"/> and layers the
    /// facility-prefix filter on top, so any non-forest-trust event is rejected. Unlike
    /// <see cref="TryReadStepEnd"/> this requires NO vmName: trust events are keyed by trustId/stepKey.
    /// </summary>
    private static bool TryReadTrustEvent(
        string line,
        out DateTimeOffset ts,
        out string? eventName,
        out string? result)
    {
        if (!TryReadEventResult(line, out ts, out eventName, out result))
        {
            return false;
        }

        if (eventName is null || !eventName.StartsWith(TrustEventPrefix, StringComparison.Ordinal))
        {
            eventName = null;
            result = null;
            return false;
        }

        return true;
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

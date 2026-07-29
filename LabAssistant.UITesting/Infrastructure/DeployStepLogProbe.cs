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

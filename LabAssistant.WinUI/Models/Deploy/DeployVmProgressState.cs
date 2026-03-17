using LabAssistant.Models.Deployment;

namespace LabAssistant.WinUI.Models.Deploy;

internal sealed class DeployVmProgressState
{
    private readonly Dictionary<string, DeployTimelineStepState> _stepStatesByKey;
    private readonly Dictionary<string, string> _stepLabelsByKey;
    private readonly List<string> _stepOrder;

    public DeployVmProgressState(string vmName, IReadOnlyList<DeployTimelineStepDefinition> expectedSteps)
    {
        VmName = vmName;
        _stepOrder = expectedSteps.Select(step => step.StepKey).ToList();
        _stepLabelsByKey = expectedSteps.ToDictionary(
            step => step.StepKey,
            step => step.Label,
            StringComparer.OrdinalIgnoreCase);
        _stepStatesByKey = expectedSteps.ToDictionary(
            step => step.StepKey,
            _ => DeployTimelineStepState.Pending,
            StringComparer.OrdinalIgnoreCase);
        Status = "Queued";
        Summary = "Queued for deployment.";
        ProgressPercent = 0;
    }

    public string VmName { get; }

    public string Status { get; private set; }

    public string Summary { get; private set; }

    public int ProgressPercent { get; private set; }

    public void ApplyStepStateUpdate(DeployStepStateUpdate update)
    {
        if (string.IsNullOrWhiteSpace(update.StepKey))
        {
            return;
        }

        if (!_stepStatesByKey.ContainsKey(update.StepKey))
        {
            _stepStatesByKey[update.StepKey] = DeployTimelineStepState.Pending;
            _stepOrder.Add(update.StepKey);
        }

        if (!string.IsNullOrWhiteSpace(update.StepLabel))
        {
            _stepLabelsByKey[update.StepKey] = update.StepLabel;
        }

        var mappedState = MapState(update.State);
        var currentState = _stepStatesByKey[update.StepKey];
        if (IsTerminal(currentState) && !IsTerminal(mappedState))
        {
            return;
        }

        _stepStatesByKey[update.StepKey] = mappedState;
        Status = mappedState switch
        {
            DeployTimelineStepState.Pending => "Queued",
            DeployTimelineStepState.Running => "Running",
            DeployTimelineStepState.Succeeded => "Succeeded",
            DeployTimelineStepState.Failed => "Failed",
            DeployTimelineStepState.Skipped => "Skipped",
            _ => Status
        };

        if (!string.IsNullOrWhiteSpace(update.Message))
        {
            Summary = update.Message.Trim();
        }
        else if (string.IsNullOrWhiteSpace(Summary) || Status == "Queued")
        {
            Summary = $"{ResolveStepLabel(update.StepKey)}: {Status}";
        }

        RefreshProgress();
    }

    public void UpdateSummaryMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Summary = message.Trim();
    }

    public void MarkCompleted(string status, string summary)
    {
        var isFailed = status.Contains("failed", StringComparison.OrdinalIgnoreCase);
        var isCancelled = status.Contains("cancel", StringComparison.OrdinalIgnoreCase);
        var hasStepFailure = _stepStatesByKey.Values.Any(state => state == DeployTimelineStepState.Failed);
        var effectiveFailed = isFailed || hasStepFailure;

        foreach (var stepKey in _stepStatesByKey.Keys.ToList())
        {
            var state = _stepStatesByKey[stepKey];
            if (state == DeployTimelineStepState.Running)
            {
                _stepStatesByKey[stepKey] = effectiveFailed ? DeployTimelineStepState.Failed : isCancelled ? DeployTimelineStepState.Skipped : DeployTimelineStepState.Succeeded;
            }
            else if (state == DeployTimelineStepState.Pending)
            {
                _stepStatesByKey[stepKey] = isCancelled ? DeployTimelineStepState.Skipped : effectiveFailed ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded;
            }
        }

        Status = effectiveFailed && !status.Contains("failed", StringComparison.OrdinalIgnoreCase)
            ? "Failed"
            : status;
        Summary = summary;
        ProgressPercent = 100;
    }

    public DeployVmResultRow ToRow()
    {
        var timelineSteps = _stepOrder
            .Select(stepKey => new DeployTimelineStepRow(ResolveStepLabel(stepKey), _stepStatesByKey[stepKey]))
            .Where(step => step.State != DeployTimelineStepState.Skipped)
            .ToList();

        return new DeployVmResultRow(
            VmName: VmName,
            Status: Status,
            Summary: Summary,
            ProgressPercent: ProgressPercent,
            TimelineSteps: timelineSteps);
    }

    private void RefreshProgress()
    {
        if (Status == "Failed")
        {
            ProgressPercent = 100;
            return;
        }

        var visibleStates = _stepStatesByKey.Values.Where(state => state != DeployTimelineStepState.Skipped).ToList();
        if (visibleStates.Count == 0)
        {
            ProgressPercent = 50;
            return;
        }

        var terminalSteps = visibleStates.Count(state =>
            state is DeployTimelineStepState.Succeeded or DeployTimelineStepState.Failed);
        var rawProgress = (int)Math.Round((double)terminalSteps / visibleStates.Count * 100d, MidpointRounding.AwayFromZero);
        ProgressPercent = Math.Clamp(rawProgress, 5, 99);
    }

    private string ResolveStepLabel(string stepKey)
    {
        return _stepLabelsByKey.TryGetValue(stepKey, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : stepKey;
    }

    private static DeployTimelineStepState MapState(DeployStepState state)
    {
        return state switch
        {
            DeployStepState.Pending => DeployTimelineStepState.Pending,
            DeployStepState.Running => DeployTimelineStepState.Running,
            DeployStepState.Succeeded => DeployTimelineStepState.Succeeded,
            DeployStepState.Failed => DeployTimelineStepState.Failed,
            DeployStepState.Skipped => DeployTimelineStepState.Skipped,
            _ => DeployTimelineStepState.Pending
        };
    }

    private static bool IsTerminal(DeployTimelineStepState state)
    {
        return state is DeployTimelineStepState.Succeeded or DeployTimelineStepState.Failed or DeployTimelineStepState.Skipped;
    }
}

namespace LabAssistant.Models.Deployment;

public static class GuestStepOutcomeResults
{
    public const string Executed = "executed";
    public const string Skipped = "skipped";
}

public static class GuestStepSkipReasons
{
    public const string NotSelected = "not_selected";
    public const string NotImplemented = "not_implemented";
    public const string PrerequisiteUnavailable = "prerequisite_unavailable";
}

public sealed class GuestStepExecutionOutcome
{
    public string StepKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Result { get; init; } = GuestStepOutcomeResults.Skipped;
    public string? SkipReason { get; init; }
    public string? Message { get; init; }
}

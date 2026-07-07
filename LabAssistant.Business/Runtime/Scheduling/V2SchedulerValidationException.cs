namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Thrown by graph validation when a plan cannot be scheduled: an unregistered node kind, a dependency cycle, or a node
/// unreachable from any entry node. The runtime adapter converts this into a blocking result rather than crashing.
/// </summary>
public sealed class V2SchedulerValidationException : Exception
{
    /// <summary>Creates the exception with the ordered set of validation errors.</summary>
    public V2SchedulerValidationException(IReadOnlyList<string> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>The individual validation errors.</summary>
    public IReadOnlyList<string> Errors { get; }

    private static string BuildMessage(IReadOnlyList<string> errors)
    {
        if (errors is null || errors.Count == 0)
        {
            return "The V2 plan failed scheduler validation.";
        }

        return "The V2 plan failed scheduler validation: " + string.Join(" ", errors);
    }
}

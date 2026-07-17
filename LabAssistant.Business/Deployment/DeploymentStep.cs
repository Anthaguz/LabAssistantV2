// DeploymentStep.cs
namespace LabAssistant.Business.Deployment;

using LabAssistant.Models.Deployment;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using System.Threading.Tasks;

public abstract class DeploymentStep
{
    protected DeploymentStep? _next;
    protected virtual string StepKey => GetType().Name;
    protected virtual string StepLabel => StepKey;

    public DeploymentStep SetNext(DeploymentStep next)
    {
        _next = next;
        return next;
    }

    public async Task ExecuteAsync(VmDeploymentContext context)
    {
        var stepKey = StepKey;
        var stepLabel = StepLabel;
        if (context.ShouldAbort?.Invoke() == true)
        {
            context.MarkCancelled();
            return;
        }
        if (!context.IsSuccess && context.PerVmFailFast) return;
        context.EmitStepState(stepKey, stepLabel, DeployStepState.Pending);
        context.EmitStepState(stepKey, stepLabel, DeployStepState.Running);
        EmitStepEvent(context, LaStatus.DeployStep_StepStarted, null, stepKey);
        try
        {
            await HandleAsync(context);
        }
        catch (Exception ex)
        {
            EmitStepEvent(
                context,
                LaStatus.DeployStep_StepFailedException,
                "exception",
                stepKey,
                    RuntimeErrorMetadataNormalizer.Merge(
                    new Dictionary<string, object?> { ["errorMessage"] = ex.Message },
                    RuntimeErrorMetadataNormalizer.FromException(ex)));
            context.EmitStepState(stepKey, stepLabel, DeployStepState.Failed, ex.Message);
            throw;
        }
        var terminalState = context.TryConsumeStepTerminalOverride(stepKey, out var overrideState, out var overrideMessage)
            ? overrideState
            : context.IsSuccess
                ? DeployStepState.Succeeded
                : DeployStepState.Failed;

        // A Failed terminal state always follows a MarkFailure call (the only thing that clears IsSuccess), which
        // already emits the rich coded failure event via StepFailedCode; an exception path emits StepFailedException
        // and rethrows before reaching here. So Failed emits no terminal log event - this avoids a duplicate,
        // context-poor StepFailed and keeps the code's severity coherent with the outcome. Both Succeeded and
        // Skipped terminal states emit StepCompleted with the outcome carried in the result field; a step that is
        // explicitly skipped (for example a guest step) additionally emits its own rich StepSkipped event with the
        // skip reason, so StepSkipped stays a single, reason-bearing signal rather than being duplicated here.
        if (terminalState != DeployStepState.Failed)
        {
            var stepResult = terminalState == DeployStepState.Skipped ? "skipped" : "success";
            EmitStepEvent(context, LaStatus.DeployStep_StepCompleted, stepResult, stepKey);
        }

        context.EmitStepState(stepKey, stepLabel, terminalState, overrideMessage);
        if (context.ShouldAbort?.Invoke() == true)
        {
            context.MarkCancelled();
            return;
        }
        if (_next != null)
        {
            await _next.ExecuteAsync(context);
        }
    }

    protected abstract Task HandleAsync(VmDeploymentContext context);

    protected static void EmitStepEvent(
        VmDeploymentContext context,
        uint code,
        string? result,
        string stepKey,
        IReadOnlyDictionary<string, object?>? extraContext = null)
    {
        if (context.StructuredEventEmitter == null)
        {
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["stepKey"] = stepKey
        };

        if (extraContext != null)
        {
            foreach (var pair in extraContext)
            {
                payload[pair.Key] = pair.Value;
            }
        }

        context.StructuredEventEmitter.Invoke(code, result, payload);
    }
}

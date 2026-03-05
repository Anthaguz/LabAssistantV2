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
        EmitStepEvent(context, "StepStarted", "info", null, stepKey);
        try
        {
            await HandleAsync(context);
        }
        catch (Exception ex)
        {
            EmitStepEvent(
                context,
                "StepFailed",
                "error",
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
        var stepResult = terminalState switch
        {
            DeployStepState.Succeeded => "success",
            DeployStepState.Skipped => "skipped",
            _ => "failed"
        };
        EmitStepEvent(context, "StepCompleted", "info", stepResult, stepKey);
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
        string eventName,
        string level,
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

        context.StructuredEventEmitter.Invoke(eventName, level, result, payload);
    }
}

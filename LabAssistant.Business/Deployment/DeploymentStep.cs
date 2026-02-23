// DeploymentStep.cs
namespace LabAssistant.Business.Deployment;

using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;
using System.Threading.Tasks;

public abstract class DeploymentStep
{
    protected DeploymentStep? _next;

    public DeploymentStep SetNext(DeploymentStep next)
    {
        _next = next;
        return next;
    }

    public async Task ExecuteAsync(VmDeploymentContext context)
    {
        if (context.ShouldAbort?.Invoke() == true)
        {
            context.MarkCancelled();
            return;
        }
        if (!context.IsSuccess && context.PerVmFailFast) return;
        await HandleAsync(context);
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
}

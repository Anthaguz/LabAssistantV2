using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Threading;
using System.Threading.Tasks;

namespace LabAssistant.Business.Deployment;

public class MultiVmDeploymentCoordinator
{
    private readonly ISessionResolver _sessionResolver;
    private readonly DeploymentPipelineBuilder _pipelineBuilder;

    public MultiVmDeploymentCoordinator(
        ISessionResolver sessionResolver,
        DeploymentPipelineBuilder pipelineBuilder)
    {
        _sessionResolver = sessionResolver;
        _pipelineBuilder = pipelineBuilder;
    }

    public async Task DeployAllAsync(MultiVmDeploymentContext multiContext)
    {
        var cancelOnFailure = multiContext.StopAllOnAnyVmFailure;
        using var cancelSource = cancelOnFailure ? new CancellationTokenSource() : null;

        var tasks = multiContext.VmContexts.Select(context => Task.Run(async () =>
        {
            DebugLogger.Log($"Deploying VM: {context.VmName}");

            // Create handle and session
            var handle = new PowerShellHandle();
            var session = new PersistentPowerShellSession();

            DebugLogger.Log($"Created and assigned PowerShell handle for VM: {context.VmName}");

            _sessionResolver.RegisterSession(handle, session);
            context.PowerShellHandle = handle;
            if (cancelOnFailure && cancelSource != null)
            {
                context.OnBlockingFailure = () =>
                {
                    if (!cancelSource.IsCancellationRequested)
                    {
                        cancelSource.Cancel();
                    }
                };
                context.ShouldAbort = () => cancelSource.IsCancellationRequested;
            }

            var pipeline = _pipelineBuilder.Build(context);

            try
            {
                await pipeline.ExecuteAsync(context);
            }
            finally
            {
                session.Dispose(); // Ensure cleanup
                _sessionResolver.RemoveSession(handle);
                DebugLogger.Log($"Disposed session for VM: {context.VmName}");
            }
        }));

        await Task.WhenAll(tasks);
    }
}

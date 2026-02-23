using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Threading;

namespace LabAssistant.Business.Deployment;

public class MultiVmDeploymentCoordinator
{
    private readonly ISessionResolver _sessionResolver;
    private readonly DeploymentPipelineBuilder _pipelineBuilder;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    private readonly IVmCleanupOrchestrator _cleanupOrchestrator;

    public MultiVmDeploymentCoordinator(
        ISessionResolver sessionResolver,
        DeploymentPipelineBuilder pipelineBuilder,
        Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory,
        IVmCleanupOrchestrator cleanupOrchestrator)
    {
        _sessionResolver = sessionResolver;
        _pipelineBuilder = pipelineBuilder;
        _hyperVFactory = hyperVFactory;
        _cleanupOrchestrator = cleanupOrchestrator;
    }

    public async Task DeployAllAsync(MultiVmDeploymentContext multiContext)
    {
        var cancelOnFailure = multiContext.StopAllOnAnyVmFailure;
        using var cancelSource = cancelOnFailure ? new CancellationTokenSource() : null;
        var cleanupResultsLock = new object();

        var tasks = multiContext.VmContexts.Select(context => Task.Run(async () =>
        {
            DebugLogger.Log($"Deploying VM: {context.VmName}");

            var handle = new PowerShellHandle();
            var session = new PersistentPowerShellSession();
            var hyperV = _hyperVFactory(session);

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

                if (!context.IsSuccess)
                {
                    var cleanupResult = await _cleanupOrchestrator.CleanupAsync(context, hyperV);
                    context.CleanupResult = cleanupResult;

                    lock (cleanupResultsLock)
                    {
                        multiContext.CleanupResults.Add(cleanupResult);
                    }

                    context.LogCallback?.Invoke(cleanupResult.HasResiduals
                        ? $"❌ Cleanup completed with residuals for '{context.VmName}'."
                        : $"✅ Cleanup completed for '{context.VmName}'.");

                    foreach (var step in cleanupResult.StepResults)
                    {
                        context.LogCallback?.Invoke($"Cleanup {step.Step}: {step.Status} - {step.Message}");
                    }
                }
            }
            finally
            {
                session.Dispose();
                _sessionResolver.RemoveSession(handle);
                DebugLogger.Log($"Disposed session for VM: {context.VmName}");
            }
        }));

        await Task.WhenAll(tasks);
    }
}

using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class MultiVmDeploymentCoordinator
{
    private readonly ISessionResolver _sessionResolver;
    private readonly IDeploymentPipelineBuilder _pipelineBuilder;
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    private readonly IVmCleanupOrchestrator _cleanupOrchestrator;

    public MultiVmDeploymentCoordinator(
        ISessionResolver sessionResolver,
        IDeploymentPipelineBuilder pipelineBuilder,
        Func<IPersistentPowerShellSession> sessionFactory,
        Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory,
        IVmCleanupOrchestrator cleanupOrchestrator)
    {
        _sessionResolver = sessionResolver;
        _pipelineBuilder = pipelineBuilder;
        _sessionFactory = sessionFactory;
        _hyperVFactory = hyperVFactory;
        _cleanupOrchestrator = cleanupOrchestrator;
    }

    public async Task DeployAllAsync(MultiVmDeploymentContext multiContext)
    {
        multiContext.MarkRunning();
        var cleanupResultsLock = new object();

        var tasks = multiContext.VmContexts.Select(context => Task.Run(async () =>
        {
            DebugLogger.Log($"Deploying VM: {context.VmName}");

            var handle = new PowerShellHandle();
            var session = _sessionFactory();
            var hyperV = _hyperVFactory(session);

            _sessionResolver.RegisterSession(handle, session);
            context.PowerShellHandle = handle;

            context.OnBlockingFailure = () =>
            {
                if (multiContext.StopAllOnAnyVmFailure)
                {
                    multiContext.RequestCancellation();
                }
            };
            context.ShouldAbort = () => multiContext.IsCancellationRequested;

            var pipeline = _pipelineBuilder.Build(context);

            try
            {
                await pipeline.ExecuteAsync(context);

                var cancelled = multiContext.UserCancellationRequested && multiContext.IsCancellationRequested;
                var needsCleanup = !context.IsSuccess || (cancelled && HasTrackedResources(context));
                if (!needsCleanup)
                {
                    return;
                }

                multiContext.MarkCleanupInProgress();
                var cleanupResult = await _cleanupOrchestrator.CleanupAsync(context, hyperV);
                context.CleanupResult = cleanupResult;

                lock (cleanupResultsLock)
                {
                    multiContext.CleanupResults.Add(cleanupResult);
                }

                context.LogCallback?.Invoke(cleanupResult.HasResiduals
                    ? $"Cleanup completed with residuals for '{context.VmName}'."
                    : $"Cleanup completed for '{context.VmName}'.");
            }
            finally
            {
                session.Dispose();
                _sessionResolver.RemoveSession(handle);
                DebugLogger.Log($"Disposed session for VM: {context.VmName}");
            }
        }));

        await Task.WhenAll(tasks);

        var hasFailures = multiContext.VmContexts.Any(vm => !vm.IsSuccess);
        var hasCleanupResiduals = multiContext.CleanupResults.Any(r => r.HasResiduals);
        multiContext.CompleteTerminalState(hasFailures, hasCleanupResiduals);
    }

    private static bool HasTrackedResources(VmDeploymentContext context)
    {
        return context.VmFolderCreated || context.DifferencingDiskCreated || context.VmRegistered || context.VmStarted;
    }
}

using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class MultiVmDeploymentCoordinator : IDeploymentCoordinator
{
    private readonly ISessionResolver _sessionResolver;
    private readonly IDeploymentPipelineBuilder _pipelineBuilder;
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    private readonly IVmCleanupOrchestrator _cleanupOrchestrator;
    private readonly IStructuredLogger _structuredLogger;

    public MultiVmDeploymentCoordinator(
        ISessionResolver sessionResolver,
        IDeploymentPipelineBuilder pipelineBuilder,
        Func<IPersistentPowerShellSession> sessionFactory,
        Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory,
        IVmCleanupOrchestrator cleanupOrchestrator,
        IStructuredLogger? structuredLogger = null)
    {
        _sessionResolver = sessionResolver;
        _pipelineBuilder = pipelineBuilder;
        _sessionFactory = sessionFactory;
        _hyperVFactory = hyperVFactory;
        _cleanupOrchestrator = cleanupOrchestrator;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public async Task DeployAllAsync(MultiVmDeploymentContext multiContext)
    {
        EnsureOperationId(multiContext);
        multiContext.MarkRunning();
        EmitDeployEvent("DeployLabStarted", multiContext, "started");
        var cleanupResultsLock = new object();

        var tasks = multiContext.VmContexts.Select(context => Task.Run(async () =>
        {
            DebugLogger.Log($"Deploying VM: {context.VmName}");

            var handle = new PowerShellHandle();
            var session = _sessionFactory();
            var hyperV = _hyperVFactory(session);

            _sessionResolver.RegisterSession(handle, session);
            context.PowerShellHandle = handle;
            context.StructuredEventEmitter = (eventName, level, result, extraContext) =>
                EmitVmScopedEvent(eventName, level, multiContext, context, result, extraContext);
            EmitVmScopedEvent("VmDeployStarted", "info", multiContext, context, "started");

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
                EmitVmScopedEvent("CleanupStarted", "info", multiContext, context, "started");
                var cleanupResult = await _cleanupOrchestrator.CleanupAsync(context, hyperV);
                context.CleanupResult = cleanupResult;
                EmitCleanupStepEvents(multiContext, context, cleanupResult);

                lock (cleanupResultsLock)
                {
                    multiContext.CleanupResults.Add(cleanupResult);
                }

                EmitVmScopedEvent(
                    "CleanupCompleted",
                    cleanupResult.HasResiduals ? "warn" : "info",
                    multiContext,
                    context,
                    cleanupResult.HasResiduals ? "completed_with_residuals" : "completed",
                    new Dictionary<string, object?>
                    {
                        ["cleanupStepCount"] = cleanupResult.StepResults.Count,
                        ["residualCount"] = cleanupResult.Residuals.Count
                    });

                if (cleanupResult.HasResiduals)
                {
                    EmitVmScopedEvent(
                        "CleanupResidualsDetected",
                        "error",
                        multiContext,
                        context,
                        "residuals_detected",
                        new Dictionary<string, object?>
                        {
                            ["residualCount"] = cleanupResult.Residuals.Count
                        });
                }

                context.LogCallback?.Invoke(cleanupResult.HasResiduals
                    ? $"Cleanup completed with residuals for '{context.VmName}'."
                    : $"Cleanup completed for '{context.VmName}'.");
            }
            finally
            {
                EmitVmTerminalEvent(multiContext, context);
                context.StructuredEventEmitter = null;
                session.Dispose();
                _sessionResolver.RemoveSession(handle);
                DebugLogger.Log($"Disposed session for VM: {context.VmName}");
            }
        }));

        await Task.WhenAll(tasks);

        var hasFailures = multiContext.VmContexts.Any(vm => !vm.IsSuccess);
        var hasCleanupResiduals = multiContext.CleanupResults.Any(r => r.HasResiduals);
        multiContext.CompleteTerminalState(hasFailures, hasCleanupResiduals);
        EmitDeployTerminalEvent(multiContext);
    }

    private static bool HasTrackedResources(VmDeploymentContext context)
    {
        return context.VmFolderCreated || context.DifferencingDiskCreated || context.VmRegistered || context.VmStarted;
    }

    private static void EnsureOperationId(MultiVmDeploymentContext multiContext)
    {
        if (string.IsNullOrWhiteSpace(multiContext.OperationId))
        {
            multiContext.OperationId = Guid.NewGuid().ToString("N");
        }
    }

    private void EmitDeployEvent(string eventName, MultiVmDeploymentContext multiContext, string? result, string level = "info", IReadOnlyDictionary<string, object?>? extra = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["vmCount"] = multiContext.VmContexts.Count,
            ["stopAllOnAnyVmFailure"] = multiContext.StopAllOnAnyVmFailure
        };

        if (extra != null)
        {
            foreach (var pair in extra)
            {
                context[pair.Key] = pair.Value;
            }
        }

        _structuredLogger.Log(ParseLevel(level), eventName, multiContext.OperationId, result, context);
    }

    private void EmitDeployTerminalEvent(MultiVmDeploymentContext multiContext)
    {
        var eventName = multiContext.OperationState switch
        {
            DeploymentOperationState.Completed => "DeployLabCompleted",
            DeploymentOperationState.Cancelled or DeploymentOperationState.CancelledWithResiduals => "DeployLabCancelled",
            _ => "DeployLabFailed"
        };

        var result = multiContext.OperationState switch
        {
            DeploymentOperationState.Completed => "success",
            DeploymentOperationState.Cancelled => "cancelled",
            DeploymentOperationState.CancelledWithResiduals => "cancelled_with_residuals",
            DeploymentOperationState.FailedWithResiduals => "failed_with_residuals",
            _ => "failed"
        };

        EmitDeployEvent(
            eventName,
            multiContext,
            result,
            multiContext.OperationState is DeploymentOperationState.Failed or DeploymentOperationState.FailedWithResiduals or DeploymentOperationState.CancelledWithResiduals ? "error" : "info",
            new Dictionary<string, object?>
            {
                ["operationState"] = multiContext.OperationState.ToString(),
                ["cleanupVmCount"] = multiContext.CleanupResults.Count,
                ["residualVmCount"] = multiContext.CleanupResults.Count(r => r.HasResiduals)
            });
    }

    private void EmitVmScopedEvent(
        string eventName,
        string level,
        MultiVmDeploymentContext multiContext,
        VmDeploymentContext vmContext,
        string? result,
        IReadOnlyDictionary<string, object?>? extraContext = null)
    {
        var context = BuildVmContext(vmContext);
        if (extraContext != null)
        {
            foreach (var pair in extraContext)
            {
                context[pair.Key] = pair.Value;
            }
        }

        _structuredLogger.Log(ParseLevel(level), eventName, multiContext.OperationId, result, context);
    }

    private void EmitVmTerminalEvent(MultiVmDeploymentContext multiContext, VmDeploymentContext vmContext)
    {
        var (eventName, result, level) = vmContext.WasCancelled
            ? ("VmDeployFailed", "cancelled", "warn")
            : vmContext.IsSuccess
                ? ("VmDeployCompleted", "success", "info")
                : ("VmDeployFailed", vmContext.CleanupResult?.HasResiduals == true ? "failed_with_residuals" : "failed", "error");

        EmitVmScopedEvent(
            eventName,
            level,
            multiContext,
            vmContext,
            result,
            new Dictionary<string, object?>
            {
                ["failureStepKey"] = vmContext.FailureStepKey,
                ["errorMessage"] = vmContext.FailureMessage,
                ["cleanupRan"] = vmContext.CleanupResult != null,
                ["residualCount"] = vmContext.CleanupResult?.Residuals.Count ?? 0
            });
    }

    private void EmitCleanupStepEvents(MultiVmDeploymentContext multiContext, VmDeploymentContext vmContext, VmCleanupResult cleanupResult)
    {
        foreach (var step in cleanupResult.StepResults)
        {
            var eventName = step.Status switch
            {
                CleanupStepStatus.Failed => "CleanupStepFailed",
                _ => "CleanupStepCompleted"
            };
            var level = step.Status switch
            {
                CleanupStepStatus.Failed => "error",
                CleanupStepStatus.Skipped => "debug",
                _ => "info"
            };

            EmitVmScopedEvent(
                eventName,
                level,
                multiContext,
                vmContext,
                step.Status.ToString().ToLowerInvariant(),
                new Dictionary<string, object?>
                {
                    ["stepKey"] = step.Step.ToString(),
                    ["resourcePath"] = step.Target,
                    ["message"] = step.Message
                });
        }
    }

    private static Dictionary<string, object?> BuildVmContext(VmDeploymentContext vmContext)
    {
        return new Dictionary<string, object?>
        {
            ["vmId"] = vmContext.VmId.ToString("N"),
            ["vmName"] = vmContext.VmName
        };
    }

    private static StructuredLogLevel ParseLevel(string level)
    {
        return level switch
        {
            "debug" => StructuredLogLevel.Debug,
            "info" => StructuredLogLevel.Info,
            "warn" => StructuredLogLevel.Warn,
            "error" => StructuredLogLevel.Error,
            _ => StructuredLogLevel.Info
        };
    }
}

using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;

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
        EmitDeployEvent(LaStatus.DeployOrchestration_Deploying, multiContext, "started");
        var cleanupResultsLock = new object();

        var tasks = multiContext.VmContexts.Select(context => Task.Run(async () =>
        {
            DebugLogger.Log($"Deploying VM: {context.VmName}");

            var handle = new PowerShellHandle();
            var sessionCreationStopwatch = Stopwatch.StartNew();
            var session = _sessionFactory();
            sessionCreationStopwatch.Stop();
            var hyperV = _hyperVFactory(session);

            _sessionResolver.RegisterSession(handle, session);
            context.PowerShellHandle = handle;
            context.OperationId = multiContext.OperationId;
            context.StructuredEventEmitter = (code, result, extraContext) =>
                EmitVmScopedEvent(code, multiContext, context, result, extraContext);
            context.StepFailedCode = LaStatus.DeployStep_StepFailed;
            context.StepFailedNonBlockingCode = LaStatus.DeployStep_StepFailedNonBlocking;
            EmitVmScopedEvent(LaStatus.DeployOrchestration_DeployingVM, multiContext, context, "started");
            HyperVPowerShellTimingLogger.LogWorkflowSessionCreated(context.VmName, sessionCreationStopwatch.ElapsedMilliseconds);

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
                EmitVmScopedEvent(LaStatus.DeployCleanup_CleaningUpVM, multiContext, context, "started");
                var cleanupResult = await _cleanupOrchestrator.CleanupAsync(context, hyperV);
                context.CleanupResult = cleanupResult;
                EmitCleanupStepEvents(multiContext, context, cleanupResult);

                lock (cleanupResultsLock)
                {
                    multiContext.CleanupResults.Add(cleanupResult);
                }

                EmitVmScopedEvent(
                    cleanupResult.HasResiduals ? LaStatus.DeployCleanup_VMCleanupLeftResiduals : LaStatus.DeployCleanup_VMCleanupComplete,
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
                        LaStatus.DeployCleanup_VMCleanupResidualsDetected,
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
                context.StepStateEmitter = null;
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

    private void EmitDeployEvent(uint code, MultiVmDeploymentContext multiContext, string? result, IReadOnlyDictionary<string, object?>? extra = null)
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

        _structuredLogger.Log(code, multiContext.OperationId, result, context);
    }

    private void EmitDeployTerminalEvent(MultiVmDeploymentContext multiContext)
    {
        var (code, result) = multiContext.OperationState switch
        {
            DeploymentOperationState.Completed => (LaStatus.DeployOrchestration_DeploySucceeded, "success"),
            DeploymentOperationState.Cancelled => (LaStatus.DeployOrchestration_DeploymentCancelled, "cancelled"),
            DeploymentOperationState.CancelledWithResiduals => (LaStatus.DeployOrchestration_DeploymentCancelledWithResiduals, "cancelled_with_residuals"),
            DeploymentOperationState.FailedWithResiduals => (LaStatus.DeployOrchestration_DeploymentFailedWithResiduals, "failed_with_residuals"),
            _ => (LaStatus.DeployOrchestration_DeployFailed, "failed")
        };

        EmitDeployEvent(
            code,
            multiContext,
            result,
            BuildDeployTerminalContext(multiContext));
    }

    private void EmitVmScopedEvent(
        uint code,
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

        _structuredLogger.Log(code, multiContext.OperationId, result, context);
    }

    private void EmitVmTerminalEvent(MultiVmDeploymentContext multiContext, VmDeploymentContext vmContext)
    {
        var (code, result) = vmContext.WasCancelled
            ? (LaStatus.DeployOrchestration_VMDeployCancelled, "cancelled")
            : vmContext.IsSuccess
                ? (LaStatus.DeployOrchestration_VMDeployed, "success")
                : vmContext.CleanupResult?.HasResiduals == true
                    ? (LaStatus.DeployOrchestration_VMDeployFailedWithResiduals, "failed_with_residuals")
                    : (LaStatus.DeployOrchestration_VMDeployFailed, "failed");

        EmitVmScopedEvent(
            code,
            multiContext,
            vmContext,
            result,
            BuildVmTerminalContext(vmContext)
            );
    }

    private static IReadOnlyDictionary<string, object?> BuildVmTerminalContext(VmDeploymentContext vmContext)
    {
        var payload = new Dictionary<string, object?>
            {
                ["failureStepKey"] = vmContext.FailureStepKey,
                ["errorMessage"] = vmContext.FailureMessage,
                ["cleanupRan"] = vmContext.CleanupResult != null,
                ["residualCount"] = vmContext.CleanupResult?.Residuals.Count ?? 0
            };

        MergeNormalizedErrorMetadata(payload, vmContext.FailureMetadata);
        return payload;
    }

    private void EmitCleanupStepEvents(MultiVmDeploymentContext multiContext, VmDeploymentContext vmContext, VmCleanupResult cleanupResult)
    {
        foreach (var step in cleanupResult.StepResults)
        {
            var code = step.Status switch
            {
                CleanupStepStatus.Failed => LaStatus.DeployCleanup_CleanupStepFailed,
                CleanupStepStatus.Skipped => LaStatus.DeployCleanup_CleanupStepSkipped,
                _ => LaStatus.DeployCleanup_CleanupStepCompleted
            };

            EmitVmScopedEvent(
                code,
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
            ["vmName"] = vmContext.VmName,
            ["vmPath"] = vmContext.VmPath,
            ["targetVhdPath"] = vmContext.VhdPath,
            ["parentVhdPath"] = vmContext.BaseVhdPath
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildDeployTerminalContext(MultiVmDeploymentContext multiContext)
    {
        var context = new Dictionary<string, object?>
        {
            ["operationState"] = multiContext.OperationState.ToString(),
            ["cleanupVmCount"] = multiContext.CleanupResults.Count,
            ["residualVmCount"] = multiContext.CleanupResults.Count(r => r.HasResiduals)
        };

        var firstFailedVm = multiContext.VmContexts.FirstOrDefault(vm => !vm.IsSuccess && !vm.WasCancelled);
        if (firstFailedVm != null)
        {
            context["failedVmName"] = firstFailedVm.VmName;
            context["failedVmId"] = firstFailedVm.VmId.ToString("N");
            context["failureStepKey"] = firstFailedVm.FailureStepKey;
            context["errorMessage"] = firstFailedVm.FailureMessage;
            context["vmPath"] = firstFailedVm.VmPath;
            context["targetVhdPath"] = firstFailedVm.VhdPath;
            context["parentVhdPath"] = firstFailedVm.BaseVhdPath;
            MergeNormalizedErrorMetadata(context, firstFailedVm.FailureMetadata);
        }

        return context;
    }

    private static void MergeNormalizedErrorMetadata(
        Dictionary<string, object?> target,
        IReadOnlyDictionary<string, object?>? failureMetadata)
    {
        if (failureMetadata == null)
        {
            return;
        }

        foreach (var key in new[] { "exceptionType", "hresult", "errorCode" })
        {
            if (failureMetadata.TryGetValue(key, out var value))
            {
                target[key] = value;
            }
        }
    }
}

using System.Collections.Concurrent;
using LabAssistant.Business.Runtime;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.GuestExecution;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed partial class V2RuntimeCapabilityServiceTests
{
    [Fact]
    public async Task V2ForestTrustRuntimeStage_ManagedBidirectionalForestTrust_PreparesDnsCreatesTrustAndValidatesBothSides()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var logger = new RecordingStructuredLogger();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor, logger);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.Contains("trust:trust-contoso-fabrikam:PrepareForestTrustDns", executedNodeIds);
        Assert.Contains("trust:trust-contoso-fabrikam:CreateForestTrust", executedNodeIds);
        Assert.Contains("trust:trust-contoso-fabrikam:ValidateForestTrust", executedNodeIds);

        var scriptList = scripts.ToList();
        var firstDns = scriptList.FindIndex(entry => entry.Script.Contains("Add-DnsServerConditionalForwarderZone", StringComparison.Ordinal));
        var createTrust = scriptList.FindIndex(entry => entry.VmName == "dc01" && entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal));
        var sourceValidation = scriptList.FindIndex(entry => entry.VmName == "dc01" && entry.Script.Contains("Forest trust validated", StringComparison.Ordinal));
        var targetValidation = scriptList.FindIndex(entry => entry.VmName == "fabrikamdc01" && entry.Script.Contains("Forest trust validated", StringComparison.Ordinal));

        Assert.True(firstDns >= 0);
        Assert.True(createTrust > firstDns);
        Assert.True(sourceValidation > createTrust);
        Assert.True(targetValidation > createTrust);
        Assert.Contains(scriptList, entry => entry.VmName == "dc01" && entry.Script.Contains("fabrikam.com", StringComparison.Ordinal));
        Assert.Contains(scriptList, entry => entry.VmName == "fabrikamdc01" && entry.Script.Contains("contoso.com", StringComparison.Ordinal));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal));

        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.TrustObjectsCreated);
        Assert.True(trustContext.TrustReady);
        Assert.False(trustContext.CleanupAttempted);
        Assert.Contains(logger.Events, item =>
            item.Event == "ForestTrustCreationCompleted" &&
            item.OperationId == multiContext.OperationId &&
            item.Result == "success" &&
            item.Context != null &&
            item.Context.TryGetValue("trustId", out var trustId) &&
            string.Equals(trustId?.ToString(), "trust-contoso-fabrikam", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_BareDomainAdmin_QualifiesCredentialsWithDomainNetBios()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        // The real harness seeds domain-admin slots with a bare "Administrator". After dcpromo the local SAM is gone,
        // so PowerShell Direct into each promoted DC and the cross-forest CreateTrustRelationship target credential must be
        // NetBIOS-qualified (DOMAIN\User) by the stage, or authentication resolves against the wrong directory.
        var credentialSlots = (Dictionary<string, V2RuntimeCredential>)request.CredentialSlotValues;
        credentialSlots["slot-admin"] = new V2RuntimeCredential { Username = "Administrator", Password = "Password123!" };
        credentialSlots["slot-fabrikam-admin"] = new V2RuntimeCredential { Username = "Administrator", Password = "Password123!" };

        var scripts = new ConcurrentQueue<(string VmName, string Username, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, credential, script, _) =>
            {
                scripts.Enqueue((vmName, credential?.Username ?? string.Empty, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);

        var scriptList = scripts.ToList();

        // CreateTrustRelationship embeds the cross-forest target credential in its script; it must name the TARGET forest.
        Assert.Contains(scriptList, entry =>
            entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal) &&
            entry.Script.Contains(@"FABRIKAM\Administrator", StringComparison.Ordinal));
        Assert.DoesNotContain(scriptList, entry =>
            entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal) &&
            entry.Script.Contains("$targetUser = 'Administrator'", StringComparison.Ordinal));

        // PowerShell Direct into each promoted DC must log on as DOMAIN\Administrator, never a bare local account.
        Assert.Contains(scriptList, entry => entry.VmName == "dc01" && entry.Username == @"CONTOSO\Administrator");
        Assert.Contains(scriptList, entry => entry.VmName == "fabrikamdc01" && entry.Username == @"FABRIKAM\Administrator");
        Assert.DoesNotContain(scriptList, entry => string.Equals(entry.Username, "Administrator", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_TargetAnchorFailed_DoesNotRunDnsOrTrustCommands()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        anchors.Single(anchor => anchor.VmId == "vm-fabrikamdc01").Context.IsSuccess = false;
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.DoesNotContain(executedNodeIds, nodeId => nodeId.StartsWith("trust:", StringComparison.Ordinal));

        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.False(trustContext.TrustObjectsCreated);
        Assert.False(trustContext.CleanupAttempted);

        var scriptList = scripts.ToList();
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("Add-DnsServerConditionalForwarderZone", StringComparison.Ordinal));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("Forest trust validated", StringComparison.Ordinal));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_PartialCreateFailure_CleansTrustObjects()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
                {
                    return Task.FromResult(new GuestCommandResult { Success = false, Error = "trust create failed after source object creation" });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.TrustObjectsCreated);
        Assert.False(trustContext.TrustReady);
        Assert.True(trustContext.CleanupAttempted);
        Assert.False(trustContext.CleanupResidual);

        var scriptList = scripts.ToList();
        Assert.Contains(scriptList, entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal));
        Assert.Equal(2, scriptList.Count(entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal)));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("Remove-DnsServerZone", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_ReadyBeforeLaterTrustFailure_CleansReadyTrustObjects()
    {
        var request = await CreateRuntimeRequestWithTwoForestTrustsAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                if (vmName == "dc01" &&
                    script.Contains("CreateTrustRelationship", StringComparison.Ordinal) &&
                    scripts.Count(entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal)) == 2)
                {
                    return Task.FromResult(new GuestCommandResult { Success = false, Error = "later trust create failed" });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.Equal(2, multiContext.V2TrustContexts.Count);
        var readyTrust = Assert.Single(multiContext.V2TrustContexts, trust => trust.TrustId == "trust-a-contoso-fabrikam");
        var failedTrust = Assert.Single(multiContext.V2TrustContexts, trust => trust.TrustId == "trust-b-contoso-fabrikam");
        Assert.True(readyTrust.TrustReady);
        Assert.True(readyTrust.CleanupAttempted);
        Assert.False(readyTrust.CleanupResidual);
        Assert.False(failedTrust.TrustReady);
        Assert.True(failedTrust.CleanupAttempted);
        Assert.False(failedTrust.CleanupResidual);

        var scriptList = scripts.ToList();
        Assert.Equal(2, scriptList.Count(entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal)));
        Assert.Equal(4, scriptList.Count(entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal)));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("Remove-DnsServerZone", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_ValidationFailure_CleansTrustObjectsAndLeavesDnsForwarders()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                if (vmName == "fabrikamdc01" && script.Contains("Forest trust validated", StringComparison.Ordinal))
                {
                    return Task.FromResult(new GuestCommandResult { Success = false, Error = "target trust validation failed" });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.TrustObjectsCreated);
        Assert.False(trustContext.TrustReady);
        Assert.True(trustContext.CleanupAttempted);
        Assert.False(trustContext.CleanupResidual);

        var scriptList = scripts.ToList();
        Assert.Contains(scriptList, entry => entry.Script.Contains("Add-DnsServerConditionalForwarderZone", StringComparison.Ordinal));
        Assert.Equal(2, scriptList.Count(entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal)));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("Remove-DnsServerZone", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CancellationAfterCreation_CleansTrustObjects()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor();
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = (vmName, _, script, _) =>
        {
            scripts.Enqueue((vmName, script));
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
            }

            return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
        };
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.TrustObjectsCreated);
        Assert.False(trustContext.TrustReady);
        Assert.True(trustContext.CleanupAttempted);
        Assert.False(trustContext.CleanupResidual);

        var scriptList = scripts.ToList();
        Assert.Contains(scriptList, entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal));
        Assert.Equal(2, scriptList.Count(entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal)));
        Assert.DoesNotContain(executedNodeIds, nodeId => nodeId == "trust:trust-contoso-fabrikam:ValidateForestTrust");
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_MissingRuntimeCredential_BlocksBeforeTrustObjectsAreCreated()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.CredentialSlotValues = request.CredentialSlotValues
            .Where(pair => pair.Key != "slot-fabrikam-admin")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.False(trustContext.TrustObjectsCreated);
        Assert.False(trustContext.TrustReady);
        Assert.False(trustContext.CleanupAttempted);

        var scriptList = scripts.ToList();
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal));
        Assert.DoesNotContain(scriptList, entry => entry.Script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal));
        Assert.DoesNotContain(executedNodeIds, nodeId => nodeId == "trust:trust-contoso-fabrikam:CreateForestTrust");
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_SourceValidationTransientFailure_RetriesThenSucceeds()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 3;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var sourceValidationAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Forest trust validated", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref sourceValidationAttempts);
                    if (attempt == 1)
                    {
                        return Task.FromResult(new GuestCommandResult { Success = false, Error = "trust object not yet replicated" });
                    }
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.True(sourceValidationAttempts >= 2, $"Expected source-side validation to retry, saw {sourceValidationAttempts} attempt(s).");
        Assert.Contains("trust:trust-contoso-fabrikam:ValidateForestTrust", executedNodeIds);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.TrustReady);
        Assert.False(trustContext.CleanupAttempted);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_SourceValidationAlwaysFails_FailsAfterExhaustingRetries()
    {
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 3;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var sourceValidationAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Forest trust validated", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref sourceValidationAttempts);
                    return Task.FromResult(new GuestCommandResult { Success = false, Error = "trust object never replicated" });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.Equal(3, sourceValidationAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.False(trustContext.TrustReady);
        Assert.True(trustContext.CleanupAttempted);
    }

    private static (MultiVmDeploymentContext MultiContext, IReadOnlyList<V2ForestTrustAnchorState> Anchors) CreateForestTrustStageContext(
        V2RuntimeExecutionRequest request)
    {
        var multiContext = new MultiVmDeploymentContext
        {
            StopAllOnAnyVmFailure = request.Settings.StopAllOnAnyVmFailure
        };
        var anchorVmIds = request.Plan.Context.Trusts
            .SelectMany(trust => new[] { trust.SourceAnchorVmId, trust.TargetAnchorVmId })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var anchors = new List<V2ForestTrustAnchorState>();

        foreach (var vmId in anchorVmIds)
        {
            var planVm = request.Plan.Context.Vms.First(vm => string.Equals(vm.VmId, vmId, StringComparison.OrdinalIgnoreCase));
            var context = new VmDeploymentContext
            {
                VmName = planVm.VmName,
                PerVmFailFast = request.Settings.PerVmFailFast,
                V2TopologyRole = planVm.TopologyRole,
                V2DomainId = planVm.DomainId,
                OperationId = multiContext.OperationId
            };
            context.ShouldAbort = () => multiContext.IsCancellationRequested;
            context.OnBlockingFailure = () =>
            {
                if (multiContext.StopAllOnAnyVmFailure)
                {
                    multiContext.RequestCancellation();
                }
            };

            multiContext.VmContexts.Add(context);
            anchors.Add(new V2ForestTrustAnchorState(planVm.VmId, context));
        }

        return (multiContext, anchors);
    }
}

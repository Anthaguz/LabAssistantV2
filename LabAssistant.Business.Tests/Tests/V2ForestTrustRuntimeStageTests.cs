using System.Collections.Concurrent;
using LabAssistant.Business.Runtime;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Diagnostics;
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
            item.Code == $"0x{LaStatus.DeployForestTrust_ForestTrustCreated:X8}" &&
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
    public async Task V2ForestTrustRuntimeStage_CreateTrust_GuestRebootMidHop_RetriesThenSucceeds()
    {
        // CreateForestTrust runs over PowerShell Direct and can hit the same torn-down-socket drop the AD DS steps
        // did. The trust-create script is existence-guarded (GetTrustRelationship -> CreateTrustRelationship only when
        // absent), so it is safe to re-run: a GuestRebooting drop must retry through the shared helper and recover
        // rather than failing the trust.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var createAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref createAttempts);
                    if (attempt == 1)
                    {
                        return Task.FromResult(new GuestCommandResult
                        {
                            Success = false,
                            Error = "Hyper-V\\Invoke-Command : The Hyper-V socket target process has ended."
                        });
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

        Assert.Equal(2, createAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.TrustReady);
        Assert.False(trustContext.CleanupAttempted);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CreateTrust_NonRebootError_FailsFastWithoutRetrying()
    {
        // A deterministic trust-create error (not a torn-down transport) must fail fast on the first attempt and drive
        // cleanup, proving the transport-drop retry did not become a blanket retry of real forest-trust failures.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var createAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref createAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "New-Object : Exception calling \".ctor\" with \"3\" argument(s): The specified domain does not exist."
                    });
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

        Assert.Equal(1, createAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.False(trustContext.TrustReady);
        Assert.True(trustContext.CleanupAttempted);
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

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupCredentialInvalidTransient_RetriesThenSucceeds()
    {
        // Finding 85: on rollback the DCs go back into flux, so the best-effort in-guest trust deletion can hit a
        // broken PowerShell Direct session that surfaces "the credential is invalid" wrapped in an
        // OpenError / PSSessionStateBroken / PSDirectException - the finding-81 transient, which the classifier reads as
        // AuthenticationRejected. The domain-admin credential was already validated when the trust was created and the
        // delete is idempotent, so the cleanup path must retry that signature rather than fail fast and leave a
        // dangling trust (a no-orphans violation).
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var sourceDeleteAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = (vmName, _, script, _) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }

            if (vmName == "dc01" && script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                var attempt = Interlocked.Increment(ref sourceDeleteAttempts);
                if (attempt == 1)
                {
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "Invoke-Command : The credential is invalid. OpenError: (LAT-dc-alpha:String) [], PSDirectException FullyQualifiedErrorId : PSSessionStateBroken"
                    });
                }
            }

            return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.True(sourceDeleteAttempts >= 2, $"Expected cleanup to retry the credential-invalid transient, saw {sourceDeleteAttempts} attempt(s).");
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.CleanupAttempted);
        Assert.False(trustContext.CleanupResidual);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CreateTrust_CredentialInvalidError_FailsFastWithoutRetrying()
    {
        // Finding-81 constraint: on the DEPLOY path a credential rejection - even when it arrives wrapped in a broken
        // session OpenError / PSSessionStateBroken - is content-indistinguishable from a genuine wrong password, so it
        // must still fail fast on the first attempt. The cleanup-only auth tolerance must not widen this path.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var createAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref createAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "Invoke-Command : The credential is invalid. OpenError: (LAT-dc-alpha:String) [], PSDirectException FullyQualifiedErrorId : PSSessionStateBroken"
                    });
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

        Assert.Equal(1, createAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.False(trustContext.TrustReady);
        Assert.True(trustContext.CleanupAttempted);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupDeterministicError_FailsFastAndFlagsResidual()
    {
        // The cleanup auth tolerance must not become a blanket retry: a deterministic delete error that is neither a
        // transport drop nor a credential rejection must fail fast on the first attempt and flag residual, so an
        // operator sees an unrecoverable orphan rather than the cleanup looping the whole transport budget.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var sourceDeleteAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = (vmName, _, script, _) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }

            if (vmName == "dc01" && script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref sourceDeleteAttempts);
                return Task.FromResult(new GuestCommandResult
                {
                    Success = false,
                    Error = "Remove-ADTrust : The specified directory service attribute or value does not exist."
                });
            }

            return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.Equal(1, sourceDeleteAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.CleanupAttempted);
        Assert.True(trustContext.CleanupResidual);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupCredentialInvalidNeverRecovers_ExhaustsBoundedRetriesAndFlagsResidual()
    {
        // Hard constraint: the cleanup auth tolerance must stay bounded. A credential rejection that never clears (the
        // worst case, a genuinely unusable credential during rollback) must exhaust GuestTransportMaxRetries and then
        // flag residual, never loop the best-effort cleanup forever.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 3;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var sourceDeleteAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = (vmName, _, script, _) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }

            if (vmName == "dc01" && script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref sourceDeleteAttempts);
                return Task.FromResult(new GuestCommandResult
                {
                    Success = false,
                    Error = "Invoke-Command : The credential is invalid. OpenError: (LAT-dc-alpha:String) [], PSDirectException FullyQualifiedErrorId : PSSessionStateBroken"
                });
            }

            return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.Equal(3, sourceDeleteAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.CleanupAttempted);
        Assert.True(trustContext.CleanupResidual);
    }

    [Fact]
    public void V2RuntimeExecutionRequest_DefaultTransportBudget_OutlastsRebootRecoveryWindow()
    {
        // Finding 85 no-orphans invariant: the forest-trust cleanup retry (including its opted-in tolerance of the
        // credential-invalid transient) is bounded by the transport budget, GuestTransportMaxRetries x
        // GuestTransportRetryDelay. That budget MUST outlast the reboot/specialize window a DC needs to become
        // reachable again during rollback - the same window the deploy-path auth grace (GuestAuthGraceWindow) measures,
        // whose documented tail is ~340-360s - or the cleanup would exhaust mid-reboot and leave exactly the dangling
        // trust it exists to remove. Pin the ceiling so a future budget reduction cannot silently reintroduce orphans.
        var request = new V2RuntimeExecutionRequest();
        var transportBudget = request.GuestTransportRetryDelay * request.GuestTransportMaxRetries;

        Assert.True(
            transportBudget >= request.GuestAuthGraceWindow,
            $"Transport budget {transportBudget.TotalSeconds:F0}s must outlast the reboot-recovery window {request.GuestAuthGraceWindow.TotalSeconds:F0}s so rollback cleanup does not exhaust mid-reboot.");
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupHangingCall_AbandonedByPerAttemptTimeout_RetriesThenSucceeds()
    {
        // Finding 85 rework: the real failure was not a missing wrap or a bad classification, it was a cleanup attempt
        // that BLOCKS. A PowerShell Direct call into a mid-reboot DC can hang in connection negotiation; the one-shot
        // session's timeout surfaces that as a thrown exception, and the retry loop used to let it escape uncaught,
        // aborting cleanup with no terminal event (cleanup.start then permanent silence). With a per-attempt timeout the
        // frozen attempt is abandoned and the loop regains control to retry through the reboot and then succeed.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 90;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        request.GuestCleanupAttemptTimeout = TimeSpan.FromMilliseconds(50);
        request.GuestCleanupRetryBudget = TimeSpan.FromSeconds(30);

        var sourceDeleteAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = async (vmName, _, script, attemptCancellation) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return new GuestCommandResult { Success = true, Output = vmName };
            }

            if (vmName == "dc01" && script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                var attempt = Interlocked.Increment(ref sourceDeleteAttempts);
                if (attempt == 1)
                {
                    // Never return until the per-attempt timeout cancels the attempt token: this is the hang the
                    // wrap must survive. Without the per-attempt bound this await would block the loop forever.
                    await Task.Delay(Timeout.Infinite, attemptCancellation);
                }
            }

            return new GuestCommandResult { Success = true, Output = vmName };
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.True(sourceDeleteAttempts >= 2, $"Expected cleanup to abandon the hung attempt and retry, saw {sourceDeleteAttempts} attempt(s).");
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.CleanupAttempted);
        Assert.False(trustContext.CleanupResidual);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupHangingCall_NeverRecovers_BoundedFailsAndFlagsResidual()
    {
        // The per-attempt timeout must not trade an infinite hang for an unbounded retry loop: a cleanup call that
        // hangs on EVERY attempt (a genuinely gone DC that never finishes negotiating) must exhaust the wall-clock
        // retry budget and then flag residual, returning a terminal result rather than hanging. The whole point of
        // finding 85 is that this path must always end - success or bounded failure - so the backstop sees residual.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 90;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        request.GuestCleanupAttemptTimeout = TimeSpan.FromMilliseconds(20);
        request.GuestCleanupRetryBudget = TimeSpan.FromMilliseconds(100);

        var sourceDeleteAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = async (vmName, _, script, attemptCancellation) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return new GuestCommandResult { Success = true, Output = vmName };
            }

            if (script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                if (vmName == "dc01")
                {
                    Interlocked.Increment(ref sourceDeleteAttempts);
                }

                // Hang on every attempt: the guest never finishes negotiating. Each attempt is abandoned at the
                // per-attempt timeout and the loop must stop once the retry budget elapses.
                await Task.Delay(Timeout.Infinite, attemptCancellation);
            }

            return new GuestCommandResult { Success = true, Output = vmName };
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.True(sourceDeleteAttempts >= 2, $"Expected cleanup to retry the hung attempt before bounded-failing, saw {sourceDeleteAttempts} attempt(s).");
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.CleanupAttempted);
        Assert.True(trustContext.CleanupResidual);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupBothAnchorsBeingTornDown_SkipsInGuestDeleteAndDoesNotConsumeBudget()
    {
        // Finding 87: on a full cancel both trust anchors are run-created VMs the runtime is about to destroy, so the
        // in-guest local-side delete is moot - the trust object dies with the disk. It must be SKIPPED, not retried:
        // retrying a delete against a rebooting or vanishing DC for the whole cleanup budget is exactly what blocked the
        // mandatory VM/disk teardown and left the ~30-minute orphan window (the finding-86 pairing). Proof: the delete
        // script is never invoked and cleanup returns promptly even though the executor would hang forever if it ran.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        // A budget large enough that, if the skip regressed, a single hung attempt would block well past the guard.
        request.GuestCleanupAttemptTimeout = TimeSpan.FromSeconds(30);
        request.GuestCleanupRetryBudget = TimeSpan.FromMinutes(15);

        var deleteInvocations = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = async (vmName, _, script, attemptCancellation) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return new GuestCommandResult { Success = true, Output = vmName };
            }

            if (script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref deleteInvocations);
                // If the skip regresses and this runs, hang forever so the timeout guard below fails loudly.
                await Task.Delay(Timeout.Infinite, attemptCancellation);
            }

            return new GuestCommandResult { Success = true, Output = vmName };
        };
        var stage = new V2ForestTrustRuntimeStage(guestExecutor);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);

        // Both anchors are run-created (differencing disk + registration) and the run is cancelling, so the finding-86
        // gate tears both down - which is precisely when their in-guest trust delete becomes moot.
        foreach (var vmContext in multiContext.VmContexts)
        {
            vmContext.DifferencingDiskCreated = true;
            vmContext.VmRegistered = true;
        }

        var cleanupTask = stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);
        var finished = await Task.WhenAny(cleanupTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(
            ReferenceEquals(finished, cleanupTask),
            "Cleanup must return promptly by skipping the moot in-guest delete, not block on the retry budget.");
        await cleanupTask;

        Assert.Equal(0, deleteInvocations);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.True(trustContext.CleanupAttempted);
        // A skipped side is not a residual: the trust object is destroyed with the anchor VM, nothing dangles.
        Assert.False(trustContext.CleanupResidual);
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupSurvivingAnchor_RunsInGuestDeleteWhileTornDownAnchorIsSkipped()
    {
        // Finding 87: when only one anchor survives the rollback (for example a pre-existing DC the run did not create),
        // that survivor keeps a real one-sided trust that must be cleared, so its in-guest delete still runs under the
        // bounded retry wrap; the other anchor is being destroyed, so its delete is skipped. This proves the decoupling
        // is precise and does not silently drop a survivor's dangling trust.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var sourceDeleteAttempts = 0;
        var targetDeleteAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = (vmName, _, script, _) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }

            if (script.Contains("DeleteLocalSideOfTrustRelationship", StringComparison.Ordinal))
            {
                if (vmName == "dc01")
                {
                    var attempt = Interlocked.Increment(ref sourceDeleteAttempts);
                    if (attempt == 1)
                    {
                        // The survivor's session can still blip on the finding-81 transient; the wrap must retry it.
                        return Task.FromResult(new GuestCommandResult
                        {
                            Success = false,
                            Error = "Invoke-Command : The credential is invalid. OpenError PSSessionStateBroken"
                        });
                    }
                }
                else if (vmName == "fabrikamdc01")
                {
                    Interlocked.Increment(ref targetDeleteAttempts);
                }
            }

            return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
        };
        var logger = new RecordingStructuredLogger();
        var stage = new V2ForestTrustRuntimeStage(guestExecutor, logger);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);

        // Only the target anchor is run-created (being torn down). The source anchor is pre-existing and survives.
        var targetContext = multiContext.VmContexts.Single(vm => vm.VmName == "fabrikamdc01");
        targetContext.DifferencingDiskCreated = true;
        targetContext.VmRegistered = true;

        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.True(sourceDeleteAttempts >= 2, $"Surviving anchor delete must run and retry the transient, saw {sourceDeleteAttempts}.");
        Assert.Equal(0, targetDeleteAttempts);
        var trustContext = Assert.Single(multiContext.V2TrustContexts);
        Assert.False(trustContext.CleanupResidual);
        Assert.Contains(logger.Events, item =>
            item.Code == $"0x{LaStatus.DeployForestTrust_ForestTrustCleanedUp:X8}" &&
            item.Result == "success" &&
            item.Context != null &&
            item.Context.TryGetValue("sourceAnchorOutcome", out var sourceOutcome) &&
            string.Equals(sourceOutcome?.ToString(), "success", StringComparison.Ordinal) &&
            item.Context.TryGetValue("targetAnchorOutcome", out var targetOutcome) &&
            string.Equals(targetOutcome?.ToString(), "skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task V2ForestTrustRuntimeStage_CleanupBothAnchorsTornDown_EmitsSkippedTerminalNotSuccess()
    {
        // Finding 87 telemetry contract: a skip is not a success. When both anchors are being destroyed the terminal
        // must be a distinct cleanup.end result=skipped carrying per-side outcomes and a reason, never a fabricated
        // cleaned-up/success for work that was deliberately not done.
        var request = await CreateRuntimeRequestWithForestTrustAsync();
        var guestExecutor = new FakeGuestCommandExecutor();
        var (multiContext, anchors) = CreateForestTrustStageContext(request);
        guestExecutor.OnExecuteAsync = (vmName, _, script, _) =>
        {
            if (vmName == "dc01" && script.Contains("CreateTrustRelationship", StringComparison.Ordinal))
            {
                multiContext.RequestUserCancellation();
            }

            return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
        };
        var logger = new RecordingStructuredLogger();
        var stage = new V2ForestTrustRuntimeStage(guestExecutor, logger);
        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = stage.InitializeRuntimeState(request, anchors, multiContext);

        await stage.ExecuteAsync(request, multiContext, anchors, executedNodeIds, CancellationToken.None);
        foreach (var vmContext in multiContext.VmContexts)
        {
            vmContext.DifferencingDiskCreated = true;
            vmContext.VmRegistered = true;
        }

        await stage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);

        Assert.Contains(logger.Events, item =>
            item.Code == $"0x{LaStatus.DeployForestTrust_ForestTrustCleanupSkipped:X8}" &&
            item.OperationId == multiContext.OperationId &&
            item.Result == "skipped" &&
            item.Context != null &&
            item.Context.TryGetValue("sourceAnchorOutcome", out var sourceOutcome) &&
            string.Equals(sourceOutcome?.ToString(), "skipped", StringComparison.Ordinal) &&
            item.Context.TryGetValue("targetAnchorOutcome", out var targetOutcome) &&
            string.Equals(targetOutcome?.ToString(), "skipped", StringComparison.Ordinal) &&
            item.Context.TryGetValue("skipReason", out _));
        Assert.DoesNotContain(logger.Events, item =>
            item.Code == $"0x{LaStatus.DeployForestTrust_ForestTrustCleanedUp:X8}" && item.Result == "success");
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

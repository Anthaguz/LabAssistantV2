using System.Collections.Concurrent;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Models.Templates;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed partial class V2RuntimeCapabilityServiceTests
{
    private readonly IV2PlanningCapabilityService _planningService = new V2PlanningCapabilityService();

    [Fact]
    public async Task ExecuteAsync_V1Template_ReturnsBlockingResultWithoutStartingRuntime()
    {
        var template = new LabTemplate
        {
            Id = "legacy-template",
            Name = "Legacy",
            SchemaVersion = "1.0.0",
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-legacy",
                    Name = "legacy01",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdxId = "disk-legacy"
                }
            ]
        };

        var plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = [CreateCatalogItem("disk-legacy", "slot-local")],
            AvailableSwitchNames = ["vSwitch-Core"],
            ResolvedCredentialSlotKeys = ["slot-local"],
            DefaultDeploymentProfile = "Balanced"
        });

        var hyperV = new FakeHyperVService();
        var service = CreateService(hyperV, new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(new V2RuntimeExecutionRequest
        {
            Template = template,
            Plan = plan,
            Settings = CreateSettings(),
            CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase)
            {
                ["slot-local"] = new() { Username = "Administrator", Password = "Password123!" }
            }
        });

        Assert.False(result.Success);
        Assert.Contains(result.BlockingMessages, message => message.Contains("V2 runtime rejects V1 templates.", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(hyperV.Operations);
        Assert.Equal(DeploymentOperationState.Idle, result.DeploymentContext.OperationState);
    }

    [Fact]
    public async Task ExecuteAsync_RootReadinessGate_BlocksNonRootProvisionUntilRootGuestTransportReady()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: false);
        var rootReadyEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRootReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hyperV = new FakeHyperVService();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = async (vmName, _, script, cancellationToken) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    rootReadyEntered.TrySetResult(true);
                    await releaseRootReady.Task.WaitAsync(cancellationToken);
                }

                return new GuestCommandResult { Success = true, Output = vmName };
            }
        };
        var service = CreateService(hyperV, guestExecutor);

        var executionTask = service.ExecuteAsync(request);
        await rootReadyEntered.Task;

        Assert.Contains("CreateVm:dc01", hyperV.Operations);
        Assert.DoesNotContain("CreateVm:member01", hyperV.Operations);

        releaseRootReady.SetResult(true);
        var result = await executionTask;

        Assert.True(result.Success);
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-dc01:GuestTransportReady");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-member01:ProvisionVm");
    }

    [Fact]
    public async Task ExecuteAsync_PostRootDomainProgression_RunsThroughV2RuntimePath()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        var service = CreateService(new FakeHyperVService(), new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("vm:vm-dc01:InstallAdDomainServicesFeature", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:PromoteFirstDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:DomainReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-replica01:PromoteReplicaDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-replica01:ReplicaDomainReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:StabilizeDomainDns", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-member01:JoinDomain", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-member01:JoinedDomainReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:ConfigureBaseRemoteAccess", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:BaseRemoteAccessReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-member01:ConfigureBaseRemoteAccess", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_AggressiveProfile_AllowsBackfillBeforeOtherProvisionCompletes_WhileBalancedWaits()
    {
        var balancedRequest = await CreateRuntimeRequestAsync("Balanced", includeStandalone: true, includeRouter: false);
        var aggressiveRequest = await CreateRuntimeRequestAsync("Aggressive", includeStandalone: true, includeRouter: false);

        var balancedHyperV = new FakeHyperVService();
        var balancedRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        balancedHyperV.OnCreateVmAsync = async vmName =>
        {
            if (vmName == "standalone01")
            {
                await balancedRelease.Task;
            }
        };

        var aggressiveHyperV = new FakeHyperVService();
        var aggressiveRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        aggressiveHyperV.OnCreateVmAsync = async vmName =>
        {
            if (vmName == "standalone01")
            {
                await aggressiveRelease.Task;
            }
        };

        var balancedService = CreateService(balancedHyperV, new FakeGuestCommandExecutor());
        var aggressiveService = CreateService(aggressiveHyperV, new FakeGuestCommandExecutor());

        var balancedTask = balancedService.ExecuteAsync(balancedRequest);
        await WaitForOperationAsync(balancedHyperV.Operations, "CreateVm:standalone01");
        Assert.DoesNotContain("StartVm:member01", balancedHyperV.Operations);
        balancedRelease.SetResult(true);
        var balancedResult = await balancedTask;

        var aggressiveTask = aggressiveService.ExecuteAsync(aggressiveRequest);
        await WaitForOperationAsync(aggressiveHyperV.Operations, "CreateVm:standalone01");
        await WaitForOperationAsync(aggressiveHyperV.Operations, "StartVm:member01");
        aggressiveRelease.SetResult(true);
        var aggressiveResult = await aggressiveTask;

        Assert.True(balancedResult.Success);
        Assert.True(aggressiveResult.Success);
        Assert.DoesNotContain("StartVm:member01", balancedHyperV.Operations.TakeWhile(op => op != "CreateVm:standalone01"));
        Assert.Contains("StartVm:member01", aggressiveHyperV.Operations);
    }

    [Fact]
    public async Task ExecuteAsync_RouterAwarePlan_ExecutesRouterRuntimeAndCrossSwitchProgression()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: true);
        var hyperV = new FakeHyperVService();
        var service = CreateService(hyperV, new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:PrepareRouterNetwork");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:InstallRouterRemoteAccessFeature");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:EnableRouterRouting");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:ConfigureRouterNat");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:ValidateCrossSwitchRouting");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:ValidateRouterEgress");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:RouterReady");
        Assert.Contains("CreateVm:router01", hyperV.Operations);
    }

    [Fact]
    public async Task ExecuteAsync_ReusesExistingTypedNetworkSwitchWithoutCreatingOrDeletingIt()
    {
        var operations = new ConcurrentQueue<string>();
        var request = await CreateSwitchRuntimeRequestAsync(
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-core",
                    Name = "Core",
                    SwitchName = "vSwitch-Core",
                    SwitchType = V2SwitchTypeCatalog.Internal
                }
            ],
            [new VmNetworkInterfaceTemplate { NicId = "nic-core", NetworkId = "lab-core" }],
            [CreateSwitch("vSwitch-Core", V2SwitchTypeCatalog.Internal)]);
        var hyperV = new FakeHyperVService { SharedOperations = operations };
        var machineAdmin = new FakeHyperVMachineAdminService(operations)
        {
            Switches =
            [
                new HyperVVirtualSwitchInfo { Name = "vSwitch-Core", SwitchType = V2SwitchTypeCatalog.Internal }
            ]
        };
        var service = CreateService(hyperV, new FakeGuestCommandExecutor(), machineAdminService: machineAdmin);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("switch:vswitch-core:EnsureNetworkSwitch", result.ExecutedNodeIds);
        Assert.Empty(machineAdmin.CreatedSwitches);
        Assert.Empty(machineAdmin.DeletedSwitches);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesMissingInternalSwitchBeforeProvisioningVm()
    {
        var operations = new ConcurrentQueue<string>();
        var request = await CreateSwitchRuntimeRequestAsync(
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-core",
                    Name = "Core",
                    SwitchName = "vSwitch-NewCore",
                    SwitchType = V2SwitchTypeCatalog.Internal
                }
            ],
            [new VmNetworkInterfaceTemplate { NicId = "nic-core", NetworkId = "lab-core" }],
            []);
        var hyperV = new FakeHyperVService { SharedOperations = operations };
        var machineAdmin = new FakeHyperVMachineAdminService(operations);
        var service = CreateService(hyperV, new FakeGuestCommandExecutor(), machineAdminService: machineAdmin);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains(machineAdmin.CreatedSwitches, request =>
            request.Name == "vSwitch-NewCore" &&
            request.SwitchType == V2SwitchTypeCatalog.Internal);
        var orderedOperations = operations.ToList();
        Assert.True(
            orderedOperations.IndexOf("CreateSwitch:vSwitch-NewCore") <
            orderedOperations.IndexOf("CreateVm:networked01"));
        Assert.Empty(machineAdmin.DeletedSwitches);
    }

    [Fact]
    public async Task ExecuteAsync_FailedDeploymentDeletesOnlySwitchesCreatedByThisRun()
    {
        var operations = new ConcurrentQueue<string>();
        var request = await CreateSwitchRuntimeRequestAsync(
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-existing",
                    Name = "Existing",
                    SwitchName = "vSwitch-Existing",
                    SwitchType = V2SwitchTypeCatalog.Internal
                },
                new LabNetworkTemplate
                {
                    NetworkId = "lab-created",
                    Name = "Created",
                    SwitchName = "vSwitch-Created",
                    SwitchType = V2SwitchTypeCatalog.Private
                }
            ],
            [
                new VmNetworkInterfaceTemplate { NicId = "nic-existing", NetworkId = "lab-existing" },
                new VmNetworkInterfaceTemplate { NicId = "nic-created", NetworkId = "lab-created" }
            ],
            [CreateSwitch("vSwitch-Existing", V2SwitchTypeCatalog.Internal)]);
        var hyperV = new FakeHyperVService
        {
            SharedOperations = operations,
            CreateVmResult = false
        };
        var machineAdmin = new FakeHyperVMachineAdminService(operations)
        {
            Switches =
            [
                new HyperVVirtualSwitchInfo { Name = "vSwitch-Existing", SwitchType = V2SwitchTypeCatalog.Internal }
            ]
        };
        var service = CreateService(hyperV, new FakeGuestCommandExecutor(), machineAdminService: machineAdmin);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Contains(machineAdmin.CreatedSwitches, request => request.Name == "vSwitch-Created");
        Assert.Contains("vSwitch-Created", machineAdmin.DeletedSwitches);
        Assert.DoesNotContain("vSwitch-Existing", machineAdmin.DeletedSwitches);
        Assert.Contains(result.DeploymentContext.V2NetworkSwitchContexts, context =>
            context.SwitchName == "vSwitch-Created" &&
            context.CreatedByDeployment &&
            context.CleanupAttempted &&
            !context.CleanupResidual);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationDuringGuestReadiness_EndsCancelled()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: false);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = async (vmName, _, script, cancellationToken) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return new GuestCommandResult { Success = true, Output = vmName };
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);
        using var cts = new CancellationTokenSource();

        var executionTask = service.ExecuteAsync(request, cts.Token);
        await entered.Task;
        cts.Cancel();
        var result = await executionTask;

        Assert.False(result.Success);
        Assert.Equal(DeploymentOperationState.Cancelled, result.DeploymentContext.OperationState);
        Assert.Contains(result.DeploymentContext.VmContexts, vm => vm.VmName == "dc01" && vm.WasCancelled);
    }

    [Fact]
    public async Task ExecuteAsync_BaseRemoteAccess_UsesDeployTimeOptions()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: false);
        request.BaseRemoteAccessOptions = new V2BaseRemoteAccessOptions
        {
            EnableRemoteDesktop = true,
            SetPrivateNetworkProfile = true,
            DisableFirewall = false,
            DisableRdpNla = false
        };

        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        var remoteAccessScript = scripts.First(entry =>
            entry.VmName == "dc01" &&
            entry.Script.Contains("Base remote access configured", StringComparison.Ordinal));
        Assert.DoesNotContain("advfirewall", remoteAccessScript.Script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetUserAuthenticationRequired", remoteAccessScript.Script, StringComparison.Ordinal);
        Assert.Contains("fDenyTSConnections", remoteAccessScript.Script, StringComparison.Ordinal);
        Assert.Contains("Set-NetConnectionProfile", remoteAccessScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_BaseRemoteAccessReady_ProbeNeverReady_FailsAfterExhaustingRetries()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 3;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Get-NetTCPConnection", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref probeAttempts);
                    return Task.FromResult(new GuestCommandResult { Success = false, Error = "RDP listener not yet bound." });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(3, probeAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("Base remote access did not become ready", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_CredentialRejected_FailsFastWithoutExhaustingRetries()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        request.GuestAuthGraceAttempts = 2;

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref probeAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "Hyper-V\\Invoke-Command : The credential is invalid."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var logger = new RecordingStructuredLogger();
        var service = CreateService(new FakeHyperVService(), guestExecutor, logger);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);

        // The grace window tolerates the first rejection (specialize may still be applying the password), then the
        // loop fails fast at attempt 2 instead of grinding through all 25 retries.
        Assert.Equal(2, probeAttempts);

        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("does not match this VM's base image", StringComparison.Ordinal));

        Assert.Contains(
            logger.Events,
            e => e.Event == "GuestReadinessAttempt" &&
                 e.Context != null &&
                 e.Context.TryGetValue("errorCategory", out var category) &&
                 (category as string) == "AuthenticationRejected");
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_TransientCredentialErrorWithinGrace_RetriesThenSucceeds()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        request.GuestAuthGraceAttempts = 5;

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    if (attempt <= 2)
                    {
                        // A freshly cloned guest can briefly reject the (correct) password while specialize applies
                        // it. Within the grace window this must be retried, not treated as a permanent failure.
                        return Task.FromResult(new GuestCommandResult
                        {
                            Success = false,
                            Error = "Hyper-V\\Invoke-Command : The credential is invalid."
                        });
                    }
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.True(probeAttempts >= 3, $"Expected the transport gate to retry past the early rejections, saw {probeAttempts} attempt(s).");
        Assert.Contains("vm:vm-dc01:GuestTransportReady", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_BaseRemoteAccessReady_ProbeTransientFailure_RetriesThenSucceeds()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 3;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Get-NetTCPConnection", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    if (attempt == 1)
                    {
                        return Task.FromResult(new GuestCommandResult { Success = false, Error = "RDP listener not yet bound." });
                    }
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.True(probeAttempts >= 2, $"Expected the gate to retry the probe, saw {probeAttempts} attempt(s).");
        Assert.Contains("vm:vm-dc01:BaseRemoteAccessReady", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_BaseRemoteAccessReady_ProbeReady_CompletesGate()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);

        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("vm:vm-dc01:BaseRemoteAccessReady", result.ExecutedNodeIds);
        Assert.Contains(
            scripts,
            entry => entry.VmName == "dc01" &&
                     entry.Script.Contains("Get-NetTCPConnection", StringComparison.Ordinal) &&
                     entry.Script.Contains("fDenyTSConnections", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ChildDomainProgression_UsesSharedPerDomainRuntime()
    {
        var request = await CreateRuntimeRequestWithChildDomainAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("vm:vm-childdc01:PromoteFirstDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childdc01:DomainReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childreplica01:PromoteReplicaDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childdc01:StabilizeDomainDns", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childmember01:JoinDomain", result.ExecutedNodeIds);

        var childPromotionScript = scripts.First(entry =>
            entry.VmName == "childdc01" &&
            entry.Script.Contains("Install-ADDSDomain", StringComparison.Ordinal));
        Assert.Contains("-DomainType ChildDomain", childPromotionScript.Script, StringComparison.Ordinal);
        Assert.Contains("-ParentDomainName 'contoso.com'", childPromotionScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TreeDomainProgression_UsesSharedPerDomainRuntime()
    {
        var request = await CreateRuntimeRequestWithTreeDomainAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("vm:vm-childdc01:PromoteFirstDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childdc01:DomainReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childreplica01:PromoteReplicaDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childdc01:StabilizeDomainDns", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-childmember01:JoinDomain", result.ExecutedNodeIds);

        var treePromotionScript = scripts.First(entry =>
            entry.VmName == "childdc01" &&
            entry.Script.Contains("Install-ADDSDomain", StringComparison.Ordinal));
        Assert.Contains("-DomainType TreeDomain", treePromotionScript.Script, StringComparison.Ordinal);
        Assert.Contains("-NewDomainName 'fabrikam.com'", treePromotionScript.Script, StringComparison.Ordinal);
        Assert.Contains("-ParentDomainName 'contoso.com'", treePromotionScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleIndependentRootForests_ExecuteEqualRootPaths()
    {
        var request = await CreateRuntimeRequestWithAdditionalForestAsync();
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("vm:vm-dc01:PromoteFirstDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-fabrikamdc01:PromoteFirstDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:DomainReady", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-fabrikamdc01:DomainReady", result.ExecutedNodeIds);

        var fabrikamForestScript = scripts.First(entry =>
            entry.VmName == "fabrikamdc01" &&
            entry.Script.Contains("Install-ADDSForest", StringComparison.Ordinal));
        Assert.Contains("-DomainName 'fabrikam.com'", fabrikamForestScript.Script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ManagedBidirectionalForestTrust_InvokesTrustStageAfterAnchorDomainsReady()
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
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("trust:trust-contoso-fabrikam:PrepareForestTrustDns", result.ExecutedNodeIds);
        Assert.Contains("trust:trust-contoso-fabrikam:CreateForestTrust", result.ExecutedNodeIds);
        Assert.Contains("trust:trust-contoso-fabrikam:ValidateForestTrust", result.ExecutedNodeIds);
        var scriptList = scripts.ToList();
        Assert.Contains(scriptList, entry => entry.Script.Contains("CreateTrustRelationship", StringComparison.Ordinal));
        Assert.Contains(scriptList, entry => entry.Script.Contains("Forest trust validated", StringComparison.Ordinal));
    }

    private static IV2RuntimeCapabilityService CreateService(
        FakeHyperVService hyperVService,
        FakeGuestCommandExecutor guestCommandExecutor,
        IStructuredLogger? logger = null,
        IHyperVMachineAdminService? machineAdminService = null)
    {
        return new V2RuntimeCapabilityService(
            () => new FakeSession(),
            _ => hyperVService,
            guestCommandExecutor,
            new FakeCleanupOrchestrator(),
            logger,
            machineAdminService);
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestAsync(
        string profile,
        bool includeStandalone,
        bool includeRouter)
    {
        var template = CreateTemplate(profile, includeStandalone, includeRouter);
        var catalogItems = new List<VhdxCatalogItem>
        {
            CreateCatalogItem("disk-dc", "slot-local"),
            CreateCatalogItem("disk-replica", "slot-local"),
            CreateCatalogItem("disk-member", "slot-local")
        };

        if (includeStandalone)
        {
            catalogItems.Add(CreateCatalogItem("disk-standalone", "slot-local"));
        }

        if (includeRouter)
        {
            catalogItems.Add(CreateCatalogItem("disk-router", "slot-local"));
        }

        var plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = catalogItems,
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm"],
            DefaultDeploymentProfile = profile
        });

        return new V2RuntimeExecutionRequest
        {
            Template = template,
            Plan = plan,
            Settings = CreateSettings(),
            CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase)
            {
                ["slot-local"] = new() { Username = "Administrator", Password = "Password123!" },
                ["slot-join"] = new() { Username = @"LAB\JoinUser", Password = "Password123!" },
                ["slot-admin"] = new() { Username = "Administrator@contoso.com", Password = "Password123!" },
                ["slot-dsrm"] = new() { Username = "DSRM", Password = "Password123!" }
            },
            GuestTransportMaxRetries = 1,
            GuestTransportRetryDelay = TimeSpan.FromMilliseconds(10)
        };
    }

    private async Task<V2RuntimeExecutionRequest> CreateSwitchRuntimeRequestAsync(
        IReadOnlyList<LabNetworkTemplate> networks,
        IReadOnlyList<VmNetworkInterfaceTemplate> nics,
        IReadOnlyList<V2AvailableSwitchInfo> availableSwitches)
    {
        var template = new LabTemplate
        {
            Id = "template-switch-runtime",
            Name = "Switch Runtime",
            SchemaVersion = "2.0.0",
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning,
            DeploymentProfile = "Balanced",
            LabNetworks = networks.ToList(),
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = "vm-networked01",
                    Name = "networked01",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdxId = "disk-networked",
                    MembershipMode = V2MembershipModeCatalog.Standalone,
                    CredentialSlots = new VmCredentialSlotBindings
                    {
                        LocalBootstrap = "slot-local"
                    },
                    Nics = nics.ToList()
                }
            ]
        };
        var catalogItems = new[] { CreateCatalogItem("disk-networked", "slot-local") };
        var plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = catalogItems,
            AvailableSwitches = availableSwitches,
            ResolvedCredentialSlotKeys = ["slot-local"],
            DefaultDeploymentProfile = "Balanced"
        });
        Assert.True(plan.Success, string.Join(Environment.NewLine, plan.Issues.Select(issue => issue.Message)));

        return new V2RuntimeExecutionRequest
        {
            Template = template,
            Plan = plan,
            Settings = CreateSettings(),
            CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase)
            {
                ["slot-local"] = new() { Username = "Administrator", Password = "Password123!" }
            },
            GuestTransportMaxRetries = 1,
            GuestTransportRetryDelay = TimeSpan.FromMilliseconds(10)
        };
    }

    private static AppSettings CreateSettings() => new()
    {
        VmBasePath = @"C:\Labs",
        DefaultCpuCount = 2,
        DefaultVmMemoryMb = 4096,
        PerVmFailFast = true,
        StopAllOnAnyVmFailure = true
    };

    private static LabTemplate CreateTemplate(string profile, bool includeStandalone, bool includeRouter)
    {
        var template = new LabTemplate
        {
            Id = $"template-{profile.ToLowerInvariant()}",
            Name = $"Template {profile}",
            SchemaVersion = "2.0.0",
            DeploymentProfile = profile,
            DirectoryTopology = CreateDirectoryTopology("forest-contoso", "domain-contoso", "contoso.com", "CONTOSO", "vm-dc01"),
            LabNetworks =
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-core",
                    Name = "Core",
                    SwitchName = "vSwitch-Core"
                },
                new LabNetworkTemplate
                {
                    NetworkId = "lab-edge",
                    Name = "Edge",
                    SwitchName = "vSwitch-Edge"
                },
                new LabNetworkTemplate
                {
                    NetworkId = "lab-external",
                    Name = "External",
                    SwitchName = "vSwitch-External"
                }
            ]
        };

        template.VmTemplates.Add(new VmTemplate
        {
            VmId = "vm-dc01",
            Name = "dc01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-dc",
            TopologyRole = "RootDomainController",
            DomainId = "domain-contoso",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-dc",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.10",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.10"]
                }
            ]
        });

        template.VmTemplates.Add(new VmTemplate
        {
            VmId = "vm-replica01",
            Name = "replica01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-replica",
            TopologyRole = "ReplicaDomainController",
            DomainId = "domain-contoso",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-replica",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.11",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.10", "8.8.8.8"]
                }
            ]
        });

        template.VmTemplates.Add(new VmTemplate
        {
            VmId = "vm-member01",
            Name = "member01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-member",
            MembershipMode = V2MembershipModeCatalog.DomainMember,
            DomainId = "domain-contoso",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-admin",
                DomainJoin = "slot-join"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-member",
                    NetworkId = includeRouter ? "lab-edge" : "lab-core",
                    IpAddress = includeRouter ? "10.0.1.20" : "10.0.0.20",
                    PrefixLength = 24,
                    DefaultGateway = includeRouter ? "10.0.1.1" : "10.0.0.1",
                    DnsServers = ["10.0.0.10"]
                }
            ]
        });

        if (includeStandalone)
        {
            template.VmTemplates.Add(new VmTemplate
            {
                VmId = "vm-standalone01",
                Name = "standalone01",
                MemoryMb = 2048,
                CpuCount = 2,
                VhdxId = "disk-standalone",
                MembershipMode = V2MembershipModeCatalog.Standalone,
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-standalone",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.30",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            });
        }

        if (includeRouter)
        {
            template.VmTemplates.Add(new VmTemplate
            {
                VmId = "vm-router01",
                Name = "router01",
                MemoryMb = 2048,
                CpuCount = 2,
                VhdxId = "disk-router",
                TopologyRole = "Router",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-router-core",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.1",
                        PrefixLength = 24
                    },
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-router-edge",
                        NetworkId = "lab-external"
                    }
                ]
            });
        }

        return template;
    }

    private static VhdxCatalogItem CreateCatalogItem(string id, string slotRef)
    {
        return new VhdxCatalogItem
        {
            Id = id,
            Path = $@"C:\BaseDisks\{id}.vhdx",
            OsName = "Windows Server",
            OsVersion = "2022",
            Generation = 2,
            Signature = $"{id}-sig",
            BootstrapProfile = new VhdxBootstrapProfile
            {
                ExpectedLocalUser = "Administrator",
                LocalCredentialSlotRef = slotRef,
                GuestTransport = "powershell-direct"
            }
        };
    }

    private static V2DirectoryTopologyTemplate CreateDirectoryTopology(
        string forestId,
        string domainId,
        string dnsName,
        string netBiosName,
        string firstDomainControllerVmId)
    {
        return new V2DirectoryTopologyTemplate
        {
            Forests =
            [
                new V2ForestTemplate
                {
                    ForestId = forestId,
                    RootDomainId = domainId
                }
            ],
            Domains =
            [
                new V2DomainTemplate
                {
                    DomainId = domainId,
                    DnsName = dnsName,
                    NetBiosName = netBiosName,
                    ForestId = forestId,
                    RelationKind = V2DomainRelationKind.Root,
                    FirstDomainControllerVmId = firstDomainControllerVmId
                }
            ]
        };
    }

    private static async Task WaitForOperationAsync(ConcurrentQueue<string> operations, string expected)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (operations.Contains(expected))
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException($"Operation '{expected}' was not observed.");
    }

    private sealed class FakeGuestCommandExecutor : IGuestCommandExecutor
    {
        public Func<string, V2RuntimeCredential, string, CancellationToken, Task<GuestCommandResult>>? OnExecuteAsync { get; set; }

        public Task<GuestCommandResult> ExecutePowerShellDirectAsync(string vmName, V2RuntimeCredential credential, string script, CancellationToken cancellationToken = default)
        {
            if (OnExecuteAsync != null)
            {
                return OnExecuteAsync(vmName, credential, script, cancellationToken);
            }

            return Task.FromResult(new GuestCommandResult
            {
                Success = true,
                Output = vmName
            });
        }
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        public ConcurrentQueue<string> Operations { get; } = new();

        public ConcurrentQueue<string>? SharedOperations { get; init; }

        public bool CreateVmResult { get; init; } = true;

        public Func<string, Task>? OnCreateVmAsync { get; set; }

        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount)
        {
            RecordOperation($"CreateVm:{vmName}");
            return InvokeAsync(OnCreateVmAsync, vmName, CreateVmResult);
        }

        public Task<bool> EnableGuestServicesAsync(string vmName)
        {
            RecordOperation($"EnableGuestServices:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> StartVmAsync(string vmName)
        {
            RecordOperation($"StartVm:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> StopVmAsync(string vmName)
        {
            RecordOperation($"StopVm:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(true);

        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(true);

        public Task<bool> RemoveVmAsync(string vmName)
        {
            RecordOperation($"RemoveVm:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath)
        {
            RecordOperation($"CreateVhd:{Path.GetFileNameWithoutExtension(vhdPath)}");
            return Task.FromResult(true);
        }

        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);

        public Task<bool> DisableVmCheckpointsAsync(string vmName)
        {
            RecordOperation($"DisableCheckpoints:{vmName}");
            return Task.FromResult(true);
        }

        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string> { "vSwitch-Core", "vSwitch-Edge", "vSwitch-External" });

        public Task<IReadOnlyList<HyperVVmNetworkAdapterInfo>> GetVmNetworkAdaptersAsync(string vmName)
        {
            IReadOnlyList<HyperVVmNetworkAdapterInfo> adapters = vmName switch
            {
                "router01" =>
                [
                    new HyperVVmNetworkAdapterInfo { AdapterName = "core", SwitchName = "vSwitch-Core", MacAddress = "00155D000001" },
                    new HyperVVmNetworkAdapterInfo { AdapterName = "external", SwitchName = "vSwitch-External", MacAddress = "00155D000002" }
                ],
                _ =>
                [
                    new HyperVVmNetworkAdapterInfo { AdapterName = "primary", SwitchName = "vSwitch-Core", MacAddress = "00155D000010" }
                ]
            };

            return Task.FromResult(adapters);
        }

        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName)
        {
            RecordOperation($"AddSwitch:{vmName}:{switchName}");
            return Task.FromResult(true);
        }

        private void RecordOperation(string operation)
        {
            Operations.Enqueue(operation);
            SharedOperations?.Enqueue(operation);
        }

        private static async Task<bool> InvokeAsync(Func<string, Task>? callback, string vmName, bool result)
        {
            if (callback != null)
            {
                await callback(vmName);
            }

            return result;
        }
    }

    private sealed class FakeHyperVMachineAdminService : IHyperVMachineAdminService
    {
        private readonly ConcurrentQueue<string> _sharedOperations;

        public FakeHyperVMachineAdminService(ConcurrentQueue<string> sharedOperations)
        {
            _sharedOperations = sharedOperations;
        }

        public List<HyperVVirtualSwitchInfo> Switches { get; set; } = [];

        public List<HyperVVirtualSwitchCreateRequest> CreatedSwitches { get; } = [];

        public List<string> DeletedSwitches { get; } = [];

        public Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync() =>
            Task.FromResult<IReadOnlyList<HyperVHostMachineVmInfo>>(Array.Empty<HyperVHostMachineVmInfo>());

        public Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName) =>
            Task.FromResult<HyperVMachineEditSnapshot?>(null);

        public Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync() =>
            Task.FromResult<IReadOnlyList<string>>(Switches.Select(item => item.Name).ToArray());

        public Task<IReadOnlyList<HyperVVirtualSwitchInfo>> ListVirtualSwitchesAsync()
        {
            _sharedOperations.Enqueue("ListSwitches");
            return Task.FromResult<IReadOnlyList<HyperVVirtualSwitchInfo>>(Switches.ToArray());
        }

        public Task<IReadOnlyList<string>> GetAttachedVmNamesForSwitchAsync(string switchName) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<HyperVMachineActionResult> CreateVirtualSwitchAsync(HyperVVirtualSwitchCreateRequest request)
        {
            _sharedOperations.Enqueue($"CreateSwitch:{request.Name}");
            CreatedSwitches.Add(request);
            Switches.Add(new HyperVVirtualSwitchInfo
            {
                Name = request.Name,
                SwitchType = request.SwitchType,
                AdapterName = request.AdapterName
            });
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }

        public Task<HyperVMachineActionResult> RenameVirtualSwitchAsync(string currentName, string newName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> DeleteVirtualSwitchAsync(string switchName)
        {
            _sharedOperations.Enqueue($"DeleteSwitch:{switchName}");
            DeletedSwitches.Add(switchName);
            Switches.RemoveAll(item => string.Equals(item.Name, switchName, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }

        public Task<HyperVMachineActionResult> StartVmAsync(string vmName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> StopVmAsync(string vmName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> RestartVmAsync(string vmName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(
            string vmName,
            IReadOnlyCollection<string> knownBaseDiskPaths,
            string? differencingDiskBasePath) =>
            Task.FromResult<IReadOnlyList<HyperVMachineDiskClassificationResult>>(Array.Empty<HyperVMachineDiskClassificationResult>());

        public Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });
    }

    private sealed class FakeCleanupOrchestrator : IVmCleanupOrchestrator
    {
        public Task<VmCleanupResult> CleanupAsync(VmDeploymentContext context, IHyperVService hyperVService)
        {
            return Task.FromResult(new VmCleanupResult { VmName = context.VmName });
        }
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public void Dispose()
        {
        }

        public Task<(string Output, string Error)> ExecuteAsync(string command) =>
            Task.FromResult<(string Output, string Error)>((string.Empty, string.Empty));
    }

    private static V2AvailableSwitchInfo CreateSwitch(string name, string switchType)
        => new()
        {
            Name = name,
            SwitchType = switchType
        };

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public ConcurrentQueue<StructuredLogEvent> Events { get; } = new();

        public void Log(StructuredLogEvent logEvent)
        {
            Events.Enqueue(logEvent);
        }

        public void Log(
            StructuredLogLevel level,
            string eventName,
            string operationId,
            string? result = null,
            IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Enqueue(StructuredLogEvent.Create(level, eventName, operationId, result, context));
        }
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestWithChildDomainAsync()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: false);
        request.Template.VmTemplates.AddRange(
        [
            new VmTemplate
            {
                VmId = "vm-childdc01",
                Name = "childdc01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-childdc",
                TopologyRole = "FirstDomainController",
                DomainId = "domain-child",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-child-admin",
                    Dsrm = "slot-dsrm",
                    ParentDomainAdmin = "slot-parent-admin"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-childdc",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.30",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.10"]
                    }
                ]
            },
            new VmTemplate
            {
                VmId = "vm-childreplica01",
                Name = "childreplica01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-childreplica",
                TopologyRole = "ReplicaDomainController",
                DomainId = "domain-child",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-child-admin",
                    Dsrm = "slot-dsrm"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-childreplica",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.31",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.30", "10.0.0.10"]
                    }
                ]
            },
            new VmTemplate
            {
                VmId = "vm-childmember01",
                Name = "childmember01",
                MemoryMb = 4096,
                CpuCount = 2,
                VhdxId = "disk-childmember",
                MembershipMode = V2MembershipModeCatalog.DomainMember,
                DomainId = "domain-child",
                CredentialSlots = new VmCredentialSlotBindings
                {
                    LocalBootstrap = "slot-local",
                    DomainAdmin = "slot-child-admin",
                    DomainJoin = "slot-join"
                },
                Nics =
                [
                    new VmNetworkInterfaceTemplate
                    {
                        NicId = "nic-childmember",
                        NetworkId = "lab-core",
                        IpAddress = "10.0.0.40",
                        PrefixLength = 24,
                        DefaultGateway = "10.0.0.1",
                        DnsServers = ["10.0.0.30", "10.0.0.10"]
                    }
                ]
            }
        ]);

        request.Template.DirectoryTopology!.Domains!.Add(new V2DomainTemplate
        {
            DomainId = "domain-child",
            DnsName = "child.contoso.com",
            NetBiosName = "CHILD",
            ForestId = "forest-contoso",
            RelationKind = V2DomainRelationKind.Child,
            ParentDomainId = "domain-contoso",
            FirstDomainControllerVmId = "vm-childdc01"
        });

        var catalogItems = new List<VhdxCatalogItem>
        {
            CreateCatalogItem("disk-dc", "slot-local"),
            CreateCatalogItem("disk-replica", "slot-local"),
            CreateCatalogItem("disk-member", "slot-local"),
            CreateCatalogItem("disk-childdc", "slot-local"),
            CreateCatalogItem("disk-childreplica", "slot-local"),
            CreateCatalogItem("disk-childmember", "slot-local")
        };

        request.Plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = request.Template,
            CatalogItems = catalogItems,
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-child-admin", "slot-parent-admin"],
            DefaultDeploymentProfile = "Balanced"
        });

        request.CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase)
        {
            ["slot-local"] = new() { Username = "Administrator", Password = "Password123!" },
            ["slot-join"] = new() { Username = @"LAB\JoinUser", Password = "Password123!" },
            ["slot-admin"] = new() { Username = "Administrator@contoso.com", Password = "Password123!" },
            ["slot-dsrm"] = new() { Username = "DSRM", Password = "Password123!" },
            ["slot-child-admin"] = new() { Username = "Administrator@child.contoso.com", Password = "Password123!" },
            ["slot-parent-admin"] = new() { Username = "Administrator@contoso.com", Password = "Password123!" }
        };

        return request;
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestWithTreeDomainAsync()
    {
        var request = await CreateRuntimeRequestWithChildDomainAsync();
        var treeDomain = Assert.Single(request.Template.DirectoryTopology!.Domains!.Where(domain => domain.DomainId == "domain-child"));
        treeDomain.RelationKind = V2DomainRelationKind.Tree;
        treeDomain.DnsName = "fabrikam.com";
        treeDomain.NetBiosName = "FABRIKAM";

        request.Plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = request.Template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-replica", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local"),
                CreateCatalogItem("disk-childdc", "slot-local"),
                CreateCatalogItem("disk-childreplica", "slot-local"),
                CreateCatalogItem("disk-childmember", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-child-admin", "slot-parent-admin"],
            DefaultDeploymentProfile = "Balanced"
        });

        request.CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(request.CredentialSlotValues, StringComparer.OrdinalIgnoreCase)
        {
            ["slot-child-admin"] = new() { Username = "Administrator@fabrikam.com", Password = "Password123!" }
        };
        return request;
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestWithAdditionalForestAsync()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: false);
        request.Template.VmTemplates.Add(new VmTemplate
        {
            VmId = "vm-fabrikamdc01",
            Name = "fabrikamdc01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-fabrikamdc",
            TopologyRole = "FirstDomainController",
            DomainId = "domain-fabrikam",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-fabrikam-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-fabrikamdc",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.50",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.50"]
                }
            ]
        });
        request.Template.DirectoryTopology!.Forests!.Add(new V2ForestTemplate
        {
            ForestId = "forest-fabrikam",
            RootDomainId = "domain-fabrikam"
        });
        request.Template.DirectoryTopology.Domains!.Add(new V2DomainTemplate
        {
            DomainId = "domain-fabrikam",
            DnsName = "fabrikam.com",
            NetBiosName = "FABRIKAM",
            ForestId = "forest-fabrikam",
            RelationKind = V2DomainRelationKind.Root,
            FirstDomainControllerVmId = "vm-fabrikamdc01"
        });

        request.Plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = request.Template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-replica", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local"),
                CreateCatalogItem("disk-fabrikamdc", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"],
            DefaultDeploymentProfile = "Balanced"
        });

        request.CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(request.CredentialSlotValues, StringComparer.OrdinalIgnoreCase)
        {
            ["slot-fabrikam-admin"] = new()
            {
                Username = "Administrator@fabrikam.com",
                Password = "Password123!"
            }
        };

        return request;
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestWithForestTrustAsync()
    {
        var request = await CreateRuntimeRequestWithAdditionalForestAsync();
        request.Template.DirectoryTopology!.Trusts =
        [
            new V2TrustTemplate
            {
                TrustId = "trust-contoso-fabrikam",
                SourceDomainId = "domain-contoso",
                TargetDomainId = "domain-fabrikam",
                TrustType = V2TrustType.Forest,
                Direction = V2TrustDirection.Bidirectional
            }
        ];

        request.Plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = request.Template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-replica", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local"),
                CreateCatalogItem("disk-fabrikamdc", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"],
            DefaultDeploymentProfile = "Balanced"
        });

        return request;
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestWithTwoForestTrustsAsync()
    {
        var request = await CreateRuntimeRequestWithAdditionalForestAsync();
        request.Template.DirectoryTopology!.Trusts =
        [
            new V2TrustTemplate
            {
                TrustId = "trust-a-contoso-fabrikam",
                SourceDomainId = "domain-contoso",
                TargetDomainId = "domain-fabrikam",
                TrustType = V2TrustType.Forest,
                Direction = V2TrustDirection.Bidirectional
            },
            new V2TrustTemplate
            {
                TrustId = "trust-b-contoso-fabrikam",
                SourceDomainId = "domain-contoso",
                TargetDomainId = "domain-fabrikam",
                TrustType = V2TrustType.Forest,
                Direction = V2TrustDirection.Bidirectional
            }
        ];

        request.Plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = request.Template,
            CatalogItems =
            [
                CreateCatalogItem("disk-dc", "slot-local"),
                CreateCatalogItem("disk-replica", "slot-local"),
                CreateCatalogItem("disk-member", "slot-local"),
                CreateCatalogItem("disk-fabrikamdc", "slot-local")
            ],
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", "vSwitch-External"],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch("vSwitch-External", "External")
            ],
            ResolvedCredentialSlotKeys = ["slot-local", "slot-join", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"],
            DefaultDeploymentProfile = "Balanced"
        });

        return request;
    }
}

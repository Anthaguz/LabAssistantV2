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
    public async Task ExecuteAsync_RouterExternalOnDefaultSwitch_ClassifiesNatSwitchAsExternalAndCompletesRouterTail()
    {
        // The router WAN lands on Hyper-V's Default Switch, a NAT switch reported as Internal. The external
        // classification must accept it as the egress attachment so the whole router tail runs to success
        // instead of failing the "no external switch attachment" gate the way a strict type == External check did.
        var request = await CreateRuntimeRequestAsync(
            "Balanced",
            includeStandalone: false,
            includeRouter: true,
            routerExternalOnDefaultSwitch: true);
        var hyperV = new FakeHyperVService
        {
            AdapterOverride = vmName => vmName == "router01"
                ?
                [
                    new HyperVVmNetworkAdapterInfo { AdapterName = "core", SwitchName = "vSwitch-Core", MacAddress = "00155D000001" },
                    new HyperVVmNetworkAdapterInfo { AdapterName = "external", SwitchName = "Default Switch", MacAddress = "00155D000002" }
                ]
                : null
        };
        var service = CreateService(hyperV, new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success, string.Join(" | ", result.DeploymentContext.VmContexts
            .Where(c => !c.IsSuccess)
            .Select(c => $"{c.VmName}:{c.FailureStepKey}:{c.FailureMessage}")));
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:EnableRouterRouting");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:ConfigureRouterNat");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:ValidateRouterEgress");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:RouterReady");
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
        // Time-based grace: each transport probe advances the fake clock by 1000 ms, so a one-second window tolerates
        // the first rejection (elapsed 0) and trips on the second (elapsed 1000 ms), independent of attempt cadence.
        request.GuestAuthGraceWindow = TimeSpan.FromMilliseconds(1000);

        long clock = 0;
        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref probeAttempts);
                    Interlocked.Add(ref clock, 1000);
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
        var service = CreateService(new FakeHyperVService(), guestExecutor, logger, nowTicks: () => Interlocked.Read(ref clock));

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);

        // The grace window tolerates the first rejection (specialize may still be applying the password), then the
        // loop fails fast once the unbroken rejection streak exceeds the window instead of grinding through all 25
        // retries.
        Assert.Equal(2, probeAttempts);

        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("does not match this VM's base image", StringComparison.Ordinal));

        Assert.Contains(
            logger.Events,
            e => e.Code == $"0x{LabAssistant.Services.Diagnostics.LaStatus.DeployGuest_ReadinessAttemptCredentialRejected:X8}" &&
                 e.Context != null &&
                 e.Context.TryGetValue("errorCategory", out var category) &&
                 (category as string) == "AuthenticationRejected");
    }

    [Fact]
    public async Task ExecuteAsync_PrepareGuestNetwork_GuestRebootMidHop_RetriesThenSucceeds()
    {
        // The guest can reboot out of its specialize/OOBE window right as prepareGuestNetwork runs, tearing down
        // the PowerShell Direct hop ("socket target process has ended"). That torn-down transport must be retried,
        // not treated as a fatal deploy failure, because the fresh hop reconnects once the guest is back.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var prepareAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Guest network prepared", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref prepareAttempts);
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
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, prepareAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_PrepareGuestNetwork_NonRebootError_FailsFastWithoutRetrying()
    {
        // A genuine in-guest script error (not a torn-down transport) is deterministic, so the step must surface it
        // immediately instead of burning the whole transport retry budget re-running a hop that will keep failing.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var prepareAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Guest network prepared", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref prepareAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "New-NetIPAddress : Instance MSFT_NetIPAddress already exists."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(1, prepareAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("Failed to prepare guest network", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_InstallAdDomainServices_GuestRebootMidHop_RetriesThenSucceeds()
    {
        // installAdDomainServices runs over PowerShell Direct while a freshly bootstrapped, loaded host can tear the
        // socket down mid-command ("target process has ended" -> GuestRebooting). That was the live 050926 failure:
        // the single-shot install turned a transient transport drop into a hard deploy failure + full rollback. The
        // feature-install script is idempotent, so the drop must be retried instead.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var installAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("AD-Domain-Services", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref installAttempts);
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
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, installAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_InstallAdDomainServices_NonRebootError_FailsFastWithoutRetrying()
    {
        // A genuine in-guest install error (not a torn-down transport) is deterministic, so the step must surface it
        // immediately and fail the deploy instead of re-running an install that will keep failing. This proves the
        // new transport-drop retry did not turn into a blanket retry of real failures.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var installAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("AD-Domain-Services", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref installAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "Install-WindowsFeature : The request to add or remove features on the specified server failed."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(1, installAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("Failed to install AD DS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_StabilizeDomainDns_GuestRebootMidHop_RetriesThenSucceeds()
    {
        // stabilizeDomainDns is idempotent (it re-applies the DNS client server list), so a torn-down PowerShell
        // Direct hop must be retried rather than failing the whole deploy - the same zero-tolerance gap class as
        // installAdDomainServices.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var stabilizeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Domain DNS stabilized", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref stabilizeAttempts);
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
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, stabilizeAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_StabilizeDomainDns_NonRebootError_FailsFastWithoutRetrying()
    {
        // A deterministic in-guest DNS error must fail fast and surface, not burn the transport retry budget.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var stabilizeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("Domain DNS stabilized", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref stabilizeAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "Set-DnsClientServerAddress : Invalid parameter InterfaceIndex."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(1, stabilizeAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("Failed to stabilize domain DNS", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ConfigureRouterNat_GuestRebootMidHop_RetriesThenSucceeds()
    {
        // The router configuration steps run over PowerShell Direct and share the same transport-drop gap the AD DS
        // steps had: a loaded host can tear the socket down mid-command ("target process has ended" -> GuestRebooting).
        // The NAT script is idempotent (netsh delete-then-add with IgnoreMissing), so the drop must be retried through
        // the shared GuestStepTransportRetry helper rather than failing the whole router deploy.
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: true);
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var natAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "router01" && script.Contains("Router NAT configured", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref natAttempts);
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
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(2, natAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_ConfigureRouterNat_NonRebootError_FailsFastWithoutRetrying()
    {
        // A deterministic in-guest router error (not a torn-down transport) must surface immediately and fail the
        // deploy, proving the new transport-drop retry did not become a blanket retry of real router failures.
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: true);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);

        var natAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "router01" && script.Contains("Router NAT configured", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref natAttempts);
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "netsh : The requested operation requires elevation."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Equal(1, natAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "router01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("Failed to configure router NAT", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_GuestStabilization_RequiresConsecutiveStableProbes_DroppedHopResetsStreak()
    {
        // The stabilization gate must not accept a lone stable probe: a dropped hop (a reboot already in flight)
        // resets the streak, so the guest has to prove it is settled across N consecutive probes before any
        // mutating step runs.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestStabilizationRequiredStableProbes = 3;
        request.GuestStabilizationProbeInterval = TimeSpan.FromMilliseconds(1);
        request.GuestStabilizationTimeout = TimeSpan.FromSeconds(30);

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnStabilizationProbeAsync = (vmName, _) =>
            {
                if (vmName != "dc01")
                {
                    return Task.FromResult(new GuestCommandResult { Success = true, Output = "STABLE" });
                }

                var attempt = Interlocked.Increment(ref probeAttempts);
                // STABLE, STABLE, dropped hop (reset), STABLE, STABLE, STABLE -> passes on the sixth probe.
                if (attempt == 3)
                {
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "The Hyper-V socket target process has ended."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = "STABLE" });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Equal(6, probeAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_GuestStabilization_TimeoutProceedsAnywayWithoutFailing()
    {
        // If the guest never reports a clean stable streak within the cap, the gate proceeds anyway and lets the
        // bounded retry on the mutating steps be the backstop. It must never fail the deploy on its own.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestStabilizationRequiredStableProbes = 3;
        request.GuestStabilizationProbeInterval = TimeSpan.FromMilliseconds(1);
        request.GuestStabilizationTimeout = TimeSpan.FromMilliseconds(80);

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnStabilizationProbeAsync = (vmName, _) =>
            {
                if (vmName != "dc01")
                {
                    return Task.FromResult(new GuestCommandResult { Success = true, Output = "STABLE" });
                }

                Interlocked.Increment(ref probeAttempts);
                return Task.FromResult(new GuestCommandResult { Success = true, Output = "PENDING" });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        // The gate never saw a STABLE probe (all PENDING), so a successful deploy proves it proceeded on the
        // timeout instead of failing. The probe count only has to show the gate actually ran.
        Assert.True(result.Success);
        Assert.True(probeAttempts >= 1);
    }

    [Fact]
    public async Task ExecuteAsync_PrepareGuestNetwork_MultiNic_BindsEachIpToItsSwitchAdapterByMac()
    {
        // Multi-NIC correctness: each static IP must bind to the adapter on its intended switch, matched by MAC,
        // not by enumeration order. Two NICs on two switches must pair each IP with that switch's adapter MAC.
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var request = await CreateSwitchRuntimeRequestAsync(
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-alpha",
                    Name = "Alpha",
                    SwitchName = "vSwitch-Alpha",
                    SwitchType = V2SwitchTypeCatalog.Internal
                },
                new LabNetworkTemplate
                {
                    NetworkId = "lab-beta",
                    Name = "Beta",
                    SwitchName = "vSwitch-Beta",
                    SwitchType = V2SwitchTypeCatalog.Internal
                }
            ],
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-alpha",
                    NetworkId = "lab-alpha",
                    IpAddress = "10.9.9.10",
                    PrefixLength = 24
                },
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-beta",
                    NetworkId = "lab-beta",
                    IpAddress = "10.9.8.10",
                    PrefixLength = 24
                }
            ],
            []);
        var machineAdmin = new FakeHyperVMachineAdminService(new ConcurrentQueue<string>());
        var service = CreateService(new FakeHyperVService(), guestExecutor, machineAdminService: machineAdmin);

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        var prepareScript = scripts
            .First(entry => entry.VmName == "networked01" &&
                            entry.Script.Contains("Guest network prepared", StringComparison.Ordinal))
            .Script;

        // The fake host assigns MACs per attached switch in sorted order: vSwitch-Alpha -> 00155D000001,
        // vSwitch-Beta -> 00155D000002. Each NIC line must pair its IP with its own switch's adapter MAC.
        var alphaLine = prepareScript
            .Split('\n')
            .First(line => line.Contains("10.9.9.10", StringComparison.Ordinal));
        var betaLine = prepareScript
            .Split('\n')
            .First(line => line.Contains("10.9.8.10", StringComparison.Ordinal));
        Assert.Contains("00155D000001", alphaLine, StringComparison.Ordinal);
        Assert.Contains("00155D000002", betaLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PrepareGuestNetwork_DhcpNicWithUnresolvableAdapter_SkipsInsteadOfFailing()
    {
        // A NIC with no static IP is a DHCP/egress NIC: it has nothing to bind, so an unresolvable host adapter
        // must not fail the whole guest-network step. Here the second NIC's switch has no host adapter at all;
        // the deploy must still succeed and simply omit that NIC from the in-guest script.
        var scripts = new ConcurrentQueue<(string VmName, string Script)>();
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                scripts.Enqueue((vmName, script));
                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var request = await CreateSwitchRuntimeRequestAsync(
            [
                new LabNetworkTemplate
                {
                    NetworkId = "lab-alpha",
                    Name = "Alpha",
                    SwitchName = "vSwitch-Alpha",
                    SwitchType = V2SwitchTypeCatalog.Internal
                },
                new LabNetworkTemplate
                {
                    NetworkId = "lab-beta",
                    Name = "Beta",
                    SwitchName = "vSwitch-Beta",
                    SwitchType = V2SwitchTypeCatalog.Internal
                }
            ],
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-alpha",
                    NetworkId = "lab-alpha",
                    IpAddress = "10.9.9.10",
                    PrefixLength = 24
                },
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-beta",
                    NetworkId = "lab-beta"
                }
            ],
            []);
        // Only the alpha switch has a host adapter; the beta (DHCP) NIC has none and no leftover to claim.
        var hyperV = new FakeHyperVService
        {
            AdapterOverride = vmName => vmName == "networked01"
                ?
                [
                    new HyperVVmNetworkAdapterInfo { AdapterName = "alpha", SwitchName = "vSwitch-Alpha", MacAddress = "00155DAAAA01" }
                ]
                : null
        };
        var machineAdmin = new FakeHyperVMachineAdminService(new ConcurrentQueue<string>());
        var service = CreateService(hyperV, guestExecutor, machineAdminService: machineAdmin);

        var result = await service.ExecuteAsync(request);

        Assert.True(
            result.Success,
            string.Join(" | ", result.DeploymentContext.VmContexts
                .Where(c => !c.IsSuccess)
                .Select(c => $"{c.VmName}:{c.FailureStepKey}:{c.FailureMessage}")));
        var prepareScript = scripts
            .First(entry => entry.VmName == "networked01" &&
                            entry.Script.Contains("Guest network prepared", StringComparison.Ordinal))
            .Script;
        Assert.Contains("nic-alpha", prepareScript, StringComparison.Ordinal);
        Assert.DoesNotContain("nic-beta", prepareScript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RouterExternalAdapterReportsEmptySwitchName_ResolvesViaLeftoverAndSucceeds()
    {
        // The Default Switch / NAT egress adapter is physically attached but frequently reports an empty switch
        // name at query time. A strict switch-name-only match dropped it and failed prepareRouterNetwork; the
        // leftover-adapter fallback must now bind it so routing (which needs the external MAC) still proceeds.
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: true);
        var hyperV = new FakeHyperVService
        {
            AdapterOverride = vmName => vmName == "router01"
                ?
                [
                    new HyperVVmNetworkAdapterInfo { AdapterName = "core", SwitchName = "vSwitch-Core", MacAddress = "00155D000001" },
                    new HyperVVmNetworkAdapterInfo { AdapterName = "external", SwitchName = string.Empty, MacAddress = "00155D000002" }
                ]
                : null
        };
        var service = CreateService(hyperV, new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:PrepareRouterNetwork");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:EnableRouterRouting");
        Assert.Contains(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:RouterReady");
    }

    [Fact]
    public void CorrelateAdaptersInOrder_UnmatchedNicClaimsEmptySwitchNameAdapterAsLeftover()
    {
        // "Default Switch" has no like-named adapter, so it falls to the leftover pass and claims the egress
        // adapter that reported an empty switch name; the named switch still matches its own adapter directly.
        var adapters = new List<HyperVVmNetworkAdapterInfo>
        {
            new() { AdapterName = "core", SwitchName = "vSwitch-Core", MacAddress = "MACCORE" },
            new() { AdapterName = "egress", SwitchName = string.Empty, MacAddress = "MACEGRESS" }
        };

        var result = V2RuntimeCapabilityService.CorrelateAdaptersInOrder(["Default Switch", "vSwitch-Core"], adapters);

        Assert.Equal("MACEGRESS", result[0]!.MacAddress);
        Assert.Equal("core", result[1]!.AdapterName);
    }

    [Fact]
    public void CorrelateAdaptersInOrder_MultipleNicsOnSameSwitch_ConsumeDistinctAdaptersInOrder()
    {
        var adapters = new List<HyperVVmNetworkAdapterInfo>
        {
            new() { AdapterName = "first", SwitchName = "vSwitch-Core", MacAddress = "MAC1" },
            new() { AdapterName = "second", SwitchName = "vSwitch-Core", MacAddress = "MAC2" }
        };

        var result = V2RuntimeCapabilityService.CorrelateAdaptersInOrder(["vSwitch-Core", "vSwitch-Core"], adapters);

        Assert.Equal("MAC1", result[0]!.MacAddress);
        Assert.Equal("MAC2", result[1]!.MacAddress);
    }

    [Fact]
    public void CorrelateAdaptersInOrder_FewerAdaptersThanNics_LeavesTrailingEntryNull()
    {
        var adapters = new List<HyperVVmNetworkAdapterInfo>
        {
            new() { AdapterName = "a", SwitchName = "A", MacAddress = "MACA" },
            new() { AdapterName = "b", SwitchName = "B", MacAddress = "MACB" }
        };

        var result = V2RuntimeCapabilityService.CorrelateAdaptersInOrder(["A", "B", "C"], adapters);

        Assert.Equal("a", result[0]!.AdapterName);
        Assert.Equal("b", result[1]!.AdapterName);
        Assert.Null(result[2]);
    }

    [Fact]
    public void CorrelateAdaptersInOrder_MultipleAdaptersReportEmptySwitchName_LeavesAmbiguousNicsNullInsteadOfGuessing()
    {
        // Two adapters both report an empty/dynamic switch name and two NICs are unmatched, so the pairing is
        // ambiguous. Guessing positionally could bind a static IP or a router egress MAC to the wrong adapter, so
        // correlation must leave both NICs null and let the caller fail loudly or skip, rather than mis-bind.
        var adapters = new List<HyperVVmNetworkAdapterInfo>
        {
            new() { AdapterName = "x", SwitchName = string.Empty, MacAddress = "MACX" },
            new() { AdapterName = "y", SwitchName = null, MacAddress = "MACY" }
        };

        var result = V2RuntimeCapabilityService.CorrelateAdaptersInOrder(["First", "Second"], adapters);

        Assert.Null(result[0]);
        Assert.Null(result[1]);
    }

    [Fact]
    public void CorrelateAdaptersInOrder_TwoEmptySwitchNameAdaptersWithOneNamedMatch_DoesNotGuessRemainingPair()
    {
        // One NIC matches its named adapter; the other two NICs and two empty-switch-name adapters remain ambiguous,
        // so only the named match resolves and the ambiguous pair stays null (no positional guess).
        var adapters = new List<HyperVVmNetworkAdapterInfo>
        {
            new() { AdapterName = "core", SwitchName = "vSwitch-Core", MacAddress = "MACCORE" },
            new() { AdapterName = "egressA", SwitchName = string.Empty, MacAddress = "MACA" },
            new() { AdapterName = "egressB", SwitchName = string.Empty, MacAddress = "MACB" }
        };

        var result = V2RuntimeCapabilityService.CorrelateAdaptersInOrder(
            ["vSwitch-Core", "vSwitch-Alpha", "vSwitch-Beta"],
            adapters);

        Assert.Equal("core", result[0]!.AdapterName);
        Assert.Null(result[1]);
        Assert.Null(result[2]);
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_TransientCredentialErrorWithinGrace_RetriesThenSucceeds()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        // Each probe advances the fake clock 1000 ms; a 10 s window tolerates the short specialize-time rejection run.
        request.GuestAuthGraceWindow = TimeSpan.FromMilliseconds(10_000);
        long clock = 0;

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    Interlocked.Add(ref clock, 1000);
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
        var service = CreateService(new FakeHyperVService(), guestExecutor, nowTicks: () => Interlocked.Read(ref clock));

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.True(probeAttempts >= 3, $"Expected the transport gate to retry past the early rejections, saw {probeAttempts} attempt(s).");
        Assert.Contains("vm:vm-dc01:GuestTransportReady", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_PromptSuppliesGoodCredential_RetriesInPlaceAndSucceeds()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 5;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        // A zero window means the very first rejection is past grace (no specialize tolerance), so the prompt is
        // offered immediately; this isolates the retry-in-place-with-corrected-credential behavior.
        request.GuestAuthGraceWindow = TimeSpan.Zero;
        var promptCount = 0;
        request.DeploymentContext = new MultiVmDeploymentContext
        {
            VmContextRegistered = ctx => ctx.RequestGuestCredential = (_, _) =>
            {
                Interlocked.Increment(ref promptCount);
                return Task.FromResult(new GuestCredentialPromptResponse
                {
                    Cancelled = false,
                    Username = "Administrator",
                    Password = "Corrected!"
                });
            }
        };

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, credential, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref probeAttempts);
                    // The stored bootstrap password is rejected; only the corrected password authenticates.
                    if (credential.Password != "Corrected!")
                    {
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
        Assert.Equal(1, promptCount);
        // The still-running VM is retried in place with the corrected credential (no re-provision).
        Assert.Contains("vm:vm-dc01:GuestTransportReady", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_PromptCancelled_FailsFast()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        // Zero window: the first rejection is immediately past grace, so the prompt is offered at attempt 1.
        request.GuestAuthGraceWindow = TimeSpan.Zero;

        request.DeploymentContext = new MultiVmDeploymentContext
        {
            VmContextRegistered = ctx => ctx.RequestGuestCredential =
                (_, _) => Task.FromResult(GuestCredentialPromptResponse.Cancel())
        };

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
        var service = CreateService(new FakeHyperVService(), guestExecutor);

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        // Zero window: the first rejection is past grace, the prompt is offered at attempt 1, and cancelling fails fast.
        Assert.Equal(1, probeAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("does not match this VM's base image", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_LeadingRebootsThenRejections_DoesNotTripGracePrematurely()
    {
        // Models the concurrent-OOBE freeze: a fresh guest reboots into specialize/OOBE (GuestRebooting) BEFORE its
        // answer-file password is applied, so the first hops are transport drops, then a short run of credential
        // rejections while specialize finishes, then success. The leading reboot hops must NOT count toward the
        // credential-rejection grace - only an unbroken run of rejections does - or the loop reprompts/fails while
        // the guest is still legitimately coming up (the exact stall seen when several guests specialize at once).
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        // Each transport probe advances the fake clock by 1000 ms; a generous 10 s window means the four-rejection
        // run (elapsed at most ~3 s from its first rejection) never reaches the grace, while the leading reboot hops
        // do not start the streak clock at all.
        request.GuestAuthGraceWindow = TimeSpan.FromMilliseconds(10_000);
        long clock = 0;

        // A re-prompt here would mean the grace tripped; wire one that fails the run so a spurious prompt is caught.
        var promptCount = 0;
        request.DeploymentContext = new MultiVmDeploymentContext
        {
            VmContextRegistered = ctx => ctx.RequestGuestCredential = (_, _) =>
            {
                Interlocked.Increment(ref promptCount);
                return Task.FromResult(GuestCredentialPromptResponse.Cancel());
            }
        };

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    Interlocked.Add(ref clock, 1000);
                    if (attempt <= 2)
                    {
                        // Guest rebooted into OOBE mid-hop - the PowerShell Direct target process went away.
                        return Task.FromResult(new GuestCommandResult
                        {
                            Success = false,
                            Error = "Hyper-V\\Invoke-Command : The Hyper-V socket target process has ended."
                        });
                    }

                    if (attempt <= 6)
                    {
                        // Specialize is still applying the (correct) answer-file password.
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
        var service = CreateService(new FakeHyperVService(), guestExecutor, nowTicks: () => Interlocked.Read(ref clock));

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        // The four-rejection run (attempts 3-6) stays under the 10 s window because the two leading reboot hops did
        // not start the streak clock; transport recovers on attempt 7 and no interactive re-prompt is offered. Under
        // the old absolute-attempt gate the reprompt would have fired at attempt 5 and cancelled the run.
        Assert.Equal(7, probeAttempts);
        Assert.Equal(0, promptCount);
        Assert.Contains("vm:vm-dc01:GuestTransportReady", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_MidWindowRebootResetsRejectionStreak_RetriesThenSucceeds()
    {
        // A reboot part-way through a rejection run means the guest made progress (it restarted into a later
        // specialize phase), so the accumulated rejection streak clock must reset. Two rejections, a reboot, then two
        // more rejections must never reach the window, even though four rejections occurred in total.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        // Each probe advances the fake clock 1000 ms; a 10 s window comfortably clears each two-rejection run
        // (elapsed ~1 s) once the reboot resets the streak clock between them.
        request.GuestAuthGraceWindow = TimeSpan.FromMilliseconds(10_000);
        long clock = 0;

        var promptCount = 0;
        request.DeploymentContext = new MultiVmDeploymentContext
        {
            VmContextRegistered = ctx => ctx.RequestGuestCredential = (_, _) =>
            {
                Interlocked.Increment(ref promptCount);
                return Task.FromResult(GuestCredentialPromptResponse.Cancel());
            }
        };

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    Interlocked.Add(ref clock, 1000);
                    if (attempt is 1 or 2 or 4 or 5)
                    {
                        return Task.FromResult(new GuestCommandResult
                        {
                            Success = false,
                            Error = "Hyper-V\\Invoke-Command : The credential is invalid."
                        });
                    }

                    if (attempt == 3)
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
        var service = CreateService(new FakeHyperVService(), guestExecutor, nowTicks: () => Interlocked.Read(ref clock));

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        // Neither the first rejection run (attempts 1-2) nor the second (attempts 4-5) reaches the window because the
        // reboot on attempt 3 reset the streak clock; transport recovers on attempt 6 with no re-prompt.
        Assert.Equal(6, probeAttempts);
        Assert.Equal(0, promptCount);
        Assert.Contains("vm:vm-dc01:GuestTransportReady", result.ExecutedNodeIds);
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_PersistentRejectionAfterReboot_StillFailsFast()
    {
        // The streak reset must not let a genuine bad password retry forever: after an initial reboot resets the
        // streak clock, an unbroken run of rejections that exceeds the window still fails fast (here once the streak
        // has run 2 s) instead of grinding through the full retry budget.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 25;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        // Each probe advances the fake clock 1000 ms. With a 2 s window the post-reboot rejection streak (starting at
        // attempt 2, elapsed 0) tolerates attempt 3 (elapsed 1 s) and trips at attempt 4 (elapsed 2 s).
        request.GuestAuthGraceWindow = TimeSpan.FromMilliseconds(2000);
        long clock = 0;

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    Interlocked.Add(ref clock, 1000);
                    if (attempt == 1)
                    {
                        return Task.FromResult(new GuestCommandResult
                        {
                            Success = false,
                            Error = "Hyper-V\\Invoke-Command : The Hyper-V socket target process has ended."
                        });
                    }

                    // The stored password is genuinely wrong: every hop past the reboot is rejected.
                    return Task.FromResult(new GuestCommandResult
                    {
                        Success = false,
                        Error = "Hyper-V\\Invoke-Command : The credential is invalid."
                    });
                }

                return Task.FromResult(new GuestCommandResult { Success = true, Output = vmName });
            }
        };
        var service = CreateService(new FakeHyperVService(), guestExecutor, nowTicks: () => Interlocked.Read(ref clock));

        var result = await service.ExecuteAsync(request);

        Assert.False(result.Success);
        // Reboot (attempt 1) resets the streak clock; rejections on attempts 2-4 build a consecutive run whose
        // elapsed reaches the 2 s window on attempt 4, where the headless run (no prompt wired) fails fast rather
        // than exhausting 25.
        Assert.Equal(4, probeAttempts);
        Assert.Contains(
            result.DeploymentContext.VmContexts,
            vm => vm.VmName == "dc01" &&
                  vm.FailureMessage != null &&
                  vm.FailureMessage.Contains("does not match this VM's base image", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_GuestTransport_ManyRejectionsWithinWindow_DoesNotTripUnderConcurrentColdStart()
    {
        // Finding 81 regression: under concurrent cold-start (two independent DC roots specializing at once) the host
        // is CPU-bound, so a guest with the CORRECT password can keep returning "the credential is invalid" for many
        // more retries than usual before specialize finishes - the live forest-trust park saw a guest still rejecting
        // past a dozen hops. With the old attempt-count grace (default 9) that unbroken run tripped the re-prompt and
        // parked the deploy headless. The time-based window must NOT trip on attempt count: here 12 consecutive
        // rejections advance the clock only 100 ms each (1.2 s total), stay well inside a 30 s window, and transport
        // then recovers on attempt 13 with no re-prompt.
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        request.GuestTransportMaxRetries = 90;
        request.GuestTransportRetryDelay = TimeSpan.FromMilliseconds(1);
        request.GuestAuthGraceWindow = TimeSpan.FromSeconds(30);
        long clock = 0;

        // A re-prompt here would mean the window tripped; wire one that fails the run so a spurious prompt is caught.
        var promptCount = 0;
        request.DeploymentContext = new MultiVmDeploymentContext
        {
            VmContextRegistered = ctx => ctx.RequestGuestCredential = (_, _) =>
            {
                Interlocked.Increment(ref promptCount);
                return Task.FromResult(GuestCredentialPromptResponse.Cancel());
            }
        };

        var probeAttempts = 0;
        var guestExecutor = new FakeGuestCommandExecutor
        {
            OnExecuteAsync = (vmName, _, script, _) =>
            {
                if (vmName == "dc01" && script.Contains("$env:COMPUTERNAME", StringComparison.Ordinal))
                {
                    var attempt = Interlocked.Increment(ref probeAttempts);
                    Interlocked.Add(ref clock, 100);
                    if (attempt <= 12)
                    {
                        // Specialize is still applying the (correct) answer-file password under heavy host load.
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
        var service = CreateService(new FakeHyperVService(), guestExecutor, nowTicks: () => Interlocked.Read(ref clock));

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        // Twelve consecutive rejections is well past the old default grace of 9, yet the 30 s window is nowhere near
        // exhausted, so no re-prompt fires and transport recovers on attempt 13.
        Assert.Equal(13, probeAttempts);
        Assert.Equal(0, promptCount);
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
        IHyperVMachineAdminService? machineAdminService = null,
        Func<long>? nowTicks = null)
    {
        var service = new V2RuntimeCapabilityService(
            () => new FakeSession(),
            _ => hyperVService,
            guestCommandExecutor,
            new FakeCleanupOrchestrator(),
            logger,
            machineAdminService);
        if (nowTicks != null)
        {
            // Deterministic wall-clock for the guest-auth grace window: tests advance it per transport probe so the
            // time-based grace can be exercised without real delays.
            service.NowTicks = nowTicks;
        }

        return service;
    }

    private async Task<V2RuntimeExecutionRequest> CreateRuntimeRequestAsync(
        string profile,
        bool includeStandalone,
        bool includeRouter,
        bool routerExternalOnDefaultSwitch = false)
    {
        var template = CreateTemplate(profile, includeStandalone, includeRouter, routerExternalOnDefaultSwitch);
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

        // The router external net normally resolves to a true External switch. The Default-Switch variant models
        // a live Hyper-V host where the router WAN is bridged onto the built-in Default Switch (a NAT switch that
        // Hyper-V reports as Internal), exercising the NAT-capable external classification.
        var externalSwitchName = routerExternalOnDefaultSwitch ? "Default Switch" : "vSwitch-External";
        var externalSwitchType = routerExternalOnDefaultSwitch ? "Internal" : "External";

        var plan = await _planningService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = catalogItems,
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge", externalSwitchName],
            AvailableSwitches =
            [
                CreateSwitch("vSwitch-Core", "Internal"),
                CreateSwitch("vSwitch-Edge", "Internal"),
                CreateSwitch(externalSwitchName, externalSwitchType)
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
            GuestTransportRetryDelay = TimeSpan.FromMilliseconds(10),
            GuestStabilizationRequiredStableProbes = 1,
            GuestStabilizationProbeInterval = TimeSpan.FromMilliseconds(1),
            GuestStabilizationTimeout = TimeSpan.FromMilliseconds(200)
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
            GuestTransportRetryDelay = TimeSpan.FromMilliseconds(10),
            GuestStabilizationRequiredStableProbes = 1,
            GuestStabilizationProbeInterval = TimeSpan.FromMilliseconds(1),
            GuestStabilizationTimeout = TimeSpan.FromMilliseconds(200)
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

    private static LabTemplate CreateTemplate(string profile, bool includeStandalone, bool includeRouter, bool routerExternalOnDefaultSwitch = false)
    {
        var externalSwitchName = routerExternalOnDefaultSwitch ? "Default Switch" : "vSwitch-External";
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
                    SwitchName = externalSwitchName
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

        // The guest-stabilization gate issues a read-only probe on every VM after transport becomes ready. Default
        // it to STABLE so the gate passes in a single hop and never spins on its timeout during tests; a test can
        // set this hook to simulate PENDING results or dropped hops for the stabilization behavior tests.
        public Func<string, CancellationToken, Task<GuestCommandResult>>? OnStabilizationProbeAsync { get; set; }

        public Task<GuestCommandResult> ExecutePowerShellDirectAsync(string vmName, V2RuntimeCredential credential, string script, CancellationToken cancellationToken = default)
        {
            if (script.Contains("IMAGE_STATE_COMPLETE", StringComparison.Ordinal))
            {
                return OnStabilizationProbeAsync?.Invoke(vmName, cancellationToken)
                    ?? Task.FromResult(new GuestCommandResult { Success = true, Output = "STABLE" });
            }

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

        // Tracks the switches attached to each VM during provisioning so GetVmNetworkAdaptersAsync can report a
        // host-side adapter (with a MAC) per switch, mirroring how the real Hyper-V host exposes them. This is what
        // the guest-network step correlates against by switch name to bind each static IP to the right adapter.
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _vmSwitches = new(StringComparer.OrdinalIgnoreCase);

        // Lets a test stand in a bespoke adapter payload for a given VM (for example an egress adapter that reports
        // an empty switch name, which the real Default Switch / NAT adapter often does). Returning null falls back to
        // the default derived inventory.
        public Func<string, IReadOnlyList<HyperVVmNetworkAdapterInfo>?>? AdapterOverride { get; init; }

        public Task<IReadOnlyList<HyperVVmNetworkAdapterInfo>> GetVmNetworkAdaptersAsync(string vmName)
        {
            if (AdapterOverride?.Invoke(vmName) is { } overridden)
            {
                return Task.FromResult(overridden);
            }

            if (vmName == "router01")
            {
                IReadOnlyList<HyperVVmNetworkAdapterInfo> routerAdapters =
                [
                    new HyperVVmNetworkAdapterInfo { AdapterName = "core", SwitchName = "vSwitch-Core", MacAddress = "00155D000001" },
                    new HyperVVmNetworkAdapterInfo { AdapterName = "external", SwitchName = "vSwitch-External", MacAddress = "00155D000002" }
                ];
                return Task.FromResult(routerAdapters);
            }

            var switches = _vmSwitches.TryGetValue(vmName, out var attached)
                ? attached.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList()
                : ["vSwitch-Core"];
            IReadOnlyList<HyperVVmNetworkAdapterInfo> adapters = switches
                .Select((name, index) => new HyperVVmNetworkAdapterInfo
                {
                    AdapterName = name,
                    SwitchName = name,
                    MacAddress = $"00155D{index + 1:X6}"
                })
                .ToList();
            return Task.FromResult(adapters);
        }

        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName)
        {
            RecordOperation($"AddSwitch:{vmName}:{switchName}");
            _vmSwitches.GetOrAdd(vmName, _ => new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase))[switchName] = 1;
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

        public Task<HyperVMachineActionResult> TurnOffVmAsync(string vmName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> RestartVmAsync(string vmName) =>
            Task.FromResult(new HyperVMachineActionResult { Success = true });

        public Task<HyperVMachineActionResult> RenameVmAsync(string currentName, string newName) =>
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

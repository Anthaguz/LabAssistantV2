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

public sealed class V2RuntimeCapabilityServiceTests
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
    public async Task ExecuteAsync_RootForestNodes_RunThroughV2RuntimePath()
    {
        var request = await CreateRuntimeRequestAsync("Conservative", includeStandalone: false, includeRouter: false);
        var service = CreateService(new FakeHyperVService(), new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains("vm:vm-dc01:InstallAdDomainServicesFeature", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:PromoteRootDomainController", result.ExecutedNodeIds);
        Assert.Contains("vm:vm-dc01:DomainReady", result.ExecutedNodeIds);
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
    public async Task ExecuteAsync_RouterAwarePlan_DefersRouterRuntimeWithoutExecutingRouterGuestNode()
    {
        var request = await CreateRuntimeRequestAsync("Balanced", includeStandalone: false, includeRouter: true);
        var hyperV = new FakeHyperVService();
        var service = CreateService(hyperV, new FakeGuestCommandExecutor());

        var result = await service.ExecuteAsync(request);

        Assert.True(result.Success);
        Assert.Contains(result.DeferredNodeIds, nodeId => nodeId == "vm:vm-router01:RouterReady");
        Assert.DoesNotContain(result.ExecutedNodeIds, nodeId => nodeId == "vm:vm-router01:RouterReady");
        Assert.Contains("CreateVm:router01", hyperV.Operations);
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

    private static IV2RuntimeCapabilityService CreateService(
        FakeHyperVService hyperVService,
        FakeGuestCommandExecutor guestCommandExecutor,
        IStructuredLogger? logger = null)
    {
        return new V2RuntimeCapabilityService(
            () => new FakeSession(),
            _ => hyperVService,
            guestCommandExecutor,
            new FakeCleanupOrchestrator(),
            logger);
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
            AvailableSwitchNames = ["vSwitch-Core", "vSwitch-Edge"],
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
            VmId = "vm-member01",
            Name = "member01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-member",
            TopologyRole = "MemberServer",
            DomainId = "domain-contoso",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
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
                TopologyRole = "StandaloneServer",
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
                        NetworkId = "lab-edge",
                        IpAddress = "10.0.1.1",
                        PrefixLength = 24
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

        public Func<string, Task>? OnCreateVmAsync { get; set; }

        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount)
        {
            Operations.Enqueue($"CreateVm:{vmName}");
            return InvokeAsync(OnCreateVmAsync, vmName);
        }

        public Task<bool> EnableGuestServicesAsync(string vmName)
        {
            Operations.Enqueue($"EnableGuestServices:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> StartVmAsync(string vmName)
        {
            Operations.Enqueue($"StartVm:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> StopVmAsync(string vmName)
        {
            Operations.Enqueue($"StopVm:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(true);

        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(true);

        public Task<bool> RemoveVmAsync(string vmName)
        {
            Operations.Enqueue($"RemoveVm:{vmName}");
            return Task.FromResult(true);
        }

        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath)
        {
            Operations.Enqueue($"CreateVhd:{Path.GetFileNameWithoutExtension(vhdPath)}");
            return Task.FromResult(true);
        }

        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);

        public Task<bool> DisableVmCheckpointsAsync(string vmName)
        {
            Operations.Enqueue($"DisableCheckpoints:{vmName}");
            return Task.FromResult(true);
        }

        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string> { "vSwitch-Core", "vSwitch-Edge" });

        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName)
        {
            Operations.Enqueue($"AddSwitch:{vmName}:{switchName}");
            return Task.FromResult(true);
        }

        private static async Task<bool> InvokeAsync(Func<string, Task>? callback, string vmName)
        {
            if (callback != null)
            {
                await callback(vmName);
            }

            return true;
        }
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
}

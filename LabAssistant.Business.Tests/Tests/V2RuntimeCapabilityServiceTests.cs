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

    private static V2AvailableSwitchInfo CreateSwitch(string name, string switchType)
        => new()
        {
            Name = name,
            SwitchType = switchType
        };

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
}

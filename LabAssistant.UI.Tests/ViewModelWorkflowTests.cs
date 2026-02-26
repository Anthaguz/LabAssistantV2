using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LabAssistant.Business.Catalog;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using LabAssistant.ViewModels;
using Xunit;

namespace LabAssistant.UI.Tests;

public class ViewModelWorkflowTests
{
    [Fact]
    public void VhdxCatalogPageViewModel_AddItem_RejectsDuplicateId()
    {
        var store = new FakeCatalogStore(
            new[]
            {
                new VhdxCatalogItem { Id = "disk-1", Path = @"C:\base\disk1.vhdx" }
            });
        var catalogService = CreateCatalogService(store);
        var viewModel = new VhdxCatalogPageViewModel(catalogService);

        var load = viewModel.LoadCatalog();
        Assert.True(load.IsSuccess);

        var result = viewModel.AddItem(new VhdxCatalogItem { Id = "disk-1", Path = @"C:\base\disk2.vhdx" });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("unique", StringComparison.OrdinalIgnoreCase));
        Assert.Single(viewModel.Items);
    }

    [Fact]
    public void MissingVhdxResolutionDialogViewModel_Import_RejectsDuplicatePath()
    {
        var store = new FakeCatalogStore(
            new[]
            {
                new VhdxCatalogItem { Id = "disk-1", Path = @"C:\base\disk1.vhdx" }
            });
        var catalogService = CreateCatalogService(store);
        var missing = new[]
        {
            new MissingVhdxReference(new VmTemplate { Name = "VM1", VhdxId = "missing" }, "missing")
        };
        var viewModel = new MissingVhdxResolutionDialogViewModel(missing, catalogService);

        var load = viewModel.LoadCatalog();
        Assert.True(load.IsSuccess);

        var result = viewModel.ImportCatalogItem(new VhdxCatalogItem
        {
            Id = "disk-2",
            Path = @"C:\base\disk1.vhdx"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Single(viewModel.CatalogOptions);
    }

    [Fact]
    public void DeployVmConfigContext_BaseVhdPath_MapsToDeploymentContextBasePath()
    {
        var settings = new FakeAppSettingsStore
        {
            Settings = new AppSettings
            {
                VmBasePath = @"C:\vm-base"
            }
        };
        var deploymentContext = new VmDeploymentContext();
        var context = new DeployVmConfigContext(null!, deploymentContext, settings);

        context.Name = "VM-A";
        context.BaseVhdPath = @"C:\catalog\base-a.vhdx";

        Assert.Equal(@"C:\vm-base\VM-A", deploymentContext.VmPath);
        Assert.Equal(@"C:\vm-base\VM-A\VM-A.vhdx", deploymentContext.VhdPath);
        Assert.Equal(@"C:\catalog\base-a.vhdx", deploymentContext.BaseVhdPath);
        Assert.Equal(deploymentContext.BaseVhdPath, deploymentContext.VhdDifferencingParentPath);
    }

    [Fact]
    public void TemplateVmConfigContext_BaseVhdPath_MapsToTemplateVhdPath()
    {
        var settingsStore = new FakeAppSettingsStore
        {
            Settings = new AppSettings
            {
                CatalogPath = @"C:\catalog\vhdx-catalog.json"
            }
        };
        var catalogStore = new FakeCatalogStore(Array.Empty<VhdxCatalogItem>());
        var catalogService = new CatalogService(catalogStore, settingsStore);
        var vm = new VmTemplate { Name = "VM-T" };
        var editor = new TemplateEditorViewModel(
            switchProvider: null,
            settingsStore: settingsStore,
            catalogStore: catalogStore,
            templateStore: new FakeTemplateStore(),
            validationService: new LabAssistant.Business.Templates.TemplateValidationService(catalogService),
            missingVhdxResolutionService: new LabAssistant.Business.Templates.MissingVhdxResolutionService(
                catalogStore,
                settingsStore));

        var context = new TemplateVmConfigContext(editor, vm);
        context.BaseVhdPath = @"C:\catalog\base-template.vhdx";

        Assert.Equal(@"C:\catalog\base-template.vhdx", vm.VhdPath);
        Assert.Equal(@"C:\catalog\base-template.vhdx", context.BaseVhdPath);
    }

    [Fact]
    public void DeployVmConfigContext_GuestStepToggles_MapToDeploymentContext()
    {
        var settings = new FakeAppSettingsStore { Settings = new AppSettings { VmBasePath = @"C:\vm-base" } };
        var deploymentContext = new VmDeploymentContext();
        var context = new DeployVmConfigContext(null, deploymentContext, settings);

        context.ConfigureTimeZoneEnabled = true;
        context.InstallSoftwareEnabled = true;
        context.InstallRoleEnabled = true;

        Assert.True(deploymentContext.ConfigureTimeZone);
        Assert.True(deploymentContext.InstallSoftware);
        Assert.True(deploymentContext.InstallRole);
        Assert.True(deploymentContext.TimeZoneConfig?.Enabled);
        Assert.True(deploymentContext.SoftwareConfig?.Enabled);
        Assert.True(deploymentContext.RoleConfig?.Enabled);

        context.InstallRoleEnabled = false;
        Assert.False(deploymentContext.InstallRole);
        Assert.False(deploymentContext.RoleConfig?.Enabled);
    }

    [Fact]
    public void TemplateVmConfigContext_GuestStepToggles_CreateAndUpdatePersistedConfigs()
    {
        var editor = CreateTemplateEditorViewModel(new FakeTemplateStore());
        var vm = new VmTemplate
        {
            VmId = "vm-1",
            Name = "vm1",
            MemoryMb = 1024,
            CpuCount = 1,
            VhdPath = @"C:\base\disk.vhdx",
            SwitchName = "Default Switch"
        };

        var context = new TemplateVmConfigContext(editor, vm);
        context.ConfigureTimeZoneEnabled = true;
        context.InstallSoftwareEnabled = true;
        context.InstallRoleEnabled = true;

        Assert.NotNull(vm.TimeZoneConfig);
        Assert.True(vm.TimeZoneConfig!.Enabled);
        Assert.NotNull(vm.SoftwareConfig);
        Assert.True(vm.SoftwareConfig!.Enabled);
        Assert.NotNull(vm.RoleConfig);
        Assert.True(vm.RoleConfig!.Enabled);

        context.InstallSoftwareEnabled = false;
        Assert.False(vm.SoftwareConfig.Enabled);
    }

    [Fact]
    public void TemplateEditorViewModel_AddVm_GeneratesVmId_AndRemoveDoesNotRegenerateOthers()
    {
        var templateStore = new FakeTemplateStore();
        var viewModel = CreateTemplateEditorViewModel(templateStore);

        viewModel.AddVm();
        viewModel.AddVm();
        viewModel.AddVm();

        var idsBefore = viewModel.VmTemplates.Select(vm => vm.VmId).ToList();
        Assert.Equal(3, idsBefore.Count);
        Assert.All(idsBefore, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(3, idsBefore.Distinct(StringComparer.Ordinal).Count());

        var removedVm = viewModel.VmTemplates[1];
        viewModel.RemoveVm(removedVm);

        var idsAfter = viewModel.VmTemplates.Select(vm => vm.VmId).ToList();
        Assert.Equal(new[] { idsBefore[0], idsBefore[2] }, idsAfter);
    }

    [Fact]
    public void TemplateEditorViewModel_LoadLegacyMissingVmId_AssignsStableVmIdsAcrossSaves()
    {
        var templateStore = new FakeTemplateStore
        {
            TemplateToLoad = new LabTemplate
            {
                Id = "template-1",
                Name = "Legacy",
                VmTemplates =
                {
                    new VmTemplate
                    {
                        VmId = "",
                        Name = "vm1",
                        MemoryMb = 1024,
                        CpuCount = 1,
                        VhdPath = @"C:\base\disk1.vhdx",
                        SwitchName = "Default Switch"
                    }
                }
            }
        };

        var viewModel = CreateTemplateEditorViewModel(templateStore);
        viewModel.LoadFromFile(@"C:\templates\legacy.json");

        var generatedVmId = viewModel.VmTemplates.Single().VmId;
        Assert.False(string.IsNullOrWhiteSpace(generatedVmId));

        viewModel.SaveToFile(@"C:\templates\legacy.json");
        var firstSaved = Assert.Single(templateStore.SavedTemplates);
        Assert.Equal(generatedVmId, firstSaved.VmTemplates.Single().VmId);

        viewModel.SaveToFile(@"C:\templates\legacy.json");
        var secondSaved = templateStore.SavedTemplates.Last();
        Assert.Equal(generatedVmId, secondSaved.VmTemplates.Single().VmId);
    }

    [Fact]
    public void TemplateEditorViewModel_SaveAs_PreservesTemplateId_AndExistingVmIds()
    {
        var templateStore = new FakeTemplateStore
        {
            TemplateToLoad = new LabTemplate
            {
                Id = "template-stable",
                Name = "Original",
                VmTemplates =
                {
                    new VmTemplate
                    {
                        VmId = "vm-1",
                        Name = "vm1",
                        MemoryMb = 1024,
                        CpuCount = 1,
                        VhdPath = @"C:\base\disk1.vhdx",
                        SwitchName = "Default Switch"
                    },
                    new VmTemplate
                    {
                        VmId = "vm-2",
                        Name = "vm2",
                        MemoryMb = 2048,
                        CpuCount = 2,
                        VhdPath = @"C:\base\disk2.vhdx",
                        SwitchName = "Default Switch"
                    }
                }
            }
        };

        var viewModel = CreateTemplateEditorViewModel(templateStore);
        viewModel.LoadFromFile(@"C:\templates\original.json");
        viewModel.Template.Name = "Edited";

        viewModel.SaveToFile(@"D:\exports\save-as.json");

        var saved = Assert.Single(templateStore.SavedTemplates);
        Assert.Equal("template-stable", saved.Id);
        Assert.Equal(new[] { "vm-1", "vm-2" }, saved.VmTemplates.Select(vm => vm.VmId).ToArray());
    }

    [Fact]
    public void TemplateEditorViewModel_ReorderVmEntries_PreservesVmIds()
    {
        var templateStore = new FakeTemplateStore();
        var viewModel = CreateTemplateEditorViewModel(templateStore);
        viewModel.AddVm();
        viewModel.AddVm();

        viewModel.VmTemplates[0].Name = "A";
        viewModel.VmTemplates[1].Name = "B";
        var firstId = viewModel.VmTemplates[0].VmId;
        var secondId = viewModel.VmTemplates[1].VmId;

        viewModel.VmTemplates.Move(0, 1);
        viewModel.SaveToFile(@"C:\templates\reordered.json");

        var saved = Assert.Single(templateStore.SavedTemplates);
        Assert.Equal(new[] { "B", "A" }, saved.VmTemplates.Select(vm => vm.Name).ToArray());
        Assert.Equal(new[] { secondId, firstId }, saved.VmTemplates.Select(vm => vm.VmId).ToArray());
    }

    [Fact]
    public void TemplateEditorViewModel_LoadAndSave_EmitStructuredTemplateEvents()
    {
        var templateStore = new FakeTemplateStore
        {
            TemplateToLoad = new LabTemplate
            {
                Id = "template-1",
                Name = "Template One",
                VmTemplates =
                {
                    new VmTemplate { VmId = "vm-1", Name = "VM1", MemoryMb = 1024, CpuCount = 1, VhdPath = @"C:\base\a.vhdx", SwitchName = "Default Switch" }
                }
            }
        };
        var logger = new RecordingStructuredLogger();
        var viewModel = CreateTemplateEditorViewModel(templateStore, logger);

        viewModel.LoadFromFile(@"C:\templates\template-1.json");
        viewModel.SaveToFile(@"C:\templates\template-1.json");

        Assert.Contains(logger.Events, e => e.Event == "TemplateImported" && e.Result is "success" or "success_with_warnings");
        Assert.Contains(logger.Events, e => e.Event == "TemplateSaved" && e.Result == "success");

        var imported = logger.Events.First(e => e.Event == "TemplateImported");
        var saved = logger.Events.First(e => e.Event == "TemplateSaved");
        Assert.False(string.IsNullOrWhiteSpace(imported.OperationId));
        Assert.False(string.IsNullOrWhiteSpace(saved.OperationId));
        Assert.True(DateTimeOffset.TryParse(imported.Ts, out var importedTs));
        Assert.True(DateTimeOffset.TryParse(saved.Ts, out var savedTs));
        Assert.Equal(TimeSpan.Zero, importedTs.Offset);
        Assert.Equal(TimeSpan.Zero, savedTs.Offset);
        Assert.Contains(imported.Level, new[] { "debug", "info", "warn", "error" });
        Assert.Contains(saved.Level, new[] { "debug", "info", "warn", "error" });
        Assert.Contains(imported.Event, new[] { "TemplateImported", "TemplateSaved" });
        Assert.Contains(saved.Event, new[] { "TemplateImported", "TemplateSaved" });
        Assert.Equal("template-1", imported.Context?["templateId"]?.ToString());
        Assert.Equal("Template One", saved.Context?["templateName"]?.ToString());
        AssertNoSensitiveContextKeys(imported);
        AssertNoSensitiveContextKeys(saved);
    }

    [Theory]
    [InlineData(false, false, DeploymentOperationState.Idle, false, true, false)]
    [InlineData(true, true, DeploymentOperationState.Running, true, false, true)]
    [InlineData(true, true, DeploymentOperationState.Cancelling, true, false, true)]
    [InlineData(true, true, DeploymentOperationState.CleanupInProgress, true, false, true)]
    [InlineData(false, false, DeploymentOperationState.Failed, false, true, false)]
    [InlineData(false, false, DeploymentOperationState.FailedWithResiduals, false, true, false)]
    [InlineData(false, false, DeploymentOperationState.Cancelled, false, true, false)]
    [InlineData(false, false, DeploymentOperationState.CancelledWithResiduals, false, true, false)]
    [InlineData(false, false, DeploymentOperationState.Completed, false, true, false)]
    public void DeploymentUiInteractivity_MapsTerminalAndActiveStatesCorrectly(
        bool isDeploying,
        bool hasActiveContext,
        DeploymentOperationState state,
        bool expectedHasActiveOperation,
        bool expectedCanEditConfig,
        bool expectedCanCancel)
    {
        var hasActiveOperation = DeploymentUiInteractivity.HasActiveOperation(isDeploying, hasActiveContext, state);

        Assert.Equal(expectedHasActiveOperation, hasActiveOperation);
        Assert.Equal(expectedCanEditConfig, !hasActiveOperation);
        Assert.Equal(expectedCanCancel, hasActiveOperation);
    }

    [Fact]
    public async Task DeploymentViewModel_RelevantVmConfigChange_TriggersQuickPreflightAndUpdatesReadinessReport()
    {
        var preflight = new RecordingPreflightService
        {
            ResultFactory = (_, mode) => new DeploymentReadinessReport
            {
                Mode = mode,
                Results =
                [
                    new DeploymentReadinessCheckResult
                    {
                        Status = DeploymentReadinessStatus.Pass,
                        Category = DeploymentReadinessCategory.DestinationPathStorage,
                        Code = mode == DeploymentPreflightMode.Quick ? "DST.QUICK.OK" : "DST.FULL.OK",
                        Message = "ok",
                        ActionableGuidance = "none"
                    }
                ]
            }
        };

        var viewModel = CreateDeploymentViewModel(preflight, new RecordingDeploymentCoordinator(), quickPreflightDebounce: TimeSpan.FromMilliseconds(10));
        viewModel.AddVmCommand.Execute(null);
        var vm = viewModel.VmEntries.Single();

        await WaitUntilAsync(() => preflight.Calls.Count > 0, TimeSpan.FromSeconds(2));
        var baselineCalls = preflight.Calls.Count;

        vm.VhdPath = @"D:\Labs\vm1\vm1.vhdx";

        await WaitUntilAsync(() => preflight.Calls.Count > baselineCalls, TimeSpan.FromSeconds(2));
        Assert.Equal(DeploymentPreflightMode.Quick, preflight.Calls.Last().Mode);
        Assert.NotNull(viewModel.ReadinessReport);
        Assert.Equal(DeploymentPreflightMode.Quick, viewModel.ReadinessReport!.Mode);
    }

    [Fact]
    public async Task DeployVmConfigContext_GuestStepToggle_TriggersQuickPreflight()
    {
        var preflight = new RecordingPreflightService
        {
            ResultFactory = (_, mode) => new DeploymentReadinessReport { Mode = mode }
        };

        var viewModel = CreateDeploymentViewModel(preflight, new RecordingDeploymentCoordinator(), quickPreflightDebounce: TimeSpan.FromMilliseconds(10));
        viewModel.AddVmCommand.Execute(null);
        var vm = viewModel.VmEntries.Single();
        var configContext = new DeployVmConfigContext(viewModel, vm.DeploymentContext, new FakeAppSettingsStore
        {
            Settings = new AppSettings { VmBasePath = @"C:\vm-base" }
        });

        await WaitUntilAsync(() => preflight.Calls.Count > 0, TimeSpan.FromSeconds(2));
        var baselineCalls = preflight.Calls.Count;

        configContext.InstallSoftwareEnabled = true;

        await WaitUntilAsync(() => preflight.Calls.Count > baselineCalls, TimeSpan.FromSeconds(2));
        Assert.Equal(DeploymentPreflightMode.Quick, preflight.Calls.Last().Mode);
    }

    [Fact]
    public async Task DeploymentViewModel_DeployAll_RunsFullPreflightAndBlocksOnFailures()
    {
        var preflight = new RecordingPreflightService
        {
            ResultFactory = (_, mode) => new DeploymentReadinessReport
            {
                Mode = mode,
                Results = mode == DeploymentPreflightMode.Full
                    ?
                    [
                        new DeploymentReadinessCheckResult
                        {
                            Status = DeploymentReadinessStatus.Fail,
                            Category = DeploymentReadinessCategory.VhdxBaseDisk,
                            Code = "VHDX.INVALID",
                            Message = "Invalid VHDX",
                            ActionableGuidance = "Fix it",
                            AffectedVmNames = ["VM1"]
                        }
                    ]
                    : []
            }
        };
        var coordinator = new RecordingDeploymentCoordinator();
        var viewModel = CreateDeploymentViewModel(preflight, coordinator, quickPreflightDebounce: TimeSpan.FromMilliseconds(5));
        viewModel.AddVmCommand.Execute(null);
        var vm = viewModel.VmEntries.Single().DeploymentContext;
        vm.BaseVhdPath = @"C:\base\good.vhdx";

        await viewModel.DeployAllCommand.ExecuteAsync(null);

        Assert.Contains(preflight.Calls, c => c.Mode == DeploymentPreflightMode.Full);
        Assert.Equal(0, coordinator.CallCount);
        Assert.True(viewModel.IsDeployBlockedByReadiness);
        Assert.NotNull(viewModel.ReadinessReport);
        Assert.Equal(DeploymentPreflightMode.Full, viewModel.ReadinessReport!.Mode);
    }

    [Fact]
    public async Task DeploymentViewModel_DeployAll_AllowsWarningsOnlyAndStartsDeployment()
    {
        var preflight = new RecordingPreflightService
        {
            ResultFactory = (_, mode) => new DeploymentReadinessReport
            {
                Mode = mode,
                Results = mode == DeploymentPreflightMode.Full
                    ?
                    [
                        new DeploymentReadinessCheckResult
                        {
                            Status = DeploymentReadinessStatus.Warn,
                            Category = DeploymentReadinessCategory.DestinationPathStorage,
                            Code = "DST.STORAGE.LOW_FREE_SPACE",
                            Message = "Low free space",
                            ActionableGuidance = "Proceed with caution",
                            AffectedVmNames = ["VM1"],
                            ResourcePath = @"C:\"
                        }
                    ]
                    : []
            }
        };
        var coordinator = new RecordingDeploymentCoordinator();
        var viewModel = CreateDeploymentViewModel(preflight, coordinator, quickPreflightDebounce: TimeSpan.FromMilliseconds(5));
        viewModel.AddVmCommand.Execute(null);
        var vm = viewModel.VmEntries.Single().DeploymentContext;
        vm.BaseVhdPath = @"C:\base\good.vhdx";

        await viewModel.DeployAllCommand.ExecuteAsync(null);

        Assert.Contains(preflight.Calls, c => c.Mode == DeploymentPreflightMode.Full);
        Assert.Equal(1, coordinator.CallCount);
        Assert.False(viewModel.IsDeployBlockedByReadiness);
    }

    [Fact]
    public async Task DeploymentViewModel_FullPreflightCanBlockAfterQuickReportWasClean()
    {
        var preflight = new RecordingPreflightService
        {
            ResultFactory = (_, mode) => new DeploymentReadinessReport
            {
                Mode = mode,
                Results = mode == DeploymentPreflightMode.Full
                    ?
                    [
                        new DeploymentReadinessCheckResult
                        {
                            Status = DeploymentReadinessStatus.Fail,
                            Category = DeploymentReadinessCategory.DestinationPathStorage,
                            Code = "DST.VM_PATH.UNWRITABLE",
                            Message = "VM path is not writable",
                            ActionableGuidance = "Choose a writable path",
                            AffectedVmNames = ["VM1"],
                            ResourcePath = @"D:\Labs\VM1"
                        }
                    ]
                    :
                    [
                        new DeploymentReadinessCheckResult
                        {
                            Status = DeploymentReadinessStatus.Pass,
                            Category = DeploymentReadinessCategory.TemplateConfig,
                            Code = "CFG.OK",
                            Message = "Quick check passed",
                            ActionableGuidance = "None"
                        }
                    ]
            }
        };
        var coordinator = new RecordingDeploymentCoordinator();
        var viewModel = CreateDeploymentViewModel(preflight, coordinator, quickPreflightDebounce: TimeSpan.FromMilliseconds(5));
        viewModel.AddVmCommand.Execute(null);
        var vm = viewModel.VmEntries.Single().DeploymentContext;
        vm.BaseVhdPath = @"C:\base\good.vhdx";

        await WaitUntilAsync(() => viewModel.ReadinessReport?.Mode == DeploymentPreflightMode.Quick, TimeSpan.FromSeconds(2));
        Assert.False(viewModel.IsDeployBlockedByReadiness);

        await viewModel.DeployAllCommand.ExecuteAsync(null);

        Assert.Equal(0, coordinator.CallCount);
        Assert.True(viewModel.IsDeployBlockedByReadiness);
        Assert.Equal(DeploymentPreflightMode.Full, viewModel.ReadinessReport?.Mode);
        Assert.Contains(preflight.Calls, c => c.Mode == DeploymentPreflightMode.Quick);
        Assert.Contains(preflight.Calls, c => c.Mode == DeploymentPreflightMode.Full);
    }

    [Fact]
    public async Task DeploymentViewModel_GuestStepCompletenessFailure_BlocksDeployAndSurfacesTemplateConfigIssue()
    {
        var preflight = new RecordingPreflightService
        {
            ResultFactory = (_, mode) => new DeploymentReadinessReport
            {
                Mode = mode,
                Results = mode == DeploymentPreflightMode.Full
                    ?
                    [
                        new DeploymentReadinessCheckResult
                        {
                            Status = DeploymentReadinessStatus.Fail,
                            Category = DeploymentReadinessCategory.TemplateConfig,
                            Code = "GST.SOFTWARE.EMPTY_PACKAGE_LIST",
                            Message = "VM 'VM1': Software install step is enabled but no packages are configured.",
                            ActionableGuidance = "Add at least one software package for this VM or disable the software step before deploying.",
                            AffectedVmNames = ["VM1"],
                            ResourceName = "software install"
                        }
                    ]
                    : []
            }
        };
        var coordinator = new RecordingDeploymentCoordinator();
        var viewModel = CreateDeploymentViewModel(preflight, coordinator, quickPreflightDebounce: TimeSpan.FromMilliseconds(5));
        viewModel.AddVmCommand.Execute(null);
        var vm = viewModel.VmEntries.Single().DeploymentContext;
        vm.BaseVhdPath = @"C:\base\good.vhdx";
        vm.InstallSoftware = true;

        await viewModel.DeployAllCommand.ExecuteAsync(null);

        Assert.Equal(0, coordinator.CallCount);
        Assert.True(viewModel.IsDeployBlockedByReadiness);
        Assert.Equal(DeploymentPreflightMode.Full, viewModel.ReadinessReport?.Mode);
        var failure = Assert.Single(viewModel.ReadinessReport!.Results);
        Assert.Equal("GST.SOFTWARE.EMPTY_PACKAGE_LIST", failure.Code);
        Assert.Equal(DeploymentReadinessCategory.TemplateConfig, failure.Category);
        Assert.Contains("software package", failure.ActionableGuidance, StringComparison.OrdinalIgnoreCase);
    }

    private static CatalogService CreateCatalogService(FakeCatalogStore store)
    {
        var settingsStore = new FakeAppSettingsStore
        {
            Settings = new AppSettings
            {
                CatalogPath = @"C:\catalog\vhdx-catalog.json"
            }
        };

        return new CatalogService(store, settingsStore);
    }

    private static TemplateEditorViewModel CreateTemplateEditorViewModel(FakeTemplateStore templateStore, IStructuredLogger? structuredLogger = null)
    {
        var settingsStore = new FakeAppSettingsStore
        {
            Settings = new AppSettings
            {
                CatalogPath = @"C:\catalog\vhdx-catalog.json"
            }
        };
        var catalogStore = new FakeCatalogStore(Array.Empty<VhdxCatalogItem>());
        var catalogService = new CatalogService(catalogStore, settingsStore);

        return new TemplateEditorViewModel(
            switchProvider: null,
            settingsStore: settingsStore,
            catalogStore: catalogStore,
            templateStore: templateStore,
            validationService: new TemplateValidationService(catalogService),
            missingVhdxResolutionService: new MissingVhdxResolutionService(catalogStore, settingsStore),
            structuredLogger: structuredLogger);
    }

    private sealed class FakeCatalogStore : IVhdxCatalogStore
    {
        private List<VhdxCatalogItem> _items;

        public FakeCatalogStore(IEnumerable<VhdxCatalogItem> items)
        {
            _items = items.Select(Clone).ToList();
        }

        public VhdxCatalogLoadResult Load(string catalogPath)
        {
            var result = new VhdxCatalogLoadResult();
            result.Items.AddRange(_items.Select(Clone));
            return result;
        }

        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items)
        {
            _items = items.Select(Clone).ToList();
            return new VhdxCatalogSaveResult();
        }

        public void EnsureCatalogFileExists(string catalogPath)
        {
        }

        private static VhdxCatalogItem Clone(VhdxCatalogItem source)
        {
            return new VhdxCatalogItem
            {
                Id = source.Id,
                Path = source.Path,
                OsName = source.OsName,
                OsVersion = source.OsVersion,
                Generation = source.Generation,
                SizeBytes = source.SizeBytes,
                Signature = source.Signature,
                Notes = source.Notes
            };
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; set; } = new();

        public string SettingsPath => string.Empty;

        public void LoadOrCreate()
        {
        }

        public void Reload()
        {
        }

        public void Save()
        {
        }

        public void ResetToDefault()
        {
        }

        public void SetTemplateFolder(string path)
        {
            Settings.TemplateFolder = path;
        }

        public void SetLogFolder(string path)
        {
            Settings.LogFolder = path;
        }

        public void SetVmBasePath(string path)
        {
            Settings.VmBasePath = path;
        }

        public void SetDifferencingDiskBasePath(string path)
        {
            Settings.DifferencingDiskBasePath = path;
        }
    }

    private sealed class FakeTemplateStore : ILabTemplateStore
    {
        public LabTemplate? TemplateToLoad { get; set; }
        public List<LabTemplate> SavedTemplates { get; } = new();
        public IReadOnlyList<string> LastLoadWarnings => Array.Empty<string>();

        public LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems)
        {
            return new LabTemplateLoadResult();
        }

        public LabTemplate LoadFromFile(string filePath)
        {
            return TemplateToLoad is null ? new LabTemplate() : CloneTemplate(TemplateToLoad);
        }

        public void SaveToFile(string filePath, LabTemplate template)
        {
            SavedTemplates.Add(CloneTemplate(template));
        }

        public string SaveToFolder(string folderPath, string templateName, LabTemplate template)
        {
            return System.IO.Path.Combine(folderPath, $"{templateName}.json");
        }

        private static LabTemplate CloneTemplate(LabTemplate source)
        {
            var clone = new LabTemplate
            {
                Id = source.Id,
                Name = source.Name,
                Description = source.Description,
                SchemaVersion = source.SchemaVersion,
                TemplateRevision = source.TemplateRevision,
                CreatedWithAppVersion = source.CreatedWithAppVersion,
                TemplateType = source.TemplateType
            };

            clone.VmTemplates = source.VmTemplates
                .Select(vm => new VmTemplate
                {
                    VmId = vm.VmId,
                    Name = vm.Name,
                    MemoryMb = vm.MemoryMb,
                    CpuCount = vm.CpuCount,
                    VhdxId = vm.VhdxId,
                    VhdPath = vm.VhdPath,
                    VhdxSignature = vm.VhdxSignature,
                    SwitchName = vm.SwitchName
                })
                .ToList();

            return clone;
        }
    }

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = new();

        public void Log(StructuredLogEvent logEvent)
        {
            Events.Add(logEvent);
        }

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
        }
    }

    private static void AssertNoSensitiveContextKeys(StructuredLogEvent logEvent)
    {
        if (logEvent.Context == null)
        {
            return;
        }

        foreach (var key in logEvent.Context.Keys)
        {
            Assert.DoesNotContain("password", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DeploymentViewModel CreateDeploymentViewModel(
        RecordingPreflightService preflightService,
        RecordingDeploymentCoordinator coordinator,
        TimeSpan? quickPreflightDebounce = null)
    {
        var switchProvider = new VirtualSwitchProvider(
            () => new FakePersistentPowerShellSession(),
            _ => new FakeHyperVService());

        var settingsStore = new FakeAppSettingsStore
        {
            Settings = new AppSettings
            {
                CatalogPath = @"C:\catalog\vhdx-catalog.json",
                VmBasePath = @"C:\labs",
                StopAllOnAnyVmFailure = true
            }
        };

        var catalogStore = new FakeCatalogStore(Array.Empty<VhdxCatalogItem>());
        var templateStore = new FakeTemplateStore();
        var appPaths = new FakeAppPaths();
        var errorFeed = new RecordingErrorFeedService();
        var outcomeBuilder = new StubOutcomeSummaryBuilder();

        return new DeploymentViewModel(
            coordinator,
            switchProvider,
            preflightService,
            settingsStore,
            templateStore,
            appPaths,
            catalogStore,
            errorFeed,
            outcomeBuilder,
            structuredLogger: new RecordingStructuredLogger(),
            quickPreflightDebounce: quickPreflightDebounce ?? TimeSpan.FromMilliseconds(10));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class RecordingPreflightService : IDeploymentPreflightService
    {
        public List<(string OperationId, DeploymentPreflightMode Mode, int VmCount)> Calls { get; } = [];
        public Func<MultiVmDeploymentContext, DeploymentPreflightMode, DeploymentReadinessReport> ResultFactory { get; set; } =
            (_, mode) => new DeploymentReadinessReport { Mode = mode };

        public Task<DeploymentReadinessReport> RunAsync(MultiVmDeploymentContext deploymentContext, DeploymentPreflightMode mode, CancellationToken cancellationToken = default)
        {
            Calls.Add((deploymentContext.OperationId, mode, deploymentContext.VmContexts.Count));
            return Task.FromResult(ResultFactory(deploymentContext, mode));
        }
    }

    private sealed class RecordingDeploymentCoordinator : IDeploymentCoordinator
    {
        public int CallCount { get; private set; }

        public Task DeployAllAsync(MultiVmDeploymentContext multiContext)
        {
            CallCount++;
            multiContext.MarkRunning();
            multiContext.CompleteTerminalState(hasFailures: false, hasCleanupResiduals: false);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePersistentPowerShellSession : IPersistentPowerShellSession
    {
        public Task<(string Output, string Error)> ExecuteAsync(string command)
            => Task.FromResult<(string Output, string Error)>((string.Empty, string.Empty));

        public void Dispose()
        {
        }
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(true);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(true);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string> { "Default Switch" });
        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(false);
        public Task<bool> RemoveVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(false);
    }

    private sealed class RecordingErrorFeedService : IErrorFeedService
    {
        public ObservableCollection<ErrorFeedItem> ActiveItems { get; } = [];
        public ObservableCollection<ErrorFeedItem> RecentItems { get; } = [];
        public int TotalErrorCount { get; private set; }
        public event Action? StateChanged;

        public void Publish(string? vmName, string title, string message, Action? viewDetailsAction = null)
        {
            var item = new ErrorFeedItem
            {
                VmName = vmName,
                Title = title,
                Message = message,
                ViewDetailsAction = viewDetailsAction
            };
            ActiveItems.Add(item);
            RecentItems.Add(item);
            TotalErrorCount++;
            StateChanged?.Invoke();
        }

        public void Dismiss(Guid id)
        {
        }
    }

    private sealed class StubOutcomeSummaryBuilder : IDeploymentOutcomeSummaryBuilder
    {
        public DeploymentOutcomeSummary Build(MultiVmDeploymentContext multiVmContext)
        {
            return new DeploymentOutcomeSummary
            {
                OperationState = multiVmContext.OperationState,
                TotalVmCount = multiVmContext.VmContexts.Count,
                VmOutcomes = multiVmContext.VmContexts.Select(vm => new VmDeploymentOutcomeSummary
                {
                    VmId = vm.VmId,
                    VmName = vm.VmName,
                    Status = vm.IsSuccess ? VmDeploymentOutcomeStatus.Succeeded : (vm.WasCancelled ? VmDeploymentOutcomeStatus.Cancelled : VmDeploymentOutcomeStatus.Failed),
                    Cleanup = new VmCleanupOutcomeSummary
                    {
                        Status = VmCleanupOutcomeStatus.NotNeeded,
                        ResidualCount = 0
                    },
                    Residuals = []
                }).ToList()
            };
        }
    }

    private sealed class FakeAppPaths : IAppPaths
    {
        public string AppRoot => @"C:\appdata\LabAssistant";
        public string ConfigFolder => @"C:\appdata\LabAssistant\Config";
        public string RootAppDataFolder => @"C:\appdata\LabAssistant";
        public string TemplatesFolder => @"C:\appdata\LabAssistant\Templates";
        public string LogsFolder => @"C:\appdata\LabAssistant\Logs";
        public string CatalogFolder => @"C:\appdata\LabAssistant\Catalog";
        public string VmBasePath => @"C:\labs";
        public string DifferencingDiskBasePath => @"C:\labs\diff";
        public string CatalogPath => @"C:\appdata\LabAssistant\Catalog\vhdx-catalog.json";
        public string SettingsFolder => @"C:\appdata\LabAssistant\Settings";
        public string SettingsFilePath => @"C:\appdata\LabAssistant\Settings\appsettings.json";
    }
}

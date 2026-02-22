using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Business.Catalog;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
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

    private static TemplateEditorViewModel CreateTemplateEditorViewModel(FakeTemplateStore templateStore)
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
            missingVhdxResolutionService: new MissingVhdxResolutionService(catalogStore, settingsStore));
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
}

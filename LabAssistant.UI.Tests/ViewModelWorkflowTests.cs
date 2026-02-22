using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Business.Catalog;
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
        public IReadOnlyList<string> LastLoadWarnings => Array.Empty<string>();

        public LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems)
        {
            return new LabTemplateLoadResult();
        }

        public LabTemplate LoadFromFile(string filePath)
        {
            return new LabTemplate();
        }

        public void SaveToFile(string filePath, LabTemplate template)
        {
        }

        public string SaveToFolder(string folderPath, string templateName, LabTemplate template)
        {
            return System.IO.Path.Combine(folderPath, $"{templateName}.json");
        }
    }
}

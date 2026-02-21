using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
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
}

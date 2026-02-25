using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;

namespace LabAssistant.ViewModels;

public sealed class VhdxCatalogPageViewModel
{
    private readonly CatalogService _catalogService;

    public VhdxCatalogPageViewModel(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public ObservableCollection<VhdxCatalogItem> Items { get; } = new();

    public string CatalogPath => _catalogService.CatalogPath;

    public CatalogOperationResult LoadCatalog()
    {
        var result = _catalogService.LoadCatalog();
        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }

        return new CatalogOperationResult(result.Errors);
    }

    public CatalogOperationResult AddItem(VhdxCatalogItem item)
    {
        if (HasDuplicateId(item.Id, null))
        {
            return CatalogOperationResult.FromError("Catalog id must be unique.");
        }

        Items.Add(item);
        var save = SaveCatalog([item]);
        if (!save.IsSuccess)
        {
            Items.Remove(item);
        }

        return save;
    }

    public CatalogOperationResult UpdateItem(VhdxCatalogItem selected, VhdxCatalogItem updated)
    {
        if (HasDuplicateId(updated.Id, selected))
        {
            return CatalogOperationResult.FromError("Catalog id must be unique.");
        }

        var original = CloneItem(selected);
        selected.Id = updated.Id;
        selected.Path = updated.Path;
        selected.OsName = updated.OsName;
        selected.OsVersion = updated.OsVersion;
        selected.Generation = updated.Generation;
        selected.Notes = updated.Notes;

        var save = SaveCatalog([selected]);
        if (!save.IsSuccess)
        {
            selected.Id = original.Id;
            selected.Path = original.Path;
            selected.OsName = original.OsName;
            selected.OsVersion = original.OsVersion;
            selected.Generation = original.Generation;
            selected.Notes = original.Notes;
        }

        return save;
    }

    public CatalogOperationResult DeleteItem(VhdxCatalogItem selected)
    {
        Items.Remove(selected);
        var save = SaveCatalog([]);
        if (!save.IsSuccess)
        {
            Items.Add(selected);
        }

        return save;
    }

    private CatalogOperationResult SaveCatalog(IEnumerable<VhdxCatalogItem> itemsToValidate)
    {
        var result = _catalogService.SaveCatalog(Items, itemsToValidate);
        return new CatalogOperationResult(result.Errors);
    }

    private bool HasDuplicateId(string id, VhdxCatalogItem? ignore)
    {
        return Items.Any(item =>
            !ReferenceEquals(item, ignore) &&
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static VhdxCatalogItem CloneItem(VhdxCatalogItem source)
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

public sealed class CatalogOperationResult
{
    public CatalogOperationResult(IEnumerable<string> errors)
    {
        Errors = errors.ToList();
    }

    public bool IsSuccess => Errors.Count == 0;

    public IReadOnlyList<string> Errors { get; }

    public static CatalogOperationResult FromError(string error)
    {
        return new CatalogOperationResult(new[] { error });
    }
}

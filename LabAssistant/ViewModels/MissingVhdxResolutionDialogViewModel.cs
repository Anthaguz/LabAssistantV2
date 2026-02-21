using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;

namespace LabAssistant.ViewModels;

public sealed class MissingVhdxResolutionDialogViewModel
{
    private readonly CatalogService _catalogService;
    private List<VhdxCatalogItem> _catalogItems = new();

    public MissingVhdxResolutionDialogViewModel(
        IEnumerable<MissingVhdxReference> missingReferences,
        CatalogService catalogService)
    {
        _catalogService = catalogService;
        Items = new ObservableCollection<MissingVhdxResolutionItem>(
            missingReferences.Select(reference => new MissingVhdxResolutionItem(reference)));
        CatalogOptions = new ObservableCollection<VhdxCatalogOption>();

        foreach (var item in Items)
        {
            item.PropertyChanged += Item_PropertyChanged;
        }
    }

    public ObservableCollection<MissingVhdxResolutionItem> Items { get; }

    public ObservableCollection<VhdxCatalogOption> CatalogOptions { get; }

    public bool CanResolve => Items.All(item => item.SelectedOption != null);

    public bool IsCatalogEmpty => _catalogItems.Count == 0;

    public event Action? StateChanged;

    public CatalogOperationResult LoadCatalog()
    {
        var result = _catalogService.LoadCatalog();
        _catalogItems = result.Items.ToList();
        RefreshCatalogOptions();
        StateChanged?.Invoke();
        return new CatalogOperationResult(result.Errors);
    }

    public CatalogOperationResult ImportCatalogItem(VhdxCatalogItem item)
    {
        var duplicatePath = FindDuplicatePath(item.Path);
        if (duplicatePath != null)
        {
            return CatalogOperationResult.FromError($"A catalog entry already exists for this path: {duplicatePath.Path}");
        }

        if (HasDuplicateId(item.Id))
        {
            return CatalogOperationResult.FromError("Catalog id must be unique.");
        }

        _catalogItems.Add(item);
        var save = SaveCatalog();
        if (!save.IsSuccess)
        {
            _catalogItems.Remove(item);
            return save;
        }

        RefreshCatalogOptions();
        StateChanged?.Invoke();
        return save;
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MissingVhdxResolutionItem.SelectedOption))
        {
            StateChanged?.Invoke();
        }
    }

    private void RefreshCatalogOptions()
    {
        CatalogOptions.Clear();
        foreach (var item in _catalogItems)
        {
            CatalogOptions.Add(new VhdxCatalogOption(item));
        }

        foreach (var item in Items)
        {
            if (item.SelectedOption != null &&
                !CatalogOptions.Any(option => string.Equals(option.Item.Id, item.SelectedOption.Item.Id, StringComparison.OrdinalIgnoreCase)))
            {
                item.SelectedOption = null;
            }
        }
    }

    private CatalogOperationResult SaveCatalog()
    {
        var result = _catalogService.SaveCatalog(_catalogItems);
        return new CatalogOperationResult(result.Errors);
    }

    private bool HasDuplicateId(string id)
    {
        return _catalogItems.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private VhdxCatalogItem? FindDuplicatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _catalogItems.FirstOrDefault(item =>
            string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
    }
}

using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Loads and caches the shared Deploy reference data needed by Deploy lanes without routing that responsibility through MainWindow or shared composition.
/// </summary>
internal sealed class DeployReferenceDataService
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IVhdxCatalogStore _vhdxCatalogStore;
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private IReadOnlyList<VhdxCatalogItem> _catalogItems = Array.Empty<VhdxCatalogItem>();
    private readonly List<TemplateVhdxCatalogOption> _vhdxCatalogOptions = [];

    public DeployReferenceDataService(
        IAppSettingsStore settingsStore,
        IVhdxCatalogStore vhdxCatalogStore,
        IMachinesCapabilityService machinesCapabilityService,
        ITemplatesCapabilityService templatesCapabilityService)
    {
        _settingsStore = settingsStore;
        _vhdxCatalogStore = vhdxCatalogStore;
        _machinesCapabilityService = machinesCapabilityService;
        _templatesCapabilityService = templatesCapabilityService;
    }

    public AppSettings DeploymentSettings => _settingsStore.Settings;

    public IReadOnlyList<string> AvailableSwitches => _availableSwitches;

    public IReadOnlyList<VhdxCatalogItem> CatalogItems => _catalogItems;

    public IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions => _vhdxCatalogOptions;

    public async Task EnsureAsync(bool forceRefresh)
    {
        if (forceRefresh || _availableSwitches.Count == 0)
        {
            await LoadSwitchesAsync();
        }

        if (forceRefresh || _vhdxCatalogOptions.Count == 0)
        {
            await LoadVhdxCatalogOptionsAsync();
        }

        _catalogItems = _vhdxCatalogStore.Load(_settingsStore.Settings.CatalogPath).Items;
    }

    private async Task LoadSwitchesAsync()
    {
        try
        {
            var switches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
            _availableSwitches = switches
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            _availableSwitches = Array.Empty<string>();
        }
    }

    private async Task LoadVhdxCatalogOptionsAsync()
    {
        _vhdxCatalogOptions.Clear();
        var result = await _templatesCapabilityService.LoadVhdxCatalogOptionsAsync();
        foreach (var item in result.Items)
        {
            _vhdxCatalogOptions.Add(new TemplateVhdxCatalogOption(
                item.Id,
                item.Path,
                item.OsName,
                item.OsVersion,
                item.Generation,
                item.Signature));
        }
    }
}

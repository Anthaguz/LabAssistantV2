using LabAssistant.Business.Assets;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Reference data (available VM switches, switch inventory, and VHDX catalog options) shared by the
/// Templates Editor and Builder subviews. Registered as a DI singleton so the switch and catalog
/// lookups are loaded and cached once for the app lifetime, matching the caching that previously
/// lived on the dissolved <c>TemplatesCapabilityRuntime</c>. Callers pass <c>forceRefresh</c> to
/// bypass the cache and re-query the underlying capability services.
/// </summary>
internal sealed class TemplatesReferenceDataService
{
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly IAssetsSwitchesCapabilityService _assetsSwitchesCapabilityService;
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly List<TemplateVhdxCatalogOption> _vhdxCatalogOptions = [];
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private IReadOnlyList<V2AvailableSwitchInfo> _availableSwitchInfo = Array.Empty<V2AvailableSwitchInfo>();

    public TemplatesReferenceDataService(
        IMachinesCapabilityService machinesCapabilityService,
        IAssetsSwitchesCapabilityService assetsSwitchesCapabilityService,
        ITemplatesCapabilityService templatesCapabilityService)
    {
        _machinesCapabilityService = machinesCapabilityService;
        _assetsSwitchesCapabilityService = assetsSwitchesCapabilityService;
        _templatesCapabilityService = templatesCapabilityService;
    }

    public async Task<TemplatesEditorReferenceData> LoadEditorReferenceDataAsync(bool forceRefresh)
    {
        await EnsureAvailableVmSwitchesAsync(forceRefresh);
        await EnsureVhdxCatalogOptionsAsync(forceRefresh);
        return new TemplatesEditorReferenceData(_availableSwitches, _vhdxCatalogOptions.ToList());
    }

    public async Task<TemplatesBuilderReferenceData> LoadBuilderReferenceDataAsync(bool forceRefresh)
    {
        await EnsureAvailableVmSwitchesAsync(forceRefresh);
        await EnsureVhdxCatalogOptionsAsync(forceRefresh);
        return new TemplatesBuilderReferenceData(_availableSwitches, _vhdxCatalogOptions.ToList(), _availableSwitchInfo);
    }

    private async Task EnsureAvailableVmSwitchesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _availableSwitches.Count > 0)
        {
            return;
        }

        _availableSwitchInfo = NormalizeSwitchInfo(await LoadAvailableVmSwitchInfoAsync());
        if (_availableSwitchInfo.Count > 0)
        {
            _availableSwitches = _availableSwitchInfo.Select(item => item.Name).ToList();
        }
        else
        {
            _availableSwitches = await LoadAvailableVmSwitchesAsync();
            _availableSwitchInfo = _availableSwitches
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => new V2AvailableSwitchInfo { Name = name.Trim(), SwitchType = "Unknown" })
                .ToList();
        }
    }

    private async Task EnsureVhdxCatalogOptionsAsync(bool forceRefresh)
    {
        if (!forceRefresh && _vhdxCatalogOptions.Count > 0)
        {
            return;
        }

        _vhdxCatalogOptions.Clear();
        var result = await _templatesCapabilityService.LoadVhdxCatalogOptionsAsync();
        _vhdxCatalogOptions.AddRange(result.Items.Select(item => new TemplateVhdxCatalogOption(
            item.Id,
            item.Path,
            item.OsName,
            item.OsVersion,
            item.Generation,
            item.Signature,
            item.BootstrapExpectedLocalUser)));
    }

    private async Task<IReadOnlyList<string>> LoadAvailableVmSwitchesAsync()
    {
        try
        {
            var switches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
            return switches
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<V2AvailableSwitchInfo>> LoadAvailableVmSwitchInfoAsync()
    {
        try
        {
            var result = await _assetsSwitchesCapabilityService.LoadAsync();
            return result.Items
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var item = group.First();
                    return new V2AvailableSwitchInfo
                    {
                        Name = item.Name.Trim(),
                        SwitchType = string.IsNullOrWhiteSpace(item.SwitchType) ? "Unknown" : item.SwitchType.Trim()
                    };
                })
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception)
        {
            return Array.Empty<V2AvailableSwitchInfo>();
        }
    }

    private static IReadOnlyList<V2AvailableSwitchInfo> NormalizeSwitchInfo(IReadOnlyList<V2AvailableSwitchInfo>? switches)
        => switches?
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var item = group.First();
                return new V2AvailableSwitchInfo
                {
                    Name = item.Name.Trim(),
                    SwitchType = string.IsNullOrWhiteSpace(item.SwitchType) ? "Unknown" : item.SwitchType.Trim()
                };
            })
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
}

/// <summary>
/// Reference data required by the Templates Editor: the set of host switches available for VM switch
/// assignments and the VHDX catalog options used to normalize and resolve base disk references.
/// </summary>
internal readonly record struct TemplatesEditorReferenceData(
    IReadOnlyList<string> AvailableVmSwitches,
    IReadOnlyList<TemplateVhdxCatalogOption> VhdxCatalogOptions);

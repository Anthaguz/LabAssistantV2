using LabAssistant.UITesting.Infrastructure.Cleanup;

namespace LabAssistant.UITesting.Infrastructure.ResourceProvider;

/// <summary>
/// Discover-existing provider. It seeds its own tagged, isolated resources
/// rather than borrowing the host's real ones: a tiny empty dynamic base disk
/// (a few MB) and a dedicated Internal switch. This keeps the run cheap, never
/// attaches a throwaway VM to the user's real lab switches, and means teardown
/// can prove that everything created was removed. An empty base disk cannot
/// boot, which also deliberately exercises the app's cleanup-on-failure path.
/// </summary>
public sealed class DiscoverExistingResourceProvider : ITestResourceProvider
{
    private readonly ResourceTagger _tagger;
    private readonly HyperVProbe _probe;
    private readonly CatalogSeeder _catalog;

    public DiscoverExistingResourceProvider(ResourceTagger tagger, HyperVProbe probe, CatalogSeeder catalog)
    {
        _tagger = tagger;
        _probe = probe;
        _catalog = catalog;
    }

    public ProvisionedResources Provision()
    {
        var switchName = EnsureDedicatedSwitch();
        var disk = _catalog.SeedTinyBaseDisk(_tagger);
        return new ProvisionedResources(
            switchName,
            SwitchCreatedByHarness: true,
            disk.CatalogId,
            disk.Path,
            disk.DisplayLabel);
    }

    private string EnsureDedicatedSwitch()
    {
        string name = _tagger.Name("switch");
        var existing = _probe.ListSwitchNames(name);
        if (existing.Count > 0)
        {
            return name; // already present from an earlier step in this run
        }

        PowerShellRunner.RunOrThrow(
            $"New-VMSwitch -Name '{Escape(name)}' -SwitchType Internal | Out-Null");
        return name;
    }

    private static string Escape(string value) => value.Replace("'", "''");
}

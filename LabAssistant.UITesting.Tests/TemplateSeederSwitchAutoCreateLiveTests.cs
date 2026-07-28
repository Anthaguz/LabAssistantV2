using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure fixture-rewrite logic of <see cref="TemplateSeeder.SeedSwitchAutoCreateLiveTemplate"/>
/// - the part that is testable without launching the app or Hyper-V. Proves the seeder stamps the
/// run-tagged identity + base image and writes the caller-supplied ghost switch name onto the bare NIC
/// (the "referenced but absent" switch the live deploy must create), so the tag-based gate can sweep the
/// switch if the runtime creates it.
/// </summary>
public sealed class TemplateSeederSwitchAutoCreateLiveTests : IDisposable
{
    private readonly string _appRoot;

    public TemplateSeederSwitchAutoCreateLiveTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-switchlive-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_appRoot))
            {
                Directory.Delete(_appRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of a temp dir; never fail a test on teardown.
        }
    }

    [Fact]
    public void SeedSwitchAutoCreateLiveTemplate_WritesGhostSwitchOntoBareNic()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "saltest");
        string ghostSwitch = tagger.Name("ghostswlive");

        var seeded = seeder.SeedSwitchAutoCreateLiveTemplate(tagger, baseDiskCatalogId: "disk-catalog-id-abc", ghostSwitchName: ghostSwitch);

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.True(tagger.IsHarnessOwned(seeded.VmName));

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();
        Assert.Equal(Path.Combine(_appRoot, "Templates", seeded.TemplateName + ".json"), seeded.FilePath);
        Assert.Equal(seeded.TemplateName, root["name"]!.GetValue<string>());

        // No lab networks: the missing switch is named directly on the bare NIC, the sole plan variable.
        Assert.Empty(root["labNetworks"]!.AsArray());

        var vm = root["vmTemplates"]!.AsArray().Single()!.AsObject();
        Assert.Equal(seeded.VmName, vm["name"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-abc", vm["vhdxId"]!.GetValue<string>());
        // A bare deploy: no credential slots, so the missing switch is the only unmet requirement.
        Assert.Null(vm["credentialSlots"]);

        var nic = vm["nics"]!.AsArray().Single()!.AsObject();
        Assert.Equal(ghostSwitch, nic["switchName"]!.GetValue<string>());
        // The ghost switch name carries the run prefix so the gate can sweep it if the runtime creates it.
        Assert.True(tagger.IsHarnessOwned(nic["switchName"]!.GetValue<string>()));
        Assert.Null(nic["networkId"]);
    }

    [Fact]
    public void SeedSwitchAutoCreateLiveTemplate_GivesEachRunADistinctVmName()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));

        var first = seeder.SeedSwitchAutoCreateLiveTemplate(new ResourceTagger("labh", "runA"), "disk-x", "ghost-a");
        var second = seeder.SeedSwitchAutoCreateLiveTemplate(new ResourceTagger("labh", "runB"), "disk-x", "ghost-b");

        Assert.NotEqual(first.VmName, second.VmName);
        Assert.NotEqual(first.TemplateName, second.TemplateName);
        Assert.NotEqual(first.FilePath, second.FilePath);
    }
}

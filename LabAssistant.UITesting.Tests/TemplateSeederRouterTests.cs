using System.Text.Json;
using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure fixture-rewrite logic of <see cref="TemplateSeeder.SeedRouterTemplate"/> - the part
/// that is testable without launching the app or Hyper-V. Proves the seeder rewrites only the LAN switch
/// (leaving the host Default Switch on the external NIC untouched), stamps the run-tagged identity, and
/// surfaces the LAN gateway IP the live scenario validates against.
/// </summary>
public sealed class TemplateSeederRouterTests : IDisposable
{
    private readonly string _appRoot;

    public TemplateSeederRouterTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-router-seed-" + Guid.NewGuid().ToString("N"));
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
    public void SeedRouterTemplate_RewritesLanSwitch_AndPreservesExternalDefaultSwitch()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "routertest");

        var seeded = seeder.SeedRouterTemplate(tagger, baseDiskCatalogId: "disk-catalog-id-123", lanSwitchName: "HarnessLanSwitch");

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.True(tagger.IsHarnessOwned(seeded.VmName));
        Assert.Equal("10.60.0.1", seeded.LanIpAddress);
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, seeded.LocalBootstrapSlotKey);

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();

        // The template file lives under the temp AppData Templates folder, named for the template.
        Assert.Equal(Path.Combine(_appRoot, "Templates", seeded.TemplateName + ".json"), seeded.FilePath);
        Assert.Equal(seeded.TemplateName, root["name"]!.GetValue<string>());

        var networks = root["labNetworks"]!.AsArray();
        var lan = networks.Single(n => n!["networkId"]!.GetValue<string>() == "lan-net")!.AsObject();
        var external = networks.Single(n => n!["networkId"]!.GetValue<string>() == "external-net")!.AsObject();
        Assert.Equal("HarnessLanSwitch", lan["switchName"]!.GetValue<string>());
        Assert.Equal("Default Switch", external["switchName"]!.GetValue<string>());

        var vm = root["vmTemplates"]!.AsArray().Single()!.AsObject();
        Assert.Equal(seeded.VmName, vm["name"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", vm["vhdxId"]!.GetValue<string>());
        Assert.Equal("Router", vm["topologyRole"]!.GetValue<string>());
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, vm["credentialSlots"]!["localBootstrap"]!.GetValue<string>());

        var lanNic = vm["nics"]!.AsArray().Single(n => n!["networkId"]!.GetValue<string>() == "lan-net")!.AsObject();
        Assert.Equal("10.60.0.1", lanNic["ipAddress"]!.GetValue<string>());
    }

    [Fact]
    public void SeedRouterTemplate_GivesEachRunADistinctVmName()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));

        var first = seeder.SeedRouterTemplate(new ResourceTagger("labh", "runA"), "disk-x", "sw-x");
        var second = seeder.SeedRouterTemplate(new ResourceTagger("labh", "runB"), "disk-x", "sw-x");

        Assert.NotEqual(first.VmName, second.VmName);
        Assert.NotEqual(first.TemplateName, second.TemplateName);

        // Each run's id round-trips uniquely, so their files never collide.
        Assert.NotEqual(first.FilePath, second.FilePath);
        var firstId = JsonNode.Parse(File.ReadAllText(first.FilePath))!["id"]!.GetValue<string>();
        var secondId = JsonNode.Parse(File.ReadAllText(second.FilePath))!["id"]!.GetValue<string>();
        Assert.NotEqual(firstId, secondId);
    }
}

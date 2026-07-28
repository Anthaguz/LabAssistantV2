using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure fixture-rewrite logic of <see cref="TemplateSeeder.SeedGuestStaticTemplate"/> and
/// <see cref="TemplateSeeder.SeedGuestStaticMultiVmTemplate"/> - the part that is testable without
/// launching the app or Hyper-V. Proves the seeders rewrite the lab-network switch and stamp the
/// run-tagged identity + base image while preserving the templated static IP(s) the live scenarios
/// validate against, and (for the multi-VM case) that each VM keeps its OWN distinct static IP.
/// </summary>
public sealed class TemplateSeederGuestStaticTests : IDisposable
{
    private readonly string _appRoot;

    public TemplateSeederGuestStaticTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-gueststatic-seed-" + Guid.NewGuid().ToString("N"));
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
    public void SeedGuestStaticTemplate_RewritesSwitch_AndSurfacesStaticIp()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "gstest");

        var seeded = seeder.SeedGuestStaticTemplate(tagger, baseDiskCatalogId: "disk-catalog-id-123", switchName: "HarnessInternalSwitch");

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.True(tagger.IsHarnessOwned(seeded.VmName));
        Assert.Equal("10.80.0.20", seeded.StaticIpAddress);
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, seeded.LocalBootstrapSlotKey);

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();

        Assert.Equal(Path.Combine(_appRoot, "Templates", seeded.TemplateName + ".json"), seeded.FilePath);
        Assert.Equal(seeded.TemplateName, root["name"]!.GetValue<string>());

        var network = root["labNetworks"]!.AsArray().Single()!.AsObject();
        Assert.Equal("HarnessInternalSwitch", network["switchName"]!.GetValue<string>());

        var vm = root["vmTemplates"]!.AsArray().Single()!.AsObject();
        Assert.Equal(seeded.VmName, vm["name"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", vm["vhdxId"]!.GetValue<string>());
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, vm["credentialSlots"]!["localBootstrap"]!.GetValue<string>());

        var nic = vm["nics"]!.AsArray().Single()!.AsObject();
        // The static IP binds by networkId (guest work), so the NIC must NOT name a switch directly.
        Assert.Equal("static-net", nic["networkId"]!.GetValue<string>());
        Assert.Equal("10.80.0.20", nic["ipAddress"]!.GetValue<string>());
        Assert.Null(nic["switchName"]);
    }

    [Fact]
    public void SeedGuestStaticTemplate_GivesEachRunADistinctVmName()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));

        var first = seeder.SeedGuestStaticTemplate(new ResourceTagger("labh", "runA"), "disk-x", "sw-x");
        var second = seeder.SeedGuestStaticTemplate(new ResourceTagger("labh", "runB"), "disk-x", "sw-x");

        Assert.NotEqual(first.VmName, second.VmName);
        Assert.NotEqual(first.TemplateName, second.TemplateName);
        Assert.NotEqual(first.FilePath, second.FilePath);
    }

    [Fact]
    public void SeedGuestStaticMultiVmTemplate_RewritesSwitch_AndGivesEachVmItsOwnDistinctIp()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "gsmtest");

        var seeded = seeder.SeedGuestStaticMultiVmTemplate(tagger, baseDiskCatalogId: "disk-catalog-id-999", switchName: "HarnessInternalSwitch");

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, seeded.LocalBootstrapSlotKey);
        Assert.Equal(2, seeded.Vms.Count);

        // Each VM is tagged, named distinctly, and holds its OWN distinct static IP.
        Assert.All(seeded.Vms, v => Assert.True(tagger.IsHarnessOwned(v.VmName)));
        Assert.Equal(seeded.Vms.Count, seeded.Vms.Select(v => v.VmName).Distinct().Count());
        Assert.Equal(new[] { "10.81.0.21", "10.81.0.22" }, seeded.Vms.Select(v => v.StaticIpAddress).ToArray());

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();
        var network = root["labNetworks"]!.AsArray().Single()!.AsObject();
        Assert.Equal("HarnessInternalSwitch", network["switchName"]!.GetValue<string>());

        var vms = root["vmTemplates"]!.AsArray();
        Assert.Equal(2, vms.Count);
        foreach (var node in vms)
        {
            var vm = node!.AsObject();
            Assert.Equal("disk-catalog-id-999", vm["vhdxId"]!.GetValue<string>());
            Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, vm["credentialSlots"]!["localBootstrap"]!.GetValue<string>());

            var nic = vm["nics"]!.AsArray().Single()!.AsObject();
            Assert.Equal("static-net", nic["networkId"]!.GetValue<string>());
            Assert.Null(nic["switchName"]);
        }

        // The seeded ground truth matches what was written to disk, VM-for-VM.
        var writtenIps = vms.Select(n => n!["nics"]!.AsArray().Single()!["ipAddress"]!.GetValue<string>()).ToArray();
        Assert.Equal(seeded.Vms.Select(v => v.StaticIpAddress).ToArray(), writtenIps);
        var writtenNames = vms.Select(n => n!["name"]!.GetValue<string>()).ToArray();
        Assert.Equal(seeded.Vms.Select(v => v.VmName).ToArray(), writtenNames);
    }
}

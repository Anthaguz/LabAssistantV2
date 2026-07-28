using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure fixture-rewrite logic of <see cref="TemplateSeeder.SeedForestTrustTemplate"/> - the
/// part that is testable without launching the app or Hyper-V. Proves the seeder rewrites the lab-network
/// switch, stamps a run-tagged identity + base image on BOTH DCs, stitches each DC's fresh vmId into the
/// firstDomainControllerVmId of the domain it owns (matched by domainId, not position), keeps the single
/// bidirectional Forest trust between the two roots, and surfaces the per-forest ground truth the live
/// scenario validates against, with source/target following the trust's own source/target domain ids.
/// </summary>
public sealed class TemplateSeederForestTrustTests : IDisposable
{
    private readonly string _appRoot;

    public TemplateSeederForestTrustTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-foresttrust-seed-" + Guid.NewGuid().ToString("N"));
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
    public void SeedForestTrustTemplate_RewritesSwitch_StitchesEachDc_AndKeepsBidirectionalTrust()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "fttest");

        var seeded = seeder.SeedForestTrustTemplate(tagger, baseDiskCatalogId: "disk-catalog-id-123", switchName: "HarnessInternalSwitch");

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.True(tagger.IsHarnessOwned(seeded.SourceDcVmName));
        Assert.True(tagger.IsHarnessOwned(seeded.TargetDcVmName));
        Assert.NotEqual(seeded.SourceDcVmName, seeded.TargetDcVmName);
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, seeded.LocalBootstrapSlotKey);

        // Source/target ground truth follows the trust's source/target domain ids: source = alpha, target = beta.
        Assert.Equal("alpha.lab", seeded.SourceDnsName);
        Assert.Equal("ALPHA", seeded.SourceNetBiosName);
        Assert.Equal("beta.lab", seeded.TargetDnsName);
        Assert.Equal("BETA", seeded.TargetNetBiosName);

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();
        Assert.Equal(Path.Combine(_appRoot, "Templates", seeded.TemplateName + ".json"), seeded.FilePath);
        Assert.Equal(seeded.TemplateName, root["name"]!.GetValue<string>());

        var network = root["labNetworks"]!.AsArray().Single()!.AsObject();
        Assert.Equal("HarnessInternalSwitch", network["switchName"]!.GetValue<string>());

        var vms = root["vmTemplates"]!.AsArray();
        Assert.Equal(2, vms.Count);

        var alphaVm = vms.Single(n => n!["domainId"]?.GetValue<string>() == "domain-alpha")!.AsObject();
        var betaVm = vms.Single(n => n!["domainId"]?.GetValue<string>() == "domain-beta")!.AsObject();

        // Both DCs are tagged, named as surfaced, and reference the same base image + bootstrap slot.
        Assert.Equal(seeded.SourceDcVmName, alphaVm["name"]!.GetValue<string>());
        Assert.Equal(seeded.TargetDcVmName, betaVm["name"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", alphaVm["vhdxId"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", betaVm["vhdxId"]!.GetValue<string>());
        Assert.Equal("FirstDomainController", alphaVm["topologyRole"]!.GetValue<string>());
        Assert.Equal("FirstDomainController", betaVm["topologyRole"]!.GetValue<string>());
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, alphaVm["credentialSlots"]!["localBootstrap"]!.GetValue<string>());
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, betaVm["credentialSlots"]!["localBootstrap"]!.GetValue<string>());

        // Each domain's firstDomainControllerVmId is stitched to the VM that owns THAT domain, so the
        // planner resolves each forest's promoting VM correctly even though both VMs share the role.
        var domains = root["directoryTopology"]!["domains"]!.AsArray();
        var alphaDomain = domains.Single(n => n!["domainId"]!.GetValue<string>() == "domain-alpha")!.AsObject();
        var betaDomain = domains.Single(n => n!["domainId"]!.GetValue<string>() == "domain-beta")!.AsObject();
        Assert.Equal(alphaVm["vmId"]!.GetValue<string>(), alphaDomain["firstDomainControllerVmId"]!.GetValue<string>());
        Assert.Equal(betaVm["vmId"]!.GetValue<string>(), betaDomain["firstDomainControllerVmId"]!.GetValue<string>());
        Assert.NotEqual(alphaVm["vmId"]!.GetValue<string>(), betaVm["vmId"]!.GetValue<string>());

        // Two forests, each rooted at its own domain.
        var forests = root["directoryTopology"]!["forests"]!.AsArray();
        Assert.Equal(2, forests.Count);
        Assert.Equal("domain-alpha", forests.Single(n => n!["forestId"]!.GetValue<string>() == "forest-alpha")!["rootDomainId"]!.GetValue<string>());
        Assert.Equal("domain-beta", forests.Single(n => n!["forestId"]!.GetValue<string>() == "forest-beta")!["rootDomainId"]!.GetValue<string>());

        // The single trust stays bidirectional Forest, alpha -> beta.
        var trust = root["directoryTopology"]!["trusts"]!.AsArray().Single()!.AsObject();
        Assert.Equal("domain-alpha", trust["sourceDomainId"]!.GetValue<string>());
        Assert.Equal("domain-beta", trust["targetDomainId"]!.GetValue<string>());
        Assert.Equal("Forest", trust["trustType"]!.GetValue<string>());
        Assert.Equal("Bidirectional", trust["direction"]!.GetValue<string>());

        // NICs bind by networkId (static IPs => guest work), so neither names a switch directly, and each
        // DC serves its own DNS on its own static address.
        var alphaNic = alphaVm["nics"]!.AsArray().Single()!.AsObject();
        var betaNic = betaVm["nics"]!.AsArray().Single()!.AsObject();
        Assert.Equal("trust-net", alphaNic["networkId"]!.GetValue<string>());
        Assert.Equal("trust-net", betaNic["networkId"]!.GetValue<string>());
        Assert.Null(alphaNic["switchName"]);
        Assert.Null(betaNic["switchName"]);
        Assert.Equal("10.0.0.10", alphaNic["ipAddress"]!.GetValue<string>());
        Assert.Equal("10.0.0.20", betaNic["ipAddress"]!.GetValue<string>());
        Assert.Equal("10.0.0.10", alphaNic["dnsServers"]!.AsArray().Single()!.GetValue<string>());
        Assert.Equal("10.0.0.20", betaNic["dnsServers"]!.AsArray().Single()!.GetValue<string>());
    }

    [Fact]
    public void SeedForestTrustTemplate_GivesEachRunADistinctIdentity()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));

        var first = seeder.SeedForestTrustTemplate(new ResourceTagger("labh", "runA"), "disk-x", "sw-x");
        var second = seeder.SeedForestTrustTemplate(new ResourceTagger("labh", "runB"), "disk-x", "sw-x");

        Assert.NotEqual(first.SourceDcVmName, second.SourceDcVmName);
        Assert.NotEqual(first.TargetDcVmName, second.TargetDcVmName);
        Assert.NotEqual(first.TemplateName, second.TemplateName);
        Assert.NotEqual(first.FilePath, second.FilePath);

        // Each run's ids round-trip uniquely, so their files and firstDomainControllerVmId never collide.
        var firstId = JsonNode.Parse(File.ReadAllText(first.FilePath))!["id"]!.GetValue<string>();
        var secondId = JsonNode.Parse(File.ReadAllText(second.FilePath))!["id"]!.GetValue<string>();
        Assert.NotEqual(firstId, secondId);
    }
}

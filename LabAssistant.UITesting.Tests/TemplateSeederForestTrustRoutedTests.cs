using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure fixture-rewrite logic of <see cref="TemplateSeeder.SeedForestTrustRoutedTemplate"/> -
/// the routed cross-forest capstone seed, testable without launching the app or Hyper-V. Proves the seeder
/// rewrites BOTH lab-network switches (alpha-net + beta-net) onto the caller's harness-owned names while
/// leaving external-net on the host Default Switch, stamps a run-tagged identity + base image on both DCs
/// and the router, stitches each DC's fresh vmId into the firstDomainControllerVmId of the domain it owns
/// (matched by domainId, not position), keeps the single bidirectional Forest trust, and surfaces the
/// per-forest ground truth - including each DC's static IP, its default gateway (the router's LAN leg on
/// its subnet), and its subnet - that the live route-hop assertion validates against.
/// </summary>
public sealed class TemplateSeederForestTrustRoutedTests : IDisposable
{
    private readonly string _appRoot;

    public TemplateSeederForestTrustRoutedTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-foresttrust-routed-seed-" + Guid.NewGuid().ToString("N"));
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
    public void SeedForestTrustRoutedTemplate_RewritesBothSwitches_TagsRouterAndDcs_AndSurfacesRoutedGroundTruth()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "ftrtest");

        var seeded = seeder.SeedForestTrustRoutedTemplate(
            tagger,
            baseDiskCatalogId: "disk-catalog-id-123",
            switchAName: "HarnessAlphaSwitch",
            switchBName: "HarnessBetaSwitch");

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.True(tagger.IsHarnessOwned(seeded.RouterVmName));
        Assert.True(tagger.IsHarnessOwned(seeded.SourceDcVmName));
        Assert.True(tagger.IsHarnessOwned(seeded.TargetDcVmName));
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, seeded.LocalBootstrapSlotKey);

        // The three VM names are distinct so the run-tag sweep removes exactly three VMs.
        Assert.NotEqual(seeded.SourceDcVmName, seeded.TargetDcVmName);
        Assert.NotEqual(seeded.RouterVmName, seeded.SourceDcVmName);
        Assert.NotEqual(seeded.RouterVmName, seeded.TargetDcVmName);

        // Source/target follow the trust's source/target domain ids: source = alpha, target = beta.
        Assert.Equal("alpha.lab", seeded.SourceDnsName);
        Assert.Equal("ALPHA", seeded.SourceNetBiosName);
        Assert.Equal("beta.lab", seeded.TargetDnsName);
        Assert.Equal("BETA", seeded.TargetNetBiosName);

        // Each side's routed ground truth: static IP, gateway (the router's LAN leg on that subnet), subnet.
        Assert.Equal("10.70.0.10", seeded.SourceDcIpAddress);
        Assert.Equal("10.70.0.1", seeded.SourceGatewayIpAddress);
        Assert.Equal("10.70.0.0/24", seeded.SourceSubnet);
        Assert.Equal("10.71.0.10", seeded.TargetDcIpAddress);
        Assert.Equal("10.71.0.1", seeded.TargetGatewayIpAddress);
        Assert.Equal("10.71.0.0/24", seeded.TargetSubnet);

        // The surfaced switch names echo what was passed, so the no-orphans sweep can assert both removed.
        Assert.Equal("HarnessAlphaSwitch", seeded.SwitchAName);
        Assert.Equal("HarnessBetaSwitch", seeded.SwitchBName);

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();
        Assert.Equal(seeded.TemplateName, root["name"]!.GetValue<string>());

        // Both lab switches are rewritten; external-net keeps the host Default Switch (never swept).
        var networks = root["labNetworks"]!.AsArray();
        var alphaNet = networks.Single(n => n!["networkId"]!.GetValue<string>() == "alpha-net")!.AsObject();
        var betaNet = networks.Single(n => n!["networkId"]!.GetValue<string>() == "beta-net")!.AsObject();
        var externalNet = networks.Single(n => n!["networkId"]!.GetValue<string>() == "external-net")!.AsObject();
        Assert.Equal("HarnessAlphaSwitch", alphaNet["switchName"]!.GetValue<string>());
        Assert.Equal("HarnessBetaSwitch", betaNet["switchName"]!.GetValue<string>());
        Assert.Equal("Default Switch", externalNet["switchName"]!.GetValue<string>());

        var vms = root["vmTemplates"]!.AsArray();
        Assert.Equal(3, vms.Count);

        var alphaVm = vms.Single(n => n!["domainId"]?.GetValue<string>() == "domain-alpha")!.AsObject();
        var betaVm = vms.Single(n => n!["domainId"]?.GetValue<string>() == "domain-beta")!.AsObject();
        var routerVm = vms.Single(n => n!["topologyRole"]?.GetValue<string>() == "Router")!.AsObject();

        // All three VMs are tagged, named as surfaced, and reference the same base image + bootstrap slot.
        Assert.Equal(seeded.SourceDcVmName, alphaVm["name"]!.GetValue<string>());
        Assert.Equal(seeded.TargetDcVmName, betaVm["name"]!.GetValue<string>());
        Assert.Equal(seeded.RouterVmName, routerVm["name"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", alphaVm["vhdxId"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", betaVm["vhdxId"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", routerVm["vhdxId"]!.GetValue<string>());

        // Each domain's firstDomainControllerVmId is stitched to the VM that owns THAT domain.
        var domains = root["directoryTopology"]!["domains"]!.AsArray();
        var alphaDomain = domains.Single(n => n!["domainId"]!.GetValue<string>() == "domain-alpha")!.AsObject();
        var betaDomain = domains.Single(n => n!["domainId"]!.GetValue<string>() == "domain-beta")!.AsObject();
        Assert.Equal(alphaVm["vmId"]!.GetValue<string>(), alphaDomain["firstDomainControllerVmId"]!.GetValue<string>());
        Assert.Equal(betaVm["vmId"]!.GetValue<string>(), betaDomain["firstDomainControllerVmId"]!.GetValue<string>());

        // The router carries a LAN leg on each subnet plus an external egress NIC (the routed bridge).
        var routerNics = routerVm["nics"]!.AsArray();
        Assert.Equal(3, routerNics.Count);
        Assert.Contains(routerNics, n => n!["networkId"]!.GetValue<string>() == "alpha-net" && n["ipAddress"]?.GetValue<string>() == "10.70.0.1");
        Assert.Contains(routerNics, n => n!["networkId"]!.GetValue<string>() == "beta-net" && n["ipAddress"]?.GetValue<string>() == "10.71.0.1");
        Assert.Contains(routerNics, n => n!["networkId"]!.GetValue<string>() == "external-net");

        // Each DC binds by networkId (static IP => guest work) with the router's leg as its default gateway.
        var alphaNic = alphaVm["nics"]!.AsArray().Single()!.AsObject();
        var betaNic = betaVm["nics"]!.AsArray().Single()!.AsObject();
        Assert.Equal("alpha-net", alphaNic["networkId"]!.GetValue<string>());
        Assert.Equal("beta-net", betaNic["networkId"]!.GetValue<string>());
        Assert.Null(alphaNic["switchName"]);
        Assert.Null(betaNic["switchName"]);
        Assert.Equal("10.70.0.10", alphaNic["ipAddress"]!.GetValue<string>());
        Assert.Equal("10.71.0.10", betaNic["ipAddress"]!.GetValue<string>());
        Assert.Equal("10.70.0.1", alphaNic["defaultGateway"]!.GetValue<string>());
        Assert.Equal("10.71.0.1", betaNic["defaultGateway"]!.GetValue<string>());

        // The single trust stays bidirectional Forest, alpha -> beta.
        var trust = root["directoryTopology"]!["trusts"]!.AsArray().Single()!.AsObject();
        Assert.Equal("domain-alpha", trust["sourceDomainId"]!.GetValue<string>());
        Assert.Equal("domain-beta", trust["targetDomainId"]!.GetValue<string>());
        Assert.Equal("Forest", trust["trustType"]!.GetValue<string>());
        Assert.Equal("Bidirectional", trust["direction"]!.GetValue<string>());
    }

    [Fact]
    public void SeedForestTrustRoutedTemplate_GivesEachRunADistinctIdentity()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));

        var first = seeder.SeedForestTrustRoutedTemplate(new ResourceTagger("labh", "runA"), "disk-x", "sw-a", "sw-b");
        var second = seeder.SeedForestTrustRoutedTemplate(new ResourceTagger("labh", "runB"), "disk-x", "sw-a", "sw-b");

        Assert.NotEqual(first.RouterVmName, second.RouterVmName);
        Assert.NotEqual(first.SourceDcVmName, second.SourceDcVmName);
        Assert.NotEqual(first.TargetDcVmName, second.TargetDcVmName);
        Assert.NotEqual(first.TemplateName, second.TemplateName);
        Assert.NotEqual(first.FilePath, second.FilePath);

        var firstId = JsonNode.Parse(File.ReadAllText(first.FilePath))!["id"]!.GetValue<string>();
        var secondId = JsonNode.Parse(File.ReadAllText(second.FilePath))!["id"]!.GetValue<string>();
        Assert.NotEqual(firstId, secondId);
    }
}

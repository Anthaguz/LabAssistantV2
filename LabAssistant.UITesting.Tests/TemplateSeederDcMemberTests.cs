using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the pure fixture-rewrite logic of <see cref="TemplateSeeder.SeedDomainControllerMemberTemplate"/> -
/// the part that is testable without launching the app or Hyper-V. Proves the seeder rewrites the
/// lab-network switch, stamps the run-tagged identity + base image on BOTH VMs, stitches the DC's fresh
/// vmId into firstDomainControllerVmId, keeps the DomainMember VM authored to join the same domain, and
/// surfaces the forest/domain ground truth the live scenario validates against.
/// </summary>
public sealed class TemplateSeederDcMemberTests : IDisposable
{
    private readonly string _appRoot;

    public TemplateSeederDcMemberTests()
    {
        _appRoot = Path.Combine(Path.GetTempPath(), "la-dcmember-seed-" + Guid.NewGuid().ToString("N"));
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
    public void SeedDomainControllerMemberTemplate_RewritesSwitch_StitchesDc_AndAuthorsMemberJoin()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));
        var tagger = new ResourceTagger("labh", "dcmtest");

        var seeded = seeder.SeedDomainControllerMemberTemplate(tagger, baseDiskCatalogId: "disk-catalog-id-123", switchName: "HarnessInternalSwitch");

        Assert.True(File.Exists(seeded.FilePath));
        Assert.True(tagger.IsHarnessOwned(seeded.TemplateName));
        Assert.True(tagger.IsHarnessOwned(seeded.DcVmName));
        Assert.True(tagger.IsHarnessOwned(seeded.MemberVmName));
        Assert.NotEqual(seeded.DcVmName, seeded.MemberVmName);
        Assert.Equal("smoke.lab", seeded.DnsName);
        Assert.Equal("SMOKE", seeded.NetBiosName);
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, seeded.LocalBootstrapSlotKey);

        var root = JsonNode.Parse(File.ReadAllText(seeded.FilePath))!.AsObject();

        Assert.Equal(Path.Combine(_appRoot, "Templates", seeded.TemplateName + ".json"), seeded.FilePath);
        Assert.Equal(seeded.TemplateName, root["name"]!.GetValue<string>());

        var network = root["labNetworks"]!.AsArray().Single()!.AsObject();
        Assert.Equal("HarnessInternalSwitch", network["switchName"]!.GetValue<string>());

        var vms = root["vmTemplates"]!.AsArray();
        Assert.Equal(2, vms.Count);

        var dc = vms.Single(n => n!["topologyRole"]?.GetValue<string>() == "FirstDomainController")!.AsObject();
        var member = vms.Single(n => n!["membershipMode"]?.GetValue<string>() == "DomainMember")!.AsObject();

        // Both VMs are tagged, named as surfaced, and reference the same base image.
        Assert.Equal(seeded.DcVmName, dc["name"]!.GetValue<string>());
        Assert.Equal(seeded.MemberVmName, member["name"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", dc["vhdxId"]!.GetValue<string>());
        Assert.Equal("disk-catalog-id-123", member["vhdxId"]!.GetValue<string>());
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, dc["credentialSlots"]!["localBootstrap"]!.GetValue<string>());
        Assert.Equal(TemplateSeeder.DcLocalBootstrapSlotKey, member["credentialSlots"]!["localBootstrap"]!.GetValue<string>());

        // The DC's fresh vmId is stitched into the domain's firstDomainControllerVmId so the planner can
        // resolve which VM owns the root domain the member joins.
        var dcVmId = dc["vmId"]!.GetValue<string>();
        var domain = root["directoryTopology"]!["domains"]!.AsArray().Single()!.AsObject();
        Assert.Equal(dcVmId, domain["firstDomainControllerVmId"]!.GetValue<string>());

        // Both VMs are bound to the same domain; the member joins the DC's forest.
        Assert.Equal("domain-smoke", dc["domainId"]!.GetValue<string>());
        Assert.Equal("domain-smoke", member["domainId"]!.GetValue<string>());
        Assert.Equal("domain-smoke", domain["domainId"]!.GetValue<string>());

        // NICs bind by networkId (static IPs => guest work), so neither names a switch directly, and the
        // member's DNS points at the DC so it can locate the domain to join.
        var dcNic = dc["nics"]!.AsArray().Single()!.AsObject();
        var memberNic = member["nics"]!.AsArray().Single()!.AsObject();
        Assert.Equal("smoke-net", dcNic["networkId"]!.GetValue<string>());
        Assert.Equal("smoke-net", memberNic["networkId"]!.GetValue<string>());
        Assert.Null(dcNic["switchName"]);
        Assert.Null(memberNic["switchName"]);
        Assert.Equal("10.0.0.10", dcNic["ipAddress"]!.GetValue<string>());
        Assert.Equal("10.0.0.20", memberNic["ipAddress"]!.GetValue<string>());
        Assert.Equal("10.0.0.10", memberNic["dnsServers"]!.AsArray().Single()!.GetValue<string>());
    }

    [Fact]
    public void SeedDomainControllerMemberTemplate_GivesEachRunADistinctIdentity()
    {
        var seeder = new TemplateSeeder(new AppDataLocations(_appRoot));

        var first = seeder.SeedDomainControllerMemberTemplate(new ResourceTagger("labh", "runA"), "disk-x", "sw-x");
        var second = seeder.SeedDomainControllerMemberTemplate(new ResourceTagger("labh", "runB"), "disk-x", "sw-x");

        Assert.NotEqual(first.DcVmName, second.DcVmName);
        Assert.NotEqual(first.MemberVmName, second.MemberVmName);
        Assert.NotEqual(first.TemplateName, second.TemplateName);
        Assert.NotEqual(first.FilePath, second.FilePath);

        // Each run's ids round-trip uniquely, so their files and firstDomainControllerVmId never collide.
        var firstId = JsonNode.Parse(File.ReadAllText(first.FilePath))!["id"]!.GetValue<string>();
        var secondId = JsonNode.Parse(File.ReadAllText(second.FilePath))!["id"]!.GetValue<string>();
        Assert.NotEqual(firstId, secondId);
    }
}

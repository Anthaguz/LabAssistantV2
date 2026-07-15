using LabAssistant.Business.Planning;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Locks the credential-reuse contract for the default (suggested) Builder template: it authors ONLY the local
/// bootstrap credential slot, so a default DC + domain-member lab deploys with a SINGLE registered credential.
/// The planner's bootstrap fallback resolves domain admin / join / DSRM from that one slot, so registering just
/// the base-disk bootstrap credential is enough to plan (and therefore deploy) a default lab.
/// Runtime-independent: authors through the real mapper and runs the real V2 planner, no Hyper-V.
/// </summary>
public sealed class TemplatesBuilderSuggestedCredentialReuseTests
{
    private const string BootstrapSlot = "slot-local";

    [Fact]
    public void SuggestedTemplate_BindsOnlyBootstrapCredentialSlotOnEveryVm()
    {
        var template = BuildSuggestedTemplate();

        Assert.NotEmpty(template.VmTemplates);
        foreach (var vm in template.VmTemplates)
        {
            var slots = vm.CredentialSlots;
            Assert.NotNull(slots);
            Assert.Equal(BootstrapSlot, slots!.LocalBootstrap);

            // Domain credentials are intentionally left unbound so the planner reuses the bootstrap slot.
            Assert.True(string.IsNullOrEmpty(slots.DomainAdmin), $"{vm.Name} should not author a domain admin slot.");
            Assert.True(string.IsNullOrEmpty(slots.DomainJoin), $"{vm.Name} should not author a domain join slot.");
            Assert.True(string.IsNullOrEmpty(slots.Dsrm), $"{vm.Name} should not author a DSRM slot.");
            Assert.True(string.IsNullOrEmpty(slots.ParentDomainAdmin), $"{vm.Name} should not author a parent domain admin slot.");
        }
    }

    [Fact]
    public async Task SuggestedTemplate_PlansCleanly_WithOnlyBootstrapCredentialResolved()
    {
        var template = BuildSuggestedTemplate();
        var request = BuildPlanRequestWithOnlyBootstrapResolved(template);

        var result = await new V2PlanningCapabilityService().BuildPlanAsync(request);

        Assert.True(
            result.Success,
            "Suggested lab must plan with only the bootstrap credential registered. Issues: " +
            string.Join("; ", result.Issues.Select(issue => $"{issue.Code}:{issue.Message}")));
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "credential-slot-missing");
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "credential-slot-unresolved");
        Assert.DoesNotContain(
            result.UnresolvedRequirements,
            requirement => requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot);
    }

    private static LabTemplate BuildSuggestedTemplate()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        var draft = TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData);
        var build = TemplatesBuilderDraftMapper.BuildDocument(draft, "suggested-credential-reuse", 1, "1.0.0", sourceFilePath: null);

        Assert.NotNull(build.Document);
        return build.Document!.Template;
    }

    private static V2PlanBuildRequest BuildPlanRequestWithOnlyBootstrapResolved(LabTemplate template)
    {
        // Every base disk carries the SAME bootstrap slot, mirroring the base-disk catalog contract.
        var catalogItems = template.VmTemplates
            .Select(vm => vm.VhdxId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => CreateCatalogItem(id!, BootstrapSlot))
            .ToList();

        // Provide exactly the switches the template declares so switch resolution never blocks the plan.
        var switches = (template.LabNetworks ?? [])
            .Where(network => !string.IsNullOrWhiteSpace(network.SwitchName))
            .Select(network => new V2AvailableSwitchInfo
            {
                Name = network.SwitchName!,
                SwitchType = string.IsNullOrWhiteSpace(network.SwitchType) ? V2SwitchTypeCatalog.Internal : network.SwitchType!
            })
            .ToList();

        return new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = catalogItems,
            AvailableSwitchNames = switches.Select(info => info.Name).ToList(),
            AvailableSwitches = switches,
            // The ONLY registered credential is the base-disk bootstrap slot.
            ResolvedCredentialSlotKeys = [BootstrapSlot],
            DefaultDeploymentProfile = "Balanced"
        };
    }

    private static VhdxCatalogItem CreateCatalogItem(string id, string slotRef)
        => new()
        {
            Id = id,
            Path = $@"C:\base\{id}.vhdx",
            OsName = "Windows Server",
            OsVersion = "2022",
            Generation = 2,
            Signature = $"{id}-sig",
            BootstrapProfile = new VhdxBootstrapProfile
            {
                ExpectedLocalUser = "Administrator",
                LocalCredentialSlotRef = slotRef,
                GuestOsFamily = "windows",
                GuestTransport = "powershell-direct"
            }
        };
}

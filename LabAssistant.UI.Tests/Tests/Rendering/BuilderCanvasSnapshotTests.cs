using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests.Rendering;

/// <summary>
/// Env-gated developer aid that renders the directory-topology canvas to PNGs so canvas layout can be eyeballed
/// without launching WinUI. It is inert during a normal test run: it only produces images when the
/// <c>CANVAS_SNAPSHOT_DIR</c> environment variable points at an output directory. Every scenario is driven
/// through the real projector, layout, geometry, and canvas view model, so the pictures reflect exactly what
/// the running app would position (minus WinUI chrome and theming).
/// </summary>
public sealed class BuilderCanvasSnapshotTests
{
    [Fact]
    public void RenderTopologySnapshots()
    {
        var outputDir = Environment.GetEnvironmentVariable("CANVAS_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            return;
        }

        foreach (var (name, caption, draft, kind, index) in Scenarios())
        {
            var projection = TemplatesBuilderDirectoryTopologyProjector.Project(draft, kind, index);
            var canvas = new BuilderTopologyCanvasViewModel(projection, (_, _) => { });
            BuilderCanvasSnapshotRenderer.Render(canvas, caption, Path.Combine(outputDir, $"{name}.png"));
        }
    }

    private static IEnumerable<(string Name, string Caption, TemplatesBuilderDraftSnapshot Draft, BuilderForestDomainResourceKind Kind, int Index)> Scenarios()
    {
        yield return (
            "01-single-forest-two-children",
            "Single forest, root with two children (root domain selected)",
            Draft(
                [F("contoso", "d-contoso")],
                [
                    D("d-contoso", "contoso.lab", "CONTOSO", "contoso", V2DomainRelationKind.Root),
                    D("d-sales", "sales.contoso.lab", "SALES", "contoso", V2DomainRelationKind.Child, "d-contoso"),
                    D("d-eng", "eng.contoso.lab", "ENG", "contoso", V2DomainRelationKind.Child, "d-contoso")
                ]),
            BuilderForestDomainResourceKind.Domain,
            0);

        yield return (
            "02-two-forests-tree-and-child",
            "Two forests: contoso (root + child + tree) and fabrikam (root)",
            Draft(
                [F("contoso", "d-contoso"), F("fabrikam", "d-fabrikam")],
                [
                    D("d-contoso", "contoso.lab", "CONTOSO", "contoso", V2DomainRelationKind.Root),
                    D("d-child", "child.contoso.lab", "CHILD", "contoso", V2DomainRelationKind.Child, "d-contoso"),
                    D("d-tree", "tailspin.lab", "TAILSPIN", "contoso", V2DomainRelationKind.Tree),
                    D("d-fabrikam", "fabrikam.lab", "FABRIKAM", "fabrikam", V2DomainRelationKind.Root)
                ]),
            BuilderForestDomainResourceKind.Forest,
            1);

        yield return (
            "03-deep-child-chain",
            "Deep chain: root -> child -> grandchild -> great-grandchild",
            Draft(
                [F("contoso", "d0")],
                [
                    D("d0", "contoso.lab", "CONTOSO", "contoso", V2DomainRelationKind.Root),
                    D("d1", "a.contoso.lab", "A", "contoso", V2DomainRelationKind.Child, "d0"),
                    D("d2", "b.a.contoso.lab", "B", "contoso", V2DomainRelationKind.Child, "d1"),
                    D("d3", "c.b.a.contoso.lab", "C", "contoso", V2DomainRelationKind.Child, "d2")
                ]),
            BuilderForestDomainResourceKind.Domain,
            2);

        yield return (
            "04-wide-four-children",
            "Wide: root centered over four children",
            Draft(
                [F("contoso", "d0")],
                [
                    D("d0", "contoso.lab", "CONTOSO", "contoso", V2DomainRelationKind.Root),
                    D("d1", "one.contoso.lab", "ONE", "contoso", V2DomainRelationKind.Child, "d0"),
                    D("d2", "two.contoso.lab", "TWO", "contoso", V2DomainRelationKind.Child, "d0"),
                    D("d3", "three.contoso.lab", "THREE", "contoso", V2DomainRelationKind.Child, "d0"),
                    D("d4", "four.contoso.lab", "FOUR", "contoso", V2DomainRelationKind.Child, "d0")
                ]),
            BuilderForestDomainResourceKind.Forest,
            0);

        yield return (
            "05-missing-and-unassigned",
            "Invalid rows stay visible: missing-parent child + unassigned domain",
            Draft(
                [F("lab", "d-missing-root")],
                [
                    D("d-orphan", "orphan.lab", "ORPHAN", "lab", V2DomainRelationKind.Child, "d-missing-parent"),
                    D("d-unassigned", "unassigned.lab", "UNASSIGNED", "forest-missing", V2DomainRelationKind.Root)
                ]),
            BuilderForestDomainResourceKind.Domain,
            0);

        yield return (
            "06-three-forests",
            "Three forests side by side, each with a couple domains",
            Draft(
                [F("contoso", "d-c"), F("fabrikam", "d-f"), F("adventure", "d-a")],
                [
                    D("d-c", "contoso.lab", "CONTOSO", "contoso", V2DomainRelationKind.Root),
                    D("d-c1", "hr.contoso.lab", "HR", "contoso", V2DomainRelationKind.Child, "d-c"),
                    D("d-f", "fabrikam.lab", "FABRIKAM", "fabrikam", V2DomainRelationKind.Root),
                    D("d-f1", "eu.fabrikam.lab", "EU", "fabrikam", V2DomainRelationKind.Child, "d-f"),
                    D("d-a", "adventure.lab", "ADVENTURE", "adventure", V2DomainRelationKind.Root),
                    D("d-a2", "tree.lab", "TREE", "adventure", V2DomainRelationKind.Tree)
                ]),
            BuilderForestDomainResourceKind.Forest,
            0);

        yield return (
            "07-standalone-container",
            "Standalone container (router + root CA) sits beside the contoso forest",
            Draft(
                [F("contoso", "d-contoso")],
                [D("d-contoso", "contoso.lab", "CONTOSO", "contoso", V2DomainRelationKind.Root)],
                [
                    Vm("vm-router", "lab-router"),
                    Vm("vm-rootca", "lab-rootca")
                ]),
            BuilderForestDomainResourceKind.Standalone,
            0);

        // Phase 1 scenarios are driven through the REAL authoring engine so the pictures reflect exactly what
        // the add-forest / add-child / add-tree gestures produce (born-with-DC drafts, forest-name-follows-root).
        var empty = Draft([], []);

        var addForest = TemplatesBuilderTopologyAuthoring.AddForest(empty);
        yield return (
            "p1-01-engine-add-forest",
            "Phase 1 engine: AddForest -> forest label follows root domain DNS name",
            addForest.Draft,
            addForest.SelectedKind,
            addForest.SelectedIndex);

        var forestId = addForest.Draft.Forests[0].ForestId;
        var rootDomainId = addForest.Draft.Forests[0].RootDomainId;

        var addChild = TemplatesBuilderTopologyAuthoring.AddChildDomain(addForest.Draft, rootDomainId);
        yield return (
            "p1-02-engine-add-child",
            "Phase 1 engine: AddChildDomain under root (child born with its own DC)",
            addChild.Draft,
            addChild.SelectedKind,
            addChild.SelectedIndex);

        var addTree = TemplatesBuilderTopologyAuthoring.AddTree(addForest.Draft, forestId);
        yield return (
            "p1-03-engine-add-tree",
            "Phase 1 engine: AddTree in active forest (parentless top-level namespace)",
            addTree.Draft,
            addTree.SelectedKind,
            addTree.SelectedIndex);

        var secondForest = TemplatesBuilderTopologyAuthoring.AddForest(addForest.Draft);
        yield return (
            "p1-04-engine-two-forests",
            "Phase 1 engine: two forests (contoso.lab + fabrikam.lab) each named after its root",
            secondForest.Draft,
            BuilderForestDomainResourceKind.Forest,
            0);
    }

    private static TemplatesBuilderForestDraft F(string forestId, string rootDomainId)
        => new(forestId, rootDomainId);

    private static TemplatesBuilderDomainDraft D(
        string domainId,
        string dnsName,
        string netbios,
        string forestId,
        V2DomainRelationKind relation,
        string parentDomainId = "")
        => new(domainId, dnsName, netbios, forestId, relation.ToString(), parentDomainId);

    private static TemplatesBuilderVmDraft Vm(
        string vmId,
        string name,
        string membershipMode = V2MembershipModeCatalog.Standalone,
        string domainId = "",
        bool isDomainController = false)
        => new(vmId, name, "4096", "2", string.Empty, membershipMode, domainId, isDomainController, default, []);

    private static TemplatesBuilderDraftSnapshot Draft(
        IReadOnlyList<TemplatesBuilderForestDraft> forests,
        IReadOnlyList<TemplatesBuilderDomainDraft> domains,
        IReadOnlyList<TemplatesBuilderVmDraft>? vms = null)
        => new(
            "Snapshot draft",
            "Draft for canvas snapshot rendering.",
            "Balanced",
            [],
            [],
            forests,
            domains,
            vms ?? [],
            false);
}

using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Deploy;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class DeployMultiSwitchBehaviorTests
{
    [Fact]
    public void DeployContextBuilder_PreservesOrderedAssignedSwitches_AndFirstSwitchCompatibilityField()
    {
        var template = new LabTemplate
        {
            VmTemplates =
            [
                new VmTemplate
                {
                    Name = "DC1",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "base-1",
                    SwitchNames = ["Lab-A", "Lab-B"]
                }
            ]
        };

        var result = DeployContextBuilder.Build(
            template,
            new AppSettings { VmBasePath = @"C:\Labs" },
            [new VhdxCatalogItem { Id = "base-1", Path = @"C:\Base\base-1.vhdx" }],
            ["Lab-A", "Lab-B"]);

        var vm = Assert.Single(result.MultiVmContext.VmContexts);
        Assert.Equal(["Lab-A", "Lab-B"], vm.VirtualSwitchNames);
        Assert.Equal("Lab-A", vm.VirtualSwitchName);
        Assert.DoesNotContain(result.CompatibilityIssues, issue => issue.IsBlocking);
    }

    [Fact]
    public void DeployContextBuilder_BlocksWhenAnyAssignedSwitchIsMissing()
    {
        var template = new LabTemplate
        {
            VmTemplates =
            [
                new VmTemplate
                {
                    Name = "APP1",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "base-1",
                    SwitchNames = ["Lab-A", "Missing-B"]
                }
            ]
        };

        var result = DeployContextBuilder.Build(
            template,
            new AppSettings { VmBasePath = @"C:\Labs" },
            [new VhdxCatalogItem { Id = "base-1", Path = @"C:\Base\base-1.vhdx" }],
            ["Lab-A"]);

        var vm = Assert.Single(result.MultiVmContext.VmContexts);
        Assert.Equal(["Lab-A"], vm.VirtualSwitchNames);
        var blockingIssue = Assert.Single(result.CompatibilityIssues.Where(issue => issue.IsBlocking));
        Assert.Contains("Missing-B", blockingIssue.Message);
    }

    [Fact]
    public void DeployContextBuilder_RejectsV2TemplateRouting()
    {
        var template = new LabTemplate
        {
            SchemaVersion = "2.0.0",
            ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning,
            VmTemplates =
            [
                new VmTemplate
                {
                    Name = "DC1",
                    MemoryMb = 4096,
                    CpuCount = 2,
                    VhdxId = "base-1"
                }
            ]
        };

        var ex = Assert.Throws<InvalidOperationException>(() => DeployContextBuilder.Build(
            template,
            new AppSettings { VmBasePath = @"C:\Labs" },
            [new VhdxCatalogItem { Id = "base-1", Path = @"C:\Base\base-1.vhdx" }],
            ["Lab-A"]));

        Assert.Contains("must be routed through the V2 planner", ex.Message);
    }
}

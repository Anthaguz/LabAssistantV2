using System.Linq;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests;

public class DeploymentPlanBuilderTests
{
    [Fact]
    public void Build_CreatesStepsPerVm()
    {
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            Version = "1.0.0",
            VmTemplates =
            {
                new VmTemplate { Name = "vm1", MemoryMb = 1024, CpuCount = 1, VhdPath = "C:/a.vhdx" },
                new VmTemplate { Name = "vm2", MemoryMb = 2048, CpuCount = 2, VhdPath = "C:/b.vhdx" }
            }
        };

        var builder = new DeploymentPlanBuilder();
        var plan = builder.Build(template);

        var expectedStepsPerVm = 9;
        Assert.Equal(expectedStepsPerVm * 2, plan.Steps.Count);
        Assert.Equal("vm1", plan.Steps.First().VmName);
        Assert.Equal("CheckHyperV", plan.Steps.First().Name);
    }
}

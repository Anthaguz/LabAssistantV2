using System.Collections.Generic;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class DryRunDeploymentPipelineTests
{
    private sealed class TestLogger : IDryRunLogger
    {
        public List<string> Entries { get; } = new();

        public void Log(string message)
        {
            Entries.Add(message);
        }
    }

    [Fact]
    public void Run_ReturnsError_WhenNoVmTemplates()
    {
        var logger = new TestLogger();
        var pipeline = new DryRunDeploymentPipeline(new DeploymentPlanBuilder(), logger);
        var template = new LabTemplate { Id = "lab", Name = "Lab", Version = "v0" };

        var result = pipeline.Run(template);

        Assert.Contains("No VM templates found.", result.Errors);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Run_LogsEachStep()
    {
        var logger = new TestLogger();
        var pipeline = new DryRunDeploymentPipeline(new DeploymentPlanBuilder(), logger);
        var template = new LabTemplate
        {
            Id = "lab",
            Name = "Lab",
            Version = "v0",
            VmTemplates =
            {
                new VmTemplate { Name = "vm1", MemoryMb = 1024, CpuCount = 1, VhdPath = "C:/base.vhdx" }
            }
        };

        var result = pipeline.Run(template);

        Assert.NotEmpty(result.Logs);
        Assert.Equal(result.Logs.Count, logger.Entries.Count);
    }
}

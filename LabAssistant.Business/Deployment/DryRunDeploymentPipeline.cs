using System;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Deployment;

public class DryRunDeploymentPipeline
{
    private readonly DeploymentPlanBuilder _planBuilder;

    public DryRunDeploymentPipeline(DeploymentPlanBuilder planBuilder)
    {
        _planBuilder = planBuilder;
    }

    public DryRunDeploymentResult Run(LabTemplate template)
    {
        var plan = _planBuilder.Build(template);
        var result = new DryRunDeploymentResult(plan);

        foreach (var step in plan.Steps)
        {
            result.Logs.Add($"{DateTime.UtcNow:O} {step.VmName} - {step.Name}");
        }

        return result;
    }
}

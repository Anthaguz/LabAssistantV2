using System;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Deployment;

public class DryRunDeploymentPipeline
{
    private readonly DeploymentPlanBuilder _planBuilder;
    private readonly IDryRunLogger _logger;

    public DryRunDeploymentPipeline(DeploymentPlanBuilder planBuilder, IDryRunLogger logger)
    {
        _planBuilder = planBuilder;
        _logger = logger;
    }

    public DryRunDeploymentResult Run(LabTemplate template)
    {
        var plan = _planBuilder.Build(template);
        var result = new DryRunDeploymentResult(plan);

        foreach (var step in plan.Steps)
        {
            var entry = $"{DateTime.UtcNow:O} {step.VmName} - {step.Name}";
            _logger.Log(entry);
            result.Logs.Add(entry);
        }

        return result;
    }
}

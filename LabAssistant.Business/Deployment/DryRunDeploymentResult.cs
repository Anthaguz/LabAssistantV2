using System.Collections.Generic;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Deployment;

public class DryRunDeploymentResult
{
    public DeploymentPlan Plan { get; }
    public List<string> Logs { get; } = new();
    public List<string> Errors { get; } = new();

    public DryRunDeploymentResult(DeploymentPlan plan)
    {
        Plan = plan;
    }
}

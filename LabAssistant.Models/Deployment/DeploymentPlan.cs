namespace LabAssistant.Models.Deployment;

public class DeploymentPlan
{
    public List<DeploymentPlanStep> Steps { get; } = new();
}

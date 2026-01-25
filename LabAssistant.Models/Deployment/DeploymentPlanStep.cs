namespace LabAssistant.Models.Deployment;

public class DeploymentPlanStep
{
    public string VmName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Details { get; set; }
}

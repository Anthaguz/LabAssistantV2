using System.Collections.Generic;

namespace LabAssistant.Models.Deployment;

public class MultiVmDeploymentContext
{
    public List<VmDeploymentContext> VmContexts { get; set; } = new();
    public bool StopAllOnAnyVmFailure { get; set; }
    public List<VmCleanupResult> CleanupResults { get; } = new();
}

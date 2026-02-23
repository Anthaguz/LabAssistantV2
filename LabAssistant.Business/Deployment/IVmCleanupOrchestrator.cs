using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;

namespace LabAssistant.Business.Deployment;

public interface IVmCleanupOrchestrator
{
    Task<VmCleanupResult> CleanupAsync(VmDeploymentContext context, IHyperVService hyperVService);
}

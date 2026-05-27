using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Planning;

public interface IV2PlanningCapabilityService
{
    Task<V2PlanBuildResult> BuildPlanAsync(V2PlanBuildRequest request, CancellationToken cancellationToken = default);
}

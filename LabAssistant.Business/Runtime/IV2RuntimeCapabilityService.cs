using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Runtime;

public interface IV2RuntimeCapabilityService
{
    Task<V2RuntimeExecutionResult> ExecuteAsync(
        V2RuntimeExecutionRequest request,
        CancellationToken cancellationToken = default);
}

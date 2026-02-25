using LabAssistant.Models.Validation;

namespace LabAssistant.Services.HyperV;

public interface IVhdxIntegrityValidator
{
    Task<VhdxIntegrityValidationResult> ValidateAsync(
        string path,
        VhdxIntegrityValidationDepth depth = VhdxIntegrityValidationDepth.Full,
        CancellationToken cancellationToken = default);
}

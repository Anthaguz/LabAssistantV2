using LabAssistant.Models.Validation;

namespace LabAssistant.Services.HyperV;

public sealed class VhdxIntegrityValidator : IVhdxIntegrityValidator
{
    private readonly IVhdxFileAccessProbe _fileAccessProbe;
    private readonly IHyperVVhdxProbe _hyperVVhdxProbe;

    public VhdxIntegrityValidator(IVhdxFileAccessProbe fileAccessProbe, IHyperVVhdxProbe hyperVVhdxProbe)
    {
        _fileAccessProbe = fileAccessProbe;
        _hyperVVhdxProbe = hyperVVhdxProbe;
    }

    public async Task<VhdxIntegrityValidationResult> ValidateAsync(
        string path,
        VhdxIntegrityValidationDepth depth = VhdxIntegrityValidationDepth.Full,
        CancellationToken cancellationToken = default)
    {
        var access = _fileAccessProbe.Probe(path);

        if (access.Status == VhdxFileAccessStatus.Missing)
        {
            return new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Missing,
                Path = path ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(path)
                    ? "Base VHDX path is missing."
                    : $"Base VHDX file not found: {path}",
                Detail = access.Detail
            };
        }

        if (access.Status == VhdxFileAccessStatus.Unreadable)
        {
            return new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Unreadable,
                Path = path ?? string.Empty,
                Message = $"Base VHDX is unreadable or inaccessible: {path}",
                Detail = access.Detail
            };
        }

        if (depth == VhdxIntegrityValidationDepth.AccessibilityOnly)
        {
            return new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Valid,
                Path = path,
                Message = "Base VHDX path is accessible."
            };
        }

        var hypervProbe = await _hyperVVhdxProbe.ProbeAsync(path, cancellationToken).ConfigureAwait(false);
        return hypervProbe.Status switch
        {
            HyperVVhdxProbeStatus.Valid => new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Valid,
                Path = path,
                Message = "Base VHDX is valid."
            },
            HyperVVhdxProbeStatus.Unreadable => new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Unreadable,
                Path = path,
                Message = $"Base VHDX is unreadable or inaccessible: {path}",
                Detail = hypervProbe.Detail
            },
            _ => new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Invalid,
                Path = path,
                Message = $"Selected base disk is not a valid Hyper-V VHDX: {path}",
                Detail = hypervProbe.Detail
            }
        };
    }
}

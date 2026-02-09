using System.Collections.Generic;

namespace LabAssistant.Models.Templates;

public sealed class MissingVhdxResolutionResult
{
    public MissingVhdxResolutionResult(
        int autoResolvedCount,
        IReadOnlyList<VmTemplate> missingVms,
        IReadOnlyList<string> catalogErrors)
    {
        AutoResolvedCount = autoResolvedCount;
        MissingVms = missingVms;
        CatalogErrors = catalogErrors;
    }

    public int AutoResolvedCount { get; }

    public IReadOnlyList<VmTemplate> MissingVms { get; }

    public IReadOnlyList<string> CatalogErrors { get; }
}

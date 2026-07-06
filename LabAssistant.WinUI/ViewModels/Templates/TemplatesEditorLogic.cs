using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

/// <summary>
/// Pure, dispatcher-free helpers that encode the Templates Editor's contractual decision logic:
/// the VHDX catalog normalization precedence and the VM switch validation rules. These are extracted
/// from the dissolved editor controller/composition so they can be unit-tested without a view, a
/// dispatcher, or Hyper-V. All returned message and label strings are preserved verbatim because
/// they are part of the observable editor behavior.
/// </summary>
internal static class TemplatesEditorLogic
{
    /// <summary>
    /// Evaluates how a VM template's stored base-disk references (vhdxId, vhdxSignature, vhdPath)
    /// resolve against the current VHDX catalog. Precedence is strictly ordered:
    /// vhdxId match (with path-conflict detection) -> missing vhdxId -> ambiguous/single signature
    /// match (with path-conflict detection) -> path match -> none. A result that
    /// <see cref="TemplateVhdxNormalizationResult.RequiresUserResolution"/> gates saving.
    /// </summary>
    public static TemplateVhdxNormalizationResult EvaluateVhdxNormalization(
        VmTemplate vmTemplate,
        IReadOnlyList<TemplateVhdxCatalogOption> catalogOptions)
    {
        ArgumentNullException.ThrowIfNull(vmTemplate);
        catalogOptions ??= Array.Empty<TemplateVhdxCatalogOption>();

        var idMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdxId)
            ? null
            : catalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, vmTemplate.VhdxId, StringComparison.OrdinalIgnoreCase));

        var signatureMatches = string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature)
            ? new List<TemplateVhdxCatalogOption>()
            : catalogOptions
                .Where(option => !string.IsNullOrWhiteSpace(option.Signature) &&
                                 string.Equals(option.Signature, vmTemplate.VhdxSignature, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var pathMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdPath)
            ? null
            : catalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, vmTemplate.VhdPath, StringComparison.OrdinalIgnoreCase));

        if (idMatch is not null)
        {
            if (pathMatch is not null && !string.Equals(pathMatch.Id, idMatch.Id, StringComparison.OrdinalIgnoreCase))
            {
                return new TemplateVhdxNormalizationResult(
                    RequiresUserResolution: true,
                    EffectiveOption: null,
                    Message: "VHD identity conflict detected. Select a catalog entry to resolve before saving.",
                    EffectiveSourceLabel: "Effective source: unresolved conflict.");
            }

            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: idMatch,
                Message: "Resolved from vhdxId.",
                EffectiveSourceLabel: "Effective source: vhdxId.");
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdxId))
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: true,
                EffectiveOption: null,
                Message: $"Catalog entry '{vmTemplate.VhdxId}' is missing. Select a replacement before saving.",
                EffectiveSourceLabel: "Effective source: unresolved missing catalog.");
        }

        if (signatureMatches.Count > 1)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: true,
                EffectiveOption: null,
                Message: "Multiple catalog entries match vhdxSignature. Select one entry before saving.",
                EffectiveSourceLabel: "Effective source: unresolved signature.");
        }

        if (signatureMatches.Count == 1)
        {
            if (pathMatch is not null &&
                !string.Equals(pathMatch.Id, signatureMatches[0].Id, StringComparison.OrdinalIgnoreCase))
            {
                return new TemplateVhdxNormalizationResult(
                    RequiresUserResolution: true,
                    EffectiveOption: null,
                    Message: "VHD identity conflict detected. Select a catalog entry to resolve before saving.",
                    EffectiveSourceLabel: "Effective source: unresolved conflict.");
            }

            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: signatureMatches[0],
                Message: "Resolved from vhdxSignature.",
                EffectiveSourceLabel: "Effective source: vhdxSignature.");
        }

        if (pathMatch is not null)
        {
            return new TemplateVhdxNormalizationResult(
                RequiresUserResolution: false,
                EffectiveOption: pathMatch,
                Message: $"Catalog entry '{pathMatch.Id}' resolves current vhdPath.",
                EffectiveSourceLabel: "Effective source: vhdPath.");
        }

        return new TemplateVhdxNormalizationResult(
            RequiresUserResolution: false,
            EffectiveOption: null,
            Message: "Catalog-backed selection is preferred.",
            EffectiveSourceLabel: "Effective source: none.");
    }

    /// <summary>
    /// Validates the switch rows assigned to a VM slot against the host's available switches. Rows
    /// must be non-empty, exist on the host (case-insensitive), and be free of duplicates. Returns
    /// <see langword="false"/> with the first violation message in <paramref name="validationError"/>.
    /// </summary>
    public static bool TryValidateSwitches(
        IReadOnlyList<string> selectedSwitches,
        IReadOnlyList<string> availableVmSwitches,
        out string? validationError)
    {
        validationError = null;
        selectedSwitches ??= Array.Empty<string>();
        availableVmSwitches ??= Array.Empty<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selectedSwitch in selectedSwitches)
        {
            if (string.IsNullOrWhiteSpace(selectedSwitch))
            {
                validationError = "Each switch row must have a selected host switch or be removed.";
                return false;
            }

            if (!availableVmSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
            {
                validationError = $"Switch '{selectedSwitch}' is not available on this host.";
                return false;
            }

            if (!seen.Add(selectedSwitch))
            {
                validationError = $"Duplicate switch '{selectedSwitch}' is not allowed.";
                return false;
            }
        }

        return true;
    }
}

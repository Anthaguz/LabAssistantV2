using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Applies shared Deploy-side switch and base-disk resolve suggestions without leaving that logic in DeployWorkspaceComposition.
/// </summary>
internal sealed class DeployResolveSuggestionsService
{
    public int Apply(
        LabTemplate template,
        IReadOnlyList<VhdxCatalogItem> catalogItems,
        IReadOnlyList<string> availableSwitches)
    {
        var normalizedSwitches = availableSwitches
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var applied = 0;
        foreach (var vm in template.VmTemplates)
        {
            // Build the requested switch selection, preferring the multi-switch list and falling back to
            // the legacy single SwitchName. Blank entries are intentionally kept here so a blank/unselected
            // switch row is pruned and counted below: switches are optional, so a dangling empty selector
            // should be cleared as a visible fix rather than dismissed as "no stale references found."
            var requested = vm.SwitchNames is { Count: > 0 }
                ? vm.SwitchNames.ToList()
                : string.IsNullOrWhiteSpace(vm.SwitchName)
                    ? new List<string>()
                    : new List<string> { vm.SwitchName };

            if (requested.Count > 0)
            {
                var normalized = requested
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Where(name => normalizedSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Anything the normalization dropped is a fix: blank rows, stale (unavailable) names, or
                // duplicates. Comparing against the full requested count captures all three.
                if (normalized.Count != requested.Count)
                {
                    applied++;
                }

                vm.SwitchNames = normalized.Count > 0 ? normalized : null;
                vm.SwitchName = normalized.Count > 0 ? normalized[0] : null;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxSignature))
            {
                var signatureMatches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems);
                if (signatureMatches.Count == 1)
                {
                    var match = signatureMatches[0];
                    vm.VhdxId = match.Id;
                    vm.VhdPath = match.Path;
                    vm.VhdxSignature = match.Signature;
                    applied++;
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdPath))
            {
                var pathMatch = catalogItems.FirstOrDefault(item =>
                    string.Equals(item.Path, vm.VhdPath, StringComparison.OrdinalIgnoreCase));
                if (pathMatch is not null)
                {
                    vm.VhdxId = pathMatch.Id;
                    vm.VhdxSignature = pathMatch.Signature;
                    vm.VhdPath = pathMatch.Path;
                    applied++;
                }
            }
        }

        return applied;
    }
}

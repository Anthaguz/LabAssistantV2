using System.Linq;

namespace LabAssistant.Business.Machines;

/// <summary>
/// Pure, Hyper-V-free validation of a proposed virtual machine name. Kept as a standalone decision
/// helper so the rename policy (F08) is unit-testable without a hypervisor present.
/// </summary>
public static class MachineNameValidator
{
    /// <summary>
    /// Hyper-V rejects virtual machine names longer than 100 characters, so the policy mirrors that
    /// limit rather than inventing a stricter one.
    /// </summary>
    public const int MaxNameLength = 100;

    public static MachineNameValidationResult Validate(string? proposedName, string? currentName = null)
    {
        if (string.IsNullOrWhiteSpace(proposedName))
        {
            return MachineNameValidationResult.Invalid("Enter a name for the virtual machine.");
        }

        var normalized = proposedName.Trim();

        if (normalized.Length > MaxNameLength)
        {
            return MachineNameValidationResult.Invalid($"The name must be {MaxNameLength} characters or fewer.");
        }

        if (normalized.Any(char.IsControl))
        {
            return MachineNameValidationResult.Invalid("The name cannot contain control characters.");
        }

        if (currentName is not null &&
            string.Equals(normalized, currentName.Trim(), StringComparison.Ordinal))
        {
            return MachineNameValidationResult.Invalid("Enter a name different from the current one.");
        }

        return MachineNameValidationResult.Valid(normalized);
    }
}

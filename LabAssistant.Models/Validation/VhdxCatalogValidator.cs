using LabAssistant.Models.Catalog;

namespace LabAssistant.Models.Validation;

/// <summary>
/// Validates VHDX catalog entries.
/// </summary>
public static class VhdxCatalogValidator
{
    public static VhdxCatalogValidationResult Validate(IEnumerable<VhdxCatalogItem> items)
    {
        var result = new VhdxCatalogValidationResult();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                result.Errors.Add("Catalog item id is required.");
            }
            else if (!seenIds.Add(item.Id))
            {
                result.Errors.Add($"Duplicate catalog id: {item.Id}.");
            }

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                result.Errors.Add($"Catalog item '{item.Id}' path is required.");
            }

            if (string.IsNullOrWhiteSpace(item.OsName))
            {
                result.Errors.Add($"Catalog item '{item.Id}' OS name is required.");
            }

            if (string.IsNullOrWhiteSpace(item.OsVersion))
            {
                result.Errors.Add($"Catalog item '{item.Id}' OS version is required.");
            }

            if (item.Generation <= 0)
            {
                result.Errors.Add($"Catalog item '{item.Id}' generation must be positive.");
            }

            ValidateBootstrapProfile(item, result);
        }

        return result;
    }

    private static void ValidateBootstrapProfile(VhdxCatalogItem item, VhdxCatalogValidationResult result)
    {
        if (item.BootstrapProfile == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.BootstrapProfile.ExpectedLocalUser) &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.LocalCredentialSlotRef) &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.GuestOsFamily) &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.GuestTransport) &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.Notes))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile must not be empty when provided.");
            return;
        }

        if (item.BootstrapProfile.ExpectedLocalUser != null &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.ExpectedLocalUser))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile.expectedLocalUser must not be empty.");
        }

        if (item.BootstrapProfile.LocalCredentialSlotRef != null &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.LocalCredentialSlotRef))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile.localCredentialSlotRef must not be empty.");
        }

        if (item.BootstrapProfile.GuestOsFamily != null &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.GuestOsFamily))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile.guestOsFamily must not be empty.");
        }

        if (item.BootstrapProfile.GuestTransport != null &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.GuestTransport))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile.guestTransport must not be empty.");
        }

        if (item.BootstrapProfile.Notes != null &&
            string.IsNullOrWhiteSpace(item.BootstrapProfile.Notes))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile.notes must not be empty.");
        }

        if (!string.IsNullOrWhiteSpace(item.BootstrapProfile.GuestTransport) &&
            !string.Equals(item.BootstrapProfile.GuestTransport, "powershell-direct", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Catalog item '{item.Id}' bootstrapProfile.guestTransport must be 'powershell-direct' in the current scope.");
        }
    }
}

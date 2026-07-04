namespace LabAssistant.WinUI.ViewModels.Deploy;

public sealed class CredentialSlotItem
{
    public CredentialSlotItem(
        string slotKey,
        string purposeSummary,
        string affectedVmSummary,
        string existingUsername,
        bool hasStoredValue)
    {
        SlotKey = slotKey;
        PurposeSummary = purposeSummary;
        AffectedVmSummary = affectedVmSummary;
        ExistingUsername = existingUsername;
        HasStoredValue = hasStoredValue;
    }

    public string SlotKey { get; }

    public string PurposeSummary { get; }

    public string AffectedVmSummary { get; }

    public string ExistingUsername { get; }

    public bool HasStoredValue { get; }

    public string StoredValueStatus => HasStoredValue ? "Stored" : "Missing";

    public string StoredValueSummary => string.IsNullOrWhiteSpace(ExistingUsername)
        ? HasStoredValue
            ? "A stored local credential is already available for this slot."
            : "No stored local credential is available for this slot yet."
        : $"Stored username: {ExistingUsername}";
}

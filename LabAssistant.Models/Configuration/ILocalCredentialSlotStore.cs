using LabAssistant.Models.Deployment;

namespace LabAssistant.Models.Configuration;

public interface ILocalCredentialSlotStore
{
    IReadOnlyList<LocalCredentialSlotDefinition> LoadDefinitions();

    bool TryGetCredential(string slotKey, out V2RuntimeCredential credential);

    void Upsert(string slotKey, string username, string password);
}

public sealed class LocalCredentialSlotDefinition
{
    public string SlotKey { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;
}

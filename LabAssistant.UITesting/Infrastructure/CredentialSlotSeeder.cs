using System.Text.Json;
using System.Text.Json.Nodes;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// A snapshot of one credential-slot record as it existed before the harness touched it.
/// <see cref="Record"/> is null when the slot was absent. The record is captured verbatim
/// (including its already-DPAPI-protected password), so restoring it writes back exactly
/// what the user had - the harness never decrypts or re-encrypts a real credential.
/// </summary>
public sealed record CredentialSlotSnapshot(string SlotKey, JsonObject? Record);

/// <summary>
/// Non-destructive editor for the app's credential-slots.json store, used by guest-work
/// scenarios to guarantee a deterministic deploy password.
///
/// The app persists deploy credentials keyed by slot ref and reads them fresh on every plan
/// evaluation. That cache means a slot filled once (for example with a placeholder during a
/// planning-only run) silently satisfies later deploys with the STALE value - so a live run
/// would promote a DC with the wrong password and never authenticate. To stay idempotent a
/// scenario captures its slot, removes it so the credential UI prompts fresh, drives the UI to
/// enter the intended password, and finally restores the captured state - leaving the user's
/// store exactly as it was (no placeholder pollution, no leaked real password).
///
/// This only ever rewrites the single targeted slot; all other records are preserved verbatim,
/// including any field the harness does not model. It never decrypts a password.
/// </summary>
public sealed class CredentialSlotSeeder
{
    private readonly AppDataLocations _appData;

    public CredentialSlotSeeder(AppDataLocations appData)
    {
        _appData = appData;
    }

    /// <summary>Captures the current record for the slot (or null if absent) so it can be restored later.</summary>
    public CredentialSlotSnapshot Capture(string slotKey)
    {
        var records = Load();
        var existing = Find(records, slotKey);
        // Deep-clone so a later rewrite of the file cannot mutate the captured snapshot.
        var clone = existing is null ? null : (JsonObject?)JsonNode.Parse(existing.ToJsonString());
        return new CredentialSlotSnapshot(slotKey, clone);
    }

    /// <summary>Removes the slot record if present, forcing the app to treat it as unresolved.</summary>
    public void Remove(string slotKey)
    {
        var records = Load();
        var existing = Find(records, slotKey);
        if (existing is null)
        {
            return;
        }

        records.Remove(existing);
        Save(records);
    }

    /// <summary>
    /// Restores the slot to its captured state: writes the original record back verbatim, or
    /// removes the slot entirely if it did not exist before. Idempotent and safe to call in a
    /// finally block even if nothing was changed.
    /// </summary>
    public void Restore(CredentialSlotSnapshot snapshot)
    {
        var records = Load();
        var existing = Find(records, snapshot.SlotKey);
        if (existing is not null)
        {
            records.Remove(existing);
        }

        if (snapshot.Record is not null)
        {
            records.Add((JsonObject)JsonNode.Parse(snapshot.Record.ToJsonString())!);
        }

        Save(records);
    }

    private static JsonObject? Find(JsonArray records, string slotKey)
    {
        foreach (var node in records)
        {
            if (node is JsonObject obj
                && obj["SlotKey"]?.GetValue<string>() is { } key
                && string.Equals(key, slotKey, StringComparison.OrdinalIgnoreCase))
            {
                return obj;
            }
        }

        return null;
    }

    private JsonArray Load()
    {
        if (!File.Exists(_appData.CredentialSlotsPath))
        {
            return new JsonArray();
        }

        var text = File.ReadAllText(_appData.CredentialSlotsPath);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new JsonArray();
        }

        return JsonNode.Parse(text) as JsonArray ?? new JsonArray();
    }

    private void Save(JsonArray records)
    {
        Directory.CreateDirectory(_appData.ConfigFolder);
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_appData.CredentialSlotsPath, records.ToJsonString(options));
    }
}

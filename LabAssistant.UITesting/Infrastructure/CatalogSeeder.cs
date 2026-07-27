using System.Text.Json;
using System.Text.Json.Serialization;
using LabAssistant.UITesting.Infrastructure.Cleanup;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Reads and writes the app's base-disk catalog (%APPDATA%\LabAssistant\Catalog\
/// vhdx-catalog.json). The shape mirrors the app's VhdxCatalogItem so a seeded
/// entry loads exactly as a user-registered one would. The harness only ever
/// adds or removes entries that carry the run tag, never touching a real disk.
/// </summary>
public sealed class CatalogSeeder
{
    private readonly AppDataLocations _appData;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CatalogSeeder(AppDataLocations appData) => _appData = appData;

    /// <summary>
    /// Ensures a tiny dynamic base disk exists on disk and is registered in the
    /// catalog under the run tag. The disk is a few MB (empty dynamic VHDX) so it
    /// costs almost no space; it lets the app create a VM/differencing disk/NIC
    /// but will not boot (no OS), which also exercises cleanup-on-failure. Returns
    /// the catalog id and path of the seeded disk.
    /// </summary>
    public SeededBaseDisk SeedTinyBaseDisk(ResourceTagger tagger, long sizeBytes = 8L * 1024 * 1024 * 1024)
    {
        Directory.CreateDirectory(_appData.DifferencingDiskBasePath);
        string diskName = tagger.Name("base.vhdx");
        string diskPath = Path.Combine(_appData.DifferencingDiskBasePath, diskName);

        if (!File.Exists(diskPath))
        {
            // Dynamic VHDX: the file starts at a few MB regardless of the logical size.
            PowerShellRunner.RunOrThrow(
                $"New-VHD -Path '{Escape(diskPath)}' -SizeBytes {sizeBytes} -Dynamic | Out-Null");
        }

        long actualSize = new FileInfo(diskPath).Length;
        string id = tagger.Name("basedisk").Replace("-", string.Empty).ToLowerInvariant();

        var items = Load();
        items.RemoveAll(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        items.Add(new CatalogItem
        {
            Id = id,
            Path = diskPath,
            OsName = "LAT Harness (empty)",
            OsVersion = "test",
            Generation = 2,
            SizeBytes = actualSize,
            Notes = $"Harness-seeded empty dynamic base disk for run {tagger.RunId}. Non-bootable; safe to delete."
        });
        Save(items);

        return new SeededBaseDisk(id, diskPath, "LAT Harness (empty) test");
    }

    /// <summary>
    /// Ensures the catalog entry with the given id carries a bootstrap profile, so the
    /// V2 planner accepts guest-work VMs (DC promotion, domain join, router) that resolve
    /// to this base image. Used for a DC/guest scenario against the REAL Windows Server
    /// image already registered in the user's catalog: the profile marks the disk
    /// guest-configurable and points at the local credential slot the deploy fills.
    ///
    /// This deliberately targets an EXISTING, untagged entry by id and only writes the
    /// bootstrap sub-object - it never changes the id or path and never deletes the
    /// backing VHDX - so the harness gate's tag-based sweep leaves the real image
    /// untouched. The write is idempotent: a matching profile is a no-op. Returns true
    /// if the entry was found (and now carries the profile), false if no such id exists.
    /// </summary>
    public bool EnsureBaseDiskBootstrapProfile(
        string catalogId,
        string expectedLocalUser,
        string localCredentialSlotRef,
        string guestOsFamily,
        string guestTransport,
        string? notes = null)
    {
        var items = Load();
        var item = items.FirstOrDefault(i => string.Equals(i.Id, catalogId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return false;
        }

        var desired = new CatalogBootstrapProfile
        {
            ExpectedLocalUser = expectedLocalUser,
            LocalCredentialSlotRef = localCredentialSlotRef,
            GuestOsFamily = guestOsFamily,
            GuestTransport = guestTransport,
            Notes = notes
        };

        if (item.BootstrapProfile is { } existing
            && string.Equals(existing.ExpectedLocalUser, desired.ExpectedLocalUser, StringComparison.Ordinal)
            && string.Equals(existing.LocalCredentialSlotRef, desired.LocalCredentialSlotRef, StringComparison.Ordinal)
            && string.Equals(existing.GuestOsFamily, desired.GuestOsFamily, StringComparison.Ordinal)
            && string.Equals(existing.GuestTransport, desired.GuestTransport, StringComparison.Ordinal))
        {
            return true; // already present with the expected values - nothing to write
        }

        item.BootstrapProfile = desired;
        Save(items);
        return true;
    }

    /// <summary>Removes catalog entries whose id/path/os carries the tag; optionally deletes the backing VHDX.</summary>
    public IReadOnlyList<string> RemoveTaggedEntries(ResourceTagger tagger, bool deleteBackingFiles)
    {
        var items = Load();
        var removed = new List<string>();

        foreach (var item in items.ToList())
        {
            // Ownership is decided ONLY by the harness tag prefix. The seeded catalog id has its
            // hyphens stripped to satisfy the app's id format, so it never carries the "LAT-" prefix;
            // the backing file path does (LAT-<runId>-base.vhdx), which is the reliable, tag-based
            // signal. We deliberately do NOT fall back to any OsName/"lat" heuristic, because this
            // path can delete the backing VHDX and a fuzzy match could destroy a real user's disk.
            bool owned = tagger.IsHarnessOwned(item.Id)
                || (!string.IsNullOrEmpty(item.Path) && tagger.IsHarnessOwned(Path.GetFileName(item.Path)));

            if (!owned)
            {
                continue;
            }

            items.Remove(item);
            removed.Add(item.Id);

            if (deleteBackingFiles && !string.IsNullOrEmpty(item.Path) && File.Exists(item.Path))
            {
                try { File.Delete(item.Path); } catch { /* swept separately by disk-file cleanup */ }
            }
        }

        if (removed.Count > 0)
        {
            Save(items);
        }

        return removed;
    }

    private List<CatalogItem> Load()
    {
        if (!File.Exists(_appData.CatalogPath))
        {
            return new List<CatalogItem>();
        }

        var json = File.ReadAllText(_appData.CatalogPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CatalogItem>();
        }

        var doc = JsonSerializer.Deserialize<CatalogDocument>(json, JsonOptions);
        return doc?.Items ?? new List<CatalogItem>();
    }

    private void Save(List<CatalogItem> items)
    {
        Directory.CreateDirectory(_appData.CatalogFolder);
        var doc = new CatalogDocument { Version = "v0", Items = items };
        File.WriteAllText(_appData.CatalogPath, JsonSerializer.Serialize(doc, JsonOptions));
    }

    private static string Escape(string value) => value.Replace("'", "''");

    // Mirrors LabAssistant.Models.Catalog.VhdxCatalogDocument / VhdxCatalogItem.
    private sealed class CatalogDocument
    {
        public string Version { get; set; } = "v0";
        public List<CatalogItem> Items { get; set; } = new();
    }

    private sealed class CatalogItem
    {
        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string OsName { get; set; } = string.Empty;
        public string OsVersion { get; set; } = string.Empty;
        public int Generation { get; set; }
        public long? SizeBytes { get; set; }
        public string? Notes { get; set; }

        // Round-tripped so a catalog rewrite (which happens on every sweep) never strips a
        // disk's bootstrap profile. Before this was modelled, Save() dropped it from EVERY
        // entry - silently un-marking real user disks as guest-configurable.
        public CatalogBootstrapProfile? BootstrapProfile { get; set; }

        // Preserves any other catalog field the harness does not model (e.g. "signature")
        // so a rewrite is lossless and cannot corrupt a real user's catalog entry.
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }

    // Mirrors LabAssistant.Models.Catalog.VhdxBootstrapProfile (only the fields the harness sets).
    private sealed class CatalogBootstrapProfile
    {
        public string? ExpectedLocalUser { get; set; }
        public string? LocalCredentialSlotRef { get; set; }
        public string? GuestOsFamily { get; set; }
        public string? GuestTransport { get; set; }
        public string? Notes { get; set; }
    }
}

/// <summary>Identity of a harness-seeded base disk.</summary>
public sealed record SeededBaseDisk(string CatalogId, string Path, string DisplayLabel);

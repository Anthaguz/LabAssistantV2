using System.Text.Json.Nodes;
using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers the read-only store seams <see cref="CredentialSlotSeeder.Count"/> and
/// <see cref="CredentialSlotSeeder.Contains"/>, which the credential-fill automation uses to tell a
/// Save that actually reached the store from one that silently no-oped. Uses a temp app root so it
/// never touches the real %APPDATA% store.
/// </summary>
public sealed class CredentialSlotSeederCountTests
{
    private static (CredentialSlotSeeder seeder, string root) NewSeeder()
    {
        var root = Path.Combine(Path.GetTempPath(), "lat-credstore-" + Guid.NewGuid().ToString("N"));
        return (new CredentialSlotSeeder(new AppDataLocations(root)), root);
    }

    private static void WriteStore(AppDataLocations appData, params string[] slotKeys)
    {
        Directory.CreateDirectory(appData.ConfigFolder);
        var array = new JsonArray();
        foreach (var key in slotKeys)
        {
            array.Add(new JsonObject { ["SlotKey"] = key, ["Username"] = "Administrator" });
        }

        File.WriteAllText(appData.CredentialSlotsPath, array.ToJsonString());
    }

    [Fact]
    public void Count_IsZero_WhenStoreAbsent()
    {
        var (seeder, root) = NewSeeder();
        try
        {
            Assert.Equal(0, seeder.Count());
            Assert.False(seeder.Contains("disk.winserver2022.local-admin"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Count_And_Contains_ReflectPersistedRecords()
    {
        var (seeder, root) = NewSeeder();
        var appData = new AppDataLocations(root);
        try
        {
            WriteStore(appData, "disk.winserver2022.local-admin", "some.other.slot");

            Assert.Equal(2, seeder.Count());
            Assert.True(seeder.Contains("disk.winserver2022.local-admin"));
            Assert.True(seeder.Contains("DISK.WINSERVER2022.LOCAL-ADMIN")); // case-insensitive
            Assert.False(seeder.Contains("absent.slot"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

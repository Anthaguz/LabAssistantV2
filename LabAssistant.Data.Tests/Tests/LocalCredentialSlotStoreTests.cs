using LabAssistant.Data.Configuration;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Data.Tests;

public sealed class LocalCredentialSlotStoreTests
{
    [Fact]
    public void Upsert_ThenLoadDefinitionsAndCredential_RoundTripsProtectedValue()
    {
        var root = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        var store = new LocalCredentialSlotStore(new AppPaths(root));

        store.Upsert("domain-admin", "LAB\\Administrator", "P@ssw0rd!");

        var definitions = store.LoadDefinitions();
        var definition = Assert.Single(definitions);
        Assert.Equal("domain-admin", definition.SlotKey);
        Assert.Equal("LAB\\Administrator", definition.Username);

        var resolved = store.TryGetCredential("domain-admin", out var credential);
        Assert.True(resolved);
        Assert.Equal("LAB\\Administrator", credential.Username);
        Assert.Equal("P@ssw0rd!", credential.Password);
    }

    [Fact]
    public void Upsert_ReplacesExistingValueForSameSlotKey()
    {
        var root = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        var store = new LocalCredentialSlotStore(new AppPaths(root));

        store.Upsert("domain-admin", "LAB\\Administrator", "first");
        store.Upsert("domain-admin", "LAB\\Admin2", "second");

        var definitions = store.LoadDefinitions();
        var definition = Assert.Single(definitions);
        Assert.Equal("LAB\\Admin2", definition.Username);

        Assert.True(store.TryGetCredential("domain-admin", out var credential));
        Assert.Equal("LAB\\Admin2", credential.Username);
        Assert.Equal("second", credential.Password);
    }
}

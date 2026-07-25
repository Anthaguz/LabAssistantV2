namespace LabAssistant.UITesting.Infrastructure.ResourceProvider;

/// <summary>What the provider made available for a deploy, and how to address it in the UI.</summary>
public sealed record ProvisionedResources(
    string SwitchName,
    bool SwitchCreatedByHarness,
    string BaseDiskCatalogId,
    string BaseDiskPath,
    string BaseDiskDisplayLabel);

/// <summary>
/// Supplies the base disk + host switch a deploy scenario needs. The seam has
/// two intended modes: borrow whatever the host already has (discover-existing)
/// or stand up a dedicated sandbox (future). Either way, anything the harness
/// creates is tagged so teardown can prove no orphans are left behind.
/// </summary>
public interface ITestResourceProvider
{
    /// <summary>Ensures a base disk and host switch exist and are ready to select in the UI.</summary>
    ProvisionedResources Provision();
}

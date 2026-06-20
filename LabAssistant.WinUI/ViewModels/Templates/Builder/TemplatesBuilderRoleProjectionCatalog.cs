namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal readonly record struct TemplatesBuilderRoleCatalogEntry(
    string RoleKey,
    string DisplayName,
    bool IsAuthorable);

internal readonly record struct TemplatesBuilderVmRoleProjection(
    string RoleKey,
    string DisplayName,
    bool IsAuthorable,
    bool IsAssigned);

internal static class TemplatesBuilderRoleProjectionCatalog
{
    public const string ActiveDirectoryDomainControllerRoleKey = "ad-domain-controller";
    public const string ActiveDirectoryDomainControllerDisplayName = "Active Directory Domain Controller";
    public const string ActiveDirectoryDomainControllerTopologyRole = "FirstDomainController";

    private const string LegacyRootDomainControllerTopologyRole = "RootDomainController";

    private static readonly TemplatesBuilderRoleCatalogEntry[] AuthorableRoles =
    [
        new(ActiveDirectoryDomainControllerRoleKey, ActiveDirectoryDomainControllerDisplayName, IsAuthorable: true)
    ];

    public static IReadOnlyList<TemplatesBuilderRoleCatalogEntry> GetAuthorableRoles()
        => AuthorableRoles;

    public static IReadOnlyList<TemplatesBuilderVmRoleProjection> ProjectVmRoles(TemplatesBuilderVmDraft vm)
        =>
        [
            new(
                ActiveDirectoryDomainControllerRoleKey,
                ActiveDirectoryDomainControllerDisplayName,
                IsAuthorable: true,
                vm.IsActiveDirectoryDomainController)
        ];

    public static bool IsActiveDirectoryDomainControllerTopologyRole(string? topologyRole)
        => string.Equals(topologyRole, ActiveDirectoryDomainControllerTopologyRole, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(topologyRole, LegacyRootDomainControllerTopologyRole, StringComparison.OrdinalIgnoreCase);
}

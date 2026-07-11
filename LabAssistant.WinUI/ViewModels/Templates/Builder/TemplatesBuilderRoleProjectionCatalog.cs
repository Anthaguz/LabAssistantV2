namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>Which panel a catalog entry belongs to in the Level 2 machine inspector.</summary>
public enum TemplatesBuilderRoleCategory
{
    Role,
    Feature
}

/// <summary>
/// Static definition of an authorable Windows role or feature in the Builder catalog. Pure data: the catalog
/// says what CAN be authored; per-VM assignment state lives on the draft.
/// </summary>
internal readonly record struct TemplatesBuilderRoleDefinition(
    string Key,
    string DisplayName,
    string Description,
    TemplatesBuilderRoleCategory Category,
    bool IsStructural,
    bool IsInstallOnly,
    bool HasConfiguration);

/// <summary>
/// One catalog entry projected against a specific VM: the definition plus that VM's live assigned/locked state.
/// This is what the Level 2 roles/features inspector binds to.
/// </summary>
internal readonly record struct TemplatesBuilderVmRoleProjection(
    string RoleKey,
    string DisplayName,
    string Description,
    TemplatesBuilderRoleCategory Category,
    bool IsAuthorable,
    bool IsAssigned,
    bool IsInstallOnly,
    bool IsLocked,
    bool HasConfiguration,
    string StatusNote);

/// <summary>
/// The catalog of authorable roles/features and the pure rules that decide, for a given VM draft, which entries
/// read as assigned or locked. Runtime-independent and unit-testable without a XAML host.
///
/// Source-of-truth split (deliberate, keeps the deploy pipeline untouched):
/// - The structural directory role (domain controller) stays on <c>IsActiveDirectoryDomainController</c> and the
///   persisted <c>TopologyRole</c>. The "Active Directory Domain Services" catalog entry only reflects/derives
///   from that flag; it is not stored again in <c>AdditionalRoles</c>.
/// - Every other role (DNS, DHCP, File Server, ADCS) is authored into the additive
///   <c>TemplatesBuilderVmDraft.AdditionalRoles</c> collection.
///
/// Rule: enabling Active Directory Domain Services forces DNS on and locks it (one-way link). Disabling it does
/// not remove DNS - see <see cref="TemplatesBuilderRoleAuthoring"/>, which persists DNS so it survives.
/// </summary>
internal static class TemplatesBuilderRoleProjectionCatalog
{
    // ----- persisted structural topology-role constants (the deploy pipeline depends on these) -----

    /// <summary>The persisted topology role Builder assigns to a VM that is an Active Directory domain controller.</summary>
    public const string ActiveDirectoryDomainControllerTopologyRole = "FirstDomainController";

    /// <summary>The persisted topology role that marks the required network router VM (bridges every switch).</summary>
    public const string RouterTopologyRole = "Router";

    private const string LegacyRootDomainControllerTopologyRole = "RootDomainController";

    // ----- catalog keys -----

    public const string ActiveDirectoryDomainControllerRoleKey = "ad-domain-controller";
    public const string ActiveDirectoryDomainControllerDisplayName = "Active Directory Domain Services";
    public const string DnsServerRoleKey = "dns-server";
    public const string DhcpServerRoleKey = "dhcp-server";
    public const string FileServerRoleKey = "file-server";
    public const string CertificateServicesRoleKey = "ad-certificate-services";

    private const string AdcsInstallOnlyNote = "The role is installed now; guided configuration is added later.";
    private const string DnsLinkedNote = "Enabled and linked automatically with Active Directory Domain Services.";

    private static readonly TemplatesBuilderRoleDefinition[] Catalog =
    [
        new(
            ActiveDirectoryDomainControllerRoleKey,
            ActiveDirectoryDomainControllerDisplayName,
            "Promotes this machine to a domain controller that hosts the directory for its domain.",
            TemplatesBuilderRoleCategory.Role,
            IsStructural: true,
            IsInstallOnly: false,
            HasConfiguration: false),
        new(
            DnsServerRoleKey,
            "DNS Server",
            "Resolves names for the lab. Enabled automatically when Active Directory Domain Services is present.",
            TemplatesBuilderRoleCategory.Role,
            IsStructural: false,
            IsInstallOnly: false,
            HasConfiguration: false),
        new(
            DhcpServerRoleKey,
            "DHCP Server",
            "Leases IP addresses to lab machines.",
            TemplatesBuilderRoleCategory.Role,
            IsStructural: false,
            IsInstallOnly: false,
            HasConfiguration: false),
        new(
            FileServerRoleKey,
            "File Server",
            "Shares folders over SMB.",
            TemplatesBuilderRoleCategory.Role,
            IsStructural: false,
            IsInstallOnly: false,
            HasConfiguration: false),
        new(
            CertificateServicesRoleKey,
            "Active Directory Certificate Services",
            "Issues certificates for the lab.",
            TemplatesBuilderRoleCategory.Role,
            IsStructural: false,
            IsInstallOnly: true,
            HasConfiguration: false)
    ];

    /// <summary>Every catalog entry (roles and features), in display order.</summary>
    public static IReadOnlyList<TemplatesBuilderRoleDefinition> GetCatalog() => Catalog;

    /// <summary>Catalog entries in the Roles panel.</summary>
    public static IReadOnlyList<TemplatesBuilderRoleDefinition> GetRoles()
        => Catalog.Where(entry => entry.Category == TemplatesBuilderRoleCategory.Role).ToList();

    /// <summary>Catalog entries in the Features panel. Empty for now; the panel renders an empty state.</summary>
    public static IReadOnlyList<TemplatesBuilderRoleDefinition> GetFeatures()
        => Catalog.Where(entry => entry.Category == TemplatesBuilderRoleCategory.Feature).ToList();

    public static TemplatesBuilderRoleDefinition? FindRole(string? roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return null;
        }

        foreach (var entry in Catalog)
        {
            if (string.Equals(entry.Key, roleKey, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Projects the whole catalog against a VM, applying assigned/locked rules.</summary>
    public static IReadOnlyList<TemplatesBuilderVmRoleProjection> ProjectVmRoles(TemplatesBuilderVmDraft vm)
        => Catalog.Select(entry => ProjectRole(vm, entry)).ToList();

    /// <summary>Projects a single catalog entry against a VM, applying assigned/locked rules.</summary>
    public static TemplatesBuilderVmRoleProjection ProjectRole(TemplatesBuilderVmDraft vm, TemplatesBuilderRoleDefinition definition)
    {
        var dnsForcedByAdds = IsDnsRole(definition.Key) && vm.IsActiveDirectoryDomainController;
        var note = dnsForcedByAdds
            ? DnsLinkedNote
            : definition.IsInstallOnly
                ? AdcsInstallOnlyNote
                : string.Empty;

        return new TemplatesBuilderVmRoleProjection(
            definition.Key,
            definition.DisplayName,
            definition.Description,
            definition.Category,
            IsAuthorable: true,
            IsAssigned: IsRoleAssigned(vm, definition.Key),
            IsInstallOnly: definition.IsInstallOnly,
            IsLocked: dnsForcedByAdds,
            HasConfiguration: definition.HasConfiguration,
            StatusNote: note);
    }

    /// <summary>
    /// True when the role reads as enabled on the VM. ADDS derives from the domain-controller flag; DNS is also
    /// forced true while the VM is a domain controller; everything else is a membership test on the additive
    /// <see cref="TemplatesBuilderVmDraft.AdditionalRoles"/> collection.
    /// </summary>
    public static bool IsRoleAssigned(TemplatesBuilderVmDraft vm, string roleKey)
    {
        if (IsStructuralRole(roleKey))
        {
            return vm.IsActiveDirectoryDomainController;
        }

        if (IsDnsRole(roleKey) && vm.IsActiveDirectoryDomainController)
        {
            return true;
        }

        return HasAdditionalRole(vm, roleKey);
    }

    public static bool HasAdditionalRole(TemplatesBuilderVmDraft vm, string roleKey)
        => (vm.AdditionalRoles ?? Array.Empty<string>())
            .Any(key => string.Equals(key, roleKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>The Active Directory Domain Services entry is the structural role bound to the DC flag.</summary>
    public static bool IsStructuralRole(string? roleKey)
        => string.Equals(roleKey, ActiveDirectoryDomainControllerRoleKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsDnsRole(string? roleKey)
        => string.Equals(roleKey, DnsServerRoleKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsActiveDirectoryDomainControllerTopologyRole(string? topologyRole)
        => string.Equals(topologyRole, ActiveDirectoryDomainControllerTopologyRole, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(topologyRole, LegacyRootDomainControllerTopologyRole, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the persisted topology role marks the network router VM.</summary>
    public static bool IsRouterTopologyRole(string? topologyRole)
        => string.Equals(topologyRole, RouterTopologyRole, StringComparison.OrdinalIgnoreCase);
}

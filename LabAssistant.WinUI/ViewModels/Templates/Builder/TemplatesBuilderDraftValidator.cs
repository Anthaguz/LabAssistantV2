using System.Net;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal enum TemplatesBuilderValidationSeverity
{
    Blocker,
    Warning
}

internal enum TemplatesBuilderValidationCategory
{
    Domain,
    Network,
    VmIdentity,
    VmMembership
}

internal readonly record struct TemplatesBuilderValidationIssue(
    TemplatesBuilderValidationSeverity Severity,
    TemplatesBuilderValidationCategory Category,
    string Message,
    string? ScopeKey = null);

internal sealed class TemplatesBuilderValidationState
{
    public static TemplatesBuilderValidationState Empty { get; } = new(
        [],
        [],
        new HashSet<TemplatesBuilderValidationCategory>(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public TemplatesBuilderValidationState(
        IReadOnlyList<TemplatesBuilderValidationIssue> blockers,
        IReadOnlyList<TemplatesBuilderValidationIssue> warnings,
        IReadOnlySet<TemplatesBuilderValidationCategory> evaluatedCategories,
        IReadOnlySet<string> evaluatedDomainIds,
        IReadOnlySet<string> evaluatedNetworkIds,
        IReadOnlySet<string> evaluatedVmIds)
    {
        Blockers = blockers;
        Warnings = warnings;
        EvaluatedCategories = evaluatedCategories;
        EvaluatedDomainIds = evaluatedDomainIds;
        EvaluatedNetworkIds = evaluatedNetworkIds;
        EvaluatedVmIds = evaluatedVmIds;
    }

    public IReadOnlyList<TemplatesBuilderValidationIssue> Blockers { get; }

    public IReadOnlyList<TemplatesBuilderValidationIssue> Warnings { get; }

    public IReadOnlySet<TemplatesBuilderValidationCategory> EvaluatedCategories { get; }

    public IReadOnlySet<string> EvaluatedDomainIds { get; }

    public IReadOnlySet<string> EvaluatedNetworkIds { get; }

    public IReadOnlySet<string> EvaluatedVmIds { get; }

    public bool HasBlockers => Blockers.Count > 0;

    public string BuildReviewSummary()
    {
        if (Blockers.Count == 0 && Warnings.Count == 0)
        {
            return "Builder validation has no current blockers or warnings.";
        }

        var blockerText = Blockers.Count == 1 ? "1 blocker" : $"{Blockers.Count} blockers";
        var warningText = Warnings.Count == 1 ? "1 warning" : $"{Warnings.Count} warnings";
        var firstIssue = Blockers.FirstOrDefault().Message ?? Warnings.First().Message;
        return $"Builder validation has {blockerText} and {warningText}. {firstIssue}";
    }
}

internal sealed class TemplatesBuilderValidationRequest
{
    private TemplatesBuilderValidationRequest(
        IReadOnlySet<TemplatesBuilderValidationCategory> categories,
        IReadOnlySet<string> domainIds,
        IReadOnlySet<string> networkIds,
        IReadOnlySet<string> vmIds)
    {
        Categories = categories;
        DomainIds = domainIds;
        NetworkIds = networkIds;
        VmIds = vmIds;
    }

    public IReadOnlySet<TemplatesBuilderValidationCategory> Categories { get; }

    public IReadOnlySet<string> DomainIds { get; }

    public IReadOnlySet<string> NetworkIds { get; }

    public IReadOnlySet<string> VmIds { get; }

    public static TemplatesBuilderValidationRequest All()
        => new(
            Enum.GetValues<TemplatesBuilderValidationCategory>().ToHashSet(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static TemplatesBuilderValidationRequest ForCategories(params TemplatesBuilderValidationCategory[] categories)
        => new(
            categories.ToHashSet(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool Includes(TemplatesBuilderValidationCategory category) => Categories.Contains(category);

    public bool IncludesDomain(string? domainId)
        => DomainIds.Count == 0 ||
           (!string.IsNullOrWhiteSpace(domainId) && DomainIds.Contains(domainId));

    public bool IncludesNetwork(string? networkId)
        => NetworkIds.Count == 0 ||
           (!string.IsNullOrWhiteSpace(networkId) && NetworkIds.Contains(networkId));

    public bool IncludesVm(string? vmId)
        => VmIds.Count == 0 ||
           (!string.IsNullOrWhiteSpace(vmId) && VmIds.Contains(vmId));

    public static TemplatesBuilderValidationRequest DetectChangedScopes(
        TemplatesBuilderDraftSnapshot previous,
        TemplatesBuilderDraftSnapshot current)
    {
        var categories = new HashSet<TemplatesBuilderValidationCategory>();
        var domainIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var networkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var vmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var previousNetworks = OrEmpty(previous.LabNetworks);
        var currentNetworks = OrEmpty(current.LabNetworks);
        var previousForests = OrEmpty(previous.Forests);
        var currentForests = OrEmpty(current.Forests);
        var previousDomains = OrEmpty(previous.Domains);
        var currentDomains = OrEmpty(current.Domains);
        var previousVms = OrEmpty(previous.Vms);
        var currentVms = OrEmpty(current.Vms);

        if (!previousNetworks.SequenceEqual(currentNetworks))
        {
            categories.Add(TemplatesBuilderValidationCategory.Network);
            AddChangedNetworkIds(previousNetworks, currentNetworks, networkIds);
        }

        if (!previousForests.SequenceEqual(currentForests) ||
            !previousDomains.SequenceEqual(currentDomains))
        {
            categories.Add(TemplatesBuilderValidationCategory.Domain);
            categories.Add(TemplatesBuilderValidationCategory.VmMembership);
            AddChangedDomainIds(previousDomains, currentDomains, domainIds);
        }

        AddChangedVmScopes(previousVms, currentVms, categories, domainIds, networkIds, vmIds);

        return categories.Count == 0
            ? ForCategories()
            : new TemplatesBuilderValidationRequest(categories, domainIds, networkIds, vmIds);
    }

    private static void AddChangedNetworkIds(
        IReadOnlyList<TemplatesBuilderLabNetworkDraft> previous,
        IReadOnlyList<TemplatesBuilderLabNetworkDraft> current,
        ISet<string> networkIds)
    {
        foreach (var network in previous.Concat(current))
        {
            if (!string.IsNullOrWhiteSpace(network.NetworkId))
            {
                networkIds.Add(network.NetworkId.Trim());
            }
        }
    }

    private static void AddChangedDomainIds(
        IReadOnlyList<TemplatesBuilderDomainDraft> previous,
        IReadOnlyList<TemplatesBuilderDomainDraft> current,
        ISet<string> domainIds)
    {
        foreach (var domain in previous.Concat(current))
        {
            if (!string.IsNullOrWhiteSpace(domain.DomainId))
            {
                domainIds.Add(domain.DomainId.Trim());
            }
        }
    }

    private static void AddChangedVmScopes(
        IReadOnlyList<TemplatesBuilderVmDraft> previous,
        IReadOnlyList<TemplatesBuilderVmDraft> current,
        ISet<TemplatesBuilderValidationCategory> categories,
        ISet<string> domainIds,
        ISet<string> networkIds,
        ISet<string> vmIds)
    {
        var count = Math.Max(previous.Count, current.Count);
        for (var i = 0; i < count; i++)
        {
            var before = i < previous.Count ? previous[i] : (TemplatesBuilderVmDraft?)null;
            var after = i < current.Count ? current[i] : (TemplatesBuilderVmDraft?)null;
            if (before == after)
            {
                continue;
            }

            AddVmId(before, vmIds);
            AddVmId(after, vmIds);

            if (before is null || after is null)
            {
                categories.Add(TemplatesBuilderValidationCategory.VmIdentity);
                categories.Add(TemplatesBuilderValidationCategory.VmMembership);
                categories.Add(TemplatesBuilderValidationCategory.Network);
                AddDomainId(before, domainIds);
                AddDomainId(after, domainIds);
                AddNicNetworkIds(before, networkIds);
                AddNicNetworkIds(after, networkIds);
                continue;
            }

            if (before.Value.VmId != after.Value.VmId ||
                before.Value.Name != after.Value.Name)
            {
                categories.Add(TemplatesBuilderValidationCategory.VmIdentity);
            }

            if (before.Value.MembershipMode != after.Value.MembershipMode ||
                before.Value.DomainId != after.Value.DomainId ||
                before.Value.IsActiveDirectoryDomainController != after.Value.IsActiveDirectoryDomainController)
            {
                categories.Add(TemplatesBuilderValidationCategory.VmMembership);
                AddDomainId(before, domainIds);
                AddDomainId(after, domainIds);
            }

            if (!before.Value.Nics.SequenceEqual(after.Value.Nics))
            {
                categories.Add(TemplatesBuilderValidationCategory.Network);
                AddNicNetworkIds(before, networkIds);
                AddNicNetworkIds(after, networkIds);
            }
        }
    }

    private static void AddVmId(TemplatesBuilderVmDraft? vm, ISet<string> vmIds)
    {
        if (vm is { } value && !string.IsNullOrWhiteSpace(value.VmId))
        {
            vmIds.Add(value.VmId.Trim());
        }
    }

    private static void AddDomainId(TemplatesBuilderVmDraft? vm, ISet<string> domainIds)
    {
        if (vm is { } value && !string.IsNullOrWhiteSpace(value.DomainId))
        {
            domainIds.Add(value.DomainId.Trim());
        }
    }

    private static void AddNicNetworkIds(TemplatesBuilderVmDraft? vm, ISet<string> networkIds)
    {
        if (vm is null)
        {
            return;
        }

        foreach (var networkId in OrEmpty(vm.Value.Nics).Select(nic => nic.NetworkId).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            networkIds.Add(networkId.Trim());
        }
    }

    private static IReadOnlyList<T> OrEmpty<T>(IReadOnlyList<T>? values)
        => values ?? Array.Empty<T>();
}

internal static class TemplatesBuilderDraftValidator
{
    public static TemplatesBuilderValidationState Validate(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderValidationRequest? request = null)
    {
        request ??= TemplatesBuilderValidationRequest.All();
        var issues = new List<TemplatesBuilderValidationIssue>();

        if (request.Includes(TemplatesBuilderValidationCategory.Domain))
        {
            ValidateDomains(draft, request, issues);
        }

        if (request.Includes(TemplatesBuilderValidationCategory.Network))
        {
            ValidateNetworks(draft, request, issues);
        }

        if (request.Includes(TemplatesBuilderValidationCategory.VmIdentity))
        {
            ValidateVmIdentity(draft, request, issues);
        }

        if (request.Includes(TemplatesBuilderValidationCategory.VmMembership))
        {
            ValidateVmMembership(draft, request, issues);
        }

        return new TemplatesBuilderValidationState(
            issues.Where(issue => issue.Severity == TemplatesBuilderValidationSeverity.Blocker).ToList(),
            issues.Where(issue => issue.Severity == TemplatesBuilderValidationSeverity.Warning).ToList(),
            request.Categories.ToHashSet(),
            request.DomainIds.ToHashSet(StringComparer.OrdinalIgnoreCase),
            request.NetworkIds.ToHashSet(StringComparer.OrdinalIgnoreCase),
            request.VmIds.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static void ValidateDomains(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderValidationRequest request,
        ICollection<TemplatesBuilderValidationIssue> issues)
    {
        var domains = OrEmpty(draft.Domains);
        var forests = OrEmpty(draft.Forests);
        AddDuplicateIssues(domains.Select(domain => domain.DomainId), "Domain id", TemplatesBuilderValidationCategory.Domain, issues);
        AddDuplicateIssues(domains.Select(domain => domain.DnsName), "Domain DNS name", TemplatesBuilderValidationCategory.Domain, issues);

        var domainIds = domains
            .Where(domain => !string.IsNullOrWhiteSpace(domain.DomainId))
            .Select(domain => domain.DomainId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var forestIds = forests
            .Where(forest => !string.IsNullOrWhiteSpace(forest.ForestId))
            .Select(forest => forest.ForestId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var domain in domains.Where(domain => request.IncludesDomain(domain.DomainId)))
        {
            var scopeKey = Scope(domain.DomainId);
            if (string.IsNullOrWhiteSpace(domain.DomainId) ||
                string.IsNullOrWhiteSpace(domain.DnsName) ||
                string.IsNullOrWhiteSpace(domain.NetBiosName) ||
                string.IsNullOrWhiteSpace(domain.ForestId))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, "Each domain requires domain id, DNS name, NetBIOS name, and forest id.", scopeKey);
            }

            if (!IsValidDnsName(domain.DnsName))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, $"Domain '{Display(domain.DomainId)}' DNS name must be a valid DNS name.", scopeKey);
            }

            if (!string.IsNullOrWhiteSpace(domain.ForestId) && !forestIds.Contains(domain.ForestId.Trim()))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, $"Domain '{Display(domain.DomainId)}' references unknown forest '{domain.ForestId}'.", scopeKey);
            }

            if (!Enum.TryParse<V2DomainRelationKind>(domain.RelationKind, ignoreCase: true, out var relationKind))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, $"Domain '{Display(domain.DomainId)}' relation kind must be Root, Child, or Tree.", scopeKey);
            }
            else if (relationKind == V2DomainRelationKind.Child &&
                     (string.IsNullOrWhiteSpace(domain.ParentDomainId) || !domainIds.Contains(domain.ParentDomainId.Trim())))
            {
                // A child domain is a subdomain and must name an existing parent domain.
                AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, $"Domain '{Display(domain.DomainId)}' requires a valid parent domain reference.", scopeKey);
            }
            else if (relationKind == V2DomainRelationKind.Tree &&
                     !string.IsNullOrWhiteSpace(domain.ParentDomainId) && !domainIds.Contains(domain.ParentDomainId.Trim()))
            {
                // A tree domain roots its own namespace and attaches to the forest via ForestId, so a parent is
                // optional; but if one is carried it must still resolve to a real domain.
                AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, $"Domain '{Display(domain.DomainId)}' references an unknown parent domain '{domain.ParentDomainId}'.", scopeKey);
            }
        }

        foreach (var forest in forests.Where(forest => !string.IsNullOrWhiteSpace(forest.RootDomainId) && !domainIds.Contains(forest.RootDomainId.Trim())))
        {
            AddBlocker(issues, TemplatesBuilderValidationCategory.Domain, $"Forest '{Display(forest.ForestId)}' references unknown root domain '{forest.RootDomainId}'.", Scope(forest.ForestId));
        }
    }

    private static void ValidateNetworks(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderValidationRequest request,
        ICollection<TemplatesBuilderValidationIssue> issues)
    {
        var networks = OrEmpty(draft.LabNetworks);
        var vms = OrEmpty(draft.Vms);
        AddDuplicateIssues(networks.Select(network => network.NetworkId), "Network id", TemplatesBuilderValidationCategory.Network, issues);
        var networksById = networks
            .Where(network => !string.IsNullOrWhiteSpace(network.NetworkId))
            .GroupBy(network => network.NetworkId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var seenIpsByNetwork = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var network in networks.Where(network => request.IncludesNetwork(network.NetworkId)))
        {
            var scopeKey = Scope(network.NetworkId);
            if (!string.IsNullOrWhiteSpace(network.SwitchType))
            {
                if (string.IsNullOrWhiteSpace(network.SwitchName))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"Network '{Display(network.NetworkId)}' switch type requires a switch name.", scopeKey);
                }

                if (!V2SwitchTypeCatalog.IsSupported(network.SwitchType))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"Network '{Display(network.NetworkId)}' switch type must be External, Internal, or Private.", scopeKey);
                }
            }
        }

        foreach (var vm in vms)
        {
            if (vm.Nics is null)
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"VM '{Display(vm.Name)}' networking data could not be loaded.", Scope(vm.VmId));
                continue;
            }

            foreach (var nic in vm.Nics.Where(nic => request.IncludesNetwork(nic.NetworkId)))
            {
                var networkKey = string.IsNullOrWhiteSpace(nic.NetworkId) ? "(unassigned)" : nic.NetworkId.Trim();
                var scopeKey = networkKey;

                if (!string.IsNullOrWhiteSpace(nic.IpAddress) && !TryParseIpv4(nic.IpAddress, out var address))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"VM '{Display(vm.Name)}' NIC '{Display(nic.NicId)}' IP address must be a valid IPv4 address.", scopeKey);
                    continue;
                }

                if (!TryParseOptionalPrefix(nic.PrefixLength, out _))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"VM '{Display(vm.Name)}' NIC '{Display(nic.NicId)}' prefix length must be 0 through 32.", scopeKey);
                }

                if (!string.IsNullOrWhiteSpace(nic.IpAddress))
                {
                    var networkIps = seenIpsByNetwork.TryGetValue(networkKey, out var existing)
                        ? existing
                        : seenIpsByNetwork[networkKey] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (networkIps.TryGetValue(nic.IpAddress.Trim(), out var existingNic))
                    {
                        AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"IP address '{nic.IpAddress}' is duplicated in network '{networkKey}' by '{existingNic}' and '{scopeKey}'.", scopeKey);
                    }
                    else
                    {
                        networkIps[nic.IpAddress.Trim()] = scopeKey;
                    }
                }

                if (networksById.TryGetValue(networkKey, out var network))
                {
                    ValidateSubnetFit(network, vm, nic, issues, scopeKey);
                }
                else if (!string.IsNullOrWhiteSpace(nic.NetworkId))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"VM '{Display(vm.Name)}' NIC '{Display(nic.NicId)}' references unknown network '{nic.NetworkId}'.", scopeKey);
                }
            }
        }
    }

    private static void ValidateSubnetFit(
        TemplatesBuilderLabNetworkDraft network,
        TemplatesBuilderVmDraft vm,
        TemplatesBuilderNicDraft nic,
        ICollection<TemplatesBuilderValidationIssue> issues,
        string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(nic.IpAddress))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(network.Subnet))
        {
            AddWarning(issues, TemplatesBuilderValidationCategory.Network, $"Network '{Display(network.NetworkId)}' has no subnet, so NIC IP subnet fit cannot be checked.", scopeKey);
            return;
        }

        if (!TryParseIpv4(nic.IpAddress, out var address))
        {
            return;
        }

        if (!TryParseCidr(network.Subnet, out var networkAddress, out var prefixLength))
        {
            AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"Network '{Display(network.NetworkId)}' subnet must be valid IPv4 CIDR notation.", Scope(network.NetworkId));
            return;
        }

        if (!IsInSubnet(address, networkAddress, prefixLength))
        {
            AddBlocker(issues, TemplatesBuilderValidationCategory.Network, $"VM '{Display(vm.Name)}' NIC '{Display(nic.NicId)}' IP address must fit network '{Display(network.NetworkId)}' subnet '{network.Subnet}'.", scopeKey);
        }
    }

    private static void ValidateVmIdentity(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderValidationRequest request,
        ICollection<TemplatesBuilderValidationIssue> issues)
    {
        var vms = OrEmpty(draft.Vms);
        if (vms.Count == 0)
        {
            AddBlocker(issues, TemplatesBuilderValidationCategory.VmIdentity, "At least one VM is required before Save.", null);
            return;
        }

        AddDuplicateIssues(vms.Select(vm => vm.VmId), "VM id", TemplatesBuilderValidationCategory.VmIdentity, issues);
        AddDuplicateIssues(vms.Select(vm => vm.Name), "VM name", TemplatesBuilderValidationCategory.VmIdentity, issues);

        foreach (var vm in vms)
        {
            if (string.IsNullOrWhiteSpace(vm.VmId) || string.IsNullOrWhiteSpace(vm.Name))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.VmIdentity, "Each VM requires a VM id and name.", Scope(vm.VmId));
            }

            if (!IsValidVmName(vm.Name))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.VmIdentity, $"VM '{Display(vm.Name)}' name contains unsupported characters or exceeds 64 characters.", Scope(vm.VmId));
            }
        }
    }

    private static void ValidateVmMembership(
        TemplatesBuilderDraftSnapshot draft,
        TemplatesBuilderValidationRequest request,
        ICollection<TemplatesBuilderValidationIssue> issues)
    {
        var domains = OrEmpty(draft.Domains);
        var vms = OrEmpty(draft.Vms);
        var domainIds = domains
            .Where(domain => !string.IsNullOrWhiteSpace(domain.DomainId))
            .Select(domain => domain.DomainId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dcDomainIds = vms
            .Where(vm => vm.IsActiveDirectoryDomainController && !string.IsNullOrWhiteSpace(vm.DomainId))
            .Select(vm => vm.DomainId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var vm in vms.Where(vm => request.IncludesVm(vm.VmId)))
        {
            var membershipMode = V2MembershipModeCatalog.Normalize(vm.MembershipMode);
            var scopeKey = Scope(vm.VmId);
            if (membershipMode is null)
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.VmMembership, $"VM '{Display(vm.Name)}' membership must be DomainMember or Standalone.", scopeKey);
                continue;
            }

            if (V2MembershipModeCatalog.IsDomainMember(membershipMode))
            {
                if (string.IsNullOrWhiteSpace(vm.DomainId))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.VmMembership, $"VM '{Display(vm.Name)}' requires a domain assignment when membership is DomainMember.", scopeKey);
                }
                else if (!domainIds.Contains(vm.DomainId.Trim()))
                {
                    AddBlocker(issues, TemplatesBuilderValidationCategory.VmMembership, $"VM '{Display(vm.Name)}' references unknown domain '{vm.DomainId}'.", scopeKey);
                }
            }

            if (V2MembershipModeCatalog.IsStandalone(membershipMode) && !string.IsNullOrWhiteSpace(vm.DomainId))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.VmMembership, $"VM '{Display(vm.Name)}' must not carry a domain assignment when membership is Standalone.", scopeKey);
            }

            if (vm.IsActiveDirectoryDomainController && !V2MembershipModeCatalog.IsDomainMember(membershipMode))
            {
                AddBlocker(issues, TemplatesBuilderValidationCategory.VmMembership, $"VM '{Display(vm.Name)}' must use DomainMember membership when assigned the Active Directory Domain Controller role.", scopeKey);
            }
        }

        foreach (var domain in domains.Where(domain =>
                     request.IncludesDomain(domain.DomainId) &&
                     !string.IsNullOrWhiteSpace(domain.DomainId) &&
                     !dcDomainIds.Contains(domain.DomainId.Trim())))
        {
            AddBlocker(issues, TemplatesBuilderValidationCategory.VmMembership, $"Domain '{Display(domain.DomainId)}' requires at least one VM assigned the Active Directory Domain Controller role.", Scope(domain.DomainId));
        }
    }

    private static void AddDuplicateIssues(
        IEnumerable<string> values,
        string label,
        TemplatesBuilderValidationCategory category,
        ICollection<TemplatesBuilderValidationIssue> issues)
    {
        foreach (var duplicate in values
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Select(value => value.Trim())
                     .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            AddBlocker(issues, category, $"{label} '{duplicate}' must be unique.", Scope(duplicate));
        }
    }

    private static void AddBlocker(
        ICollection<TemplatesBuilderValidationIssue> issues,
        TemplatesBuilderValidationCategory category,
        string message,
        string? scopeKey)
        => issues.Add(new TemplatesBuilderValidationIssue(TemplatesBuilderValidationSeverity.Blocker, category, message, scopeKey));

    private static void AddWarning(
        ICollection<TemplatesBuilderValidationIssue> issues,
        TemplatesBuilderValidationCategory category,
        string message,
        string? scopeKey)
        => issues.Add(new TemplatesBuilderValidationIssue(TemplatesBuilderValidationSeverity.Warning, category, message, scopeKey));

    private static bool IsValidDnsName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 253 || !value.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        return value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .All(label => label.Length <= 63 &&
                          label.All(character => char.IsLetterOrDigit(character) || character == '-') &&
                          label[0] != '-' &&
                          label[^1] != '-');
    }

    private static bool IsValidVmName(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 64)
        {
            return false;
        }

        return value.Trim().All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.');
    }

    private static bool TryParseIpv4(string value, out IPAddress address)
        => IPAddress.TryParse(value.Trim(), out address!) &&
           address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;

    private static bool TryParseOptionalPrefix(string value, out int? prefix)
    {
        prefix = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!int.TryParse(value.Trim(), out var parsed) || parsed is < 0 or > 32)
        {
            return false;
        }

        prefix = parsed;
        return true;
    }

    private static bool TryParseCidr(string value, out IPAddress networkAddress, out int prefixLength)
    {
        networkAddress = IPAddress.None;
        prefixLength = 0;
        var parts = value.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryParseIpv4(parts[0], out networkAddress) ||
            !int.TryParse(parts[1], out prefixLength) ||
            prefixLength is < 0 or > 32)
        {
            return false;
        }

        return true;
    }

    private static bool IsInSubnet(IPAddress address, IPAddress networkAddress, int prefixLength)
    {
        var addressValue = ToUInt32(address);
        var networkValue = ToUInt32(networkAddress);
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return (addressValue & mask) == (networkValue & mask);
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               bytes[3];
    }

    private static string Display(string value)
        => string.IsNullOrWhiteSpace(value) ? "(unnamed)" : value.Trim();

    private static string? Scope(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<T> OrEmpty<T>(IReadOnlyList<T>? values)
        => values ?? Array.Empty<T>();
}

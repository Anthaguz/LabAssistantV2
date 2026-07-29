using LabAssistant.Business.Runtime;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.Business.Planning;

public sealed class V2PlanningCapabilityService : IV2PlanningCapabilityService
{
    private static readonly HashSet<string> KnownTopologyRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Router",
        "FirstDomainController",
        "ReplicaDomainController"
    };

    private static readonly HashSet<string> KnownCapabilityRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pki",
        "Sql",
        "Web",
        "Operations"
    };

    /// <summary>
    /// Default type used when a template references a switch that does not yet exist on the host and no explicit
    /// switch type is declared for it. Internal keeps the lab isolated from the internet while still letting the
    /// VMs reach each other and the host reach them (for inspection, console, RDP, or acting as a DHCP/tooling
    /// source). External references stay blocking until an adapter mapping exists, and an existing switch of a
    /// different type still blocks via the type-mismatch path.
    /// </summary>
    private const string DefaultAutoCreatedSwitchType = V2SwitchTypeCatalog.Internal;

    /// <summary>
    /// Topology roles that are treated as an evident DHCP provider for the purpose of the plan-time no-IP
    /// visibility warning. A switch that hosts one of these infrastructure VMs is assumed to have a DHCP server,
    /// so a NIC with no static IP there is a legitimate DHCP client rather than a silent no-address failure.
    /// </summary>
    private static readonly IReadOnlyList<string> DhcpProviderTopologyRoles =
    [
        "Router",
        "FirstDomainController",
        "ReplicaDomainController"
    ];

    public Task<V2PlanBuildResult> BuildPlanAsync(V2PlanBuildRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Template);

        var template = request.Template;
        var issues = new List<V2PlanIssue>();
        var unresolved = new List<V2UnresolvedRequirement>();
        var nodes = new List<V2PlanNode>();
        var dependencies = new List<V2PlanDependency>();

        var executionEngine = template.ExecutionEngine != default
            ? template.ExecutionEngine
            : TemplateSchemaVersionCatalog.Classify(template.SchemaVersion);

        if (executionEngine != TemplateExecutionEngine.V2UnifiedPlanning)
        {
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "v2-template-required",
                Message = $"Template '{template.Name}' uses schema '{template.SchemaVersion}' and is not routed to the V2 planning engine.",
                SuggestedAction = "Use a V2 template schema version before requesting a V2 orchestration plan."
            });

            return Task.FromResult(new V2PlanBuildResult
            {
                Success = false,
                Context = new V2ResolvedPlanningContext
                {
                    ExecutionEngine = executionEngine,
                    ResolvedDeploymentProfileName = ResolveDefaultProfileName(request.DefaultDeploymentProfile),
                    ResolvedDeploymentProfile = ResolveDefaultProfile(request.DefaultDeploymentProfile),
                    RouterSemanticsRequired = false,
                    DomainSemanticsRequired = false
                },
                Issues = issues,
                UnresolvedRequirements = unresolved
            });
        }

        var validation = LabTemplateValidator.Validate(template, request.CatalogItems);
        foreach (var error in validation.Errors)
        {
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "template-validation",
                Message = error,
                SuggestedAction = "Correct the V2 template validation error and rebuild the plan."
            });
        }

        var resolvedProfile = ResolveProfile(template.DeploymentProfile, request.DefaultDeploymentProfile, issues);
        var labNetworks = (template.LabNetworks ?? [])
            .Where(network => !string.IsNullOrWhiteSpace(network.NetworkId))
            .GroupBy(network => network.NetworkId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var availableSwitches = request.AvailableSwitches
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => NormalizeSwitchType(group.First().SwitchType),
                StringComparer.OrdinalIgnoreCase);
        foreach (var switchName in request.AvailableSwitchNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()))
        {
            availableSwitches.TryAdd(switchName, null);
        }
        var externalSwitchAdapterMappings = request.ExternalSwitchAdapterMappings
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .GroupBy(pair => pair.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value.Trim(),
                StringComparer.OrdinalIgnoreCase);
        var switchRequirements = new Dictionary<string, NetworkSwitchRequirementBuilder>(StringComparer.OrdinalIgnoreCase);
        var resolvedSlots = new HashSet<string>(
            request.ResolvedCredentialSlotKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var sortedVms = template.VmTemplates
            .OrderBy(vm => string.IsNullOrWhiteSpace(vm.Name) ? "~" : vm.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(vm => vm.VmId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var states = sortedVms
            .Select(vm => ResolveVmState(
                vm,
                request.CatalogItems,
                labNetworks,
                availableSwitches,
                externalSwitchAdapterMappings,
                switchRequirements,
                resolvedSlots,
                issues,
                unresolved))
            .ToList();

        EmitNoDhcpVisibilityWarnings(states, issues);

        var hasRootDc = states.Any(state => state.TopologyRoleIs("FirstDomainController"));
        var hasReplicaDc = states.Any(state => state.TopologyRoleIs("ReplicaDomainController"));
        var hasDomainMembers = states.Any(state => state.RequiresDomainJoin);
        var domainRequired = hasRootDc || hasReplicaDc || hasDomainMembers;
        var topologyResolution = domainRequired
            ? V2DirectoryTopologyResolver.Resolve(
                template,
                states.ToDictionary(
                    state => state.Vm.VmId,
                    state => new V2VmTopologyInfo(state.Vm.VmId, state.Vm.Name, state.TopologyRole, state.DomainId),
                    StringComparer.OrdinalIgnoreCase),
                issues)
            : V2DirectoryTopologyResolution.Empty;

        foreach (var state in states)
        {
            state.ResolvedDomain = topologyResolution.FindDomain(state.DomainId);
            if (state.ResolvedDomain is not null)
            {
                state.ResolvedForest = topologyResolution.Forests.FirstOrDefault(forest =>
                    string.Equals(forest.ForestId, state.ResolvedDomain.ForestId, StringComparison.OrdinalIgnoreCase));

                if (state.TopologyRoleIs("FirstDomainController") &&
                    state.ResolvedDomain.RelationKind is V2DomainRelationKind.Child or V2DomainRelationKind.Tree)
                {
                    AddCredentialSlotRequirement(
                        unresolved,
                        issues,
                        resolvedSlots,
                        state.Vm,
                        state.Vm.Name,
                        state.EffectiveParentDomainAdminSlot,
                        "parent-domain-admin",
                        $"{state.ResolvedDomain.RelationKind.ToString().ToLowerInvariant()}-domain creation");
                }
            }
        }

        var resolvedTrusts = ResolveTrusts(topologyResolution, states, resolvedSlots, issues, unresolved);
        var resolvedSwitchRequirements = switchRequirements.Values
            .Select(builder => builder.Build())
            .OrderBy(requirement => requirement.SwitchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(requirement => requirement.SwitchType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var routerRequired = DetermineRouterRequirement(states, domainRequired);
        if (routerRequired && !states.Any(state => state.IsRouterCapable))
        {
            unresolved.Add(new V2UnresolvedRequirement
            {
                Kind = V2UnresolvedRequirementKind.RouterRequirement,
                Key = "router-required",
                AffectedVmIds = states.Where(state => state.RequiresRouterDependency).Select(state => state.Vm.VmId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Description = "Cross-switch domain-dependent work requires a router-capable VM, but none is present in the V2 template."
            });
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "router-required",
                Message = "Cross-switch dependent work requires a router-capable VM.",
                SuggestedAction = "Add a Router topology-role VM or remove the cross-switch dependency."
            });
        }

        var context = new V2ResolvedPlanningContext
        {
            ExecutionEngine = executionEngine,
            ResolvedDeploymentProfile = resolvedProfile,
            ResolvedDeploymentProfileName = V2SchedulerPolicyCatalog.GetCanonicalProfileName(resolvedProfile),
            RouterSemanticsRequired = routerRequired,
            DomainSemanticsRequired = domainRequired,
            Forests = topologyResolution.Forests,
            Domains = topologyResolution.Domains,
            Trusts = resolvedTrusts,
            NetworkSwitchRequirements = resolvedSwitchRequirements,
            Vms = states.Select(state => state.ToContext()).ToArray()
        };

        EmitSwitchNodes(resolvedSwitchRequirements, nodes);
        EmitNodes(states, routerRequired, nodes);
        EmitTrustNodes(resolvedTrusts, nodes);
        EmitDependencies(states, routerRequired, dependencies);
        EmitSwitchDependencies(resolvedSwitchRequirements, nodes, states, dependencies);
        EmitTrustDependencies(resolvedTrusts, nodes, states, dependencies);
        ApplyWaveHints(nodes, dependencies, resolvedProfile, states);
        var waves = BuildWaves(nodes);

        var success = !issues.Any(issue => issue.Severity == V2PlanIssueSeverity.Blocking) &&
                      unresolved.Count == 0;

        return Task.FromResult(new V2PlanBuildResult
        {
            Success = success,
            Context = context,
            Nodes = nodes.OrderBy(node => node.WaveHint).ThenBy(node => node.NodeId, StringComparer.Ordinal).ToArray(),
            Dependencies = dependencies
                .OrderBy(dep => dep.ToNodeId, StringComparer.Ordinal)
                .ThenBy(dep => dep.FromNodeId, StringComparer.Ordinal)
                .ThenBy(dep => dep.ReasonCode)
                .ToArray(),
            Waves = waves,
            Issues = issues
                .OrderBy(issue => issue.Severity)
                .ThenBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(issue => issue.Code, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            UnresolvedRequirements = unresolved
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        });
    }

    private static ResolvedVmState ResolveVmState(
        VmTemplate vm,
        IReadOnlyList<VhdxCatalogItem> catalogItems,
        IReadOnlyDictionary<string, LabNetworkTemplate> labNetworks,
        IReadOnlyDictionary<string, string?> availableSwitches,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings,
        IDictionary<string, NetworkSwitchRequirementBuilder> switchRequirements,
        ISet<string> resolvedSlots,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        var topologyRole = NormalizeTopologyRole(vm.TopologyRole);
        var membershipMode = NormalizeMembershipMode(vm);
        var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name.Trim();

        if (!string.IsNullOrWhiteSpace(topologyRole) && !KnownTopologyRoles.Contains(topologyRole))
        {
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "unknown-topology-role",
                VmId = vm.VmId,
                VmName = vmName,
                Message = $"VM '{vmName}' uses unsupported topology role '{vm.TopologyRole}'.",
                SuggestedAction = "Use one of the supported V2 topology roles."
            });
        }

        var knownCapabilities = new List<string>();
        foreach (var capabilityRole in vm.CapabilityRoles ?? [])
        {
            var normalized = Normalize(capabilityRole);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            if (KnownCapabilityRoles.Contains(normalized))
            {
                knownCapabilities.Add(normalized);
            }
            else
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Warning,
                    Code = "unknown-capability-role",
                    VmId = vm.VmId,
                    VmName = vmName,
                    Message = $"VM '{vmName}' uses unknown capability role '{capabilityRole}'.",
                    SuggestedAction = "Use a supported capability role or extend the planner/runtime contract later."
                });
            }
        }

        var catalogItem = ResolveCatalogItem(vm, catalogItems);
        if (catalogItem is null)
        {
            unresolved.Add(new V2UnresolvedRequirement
            {
                Kind = V2UnresolvedRequirementKind.CatalogReference,
                Key = string.IsNullOrWhiteSpace(vm.VhdxId) ? vm.VhdPath ?? vm.VmId : vm.VhdxId!,
                AffectedVmIds = [vm.VmId],
                Description = $"VM '{vmName}' could not resolve its base image from the VHDX catalog."
            });
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "catalog-reference-missing",
                VmId = vm.VmId,
                VmName = vmName,
                Message = $"VM '{vmName}' could not resolve its VHDX catalog reference.",
                SuggestedAction = "Select a valid VHDX catalog item or update the template mapping."
            });
        }

        var bootstrapProfile = catalogItem?.BootstrapProfile;
        if (!string.IsNullOrWhiteSpace(vm.BootstrapProfileRef) && bootstrapProfile is null)
        {
            unresolved.Add(new V2UnresolvedRequirement
            {
                Kind = V2UnresolvedRequirementKind.BootstrapProfile,
                Key = vm.BootstrapProfileRef!,
                AffectedVmIds = [vm.VmId],
                Description = $"VM '{vmName}' references bootstrap profile '{vm.BootstrapProfileRef}', but the selected base image does not expose bootstrap metadata."
            });
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "bootstrap-profile-missing",
                VmId = vm.VmId,
                VmName = vmName,
                Message = $"VM '{vmName}' requires bootstrap assumptions that are missing from the resolved VHDX catalog item.",
                SuggestedAction = "Add bootstrap metadata to the VHDX catalog entry or choose a compatible base image."
            });
        }

        var resolvedNics = ResolveNics(
            vm,
            labNetworks,
            availableSwitches,
            externalSwitchAdapterMappings,
            switchRequirements,
            issues,
            unresolved);
        var requiresGuestWork = DetermineGuestWorkRequirement(topologyRole, membershipMode, knownCapabilities, resolvedNics, vm);
        var bootstrapSlot = Normalize(vm.CredentialSlots?.LocalBootstrap) ?? Normalize(bootstrapProfile?.LocalCredentialSlotRef);
        // Credential-reuse policy: a lab's domain admin is the promoted local Administrator baked into the VHDX, so
        // when the template does not author distinct domain/DSRM/parent slots we reuse the effective bootstrap slot
        // (which itself resolves from the base-disk catalog's LocalCredentialSlotRef). This keeps templates a pure
        // topology artifact - no authored credentials - while still letting DC promotion and domain joins plan. The
        // only credential blocker that survives is a genuinely missing base-disk bootstrap contract (bootstrapSlot
        // null), enforced by the local-bootstrap requirement below.
        var domainAdminSlot = Normalize(vm.CredentialSlots?.DomainAdmin) ?? bootstrapSlot;
        var domainJoinSlot = Normalize(vm.CredentialSlots?.DomainJoin) ?? domainAdminSlot;
        var dsrmSlot = Normalize(vm.CredentialSlots?.Dsrm) ?? bootstrapSlot;
        var parentDomainAdminSlot = Normalize(vm.CredentialSlots?.ParentDomainAdmin) ?? bootstrapSlot;

        if (requiresGuestWork)
        {
            if (bootstrapProfile is null)
            {
                unresolved.Add(new V2UnresolvedRequirement
                {
                    Kind = V2UnresolvedRequirementKind.BootstrapProfile,
                    Key = vm.VmId,
                    AffectedVmIds = [vm.VmId],
                    Description = $"VM '{vmName}' requires guest-side planning, but its resolved base image does not provide bootstrap assumptions."
                });
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "bootstrap-profile-required",
                    VmId = vm.VmId,
                    VmName = vmName,
                    Message = $"VM '{vmName}' requires bootstrap metadata before V2 guest work can be planned.",
                    SuggestedAction = "Populate the VHDX bootstrap profile for the selected base image."
                });
            }

            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, bootstrapSlot, "local-bootstrap", "guest bootstrap access");
        }

        if (string.Equals(topologyRole, "ReplicaDomainController", StringComparison.OrdinalIgnoreCase))
        {
            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, domainAdminSlot, "domain-admin", "replica promotion");
            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, dsrmSlot, "dsrm", "replica promotion");
        }

        if (string.Equals(topologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase))
        {
            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, domainAdminSlot, "domain-admin", "first domain-controller creation");
            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, dsrmSlot, "dsrm", "first domain-controller creation");
        }

        var requiresDomainJoin = V2MembershipModeCatalog.IsDomainMember(membershipMode) &&
                                 !string.Equals(topologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(topologyRole, "ReplicaDomainController", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(topologyRole, "Router", StringComparison.OrdinalIgnoreCase);

        if (requiresDomainJoin)
        {
            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, domainJoinSlot, "domain-join", "domain join");
            AddCredentialSlotRequirement(unresolved, issues, resolvedSlots, vm, vmName, domainAdminSlot, "domain-admin", "domain join");
        }

        var state = new ResolvedVmState(vm, topologyRole, membershipMode, knownCapabilities, catalogItem, bootstrapProfile, resolvedNics)
        {
            RequiresGuestWork = requiresGuestWork,
            DomainId = Normalize(vm.DomainId),
            EffectiveBootstrapSlot = bootstrapSlot,
            EffectiveDomainAdminSlot = domainAdminSlot,
            EffectiveDomainJoinSlot = domainJoinSlot,
            EffectiveDsrmSlot = dsrmSlot,
            EffectiveParentDomainAdminSlot = parentDomainAdminSlot,
            RequiresDomainJoin = requiresDomainJoin,
            IsRouterCapable = string.Equals(topologyRole, "Router", StringComparison.OrdinalIgnoreCase)
        };
        // Router VMs may reach egress through a NAT-capable host switch (Hyper-V Default Switch) that reports
        // type Internal, so router external classification accepts that as a WAN attachment in addition to a
        // true External switch. Non-router states keep the strict External-type check, so member egress
        // expectations and general switch reconciliation are unchanged.
        Func<V2ResolvedVmNetworkInterface, bool> isExternalAttachment = state.IsRouterCapable
            ? nic => RouterExternalAttachmentPolicy.IsExternalAttachment(nic.EffectiveSwitchType, nic.EffectiveSwitchName)
            : nic => RouterExternalAttachmentPolicy.IsExternalSwitchType(nic.EffectiveSwitchType);
        state.HasExternalSwitchAttachment = state.ResolvedNics.Any(isExternalAttachment);
        state.HasNonExternalSwitchAttachment = state.ResolvedNics.Any(nic => !isExternalAttachment(nic));
        state.RouterProvidesEgress = state.IsRouterCapable && state.HasExternalSwitchAttachment && state.HasNonExternalSwitchAttachment;

        return state;
    }

    private static IReadOnlyList<V2ResolvedTrustPlanningContext> ResolveTrusts(
        V2DirectoryTopologyResolution topology,
        IReadOnlyList<ResolvedVmState> states,
        ISet<string> resolvedSlots,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        if (topology.Trusts.Count == 0)
        {
            return Array.Empty<V2ResolvedTrustPlanningContext>();
        }

        var domainsById = topology.Domains.ToDictionary(domain => domain.DomainId, StringComparer.OrdinalIgnoreCase);
        var statesByVmId = states.ToDictionary(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase);
        var resolvedTrusts = new List<V2ResolvedTrustPlanningContext>();

        foreach (var trust in topology.Trusts.OrderBy(item => item.TrustId, StringComparer.OrdinalIgnoreCase))
        {
            var trustId = Normalize(trust.TrustId) ?? "<unnamed-trust>";
            if (trust.TrustType != V2TrustType.Forest || trust.Direction != V2TrustDirection.Bidirectional)
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "trust-shape-unsupported",
                    Message = $"Trust '{trustId}' uses unsupported type '{trust.TrustType}' or direction '{trust.Direction}'.",
                    SuggestedAction = "Use a bidirectional forest trust between two managed V2 domains/forests for the first executable trust slice."
                });
                continue;
            }

            if (!domainsById.TryGetValue(trust.SourceDomainId, out var sourceDomain) ||
                !domainsById.TryGetValue(trust.TargetDomainId, out var targetDomain))
            {
                continue;
            }

            if (string.Equals(sourceDomain.DomainId, targetDomain.DomainId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "trust-self-reference",
                    Message = $"Trust '{trustId}' must reference two different managed domains.",
                    SuggestedAction = "Point the trust source and target to distinct V2 domains."
                });
                continue;
            }

            if (string.Equals(sourceDomain.ForestId, targetDomain.ForestId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "trust-forest-reference-unsupported",
                    Message = $"Trust '{trustId}' references domains in the same forest.",
                    SuggestedAction = "Use a bidirectional forest trust between two managed V2 forests."
                });
                continue;
            }

            if (!statesByVmId.TryGetValue(sourceDomain.FirstDomainControllerVmId, out var sourceState) ||
                !statesByVmId.TryGetValue(targetDomain.FirstDomainControllerVmId, out var targetState))
            {
                continue;
            }

            AddCredentialSlotRequirement(
                unresolved,
                issues,
                resolvedSlots,
                sourceState.Vm,
                sourceState.Vm.Name,
                sourceState.EffectiveDomainAdminSlot,
                "domain-admin",
                $"forest trust '{trustId}' source domain '{sourceDomain.DnsName}'");
            AddCredentialSlotRequirement(
                unresolved,
                issues,
                resolvedSlots,
                targetState.Vm,
                targetState.Vm.Name,
                targetState.EffectiveDomainAdminSlot,
                "domain-admin",
                $"forest trust '{trustId}' target domain '{targetDomain.DnsName}'");

            resolvedTrusts.Add(new V2ResolvedTrustPlanningContext
            {
                TrustId = trustId,
                TrustType = trust.TrustType,
                Direction = trust.Direction,
                SourceDomainId = sourceDomain.DomainId,
                SourceDomainDnsName = sourceDomain.DnsName,
                SourceDomainNetBiosName = sourceDomain.NetBiosName,
                SourceForestId = sourceDomain.ForestId,
                SourceAnchorVmId = sourceState.Vm.VmId,
                SourceAnchorVmName = sourceState.Vm.Name,
                SourceDomainAdminCredentialSlot = sourceState.EffectiveDomainAdminSlot,
                TargetDomainId = targetDomain.DomainId,
                TargetDomainDnsName = targetDomain.DnsName,
                TargetDomainNetBiosName = targetDomain.NetBiosName,
                TargetForestId = targetDomain.ForestId,
                TargetAnchorVmId = targetState.Vm.VmId,
                TargetAnchorVmName = targetState.Vm.Name,
                TargetDomainAdminCredentialSlot = targetState.EffectiveDomainAdminSlot,
                PrepareDnsNodeId = BuildTrustNodeId(trustId, V2PlanNodeKind.PrepareForestTrustDns),
                CreateTrustNodeId = BuildTrustNodeId(trustId, V2PlanNodeKind.CreateForestTrust),
                ValidateTrustNodeId = BuildTrustNodeId(trustId, V2PlanNodeKind.ValidateForestTrust)
            });
        }

        return resolvedTrusts.ToArray();
    }

    private static void AddCredentialSlotRequirement(
        List<V2UnresolvedRequirement> unresolved,
        List<V2PlanIssue> issues,
        ISet<string> resolvedSlots,
        VmTemplate vm,
        string vmName,
        string? slotKey,
        string slotPurpose,
        string description)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            unresolved.Add(new V2UnresolvedRequirement
            {
                Kind = V2UnresolvedRequirementKind.CredentialSlot,
                Key = $"{slotPurpose}:{vm.VmId}",
                AffectedVmIds = [vm.VmId],
                Description = $"VM '{vmName}' is missing a credential-slot reference for {description}."
            });
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "credential-slot-missing",
                VmId = vm.VmId,
                VmName = vmName,
                Message = $"VM '{vmName}' requires a credential-slot reference for {description}.",
                SuggestedAction = "Assign the required credential slot in the V2 template or base-image bootstrap metadata."
            });
            return;
        }

        if (resolvedSlots.Contains(slotKey))
        {
            return;
        }

        unresolved.Add(new V2UnresolvedRequirement
        {
            Kind = V2UnresolvedRequirementKind.CredentialSlot,
            Key = slotKey,
            AffectedVmIds = [vm.VmId],
            Description = $"VM '{vmName}' requires credential slot '{slotKey}' for {description}, but it is unresolved on this machine."
        });
        issues.Add(new V2PlanIssue
        {
            Severity = V2PlanIssueSeverity.Blocking,
            Code = "credential-slot-unresolved",
            VmId = vm.VmId,
            VmName = vmName,
            Message = $"VM '{vmName}' requires unresolved credential slot '{slotKey}' for {description}.",
            SuggestedAction = "Resolve the credential slot locally before starting V2 deployment."
        });
    }

    private static List<V2ResolvedVmNetworkInterface> ResolveNics(
        VmTemplate vm,
        IReadOnlyDictionary<string, LabNetworkTemplate> labNetworks,
        IReadOnlyDictionary<string, string?> availableSwitches,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings,
        IDictionary<string, NetworkSwitchRequirementBuilder> switchRequirements,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        var result = new List<V2ResolvedVmNetworkInterface>();
        var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name.Trim();
        var templateNics = vm.Nics;

        if (templateNics is { Count: > 0 })
        {
            foreach (var nic in templateNics.OrderBy(nic => nic.NicId, StringComparer.OrdinalIgnoreCase))
            {
                var networkId = Normalize(nic.NetworkId);
                LabNetworkTemplate? network = null;
                if (!string.IsNullOrWhiteSpace(networkId) && !labNetworks.TryGetValue(networkId, out network))
                {
                    unresolved.Add(new V2UnresolvedRequirement
                    {
                        Kind = V2UnresolvedRequirementKind.LabNetwork,
                        Key = networkId,
                        AffectedVmIds = [vm.VmId],
                        Description = $"VM '{vmName}' references lab network '{networkId}' on NIC '{nic.NicId}', but that network does not exist in the template."
                    });
                    issues.Add(new V2PlanIssue
                    {
                        Severity = V2PlanIssueSeverity.Blocking,
                        Code = "lab-network-missing",
                        VmId = vm.VmId,
                        VmName = vmName,
                        Message = $"VM '{vmName}' references missing lab network '{networkId}'.",
                        SuggestedAction = "Define the missing lab network or update the NIC mapping."
                    });
                }

                var nicSwitchOverride = Normalize(nic.SwitchName);
                var effectiveSwitch = nicSwitchOverride ?? Normalize(network?.SwitchName);
                var declaredNetworkSwitchType = nicSwitchOverride is null
                    ? NormalizeSupportedSwitchType(network?.SwitchType)
                    : null;
                var effectiveSwitchType = ResolveEffectiveSwitch(
                    vm,
                    vmName,
                    nic.NicId,
                    networkId,
                    effectiveSwitch,
                    declaredNetworkSwitchType,
                    availableSwitches,
                    externalSwitchAdapterMappings,
                    switchRequirements,
                    issues,
                    unresolved);

                result.Add(new V2ResolvedVmNetworkInterface
                {
                    NicId = nic.NicId,
                    Name = nic.Name,
                    NetworkId = networkId,
                    EffectiveSwitchName = effectiveSwitch,
                    EffectiveSwitchType = effectiveSwitchType,
                    IpAddress = nic.IpAddress,
                    PrefixLength = nic.PrefixLength,
                    DefaultGateway = nic.DefaultGateway,
                    DnsServers = nic.DnsServers?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray() ?? Array.Empty<string>()
                });
            }

            return result;
        }

        var fallbackSwitches = (vm.SwitchNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (fallbackSwitches.Count == 0 && !string.IsNullOrWhiteSpace(vm.SwitchName))
        {
            fallbackSwitches.Add(vm.SwitchName.Trim());
        }

        if (fallbackSwitches.Count == 0)
        {
            return result;
        }

        for (var index = 0; index < fallbackSwitches.Count; index++)
        {
            var switchName = fallbackSwitches[index];
            var nicId = $"fallback-{index + 1}";
            if (!availableSwitches.TryGetValue(switchName, out var switchType))
            {
                // Legacy vm.SwitchName / vm.SwitchNames references carry no declared type, so a switch that is
                // missing on the host is auto-created as Internal by default rather than blocking the deploy.
                AddSwitchRequirement(
                    switchRequirements,
                    networkId: null,
                    vm,
                    vmName,
                    nicId,
                    switchName,
                    DefaultAutoCreatedSwitchType,
                    externalAdapterName: null,
                    issues,
                    unresolved);
                switchType = DefaultAutoCreatedSwitchType;
            }

            result.Add(new V2ResolvedVmNetworkInterface
            {
                NicId = nicId,
                EffectiveSwitchName = switchName,
                EffectiveSwitchType = switchType
            });
        }

        return result;
    }

    private static string? ResolveEffectiveSwitch(
        VmTemplate vm,
        string vmName,
        string nicId,
        string? networkId,
        string? effectiveSwitch,
        string? declaredNetworkSwitchType,
        IReadOnlyDictionary<string, string?> availableSwitches,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings,
        IDictionary<string, NetworkSwitchRequirementBuilder> switchRequirements,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        if (string.IsNullOrWhiteSpace(effectiveSwitch))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(declaredNetworkSwitchType))
        {
            if (availableSwitches.TryGetValue(effectiveSwitch, out var legacyAvailableSwitchType))
            {
                return NormalizeSupportedSwitchType(legacyAvailableSwitchType) ?? NormalizeSwitchType(legacyAvailableSwitchType);
            }

            // A bare switch reference (a NIC-level switch override, or a lab network that declares no switchType)
            // that is not present on the host is auto-created as Internal by default rather than blocking the
            // deploy. Runtime EnsureNetworkSwitchAsync creates and cleans these up. External still blocks until an
            // adapter mapping is supplied, and an existing switch of a different type still blocks via type mismatch.
            AddSwitchRequirement(
                switchRequirements,
                networkId,
                vm,
                vmName,
                nicId,
                effectiveSwitch,
                DefaultAutoCreatedSwitchType,
                externalAdapterName: null,
                issues,
                unresolved);
            return DefaultAutoCreatedSwitchType;
        }

        if (availableSwitches.TryGetValue(effectiveSwitch, out var availableSwitchType))
        {
            var normalizedAvailableType = NormalizeSupportedSwitchType(availableSwitchType);
            if (!string.Equals(normalizedAvailableType, declaredNetworkSwitchType, StringComparison.OrdinalIgnoreCase))
            {
                AddSwitchTypeMismatchIssue(
                    vm,
                    vmName,
                    nicId,
                    effectiveSwitch,
                    declaredNetworkSwitchType,
                    availableSwitchType,
                    issues,
                    unresolved);
                return normalizedAvailableType ?? NormalizeSwitchType(availableSwitchType);
            }

            AddSwitchRequirement(
                switchRequirements,
                networkId,
                vm,
                vmName,
                nicId,
                effectiveSwitch,
                declaredNetworkSwitchType,
                externalAdapterName: null,
                issues,
                unresolved);
            return declaredNetworkSwitchType;
        }

        if (SwitchTypeIs(declaredNetworkSwitchType, V2SwitchTypeCatalog.External))
        {
            if (!externalSwitchAdapterMappings.TryGetValue(effectiveSwitch, out var adapterName))
            {
                unresolved.Add(new V2UnresolvedRequirement
                {
                    Kind = V2UnresolvedRequirementKind.ExternalSwitchAdapterMapping,
                    Key = effectiveSwitch,
                    AffectedVmIds = [vm.VmId],
                    Description = $"External switch '{effectiveSwitch}' for VM '{vmName}' requires a deploy-review adapter mapping before it can be created."
                });
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Blocking,
                    Code = "external-switch-adapter-required",
                    VmId = vm.VmId,
                    VmName = vmName,
                    Message = $"VM '{vmName}' requires missing External switch '{effectiveSwitch}', but no deploy-review adapter mapping was provided.",
                    SuggestedAction = "Map the External switch to a host adapter for this deployment run or pre-create a matching External switch."
                });
                return declaredNetworkSwitchType;
            }

            AddSwitchRequirement(
                switchRequirements,
                networkId,
                vm,
                vmName,
                nicId,
                effectiveSwitch,
                declaredNetworkSwitchType,
                adapterName,
                issues,
                unresolved);
            return declaredNetworkSwitchType;
        }

        AddSwitchRequirement(
            switchRequirements,
            networkId,
            vm,
            vmName,
            nicId,
            effectiveSwitch,
            declaredNetworkSwitchType,
            externalAdapterName: null,
            issues,
            unresolved);
        return declaredNetworkSwitchType;
    }

    private static void AddSwitchTypeMismatchIssue(
        VmTemplate vm,
        string vmName,
        string nicId,
        string switchName,
        string expectedType,
        string? actualType,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        unresolved.Add(new V2UnresolvedRequirement
        {
            Kind = V2UnresolvedRequirementKind.SwitchReference,
            Key = switchName,
            AffectedVmIds = [vm.VmId],
            Description = $"VM '{vmName}' requires switch '{switchName}' as {expectedType}, but the current host reports type '{NormalizeSwitchType(actualType) ?? "Unknown"}'."
        });
        issues.Add(new V2PlanIssue
        {
            Severity = V2PlanIssueSeverity.Blocking,
            Code = "switch-type-mismatch",
            VmId = vm.VmId,
            VmName = vmName,
            Message = $"VM '{vmName}' requires switch '{switchName}' as {expectedType}, but the host switch type is '{NormalizeSwitchType(actualType) ?? "Unknown"}'.",
            SuggestedAction = "Use a switch with the same name and type, rename one of the switches, or update the lab network switchType."
        });
    }

    private static void AddSwitchRequirement(
        IDictionary<string, NetworkSwitchRequirementBuilder> switchRequirements,
        string? networkId,
        VmTemplate vm,
        string vmName,
        string nicId,
        string switchName,
        string switchType,
        string? externalAdapterName,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        var key = switchName.Trim();
        if (!switchRequirements.TryGetValue(key, out var builder))
        {
            builder = new NetworkSwitchRequirementBuilder(switchName, switchType, externalAdapterName);
            switchRequirements[key] = builder;
        }
        else if (!string.Equals(builder.SwitchType, switchType, StringComparison.OrdinalIgnoreCase))
        {
            AddDeclaredSwitchTypeConflictIssue(vm, vmName, nicId, switchName, switchType, builder.SwitchType, issues, unresolved);
            return;
        }

        builder.AddNetwork(networkId);
        builder.AddVm(vm.VmId);
        builder.SetExternalAdapterName(externalAdapterName);
    }

    private static void AddDeclaredSwitchTypeConflictIssue(
        VmTemplate vm,
        string vmName,
        string nicId,
        string switchName,
        string expectedType,
        string declaredType,
        List<V2PlanIssue> issues,
        List<V2UnresolvedRequirement> unresolved)
    {
        unresolved.Add(new V2UnresolvedRequirement
        {
            Kind = V2UnresolvedRequirementKind.SwitchReference,
            Key = switchName,
            AffectedVmIds = [vm.VmId],
            Description = $"VM '{vmName}' requires switch '{switchName}' as {expectedType} on NIC '{nicId}', but another lab network declares it as {declaredType}."
        });
        issues.Add(new V2PlanIssue
        {
            Severity = V2PlanIssueSeverity.Blocking,
            Code = "switch-type-mismatch",
            VmId = vm.VmId,
            VmName = vmName,
            Message = $"Switch '{switchName}' is declared with conflicting V2 switch types: {declaredType} and {expectedType}.",
            SuggestedAction = "Use one switch type per switch name, or assign different switch names to the lab networks."
        });
    }

    /// <summary>
    /// Emits a non-blocking plan-time warning for every NIC that has no static IP and sits on an isolated
    /// (Internal or Private) switch where no evident DHCP provider is present. Legitimate labs run their own DHCP
    /// on a router or domain controller, so this stays non-blocking; it exists only to surface the "no IP" case up
    /// front instead of leaving it a silent runtime mystery. A switch is considered covered when any router or
    /// domain-controller VM has a NIC on it.
    /// </summary>
    private static void EmitNoDhcpVisibilityWarnings(
        IReadOnlyList<ResolvedVmState> states,
        List<V2PlanIssue> issues)
    {
        var switchesWithProvider = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in states)
        {
            if (!DhcpProviderTopologyRoles.Any(state.TopologyRoleIs))
            {
                continue;
            }

            foreach (var nic in state.ResolvedNics)
            {
                if (!string.IsNullOrWhiteSpace(nic.EffectiveSwitchName))
                {
                    switchesWithProvider.Add(nic.EffectiveSwitchName.Trim());
                }
            }
        }

        foreach (var state in states)
        {
            foreach (var nic in state.ResolvedNics)
            {
                if (!string.IsNullOrWhiteSpace(nic.IpAddress))
                {
                    continue;
                }

                // Only fully isolated switch types lack an inherent external DHCP source; External switches are
                // assumed to reach a real DHCP server, so a missing static IP there is an ordinary DHCP lease.
                if (!SwitchTypeIs(nic.EffectiveSwitchType, V2SwitchTypeCatalog.Internal) &&
                    !SwitchTypeIs(nic.EffectiveSwitchType, V2SwitchTypeCatalog.Private))
                {
                    continue;
                }

                var switchName = nic.EffectiveSwitchName?.Trim();
                if (string.IsNullOrWhiteSpace(switchName) || switchesWithProvider.Contains(switchName))
                {
                    continue;
                }

                var switchTypeLabel = NormalizeSwitchType(nic.EffectiveSwitchType) ?? "isolated";
                issues.Add(new V2PlanIssue
                {
                    Severity = V2PlanIssueSeverity.Warning,
                    Code = "nic-no-static-ip-no-dhcp-provider",
                    VmId = state.Vm.VmId,
                    VmName = state.Vm.Name,
                    Message = $"VM '{state.Vm.Name}' NIC '{nic.NicId}' has no static IP on {switchTypeLabel} switch '{switchName}', and no router or domain controller offering DHCP is present on that switch. The VM may not receive an IP address.",
                    SuggestedAction = "Assign a static IP to this NIC, or place a DHCP provider (for example a router or domain controller) on this switch."
                });
            }
        }
    }

    private static bool DetermineGuestWorkRequirement(
        string? topologyRole,
        string? membershipMode,
        IReadOnlyCollection<string> knownCapabilities,
        IReadOnlyCollection<V2ResolvedVmNetworkInterface> nics,
        VmTemplate vm)
    {
        if (!string.IsNullOrWhiteSpace(topologyRole))
        {
            return true;
        }

        if (V2MembershipModeCatalog.IsDomainMember(membershipMode))
        {
            return true;
        }

        if (knownCapabilities.Count > 0)
        {
            return true;
        }

        if (nics.Any(NicRequiresGuestConfiguration))
        {
            return true;
        }

        return vm.GuestNetworkConfig?.Enabled == true ||
               vm.RoleConfig?.Enabled == true ||
               vm.SoftwareConfig?.Enabled == true ||
               vm.TimeZoneConfig?.Enabled == true;
    }

    /// <summary>
    /// A NIC needs in-guest network preparation only when it carries configuration to apply (a lab-network
    /// binding, a static address, a gateway, or DNS servers). A bare NIC that only names a switch takes DHCP
    /// and needs no guest-side work. This is the single source of truth shared by
    /// <see cref="DetermineGuestWorkRequirement"/> and <see cref="ResolvedVmState.RequiresNetworkBootstrap"/>:
    /// keeping both on the same predicate guarantees the PrepareGuestNetwork node is never emitted without the
    /// GuestTransportReady gate that must precede it, so it can never become an orphaned in-degree-zero node the
    /// ready-set scheduler would admit before the VM is even started.
    /// </summary>
    private static bool NicRequiresGuestConfiguration(V2ResolvedVmNetworkInterface nic)
        => !string.IsNullOrWhiteSpace(nic.NetworkId) ||
           !string.IsNullOrWhiteSpace(nic.IpAddress) ||
           nic.PrefixLength.HasValue ||
           !string.IsNullOrWhiteSpace(nic.DefaultGateway) ||
           nic.DnsServers.Count > 0;

    private static bool DetermineRouterRequirement(IReadOnlyList<ResolvedVmState> states, bool domainRequired)
    {
        var routerRequired = states.Any(state => state.RouterProvidesEgress || (state.IsRouterCapable && state.NetworkKeys.Count > 1));

        var rootNetworks = states
            .Where(state => state.TopologyRoleIs("FirstDomainController"))
            .SelectMany(state => state.NetworkKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (domainRequired && rootNetworks.Count > 0)
        {
            foreach (var state in states.Where(state => state.TopologyRoleIs("ReplicaDomainController") || state.RequiresDomainJoin))
            {
                if (state.NetworkKeys.Count == 0)
                {
                    continue;
                }

                if (state.NetworkKeys.Any(network => !rootNetworks.Contains(network)))
                {
                    state.RequiresRouterDependency = true;
                    routerRequired = true;
                }
            }
        }

        var routerProvidesEgress = states.Any(state => state.RouterProvidesEgress);
        if (routerProvidesEgress)
        {
            foreach (var state in states.Where(state =>
                         !state.IsRouterCapable &&
                         !state.HasExternalSwitchAttachment &&
                         state.HasNonExternalSwitchAttachment))
            {
                state.ExpectsRouterEgress = true;
                routerRequired = true;
            }
        }

        return routerRequired;
    }

    private static void EmitSwitchNodes(
        IReadOnlyList<V2ResolvedNetworkSwitchRequirement> switchRequirements,
        List<V2PlanNode> nodes)
    {
        foreach (var requirement in switchRequirements)
        {
            nodes.Add(new V2PlanNode
            {
                NodeId = requirement.NodeId,
                Kind = V2PlanNodeKind.EnsureNetworkSwitch,
                DisplayName = $"Ensure switch {requirement.SwitchName}",
                WorkloadClass = V2WorkloadClass.HeavyHost,
                SwitchName = requirement.SwitchName
            });
        }
    }

    private static void EmitNodes(IReadOnlyList<ResolvedVmState> states, bool routerRequired, List<V2PlanNode> nodes)
    {
        foreach (var state in states)
        {
            state.Nodes[V2PlanNodeKind.ProvisionVm] = AddNode(nodes, state, V2PlanNodeKind.ProvisionVm, "Provision VM", V2WorkloadClass.HeavyHost);

            if (state.RequiresGuestWork)
            {
                state.Nodes[V2PlanNodeKind.EnableGuestServices] = AddNode(nodes, state, V2PlanNodeKind.EnableGuestServices, "Enable guest services", V2WorkloadClass.HeavyHost);
                state.Nodes[V2PlanNodeKind.StartVm] = AddNode(nodes, state, V2PlanNodeKind.StartVm, "Start VM", V2WorkloadClass.HeavyHost);
                state.Nodes[V2PlanNodeKind.GuestTransportReady] = AddNode(nodes, state, V2PlanNodeKind.GuestTransportReady, "Wait for guest transport", V2WorkloadClass.LightWaitValidation);
            }
            else
            {
                state.Nodes[V2PlanNodeKind.StartVm] = AddNode(nodes, state, V2PlanNodeKind.StartVm, "Start VM", V2WorkloadClass.HeavyHost);
            }

            if (state.RequiresNetworkBootstrap && !state.TopologyRoleIs("Router"))
            {
                state.Nodes[V2PlanNodeKind.PrepareGuestNetwork] = AddNode(nodes, state, V2PlanNodeKind.PrepareGuestNetwork, "Prepare guest network", V2WorkloadClass.MediumGuest);
            }

            if (state.RequiresGuestWork)
            {
                state.Nodes[V2PlanNodeKind.ConfigureBaseRemoteAccess] = AddNode(nodes, state, V2PlanNodeKind.ConfigureBaseRemoteAccess, "Configure base remote access", V2WorkloadClass.MediumGuest);
                state.Nodes[V2PlanNodeKind.BaseRemoteAccessReady] = AddNode(nodes, state, V2PlanNodeKind.BaseRemoteAccessReady, "Base remote access ready", V2WorkloadClass.LightWaitValidation);
            }

            if (state.TopologyRoleIs("FirstDomainController") || state.TopologyRoleIs("ReplicaDomainController"))
            {
                state.Nodes[V2PlanNodeKind.InstallAdDomainServicesFeature] = AddNode(nodes, state, V2PlanNodeKind.InstallAdDomainServicesFeature, "Install AD DS feature", V2WorkloadClass.HeavyGuest);
            }

            if (state.TopologyRoleIs("Router") && routerRequired)
            {
                state.Nodes[V2PlanNodeKind.PrepareRouterNetwork] = AddNode(nodes, state, V2PlanNodeKind.PrepareRouterNetwork, "Prepare router network", V2WorkloadClass.MediumGuest);
                state.Nodes[V2PlanNodeKind.InstallRouterRemoteAccessFeature] = AddNode(nodes, state, V2PlanNodeKind.InstallRouterRemoteAccessFeature, "Install router remote-access feature", V2WorkloadClass.HeavyGuest);
                state.Nodes[V2PlanNodeKind.EnableRouterRouting] = AddNode(nodes, state, V2PlanNodeKind.EnableRouterRouting, "Enable router routing", V2WorkloadClass.HeavyGuest);
                state.Nodes[V2PlanNodeKind.ConfigureRouterNat] = AddNode(nodes, state, V2PlanNodeKind.ConfigureRouterNat, "Configure router NAT", V2WorkloadClass.HeavyGuest);
                state.Nodes[V2PlanNodeKind.ValidateCrossSwitchRouting] = AddNode(nodes, state, V2PlanNodeKind.ValidateCrossSwitchRouting, "Validate cross-switch routing", V2WorkloadClass.LightWaitValidation);
                if (state.RouterProvidesEgress)
                {
                    state.Nodes[V2PlanNodeKind.ValidateRouterEgress] = AddNode(nodes, state, V2PlanNodeKind.ValidateRouterEgress, "Validate router egress", V2WorkloadClass.LightWaitValidation);
                }
                state.Nodes[V2PlanNodeKind.RouterReady] = AddNode(nodes, state, V2PlanNodeKind.RouterReady, "Router ready", V2WorkloadClass.HeavyGuest);
            }

            if (state.TopologyRoleIs("FirstDomainController"))
            {
                state.Nodes[V2PlanNodeKind.PromoteFirstDomainController] = AddNode(nodes, state, V2PlanNodeKind.PromoteFirstDomainController, GetFirstDomainControllerDisplayName(state), V2WorkloadClass.HeavyGuest);
                state.Nodes[V2PlanNodeKind.DomainReady] = AddNode(nodes, state, V2PlanNodeKind.DomainReady, "Domain ready", V2WorkloadClass.LightWaitValidation);
            }

            if (state.TopologyRoleIs("ReplicaDomainController"))
            {
                state.Nodes[V2PlanNodeKind.PromoteReplicaDomainController] = AddNode(nodes, state, V2PlanNodeKind.PromoteReplicaDomainController, "Promote replica domain controller", V2WorkloadClass.HeavyGuest);
                state.Nodes[V2PlanNodeKind.ReplicaDomainReady] = AddNode(nodes, state, V2PlanNodeKind.ReplicaDomainReady, "Replica domain ready", V2WorkloadClass.LightWaitValidation);
            }

            if (state.RequiresDomainJoin)
            {
                state.Nodes[V2PlanNodeKind.JoinDomain] = AddNode(nodes, state, V2PlanNodeKind.JoinDomain, "Join domain", V2WorkloadClass.MediumGuest);
                state.Nodes[V2PlanNodeKind.JoinedDomainReady] = AddNode(nodes, state, V2PlanNodeKind.JoinedDomainReady, "Joined domain ready", V2WorkloadClass.LightWaitValidation);
            }

            foreach (var capabilityRole in state.KnownCapabilityRoles)
            {
                var node = AddNode(nodes, state, V2PlanNodeKind.ApplyCapabilityRole, $"Apply {capabilityRole} capability", GetCapabilityWorkload(capabilityRole), capabilityRole);
                state.CapabilityNodes[capabilityRole] = node;
            }
        }

        foreach (var domainGroup in states
                     .Where(state => state.TopologyRoleIs("FirstDomainController") && state.ResolvedDomain is not null)
                     .GroupBy(state => state.ResolvedDomain!.DomainId, StringComparer.OrdinalIgnoreCase))
        {
            var rootState = domainGroup.OrderBy(state => state.Vm.Name, StringComparer.OrdinalIgnoreCase).ThenBy(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase).First();
            var domainStates = states.Where(state => string.Equals(state.DomainId, domainGroup.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            if (domainStates.Any(state => state.TopologyRoleIs("ReplicaDomainController")) || domainStates.Any(state => state.RequiresDomainJoin))
            {
                rootState.Nodes[V2PlanNodeKind.StabilizeDomainDns] = AddNode(nodes, rootState, V2PlanNodeKind.StabilizeDomainDns, "Stabilize domain DNS", V2WorkloadClass.MediumGuest);
            }
        }
    }

    private static void EmitTrustNodes(IReadOnlyList<V2ResolvedTrustPlanningContext> trusts, List<V2PlanNode> nodes)
    {
        foreach (var trust in trusts)
        {
            AddTrustNode(nodes, trust, V2PlanNodeKind.PrepareForestTrustDns, "Prepare forest trust DNS", V2WorkloadClass.MediumGuest);
            AddTrustNode(nodes, trust, V2PlanNodeKind.CreateForestTrust, "Create forest trust", V2WorkloadClass.HeavyGuest);
            AddTrustNode(nodes, trust, V2PlanNodeKind.ValidateForestTrust, "Validate forest trust", V2WorkloadClass.LightWaitValidation);
        }
    }

    private static void EmitDependencies(IReadOnlyList<ResolvedVmState> states, bool routerRequired, List<V2PlanDependency> dependencies)
    {
        var domainReadyByDomainId = states
            .Where(state => state.ResolvedDomain is not null && state.TryGetNode(V2PlanNodeKind.DomainReady) is not null)
            .ToDictionary(state => state.ResolvedDomain!.DomainId, state => state.TryGetNode(V2PlanNodeKind.DomainReady)!, StringComparer.OrdinalIgnoreCase);
        var dnsGateByDomainId = states
            .Where(state => state.ResolvedDomain is not null && state.TryGetNode(V2PlanNodeKind.StabilizeDomainDns) is not null)
            .ToDictionary(state => state.ResolvedDomain!.DomainId, state => state.TryGetNode(V2PlanNodeKind.StabilizeDomainDns)!, StringComparer.OrdinalIgnoreCase);
        var routerNode = states
            .Where(state => state.IsRouterCapable)
            .OrderBy(state => state.Vm.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase)
            .Select(state => state.TryGetNode(V2PlanNodeKind.RouterReady))
            .FirstOrDefault(node => node is not null);

        foreach (var state in states)
        {
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ProvisionVm), state.TryGetNode(V2PlanNodeKind.EnableGuestServices), V2PlanDependencyReasonCode.VmLifecycle, "Guest services require a provisioned VM.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.EnableGuestServices), state.TryGetNode(V2PlanNodeKind.StartVm), V2PlanDependencyReasonCode.VmLifecycle, "Guest services must be enabled before V2 guest orchestration starts.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ProvisionVm), state.TryGetNode(V2PlanNodeKind.StartVm), V2PlanDependencyReasonCode.VmLifecycle, "VM must be provisioned before it can start.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.StartVm), state.TryGetNode(V2PlanNodeKind.GuestTransportReady), V2PlanDependencyReasonCode.VmLifecycle, "Guest transport requires a started VM.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.GuestTransportReady), state.TryGetNode(V2PlanNodeKind.PrepareGuestNetwork), V2PlanDependencyReasonCode.VmLifecycle, "Guest networking preparation requires guest transport.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.PrepareGuestNetwork), state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), V2PlanDependencyReasonCode.RoleOrdering, "Base remote access waits for guest network preparation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.GuestTransportReady), state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), V2PlanDependencyReasonCode.RoleOrdering, "Base remote access waits for guest transport.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), state.TryGetNode(V2PlanNodeKind.BaseRemoteAccessReady), V2PlanDependencyReasonCode.RoleOrdering, "Base remote access readiness follows configuration.", dependencies);

            var guestAnchor = state.TryGetNode(V2PlanNodeKind.PrepareGuestNetwork) ??
                              state.TryGetNode(V2PlanNodeKind.GuestTransportReady) ??
                              state.TryGetNode(V2PlanNodeKind.StartVm);

            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.GuestTransportReady), state.TryGetNode(V2PlanNodeKind.PrepareRouterNetwork), V2PlanDependencyReasonCode.VmLifecycle, "Router network preparation requires guest transport.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.PrepareRouterNetwork), state.TryGetNode(V2PlanNodeKind.InstallRouterRemoteAccessFeature), V2PlanDependencyReasonCode.RoleOrdering, "Router role installation waits for router network preparation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.InstallRouterRemoteAccessFeature), state.TryGetNode(V2PlanNodeKind.EnableRouterRouting), V2PlanDependencyReasonCode.RoleOrdering, "Router routing enablement waits for router role installation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.EnableRouterRouting), state.TryGetNode(V2PlanNodeKind.ConfigureRouterNat), V2PlanDependencyReasonCode.RoleOrdering, "Router NAT configuration waits for routing enablement.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ConfigureRouterNat), state.TryGetNode(V2PlanNodeKind.ValidateCrossSwitchRouting), V2PlanDependencyReasonCode.RoleOrdering, "Cross-switch validation waits for router NAT configuration.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ConfigureRouterNat), state.TryGetNode(V2PlanNodeKind.ValidateRouterEgress), V2PlanDependencyReasonCode.RoleOrdering, "Router egress validation waits for router NAT configuration.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ValidateCrossSwitchRouting), state.TryGetNode(V2PlanNodeKind.RouterReady), V2PlanDependencyReasonCode.RoleOrdering, "Router readiness waits for cross-switch validation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ValidateRouterEgress), state.TryGetNode(V2PlanNodeKind.RouterReady), V2PlanDependencyReasonCode.RoleOrdering, "Router readiness waits for egress validation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ConfigureRouterNat), state.TryGetNode(V2PlanNodeKind.RouterReady), V2PlanDependencyReasonCode.RoleOrdering, "Router readiness waits for NAT configuration.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.RouterReady), state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), V2PlanDependencyReasonCode.RoleOrdering, "Router base remote access waits for router readiness.", dependencies);
            // Base remote access must be configured on the VM's FINAL role state, i.e. after AD promotion or
            // domain join, otherwise the ready-set scheduler (which admits ConfigureBaseRemoteAccess as soon as
            // guest transport + network are ready) runs it concurrently with - or before - the promotion reboot,
            // where RDP/NLA/firewall settings do not durably survive. These gates keep it last for role VMs.
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.DomainReady), state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), V2PlanDependencyReasonCode.RoleOrdering, "Base remote access waits for first domain-controller readiness.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ReplicaDomainReady), state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), V2PlanDependencyReasonCode.RoleOrdering, "Base remote access waits for replica domain-controller readiness.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.JoinedDomainReady), state.TryGetNode(V2PlanNodeKind.ConfigureBaseRemoteAccess), V2PlanDependencyReasonCode.RoleOrdering, "Base remote access waits for domain-join readiness.", dependencies);
            AddDependencyIfPresent(guestAnchor, state.TryGetNode(V2PlanNodeKind.InstallAdDomainServicesFeature), V2PlanDependencyReasonCode.RoleOrdering, "AD DS feature installation requires guest bootstrap.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.InstallAdDomainServicesFeature), state.TryGetNode(V2PlanNodeKind.PromoteFirstDomainController), V2PlanDependencyReasonCode.RoleOrdering, "First domain-controller creation requires AD DS feature installation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.PromoteFirstDomainController), state.TryGetNode(V2PlanNodeKind.DomainReady), V2PlanDependencyReasonCode.DomainRequired, "Domain readiness follows first domain-controller creation.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.DomainReady), state.TryGetNode(V2PlanNodeKind.StabilizeDomainDns), V2PlanDependencyReasonCode.DomainRequired, "DNS stabilization waits for domain readiness.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.InstallAdDomainServicesFeature), state.TryGetNode(V2PlanNodeKind.PromoteReplicaDomainController), V2PlanDependencyReasonCode.RoleOrdering, "Replica promotion requires AD DS feature installation.", dependencies);
            AddDependencyIfPresent(guestAnchor, state.TryGetNode(V2PlanNodeKind.JoinDomain), V2PlanDependencyReasonCode.RoleOrdering, "Domain join requires guest bootstrap.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.PromoteReplicaDomainController), state.TryGetNode(V2PlanNodeKind.ReplicaDomainReady), V2PlanDependencyReasonCode.DomainRequired, "Replica readiness follows replica promotion.", dependencies);
            AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.JoinDomain), state.TryGetNode(V2PlanNodeKind.JoinedDomainReady), V2PlanDependencyReasonCode.DomainRequired, "Joined-domain readiness follows domain join.", dependencies);

            if (state.TopologyRoleIs("FirstDomainController") &&
                state.ResolvedDomain?.RelationKind is V2DomainRelationKind.Child or V2DomainRelationKind.Tree &&
                state.ResolvedDomain.ParentDomainId is not null &&
                domainReadyByDomainId.TryGetValue(state.ResolvedDomain.ParentDomainId, out var parentDomainReady))
            {
                AddDependencyIfPresent(parentDomainReady, state.TryGetNode(V2PlanNodeKind.PromoteFirstDomainController), V2PlanDependencyReasonCode.DomainRequired, "Dependent-domain creation waits for parent-domain readiness.", dependencies);
            }

            if (state.TopologyRoleIs("ReplicaDomainController") &&
                state.DomainId is not null &&
                domainReadyByDomainId.TryGetValue(state.DomainId, out var replicaDomainReady))
            {
                AddDependencyIfPresent(replicaDomainReady, state.TryGetNode(V2PlanNodeKind.PromoteReplicaDomainController), V2PlanDependencyReasonCode.DomainRequired, "Replica promotion waits for domain readiness.", dependencies);
            }

            if (state.RequiresDomainJoin &&
                state.DomainId is not null &&
                domainReadyByDomainId.TryGetValue(state.DomainId, out var memberDomainReady))
            {
                AddDependencyIfPresent(memberDomainReady, state.TryGetNode(V2PlanNodeKind.JoinDomain), V2PlanDependencyReasonCode.DomainRequired, "Domain join waits for domain readiness.", dependencies);
            }

            if (state.RequiresDomainJoin &&
                state.DomainId is not null &&
                dnsGateByDomainId.TryGetValue(state.DomainId, out var dnsGate))
            {
                AddDependencyIfPresent(dnsGate, state.TryGetNode(V2PlanNodeKind.JoinDomain), V2PlanDependencyReasonCode.DomainRequired, "Domain join waits for DNS stabilization.", dependencies);
            }

            if (state.TopologyRoleIs("ReplicaDomainController") &&
                state.DomainId is not null &&
                dnsGateByDomainId.TryGetValue(state.DomainId, out var replicaDnsGate))
            {
                AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ReplicaDomainReady), replicaDnsGate, V2PlanDependencyReasonCode.DomainRequired, "DNS stabilization waits for replica readiness.", dependencies);
            }

            if (routerRequired && state.RequiresRouterDependency && routerNode is not null)
            {
                AddDependencyIfPresent(routerNode, state.TryGetNode(V2PlanNodeKind.JoinDomain), V2PlanDependencyReasonCode.RouterRequired, "Cross-switch domain work waits for router readiness.", dependencies);
                AddDependencyIfPresent(routerNode, state.TryGetNode(V2PlanNodeKind.PromoteReplicaDomainController), V2PlanDependencyReasonCode.RouterRequired, "Cross-switch replica promotion waits for router readiness.", dependencies);
                AddDependencyIfPresent(routerNode, state.TryGetNode(V2PlanNodeKind.PromoteFirstDomainController), V2PlanDependencyReasonCode.RouterRequired, "Cross-switch dependent-domain creation waits for router readiness.", dependencies);
            }

            if (state.TopologyRoleIs("Router"))
            {
                foreach (var target in GetRouterValidationTargets(states))
                {
                    AddDependencyIfPresent(target.TryGetNode(V2PlanNodeKind.PrepareGuestNetwork), state.TryGetNode(V2PlanNodeKind.ValidateCrossSwitchRouting), V2PlanDependencyReasonCode.RoleOrdering, $"Cross-switch routing validation waits for guest network preparation on '{target.Vm.Name}'.", dependencies);
                }

                foreach (var target in GetRouterEgressTargets(states))
                {
                    AddDependencyIfPresent(target.TryGetNode(V2PlanNodeKind.PrepareGuestNetwork), state.TryGetNode(V2PlanNodeKind.ValidateRouterEgress), V2PlanDependencyReasonCode.RoleOrdering, $"Router egress validation waits for guest network preparation on '{target.Vm.Name}'.", dependencies);
                }
            }

            foreach (var explicitDependency in state.Vm.DependsOn ?? [])
            {
                var target = states.FirstOrDefault(candidate =>
                    string.Equals(candidate.Vm.VmId, explicitDependency, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Vm.Name, explicitDependency, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    continue;
                }

                var targetAnchor = target.GetCompletionAnchor();
                var sourceAnchor = state.GetGuestAnchor();
                AddDependencyIfPresent(targetAnchor, sourceAnchor, V2PlanDependencyReasonCode.ExplicitDependsOn, $"VM '{state.Vm.Name}' explicitly depends on '{target.Vm.Name}'.", dependencies);
            }

            foreach (var capabilityNode in state.CapabilityNodes.Values)
            {
                if (state.RequiresDomainJoin)
                {
                    AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.JoinedDomainReady), capabilityNode, V2PlanDependencyReasonCode.RoleOrdering, "Capability work on a domain member waits for joined-domain readiness.", dependencies);
                }
                else if (state.TopologyRoleIs("FirstDomainController"))
                {
                    AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.DomainReady), capabilityNode, V2PlanDependencyReasonCode.RoleOrdering, "Capability work on a first domain controller waits for domain readiness.", dependencies);
                }
                else if (state.TopologyRoleIs("ReplicaDomainController"))
                {
                    AddDependencyIfPresent(state.TryGetNode(V2PlanNodeKind.ReplicaDomainReady), capabilityNode, V2PlanDependencyReasonCode.RoleOrdering, "Capability work on a replica domain controller waits for replica readiness.", dependencies);
                }
                else
                {
                    AddDependencyIfPresent(state.GetGuestAnchor(), capabilityNode, V2PlanDependencyReasonCode.RoleOrdering, "Capability work waits for guest readiness.", dependencies);
                }
            }
        }
    }

    private static void EmitTrustDependencies(
        IReadOnlyList<V2ResolvedTrustPlanningContext> trusts,
        IReadOnlyList<V2PlanNode> nodes,
        IReadOnlyList<ResolvedVmState> states,
        List<V2PlanDependency> dependencies)
    {
        if (trusts.Count == 0)
        {
            return;
        }

        var nodeById = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var domainReadyByDomainId = states
            .Where(state => state.ResolvedDomain is not null && state.TryGetNode(V2PlanNodeKind.DomainReady) is not null)
            .ToDictionary(state => state.ResolvedDomain!.DomainId, state => state.TryGetNode(V2PlanNodeKind.DomainReady)!, StringComparer.OrdinalIgnoreCase);
        var statesByVmId = states.ToDictionary(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase);
        // Resolve the router the same deterministic way EmitDependencies does (lowest Name, then VmId, that carries a
        // RouterReady node). A routed cross-forest trust must not start its DNS/RPC work until this node is forwarding
        // between the two forests' subnets.
        var routerState = states
            .Where(state => state.IsRouterCapable)
            .OrderBy(state => state.Vm.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(state => state.TryGetNode(V2PlanNodeKind.RouterReady) is not null);
        var routerNode = routerState?.TryGetNode(V2PlanNodeKind.RouterReady);

        foreach (var trust in trusts)
        {
            nodeById.TryGetValue(trust.PrepareDnsNodeId, out var prepareDns);
            nodeById.TryGetValue(trust.CreateTrustNodeId, out var createTrust);
            nodeById.TryGetValue(trust.ValidateTrustNodeId, out var validateTrust);

            if (domainReadyByDomainId.TryGetValue(trust.SourceDomainId, out var sourceReady))
            {
                AddDependencyIfPresent(sourceReady, prepareDns, V2PlanDependencyReasonCode.TrustRequired, "Forest trust DNS preparation waits for source domain readiness.", dependencies);
            }

            if (domainReadyByDomainId.TryGetValue(trust.TargetDomainId, out var targetReady))
            {
                AddDependencyIfPresent(targetReady, prepareDns, V2PlanDependencyReasonCode.TrustRequired, "Forest trust DNS preparation waits for target domain readiness.", dependencies);
            }

            // When the two trust endpoints sit on different router-bridged subnets, the trust's cross-forest DNS
            // forwarding and RPC cannot flow until the router is routing between them. Domain readiness alone does not
            // imply that path exists, and a FirstDomainController never carries RequiresRouterDependency, so without
            // this edge the trust stage races the router and only survives on #916's transient DNS/RPC retries (a
            // masked, flaky green). Same-L2 trusts (both controllers on one switch, no router) and any trust whose
            // endpoints the router does not bridge are deliberately left untouched.
            if (routerNode is not null &&
                routerState is not null &&
                prepareDns is not null &&
                statesByVmId.TryGetValue(trust.SourceAnchorVmId, out var sourceAnchorState) &&
                statesByVmId.TryGetValue(trust.TargetAnchorVmId, out var targetAnchorState) &&
                TrustSpansRouterBridgedBoundary(routerState, sourceAnchorState, targetAnchorState))
            {
                AddDependencyIfPresent(routerNode, prepareDns, V2PlanDependencyReasonCode.RouterRequired, "Forest trust DNS preparation waits for router readiness.", dependencies);
            }

            AddDependencyIfPresent(prepareDns, createTrust, V2PlanDependencyReasonCode.TrustRequired, "Forest trust creation waits for DNS forwarding preparation.", dependencies);
            AddDependencyIfPresent(createTrust, validateTrust, V2PlanDependencyReasonCode.TrustRequired, "Forest trust validation waits for trust creation.", dependencies);
        }
    }

    /// <summary>
    /// Determines whether a forest trust spans a router-bridged boundary, meaning its two domain-controller
    /// endpoints sit on disjoint networks that the router carries a leg on each of. This is the structural
    /// condition under which the trust's cross-subnet DNS forwarding and RPC depend on the router forwarding
    /// traffic, so the trust's DNS-preparation node must wait for router readiness. It intentionally does not use
    /// the coarse router-required or egress classification: the only thing that matters here is whether the two
    /// endpoints can reach each other without the router. A same-L2 trust (shared network) returns false, as does a
    /// trust whose endpoints the router does not bridge.
    /// </summary>
    private static bool TrustSpansRouterBridgedBoundary(
        ResolvedVmState routerState,
        ResolvedVmState sourceState,
        ResolvedVmState targetState)
    {
        var sourceNetworks = sourceState.NetworkKeys;
        var targetNetworks = targetState.NetworkKeys;
        if (sourceNetworks.Count == 0 || targetNetworks.Count == 0)
        {
            return false;
        }

        // Shared network: the controllers already reach each other on one L2, so the router is not on the trust path.
        var targetNetworkSet = new HashSet<string>(targetNetworks, StringComparer.OrdinalIgnoreCase);
        if (sourceNetworks.Any(network => targetNetworkSet.Contains(network)))
        {
            return false;
        }

        // Disjoint networks: the edge is warranted only if the router actually bridges both, so it is the thing that
        // makes them reachable to each other.
        var routerNetworkSet = new HashSet<string>(routerState.NetworkKeys, StringComparer.OrdinalIgnoreCase);
        return routerNetworkSet.Overlaps(sourceNetworks) && routerNetworkSet.Overlaps(targetNetworks);
    }

    private static void EmitSwitchDependencies(
        IReadOnlyList<V2ResolvedNetworkSwitchRequirement> switchRequirements,
        IReadOnlyList<V2PlanNode> nodes,
        IReadOnlyList<ResolvedVmState> states,
        List<V2PlanDependency> dependencies)
    {
        if (switchRequirements.Count == 0)
        {
            return;
        }

        var nodeById = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var stateByVmId = states.ToDictionary(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in switchRequirements)
        {
            if (!nodeById.TryGetValue(requirement.NodeId, out var switchNode))
            {
                continue;
            }

            foreach (var vmId in requirement.AffectedVmIds)
            {
                if (!stateByVmId.TryGetValue(vmId, out var state))
                {
                    continue;
                }

                AddDependencyIfPresent(
                    switchNode,
                    state.TryGetNode(V2PlanNodeKind.ProvisionVm),
                    V2PlanDependencyReasonCode.SwitchRequired,
                    $"VM '{state.Vm.Name}' waits for switch '{requirement.SwitchName}' to be available.",
                    dependencies);
            }
        }
    }

    private static void AddDependencyIfPresent(
        V2PlanNode? from,
        V2PlanNode? to,
        V2PlanDependencyReasonCode reasonCode,
        string description,
        List<V2PlanDependency> dependencies)
    {
        if (from is null || to is null)
        {
            return;
        }

        if (dependencies.Any(existing =>
                existing.FromNodeId == from.NodeId &&
                existing.ToNodeId == to.NodeId &&
                existing.ReasonCode == reasonCode))
        {
            return;
        }

        dependencies.Add(new V2PlanDependency
        {
            FromNodeId = from.NodeId,
            ToNodeId = to.NodeId,
            ReasonCode = reasonCode,
            Description = description,
            IsBlockingGate = true
        });
    }

    private static void ApplyWaveHints(
        IReadOnlyList<V2PlanNode> nodes,
        IReadOnlyList<V2PlanDependency> dependencies,
        V2DeploymentProfile profile,
        IReadOnlyList<ResolvedVmState> states)
    {
        var byId = nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var stateByVmId = states.ToDictionary(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase);
        var dependencyMap = dependencies
            .GroupBy(dep => dep.ToNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var cache = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            node.WaveHint = ComputeWave(node.NodeId, profile, byId, stateByVmId, dependencyMap, cache);
        }
    }

    private static int ComputeWave(
        string nodeId,
        V2DeploymentProfile profile,
        IReadOnlyDictionary<string, V2PlanNode> nodes,
        IReadOnlyDictionary<string, ResolvedVmState> stateByVmId,
        IReadOnlyDictionary<string, V2PlanDependency[]> dependencyMap,
        IDictionary<string, int> cache)
    {
        if (cache.TryGetValue(nodeId, out var wave))
        {
            return wave;
        }

        var node = nodes[nodeId];
        if (node.Kind == V2PlanNodeKind.EnsureNetworkSwitch)
        {
            cache[nodeId] = 0;
            return 0;
        }

        var state = stateByVmId[node.VmId];
        var baseWave = GetBaseWave(profile, state, node);
        if (!dependencyMap.TryGetValue(nodeId, out var incoming) || incoming.Length == 0)
        {
            cache[nodeId] = baseWave;
            return baseWave;
        }

        var dependencyWave = incoming.Max(dep => ComputeWave(dep.FromNodeId, profile, nodes, stateByVmId, dependencyMap, cache) + 1);
        wave = Math.Max(baseWave, dependencyWave);
        cache[nodeId] = wave;
        return wave;
    }

    private static int GetBaseWave(V2DeploymentProfile profile, ResolvedVmState state, V2PlanNode node)
    {
        var roleOffset = profile switch
        {
            V2DeploymentProfile.Conservative => GetRoleOffsetConservative(state),
            V2DeploymentProfile.Balanced => GetRoleOffsetBalanced(state),
            _ => GetRoleOffsetAggressive(state)
        };

        return node.Kind switch
        {
            V2PlanNodeKind.EnsureNetworkSwitch => 0,
            V2PlanNodeKind.ProvisionVm => 10 + roleOffset,
            V2PlanNodeKind.EnableGuestServices => 20 + roleOffset,
            V2PlanNodeKind.StartVm => 30 + roleOffset,
            V2PlanNodeKind.GuestTransportReady => 40 + roleOffset,
            V2PlanNodeKind.PrepareGuestNetwork => 50 + roleOffset,
            V2PlanNodeKind.ConfigureBaseRemoteAccess => 120 + roleOffset,
            V2PlanNodeKind.BaseRemoteAccessReady => 121 + roleOffset,
            V2PlanNodeKind.PrepareRouterNetwork => 52 + roleOffset,
            V2PlanNodeKind.InstallRouterRemoteAccessFeature => 54 + roleOffset,
            V2PlanNodeKind.EnableRouterRouting => 56 + roleOffset,
            V2PlanNodeKind.ConfigureRouterNat => 58 + roleOffset,
            V2PlanNodeKind.ValidateCrossSwitchRouting => 60 + roleOffset,
            V2PlanNodeKind.ValidateRouterEgress => 62 + roleOffset,
            V2PlanNodeKind.InstallAdDomainServicesFeature => 65 + roleOffset,
            V2PlanNodeKind.PromoteFirstDomainController => 70 + roleOffset,
            V2PlanNodeKind.DomainReady => 80 + roleOffset,
            V2PlanNodeKind.RouterReady => 82 + roleOffset,
            V2PlanNodeKind.PromoteReplicaDomainController => 90 + roleOffset,
            V2PlanNodeKind.ReplicaDomainReady => 95 + roleOffset,
            V2PlanNodeKind.StabilizeDomainDns => 100 + roleOffset,
            V2PlanNodeKind.JoinDomain => 110 + roleOffset,
            V2PlanNodeKind.JoinedDomainReady => 120 + roleOffset,
            V2PlanNodeKind.ApplyCapabilityRole => 130 + roleOffset,
            V2PlanNodeKind.PrepareForestTrustDns => 122 + roleOffset,
            V2PlanNodeKind.CreateForestTrust => 123 + roleOffset,
            V2PlanNodeKind.ValidateForestTrust => 124 + roleOffset,
            _ => 100 + roleOffset
        };
    }

    private static int GetRoleOffsetConservative(ResolvedVmState state)
    {
        if (state.TopologyRoleIs("FirstDomainController"))
        {
            return 0;
        }

        if (state.TopologyRoleIs("Router"))
        {
            return 5;
        }

        if (state.TopologyRoleIs("ReplicaDomainController"))
        {
            return 10;
        }

        if (state.RequiresDomainJoin)
        {
            return 30;
        }

        return 40;
    }

    private static int GetRoleOffsetBalanced(ResolvedVmState state)
    {
        if (state.TopologyRoleIs("FirstDomainController"))
        {
            return 0;
        }

        if (state.TopologyRoleIs("Router"))
        {
            return 5;
        }

        if (state.TopologyRoleIs("ReplicaDomainController"))
        {
            return 10;
        }

        if (state.RequiresDomainJoin)
        {
            return 15;
        }

        return 20;
    }

    private static int GetRoleOffsetAggressive(ResolvedVmState state)
    {
        if (state.TopologyRoleIs("FirstDomainController"))
        {
            return 0;
        }

        if (state.TopologyRoleIs("Router"))
        {
            return 2;
        }

        if (state.TopologyRoleIs("ReplicaDomainController"))
        {
            return 5;
        }

        if (state.RequiresDomainJoin)
        {
            return 8;
        }

        return 10;
    }

    private static IReadOnlyList<V2SchedulingWave> BuildWaves(IReadOnlyList<V2PlanNode> nodes)
    {
        return nodes
            .GroupBy(node => node.WaveHint)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var nodeIds = group.Select(node => node.NodeId).OrderBy(nodeId => nodeId, StringComparer.Ordinal).ToArray();
                var workloadSummary = string.Join(", ", group.Select(node => node.WorkloadClass).Distinct().OrderBy(value => value));
                return new V2SchedulingWave
                {
                    WaveNumber = group.Key,
                    DisplayName = $"Wave {group.Key}",
                    NodeIds = nodeIds,
                    Summary = $"{group.Count()} node(s): {workloadSummary}"
                };
            })
            .ToArray();
    }

    private static V2PlanNode AddNode(
        List<V2PlanNode> nodes,
        ResolvedVmState state,
        V2PlanNodeKind kind,
        string displayName,
        V2WorkloadClass workloadClass,
        string? capabilityRole = null)
    {
        var suffix = capabilityRole is null
            ? kind.ToString()
            : $"{kind}:{capabilityRole}";
        var node = new V2PlanNode
        {
            NodeId = $"vm:{state.Vm.VmId}:{suffix}",
            VmId = state.Vm.VmId,
            VmName = state.Vm.Name,
            Kind = kind,
            DisplayName = displayName,
            WorkloadClass = workloadClass,
            CapabilityRole = capabilityRole,
            RoleContext = new V2VmRoleContext
            {
                TopologyRole = state.TopologyRole,
                MembershipMode = state.MembershipMode,
                CapabilityRoles = state.KnownCapabilityRoles.ToArray()
            }
        };
        nodes.Add(node);
        return node;
    }

    private static V2PlanNode AddTrustNode(
        List<V2PlanNode> nodes,
        V2ResolvedTrustPlanningContext trust,
        V2PlanNodeKind kind,
        string displayName,
        V2WorkloadClass workloadClass)
    {
        var nodeId = kind switch
        {
            V2PlanNodeKind.PrepareForestTrustDns => trust.PrepareDnsNodeId,
            V2PlanNodeKind.CreateForestTrust => trust.CreateTrustNodeId,
            V2PlanNodeKind.ValidateForestTrust => trust.ValidateTrustNodeId,
            _ => BuildTrustNodeId(trust.TrustId, kind)
        };

        var node = new V2PlanNode
        {
            NodeId = nodeId,
            VmId = trust.SourceAnchorVmId,
            VmName = trust.SourceAnchorVmName,
            Kind = kind,
            DisplayName = displayName,
            WorkloadClass = workloadClass,
            TrustId = trust.TrustId
        };
        nodes.Add(node);
        return node;
    }

    private static string BuildTrustNodeId(string trustId, V2PlanNodeKind kind)
        => $"trust:{trustId}:{kind}";

    private static V2WorkloadClass GetCapabilityWorkload(string capabilityRole)
    {
        return capabilityRole switch
        {
            "Pki" => V2WorkloadClass.HeavyGuest,
            "Sql" => V2WorkloadClass.HeavyGuest,
            _ => V2WorkloadClass.MediumGuest
        };
    }

    private static string GetFirstDomainControllerDisplayName(ResolvedVmState state)
    {
        return state.ResolvedDomain?.RelationKind switch
        {
            V2DomainRelationKind.Child => "Create child domain controller",
            V2DomainRelationKind.Tree => "Create tree domain controller",
            _ => "Create first domain controller"
        };
    }

    private static VhdxCatalogItem? ResolveCatalogItem(VmTemplate vm, IReadOnlyList<VhdxCatalogItem> catalogItems)
    {
        if (!string.IsNullOrWhiteSpace(vm.VhdxId))
        {
            return catalogItems.FirstOrDefault(item => string.Equals(item.Id, vm.VhdxId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(vm.VhdPath))
        {
            var byPath = catalogItems.FirstOrDefault(item => string.Equals(item.Path, vm.VhdPath, StringComparison.OrdinalIgnoreCase));
            if (byPath is not null)
            {
                return byPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(vm.VhdxSignature))
        {
            var matches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems).ToList();
            if (matches.Count == 1)
            {
                return matches[0];
            }
        }

        return null;
    }

    private static V2DeploymentProfile ResolveProfile(string? persistedValue, string? defaultValue, List<V2PlanIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(persistedValue) &&
            V2SchedulerPolicyCatalog.TryResolveProfile(persistedValue, out var persistedProfile))
        {
            return persistedProfile;
        }

        if (!string.IsNullOrWhiteSpace(defaultValue) &&
            V2SchedulerPolicyCatalog.TryResolveProfile(defaultValue, out var defaultProfile))
        {
            return defaultProfile;
        }

        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            issues.Add(new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Warning,
                Code = "deployment-profile-default-invalid",
                Message = $"Default deployment profile '{defaultValue}' is invalid. Falling back to Conservative.",
                SuggestedAction = "Use Conservative, Balanced, or Aggressive as the deploy-time default."
            });
        }

        return V2DeploymentProfile.Conservative;
    }

    private static string ResolveDefaultProfileName(string? defaultValue)
    {
        return !string.IsNullOrWhiteSpace(defaultValue) &&
               V2SchedulerPolicyCatalog.TryResolveProfile(defaultValue, out var profile)
            ? V2SchedulerPolicyCatalog.GetCanonicalProfileName(profile)
            : V2SchedulerPolicyCatalog.GetCanonicalProfileName(V2DeploymentProfile.Conservative);
    }

    private static V2DeploymentProfile ResolveDefaultProfile(string? defaultValue)
    {
        return !string.IsNullOrWhiteSpace(defaultValue) &&
               V2SchedulerPolicyCatalog.TryResolveProfile(defaultValue, out var profile)
            ? profile
            : V2DeploymentProfile.Conservative;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSwitchType(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSupportedSwitchType(string? value)
        => V2SwitchTypeCatalog.TryNormalize(value, out var switchType) ? switchType : null;

    private static bool SwitchTypeIs(string? switchType, string expectedType)
        => string.Equals(switchType, expectedType, StringComparison.OrdinalIgnoreCase);

    private static string BuildSwitchNodeId(string switchName)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var ch in switchName.Trim())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }

        var normalized = builder.ToString().Trim('-');
        return $"switch:{(normalized.Length == 0 ? "unnamed" : normalized)}:EnsureNetworkSwitch";
    }

    private static IReadOnlyList<ResolvedVmState> GetRouterValidationTargets(IReadOnlyList<ResolvedVmState> states)
    {
        return states
            .Where(state => state.RequiresRouterDependency && !state.IsRouterCapable)
            .GroupBy(
                state => state.ResolvedNics
                    .Select(nic => Normalize(nic.NetworkId) ?? Normalize(nic.EffectiveSwitchName))
                    .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key)) ?? state.Vm.VmId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(state => state.Vm.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
    }

    private static IReadOnlyList<ResolvedVmState> GetRouterEgressTargets(IReadOnlyList<ResolvedVmState> states)
    {
        return states
            .Where(state => state.ExpectsRouterEgress && !state.IsRouterCapable)
            .GroupBy(
                state => state.ResolvedNics
                    .Select(nic => Normalize(nic.NetworkId) ?? Normalize(nic.EffectiveSwitchName))
                    .FirstOrDefault(key => !string.IsNullOrWhiteSpace(key)) ?? state.Vm.VmId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(state => state.Vm.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(state => state.Vm.VmId, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
    }

    private static string? NormalizeTopologyRole(string? value)
    {
        var normalized = Normalize(value);
        if (string.Equals(normalized, "RootDomainController", StringComparison.OrdinalIgnoreCase))
        {
            return "FirstDomainController";
        }

        if (string.Equals(normalized, "MemberServer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "StandaloneServer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private static string? NormalizeMembershipMode(VmTemplate vm)
    {
        var explicitMode = V2MembershipModeCatalog.Normalize(vm.MembershipMode);
        if (!string.IsNullOrWhiteSpace(explicitMode))
        {
            return explicitMode;
        }

        if (string.Equals(vm.TopologyRole, "MemberServer", StringComparison.OrdinalIgnoreCase))
        {
            return V2MembershipModeCatalog.DomainMember;
        }

        if (string.Equals(vm.TopologyRole, "StandaloneServer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(vm.TopologyRole, "Router", StringComparison.OrdinalIgnoreCase))
        {
            return V2MembershipModeCatalog.Standalone;
        }

        return string.IsNullOrWhiteSpace(vm.DomainId)
            ? null
            : V2MembershipModeCatalog.DomainMember;
    }

    private sealed class ResolvedVmState
    {
        public ResolvedVmState(
            VmTemplate vm,
            string? topologyRole,
            string? membershipMode,
            IReadOnlyList<string> knownCapabilityRoles,
            VhdxCatalogItem? catalogItem,
            VhdxBootstrapProfile? bootstrapProfile,
            IReadOnlyList<V2ResolvedVmNetworkInterface> resolvedNics)
        {
            Vm = vm;
            TopologyRole = topologyRole;
            MembershipMode = membershipMode;
            KnownCapabilityRoles = knownCapabilityRoles;
            CatalogItem = catalogItem;
            BootstrapProfile = bootstrapProfile;
            ResolvedNics = resolvedNics;
            NetworkKeys = resolvedNics
                .Select(nic => Normalize(nic.NetworkId) ?? Normalize(nic.EffectiveSwitchName))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();
        }

        public VmTemplate Vm { get; }

        public string? TopologyRole { get; }

        public string? MembershipMode { get; }

        public string? DomainId { get; set; }

        public IReadOnlyList<string> KnownCapabilityRoles { get; }

        public VhdxCatalogItem? CatalogItem { get; }

        public VhdxBootstrapProfile? BootstrapProfile { get; }

        public IReadOnlyList<V2ResolvedVmNetworkInterface> ResolvedNics { get; }

        public IReadOnlyList<string> NetworkKeys { get; }

        public Dictionary<V2PlanNodeKind, V2PlanNode> Nodes { get; } = new();

        public Dictionary<string, V2PlanNode> CapabilityNodes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool RequiresGuestWork { get; set; }

        public bool RequiresDomainJoin { get; set; }

        public bool IsRouterCapable { get; set; }

        public bool RequiresRouterDependency { get; set; }

        public bool ExpectsRouterEgress { get; set; }

        public bool RouterProvidesEgress { get; set; }

        public bool HasExternalSwitchAttachment { get; set; }

        public bool HasNonExternalSwitchAttachment { get; set; }

        public V2ResolvedDomainPlanningContext? ResolvedDomain { get; set; }

        public V2ResolvedForestPlanningContext? ResolvedForest { get; set; }

        public string? EffectiveBootstrapSlot { get; set; }

        public string? EffectiveDomainAdminSlot { get; set; }

        public string? EffectiveDomainJoinSlot { get; set; }

        public string? EffectiveDsrmSlot { get; set; }

        public string? EffectiveParentDomainAdminSlot { get; set; }

        /// <summary>
        /// True only when at least one NIC carries in-guest network configuration to apply. Shares
        /// <see cref="NicRequiresGuestConfiguration"/> with <see cref="DetermineGuestWorkRequirement"/> so the
        /// PrepareGuestNetwork node is emitted only alongside the GuestTransportReady gate that must precede it.
        /// </summary>
        public bool RequiresNetworkBootstrap => ResolvedNics.Any(NicRequiresGuestConfiguration);

        public bool TopologyRoleIs(string role)
            => string.Equals(TopologyRole, role, StringComparison.OrdinalIgnoreCase);

        public V2PlanNode? TryGetNode(V2PlanNodeKind kind)
            => Nodes.TryGetValue(kind, out var node) ? node : null;

        public V2PlanNode? GetGuestAnchor()
            => TryGetNode(V2PlanNodeKind.JoinedDomainReady) ??
               TryGetNode(V2PlanNodeKind.ReplicaDomainReady) ??
               TryGetNode(V2PlanNodeKind.StabilizeDomainDns) ??
               TryGetNode(V2PlanNodeKind.RouterReady) ??
               TryGetNode(V2PlanNodeKind.PrepareGuestNetwork) ??
               TryGetNode(V2PlanNodeKind.ValidateRouterEgress) ??
               TryGetNode(V2PlanNodeKind.ValidateCrossSwitchRouting) ??
               TryGetNode(V2PlanNodeKind.ConfigureRouterNat) ??
               TryGetNode(V2PlanNodeKind.EnableRouterRouting) ??
               TryGetNode(V2PlanNodeKind.InstallRouterRemoteAccessFeature) ??
               TryGetNode(V2PlanNodeKind.PrepareRouterNetwork) ??
               TryGetNode(V2PlanNodeKind.GuestTransportReady) ??
               TryGetNode(V2PlanNodeKind.StartVm);

        public V2PlanNode? GetCompletionAnchor()
            => TryGetNode(V2PlanNodeKind.DomainReady) ??
               TryGetNode(V2PlanNodeKind.BaseRemoteAccessReady) ??
               TryGetNode(V2PlanNodeKind.JoinedDomainReady) ??
               TryGetNode(V2PlanNodeKind.StabilizeDomainDns) ??
               TryGetNode(V2PlanNodeKind.ReplicaDomainReady) ??
               TryGetNode(V2PlanNodeKind.RouterReady) ??
               TryGetNode(V2PlanNodeKind.PromoteReplicaDomainController) ??
               TryGetNode(V2PlanNodeKind.JoinDomain) ??
               CapabilityNodes.Values.OrderBy(node => node.NodeId, StringComparer.Ordinal).LastOrDefault() ??
               GetGuestAnchor() ??
               TryGetNode(V2PlanNodeKind.StartVm) ??
               TryGetNode(V2PlanNodeKind.ProvisionVm);

        public V2ResolvedVmPlanningContext ToContext()
        {
            return new V2ResolvedVmPlanningContext
            {
                VmId = Vm.VmId,
                VmName = Vm.Name,
                TopologyRole = TopologyRole,
                MembershipMode = MembershipMode,
                DomainId = DomainId,
                CapabilityRoles = KnownCapabilityRoles.ToArray(),
                ResolvedCatalogItemId = CatalogItem?.Id,
                ResolvedCatalogPath = CatalogItem?.Path,
                BootstrapProfileRef = Vm.BootstrapProfileRef,
                HasBootstrapProfile = BootstrapProfile is not null,
                EffectiveBootstrapUser = BootstrapProfile?.ExpectedLocalUser,
                EffectiveBootstrapCredentialSlot = EffectiveBootstrapSlot,
                EffectiveDomainAdminCredentialSlot = EffectiveDomainAdminSlot,
                EffectiveDomainJoinCredentialSlot = EffectiveDomainJoinSlot,
                EffectiveDsrmCredentialSlot = EffectiveDsrmSlot,
                EffectiveParentDomainAdminCredentialSlot = EffectiveParentDomainAdminSlot,
                RequiresGuestWork = RequiresGuestWork,
                RequiresDomainJoin = RequiresDomainJoin,
                IsRouterCapable = IsRouterCapable,
                RequiresRouterDependency = RequiresRouterDependency,
                ExpectsRouterEgress = ExpectsRouterEgress,
                Nics = ResolvedNics.ToArray()
            };
        }
    }

    private sealed class NetworkSwitchRequirementBuilder
    {
        private readonly HashSet<string> _networkIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _affectedVmIds = new(StringComparer.OrdinalIgnoreCase);

        public NetworkSwitchRequirementBuilder(string switchName, string switchType, string? externalAdapterName)
        {
            SwitchName = switchName.Trim();
            SwitchType = switchType.Trim();
            ExternalAdapterName = Normalize(externalAdapterName);
        }

        public string SwitchName { get; }

        public string SwitchType { get; }

        public string? ExternalAdapterName { get; private set; }

        public void AddNetwork(string? networkId)
        {
            var normalized = Normalize(networkId);
            if (normalized is not null)
            {
                _networkIds.Add(normalized);
            }
        }

        public void AddVm(string vmId)
        {
            var normalized = Normalize(vmId);
            if (normalized is not null)
            {
                _affectedVmIds.Add(normalized);
            }
        }

        public void SetExternalAdapterName(string? externalAdapterName)
        {
            ExternalAdapterName ??= Normalize(externalAdapterName);
        }

        public V2ResolvedNetworkSwitchRequirement Build()
            => new()
            {
                NodeId = BuildSwitchNodeId(SwitchName),
                SwitchName = SwitchName,
                SwitchType = SwitchType,
                ExternalAdapterName = ExternalAdapterName,
                NetworkIds = _networkIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
                AffectedVmIds = _affectedVmIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
            };
    }
}

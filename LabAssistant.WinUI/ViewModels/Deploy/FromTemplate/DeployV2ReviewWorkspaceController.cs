using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Business.Templates;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployV2ReviewWorkspaceController
{
    private readonly DeployV2ReviewWorkspaceViewModel _workspace;
    private readonly DeployV2ReviewProjectionService _projectionService;
    private readonly IDeployFromTemplateV2ReviewHost _host;

    public DeployV2ReviewWorkspaceController(
        DeployV2ReviewWorkspaceViewModel workspace,
        DeployV2ReviewProjectionService projectionService,
        IDeployFromTemplateV2ReviewHost host)
    {
        _workspace = workspace;
        _projectionService = projectionService;
        _host = host;
    }

    public async Task RefreshPlanAsync(LabTemplate template)
    {
        _workspace.BeginPlanning();
        _host.ApplyWorkspaceState();

        // One operationId scopes this whole plan-review build so its start, credential-resolution summary,
        // and terminal correlate in the log. Finding 91: without this a blocked plan-review is a silent
        // black hole in the telemetry between template.library-load and the wedge.
        var operationId = Guid.NewGuid().ToString("N");
        var logger = _host.StructuredLogger;

        logger.Log(
            LaStatus.DeployOrchestration_BuildingPlan,
            operationId,
            "started",
            new Dictionary<string, object?>
            {
                ["template"] = template.Name,
                ["vmCount"] = template.VmTemplates.Count
            });

        try
        {
            await _host.EnsureReferenceDataAsync(forceRefresh: false);
            var slotDefinitions = _host.LoadLocalCredentialSlotDefinitions();
            var slotValues = BuildResolvedSlotDictionary(slotDefinitions, logger, operationId);
            var plan = await _host.BuildV2PlanAsync(
                template,
                slotValues.Keys.ToList(),
                _workspace.ExternalSwitchAdapterMappings);
            var projection = _projectionService.Build(template, plan, slotDefinitions, slotValues);

            _workspace.ApplyProjection(
                projection.Summary,
                projection.Blockers,
                projection.CredentialSlots,
                projection.Waves,
                projection.Diagnostics,
                plan,
                slotValues);

            _host.ApplyWorkspaceState();

            EmitPlanBuildTerminal(logger, operationId, plan, slotValues);
        }
        catch (Exception ex)
        {
            _workspace.SetPlanningFailed($"V2 planning failed. {ex.Message}");
            _host.ApplyWorkspaceState();
            logger.Log(
                LaStatus.DeployOrchestration_PlanBuildFailed,
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["error"] = ex.Message,
                    ["errorType"] = ex.GetType().Name
                });
        }
    }

    /// <summary>
    /// Emits the credential-resolution summary (resolved / unresolved keys by key, plus the unresolved
    /// requirement kinds and blocking issue codes) and exactly one terminal event for a successfully
    /// built plan. The terminal keys off the same startability gate the Start button reads
    /// (<see cref="DeployV2ReviewWorkspaceViewModel.CanStartDeploy"/>) so the log and the UI never disagree.
    /// </summary>
    private void EmitPlanBuildTerminal(
        IStructuredLogger logger,
        string operationId,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> resolvedSlotValues)
    {
        var unresolvedCredentialKeys = plan.UnresolvedRequirements
            .Where(requirement => requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot)
            .Select(requirement => requirement.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unresolvedRequirements = plan.UnresolvedRequirements
            .Select(requirement => $"{requirement.Kind}:{requirement.Key}")
            .ToList();
        var blockingIssueCodes = plan.Issues
            .Where(issue => issue.Severity == V2PlanIssueSeverity.Blocking)
            .Select(issue => issue.Code)
            .ToList();
        // Pair each blocking issue code with the VM it fired on so the emitted line answers, per VM, whether the
        // blocker is "credential-slot-missing" (bootstrapSlot null, the missing path that ignores the store) or
        // "credential-slot-unresolved" (a real shared key the store just lacks). That distinction is what tells
        // apart the store-irrelevant 13-fanout wedge from an ordinary unresolved-key case.
        var blockingIssueDetails = plan.Issues
            .Where(issue => issue.Severity == V2PlanIssueSeverity.Blocking)
            .Select(issue => $"{issue.Code}@{(string.IsNullOrWhiteSpace(issue.VmId) ? "<global>" : issue.VmId)}")
            .ToList();
        var resolvedKeys = resolvedSlotValues.Keys
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Per credential-slot requirement: its Key, the VMs it affects, and whether the store resolved that Key.
        // Read alongside the credential-store probe this decides the H-A wedge: the collapsed 'disk...' row shows
        // resolved=false here while the probe shows whether the store even holds/decrypts it on this replan.
        var credentialRequirementDetails = plan.UnresolvedRequirements
            .Where(requirement => requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot)
            .Select(requirement =>
                $"{requirement.Key}|vms={string.Join("/", requirement.AffectedVmIds)}|" +
                $"resolved={resolvedSlotValues.ContainsKey(requirement.Key)}")
            .ToList();

        // Per-VM planning inputs that decide the credential-slot count. bootstrapSlot null (authored
        // localBootstrap absent AND no catalog bootstrap profile) is what expands the plan from one shared
        // slot to a distinct requirement per purpose/VM. Surfacing the catalog match outcome (matched id +
        // signature, or "<miss>" + the searched VhdxId) and the two bootstrap sources separately
        // (template-authored vs catalog-profile) makes a wedged plan-review a one-line diagnosis of
        // template-load-drop vs catalog-miss vs base re-identification, live, with no artifact hunt.
        var vmContexts = plan.Context.Vms;
        var catalogMissVmIds = vmContexts
            .Where(vm => string.IsNullOrWhiteSpace(vm.ResolvedCatalogItemId))
            .Select(vm => vm.VmId)
            .ToList();
        var bootstrapSlotMissVmIds = vmContexts
            .Where(vm => vm.RequiresGuestWork && string.IsNullOrWhiteSpace(vm.EffectiveBootstrapCredentialSlot))
            .Select(vm => vm.VmId)
            .ToList();
        var vmDiagnostics = vmContexts
            .Select(vm =>
                $"{vm.VmId}[catalog={(string.IsNullOrWhiteSpace(vm.ResolvedCatalogItemId) ? "<miss>" : vm.ResolvedCatalogItemId)}," +
                $"catalogMatch={vm.CatalogMatchOutcome ?? "<null>"}," +
                $"searchedVhdxId={(string.IsNullOrWhiteSpace(vm.SearchedVhdxId) ? "<null>" : vm.SearchedVhdxId)}," +
                $"catalogSig={(string.IsNullOrWhiteSpace(vm.ResolvedCatalogSignature) ? "<null>" : vm.ResolvedCatalogSignature)}," +
                $"bootstrapProfile={vm.HasBootstrapProfile}," +
                $"bootstrapAuthored={(string.IsNullOrWhiteSpace(vm.TemplateAuthoredBootstrapSlot) ? "<null>" : vm.TemplateAuthoredBootstrapSlot)}," +
                $"bootstrapCatalog={(string.IsNullOrWhiteSpace(vm.CatalogProfileBootstrapSlot) ? "<null>" : vm.CatalogProfileBootstrapSlot)}," +
                $"bootstrapSource={vm.BootstrapSlotSource ?? "<null>"}," +
                $"bootstrapSlot={(string.IsNullOrWhiteSpace(vm.EffectiveBootstrapCredentialSlot) ? "<null>" : vm.EffectiveBootstrapCredentialSlot)}," +
                $"guestWork={vm.RequiresGuestWork}]")
            .ToList();

        logger.Log(
            LaStatus.DeployOrchestration_CredentialResolution,
            operationId,
            "resolved",
            new Dictionary<string, object?>
            {
                ["resolvedSlotKeys"] = string.Join(", ", resolvedKeys),
                ["resolvedSlotCount"] = resolvedKeys.Count,
                ["unresolvedCredentialKeys"] = string.Join(", ", unresolvedCredentialKeys),
                ["unresolvedCredentialCount"] = unresolvedCredentialKeys.Count,
                ["unresolvedRequirements"] = string.Join(", ", unresolvedRequirements),
                ["blockingIssueCodes"] = string.Join(", ", blockingIssueCodes),
                ["blockingIssueDetails"] = string.Join(", ", blockingIssueDetails),
                ["credentialRequirementDetails"] = string.Join(" ; ", credentialRequirementDetails),
                ["vmCount"] = vmContexts.Count,
                ["catalogMissVmIds"] = string.Join(", ", catalogMissVmIds),
                ["bootstrapSlotMissVmIds"] = string.Join(", ", bootstrapSlotMissVmIds),
                ["vmDiagnostics"] = string.Join(" ; ", vmDiagnostics)
            });

        var startable = _workspace.CanStartDeploy;
        var terminalContext = new Dictionary<string, object?>
        {
            ["success"] = plan.Success,
            ["startable"] = startable,
            ["blockingIssueCount"] = blockingIssueCodes.Count,
            ["unresolvedRequirementCount"] = plan.UnresolvedRequirements.Count
        };

        if (startable)
        {
            logger.Log(LaStatus.DeployOrchestration_PlanReady, operationId, "success", terminalContext);
        }
        else
        {
            logger.Log(LaStatus.DeployOrchestration_PlanBlocked, operationId, "blocked", terminalContext);
        }
    }

    public void SelectCredentialSlot(string? slotKey)
    {
        _workspace.SelectCredentialSlot(slotKey);
        _host.ApplyWorkspaceState();
    }

    public void SetExternalSwitchAdapterMapping(string switchName, string adapterName)
    {
        _workspace.SetExternalSwitchAdapterMapping(switchName, adapterName);
        _host.ApplyWorkspaceState();
    }

    public async Task SaveCredentialSlotAsync(string username, string password)
    {
        // Finding 91 path B: instrument the save/upsert so the fill loop is diagnosable. Emit the selected key
        // (or a skip when none is selected) and the store's slot-key list before and after the upsert, so a
        // wedge where the fill never lands the key in the store is a one-line read: save-skipped, wrong key, or
        // upsert-ran-yet-store-unchanged. Correlate with the next replan's credential-store probe.
        var operationId = Guid.NewGuid().ToString("N");
        var logger = _host.StructuredLogger;
        var selectedKey = _workspace.SelectedCredentialSlotKey;

        if (string.IsNullOrWhiteSpace(selectedKey))
        {
            logger.Log(
                LaStatus.DeployOrchestration_CredentialSlotSaved,
                operationId,
                "save-skipped",
                new Dictionary<string, object?>
                {
                    ["reason"] = "no slot selected",
                    ["selectedSlotKey"] = "<none>"
                });
            _workspace.SetPlanningFailed("Select a credential slot before saving.");
            _host.ApplyWorkspaceState();
            return;
        }

        var storeKeysBefore = _host.LoadLocalCredentialSlotDefinitions()
            .Select(definition => definition.SlotKey)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _host.UpsertLocalCredentialSlot(selectedKey, username, password);
        var storeKeysAfter = _host.LoadLocalCredentialSlotDefinitions()
            .Select(definition => definition.SlotKey)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        logger.Log(
            LaStatus.DeployOrchestration_CredentialSlotSaved,
            operationId,
            "saved",
            new Dictionary<string, object?>
            {
                ["selectedSlotKey"] = selectedKey,
                ["storeCountBefore"] = storeKeysBefore.Count,
                ["storeKeysBefore"] = string.Join(", ", storeKeysBefore),
                ["storeCountAfter"] = storeKeysAfter.Count,
                ["storeKeysAfter"] = string.Join(", ", storeKeysAfter)
            });

        if (_host.ActiveTemplateDocument is not null)
        {
            await RefreshPlanAsync(_host.ActiveTemplateDocument.Template);
        }
    }

    public async Task<V2RuntimeExecutionResult> StartDeployAsync(LabTemplate template)
    {
        if (_workspace.CurrentPlan is null)
        {
            throw new InvalidOperationException("V2 deployment cannot start until planning succeeds.");
        }

        return await _host.ExecuteV2DeployAsync(
            template,
            _workspace.CurrentPlan,
            _workspace.ResolvedCredentialSlotValues,
            _workspace.CreateBaseRemoteAccessOptions(),
            new MultiVmDeploymentContext());
    }

    private IReadOnlyDictionary<string, V2RuntimeCredential> BuildResolvedSlotDictionary(
        IReadOnlyList<LabAssistant.Models.Configuration.LocalCredentialSlotDefinition> slotDefinitions,
        IStructuredLogger logger,
        string operationId)
    {
        var resolved = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);
        // Classify EACH store slot as found-ok / record-absent / decrypt-threw (finding 91 path A). This is the
        // per-replan proof of whether a just-saved bootstrap key is actually readable back on the next plan build:
        // if the fill loop saves 'disk...' yet this keeps printing record-absent, the store read path is the bug.
        var storeSlotOutcomes = new List<string>();
        foreach (var definition in slotDefinitions)
        {
            try
            {
                if (_host.TryGetLocalCredentialSlotValue(definition.SlotKey, out var credential))
                {
                    resolved[definition.SlotKey] = credential;
                    storeSlotOutcomes.Add($"{definition.SlotKey}=found-ok");
                }
                else
                {
                    storeSlotOutcomes.Add($"{definition.SlotKey}=record-absent");
                }
            }
            catch (Exception ex)
            {
                // A present record whose password fails to decrypt throws here. Name the exact slot and log it
                // BEFORE it propagates so a decrypt failure is a named diagnosis, not an opaque "V2 planning
                // failed" red row. Rethrow to preserve the existing failed-path behavior unchanged.
                storeSlotOutcomes.Add($"{definition.SlotKey}=decrypt-threw:{ex.GetType().Name}");
                logger.Log(
                    LaStatus.DeployOrchestration_CredentialStoreProbe,
                    operationId,
                    "probed",
                    new Dictionary<string, object?>
                    {
                        ["storeSlotOutcomes"] = string.Join(", ", storeSlotOutcomes),
                        ["storeSlotCount"] = slotDefinitions.Count,
                        ["resolvedSlotKeys"] = string.Join(", ", resolved.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase)),
                        ["threwOnSlot"] = definition.SlotKey
                    });
                throw;
            }
        }

        logger.Log(
            LaStatus.DeployOrchestration_CredentialStoreProbe,
            operationId,
            "probed",
            new Dictionary<string, object?>
            {
                ["storeSlotOutcomes"] = string.Join(", ", storeSlotOutcomes),
                ["storeSlotCount"] = slotDefinitions.Count,
                ["resolvedSlotKeys"] = string.Join(", ", resolved.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            });

        return resolved;
    }
}

internal interface IDeployFromTemplateV2ReviewHost
{
    TemplateEditorDocument? ActiveTemplateDocument { get; }

    /// <summary>
    /// Structured event sink for operationId-scoped plan-review build telemetry (finding 91).
    /// </summary>
    IStructuredLogger StructuredLogger { get; }

    Task EnsureReferenceDataAsync(bool forceRefresh);

    IReadOnlyList<LabAssistant.Models.Configuration.LocalCredentialSlotDefinition> LoadLocalCredentialSlotDefinitions();

    bool TryGetLocalCredentialSlotValue(string slotKey, out V2RuntimeCredential credential);

    void UpsertLocalCredentialSlot(string slotKey, string username, string password);

    Task<V2PlanBuildResult> BuildV2PlanAsync(
        LabTemplate template,
        IReadOnlyCollection<string> resolvedCredentialSlotKeys,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings);

    Task<V2RuntimeExecutionResult> ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
        V2BaseRemoteAccessOptions baseRemoteAccessOptions,
        MultiVmDeploymentContext deploymentContext);

    void ApplyWorkspaceState();
}

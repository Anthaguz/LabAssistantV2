using LabAssistant.Business.Templates;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using System.Collections.ObjectModel;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceViewModel
{
    public TemplateLibraryItem? SelectedTemplateLibraryItem { get; private set; }

    public string? SelectedTemplateFilePath { get; private set; }

    public TemplateEditorDocument? ActiveTemplateDocument { get; private set; }

    public string TemplateSummaryText { get; private set; } =
        "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";

    public string TemplateRemediationText { get; private set; } =
        "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";

    public string ActionStatusText { get; private set; } = "No action selected.";

    public ObservableCollection<DeployIssueRow> IssueRows { get; } = [];

    public ObservableCollection<string> SharedIssueSummaries { get; } = [];

    public ObservableCollection<DeployVmResultRow> ResultRows { get; } = [];

    public string ReadinessSummaryText { get; private set; } =
        "Select a template to evaluate readiness and run deploy.";

    public string SharedIssuesSummaryText { get; private set; } =
        "Shared review items appear here when multiple VMs need the same remediation.";

    public string GlobalIssuesBadgeText { get; private set; } = "Issues: 0";

    public bool IsEvaluatingReadiness { get; private set; }

    public bool IsStarting { get; private set; }

    public string LifecycleState { get; private set; } = "Idle";

    public int ProgressPercent { get; private set; }

    public string ProgressSummary { get; private set; } = "No deployment started.";

    public bool ShowAllVmRows { get; private set; }

    private readonly Dictionary<string, DeployVmProgressState> _progressByVm = new(StringComparer.OrdinalIgnoreCase);

    public void SetSelectedTemplateLibraryItem(TemplateLibraryItem? selectedTemplateLibraryItem)
    {
        SelectedTemplateLibraryItem = selectedTemplateLibraryItem;
        SelectedTemplateFilePath = selectedTemplateLibraryItem?.FilePath;
    }

    public void ClearSelection(string actionStatusText)
    {
        SelectedTemplateLibraryItem = null;
        SelectedTemplateFilePath = null;
        ActiveTemplateDocument = null;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)
    {
        ActiveTemplateDocument = document;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    public void SetSelectionLoadFailed(string actionStatusText)
    {
        ActiveTemplateDocument = null;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    public void SetActionStatus(string actionStatusText)
    {
        ActionStatusText = actionStatusText;
    }

    public void SetReadinessSummary(string readinessSummaryText)
    {
        ReadinessSummaryText = readinessSummaryText;
    }

    public void SetWorkflowState(
        bool isEvaluatingReadiness,
        bool isStarting,
        string lifecycleState,
        int progressPercent,
        string progressSummary)
    {
        IsEvaluatingReadiness = isEvaluatingReadiness;
        IsStarting = isStarting;
        LifecycleState = lifecycleState;
        ProgressPercent = progressPercent;
        ProgressSummary = progressSummary;
    }

    public void SetShowAllVmRows(bool showAllVmRows)
    {
        ShowAllVmRows = showAllVmRows;
    }

    public void ClearResultRows()
    {
        ResultRows.Clear();
    }

    public void InitializeProgressRows(MultiVmDeploymentContext context)
    {
        _progressByVm.Clear();

        foreach (var vmContext in context.VmContexts)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            _progressByVm[vmName] = new DeployVmProgressState(vmName, BuildExpectedDeploySteps(vmContext));
        }

        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    public void InitializeProgressRows(V2PlanBuildResult plan)
    {
        _progressByVm.Clear();

        foreach (var vmGroup in plan.Nodes
                     .GroupBy(node => string.IsNullOrWhiteSpace(node.VmName) ? "Unnamed-VM" : node.VmName.Trim(), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var steps = vmGroup
                .Select(node => new DeployTimelineStepDefinition(MapV2StepKey(node.Kind), node.DisplayName))
                .GroupBy(step => step.StepKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            _progressByVm[vmGroup.Key] = new DeployVmProgressState(vmGroup.Key, steps);
        }

        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    public void UpdateProgressMessage(string vmName, string? message)
    {
        if (!_progressByVm.TryGetValue(vmName, out var state))
        {
            return;
        }

        state.UpdateSummaryMessage(message);
        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    public void ApplyProgressUpdate(string vmName, DeployStepStateUpdate update)
    {
        if (!_progressByVm.TryGetValue(vmName, out var state))
        {
            return;
        }

        state.ApplyStepStateUpdate(update);
        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    public void ClearGroupedIssueState()
    {
        IssueRows.Clear();
        SharedIssueSummaries.Clear();
        GlobalIssuesBadgeText = "Issues: 0";
        SharedIssuesSummaryText = "Shared review items appear here when multiple VMs need the same remediation.";
    }

    public void ReplaceIssueRows(IReadOnlyList<DeployIssueRow> issueRows)
    {
        IssueRows.Clear();
        foreach (var issueRow in issueRows)
        {
            IssueRows.Add(issueRow);
        }

        GlobalIssuesBadgeText = $"Issues: {IssueRows.Count}";
        RefreshSharedIssueSummaries();
    }

    public void RefreshResultRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        ResultRows.Clear();

        if (ShowAllVmRows && _progressByVm.Count > 0)
        {
            foreach (var state in _progressByVm.Values.OrderBy(value => value.VmName, StringComparer.OrdinalIgnoreCase))
            {
                ResultRows.Add(state.ToRow());
            }

            return;
        }

        if (ActiveTemplateDocument is null)
        {
            return;
        }

        var vmNames = ActiveTemplateDocument.Template.VmTemplates
            .Select(vm => string.IsNullOrWhiteSpace(vm.Name) ? "Unnamed-VM" : vm.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compatibilityByVm = compatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (readinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var vmName in vmNames)
        {
            compatibilityByVm.TryGetValue(vmName, out var vmCompatibilityIssues);
            readinessByVm.TryGetValue(vmName, out var vmReadinessResults);

            vmCompatibilityIssues ??= [];
            vmReadinessResults ??= [];

            var hasBlocking = vmCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Fail);
            var hasWarnings = vmCompatibilityIssues.Any(issue => !issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Warn);
            if (!hasBlocking && !hasWarnings)
            {
                continue;
            }

            var status = hasBlocking ? "Blocked" : hasWarnings ? "Warning" : "Ready";
            var blockingCount = vmCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = vmCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            var summary = $"Blocking: {blockingCount} | Warnings: {warningCount}";

            ResultRows.Add(new DeployVmResultRow(
                VmName: vmName,
                Status: status,
                Summary: summary,
                ProgressPercent: hasBlocking ? 100 : 80,
                TimelineSteps: CreateReadinessTimelineSteps(vmCompatibilityIssues, vmReadinessResults, hasBlocking)));
        }
    }

    public void ApplyOutcomeSummary(DeploymentOutcomeSummary summary)
    {
        ResultRows.Clear();

        foreach (var vmOutcome in summary.VmOutcomes)
        {
            if (_progressByVm.TryGetValue(vmOutcome.VmName, out var liveState))
            {
                liveState.MarkCompleted(vmOutcome.Status.ToString(), BuildCleanupSummary(vmOutcome));
                ResultRows.Add(liveState.ToRow());
            }
            else
            {
                ResultRows.Add(new DeployVmResultRow(
                    VmName: vmOutcome.VmName,
                    Status: vmOutcome.Status.ToString(),
                    Summary: BuildCleanupSummary(vmOutcome),
                    ProgressPercent: 100,
                    TimelineSteps: CreateOutcomeTimelineSteps(vmOutcome)));
            }
        }

        var issueRows = new List<DeployIssueRow>();
        foreach (var residual in summary.Residuals)
        {
            issueRows.Add(new DeployIssueRow(
                Scope: residual.VmName,
                Severity: "Warn",
                Message: $"{residual.ResourceType} '{residual.Identifier}' residual. Suggested action: {residual.SuggestedAction}"));
        }

        ReplaceIssueRows(issueRows);
    }

    public void RefreshReviewState(bool hasBlockingFailures)
    {
        if (ActiveTemplateDocument is null)
        {
            TemplateSummaryText = "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";
            TemplateRemediationText = "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";
            ReadinessSummaryText = "Select a template to evaluate readiness and run deploy.";
            ClearGroupedIssueState();
            return;
        }

        TemplateSummaryText =
            $"Template '{ActiveTemplateDocument.Template.Name}' will deploy {ActiveTemplateDocument.Template.VmTemplates.Count} VM(s). Review shared environment blockers here before deciding whether to remediate or open the template editor.";
        TemplateRemediationText = hasBlockingFailures
            ? "Blocking issues are grouped below when possible. Use Resolve Suggestions for safe shared remaps, or Open in Templates Editor for structural fixes."
            : "This surface is for template review and remediation. Use Open in Templates Editor only when the template itself needs structural changes.";
    }

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        if (string.IsNullOrWhiteSpace(SelectedTemplateFilePath))
        {
            SelectedTemplateLibraryItem = null;
            return;
        }

        SelectedTemplateLibraryItem = items
            .FirstOrDefault(item => string.Equals(item.FilePath, SelectedTemplateFilePath, StringComparison.OrdinalIgnoreCase));

        if (SelectedTemplateLibraryItem is null)
        {
            SelectedTemplateFilePath = null;
            ActiveTemplateDocument = null;
            RefreshReviewState(hasBlockingFailures: false);
        }
    }

    private void RefreshSharedIssueSummaries()
    {
        SharedIssueSummaries.Clear();

        var groupedIssues = IssueRows
            .Where(issue => !string.Equals(issue.Scope, "Global", StringComparison.OrdinalIgnoreCase))
            .GroupBy(issue => $"{issue.Severity}|{issue.Message}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(issue => issue.Scope).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .OrderByDescending(group => group.Key.StartsWith("Block|", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(group => group.Count())
            .ToList();

        foreach (var group in groupedIssues)
        {
            var scopes = group
                .Select(issue => issue.Scope)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var message = group.First().Message;
            var severity = group.First().Severity;
            SharedIssueSummaries.Add($"{severity}: {message} Shared across {scopes.Count} VM(s): {string.Join(", ", scopes)}");
        }

        SharedIssuesSummaryText = SharedIssueSummaries.Count > 0
            ? "Shared environment and compatibility issues detected across multiple VMs. Fix them here when safe, or open Templates Editor for structural changes."
            : "No shared review items are currently grouped. Review the readiness summary, then use the main actions below.";
    }

    private static IReadOnlyList<DeployTimelineStepDefinition> BuildExpectedDeploySteps(VmDeploymentContext context)
    {
        var steps = new List<DeployTimelineStepDefinition>
        {
            new(DeploymentStepKeys.CheckHyperV, "Check Hyper-V"),
            new(DeploymentStepKeys.CreateVmFolder, "Create VM folder"),
            new(DeploymentStepKeys.CreateVhd, "Create differencing disk"),
            new(DeploymentStepKeys.CreateVm, "Create VM"),
            new(DeploymentStepKeys.AddNicToVm, "Add network adapter"),
            new(DeploymentStepKeys.ConfigureVm, "Configure VM"),
            new(DeploymentStepKeys.EnableGuestServices, "Enable guest services"),
            new(DeploymentStepKeys.DisableVmCheckpoints, "Disable VM checkpoints"),
            new(DeploymentStepKeys.StartVm, "Start VM")
        };

        if (context.ConfigureTimeZone)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.SetTimeZone, "Set Time Zone"));
        }

        if (context.InstallSoftware)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.InstallSoftware, "Install Software"));
        }

        if (context.InstallRole)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.InstallRole, "Install Role"));
        }

        if (context.ConfigureNetworkInformation)
        {
            steps.Add(new DeployTimelineStepDefinition(DeploymentStepKeys.ConfigureNetworkInformation, "Configure Network Information"));
        }

        return steps;
    }

    private static string MapV2StepKey(V2PlanNodeKind kind)
    {
        return kind switch
        {
            V2PlanNodeKind.ProvisionVm => DeploymentStepKeys.V2ProvisionVm,
            V2PlanNodeKind.EnableGuestServices => DeploymentStepKeys.V2EnableGuestServices,
            V2PlanNodeKind.StartVm => DeploymentStepKeys.V2StartVm,
            V2PlanNodeKind.GuestTransportReady => DeploymentStepKeys.V2GuestTransportReady,
            V2PlanNodeKind.PrepareGuestNetwork => DeploymentStepKeys.V2PrepareGuestNetwork,
            V2PlanNodeKind.PrepareRouterNetwork => DeploymentStepKeys.V2PrepareRouterNetwork,
            V2PlanNodeKind.InstallRouterRemoteAccessFeature => DeploymentStepKeys.V2InstallRouterRemoteAccessFeature,
            V2PlanNodeKind.EnableRouterRouting => DeploymentStepKeys.V2EnableRouterRouting,
            V2PlanNodeKind.ConfigureRouterNat => DeploymentStepKeys.V2ConfigureRouterNat,
            V2PlanNodeKind.ValidateCrossSwitchRouting => DeploymentStepKeys.V2ValidateCrossSwitchRouting,
            V2PlanNodeKind.ValidateRouterEgress => DeploymentStepKeys.V2ValidateRouterEgress,
            V2PlanNodeKind.InstallAdDomainServicesFeature => DeploymentStepKeys.V2InstallAdDomainServices,
            V2PlanNodeKind.RouterReady => DeploymentStepKeys.V2RouterReady,
            V2PlanNodeKind.DomainReady => DeploymentStepKeys.V2DomainReady,
            V2PlanNodeKind.PromoteRootDomainController => DeploymentStepKeys.V2PromoteRootDomainController,
            V2PlanNodeKind.PromoteReplicaDomainController => DeploymentStepKeys.V2PromoteReplicaDomainController,
            V2PlanNodeKind.ReplicaDomainReady => DeploymentStepKeys.V2ReplicaDomainReady,
            V2PlanNodeKind.StabilizeDomainDns => DeploymentStepKeys.V2StabilizeDomainDns,
            V2PlanNodeKind.JoinDomain => DeploymentStepKeys.V2JoinDomain,
            V2PlanNodeKind.JoinedDomainReady => DeploymentStepKeys.V2JoinedDomainReady,
            _ => kind.ToString()
        };
    }

    private static IReadOnlyList<DeployTimelineStepRow> CreateReadinessTimelineSteps(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        IReadOnlyList<DeploymentReadinessCheckResult> readinessResults,
        bool hasBlocking)
    {
        var state = hasBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded;
        var rows = new List<DeployTimelineStepRow>
        {
            new("Readiness evaluation", state)
        };

        foreach (var issue in compatibilityIssues)
        {
            var issueState = issue.IsBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending;
            rows.Add(new DeployTimelineStepRow($"{issue.Message} {issue.Guidance}".Trim(), issueState));
        }

        foreach (var result in readinessResults.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
        {
            var issueState = result.Status == DeploymentReadinessStatus.Fail ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending;
            rows.Add(new DeployTimelineStepRow($"{result.Message} {result.ActionableGuidance}".Trim(), issueState));
        }

        return rows;
    }

    private static IReadOnlyList<DeployTimelineStepRow> CreateOutcomeTimelineSteps(VmDeploymentOutcomeSummary vmOutcome)
    {
        var outcomeState = vmOutcome.Status switch
        {
            VmDeploymentOutcomeStatus.Succeeded => DeployTimelineStepState.Succeeded,
            VmDeploymentOutcomeStatus.Failed => DeployTimelineStepState.Failed,
            VmDeploymentOutcomeStatus.Cancelled => DeployTimelineStepState.Skipped,
            _ => DeployTimelineStepState.Pending
        };

        var rows = new List<DeployTimelineStepRow>
        {
            new("Deploy VM", outcomeState)
        };

        if (vmOutcome.Cleanup.CleanupRan)
        {
            var cleanupState = vmOutcome.Cleanup.Status switch
            {
                VmCleanupOutcomeStatus.Succeeded => DeployTimelineStepState.Succeeded,
                VmCleanupOutcomeStatus.Residuals => DeployTimelineStepState.Failed,
                _ => DeployTimelineStepState.Skipped
            };

            if (cleanupState != DeployTimelineStepState.Skipped)
            {
                rows.Add(new("Cleanup", cleanupState));
            }
        }

        return rows;
    }

    private static string BuildCleanupSummary(VmDeploymentOutcomeSummary vmOutcome)
    {
        var cleanup = vmOutcome.Cleanup;
        if (!cleanup.CleanupRan)
        {
            return "Completed";
        }

        return cleanup.Status switch
        {
            VmCleanupOutcomeStatus.Succeeded => "Cleanup completed",
            VmCleanupOutcomeStatus.Residuals => $"Cleanup completed with residuals ({cleanup.ResidualCount}). Manual cleanup may be required.",
            _ => "Cleanup not needed"
        };
    }
}

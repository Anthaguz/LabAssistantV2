using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Observable state owner for the From Template V2 review pane. It is driven by
/// <see cref="DeployV2ReviewWorkspaceController"/> and bound directly through x:Bind as a child of
/// <see cref="DeployFromTemplateViewModel"/>. Mutations raise a blanket change notification so the
/// compiled bindings re-evaluate the projected review state (status, plan summary, and the blocker,
/// credential-slot, wave, and diagnostic collections).
/// </summary>
internal sealed class DeployV2ReviewWorkspaceViewModel : ObservableObject
{
    public bool IsVisible { get; private set; }

    public bool IsPlanning { get; private set; }

    public string StatusText { get; private set; } = "V2 planning has not run yet.";

    public DeployV2PlanSummaryRow? PlanSummary { get; private set; }

    /// <summary>
    /// Gets the human-readable plan-summary block rendered in the V2 review pane. Kept here (rather
    /// than in the view) so the projection stays runtime-independent and unit-testable.
    /// </summary>
    public string PlanSummaryText => PlanSummary is null
        ? "No V2 plan has been projected yet."
        : $"{PlanSummary.TemplateName} | {PlanSummary.ExecutionEngine} | Profile: {PlanSummary.DeploymentProfile}\n" +
          $"VMs: {PlanSummary.VmCount} | Nodes: {PlanSummary.NodeCount} | Unresolved: {PlanSummary.UnresolvedRequirementCount}\n" +
          $"{PlanSummary.RouterSummary} | {PlanSummary.DomainSummary} | {PlanSummary.StartabilitySummary}";

    public V2PlanBuildResult? CurrentPlan { get; private set; }

    public ObservableCollection<DeployV2BlockerRow> BlockerRows { get; } = [];

    public ObservableCollection<DeployV2CredentialSlotRow> CredentialSlotRows { get; } = [];

    public ObservableCollection<DeployV2WaveRow> WaveRows { get; } = [];

    public ObservableCollection<DeployV2DiagnosticRow> DiagnosticRows { get; } = [];

    public string SelectedCredentialSlotKey { get; private set; } = string.Empty;

    public string SelectedCredentialSlotPurpose { get; private set; } = "Select a slot below to create or update its local value.";

    public string SelectedCredentialSlotUsername { get; private set; } = string.Empty;

    public bool HasBlockingItems { get; private set; }

    public bool CanStartDeploy { get; private set; }

    public DeployV2BaseRemoteAccessRow BaseRemoteAccess { get; private set; } = CreateDefaultBaseRemoteAccess();

    public IReadOnlyDictionary<string, V2RuntimeCredential> ResolvedCredentialSlotValues { get; private set; } =
        new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> ExternalSwitchAdapterMappings { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public void Hide()
    {
        IsVisible = false;
        IsPlanning = false;
        StatusText = "V2 planning has not run yet.";
        PlanSummary = null;
        CurrentPlan = null;
        HasBlockingItems = false;
        CanStartDeploy = false;
        ResolvedCredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);
        ExternalSwitchAdapterMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SelectedCredentialSlotKey = string.Empty;
        SelectedCredentialSlotPurpose = "Select a slot below to create or update its local value.";
        SelectedCredentialSlotUsername = string.Empty;
        BaseRemoteAccess = CreateDefaultBaseRemoteAccess();
        ClearRows();
        OnPropertyChanged(string.Empty);
    }

    public void BeginPlanning()
    {
        IsVisible = true;
        IsPlanning = true;
        StatusText = "Building V2 deployment plan...";
        OnPropertyChanged(string.Empty);
    }

    public void ApplyProjection(
        DeployV2PlanSummaryRow summary,
        IReadOnlyList<DeployV2BlockerRow> blockers,
        IReadOnlyList<DeployV2CredentialSlotRow> credentialSlots,
        IReadOnlyList<DeployV2WaveRow> waves,
        IReadOnlyList<DeployV2DiagnosticRow> diagnostics,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> resolvedCredentialSlotValues)
    {
        IsVisible = true;
        IsPlanning = false;
        PlanSummary = summary;
        CurrentPlan = plan;
        ResolvedCredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(resolvedCredentialSlotValues, StringComparer.OrdinalIgnoreCase);
        ReplaceRows(BlockerRows, blockers);
        ReplaceRows(CredentialSlotRows, credentialSlots);
        ReplaceRows(WaveRows, waves);
        ReplaceRows(DiagnosticRows, diagnostics);
        HasBlockingItems = blockers.Any(row => string.Equals(row.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        CanStartDeploy = plan.Success && !HasBlockingItems;
        StatusText = CanStartDeploy
            ? "V2 plan is ready. You can start deployment."
            : HasBlockingItems
                ? "V2 deployment is blocked until review items are resolved."
                : "V2 plan is available for review.";

        if (CredentialSlotRows.Count == 0)
        {
            SelectedCredentialSlotKey = string.Empty;
            SelectedCredentialSlotPurpose = "No unresolved credential slots remain.";
            SelectedCredentialSlotUsername = string.Empty;
            OnPropertyChanged(string.Empty);
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCredentialSlotKey) ||
            CredentialSlotRows.All(row => !string.Equals(row.SlotKey, SelectedCredentialSlotKey, StringComparison.OrdinalIgnoreCase)))
        {
            SelectCredentialSlot(CredentialSlotRows[0].SlotKey);
        }
        else
        {
            SelectCredentialSlot(SelectedCredentialSlotKey);
        }

        OnPropertyChanged(string.Empty);
    }

    public void SetPlanningFailed(string message)
    {
        IsVisible = true;
        IsPlanning = false;
        PlanSummary = null;
        CurrentPlan = null;
        HasBlockingItems = true;
        CanStartDeploy = false;
        StatusText = message;
        ResolvedCredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);
        ExternalSwitchAdapterMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        SelectedCredentialSlotKey = string.Empty;
        SelectedCredentialSlotPurpose = "Planning failed. Fix the template or environment, then retry.";
        SelectedCredentialSlotUsername = string.Empty;
        ClearRows();
        BlockerRows.Add(new DeployV2BlockerRow("Block", "Global", message));
        OnPropertyChanged(string.Empty);
    }

    public void UpdateBaseRemoteAccessOptions(bool disableFirewall, bool disableRdpNla)
    {
        BaseRemoteAccess = BaseRemoteAccess with
        {
            DisableFirewall = disableFirewall,
            DisableRdpNla = disableRdpNla
        };
        OnPropertyChanged(string.Empty);
    }

    public void SetExternalSwitchAdapterMapping(string switchName, string adapterName)
    {
        var mappings = new Dictionary<string, string>(ExternalSwitchAdapterMappings, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(switchName) || string.IsNullOrWhiteSpace(adapterName))
        {
            if (!string.IsNullOrWhiteSpace(switchName))
            {
                mappings.Remove(switchName.Trim());
            }
        }
        else
        {
            mappings[switchName.Trim()] = adapterName.Trim();
        }

        ExternalSwitchAdapterMappings = mappings;
        OnPropertyChanged(string.Empty);
    }

    public V2BaseRemoteAccessOptions CreateBaseRemoteAccessOptions()
    {
        return new V2BaseRemoteAccessOptions
        {
            EnableRemoteDesktop = BaseRemoteAccess.EnableRemoteDesktop,
            SetPrivateNetworkProfile = BaseRemoteAccess.SetPrivateNetworkProfile,
            DisableFirewall = BaseRemoteAccess.DisableFirewall,
            DisableRdpNla = BaseRemoteAccess.DisableRdpNla
        };
    }

    public void SelectCredentialSlot(string? slotKey)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            SelectedCredentialSlotKey = string.Empty;
            SelectedCredentialSlotPurpose = "Select a slot below to create or update its local value.";
            SelectedCredentialSlotUsername = string.Empty;
            OnPropertyChanged(string.Empty);
            return;
        }

        var row = CredentialSlotRows.FirstOrDefault(candidate =>
            string.Equals(candidate.SlotKey, slotKey.Trim(), StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            SelectedCredentialSlotKey = string.Empty;
            SelectedCredentialSlotPurpose = "Select a slot below to create or update its local value.";
            SelectedCredentialSlotUsername = string.Empty;
            OnPropertyChanged(string.Empty);
            return;
        }

        SelectedCredentialSlotKey = row.SlotKey;
        SelectedCredentialSlotPurpose = $"{row.PurposeSummary} | {row.AffectedVmSummary}";
        SelectedCredentialSlotUsername = row.ExistingUsername;
        OnPropertyChanged(string.Empty);
    }

    private void ClearRows()
    {
        BlockerRows.Clear();
        CredentialSlotRows.Clear();
        WaveRows.Clear();
        DiagnosticRows.Clear();
    }

    private static void ReplaceRows<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static DeployV2BaseRemoteAccessRow CreateDefaultBaseRemoteAccess()
        => new(
            EnableRemoteDesktop: true,
            SetPrivateNetworkProfile: true,
            DisableFirewall: true,
            DisableRdpNla: true);
}

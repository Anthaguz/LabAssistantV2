using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.Models.Deploy;

/// <summary>
/// Shared projection from a V2 deployment plan to per-VM live-progress state, used by every deploy lane that
/// runs on the V2 graph engine (From Template and Quick Deploy). Centralizing the plan-node-to-step-key mapping
/// and the per-VM grouping here keeps the lanes' progress timelines identical and prevents the two surfaces
/// from drifting apart when the V2 plan node vocabulary changes.
/// </summary>
internal static class DeployV2ProgressPlan
{
    /// <summary>
    /// Builds one <see cref="DeployVmProgressState"/> per VM in the plan, ordered by VM name, with the VM's
    /// distinct plan nodes projected into an ordered step timeline. Nodes with no VM name are grouped under a
    /// stable placeholder so their steps still surface.
    /// </summary>
    public static IReadOnlyList<DeployVmProgressState> BuildProgressStates(V2PlanBuildResult plan)
    {
        var states = new List<DeployVmProgressState>();

        foreach (var vmGroup in plan.Nodes
                     .GroupBy(node => string.IsNullOrWhiteSpace(node.VmName) ? "Unnamed-VM" : node.VmName.Trim(), StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var steps = vmGroup
                .Select(node => new DeployTimelineStepDefinition(MapStepKey(node.Kind), node.DisplayName))
                .GroupBy(step => step.StepKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            states.Add(new DeployVmProgressState(vmGroup.Key, steps));
        }

        return states;
    }

    /// <summary>
    /// Maps a V2 plan node kind to the canonical deployment step key the runtime emits for that node, so the
    /// live per-attempt step-state updates align with the pre-seeded timeline rows.
    /// </summary>
    public static string MapStepKey(V2PlanNodeKind kind)
    {
        return kind switch
        {
            V2PlanNodeKind.EnsureNetworkSwitch => DeploymentStepKeys.V2EnsureNetworkSwitch,
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
}

using System.Text;
using LabAssistant.Models.Templates;

namespace LabAssistant.Deployment.Harness;

/// <summary>Renders a V2 plan (nodes, dependencies, waves, issues) to human-readable text for logs and console.</summary>
public static class PlanTextFormatter
{
    /// <summary>Formats the full plan into a multi-line string.</summary>
    public static string Format(V2PlanBuildResult plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- V2 PLAN (success={plan.Success}, profile={plan.Context.ResolvedDeploymentProfileName}) ---");

        sb.AppendLine($"Nodes ({plan.Nodes.Count}):");
        foreach (var n in plan.Nodes.OrderBy(n => n.WaveHint))
        {
            var extra = string.IsNullOrWhiteSpace(n.CapabilityRole) ? string.Empty : $" capRole={n.CapabilityRole}";
            sb.AppendLine($"  [wave {n.WaveHint}] {n.NodeId}  kind={n.Kind} vm='{n.VmName}' class={n.WorkloadClass}{extra}");
        }

        sb.AppendLine($"Dependencies ({plan.Dependencies.Count}):");
        foreach (var d in plan.Dependencies)
        {
            sb.AppendLine($"  {d.FromNodeId} -> {d.ToNodeId}  reason={d.ReasonCode} gate={d.IsBlockingGate}  {d.Description}");
        }

        sb.AppendLine($"Waves ({plan.Waves.Count}):");
        foreach (var w in plan.Waves)
        {
            sb.AppendLine($"  wave {w.WaveNumber} '{w.DisplayName}': {string.Join(", ", w.NodeIds)}");
        }

        if (plan.Issues.Count > 0)
        {
            sb.AppendLine($"Issues ({plan.Issues.Count}):");
            foreach (var i in plan.Issues)
            {
                sb.AppendLine($"  [{i.Severity}] {i.Code} vm='{i.VmName}': {i.Message}  -> {i.SuggestedAction}");
            }
        }

        if (plan.UnresolvedRequirements.Count > 0)
        {
            sb.AppendLine($"Unresolved requirements ({plan.UnresolvedRequirements.Count}):");
            foreach (var u in plan.UnresolvedRequirements)
            {
                sb.AppendLine($"  {u.Kind} key='{u.Key}': {u.Description}");
            }
        }

        return sb.ToString();
    }
}

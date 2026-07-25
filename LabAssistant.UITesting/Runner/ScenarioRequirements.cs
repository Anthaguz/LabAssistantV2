namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Declares what standing infrastructure a scenario needs so the harness can
/// bracket it correctly. The value drives whether the harness seeds Hyper-V
/// resources and runs the non-negotiable teardown + no-orphans gate around the
/// scenario, or launches a plain UI-only pass.
/// </summary>
public enum ScenarioRequirements
{
    /// <summary>
    /// Pure UI workflow: no Hyper-V resources are created, so no seeding or
    /// no-orphans gate is required. The harness only launches the app and runs.
    /// </summary>
    None = 0,

    /// <summary>
    /// The scenario creates real Hyper-V resources (VMs, disks, switches). The
    /// harness seeds a tagged base disk + switch before launch and, in a finally
    /// block, tears down everything the run created and proves no tagged resource
    /// (or untagged new VM) survives. This is the guard behind the no-orphans
    /// invariant.
    /// </summary>
    HyperV = 1,
}

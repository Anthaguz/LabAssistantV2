using LabAssistant.UITesting.Infrastructure;
using LabAssistant.UITesting.Infrastructure.Cleanup;
using LabAssistant.UITesting.Infrastructure.ResourceProvider;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// The tagged Hyper-V resources a <see cref="ScenarioRequirements.HyperV"/>
/// scenario runs against, plus the seams it uses to name new resources and read
/// ground truth. The harness owns the lifecycle: it seeds these before launch
/// and unconditionally tears them down afterwards, so a scenario must only
/// create resources through the tagger (never an arbitrary name) or they will
/// evade cleanup and break the no-orphans invariant.
/// </summary>
public sealed class HyperVScenarioResources
{
    public HyperVScenarioResources(ResourceTagger tagger, HyperVProbe probe, ProvisionedResources provisioned)
    {
        Tagger = tagger;
        Probe = probe;
        Provisioned = provisioned;
    }

    /// <summary>The naming authority for this run. Every VM/disk/switch the scenario creates must be named through it.</summary>
    public ResourceTagger Tagger { get; }

    /// <summary>Read-only Hyper-V ground-truth probe (VM/switch listings, per-VM state).</summary>
    public HyperVProbe Probe { get; }

    /// <summary>The seeded base disk + host switch the scenario selects in the UI.</summary>
    public ProvisionedResources Provisioned { get; }

    /// <summary>Convenience: builds a run-tagged, sweepable resource name.</summary>
    public string Name(string suffix) => Tagger.Name(suffix);
}

using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Runner;

/// <summary>
/// Everything a scenario needs while it runs: the live app, the shared finding
/// recorder, config, the repo root, and - for Hyper-V scenarios only - the
/// seeded, tagged resources the harness stood up. <see cref="HyperV"/> is null
/// for <see cref="ScenarioRequirements.None"/> scenarios; a Hyper-V scenario can
/// rely on it being non-null because the harness only invokes it after seeding.
/// </summary>
public sealed class ScenarioContext
{
    public ScenarioContext(
        AppHost host,
        FindingRecorder recorder,
        HarnessConfig config,
        string repoRoot,
        HyperVScenarioResources? hyperV)
    {
        Host = host;
        Recorder = recorder;
        Config = config;
        RepoRoot = repoRoot;
        HyperV = hyperV;
    }

    public AppHost Host { get; }
    public FindingRecorder Recorder { get; }
    public HarnessConfig Config { get; }
    public string RepoRoot { get; }

    /// <summary>The seeded Hyper-V resources for a Hyper-V scenario; null for UI-only scenarios.</summary>
    public HyperVScenarioResources? HyperV { get; }

    /// <summary>
    /// Returns <see cref="HyperV"/> or throws if a scenario declared the wrong
    /// requirements. Keeps Hyper-V scenario code free of null checks while making
    /// a misconfiguration fail loudly instead of NRE-ing deep in the workflow.
    /// </summary>
    public HyperVScenarioResources RequireHyperV()
        => HyperV ?? throw new InvalidOperationException(
            "This scenario used Hyper-V resources but did not declare Requirements = ScenarioRequirements.HyperV.");
}

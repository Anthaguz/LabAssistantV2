using LabAssistant.UITesting.Infrastructure;

namespace LabAssistant.UITesting.Runner;

/// <summary>Shared state passed to every scenario during a run.</summary>
public sealed class RunContext
{
    public RunContext(AppHost host, FindingRecorder recorder, HarnessConfig config, string repoRoot)
    {
        Host = host;
        Recorder = recorder;
        Config = config;
        RepoRoot = repoRoot;
    }

    public AppHost Host { get; }
    public FindingRecorder Recorder { get; }
    public HarnessConfig Config { get; }
    public string RepoRoot { get; }
}

/// <summary>
/// A self-contained, user-like workflow the harness can run. Implementations
/// own their own setup and teardown so a failure in one never leaks state into
/// the next.
/// </summary>
public interface IScenario
{
    string Name { get; }

    void Run(RunContext context);
}

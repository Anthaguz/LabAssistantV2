namespace LabAssistant.UITesting.Runner;

/// <summary>
/// A self-contained, user-like workflow the harness can run. A scenario only
/// drives the UI and records findings; it never owns process launch or resource
/// teardown. The harness reads <see cref="Requirements"/> to decide how to
/// bracket the run (plain UI pass vs. seeded Hyper-V pass with a mandatory
/// no-orphans gate), so a scenario must declare its needs honestly.
/// </summary>
public interface IScenario
{
    /// <summary>Stable, kebab-case identifier used in findings and the run log (e.g. "quick-deploy-single-vm").</summary>
    string Name { get; }

    /// <summary>The product capability this scenario exercises (e.g. "Deploy", "Machines", "Shell"). Used to group findings.</summary>
    string Capability { get; }

    /// <summary>What the harness must stand up (and tear down) around this scenario.</summary>
    ScenarioRequirements Requirements { get; }

    /// <summary>Drives the workflow. Throwing is caught by the harness and recorded as a failure finding.</summary>
    void Run(ScenarioContext context);
}

using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Runtime.Scheduling;

/// <summary>
/// Produces concrete <see cref="V2SchedulerOptions"/> for a deployment profile. The Conservative/Balanced/Aggressive
/// presets differ only in their concurrency numbers, replacing the old profile-specific code paths.
/// </summary>
/// <remarks>
/// The concurrency numbers here are conservative starting points and are expected to be calibrated against real hardware
/// (ADR open decision OD3). Correctness never depends on them: the dependency edges enforce ordering, and these caps
/// only bound how much runs at once.
/// </remarks>
public static class V2SchedulerOptionsFactory
{
    // Reserved for future scheduler-driven gate probes; today's gate executors retry internally.
    private static readonly TimeSpan DefaultGateRetryDelay = TimeSpan.FromSeconds(10);
    private const int DefaultGateMaxRetries = 30;

    /// <summary>Returns the options preset for <paramref name="profile"/>.</summary>
    public static V2SchedulerOptions ForProfile(V2DeploymentProfile profile) => profile switch
    {
        V2DeploymentProfile.Conservative => Create(
            globalMax: 4,
            heavyHost: 2,
            heavyGuest: 1,
            mediumGuest: 2,
            lightWaitValidation: 8),
        V2DeploymentProfile.Aggressive => Create(
            globalMax: 16,
            heavyHost: 8,
            heavyGuest: 4,
            mediumGuest: 8,
            lightWaitValidation: 32),
        _ => Create(
            globalMax: 8,
            heavyHost: 4,
            heavyGuest: 2,
            mediumGuest: 4,
            lightWaitValidation: 16)
    };

    private static V2SchedulerOptions Create(
        int globalMax,
        int heavyHost,
        int heavyGuest,
        int mediumGuest,
        int lightWaitValidation)
    {
        var byClass = new Dictionary<V2WorkloadClass, int>
        {
            [V2WorkloadClass.HeavyHost] = heavyHost,
            [V2WorkloadClass.HeavyGuest] = heavyGuest,
            [V2WorkloadClass.MediumGuest] = mediumGuest,
            [V2WorkloadClass.LightWaitValidation] = lightWaitValidation
        };

        return new V2SchedulerOptions(byClass, globalMax, DefaultGateRetryDelay, DefaultGateMaxRetries);
    }
}

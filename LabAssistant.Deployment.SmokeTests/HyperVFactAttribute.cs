using LabAssistant.Deployment.Harness;
using Xunit;

namespace LabAssistant.Deployment.SmokeTests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself unless every real-Hyper-V precondition is met (opt-in env
/// var, Windows, elevation, prepared base image present, admin password supplied). This keeps an ordinary
/// <c>dotnet test</c> - on CI or a dev box - green without ever provisioning a VM. Set
/// <c>LABASSISTANT_RUN_HYPERV_SMOKE=1</c> (and the base-image/password env vars) on a Hyper-V host to run it.
/// </summary>
public sealed class HyperVFactAttribute : FactAttribute
{
    public HyperVFactAttribute()
    {
        var skipReason = HarnessCapabilities.DescribeSkip(HarnessOptions.FromEnvironment());
        if (skipReason is not null)
        {
            Skip = $"Hyper-V smoke test skipped: {skipReason}.";
        }
    }
}

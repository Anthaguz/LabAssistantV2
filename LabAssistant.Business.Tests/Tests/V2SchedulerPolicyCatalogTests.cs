using System;
using System.Linq;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests;

public class V2SchedulerPolicyCatalogTests
{
    [Theory]
    [InlineData("Conservative", V2DeploymentProfile.Conservative)]
    [InlineData("balanced", V2DeploymentProfile.Balanced)]
    [InlineData(" Aggressive ", V2DeploymentProfile.Aggressive)]
    public void TryResolveProfile_RecognizesSupportedProfiles(string persistedValue, V2DeploymentProfile expectedProfile)
    {
        var resolved = V2SchedulerPolicyCatalog.TryResolveProfile(persistedValue, out var profile);

        Assert.True(resolved);
        Assert.Equal(expectedProfile, profile);
        Assert.Equal(Enum.GetName(expectedProfile), V2SchedulerPolicyCatalog.GetCanonicalProfileName(profile));
    }

    [Fact]
    public void SupportedProfiles_ExposeStableAdCoreInvariants()
    {
        foreach (var persistedName in V2SchedulerPolicyCatalog.SupportedProfileNames)
        {
            Assert.True(V2SchedulerPolicyCatalog.TryResolveProfile(persistedName, out var profile));

            var policy = V2SchedulerPolicyCatalog.GetPolicy(profile);

            Assert.True(policy.AdCore.DcProvisioningAndBootFirst);
            Assert.True(policy.AdCore.CriticalDcGuestWorkStartsBeforeNonCoreGuestWork);
            Assert.True(policy.AdCore.RequireDomainReadinessBeforeDomainDependentWork);
            Assert.True(policy.AdCore.RequireRouterReadinessForCrossSwitchDependencies);
            Assert.True(policy.LightWaitValidationBroadlyOverlapSafe);
        }
    }

    [Fact]
    public void SupportedWorkloadClasses_ExposeExpectedBaselineSet()
    {
        var workloadClasses = V2SchedulerPolicyCatalog.SupportedWorkloadClasses;

        Assert.Equal(
            [
                V2WorkloadClass.HeavyHost,
                V2WorkloadClass.HeavyGuest,
                V2WorkloadClass.MediumGuest,
                V2WorkloadClass.LightWaitValidation
            ],
            workloadClasses.OrderBy(value => value).ToArray());
    }
}

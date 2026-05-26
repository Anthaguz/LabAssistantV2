using System;
using System.Collections.Generic;

namespace LabAssistant.Models.Templates;

/// <summary>
/// Centralized V2 scheduler-policy contract used by validation and later planning work.
/// </summary>
public static class V2SchedulerPolicyCatalog
{
    private static readonly IReadOnlyDictionary<string, V2DeploymentProfile> ProfileNames =
        new Dictionary<string, V2DeploymentProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(V2DeploymentProfile.Conservative)] = V2DeploymentProfile.Conservative,
            [nameof(V2DeploymentProfile.Balanced)] = V2DeploymentProfile.Balanced,
            [nameof(V2DeploymentProfile.Aggressive)] = V2DeploymentProfile.Aggressive
        };

    private static readonly IReadOnlyDictionary<V2DeploymentProfile, V2DeploymentProfilePolicy> Policies =
        new Dictionary<V2DeploymentProfile, V2DeploymentProfilePolicy>
        {
            [V2DeploymentProfile.Conservative] = new(
                V2DeploymentProfile.Conservative,
                nameof(V2DeploymentProfile.Conservative),
                V2OverlapPolicyLevel.Minimal,
                V2OverlapPolicyLevel.Minimal,
                AllowsMediumGuestBackfillDuringDcFirstProgression: false,
                LightWaitValidationBroadlyOverlapSafe: true,
                new V2AdCoreSchedulingPolicy(
                    DcProvisioningAndBootFirst: true,
                    CriticalDcGuestWorkStartsBeforeNonCoreGuestWork: true,
                    AllowLaterVmProvisioningBackfill: false,
                    RequireDomainReadinessBeforeDomainDependentWork: true,
                    RequireRouterReadinessForCrossSwitchDependencies: true)),
            [V2DeploymentProfile.Balanced] = new(
                V2DeploymentProfile.Balanced,
                nameof(V2DeploymentProfile.Balanced),
                V2OverlapPolicyLevel.Moderate,
                V2OverlapPolicyLevel.Minimal,
                AllowsMediumGuestBackfillDuringDcFirstProgression: true,
                LightWaitValidationBroadlyOverlapSafe: true,
                new V2AdCoreSchedulingPolicy(
                    DcProvisioningAndBootFirst: true,
                    CriticalDcGuestWorkStartsBeforeNonCoreGuestWork: true,
                    AllowLaterVmProvisioningBackfill: true,
                    RequireDomainReadinessBeforeDomainDependentWork: true,
                    RequireRouterReadinessForCrossSwitchDependencies: true)),
            [V2DeploymentProfile.Aggressive] = new(
                V2DeploymentProfile.Aggressive,
                nameof(V2DeploymentProfile.Aggressive),
                V2OverlapPolicyLevel.High,
                V2OverlapPolicyLevel.Moderate,
                AllowsMediumGuestBackfillDuringDcFirstProgression: true,
                LightWaitValidationBroadlyOverlapSafe: true,
                new V2AdCoreSchedulingPolicy(
                    DcProvisioningAndBootFirst: true,
                    CriticalDcGuestWorkStartsBeforeNonCoreGuestWork: true,
                    AllowLaterVmProvisioningBackfill: true,
                    RequireDomainReadinessBeforeDomainDependentWork: true,
                    RequireRouterReadinessForCrossSwitchDependencies: true))
        };

    public static IReadOnlyCollection<string> SupportedProfileNames => ProfileNames.Keys.ToArray();

    public static IReadOnlyCollection<V2WorkloadClass> SupportedWorkloadClasses =>
        [
            V2WorkloadClass.HeavyHost,
            V2WorkloadClass.HeavyGuest,
            V2WorkloadClass.MediumGuest,
            V2WorkloadClass.LightWaitValidation
        ];

    public static bool TryResolveProfile(string? persistedValue, out V2DeploymentProfile profile)
    {
        profile = default;
        if (string.IsNullOrWhiteSpace(persistedValue))
        {
            return false;
        }

        return ProfileNames.TryGetValue(persistedValue.Trim(), out profile);
    }

    public static bool IsSupportedProfile(string? persistedValue)
    {
        return TryResolveProfile(persistedValue, out _);
    }

    public static string GetCanonicalProfileName(V2DeploymentProfile profile)
    {
        return GetPolicy(profile).CanonicalName;
    }

    public static V2DeploymentProfilePolicy GetPolicy(V2DeploymentProfile profile)
    {
        return Policies[profile];
    }
}

/// <summary>
/// Intent-level policy metadata for a V2 deployment profile.
/// </summary>
public sealed record V2DeploymentProfilePolicy(
    V2DeploymentProfile Profile,
    string CanonicalName,
    V2OverlapPolicyLevel HeavyHostOverlap,
    V2OverlapPolicyLevel HeavyGuestOverlap,
    bool AllowsMediumGuestBackfillDuringDcFirstProgression,
    bool LightWaitValidationBroadlyOverlapSafe,
    V2AdCoreSchedulingPolicy AdCore);

/// <summary>
/// AD-core invariants that every deployment profile must preserve.
/// </summary>
public sealed record V2AdCoreSchedulingPolicy(
    bool DcProvisioningAndBootFirst,
    bool CriticalDcGuestWorkStartsBeforeNonCoreGuestWork,
    bool AllowLaterVmProvisioningBackfill,
    bool RequireDomainReadinessBeforeDomainDependentWork,
    bool RequireRouterReadinessForCrossSwitchDependencies);

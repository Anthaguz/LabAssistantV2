namespace LabAssistant.Models.Templates;

/// <summary>
/// Coarse workload classes that guide V2 scheduling policy decisions.
/// </summary>
public enum V2WorkloadClass
{
    HeavyHost = 0,
    HeavyGuest = 1,
    MediumGuest = 2,
    LightWaitValidation = 3
}

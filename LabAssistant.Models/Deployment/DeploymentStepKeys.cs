namespace LabAssistant.Models.Deployment;

public static class DeploymentStepKeys
{
    public const string CheckHyperV = "CheckHyperV";
    public const string CreateVmFolder = "CreateVmFolder";
    public const string CreateVhd = "CreateVhd";
    public const string CreateVm = "CreateVm";
    public const string AddNicToVm = "AddNicToVm";
    public const string ConfigureVm = "ConfigureVm";
    public const string EnableGuestServices = "EnableGuestServices";
    public const string DisableVmCheckpoints = "DisableVmCheckpoints";
    public const string StartVm = "StartVm";

    public const string SetTimeZone = "SetTimeZone";
    public const string InstallSoftware = "InstallSoftware";
    public const string InstallRole = "InstallRole";
    public const string ConfigureNetworkInformation = "ConfigureNetworkInformation";

    public const string V2ProvisionVm = "v2.provision";
    public const string V2StartVm = "v2.start";
    public const string V2GuestTransportReady = "v2.guestTransportReady";
    public const string V2BootstrapGuestNetwork = "v2.bootstrapGuestNetwork";
    public const string V2InstallAdDomainServices = "v2.installAdDomainServices";
    public const string V2PromoteRootDomainController = "v2.promoteRootDomainController";
    public const string V2DomainReady = "v2.domainReady";
}

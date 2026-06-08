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
    public const string V2EnableGuestServices = "v2.enableGuestServices";
    public const string V2StartVm = "v2.start";
    public const string V2GuestTransportReady = "v2.guestTransportReady";
    public const string V2PrepareGuestNetwork = "v2.prepareGuestNetwork";
    public const string V2PrepareRouterNetwork = "v2.prepareRouterNetwork";
    public const string V2InstallRouterRemoteAccessFeature = "v2.installRouterRemoteAccessFeature";
    public const string V2EnableRouterRouting = "v2.enableRouterRouting";
    public const string V2ConfigureRouterNat = "v2.configureRouterNat";
    public const string V2ValidateCrossSwitchRouting = "v2.validateCrossSwitchRouting";
    public const string V2ValidateRouterEgress = "v2.validateRouterEgress";
    public const string V2InstallAdDomainServices = "v2.installAdDomainServices";
    public const string V2PromoteRootDomainController = "v2.promoteRootDomainController";
    public const string V2DomainReady = "v2.domainReady";
    public const string V2RouterReady = "v2.routerReady";
    public const string V2PromoteReplicaDomainController = "v2.promoteReplicaDomainController";
    public const string V2ReplicaDomainReady = "v2.replicaDomainReady";
    public const string V2StabilizeDomainDns = "v2.stabilizeDomainDns";
    public const string V2JoinDomain = "v2.joinDomain";
    public const string V2JoinedDomainReady = "v2.joinedDomainReady";
}

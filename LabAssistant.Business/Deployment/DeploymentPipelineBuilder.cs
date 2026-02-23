using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;
using System;
using System.Xml.Linq;

public class DeploymentPipelineBuilder : IDeploymentPipelineBuilder
{
    private readonly CreateVmFolderStep _createVmFolderStep;
    private readonly CreateVhdStep _createVhd;
    private readonly CreateVmStep _createVmStep;
    private readonly AddNicToVmStep _addNicToVmStep;
    private readonly ConfigureVmStep _configureVmStep;
    private readonly StartVmStep _startVmStep;
    private readonly EnableGuestServicesStep _enableGuestServicesStep;
    private readonly DisableVmCheckpoints _disableVmCheckpoints;
    private readonly SetTimeZoneStep _setTimeZoneStep;
    private readonly InstallSoftwareStep _installSoftwareStep;
    private readonly InstallRoleStep _installRoleStep;
    private readonly ConfigureNetworkInformationStep _configureNetworkInformationStep;

    public DeploymentPipelineBuilder(
        CreateVmFolderStep createVmFolderStep,
        CreateVhdStep createVhd,
        CreateVmStep createVmStep,
        AddNicToVmStep addNicToVmStep,
        ConfigureVmStep configureVmStep,
        StartVmStep startVmStep,
        EnableGuestServicesStep enableGuestServicesStep,
        DisableVmCheckpoints disableVmCheckpoints,
        SetTimeZoneStep setTimeZoneStep,
        InstallSoftwareStep installSoftwareStep,
        InstallRoleStep installRoleStep,
        ConfigureNetworkInformationStep configureNetworkInformationStep)
    {
        _createVhd = createVhd;
        _createVmFolderStep = createVmFolderStep;
        _addNicToVmStep = addNicToVmStep;
        _createVmStep = createVmStep;
        _configureVmStep = configureVmStep;
        _startVmStep = startVmStep;
        _enableGuestServicesStep = enableGuestServicesStep;
        _disableVmCheckpoints = disableVmCheckpoints;
        _setTimeZoneStep = setTimeZoneStep;
        _installSoftwareStep = installSoftwareStep;
        _installRoleStep = installRoleStep;
        _configureNetworkInformationStep = configureNetworkInformationStep;

    }

    public DeploymentStep Build(VmDeploymentContext context)
    {
        DebugLogger.Log($"Creating VM: {context.VmName} with VHD: {context.VhdPath}, Memory: {context.MemoryMb}MB, CPUs: {context.CpuCount}");
        var check = new CheckHyperVStep();

        var current = check
            .SetNext(_createVmFolderStep)
            .SetNext(_createVhd)
            .SetNext(_createVmStep)
            .SetNext(_addNicToVmStep)
            .SetNext(_configureVmStep)
            .SetNext(_enableGuestServicesStep)
            .SetNext(_disableVmCheckpoints)
            .SetNext(_startVmStep);

        if (context.ConfigureTimeZone)
            current = current.SetNext(_setTimeZoneStep);

        if (context.InstallSoftware)
            current = current.SetNext(_installSoftwareStep);

        if (context.InstallRole)
            current = current.SetNext(_installRoleStep);

        if (context.ConfigureNetworkInformation)
            current = current.SetNext(_configureNetworkInformationStep);

        return check;
    }
}

using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests;

public class GuestStepConfigurationCompletenessPreflightCheckTests
{
    private readonly GuestStepConfigurationCompletenessPreflightCheck _check = new();

    [Fact]
    public async Task ExecuteAsync_EnabledTimeZoneWithoutConfig_ReturnsBlockingFailure()
    {
        var context = CreateContext(vm =>
        {
            vm.ConfigureTimeZone = true;
            vm.TimeZoneConfig = new TimeZoneStepConfig { Enabled = true };
        });

        var result = Assert.Single((await _check.ExecuteAsync(context, DeploymentPreflightMode.Full))
            .Where(r => r.Code == "GST.TIMEZONE.MISSING_CONFIG"));

        Assert.Equal(DeploymentReadinessStatus.Fail, result.Status);
        Assert.Equal(DeploymentReadinessCategory.TemplateConfig, result.Category);
        Assert.Contains("vm1", result.AffectedVmNames);
        Assert.Equal("time zone", result.ResourceName);
    }

    [Fact]
    public async Task ExecuteAsync_EnabledStepsWithValidConfig_ReturnsPassResults()
    {
        var context = CreateContext(vm =>
        {
            vm.ConfigureTimeZone = true;
            vm.InstallSoftware = true;
            vm.InstallRole = true;
            vm.TimeZoneConfig = new TimeZoneStepConfig { Enabled = true, TimeZoneId = "Pacific Standard Time" };
            vm.SoftwareConfig = new SoftwareStepConfig { Enabled = true, Packages = ["git"] };
            vm.RoleConfig = new RoleStepConfig { Enabled = true, Roles = ["WebServer"] };
        });

        var results = await _check.ExecuteAsync(context, DeploymentPreflightMode.Full);

        Assert.Contains(results, r => r.Code == "GST.TIMEZONE.CONFIG_OK" && r.Status == DeploymentReadinessStatus.Pass);
        Assert.Contains(results, r => r.Code == "GST.SOFTWARE.CONFIG_OK" && r.Status == DeploymentReadinessStatus.Pass);
        Assert.Contains(results, r => r.Code == "GST.ROLE.CONFIG_OK" && r.Status == DeploymentReadinessStatus.Pass);
        Assert.DoesNotContain(results, r => r.Status == DeploymentReadinessStatus.Fail);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledOptionalStepsWithMissingConfig_DoNotBlock()
    {
        var context = CreateContext(vm =>
        {
            vm.ConfigureTimeZone = false;
            vm.InstallSoftware = false;
            vm.InstallRole = false;
            vm.TimeZoneConfig = null;
            vm.SoftwareConfig = new SoftwareStepConfig { Enabled = false };
            vm.RoleConfig = new RoleStepConfig { Enabled = false };
        });

        var results = await _check.ExecuteAsync(context, DeploymentPreflightMode.Full);

        Assert.DoesNotContain(results, r => r.Status == DeploymentReadinessStatus.Fail);
        Assert.DoesNotContain(results, r => r.Code.StartsWith("GST.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_PlaceholderGuestNetworkVisibility_DoesNotCreateBlockingFailure()
    {
        var context = CreateContext(vm =>
        {
            vm.ConfigureNetworkInformation = true;
            vm.GuestNetworkConfig = new GuestNetworkStepConfig { Enabled = false };
        });

        var results = await _check.ExecuteAsync(context, DeploymentPreflightMode.Full);

        Assert.DoesNotContain(results, r => r.Status == DeploymentReadinessStatus.Fail && r.Code.StartsWith("GST.NETWORK", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_QuickAndFullModes_ProduceSameCheapCompletenessChecks()
    {
        var context = CreateContext(vm =>
        {
            vm.InstallSoftware = true;
            vm.SoftwareConfig = new SoftwareStepConfig { Enabled = true };
        });

        var quick = await _check.ExecuteAsync(context, DeploymentPreflightMode.Quick);
        var full = await _check.ExecuteAsync(context, DeploymentPreflightMode.Full);

        Assert.Contains(quick, r => r.Code == "GST.SOFTWARE.EMPTY_PACKAGE_LIST" && r.Status == DeploymentReadinessStatus.Fail);
        Assert.Contains(full, r => r.Code == "GST.SOFTWARE.EMPTY_PACKAGE_LIST" && r.Status == DeploymentReadinessStatus.Fail);
    }

    private static MultiVmDeploymentContext CreateContext(Action<VmDeploymentContext> configure)
    {
        var vm = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm1",
            VmPath = @"C:\Labs\vm1",
            VhdPath = @"C:\Labs\vm1\vm1.vhdx"
        };

        configure(vm);

        return new MultiVmDeploymentContext
        {
            VmContexts = { vm }
        };
    }
}

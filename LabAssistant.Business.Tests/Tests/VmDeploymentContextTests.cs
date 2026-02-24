using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class VmDeploymentContextTests
{
    [Fact]
    public void ResetForNewOperation_ClearsRuntimeState_AndPreservesConfiguration()
    {
        var context = new VmDeploymentContext
        {
            VmName = "VM1",
            VmPath = @"C:\Labs\VM1",
            VhdPath = @"C:\Labs\VM1\VM1.vhdx",
            BaseVhdPath = @"C:\Base\disk.vhdx",
            VirtualSwitchName = "Default Switch",
            MemoryMb = 4096,
            CpuCount = 4,
            IsSuccess = false,
            GuestServicesEnabled = true,
            VmFolderCreated = true,
            DifferencingDiskCreated = true,
            VmRegistered = true,
            VmStarted = true
        };

        context.MarkFailure("CreateVm", "failure");
        context.MarkCancelled();
        context.CleanupResult = new VmCleanupResult { VmName = "VM1" };
        context.Logs.Add("runtime log");

        context.ResetForNewOperation();

        Assert.True(context.IsSuccess);
        Assert.False(context.GuestServicesEnabled);
        Assert.False(context.VmFolderCreated);
        Assert.False(context.DifferencingDiskCreated);
        Assert.False(context.VmRegistered);
        Assert.False(context.VmStarted);
        Assert.False(context.WasCancelled);
        Assert.Null(context.FailureStepKey);
        Assert.Null(context.FailureMessage);
        Assert.Null(context.CleanupResult);
        Assert.Empty(context.Logs);

        Assert.Equal("VM1", context.VmName);
        Assert.Equal(@"C:\Labs\VM1", context.VmPath);
        Assert.Equal(@"C:\Labs\VM1\VM1.vhdx", context.VhdPath);
        Assert.Equal(@"C:\Base\disk.vhdx", context.BaseVhdPath);
        Assert.Equal("Default Switch", context.VirtualSwitchName);
        Assert.Equal(4096, context.MemoryMb);
        Assert.Equal(4, context.CpuCount);
    }
}

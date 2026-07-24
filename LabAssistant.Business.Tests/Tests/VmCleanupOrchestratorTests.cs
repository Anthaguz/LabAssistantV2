using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.FileSystem;
using LabAssistant.Services.HyperV;
using Xunit;

namespace LabAssistant.Business.Tests;

public class VmCleanupOrchestratorTests
{
    [Fact]
    public async Task Cleanup_ContinuesAfterFailedStep()
    {
        var fs = new FakeDeploymentFileSystem
        {
            Directories = { @"C:\vm\vm1" },
            Files = { @"C:\vm\vm1\vm1.vhdx" }
        };
        var hyperv = new FakeHyperVService
        {
            VmExists = true,
            VmRunning = true,
            StopVmSuccess = false,
            RemoveVmSuccess = true
        };

        var orchestrator = new VmCleanupOrchestrator(fs);
        var result = await orchestrator.CleanupAsync(CreateContext(), hyperv);

        Assert.Equal(CleanupStepStatus.Failed, result.StepResults[0].Status);
        Assert.Contains(result.StepResults, s => s.Step == CleanupStepName.RemoveVmRegistration && s.Status == CleanupStepStatus.Succeeded);
        Assert.Contains(result.StepResults, s => s.Step == CleanupStepName.RemoveVmDirectory && s.Status == CleanupStepStatus.Succeeded);
        Assert.Contains(result.StepResults, s => s.Step == CleanupStepName.RemoveDifferencingDisk && s.Status == CleanupStepStatus.Skipped);
    }

    [Fact]
    public async Task Cleanup_ReportsResidual_WhenDeleteFails()
    {
        var fs = new FakeDeploymentFileSystem
        {
            Directories = { @"C:\vm\vm1" },
            Files = { @"C:\vm\vm1\vm1.vhdx" },
            DeleteDirectorySuccess = false,
            DeleteFileSuccess = false
        };
        var hyperv = new FakeHyperVService
        {
            VmExists = false
        };

        var orchestrator = new VmCleanupOrchestrator(fs);
        var result = await orchestrator.CleanupAsync(CreateContext(), hyperv);

        Assert.True(result.HasResiduals);
        Assert.Contains(result.Residuals, r => r.ResourceType == "vm-directory");
        Assert.Contains(result.Residuals, r => r.ResourceType == "differencing-disk");
    }

    [Fact]
    public async Task Cleanup_RecordsSkipped_WhenResourcesMissing()
    {
        var fs = new FakeDeploymentFileSystem();
        var hyperv = new FakeHyperVService { VmExists = false, VmRunning = false };
        var orchestrator = new VmCleanupOrchestrator(fs);

        var result = await orchestrator.CleanupAsync(CreateContext(), hyperv);

        Assert.All(result.StepResults, step => Assert.Equal(CleanupStepStatus.Skipped, step.Status));
    }

    [Fact]
    public async Task Cleanup_SkipsVmRegistration_WhenEagerFlagSetButVmNeverCreated()
    {
        // Provisioning sets VmRegistered eagerly before CreateVmAsync. When creation fails the VM does not
        // exist, yet Remove-VM against a missing VM reports failure. Cleanup must recognise the VM is gone
        // and skip cleanly instead of surfacing a spurious "remove manually" residual.
        var fs = new FakeDeploymentFileSystem();
        var hyperv = new FakeHyperVService
        {
            VmExists = false,
            VmRunning = false,
            RemoveVmSuccess = false
        };
        var context = CreateContext();
        context.VmRegistered = true;

        var orchestrator = new VmCleanupOrchestrator(fs);
        var result = await orchestrator.CleanupAsync(context, hyperv);

        var registrationStep = Assert.Single(result.StepResults, s => s.Step == CleanupStepName.RemoveVmRegistration);
        Assert.Equal(CleanupStepStatus.Skipped, registrationStep.Status);
        Assert.DoesNotContain(result.Residuals, r => r.ResourceType == "vm-registration");
        Assert.False(result.HasResiduals);
    }

    [Fact]
    public async Task Cleanup_PreservesDeterministicOrder()
    {
        var fs = new FakeDeploymentFileSystem
        {
            Directories = { @"C:\vm\vm1" },
            Files = { @"C:\vm\vm1\vm1.vhdx" }
        };
        var hyperv = new FakeHyperVService
        {
            VmExists = true,
            VmRunning = false,
            RemoveVmSuccess = true
        };
        var orchestrator = new VmCleanupOrchestrator(fs);

        var result = await orchestrator.CleanupAsync(CreateContext(), hyperv);

        Assert.Equal(
            new[]
            {
                CleanupStepName.StopVm,
                CleanupStepName.RemoveVmRegistration,
                CleanupStepName.RemoveVmDirectory,
                CleanupStepName.RemoveDifferencingDisk
            },
            result.StepResults.Select(s => s.Step).ToArray());
    }

    private static VmDeploymentContext CreateContext()
    {
        return new VmDeploymentContext
        {
            VmName = "vm1",
            VmPath = @"C:\vm\vm1",
            VhdPath = @"C:\vm\vm1\vm1.vhdx"
        };
    }

    private sealed class FakeDeploymentFileSystem : IDeploymentFileSystem
    {
        public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool DeleteDirectorySuccess { get; set; } = true;
        public bool DeleteFileSuccess { get; set; } = true;

        public bool DirectoryExists(string path) => Directories.Contains(path);

        public bool FileExists(string path) => Files.Contains(path);

        public bool DeleteDirectory(string path, out string? error)
        {
            if (!DeleteDirectorySuccess)
            {
                error = "Directory delete failed";
                return false;
            }

            Directories.Remove(path);
            // Simulate recursive delete removing contained file.
            Files.RemoveWhere(file => file.StartsWith(path, StringComparison.OrdinalIgnoreCase));
            error = null;
            return true;
        }

        public bool DeleteFile(string path, out string? error)
        {
            if (!DeleteFileSuccess)
            {
                error = "File delete failed";
                return false;
            }

            Files.Remove(path);
            error = null;
            return true;
        }
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        public bool VmExists { get; set; }
        public bool VmRunning { get; set; }
        public bool StopVmSuccess { get; set; } = true;
        public bool RemoveVmSuccess { get; set; } = true;

        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(StopVmSuccess);
        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(VmExists);
        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(VmRunning);
        public Task<bool> RemoveVmAsync(string vmName) => Task.FromResult(RemoveVmSuccess);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(true);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string>());
        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);
    }
}

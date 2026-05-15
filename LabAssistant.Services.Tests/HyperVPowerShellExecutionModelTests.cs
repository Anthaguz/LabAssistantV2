using LabAssistant.Models.Configuration;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

public class HyperVPowerShellExecutionModelTests
{
    [Fact]
    public async Task QueryExecutor_ReusesSinglePersistentSessionAcrossCalls()
    {
        var session = new FakeSession();
        session.Results.Enqueue((@"[{""VmName"":""vm1""}]", string.Empty));
        session.Results.Enqueue((@"[{""VmName"":""vm2""}]", string.Empty));

        var factoryCallCount = 0;
        using var executor = new HyperVQueryExecutor(() =>
        {
            factoryCallCount++;
            return session;
        });

        await executor.ExecuteAsync("machines_list_host_vms", "Get-VM");
        await executor.ExecuteAsync("machines_list_virtual_switches", "Get-VMSwitch");

        Assert.Equal(1, factoryCallCount);
        Assert.Equal(["Get-VM", "Get-VMSwitch"], session.Commands);
        Assert.False(session.Disposed);
    }

    [Fact]
    public async Task AdministrativeCommandExecutor_CreatesFreshSessionPerCommand()
    {
        var sessions = new List<FakeSession>();
        var executor = new HyperVAdministrativeCommandExecutor(() =>
        {
            var session = new FakeSession();
            session.Results.Enqueue((string.Empty, string.Empty));
            sessions.Add(session);
            return session;
        });

        await executor.ExecuteAsync("machines_start_vm", "Start-VM -Name 'vm1'");
        await executor.ExecuteAsync("machines_stop_vm", "Stop-VM -Name 'vm1' -Force");

        Assert.Equal(2, sessions.Count);
        Assert.Equal(["Start-VM -Name 'vm1'"], sessions[0].Commands);
        Assert.Equal(["Stop-VM -Name 'vm1' -Force"], sessions[1].Commands);
        Assert.All(sessions, session => Assert.True(session.Disposed));
    }

    [Fact]
    public async Task HyperVMachineAdminService_ListHostVmsAsync_UsesQueryExecutor()
    {
        var queryExecutor = new RecordingQueryExecutor
        {
            Result = new HyperVPowerShellExecutionResult
            {
                Output = """{"VmId":"vm-1","VmName":"vm1","State":"Running","VmPath":"C:\\VMs\\vm1","DiskPaths":["C:\\VMs\\vm1\\disk.vhdx"]}"""
            }
        };
        var adminExecutor = new RecordingAdministrativeCommandExecutor();
        var service = new HyperVMachineAdminService(queryExecutor, adminExecutor, new FakeAppSettingsStore());

        var result = await service.ListHostVmsAsync();

        var vm = Assert.Single(result);
        Assert.Equal("machines_list_host_vms", Assert.Single(queryExecutor.QueryNames));
        Assert.Empty(adminExecutor.CommandNames);
        Assert.Equal("vm1", vm.VmName);
        Assert.Equal("Running", vm.State);
    }

    [Fact]
    public async Task HyperVMachineAdminService_StartVmAsync_UsesAdministrativeCommandExecutor()
    {
        var queryExecutor = new RecordingQueryExecutor();
        var adminExecutor = new RecordingAdministrativeCommandExecutor
        {
            Result = new HyperVPowerShellExecutionResult
            {
                Error = "System.Management.Automation.RuntimeException: boom 0x80070005\r\nFullyQualifiedErrorId : OperationFailed,Microsoft.HyperV.PowerShell"
            }
        };
        var service = new HyperVMachineAdminService(queryExecutor, adminExecutor, new FakeAppSettingsStore());

        var result = await service.StartVmAsync("vm1");

        Assert.False(result.Success);
        Assert.Equal("machines_start_vm", Assert.Single(adminExecutor.CommandNames));
        Assert.Empty(queryExecutor.QueryNames);
        Assert.NotNull(result.FailureMetadata);
        Assert.Equal("RuntimeException", result.FailureMetadata!["exceptionType"]);
        Assert.Equal("0x80070005", result.FailureMetadata!["hresult"]);
        Assert.Equal("OperationFailed", result.FailureMetadata!["errorCode"]);
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public Queue<(string Output, string Error)> Results { get; } = new();

        public List<string> Commands { get; } = [];

        public bool Disposed { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            Commands.Add(command);
            if (Results.Count == 0)
            {
                return Task.FromResult((string.Empty, string.Empty));
            }

            return Task.FromResult(Results.Dequeue());
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class RecordingQueryExecutor : IHyperVQueryExecutor
    {
        public List<string> QueryNames { get; } = [];

        public HyperVPowerShellExecutionResult Result { get; set; } = new();

        public Task<HyperVPowerShellExecutionResult> ExecuteAsync(
            string queryName,
            string script,
            CancellationToken cancellationToken = default)
        {
            QueryNames.Add(queryName);
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingAdministrativeCommandExecutor : IHyperVAdministrativeCommandExecutor
    {
        public List<string> CommandNames { get; } = [];

        public HyperVPowerShellExecutionResult Result { get; set; } = new();

        public Task<HyperVPowerShellExecutionResult> ExecuteAsync(
            string commandName,
            string script,
            CancellationToken cancellationToken = default)
        {
            CommandNames.Add(commandName);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; } = new();

        public string SettingsPath => string.Empty;

        public void LoadOrCreate() { }

        public void Reload() { }

        public void Save() { }

        public void ResetToDefault() { }

        public void SetTemplateFolder(string path) => Settings.TemplateFolder = path;

        public void SetLogFolder(string path) => Settings.LogFolder = path;

        public void SetVmBasePath(string path) => Settings.VmBasePath = path;

        public void SetDifferencingDiskBasePath(string path) => Settings.DifferencingDiskBasePath = path;
    }
}

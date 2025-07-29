using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;
using LabAssistant.Services.Configuration;
using Microsoft.VisualBasic.Logging;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using LabAssistant.Views;

namespace LabAssistant.ViewModels;

public partial class DeploymentViewModel : ObservableObject
{
    private readonly VirtualSwitchProvider _switchProvider;
    private readonly MultiVmDeploymentCoordinator _coordinator;
    public Action<string>? LogHandler { get; set; }

    public ObservableCollection<string> AvailableSwitches { get; } = new();

    [ObservableProperty]
    private bool isDeploying;

    [ObservableProperty]
    private ObservableCollection<string> logs = new();

    [ObservableProperty]
    public ObservableCollection<VmLogGroup> logGroups = new();

    public ObservableCollection<VmEntryViewModel> VmEntries { get; }

    public DeploymentViewModel(MultiVmDeploymentCoordinator coordinator, VirtualSwitchProvider switchProvider)
    {
        LogHandler = message =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(message);
            });
        };

        _switchProvider = switchProvider;
        _coordinator = coordinator;
        VmEntries = new ObservableCollection<VmEntryViewModel>();
        _ = LoadAvailableSwitches();

        DebugLogger.Log("DeploymentViewModel initialized with default VM entry.");
    }

    private async Task LoadAvailableSwitches()
    {
        var switches = await _switchProvider.GetVirtualSwitchesAsync();
        AvailableSwitches.Clear();
        foreach (var s in switches)
            AvailableSwitches.Add(s);
    }
    public void AddLog(Guid vmId, string vmName, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var group = LogGroups.FirstOrDefault(g => g.VmId == vmId);
            if (group == null)
            {
                group = new VmLogGroup { VmId = vmId, VmName = vmName };
                LogGroups.Add(group);
            }

            // Update name in case it changed
            if (group.VmName != vmName)
                group.VmName = vmName;

            group.Entries.Add(message);
        });
    }

    [RelayCommand]
    private void DeleteVm(VmEntryViewModel vmEntry)
    {
        RemoveVm(vmEntry);
    }
    public void RemoveVm(VmEntryViewModel vmEntry)
    {
        if (VmEntries.Contains(vmEntry))
            VmEntries.Remove(vmEntry);

        var logGroup = LogGroups.FirstOrDefault(g => g.VmId == vmEntry.DeploymentContext.VmId);
        if (logGroup != null)
            LogGroups.Remove(logGroup);
    }

    [RelayCommand]
    private void AddVm()
    {
        string vmName = $"VM{VmEntries.Count + 1}";
        DebugLogger.Log($"Adding new VM entry. {vmName}");
        Guid VmId = Guid.NewGuid();
        var context = new VmDeploymentContext
        {
            VmId = VmId,
            VmName = vmName,
            IsVhdDifferencing = true,
            VhdDifferencingParentPath = "C:\\training\\BaseVHDX\\En_Win_Server_2022 .vhdx",
            VirtualSwitchName = AvailableSwitches.FirstOrDefault() ?? "",
            MemoryMb = 2048,
            CpuCount = 2,
            VmPath = $"{SettingsManager.Settings.VmBasePath}\\{vmName}",
            VhdPath = $"{SettingsManager.Settings.VmBasePath}\\{vmName}\\{vmName}.vhdx",
            LogCallback = msg => AddLog(VmId, vmName, msg)
        };
        var vmEntry = new VmEntryViewModel(context, this);
        VmEntries.Add(vmEntry);
        vmEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vmEntry.VmName))
            {
                var logGroup = LogGroups.FirstOrDefault(g => g.VmId == context.VmId);
                if (logGroup != null)
                    logGroup.VmName = vmEntry.VmName;
            }
        };


    }

    [RelayCommand]
    private async Task DeployAllAsync()
    {
        DebugLogger.Log("Starting deployment.");
        IsDeploying = true;
        Logs.Clear();
        Logs.Add("Starting VM deployments...");

        var multiContext = new MultiVmDeploymentContext
        {
            VmContexts = VmEntries.Select(vm => vm.DeploymentContext).ToList()
        };

        await _coordinator.DeployAllAsync(multiContext);

        foreach (var ctx in multiContext.VmContexts)
        {
            foreach (var log in ctx.Logs)
                Logs.Add($"[{ctx.VmName}] {log}");
        }

        DebugLogger.Log("All deployments completed.");
        Logs.Add("All deployments completed.");
        IsDeploying = false;
    }

    [RelayCommand]
    private void OpenVmDetail(VmEntryViewModel selectedVm)
    {
        selectedVm.StartEditing();
        var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
        mainWindow.MainContentFrame.Navigate(new VmDetailPage(selectedVm));
    }
}
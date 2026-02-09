using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LabAssistant;
using LabAssistant.Views;

namespace LabAssistant.ViewModels;

public partial class DeploymentViewModel : ObservableObject
{
    private readonly VirtualSwitchProvider _switchProvider;
    private readonly MultiVmDeploymentCoordinator _coordinator;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILabTemplateStore _templateStore;
    private readonly IAppPaths _appPaths;
    public Action<string>? LogHandler { get; set; }

    public ObservableCollection<string> AvailableSwitches { get; } = new();

    [ObservableProperty]
    private bool isDeploying;

    [ObservableProperty]
    private ObservableCollection<string> logs = new();

    [ObservableProperty]
    public ObservableCollection<VmLogGroup> logGroups = new();

    public ObservableCollection<VmEntryViewModel> VmEntries { get; }

    public DeploymentViewModel(
        MultiVmDeploymentCoordinator coordinator,
        VirtualSwitchProvider switchProvider,
        IAppSettingsStore settingsStore,
        ILabTemplateStore templateStore,
        IAppPaths appPaths)
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
        _settingsStore = settingsStore;
        _templateStore = templateStore;
        _appPaths = appPaths;
        VmEntries = new ObservableCollection<VmEntryViewModel>();
        _ = LoadAvailableSwitches();

        DebugLogger.Log("DeploymentViewModel initialized with default VM entry.");
    }

    private async Task LoadAvailableSwitches()
    {
        var switches = await _switchProvider.GetVirtualSwitchesAsync();
        AvailableSwitches.Clear();
        foreach (var s in switches)
        {
            AvailableSwitches.Add(s);
        }
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

            if (group.VmName != vmName)
            {
                group.VmName = vmName;
            }

            group.Entries.Add(message);
        });
    }

    private void ApplyDeploymentPolicy(VmDeploymentContext context)
    {
        var settings = _settingsStore.Settings;
        context.PerVmFailFast = settings.PerVmFailFast;
        context.NonBlockingOptionalSteps = new List<string>(settings.NonBlockingOptionalSteps ?? new List<string>());
    }

    [RelayCommand]
    private void DeleteVm(VmEntryViewModel vmEntry)
    {
        RemoveVm(vmEntry);
    }

    public void RemoveVm(VmEntryViewModel vmEntry)
    {
        if (VmEntries.Contains(vmEntry))
        {
            VmEntries.Remove(vmEntry);
        }

        var logGroup = LogGroups.FirstOrDefault(g => g.VmId == vmEntry.DeploymentContext.VmId);
        if (logGroup != null)
        {
            LogGroups.Remove(logGroup);
        }
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
            VhdDifferencingParentPath = string.Empty,
            VirtualSwitchName = AvailableSwitches.FirstOrDefault() ?? "",
            MemoryMb = 2048,
            CpuCount = 2,
            VmPath = $"{_settingsStore.Settings.VmBasePath}\\{vmName}",
            VhdPath = $"{_settingsStore.Settings.VmBasePath}\\{vmName}\\{vmName}.vhdx",
            LogCallback = msg => AddLog(VmId, vmName, msg)
        };
        ApplyDeploymentPolicy(context);
        var vmEntry = new VmEntryViewModel(context, this, _settingsStore);
        VmEntries.Add(vmEntry);
        vmEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vmEntry.VmName))
            {
                var logGroup = LogGroups.FirstOrDefault(g => g.VmId == context.VmId);
                if (logGroup != null)
                {
                    logGroup.VmName = vmEntry.VmName;
                }
            }
        };
    }

    [RelayCommand]
    private void SaveAsTemplate()
    {
        if (VmEntries.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Add at least one VM before saving as a template.",
                "Save as Template",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var owner = (MainWindow)System.Windows.Application.Current.MainWindow;
        var detailsDialog = new TemplateSaveDetailsDialog(null, null, "v0")
        {
            Owner = owner
        };
        if (detailsDialog.ShowDialog() != true)
        {
            return;
        }

        var templateName = detailsDialog.TemplateName;
        var templateDescription = detailsDialog.TemplateDescription;
        var templateVersion = detailsDialog.TemplateVersion;
        var templateId = Guid.NewGuid().ToString("N");

        var template = new LabTemplate
        {
            Id = templateId,
            Name = templateName,
            Description = string.IsNullOrWhiteSpace(templateDescription) ? null : templateDescription,
            Version = templateVersion,
            VmTemplates = VmEntries.Select(entry =>
            {
                var context = entry.DeploymentContext;
                var baseVhdPath = !string.IsNullOrWhiteSpace(context.VhdDifferencingParentPath)
                    ? context.VhdDifferencingParentPath
                    : context.VhdPath;

                return new VmTemplate
                {
                    Name = context.VmName,
                    MemoryMb = context.MemoryMb,
                    CpuCount = context.CpuCount,
                    SwitchName = context.VirtualSwitchName,
                    VhdxId = context.VhdxId,
                    VhdPath = baseVhdPath
                };
            }).ToList()
        };

        var reviewDialog = new TemplateSaveReviewDialog(
            new TemplateSaveReviewModel(template.Name, template.Id, template.VmTemplates.Count, template.Description, template.Version))
        {
            Owner = owner
        };

        if (reviewDialog.ShowDialog() != true)
        {
            return;
        }

        var templateFolder = _settingsStore.Settings.TemplateFolder;
        if (string.IsNullOrWhiteSpace(templateFolder))
        {
            templateFolder = _appPaths.TemplatesFolder;
        }

        var filePath = _templateStore.SaveToFolder(templateFolder, template.Name, template);

        System.Windows.MessageBox.Show(
            $"Template saved to {filePath}",
            "Save as Template",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }


    [RelayCommand]
    private async Task DeployAllAsync()
    {
        DebugLogger.Log("Starting deployment.");
        IsDeploying = true;
        Logs.Clear();
        Logs.Add("Starting VM deployments...");

        foreach (var entry in VmEntries)
        {
            ApplyDeploymentPolicy(entry.DeploymentContext);
        }

        var multiContext = new MultiVmDeploymentContext
        {
            VmContexts = VmEntries.Select(vm => vm.DeploymentContext).ToList(),
            StopAllOnAnyVmFailure = _settingsStore.Settings.StopAllOnAnyVmFailure
        };

        await _coordinator.DeployAllAsync(multiContext);

        foreach (var ctx in multiContext.VmContexts)
        {
            foreach (var log in ctx.Logs)
            {
                Logs.Add($"[{ctx.VmName}] {log}");
            }
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
        mainWindow.MainContentFrame.Navigate(new VmDetailPage(this, selectedVm, () => mainWindow.MainContentFrame.Navigate(new Views.DeployPage())));
    }
}

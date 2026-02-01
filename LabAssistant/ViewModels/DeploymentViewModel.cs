using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Configuration;
using LabAssistant.Services.Logging;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LabAssistant;
using LabAssistant.Views;

namespace LabAssistant.ViewModels;

public partial class DeploymentViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        var nameDialog = new InputDialog("Enter template name")
        {
            Owner = owner
        };
        if (nameDialog.ShowDialog() != true)
        {
            return;
        }

        var templateName = nameDialog.ResponseText.Trim();
        if (string.IsNullOrWhiteSpace(templateName))
        {
            System.Windows.MessageBox.Show(
                "Template name is required.",
                "Save as Template",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var templateId = Guid.NewGuid().ToString("N");

        var template = new LabTemplate
        {
            Id = templateId,
            Name = templateName,
            Version = "v0",
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
                    VhdPath = baseVhdPath
                };
            }).ToList()
        };

        var templateFolder = SettingsManager.Settings.TemplateFolder;
        if (string.IsNullOrWhiteSpace(templateFolder))
        {
            templateFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LabAssistant",
                "Templates");
        }

        Directory.CreateDirectory(templateFolder);
        var fileName = GetTemplateFileName(template.Name, templateFolder);
        var filePath = Path.Combine(templateFolder, fileName);
        var json = JsonSerializer.Serialize(template, TemplateJsonOptions);
        File.WriteAllText(filePath, json);

        System.Windows.MessageBox.Show(
            $"Template saved to {filePath}",
            "Save as Template",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "lab-template" : sanitized;
    }

    private static string GetTemplateFileName(string templateName, string folderPath)
    {
        var baseName = SanitizeFileName(templateName);
        var fileName = $"{baseName}.json";
        var filePath = Path.Combine(folderPath, fileName);
        var suffix = 1;

        while (File.Exists(filePath))
        {
            fileName = $"{baseName}-{suffix}.json";
            filePath = Path.Combine(folderPath, fileName);
            suffix++;
        }

        return fileName;
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

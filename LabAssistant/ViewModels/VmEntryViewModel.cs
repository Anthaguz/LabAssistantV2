using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Configuration;
using System.Collections.ObjectModel;
using System.Windows.Input;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace LabAssistant.ViewModels;

public partial class VmEntryViewModel : ObservableObject
{
    public VmDeploymentContext DeploymentContext { get; }
    public ObservableCollection<string> AvailableSwitches => _parentDeploymentViewModel.AvailableSwitches;
    private readonly DeploymentViewModel _parentDeploymentViewModel;
    private string _originalVmName = string.Empty;
    private string _originalVhdPath = string.Empty;
    private string _originalSelectedSwitchName = string.Empty;
    private bool _originalConfigureTimeZone = false;
    private bool _originalInstallSoftware = false;
    public VmEntryViewModel(VmDeploymentContext context, DeploymentViewModel parent)
    {
        DeploymentContext = context;

        vmName = context.VmName;
        vhdPath = context.VhdPath;
        selectedSwitchName = context.VirtualSwitchName;
        configureTimeZone = context.ConfigureTimeZone;
        installSoftware = context.InstallSoftware;
        _parentDeploymentViewModel = parent;

    }
    public void StartEditing()
    {
        _originalVmName = VmName;
        _originalVhdPath = VhdPath;
        _originalSelectedSwitchName = SelectedSwitchName;
        _originalConfigureTimeZone = ConfigureTimeZone;
        _originalInstallSoftware = InstallSoftware;
        IsEditingName = true;
    }

    public ICommand CancelChangesCommand => new RelayCommand(CancelChanges);

    private void CancelChanges()
    {
        VmName = _originalVmName;
        VhdPath = _originalVhdPath;
        SelectedSwitchName = _originalSelectedSwitchName;
        ConfigureTimeZone = _originalConfigureTimeZone;
        InstallSoftware = _originalInstallSoftware;
    }
    // Used for switching UI between label and textbox
    [ObservableProperty]
    private bool isEditingName = false;

    [ObservableProperty]
    private string vmName;

    [ObservableProperty]
    private string vhdPath;

    [ObservableProperty]
    private string selectedSwitchName;

    [ObservableProperty]
    private bool configureTimeZone;

    [ObservableProperty]
    private bool installSoftware;


    [RelayCommand]
    private void StopEditing() => IsEditingName = false;

    partial void OnVmNameChanged(string value)
    {
        DeploymentContext.VmName = value;
        DeploymentContext.VmPath = $@"{SettingsManager.Settings.VmBasePath}\{value}";
        DeploymentContext.VhdPath = $@"{DeploymentContext.VmPath}\{value}.vhdx";

        // Update local VhdPath to reflect change in GUI
        VhdPath = DeploymentContext.VhdPath;
    }

    partial void OnVhdPathChanged(string value)
    {
        DeploymentContext.VhdPath = value;
    }

    partial void OnSelectedSwitchNameChanged(string value)
    {
        DeploymentContext.VirtualSwitchName = value;
    }

    partial void OnConfigureTimeZoneChanged(bool value)
    {
        DeploymentContext.ConfigureTimeZone = value;
    }

    partial void OnInstallSoftwareChanged(bool value)
    {
        DeploymentContext.InstallSoftware = value;
    }
}

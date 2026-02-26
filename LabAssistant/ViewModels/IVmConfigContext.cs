namespace LabAssistant.ViewModels
{
    public interface IVmConfigContext
    {
        string Name { get; set; }
        int MemoryMb { get; set; }
        int CpuCount { get; set; }
        string? SwitchName { get; set; }
        string? VhdxId { get; set; }
        string? BaseVhdPath { get; set; }
        string? VhdxSignature { get; set; }
        bool ConfigureTimeZoneEnabled { get; set; }
        bool InstallSoftwareEnabled { get; set; }
        bool InstallRoleEnabled { get; set; }
        System.Collections.ObjectModel.ObservableCollection<string> AvailableSwitches { get; }
        bool HasSwitches { get; }
        bool ShowSwitchWarning { get; }
        string SwitchWarning { get; }
    }
}

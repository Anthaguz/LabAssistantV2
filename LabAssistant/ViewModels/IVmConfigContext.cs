namespace LabAssistant.ViewModels
{
    public interface IVmConfigContext
    {
        string Name { get; set; }
        int MemoryMb { get; set; }
        int CpuCount { get; set; }
        string? SwitchName { get; set; }
        string? VhdxId { get; set; }
        string? VhdPath { get; set; }
        System.Collections.ObjectModel.ObservableCollection<string> AvailableSwitches { get; }
        bool HasSwitches { get; }
        bool ShowSwitchWarning { get; }
        string SwitchWarning { get; }
    }
}

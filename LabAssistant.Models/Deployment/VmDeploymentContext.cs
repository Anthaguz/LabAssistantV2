using LabAssistant.Models.PowerShell;
namespace LabAssistant.Models.Deployment
{

    public class VmDeploymentContext
    {
        public Guid VmId;
        //Vm Base information
        public string VmPath { get; set; } = string.Empty;
        public string VmName { get; set; } = string.Empty;
        public int MemoryMb { get; set; } = 2048; // Default to 2GB
        public int CpuCount { get; set; } = 2; // Default to 2 CPUs

        // VHD Information
        public string VhdPath { get; set; } = string.Empty;
        public bool IsVhdDifferencing { get; set; } = false; 
        public string VhdDifferencingParentPath { get; set; } = string.Empty;
        public string? VhdxId { get; set; }
        public string? VhdxSignature { get; set; }

        //Network Configuration
        public string VirtualSwitchName { get; set; } = string.Empty;

        public bool IsSuccess { get; set; } = true;
        public bool GuestServicesEnabled { get; set; } = false;
        public bool ConfigureTimeZone { get; set; } = false;
        public bool InstallSoftware { get; set; } = false;

        // Logging and PowerShell
        public List<string> Logs { get; } = new();
        public PowerShellHandle? PowerShellHandle { get; set; }
        public Action<string>? LogCallback { get; set; }

    }
}

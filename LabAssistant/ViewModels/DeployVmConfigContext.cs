using System.Collections.ObjectModel;
using System.ComponentModel;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;

namespace LabAssistant.ViewModels
{
    public class DeployVmConfigContext : INotifyPropertyChanged, IVmConfigContext
    {
        private readonly DeploymentViewModel _deploymentViewModel;
        private readonly IAppSettingsStore _settingsStore;
        private readonly VmDeploymentContext _context;

        public DeployVmConfigContext(DeploymentViewModel deploymentViewModel, VmDeploymentContext context, IAppSettingsStore settingsStore)
        {
            _deploymentViewModel = deploymentViewModel;
            _context = context;
            _settingsStore = settingsStore;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _context.VmName;
            set
            {
                if (_context.VmName != value)
                {
                    _context.VmName = value;
                    _context.VmPath = $@"{_settingsStore.Settings.VmBasePath}\{value}";
                    _context.VhdPath = $@"{_context.VmPath}\{value}.vhdx";
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public int MemoryMb
        {
            get => _context.MemoryMb;
            set
            {
                if (_context.MemoryMb != value)
                {
                    _context.MemoryMb = value;
                    OnPropertyChanged(nameof(MemoryMb));
                }
            }
        }

        public int CpuCount
        {
            get => _context.CpuCount;
            set
            {
                if (_context.CpuCount != value)
                {
                    _context.CpuCount = value;
                    OnPropertyChanged(nameof(CpuCount));
                }
            }
        }

        public string? SwitchName
        {
            get => _context.VirtualSwitchName;
            set
            {
                if (_context.VirtualSwitchName != value)
                {
                    _context.VirtualSwitchName = value ?? string.Empty;
                    OnPropertyChanged(nameof(SwitchName));
                }
            }
        }

        public string? VhdxId
        {
            get => _context.VhdxId;
            set
            {
                if (_context.VhdxId != value)
                {
                    _context.VhdxId = value;
                    OnPropertyChanged(nameof(VhdxId));
                }
            }
        }

        public string? VhdPath
        {
            get => _context.VhdDifferencingParentPath;
            set
            {
                if (_context.VhdDifferencingParentPath != value)
                {
                    _context.VhdDifferencingParentPath = value ?? string.Empty;
                    OnPropertyChanged(nameof(VhdPath));
                }
            }
        }

        public ObservableCollection<string> AvailableSwitches => _deploymentViewModel.AvailableSwitches;

        public bool HasSwitches => AvailableSwitches.Count > 0;

        public bool ShowSwitchWarning => !HasSwitches;

        public string SwitchWarning => "No virtual switches found. Networking configuration is unavailable.";

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

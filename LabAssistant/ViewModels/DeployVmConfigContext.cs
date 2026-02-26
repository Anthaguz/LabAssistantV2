using System.Collections.ObjectModel;
using System.ComponentModel;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels
{
    public class DeployVmConfigContext : INotifyPropertyChanged, IVmConfigContext
    {
        private readonly DeploymentViewModel? _deploymentViewModel;
        private readonly IAppSettingsStore _settingsStore;
        private readonly VmDeploymentContext _context;

        public DeployVmConfigContext(DeploymentViewModel? deploymentViewModel, VmDeploymentContext context, IAppSettingsStore settingsStore)
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

        public string? BaseVhdPath
        {
            get => _context.BaseVhdPath;
            set
            {
                if (_context.BaseVhdPath != value)
                {
                    _context.BaseVhdPath = value ?? string.Empty;
                    OnPropertyChanged(nameof(BaseVhdPath));
                }
            }
        }

        public string? VhdxSignature
        {
            get => _context.VhdxSignature;
            set
            {
                if (_context.VhdxSignature != value)
                {
                    _context.VhdxSignature = value;
                    OnPropertyChanged(nameof(VhdxSignature));
                }
            }
        }

        public bool ConfigureTimeZoneEnabled
        {
            get => _context.ConfigureTimeZone || (_context.TimeZoneConfig?.Enabled ?? false);
            set
            {
                if (ConfigureTimeZoneEnabled != value)
                {
                    _context.ConfigureTimeZone = value;
                    _context.TimeZoneConfig ??= new TimeZoneStepConfig();
                    _context.TimeZoneConfig.Enabled = value;
                    OnPropertyChanged(nameof(ConfigureTimeZoneEnabled));
                }
            }
        }

        public bool InstallSoftwareEnabled
        {
            get => _context.InstallSoftware || (_context.SoftwareConfig?.Enabled ?? false);
            set
            {
                if (InstallSoftwareEnabled != value)
                {
                    _context.InstallSoftware = value;
                    _context.SoftwareConfig ??= new SoftwareStepConfig();
                    _context.SoftwareConfig.Enabled = value;
                    OnPropertyChanged(nameof(InstallSoftwareEnabled));
                }
            }
        }

        public bool InstallRoleEnabled
        {
            get => _context.InstallRole || (_context.RoleConfig?.Enabled ?? false);
            set
            {
                if (InstallRoleEnabled != value)
                {
                    _context.InstallRole = value;
                    _context.RoleConfig ??= new RoleStepConfig();
                    _context.RoleConfig.Enabled = value;
                    OnPropertyChanged(nameof(InstallRoleEnabled));
                }
            }
        }

        public ObservableCollection<string> AvailableSwitches => _deploymentViewModel?.AvailableSwitches ?? new ObservableCollection<string>();

        public bool HasSwitches => AvailableSwitches.Count > 0;

        public bool ShowSwitchWarning => !HasSwitches;

        public string SwitchWarning => "No virtual switches found. Networking configuration is unavailable.";

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName is nameof(Name)
                or nameof(SwitchName)
                or nameof(VhdxId)
                or nameof(BaseVhdPath)
                or nameof(ConfigureTimeZoneEnabled)
                or nameof(InstallSoftwareEnabled)
                or nameof(InstallRoleEnabled))
            {
                _deploymentViewModel?.RequestQuickPreflightRefresh();
            }
        }
    }
}

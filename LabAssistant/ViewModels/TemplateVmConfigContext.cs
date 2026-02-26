using System.Collections.ObjectModel;
using System.ComponentModel;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels
{
    public class TemplateVmConfigContext : INotifyPropertyChanged, IVmConfigContext
    {
        private readonly TemplateEditorViewModel _editorViewModel;
        private readonly VmTemplate _vmTemplate;

        public TemplateVmConfigContext(TemplateEditorViewModel editorViewModel, VmTemplate vmTemplate)
        {
            _editorViewModel = editorViewModel;
            _vmTemplate = vmTemplate;
            _editorViewModel.PropertyChanged += EditorViewModelOnPropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _vmTemplate.Name;
            set
            {
                if (_vmTemplate.Name != value)
                {
                    _vmTemplate.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public int MemoryMb
        {
            get => _vmTemplate.MemoryMb;
            set
            {
                if (_vmTemplate.MemoryMb != value)
                {
                    _vmTemplate.MemoryMb = value;
                    OnPropertyChanged(nameof(MemoryMb));
                }
            }
        }

        public int CpuCount
        {
            get => _vmTemplate.CpuCount;
            set
            {
                if (_vmTemplate.CpuCount != value)
                {
                    _vmTemplate.CpuCount = value;
                    OnPropertyChanged(nameof(CpuCount));
                }
            }
        }

        public string? SwitchName
        {
            get => _vmTemplate.SwitchName;
            set
            {
                if (_vmTemplate.SwitchName != value)
                {
                    _vmTemplate.SwitchName = value;
                    OnPropertyChanged(nameof(SwitchName));
                }
            }
        }

        public string? VhdxId
        {
            get => _vmTemplate.VhdxId;
            set
            {
                if (_vmTemplate.VhdxId != value)
                {
                    _vmTemplate.VhdxId = value;
                    OnPropertyChanged(nameof(VhdxId));
                }
            }
        }

        public string? BaseVhdPath
        {
            get => _vmTemplate.VhdPath;
            set
            {
                if (_vmTemplate.VhdPath != value)
                {
                    _vmTemplate.VhdPath = value;
                    OnPropertyChanged(nameof(BaseVhdPath));
                }
            }
        }

        public string? VhdxSignature
        {
            get => _vmTemplate.VhdxSignature;
            set
            {
                if (_vmTemplate.VhdxSignature != value)
                {
                    _vmTemplate.VhdxSignature = value;
                    OnPropertyChanged(nameof(VhdxSignature));
                }
            }
        }

        public bool ConfigureTimeZoneEnabled
        {
            get => _vmTemplate.TimeZoneConfig?.Enabled ?? false;
            set
            {
                if ((_vmTemplate.TimeZoneConfig?.Enabled ?? false) == value)
                {
                    return;
                }

                _vmTemplate.TimeZoneConfig ??= new TimeZoneStepConfig();
                _vmTemplate.TimeZoneConfig.Enabled = value;
                OnPropertyChanged(nameof(ConfigureTimeZoneEnabled));
            }
        }

        public bool InstallSoftwareEnabled
        {
            get => _vmTemplate.SoftwareConfig?.Enabled ?? false;
            set
            {
                if ((_vmTemplate.SoftwareConfig?.Enabled ?? false) == value)
                {
                    return;
                }

                _vmTemplate.SoftwareConfig ??= new SoftwareStepConfig();
                _vmTemplate.SoftwareConfig.Enabled = value;
                OnPropertyChanged(nameof(InstallSoftwareEnabled));
            }
        }

        public bool InstallRoleEnabled
        {
            get => _vmTemplate.RoleConfig?.Enabled ?? false;
            set
            {
                if ((_vmTemplate.RoleConfig?.Enabled ?? false) == value)
                {
                    return;
                }

                _vmTemplate.RoleConfig ??= new RoleStepConfig();
                _vmTemplate.RoleConfig.Enabled = value;
                OnPropertyChanged(nameof(InstallRoleEnabled));
            }
        }

        public ObservableCollection<string> AvailableSwitches => _editorViewModel.AvailableSwitches;

        public bool HasSwitches => _editorViewModel.HasSwitches;

        public bool ShowSwitchWarning => _editorViewModel.ShowSwitchWarning;

        public string SwitchWarning => _editorViewModel.SwitchWarning;

        private void EditorViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(TemplateEditorViewModel.HasSwitches)
                or nameof(TemplateEditorViewModel.ShowSwitchWarning)
                or nameof(TemplateEditorViewModel.SwitchWarning))
            {
                OnPropertyChanged(e.PropertyName);
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

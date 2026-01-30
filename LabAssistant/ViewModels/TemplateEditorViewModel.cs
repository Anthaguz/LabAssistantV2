using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using LabAssistant.Business;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels
{
    public class TemplateEditorViewModel : INotifyPropertyChanged
    {
        private static readonly JsonSerializerOptions TemplateJsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly VirtualSwitchProvider? _switchProvider;
        private LabTemplate _template;
        private ObservableCollection<VmTemplate> _vmTemplates;
        private string? _currentTemplatePath;
        private bool _hasSwitches;
        private string _switchWarning = string.Empty;
        private string? _defaultSwitchName;

        public TemplateEditorViewModel()
            : this(null)
        {
        }

        public TemplateEditorViewModel(VirtualSwitchProvider? switchProvider)
        {
            _switchProvider = switchProvider;
            _template = new LabTemplate { Version = "v0" };
            _vmTemplates = new ObservableCollection<VmTemplate>();
            _vmTemplates.CollectionChanged += VmTemplates_CollectionChanged;
            SyncVmTemplates();

            if (_switchProvider != null)
            {
                _ = LoadAvailableSwitchesAsync();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LabTemplate Template
        {
            get => _template;
            private set
            {
                if (!ReferenceEquals(_template, value))
                {
                    _template = value;
                    OnPropertyChanged(nameof(Template));
                }
            }
        }

        public ObservableCollection<VmTemplate> VmTemplates
        {
            get => _vmTemplates;
            private set
            {
                if (!ReferenceEquals(_vmTemplates, value))
                {
                    _vmTemplates.CollectionChanged -= VmTemplates_CollectionChanged;
                    _vmTemplates = value;
                    _vmTemplates.CollectionChanged += VmTemplates_CollectionChanged;
                    OnPropertyChanged(nameof(VmTemplates));
                }
            }
        }

        public string? CurrentTemplatePath
        {
            get => _currentTemplatePath;
            private set
            {
                if (!string.Equals(_currentTemplatePath, value, StringComparison.Ordinal))
                {
                    _currentTemplatePath = value;
                    OnPropertyChanged(nameof(CurrentTemplatePath));
                }
            }
        }

        public ObservableCollection<string> AvailableSwitches { get; } = new();

        public bool HasSwitches
        {
            get => _hasSwitches;
            private set
            {
                if (_hasSwitches != value)
                {
                    _hasSwitches = value;
                    OnPropertyChanged(nameof(HasSwitches));
                    OnPropertyChanged(nameof(ShowSwitchWarning));
                }
            }
        }

        public bool ShowSwitchWarning => !HasSwitches;

        public string SwitchWarning
        {
            get => _switchWarning;
            private set
            {
                if (!string.Equals(_switchWarning, value, StringComparison.Ordinal))
                {
                    _switchWarning = value;
                    OnPropertyChanged(nameof(SwitchWarning));
                }
            }
        }

        public void AddVm()
        {
            VmTemplates.Add(new VmTemplate
            {
                Name = "New VM",
                MemoryMb = 2048,
                CpuCount = 2,
                SwitchName = _defaultSwitchName
            });
        }

        public void RemoveVm(VmTemplate vmTemplate)
        {
            if (VmTemplates.Contains(vmTemplate))
            {
                VmTemplates.Remove(vmTemplate);
            }
        }

        public void LoadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var template = JsonSerializer.Deserialize<LabTemplate>(json, TemplateJsonOptions);
            if (template == null)
            {
                throw new InvalidOperationException("Template file could not be loaded.");
            }

            Template = template;
            VmTemplates = new ObservableCollection<VmTemplate>(template.VmTemplates ?? new());
            SyncVmTemplates();
            ApplyDefaultSwitchToTemplates();
            CurrentTemplatePath = filePath;
        }

        public void SaveToFile(string filePath)
        {
            SyncVmTemplates();
            var json = JsonSerializer.Serialize(Template, TemplateJsonOptions);
            File.WriteAllText(filePath, json);
            CurrentTemplatePath = filePath;
        }

        private void VmTemplates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncVmTemplates();
        }

        private void SyncVmTemplates()
        {
            Template.VmTemplates = VmTemplates.ToList();
        }

        private async Task LoadAvailableSwitchesAsync()
        {
            if (_switchProvider == null)
            {
                return;
            }

            var switches = await _switchProvider.GetVirtualSwitchesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableSwitches.Clear();
                foreach (var switchName in switches)
                {
                    AvailableSwitches.Add(switchName);
                }

                _defaultSwitchName = switches.FirstOrDefault();
                HasSwitches = switches.Count > 0;
                SwitchWarning = HasSwitches
                    ? string.Empty
                    : "No virtual switches found. Networking configuration is unavailable.";

                ApplyDefaultSwitchToTemplates();
            });
        }

        private void ApplyDefaultSwitchToTemplates()
        {
            if (string.IsNullOrWhiteSpace(_defaultSwitchName))
            {
                return;
            }

            foreach (var vm in VmTemplates)
            {
                if (string.IsNullOrWhiteSpace(vm.SwitchName))
                {
                    vm.SwitchName = _defaultSwitchName;
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

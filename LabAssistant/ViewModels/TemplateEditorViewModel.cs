using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Business;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.ViewModels
{
    public class TemplateEditorViewModel : INotifyPropertyChanged
    {
        private readonly VirtualSwitchProvider? _switchProvider;
        private readonly IAppSettingsStore _settingsStore;
        private readonly IVhdxCatalogStore _catalogStore;
        private readonly ILabTemplateStore _templateStore;
        private LabTemplate _template;
        private ObservableCollection<VmTemplate> _vmTemplates;
        private string? _currentTemplatePath;
        private bool _hasSwitches;
        private string _switchWarning = string.Empty;
        private string? _defaultSwitchName;

        public TemplateEditorViewModel(
            VirtualSwitchProvider? switchProvider,
            IAppSettingsStore settingsStore,
            IVhdxCatalogStore catalogStore,
            ILabTemplateStore templateStore)
        {
            _switchProvider = switchProvider;
            _settingsStore = settingsStore;
            _catalogStore = catalogStore;
            _templateStore = templateStore;
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
            var template = _templateStore.LoadFromFile(filePath);
            Template = template;
            VmTemplates = new ObservableCollection<VmTemplate>(template.VmTemplates ?? new());
            SyncVmTemplates();
            ApplyDefaultSwitchToTemplates();
            CurrentTemplatePath = filePath;
        }

        public void SaveToFile(string filePath)
        {
            SyncVmTemplates();
            _templateStore.SaveToFile(filePath, Template);
            CurrentTemplatePath = filePath;
        }

        public int AutoResolveMissingVhdxBySignature()
        {
            SyncVmTemplates();
            var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            var catalogItems = catalogResult.Items;
            var catalogIds = new HashSet<string>(
                catalogItems.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);

            var resolvedCount = 0;
            foreach (var vm in Template.VmTemplates)
            {
                var hasId = !string.IsNullOrWhiteSpace(vm.VhdxId);
                var hasValidId = hasId && catalogIds.Contains(vm.VhdxId!);
                if (hasValidId)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(vm.VhdxSignature))
                {
                    continue;
                }

                var matches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems);
                if (matches.Count == 1)
                {
                    var match = matches[0];
                    vm.VhdxId = match.Id;
                    vm.VhdPath = match.Path;
                    vm.VhdxSignature = match.Signature;
                    resolvedCount++;
                }
            }

            return resolvedCount;
        }

        public List<MissingVhdxReference> GetMissingVhdxReferences()
        {
            SyncVmTemplates();
            var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            var catalogIds = new HashSet<string>(
                catalogResult.Items.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);

            var missing = new List<MissingVhdxReference>();
            foreach (var vm in Template.VmTemplates)
            {
                if (string.IsNullOrWhiteSpace(vm.VhdxId))
                {
                    continue;
                }

                if (!catalogIds.Contains(vm.VhdxId))
                {
                    missing.Add(new MissingVhdxReference(vm, vm.VhdxId));
                }
            }

            return missing;
        }

        public TemplateValidationSummary ValidateForSave()
        {
            SyncVmTemplates();
            var warnings = new List<string>();
            var errors = new List<string>();

            var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            if (catalogResult.Errors.Count > 0)
            {
                warnings.AddRange(catalogResult.Errors.Select(error => $"Catalog: {error}"));
            }

            var validation = LabTemplateValidator.Validate(Template, catalogResult.Items);
            errors.AddRange(validation.Errors);

            var catalogIds = new HashSet<string>(
                catalogResult.Items.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (var vm in Template.VmTemplates)
            {
                if (string.IsNullOrWhiteSpace(vm.VhdxId))
                {
                    continue;
                }

                if (!catalogIds.Contains(vm.VhdxId))
                {
                    var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name;
                    errors.Add($"VM '{vmName}' references missing VHDX id '{vm.VhdxId}'.");
                }
            }

            return new TemplateValidationSummary(errors, warnings);
        }

        public List<VmValidationIssue> BuildVmValidationIssues()
        {
            SyncVmTemplates();
            var issues = new List<VmValidationIssue>();

            var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            var catalogIds = new HashSet<string>(
                catalogResult.Items.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (var vm in Template.VmTemplates)
            {
                if (string.IsNullOrWhiteSpace(vm.Name))
                {
                    issues.Add(new VmValidationIssue(vm, "VM name is required.", VmValidationField.Name));
                }

                if (vm.MemoryMb <= 0)
                {
                    issues.Add(new VmValidationIssue(vm, "Memory must be a positive number.", VmValidationField.MemoryMb));
                }

                if (vm.CpuCount <= 0)
                {
                    issues.Add(new VmValidationIssue(vm, "CPU count must be a positive number.", VmValidationField.CpuCount));
                }

                if (string.IsNullOrWhiteSpace(vm.SwitchName) && AvailableSwitches.Count > 0)
                {
                    issues.Add(new VmValidationIssue(vm, "Select a virtual switch.", VmValidationField.SwitchName));
                }

                if (string.IsNullOrWhiteSpace(vm.VhdxId) && string.IsNullOrWhiteSpace(vm.VhdPath))
                {
                    issues.Add(new VmValidationIssue(vm, "Select a base VHDX.", VmValidationField.Vhdx));
                }
                else if (!string.IsNullOrWhiteSpace(vm.VhdxId) && !catalogIds.Contains(vm.VhdxId))
                {
                    issues.Add(new VmValidationIssue(vm, "Selected VHDX is not in the local catalog.", VmValidationField.Vhdx));
                }
            }

            return issues;
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

    public class TemplateValidationSummary
    {
        public TemplateValidationSummary(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = errors.ToList();
            Warnings = warnings.ToList();
        }

        public List<string> Errors { get; }

        public List<string> Warnings { get; }
    }

    public sealed class VmValidationIssue
    {
        public VmValidationIssue(VmTemplate vm, string message, VmValidationField field)
        {
            Vm = vm;
            Message = message;
            Field = field;
        }

        public VmTemplate Vm { get; }

        public string Message { get; }

        public VmValidationField Field { get; }
    }

    public enum VmValidationField
    {
        Name,
        MemoryMb,
        CpuCount,
        SwitchName,
        Vhdx
    }
}

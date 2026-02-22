using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
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
        private readonly TemplateValidationService _validationService;
        private readonly MissingVhdxResolutionService _missingVhdxResolutionService;
        private LabTemplate _template;
        private ObservableCollection<VmTemplate> _vmTemplates;
        private string? _currentTemplatePath;
        private bool _hasSwitches;
        private string _switchWarning = string.Empty;
        private string? _defaultSwitchName;
        private Dictionary<string, int> _vmIssueCounts = new(StringComparer.OrdinalIgnoreCase);

        public TemplateEditorViewModel(
            VirtualSwitchProvider? switchProvider,
            IAppSettingsStore settingsStore,
            IVhdxCatalogStore catalogStore,
            ILabTemplateStore templateStore,
            TemplateValidationService validationService,
            MissingVhdxResolutionService missingVhdxResolutionService)
        {
            _switchProvider = switchProvider;
            _settingsStore = settingsStore;
            _catalogStore = catalogStore;
            _templateStore = templateStore;
            _validationService = validationService;
            _missingVhdxResolutionService = missingVhdxResolutionService;
            _template = new LabTemplate();
            _vmTemplates = new ObservableCollection<VmTemplate>();
            _vmTemplates.CollectionChanged += VmTemplates_CollectionChanged;
            EnsureCanonicalTemplateDefaults(_template);
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

        public IReadOnlyDictionary<string, int> VmIssueCounts
        {
            get => _vmIssueCounts;
            private set
            {
                _vmIssueCounts = new Dictionary<string, int>(value, StringComparer.OrdinalIgnoreCase);
                OnPropertyChanged(nameof(VmIssueCounts));
            }
        }

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
            EnsureCanonicalTemplateDefaults(template);
            Template = template;
            VmTemplates = new ObservableCollection<VmTemplate>(template.VmTemplates ?? new());
            SyncVmTemplates();
            ApplyDefaultSwitchToTemplates();
            CurrentTemplatePath = filePath;
        }

        public void SaveToFile(string filePath)
        {
            EnsureCanonicalTemplateDefaults(Template);
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
            return _validationService.ValidateForSave(Template);
        }

        public MissingVhdxDialogState ResolveMissingVhdxForDialog()
        {
            SyncVmTemplates();
            var resolution = _missingVhdxResolutionService.ResolveMissingVhdx(Template);
            var missing = resolution.MissingVms
                .Where(vm => !string.IsNullOrWhiteSpace(vm.VhdxId))
                .Select(vm => new MissingVhdxReference(vm, vm.VhdxId!))
                .ToList();

            return new MissingVhdxDialogState(
                resolution.CatalogErrors,
                missing);
        }

        public SaveValidationState BuildSaveValidationState()
        {
            var vmIssues = BuildVmValidationIssues();
            var missing = GetMissingVhdxReferences();
            var summary = ValidateForSave();

            return new SaveValidationState(vmIssues, missing, summary);
        }

        public void ApplyResolvedMissingVhdx(IEnumerable<ResolvedVhdxSelection> selections)
        {
            foreach (var selection in selections)
            {
                selection.Vm.VhdxId = selection.Item.Id;
                selection.Vm.VhdPath = selection.Item.Path;
                selection.Vm.VhdxSignature = VhdxSignature.Build(selection.Item);
            }
        }

        public List<VmValidationIssue> BuildVmValidationIssues()
        {
            SyncVmTemplates();
            var issues = new List<VmValidationIssue>();
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            var catalogIds = new HashSet<string>(
                catalogResult.Items.Select(item => item.Id),
                StringComparer.OrdinalIgnoreCase);

            foreach (var vm in Template.VmTemplates)
            {
                var vmKey = vm.Name ?? string.Empty;

                if (string.IsNullOrWhiteSpace(vm.Name))
                {
                    issues.Add(new VmValidationIssue(vm, "VM name is required.", VmValidationField.Name));
                    IncrementCount(counts, vmKey);
                }

                if (vm.MemoryMb <= 0)
                {
                    issues.Add(new VmValidationIssue(vm, "Memory must be a positive number.", VmValidationField.MemoryMb));
                    IncrementCount(counts, vmKey);
                }

                if (vm.CpuCount <= 0)
                {
                    issues.Add(new VmValidationIssue(vm, "CPU count must be a positive number.", VmValidationField.CpuCount));
                    IncrementCount(counts, vmKey);
                }

                if (string.IsNullOrWhiteSpace(vm.SwitchName) && AvailableSwitches.Count > 0)
                {
                    issues.Add(new VmValidationIssue(vm, "Select a virtual switch.", VmValidationField.SwitchName));
                    IncrementCount(counts, vmKey);
                }

                if (string.IsNullOrWhiteSpace(vm.VhdxId) && string.IsNullOrWhiteSpace(vm.VhdPath))
                {
                    issues.Add(new VmValidationIssue(vm, "Select a base VHDX.", VmValidationField.Vhdx));
                    IncrementCount(counts, vmKey);
                }
                else if (!string.IsNullOrWhiteSpace(vm.VhdxId) && !catalogIds.Contains(vm.VhdxId))
                {
                    issues.Add(new VmValidationIssue(vm, "Selected VHDX is not in the local catalog.", VmValidationField.Vhdx));
                    IncrementCount(counts, vmKey);
                }
            }

            VmIssueCounts = counts;
            return issues;
        }

        public List<VmValidationDisplayItem> BuildVmValidationDisplayItems()
        {
            return BuildVmValidationIssues()
                .Select(issue => new VmValidationDisplayItem(issue))
                .ToList();
        }

        public List<VmValidationDisplayItem> FilterVmValidationDisplayItems(
            IReadOnlyCollection<VmValidationDisplayItem> items,
            string? filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return items.ToList();
            }

            return items
                .Where(item => item.VmName.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void VmTemplates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncVmTemplates();
        }

        private void SyncVmTemplates()
        {
            foreach (var vm in VmTemplates.Where(vm => string.IsNullOrWhiteSpace(vm.VmId)).ToList())
            {
                // Keep vmId immutable in normal editing; only backfill when missing.
                var index = VmTemplates.IndexOf(vm);
                if (index < 0)
                {
                    continue;
                }

                VmTemplates[index] = CloneWithVmId(vm, Guid.NewGuid().ToString("N"));
            }

            Template.VmTemplates = VmTemplates.ToList();
        }

        private static void EnsureCanonicalTemplateDefaults(LabTemplate template)
        {
            if (string.IsNullOrWhiteSpace(template.SchemaVersion))
            {
                template.SchemaVersion = LabTemplate.CurrentSchemaVersion;
            }

            if (template.TemplateRevision <= 0)
            {
                template.TemplateRevision = 1;
            }

            if (string.IsNullOrWhiteSpace(template.CreatedWithAppVersion))
            {
                template.CreatedWithAppVersion = GetAppVersion();
            }

            if (string.IsNullOrWhiteSpace(template.TemplateType))
            {
                template.TemplateType = LabTemplate.SupportedTemplateType;
            }
        }

        private static VmTemplate CloneWithVmId(VmTemplate source, string vmId)
        {
            return new VmTemplate
            {
                VmId = vmId,
                Name = source.Name,
                MemoryMb = source.MemoryMb,
                CpuCount = source.CpuCount,
                VhdxId = source.VhdxId,
                VhdPath = source.VhdPath,
                VhdxSignature = source.VhdxSignature,
                SwitchName = source.SwitchName
            };
        }

        private static string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
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

        private static void IncrementCount(IDictionary<string, int> counts, string key)
        {
            if (counts.TryGetValue(key, out var current))
            {
                counts[key] = current + 1;
                return;
            }

            counts[key] = 1;
        }
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

    public sealed class VmValidationDisplayItem
    {
        public VmValidationDisplayItem(VmValidationIssue issue)
        {
            Issue = issue;
        }

        public VmValidationIssue Issue { get; }

        public VmTemplate Vm => Issue.Vm;

        public VmValidationField Field => Issue.Field;

        public string VmName => string.IsNullOrWhiteSpace(Issue.Vm.Name) ? "<unnamed VM>" : Issue.Vm.Name;

        public string DisplayText => $"{VmName}: {Issue.Message}";
    }

    public sealed class SaveValidationState
    {
        public SaveValidationState(
            IReadOnlyList<VmValidationIssue> vmIssues,
            IReadOnlyList<MissingVhdxReference> missingReferences,
            TemplateValidationSummary summary)
        {
            VmIssues = vmIssues;
            MissingReferences = missingReferences;
            Summary = summary;
        }

        public IReadOnlyList<VmValidationIssue> VmIssues { get; }

        public IReadOnlyList<MissingVhdxReference> MissingReferences { get; }

        public TemplateValidationSummary Summary { get; }
    }

}

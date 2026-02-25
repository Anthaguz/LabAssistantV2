using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using LabAssistant.Views;
using System.Collections.Specialized;
using System.ComponentModel;

namespace LabAssistant.ViewModels;

public partial class DeploymentViewModel : ObservableObject
{
    private readonly VirtualSwitchProvider _switchProvider;
    private readonly IDeploymentCoordinator _coordinator;
    private readonly IDeploymentPreflightService _preflightService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILabTemplateStore _templateStore;
    private readonly IAppPaths _appPaths;
    private readonly IVhdxCatalogStore _catalogStore;
    private readonly IErrorFeedService _errorFeed;
    private readonly IDeploymentOutcomeSummaryBuilder _outcomeSummaryBuilder;
    private readonly IStructuredLogger _structuredLogger;
    private readonly TimeSpan _quickPreflightDebounce;
    private CancellationTokenSource? _quickPreflightDebounceCts;
    private int _preflightRequestVersion;
    private bool _deployStartPreflightInProgress;
    public Action<string>? LogHandler { get; set; }

    public ObservableCollection<string> AvailableSwitches { get; } = new();

    [ObservableProperty]
    private bool isDeploying;

    [ObservableProperty]
    private ObservableCollection<string> logs = new();

    [ObservableProperty]
    public ObservableCollection<VmLogGroup> logGroups = new();

    [ObservableProperty]
    private DeploymentOperationState operationState = DeploymentOperationState.Idle;

    [ObservableProperty]
    private DeploymentOutcomeSummary? deploymentSummary;

    public bool HasDeploymentSummary => DeploymentSummary != null;

    [ObservableProperty]
    private DeploymentReadinessReport? readinessReport;

    [ObservableProperty]
    private bool isReadinessCheckInProgress;

    [ObservableProperty]
    private string? readinessStatusMessage;

    public bool HasReadinessReport => ReadinessReport != null;
    public bool IsReadinessQuickReport => ReadinessReport?.Mode == DeploymentPreflightMode.Quick;
    public bool IsReadinessFullReport => ReadinessReport?.Mode == DeploymentPreflightMode.Full;
    public bool IsDeployBlockedByReadiness => ReadinessReport?.HasBlockingFailures == true;
    public int ReadinessPassCount => ReadinessReport?.Results.Count(r => r.Status == DeploymentReadinessStatus.Pass) ?? 0;
    public int ReadinessWarnCount => ReadinessReport?.Results.Count(r => r.Status == DeploymentReadinessStatus.Warn) ?? 0;
    public int ReadinessFailCount => ReadinessReport?.Results.Count(r => r.Status == DeploymentReadinessStatus.Fail) ?? 0;

    public ObservableCollection<VmEntryViewModel> VmEntries { get; }
    private MultiVmDeploymentContext? _activeDeploymentContext;
    private bool HasActiveOperationContext => _activeDeploymentContext != null;
    public bool IsConfigurationEditingEnabled => !DeploymentUiInteractivity.HasActiveOperation(IsDeploying, HasActiveOperationContext, OperationState);
    public bool CanCancelDeploymentOperation => DeploymentUiInteractivity.HasActiveOperation(IsDeploying, HasActiveOperationContext, OperationState);
    public bool ShowCancelDeploymentAction => CanCancelDeploymentOperation;
    public bool CanStartDeploymentOperation => !CanCancelDeploymentOperation && !_deployStartPreflightInProgress && !IsDeployBlockedByReadiness;

    public DeploymentViewModel(
        IDeploymentCoordinator coordinator,
        VirtualSwitchProvider switchProvider,
        IDeploymentPreflightService preflightService,
        IAppSettingsStore settingsStore,
        ILabTemplateStore templateStore,
        IAppPaths appPaths,
        IVhdxCatalogStore catalogStore,
        IErrorFeedService errorFeed,
        IDeploymentOutcomeSummaryBuilder outcomeSummaryBuilder,
        IStructuredLogger? structuredLogger = null,
        TimeSpan? quickPreflightDebounce = null)
    {
        LogHandler = message =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(message);
            });
        };

        _switchProvider = switchProvider;
        _coordinator = coordinator;
        _preflightService = preflightService;
        _settingsStore = settingsStore;
        _templateStore = templateStore;
        _appPaths = appPaths;
        _catalogStore = catalogStore;
        _errorFeed = errorFeed;
        _outcomeSummaryBuilder = outcomeSummaryBuilder;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
        _quickPreflightDebounce = quickPreflightDebounce ?? TimeSpan.FromMilliseconds(350);
        VmEntries = new ObservableCollection<VmEntryViewModel>();
        VmEntries.CollectionChanged += HandleVmEntriesCollectionChanged;
        _ = LoadAvailableSwitches();

        DebugLogger.Log("DeploymentViewModel initialized with default VM entry.");
    }

    private async Task LoadAvailableSwitches()
    {
        var switches = await _switchProvider.GetVirtualSwitchesAsync();
        AvailableSwitches.Clear();
        foreach (var s in switches)
        {
            AvailableSwitches.Add(s);
        }

        RequestQuickPreflightRefresh();
    }

    public void AddLog(Guid vmId, string vmName, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var group = LogGroups.FirstOrDefault(g => g.VmId == vmId);
            if (group == null)
            {
                group = new VmLogGroup { VmId = vmId, VmName = vmName };
                LogGroups.Add(group);
            }

            if (group.VmName != vmName)
            {
                group.VmName = vmName;
            }

            group.Entries.Add(message);

            if (IsRuntimeErrorMessage(message))
            {
                var entry = VmEntries.FirstOrDefault(vm => vm.DeploymentContext.VmId == vmId);
                _errorFeed.Publish(
                    vmName,
                    "Deployment error",
                    message,
                    entry != null ? () => OpenVmDetail(entry) : null);
            }
        });
    }

    private static bool IsRuntimeErrorMessage(string message)
    {
        return message.Contains("❌", StringComparison.Ordinal)
               || message.Contains("error", StringComparison.OrdinalIgnoreCase)
               || message.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyDeploymentPolicy(VmDeploymentContext context)
    {
        var settings = _settingsStore.Settings;
        context.PerVmFailFast = settings.PerVmFailFast;
        context.NonBlockingOptionalSteps = new List<string>(settings.NonBlockingOptionalSteps ?? new List<string>());
    }

    private bool ValidateVhdxSelections(string actionLabel)
    {
        var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
        if (catalogResult.Errors.Count > 0)
        {
            var message = "Catalog errors prevent validation:" + Environment.NewLine
                          + string.Join(Environment.NewLine, catalogResult.Errors.Select(error => $"- {error}"));
            System.Windows.MessageBox.Show(
                message,
                actionLabel,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }

        var catalogIds = new HashSet<string>(
            catalogResult.Items.Select(item => item.Id),
            StringComparer.OrdinalIgnoreCase);

        var missing = new List<string>();
        foreach (var entry in VmEntries)
        {
            var context = entry.DeploymentContext;
            var hasBase = !string.IsNullOrWhiteSpace(context.BaseVhdPath);
            var hasId = !string.IsNullOrWhiteSpace(context.VhdxId);

            if (!hasBase && !hasId)
            {
                missing.Add($"{context.VmName}: no base VHDX selected");
                continue;
            }

            if (hasId && !catalogIds.Contains(context.VhdxId!))
            {
                missing.Add($"{context.VmName}: missing VHDX id '{context.VhdxId}'");
            }
        }

        if (missing.Count == 0)
        {
            return true;
        }

        var missingMessage = "Resolve missing VHDX selections before continuing:" + Environment.NewLine
                             + string.Join(Environment.NewLine, missing.Select(item => $"- {item}"));
        System.Windows.MessageBox.Show(
            missingMessage,
            actionLabel,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
        return false;
    }

    [RelayCommand(CanExecute = nameof(IsConfigurationEditingEnabled))]
    private void DeleteVm(VmEntryViewModel vmEntry)
    {
        RemoveVm(vmEntry);
    }

    public void RemoveVm(VmEntryViewModel vmEntry)
    {
        if (VmEntries.Contains(vmEntry))
        {
            VmEntries.Remove(vmEntry);
        }

        var logGroup = LogGroups.FirstOrDefault(g => g.VmId == vmEntry.DeploymentContext.VmId);
        if (logGroup != null)
        {
            LogGroups.Remove(logGroup);
        }

        RequestQuickPreflightRefresh();
    }

    [RelayCommand(CanExecute = nameof(IsConfigurationEditingEnabled))]
    private void AddVm()
    {
        string vmName = $"VM{VmEntries.Count + 1}";
        DebugLogger.Log($"Adding new VM entry. {vmName}");
        Guid VmId = Guid.NewGuid();
        var context = new VmDeploymentContext
        {
            VmId = VmId,
            VmName = vmName,
            BaseVhdPath = string.Empty,
            VirtualSwitchName = AvailableSwitches.FirstOrDefault() ?? "",
            MemoryMb = 2048,
            CpuCount = 2,
            VmPath = $"{_settingsStore.Settings.VmBasePath}\\{vmName}",
            VhdPath = $"{_settingsStore.Settings.VmBasePath}\\{vmName}\\{vmName}.vhdx",
            LogCallback = msg => AddLog(VmId, vmName, msg)
        };
        ApplyDeploymentPolicy(context);
        var vmEntry = new VmEntryViewModel(context, this, _settingsStore);
        VmEntries.Add(vmEntry);
        vmEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vmEntry.VmName))
            {
                var logGroup = LogGroups.FirstOrDefault(g => g.VmId == context.VmId);
                if (logGroup != null)
                {
                    logGroup.VmName = vmEntry.VmName;
                }
            }
        };

        RequestQuickPreflightRefresh();
    }

    [RelayCommand(CanExecute = nameof(IsConfigurationEditingEnabled))]
    private void SaveAsTemplate()
    {
        if (VmEntries.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Add at least one VM before saving as a template.",
                "Save as Template",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        if (!ValidateVhdxSelections("Save as Template"))
        {
            return;
        }

        var owner = (MainWindow)System.Windows.Application.Current.MainWindow;
        var detailsDialog = new TemplateSaveDetailsDialog(null, null, LabTemplate.CurrentSchemaVersion)
        {
            Owner = owner
        };
        if (detailsDialog.ShowDialog() != true)
        {
            return;
        }

        var templateName = detailsDialog.TemplateName;
        var templateDescription = detailsDialog.TemplateDescription;
        var templateVersion = detailsDialog.TemplateVersion;
        var templateId = Guid.NewGuid().ToString("N");

        var operationId = Guid.NewGuid().ToString("N");
        var template = new LabTemplate
        {
            Id = templateId,
            Name = templateName,
            Description = string.IsNullOrWhiteSpace(templateDescription) ? null : templateDescription,
            SchemaVersion = templateVersion,
            TemplateRevision = 1,
            CreatedWithAppVersion = GetAppVersion(),
            TemplateType = LabTemplate.SupportedTemplateType,
            VmTemplates = VmEntries.Select(entry =>
            {
                var context = entry.DeploymentContext;
                var baseVhdPath = !string.IsNullOrWhiteSpace(context.BaseVhdPath)
                    ? context.BaseVhdPath
                    : context.VhdPath;

                return new VmTemplate
                {
                    VmId = context.VmId.ToString("N"),
                    Name = context.VmName,
                    MemoryMb = context.MemoryMb,
                    CpuCount = context.CpuCount,
                    SwitchName = context.VirtualSwitchName,
                    VhdxId = context.VhdxId,
                    VhdPath = baseVhdPath,
                    VhdxSignature = context.VhdxSignature
                };
            }).ToList()
        };

        var reviewDialog = new TemplateSaveReviewDialog(
            new TemplateSaveReviewModel(template.Name, template.Id, template.VmTemplates.Count, template.Description, template.Version))
        {
            Owner = owner
        };

        if (reviewDialog.ShowDialog() != true)
        {
            return;
        }

        var templateFolder = _settingsStore.Settings.TemplateFolder;
        if (string.IsNullOrWhiteSpace(templateFolder))
        {
            templateFolder = _appPaths.TemplatesFolder;
        }

        try
        {
            var filePath = _templateStore.SaveToFolder(templateFolder, template.Name, template);
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "TemplateSaved",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["templateId"] = template.Id,
                    ["templateName"] = template.Name,
                    ["templateVmCount"] = template.VmTemplates.Count,
                    ["resourcePath"] = filePath
                });

            System.Windows.MessageBox.Show(
                $"Template saved to {filePath}",
                "Save as Template",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "TemplateSaved",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["templateId"] = template.Id,
                    ["templateName"] = template.Name,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });
            throw;
        }
    }

    private static string GetAppVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    }


    [RelayCommand(CanExecute = nameof(CanStartDeploymentOperation))]
    private async Task DeployAllAsync()
    {
        DebugLogger.Log("Starting deployment.");
        if (!ValidateVhdxSelections("Deploy"))
        {
            return;
        }

        CancelPendingQuickPreflight();
        _deployStartPreflightInProgress = true;
        RefreshOperationUiState();
        var fullPreflight = await RunPreflightAsync(DeploymentPreflightMode.Full, CancellationToken.None);
        _deployStartPreflightInProgress = false;
        RefreshOperationUiState();

        if (fullPreflight.HasBlockingFailures)
        {
            ReadinessStatusMessage = "Deployment blocked by readiness failures. Review the readiness report and fix blocking issues.";
            Logs.Add("Deployment blocked by readiness failures.");
            return;
        }

        IsDeploying = true;
        DeploymentSummary = null;
        Logs.Clear();
        Logs.Add("Starting VM deployments...");

        foreach (var entry in VmEntries)
        {
            ApplyDeploymentPolicy(entry.DeploymentContext);
            entry.DeploymentContext.ResetForNewOperation();
        }

        var multiContext = new MultiVmDeploymentContext
        {
            OperationId = Guid.NewGuid().ToString("N"),
            VmContexts = VmEntries.Select(vm => vm.DeploymentContext).ToList(),
            StopAllOnAnyVmFailure = _settingsStore.Settings.StopAllOnAnyVmFailure
        };
        _activeDeploymentContext = multiContext;
        RefreshOperationUiState();
        OperationState = multiContext.OperationState;
        multiContext.OperationStateChanged += HandleOperationStateChanged;

        var deploymentFailed = false;
        try
        {
            await _coordinator.DeployAllAsync(multiContext);
        }
        catch (Exception ex)
        {
            deploymentFailed = true;
            _errorFeed.Publish(null, "Deployment failed", ex.Message);
            Logs.Add($"Deployment failed: {ex.Message}");
        }
        finally
        {
            multiContext.OperationStateChanged -= HandleOperationStateChanged;
            OperationState = multiContext.OperationState;
            DeploymentSummary = _outcomeSummaryBuilder.Build(multiContext);
            _activeDeploymentContext = null;
            RefreshOperationUiState();
            IsDeploying = false;
        }

        foreach (var ctx in multiContext.VmContexts)
        {
            foreach (var log in ctx.Logs)
            {
                Logs.Add($"[{ctx.VmName}] {log}");
            }
        }

        if (!deploymentFailed)
        {
            DebugLogger.Log("All deployments completed.");
            Logs.Add("All deployments completed.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelDeploymentOperation))]
    private void CancelDeployment()
    {
        if (_activeDeploymentContext == null || !IsDeploying)
        {
            return;
        }

        _activeDeploymentContext.RequestUserCancellation();
        OperationState = _activeDeploymentContext.OperationState;
        Logs.Add("Cancellation requested. Waiting for a safe boundary...");
    }

    [RelayCommand]
    private void OpenVmDetail(VmEntryViewModel selectedVm)
    {
        selectedVm.StartEditing();
        var mainWindow = (MainWindow)System.Windows.Application.Current.MainWindow;
        mainWindow.MainContentFrame.Navigate(new VmDetailPage(this, selectedVm, () => mainWindow.MainContentFrame.Navigate(new Views.DeployPage())));
    }

    [RelayCommand]
    private void OpenVmOutcomeDetail(VmDeploymentOutcomeSummary outcome)
    {
        var entry = VmEntries.FirstOrDefault(vm => vm.DeploymentContext.VmId == outcome.VmId);
        if (entry != null)
        {
            OpenVmDetail(entry);
        }
    }

    private void HandleOperationStateChanged(object? sender, DeploymentOperationStateChangedEventArgs e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            OperationState = e.State;
        });
    }

    partial void OnIsDeployingChanged(bool value)
    {
        RefreshOperationUiState();
    }

    partial void OnOperationStateChanged(DeploymentOperationState value)
    {
        RefreshOperationUiState();
    }

    private void RefreshOperationUiState()
    {
        OnPropertyChanged(nameof(IsConfigurationEditingEnabled));
        OnPropertyChanged(nameof(CanCancelDeploymentOperation));
        OnPropertyChanged(nameof(ShowCancelDeploymentAction));
        OnPropertyChanged(nameof(CanStartDeploymentOperation));
        OnPropertyChanged(nameof(IsDeployBlockedByReadiness));

        AddVmCommand.NotifyCanExecuteChanged();
        SaveAsTemplateCommand.NotifyCanExecuteChanged();
        DeployAllCommand.NotifyCanExecuteChanged();
        CancelDeploymentCommand.NotifyCanExecuteChanged();
        DeleteVmCommand.NotifyCanExecuteChanged();
    }

    private void HandleVmEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var oldItem in e.OldItems.OfType<VmEntryViewModel>())
            {
                oldItem.PropertyChanged -= HandleVmEntryPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var newItem in e.NewItems.OfType<VmEntryViewModel>())
            {
                newItem.PropertyChanged += HandleVmEntryPropertyChanged;
            }
        }

        DeleteVmCommand.NotifyCanExecuteChanged();
    }

    partial void OnDeploymentSummaryChanged(DeploymentOutcomeSummary? value)
    {
        OnPropertyChanged(nameof(HasDeploymentSummary));
    }

    partial void OnReadinessReportChanged(DeploymentReadinessReport? value)
    {
        OnPropertyChanged(nameof(HasReadinessReport));
        OnPropertyChanged(nameof(IsReadinessQuickReport));
        OnPropertyChanged(nameof(IsReadinessFullReport));
        OnPropertyChanged(nameof(IsDeployBlockedByReadiness));
        OnPropertyChanged(nameof(ReadinessPassCount));
        OnPropertyChanged(nameof(ReadinessWarnCount));
        OnPropertyChanged(nameof(ReadinessFailCount));
        RefreshOperationUiState();
    }

    public void RequestQuickPreflightRefresh()
    {
        if (IsDeploying || _deployStartPreflightInProgress)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _quickPreflightDebounceCts, cts);
        previous?.Cancel();
        previous?.Dispose();
        var requestVersion = Interlocked.Increment(ref _preflightRequestVersion);

        _ = RunQuickPreflightDebouncedAsync(requestVersion, cts.Token);
    }

    private async Task RunQuickPreflightDebouncedAsync(int requestVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_quickPreflightDebounce, cancellationToken).ConfigureAwait(false);
            var report = await RunPreflightAsync(DeploymentPreflightMode.Quick, cancellationToken, updateUiState: false).ConfigureAwait(false);

            if (requestVersion != _preflightRequestVersion || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                ReadinessReport = report;
                ReadinessStatusMessage = report.Results.Count == 0
                    ? "Quick readiness check completed (no issues detected)."
                    : "Quick readiness check updated.";
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => ReadinessStatusMessage = $"Quick readiness check failed: {ex.Message}");
        }
    }

    private async Task<DeploymentReadinessReport> RunPreflightAsync(DeploymentPreflightMode mode, CancellationToken cancellationToken)
        => await RunPreflightAsync(mode, cancellationToken, updateUiState: true).ConfigureAwait(false);

    private async Task<DeploymentReadinessReport> RunPreflightAsync(
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken,
        bool updateUiState)
    {
        if (updateUiState)
        {
            RunOnUiThread(() =>
            {
                IsReadinessCheckInProgress = true;
                ReadinessStatusMessage = mode == DeploymentPreflightMode.Full
                    ? "Running full readiness check..."
                    : "Running quick readiness check...";
            });
        }

        try
        {
            var context = new MultiVmDeploymentContext
            {
                VmContexts = VmEntries.Select(vm => vm.DeploymentContext).ToList(),
                StopAllOnAnyVmFailure = _settingsStore.Settings.StopAllOnAnyVmFailure
            };

            var report = await _preflightService.RunAsync(context, mode, cancellationToken).ConfigureAwait(false);

            if (updateUiState)
            {
                RunOnUiThread(() =>
                {
                    ReadinessReport = report;
                    ReadinessStatusMessage = report.HasBlockingFailures
                        ? $"{mode} readiness check found blocking failures."
                        : report.HasWarnings
                            ? $"{mode} readiness check found warnings."
                            : $"{mode} readiness check passed.";
                });
            }

            return report;
        }
        finally
        {
            if (updateUiState)
            {
                RunOnUiThread(() => IsReadinessCheckInProgress = false);
            }
        }
    }

    private void CancelPendingQuickPreflight()
    {
        var previous = Interlocked.Exchange(ref _quickPreflightDebounceCts, null);
        if (previous == null)
        {
            return;
        }

        previous.Cancel();
        previous.Dispose();
        Interlocked.Increment(ref _preflightRequestVersion);
    }

    private void HandleVmEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(VmEntryViewModel.VmName)
            or nameof(VmEntryViewModel.VhdPath)
            or nameof(VmEntryViewModel.SelectedSwitchName))
        {
            RequestQuickPreflightRefresh();
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}

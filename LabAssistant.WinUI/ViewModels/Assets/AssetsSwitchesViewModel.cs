using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Infrastructure;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Assets;

public partial class AssetsSwitchesViewModel : ViewModelBase
{
    private readonly IAssetsSwitchesCapabilityService _capabilityService;
    private readonly IReadOnlyList<string> _switchTypes = ["External", "Internal", "Private"];
    private bool _hasLoaded;
    private bool _isSaving;
    private bool _isDeleting;
    private bool _isDraftActive;
    private bool _isUpdatingEditor;
    private int _validationRequestVersion;
    private int _attachedVmRequestVersion;

    public AssetsSwitchesViewModel(IAssetsSwitchesCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
        StatusMessage = "Select a virtual switch or click New Switch to begin.";
        SelectedType = "External";
    }

    public Func<SwitchListItem, AssetsSwitchDeleteAssessment, Task<bool>>? ConfirmDeleteAsync { get; set; }

    public IReadOnlyList<string> SwitchTypes => _switchTypes;

    [ObservableProperty]
    private ObservableCollection<SwitchListItem> _switches = [];

    [ObservableProperty]
    private SwitchListItem? _selectedSwitch;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _switchName = string.Empty;

    [ObservableProperty]
    private string _selectedType = "External";

    [ObservableProperty]
    private string _adapterName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _attachedVmNames = [];

    [ObservableProperty]
    private string _selectedSwitchValidationText = "Select a switch or click New Switch to begin.";

    [ObservableProperty]
    private string _deleteConstraintText = string.Empty;

    [ObservableProperty]
    private string _attachedVmHintText = "No attached VMs.";

    public bool CanRefresh => !IsBusy;

    public bool CanCreateSwitch => !IsBusy;

    public bool CanSaveChanges => !IsBusy;

    public bool CanDeleteSwitch => !IsBusy && SelectedSwitch is not null;

    public bool CanEditSwitchType => !IsEditingExisting && !IsBusy;

    public bool CanEditAdapter => !IsEditingExisting && !IsBusy && string.Equals(SelectedType, "External", StringComparison.OrdinalIgnoreCase);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasDeleteConstraint => !string.IsNullOrWhiteSpace(DeleteConstraintText);

    public bool IsBusy => IsLoading || _isSaving || _isDeleting;

    public bool IsEditingExisting => SelectedSwitch is not null && !_isDraftActive;

    public string LoadingStateText => IsLoading
        ? "Loading current Hyper-V virtual switches. Current details remain visible until refresh completes."
        : "Virtual switch inventory is idle.";

    public string EmptyStateText => "No virtual switches were found on this host. Click New Switch to prepare a new switch.";

    public string ErrorStateText => string.IsNullOrWhiteSpace(ErrorMessage)
        ? "No switch load or action errors."
        : ErrorMessage!;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !IsLoading && IsEmpty && !HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DeleteConstraintVisibility => string.IsNullOrWhiteSpace(DeleteConstraintText) ? Visibility.Collapsed : Visibility.Visible;

    public override async Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        await EnsureInventoryAsync(forceRefresh: false, cancellationToken);
        IsInitialized = true;
    }

    public override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        _hasLoaded = false;
        _isSaving = false;
        _isDeleting = false;
        _isDraftActive = false;
        _isUpdatingEditor = false;
        _validationRequestVersion++;
        _attachedVmRequestVersion++;
        IsLoading = false;
        Switches.Clear();
        AttachedVmNames.Clear();
        SetSelectedSwitch(null);
        ClearEditor();
        IsEmpty = true;
        SetError(null);
        StatusMessage = "Select a virtual switch or click New Switch to begin.";
        IsInitialized = false;
    }

    public Task EnsureInventoryAsync(bool forceRefresh)
    {
        return EnsureInventoryAsync(forceRefresh, LifecycleToken);
    }

    private async Task EnsureInventoryAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (IsLoading || (!forceRefresh && (_hasLoaded || _isDraftActive)))
        {
            NotifyStateChanged();
            return;
        }

        await LoadInventoryAsync(forceRefresh, cancellationToken);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInventoryAsync(forceRefresh: true, LifecycleToken);
    }

    [RelayCommand]
    private async Task CreateSwitchAsync()
    {
        _isDraftActive = true;
        SetSelectedSwitch(null);
        SetEditorFields(string.Empty, "External", string.Empty);
        SetError(null);
        SelectedSwitchValidationText = "Enter a switch name, choose a type, and provide an adapter for External switches.";
        DeleteConstraintText = string.Empty;
        SetAttachedVmState(Array.Empty<string>(), "Attached VMs are shown for existing switches.");
        StatusMessage = "Preparing a new virtual switch draft. Complete the fields and apply when the validation state is ready.";
        await RefreshValidationAsync(LifecycleToken);
    }

    [RelayCommand]
    private async Task DeleteSwitchAsync()
    {
        if (SelectedSwitch is null)
        {
            StatusMessage = "Select a virtual switch to delete.";
            NotifyStateChanged();
            return;
        }

        _isDeleting = true;
        NotifyStateChanged();
        try
        {
            var cancellationToken = LifecycleToken;
            var assessment = await _capabilityService.AssessDeleteAsync(SelectedSwitch.Name, GetKnownInventorySnapshot(), cancellationToken);
            ApplyDeleteAssessment(assessment);
            if (!assessment.CanDelete)
            {
                SetError($"Delete is blocked for '{SelectedSwitch.Name}'.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", assessment.BlockingReasons.DefaultIfEmpty("At least one VM is attached to this switch."))}");
                StatusMessage = "Delete blocked. Disconnect the attached VMs from this switch and refresh before trying again.";
                return;
            }

            if (ConfirmDeleteAsync is not null && !await ConfirmDeleteAsync(SelectedSwitch, assessment))
            {
                StatusMessage = "Virtual switch delete canceled.";
                return;
            }

            var result = await _capabilityService.DeleteAsync(SelectedSwitch.Name, assessment, cancellationToken);
            StatusMessage = result.UserMessage;
            if (!result.Success)
            {
                SetError($"Delete failed. Review the details below before trying again.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors.DefaultIfEmpty(result.UserMessage))}");
                return;
            }

            _isDraftActive = false;
            SetError(null);
            await LoadInventoryAsync(forceRefresh: true, cancellationToken);
        }
        finally
        {
            _isDeleting = false;
            NotifyStateChanged();
        }
    }

    [RelayCommand]
    private Task SaveChangesAsync()
    {
        return SaveChangesCoreAsync(LifecycleToken);
    }

    partial void OnSelectedSwitchChanged(SwitchListItem? value)
    {
        _ = HandleSelectionChangedAsync(value, LifecycleToken);
    }

    partial void OnIsEmptyChanged(bool value) => NotifyStateChanged();

    partial void OnSwitchNameChanged(string value) => HandleEditorChanged();

    partial void OnSelectedTypeChanged(string value)
    {
        if (!_isUpdatingEditor && !string.Equals(value, "External", StringComparison.OrdinalIgnoreCase))
        {
            AdapterName = string.Empty;
        }

        HandleEditorChanged();
    }

    partial void OnAdapterNameChanged(string value) => HandleEditorChanged();

    private async Task LoadInventoryAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        SetLoading(true);
        StatusMessage = forceRefresh ? "Refreshing virtual switches..." : "Loading virtual switches...";
        try
        {
            var previousSelectionName = SelectedSwitch?.Name;
            var result = await _capabilityService.LoadAsync(forceRefresh, cancellationToken);
            _hasLoaded = true;

            Switches.Clear();
            foreach (var item in result.Items)
            {
                Switches.Add(new SwitchListItem(item));
            }

            IsEmpty = Switches.Count == 0;
            SetError(result.Errors.Count > 0
                ? $"Switch inventory load completed with issues:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors)}"
                : null);

            if (_isDraftActive)
            {
                StatusMessage = forceRefresh
                    ? "Virtual switch inventory refreshed. The current new-switch draft was preserved."
                    : "Virtual switch inventory loaded. The current new-switch draft was preserved.";
                return;
            }

            var selected = !string.IsNullOrWhiteSpace(previousSelectionName)
                ? Switches.FirstOrDefault(row => string.Equals(row.Name, previousSelectionName, StringComparison.OrdinalIgnoreCase))
                : null;
            SetSelectedSwitch(selected ?? Switches.FirstOrDefault());
            StatusMessage = result.Errors.Count > 0
                ? $"Loaded {Switches.Count} switch(es) with {result.Errors.Count} issue(s). Review the error panel and refresh after correcting the host state."
                : $"Loaded {Switches.Count} switch(es).";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = forceRefresh ? "Virtual switch refresh canceled." : "Virtual switch load canceled.";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = "Unable to load virtual switches.";
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task HandleSelectionChangedAsync(SwitchListItem? selectedSwitch, CancellationToken cancellationToken = default)
    {
        try
        {
            if (selectedSwitch is not null)
            {
                _isDraftActive = false;
                SetError(null);
                SetEditorFields(selectedSwitch.Name, selectedSwitch.Type, selectedSwitch.AdapterName ?? string.Empty);
                SelectedSwitchValidationText = selectedSwitch.Status;
                DeleteConstraintText = string.Empty;
                SetAttachedVmState(Array.Empty<string>(), "Loading attached VMs...");
                await LoadAttachedVmNamesAsync(selectedSwitch.Name, cancellationToken);
                await RefreshValidationAsync(cancellationToken);
                StatusMessage = "Selected virtual switch details are ready.";
            }
            else if (!_isDraftActive)
            {
                ClearEditor();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        NotifyStateChanged();
    }

    private async Task SaveChangesCoreAsync(CancellationToken cancellationToken = default)
    {
        var draft = CaptureDraft(SelectedSwitch is null);
        _isSaving = true;
        StatusMessage = draft.IsNew ? "Creating virtual switch..." : "Updating virtual switch...";
        NotifyStateChanged();
        try
        {
            var result = await _capabilityService.SaveAsync(draft, cancellationToken);
            StatusMessage = result.UserMessage;
            if (!result.Success)
            {
                SetError($"Switch save failed. Review the message below, correct the configuration, and try Save Changes again.{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors.DefaultIfEmpty(result.UserMessage))}");
                return;
            }

            _isDraftActive = false;
            SetError(null);
            await LoadInventoryAsync(forceRefresh: true, cancellationToken);
            if (result.Item is not null)
            {
                var selected = Switches.FirstOrDefault(row => string.Equals(row.Name, result.Item.Name, StringComparison.OrdinalIgnoreCase));
                SetSelectedSwitch(selected);
            }
        }
        finally
        {
            _isSaving = false;
            NotifyStateChanged();
        }
    }

    private async Task RefreshValidationAsync(CancellationToken cancellationToken = default)
    {
        var requestVersion = ++_validationRequestVersion;
        var draft = CaptureDraft(SelectedSwitch is null);
        var validation = await _capabilityService.ValidateAsync(draft, GetKnownInventorySnapshot(), cancellationToken);
        if (requestVersion != _validationRequestVersion)
        {
            return;
        }

        var validationText = FormatValidationText(validation);
        SelectedSwitchValidationText = validationText;
        if (SelectedSwitch is not null)
        {
            SelectedSwitch.Status = validationText;
        }
    }

    private async Task LoadAttachedVmNamesAsync(string switchName, CancellationToken cancellationToken = default)
    {
        var requestVersion = ++_attachedVmRequestVersion;
        var vmNames = await _capabilityService.GetAttachedVmNamesAsync(switchName, cancellationToken);
        if (requestVersion != _attachedVmRequestVersion || SelectedSwitch is null || !string.Equals(SelectedSwitch.Name, switchName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetAttachedVmState(
            vmNames,
            vmNames.Count > 0
                ? "Attached VMs currently using this switch."
                : "No attached VMs.");
        SelectedSwitch.AttachedVmCount = vmNames.Count;
    }

    private void ApplyDeleteAssessment(AssetsSwitchDeleteAssessment assessment)
    {
        SetAttachedVmState(
            assessment.AttachedVmNames,
            assessment.AttachedVmNames.Count > 0
                ? "Attached VMs currently using this switch."
                : "No attached VMs.");
        DeleteConstraintText = assessment.CanDelete ? string.Empty : $"Delete blocked. {assessment.Summary}";
        if (SelectedSwitch is not null)
        {
            SelectedSwitch.DeleteSummary = DeleteConstraintText;
            SelectedSwitch.AttachedVmCount = assessment.AttachedVmNames.Count;
        }
    }

    private void HandleEditorChanged()
    {
        if (_isUpdatingEditor)
        {
            return;
        }

        if (SelectedSwitch is null)
        {
            _isDraftActive = true;
            StatusMessage = "Switch draft changed. Review validation, then Save Changes to create the switch.";
        }
        else
        {
            StatusMessage = "Switch configuration changed. Review validation, then Save Changes to apply the update.";
        }

        DeleteConstraintText = string.Empty;
        _ = RefreshValidationAsync(LifecycleToken);
        NotifyStateChanged();
    }

    private AssetsSwitchDraft CaptureDraft(bool isNewOverride)
    {
        return new AssetsSwitchDraft
        {
            IsNew = isNewOverride,
            OriginalName = isNewOverride ? null : SelectedSwitch?.Name,
            Name = SwitchName.Trim(),
            SwitchType = SelectedType,
            AdapterName = string.IsNullOrWhiteSpace(AdapterName) ? null : AdapterName.Trim()
        };
    }

    private IReadOnlyList<AssetsSwitchRecord> GetKnownInventorySnapshot()
    {
        return Switches
            .Select(row => new AssetsSwitchRecord
            {
                Name = row.Name,
                SwitchType = row.Type,
                AdapterName = row.AdapterName
            })
            .ToList();
    }

    private void SetAttachedVmState(IEnumerable<string> vmNames, string hintText)
    {
        AttachedVmNames.Clear();
        foreach (var vmName in vmNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            AttachedVmNames.Add(vmName);
        }

        AttachedVmHintText = hintText;
    }

    private void SetEditorFields(string name, string selectedType, string adapterName)
    {
        _isUpdatingEditor = true;
        try
        {
            SwitchName = name;
            SelectedType = selectedType;
            AdapterName = adapterName;
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    private void ClearEditor()
    {
        SetEditorFields(string.Empty, "External", string.Empty);
        SelectedSwitchValidationText = "Select a switch or click New Switch to begin.";
        DeleteConstraintText = string.Empty;
        SetAttachedVmState(Array.Empty<string>(), "No attached VMs.");
    }

    private void SetSelectedSwitch(SwitchListItem? selectedSwitch)
    {
        SelectedSwitch = selectedSwitch;
        if (selectedSwitch is null && !_isDraftActive)
        {
            ClearEditor();
        }
    }

    private void SetLoading(bool isLoading)
    {
        IsLoading = isLoading;
        NotifyStateChanged();
    }

    private void SetError(string? errorMessage)
    {
        ErrorMessage = errorMessage;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanCreateSwitch));
        OnPropertyChanged(nameof(CanSaveChanges));
        OnPropertyChanged(nameof(CanDeleteSwitch));
        OnPropertyChanged(nameof(CanEditSwitchType));
        OnPropertyChanged(nameof(CanEditAdapter));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasDeleteConstraint));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(IsEditingExisting));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(DeleteConstraintVisibility));
        OnPropertyChanged(nameof(LoadingStateText));
        OnPropertyChanged(nameof(ErrorStateText));
    }

    private static string FormatValidationText(AssetsSwitchValidationResult validation)
    {
        var heading = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? $"Ready: {validation.Summary}"
            : string.Equals(validation.Severity, "Warn", StringComparison.OrdinalIgnoreCase)
                ? $"Warning: {validation.Summary}"
                : $"Blocking: {validation.Summary}";
        if (validation.Details.Count == 0 || string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase))
        {
            return heading;
        }

        return string.Join(Environment.NewLine, new[] { heading }.Concat(validation.Details.Select(detail => "- " + detail)));
    }
}

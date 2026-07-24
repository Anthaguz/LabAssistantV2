using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Infrastructure;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Assets;

public partial class AssetsBaseDisksViewModel : ViewModelBase
{
    private readonly IAssetsBaseDisksCapabilityService _capabilityService;
    private bool _hasLoaded;
    private bool _isSaving;
    private bool _isRemoving;
    private bool _isDraftActive;
    private bool _isUpdatingEditor;

    public AssetsBaseDisksViewModel(IAssetsBaseDisksCapabilityService capabilityService)
    {
        _capabilityService = capabilityService;
        StatusMessage = "Select a base disk or import a VHDX to begin.";
    }

    public Func<string?>? PickBaseDiskFilePath { get; set; }

    public Func<BaseDiskListItem, AssetsBaseDiskRemovalAssessment, Task<bool>>? ConfirmRemoveAsync { get; set; }

    [ObservableProperty]
    private ObservableCollection<BaseDiskListItem> _baseDisks = [];

    [ObservableProperty]
    private BaseDiskListItem? _selectedDisk;

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _osName = string.Empty;

    [ObservableProperty]
    private string _osVersion = string.Empty;

    [ObservableProperty]
    private string _diskPath = string.Empty;

    [ObservableProperty]
    private string _generationText = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _selectedDiskSummaryText = "Select a base disk or import a VHDX to begin.";

    [ObservableProperty]
    private string _selectedDiskValidationText = "Validation has not been evaluated.";

    [ObservableProperty]
    private string _referenceWarningText = "No removal assessment has been performed.";

    public bool CanRefresh => !IsBusy;

    public bool CanAddDisk => !IsBusy;

    public bool CanValidate => !IsBusy && CaptureDraft() is not null;

    public bool CanSaveMetadata => !IsBusy && CaptureDraft() is not null;

    public bool CanRemove => !IsLoading && !_isSaving && !_isRemoving && SelectedDisk is not null;

    public bool CanBrowsePath => !IsBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasDetails => SelectedDisk is not null || _isDraftActive;

    public bool IsBusy => IsLoading || _isSaving || _isRemoving;

    public string LoadingStateText => IsLoading
        ? "Loading base disk catalog. Current details remain visible until refresh completes."
        : "Base disk catalog is idle.";

    public string EmptyStateText => "No base disks are registered. Use Import Disk to choose a VHDX and then save its metadata.";

    public string ErrorStateText => string.IsNullOrWhiteSpace(ErrorMessage)
        ? "No catalog load, validation, save, or remove errors."
        : ErrorMessage!;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyVisibility => !IsLoading && IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DetailsVisibility => HasDetails ? Visibility.Visible : Visibility.Collapsed;

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
        _isRemoving = false;
        _isDraftActive = false;
        _isUpdatingEditor = false;
        IsLoading = false;
        BaseDisks.Clear();
        SetSelectedDisk(null);
        ClearEditorFields();
        IsEmpty = true;
        SetError(null);
        StatusMessage = "Select a base disk or import a VHDX to begin.";
        SelectedDiskSummaryText = "Select a base disk or import a VHDX to begin.";
        SelectedDiskValidationText = "Validation has not been evaluated.";
        ReferenceWarningText = "No removal assessment has been performed.";
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
    private Task AddDiskAsync()
    {
        var selectedPath = PickBaseDiskFilePath?.Invoke();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return Task.CompletedTask;
        }

        _isDraftActive = true;
        SetSelectedDisk(null);
        SetEditorFields(
            System.IO.Path.GetFileNameWithoutExtension(selectedPath),
            string.Empty,
            selectedPath,
            "1",
            string.Empty);
        SetError(null);
        SelectedDiskSummaryText = "New base disk draft. Review metadata, validate, then save to register it.";
        SelectedDiskValidationText = "Validation has not been evaluated for this draft yet.";
        ReferenceWarningText = "New base disk draft. Removal assessment is not applicable.";
        StatusMessage = "Selected VHDX path. Review metadata, validate, and click Save Metadata to register the base disk.";
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RemoveDiskAsync()
    {
        if (SelectedDisk is null)
        {
            StatusMessage = "Select a base disk to remove.";
            NotifyStateChanged();
            return;
        }

        _isRemoving = true;
        NotifyStateChanged();
        try
        {
            var cancellationToken = LifecycleToken;
            var assessment = await _capabilityService.AssessRemoveAsync(SelectedDisk.Id, cancellationToken);
            SelectedDisk.ReferenceSummary = assessment.ReferenceSignalSummary;
            ReferenceWarningText = string.Join(Environment.NewLine, assessment.WarningReasons.DefaultIfEmpty(assessment.ReferenceSignalSummary));

            if (!assessment.Exists || !assessment.CanRemove)
            {
                SetError(string.Join(Environment.NewLine, assessment.BlockingReasons.DefaultIfEmpty("Base disk removal is blocked.")));
                StatusMessage = assessment.BlockingReasons.FirstOrDefault() ?? "Base disk removal is blocked. Resolve the issue and try again.";
                return;
            }

            if (ConfirmRemoveAsync is not null && !await ConfirmRemoveAsync(SelectedDisk, assessment))
            {
                StatusMessage = "Base disk removal canceled.";
                return;
            }

            var result = await _capabilityService.RemoveAsync(SelectedDisk.Id, cancellationToken);
            StatusMessage = result.UserMessage;
            if (!result.Success)
            {
                SetError($"Remove failed. {string.Join(Environment.NewLine, result.Errors.DefaultIfEmpty(result.UserMessage))}");
                return;
            }

            _isDraftActive = false;
            SetError(null);
            await LoadInventoryAsync(forceRefresh: true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Lifecycle cancellation on navigate-away; nothing to surface to the user.
        }
        catch (Exception ex)
        {
            SetError($"Remove failed. {ex.Message}");
            StatusMessage = "Base disk removal failed unexpectedly. See the error details.";
        }
        finally
        {
            _isRemoving = false;
            NotifyStateChanged();
        }
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        var draft = CaptureDraft();
        if (draft is null)
        {
            StatusMessage = "Select or prepare a base disk draft before validating.";
            NotifyStateChanged();
            return;
        }

        try
        {
            var validation = await _capabilityService.ValidateAsync(draft, LifecycleToken);
            ApplyValidation(validation);
            StatusMessage = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
                ? "Validation passed. The base disk is ready to use."
                : "Validation blocked. Review the details and correct the metadata or path before saving.";
        }
        catch (OperationCanceledException)
        {
            // Lifecycle cancellation on navigate-away; nothing to surface to the user.
        }
        catch (Exception ex)
        {
            SetError($"Validation failed. {ex.Message}");
            StatusMessage = "Validation failed unexpectedly. See the error details.";
        }

        NotifyStateChanged();
    }

    [RelayCommand]
    private Task SaveMetadataAsync()
    {
        return SaveMetadataCoreAsync(LifecycleToken);
    }

    [RelayCommand]
    private Task BrowsePathAsync()
    {
        var selectedPath = PickBaseDiskFilePath?.Invoke();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return Task.CompletedTask;
        }

        DiskPath = selectedPath;
        if (SelectedDisk is null)
        {
            _isDraftActive = true;
        }

        StatusMessage = "Updated base disk path. Validate and save metadata to persist the change.";
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    partial void OnSelectedDiskChanged(BaseDiskListItem? value)
    {
        if (value is not null)
        {
            _isDraftActive = false;
            SetEditorFields(
                value.OsName,
                value.OsVersion,
                value.Path,
                value.Generation.ToString(),
                value.Notes ?? string.Empty);
            SelectedDiskSummaryText = $"Catalog id: {value.Id}{Environment.NewLine}{value.Path}";
            SelectedDiskValidationText = value.Status;
            ReferenceWarningText = value.ReferenceSummary;
        }
        else if (!_isDraftActive)
        {
            ClearEditorFields();
            SelectedDiskSummaryText = "Select a base disk or import a VHDX to begin.";
            SelectedDiskValidationText = "Validation has not been evaluated.";
            ReferenceWarningText = "No removal assessment has been performed.";
        }

        NotifyStateChanged();
    }

    partial void OnIsEmptyChanged(bool value) => NotifyStateChanged();

    partial void OnOsNameChanged(string value) => HandleEditorChanged();

    partial void OnOsVersionChanged(string value) => HandleEditorChanged();

    partial void OnDiskPathChanged(string value) => HandleEditorChanged();

    partial void OnGenerationTextChanged(string value) => HandleEditorChanged();

    partial void OnNotesChanged(string value) => HandleEditorChanged();

    private async Task LoadInventoryAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        if (IsLoading)
        {
            return;
        }

        SetLoading(true);
        StatusMessage = forceRefresh ? "Refreshing base disks..." : "Loading base disks...";
        try
        {
            var previousSelectionId = SelectedDisk?.Id;
            var result = await _capabilityService.LoadAsync(forceRefresh, cancellationToken);
            _hasLoaded = true;

            BaseDisks.Clear();
            foreach (var item in result.Items)
            {
                BaseDisks.Add(new BaseDiskListItem(item));
            }

            IsEmpty = BaseDisks.Count == 0;
            SetError(result.Errors.Count > 0
                ? $"Catalog load completed with issues:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", result.Errors)}"
                : null);

            if (_isDraftActive)
            {
                StatusMessage = forceRefresh
                    ? "Base disk inventory refreshed. The current draft was preserved."
                    : "Base disk inventory loaded. The current draft was preserved.";
                return;
            }

            var selected = BaseDisks.FirstOrDefault(row => string.Equals(row.Id, previousSelectionId, StringComparison.OrdinalIgnoreCase))
                ?? BaseDisks.FirstOrDefault();
            SetSelectedDisk(selected);
            StatusMessage = result.Errors.Count > 0
                ? $"Loaded {BaseDisks.Count} base disk(s) with {result.Errors.Count} issue(s). Review the error panel and refresh after correcting the catalog."
                : $"Loaded {BaseDisks.Count} base disk(s).";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = forceRefresh ? "Base disk refresh canceled." : "Base disk load canceled.";
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            StatusMessage = "Unable to load the base disk catalog.";
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task SaveMetadataCoreAsync(CancellationToken cancellationToken = default)
    {
        var draft = CaptureDraft();
        if (draft is null)
        {
            StatusMessage = "Complete required metadata before saving.";
            NotifyStateChanged();
            return;
        }

        _isSaving = true;
        StatusMessage = draft.IsNew ? "Registering base disk..." : "Saving base disk metadata...";
        NotifyStateChanged();
        try
        {
            var result = await _capabilityService.SaveAsync(draft, cancellationToken);
            StatusMessage = result.UserMessage;
            if (!result.Success)
            {
                SetError($"Save failed. {string.Join(Environment.NewLine, result.Errors.DefaultIfEmpty(result.UserMessage))}");
                return;
            }

            _isDraftActive = false;
            SetError(null);
            await LoadInventoryAsync(forceRefresh: true, cancellationToken);
            if (result.Item is not null)
            {
                var savedItem = BaseDisks.FirstOrDefault(row => string.Equals(row.Id, result.Item.Id, StringComparison.OrdinalIgnoreCase));
                SetSelectedDisk(savedItem);
            }

            var validation = await _capabilityService.ValidateAsync(new AssetsBaseDiskDraft
            {
                Id = result.Item?.Id ?? draft.Id,
                Path = draft.Path,
                OsName = draft.OsName,
                OsVersion = draft.OsVersion,
                Generation = draft.Generation,
                Notes = draft.Notes,
                IsNew = false
            }, cancellationToken);
            ApplyValidation(validation);
        }
        catch (OperationCanceledException)
        {
            // Lifecycle cancellation on navigate-away; nothing to surface to the user.
        }
        catch (Exception ex)
        {
            SetError($"Save failed. {ex.Message}");
            StatusMessage = "Base disk metadata save failed unexpectedly. See the error details.";
        }
        finally
        {
            _isSaving = false;
            NotifyStateChanged();
        }
    }

    private AssetsBaseDiskDraft? CaptureDraft()
    {
        if (!int.TryParse(GenerationText.Trim(), out var generation) || generation <= 0)
        {
            return null;
        }

        var normalizedPath = DiskPath.Trim();
        var normalizedOsName = OsName.Trim();
        var normalizedOsVersion = OsVersion.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPath) || string.IsNullOrWhiteSpace(normalizedOsName) || string.IsNullOrWhiteSpace(normalizedOsVersion))
        {
            return null;
        }

        return new AssetsBaseDiskDraft
        {
            Id = SelectedDisk?.Id,
            Path = normalizedPath,
            OsName = normalizedOsName,
            OsVersion = normalizedOsVersion,
            Generation = generation,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
            IsNew = SelectedDisk is null
        };
    }

    private void ApplyValidation(AssetsBaseDiskValidationResult validation)
    {
        var validationText = FormatValidationText(validation);
        SelectedDiskValidationText = validationText;
        if (SelectedDisk is not null)
        {
            SelectedDisk.Status = validationText;
        }
    }

    private void HandleEditorChanged()
    {
        if (_isUpdatingEditor)
        {
            return;
        }

        if (SelectedDisk is null)
        {
            _isDraftActive = true;
            StatusMessage = "Base disk draft changed. Validate and Save Metadata to register it.";
        }
        else
        {
            StatusMessage = "Base disk metadata changed. Validate and Save Metadata to persist changes.";
        }

        NotifyStateChanged();
    }

    private void SetEditorFields(string osName, string osVersion, string path, string generationText, string notes)
    {
        _isUpdatingEditor = true;
        try
        {
            OsName = osName;
            OsVersion = osVersion;
            DiskPath = path;
            GenerationText = generationText;
            Notes = notes;
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    private void ClearEditorFields()
    {
        SetEditorFields(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private void SetSelectedDisk(BaseDiskListItem? selectedDisk)
    {
        SelectedDisk = selectedDisk;
        if (selectedDisk is null && !_isDraftActive)
        {
            ClearEditorFields();
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
        OnPropertyChanged(nameof(CanAddDisk));
        OnPropertyChanged(nameof(CanValidate));
        OnPropertyChanged(nameof(CanSaveMetadata));
        OnPropertyChanged(nameof(CanRemove));
        OnPropertyChanged(nameof(CanBrowsePath));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasDetails));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyVisibility));
        OnPropertyChanged(nameof(ErrorVisibility));
        OnPropertyChanged(nameof(DetailsVisibility));
        OnPropertyChanged(nameof(LoadingStateText));
        OnPropertyChanged(nameof(ErrorStateText));
    }

    private static string FormatValidationText(AssetsBaseDiskValidationResult validation)
    {
        var label = string.Equals(validation.Severity, "Pass", StringComparison.OrdinalIgnoreCase)
            ? "Ready"
            : string.Equals(validation.Severity, "Warn", StringComparison.OrdinalIgnoreCase)
                ? "Warning"
                : "Blocking";
        if (validation.Details.Count == 0)
        {
            return $"{label}: {validation.Summary}";
        }

        return $"{label}: {validation.Summary}{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", validation.Details)}";
    }
}

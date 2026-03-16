using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsSwitchesView : UserControl
{
    private const double CompactLayoutThreshold = 1040;
    private bool _isUpdatingSelection;
    private bool _isUpdatingEditor;
    private readonly AssetsSwitchesViewPresentationModel _presentation = new();

    public event EventHandler? RefreshRequested;
    public event EventHandler? CreateRequested;
    public event EventHandler? ApplyRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? SelectedSwitchChanged;
    public event EventHandler? EditorChanged;

    public AssetsSwitchesView()
    {
        InitializeComponent();
        DataContext = _presentation;
        SizeChanged += AssetsSwitchesView_SizeChanged;
        WireHandlers();
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    public AssetsSwitchListRow? SelectedSwitch => AssetsSwitchesListView.SelectedItem as AssetsSwitchListRow;

    public void SetInventorySource(object? itemsSource)
    {
        AssetsSwitchesListView.ItemsSource = itemsSource;
    }

    public void SetAttachedVmSource(object? itemsSource)
    {
        AssetsSwitchesAttachedVmsListView.ItemsSource = itemsSource;
    }

    public void SetSelectedSwitch(AssetsSwitchListRow? selectedSwitch)
    {
        _isUpdatingSelection = true;
        try
        {
            AssetsSwitchesListView.SelectedItem = selectedSwitch;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    public AssetsSwitchesFormValues CaptureFormValues()
    {
        return new AssetsSwitchesFormValues(
            AssetsSwitchesNameTextBox.Text,
            GetSelectedSwitchType(),
            AssetsSwitchesAdapterTextBox.Text);
    }

    public string GetSelectedSwitchType()
    {
        return AssetsSwitchesTypeComboBox.SelectedItem is ComboBoxItem item
            ? item.Content?.ToString() ?? string.Empty
            : string.Empty;
    }

    public void ApplyEditorDraft(AssetsSwitchDraft draft)
    {
        _isUpdatingEditor = true;
        try
        {
            AssetsSwitchesNameTextBox.Text = draft.Name;
            SetSwitchTypeSelection(draft.SwitchType);
            AssetsSwitchesAdapterTextBox.Text = draft.AdapterName ?? string.Empty;
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    public void ClearEditor()
    {
        _isUpdatingEditor = true;
        try
        {
            AssetsSwitchesNameTextBox.Text = string.Empty;
            SetSwitchTypeSelection(string.Empty);
            AssetsSwitchesAdapterTextBox.Text = string.Empty;
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    public void UpdateWorkspaceState(AssetsSwitchesViewState state)
    {
        _presentation.Apply(state);
    }

    private void AssetsSwitchesView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        AssetsSwitchesListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        AssetsSwitchesDetailsColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.4, GridUnitType.Star);
        AssetsSwitchesPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        AssetsSwitchesStateRowDefinition.Height = GridLength.Auto;
        AssetsSwitchesDetailsRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(AssetsSwitchesListRegion, 0);
        Grid.SetColumn(AssetsSwitchesListRegion, 0);

        Grid.SetRow(AssetsSwitchesDetailsRegion, useStackedLayout ? 2 : 0);
        Grid.SetColumn(AssetsSwitchesDetailsRegion, useStackedLayout ? 0 : 1);

        Grid.SetRow(AssetsSwitchesStateRegion, 1);
        Grid.SetColumn(AssetsSwitchesStateRegion, 0);
        Grid.SetColumnSpan(AssetsSwitchesStateRegion, useStackedLayout ? 1 : 2);
    }

    private void WireHandlers()
    {
        AssetsSwitchesRefreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        AssetsSwitchesCreateButton.Click += (_, _) => CreateRequested?.Invoke(this, EventArgs.Empty);
        AssetsSwitchesApplyButton.Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);
        AssetsSwitchesDeleteButton.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        AssetsSwitchesListView.SelectionChanged += AssetsSwitchesListView_SelectionChanged;
        AssetsSwitchesNameTextBox.TextChanged += AssetsSwitchesEditorInput_Changed;
        AssetsSwitchesTypeComboBox.SelectionChanged += AssetsSwitchesEditorInput_Changed;
        AssetsSwitchesAdapterTextBox.TextChanged += AssetsSwitchesEditorInput_Changed;
    }

    private void AssetsSwitchesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        SelectedSwitchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AssetsSwitchesEditorInput_Changed(object sender, object e)
    {
        if (_isUpdatingEditor)
        {
            return;
        }

        EditorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetSwitchTypeSelection(string switchType)
    {
        var match = AssetsSwitchesTypeComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), switchType, StringComparison.OrdinalIgnoreCase));
        AssetsSwitchesTypeComboBox.SelectedItem = match;
    }
}

public sealed record AssetsSwitchesFormValues(
    string NameText,
    string SelectedSwitchType,
    string AdapterNameText);

public sealed record AssetsSwitchesViewState(
    bool CanRefresh,
    bool CanCreate,
    bool CanApply,
    bool CanDelete,
    bool CanEditSwitchType,
    bool CanEditAdapter,
    bool IsLoadingVisible,
    bool IsEmptyVisible,
    bool IsErrorVisible,
    string StatusText,
    string SelectedSwitchValidationText,
    string DeleteConstraintText,
    string AttachedVmHintText,
    string ErrorStateText,
    string LoadingStateText,
    string EmptyStateText);

internal sealed class AssetsSwitchesViewPresentationModel : INotifyPropertyChanged
{
    private bool _canRefresh;
    private bool _canCreate;
    private bool _canApply;
    private bool _canDelete;
    private bool _canEditSwitchType;
    private bool _canEditAdapter;
    private string _statusText = string.Empty;
    private string _selectedSwitchValidationText = "Select a switch or click New to begin.";
    private string _deleteConstraintText = string.Empty;
    private string _attachedVmHintText = "No attached VMs.";
    private string _errorStateText = "Switch load, save, and blocked-delete guidance appears here with recovery steps.";
    private string _loadingStateText = "Switch inventory loading guidance appears here.";
    private string _emptyStateText = "No virtual switches were found on this host. Click Create to prepare a new switch.";
    private Visibility _statusVisibility = Visibility.Collapsed;
    private Visibility _deleteConstraintVisibility = Visibility.Collapsed;
    private Visibility _loadingStateVisibility = Visibility.Collapsed;
    private Visibility _emptyStateVisibility = Visibility.Collapsed;
    private Visibility _errorStateVisibility = Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool CanRefresh
    {
        get => _canRefresh;
        set => SetProperty(ref _canRefresh, value);
    }

    public bool CanCreate
    {
        get => _canCreate;
        set => SetProperty(ref _canCreate, value);
    }

    public bool CanApply
    {
        get => _canApply;
        set => SetProperty(ref _canApply, value);
    }

    public bool CanDelete
    {
        get => _canDelete;
        set => SetProperty(ref _canDelete, value);
    }

    public bool CanEditSwitchType
    {
        get => _canEditSwitchType;
        set => SetProperty(ref _canEditSwitchType, value);
    }

    public bool CanEditAdapter
    {
        get => _canEditAdapter;
        set => SetProperty(ref _canEditAdapter, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SelectedSwitchValidationText
    {
        get => _selectedSwitchValidationText;
        set => SetProperty(ref _selectedSwitchValidationText, value);
    }

    public string DeleteConstraintText
    {
        get => _deleteConstraintText;
        set => SetProperty(ref _deleteConstraintText, value);
    }

    public string AttachedVmHintText
    {
        get => _attachedVmHintText;
        set => SetProperty(ref _attachedVmHintText, value);
    }

    public string ErrorStateText
    {
        get => _errorStateText;
        set => SetProperty(ref _errorStateText, value);
    }

    public string LoadingStateText
    {
        get => _loadingStateText;
        set => SetProperty(ref _loadingStateText, value);
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        set => SetProperty(ref _emptyStateText, value);
    }

    public Visibility StatusVisibility
    {
        get => _statusVisibility;
        set => SetProperty(ref _statusVisibility, value);
    }

    public Visibility DeleteConstraintVisibility
    {
        get => _deleteConstraintVisibility;
        set => SetProperty(ref _deleteConstraintVisibility, value);
    }

    public Visibility LoadingStateVisibility
    {
        get => _loadingStateVisibility;
        set => SetProperty(ref _loadingStateVisibility, value);
    }

    public Visibility EmptyStateVisibility
    {
        get => _emptyStateVisibility;
        set => SetProperty(ref _emptyStateVisibility, value);
    }

    public Visibility ErrorStateVisibility
    {
        get => _errorStateVisibility;
        set => SetProperty(ref _errorStateVisibility, value);
    }

    public void Apply(AssetsSwitchesViewState state)
    {
        CanRefresh = state.CanRefresh;
        CanCreate = state.CanCreate;
        CanApply = state.CanApply;
        CanDelete = state.CanDelete;
        CanEditSwitchType = state.CanEditSwitchType;
        CanEditAdapter = state.CanEditAdapter;
        StatusText = state.StatusText;
        SelectedSwitchValidationText = state.SelectedSwitchValidationText;
        DeleteConstraintText = state.DeleteConstraintText;
        AttachedVmHintText = state.AttachedVmHintText;
        ErrorStateText = state.ErrorStateText;
        LoadingStateText = state.LoadingStateText;
        EmptyStateText = state.EmptyStateText;
        StatusVisibility = string.IsNullOrWhiteSpace(state.StatusText) ? Visibility.Collapsed : Visibility.Visible;
        DeleteConstraintVisibility = string.IsNullOrWhiteSpace(state.DeleteConstraintText) ? Visibility.Collapsed : Visibility.Visible;
        LoadingStateVisibility = state.IsLoadingVisible ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateVisibility = state.IsEmptyVisible ? Visibility.Visible : Visibility.Collapsed;
        ErrorStateVisibility = state.IsErrorVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

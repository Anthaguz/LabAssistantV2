using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsSwitchesView : UserControl
{
    private const double CompactLayoutThreshold = 1040;
    private bool _isUpdatingSelection;
    private bool _isUpdatingEditor;

    public event EventHandler? RefreshRequested;
    public event EventHandler? CreateRequested;
    public event EventHandler? ApplyRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? SelectedSwitchChanged;
    public event EventHandler? EditorChanged;

    public AssetsSwitchesView()
    {
        InitializeComponent();
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
        AssetsSwitchesRefreshButton.IsEnabled = state.CanRefresh;
        AssetsSwitchesCreateButton.IsEnabled = state.CanCreate;
        AssetsSwitchesApplyButton.IsEnabled = state.CanApply;
        AssetsSwitchesDeleteButton.IsEnabled = state.CanDelete;
        AssetsSwitchesTypeComboBox.IsEnabled = state.CanEditSwitchType;
        AssetsSwitchesAdapterTextBox.IsEnabled = state.CanEditAdapter;
        AssetsSwitchesStatusTextBlock.Text = state.StatusText;
        AssetsSwitchesSelectedSwitchValidationTextBlock.Text = state.SelectedSwitchValidationText;
        AssetsSwitchesDeleteConstraintTextBlock.Text = state.DeleteConstraintText;
        AssetsSwitchesAttachedVmsHintTextBlock.Text = state.AttachedVmHintText;
        AssetsSwitchesErrorStateTextBox.Text = state.ErrorStateText;
        AssetsSwitchesLoadingStatePanel.Visibility = state.IsLoadingVisible ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesEmptyStatePanel.Visibility = state.IsEmptyVisible ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesErrorStatePanel.Visibility = state.IsErrorVisible ? Visibility.Visible : Visibility.Collapsed;
        AssetsSwitchesLoadingStateTextBlock.Text = state.LoadingStateText;
        AssetsSwitchesEmptyStateTextBlock.Text = state.EmptyStateText;
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

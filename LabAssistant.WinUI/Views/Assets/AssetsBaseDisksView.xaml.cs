using LabAssistant.Business.Assets;
using LabAssistant.WinUI.Models.Assets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsBaseDisksView : UserControl
{
    private const double CompactLayoutThreshold = 1040;
    private bool _isUpdatingSelection;
    private bool _isUpdatingEditor;

    public event EventHandler? RefreshRequested;
    public event EventHandler? ImportRequested;
    public event EventHandler? ValidateRequested;
    public event EventHandler? SaveMetadataRequested;
    public event EventHandler? RemoveRequested;
    public event EventHandler? BrowsePathRequested;
    public event EventHandler? SelectedBaseDiskChanged;
    public event EventHandler? MetadataChanged;

    public AssetsBaseDisksView()
    {
        InitializeComponent();
        SizeChanged += AssetsBaseDisksView_SizeChanged;
        WireHandlers();
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    public AssetsBaseDiskListRow? SelectedBaseDisk => AssetsBaseDisksListView.SelectedItem as AssetsBaseDiskListRow;

    public void SetInventorySource(object? itemsSource)
    {
        AssetsBaseDisksListView.ItemsSource = itemsSource;
    }

    public void SetSelectedBaseDisk(AssetsBaseDiskListRow? selectedBaseDisk)
    {
        _isUpdatingSelection = true;
        try
        {
            AssetsBaseDisksListView.SelectedItem = selectedBaseDisk;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    public AssetsBaseDiskFormValues CaptureFormValues()
    {
        return new AssetsBaseDiskFormValues(
            AssetsBaseDisksOsNameTextBox.Text,
            AssetsBaseDisksOsVersionTextBox.Text,
            AssetsBaseDisksPathTextBox.Text,
            AssetsBaseDisksGenerationTextBox.Text,
            AssetsBaseDisksNotesTextBox.Text);
    }

    public void ApplyEditorDraft(AssetsBaseDiskDraft draft)
    {
        _isUpdatingEditor = true;
        try
        {
            AssetsBaseDisksOsNameTextBox.Text = draft.OsName;
            AssetsBaseDisksOsVersionTextBox.Text = draft.OsVersion;
            AssetsBaseDisksPathTextBox.Text = draft.Path;
            AssetsBaseDisksGenerationTextBox.Text = draft.Generation > 0 ? draft.Generation.ToString() : string.Empty;
            AssetsBaseDisksNotesTextBox.Text = draft.Notes ?? string.Empty;
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
            AssetsBaseDisksOsNameTextBox.Text = string.Empty;
            AssetsBaseDisksOsVersionTextBox.Text = string.Empty;
            AssetsBaseDisksPathTextBox.Text = string.Empty;
            AssetsBaseDisksGenerationTextBox.Text = string.Empty;
            AssetsBaseDisksNotesTextBox.Text = string.Empty;
        }
        finally
        {
            _isUpdatingEditor = false;
        }
    }

    public void SetDraftPath(string path)
    {
        AssetsBaseDisksPathTextBox.Text = path;
    }

    public void UpdateWorkspaceState(AssetsBaseDisksViewState state)
    {
        AssetsBaseDisksRefreshButton.IsEnabled = state.CanRefresh;
        AssetsBaseDisksImportButton.IsEnabled = state.CanImport;
        AssetsBaseDisksValidateButton.IsEnabled = state.CanValidate;
        AssetsBaseDisksRemoveButton.IsEnabled = state.CanRemove;
        AssetsBaseDisksBrowsePathButton.IsEnabled = state.CanBrowsePath;
        AssetsBaseDisksSaveMetadataButton.IsEnabled = state.CanSaveMetadata;
        AssetsBaseDisksLoadingStatePanel.Visibility = state.IsLoadingVisible ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksEmptyStatePanel.Visibility = state.IsEmptyVisible ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksErrorStatePanel.Visibility = state.IsErrorVisible ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksStatusTextBlock.Text = state.StatusText;
        AssetsBaseDisksSelectedDiskSummaryTextBlock.Text = state.SelectedDiskSummaryText;
        AssetsBaseDisksSelectedDiskValidationTextBlock.Text = state.SelectedDiskValidationText;
        AssetsBaseDisksReferenceWarningTextBlock.Text = state.ReferenceWarningText;
        AssetsBaseDisksErrorStateTextBlock.Text = state.ErrorStateText;
        AssetsBaseDisksLoadingStateTextBlock.Text = state.LoadingStateText;
        AssetsBaseDisksEmptyStateTextBlock.Text = state.EmptyStateText;
        AssetsBaseDisksSelectedDiskValidationTextBlock.Visibility = state.ShowDetailsMessages ? Visibility.Visible : Visibility.Collapsed;
        AssetsBaseDisksReferenceWarningTextBlock.Visibility = state.ShowDetailsMessages ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AssetsBaseDisksView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void WireHandlers()
    {
        AssetsBaseDisksRefreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        AssetsBaseDisksImportButton.Click += (_, _) => ImportRequested?.Invoke(this, EventArgs.Empty);
        AssetsBaseDisksValidateButton.Click += (_, _) => ValidateRequested?.Invoke(this, EventArgs.Empty);
        AssetsBaseDisksSaveMetadataButton.Click += (_, _) => SaveMetadataRequested?.Invoke(this, EventArgs.Empty);
        AssetsBaseDisksRemoveButton.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
        AssetsBaseDisksBrowsePathButton.Click += (_, _) => BrowsePathRequested?.Invoke(this, EventArgs.Empty);
        AssetsBaseDisksListView.SelectionChanged += AssetsBaseDisksListView_SelectionChanged;
        AssetsBaseDisksOsNameTextBox.TextChanged += AssetsBaseDisksMetadataInput_TextChanged;
        AssetsBaseDisksOsVersionTextBox.TextChanged += AssetsBaseDisksMetadataInput_TextChanged;
        AssetsBaseDisksGenerationTextBox.TextChanged += AssetsBaseDisksMetadataInput_TextChanged;
        AssetsBaseDisksNotesTextBox.TextChanged += AssetsBaseDisksMetadataInput_TextChanged;
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        AssetsBaseDisksListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        AssetsBaseDisksDetailsColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.4, GridUnitType.Star);
        AssetsBaseDisksPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        AssetsBaseDisksStateRowDefinition.Height = GridLength.Auto;
        AssetsBaseDisksDetailsRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(AssetsBaseDisksListRegion, 0);
        Grid.SetColumn(AssetsBaseDisksListRegion, 0);

        Grid.SetRow(AssetsBaseDisksDetailsRegion, useStackedLayout ? 2 : 0);
        Grid.SetColumn(AssetsBaseDisksDetailsRegion, useStackedLayout ? 0 : 1);

        Grid.SetRow(AssetsBaseDisksStateRegion, 1);
        Grid.SetColumn(AssetsBaseDisksStateRegion, 0);
        Grid.SetColumnSpan(AssetsBaseDisksStateRegion, useStackedLayout ? 1 : 2);
    }

    private void AssetsBaseDisksListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        SelectedBaseDiskChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AssetsBaseDisksMetadataInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingEditor)
        {
            return;
        }

        MetadataChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed record AssetsBaseDiskFormValues(
    string? OsNameText,
    string? OsVersionText,
    string? PathText,
    string? GenerationText,
    string? NotesText);

public sealed record AssetsBaseDisksViewState(
    bool CanRefresh,
    bool CanImport,
    bool CanValidate,
    bool CanRemove,
    bool CanBrowsePath,
    bool CanSaveMetadata,
    bool IsLoadingVisible,
    bool IsEmptyVisible,
    bool IsErrorVisible,
    bool ShowDetailsMessages,
    string StatusText,
    string SelectedDiskSummaryText,
    string SelectedDiskValidationText,
    string ReferenceWarningText,
    string ErrorStateText,
    string LoadingStateText,
    string EmptyStateText);

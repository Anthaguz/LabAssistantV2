using LabAssistant.Business.Templates;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesLibraryView : UserControl
{
    private bool _isUpdatingSearchText;
    private bool _isUpdatingSelection;

    public event EventHandler? SearchTextChanged;
    public event EventHandler? SelectedTemplateChanged;
    public event EventHandler? ApplySearchRequested;
    public event EventHandler? ClearSearchRequested;
    public event EventHandler? ReloadRequested;
    public event EventHandler? OpenTemplateRequested;
    public event EventHandler? CreateTemplateRequested;
    public event EventHandler? DeleteTemplateRequested;
    public event EventHandler? ImportTemplateRequested;
    public event EventHandler? ExportTemplateRequested;

    public TemplatesLibraryView()
    {
        InitializeComponent();
        TemplateSearchTextBox.TextChanged += TemplateSearchTextBox_TextChanged;
        TemplateLibraryListView.SelectionChanged += TemplateLibraryListView_SelectionChanged;
        ApplyTemplateSearchButton.Click += ApplyTemplateSearchButton_Click;
        ClearTemplateSearchButton.Click += ClearTemplateSearchButton_Click;
        ReloadTemplatesButton.Click += ReloadTemplatesButton_Click;
        OpenTemplateInEditorButton.Click += OpenTemplateInEditorButton_Click;
        CreateTemplateButton.Click += CreateTemplateButton_Click;
        DeleteTemplateButton.Click += DeleteTemplateButton_Click;
        ImportTemplateButton.Click += ImportTemplateButton_Click;
        ExportTemplateButton.Click += ExportTemplateButton_Click;
    }

    public ListView TemplateLibraryListViewControl => TemplateLibraryListView;

    public TemplateLibraryItem? SelectedTemplate => TemplateLibraryListView.SelectedItem as TemplateLibraryItem;

    public TextBox TemplateSearchTextBoxControl => TemplateSearchTextBox;

    public string SearchText => TemplateSearchTextBox.Text;

    public Button ApplyTemplateSearchButtonControl => ApplyTemplateSearchButton;

    public Button ClearTemplateSearchButtonControl => ClearTemplateSearchButton;

    public Button ReloadTemplatesButtonControl => ReloadTemplatesButton;

    public Button OpenTemplateInEditorButtonControl => OpenTemplateInEditorButton;

    public Button CreateTemplateButtonControl => CreateTemplateButton;

    public Button DeleteTemplateButtonControl => DeleteTemplateButton;

    public Button ImportTemplateButtonControl => ImportTemplateButton;

    public Button ExportTemplateButtonControl => ExportTemplateButton;

    public TextBlock TemplatesLibraryStatusTextBlockControl => TemplatesLibraryStatusTextBlock;

    public void SetInventorySource(object? itemsSource)
    {
        TemplateLibraryListView.ItemsSource = itemsSource;
    }

    public void SetSelectedTemplate(TemplateLibraryItem? selectedTemplate)
    {
        if (ReferenceEquals(TemplateLibraryListView.SelectedItem, selectedTemplate))
        {
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            TemplateLibraryListView.SelectedItem = selectedTemplate;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    public void SetSearchText(string searchText)
    {
        if (string.Equals(TemplateSearchTextBox.Text, searchText, StringComparison.Ordinal))
        {
            return;
        }

        _isUpdatingSearchText = true;
        try
        {
            TemplateSearchTextBox.Text = searchText;
        }
        finally
        {
            _isUpdatingSearchText = false;
        }
    }

    public void SetStatusText(string statusText)
    {
        TemplatesLibraryStatusTextBlock.Text = statusText;
    }

    private void TemplateSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSearchText)
        {
            return;
        }

        SearchTextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TemplateLibraryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        SelectedTemplateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyTemplateSearchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ApplySearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearTemplateSearchButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ClearSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReloadTemplatesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenTemplateInEditorButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenTemplateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CreateTemplateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CreateTemplateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteTemplateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        DeleteTemplateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ImportTemplateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ImportTemplateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExportTemplateButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ExportTemplateRequested?.Invoke(this, EventArgs.Empty);
    }
}

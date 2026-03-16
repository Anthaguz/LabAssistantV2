using LabAssistant.Business.Templates;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesLibraryView : UserControl
{
    private bool _isUpdatingSearchText;
    private bool _isUpdatingSelection;

    public event EventHandler? SearchTextChanged;
    public event EventHandler? SelectedTemplateChanged;

    public TemplatesLibraryView()
    {
        InitializeComponent();
        TemplateSearchTextBox.TextChanged += TemplateSearchTextBox_TextChanged;
        TemplateLibraryListView.SelectionChanged += TemplateLibraryListView_SelectionChanged;
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
}

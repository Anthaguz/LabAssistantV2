using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesLibraryView : UserControl
{
    private bool _isUpdatingSearchText;

    public event EventHandler? SearchTextChanged;

    public TemplatesLibraryView()
    {
        InitializeComponent();
        TemplateSearchTextBox.TextChanged += TemplateSearchTextBox_TextChanged;
    }

    public ListView TemplateLibraryListViewControl => TemplateLibraryListView;

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
}

using System.Windows.Controls;
using LabAssistant.Models.Templates;

namespace LabAssistant.Views;

public partial class TemplateDetailsPage : Page
{
    public TemplateDetailsPage(LabTemplate template)
    {
        InitializeComponent();
        TemplateNameText.Text = template.Name;
        TemplateDescriptionText.Text = template.Description ?? string.Empty;
        VmListView.ItemsSource = template.VmTemplates;
    }
}

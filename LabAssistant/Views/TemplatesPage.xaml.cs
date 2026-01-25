using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Services;
using LabAssistant.Services.Templates;

namespace LabAssistant.Views
{
    public partial class TemplatesPage : Page
    {
        private readonly LabTemplateLoader _templateLoader = new LabTemplateLoader();
        private readonly List<LabTemplate> _templates = new List<LabTemplate>();

        public TemplatesPage()
        {
            InitializeComponent();
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            _templates.Clear();

            foreach (var folder in SettingsManager.Current.TemplatePaths)
            {
                if (Directory.Exists(folder))
                {
                    var loadResult = _templateLoader.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());
                    _templates.AddRange(loadResult.Templates);
                }
            }

            TemplatesListBox.ItemsSource = null;
            TemplatesListBox.ItemsSource = _templates;
            EmptyStateText.Visibility = _templates.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private void ReloadTemplates_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplates();
        }
    }
}

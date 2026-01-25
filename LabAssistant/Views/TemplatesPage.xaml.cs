using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Configuration;
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

            var folder = SettingsManager.Settings.TemplateFolder;
            if (!string.IsNullOrWhiteSpace(folder))
            {
                if (Directory.Exists(folder))
                {
                    var loadResult = _templateLoader.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());
                    _templates.AddRange(loadResult.Templates);
                    if (loadResult.Errors.Count > 0)
                    {
                        MainWindow.CurrentInstance?.ShowError(string.Join(Environment.NewLine, loadResult.Errors));
                    }
                }
            }

            TemplatesListBox.ItemsSource = null;
            TemplatesListBox.ItemsSource = _templates;
            EmptyStateText.Visibility = _templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ReloadTemplates_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplates();
        }

        private void TemplatesListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (TemplatesListBox.SelectedItem is LabTemplate template)
            {
                NavigationService?.Navigate(new TemplateDetailsPage(template));
            }
        }
    }
}

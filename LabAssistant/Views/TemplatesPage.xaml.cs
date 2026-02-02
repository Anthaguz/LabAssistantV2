using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views
{
    public partial class TemplatesPage : Page
    {
        private readonly ILabTemplateStore _templateStore;
        private readonly IVhdxCatalogStore _catalogStore;
        private readonly IAppSettingsStore _settingsStore;
        private readonly List<LabTemplate> _templates = new List<LabTemplate>();

        public TemplatesPage()
        {
            InitializeComponent();
            _templateStore = App.Services.GetRequiredService<ILabTemplateStore>();
            _catalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();
            _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            _templates.Clear();

            var folder = _settingsStore.Settings.TemplateFolder;
            if (!string.IsNullOrWhiteSpace(folder))
            {
                var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
                var loadResult = _templateStore.LoadFromFolder(folder, catalogResult.Items);
                _templates.AddRange(loadResult.Templates);
                var errors = new List<string>();
                if (catalogResult.Errors.Count > 0)
                {
                    errors.AddRange(catalogResult.Errors);
                }

                if (loadResult.Errors.Count > 0)
                {
                    errors.AddRange(loadResult.Errors);
                }

                if (errors.Count > 0)
                {
                    MainWindow.CurrentInstance?.ShowError(string.Join(Environment.NewLine, errors));
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

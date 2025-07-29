using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models;
using LabAssistant.Services;

namespace LabAssistant.Views
{
    public partial class TemplatesPage : Page
    {
        private List<TemplateModel> _templates = new List<TemplateModel>();

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
                    var jsonFiles = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
                    foreach (var file in jsonFiles)
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            var template = JsonSerializer.Deserialize<TemplateModel>(content);
                            if (template != null)
                            {
                                _templates.Add(template);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Failed to load template {file}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }

            TemplatesListBox.ItemsSource = null;
            TemplatesListBox.ItemsSource = _templates;
        }

        private void ReloadTemplates_Click(object sender, RoutedEventArgs e)
        {
            LoadTemplates();
        }
    }
}

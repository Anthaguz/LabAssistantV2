using System;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Templates;
using LabAssistant.ViewModels;

namespace LabAssistant.Views
{
    public partial class TemplateVmDetailPage : Page
    {
        private readonly Action _onBack;

        public TemplateVmDetailPage(TemplateEditorViewModel editorViewModel, VmTemplate vmTemplate, Action onBack)
        {
            InitializeComponent();
            DataContext = new TemplateVmDetailViewModel(editorViewModel, vmTemplate);
            _onBack = onBack;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _onBack();
        }
    }
}

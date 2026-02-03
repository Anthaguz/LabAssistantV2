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
        private readonly Action? _onLoaded;

        public TemplateVmDetailPage(TemplateEditorViewModel editorViewModel, VmTemplate vmTemplate, Action onBack, Action? onLoaded = null)
        {
            InitializeComponent();
            DataContext = new TemplateVmConfigContext(editorViewModel, vmTemplate);
            _onBack = onBack;
            _onLoaded = onLoaded;
            Loaded += TemplateVmDetailPage_Loaded;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _onBack();
        }

        private void TemplateVmDetailPage_Loaded(object sender, RoutedEventArgs e)
        {
            _onLoaded?.Invoke();
        }

        public void FocusField(VmDetailField field)
        {
            switch (field)
            {
                case VmDetailField.Name:
                    VmConfigPanel.FocusNameField();
                    break;
                case VmDetailField.Memory:
                    VmConfigPanel.FocusMemoryField();
                    break;
                case VmDetailField.Cpu:
                    VmConfigPanel.FocusCpuField();
                    break;
                case VmDetailField.Switch:
                    VmConfigPanel.FocusSwitchField();
                    break;
                case VmDetailField.Vhdx:
                    VmConfigPanel.FocusVhdxField();
                    break;
            }
        }
    }

    public enum VmDetailField
    {
        Name,
        Memory,
        Cpu,
        Switch,
        Vhdx
    }
}

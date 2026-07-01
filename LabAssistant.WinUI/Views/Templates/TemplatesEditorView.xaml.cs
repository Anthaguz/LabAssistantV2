using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesEditorView : UserControl
{
    public TemplatesEditorViewModel ViewModel { get; }

    public TemplatesEditorView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesEditorViewModel>();
        InitializeComponent();
        InitializeBridge();
    }

    partial void InitializeBridge();
}

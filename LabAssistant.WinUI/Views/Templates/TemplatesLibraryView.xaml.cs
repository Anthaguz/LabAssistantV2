using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesLibraryView : UserControl
{
    public TemplatesLibraryViewModel ViewModel { get; }

    public TemplatesLibraryView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesLibraryViewModel>();
        InitializeComponent();
        InitializeBridge();
    }

    partial void InitializeBridge();
}

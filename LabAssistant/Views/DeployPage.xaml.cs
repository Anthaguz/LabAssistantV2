using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models;
using LabAssistant.Services;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views
{
    public partial class DeployPage : Page
    {
        public DeployPage()
        {
            InitializeComponent();
            DataContext = App.Services.GetRequiredService<DeploymentViewModel>();
        }
    }
}

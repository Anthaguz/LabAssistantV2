using LabAssistant.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LabAssistant.Views
{
    public partial class VmDetailPage : Page
    {
        public VmDetailPage(VmEntryViewModel vmEntry)
        {
            InitializeComponent();
            DataContext = vmEntry;
        }

        private void ApplyChanges(object sender, RoutedEventArgs e)
        {
            ((MainWindow)System.Windows.Application.Current.MainWindow).MainContentFrame.Navigate( new Views.DeployPage());
        }
    }
}

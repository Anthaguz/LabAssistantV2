using System.Windows;
using System.Windows.Controls;

namespace LabAssistant
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new Views.DeployPage()); // Default page
        }
        public Frame MainContentFrame => MainFrame;


        private void DeployButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.DeployPage());
        }

        private void TemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.TemplatesPage());
        }

        private void SwitchesButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.SwitchesPage());
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.SettingsPage());
        }

        private void LogsButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.LogsPage());
        }

    }
}

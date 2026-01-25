using System.Windows;
using System.Windows.Controls;

namespace LabAssistant
{
    public partial class MainWindow : Window
    {
        public static MainWindow? CurrentInstance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            CurrentInstance = this;
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

        public void ShowError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ErrorBanner.Visibility = Visibility.Collapsed;
                return;
            }

            ErrorBannerText.Text = message;
            ErrorBanner.Visibility = Visibility.Visible;
        }

    }
}

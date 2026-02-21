using System.Windows;
using System.Windows.Controls;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant
{
    public partial class MainWindow : Window
    {
        private readonly IErrorFeedService _errorFeed;
        public static MainWindow? CurrentInstance { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            _errorFeed = App.Services.GetRequiredService<IErrorFeedService>();
            DataContext = _errorFeed;
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

        private void TemplateEditorButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.TemplateEditorPage());
        }

        private void VhdxCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Views.VhdxCatalogPage());
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

        private void SnackViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not ErrorFeedItem item)
            {
                return;
            }

            item.ViewDetailsAction?.Invoke();
        }

        private void SnackDismiss_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not ErrorFeedItem item)
            {
                return;
            }

            _errorFeed.Dismiss(item.Id);
        }

        private void SnackCard_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is ErrorFeedItem item)
            {
                item.IsHovered = true;
            }
        }

        private void SnackCard_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is ErrorFeedItem item)
            {
                item.IsHovered = false;
            }
        }
    }
}

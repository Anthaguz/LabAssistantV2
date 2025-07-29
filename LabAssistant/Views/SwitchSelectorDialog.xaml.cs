using System.Collections.Generic;
using System.Windows;

namespace LabAssistant.Views
{
    public partial class SwitchSelectorDialog : Window
    {
        public string SelectedSwitch { get; private set; }

        public SwitchSelectorDialog(List<string> switches)
        {
            InitializeComponent();
            SwitchesComboBox.ItemsSource = switches;

            if (switches.Count > 0)
                SwitchesComboBox.SelectedIndex = 0;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (SwitchesComboBox.SelectedItem != null)
            {
                SelectedSwitch = SwitchesComboBox.SelectedItem.ToString();
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Please select a switch first.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

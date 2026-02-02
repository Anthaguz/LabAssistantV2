using System.Windows;

namespace LabAssistant.Views
{
    public partial class TemplateSaveReviewDialog : Window
    {
        public TemplateSaveReviewDialog(TemplateSaveReviewModel model)
        {
            InitializeComponent();
            DataContext = model;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

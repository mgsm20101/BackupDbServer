using System.Collections.Generic;
using System.Windows;

namespace BackupDbServer.Views
{
    public partial class SqlExecutorWindow : Window
    {
        public SqlExecutorWindow()
        {
            InitializeComponent();
        }

        public SqlExecutorWindow(List<string> selectedDatabases) : this()
        {
            if (DataContext is ViewModels.SqlExecutorViewModel viewModel)
            {
                viewModel.SelectedDatabases = selectedDatabases;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
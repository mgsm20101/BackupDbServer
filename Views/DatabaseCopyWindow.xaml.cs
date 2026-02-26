using BackupDbServer.ViewModels;
using System.Windows;

namespace BackupDbServer.Views
{
    public partial class DatabaseCopyWindow : Window
    {
        public DatabaseCopyWindow()
        {
            InitializeComponent();
            DataContext = new DatabaseCopyViewModel();
        }
    }
}

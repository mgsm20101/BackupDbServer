using System.Windows;
using BackupDbServer.ViewModels;

namespace BackupDbServer.Views
{
    public partial class DailyTaskManagementWindow : Window
    {
        public DailyTaskManagementWindow(DailyTaskViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
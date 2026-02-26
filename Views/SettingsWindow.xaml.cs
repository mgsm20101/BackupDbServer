using BackupDbServer.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace BackupDbServer.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            // تعيين الـ ViewModel كمصدر بيانات للنافذة
            DataContext = new SettingsViewModel();
        }

        // PasswordBox لا يدعم الربط مباشرة لأسباب أمنية
        // لذا، نستخدم هذا الحدث لتحديث كلمة المرور في الـ ViewModel
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.Password = (sender as PasswordBox)?.Password ?? string.Empty;
            }
        }
    }
}

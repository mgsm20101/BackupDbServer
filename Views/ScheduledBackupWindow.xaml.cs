using BackupDbServer.ViewModels;
using System.Windows;

namespace BackupDbServer.Views
{
    public partial class ScheduledBackupWindow : Window
    {
        public ScheduledBackupWindow()
        {
            InitializeComponent();
            DataContext = new ScheduledBackupViewModel();
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveConfigBeforeClose();
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // حفظ الإعدادات عند إغلاق النافذة
            if (DataContext is ScheduledBackupViewModel viewModel)
            {
                try
                {
                    // تحديث حالة IsRuning في الكونفيغ
                    viewModel.Config.IsRuning = viewModel.IsServiceRunning;
                    
                    // حفظ الإعدادات بشكل متزامن
                    viewModel.SaveConfigAsync(null).Wait(2000); // انتظار ثانيتين كحد أقصى
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"خطأ في حفظ الإعدادات عند الإغلاق: {ex.Message}");
                }
            }
            base.OnClosing(e);
        }

        private async System.Threading.Tasks.Task SaveConfigBeforeClose()
        {
            if (DataContext is ScheduledBackupViewModel viewModel)
            {
                try
                {
                    await viewModel.SaveConfigAsync(null);
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"خطأ في حفظ الإعدادات: {ex.Message}");
                }
            }
        }
    }
}
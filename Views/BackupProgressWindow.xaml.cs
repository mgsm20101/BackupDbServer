using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BackupDbServer.Views
{
    /// <summary>
    /// Interaction logic for BackupProgressWindow.xaml
    /// </summary>
    public partial class BackupProgressWindow : Window
    {
        private readonly BackupRestoreService _backupService;

        public BackupProgressWindow()
        {
            InitializeComponent();
            
            // استخدام BackupService من ServiceContainer
            _backupService = ServiceContainer.BackupService;
            
            Loaded += BackupProgressWindow_Loaded;
        }

        private async void BackupProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var config = SettingsManager.LoadSettings();
            _backupService.UpdateSettings(config);

            StatusText.Text = "جاري بدء النسخ الاحتياطي...";

            var selectedDatabases = config.SelectedDatabases?.Where(db => !string.IsNullOrEmpty(db)).ToList() 
                ?? new List<string>();

            if (!selectedDatabases.Any())
            {
                StatusText.Text = "❌ لا توجد قواعد بيانات محددة للنسخ الاحتياطي";
                return;
            }

            int total = selectedDatabases.Count;
            int done = 0;

            ProgressBar.Minimum = 0;
            ProgressBar.Maximum = total;

            // استخدام BackupRestoreService المحسن
            var progress = new Progress<string>(message =>
            {
                StatusText.Text = message;
                
                // تحديث العداد عند نجاح النسخ
                if (message.Contains("✅") || message.Contains("تم النسخ"))
                {
                    done++;
                    ProgressCount.Text = $"{done} من {total}";
                    ProgressBar.Value = done;
                    ResultsList.Items.Add($"✔ {message}");
                }
                else if (message.Contains("❌") || message.Contains("فشل"))
                {
                    ResultsList.Items.Add($"❌ {message}");
                }
            });

            try
            {
                await _backupService.BackupDatabasesAsync(selectedDatabases, progress);
                StatusText.Text = $"✅ تم الانتهاء - نجح نسخ {done} من {total} قاعدة بيانات";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ خطأ: {ex.Message}";
                ResultsList.Items.Add($"❌ خطأ في عملية النسخ: {ex.Message}");
            }
        }
    }
}

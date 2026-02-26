using System.Windows;
using BackupDbServer.Helpers;
using System.ComponentModel;

namespace BackupDbServer.Views
{
    public partial class MainWindow : Window
    {
        private TrayIconManager? _trayIconManager;
        private FileLogger? _logger;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                _logger = new FileLogger();
                InitializeTrayIcon();
                _logger.LogInformation("تم تهيئة النافذة الرئيسية بنجاح");
            }
            catch (Exception ex)
            {
                var logger = new FileLogger();
                logger.LogError("خطأ في تهيئة النافذة الرئيسية", ex);
                
                System.Windows.MessageBox.Show($"خطأ في تهيئة النافذة الرئيسية:\n{ex.Message}", 
                              "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// تهيئة أيقونة شريط المهام
        /// </summary>
        private void InitializeTrayIcon()
        {
            try
            {
                _trayIconManager = new TrayIconManager(this);
                
                // ربط TrayIconManager مع MainViewModel
                if (this.DataContext is ViewModels.MainViewModel mainViewModel)
                {
                    mainViewModel.SetTrayIconManager(_trayIconManager);
                }
                
                // إخفاء النافذة من شريط المهام عند التصغير
                this.StateChanged += MainWindow_StateChanged;
                
                // معالجة إغلاق النافذة لإخفائها بدلاً من الإغلاق
                this.Closing += MainWindow_Closing;
                
                _logger?.LogInformation("تم تهيئة أيقونة شريط المهام بنجاح");
            }
            catch (Exception ex)
            {
                _logger?.LogError("خطأ في تهيئة أيقونة شريط المهام", ex);
                System.Windows.MessageBox.Show($"خطأ في تهيئة أيقونة شريط المهام:\n{ex.Message}", 
                              "تحذير", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// معالج تغيير حالة النافذة
        /// </summary>
        private void MainWindow_StateChanged(object? sender, System.EventArgs e)
        {
            try
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    // إخفاء النافذة عند التصغير
                    this.Hide();
                    
                    // إظهار إشعار للمستخدم في المرة الأولى
                    if (_trayIconManager != null && !HasShownTrayNotification())
                    {
                        _trayIconManager.ShowBalloonTip(
                            "نظام النسخ الاحتياطي", 
                            "البرنامج يعمل الآن بالخلفية. انقر على الأيقونة لإظهار النافذة مرة أخرى.", 
                            Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                        
                        SetTrayNotificationShown();
                    }
                    
                    _logger?.LogInformation("تم تصغير النافذة إلى شريط المهام");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("خطأ في معالج تغيير حالة النافذة", ex);
            }
        }

        /// <summary>
        /// معالج إغلاق النافذة
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                // منع الإغلاق الفعلي للنافذة، وإخفاؤها بدلاً من ذلك
                e.Cancel = true;
                this.Hide();
                
                // إظهار إشعار للمستخدم
                _trayIconManager?.ShowBalloonTip(
                    "نظام النسخ الاحتياطي", 
                    "البرنامج يعمل بالخلفية. انقر بالزر الأيمن على الأيقونة واختر 'إنهاء البرنامج' للخروج نهائياً.", 
                    Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
                    
                _logger?.LogInformation("تم إخفاء النافذة بدلاً من الإغلاق");
            }
            catch (Exception ex)
            {
                _logger?.LogError("خطأ في معالج إغلاق النافذة", ex);
                // في حالة الخطأ، نسمح بالإغلاق
                e.Cancel = false;
            }
        }

        /// <summary>
        /// التحقق من أنه تم إظهار إشعار الـ Tray من قبل
        /// </summary>
        private bool HasShownTrayNotification()
        {
            return Properties.Settings.Default.HasShownTrayNotification;
        }

        /// <summary>
        /// تعيين أنه تم إظهار إشعار الـ Tray
        /// </summary>
        private void SetTrayNotificationShown()
        {
            Properties.Settings.Default.HasShownTrayNotification = true;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// تنظيف الموارد عند إغلاق النافذة
        /// </summary>
        protected override void OnClosed(System.EventArgs e)
        {
            try
            {
                _logger?.LogInformation("بدء تنظيف موارد النافذة الرئيسية");
                _trayIconManager?.Dispose();
                _logger?.LogInformation("تم تنظيف موارد النافذة الرئيسية بنجاح");
            }
            catch (Exception ex)
            {
                _logger?.LogError("خطأ في تنظيف موارد النافذة الرئيسية", ex);
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}

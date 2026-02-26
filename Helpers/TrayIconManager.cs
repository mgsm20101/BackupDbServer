using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using BackupDbServer.ViewModels;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// إدارة أيقونة شريط المهام (System Tray)
    /// </summary>
    public class TrayIconManager : IDisposable
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly Window _mainWindow;
        private bool _disposed = false;

        public TrayIconManager(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            InitializeTrayIcon();
        }

        /// <summary>
        /// تهيئة أيقونة شريط المهام
        /// </summary>
        private void InitializeTrayIcon()
        {
            // إنشاء مصدر الأيقونة من الملف
            System.Windows.Media.ImageSource iconSource;
            try
            {
                var iconUri = new Uri("pack://application:,,,/Resources/icon.ico");
                iconSource = new System.Windows.Media.Imaging.BitmapImage(iconUri);
            }
            catch
            {
                // في حالة فشل تحميل الأيقونة، استخدم الأيقونة الافتراضية
                iconSource = System.Windows.Application.Current.Resources["TrayIcon"] as System.Windows.Media.ImageSource;
            }

            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "نظام النسخ الاحتياطي لقواعد البيانات",
                IconSource = iconSource,
                ContextMenu = CreateContextMenu()
            };

            // إضافة معالجات الأحداث
            _taskbarIcon.TrayLeftMouseUp += OnTrayIconClicked;
            _taskbarIcon.TrayMouseDoubleClick += OnTrayIconDoubleClicked;
        }

        /// <summary>
        /// إنشاء قائمة السياق للأيقونة
        /// </summary>
        private ContextMenu CreateContextMenu()
        {
            var contextMenu = new ContextMenu();

            // عنصر إظهار النافذة
            var showMenuItem = new MenuItem
            {
                Header = "إظهار النافذة الرئيسية",
                FontWeight = FontWeights.Bold
            };
            showMenuItem.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(showMenuItem);

            // فاصل
            contextMenu.Items.Add(new Separator());

            // عنصر بدء النسخ الاحتياطي السريع
            var quickBackupMenuItem = new MenuItem
            {
                Header = "نسخ احتياطي سريع"
            };
            quickBackupMenuItem.Click += OnQuickBackupClicked;
            contextMenu.Items.Add(quickBackupMenuItem);

            // عنصر عرض السجل
            var showLogMenuItem = new MenuItem
            {
                Header = "عرض سجل العمليات"
            };
            showLogMenuItem.Click += OnShowLogClicked;
            contextMenu.Items.Add(showLogMenuItem);

            // فاصل
            contextMenu.Items.Add(new Separator());

            // عنصر الإعدادات
            var settingsMenuItem = new MenuItem
            {
                Header = "الإعدادات"
            };
            settingsMenuItem.Click += OnSettingsClicked;
            contextMenu.Items.Add(settingsMenuItem);

            // فاصل
            contextMenu.Items.Add(new Separator());

            // عنصر الخروج
            var exitMenuItem = new MenuItem
            {
                Header = "إنهاء البرنامج"
            };
            exitMenuItem.Click += OnExitClicked;
            contextMenu.Items.Add(exitMenuItem);

            return contextMenu;
        }

        /// <summary>
        /// معالج النقر على الأيقونة
        /// </summary>
        private void OnTrayIconClicked(object sender, RoutedEventArgs e)
        {
            // عند النقر مرة واحدة، نظهر/نخفي النافذة
            if (_mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized)
            {
                HideMainWindow();
            }
            else
            {
                ShowMainWindow();
            }
        }

        /// <summary>
        /// معالج النقر المزدوج على الأيقونة
        /// </summary>
        private void OnTrayIconDoubleClicked(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>
        /// معالج النقر على نسخ احتياطي سريع
        /// </summary>
        private void OnQuickBackupClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                // الحصول على MainViewModel
                var mainViewModel = _mainWindow.DataContext as MainViewModel;
                if (mainViewModel != null)
                {
                    // تنفيذ النسخ الاحتياطي للقواعد المحددة
                    if (mainViewModel.RunBackupCommand.CanExecute(null))
                    {
                        ShowBalloonTip("نسخ احتياطي سريع", "جاري بدء النسخ الاحتياطي السريع...", BalloonIcon.Info);
                        mainViewModel.RunBackupCommand.Execute(null);
                    }
                    else
                    {
                        ShowBalloonTip("نسخ احتياطي سريع", "لا يمكن بدء النسخ الاحتياطي الآن. تأكد من تحديد قواعد البيانات.", BalloonIcon.Warning);
                        ShowMainWindow();
                    }
                }
                else
                {
                    ShowMainWindow();
                }
            }
            catch (Exception ex)
            {
                ShowBalloonTip("خطأ", $"حدث خطأ: {ex.Message}", BalloonIcon.Error);
            }
        }

        /// <summary>
        /// معالج النقر على عرض السجل
        /// </summary>
        private void OnShowLogClicked(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
            // هنا يمكن إضافة كود للانتقال لتاب السجل
        }

        /// <summary>
        /// معالج النقر على الإعدادات
        /// </summary>
        private void OnSettingsClicked(object sender, RoutedEventArgs e)
        {
            ShowMainWindow();
            // هنا يمكن إضافة كود لفتح نافذة الإعدادات
        }

        /// <summary>
        /// معالج النقر على الخروج
        /// </summary>
        private void OnExitClicked(object sender, RoutedEventArgs e)
        {
            // إنهاء البرنامج نهائياً
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// إظهار النافذة الرئيسية
        /// </summary>
        public void ShowMainWindow()
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.BringIntoView();

            // التأكد من إحضار النافذة للمقدمة
            if (!_mainWindow.Topmost)
            {
                _mainWindow.Topmost = true;
                _mainWindow.Topmost = false;
            }
        }

        /// <summary>
        /// إخفاء النافذة الرئيسية
        /// </summary>
        public void HideMainWindow()
        {
            _mainWindow.Hide();
        }

        /// <summary>
        /// عرض رسالة في بالون الإشعارات
        /// </summary>
        public void ShowBalloonTip(string title, string message, BalloonIcon icon = BalloonIcon.Info)
        {
            _taskbarIcon?.ShowBalloonTip(title, message, icon);
        }

        /// <summary>
        /// تحديث أيقونة شريط المهام
        /// </summary>
        public void UpdateIcon(System.Windows.Media.ImageSource newIcon)
        {
            if (_taskbarIcon != null)
            {
                _taskbarIcon.IconSource = newIcon;
            }
        }

        /// <summary>
        /// تحديث نص التلميح
        /// </summary>
        public void UpdateToolTip(string newToolTip)
        {
            if (_taskbarIcon != null)
            {
                _taskbarIcon.ToolTipText = newToolTip;
            }
        }

        /// <summary>
        /// تنظيف الموارد
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _taskbarIcon?.Dispose();
                _disposed = true;
            }
        }
    }
}

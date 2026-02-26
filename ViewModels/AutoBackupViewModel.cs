using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BackupDbServer.ViewModels
{
    public class AutoBackupViewModel : ViewModelBase
    {
        private readonly IAutoBackupService _autoBackupService;
        private readonly BackupRestoreService _backupService;
        private readonly ILogger _logger;
        private AutoBackupConfig _config;
        private DispatcherTimer? _statusUpdateTimer;
        private string _statusText = "غير نشط";
        private bool _isServiceRunning;
        private ObservableCollection<BackupScheduleEntry> _recentBackups;
        private ObservableCollection<DatabaseSelectionViewModel> _availableDatabases;

        public AutoBackupConfig Config
        {
            get => _config;
            set 
            { 
                _config = value; 
                OnPropertyChanged(); 
                
                // حفظ تلقائي عند تغيير الإعدادات
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _autoBackupService.UpdateConfigAsync(_config);
                        _logger.LogInformation("تم حفظ الإعدادات تلقائياً");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"فشل الحفظ التلقائي للإعدادات: {ex.Message}", ex);
                    }
                });
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            set { _isServiceRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ServiceStatusText)); }
        }

        public string ServiceStatusText => IsServiceRunning ? "نشط" : "متوقف";

        public ObservableCollection<BackupScheduleEntry> RecentBackups
        {
            get => _recentBackups;
            set { _recentBackups = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DatabaseSelectionViewModel> AvailableDatabases
        {
            get => _availableDatabases;
            set { _availableDatabases = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedDatabasesText)); }
        }

        public string SelectedDatabasesText
        {
            get
            {
                if (_config?.SelectedDatabases == null || !_config.SelectedDatabases.Any())
                    return "لم يتم تحديد أي قواعد بيانات";

                return $"قواعد البيانات المحددة: {string.Join(", ", _config.SelectedDatabases)}";
            }
        }

        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }
        public ICommand RunBackupNowCommand { get; }
        public ICommand SaveConfigCommand { get; }
        public ICommand LoadDatabasesCommand { get; }
        public ICommand SelectGoogleCredentialsCommand { get; }
        public ICommand TestGoogleDriveCommand { get; }

        public AutoBackupViewModel(IAutoBackupService autoBackupService, BackupRestoreService backupService, ILogger logger)
        {
            _autoBackupService = autoBackupService;
            _backupService = backupService;
            _logger = logger;

            _config = _autoBackupService.GetConfig();
            
            // مراقبة تغييرات الإعدادات
            _config.PropertyChanged += OnConfigPropertyChanged;
            
            // تحميل قواعد البيانات المحددة من الإعدادات الرئيسية
            var mainSettings = SettingsManager.LoadSettings();
            if (mainSettings.SelectedDatabases != null && mainSettings.SelectedDatabases.Any())
            {
                _config.SelectedDatabases = mainSettings.SelectedDatabases;
            }
            
            _recentBackups = new ObservableCollection<BackupScheduleEntry>();
            _availableDatabases = new ObservableCollection<DatabaseSelectionViewModel>();

            StartServiceCommand = new AsyncRelayCommand(StartServiceAsync);
            StopServiceCommand = new AsyncRelayCommand(StopServiceAsync);
            RunBackupNowCommand = new AsyncRelayCommand(RunBackupNowAsync);
            SaveConfigCommand = new AsyncRelayCommand(SaveConfigAsync);
            LoadDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);
            SelectGoogleCredentialsCommand = new RelayCommand(SelectGoogleCredentials);
            TestGoogleDriveCommand = new AsyncRelayCommand(TestGoogleDriveAsync);

            // الاشتراك في الأحداث
            _autoBackupService.StatusChanged += OnStatusChanged;
            _autoBackupService.BackupCompleted += OnBackupCompleted;

            // تحديث الحالة الأولية
            IsServiceRunning = _autoBackupService.IsRunning;
            LoadRecentBackups();
            _ = LoadDatabasesAsync(parameter: null);

            // إنشاء Timer لتحديث حالة الخدمة كل 5 ثوان
            _statusUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _statusUpdateTimer.Tick += (s, e) => 
            {
                IsServiceRunning = _autoBackupService.IsRunning;
            };
            _statusUpdateTimer.Start();
        }

        private async void OnConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                _logger.LogInformation($"تغيير في إعدادات النسخ التلقائي: {e.PropertyName}");
                
                // تحديث التاريخ التالي عند تغيير الوقت أو تفعيل الوقت المحدد
                if (e.PropertyName == nameof(AutoBackupConfig.ScheduledTime) || 
                    e.PropertyName == nameof(AutoBackupConfig.UseScheduledTime))
                {
                    _config.RefreshNextBackupTime();
                    _logger.LogInformation($"تم تحديث التاريخ التالي: {_config.NextBackupTime:yyyy-MM-dd HH:mm:ss}");
                }
                
                // حفظ الإعدادات عند أي تغيير مهم
                if (IsImportantConfigChange(e.PropertyName))
                {
                    await _autoBackupService.UpdateConfigAsync(_config);
                    _logger.LogInformation("تم حفظ الإعدادات تلقائياً بعد التغيير");
                    
                    // تحديث الحالة في الواجهة
                    OnPropertyChanged(nameof(Config));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل معالجة تغيير الإعدادات: {ex.Message}", ex);
            }
        }

        private bool IsImportantConfigChange(string? propertyName)
        {
            return propertyName == nameof(AutoBackupConfig.IsEnabled) ||
                   propertyName == nameof(AutoBackupConfig.UseScheduledTime) ||
                   propertyName == nameof(AutoBackupConfig.ScheduledTime) ||
                   propertyName == nameof(AutoBackupConfig.IntervalHours) ||
                   propertyName == nameof(AutoBackupConfig.UploadToGoogleDrive) ||
                   propertyName == nameof(AutoBackupConfig.UseGoogleDriveScheduledTime) ||
                   propertyName == nameof(AutoBackupConfig.GoogleDriveUploadTime);
        }

        private async Task StartServiceAsync(object? parameter)
        {
            try
            {
                // حفظ الإعدادات قبل البدء
                await SaveConfigAsync(null);
                
                await _autoBackupService.StartAsync();
                IsServiceRunning = _autoBackupService.IsRunning;
                StatusText = "تم بدء الخدمة";
                
                _logger.LogInformation("تم بدء خدمة النسخ التلقائي من الواجهة");
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل بدء الخدمة: {ex.Message}", ex);
                StatusText = $"فشل بدء الخدمة: {ex.Message}";
            }
        }

        private async Task StopServiceAsync(object? parameter)
        {
            try
            {
                await _autoBackupService.StopAsync();
                IsServiceRunning = _autoBackupService.IsRunning;
                StatusText = "تم إيقاف الخدمة";
                
                _logger.LogInformation("تم إيقاف خدمة النسخ التلقائي من الواجهة");
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل إيقاف الخدمة: {ex.Message}", ex);
                StatusText = $"فشل إيقاف الخدمة: {ex.Message}";
            }
        }

        private async Task RunBackupNowAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري تنفيذ النسخ الاحتياطي...";
                var success = await _autoBackupService.RunBackupNowAsync();
                StatusText = success ? "تم النسخ الاحتياطي بنجاح" : "فشل النسخ الاحتياطي";
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل تنفيذ النسخ الاحتياطي: {ex.Message}", ex);
                StatusText = $"فشل النسخ الاحتياطي: {ex.Message}";
            }
        }

        private async Task SaveConfigAsync(object? parameter)
        {
            try
            {
                // تحديث قواعد البيانات المحددة
                var selectedDatabases = AvailableDatabases
                    .Where(db => db.IsSelected)
                    .Select(db => db.DatabaseName)
                    .ToList();

                _config.SelectedDatabases = selectedDatabases;

                // حفظ الإعدادات في AutoBackupService
                await _autoBackupService.UpdateConfigAsync(_config);

                // حفظ قواعد البيانات المحددة في الإعدادات الرئيسية أيضاً
                var mainSettings = SettingsManager.LoadSettings();
                mainSettings.SelectedDatabases = selectedDatabases;
                SettingsManager.SaveSettings(mainSettings);

                // تحديث النص المعروض
                OnPropertyChanged(nameof(SelectedDatabasesText));
                
                StatusText = "تم حفظ الإعدادات";
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل حفظ الإعدادات: {ex.Message}", ex);
                StatusText = $"فشل حفظ الإعدادات: {ex.Message}";
            }
        }

        private async Task LoadDatabasesAsync(object? parameter)
        {
            try
            {
                var databases = await _backupService.GetDatabasesAsync();
                
                // تحميل قواعد البيانات المحددة من الإعدادات الرئيسية
                var mainSettings = SettingsManager.LoadSettings();
                var selectedDatabases = mainSettings.SelectedDatabases ?? new List<string>();

                // تحديث _config بقواعد البيانات المحددة
                if (selectedDatabases.Any())
                {
                    _config.SelectedDatabases = selectedDatabases;
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableDatabases.Clear();
                    foreach (var db in databases)
                    {
                        AvailableDatabases.Add(new DatabaseSelectionViewModel
                        {
                            DatabaseName = db,
                            IsSelected = selectedDatabases.Contains(db) || _config.SelectedDatabases.Contains(db)
                        });
                    }
                    
                    // تحديث النص المعروض
                    OnPropertyChanged(nameof(SelectedDatabasesText));
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل تحميل قواعد البيانات: {ex.Message}", ex);
                StatusText = $"فشل تحميل قواعد البيانات: {ex.Message}";
            }
        }

        private void SelectGoogleCredentials(object? parameter)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "اختر ملف اعتماد Google Drive",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _config.GoogleDriveCredentialsPath = openFileDialog.FileName;
                OnPropertyChanged(nameof(Config));
            }
        }

        private async Task TestGoogleDriveAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري اختبار Google Drive...";
                
                // استخدام خدمة Google Drive من AutoBackupService بدلاً من إنشاء كائن جديد
                var googleDriveService = new GoogleDriveServiceFixed(_logger, _config.GoogleDriveCredentialsPath);

                if (await googleDriveService.InitializeAsync(_config.GoogleDriveCredentialsPath))
                {
                    // تفعيل رفع الملفات إلى Google Drive تلقائياً عند نجاح الاتصال
                    _config.UploadToGoogleDrive = true;
                    OnPropertyChanged(nameof(Config));
                    
                    // حفظ التغييرات
                    await _autoBackupService.UpdateConfigAsync(_config);
                    
                    StatusText = "تم الاتصال بـ Google Drive بنجاح وتم تفعيل الرفع التلقائي";
                    System.Windows.MessageBox.Show("تم الاتصال بـ Google Drive بنجاح!\nتم تفعيل الرفع التلقائي للنسخ الاحتياطية.", "نجح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // إيقاف رفع الملفات إلى Google Drive عند فشل الاتصال
                    _config.UploadToGoogleDrive = false;
                    OnPropertyChanged(nameof(Config));
                    
                    // حفظ التغييرات
                    await _autoBackupService.UpdateConfigAsync(_config);
                    
                    StatusText = "فشل الاتصال بـ Google Drive";
                    System.Windows.MessageBox.Show("فشل الاتصال بـ Google Drive. تحقق من ملف الاعتماد.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                // إيقاف رفع الملفات إلى Google Drive عند حدوث خطأ
                _config.UploadToGoogleDrive = false;
                OnPropertyChanged(nameof(Config));
                
                // حفظ التغييرات
                try
                {
                    await _autoBackupService.UpdateConfigAsync(_config);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError($"فشل حفظ إعدادات Google Drive: {saveEx.Message}", saveEx);
                }
                
                _logger.LogError($"فشل اختبار Google Drive: {ex.Message}", ex);
                StatusText = $"فشل اختبار Google Drive: {ex.Message}";
                System.Windows.MessageBox.Show($"فشل اختبار Google Drive: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnStatusChanged(object? sender, string status)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = status;
            });
        }

        private void OnBackupCompleted(object? sender, BackupScheduleEntry entry)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RecentBackups.Insert(0, entry);

                // الاحتفاظ بآخر 50 نسخة فقط
                while (RecentBackups.Count > 50)
                {
                    RecentBackups.RemoveAt(RecentBackups.Count - 1);
                }
            });
        }

        private void LoadRecentBackups()
        {
            var recentBackups = _autoBackupService.GetRecentBackups();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RecentBackups.Clear();
                foreach (var backup in recentBackups)
                {
                    RecentBackups.Add(backup);
                }
            });
        }

        /// <summary>
        /// تنظيف الموارد عند إغلاق النافذة
        /// </summary>
        ~AutoBackupViewModel()
        {
            if (_statusUpdateTimer != null)
            {
                _statusUpdateTimer.Stop();
                _statusUpdateTimer = null;
            }

            if (_autoBackupService != null)
            {
                _autoBackupService.StatusChanged -= OnStatusChanged;
                _autoBackupService.BackupCompleted -= OnBackupCompleted;
            }
        }
    }
}

using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BackupDbServer.ViewModels
{
    public class ScheduledBackupViewModel : ViewModelBase
    {
        private readonly IScheduledBackupService _scheduledBackupService;
        private readonly ILogger _logger;
        private readonly WindowsServiceManager _windowsServiceManager;
        private ScheduledBackupConfig _config = new();
        private string _statusText = "متوقف";
        private bool _isServiceRunning;
        private bool _isWindowsServiceInstalled;
        private string _windowsServiceStatus = "غير مثبتة";
        private ObservableCollection<ScheduledBackupHistoryItem> _backupHistory = new();

        public ScheduledBackupConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                OnPropertyChanged();
                UpdateDaySelections();
                OnPropertyChanged(nameof(NextBackupText));
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public bool IsServiceRunning
        {
            get => _isServiceRunning;
            set
            {
                _isServiceRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartService));
            }
        }

        public bool IsWindowsServiceInstalled
        {
            get => _isWindowsServiceInstalled;
            set
            {
                _isWindowsServiceInstalled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanInstallService));
                OnPropertyChanged(nameof(CanUninstallService));
                OnPropertyChanged(nameof(CanStartWindowsService));
                OnPropertyChanged(nameof(CanStopWindowsService));
            }
        }

        public string WindowsServiceStatus
        {
            get => _windowsServiceStatus;
            set
            {
                _windowsServiceStatus = value;
                OnPropertyChanged();
            }
        }

        public bool CanStartService => !IsServiceRunning && Config.IsEnabled;
        public bool CanInstallService => !IsWindowsServiceInstalled;
        public bool CanUninstallService => IsWindowsServiceInstalled;
        public bool CanStartWindowsService => IsWindowsServiceInstalled && WindowsServiceStatus == "متوقفة";
        public bool CanStopWindowsService => IsWindowsServiceInstalled && WindowsServiceStatus == "تعمل";

        public ObservableCollection<ScheduledBackupHistoryItem> BackupHistory
        {
            get => _backupHistory;
            set
            {
                _backupHistory = value;
                OnPropertyChanged();
            }
        }

        public string NextBackupText
        {
            get
            {
                if (!Config.IsEnabled)
                    return "الجدولة التلقائية غير مفعلة";
                    
                if (!Config.ScheduledDays.Any())
                    return "لم يتم تحديد أيام للجدولة";
                    
                var nextTime = _scheduledBackupService?.GetNextBackupTime();
                if (nextTime.HasValue)
                {
                    var timeSpan = nextTime.Value - DateTime.Now;
                    if (timeSpan.TotalHours < 24)
                    {
                        return $"النسخ الاحتياطي التالي: اليوم في {nextTime.Value:HH:mm}";
                    }
                    else
                    {
                        return $"النسخ الاحتياطي التالي: {nextTime.Value:yyyy-MM-dd HH:mm}";
                    }
                }
                return "النسخ الاحتياطي التالي: غير محدد";
            }
        }

        public string LastBackupText
        {
            get
            {
                return Config.LastBackupTime?.ToString("آخر نسخ احتياطي: yyyy-MM-dd HH:mm") ?? "لم يتم تنفيذ نسخ احتياطي بعد";
            }
        }

        public bool IsNotificationEnabled => Config.NotifyOnCompletion || Config.NotifyOnError;

        // خصائص أيام الأسبوع
        public bool IsSundaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Sunday);
            set => UpdateDaySelection(DayOfWeek.Sunday, value);
        }

        public bool IsMondaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Monday);
            set => UpdateDaySelection(DayOfWeek.Monday, value);
        }

        public bool IsTuesdaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Tuesday);
            set => UpdateDaySelection(DayOfWeek.Tuesday, value);
        }

        public bool IsWednesdaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Wednesday);
            set => UpdateDaySelection(DayOfWeek.Wednesday, value);
        }

        public bool IsThursdaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Thursday);
            set => UpdateDaySelection(DayOfWeek.Thursday, value);
        }

        public bool IsFridaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Friday);
            set => UpdateDaySelection(DayOfWeek.Friday, value);
        }

        public bool IsSaturdaySelected
        {
            get => Config.ScheduledDays.Contains(DayOfWeek.Saturday);
            set => UpdateDaySelection(DayOfWeek.Saturday, value);
        }

        public int SelectedHour
        {
            get => Config.ScheduledTime.Hour;
            set
            {
                Config.ScheduledTime = new TimeOnly(value, Config.ScheduledTime.Minute);
                OnPropertyChanged();
                OnPropertyChanged(nameof(NextBackupText));
                OnPropertyChanged(nameof(DisplayScheduledTime));
            }
        }

        public int SelectedMinute
        {
            get => Config.ScheduledTime.Minute;
            set
            {
                Config.ScheduledTime = new TimeOnly(Config.ScheduledTime.Hour, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(NextBackupText));
                OnPropertyChanged(nameof(DisplayScheduledTime));
            }
        }

        public string DisplayScheduledTime
        {
            get => Config.ScheduledTime.ToString("HH:mm");
        }

        // الأوامر الأصلية
        public ICommand SaveCommand { get; }
        public ICommand StartServiceCommand { get; }
        public ICommand StopServiceCommand { get; }
        public ICommand RunBackupNowCommand { get; }
        public ICommand RefreshLogCommand { get; }
        public ICommand ClearLogCommand { get; }

        // أوامر Windows Service
        public ICommand InstallWindowsServiceCommand { get; }
        public ICommand UninstallWindowsServiceCommand { get; }
        public ICommand StartWindowsServiceCommand { get; }
        public ICommand StopWindowsServiceCommand { get; }
        public ICommand RestartWindowsServiceCommand { get; }
        public ICommand RefreshServiceStatusCommand { get; }

        public ScheduledBackupViewModel() : this(null)
        {
        }

        public ScheduledBackupViewModel(IScheduledBackupService? scheduledBackupService)
        {
            // استخدام ServiceContainer بدلاً من إنشاء instances جديدة
            _logger = ServiceContainer.Logger;
            _scheduledBackupService = scheduledBackupService ?? ServiceContainer.ScheduledBackupService;
            _windowsServiceManager = new WindowsServiceManager();
            
            // تحديث الحالة الأولية
            if (Config.IsEnabled && Config.NextBackupTime.HasValue)
                StatusText = $"الجدولة نشطة - التالي: {Config.NextBackupTime.Value:yyyy-MM-dd HH:mm}";
            else
                StatusText = "الجدولة غير نشطة";
            // تهيئة الأوامر الأصلية
            SaveCommand = new AsyncRelayCommand(SaveConfigAsync);
            StartServiceCommand = new AsyncRelayCommand(StartServiceAsync, CanExecuteStartService);
            StopServiceCommand = new AsyncRelayCommand(StopServiceAsync, CanExecuteStopService);
            RunBackupNowCommand = new AsyncRelayCommand(RunBackupNowAsync);
            RefreshLogCommand = new RelayCommand(RefreshLog);
            ClearLogCommand = new RelayCommand(ClearLog);

            // تهيئة أوامر Windows Service
            InstallWindowsServiceCommand = new AsyncRelayCommand(InstallWindowsServiceAsync);
            UninstallWindowsServiceCommand = new AsyncRelayCommand(UninstallWindowsServiceAsync);
            StartWindowsServiceCommand = new AsyncRelayCommand(StartWindowsServiceAsync);
            StopWindowsServiceCommand = new AsyncRelayCommand(StopWindowsServiceAsync);
            RestartWindowsServiceCommand = new AsyncRelayCommand(RestartWindowsServiceAsync);
            RefreshServiceStatusCommand = new AsyncRelayCommand(RefreshServiceStatusAsync);

            // تحميل الإعدادات
            Config = _scheduledBackupService.Config;
            IsServiceRunning = _scheduledBackupService.IsRunning;

            // الاشتراك في الأحداث
            _scheduledBackupService.StatusChanged += OnStatusChanged;
            _scheduledBackupService.BackupCompleted += OnBackupCompleted;

            LoadBackupHistory();
            
            // تحديث حالة Windows Service
            _ = RefreshServiceStatusAsync(null);
        }

        // Windows Service Methods
        private async Task InstallWindowsServiceAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري تثبيت خدمة Windows...";
                
                var result = await _windowsServiceManager.InstallServiceAsync();
                
                if (result)
                {
                    System.Windows.MessageBox.Show(
                        "تم تثبيت خدمة Windows بنجاح!\nيمكنك الآن تشغيل النسخ الاحتياطي كخدمة تعمل في الخلفية.", 
                        "نجح التثبيت", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "فشل في تثبيت خدمة Windows.\nتأكد من تشغيل البرنامج كمدير.", 
                        "فشل التثبيت", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                
                await RefreshServiceStatusAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تثبيت خدمة Windows: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"خطأ في تثبيت الخدمة: {ex.Message}", 
                    "خطأ", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            finally
            {
                StatusText = "جاهز";
            }
        }

        private async Task UninstallWindowsServiceAsync(object? parameter)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "هل أنت متأكد من إلغاء تثبيت خدمة Windows؟\nسيتم إيقاف الخدمة أولاً ثم حذفها من النظام.", 
                    "تأكيد الإلغاء", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    StatusText = "جاري إلغاء تثبيت خدمة Windows...";
                    
                    var success = await _windowsServiceManager.UninstallServiceAsync();
                    
                    if (success)
                    {
                        System.Windows.MessageBox.Show(
                            "تم إلغاء تثبيت خدمة Windows بنجاح.", 
                            "نجح الإلغاء", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "فشل في إلغاء تثبيت خدمة Windows.", 
                            "فشل الإلغاء", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                    }
                    
                    await RefreshServiceStatusAsync(null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في إلغاء تثبيت خدمة Windows: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"خطأ في إلغاء تثبيت الخدمة: {ex.Message}", 
                    "خطأ", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            finally
            {
                StatusText = "جاهز";
            }
        }

        private async Task StartWindowsServiceAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري تشغيل خدمة Windows...";
                
                var result = await _windowsServiceManager.StartServiceAsync();
                
                if (result)
                {
                    System.Windows.MessageBox.Show(
                        "تم تشغيل خدمة Windows بنجاح!", 
                        "نجح التشغيل", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "فشل في تشغيل خدمة Windows.", 
                        "فشل التشغيل", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                
                await RefreshServiceStatusAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تشغيل خدمة Windows: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"خطأ في تشغيل الخدمة: {ex.Message}", 
                    "خطأ", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            finally
            {
                StatusText = "جاهز";
            }
        }

        private async Task StopWindowsServiceAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري إيقاف خدمة Windows...";
                
                var result = await _windowsServiceManager.StopServiceAsync();
                
                if (result)
                {
                    System.Windows.MessageBox.Show(
                        "تم إيقاف خدمة Windows بنجاح!", 
                        "نجح الإيقاف", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "فشل في إيقاف خدمة Windows.", 
                        "فشل الإيقاف", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                
                await RefreshServiceStatusAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في إيقاف خدمة Windows: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"خطأ في إيقاف الخدمة: {ex.Message}", 
                    "خطأ", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            finally
            {
                StatusText = "جاهز";
            }
        }

        private async Task RestartWindowsServiceAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري إعادة تشغيل خدمة Windows...";
                
                var result = await _windowsServiceManager.RestartServiceAsync();
                
                if (result)
                {
                    System.Windows.MessageBox.Show(
                        "تم إعادة تشغيل خدمة Windows بنجاح!", 
                        "نجحت إعادة التشغيل", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "فشل في إعادة تشغيل خدمة Windows.", 
                        "فشلت إعادة التشغيل", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                
                await RefreshServiceStatusAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في إعادة تشغيل خدمة Windows: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"خطأ في إعادة تشغيل الخدمة: {ex.Message}", 
                    "خطأ", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            finally
            {
                StatusText = "جاهز";
            }
        }

        private async Task RefreshServiceStatusAsync(object? parameter)
        {
            try
            {
                IsWindowsServiceInstalled = _windowsServiceManager.IsServiceInstalled();
                WindowsServiceStatus = _windowsServiceManager.GetServiceStatusText();
                
                OnPropertyChanged(nameof(CanInstallService));
                OnPropertyChanged(nameof(CanUninstallService));
                OnPropertyChanged(nameof(CanStartWindowsService));
                OnPropertyChanged(nameof(CanStopWindowsService));
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحديث حالة الخدمة: {ex.Message}", ex);
                WindowsServiceStatus = "خطأ في التحديث";
            }
            
            await Task.CompletedTask;
        }

        // باقي الطرق الأصلية...
        public async Task SaveConfigAsync(object? parameter)
        {
            try
            {
                // تحديث حالة IsRuning في الكونفيغ
                Config.IsRuning = IsServiceRunning;
                
                await _scheduledBackupService.UpdateConfigAsync(Config);
                System.Windows.MessageBox.Show("تم حفظ الإعدادات بنجاح", "نجح الحفظ", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
                OnPropertyChanged(nameof(NextBackupText));
                OnPropertyChanged(nameof(CanStartService));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في حفظ الإعدادات: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartServiceAsync(object? parameter)
        {
            try
            {
                await _scheduledBackupService.StartAsync();
                IsServiceRunning = _scheduledBackupService.IsRunning;
                Config.IsRuning = _scheduledBackupService.IsRunning;
                // حفظ الحالة الجديدة
                await _scheduledBackupService.SaveConfigAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في تشغيل الخدمة: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StopServiceAsync(object? parameter)
        {
            try
            {
                await _scheduledBackupService.StopAsync();
                IsServiceRunning = _scheduledBackupService.IsRunning;
                Config.IsRuning = _scheduledBackupService.IsRunning;
                // حفظ الحالة الجديدة
                await _scheduledBackupService.SaveConfigAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في إيقاف الخدمة: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunBackupNowAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري تنفيذ النسخ الاحتياطي...";
                var result = await _scheduledBackupService.RunBackupNowAsync();
                
                var message = result.IsSuccess 
                    ? $"تم النسخ الاحتياطي بنجاح\nقواعد البيانات: {result.BackedUpDatabases.Count}\nالوقت المستغرق: {result.Duration:mm\\:ss}"
                    : $"فشل النسخ الاحتياطي: {result.ErrorMessage}";
                
                System.Windows.MessageBox.Show(message, result.IsSuccess ? "نجح النسخ" : "فشل النسخ", 
                               MessageBoxButton.OK, result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ أثناء النسخ الاحتياطي: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteStartService(object? parameter) => CanStartService;
        private bool CanExecuteStopService(object? parameter) => IsServiceRunning;

        private void RefreshLog(object? parameter)
        {
            LoadBackupHistory();
        }

        private void ClearLog(object? parameter)
        {
            if (System.Windows.MessageBox.Show("هل تريد مسح سجل العمليات؟", "تأكيد", 
                               MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                BackupHistory.Clear();
            }
        }

        private void UpdateDaySelection(DayOfWeek day, bool isSelected)
        {
            if (isSelected && !Config.ScheduledDays.Contains(day))
            {
                Config.ScheduledDays.Add(day);
            }
            else if (!isSelected && Config.ScheduledDays.Contains(day))
            {
                Config.ScheduledDays.Remove(day);
            }
            OnPropertyChanged(nameof(NextBackupText));
        }

        private void UpdateDaySelections()
        {
            OnPropertyChanged(nameof(IsSundaySelected));
            OnPropertyChanged(nameof(IsMondaySelected));
            OnPropertyChanged(nameof(IsTuesdaySelected));
            OnPropertyChanged(nameof(IsWednesdaySelected));
            OnPropertyChanged(nameof(IsThursdaySelected));
            OnPropertyChanged(nameof(IsFridaySelected));
            OnPropertyChanged(nameof(IsSaturdaySelected));
        }

        private void OnStatusChanged(object? sender, string status)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = status;
            });
        }

        private void OnBackupCompleted(object? sender, ScheduledBackupResult result)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var historyItem = new ScheduledBackupHistoryItem
                {
                    StartTime = result.StartTime,
                    IsSuccess = result.IsSuccess,
                    DatabasesText = string.Join(", ", result.BackedUpDatabases),
                    DurationText = result.Duration.ToString(@"mm\:ss"),
                    ErrorMessage = result.ErrorMessage
                };
                
                BackupHistory.Insert(0, historyItem);
                
                // الاحتفاظ بـ 100 عنصر فقط
                while (BackupHistory.Count > 100)
                {
                    BackupHistory.RemoveAt(BackupHistory.Count - 1);
                }
                
                OnPropertyChanged(nameof(LastBackupText));
            });
        }

        private void LoadBackupHistory()
        {
            // TODO: تحميل سجل العمليات من ملف أو قاعدة بيانات
        }
    }

    public class ScheduledBackupHistoryItem
    {
        public DateTime StartTime { get; set; }
        public bool IsSuccess { get; set; }
        public string DatabasesText { get; set; } = string.Empty;
        public string DurationText { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string StatusText => IsSuccess ? "نجح" : "فشل";
    }
}
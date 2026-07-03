using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Services;
using BackupDbServer.Service;
using BackupDbServer.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System;
using System.IO;
using Microsoft.Win32;
using System.Collections.Generic;

namespace BackupDbServer.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly BackupRestoreService _backupService;
        private ObservableCollection<DatabaseSelectionViewModel> _databases;
        private string _statusText = "جاهز";
        private bool _isBusy;

        private IAutoBackupService _autoBackupService;
        private ILogger _logger;
        private readonly DockerSqlServerManager _dockerManager;
        private readonly IScheduledBackupService _scheduledBackupService;
        private readonly LogService _logService;
        private DailyTaskService _dailyTaskService;
        private System.Windows.Threading.DispatcherTimer _progressUpdateTimer;
        private TrayIconManager? _trayIconManager;

        // ViewModel صيانة وتسريع IIS (مُستضاف في تبويب «إدارة IIS»)
        public IisMaintenanceViewModel IisMaintenance { get; } = new IisMaintenanceViewModel();

        // إضافة instance من Settings
        private Settings _settings;
        public Settings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DatabaseSelectionViewModel> Databases
        {
            get => _databases;
            set { _databases = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private string _iisSourcePath = string.Empty;
        public string IisSourcePath
        {
            get => _iisSourcePath;
            set { _iisSourcePath = value; OnPropertyChanged(); }
        }

        private string _iisDestinationPath = string.Empty;
        public string IisDestinationPath
        {
            get => _iisDestinationPath;
            set { _iisDestinationPath = value; OnPropertyChanged(); }
        }

        private string _iisSiteName = string.Empty;
        public string IisSiteName
        {
            get => _iisSiteName;
            set { _iisSiteName = value; OnPropertyChanged(); }
        }

        private string _iisAppName = string.Empty;
        public string IisAppName
        {
            get => _iisAppName;
            set { _iisAppName = value; OnPropertyChanged(); }
        }

        private string _iisPhysicalPath = string.Empty;
        public string IisPhysicalPath
        {
            get => _iisPhysicalPath;
            set { _iisPhysicalPath = value; OnPropertyChanged(); }
        }

        private string _settingsFilePath = string.Empty;
        public string SettingsFilePath
        {
            get => _settingsFilePath;
            set { _settingsFilePath = value; OnPropertyChanged(); }
        }

        private string _settingsKey = string.Empty;
        public string SettingsKey
        {
            get => _settingsKey;
            set { _settingsKey = value; OnPropertyChanged(); }
        }

        private string _settingsValue = string.Empty;
        public string SettingsValue
        {
            get => _settingsValue;
            set { _settingsValue = value; OnPropertyChanged(); }
        }

        private string _newDbOperations = string.Empty;
        public string NewDbOperations
        {
            get => _newDbOperations;
            set { _newDbOperations = value; OnPropertyChanged(); }
        }

        // خصائص نوع الاتصال
        public bool IsLocalConnection
        {
            get => Settings.ConnectionType == ConnectionType.Local;
            set
            {
                if (value) Settings.ConnectionType = ConnectionType.Local;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDockerConnection));
                OnPropertyChanged(nameof(IsRemoteConnection));
            }
        }

        public bool IsDockerConnection
        {
            get => Settings.ConnectionType == ConnectionType.Docker;
            set
            {
                if (value) Settings.ConnectionType = ConnectionType.Docker;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocalConnection));
                OnPropertyChanged(nameof(IsRemoteConnection));
            }
        }

        public bool IsRemoteConnection
        {
            get => Settings.ConnectionType == ConnectionType.Remote;
            set
            {
                if (value) Settings.ConnectionType = ConnectionType.Remote;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocalConnection));
                OnPropertyChanged(nameof(IsDockerConnection));
            }
        }

        // خصائص Docker
        public string DockerContainerName
        {
            get => Settings.DockerContainerName;
            set
            {
                Settings.DockerContainerName = value;
                OnPropertyChanged();
            }
        }

        public int DockerPort
        {
            get => Settings.Port;
            set
            {
                Settings.Port = value;
                OnPropertyChanged();
            }
        }

        private string _dockerStatus = "غير معروف";
        public string DockerStatus
        {
            get => _dockerStatus;
            set
            {
                _dockerStatus = value;
                OnPropertyChanged();
            }
        }

        private string _connectionStatus = "غير متصل";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus = value;
                OnPropertyChanged();
            }
        }

        // خصائص إضافية للإعدادات
        public string ServerName
        {
            get => Settings.ServerName;
            set
            {
                Settings.ServerName = value;
                OnPropertyChanged();
            }
        }

        public string Username
        {
            get => Settings.Username;
            set
            {
                Settings.Username = value;
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get => Settings.Password;
            set
            {
                Settings.Password = value;
                OnPropertyChanged();
            }
        }

        public bool UseWindowsAuthentication
        {
            get => Settings.UseWindowsAuthentication;
            set
            {
                Settings.UseWindowsAuthentication = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseSqlAuthentication));
            }
        }

        public bool UseSqlAuthentication
        {
            get => !Settings.UseWindowsAuthentication;
            set
            {
                Settings.UseWindowsAuthentication = !value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseWindowsAuthentication));
            }
        }

        public bool UseCustomPort
        {
            get => Settings.UseCustomPort;
            set
            {
                Settings.UseCustomPort = value;
                OnPropertyChanged();
            }
        }

        public int Port
        {
            get => Settings.Port;
            set
            {
                Settings.Port = value;
                OnPropertyChanged();
            }
        }

        public int ConnectionTimeout
        {
            get => Settings.ConnectionTimeout;
            set
            {
                Settings.ConnectionTimeout = value;
                OnPropertyChanged();
            }
        }

        public bool TrustServerCertificate
        {
            get => Settings.TrustServerCertificate;
            set
            {
                Settings.TrustServerCertificate = value;
                OnPropertyChanged();
            }
        }

        // خصائص تقدم العمليات
        private OperationProgress _currentOperation = new OperationProgress();
        public OperationProgress CurrentOperation
        {
            get => _currentOperation;
            set
            {
                _currentOperation = value;
                OnPropertyChanged();
            }
        }

        private ObservableCollection<OperationProgress> _operationHistory = new ObservableCollection<OperationProgress>();
        public ObservableCollection<OperationProgress> OperationHistory
        {
            get => _operationHistory;
            set
            {
                _operationHistory = value;
                OnPropertyChanged();
            }
        }

        // خصائص إحصائيات التقدم
        public string CurrentOperationDisplay => CurrentOperation.IsRunning ? $"{CurrentOperation.OperationName} - {CurrentOperation.CurrentStep}" : "لا توجد عمليات جارية";
        public string ProgressDisplay => CurrentOperation.IsRunning ? $"{CurrentOperation.ProgressPercentage}%" : "0%";
        public string TimeElapsedDisplay => CurrentOperation.IsRunning ? CurrentOperation.ElapsedTime.ToString(@"hh\:mm\:ss") : "00:00:00";
        public string EstimatedTimeDisplay => CurrentOperation.IsRunning ? CurrentOperation.EstimatedTimeRemainingText : "غير محدد";
        
        // إحصائيات سجل العمليات
        public int CompletedOperationsCount => OperationHistory.Count;
        public int SuccessfulOperationsFromHistory => OperationHistory.Count(op => op.Status.Contains("نجح") || op.Status.Contains("مكتمل"));
        public int FailedOperationsFromHistory => OperationHistory.Count(op => op.Status.Contains("فشل") || op.Status.Contains("خطأ"));
        public int RunningOperationsCount => CurrentOperation.IsRunning ? 1 : 0;

        public bool Encrypt
        {
            get => Settings.Encrypt;
            set
            {
                Settings.Encrypt = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadDatabasesCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand RunBackupCommand { get; }
        public ICommand SaveSelectionCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand UnselectAllCommand { get; }
        public ICommand ExecuteSqlForAllCommand { get; }
        public ICommand EnableMaintenanceAllCommand { get; }
        public ICommand DisableMaintenanceAllCommand { get; }
        public ICommand RefreshMaintenanceAllCommand { get; }
        public ICommand? CopyDatabaseCommand { get; }
        public ICommand RunOperationsOnNewDbCommand { get; }
        public ICommand BrowseIisSourceCommand { get; }
        public ICommand BrowseIisDestinationCommand { get; }
        public ICommand CopyIisFolderCommand { get; }
        public ICommand CreateVirtualAppCommand { get; }
        public ICommand BrowseSettingsFileCommand { get; }
        public ICommand UpdateSettingsCommand { get; }
        public ICommand OpenCopyDatabaseCommand { get; }
        public ICommand OpenAutoBackupCommand { get; }
        public ICommand OpenScheduledBackupCommand { get; }
        public ICommand OpenAutoDeployCommand { get; }
        public ICommand OpenDailyTasksCommand { get; }
        public ICommand CheckDockerStatusCommand { get; }
        public ICommand CreateDockerContainerCommand { get; }
        
        // أوامر السجل
        public ICommand RefreshLogCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ExportLogCommand { get; }
        public ICommand ApplyFilterCommand { get; }

        // خصائص السجل
        public ObservableCollection<LogEntry> LogEntries => _logService.LogEntries;
        
        public int TotalOperationsCount => _logService.GetTotalOperationsCount();
        public int SuccessfulOperationsCount => _logService.GetSuccessfulOperationsCount();
        public int FailedOperationsCount => _logService.GetFailedOperationsCount();
        public int RunningOperationsCountFromLog => _logService.GetRunningOperationsCount();
        public string LastOperationTime => _logService.GetLastOperationTime()?.ToString("yyyy-MM-dd HH:mm:ss") ?? "لا توجد عمليات";

        // خصائص الفلترة
        public List<string> OperationTypes { get; } = new List<string> 
        { 
            "الكل", "نسخ احتياطي", "استعادة", "نسخ تلقائي", "اختبار اتصال", "تحديث قواعد البيانات" 
        };
        
        public List<string> StatusTypes { get; } = new List<string> 
        { 
            "الكل", "نجح", "فشل", "جاري" 
        };

        private string _selectedOperationType = "الكل";
        public string SelectedOperationType
        {
            get => _selectedOperationType;
            set { _selectedOperationType = value; OnPropertyChanged(); }
        }

        private string _selectedStatus = "الكل";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set { _selectedStatus = value; OnPropertyChanged(); }
        }

        private bool _showTodayOnly = false;
        public bool ShowTodayOnly
        {
            get => _showTodayOnly;
            set { _showTodayOnly = value; OnPropertyChanged(); }
        }

        private bool _showSuccessOnly = false;
        public bool ShowSuccessOnly
        {
            get => _showSuccessOnly;
            set { _showSuccessOnly = value; OnPropertyChanged(); }
        }

        private bool _showFailedOnly = false;
        public bool ShowFailedOnly
        {
            get => _showFailedOnly;
            set { _showFailedOnly = value; OnPropertyChanged(); }
        }
        public ICommand StartDockerContainerCommand { get; }
        public ICommand StopDockerContainerCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public MainViewModel()
        {
            // استخدام ServiceContainer بدلاً من إنشاء instances جديدة
            _logger = ServiceContainer.Logger;
            _backupService = ServiceContainer.BackupService;
            _databases = new ObservableCollection<DatabaseSelectionViewModel>();
            _dockerManager = new DockerSqlServerManager(_logger);
            _logService = new LogService(); // إضافة خدمة السجل

            // الحصول على خدمة الجدولة من ServiceContainer
            _scheduledBackupService = ServiceContainer.ScheduledBackupService;

            // تحميل الإعدادات
            _settings = _backupService._settings;

            LoadDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            RunBackupCommand = new AsyncRelayCommand(RunBackupAsync, CanExecuteOperation);
            SaveSelectionCommand = new RelayCommand(SaveSelection, CanExecuteOperation);
            RestoreCommand = new AsyncRelayCommand(RunRestoreAsync, CanExecuteOperation);
            SelectAllCommand = new RelayCommand(SelectAll, CanExecuteOperation);
            UnselectAllCommand = new RelayCommand(UnselectAll, CanExecuteOperation);

            OpenCopyDatabaseCommand = new RelayCommand(OpenCopyDatabase, CanExecuteOperation);

            ExecuteSqlForAllCommand = new AsyncRelayCommand(ExecuteSqlForAllAsync, CanExecuteOperation);
            EnableMaintenanceAllCommand = new AsyncRelayCommand(_ => ApplyMaintenanceToAllAsync(true), CanExecuteOperation);
            DisableMaintenanceAllCommand = new AsyncRelayCommand(_ => ApplyMaintenanceToAllAsync(false), CanExecuteOperation);
            RefreshMaintenanceAllCommand = new AsyncRelayCommand(_ => RefreshMaintenanceAllAsync(), CanExecuteOperation);
            RunOperationsOnNewDbCommand = new AsyncRelayCommand(RunOperationsOnNewDbAsync, CanExecuteOperation);
            BrowseIisSourceCommand = new RelayCommand(BrowseIisSource);
            BrowseIisDestinationCommand = new RelayCommand(BrowseIisDestination);
            CopyIisFolderCommand = new RelayCommand(CopyIisFolder);
            CreateVirtualAppCommand = new RelayCommand(CreateVirtualApp);
            BrowseSettingsFileCommand = new RelayCommand(BrowseSettingsFile);
            UpdateSettingsCommand = new RelayCommand(UpdateSettings);
            OpenAutoDeployCommand = new RelayCommand(OpenAutoDeploy);
            OpenDailyTasksCommand = new RelayCommand(OpenDailyTasks);
            OpenScheduledBackupCommand = new RelayCommand(OpenScheduledBackup);
            
            // أوامر السجل
            RefreshLogCommand = new AsyncRelayCommand(RefreshLogAsync);
            ClearLogCommand = new RelayCommand(ClearLog);
            ExportLogCommand = new AsyncRelayCommand(ExportLogAsync);
            ApplyFilterCommand = new RelayCommand(ApplyFilter);

            // الحصول على خدمة النسخ التلقائي من ServiceContainer
            _autoBackupService = ServiceContainer.AutoBackupService;
            OpenAutoBackupCommand = new RelayCommand(OpenAutoBackup, CanExecuteOperation);

            // تشغيل الخدمة التلقائية إذا كانت مفعلة في الإعدادات
            _ = StartAutoBackupServiceIfEnabledAsync();
            
            // الحصول على خدمة المهام اليومية من ServiceContainer
            _dailyTaskService = ServiceContainer.DailyTaskService;
            
            // إضافة سجل لبدء التطبيق
            LogEntry("تطبيق", "نجح", "", "تم بدء تشغيل نظام النسخ الاحتياطي لقواعد البيانات");

            // تهيئة أوامر Docker
            CheckDockerStatusCommand = new AsyncRelayCommand(CheckDockerStatusAsync);
            CreateDockerContainerCommand = new AsyncRelayCommand(CreateDockerContainerAsync);
            StartDockerContainerCommand = new AsyncRelayCommand(StartDockerContainerAsync);
            StopDockerContainerCommand = new AsyncRelayCommand(StopDockerContainerAsync);

            // أوامر الإعدادات الجديدة
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync);
            SaveSettingsCommand = new RelayCommand(SaveSettingsMethod);

            // تشغيل timer لتحديث خصائص تقدم العمليات كل ثانية
            _progressUpdateTimer = new System.Windows.Threading.DispatcherTimer();
            _progressUpdateTimer.Interval = TimeSpan.FromSeconds(1);
            _progressUpdateTimer.Tick += UpdateProgressDisplay;
            _progressUpdateTimer.Start();

            // فحص حالة Docker عند التشغيل
            _ = CheckDockerStatusAsync(null);
        }

        // طريقة مساعدة لتحميل أو إنشاء الإعدادات
        private Settings LoadOrCreateSettings()
        {
            try
            {
                // محاولة تحميل الإعدادات الحالية
                var backupConfig = SettingsManager.LoadSettings();
                
                // تحويل BackupConfig إلى Settings - استخدام الخصائص الموجودة فقط
                return new Settings
                {
                    // استخدام الخصائص الموجودة في BackupConfig أو قيم افتراضية
                    ServerName = GetPropertySafely(() => backupConfig.Server, "localhost"),
                    Username = GetPropertySafely(() => backupConfig.Username, ""),
                    Password = GetPropertySafely(() => backupConfig.Password, ""),
                    UseWindowsAuthentication = GetPropertySafely(() => backupConfig.UseWindowsAuthentication, true),
                    BackupPath = GetPropertySafely(() => backupConfig.BackupPath, @"C:\Backups"),
                    //LogPath = GetPropertySafely(() => backupConfig.LogPath, @"C:\Logs"),
                    SelectedDatabases = backupConfig.SelectedDatabases ?? new System.Collections.Generic.List<string>(),
                    
                    // إعدادات افتراضية للمعاملات الجديدة
                    ConnectionType = ConnectionType.Local,
                    DockerContainerName = "dbs-2022-CTP23-1r",
                    Port = 1433,
                    UseCustomPort = false,
                    ConnectionTimeout = 30,
                    TrustServerCertificate = true,
                    Encrypt = false
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError($"خطأ في تحميل الإعدادات: {ex.Message}", ex);
                
                // إرجاع إعدادات افتراضية في حالة الخطأ
                return new Settings();
            }
        }

        // طريقة مساعدة للحصول على خاصية بأمان
        private T GetPropertySafely<T>(Func<T> propertyGetter, T defaultValue)
        {
            try
            {
                return propertyGetter();
            }
            catch
            {
                return defaultValue;
            }
        }

        // طريقة لحفظ Settings كـ BackupConfig
        private void SaveCurrentSettings()
        {
            try
            {
                var backupConfig = SettingsManager.LoadSettings();
                
                // تحديث الخصائص الموجودة فقط
                SetPropertySafely(() => backupConfig.Server = Settings.ServerName);
                SetPropertySafely(() => backupConfig.Username = Settings.Username);
                SetPropertySafely(() => backupConfig.Password = Settings.Password);
                SetPropertySafely(() => backupConfig.UseWindowsAuthentication = Settings.UseWindowsAuthentication);
                SetPropertySafely(() => backupConfig.BackupPath = Settings.BackupPath);
               // SetPropertySafely(() => backupConfig.LogPath = Settings.LogPath);
                backupConfig.SelectedDatabases = Settings.SelectedDatabases;
                
                SettingsManager.SaveSettings(backupConfig);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"خطأ في حفظ الإعدادات: {ex.Message}", ex);
            }
        }

        // طريقة مساعدة لتعيين خاصية بأمان
        private void SetPropertySafely(Action propertySetter)
        {
            try
            {
                propertySetter();
            }
            catch
            {
                // تجاهل الأخطاء في حالة عدم وجود الخاصية
            }
        }

        private bool CanExecuteOperation(object? parameter) => !IsBusy;

        private async Task LoadDatabasesAsync(object? parameter)
        {
            // بدء تقدم العمليات فوراً عند الضغط على الزر
            StartOperation("تحميل قائمة قواعد البيانات", "الاتصال بالخادم...");
            
            IsBusy = true;
            var startTime = DateTime.Now;
            
            try
            {
                UpdateOperationProgress("جاري الاتصال بقاعدة البيانات...", 10);
                
                // delay قصير لإظهار التقدم
                await Task.Delay(300);
                
                var dbNames = await _backupService.GetDatabasesAsync();
                var backupConfig = SettingsManager.LoadSettings();
                var savedSelection = backupConfig.SelectedDatabases ?? new System.Collections.Generic.List<string>();

                UpdateOperationProgress("جاري تحميل قائمة قواعد البيانات...", 50);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Databases.Clear();
                    foreach (var dbName in dbNames)
                    {
                        var dbViewModel = new DatabaseSelectionViewModel { DatabaseName = dbName, IsSelected = savedSelection.Contains(dbName) };
                        WireMaintenanceCommands(dbViewModel);
                        Databases.Add(dbViewModel);
                    }
                });
                
                UpdateOperationProgress("تم تحميل قائمة قواعد البيانات بنجاح", 100);
                
                var duration = (DateTime.Now - startTime).ToString(@"mm\:ss");
                StatusText = $"تم تحميل {Databases.Count} قاعدة بيانات بنجاح.";
                
                // إكمال العملية بنجاح
                CompleteOperation("مكتمل بنجاح");
                
                LogEntry("تحديث قواعد البيانات", "نجح", "", $"تم تحميل {Databases.Count} قاعدة بيانات", duration);
            }
            catch (Exception ex)
            {
                var duration = (DateTime.Now - startTime).ToString(@"mm\:ss");
                StatusText = "خطأ في الاتصال بالسيرفر. يرجى التحقق من الإعدادات.";
                
                // إكمال العملية بفشل
                CompleteOperation($"فشل: {ex.Message}");
                
                System.Windows.MessageBox.Show($"فشل تحميل قواعد البيانات: {ex.Message}", "خطأ في الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                LogEntry("تحديث قواعد البيانات", "فشل", "", $"فشل تحميل قواعد البيانات: {ex.Message}", duration);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ===== صيانة XmlPath لكل قاعدة بيانات =====

        /// <summary>ربط أوامر صيانة XmlPath بعنصر القاعدة.</summary>
        private void WireMaintenanceCommands(DatabaseSelectionViewModel db)
        {
            db.EnableMaintenanceCommand = new AsyncRelayCommand(_ => ToggleMaintenanceAsync(db, true));
            db.DisableMaintenanceCommand = new AsyncRelayCommand(_ => ToggleMaintenanceAsync(db, false));
            db.RefreshMaintenanceCommand = new AsyncRelayCommand(_ => RefreshMaintenanceStateAsync(db));
        }

        /// <summary>تفعيل/استرجاع صيانة XmlPath لقاعدة محددة مع تأكيد المستخدم.</summary>
        private async Task ToggleMaintenanceAsync(DatabaseSelectionViewModel db, bool enable)
        {
            var action = enable ? "إيقاف عمل XML للصيانة" : "استرجاع عمل XML";
            var confirm = System.Windows.MessageBox.Show(
                $"هل أنت متأكد من «{action}» لقاعدة البيانات: {db.DatabaseName}؟\n" +
                (enable
                    ? "سيتم تعديل الإجراء SP_Company_SelectAll ليُرجِع XmlPath = NULL (تعطيل تطبيق العميل)."
                    : "سيتم استرجاع الإجراء SP_Company_SelectAll إلى حالته الأصلية."),
                "تأكيد عملية الصيانة",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                IsBusy = true;
                StatusText = $"{action} لـ {db.DatabaseName}...";

                if (enable)
                    await ServiceContainer.XmlMaintenance.EnableMaintenanceAsync(db.DatabaseName);
                else
                    await ServiceContainer.XmlMaintenance.DisableMaintenanceAsync(db.DatabaseName);

                db.MaintenanceState = enable ? MaintenanceState.Maintenance : MaintenanceState.Normal;

                StatusText = $"تم {action} لـ {db.DatabaseName} بنجاح.";
                LogEntry("صيانة XmlPath", "نجح", db.DatabaseName, action);
                System.Windows.MessageBox.Show($"تم {action} لقاعدة البيانات {db.DatabaseName} بنجاح.",
                    "تم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"فشل {action} لـ {db.DatabaseName}.";
                LogEntry("صيانة XmlPath", "فشل", db.DatabaseName, ex.Message);
                System.Windows.MessageBox.Show($"تعذّر {action}:\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>تطبيق صيانة XmlPath (إيقاف/استرجاع) على كل قواعد البيانات.</summary>
        private async Task ApplyMaintenanceToAllAsync(bool enable)
        {
            var dbs = Databases.ToList();
            if (!dbs.Any())
            {
                System.Windows.MessageBox.Show("لا توجد قواعد بيانات. اضغط «تحديث» أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var action = enable ? "إيقاف XML للصيانة" : "استرجاع XML";
            var confirm = System.Windows.MessageBox.Show(
                $"هل أنت متأكد من تطبيق «{action}» على كل قواعد البيانات ({dbs.Count})؟\n" +
                (enable
                    ? "سيتم تعديل SP_Company_SelectAll في كل قاعدة بها هذا الإجراء (XmlPath = القيمة + '1')."
                    : "سيتم استرجاع SP_Company_SelectAll الأصلي في كل القواعد."),
                "تأكيد صيانة XML للكل",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsBusy = true;
            int ok = 0, skipped = 0, failed = 0;
            try
            {
                foreach (var db in dbs)
                {
                    try
                    {
                        if (enable)
                            await ServiceContainer.XmlMaintenance.EnableMaintenanceAsync(db.DatabaseName);
                        else
                            await ServiceContainer.XmlMaintenance.DisableMaintenanceAsync(db.DatabaseName);

                        db.MaintenanceState = enable ? MaintenanceState.Maintenance : MaintenanceState.Normal;
                        ok++;
                        StatusText = $"{action}: {db.DatabaseName} ({ok + skipped + failed}/{dbs.Count})";
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("SP_Company_SelectAll"))
                    {
                        // قاعدة لا تحتوي الإجراء — تخطّي بدون اعتبارها فشلاً
                        db.MaintenanceState = MaintenanceState.NotAvailable;
                        skipped++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        LogEntry("صيانة XML (الكل)", "فشل", db.DatabaseName, ex.Message);
                    }
                }

                StatusText = $"اكتمل {action} للكل — نجح {ok}، تخطّي {skipped}، فشل {failed}.";
                LogEntry("صيانة XML (الكل)", failed == 0 ? "نجح" : "جزئي", "",
                    $"{action}: نجح {ok}، تخطّي {skipped}، فشل {failed} من {dbs.Count}");
                System.Windows.MessageBox.Show(
                    $"اكتمل «{action}» للكل:\n✅ نجح: {ok}\n⏭️ تخطّي (لا يحتوي الإجراء): {skipped}\n❌ فشل: {failed}",
                    "النتيجة", MessageBoxButton.OK,
                    failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>تحديث حالة صيانة XmlPath لكل القواعد.</summary>
        private async Task RefreshMaintenanceAllAsync()
        {
            var dbs = Databases.ToList();
            if (!dbs.Any())
            {
                System.Windows.MessageBox.Show("لا توجد قواعد بيانات. اضغط «تحديث» أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            try
            {
                int done = 0;
                foreach (var db in dbs)
                {
                    try
                    {
                        db.MaintenanceState = await ServiceContainer.XmlMaintenance.GetStateAsync(db.DatabaseName);
                    }
                    catch
                    {
                        db.MaintenanceState = MaintenanceState.NotAvailable;
                    }
                    done++;
                    StatusText = $"تحديث حالة XML... ({done}/{dbs.Count})";
                }
                StatusText = "تم تحديث حالة XML لكل القواعد.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>تحديث حالة صيانة XmlPath لقاعدة محددة.</summary>
        private async Task RefreshMaintenanceStateAsync(DatabaseSelectionViewModel db)
        {
            try
            {
                var state = await ServiceContainer.XmlMaintenance.GetStateAsync(db.DatabaseName);
                db.MaintenanceState = state;
                if (state == MaintenanceState.NotAvailable)
                {
                    System.Windows.MessageBox.Show(
                        $"قاعدة البيانات {db.DatabaseName} لا تحتوي على الإجراء SP_Company_SelectAll — ميزة الصيانة غير متاحة لها.",
                        "غير متاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"تعذّر قراءة حالة الصيانة لـ {db.DatabaseName}:\n{ex.Message}",
                    "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RunBackupAsync(object? parameter)
        {
            var selectedDbs = Databases.Where(db => db.IsSelected).Select(db => db.DatabaseName).ToList();
            if (!selectedDbs.Any())
            {
                System.Windows.MessageBox.Show("الرجاء تحديد قاعدة بيانات واحدة على الأقل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                LogEntry("نسخ احتياطي", "فشل", "", "لم يتم تحديد أي قاعدة بيانات");
                return;
            }

            // بدء تقدم العمليات فوراً عند الضغط على الزر
            StartOperation("نسخ احتياطي لقواعد البيانات", "تحضير العملية...");
            
            IsBusy = true;
            var startTime = DateTime.Now;

            // تسجيل بداية العملية
            LogEntry("نسخ احتياطي", "جاري", string.Join(", ", selectedDbs), $"بدء النسخ الاحتياطي لـ {selectedDbs.Count} قاعدة بيانات");

            var totalDbs = selectedDbs.Count;

            // تحديث التقدم: مرحلة التحضير
            UpdateOperationProgress("جاري التحضير للنسخ الاحتياطي...", 5);
            
            // delay قصير لإظهار التقدم
            await Task.Delay(500);

            // إنشاء progress handler محسن
            var progress = new Progress<string>(update => 
            {
                StatusText = update;
                
                // تحديد قاعدة البيانات الحالية من النص وحساب التقدم
                for (int i = 0; i < selectedDbs.Count; i++)
                {
                    var dbName = selectedDbs[i];
                    if (update.Contains(dbName))
                    {
                        // حساب التقدم بناءً على القاعدة الحالية
                        var baseProgress = 10; // بداية من 10% بعد التحضير
                        var dbProgress = (int)(((double)(i + 1) / totalDbs) * 85); // 85% للعمليات الفعلية
                        var totalProgress = baseProgress + dbProgress;
                        
                        var estimatedTimeRemaining = TimeSpan.FromMinutes((totalDbs - i - 1) * 1.5); // تقدير أكثر واقعية
                        
                        UpdateOperationProgress(update, Math.Min(totalProgress, 95), dbName, estimatedTimeRemaining);
                        break;
                    }
                }
                
                // إذا لم نجد اسم قاعدة بيانات، نحديث الخطوة فقط
                if (!selectedDbs.Any(db => update.Contains(db)))
                {
                    UpdateOperationProgress(update, CurrentOperation.ProgressPercentage);
                }
            });

            try
            {
                // تشغيل النسخ الاحتياطي على خيط خلفي حتى لا تتجمّد الواجهة (Progress يعيد التحديثات للواجهة تلقائياً)
                await Task.Run(() => _backupService.BackupDatabasesAsync(selectedDbs, progress));

                var duration = (DateTime.Now - startTime).ToString(@"mm\:ss");
                
                // تحديث التقدم إلى 100% قبل الإكمال
                UpdateOperationProgress("جاري إنهاء العملية...", 100);
                await Task.Delay(300); // delay قصير لإظهار التحديث
                
                // إكمال العملية بنجاح
                CompleteOperation("مكتمل بنجاح");
                
                System.Windows.MessageBox.Show($"✅ تمت عملية النسخ الاحتياطي لـ {selectedDbs.Count} قاعدة بيانات بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "اكتمل النسخ الاحتياطي بنجاح.";
                
                // تسجيل نجاح العملية
                LogEntry("نسخ احتياطي", "نجح", string.Join(", ", selectedDbs), $"تم النسخ الاحتياطي بنجاح لـ {selectedDbs.Count} قاعدة بيانات", duration);
            }
            catch (Exception ex)
            {
                var duration = (DateTime.Now - startTime).ToString(@"mm\:ss");
                
                // إكمال العملية بفشل
                CompleteOperation($"فشل: {ex.Message}");
                
                System.Windows.MessageBox.Show($"❌ فشل النسخ الاحتياطي.\n\nالخطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = $"فشل النسخ الاحتياطي: {ex.Message}";
                
                // تسجيل فشل العملية
                LogEntry("نسخ احتياطي", "فشل", string.Join(", ", selectedDbs), $"فشل النسخ الاحتياطي: {ex.Message}", duration);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SaveSelection(object? parameter)
        {
            try
            {
                var backupConfig = SettingsManager.LoadSettings();
                backupConfig.SelectedDatabases = Databases
                    .Where(db => db.IsSelected)
                    .Select(db => db.DatabaseName)
                    .ToList();
                SettingsManager.SaveSettings(backupConfig);
                
                // تحديث Settings أيضاً
                Settings.SelectedDatabases = backupConfig.SelectedDatabases;
                
                System.Windows.MessageBox.Show("✅ تم حفظ اختيار قواعد البيانات.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"فشل حفظ الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenSettings(object? parameter)
        {
            var settingsWindow = new SettingsWindow();
            
            // إذا تم حفظ الإعدادات (تم إغلاق النافذة بنجاح)
            if (settingsWindow.ShowDialog() == true)
            {
                // إعادة تحميل الإعدادات الجديدة
                RefreshSettings();
                
                // إعادة تحميل قواعد البيانات بالإعدادات الجديدة
                _ = LoadDatabasesAsync(null);
            }
        }

        /// <summary>
        /// تحديث الإعدادات في النموذج الرئيسي بعد التغيير
        /// </summary>
        private void RefreshSettings()
        {
            try
            {
                // إعادة تحميل الإعدادات من SettingsManager
                var newConfig = SettingsManager.LoadSettings();
                
                // تحديث BackupService بالإعدادات الجديدة
                _backupService.UpdateSettings(newConfig);
                
                // تحديث Settings المحلية
                _settings = _backupService._settings;
                
                // إشعار بالتحديث
                StatusText = "تم تحديث الإعدادات بنجاح";
                
                _logger.LogInformation("تم تحديث الإعدادات في MainViewModel بنجاح");
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحديث الإعدادات: {ex.Message}", ex);
                StatusText = $"خطأ في تحديث الإعدادات: {ex.Message}";
            }
        }

        private async Task RunRestoreAsync(object? parameter)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "اختر ملف النسخة الاحتياطية",
                Filter = "Backup Files (*.bak)|*.bak|All files (*.*)|*.*",
                DefaultExt = ".bak",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            string backupFilePath = openFileDialog.FileName;
            string originalDbName = Path.GetFileNameWithoutExtension(backupFilePath).Split('_').FirstOrDefault() ?? "RestoredDB";
            string newDbName = $"TEST_{originalDbName}";

            var confirmResult = System.Windows.MessageBox.Show(
                $"هل أنت متأكد أنك تريد استعادة الملف:\n{Path.GetFileName(backupFilePath)}\n\nباسم جديد:\n{newDbName}؟",
                "تأكيد الاستعادة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.No)
            {
                return;
            }

            IsBusy = true;
            var progress = new Progress<string>(update => StatusText = update);

            try
            {
                await _backupService.RestoreDatabaseAsync(backupFilePath, newDbName, progress);
                System.Windows.MessageBox.Show($"✅ تمت استعادة قاعدة البيانات بنجاح باسم '{newDbName}'!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadDatabasesAsync(null);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"❌ فشلت عملية الاستعادة.\n\nالخطأ: {ex.Message}", "خطأ فادح", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "فشلت عملية الاستعادة.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteSqlForAllAsync(object? parameter)
        {
            var selectedDbs = Databases.Where(db => db.IsSelected).Select(db => db.DatabaseName).ToList();
            if (!selectedDbs.Any())
            {
                System.Windows.MessageBox.Show("الرجاء تحديد قاعدة بيانات واحدة على الأقل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            StatusText = "جاري تنفيذ سكربت SQL على القواعد المحددة...";

            try
            {
                var excutSql = new SqlExecutorWindow(selectedDbs);
                excutSql.ShowDialog();

                System.Windows.MessageBox.Show($"تم تنفيذ السكربت على {selectedDbs.Count} قاعدة بيانات بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "اكتمل تنفيذ السكربت.";
                
                // إضافة await لتجنب التحذير
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"فشل تنفيذ السكربت: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText = "فشل تنفيذ السكربت.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenCopyDatabase(object? parameter)
        {
            var copyWindow = new DatabaseCopyWindow();
            copyWindow.ShowDialog();
            _ = LoadDatabasesAsync(null);
        }

        private Task RunOperationsOnNewDbAsync(object? parameter)
        {
            return Task.CompletedTask;
        }

        private void BrowseIisSource(object? parameter)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                IisSourcePath = dialog.SelectedPath;
            }
        }

        private void BrowseIisDestination(object? parameter)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                IisDestinationPath = dialog.SelectedPath;
            }
        }

        private void CopyIisFolder(object? parameter)
        {
            // تنفيذ نسخ المجلد
        }

        private void CreateVirtualApp(object? parameter)
        {
            // إنشاء البرنامج الفرعي
        }

        private void BrowseSettingsFile(object? parameter)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "ملفات الإعدادات|*.json;*.config|جميع الملفات|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                SettingsFilePath = dialog.FileName;
            }
        }

        private void OpenAutoBackup(object? parameter)
        {
            var autoBackupWindow = new AutoBackupWindow();
            autoBackupWindow.DataContext = new AutoBackupViewModel(_autoBackupService, _backupService, _logger);
            autoBackupWindow.ShowDialog();
        }

        private void UpdateSettings(object? parameter)
        {
            // تعديل ملف الإعدادات
        }

        private void SelectAll(object? parameter)
        {
            foreach (var database in Databases)
            {
                database.IsSelected = true;
            }
            StatusText = $"تم تحديد جميع قواعد البيانات ({Databases.Count} قاعدة)";
        }

        private void OpenScheduledBackup(object? parameter)
        {
            try
            {
                var scheduledBackupWindow = new Views.ScheduledBackupWindow();
                scheduledBackupWindow.DataContext = new ScheduledBackupViewModel(_scheduledBackupService);
                scheduledBackupWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في فتح نافذة الجدولة: {ex.Message}", "خطأ",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UnselectAll(object? parameter)
        {
            foreach (var database in Databases)
            {
                database.IsSelected = false;
            }
            StatusText = "تم إلغاء تحديد جميع قواعد البيانات";
        }

        // Docker Methods
        private async Task CheckDockerStatusAsync(object? parameter)
        {
            try
            {
                var isDockerInstalled = await _dockerManager.IsDockerInstalledAsync();
                if (!isDockerInstalled)
                {
                    DockerStatus = "Docker غير مثبت";
                    return;
                }

                var containerExists = await _dockerManager.DoesContainerExistAsync(DockerContainerName);
                if (!containerExists)
                {
                    DockerStatus = "الحاوية غير موجودة";
                    return;
                }

                var isRunning = await _dockerManager.IsContainerRunningAsync(DockerContainerName);
                var status = await _dockerManager.GetContainerStatusAsync(DockerContainerName);
                
                DockerStatus = isRunning ? $"يعمل - {status}" : $"متوقف - {status}";
            }
            catch (Exception ex)
            {
                DockerStatus = $"خطأ: {ex.Message}";
                _logger.LogError($"خطأ في فحص حالة Docker: {ex.Message}", ex);
            }
        }

        private async Task CreateDockerContainerAsync(object? parameter)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "هل تريد إنشاء حاوية SQL Server جديدة؟\n" +
                    "سيتم استخدام كلمة المرور الافتراضية: YourStrong@Passw0rd\n" +
                    "يمكنك تغييرها لاحقاً من إعدادات المصادقة.",
                    "إنشاء حاوية جديدة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ConnectionStatus = "جاري إنشاء الحاوية...";
                    
                    var createResult = await _dockerManager.CreateSqlServerContainerAsync(
                        DockerContainerName, 
                        "YourStrong@Passw0rd", 
                        DockerPort);

                    if (createResult.Success)
                    {
                        ConnectionStatus = "تم إنشاء الحاوية بنجاح";
                        System.Windows.MessageBox.Show("تم إنشاء حاوية SQL Server بنجاح!", "نجح الإنشاء", 
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ConnectionStatus = $"فشل إنشاء الحاوية: {createResult.Error}";
                        System.Windows.MessageBox.Show($"فشل في إنشاء الحاوية:\n{createResult.Error}", "خطأ", 
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    await CheckDockerStatusAsync(null);
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"خطأ: {ex.Message}";
                System.Windows.MessageBox.Show($"خطأ في إنشاء الحاوية: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartDockerContainerAsync(object? parameter)
        {
            try
            {
                ConnectionStatus = "جاري تشغيل الحاوية...";
                
                var result = await _dockerManager.StartContainerAsync(DockerContainerName);
                
                if (result.Success)
                {
                    ConnectionStatus = "تم تشغيل الحاوية بنجاح";
                    System.Windows.MessageBox.Show("تم تشغيل حاوية SQL Server بنجاح!", "نجح التشغيل", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ConnectionStatus = $"فشل تشغيل الحاوية: {result.Error}";
                    System.Windows.MessageBox.Show($"فشل في تشغيل الحاوية:\n{result.Error}", "خطأ", 
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }

                await CheckDockerStatusAsync(null);
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"خطأ: {ex.Message}";
                System.Windows.MessageBox.Show($"خطأ في تشغيل الحاوية: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StopDockerContainerAsync(object? parameter)
        {
            try
            {
                ConnectionStatus = "جاري إيقاف الحاوية...";
                
                var result = await _dockerManager.StopContainerAsync(DockerContainerName);
                
                if (result.Success)
                {
                    ConnectionStatus = "تم إيقاف الحاوية بنجاح";
                    System.Windows.MessageBox.Show("تم إيقاف حاوية SQL Server بنجاح!", "نجح الإيقاف", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ConnectionStatus = $"فشل إيقاف الحاوية: {result.Error}";
                    System.Windows.MessageBox.Show($"فشل في إيقاف الحاوية:\n{result.Error}", "خطأ", 
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }

                await CheckDockerStatusAsync(null);
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"خطأ: {ex.Message}";
                System.Windows.MessageBox.Show($"خطأ في إيقاف الحاوية: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Settings Methods
        private async Task TestConnectionAsync(object? parameter)
        {
            IsBusy = true;
            ConnectionStatus = "جاري اختبار الاتصال...";

            try
            {
                // اختبار الاتصال باستخدام الخدمة من ServiceContainer
                var databases = await _backupService.GetDatabasesAsync();
                
                ConnectionStatus = $"نجح الاتصال - تم العثور على {databases.Count} قاعدة بيانات";
                System.Windows.MessageBox.Show($"✅ نجح الاتصال!\nتم العثور على {databases.Count} قاعدة بيانات.", 
                               "نجح الاختبار", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"فشل الاتصال: {ex.Message}";
                System.Windows.MessageBox.Show($"❌ فشل الاتصال!\n\nالخطأ: {ex.Message}", 
                               "فشل الاختبار", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SaveSettingsMethod(object? parameter)
        {
            try
            {
                // حفظ الإعدادات الأساسية والاتصال
                SaveCurrentSettings();
                
                // حفظ إعدادات الاتصال في BackupConfig
                var backupConfig = SettingsManager.LoadSettings();
                
                // تحديث إعدادات الاتصال
                backupConfig.Server = Settings.ServerName;
                backupConfig.Username = Settings.Username; 
                backupConfig.Password = Settings.Password;
                backupConfig.UseWindowsAuthentication = Settings.UseWindowsAuthentication;
                backupConfig.ConnectionType = Settings.ConnectionType;
                backupConfig.DockerContainerName = Settings.DockerContainerName;
                backupConfig.Port = Settings.Port;
                backupConfig.ConnectionTimeout = Settings.ConnectionTimeout;
                backupConfig.TrustServerCertificate = Settings.TrustServerCertificate;
                backupConfig.Encrypt = Settings.Encrypt;
                
                // حفظ الإعدادات
                SettingsManager.SaveSettings(backupConfig);
                
                // تحديث BackupService بالإعدادات الجديدة
                _backupService.UpdateSettings(backupConfig);
                
                System.Windows.MessageBox.Show("✅ تم حفظ إعدادات الاتصال بنجاح!", "نجح الحفظ", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
                ConnectionStatus = "تم حفظ إعدادات الاتصال";
                
                // تسجيل العملية
                LogEntry("حفظ إعدادات", "نجح", "", $"تم حفظ إعدادات الاتصال - النوع: {Settings.ConnectionType}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"❌ فشل حفظ الإعدادات!\n\nالخطأ: {ex.Message}", 
                               "فشل الحفظ", MessageBoxButton.OK, MessageBoxImage.Error);
                ConnectionStatus = $"فشل حفظ الإعدادات: {ex.Message}";
                
                // تسجيل الفشل
                LogEntry("حفظ إعدادات", "فشل", "", $"فشل حفظ إعدادات الاتصال: {ex.Message}");
            }
        }

        /// <summary>
        /// تشغيل خدمة النسخ الاحتياطي التلقائي إذا كانت مفعلة في الإعدادات
        /// </summary>
        private async Task StartAutoBackupServiceIfEnabledAsync()
        {
            try
            {
                _logger.LogInformation("فحص إعدادات الخدمة التلقائية عند بدء التطبيق...");
                
                var config = _autoBackupService.GetConfig();
                if (config.IsEnabled)
                {
                    _logger.LogInformation("الخدمة التلقائية مفعلة - جاري البدء...");
                    await _autoBackupService.StartAsync();
                    _logger.LogInformation("✅ تم تشغيل خدمة النسخ الاحتياطي التلقائي بنجاح");
                }
                else
                {
                    _logger.LogInformation("الخدمة التلقائية غير مفعلة في الإعدادات");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ فشل تشغيل الخدمة التلقائية: {ex.Message}", ex);
            }
        }

        // دوال إدارة السجل
        private async Task RefreshLogAsync(object? parameter)
        {
            await _logService.LoadLogAsync();
            RefreshLogStatistics();
        }

        private void ClearLog(object? parameter)
        {
            var result = System.Windows.MessageBox.Show(
                "هل أنت متأكد من أنك تريد مسح جميع سجلات العمليات؟\nلا يمكن التراجع عن هذا الإجراء.",
                "تأكيد مسح السجل",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _logService.ClearLog();
                RefreshLogStatistics();
                LogEntry("إدارة السجل", "نجح", "", "تم مسح جميع سجلات العمليات");
            }
        }

        private async Task ExportLogAsync(object? parameter)
        {
            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "ملفات CSV (*.csv)|*.csv|جميع الملفات (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"operation_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var result = await _logService.ExportLogAsync(saveDialog.FileName);
                    System.Windows.MessageBox.Show(result, "تصدير السجل", MessageBoxButton.OK, MessageBoxImage.Information);
                    LogEntry("إدارة السجل", "نجح", "", $"تم تصدير السجل إلى: {saveDialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في تصدير السجل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                LogEntry("إدارة السجل", "فشل", "", $"فشل تصدير السجل: {ex.Message}");
            }
        }

        private void ApplyFilter(object? parameter)
        {
            try
            {
                var filtered = _logService.FilterLogs(
                    SelectedOperationType,
                    SelectedStatus,
                    ShowTodayOnly,
                    ShowSuccessOnly,
                    ShowFailedOnly);

                // في WPF، يمكننا استخدام CollectionView للفلترة
                // لكن للبساطة، سنعرض رسالة بعدد النتائج
                System.Windows.MessageBox.Show(
                    $"تم العثور على {filtered.Count} سجل يطابق معايير الفلترة",
                    "نتائج الفلترة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LogEntry("إدارة السجل", "نجح", "", $"تم تطبيق فلتر على السجل - {filtered.Count} نتيجة");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"خطأ في تطبيق الفلتر: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshLogStatistics()
        {
            OnPropertyChanged(nameof(TotalOperationsCount));
            OnPropertyChanged(nameof(SuccessfulOperationsCount));
            OnPropertyChanged(nameof(FailedOperationsCount));
            OnPropertyChanged(nameof(RunningOperationsCountFromLog));
            OnPropertyChanged(nameof(LastOperationTime));
        }

        public void LogEntry(string operationType, string status, string databaseName, string description, string duration = "")
        {
            _logService.AddLogEntry(operationType, status, databaseName, description, duration);
            RefreshLogStatistics();
        }

        /// <summary>
        /// تحديث عرض تقدم العمليات كل ثانية
        /// </summary>
        private void UpdateProgressDisplay(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CurrentOperationDisplay));
            OnPropertyChanged(nameof(ProgressDisplay));
            OnPropertyChanged(nameof(TimeElapsedDisplay));
            OnPropertyChanged(nameof(EstimatedTimeDisplay));
            OnPropertyChanged(nameof(RunningOperationsCount));
            
            // تحديث الوقت المنقضي للعملية الحالية
            if (CurrentOperation.IsRunning)
            {
                // استدعاء ميثود التحديث في OperationProgress
                CurrentOperation.UpdateElapsedTime();
                
                // تحديث الخصائص المحسوبة في MainViewModel
                OnPropertyChanged(nameof(CurrentOperation));
            }
            
            // تحديث حالة أيقونة شريط المهام
            UpdateTrayIconStatus();
        }

        /// <summary>
        /// بدء عملية جديدة
        /// </summary>
        public void StartOperation(string operationName, string initialStep = "بدء العملية...")
        {
            CurrentOperation = new OperationProgress
            {
                OperationName = operationName,
                CurrentStep = initialStep,
                IsRunning = true,
                StartTime = DateTime.Now,
                ProgressPercentage = 0,
                Status = "جاري..."
            };
            
            // إشعار بتحديث الخصائص
            OnPropertyChanged(nameof(CurrentOperationDisplay));
            OnPropertyChanged(nameof(ProgressDisplay));
            OnPropertyChanged(nameof(TimeElapsedDisplay));
            OnPropertyChanged(nameof(EstimatedTimeDisplay));
        }

        /// <summary>
        /// تحديث تقدم العملية الحالية
        /// </summary>
        public void UpdateOperationProgress(string currentStep, int progressPercentage, string currentDatabase = "", TimeSpan estimatedTimeRemaining = default)
        {
            if (CurrentOperation.IsRunning)
            {
                CurrentOperation.CurrentStep = currentStep;
                CurrentOperation.ProgressPercentage = progressPercentage;
                CurrentOperation.CurrentDatabase = currentDatabase;
                if (estimatedTimeRemaining != default)
                {
                    CurrentOperation.EstimatedTimeRemaining = estimatedTimeRemaining;
                }
                
                // إشعار بتحديث الخصائص المعتمدة على العملية الحالية
                OnPropertyChanged(nameof(CurrentOperationDisplay));
                OnPropertyChanged(nameof(ProgressDisplay));
                OnPropertyChanged(nameof(CurrentOperation));
            }
        }

        /// <summary>
        /// إنهاء العملية الحالية
        /// </summary>
        public void CompleteOperation(string finalStatus = "مكتمل")
        {
            if (CurrentOperation.IsRunning)
            {
                var operationName = CurrentOperation.OperationName;
                CurrentOperation.IsRunning = false;
                CurrentOperation.Status = finalStatus;
                CurrentOperation.ProgressPercentage = 100;
                
                // إضافة إلى سجل العمليات
                OperationHistory.Add(CurrentOperation);
                
                // إشعار شريط المهام بانتهاء العملية
                if (_trayIconManager != null)
                {
                    var isSuccess = finalStatus.Contains("نجح") || finalStatus.Contains("مكتمل");
                    var icon = isSuccess ? Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info : Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Error;
                    var message = isSuccess ? $"تمت {operationName} بنجاح" : $"فشل في {operationName}";
                    
                    _trayIconManager.ShowBalloonTip("نظام النسخ الاحتياطي", message, icon);
                }
                
                // إنشاء عملية جديدة فارغة
                CurrentOperation = new OperationProgress();
                
                // إشعار بتحديث الخصائص
                OnPropertyChanged(nameof(CurrentOperationDisplay));
                OnPropertyChanged(nameof(ProgressDisplay));
                OnPropertyChanged(nameof(TimeElapsedDisplay));
                OnPropertyChanged(nameof(EstimatedTimeDisplay));
                OnPropertyChanged(nameof(OperationHistory));
                OnPropertyChanged(nameof(CompletedOperationsCount));
                OnPropertyChanged(nameof(SuccessfulOperationsFromHistory));
                OnPropertyChanged(nameof(FailedOperationsFromHistory));
                OnPropertyChanged(nameof(RunningOperationsCount));
            }
        }

        /// <summary>
        /// تعيين مدير أيقونة شريط المهام
        /// </summary>
        public void SetTrayIconManager(TrayIconManager trayIconManager)
        {
            _trayIconManager = trayIconManager;
        }

        /// <summary>
        /// تحديث حالة أيقونة شريط المهام حسب العمليات الجارية
        /// </summary>
        private void UpdateTrayIconStatus()
        {
            if (_trayIconManager != null)
            {
                if (CurrentOperation.IsRunning)
                {
                    _trayIconManager.UpdateToolTip($"نظام النسخ الاحتياطي - {CurrentOperation.OperationName} ({CurrentOperation.ProgressPercentage}%)");
                }
                else
                {
                    var status = IsBusy ? "مشغول" : "جاهز";
                    _trayIconManager.UpdateToolTip($"نظام النسخ الاحتياطي - {status}");
                }
            }
        }

        private void OpenAutoDeploy(object? parameter)
        {
            try
            {
                var deployService = new Service.DeployService(_logger);
                var deployViewModel = new DeployViewModel(deployService, _logger);
                var deployWindow = new automationIIs(deployViewModel);
                deployWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في فتح نافذة النشر الآلي: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"فشل في فتح نافذة النشر الآلي: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OpenDailyTasks(object? parameter)
        {
            try
            {
                var viewModel = new DailyTaskViewModel(_dailyTaskService, _logger);
                var window = new DailyTaskManagementWindow(viewModel);
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في فتح نافذة المهام اليومية: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"فشل في فتح نافذة المهام اليومية: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
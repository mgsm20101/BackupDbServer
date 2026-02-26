using BackupDbServer.Helpers; // يحتوي على SettingsManager و BackupConfig
using BackupDbServer.Models;
using BackupDbServer.Services;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;

namespace BackupDbServer.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ILogger _logger;
        private readonly DockerSqlServerManager _dockerManager;

        // الخصائص التي سترتبط بها الواجهة
        private string _server = string.Empty;
        public string Server
        {
            get => _server;
            set { _server = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private string _tempPath = string.Empty;
        public string TempPath
        {
            get => _tempPath;
            set { _tempPath = value; OnPropertyChanged(); }
        }

        private string _backupPath = string.Empty;
        public string BackupPath
        {
            get => _backupPath;
            set { _backupPath = value; OnPropertyChanged(); }
        }

        private bool _isTestingConnection;
        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set { _isTestingConnection = value; OnPropertyChanged(); }
        }

        // خصائص أنواع الاتصال
        private ConnectionType _connectionType = ConnectionType.Local;
        public ConnectionType ConnectionType
        {
            get => _connectionType;
            set 
            { 
                _connectionType = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocalConnection));
                OnPropertyChanged(nameof(IsDockerConnection));
                OnPropertyChanged(nameof(IsRemoteConnection));
                UpdateConnectionString();
                _ = UpdateDockerStatusAsync();
            }
        }

        public bool IsLocalConnection
        {
            get => ConnectionType == ConnectionType.Local;
            set { if (value) ConnectionType = ConnectionType.Local; }
        }

        public bool IsDockerConnection
        {
            get => ConnectionType == ConnectionType.Docker;
            set { if (value) ConnectionType = ConnectionType.Docker; }
        }

        public bool IsRemoteConnection
        {
            get => ConnectionType == ConnectionType.Remote;
            set { if (value) ConnectionType = ConnectionType.Remote; }
        }

        // خصائص Docker
        private string _dockerContainerName = "dbs-2022-CTP23-1r";
        public string DockerContainerName
        {
            get => _dockerContainerName;
            set { _dockerContainerName = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private string _dockerStatus = "غير معروف";
        public string DockerStatus
        {
            get => _dockerStatus;
            set { _dockerStatus = value; OnPropertyChanged(); }
        }

        // خصائص الاتصال المتقدمة
        private bool _useWindowsAuthentication = true;
        public bool UseWindowsAuthentication
        {
            get => _useWindowsAuthentication;
            set 
            { 
                _useWindowsAuthentication = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseSqlAuthentication));
                UpdateConnectionString();
            }
        }

        public bool UseSqlAuthentication
        {
            get => !UseWindowsAuthentication;
            set { UseWindowsAuthentication = !value; }
        }

        private int _port = 1433;
        public int Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private bool _useCustomPort = false;
        public bool UseCustomPort
        {
            get => _useCustomPort;
            set { _useCustomPort = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private int _connectionTimeout = 30;
        public int ConnectionTimeout
        {
            get => _connectionTimeout;
            set { _connectionTimeout = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private bool _trustServerCertificate = true;
        public bool TrustServerCertificate
        {
            get => _trustServerCertificate;
            set { _trustServerCertificate = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        private bool _encrypt = false;
        public bool Encrypt
        {
            get => _encrypt;
            set { _encrypt = value; OnPropertyChanged(); UpdateConnectionString(); }
        }

        // خاصية عرض سلسلة الاتصال
        private string _connectionString = string.Empty;
        public string ConnectionString
        {
            get => _connectionString;
            set { _connectionString = value; OnPropertyChanged(); }
        }

        // الأوامر
        public ICommand SaveCommand { get; }
        public ICommand CloseWindowCommand { get; }
        public ICommand BrowseCommand { get; } // أمر جديد لاختيار المسار
        public ICommand TestConnectionCommand { get; } // أمر جديد لاختبار الاتصال
        public ICommand CheckDockerStatusCommand { get; }
        public ICommand CreateDockerContainerCommand { get; }
        public ICommand StartDockerContainerCommand { get; }
        public ICommand StopDockerContainerCommand { get; }

        // خاصية لكلمة المرور (سيتم التعامل معها بشكل خاص)
        private string _password = string.Empty;
        public string Password { get { return _password; } set { _password = value; OnPropertyChanged(); UpdateConnectionString(); } }

        public SettingsViewModel()
        {
            _logger = ServiceContainer.Logger; // استخدام ServiceContainer
            _dockerManager = new DockerSqlServerManager(_logger);
            
            LoadSettings();

            // ربط الأوامر بالدوال التي ستنفذها
            SaveCommand = new RelayCommand(SaveSettings, CanSave);
            CloseWindowCommand = new RelayCommand(CloseWindow);
            BrowseCommand = new RelayCommand(BrowsePath);
            TestConnectionCommand = new AsyncRelayCommand(TestConnection, CanTestConnection);
            CheckDockerStatusCommand = new AsyncRelayCommand(CheckDockerStatusAsync);
            CreateDockerContainerCommand = new AsyncRelayCommand(CreateDockerContainerAsync);
            StartDockerContainerCommand = new AsyncRelayCommand(StartDockerContainerAsync);
            StopDockerContainerCommand = new AsyncRelayCommand(StopDockerContainerAsync);

            // تحديث حالة Docker عند التشغيل
            _ = UpdateDockerStatusAsync();
        }

        private void UpdateConnectionString()
        {
            try
            {
                var builder = new SqlConnectionStringBuilder
                {
                    ConnectTimeout = ConnectionTimeout,
                    TrustServerCertificate = TrustServerCertificate,
                    Encrypt = Encrypt
                };

                switch (ConnectionType)
                {
                    case ConnectionType.Local:
                        builder.DataSource = UseCustomPort ? $"{Server},{Port}" : Server;
                        break;
                    case ConnectionType.Docker:
                        builder.DataSource = UseCustomPort ? $"localhost,{Port}" : "localhost,1433";
                        break;
                    case ConnectionType.Remote:
                        builder.DataSource = UseCustomPort ? $"{Server},{Port}" : Server;
                        break;
                }

                if (UseWindowsAuthentication)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    builder.UserID = Username;
                    builder.Password = Password;
                }

                ConnectionString = builder.ConnectionString;
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحديث سلسلة الاتصال: {ex.Message}", ex);
                ConnectionString = "خطأ في بناء سلسلة الاتصال";
            }
        }

        private async Task UpdateDockerStatusAsync()
        {
            try
            {
                if (ConnectionType == ConnectionType.Docker)
                {
                    var isDockerInstalled = await _dockerManager.IsDockerInstalledAsync();
                    if (!isDockerInstalled)
                    {
                        DockerStatus = "🔴 Docker غير مثبت";
                        return;
                    }

                    var containerExists = await _dockerManager.DoesContainerExistAsync(DockerContainerName);
                    if (!containerExists)
                    {
                        DockerStatus = "🟡 الحاوية غير موجودة";
                        return;
                    }

                    var isRunning = await _dockerManager.IsContainerRunningAsync(DockerContainerName);
                    DockerStatus = isRunning ? "🟢 يعمل" : "🟡 متوقف";
                }
                else
                {
                    DockerStatus = "غير مطلوب";
                }
            }
            catch (Exception ex)
            {
                DockerStatus = $"🔴 خطأ: {ex.Message}";
                _logger.LogError($"خطأ في فحص حالة Docker: {ex.Message}", ex);
            }
        }

        private void BrowsePath(object? parameter)
        {
            // لاستخدام FolderBrowserDialog، تحتاج لإضافة مرجع لمشروعك
            // Right-click on project -> Add -> Reference -> Assemblies -> Framework -> System.Windows.Forms
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    string? pathType = parameter as string;

                    if (pathType == "Temp")
                    {
                        TempPath = selectedPath;
                    }
                    else if (pathType == "Backup")
                    {
                        BackupPath = selectedPath;
                    }
                }
            }
        }

        private async Task TestConnection(object? parameter)
        {
            IsTestingConnection = true;
            try
            {
                // إنشاء BackupRestoreService مؤقت مع الإعدادات الحالية
                var tempConfig = new BackupConfig
                {
                    Server = this.Server,
                    Username = this.Username,
                    Password = this.Password,
                    UseWindowsAuthentication = this.UseWindowsAuthentication,
                    ConnectionType = this.ConnectionType,
                    DockerContainerName = this.DockerContainerName,
                    Port = this.Port,
                    ConnectionTimeout = this.ConnectionTimeout,
                    TrustServerCertificate = this.TrustServerCertificate,
                    Encrypt = this.Encrypt,
                    BackupPath = this.BackupPath,
                    TempPath = this.TempPath,
                    SelectedDatabases = new List<string>()
                };

                // حفظ الإعدادات مؤقتاً لاختبار الاتصال
                var originalSettings = SettingsManager.LoadSettings();
                SettingsManager.SaveSettings(tempConfig);

                // استخدام BackupService من ServiceContainer لاختبار الاتصال
                var testService = ServiceContainer.BackupService;
                var (success, message) = await testService.TestConnectionAsync();

                // استعادة الإعدادات الأصلية
                SettingsManager.SaveSettings(originalSettings);

                // عرض النتيجة
                if (success)
                {
                    System.Windows.MessageBox.Show(message, "نجح الاتصال", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(message, "فشل الاتصال", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"❌ فشل اختبار الاتصال.\n\nالخطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.LogError($"خطأ في اختبار الاتصال: {ex.Message}", ex);
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        private async Task CheckDockerStatusAsync(object? parameter)
        {
            await UpdateDockerStatusAsync();
        }

        private async Task CreateDockerContainerAsync(object? parameter)
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "هل تريد إنشاء حاوية SQL Server جديدة؟\n" +
                    "سيتم استخدام كلمة المرور الافتراضية: YourStrong@Passw0rd",
                    "إنشاء حاوية جديدة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    DockerStatus = "🟡 جاري الإنشاء...";
                    
                    var createResult = await _dockerManager.CreateSqlServerContainerAsync(
                        DockerContainerName, 
                        "YourStrong@Passw0rd", 
                        Port);

                    if (createResult.Success)
                    {
                        DockerStatus = "🟢 تم الإنشاء بنجاح";
                        System.Windows.MessageBox.Show("تم إنشاء حاوية SQL Server بنجاح!", "نجح الإنشاء", 
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        DockerStatus = $"🔴 فشل الإنشاء";
                        System.Windows.MessageBox.Show($"فشل في إنشاء الحاوية:\n{createResult.Error}", "خطأ", 
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    await UpdateDockerStatusAsync();
                }
            }
            catch (Exception ex)
            {
                DockerStatus = $"🔴 خطأ: {ex.Message}";
                System.Windows.MessageBox.Show($"خطأ في إنشاء الحاوية: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartDockerContainerAsync(object? parameter)
        {
            try
            {
                DockerStatus = "🟡 جاري التشغيل...";
                
                var result = await _dockerManager.StartContainerAsync(DockerContainerName);
                
                if (result.Success)
                {
                    DockerStatus = "🟢 يعمل";
                    System.Windows.MessageBox.Show("تم تشغيل حاوية SQL Server بنجاح!", "نجح التشغيل", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DockerStatus = $"🔴 فشل التشغيل";
                    System.Windows.MessageBox.Show($"فشل في تشغيل الحاوية:\n{result.Error}", "خطأ", 
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                await UpdateDockerStatusAsync();
            }
            catch (Exception ex)
            {
                DockerStatus = $"🔴 خطأ: {ex.Message}";
                System.Windows.MessageBox.Show($"خطأ في تشغيل الحاوية: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StopDockerContainerAsync(object? parameter)
        {
            try
            {
                DockerStatus = "🟡 جاري الإيقاف...";
                
                var result = await _dockerManager.StopContainerAsync(DockerContainerName);
                
                if (result.Success)
                {
                    DockerStatus = "🟡 متوقف";
                    System.Windows.MessageBox.Show("تم إيقاف حاوية SQL Server بنجاح!", "نجح الإيقاف", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DockerStatus = $"🔴 فشل الإيقاف";
                    System.Windows.MessageBox.Show($"فشل في إيقاف الحاوية:\n{result.Error}", "خطأ", 
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                await UpdateDockerStatusAsync();
            }
            catch (Exception ex)
            {
                DockerStatus = $"🔴 خطأ: {ex.Message}";
                System.Windows.MessageBox.Show($"خطأ في إيقاف الحاوية: {ex.Message}", "خطأ", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadSettings()
        {
            try
            {
                var config = SettingsManager.LoadSettings();
                
                // تحميل الإعدادات الأساسية
                Server = config.Server ?? "localhost";
                Username = config.Username ?? "";
                Password = config.Password ?? "";
                TempPath = config.TempPath ?? @"C:\Temp";
                BackupPath = config.BackupPath ?? @"C:\Backups";
                
                // تحميل إعدادات الاتصال
                ConnectionType = config.ConnectionType;
                DockerContainerName = config.DockerContainerName ?? "dbs-2022-CTP23-1r";
                UseWindowsAuthentication = config.UseWindowsAuthentication;
                Port = config.Port > 0 ? config.Port : 1433;
                UseCustomPort = Port != 1433;
                ConnectionTimeout = config.ConnectionTimeout > 0 ? config.ConnectionTimeout : 30;
                TrustServerCertificate = config.TrustServerCertificate;
                Encrypt = config.Encrypt;
                
                // تحديث سلسلة الاتصال
                UpdateConnectionString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في تحميل الإعدادات: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"خطأ في تحميل الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveSettings(object? parameter)
        {
            try
            {
                var config = new BackupConfig
                {
                    Server = this.Server,
                    Username = this.Username,
                    Password = this.Password,
                    TempPath = this.TempPath,
                    BackupPath = this.BackupPath,
                    
                    // حفظ إعدادات الاتصال الجديدة
                    ConnectionType = this.ConnectionType,
                    DockerContainerName = this.DockerContainerName,
                    UseWindowsAuthentication = this.UseWindowsAuthentication,
                    Port = this.Port,
                    ConnectionTimeout = this.ConnectionTimeout,
                    TrustServerCertificate = this.TrustServerCertificate,
                    Encrypt = this.Encrypt,
                    
                    // الاحتفاظ بقواعد البيانات المحددة
                    SelectedDatabases = SettingsManager.LoadSettings().SelectedDatabases
                };

                SettingsManager.SaveSettings(config);

                System.Windows.MessageBox.Show("✅ تم حفظ الإعدادات بنجاح", "تم", MessageBoxButton.OK, MessageBoxImage.Information);

                // إغلاق النافذة بعد الحفظ مع تعيين النتيجة
                CloseWindowWithResult(parameter, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في حفظ الإعدادات: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"خطأ في حفظ الإعدادات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private bool CanTestConnection(object? parameter)
        {
            // يمكن اختبار الاتصال إذا كانت الإعدادات مكتملة
            if (ConnectionType == ConnectionType.Docker)
            {
                return !string.IsNullOrWhiteSpace(DockerContainerName);
            }
            else
            {
                if (UseWindowsAuthentication)
                {
                    return !string.IsNullOrWhiteSpace(Server);
                }
                else
                {
                    return !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Username);
                }
            }
        }

        // دالة لتحديد ما إذا كان يمكن تنفيذ أمر الحفظ
        private bool CanSave(object? parameter)
        {
            // يجب أن تكون المسارات محددة
            bool hasValidPaths = !string.IsNullOrWhiteSpace(BackupPath);
            
            // يجب أن تكون إعدادات الاتصال صحيحة
            bool hasValidConnection = false;
            if (ConnectionType == ConnectionType.Docker)
            {
                hasValidConnection = !string.IsNullOrWhiteSpace(DockerContainerName);
            }
            else
            {
                if (UseWindowsAuthentication)
                {
                    hasValidConnection = !string.IsNullOrWhiteSpace(Server);
                }
                else
                {
                    hasValidConnection = !string.IsNullOrWhiteSpace(Server) && !string.IsNullOrWhiteSpace(Username);
                }
            }
            
            return hasValidPaths && hasValidConnection;
        }

        private void CloseWindow(object? parameter)
        {
            if (parameter is Window window)
            {
                window.Close();
            }
        }

        private void CloseWindowWithResult(object? parameter, bool dialogResult)
        {
            if (parameter is Window window)
            {
                window.DialogResult = dialogResult;
                window.Close();
            }
        }
    }
}

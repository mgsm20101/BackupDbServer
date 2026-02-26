using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BackupDbServer.Models
{
    public class AutoBackupConfig : INotifyPropertyChanged
    {
        private bool _isEnabled = false;
        private int _intervalHours = 24;
        private DateTime _nextBackupTime = DateTime.Now.AddHours(24);
        private bool _deleteOldBackups = true;
        private int _retentionDays = 7;
        private List<string> _selectedDatabases = new List<string>();
        private bool _useScheduledTime = false;
        private TimeOnly _scheduledTime = new TimeOnly(2, 0);
        private bool _runOnlyOnce = false;
        private bool _uploadToGoogleDrive = false;
        private string _googleDriveCredentialsPath = "credentials.json";
        private string _googleDriveFolderName = "Database Backups";
        private bool _deleteLocalAfterUpload = false;
        private bool _useGoogleDriveScheduledTime = false;
        private TimeOnly _googleDriveUploadTime = new TimeOnly(3, 0);

        public bool IsEnabled 
        { 
            get => _isEnabled; 
            set { _isEnabled = value; OnPropertyChanged(); } 
        }
        
        public int IntervalHours 
        { 
            get => _intervalHours; 
            set 
            { 
                _intervalHours = value; 
                OnPropertyChanged();
                
                // تحديث التاريخ التالي إذا لم يكن يستخدم الوقت المحدد
                if (!_useScheduledTime)
                {
                    UpdateNextBackupTime();
                }
            } 
        }
        
        public DateTime NextBackupTime 
        { 
            get => _nextBackupTime; 
            set { _nextBackupTime = value; OnPropertyChanged(); } 
        }
        
        public bool DeleteOldBackups 
        { 
            get => _deleteOldBackups; 
            set { _deleteOldBackups = value; OnPropertyChanged(); } 
        }
        
        public int RetentionDays 
        { 
            get => _retentionDays; 
            set { _retentionDays = value; OnPropertyChanged(); } 
        }
        
        public List<string> SelectedDatabases 
        { 
            get => _selectedDatabases; 
            set { _selectedDatabases = value; OnPropertyChanged(); } 
        }

        // إعدادات توقيت النسخ الاحتياطي
        public bool UseScheduledTime 
        { 
            get => _useScheduledTime; 
            set 
            { 
                _useScheduledTime = value; 
                OnPropertyChanged();
                
                // تحديث التاريخ التالي عند تفعيل/إلغاء الوقت المحدد
                if (value)
                {
                    UpdateNextBackupTime();
                }
            } 
        }
        
        public TimeOnly ScheduledTime 
        { 
            get => _scheduledTime; 
            set 
            { 
                _scheduledTime = value; 
                OnPropertyChanged();
                
                // تحديث التاريخ التالي عند تغيير الوقت
                if (_useScheduledTime)
                {
                    UpdateNextBackupTime();
                }
            } 
        }
        
        public bool RunOnlyOnce 
        { 
            get => _runOnlyOnce; 
            set 
            { 
                _runOnlyOnce = value; 
                OnPropertyChanged();
                
                // تحديث التاريخ التالي عند تغيير نمط التشغيل
                if (_useScheduledTime)
                {
                    UpdateNextBackupTime();
                }
            } 
        }

        // إعدادات Google Drive
        public bool UploadToGoogleDrive 
        { 
            get => _uploadToGoogleDrive; 
            set { _uploadToGoogleDrive = value; OnPropertyChanged(); } 
        }
        
        public string GoogleDriveCredentialsPath 
        { 
            get => _googleDriveCredentialsPath; 
            set { _googleDriveCredentialsPath = value; OnPropertyChanged(); } 
        }
        
        public string GoogleDriveFolderName 
        { 
            get => _googleDriveFolderName; 
            set { _googleDriveFolderName = value; OnPropertyChanged(); } 
        }
        
        public bool DeleteLocalAfterUpload 
        { 
            get => _deleteLocalAfterUpload; 
            set { _deleteLocalAfterUpload = value; OnPropertyChanged(); } 
        }
        
        // إعدادات توقيت رفع Google Drive
        public bool UseGoogleDriveScheduledTime 
        { 
            get => _useGoogleDriveScheduledTime; 
            set { _useGoogleDriveScheduledTime = value; OnPropertyChanged(); } 
        }
        
        public TimeOnly GoogleDriveUploadTime 
        { 
            get => _googleDriveUploadTime; 
            set { _googleDriveUploadTime = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// تحديث التاريخ التالي للنسخ الاحتياطي حسب الوقت المحدد
        /// </summary>
        private void UpdateNextBackupTime()
        {
            if (_useScheduledTime)
            {
                var today = DateTime.Today;
                var scheduledDateTime = today.Add(_scheduledTime.ToTimeSpan());
                var now = DateTime.Now;

                // تسجيل حالة التحديث
                System.Diagnostics.Debug.WriteLine($"UpdateNextBackupTime - اليوم: {today:yyyy-MM-dd}, الوقت المحدد: {_scheduledTime}, الوقت الكامل: {scheduledDateTime:yyyy-MM-dd HH:mm:ss}, الآن: {now:yyyy-MM-dd HH:mm:ss}");

                // إذا كان الوقت المحدد قد مضى اليوم، حدد لغداً
                if (scheduledDateTime <= now)
                {
                    if (!_runOnlyOnce)
                    {
                        _nextBackupTime = today.AddDays(1).Add(_scheduledTime.ToTimeSpan());
                        System.Diagnostics.Debug.WriteLine($"الوقت مضى اليوم - سيتم النسخ غداً في: {_nextBackupTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        // إذا كان التشغيل مرة واحدة فقط ومضى الوقت، لا نحدث التاريخ
                        System.Diagnostics.Debug.WriteLine("الوقت مضى والتشغيل مرة واحدة فقط - لا يتم تحديث التاريخ");
                        return;
                    }
                }
                else
                {
                    _nextBackupTime = scheduledDateTime;
                    System.Diagnostics.Debug.WriteLine($"سيتم النسخ اليوم في: {_nextBackupTime:yyyy-MM-dd HH:mm:ss}");
                }

                OnPropertyChanged(nameof(NextBackupTime));
                System.Diagnostics.Debug.WriteLine($"تم تحديث NextBackupTime إلى: {_nextBackupTime:yyyy-MM-dd HH:mm:ss}");
            }
            else
            {
                // استخدام الفترة الزمنية العادية
                _nextBackupTime = DateTime.Now.AddHours(_intervalHours);
                OnPropertyChanged(nameof(NextBackupTime));
                System.Diagnostics.Debug.WriteLine($"استخدام الفترة الزمنية - النسخ التالي خلال {_intervalHours} ساعات: {_nextBackupTime:yyyy-MM-dd HH:mm:ss}");
            }
        }

        /// <summary>
        /// تحديث التاريخ التالي يدوياً (للاستخدام من الخارج)
        /// </summary>
        public void RefreshNextBackupTime()
        {
            UpdateNextBackupTime();
        }
    }
    
    public class BackupScheduleEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DatabaseName { get; set; } = string.Empty;
        public DateTime BackupTime { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Success, Failed
        public string LocalFilePath { get; set; } = string.Empty;
        public string GoogleDriveFileId { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
    }
}

using BackupDbServer.Helpers;
using BackupDbServer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BackupDbServer.ViewModels
{
    public class DatabaseCopyViewModel : ViewModelBase
    {
        private readonly DatabaseCopyService _copyService;
        private readonly BackupRestoreService _backupService;

        // خصائص ربط البيانات
        private ObservableCollection<string> _availableDatabases;
        public ObservableCollection<string> AvailableDatabases
        {
            get => _availableDatabases;
            set { _availableDatabases = value; OnPropertyChanged(); }
        }

        private string _sourceDatabase = string.Empty;
        public string SourceDatabase
        {
            get => _sourceDatabase;
            set { _sourceDatabase = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartCopy)); }
        }

        private string _targetDatabaseName = string.Empty;
        public string TargetDatabaseName
        {
            get => _targetDatabaseName;
            set { _targetDatabaseName = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartCopy)); }
        }

        private string _sqlScripts = string.Empty;
        public string SqlScripts
        {
            get => _sqlScripts;
            set { _sqlScripts = value; OnPropertyChanged(); }
        }

        private string _fileModifications = string.Empty;
        public string FileModifications
        {
            get => _fileModifications;
            set { _fileModifications = value; OnPropertyChanged(); }
        }

        private string _statusText = "جاهز لبدء النسخ";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanStartCopy)); }
        }

        public bool CanStartCopy => !IsBusy &&
                                   !string.IsNullOrWhiteSpace(SourceDatabase) &&
                                   !string.IsNullOrWhiteSpace(TargetDatabaseName);

        // الأوامر
        public ICommand StartCopyCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand LoadDatabasesCommand { get; }

        public DatabaseCopyViewModel()
        {
            _copyService = new DatabaseCopyService();
            _backupService = ServiceContainer.BackupService; // استخدام ServiceContainer
            _availableDatabases = new ObservableCollection<string>();

            StartCopyCommand = new AsyncRelayCommand(StartCopyAsync);
            CancelCommand = new RelayCommand(Cancel);
            LoadDatabasesCommand = new AsyncRelayCommand(LoadDatabasesAsync);

            // تحميل قائمة قواعد البيانات عند بدء التشغيل
            _ = LoadDatabasesAsync(null);
        }

        private async Task LoadDatabasesAsync(object? parameter)
        {
            try
            {
                StatusText = "جاري تحميل قائمة قواعد البيانات...";
                var databases = await _backupService.GetDatabasesAsync();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableDatabases.Clear();
                    foreach (var db in databases)
                    {
                        AvailableDatabases.Add(db);
                    }
                });

                StatusText = $"تم تحميل {databases.Count} قاعدة بيانات.";
            }
            catch (Exception ex)
            {
                StatusText = $"خطأ في تحميل قواعد البيانات: {ex.Message}";
                System.Windows.MessageBox.Show($"فشل تحميل قواعد البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StartCopyAsync(object? parameter)
        {
            if (!CanStartCopy) return;

            // التحقق من عدم وجود قاعدة بيانات بنفس الاسم الجديد
            if (AvailableDatabases.Contains(TargetDatabaseName))
            {
                var result = System.Windows.MessageBox.Show(
                    $"قاعدة البيانات '{TargetDatabaseName}' موجودة بالفعل. هل تريد استبدالها؟",
                    "تأكيد الاستبدال",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    return;
            }

            IsBusy = true;
            var progress = new Progress<string>(update => StatusText = update);

            try
            {
                // تحضير سكربتات SQL
                var sqlScriptsList = PrepareSqlScripts();

                // تحضير تعديلات الملفات
                var fileReplacements = PrepareFileModifications();

                // بدء عملية النسخ
                await _copyService.CopyDatabaseWithModificationsAsync(
                    SourceDatabase,
                    TargetDatabaseName,
                    sqlScriptsList,
                    fileReplacements,
                    progress);

                System.Windows.MessageBox.Show(
                    $"✅ تم نسخ قاعدة البيانات بنجاح!\n\nقاعدة البيانات الجديدة: {TargetDatabaseName}",
                    "نجح النسخ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // إعادة تحميل قائمة قواعد البيانات
                await LoadDatabasesAsync(null);
            }
            catch (Exception ex)
            {
                StatusText = $"فشل النسخ: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"❌ فشل نسخ قاعدة البيانات:\n\n{ex.Message}",
                    "خطأ في النسخ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private List<string> PrepareSqlScripts()
        {
            var scripts = new List<string>();

            if (!string.IsNullOrWhiteSpace(SqlScripts))
            {
                // تقسيم السكربتات بناءً على كلمة GO
                var scriptParts = SqlScripts.Split(new[] { "GO", "go", "Go" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var script in scriptParts)
                {
                    var trimmedScript = script.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedScript))
                    {
                        scripts.Add(trimmedScript);
                    }
                }
            }

            return scripts;
        }

        private Dictionary<string, string> PrepareFileModifications()
        {
            var modifications = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(FileModifications))
            {
                var lines = FileModifications.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.Contains('|'))
                    {
                        var parts = trimmedLine.Split('|');
                        if (parts.Length == 2)
                        {
                            string filePath = parts[0].Trim();
                            string replacements = parts[1].Trim();

                            if (File.Exists(filePath))
                            {
                                modifications[filePath] = replacements;
                            }
                        }
                    }
                }
            }

            return modifications;
        }

        private void Cancel(object? parameter)
        {
            if (parameter is Window window)
            {
                window.Close();
            }
        }
    }
}

using BackupDbServer.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BackupDbServer.ViewModels
{
    public class SqlExecutorViewModel : ViewModelBase
    {
        private List<string> _selectedDatabases = new();
        private string _sqlScript = string.Empty;
        private string _executionResults = string.Empty;
        private bool _isBusy;

        public List<string> SelectedDatabases
        {
            get => _selectedDatabases;
            set
            {
                _selectedDatabases = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDatabasesText));
            }
        }

        public string SelectedDatabasesText
        {
            get
            {
                if (!_selectedDatabases.Any())
                    return "لم يتم تحديد أي قواعد بيانات";

                return $"قواعد البيانات المحددة ({_selectedDatabases.Count}): {string.Join(", ", _selectedDatabases)}";
            }
        }

        public string SqlScript
        {
            get => _sqlScript;
            set
            {
                _sqlScript = value;
                OnPropertyChanged();
            }
        }

        public string ExecutionResults
        {
            get => _executionResults;
            set
            {
                _executionResults = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public ICommand ExecuteScriptCommand { get; }
        public ICommand LoadScriptFromFileCommand { get; }
        public ICommand SaveScriptToFileCommand { get; }

        public SqlExecutorViewModel()
        {
            ExecuteScriptCommand = new AsyncRelayCommand(ExecuteScriptAsync, CanExecuteScript);
            LoadScriptFromFileCommand = new RelayCommand(LoadScriptFromFile);
            SaveScriptToFileCommand = new RelayCommand(SaveScriptToFile);
        }

        private bool CanExecuteScript(object? parameter)
        {
            return !IsBusy && !string.IsNullOrWhiteSpace(SqlScript) && SelectedDatabases.Any();
        }

        private async Task ExecuteScriptAsync(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(SqlScript) || !SelectedDatabases.Any())
            {
                System.Windows.MessageBox.Show("الرجاء إدخال سكربت SQL وتحديد قواعد البيانات.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            ExecutionResults = "بدء تنفيذ السكربت...\n";

            try
            {
                var settings = SettingsManager.LoadSettings();
                var connectionString =  $"Server={settings. Server};User ID={ settings.Username};Password={settings.Password};TrustServerCertificate=True;";

        var totalDatabases = SelectedDatabases.Count;
                var successCount = 0;
                var errorCount = 0;

                ExecutionResults += $"سيتم تنفيذ السكربت على {totalDatabases} قاعدة بيانات...\n\n";

                foreach (var dbName in SelectedDatabases)
                {
                    try
                    {
                        ExecutionResults += $"[{DateTime.Now:HH:mm:ss}] بدء تنفيذ السكربت على قاعدة البيانات: {dbName}\n";

                        var dbConnectionString = UpdateConnectionStringForDatabase(connectionString, dbName);
                        await ExecuteScriptOnDatabaseAsync(dbConnectionString, dbName);

                        ExecutionResults += $"[{DateTime.Now:HH:mm:ss}]  تم تنفيذ السكربت بنجاح على قاعدة البيانات: {dbName}\n\n";
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        ExecutionResults += $"[{DateTime.Now:HH:mm:ss}]  فشل تنفيذ السكربت على قاعدة البيانات {dbName}: {ex.Message}\n\n";
                        errorCount++;
                    }

                    await Task.Delay(100);
                }

                ExecutionResults += new string('=', 50) + "\n";
                ExecutionResults += $"نتائج التنفيذ:\n";
                ExecutionResults += $" نجح: {successCount} قاعدة بيانات\n";
                ExecutionResults += $" فشل: {errorCount} قاعدة بيانات\n";
                ExecutionResults += $" المجموع: {totalDatabases} قاعدة بيانات\n";

                if (errorCount == 0)
                {
                    System.Windows.MessageBox.Show($"تم تنفيذ السكربت بنجاح على جميع قواعد البيانات ({successCount}/{totalDatabases})", 
                        "نجح التنفيذ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show($"تم التنفيذ مع وجود أخطاء:\nنجح: {successCount}\nفشل: {errorCount}\n\nالرجاء مراجعة نتائج التنفيذ للتفاصيل.", 
                        "تحذير", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ExecutionResults += $" خطأ عام: {ex.Message}\n";
                System.Windows.MessageBox.Show($"حدث خطأ أثناء تنفيذ السكربت: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteScriptOnDatabaseAsync(string connectionString, string databaseName)
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var batches = SplitScriptIntoBatches(SqlScript);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;

                using var command = new SqlCommand(batch, connection);
                command.CommandTimeout = 300;

                await command.ExecuteNonQueryAsync();
            }
        }

        private List<string> SplitScriptIntoBatches(string script)
        {
            var batches = new List<string>();
            var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var currentBatch = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentBatch.Length > 0)
                    {
                        batches.Add(currentBatch.ToString());
                        currentBatch.Clear();
                    }
                }
                else
                {
                    currentBatch.AppendLine(line);
                }
            }

            if (currentBatch.Length > 0)
            {
                batches.Add(currentBatch.ToString());
            }

            return batches;
        }

        private string UpdateConnectionStringForDatabase(string originalConnectionString, string databaseName)
        {
            var builder = new SqlConnectionStringBuilder(originalConnectionString);
            builder.InitialCatalog = databaseName;
            return builder.ConnectionString;
        }

        private void LoadScriptFromFile(object? parameter)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "اختر ملف السكربت",
                Filter = "SQL Files (*.sql)|*.sql|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "sql"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    SqlScript = File.ReadAllText(openFileDialog.FileName, Encoding.UTF8);
                    ExecutionResults += $"تم تحميل السكربت من: {openFileDialog.FileName}\n";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"فشل تحميل الملف: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void SaveScriptToFile(object? parameter)
        {
            if (string.IsNullOrWhiteSpace(SqlScript))
            {
                System.Windows.MessageBox.Show("لا يوجد سكربت للحفظ.", "تنبيه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new System.Windows.Forms.SaveFileDialog
            {
                Title = "حفظ السكربت",
                Filter = "SQL Files (*.sql)|*.sql|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                DefaultExt = "sql",
                FileName = $"script_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.sql"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, SqlScript, Encoding.UTF8);
                    ExecutionResults += $"تم حفظ السكربت في: {saveFileDialog.FileName}\n";
                    System.Windows.MessageBox.Show("تم حفظ السكربت بنجاح.", "نجح الحفظ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"فشل حفظ الملف: {ex.Message}", "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BackupDbServer.Models;
using BackupDbServer.Helpers;
using MessageBox = System.Windows.MessageBox;

namespace BackupDbServer.Views
{
    public partial class DailyTaskEditorWindow : Window
    {
        private readonly DailyTaskConfig _task;

        public DailyTaskEditorWindow(DailyTaskConfig task)
        {
            InitializeComponent();
            _task = task;
            LoadTaskTypes();
            LoadTaskData();
        }

        private void LoadTaskTypes()
        {
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "نص SQL", Tag = TaskType.SqlScript });
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "إجراء مخزن", Tag = TaskType.StoredProcedure });
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "نسخ احتياطي", Tag = TaskType.BackupDatabase });
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "فحص السلامة", Tag = TaskType.CheckIntegrity });
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "تحديث الإحصائيات", Tag = TaskType.UpdateStatistics });
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "إعادة بناء الفهارس", Tag = TaskType.RebuildIndexes });
            TaskTypeComboBox.Items.Add(new ComboBoxItem { Content = "ضغط القاعدة", Tag = TaskType.ShrinkDatabase });
        }

        private void LoadTaskData()
        {
            TaskNameTextBox.Text = _task.TaskName;
            IsEnabledCheckBox.IsChecked = _task.IsEnabled;
            
            // تحميل قائمة قواعد البيانات
            LoadDatabases();
            
            // تعيين قاعدة البيانات الحالية
            if (!string.IsNullOrEmpty(_task.DatabaseName))
            {
                DatabaseNameComboBox.Text = _task.DatabaseName;
            }
            
            HourTextBox.Text = _task.ExecutionTime.Hour.ToString("00");
            MinuteTextBox.Text = _task.ExecutionTime.Minute.ToString("00");
            
            SqlScriptTextBox.Text = _task.SqlScript;
            StoredProcedureTextBox.Text = _task.StoredProcedureName;

            // اختيار نوع المهمة
            foreach (ComboBoxItem item in TaskTypeComboBox.Items)
            {
                if ((TaskType)item.Tag == _task.Type)
                {
                    TaskTypeComboBox.SelectedItem = item;
                    break;
                }
            }

            UpdateFieldsVisibility();
        }

        private void TaskTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFieldsVisibility();
        }

        private void LoadDatabases()
        {
            try
            {
                var config = SettingsManager.LoadSettings();
                var databases = GetDatabaseList(config);
                
                DatabaseNameComboBox.Items.Clear();
                foreach (var db in databases)
                {
                    DatabaseNameComboBox.Items.Add(db);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تحميل قائمة قواعد البيانات: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private List<string> GetDatabaseList(BackupConfig config)
        {
            var databases = new List<string>();
            
            try
            {
                var builder = new SqlConnectionStringBuilder();
                
                // تحديد DataSource بناءً على نوع الاتصال (نفس منطق الصفحة الرئيسية)
                switch (config.ConnectionType)
                {
                    case Models.ConnectionType.Local:
                        builder.DataSource = config.Port != 1433 ? $"{config.Server},{config.Port}" : (config.Server ?? "localhost");
                        break;
                    case Models.ConnectionType.Docker:
                        builder.DataSource = config.Port != 1433 ? $"localhost,{config.Port}" : "localhost,1433";
                        break;
                    case Models.ConnectionType.Remote:
                        builder.DataSource = config.Port != 1433 ? $"{config.Server},{config.Port}" : (config.Server ?? "localhost");
                        break;
                    default:
                        builder.DataSource = config.Server ?? "localhost";
                        break;
                }
                
                builder.IntegratedSecurity = config.UseWindowsAuthentication;
                builder.ConnectTimeout = config.ConnectionTimeout;
                builder.TrustServerCertificate = config.TrustServerCertificate;
                builder.Encrypt = config.Encrypt;

                if (!config.UseWindowsAuthentication)
                {
                    builder.UserID = config.Username;
                    builder.Password = config.Password;
                }

                using (var connection = new SqlConnection(builder.ToString()))
                {
                    connection.Open();
                    using (var command = new SqlCommand("SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb') ORDER BY name", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                databases.Add(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في جلب قواعد البيانات: {ex.Message}");
            }
            
            return databases;
        }

        private void RefreshDatabases_Click(object sender, RoutedEventArgs e)
        {
            LoadDatabases();
        }

        private void UpdateFieldsVisibility()
        {
            if (TaskTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var taskType = (TaskType)selectedItem.Tag;

                // إخفاء جميع الحقول أولاً
                SqlScriptLabel.Visibility = Visibility.Collapsed;
                SqlScriptTextBox.Visibility = Visibility.Collapsed;
                StoredProcLabel.Visibility = Visibility.Collapsed;
                StoredProcedureTextBox.Visibility = Visibility.Collapsed;

                // إظهار الحقول المطلوبة
                switch (taskType)
                {
                    case TaskType.SqlScript:
                        SqlScriptLabel.Visibility = Visibility.Visible;
                        SqlScriptTextBox.Visibility = Visibility.Visible;
                        break;
                    case TaskType.StoredProcedure:
                        StoredProcLabel.Visibility = Visibility.Visible;
                        StoredProcedureTextBox.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // التحقق من الحقول
                if (string.IsNullOrWhiteSpace(TaskNameTextBox.Text))
                {
                    MessageBox.Show("يرجى إدخال اسم المهمة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(DatabaseNameComboBox.Text))
                {
                    MessageBox.Show("يرجى اختيار أو إدخال اسم قاعدة البيانات", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(HourTextBox.Text, out int hour) || hour < 0 || hour > 23)
                {
                    MessageBox.Show("يرجى إدخال ساعة صحيحة (0-23)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(MinuteTextBox.Text, out int minute) || minute < 0 || minute > 59)
                {
                    MessageBox.Show("يرجى إدخال دقيقة صحيحة (0-59)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // حفظ البيانات
                _task.TaskName = TaskNameTextBox.Text;
                _task.IsEnabled = IsEnabledCheckBox.IsChecked == true;
                _task.DatabaseName = DatabaseNameComboBox.Text;
                _task.ExecutionTime = new TimeOnly(hour, minute);
                _task.SqlScript = SqlScriptTextBox.Text;
                _task.StoredProcedureName = StoredProcedureTextBox.Text;

                if (TaskTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    _task.Type = (TaskType)selectedItem.Tag;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في حفظ المهمة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
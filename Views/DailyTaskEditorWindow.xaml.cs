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
            LoadScheduleOptions();
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

        private void LoadScheduleOptions()
        {
            ScheduleModeComboBox.Items.Add(new ComboBoxItem { Content = "يومياً في وقت محدد", Tag = ScheduleMode.Daily });
            ScheduleModeComboBox.Items.Add(new ComboBoxItem { Content = "تكرار كل فترة", Tag = ScheduleMode.Interval });

            IntervalPresetComboBox.Items.Add(new ComboBoxItem { Content = "كل 30 دقيقة", Tag = 30 });
            IntervalPresetComboBox.Items.Add(new ComboBoxItem { Content = "كل ساعة", Tag = 60 });
            IntervalPresetComboBox.Items.Add(new ComboBoxItem { Content = "كل ساعتين", Tag = 120 });
            IntervalPresetComboBox.Items.Add(new ComboBoxItem { Content = "كل 6 ساعات", Tag = 360 });
            IntervalPresetComboBox.Items.Add(new ComboBoxItem { Content = "كل 12 ساعة", Tag = 720 });
            IntervalPresetComboBox.Items.Add(new ComboBoxItem { Content = "مخصص", Tag = 0 });
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

            // اختيار نمط الجدولة
            foreach (ComboBoxItem item in ScheduleModeComboBox.Items)
            {
                if ((ScheduleMode)item.Tag == _task.Mode)
                {
                    ScheduleModeComboBox.SelectedItem = item;
                    break;
                }
            }

            // مطابقة الفترة مع أحد الخيارات الجاهزة أو تفعيل "مخصص"
            ComboBoxItem? matchedPreset = null;
            foreach (ComboBoxItem item in IntervalPresetComboBox.Items)
            {
                if ((int)item.Tag == _task.IntervalMinutes)
                {
                    matchedPreset = item;
                    break;
                }
            }

            if (matchedPreset != null)
            {
                IntervalPresetComboBox.SelectedItem = matchedPreset;
            }
            else
            {
                // قيمة مخصصة: اختر خيار "مخصص" (Tag=0) واكتب القيمة
                foreach (ComboBoxItem item in IntervalPresetComboBox.Items)
                {
                    if ((int)item.Tag == 0)
                    {
                        IntervalPresetComboBox.SelectedItem = item;
                        break;
                    }
                }
                CustomMinutesTextBox.Text = _task.IntervalMinutes.ToString();
            }

            UpdateFieldsVisibility();
            UpdateScheduleModeVisibility();
        }

        private void TaskTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFieldsVisibility();
        }

        private void ScheduleModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateScheduleModeVisibility();
        }

        private void IntervalPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCustomMinutesVisibility();
        }

        private void UpdateScheduleModeVisibility()
        {
            // العناصر قد تكون null أثناء التهيئة قبل اكتمال بناء الواجهة
            if (DailyPanel == null || IntervalPanel == null)
                return;

            var isInterval = ScheduleModeComboBox.SelectedItem is ComboBoxItem selected
                             && (ScheduleMode)selected.Tag == ScheduleMode.Interval;

            DailyPanel.Visibility = isInterval ? Visibility.Collapsed : Visibility.Visible;
            IntervalPanel.Visibility = isInterval ? Visibility.Visible : Visibility.Collapsed;

            UpdateCustomMinutesVisibility();
        }

        private void UpdateCustomMinutesVisibility()
        {
            if (CustomMinutesTextBox == null || CustomMinutesLabel == null)
                return;

            var isCustom = IntervalPresetComboBox.SelectedItem is ComboBoxItem selected
                           && (int)selected.Tag == 0;

            CustomMinutesTextBox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            CustomMinutesLabel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
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

                var mode = ScheduleModeComboBox.SelectedItem is ComboBoxItem modeItem
                    ? (ScheduleMode)modeItem.Tag
                    : ScheduleMode.Daily;

                int hour = _task.ExecutionTime.Hour;
                int minute = _task.ExecutionTime.Minute;
                int intervalMinutes = _task.IntervalMinutes;

                if (mode == ScheduleMode.Daily)
                {
                    if (!int.TryParse(HourTextBox.Text, out hour) || hour < 0 || hour > 23)
                    {
                        MessageBox.Show("يرجى إدخال ساعة صحيحة (0-23)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!int.TryParse(MinuteTextBox.Text, out minute) || minute < 0 || minute > 59)
                    {
                        MessageBox.Show("يرجى إدخال دقيقة صحيحة (0-59)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else // ScheduleMode.Interval
                {
                    if (IntervalPresetComboBox.SelectedItem is not ComboBoxItem presetItem)
                    {
                        MessageBox.Show("يرجى اختيار الفترة الزمنية للتكرار", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var presetMinutes = (int)presetItem.Tag;
                    if (presetMinutes == 0)
                    {
                        // قيمة مخصصة
                        if (!int.TryParse(CustomMinutesTextBox.Text, out intervalMinutes) || intervalMinutes < 1)
                        {
                            MessageBox.Show("يرجى إدخال عدد دقائق صحيح (1 أو أكثر)", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        intervalMinutes = presetMinutes;
                    }
                }

                // حفظ البيانات
                _task.TaskName = TaskNameTextBox.Text;
                _task.IsEnabled = IsEnabledCheckBox.IsChecked == true;
                _task.DatabaseName = DatabaseNameComboBox.Text;
                _task.Mode = mode;
                _task.ExecutionTime = new TimeOnly(hour, minute);
                _task.IntervalMinutes = intervalMinutes;
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
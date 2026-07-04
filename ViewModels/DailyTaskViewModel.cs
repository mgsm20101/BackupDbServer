using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace BackupDbServer.ViewModels
{
    public class DailyTaskViewModel : ViewModelBase
    {
        private readonly DailyTaskService _taskService;
        private readonly ILogger _logger;
        private readonly System.Timers.Timer _refreshTimer;

        public DailyTaskViewModel(DailyTaskService taskService, ILogger logger)
        {
            _taskService = taskService;
            _logger = logger;
            
            Tasks = new ObservableCollection<DailyTaskConfig>();
            TaskTypes = new ObservableCollection<TaskTypeItem>
            {
                new TaskTypeItem { Type = TaskType.SqlScript, DisplayName = "نص SQL" },
                new TaskTypeItem { Type = TaskType.StoredProcedure, DisplayName = "إجراء مخزن" },
                new TaskTypeItem { Type = TaskType.BackupDatabase, DisplayName = "نسخ احتياطي" },
                new TaskTypeItem { Type = TaskType.CheckIntegrity, DisplayName = "فحص السلامة" },
                new TaskTypeItem { Type = TaskType.UpdateStatistics, DisplayName = "تحديث الإحصائيات" },
                new TaskTypeItem { Type = TaskType.RebuildIndexes, DisplayName = "إعادة بناء الفهارس" },
                new TaskTypeItem { Type = TaskType.ShrinkDatabase, DisplayName = "ضغط القاعدة" }
            };

            AddTaskCommand = new RelayCommand(AddTask);
            EditTaskCommand = new RelayCommand(EditTask, CanEditOrDeleteTask);
            DeleteTaskCommand = new RelayCommand(DeleteTask, CanEditOrDeleteTask);
            TestTaskCommand = new AsyncRelayCommand(TestTaskAsync, CanEditOrDeleteTask);
            RefreshCommand = new RelayCommand(Refresh);

            LoadTasks();
            
            // مؤقت لتحديث الوقت المتبقي كل دقيقة
            _refreshTimer = new System.Timers.Timer(60000); // 60 ثانية
            _refreshTimer.Elapsed += (s, e) => System.Windows.Application.Current.Dispatcher.Invoke(() => RefreshTimeRemaining());
            _refreshTimer.Start();
        }

        public ObservableCollection<DailyTaskConfig> Tasks { get; }
        public ObservableCollection<TaskTypeItem> TaskTypes { get; }

        private DailyTaskConfig? _selectedTask;
        public DailyTaskConfig? SelectedTask
        {
            get => _selectedTask;
            set
            {
                _selectedTask = value;
                OnPropertyChanged();
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "جاهز";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _serviceStatus = "";
        public string ServiceStatus
        {
            get => _serviceStatus;
            set { _serviceStatus = value; OnPropertyChanged(); }
        }

        public RelayCommand AddTaskCommand { get; }
        public RelayCommand EditTaskCommand { get; }
        public RelayCommand DeleteTaskCommand { get; }
        public AsyncRelayCommand TestTaskCommand { get; }
        public RelayCommand RefreshCommand { get; }

        private void LoadTasks()
        {
            Tasks.Clear();
            var tasks = _taskService.GetAllTasks();
            foreach (var task in tasks)
            {
                Tasks.Add(task);
            }
            
            var enabledTasks = Tasks.Count(t => t.IsEnabled);
            StatusMessage = $"تم تحميل {tasks.Count} مهمة ({enabledTasks} مفعلة)";
            UpdateServiceStatus();
        }

        private void UpdateServiceStatus()
        {
            var enabledTasks = Tasks.Where(t => t.IsEnabled).ToList();
            if (!enabledTasks.Any())
            {
                ServiceStatus = "⚠️ لا توجد مهام مفعلة";
                return;
            }

            var now = DateTime.Now;
            var nextTask = enabledTasks
                .Select(t => new 
                { 
                    Task = t, 
                    NextRun = new DateTime(now.Year, now.Month, now.Day, t.ExecutionTime.Hour, t.ExecutionTime.Minute, 0)
                })
                .Where(x => x.NextRun > now)
                .OrderBy(x => x.NextRun)
                .FirstOrDefault();

            if (nextTask != null)
            {
                var timeUntil = nextTask.NextRun - now;
                ServiceStatus = $"🕒 المهمة التالية: {nextTask.Task.TaskName} بعد {timeUntil.Hours:D2}:{timeUntil.Minutes:D2}";
            }
            else
            {
                var tomorrow = now.AddDays(1).Date;
                var firstTomorrow = enabledTasks
                    .OrderBy(t => t.ExecutionTime)
                    .FirstOrDefault();
                
                if (firstTomorrow != null)
                {
                    ServiceStatus = $"🌙 المهمة التالية: {firstTomorrow.TaskName} غداً الساعة {firstTomorrow.ExecutionTime:HH:mm}";
                }
                else
                {
                    ServiceStatus = $"✅ {enabledTasks.Count} مهام مفعلة";
                }
            }
        }

        private void AddTask(object? parameter)
        {
            var newTask = new DailyTaskConfig
            {
                TaskName = $"مهمة جديدة {Tasks.Count + 1}",
                ExecutionTime = new TimeOnly(2, 0)
            };

            var editor = new Views.DailyTaskEditorWindow(newTask);
            if (editor.ShowDialog() == true)
            {
                _taskService.AddTask(newTask);
                Tasks.Add(newTask);
                StatusMessage = $"تمت إضافة المهمة: {newTask.TaskName}";
            }
        }

        private void EditTask(object? parameter)
        {
            if (SelectedTask == null) return;

            var taskCopy = new DailyTaskConfig
            {
                TaskId = SelectedTask.TaskId,
                TaskName = SelectedTask.TaskName,
                IsEnabled = SelectedTask.IsEnabled,
                DatabaseName = SelectedTask.DatabaseName,
                ExecutionTime = SelectedTask.ExecutionTime,
                Type = SelectedTask.Type,
                SqlScript = SelectedTask.SqlScript,
                StoredProcedureName = SelectedTask.StoredProcedureName
            };

            var editor = new Views.DailyTaskEditorWindow(taskCopy);
            if (editor.ShowDialog() == true)
            {
                _taskService.UpdateTask(taskCopy);
                var index = Tasks.IndexOf(SelectedTask);
                Tasks[index] = taskCopy;
                SelectedTask = taskCopy;
                StatusMessage = $"تم تحديث المهمة: {taskCopy.TaskName}";
            }
        }

        private void DeleteTask(object? parameter)
        {
            if (SelectedTask == null) return;

            var result = MessageBox.Show(
                $"هل تريد حذف المهمة: {SelectedTask.TaskName}؟",
                "تأكيد الحذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _taskService.DeleteTask(SelectedTask.TaskId);
                Tasks.Remove(SelectedTask);
                StatusMessage = $"تم حذف المهمة: {SelectedTask.TaskName}";
                SelectedTask = null;
            }
        }

        private async Task TestTaskAsync(object? parameter)
        {
            if (SelectedTask == null) return;

            try
            {
                IsBusy = true;
                StatusMessage = $"جاري اختبار المهمة: {SelectedTask.TaskName}...";

                await _taskService.ExecuteTaskAsync(SelectedTask);

                if (SelectedTask.LastExecutionSuccess)
                {
                    StatusMessage = $"✅ نجح الاختبار: {SelectedTask.LastExecutionMessage}";
                    MessageBox.Show(
                        $"تم تنفيذ المهمة بنجاح!\n\n{SelectedTask.LastExecutionMessage}",
                        "نجح الاختبار",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = $"❌ فشل الاختبار: {SelectedTask.LastExecutionMessage}";
                    MessageBox.Show(
                        $"فشل تنفيذ المهمة!\n\n{SelectedTask.LastExecutionMessage}",
                        "فشل الاختبار",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                // تحديث القائمة
                var index = Tasks.IndexOf(SelectedTask);
                Tasks[index] = SelectedTask;
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ: {ex.Message}";
                MessageBox.Show($"خطأ في اختبار المهمة:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Refresh(object? parameter)
        {
            LoadTasks();
            UpdateServiceStatus();
        }
        
        private void RefreshTimeRemaining()
        {
            // تحديث عرض الوقت المتبقي عن طريق تحديث ObservableCollection
            // هذا سيجبر الواجهة على إعادة قراءة الخاصية المحسوبة TimeUntilNextRun
            var tasks = Tasks.ToList();
            Tasks.Clear();
            foreach (var task in tasks)
            {
                Tasks.Add(task);
            }
            UpdateServiceStatus();
        }

        private bool CanEditOrDeleteTask(object? parameter)
        {
            return SelectedTask != null && !IsBusy;
        }
        
        ~DailyTaskViewModel()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
        }
    }

    public class TaskTypeItem
    {
        public TaskType Type { get; set; }
        public string DisplayName { get; set; } = "";
    }
}
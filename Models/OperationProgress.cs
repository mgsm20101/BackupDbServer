using System;
using System.ComponentModel;

namespace BackupDbServer.Models
{
    /// <summary>
    /// نموذج لتتبع تقدم العمليات
    /// </summary>
    public class OperationProgress : INotifyPropertyChanged
    {
        private string _operationName = string.Empty;
        private string _currentStep = string.Empty;
        private int _progressPercentage;
        private bool _isRunning;
        private DateTime _startTime;
        private TimeSpan _estimatedTimeRemaining;
        private string _currentDatabase = string.Empty;
        private string _status = string.Empty;

        /// <summary>
        /// اسم العملية
        /// </summary>
        public string OperationName
        {
            get => _operationName;
            set
            {
                _operationName = value;
                OnPropertyChanged(nameof(OperationName));
            }
        }

        /// <summary>
        /// الخطوة الحالية
        /// </summary>
        public string CurrentStep
        {
            get => _currentStep;
            set
            {
                _currentStep = value;
                OnPropertyChanged(nameof(CurrentStep));
            }
        }

        /// <summary>
        /// نسبة التقدم (0-100)
        /// </summary>
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                _progressPercentage = value;
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }

        /// <summary>
        /// هل العملية تعمل حالياً
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(StatusIcon));
            }
        }

        /// <summary>
        /// وقت بداية العملية
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                OnPropertyChanged(nameof(StartTime));
                OnPropertyChanged(nameof(ElapsedTime));
            }
        }

        /// <summary>
        /// الوقت المقدر المتبقي
        /// </summary>
        public TimeSpan EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            set
            {
                _estimatedTimeRemaining = value;
                OnPropertyChanged(nameof(EstimatedTimeRemaining));
                OnPropertyChanged(nameof(EstimatedTimeRemainingText));
            }
        }

        /// <summary>
        /// قاعدة البيانات الحالية قيد المعالجة
        /// </summary>
        public string CurrentDatabase
        {
            get => _currentDatabase;
            set
            {
                _currentDatabase = value;
                OnPropertyChanged(nameof(CurrentDatabase));
            }
        }

        /// <summary>
        /// حالة العملية
        /// </summary>
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        /// <summary>
        /// الوقت المنقضي منذ بداية العملية
        /// </summary>
        public TimeSpan ElapsedTime 
        { 
            get 
            {
                if (IsRunning && StartTime != default)
                {
                    return DateTime.Now - StartTime;
                }
                return TimeSpan.Zero;
            }
        }

        /// <summary>
        /// تحديث الوقت المنقضي (يتم استدعاؤها من Timer)
        /// </summary>
        public void UpdateElapsedTime()
        {
            if (IsRunning)
            {
                OnPropertyChanged(nameof(ElapsedTime));
                OnPropertyChanged(nameof(EstimatedTimeRemainingText));
            }
        }

        /// <summary>
        /// النص المقدر للوقت المتبقي
        /// </summary>
        public string EstimatedTimeRemainingText
        {
            get
            {
                if (EstimatedTimeRemaining == TimeSpan.Zero)
                    return "غير محدد";

                if (EstimatedTimeRemaining.Hours > 0)
                    return $"{EstimatedTimeRemaining.Hours:00}:{EstimatedTimeRemaining.Minutes:00}:{EstimatedTimeRemaining.Seconds:00}";
                else
                    return $"{EstimatedTimeRemaining.Minutes:00}:{EstimatedTimeRemaining.Seconds:00}";
            }
        }

        /// <summary>
        /// أيقونة الحالة
        /// </summary>
        public string StatusIcon => IsRunning ? "🔄" : (ProgressPercentage == 100 ? "✅" : "⏸️");

        public event PropertyChangedEventHandler? PropertyChanged;

        public virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

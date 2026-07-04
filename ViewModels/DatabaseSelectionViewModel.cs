using System.Windows.Input;
using BackupDbServer.Helpers;
using BackupDbServer.Services;

namespace BackupDbServer.ViewModels
{
    public class DatabaseSelectionViewModel : ViewModelBase
    {
        private bool _isSelected;
        private string _databaseName = string.Empty;
        private string _status = "جاهز";
        private MaintenanceState _maintenanceState = MaintenanceState.Normal;

        public string DatabaseName
        {
            get => _databaseName;
            set => SetProperty(ref _databaseName, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>حالة صيانة XmlPath لهذه القاعدة.</summary>
        public MaintenanceState MaintenanceState
        {
            get => _maintenanceState;
            set
            {
                if (SetProperty(ref _maintenanceState, value))
                {
                    OnPropertyChanged(nameof(IsInMaintenanceMode));
                    OnPropertyChanged(nameof(MaintenanceStatusText));
                }
            }
        }

        public bool IsInMaintenanceMode => _maintenanceState == MaintenanceState.Maintenance;

        /// <summary>نص حالة الصيانة المعروض في الواجهة.</summary>
        public string MaintenanceStatusText => _maintenanceState switch
        {
            MaintenanceState.Maintenance => "🔴 صيانة (XML موقوف)",
            MaintenanceState.Normal => "🟢 طبيعي",
            _ => "—"
        };

        // أوامر تُحقَن من MainViewModel (لتفادي إعطاء العنصر مرجع الخدمة مباشرة)
        public ICommand? EnableMaintenanceCommand { get; set; }
        public ICommand? DisableMaintenanceCommand { get; set; }
        public ICommand? RefreshMaintenanceCommand { get; set; }
    }
}

using BackupDbServer.Helpers;

namespace BackupDbServer.ViewModels
{
    public class DatabaseSelectionViewModel : ViewModelBase
    {
        private bool _isSelected;
        private string _databaseName = string.Empty;
        private string _status = "جاهز";

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
    }
}

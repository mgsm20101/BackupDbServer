using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Service;

namespace BackupDbServer.ViewModels
{
    public class DeployViewModel : ViewModelBase
    {
        private readonly DeployService _deployService;
        private readonly ILogger _logger;

        public DeployViewModel(DeployService deployService, ILogger logger)
        {
            _deployService = deployService;
            _logger = logger;
            Config = new DeployConfig();
            DeployCommand = new AsyncRelayCommand(DeployAsync);
            BrowseBakFileCommand = new RelayCommand(BrowseBakFile);
            BrowseZipFileCommand = new RelayCommand(BrowseZipFile);
        }

        public DeployViewModel() : this(new DeployService(ServiceContainer.Logger), ServiceContainer.Logger)
        {
        }

        public DeployConfig Config { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusText = "جاهز للنشر";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public AsyncRelayCommand DeployCommand { get; }
        public RelayCommand BrowseBakFileCommand { get; }
        public RelayCommand BrowseZipFileCommand { get; }

        private async Task DeployAsync(object? parameter)
        {
            try
            {
                IsBusy = true;
                StatusText = "جاري النشر...";

                await Task.Run(() => _deployService.DeployApplication(Config));

                StatusText = "تم النشر بنجاح ✅";
                System.Windows.MessageBox.Show("تم النشر بنجاح!", "نجح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في النشر: {ex.Message}", ex);
                StatusText = $"فشل النشر: {ex.Message}";
                System.Windows.MessageBox.Show($"فشل النشر: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BrowseBakFile(object? parameter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ملفات النسخ الاحتياطي (*.bak)|*.bak|جميع الملفات (*.*)|*.*",
                Title = "اختر ملف النسخ الاحتياطي"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Config.BakPath = dialog.FileName;
                OnPropertyChanged(nameof(Config));
            }
        }

        private void BrowseZipFile(object? parameter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ملفات مضغوطة (*.zip)|*.zip|جميع الملفات (*.*)|*.*",
                Title = "اختر ملف الحزمة المضغوطة"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                Config.ZipPackagePath = dialog.FileName;
                OnPropertyChanged(nameof(Config));
            }
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using BackupDbServer.Helpers;
using BackupDbServer.Models;
using BackupDbServer.Services;

namespace BackupDbServer.ViewModels
{
    /// <summary>
    /// ViewModel لصيانة وتسريع IIS (تعداد/تحسين Application Pools، إعادة تدوير، iisreset).
    /// يُستضاف داخل تبويب «إدارة IIS» في النافذة الرئيسية.
    /// </summary>
    public class IisMaintenanceViewModel : ViewModelBase
    {
        private readonly IisMaintenanceService _iisService;
        private readonly ILogger _logger;

        public IisMaintenanceViewModel(ILogger logger)
        {
            _logger = logger;
            _iisService = new IisMaintenanceService(logger);

            RefreshPoolsCommand = new AsyncRelayCommand(RefreshPoolsAsync);
            OptimizeSelectedPoolCommand = new AsyncRelayCommand(OptimizeSelectedPoolAsync);
            OptimizeAllPoolsCommand = new AsyncRelayCommand(OptimizeAllPoolsAsync);
            RecyclePoolCommand = new AsyncRelayCommand(RecyclePoolAsync);
            RestartIisCommand = new AsyncRelayCommand(RestartIisAsync);

            IsAdministrator = CheckIsAdministrator();
        }

        public IisMaintenanceViewModel() : this(ServiceContainer.Logger) { }

        public ObservableCollection<AppPoolInfo> AppPools { get; } = new();

        private AppPoolInfo? _selectedAppPool;
        public AppPoolInfo? SelectedAppPool
        {
            get => _selectedAppPool;
            set { _selectedAppPool = value; OnPropertyChanged(); }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusText = "اضغط 'تحديث القائمة' لعرض Application Pools.";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsAdministrator { get; }
        public bool IsNotAdministrator => !IsAdministrator;

        public AsyncRelayCommand RefreshPoolsCommand { get; }
        public AsyncRelayCommand OptimizeSelectedPoolCommand { get; }
        public AsyncRelayCommand OptimizeAllPoolsCommand { get; }
        public AsyncRelayCommand RecyclePoolCommand { get; }
        public AsyncRelayCommand RestartIisCommand { get; }

        private async Task RefreshPoolsAsync(object? parameter)
        {
            await RunIisOperationAsync("تحميل قائمة Application Pools", () =>
            {
                var pools = _iisService.ListApplicationPools();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AppPools.Clear();
                    foreach (var p in pools) AppPools.Add(p);
                });
                StatusText = $"تم تحميل {pools.Count} Application Pool.";
                return Task.CompletedTask;
            });
        }

        private async Task OptimizeSelectedPoolAsync(object? parameter)
        {
            if (SelectedAppPool == null)
            {
                System.Windows.MessageBox.Show("الرجاء تحديد Application Pool أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var name = SelectedAppPool.Name;
            await RunIisOperationAsync($"تحسين {name}", async () =>
            {
                await _iisService.OptimizeApplicationPoolAsync(name);
                StatusText = $"تم تحسين {name}.";
            });
            await RefreshPoolsAsync(null);
        }

        private async Task OptimizeAllPoolsAsync(object? parameter)
        {
            await RunIisOperationAsync("تحسين جميع الـ Pools", async () =>
            {
                await _iisService.OptimizeAllAsync();
                StatusText = "تم تحسين جميع الـ Application Pools.";
            });
            await RefreshPoolsAsync(null);
        }

        private async Task RecyclePoolAsync(object? parameter)
        {
            if (SelectedAppPool == null)
            {
                System.Windows.MessageBox.Show("الرجاء تحديد Application Pool أولاً.", "تنبيه",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var name = SelectedAppPool.Name;
            await RunIisOperationAsync($"إعادة تدوير {name}", async () =>
            {
                await _iisService.RecycleAppPoolAsync(name);
                StatusText = $"تم إعادة تدوير {name}.";
            });
            await RefreshPoolsAsync(null);
        }

        private async Task RestartIisAsync(object? parameter)
        {
            var confirm = System.Windows.MessageBox.Show(
                "سيتم إعادة تشغيل خدمة IIS بالكامل (iisreset /restart) وقطع جميع المواقع مؤقتاً. متابعة؟",
                "تأكيد إعادة تشغيل IIS", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            await RunIisOperationAsync("إعادة تشغيل IIS", async () =>
            {
                var output = await _iisService.RestartIisAsync();
                StatusText = "تم إعادة تشغيل IIS.\n" + output;
            });
        }

        /// <summary>غلاف موحّد لعمليات IIS: فحص الصلاحيات + IsBusy + معالجة الأخطاء.</summary>
        private async Task RunIisOperationAsync(string actionName, Func<Task> action)
        {
            if (!IsAdministrator)
            {
                System.Windows.MessageBox.Show(
                    "تتطلب عمليات IIS تشغيل البرنامج كمسؤول (Administrator).",
                    "صلاحيات غير كافية", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsBusy = true;
                StatusText = $"جاري {actionName}...";
                await action();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[IIS] فشل {actionName}: {ex.Message}", ex);
                StatusText = $"فشل {actionName}: {ex.Message}";
                System.Windows.MessageBox.Show($"فشل {actionName}:\n{ex.Message}", "خطأ",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static bool CheckIsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BackupDbServer.Helpers;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// معالج الأخطاء العامة والحماية من الإغلاق المفاجئ
    /// </summary>
    public static class CrashProtectionManager
    {
        private static ILogger? _logger;
        private static bool _isInitialized = false;

        /// <summary>
        /// تهيئة نظام الحماية من الأخطاء
        /// </summary>
        public static void Initialize(ILogger logger)
        {
            if (_isInitialized)
                return;

            _logger = logger;

            // معالجة الأخطاء غير المعالجة في UI Thread
            System.Windows.Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

            // معالجة الأخطاء في Background Threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // معالجة أخطاء Task
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // معالجة إغلاق التطبيق
            System.Windows.Application.Current.SessionEnding += OnSessionEnding;
            System.Windows.Application.Current.Exit += OnApplicationExit;

            // معالجة إشارات النظام للإغلاق
            Console.CancelKeyPress += OnCancelKeyPress;

            _isInitialized = true;
            _logger?.LogInformation("تم تفعيل نظام الحماية من الأخطاء والإغلاق المفاجئ");
        }

        /// <summary>
        /// معالجة الأخطاء في UI Thread
        /// </summary>
        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var errorMessage = $"خطأ غير معالج في واجهة المستخدم: {e.Exception.Message}\n" +
                                 $"النوع: {e.Exception.GetType().Name}\n" +
                                 $"المصدر: {e.Exception.Source}\n" +
                                 $"التفاصيل: {e.Exception.StackTrace}";

                _logger?.LogError(errorMessage, e.Exception);

                // محاولة إظهار رسالة للمستخدم
                ShowCrashDialog("خطأ في واجهة المستخدم", e.Exception);

                // منع الإغلاق المفاجئ
                e.Handled = true;
            }
            catch (Exception ex)
            {
                // في حالة فشل التسجيل، نحفظ في ملف طوارئ
                SaveEmergencyLog($"فشل في معالجة خطأ UI: {ex.Message}\nالخطأ الأصلي: {e.Exception.Message}");
            }
        }

        /// <summary>
        /// معالجة الأخطاء في Background Threads
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                var errorMessage = $"خطأ غير معالج في العملية الخلفية: {exception?.Message ?? "خطأ غير معروف"}\n" +
                                 $"النوع: {exception?.GetType().Name ?? "غير معروف"}\n" +
                                 $"الإنهاء: {e.IsTerminating}\n" +
                                 $"التفاصيل: {exception?.StackTrace ?? "غير متوفر"}";

                _logger?.LogError(errorMessage, exception);

                if (e.IsTerminating)
                {
                    // إذا كان النظام سينهي التطبيق، نحفظ حالة طوارئ
                    SaveEmergencyBackupState();
                    ShowCrashDialog("خطأ حرج - سيتم إغلاق البرنامج", exception);
                }
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"فشل في معالجة خطأ Background: {ex.Message}");
            }
        }

        /// <summary>
        /// معالجة أخطاء Task غير المراقبة
        /// </summary>
        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                var errorMessage = $"خطأ Task غير مراقب: {e.Exception.Message}\n" +
                                 $"عدد الاستثناءات: {e.Exception.InnerExceptions.Count}\n" +
                                 $"التفاصيل: {e.Exception}";

                _logger?.LogError(errorMessage, e.Exception);

                // منع إنهاء التطبيق
                e.SetObserved();
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"فشل في معالجة خطأ Task: {ex.Message}");
            }
        }

        /// <summary>
        /// معالجة إنهاء جلسة Windows
        /// </summary>
        private static void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            try
            {
                var reason = e.ReasonSessionEnding == ReasonSessionEnding.Logoff ? "تسجيل خروج" : "إيقاف تشغيل";
                _logger?.LogInformation($"تم اكتشاف إنهاء جلسة Windows: {reason}");

                // حفظ حالة طوارئ
                SaveEmergencyBackupState();

                // إعطاء وقت إضافي للحفظ
                Task.Delay(2000).Wait();
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"خطأ في معالجة إنهاء الجلسة: {ex.Message}");
            }
        }

        /// <summary>
        /// معالجة إغلاق التطبيق العادي
        /// </summary>
        private static void OnApplicationExit(object sender, ExitEventArgs e)
        {
            try
            {
                _logger?.LogInformation($"إغلاق التطبيق بكود: {e.ApplicationExitCode}");
                
                if (e.ApplicationExitCode != 0)
                {
                    SaveEmergencyBackupState();
                }
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"خطأ في معالجة إغلاق التطبيق: {ex.Message}");
            }
        }

        /// <summary>
        /// معالجة Ctrl+C أو إشارات الإيقاف
        /// </summary>
        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            try
            {
                _logger?.LogInformation("تم اكتشاف إشارة إيقاف (Ctrl+C)");
                SaveEmergencyBackupState();

                // منع الإغلاق المباشر لإعطاء وقت للحفظ
                e.Cancel = true;
                
                // الإغلاق بشكل منظم بعد الحفظ
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown(0));
                });
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"خطأ في معالجة إشارة الإيقاف: {ex.Message}");
            }
        }

        /// <summary>
        /// حفظ حالة طوارئ للتطبيق
        /// </summary>
        private static void SaveEmergencyBackupState()
        {
            try
            {
                var emergencyData = new
                {
                    Timestamp = DateTime.Now,
                    LastKnownState = "Emergency shutdown detected",
                    ProcessId = Environment.ProcessId,
                    MachineName = Environment.MachineName,
                    UserName = Environment.UserName,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                var emergencyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emergency_state.json");
                var json = System.Text.Json.JsonSerializer.Serialize(emergencyData, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                File.WriteAllText(emergencyFile, json);
                _logger?.LogInformation($"تم حفظ حالة الطوارئ في: {emergencyFile}");
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"فشل في حفظ حالة الطوارئ: {ex.Message}");
            }
        }

        /// <summary>
        /// حفظ لوج طوارئ عند فشل النظام العادي
        /// </summary>
        private static void SaveEmergencyLog(string message)
        {
            try
            {
                var logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emergency.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // آخر محاولة - استخدام EventLog Windows
                try
                {
                    System.Diagnostics.EventLog.WriteEntry("Application", 
                        $"BackupDbServer Emergency: {message}", 
                        System.Diagnostics.EventLogEntryType.Error);
                }
                catch
                {
                    // لا يمكن فعل شيء أكثر
                }
            }
        }

        /// <summary>
        /// إظهار حوار خطأ للمستخدم
        /// </summary>
        private static void ShowCrashDialog(string title, Exception? exception)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var message = $"حدث خطأ في البرنامج:\n\n{exception?.Message ?? "خطأ غير معروف"}\n\nسيتم حفظ تفاصيل الخطأ في ملف السجل.";
                    System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            }
            catch
            {
                // إذا فشل إظهار MessageBox، لا نفعل شيء
            }
        }

        /// <summary>
        /// التحقق من وجود حالة طوارئ سابقة عند بدء التطبيق
        /// </summary>
        public static bool CheckForPreviousCrash()
        {
            try
            {
                var emergencyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emergency_state.json");
                if (File.Exists(emergencyFile))
                {
                    var json = File.ReadAllText(emergencyFile);
                    _logger?.LogWarning($"تم اكتشاف إغلاق مفاجئ سابق: {json}");
                    
                    // نقل الملف لملف archive
                    var archiveFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                        $"crash_archive_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    File.Move(emergencyFile, archiveFile);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"خطأ في فحص الإغلاق المفاجئ السابق: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// إلغاء تفعيل نظام الحماية
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.DispatcherUnhandledException -= OnDispatcherUnhandledException;
                    System.Windows.Application.Current.SessionEnding -= OnSessionEnding;
                    System.Windows.Application.Current.Exit -= OnApplicationExit;
                }

                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                Console.CancelKeyPress -= OnCancelKeyPress;

                _logger?.LogInformation("تم إلغاء تفعيل نظام الحماية من الأخطاء");
                _isInitialized = false;
            }
            catch (Exception ex)
            {
                SaveEmergencyLog($"خطأ في تنظيف نظام الحماية: {ex.Message}");
            }
        }
    }
}

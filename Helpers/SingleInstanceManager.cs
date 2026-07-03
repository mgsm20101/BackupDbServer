using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// إدارة تشغيل نسخة واحدة فقط من التطبيق
    /// </summary>
    public class SingleInstanceManager
    {
        private static Mutex? _mutex;
        private const string MUTEX_NAME = "BackupDbServer_SingleInstance";
        private const string PIPE_NAME = "BackupDbServer_Pipe";

        /// <summary>
        /// التحقق من أن التطبيق هو النسخة الوحيدة المشغلة
        /// </summary>
        /// <returns>true إذا كان هذا هو التشغيل الأول، false إذا كان هناك نسخة أخرى تعمل</returns>
        public static bool IsFirstInstance()
        {
            bool isFirstInstance;
            _mutex = new Mutex(true, MUTEX_NAME, out isFirstInstance);

            if (!isFirstInstance)
            {
                // إرسال إشارة للنسخة الموجودة لإظهار النافذة
                NotifyExistingInstance();
                return false;
            }

            // بدء الاستماع للإشارات من النسخ الأخرى
            StartListening();
            return true;
        }

        /// <summary>
        /// إرسال إشارة للنسخة الموجودة لإظهار النافذة
        /// </summary>
        private static void NotifyExistingInstance()
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
                client.Connect(2000); // انتظار لمدة ثانيتين
                using var writer = new StreamWriter(client);
                writer.WriteLine("SHOW_WINDOW");
                writer.Flush();
            }
            catch
            {
                // في حالة فشل الاتصال، لا نفعل شيئاً
            }
        }

        /// <summary>
        /// بدء الاستماع للإشارات من النسخ الأخرى
        /// </summary>
        private static void StartListening()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.In);
                        await server.WaitForConnectionAsync();
                        
                        using var reader = new StreamReader(server);
                        var message = await reader.ReadLineAsync();
                        
                        if (message == "SHOW_WINDOW")
                        {
                            // إظهار النافذة الرئيسية في الـ UI Thread
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                var mainWindow = System.Windows.Application.Current.MainWindow;
                                if (mainWindow != null)
                                {
                                    // بوابة القفل: اطلب كلمة السر قبل الإظهار إن كان مقفلاً
                                    var appLock = ServiceContainer.AppLock;
                                    if (appLock.IsEnabled && appLock.IsLocked)
                                    {
                                        if (!appLock.TryUnlock(mainWindow))
                                        {
                                            return;
                                        }
                                    }

                                    if (mainWindow.WindowState == WindowState.Minimized)
                                    {
                                        mainWindow.WindowState = WindowState.Normal;
                                    }
                                    
                                    mainWindow.Show();
                                    mainWindow.Activate();
                                    mainWindow.BringIntoView();
                                    
                                    // التأكد من إحضار النافذة للمقدمة
                                    if (!mainWindow.Topmost)
                                    {
                                        mainWindow.Topmost = true;
                                        mainWindow.Topmost = false;
                                    }
                                }
                            });
                        }
                    }
                    catch
                    {
                        // في حالة حدوث خطأ، نواصل الاستماع
                        await Task.Delay(1000);
                    }
                }
            });
        }

        /// <summary>
        /// تنظيف الموارد عند إغلاق التطبيق
        /// </summary>
        public static void Cleanup()
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }
}

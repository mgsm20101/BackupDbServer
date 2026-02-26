using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// مساعد للتعامل مع مسارات الشبكة (Network/UNC Paths)
    /// </summary>
    public static class NetworkPathHelper
    {
        /// <summary>
        /// التحقق من أن المسار هو مسار شبكة (UNC Path)
        /// </summary>
        /// <param name="path">المسار المراد فحصه</param>
        /// <returns>true إذا كان مسار شبكة</returns>
        public static bool IsNetworkPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                // مسار UNC يبدأ بـ \\ أو //
                if (path.StartsWith(@"\\") || path.StartsWith("//"))
                    return true;

                // فحص إذا كان mapped network drive
                var rootPath = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(rootPath))
                {
                    var driveInfo = new DriveInfo(rootPath);
                    return driveInfo.DriveType == DriveType.Network;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// التحقق من توفر مسار الشبكة
        /// </summary>
        /// <param name="path">مسار الشبكة</param>
        /// <param name="timeoutSeconds">مهلة الانتظار بالثواني</param>
        /// <returns>true إذا كان المسار متاحاً</returns>
        public static async Task<bool> IsNetworkPathAvailableAsync(string path, int timeoutSeconds = 5)
        {
            if (!IsNetworkPath(path))
                return Directory.Exists(path); // مسار محلي - فحص عادي

            try
            {
                // استخراج اسم الخادم من UNC path
                var serverName = ExtractServerName(path);
                
                if (string.IsNullOrEmpty(serverName))
                    return Directory.Exists(path);

                // 1. فحص الاتصال بالخادم أولاً (أسرع)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var pingAvailable = await PingServerAsync(serverName, cts.Token);
                
                if (!pingAvailable)
                    return false;

                // 2. فحص المسار نفسه
                return await Task.Run(() => Directory.Exists(path), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return false; // Timeout
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// استخراج اسم الخادم من UNC path
        /// </summary>
        private static string? ExtractServerName(string uncPath)
        {
            try
            {
                if (uncPath.StartsWith(@"\\"))
                {
                    var parts = uncPath.Substring(2).Split('\\', '/');
                    return parts.Length > 0 ? parts[0] : null;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// فحص الاتصال بالخادم عبر Ping
        /// </summary>
        private static async Task<bool> PingServerAsync(string serverName, CancellationToken cancellationToken)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(serverName, 2000); // 2 ثانية timeout
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                // قد يكون Ping معطل على الخادم، نجرب الاتصال المباشر
                return true;
            }
        }

        /// <summary>
        /// تنفيذ عملية على مسار شبكة مع Retry Logic
        /// </summary>
        public static async Task<T?> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            int maxRetries = 3,
            int delayMilliseconds = 1000,
            ILogger? logger = null)
        {
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < maxRetries)
            {
                try
                {
                    return await operation();
                }
                catch (IOException ex) when (IsNetworkError(ex))
                {
                    lastException = ex;
                    attempt++;
                    
                    if (attempt < maxRetries)
                    {
                        logger?.LogWarning($"محاولة {attempt}/{maxRetries} فشلت: {ex.Message}. إعادة المحاولة...");
                        await Task.Delay(delayMilliseconds * attempt); // Exponential backoff
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger?.LogError($"خطأ في الصلاحيات: {ex.Message}", ex);
                    throw; // لا نعيد المحاولة في حالة مشاكل الصلاحيات
                }
            }

            logger?.LogError($"فشلت جميع المحاولات ({maxRetries})", lastException);
            return default;
        }

        /// <summary>
        /// تنفيذ عملية على مسار شبكة مع Retry Logic (بدون قيمة إرجاع)
        /// </summary>
        public static async Task<bool> ExecuteWithRetryAsync(
            Func<Task> operation,
            int maxRetries = 3,
            int delayMilliseconds = 1000,
            ILogger? logger = null)
        {
            var result = await ExecuteWithRetryAsync(async () =>
            {
                await operation();
                return true;
            }, maxRetries, delayMilliseconds, logger);

            return result;
        }

        /// <summary>
        /// فحص إذا كان الخطأ متعلق بالشبكة
        /// </summary>
        private static bool IsNetworkError(Exception ex)
        {
            if (ex is IOException ioEx)
            {
                var hResult = ioEx.HResult;
                
                // أكواد أخطاء الشبكة الشائعة
                return hResult == unchecked((int)0x80070035) || // ERROR_BAD_NETPATH
                       hResult == unchecked((int)0x80070043) || // ERROR_BAD_NET_NAME
                       hResult == unchecked((int)0x80070003) || // ERROR_PATH_NOT_FOUND (قد يكون شبكة)
                       hResult == unchecked((int)0x80070033) || // ERROR_REM_NOT_LIST
                       ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                       ioEx.Message.Contains("شبكة", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// حساب حجم المجلد مع دعم مسارات الشبكة
        /// </summary>
        public static async Task<long> GetDirectorySizeAsync(
            string path,
            int timeoutSeconds = 30,
            ILogger? logger = null)
        {
            try
            {
                var isNetwork = IsNetworkPath(path);
                
                if (isNetwork)
                {
                    logger?.LogInformation($"📡 حساب حجم مجلد شبكة: {path}");
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                
                return await Task.Run(() =>
                {
                    try
                    {
                        var directory = new DirectoryInfo(path);
                        long size = 0;
                        
                        foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
                        {
                            if (cts.Token.IsCancellationRequested)
                                break;
                                
                            size += file.Length;
                        }
                        
                        return size;
                    }
                    catch
                    {
                        return 0;
                    }
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger?.LogWarning($"⏱️ انتهى وقت حساب حجم المجلد: {path}");
                return 0;
            }
            catch (Exception ex)
            {
                logger?.LogError($"خطأ في حساب حجم المجلد: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        /// الحصول على معلومات مسار الشبكة
        /// </summary>
        public static NetworkPathInfo GetNetworkPathInfo(string path)
        {
            var info = new NetworkPathInfo
            {
                OriginalPath = path,
                IsNetworkPath = IsNetworkPath(path)
            };

            if (info.IsNetworkPath)
            {
                try
                {
                    if (path.StartsWith(@"\\"))
                    {
                        var parts = path.Substring(2).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            info.ServerName = parts[0];
                            if (parts.Length > 1)
                            {
                                info.ShareName = parts[1];
                            }
                        }
                        info.PathType = "UNC";
                    }
                    else
                    {
                        var rootPath = Path.GetPathRoot(path);
                        if (!string.IsNullOrEmpty(rootPath))
                        {
                            var driveInfo = new DriveInfo(rootPath);
                            if (driveInfo.DriveType == DriveType.Network)
                            {
                                info.PathType = "Mapped Drive";
                                info.DriveLetter = rootPath.TrimEnd('\\', '/');
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    info.Error = ex.Message;
                }
            }
            else
            {
                info.PathType = "Local";
            }

            return info;
        }

        /// <summary>
        /// معلومات مسار الشبكة
        /// </summary>
        public class NetworkPathInfo
        {
            public string OriginalPath { get; set; } = "";
            public bool IsNetworkPath { get; set; }
            public string PathType { get; set; } = "Unknown";
            public string? ServerName { get; set; }
            public string? ShareName { get; set; }
            public string? DriveLetter { get; set; }
            public string? Error { get; set; }

            public string GetDescription()
            {
                if (!IsNetworkPath)
                    return $"📁 مسار محلي: {OriginalPath}";

                if (!string.IsNullOrEmpty(Error))
                    return $"⚠️ خطأ في تحليل المسار: {Error}";

                return PathType switch
                {
                    "UNC" => $"📡 UNC Path: \\\\{ServerName}\\{ShareName}",
                    "Mapped Drive" => $"🗺️ Mapped Drive: {DriveLetter} (شبكة)",
                    _ => $"📡 مسار شبكة: {OriginalPath}"
                };
            }
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

namespace BackupDbServer.Helpers
{
    /// <summary>
    /// بيانات قفل التطبيق المخزّنة على القرص (لا تحتوي كلمة السر نصاً — فقط تجزئتها)
    /// </summary>
    public class AppLockData
    {
        public bool IsEnabled { get; set; } = false;
        public string? PasswordHash { get; set; }
        public string? Salt { get; set; }
        public int Iterations { get; set; } = 100_000;
    }

    /// <summary>
    /// إدارة قفل التطبيق بكلمة سر.
    /// - تخزين تجزئة كلمة السر (PBKDF2 / HMACSHA256) في app_lock.json — بدون نص صريح.
    /// - إدارة حالة القفل في الذاكرة (IsLocked) وبوابة فك القفل المشتركة (TryUnlock).
    /// </summary>
    public class AppLockManager
    {
        private const int SaltSize = 16;   // 128-bit
        private const int HashSize = 32;   // 256-bit
        private const int DefaultIterations = 100_000;

        // الطريق الخلفي: ملف استرجاع سري — وجوده يُلغي القفل ويُحذف بعدها.
        private const string ResetFileName = "RESET_PASSWORD.flag";

        private readonly string _filePath;
        private readonly string _resetFilePath;
        private readonly object _lock = new object();
        private AppLockData _data;

        public AppLockManager()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BackupDbServer");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "app_lock.json");
            _resetFilePath = Path.Combine(dir, ResetFileName);
            _data = Load();

            // فحص ملف الاسترجاع عند بدء التشغيل
            CheckAndApplyResetFile();
        }

        /// <summary>المسار الكامل لملف الاسترجاع السري (للعرض/التوثيق).</summary>
        public string ResetFilePath => _resetFilePath;

        /// <summary>هل تم تعيين كلمة سر (موجودة تجزئتها)؟</summary>
        public bool IsPasswordSet =>
            !string.IsNullOrEmpty(_data.PasswordHash) && !string.IsNullOrEmpty(_data.Salt);

        /// <summary>هل الحماية مفعّلة فعلياً (مُفعّلة + كلمة سر موجودة)؟</summary>
        public bool IsEnabled => _data.IsEnabled && IsPasswordSet;

        /// <summary>هل التطبيق مقفل حالياً ويتطلب كلمة سر للإظهار؟</summary>
        public bool IsLocked { get; private set; }

        /// <summary>تفعيل القفل (يُتجاهَل إن لم تكن الحماية مفعّلة).</summary>
        public void Lock()
        {
            if (IsEnabled)
            {
                IsLocked = true;
            }
        }

        /// <summary>فك القفل بعد التحقق الناجح.</summary>
        public void Unlock() => IsLocked = false;

        /// <summary>
        /// تعيين/تغيير كلمة السر وتفعيل الحماية. يُخزَّن salt + hash جديدان.
        /// </summary>
        public void SetPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("كلمة السر لا يمكن أن تكون فارغة", nameof(password));

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = DeriveHash(password, salt, DefaultIterations);

            lock (_lock)
            {
                _data.Salt = Convert.ToBase64String(salt);
                _data.PasswordHash = Convert.ToBase64String(hash);
                _data.Iterations = DefaultIterations;
                _data.IsEnabled = true;
                Save();
            }
        }

        /// <summary>التحقق من مطابقة كلمة السر (مقارنة ثابتة الزمن).</summary>
        public bool VerifyPassword(string password)
        {
            if (!IsPasswordSet || string.IsNullOrEmpty(password))
                return false;

            try
            {
                var salt = Convert.FromBase64String(_data.Salt!);
                var expected = Convert.FromBase64String(_data.PasswordHash!);
                var actual = DeriveHash(password, salt, _data.Iterations);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>تغيير كلمة السر بعد التحقق من القديمة.</summary>
        public bool ChangePassword(string oldPassword, string newPassword)
        {
            if (!VerifyPassword(oldPassword))
                return false;

            SetPassword(newPassword);
            return true;
        }

        /// <summary>تعطيل الحماية مع التحقق من كلمة السر الحالية.</summary>
        public bool Disable(string currentPassword)
        {
            if (IsPasswordSet && !VerifyPassword(currentPassword))
                return false;

            ForceDisable();
            return true;
        }

        /// <summary>
        /// تعطيل الحماية دون إعادة تحقق — يُستدعى فقط بعد التحقق من كلمة السر عبر نافذة فك القفل.
        /// </summary>
        public void ForceDisable()
        {
            lock (_lock)
            {
                _data.IsEnabled = false;
                IsLocked = false;
                Save();
            }
        }

        /// <summary>
        /// إعادة تعيين كاملة للقفل: مسح كلمة السر وتعطيل الحماية (للاسترجاع عند النسيان).
        /// </summary>
        public void ResetLock()
        {
            lock (_lock)
            {
                _data.IsEnabled = false;
                _data.PasswordHash = null;
                _data.Salt = null;
                IsLocked = false;
                Save();
            }
        }

        /// <summary>
        /// الطريق الخلفي: إن وُجد ملف الاسترجاع السري، يُلغى القفل ويُحذف الملف.
        /// يُرجِع true إذا تمّت إعادة التعيين.
        /// </summary>
        public bool CheckAndApplyResetFile()
        {
            try
            {
                if (File.Exists(_resetFilePath))
                {
                    ResetLock();
                    try { File.Delete(_resetFilePath); } catch { /* تجاهل فشل الحذف */ }

                    try { ServiceContainer.Logger.LogWarning($"⚠️ تم إلغاء قفل البرنامج عبر ملف الاسترجاع: {_resetFilePath}"); }
                    catch { /* تجاهل أخطاء التسجيل */ }

                    return true;
                }
            }
            catch
            {
                // تجاهل أخطاء الوصول للملف
            }
            return false;
        }

        /// <summary>
        /// بوابة فك القفل المشتركة: تُظهر نافذة كلمة السر إن كان التطبيق مقفلاً.
        /// تُرجِع true إذا لم يكن هناك قفل أو نجح فك القفل، false إذا أُلغي/فشل.
        /// </summary>
        public bool TryUnlock(Window? owner = null)
        {
            // الطريق الخلفي: فحص ملف الاسترجاع السري قبل طلب كلمة السر
            if (CheckAndApplyResetFile())
            {
                System.Windows.MessageBox.Show(
                    "تم إلغاء قفل البرنامج عبر ملف الاسترجاع.\nيمكنك تعيين كلمة سر جديدة من الإعدادات.",
                    "استرجاع كلمة السر", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return true;
            }

            if (!IsEnabled || !IsLocked)
                return true;

            var dialog = new Views.PasswordPromptWindow(this, Views.PasswordPromptMode.Unlock);
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }

            var ok = dialog.ShowDialog() == true;
            if (ok)
            {
                Unlock();
            }
            return ok;
        }

        private static byte[] DeriveHash(string password, byte[] salt, int iterations)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(HashSize);
        }

        private AppLockData Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var data = JsonSerializer.Deserialize<AppLockData>(json);
                    if (data != null)
                        return data;
                }
            }
            catch
            {
                // في حالة تلف الملف، نبدأ بإعدادات افتراضية (بدون حماية)
            }
            return new AppLockData();
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // تجاهل أخطاء الكتابة بصمت — لا نريد تعطيل التطبيق بسبب فشل حفظ القفل
            }
        }
    }
}

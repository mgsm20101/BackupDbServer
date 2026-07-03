
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BackupDbServer.Helpers
{
    public static class CompressionHelper
    {
        /// <summary>
        /// ضغط ملف النسخة الاحتياطية مع دعم Retention Policy
        /// </summary>
        /// <param name="backupFilePath">مسار ملف .bak</param>
        /// <param name="progress">Progress handler</param>
        /// <param name="useRetentionPolicy">استخدام سياسة الاحتفاظ (مجلدات بالتاريخ)</param>
        /// <returns>مسار الملف المضغوط</returns>
        public static async Task<string> CompressBackupFileAsync(string backupFilePath, IProgress<string> progress = null, bool useRetentionPolicy = true)
        {
            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException($"ملف النسخة الاحتياطية غير موجود: {backupFilePath}");
            }

            var fileName = Path.GetFileName(backupFilePath);
            string zipFilePath;

            try
            {
                progress?.Report($"بدء ضغط الملف: {fileName}");

                // الحصول على حجم الملف الأصلي
                var originalFileInfo = new FileInfo(backupFilePath);
                var originalSizeMB = Math.Round(originalFileInfo.Length / (1024.0 * 1024.0), 2);
                progress?.Report($"حجم الملف الأصلي: {originalSizeMB} MB");

                // تحديد مجلد الوجهة حسب سياسة الاحتفاظ
                string destinationFolder;
                if (useRetentionPolicy)
                {
                    var baseBackupPath = Path.GetDirectoryName(backupFilePath);
                    var retentionPolicy = ServiceContainer.RetentionPolicy;
                    var backupDate = DateTime.Now;
                    
                    // إنشاء مجلد بتاريخ اليوم (YYYY-MM-DD)
                    destinationFolder = retentionPolicy.CreateDatedBackupFolder(baseBackupPath, backupDate);
                    progress?.Report($"📁 المجلد المستهدف: {Path.GetFileName(destinationFolder)}");
                }
                else
                {
                    destinationFolder = Path.GetDirectoryName(backupFilePath);
                }

                // تحديد مسار ملف ZIP في المجلد الجديد
                var zipFileName = Path.ChangeExtension(fileName, ".zip");
                zipFilePath = Path.Combine(destinationFolder, zipFileName);

                // إنشاء ملف ZIP على خيط خلفي (عملية متزامنة ثقيلة على المعالج — يجب ألا تعمل على خيط الواجهة)
                await Task.Run(() =>
                {
                    using var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
                    // إضافة الملف إلى الأرشيف مع ضغط أقصى
                    zipArchive.CreateEntryFromFile(backupFilePath, fileName, CompressionLevel.Optimal);
                });
                progress?.Report($"📦 تم إنشاء الأرشيف المضغوط: {zipFileName}");

                // حساب نسبة الضغط
                var compressedFileInfo = new FileInfo(zipFilePath);
                var compressedSizeMB = Math.Round(compressedFileInfo.Length / (1024.0 * 1024.0), 2);
                var compressionRatio = Math.Round(((originalFileInfo.Length - compressedFileInfo.Length) / (double)originalFileInfo.Length) * 100, 1);

                progress?.Report($"✅ تم الضغط بنجاح!");
                progress?.Report($"الحجم بعد الضغط: {compressedSizeMB} MB");
                progress?.Report($"نسبة الضغط: {compressionRatio}%");

                // حذف الملف الأصلي بعد الضغط الناجح
                File.Delete(backupFilePath);
                progress?.Report($"🗑️ تم حذف الملف الأصلي");

                // تطبيق سياسة الاحتفاظ (حذف النسخ القديمة) - مع دعم مسارات الشبكة
                if (useRetentionPolicy)
                {
                    progress?.Report($"🔍 تطبيق سياسة الاحتفاظ...");
                    var baseBackupPath = Path.GetDirectoryName(destinationFolder);
                    var retentionPolicy = ServiceContainer.RetentionPolicy;
                    var deletedCount = await retentionPolicy.ApplyRetentionPolicy(baseBackupPath);
                    
                    if (deletedCount > 0)
                    {
                        progress?.Report($"🧹 تم حذف {deletedCount} نسخة قديمة حسب سياسة الاحتفاظ");
                    }
                }

                return zipFilePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل في ضغط الملف: {ex.Message}", ex);
            }
        }

        public static async Task<string> DecompressBackupFileAsync(string zipFilePath, string extractPath = null)
        {
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException($"ملف ZIP غير موجود: {zipFilePath}");
            }

            try
            {
                extractPath ??= Path.GetDirectoryName(zipFilePath);

                using var zipArchive = ZipFile.OpenRead(zipFilePath);
                
                // البحث عن ملف .bak في الأرشيف
                ZipArchiveEntry bakEntry = null;
                foreach (var entry in zipArchive.Entries)
                {
                    if (entry.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    {
                        bakEntry = entry;
                        break;
                    }
                }

                if (bakEntry == null)
                {
                    throw new InvalidOperationException("لا يوجد ملف .bak في الأرشيف المضغوط");
                }

                var extractedFilePath = Path.Combine(extractPath, bakEntry.Name);
                
                // استخراج الملف
                bakEntry.ExtractToFile(extractedFilePath, overwrite: true);

                return extractedFilePath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل في استخراج الملف من الأرشيف: {ex.Message}", ex);
            }
        }

        public static bool IsCompressedBackup(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<(long OriginalSize, long CompressedSize, double CompressionRatio)> GetCompressionInfoAsync(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
            {
                throw new FileNotFoundException($"ملف ZIP غير موجود: {zipFilePath}");
            }

            try
            {
                using var zipArchive = ZipFile.OpenRead(zipFilePath);
                
                long originalSize = 0;
                long compressedSize = 0;

                foreach (var entry in zipArchive.Entries)
                {
                    originalSize += entry.Length;
                    compressedSize += entry.CompressedLength;
                }

                var compressionRatio = originalSize > 0 ? 
                    ((originalSize - compressedSize) / (double)originalSize) * 100 : 0;

                return (originalSize, compressedSize, compressionRatio);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل في قراءة معلومات الضغط: {ex.Message}", ex);
            }
        }
    }
}
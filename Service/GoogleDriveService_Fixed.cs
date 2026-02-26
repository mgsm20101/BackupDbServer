using BackupDbServer.Helpers;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.IO;

namespace BackupDbServer.Services
{
    public class GoogleDriveServiceFixed : IGoogleDriveService
    {
        private DriveService? _driveService;
        private readonly ILogger _logger;
        private readonly string _credentialsPath;
        private readonly string[] _scopes = { DriveService.Scope.Drive };

        public GoogleDriveServiceFixed(ILogger logger, string credentialsPath = "credentials.json")
        {
            _logger = logger;
            _credentialsPath = credentialsPath;
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                if (!File.Exists(_credentialsPath))
                {
                    _logger.LogError($"ملف الاعتماد غير موجود: {_credentialsPath}");
                    return false;
                }

                UserCredential credential;
                using (var stream = new FileStream(_credentialsPath, FileMode.Open, FileAccess.Read))
                {
                    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        _scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore("DriveCredentials", true));
                }

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Database Backup Manager"
                });

                _logger.LogInformation("تم تهيئة Google Drive بنجاح");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل تهيئة Google Drive: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<string> UploadFileAsync(string filePath, string fileName, string? folderId = null)
        {
            if (_driveService == null)
            {
                _logger.LogError("Google Drive غير مهيأ");
                return string.Empty;
            }

            if (!File.Exists(filePath))
            {
                _logger.LogError($"الملف غير موجود: {filePath}");
                return string.Empty;
            }
            
            try
            {
                var fileInfo = new FileInfo(filePath);
                _logger.LogInformation($"بدء رفع الملف: {fileName} (حجم: {fileInfo.Length / 1024 / 1024} MB)");

                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = fileName,
                    Parents = folderId != null ? new List<string> { folderId } : null
                };

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var request = _driveService.Files.Create(fileMetadata, stream, GetMimeType(filePath));
                    request.Fields = "id";
                    request.ChunkSize = 1024 * 1024; // 1MB chunks

                    var response = await request.UploadAsync();

                    if (response.Status == Google.Apis.Upload.UploadStatus.Completed)
                    {
                        var fileId = request.ResponseBody?.Id ?? string.Empty;
                        _logger.LogInformation($"تم رفع الملف بنجاح: {fileName} (ID: {fileId})");
                        return fileId;
                    }
                    else
                    {
                        _logger.LogError($"فشل رفع الملف: {fileName} - الحالة: {response.Status}");
                        if (response.Exception != null)
                        {
                            _logger.LogError($"خطأ في الرفع: {response.Exception.Message}", response.Exception);
                        }
                        return string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"خطأ في رفع الملف {fileName}: {ex.Message}", ex);
                return string.Empty;
            }
        }

        public async Task<string> CreateFolderAsync(string folderName, string credentialsPath, string? parentFolderId = null)
        {
            if (_driveService == null)
            {
                _logger.LogError("Google Drive غير مهيأ");
                return string.Empty;
            }

            try
            {
                // البحث عن المجلد أولاً
                var existingFolderId = await FindFolderByNameAsync(folderName, parentFolderId);
                if (!string.IsNullOrEmpty(existingFolderId))
                {
                    _logger.LogInformation($"تم العثور على المجلد الموجود: {folderName}");
                    return existingFolderId;
                }

                // إنشاء المجلد إذا لم يكن موجوداً
                var folderMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = folderName,
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = parentFolderId != null ? new List<string> { parentFolderId } : null
                };

                var request = _driveService.Files.Create(folderMetadata);
                request.Fields = "id";
                var folder = await request.ExecuteAsync();

                _logger.LogInformation($"تم إنشاء المجلد الجديد: {folderName}");
                return folder.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل إنشاء/العثور على المجلد {folderName}: {ex.Message}", ex);
                return string.Empty;
            }
        }

        private async Task<string> FindFolderByNameAsync(string folderName, string? parentFolderId = null)
        {
            if (_driveService == null) return string.Empty;

            try
            {
                var request = _driveService.Files.List();
                
                // البحث في المجلد الأب إذا تم تحديده، وإلا في الجذر
                if (!string.IsNullOrEmpty(parentFolderId))
                {
                    request.Q = $"name='{folderName}' and mimeType='application/vnd.google-apps.folder' and '{parentFolderId}' in parents and trashed=false";
                }
                else
                {
                    request.Q = $"name='{folderName}' and mimeType='application/vnd.google-apps.folder' and trashed=false";
                }
                
                request.Fields = "files(id,name)";

                var response = await request.ExecuteAsync();
                var folder = response.Files?.FirstOrDefault();

                return folder?.Id ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل البحث عن المجلد {folderName}: {ex.Message}", ex);
                return string.Empty;
            }
        }

        public async Task<bool> DeleteFileAsync(string fileId)
        {
            if (_driveService == null)
            {
                _logger.LogError("Google Drive غير مهيأ");
                return false;
            }

            try
            {
                await _driveService.Files.Delete(fileId).ExecuteAsync();
                _logger.LogInformation($"تم حذف الملف: {fileId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل حذف الملف {fileId}: {ex.Message}", ex);
                return false;
            }
        }

        public async Task<List<Google.Apis.Drive.v3.Data.File>> GetFilesInFolderAsync(string folderId)
        {
            if (_driveService == null)
            {
                _logger.LogError("Google Drive غير مهيأ");
                return new List<Google.Apis.Drive.v3.Data.File>();
            }

            try
            {
                var request = _driveService.Files.List();
                request.Q = $"'{folderId}' in parents and trashed=false";
                request.Fields = "files(id,name,size,createdTime)";

                var response = await request.ExecuteAsync();
                return response.Files?.ToList() ?? new List<Google.Apis.Drive.v3.Data.File>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل في الحصول على الملفات في المجلد {folderId}: {ex.Message}", ex);
                return new List<Google.Apis.Drive.v3.Data.File>();
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                if (_driveService == null) return false;

                var request = _driveService.About.Get();
                request.Fields = "user";
                var about = await request.ExecuteAsync();

                return about.User != null;
            }
            catch
            {
                return false;
            }
        }

        private string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".bak" => "application/octet-stream",
                ".sql" => "text/plain",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                _ => "application/octet-stream"
            };
        }

        public async Task<bool> InitializeAsync(string credentialsPath)
        {
            try
            {
                if (!File.Exists(credentialsPath))
                {
                    _logger.LogError($"ملف الاعتماد غير موجود: {credentialsPath}");
                    return false;
                }

                UserCredential credential;
                using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
                {
                    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        _scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore("DriveCredentials", true));
                }

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Database Backup Manager"
                });

                _logger.LogInformation("تم تهيئة Google Drive بنجاح");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"فشل تهيئة Google Drive: {ex.Message}", ex);
                return false;
            }
        }

        public void UpdateCredentialsPath(string credentialsPath)
        {
            // في هذا التنفيذ، مسار الاعتماد ثابت
            // يمكن تحسين هذا لاحقاً إذا لزم الأمر
        }
    }
}

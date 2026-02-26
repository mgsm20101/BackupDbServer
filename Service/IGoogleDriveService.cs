namespace BackupDbServer.Services
{
    public interface IGoogleDriveService
    {
        Task<bool> InitializeAsync();
        Task<bool> InitializeAsync(string credentialsPath);
        void UpdateCredentialsPath(string credentialsPath);
        Task<string> UploadFileAsync(string filePath, string fileName, string? folderId = null);
        Task<string> CreateFolderAsync(string folderName, string credentialsPath, string? parentFolderId = null);
        Task<bool> DeleteFileAsync(string fileId);
        Task<List<Google.Apis.Drive.v3.Data.File>> GetFilesInFolderAsync(string folderId);
        Task<bool> IsAuthenticatedAsync();
    }
}

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BackupDbServer.Helpers;

namespace BackupDbServer.Services
{
    public class DockerSqlServerManager
    {
        private readonly ILogger _logger;

        public DockerSqlServerManager(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsDockerInstalledAsync()
        {
            try
            {
                var result = await RunDockerCommandAsync("--version");
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IsContainerRunningAsync(string containerName)
        {
            try
            {
                var result = await RunDockerCommandAsync($"ps -q -f name={containerName}");
                return result.Success && !string.IsNullOrWhiteSpace(result.Output);
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DoesContainerExistAsync(string containerName)
        {
            try
            {
                var result = await RunDockerCommandAsync($"ps -a -q -f name={containerName}");
                return result.Success && !string.IsNullOrWhiteSpace(result.Output);
            }
            catch
            {
                return false;
            }
        }

        public async Task<DockerCommandResult> CreateSqlServerContainerAsync(
            string containerName = "dbs-2022-CTP23-1r",
            string saPassword = "YourStrong@Passw0rd",
            int port = 1433)
        {
            var command = $"run -d " +
                         $"--name {containerName} " +
                         $"-e \"ACCEPT_EULA=Y\" " +
                         $"-e \"SA_PASSWORD={saPassword}\" " +
                         $"-p {port}:1433 " +
                         $"mcr.microsoft.com/mssql/server:2022-latest";

            var result = await RunDockerCommandAsync(command);
            
            if (result.Success)
            {
                _logger.LogInformation($"تم إنشاء حاوية SQL Server: {containerName}");
            }
            else
            {
                _logger.LogError($"فشل في إنشاء حاوية SQL Server: {result.Error}");
            }

            return result;
        }

        public async Task<DockerCommandResult> StartContainerAsync(string containerName)
        {
            var result = await RunDockerCommandAsync($"start {containerName}");
            
            if (result.Success)
            {
                _logger.LogInformation($"تم تشغيل الحاوية: {containerName}");
            }
            else
            {
                _logger.LogError($"فشل في تشغيل الحاوية: {result.Error}");
            }

            return result;
        }

        public async Task<DockerCommandResult> StopContainerAsync(string containerName)
        {
            var result = await RunDockerCommandAsync($"stop {containerName}");
            
            if (result.Success)
            {
                _logger.LogInformation($"تم إيقاف الحاوية: {containerName}");
            }
            else
            {
                _logger.LogError($"فشل في إيقاف الحاوية: {result.Error}");
            }

            return result;
        }

        public async Task<DockerCommandResult> RemoveContainerAsync(string containerName)
        {
            var result = await RunDockerCommandAsync($"rm {containerName}");
            
            if (result.Success)
            {
                _logger.LogInformation($"تم حذف الحاوية: {containerName}");
            }
            else
            {
                _logger.LogError($"فشل في حذف الحاوية: {result.Error}");
            }

            return result;
        }

        public async Task<string> GetContainerStatusAsync(string containerName)
        {
            try
            {
                var result = await RunDockerCommandAsync($"ps -a --format \"table {{{{.Status}}}}\" -f name={containerName}");
                
                if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
                {
                    var lines = result.Output.Split('\n');
                    if (lines.Length > 1)
                    {
                        return lines[1].Trim();
                    }
                }
                
                return "غير موجود";
            }
            catch
            {
                return "خطأ";
            }
        }

        private async Task<DockerCommandResult> RunDockerCommandAsync(string arguments)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "docker";
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                return new DockerCommandResult
                {
                    Success = process.ExitCode == 0,
                    Output = output?.Trim() ?? string.Empty,
                    Error = error?.Trim() ?? string.Empty,
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                return new DockerCommandResult
                {
                    Success = false,
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }
    }

    public class DockerCommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int ExitCode { get; set; }
    }
}
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class StorageCleanupService : BackgroundService
    {
        private readonly string? _tempPath;
        private readonly ILogger<StorageCleanupService> _logger;

        public StorageCleanupService(IConfiguration configuration, ILogger<StorageCleanupService> logger)
        {
            _logger = logger;
            var root = configuration["Storage:RootPath"];
            if (!string.IsNullOrEmpty(root))
            {
                _tempPath = Path.Combine(root, "temp");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Storage Cleanup Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CleanupTempDirectories();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up storage temp directory.");
                }

                // Chạy kiểm tra mỗi 6 giờ
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        private void CleanupTempDirectories()
        {
            if (string.IsNullOrEmpty(_tempPath) || !Directory.Exists(_tempPath))
            {
                return;
            }

            _logger.LogInformation("Scanning temp folder for orphaned chunks: {Path}", _tempPath);
            var tempDirs = Directory.GetDirectories(_tempPath);
            var threshold = DateTime.UtcNow.AddDays(-1);

            int deletedCount = 0;

            foreach (var dir in tempDirs)
            {
                var dirInfo = new DirectoryInfo(dir);
                // Nếu thư mục chunk tạm không có thay đổi gì trong 24 giờ qua thì coi như bị bỏ rơi (orphaned)
                if (dirInfo.LastWriteTimeUtc < threshold)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        _logger.LogInformation("Deleted orphaned temp folder: {Path}", dir);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete temp directory {Path}", dir);
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Storage cleanup completed. Deleted {Count} orphaned folders.", deletedCount);
            }
        }
    }
}

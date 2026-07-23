using System.IO;

namespace SystemCacheCleaner.Services;

public class DiskSpaceService : IDiskSpaceService
{
    public SystemDiskInfo GetSystemDiskInfo()
    {
        try
        {
            string systemPath = Environment.SystemDirectory;
            string driveName = Path.GetPathRoot(systemPath) ?? "C:\\";

            DriveInfo driveInfo = new DriveInfo(driveName);
            if (!driveInfo.IsReady)
            {
                return new SystemDiskInfo(
                    DriveName: driveName,
                    TotalBytes: 0,
                    FreeBytes: 0,
                    UsedBytes: 0,
                    UsedPercentage: 0,
                    IsAvailable: false,
                    StatusMessage: $"系统盘 ({driveName}) 未就绪"
                );
            }

            long totalBytes = driveInfo.TotalSize;
            long freeBytes = driveInfo.AvailableFreeSpace;
            long usedBytes = totalBytes - freeBytes;
            double usedPercentage = totalBytes > 0 ? (double)usedBytes / totalBytes * 100 : 0;

            return new SystemDiskInfo(
                DriveName: driveName.TrimEnd('\\'),
                TotalBytes: totalBytes,
                FreeBytes: freeBytes,
                UsedBytes: usedBytes,
                UsedPercentage: usedPercentage,
                IsAvailable: true,
                StatusMessage: "正常"
            );
        }
        catch (Exception ex)
        {
            return new SystemDiskInfo(
                DriveName: "C:",
                TotalBytes: 0,
                FreeBytes: 0,
                UsedBytes: 0,
                UsedPercentage: 0,
                IsAvailable: false,
                StatusMessage: $"读取系统盘信息失败: {ex.Message}"
            );
        }
    }
}

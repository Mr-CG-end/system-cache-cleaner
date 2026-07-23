namespace SystemCacheCleaner.Services;

public record SystemDiskInfo(
    string DriveName,
    long TotalBytes,
    long FreeBytes,
    long UsedBytes,
    double UsedPercentage,
    bool IsAvailable,
    string StatusMessage
);

public interface IDiskSpaceService
{
    SystemDiskInfo GetSystemDiskInfo();
}

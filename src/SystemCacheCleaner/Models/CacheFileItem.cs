namespace SystemCacheCleaner.Models;

public record CacheFileItem(
    string FilePath,
    string CategoryId,
    long FileSizeBytes,
    DateTime LastWriteTime
);

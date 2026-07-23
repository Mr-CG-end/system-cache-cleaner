namespace SystemCacheCleaner.Models;

public record CategoryScanResult(
    CacheCategoryDefinition Category,
    IReadOnlyList<CacheFileItem> Files,
    long TotalBytes,
    int FileCount,
    bool IsSelected,
    string StatusMessage
);

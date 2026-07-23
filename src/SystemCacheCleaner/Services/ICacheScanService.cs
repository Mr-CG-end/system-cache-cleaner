using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Services;

public record ScanProgressReport(
    string CurrentCategoryId,
    string CurrentCategoryName,
    int ProcessedCategories,
    int TotalCategories,
    int DiscoveredFiles,
    long DiscoveredBytes
);

public interface ICacheScanService
{
    Task<ScanSessionResult> ScanAsync(
        IReadOnlyList<CacheCategoryDefinition> categories,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
}

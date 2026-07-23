using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Services;

public record CleanupProgressReport(
    string CurrentFilePath,
    int ProcessedFiles,
    int TotalFiles,
    long TotalDeletedBytes
);

public interface ICleanupService
{
    Task<CleanupSessionResult> CleanupAsync(
        IReadOnlyList<CategoryScanResult> selectedCategoryResults,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
}

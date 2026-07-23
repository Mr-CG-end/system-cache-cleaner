namespace SystemCacheCleaner.Models;

public record CleanupSessionResult(
    DateTime StartTime,
    DateTime EndTime,
    int SuccessCount,
    int SkipCount,
    int FailCount,
    long TotalDeletedBytes,
    IReadOnlyList<CleanupFileResult> FileResults,
    bool IsCancelled
);

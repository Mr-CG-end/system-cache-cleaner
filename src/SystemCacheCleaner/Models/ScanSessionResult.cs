namespace SystemCacheCleaner.Models;

public record ScanSessionResult(
    DateTime StartTime,
    DateTime EndTime,
    IReadOnlyList<CategoryScanResult> CategoryResults,
    bool IsCancelled,
    string GlobalStatusSummary
);

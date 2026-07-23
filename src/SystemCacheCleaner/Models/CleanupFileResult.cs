namespace SystemCacheCleaner.Models;

public record CleanupFileResult(
    string FilePath,
    long FileSizeBytes,
    CleanupFileStatus Status,
    string Message
);

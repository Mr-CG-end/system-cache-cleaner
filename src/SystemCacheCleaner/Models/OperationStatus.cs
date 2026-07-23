namespace SystemCacheCleaner.Models;

public enum OperationStatus
{
    Idle,
    Scanning,
    ScanCompleted,
    Confirmation,
    Cleaning,
    CleanupCompleted,
    PartialCompleted
}

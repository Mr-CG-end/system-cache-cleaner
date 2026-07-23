namespace SystemCacheCleaner.Infrastructure;

public static class ByteSizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        double size = bytes;
        int unitIndex = 0;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        if (unitIndex == 0)
        {
            return $"{bytes} B";
        }

        return $"{size:N2} {Units[unitIndex]}";
    }
}

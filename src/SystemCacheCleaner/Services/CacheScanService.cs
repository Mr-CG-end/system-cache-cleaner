using System.IO;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Services;

public class CacheScanService : ICacheScanService
{
    public async Task<ScanSessionResult> ScanAsync(
        IReadOnlyList<CacheCategoryDefinition> categories,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.Now;
        List<CategoryScanResult> categoryResults = new List<CategoryScanResult>();
        bool isCancelled = false;
        int totalCategories = categories.Count;
        int processedCategories = 0;
        int globalDiscoveredFiles = 0;
        long globalDiscoveredBytes = 0;

        if (cancellationToken.IsCancellationRequested)
        {
            isCancelled = true;
        }
        else
        {
            try
            {
                await Task.Run(() =>
                {
                foreach (CacheCategoryDefinition category in categories)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        isCancelled = true;
                        break;
                    }

                    List<CacheFileItem> categoryFiles = new List<CacheFileItem>();
                    long categoryBytes = 0;
                    string statusMessage = "完成";
                    bool directoryFound = false;
                    bool categoryHadWarnings = false;

                    foreach (string rootPath in category.RootPaths)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            isCancelled = true;
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                        {
                            categoryHadWarnings = true;
                            continue;
                        }

                        directoryFound = true;
                        categoryHadWarnings |= ScanDirectoryRecursive(
                            rootPath,
                            category.CategoryId,
                            categoryFiles,
                            ref categoryBytes,
                            ref globalDiscoveredFiles,
                            ref globalDiscoveredBytes,
                            category,
                            processedCategories,
                            totalCategories,
                            progress,
                            cancellationToken
                        );

                        if (cancellationToken.IsCancellationRequested)
                        {
                            isCancelled = true;
                            break;
                        }
                    }

                    if (isCancelled)
                    {
                        statusMessage = "扫描已取消";
                    }
                    else if (!directoryFound)
                    {
                        statusMessage = "目录不存在，已跳过";
                    }
                    else if (categoryHadWarnings)
                    {
                        statusMessage = "扫描完成，但部分链接、无权限或读取失败的项目已跳过";
                    }

                    CategoryScanResult categoryResult = new CategoryScanResult(
                        Category: category,
                        Files: categoryFiles.AsReadOnly(),
                        TotalBytes: categoryBytes,
                        FileCount: categoryFiles.Count,
                        IsSelected: category.IsDefaultSelected,
                        StatusMessage: statusMessage
                    );

                    categoryResults.Add(categoryResult);
                    processedCategories++;

                    progress?.Report(new ScanProgressReport(
                        CurrentCategoryId: category.CategoryId,
                        CurrentCategoryName: category.DisplayName,
                        ProcessedCategories: processedCategories,
                        TotalCategories: totalCategories,
                        DiscoveredFiles: globalDiscoveredFiles,
                        DiscoveredBytes: globalDiscoveredBytes
                    ));

                    if (isCancelled)
                    {
                        break;
                    }
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            isCancelled = true;
        }
        }

        DateTime endTime = DateTime.Now;
        string globalStatus = isCancelled ? "扫描已被取消，结果可能不完整" : "扫描完成";

        return new ScanSessionResult(
            StartTime: startTime,
            EndTime: endTime,
            CategoryResults: categoryResults.AsReadOnly(),
            IsCancelled: isCancelled,
            GlobalStatusSummary: globalStatus
        );
    }

    private bool ScanDirectoryRecursive(
        string currentDir,
        string categoryId,
        List<CacheFileItem> categoryFiles,
        ref long categoryBytes,
        ref int globalDiscoveredFiles,
        ref long globalDiscoveredBytes,
        CacheCategoryDefinition category,
        int processedCategories,
        int totalCategories,
        IProgress<ScanProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // 重解析点 (Junction / Symlink) 检查，不进入链接目标内部
        if (PathSafety.HasReparsePoint(currentDir))
        {
            return true;
        }

        DirectoryInfo dirInfo = new DirectoryInfo(currentDir);
        bool hadWarnings = false;

        // 使用惰性枚举，以便在大型目录中逐项响应取消。
        try
        {
            foreach (FileInfo file in dirInfo.EnumerateFiles())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return hadWarnings;
                }

                try
                {
                    // 跳过带 ReparsePoint 的文件
                    if (PathSafety.HasReparsePoint(file.FullName))
                    {
                        hadWarnings = true;
                        continue;
                    }

                    long fileLen = file.Length;
                    DateTime lastWrite = file.LastWriteTime;

                    CacheFileItem item = new CacheFileItem(
                        FilePath: file.FullName,
                        CategoryId: categoryId,
                        FileSizeBytes: fileLen,
                        LastWriteTime: lastWrite
                    );

                    categoryFiles.Add(item);
                    categoryBytes += fileLen;
                    globalDiscoveredFiles++;
                    globalDiscoveredBytes += fileLen;

                    if (globalDiscoveredFiles % 20 == 0)
                    {
                        progress?.Report(new ScanProgressReport(
                            CurrentCategoryId: category.CategoryId,
                            CurrentCategoryName: category.DisplayName,
                            ProcessedCategories: processedCategories,
                            TotalCategories: totalCategories,
                            DiscoveredFiles: globalDiscoveredFiles,
                            DiscoveredBytes: globalDiscoveredBytes
                        ));
                    }
                }
                catch
                {
                    // 单个文件读取失败只跳过该文件，并向上层报告部分完成。
                    hadWarnings = true;
                }
            }
        }
        catch
        {
            // 无权限或目录枚举失败时保留已完成统计，并报告部分完成。
            hadWarnings = true;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return hadWarnings;
        }

        try
        {
            foreach (DirectoryInfo subDir in dirInfo.EnumerateDirectories())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return hadWarnings;
                }

                hadWarnings |= ScanDirectoryRecursive(
                    subDir.FullName,
                    categoryId,
                    categoryFiles,
                    ref categoryBytes,
                    ref globalDiscoveredFiles,
                    ref globalDiscoveredBytes,
                    category,
                    processedCategories,
                    totalCategories,
                    progress,
                    cancellationToken
                );
            }
        }
        catch
        {
            hadWarnings = true;
        }

        return hadWarnings;
    }
}

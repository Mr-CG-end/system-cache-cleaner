using System.IO;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Services;

public class CleanupService : ICleanupService
{
    public async Task<CleanupSessionResult> CleanupAsync(
        IReadOnlyList<CategoryScanResult> selectedCategoryResults,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.Now;
        List<CleanupFileResult> fileResults = new List<CleanupFileResult>();
        int successCount = 0;
        int skipCount = 0;
        int failCount = 0;
        long totalDeletedBytes = 0;
        bool isCancelled = false;

        // 收集所有需要处理的目标文件项
        List<(CacheCategoryDefinition Category, CacheFileItem FileItem)> targets = new List<(CacheCategoryDefinition, CacheFileItem)>();

        if (selectedCategoryResults != null)
        {
            foreach (CategoryScanResult catResult in selectedCategoryResults)
            {
                if (!catResult.IsSelected || catResult.Files == null)
                {
                    continue;
                }

                foreach (CacheFileItem fileItem in catResult.Files)
                {
                    targets.Add((catResult.Category, fileItem));
                }
            }
        }

        int totalFiles = targets.Count;
        int processedFiles = 0;

        await Task.Run(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                isCancelled = true;
                return;
            }

            foreach (var (category, fileItem) in targets)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    isCancelled = true;
                    break;
                }

                string filePath = fileItem.FilePath;

                // 1. 二次路径安全与重解析点防护校验
                if (!PathSafety.IsPathSafe(filePath, category.RootPaths))
                {
                    failCount++;
                    fileResults.Add(new CleanupFileResult(
                        FilePath: filePath,
                        FileSizeBytes: fileItem.FileSizeBytes,
                        Status: CleanupFileStatus.Failed,
                        Message: "安全校验未通过: 路径不在白名单根目录内或包含重解析点"
                    ));
                    processedFiles++;
                    ReportProgress(progress, filePath, processedFiles, totalFiles, totalDeletedBytes);
                    continue;
                }

                // 2. 文件不存在判定为跳过 (Skipped)
                if (!File.Exists(filePath))
                {
                    skipCount++;
                    fileResults.Add(new CleanupFileResult(
                        FilePath: filePath,
                        FileSizeBytes: fileItem.FileSizeBytes,
                        Status: CleanupFileStatus.Skipped,
                        Message: "文件已不存在，已跳过"
                    ));
                    processedFiles++;
                    ReportProgress(progress, filePath, processedFiles, totalFiles, totalDeletedBytes);
                    continue;
                }

                // 3. 执行单文件删除与异常隔离
                try
                {
                    File.Delete(filePath);
                    successCount++;
                    totalDeletedBytes += fileItem.FileSizeBytes;

                    fileResults.Add(new CleanupFileResult(
                        FilePath: filePath,
                        FileSizeBytes: fileItem.FileSizeBytes,
                        Status: CleanupFileStatus.Success,
                        Message: "删除成功"
                    ));
                }
                catch (Exception ex)
                {
                    failCount++;
                    fileResults.Add(new CleanupFileResult(
                        FilePath: filePath,
                        FileSizeBytes: fileItem.FileSizeBytes,
                        Status: CleanupFileStatus.Failed,
                        Message: $"删除失败: {ex.Message}"
                    ));
                }

                processedFiles++;
                ReportProgress(progress, filePath, processedFiles, totalFiles, totalDeletedBytes);
            }
        }).ConfigureAwait(false);

        DateTime endTime = DateTime.Now;

        return new CleanupSessionResult(
            StartTime: startTime,
            EndTime: endTime,
            SuccessCount: successCount,
            SkipCount: skipCount,
            FailCount: failCount,
            TotalDeletedBytes: totalDeletedBytes,
            FileResults: fileResults.AsReadOnly(),
            IsCancelled: isCancelled
        );
    }

    private static void ReportProgress(
        IProgress<CleanupProgressReport>? progress,
        string currentFilePath,
        int processedFiles,
        int totalFiles,
        long totalDeletedBytes)
    {
        progress?.Report(new CleanupProgressReport(
            CurrentFilePath: currentFilePath,
            ProcessedFiles: processedFiles,
            TotalFiles: totalFiles,
            TotalDeletedBytes: totalDeletedBytes
        ));
    }
}

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class CleanupServiceTests
{
    private string _tempTestDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SCC_CleanupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempTestDir))
        {
            try
            {
                Directory.Delete(_tempTestDir, true);
            }
            catch
            {
            }
        }
    }

    [TestMethod]
    public async Task CleanupAsync_OnlyDeletesSelectedCategories_UT_CLEAN_01()
    {
        // UT-CLEAN-01 / AC-M05-03: 只清理选中的类别，未选择类别的文件完全保留
        string dirA = Path.Combine(_tempTestDir, "CategoryA");
        string dirB = Path.Combine(_tempTestDir, "CategoryB");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        string fileA = Path.Combine(dirA, "a.tmp");
        string fileB = Path.Combine(dirB, "b.tmp");
        File.WriteAllBytes(fileA, new byte[1024]);
        File.WriteAllBytes(fileB, new byte[2048]);

        CacheCategoryDefinition catA = new CacheCategoryDefinition("cat-a", "类别A", "说明", new[] { dirA }, true, "低");
        CacheCategoryDefinition catB = new CacheCategoryDefinition("cat-b", "类别B", "说明", new[] { dirB }, false, "低");

        CacheFileItem itemA = new CacheFileItem(fileA, "cat-a", 1024, DateTime.Now);
        CacheFileItem itemB = new CacheFileItem(fileB, "cat-b", 2048, DateTime.Now);

        CategoryScanResult scanA = new CategoryScanResult(catA, new[] { itemA }, 1024, 1, IsSelected: true, "完成");
        CategoryScanResult scanB = new CategoryScanResult(catB, new[] { itemB }, 2048, 1, IsSelected: false, "完成");

        ICleanupService cleanupService = new CleanupService();
        CleanupSessionResult sessionResult = await cleanupService.CleanupAsync(new[] { scanA, scanB });

        Assert.AreEqual(1, sessionResult.SuccessCount);
        Assert.AreEqual(1024, sessionResult.TotalDeletedBytes);
        Assert.IsFalse(File.Exists(fileA)); // 选中 A，被清理
        Assert.IsTrue(File.Exists(fileB));  // 未选中 B，保留！
    }

    [TestMethod]
    public async Task CleanupAsync_NonExistentFile_RecordsSkipped_UT_CLEAN_02()
    {
        // UT-CLEAN-02 / AC-M05-06: 待处理文件不存在，服务记录为 Skipped，不计为失败
        string dirA = Path.Combine(_tempTestDir, "CategorySkipped");
        Directory.CreateDirectory(dirA);
        string missingFilePath = Path.Combine(dirA, "already_deleted.tmp");

        CacheCategoryDefinition catA = new CacheCategoryDefinition("cat-a", "类别A", "说明", new[] { dirA }, true, "低");
        CacheFileItem itemMissing = new CacheFileItem(missingFilePath, "cat-a", 1024, DateTime.Now);
        CategoryScanResult scanA = new CategoryScanResult(catA, new[] { itemMissing }, 1024, 1, IsSelected: true, "完成");

        ICleanupService cleanupService = new CleanupService();
        CleanupSessionResult sessionResult = await cleanupService.CleanupAsync(new[] { scanA });

        Assert.AreEqual(0, sessionResult.SuccessCount);
        Assert.AreEqual(1, sessionResult.SkipCount);
        Assert.AreEqual(0, sessionResult.FailCount);
        Assert.AreEqual(CleanupFileStatus.Skipped, sessionResult.FileResults[0].Status);
    }

    [TestMethod]
    public async Task CleanupAsync_ExclusiveLockedFile_RecordsFailedAndContinues_UT_CLEAN_03()
    {
        // UT-CLEAN-03 / AC-M05-07: 独占锁定文件 (FileShare.None)，记录为 Failed 并不让服务崩溃
        string dirA = Path.Combine(_tempTestDir, "CategoryLocked");
        Directory.CreateDirectory(dirA);

        string lockedFile = Path.Combine(dirA, "locked.tmp");
        string normalFile = Path.Combine(dirA, "normal.tmp");
        File.WriteAllBytes(lockedFile, new byte[512]);
        File.WriteAllBytes(normalFile, new byte[512]);

        // 独占锁定 lockedFile
        using (FileStream fs = File.Open(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            CacheCategoryDefinition catA = new CacheCategoryDefinition("cat-a", "类别A", "说明", new[] { dirA }, true, "低");
            CacheFileItem itemLocked = new CacheFileItem(lockedFile, "cat-a", 512, DateTime.Now);
            CacheFileItem itemNormal = new CacheFileItem(normalFile, "cat-a", 512, DateTime.Now);

            CategoryScanResult scanA = new CategoryScanResult(catA, new[] { itemLocked, itemNormal }, 1024, 2, IsSelected: true, "完成");

            ICleanupService cleanupService = new CleanupService();
            CleanupSessionResult sessionResult = await cleanupService.CleanupAsync(new[] { scanA });

            Assert.AreEqual(1, sessionResult.SuccessCount); // normal.tmp 正常删除
            Assert.AreEqual(1, sessionResult.FailCount);    // locked.tmp 记录失败
            Assert.IsTrue(File.Exists(lockedFile));         // 锁定文件依然存在
            Assert.IsFalse(File.Exists(normalFile));        // 普通文件成功删除
        }
    }

    [TestMethod]
    public async Task CleanupAsync_InjectedOutPath_RejectsDeletion_UT_CLEAN_04()
    {
        // UT-CLEAN-04 / AC-M05-04 / AC-M05-05: 向快照注入越界路径，校验 PathSafety 拒绝删除
        string dirRoot = Path.Combine(_tempTestDir, "Root");
        string dirOutside = Path.Combine(_tempTestDir, "Root2");
        Directory.CreateDirectory(dirRoot);
        Directory.CreateDirectory(dirOutside);

        string outsideFile = Path.Combine(dirOutside, "outside.tmp");
        File.WriteAllBytes(outsideFile, new byte[512]);

        CacheCategoryDefinition catRoot = new CacheCategoryDefinition("cat-root", "根类别", "说明", new[] { dirRoot }, true, "低");
        // 恶意/错误注入越界外部路径
        CacheFileItem itemOutside = new CacheFileItem(outsideFile, "cat-root", 512, DateTime.Now);
        CategoryScanResult scanRoot = new CategoryScanResult(catRoot, new[] { itemOutside }, 512, 1, IsSelected: true, "完成");

        ICleanupService cleanupService = new CleanupService();
        CleanupSessionResult sessionResult = await cleanupService.CleanupAsync(new[] { scanRoot });

        Assert.AreEqual(0, sessionResult.SuccessCount);
        Assert.AreEqual(1, sessionResult.FailCount);
        Assert.IsTrue(sessionResult.FileResults[0].Message.Contains("安全校验未通过"));
        Assert.IsTrue(File.Exists(outsideFile)); // 文件被保护未删！
    }

    [TestMethod]
    public async Task CleanupAsync_PreCancelled_ReturnsCancelledReport()
    {
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ICleanupService cleanupService = new CleanupService();
        CleanupSessionResult sessionResult = await cleanupService.CleanupAsync(
            Array.Empty<CategoryScanResult>(),
            cancellationToken: cancellation.Token);

        Assert.IsTrue(sessionResult.IsCancelled);
        Assert.AreEqual(0, sessionResult.SuccessCount);
        Assert.AreEqual(0, sessionResult.SkipCount);
        Assert.AreEqual(0, sessionResult.FailCount);
    }

    [TestMethod]
    public async Task CleanupAsync_CancelAfterFirstFile_StopsBeforeNextFile_UT_CLEAN_05()
    {
        string dirA = Path.Combine(_tempTestDir, "CategoryCancelled");
        Directory.CreateDirectory(dirA);

        string firstFile = Path.Combine(dirA, "first.tmp");
        string secondFile = Path.Combine(dirA, "second.tmp");
        File.WriteAllBytes(firstFile, new byte[128]);
        File.WriteAllBytes(secondFile, new byte[256]);

        CacheCategoryDefinition category = new CacheCategoryDefinition(
            "cat-a", "类别A", "说明", new[] { dirA }, true, "低");
        CategoryScanResult scanResult = new CategoryScanResult(
            category,
            new[]
            {
                new CacheFileItem(firstFile, "cat-a", 128, DateTime.Now),
                new CacheFileItem(secondFile, "cat-a", 256, DateTime.Now)
            },
            384,
            2,
            IsSelected: true,
            "完成");

        using CancellationTokenSource cancellation = new CancellationTokenSource();
        IProgress<CleanupProgressReport> progress = new CallbackProgress<CleanupProgressReport>(
            report =>
            {
                if (report.ProcessedFiles == 1)
                {
                    cancellation.Cancel();
                }
            });

        ICleanupService cleanupService = new CleanupService();
        CleanupSessionResult sessionResult = await cleanupService.CleanupAsync(
            new[] { scanResult },
            progress,
            cancellation.Token);

        Assert.IsTrue(sessionResult.IsCancelled);
        Assert.AreEqual(1, sessionResult.SuccessCount);
        Assert.AreEqual(128, sessionResult.TotalDeletedBytes);
        Assert.IsFalse(File.Exists(firstFile));
        Assert.IsTrue(File.Exists(secondFile));
    }
}

internal sealed class CallbackProgress<T> : IProgress<T>
{
    private readonly Action<T> _callback;

    public CallbackProgress(Action<T> callback)
    {
        _callback = callback;
    }

    public void Report(T value)
    {
        _callback(value);
    }
}

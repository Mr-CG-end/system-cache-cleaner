using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class CacheScanServiceTests
{
    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }

    private string _tempTestDir = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SCC_ScanTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task ScanAsync_EmptyDirectory_ReturnsZeroFilesAndBytes()
    {
        // UT-SCAN-01 / AC-M03-01: 扫描空目录
        string emptyDir = Path.Combine(_tempTestDir, "EmptyCat");
        Directory.CreateDirectory(emptyDir);

        CacheCategoryDefinition category = new CacheCategoryDefinition(
            CategoryId: "cat-empty",
            DisplayName: "空目录测试",
            Description: "测试用",
            RootPaths: new[] { emptyDir },
            IsDefaultSelected: true,
            RiskLevel: "低"
        );

        ICacheScanService scanService = new CacheScanService();
        ScanSessionResult result = await scanService.ScanAsync(new[] { category });

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsCancelled);
        Assert.AreEqual(1, result.CategoryResults.Count);

        CategoryScanResult catResult = result.CategoryResults[0];
        Assert.AreEqual(0, catResult.FileCount);
        Assert.AreEqual(0, catResult.TotalBytes);
        Assert.AreEqual("完成", catResult.StatusMessage);
    }

    [TestMethod]
    public async Task ScanAsync_NestedDirectories_CountsFilesCorrectly()
    {
        // UT-SCAN-02 / AC-M03-02 / AC-M03-03: 多层嵌套目录与准确统计
        string catDir = Path.Combine(_tempTestDir, "NestedCat");
        string subDir = Path.Combine(catDir, "SubDir");
        Directory.CreateDirectory(subDir);

        string file1 = Path.Combine(catDir, "file1.tmp");
        string file2 = Path.Combine(subDir, "file2.tmp");
        File.WriteAllBytes(file1, new byte[1024]);
        File.WriteAllBytes(file2, new byte[2048]);

        CacheCategoryDefinition category = new CacheCategoryDefinition(
            CategoryId: "cat-nested",
            DisplayName: "嵌套目录测试",
            Description: "测试用",
            RootPaths: new[] { catDir },
            IsDefaultSelected: true,
            RiskLevel: "低"
        );

        ICacheScanService scanService = new CacheScanService();
        ScanSessionResult result = await scanService.ScanAsync(new[] { category });

        CategoryScanResult catResult = result.CategoryResults[0];
        Assert.AreEqual(2, catResult.FileCount);
        Assert.AreEqual(3072, catResult.TotalBytes);
    }

    [TestMethod]
    public async Task ScanAsync_ThreeCategories_ReturnsExactExpectedStatistics()
    {
        // AC-M03-02: A=2 个/3072 B，B=1 个/4096 B，C=0 个/0 B。
        string categoryAPath = Path.Combine(_tempTestDir, "CategoryA");
        string categoryBPath = Path.Combine(_tempTestDir, "CategoryB");
        string categoryBSubPath = Path.Combine(categoryBPath, "Nested");
        string categoryCPath = Path.Combine(_tempTestDir, "CategoryC");
        Directory.CreateDirectory(categoryAPath);
        Directory.CreateDirectory(categoryBSubPath);
        Directory.CreateDirectory(categoryCPath);

        File.WriteAllBytes(Path.Combine(categoryAPath, "a1.tmp"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(categoryAPath, "a2.tmp"), new byte[2048]);
        File.WriteAllBytes(Path.Combine(categoryBSubPath, "b1.tmp"), new byte[4096]);

        CacheCategoryDefinition[] categories =
        {
            CreateCategory("a", categoryAPath),
            CreateCategory("b", categoryBPath),
            CreateCategory("c", categoryCPath)
        };

        ICacheScanService scanService = new CacheScanService();
        ScanSessionResult result = await scanService.ScanAsync(categories);

        Assert.AreEqual(3, result.CategoryResults.Count);
        Assert.AreEqual(2, result.CategoryResults[0].FileCount);
        Assert.AreEqual(3072, result.CategoryResults[0].TotalBytes);
        Assert.AreEqual(1, result.CategoryResults[1].FileCount);
        Assert.AreEqual(4096, result.CategoryResults[1].TotalBytes);
        Assert.AreEqual(0, result.CategoryResults[2].FileCount);
        Assert.AreEqual(0, result.CategoryResults[2].TotalBytes);
    }

    [TestMethod]
    public async Task ScanAsync_NonExistentDirectory_ReturnsNonExistentStatus()
    {
        // UT-SCAN-03 / AC-M03-04: 目录不存在
        string missingDir = Path.Combine(_tempTestDir, "DoesNotExist_" + Guid.NewGuid().ToString("N"));

        CacheCategoryDefinition category = new CacheCategoryDefinition(
            CategoryId: "cat-missing",
            DisplayName: "缺失目录测试",
            Description: "测试用",
            RootPaths: new[] { missingDir },
            IsDefaultSelected: true,
            RiskLevel: "低"
        );

        ICacheScanService scanService = new CacheScanService();
        ScanSessionResult result = await scanService.ScanAsync(new[] { category });

        CategoryScanResult catResult = result.CategoryResults[0];
        Assert.AreEqual(0, catResult.FileCount);
        Assert.AreEqual(0, catResult.TotalBytes);
        Assert.AreEqual("目录不存在，已跳过", catResult.StatusMessage);
    }

    [TestMethod]
    public async Task ScanAsync_CancelTokenTriggered_ReturnsCancelledSession()
    {
        // UT-SCAN-04 / AC-M03-05: 扫描开始并产生部分结果后触发取消令牌。
        string catDir = Path.Combine(_tempTestDir, "CancelCat");
        Directory.CreateDirectory(catDir);
        for (int i = 0; i < 100; i++)
        {
            File.WriteAllBytes(Path.Combine(catDir, $"test_{i}.tmp"), new byte[128]);
        }

        CacheCategoryDefinition category = new CacheCategoryDefinition(
            CategoryId: "cat-cancel",
            DisplayName: "取消测试",
            Description: "测试用",
            RootPaths: new[] { catDir },
            IsDefaultSelected: true,
            RiskLevel: "低"
        );
        string secondCategoryDir = Path.Combine(_tempTestDir, "MustNotStart");
        Directory.CreateDirectory(secondCategoryDir);
        File.WriteAllBytes(Path.Combine(secondCategoryDir, "should-not-be-scanned.tmp"), new byte[256]);
        CacheCategoryDefinition secondCategory = CreateCategory("cat-not-started", secondCategoryDir);

        CancellationTokenSource cts = new CancellationTokenSource();
        InlineProgress<ScanProgressReport> progress = new InlineProgress<ScanProgressReport>(_ => cts.Cancel());

        ICacheScanService scanService = new CacheScanService();
        ScanSessionResult result = await scanService.ScanAsync(new[] { category, secondCategory }, progress, cts.Token);

        Assert.IsTrue(result.IsCancelled, $"Expected IsCancelled to be true but got {result.IsCancelled}");
        Assert.IsTrue(result.GlobalStatusSummary.Contains("取消"), $"Actual status summary: '{result.GlobalStatusSummary}'");
        Assert.AreEqual(1, result.CategoryResults.Count);
        Assert.IsTrue(result.CategoryResults[0].FileCount > 0);
        Assert.IsTrue(result.CategoryResults[0].FileCount < 100);
        Assert.AreEqual("扫描已取消", result.CategoryResults[0].StatusMessage);
    }

    [TestMethod]
    public async Task ScanAsync_SecondScanReflectsCurrentFilesWithoutAccumulation()
    {
        // AC-M03-08: 第二次扫描必须是新快照，不能累计第一次结果。
        string catDir = Path.Combine(_tempTestDir, "RescanCat");
        Directory.CreateDirectory(catDir);
        string firstFile = Path.Combine(catDir, "first.tmp");
        File.WriteAllBytes(firstFile, new byte[100]);

        CacheCategoryDefinition category = CreateCategory("cat-rescan", catDir);
        ICacheScanService scanService = new CacheScanService();

        ScanSessionResult firstResult = await scanService.ScanAsync(new[] { category });
        Assert.AreEqual(1, firstResult.CategoryResults[0].FileCount);
        Assert.AreEqual(100, firstResult.CategoryResults[0].TotalBytes);

        File.Delete(firstFile);
        File.WriteAllBytes(Path.Combine(catDir, "second.tmp"), new byte[200]);
        File.WriteAllBytes(Path.Combine(catDir, "third.tmp"), new byte[300]);

        ScanSessionResult secondResult = await scanService.ScanAsync(new[] { category });
        Assert.AreEqual(2, secondResult.CategoryResults[0].FileCount);
        Assert.AreEqual(500, secondResult.CategoryResults[0].TotalBytes);
    }

    [TestMethod]
    public async Task ScanAsync_DirectoryLinkTarget_IsNotScannedAndReportsWarning()
    {
        // AC-M03-09 / M02 安全边界：扫描不进入指向白名单外部的链接目标。
        string allowedRoot = Path.Combine(_tempTestDir, "Allowed");
        string outsideRoot = Path.Combine(_tempTestDir, "Outside");
        string linkPath = Path.Combine(allowedRoot, "Link");
        Directory.CreateDirectory(allowedRoot);
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllBytes(Path.Combine(allowedRoot, "inside.tmp"), new byte[100]);
        File.WriteAllBytes(Path.Combine(outsideRoot, "outside.tmp"), new byte[900]);

        if (!TestDirectoryLink.TryCreate(linkPath, outsideRoot, out string failureReason))
        {
            Assert.Inconclusive($"当前环境无法创建目录符号链接或 junction：{failureReason}");
            return;
        }

        ICacheScanService scanService = new CacheScanService();
        ScanSessionResult result = await scanService.ScanAsync(new[] { CreateCategory("cat-link", allowedRoot) });

        CategoryScanResult categoryResult = result.CategoryResults[0];
        Assert.AreEqual(1, categoryResult.FileCount);
        Assert.AreEqual(100, categoryResult.TotalBytes);
        StringAssert.Contains(categoryResult.StatusMessage, "跳过");
    }

    [TestMethod]
    public async Task ScanAsync_DoesNotModifyFiles_AC_M03_09()
    {
        // AC-M03-09: 比较扫描前后文件路径、长度和修改时间，确认扫描没有擦除或修改文件
        string catDir = Path.Combine(_tempTestDir, "UnmodifiedTest");
        Directory.CreateDirectory(catDir);
        string filePath = Path.Combine(catDir, "sample.dat");
        byte[] originalContent = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        File.WriteAllBytes(filePath, originalContent);
        DateTime originalWriteTime = File.GetLastWriteTimeUtc(filePath);

        CacheCategoryDefinition category = new CacheCategoryDefinition(
            CategoryId: "cat-unmodified",
            DisplayName: "安全只读测试",
            Description: "测试用",
            RootPaths: new[] { catDir },
            IsDefaultSelected: true,
            RiskLevel: "低"
        );

        ICacheScanService scanService = new CacheScanService();
        await scanService.ScanAsync(new[] { category });

        Assert.IsTrue(File.Exists(filePath));
        byte[] afterContent = File.ReadAllBytes(filePath);
        CollectionAssert.AreEqual(originalContent, afterContent);
        Assert.AreEqual(originalWriteTime, File.GetLastWriteTimeUtc(filePath));
    }

    private static CacheCategoryDefinition CreateCategory(string categoryId, string rootPath)
    {
        return new CacheCategoryDefinition(
            CategoryId: categoryId,
            DisplayName: categoryId,
            Description: "测试用",
            RootPaths: new[] { rootPath },
            IsDefaultSelected: true,
            RiskLevel: "低"
        );
    }
}

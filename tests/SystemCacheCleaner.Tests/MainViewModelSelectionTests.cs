using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner.Tests;

public class FakeCacheScanService : ICacheScanService
{
    private readonly ScanSessionResult _resultToReturn;

    public FakeCacheScanService(ScanSessionResult resultToReturn)
    {
        _resultToReturn = resultToReturn;
    }

    public Task<ScanSessionResult> ScanAsync(
        IReadOnlyList<CacheCategoryDefinition> categories,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_resultToReturn);
    }
}

public class FailingAfterFirstScanService : ICacheScanService
{
    private readonly ScanSessionResult _firstResult;
    private int _callCount;

    public FailingAfterFirstScanService(ScanSessionResult firstResult)
    {
        _firstResult = firstResult;
    }

    public Task<ScanSessionResult> ScanAsync(
        IReadOnlyList<CacheCategoryDefinition> categories,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _callCount++;
        if (_callCount == 1)
        {
            return Task.FromResult(_firstResult);
        }

        throw new IOException("模拟扫描失败");
    }
}

[TestClass]
public class MainViewModelSelectionTests
{
    [TestMethod]
    public async Task ScanCompleted_DefaultSelectionAndSummary_MatchesRequirements()
    {
        // AC-M04-02: 检查默认选择状态 (user-temp: true, windows-temp: false, edge-cache: true)
        CacheCategoryDefinition catUserTemp = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CacheCategoryDefinition catWinTemp = new CacheCategoryDefinition("windows-temp", "Windows 临时文件", "说明", new[] { @"C:\Windows\Temp" }, false, "中");
        CacheCategoryDefinition catEdgeCache = new CacheCategoryDefinition("edge-cache", "Edge 缓存", "说明", new[] { @"C:\Edge" }, true, "低");

        CategoryScanResult res1 = new CategoryScanResult(catUserTemp, new List<CacheFileItem>().AsReadOnly(), 1024 * 1024, 10, true, "完成"); // 1 MB
        CategoryScanResult res2 = new CategoryScanResult(catWinTemp, new List<CacheFileItem>().AsReadOnly(), 2 * 1024 * 1024, 20, false, "完成"); // 2 MB
        CategoryScanResult res3 = new CategoryScanResult(catEdgeCache, new List<CacheFileItem>().AsReadOnly(), 3 * 1024 * 1024, 30, true, "完成"); // 3 MB

        ScanSessionResult fakeSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { res1, res2, res3 }, false, "完成");
        FakeCacheScanService fakeScanService = new FakeCacheScanService(fakeSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScanService);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        Assert.AreEqual(3, vm.CategoryResults.Count);
        Assert.IsTrue(vm.CategoryResults[0].IsSelected);  // user-temp 默认选中
        Assert.IsFalse(vm.CategoryResults[1].IsSelected); // windows-temp 默认不选中
        Assert.IsTrue(vm.CategoryResults[2].IsSelected);  // edge-cache 默认选中

        // AC-M04-03: 初始释放空间为选中之和 (1 MB + 3 MB = 4 MB)
        Assert.IsTrue(vm.SelectedSummaryText.Contains("4.00 MB"));
        Assert.IsTrue(vm.CleanCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task SelectionToggle_UpdatesSummaryAndCleanCommandState()
    {
        // AC-M04-03, AC-M04-05: 逐项取消与全部取消
        CacheCategoryDefinition catUserTemp = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult res1 = new CategoryScanResult(catUserTemp, new List<CacheFileItem>().AsReadOnly(), 1024 * 1024, 10, true, "完成"); // 1 MB

        ScanSessionResult fakeSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { res1 }, false, "完成");
        FakeCacheScanService fakeScanService = new FakeCacheScanService(fakeSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScanService);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        Assert.IsTrue(vm.CleanCommand.CanExecute(null));

        // 取消勾选 user-temp
        vm.CategoryResults[0].IsSelected = false;

        // 期待: 预释放空间变为 0 B 且“立即清理”禁用 (AC-M04-05)
        Assert.IsTrue(vm.SelectedSummaryText.Contains("0 B"));
        Assert.IsFalse(vm.CleanCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ZeroBytesCategory_DisablesCleanCommand()
    {
        // AC-M04-06: 勾选统计为 0 B 的类别，不允许执行清理
        CacheCategoryDefinition catZero = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resZero = new CategoryScanResult(catZero, new List<CacheFileItem>().AsReadOnly(), 0, 0, true, "完成");

        ScanSessionResult fakeSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resZero }, false, "完成");
        FakeCacheScanService fakeScanService = new FakeCacheScanService(fakeSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScanService);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        Assert.IsTrue(vm.CategoryResults[0].IsSelected);
        Assert.IsFalse(vm.CleanCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task CancelledScan_DisablesCleanCommand()
    {
        // AC-M04-07: 取消扫描后结果不完整，即使勾选，“立即清理”保持禁用
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 1024 * 1024, 10, true, "扫描已取消");

        ScanSessionResult fakeSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, true, "已取消");
        FakeCacheScanService fakeScanService = new FakeCacheScanService(fakeSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScanService);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        Assert.IsTrue(vm.IsScanCancelled);
        Assert.IsFalse(vm.CleanCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task RescanFailure_ClearsPreviousResultsAndSelectionSummary()
    {
        // 重新扫描开始后不得继续展示上一次快照的选择数量和预计空间。
        CacheCategoryDefinition category = new CacheCategoryDefinition(
            "user-temp",
            "用户临时文件",
            "说明",
            new[] { @"C:\Temp" },
            true,
            "低");
        CategoryScanResult categoryResult = new CategoryScanResult(
            category,
            Array.Empty<CacheFileItem>(),
            1024 * 1024,
            1,
            true,
            "完成");
        ScanSessionResult firstResult = new ScanSessionResult(
            DateTime.Now,
            DateTime.Now,
            new[] { categoryResult },
            false,
            "完成");

        MainViewModel vm = new MainViewModel(scanService: new FailingAfterFirstScanService(firstResult));
        AsyncRelayCommand scanCommand = (AsyncRelayCommand)vm.StartScanCommand;

        await scanCommand.ExecuteAsync(null);
        StringAssert.Contains(vm.SelectedSummaryText, "1.00 MB");

        await scanCommand.ExecuteAsync(null);

        Assert.AreEqual(OperationStatus.Idle, vm.Status);
        Assert.AreEqual(0, vm.CategoryResults.Count);
        StringAssert.Contains(vm.SelectedSummaryText, "已选择 0 类");
        StringAssert.Contains(vm.SelectedSummaryText, "0 B");
        Assert.IsFalse(vm.CleanCommand.CanExecute(null));
    }
}

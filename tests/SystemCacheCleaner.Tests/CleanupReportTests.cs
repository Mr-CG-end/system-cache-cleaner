using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class CleanupReportTests
{
    [TestMethod]
    public async Task Cleanup_FullSuccess_SetsStatusToCleanupCompleted_AC_M06_01()
    {
        // AC-M06-01: 全部成功的清理，总体状态为 CleanupCompleted
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 1024, 1, IsSelected: true, "完成");
        ScanSessionResult fakeScanSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, false, "完成");
        FakeCacheScanService fakeScan = new FakeCacheScanService(fakeScanSession);

        CleanupFileResult fileResult = new CleanupFileResult(@"C:\Temp\a.tmp", 1024, CleanupFileStatus.Success, "成功");
        CleanupSessionResult fakeCleanSession = new CleanupSessionResult(DateTime.Now, DateTime.Now, 1, 0, 0, 1024, new[] { fileResult }, false);
        FakeCleanupService fakeClean = new FakeCleanupService(fakeCleanSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScan, cleanupService: fakeClean);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        vm.SetConfirmDialogHandler((cat, file, space) => true);
        vm.SetReportDialogHandler(session => false); // 模拟报告弹窗不点击重新扫描

        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

        Assert.AreEqual(OperationStatus.CleanupCompleted, vm.Status);
        Assert.IsNotNull(vm.LastCleanupResult);
        Assert.AreEqual(1, vm.LastCleanupResult.SuccessCount);
        Assert.AreEqual(0, vm.LastCleanupResult.FailCount);
    }

    [TestMethod]
    public async Task Cleanup_PartialFailure_SetsStatusToPartialCompleted_AC_M06_02()
    {
        // AC-M06-02: 存在跳过或失败文件时，状态为 PartialCompleted，不得使用全部成功文案
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 2048, 2, IsSelected: true, "完成");
        ScanSessionResult fakeScanSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, false, "完成");
        FakeCacheScanService fakeScan = new FakeCacheScanService(fakeScanSession);

        CleanupFileResult f1 = new CleanupFileResult(@"C:\Temp\a.tmp", 1024, CleanupFileStatus.Success, "成功");
        CleanupFileResult f2 = new CleanupFileResult(@"C:\Temp\locked.tmp", 1024, CleanupFileStatus.Failed, "文件被独占锁定");
        CleanupSessionResult fakeCleanSession = new CleanupSessionResult(DateTime.Now, DateTime.Now, 1, 0, 1, 1024, new[] { f1, f2 }, false);
        FakeCleanupService fakeClean = new FakeCleanupService(fakeCleanSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScan, cleanupService: fakeClean);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        vm.SetConfirmDialogHandler((cat, file, space) => true);
        vm.SetReportDialogHandler(session => false);

        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

        Assert.AreEqual(OperationStatus.PartialCompleted, vm.Status);
        Assert.IsTrue(vm.StatusDescription.Contains("部分跳过/失败"));
    }

    [TestMethod]
    public async Task ReportDialog_ClickRescan_TriggersNewScan_AC_M06_05_06()
    {
        // AC-M06-05, AC-M06-06: 清理完成后在报告中点击“重新扫描”，直接触发新一轮扫描
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 1024, 1, IsSelected: true, "完成");
        ScanSessionResult fakeScanSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, false, "完成");
        FakeCacheScanService fakeScan = new FakeCacheScanService(fakeScanSession);

        CleanupFileResult f1 = new CleanupFileResult(@"C:\Temp\a.tmp", 1024, CleanupFileStatus.Success, "成功");
        CleanupSessionResult fakeCleanSession = new CleanupSessionResult(DateTime.Now, DateTime.Now, 1, 0, 0, 1024, new[] { f1 }, false);
        FakeCleanupService fakeClean = new FakeCleanupService(fakeCleanSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScan, cleanupService: fakeClean);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        vm.SetConfirmDialogHandler((cat, file, space) => true);
        // 模拟用户在报告弹窗中点击“重新扫描” (返回 true)
        vm.SetReportDialogHandler(session => true);

        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

        // 期待: 重新触发了扫描，最终状态回到 ScanCompleted 且刷新了结果
        Assert.AreEqual(OperationStatus.ScanCompleted, vm.Status);
    }

    [TestMethod]
    public async Task Cleanup_RefreshesSystemDiskInfo_AC_M06_04()
    {
        CacheCategoryDefinition category = new CacheCategoryDefinition(
            "user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult categoryResult = new CategoryScanResult(
            category, Array.Empty<CacheFileItem>(), 1, 1, true, "完成");
        ScanSessionResult scanResult = new ScanSessionResult(
            DateTime.Now, DateTime.Now, new[] { categoryResult }, false, "完成");
        CleanupSessionResult cleanupResult = new CleanupSessionResult(
            DateTime.Now, DateTime.Now, 1, 0, 0, 1,
            Array.Empty<CleanupFileResult>(), false);
        CountingDiskSpaceService diskService = new CountingDiskSpaceService();
        MainViewModel vm = new MainViewModel(
            diskSpaceService: diskService,
            scanService: new FakeCacheScanService(scanResult),
            cleanupService: new FakeCleanupService(cleanupResult));
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        vm.SetConfirmDialogHandler((_, _, _) => true);
        vm.SetReportDialogHandler(_ => false);

        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

        Assert.AreEqual(2, diskService.CallCount);
        Assert.AreEqual(80, vm.SystemDiskInfo?.FreeBytes);
    }
}

public class CountingDiskSpaceService : IDiskSpaceService
{
    public int CallCount { get; private set; }

    public SystemDiskInfo GetSystemDiskInfo()
    {
        CallCount++;
        long freeBytes = CallCount == 1 ? 40 : 80;
        return new SystemDiskInfo(
            "C:", 100, freeBytes, 100 - freeBytes,
            100 - freeBytes, true, "正常");
    }
}

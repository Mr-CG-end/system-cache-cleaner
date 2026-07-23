using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner.Tests;

public class FakeCleanupService : ICleanupService
{
    private readonly CleanupSessionResult _resultToReturn;

    public FakeCleanupService(CleanupSessionResult resultToReturn)
    {
        _resultToReturn = resultToReturn;
    }

    public Task<CleanupSessionResult> CleanupAsync(
        IReadOnlyList<CategoryScanResult> selectedCategoryResults,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_resultToReturn);
    }
}

[TestClass]
public class CleanupViewModelTests
{
    [TestMethod]
    public async Task CleanCommand_DialogCancelled_DoesNotPerformCleanup_AC_M05_02()
    {
        // AC-M05-02: 用户在弹窗点击取消，保持现状，不进行清理
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 1024 * 1024, 10, true, "完成");
        ScanSessionResult fakeScanSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, false, "完成");
        FakeCacheScanService fakeScan = new FakeCacheScanService(fakeScanSession);

        CleanupSessionResult fakeCleanSession = new CleanupSessionResult(DateTime.Now, DateTime.Now, 10, 0, 0, 1024 * 1024, new List<CleanupFileResult>().AsReadOnly(), false);
        FakeCleanupService fakeClean = new FakeCleanupService(fakeCleanSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScan, cleanupService: fakeClean);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        Assert.AreEqual(OperationStatus.ScanCompleted, vm.Status);
        Assert.IsTrue(vm.CleanCommand.CanExecute(null));

        // 模拟弹窗 Handler 返回 false (取消)
        vm.SetConfirmDialogHandler((catNames, fileCount, expectedBytes) => false);

        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

        // 期待: 状态维持 ScanCompleted，未保存 CleanupResult
        Assert.AreEqual(OperationStatus.ScanCompleted, vm.Status);
        Assert.IsNull(vm.LastCleanupResult);
    }

    [TestMethod]
    public async Task CleanCommand_DialogConfirmed_ExecutesCleanupAndUpdateStatus()
    {
        // AC-M05-01 / AC-M05-03: 用户确认弹窗后，成功执行清理并更新状态至 CleanupCompleted
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 1024 * 1024, 10, true, "完成");
        ScanSessionResult fakeScanSession = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, false, "完成");
        FakeCacheScanService fakeScan = new FakeCacheScanService(fakeScanSession);

        CleanupSessionResult fakeCleanSession = new CleanupSessionResult(DateTime.Now, DateTime.Now, 10, 0, 0, 1024 * 1024, new List<CleanupFileResult>().AsReadOnly(), false);
        FakeCleanupService fakeClean = new FakeCleanupService(fakeCleanSession);

        MainViewModel vm = new MainViewModel(scanService: fakeScan, cleanupService: fakeClean);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        // 模拟弹窗 Handler 返回 true (确认)
        vm.SetConfirmDialogHandler((catNames, fileCount, expectedBytes) => true);

        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

        Assert.AreEqual(OperationStatus.CleanupCompleted, vm.Status);
        Assert.IsNotNull(vm.LastCleanupResult);
        Assert.AreEqual(10, vm.LastCleanupResult.SuccessCount);
    }

    [TestMethod]
    public async Task CleanCommand_NewlySelectedDefaultOffCategory_IsCleanedAndNamedInConfirmation()
    {
        string root = Path.Combine(Path.GetTempPath(), "SCC_CleanupVmTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "selected.tmp");
        await File.WriteAllBytesAsync(file, new byte[32]);

        try
        {
            CacheCategoryDefinition category = new CacheCategoryDefinition(
                "windows-temp", "Windows 临时文件", "说明", new[] { root }, false, "中");
            CacheFileItem item = new CacheFileItem(file, category.CategoryId, 32, DateTime.Now);
            CategoryScanResult categoryResult = new CategoryScanResult(
                category, new[] { item }, 32, 1, IsSelected: false, "完成");
            ScanSessionResult scanResult = new ScanSessionResult(
                DateTime.Now, DateTime.Now, new[] { categoryResult }, false, "完成");

            MainViewModel vm = new MainViewModel(
                scanService: new FakeCacheScanService(scanResult),
                cleanupService: new CleanupService());
            await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
            vm.CategoryResults[0].IsSelected = true;

            IReadOnlyList<string>? confirmedCategoryNames = null;
            vm.SetConfirmDialogHandler((categoryNames, _, _) =>
            {
                confirmedCategoryNames = categoryNames;
                return true;
            });

            await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);

            CollectionAssert.AreEqual(
                new[] { "Windows 临时文件" },
                confirmedCategoryNames?.ToArray());
            Assert.IsFalse(File.Exists(file));
            Assert.AreEqual(1, vm.LastCleanupResult?.SuccessCount);
            Assert.AreEqual(OperationStatus.CleanupCompleted, vm.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

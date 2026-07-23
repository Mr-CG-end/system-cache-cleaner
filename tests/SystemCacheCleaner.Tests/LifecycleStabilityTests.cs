using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Infrastructure;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class LifecycleStabilityTests
{
    [TestMethod]
    public async Task WindowClosing_InIdleState_AllowsCloseDirectly()
    {
        // 空闲状态直接允许关闭
        MainViewModel vm = new MainViewModel();
        Assert.AreEqual(OperationStatus.Idle, vm.Status);

        bool canClose = await vm.ConfirmWindowClosingAsync();
        Assert.IsTrue(canClose);
    }

    [TestMethod]
    public async Task WindowClosing_InScanningState_AsksConfirmation_AC_M07_01()
    {
        // AC-M07-01: 扫描中关闭窗口拦截
        TaskCompletionSource<ScanSessionResult> tcs = new TaskCompletionSource<ScanSessionResult>();
        FakeAsyncScanService fakeSlowScan = new FakeAsyncScanService(tcs.Task);

        MainViewModel vm = new MainViewModel(scanService: fakeSlowScan);
        _ = ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);

        Assert.IsTrue(vm.IsScanning);

        // 模拟关闭确认 Handler 返回 false (用户在关闭弹窗中点击取消)
        bool handlerCalled = false;
        vm.SetClosingConfirmHandler((msg, title) =>
        {
            handlerCalled = true;
            return false;
        });

        bool canCloseCancel = await vm.ConfirmWindowClosingAsync();
        Assert.IsTrue(handlerCalled);
        Assert.IsFalse(canCloseCancel); // 不允许关闭窗口

        // 模拟关闭确认 Handler 返回 true (用户在关闭弹窗中点击确定)
        vm.SetClosingConfirmHandler((msg, title) => true);
        Task<bool> closeTask = vm.ConfirmWindowClosingAsync();
        Assert.IsFalse(closeTask.IsCompleted, "确认关闭后应等待扫描任务结束");

        tcs.SetResult(new ScanSessionResult(DateTime.Now, DateTime.Now, new List<CategoryScanResult>().AsReadOnly(), true, "已取消"));
        Assert.IsTrue(await closeTask);
    }

    [TestMethod]
    public async Task ConsecutiveScans_ThreeTimesWithCancellation_RemainsStable_AC_M07_03()
    {
        // AC-M07-03: 连续执行至少 3 次扫描（其中一次取消），命令与状态正常恢复，无卡死
        CacheCategoryDefinition catUser = new CacheCategoryDefinition("user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
        CategoryScanResult resUser = new CategoryScanResult(catUser, new List<CacheFileItem>().AsReadOnly(), 1024, 1, IsSelected: true, "完成");
        ScanSessionResult scanSuccess = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, false, "完成");
        ScanSessionResult scanCancel = new ScanSessionResult(DateTime.Now, DateTime.Now, new[] { resUser }, true, "已取消");

        // 同一个窗口连续执行：正常、取消、再次正常
        SequencedScanService scanService = new SequencedScanService(scanSuccess, scanCancel, scanSuccess);
        MainViewModel vm = new MainViewModel(scanService: scanService);
        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        Assert.AreEqual(OperationStatus.ScanCompleted, vm.Status);
        Assert.IsFalse(vm.IsScanCancelled);

        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        Assert.AreEqual(OperationStatus.ScanCompleted, vm.Status);
        Assert.IsTrue(vm.IsScanCancelled);

        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        Assert.AreEqual(OperationStatus.ScanCompleted, vm.Status);
        Assert.IsFalse(vm.IsScanCancelled);
        Assert.AreEqual(3, scanService.CallCount);
    }

    [TestMethod]
    public async Task WindowClosing_InCleaningState_CancelsAndWaits_AC_M07_02_07()
    {
        CacheCategoryDefinition category = CreateCategory();
        CategoryScanResult categoryResult = CreateCategoryResult(category);
        ScanSessionResult scanResult = new ScanSessionResult(
            DateTime.Now, DateTime.Now, new[] { categoryResult }, false, "完成");
        TaskCompletionSource<CleanupSessionResult> cleanupTcs =
            new TaskCompletionSource<CleanupSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeAsyncCleanupService cleanupService = new FakeAsyncCleanupService(cleanupTcs.Task);
        MainViewModel vm = new MainViewModel(
            scanService: new FakeCacheScanService(scanResult),
            cleanupService: cleanupService);

        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        vm.SetConfirmDialogHandler((_, _, _) => true);
        Task cleanTask = ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);
        Assert.IsTrue(vm.IsCleaning);

        vm.SetClosingConfirmHandler((_, _) => true);
        Task<bool> closeTask = vm.ConfirmWindowClosingAsync();

        Assert.IsTrue(cleanupService.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(closeTask.IsCompleted, "窗口必须等待清理服务返回后才能关闭");

        cleanupTcs.SetResult(new CleanupSessionResult(
            DateTime.Now, DateTime.Now, 0, 0, 0, 0,
            Array.Empty<CleanupFileResult>(), true));

        Assert.IsTrue(await closeTask);
        await cleanTask;
        Assert.AreEqual(OperationStatus.PartialCompleted, vm.Status);
    }

    [TestMethod]
    public async Task PreviousReport_IsDisabledWhileCleaning_AC_M07_04()
    {
        CacheCategoryDefinition category = CreateCategory();
        CategoryScanResult categoryResult = CreateCategoryResult(category);
        ScanSessionResult scanResult = new ScanSessionResult(
            DateTime.Now, DateTime.Now, new[] { categoryResult }, false, "完成");
        CleanupSessionResult firstCleanup = new CleanupSessionResult(
            DateTime.Now, DateTime.Now, 1, 0, 0, 1,
            Array.Empty<CleanupFileResult>(), false);
        TaskCompletionSource<CleanupSessionResult> secondCleanupTcs =
            new TaskCompletionSource<CleanupSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        SequencedCleanupService cleanupService =
            new SequencedCleanupService(firstCleanup, secondCleanupTcs.Task);
        MainViewModel vm = new MainViewModel(
            scanService: new FakeCacheScanService(scanResult),
            cleanupService: cleanupService);
        vm.SetConfirmDialogHandler((_, _, _) => true);
        vm.SetReportDialogHandler(_ => false);

        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        await ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);
        Assert.IsNotNull(vm.LastCleanupResult);

        await ((AsyncRelayCommand)vm.StartScanCommand).ExecuteAsync(null);
        Task secondCleanTask = ((AsyncRelayCommand)vm.CleanCommand).ExecuteAsync(null);
        Assert.IsTrue(vm.IsCleaning);
        Assert.IsFalse(vm.ShowReportCommand.CanExecute(null));
        Assert.IsFalse(vm.RescanCommand.CanExecute(null));

        secondCleanupTcs.SetResult(firstCleanup);
        await secondCleanTask;
    }

    private static CacheCategoryDefinition CreateCategory()
    {
        return new CacheCategoryDefinition(
            "user-temp", "用户临时文件", "说明", new[] { @"C:\Temp" }, true, "低");
    }

    private static CategoryScanResult CreateCategoryResult(CacheCategoryDefinition category)
    {
        CacheFileItem item = new CacheFileItem(
            @"C:\Temp\a.tmp", category.CategoryId, 1, DateTime.Now);
        return new CategoryScanResult(
            category, new[] { item }, 1, 1, IsSelected: true, "完成");
    }
}

public class FakeAsyncScanService : ICacheScanService
{
    private readonly Task<ScanSessionResult> _taskToReturn;

    public FakeAsyncScanService(Task<ScanSessionResult> taskToReturn)
    {
        _taskToReturn = taskToReturn;
    }

    public Task<ScanSessionResult> ScanAsync(
        IReadOnlyList<CacheCategoryDefinition> categories,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _taskToReturn;
    }
}

public class SequencedScanService : ICacheScanService
{
    private readonly Queue<ScanSessionResult> _results;

    public SequencedScanService(params ScanSessionResult[] results)
    {
        _results = new Queue<ScanSessionResult>(results);
    }

    public int CallCount { get; private set; }

    public Task<ScanSessionResult> ScanAsync(
        IReadOnlyList<CacheCategoryDefinition> categories,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_results.Dequeue());
    }
}

public class FakeAsyncCleanupService : ICleanupService
{
    private readonly Task<CleanupSessionResult> _taskToReturn;

    public FakeAsyncCleanupService(Task<CleanupSessionResult> taskToReturn)
    {
        _taskToReturn = taskToReturn;
    }

    public CancellationToken CancellationToken { get; private set; }

    public Task<CleanupSessionResult> CleanupAsync(
        IReadOnlyList<CategoryScanResult> selectedCategoryResults,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        CancellationToken = cancellationToken;
        return _taskToReturn;
    }
}

public class SequencedCleanupService : ICleanupService
{
    private readonly Queue<Task<CleanupSessionResult>> _results;

    public SequencedCleanupService(
        CleanupSessionResult firstResult,
        Task<CleanupSessionResult> secondResult)
    {
        _results = new Queue<Task<CleanupSessionResult>>(
            new[] { Task.FromResult(firstResult), secondResult });
    }

    public Task<CleanupSessionResult> CleanupAsync(
        IReadOnlyList<CategoryScanResult> selectedCategoryResults,
        IProgress<CleanupProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _results.Dequeue();
    }
}

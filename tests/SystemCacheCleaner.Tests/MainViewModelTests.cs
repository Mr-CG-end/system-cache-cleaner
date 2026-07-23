using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;
using SystemCacheCleaner.ViewModels;

namespace SystemCacheCleaner.Tests;

public class FakeDiskSpaceService : IDiskSpaceService
{
    public SystemDiskInfo GetSystemDiskInfo()
    {
        return new SystemDiskInfo(
            DriveName: "C:",
            TotalBytes: 100 * 1024 * 1024 * 1024L, // 100 GB
            FreeBytes: 40 * 1024 * 1024 * 1024L,   // 40 GB
            UsedBytes: 60 * 1024 * 1024 * 1024L,   // 60 GB
            UsedPercentage: 60.0,
            IsAvailable: true,
            StatusMessage: "正常"
        );
    }
}

[TestClass]
public class MainViewModelTests
{
    [TestMethod]
    public void Constructor_InitialState_MatchesRequirements()
    {
        FakeDiskSpaceService fakeDiskService = new FakeDiskSpaceService();
        MainViewModel vm = new MainViewModel(diskSpaceService: fakeDiskService, isDemoMode: false);

        Assert.AreEqual("系统缓存清理工具软件 V1.0", vm.WindowTitle);
        Assert.IsFalse(vm.IsDemoMode);
        Assert.AreEqual(OperationStatus.Idle, vm.Status);
        Assert.IsTrue(vm.IsIdle);
        Assert.IsTrue(vm.StartScanCommand.CanExecute(null));
        Assert.IsFalse(vm.CleanCommand.CanExecute(null));
        Assert.IsNotNull(vm.SystemDiskInfo);
        Assert.IsTrue(vm.SystemDiskDisplayText.Contains("C:"));
    }

    [TestMethod]
    public void DemoMode_SetsIsDemoModeProperty()
    {
        FakeDiskSpaceService fakeDiskService = new FakeDiskSpaceService();
        MainViewModel vm = new MainViewModel(diskSpaceService: fakeDiskService, isDemoMode: true);

        Assert.IsTrue(vm.IsDemoMode);
        Assert.AreEqual("演示模式：当前数据不是真实系统缓存", vm.DemoModeBannerText);
    }

    [TestMethod]
    public void StartScanCommand_Execution_UpdatesStatusToScanning()
    {
        FakeDiskSpaceService fakeDiskService = new FakeDiskSpaceService();
        MainViewModel vm = new MainViewModel(diskSpaceService: fakeDiskService);

        Assert.AreEqual(OperationStatus.Idle, vm.Status);
        vm.StartScanCommand.Execute(null);

        Assert.AreEqual(OperationStatus.Scanning, vm.Status);
        Assert.IsTrue(vm.IsScanning);
    }
}

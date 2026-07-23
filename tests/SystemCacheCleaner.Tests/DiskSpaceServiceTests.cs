using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Services;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class DiskSpaceServiceTests
{
    [TestMethod]
    public void GetSystemDiskInfo_ReturnsValidDiskInfo()
    {
        IDiskSpaceService service = new DiskSpaceService();
        SystemDiskInfo diskInfo = service.GetSystemDiskInfo();

        Assert.IsNotNull(diskInfo);
        Assert.IsFalse(string.IsNullOrEmpty(diskInfo.DriveName));
        if (diskInfo.IsAvailable)
        {
            Assert.IsTrue(diskInfo.TotalBytes > 0);
            Assert.IsTrue(diskInfo.FreeBytes >= 0);
            Assert.IsTrue(diskInfo.UsedBytes >= 0);
            Assert.IsTrue(diskInfo.UsedPercentage >= 0 && diskInfo.UsedPercentage <= 100);
        }
    }
}

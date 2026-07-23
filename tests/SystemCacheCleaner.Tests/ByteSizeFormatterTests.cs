using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Infrastructure;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class ByteSizeFormatterTests
{
    [TestMethod]
    public void Format_ZeroAndNegative_ReturnsZeroBytes()
    {
        Assert.AreEqual("0 B", ByteSizeFormatter.Format(0));
        Assert.AreEqual("0 B", ByteSizeFormatter.Format(-100));
    }

    [TestMethod]
    public void Format_Bytes_ReturnsBytesString()
    {
        Assert.AreEqual("512 B", ByteSizeFormatter.Format(512));
    }

    [TestMethod]
    public void Format_Kilobytes_ReturnsKBString()
    {
        Assert.AreEqual("1.00 KB", ByteSizeFormatter.Format(1024));
        Assert.AreEqual("2.50 KB", ByteSizeFormatter.Format(2560));
    }

    [TestMethod]
    public void Format_Megabytes_ReturnsMBString()
    {
        Assert.AreEqual("1.00 MB", ByteSizeFormatter.Format(1024 * 1024));
    }

    [TestMethod]
    public void Format_Gigabytes_ReturnsGBString()
    {
        long oneGb = 1024L * 1024L * 1024L;
        Assert.AreEqual("1.00 GB", ByteSizeFormatter.Format(oneGb));
    }
}

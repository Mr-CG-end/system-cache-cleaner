using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Models;
using SystemCacheCleaner.Services;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class CacheCatalogTests
{
    [TestMethod]
    public void GetCategories_NormalMode_ReturnsExactThreeCategories()
    {
        // 对应 AC-M02-01: 普通模式恰好返回 user-temp、windows-temp、edge-cache 三类
        ICacheCatalog catalog = new CacheCatalog(isDemoMode: false);
        IReadOnlyList<CacheCategoryDefinition> categories = catalog.GetCategories();

        Assert.AreEqual(3, categories.Count);

        CacheCategoryDefinition userTemp = categories[0];
        Assert.AreEqual("user-temp", userTemp.CategoryId);
        Assert.AreEqual("用户临时文件", userTemp.DisplayName);
        Assert.IsTrue(userTemp.IsDefaultSelected);
        Assert.IsTrue(userTemp.RootPaths.Count > 0);

        CacheCategoryDefinition windowsTemp = categories[1];
        Assert.AreEqual("windows-temp", windowsTemp.CategoryId);
        Assert.AreEqual("Windows 临时文件", windowsTemp.DisplayName);
        Assert.IsFalse(windowsTemp.IsDefaultSelected); // 默认不勾选

        CacheCategoryDefinition edgeCache = categories[2];
        Assert.AreEqual("edge-cache", edgeCache.CategoryId);
        Assert.AreEqual("Microsoft Edge 缓存", edgeCache.DisplayName);
        Assert.IsTrue(edgeCache.IsDefaultSelected);

        foreach (CacheCategoryDefinition category in categories)
        {
            Assert.IsTrue(
                category.RootPaths.All(Path.IsPathFullyQualified),
                $"{category.CategoryId} contains a non-absolute whitelist path.");
        }
    }

    [TestMethod]
    public void GetCategories_DemoMode_MapsToDemoCacheSubDirectories()
    {
        // 对应 AC-M02-03: 演示模式下全部映射到 DemoCache 子目录
        ICacheCatalog catalog = new CacheCatalog(isDemoMode: true);
        IReadOnlyList<CacheCategoryDefinition> categories = catalog.GetCategories();

        Assert.AreEqual(3, categories.Count);

        foreach (CacheCategoryDefinition category in categories)
        {
            Assert.AreEqual(1, category.RootPaths.Count);
            string path = category.RootPaths[0];
            Assert.IsTrue(Path.IsPathFullyQualified(path), $"Path {path} should be absolute");
            Assert.IsTrue(path.Contains(@"SystemCacheCleaner\DemoCache"), $"Path {path} should be under DemoCache");
        }

        Assert.IsTrue(categories[0].RootPaths[0].EndsWith("UserTemp"));
        Assert.IsTrue(categories[1].RootPaths[0].EndsWith("WindowsTemp"));
        Assert.IsTrue(categories[2].RootPaths[0].EndsWith("EdgeCache"));
    }
}

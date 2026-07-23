using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SystemCacheCleaner.Infrastructure;

namespace SystemCacheCleaner.Tests;

[TestClass]
public class PathSafetyTests
{
    private string _tempTestRoot = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _tempTestRoot = Path.Combine(Path.GetTempPath(), "SCC_PathSafetyTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempTestRoot))
        {
            try
            {
                Directory.Delete(_tempTestRoot, true);
            }
            catch
            {
                // 忽略清理异常
            }
        }
    }

    [TestMethod]
    public void IsUnderRoot_ValidSubFile_ReturnsTrue()
    {
        // AC-M02-04: 白名单根目录下的正常文件
        string root = Path.Combine(_tempTestRoot, "Root");
        Directory.CreateDirectory(root);
        string subFile = Path.Combine(root, "sub", "test.tmp");

        Assert.IsTrue(PathSafety.IsUnderRoot(subFile, root));
    }

    [TestMethod]
    public void IsUnderRoot_SimilarPrefixPath_ReturnsFalse()
    {
        // AC-M02-05 / UT-SAFE-02: 根目录 C:\Temp\Root，待查文件 C:\Temp\Root2\a.tmp -> 必须拒绝
        string root = Path.Combine(_tempTestRoot, "Root");
        string similarRootFile = Path.Combine(_tempTestRoot, "Root2", "a.tmp");

        Assert.IsFalse(PathSafety.IsUnderRoot(similarRootFile, root));
    }

    [TestMethod]
    public void IsUnderRoot_RootPathItself_ReturnsFalse()
    {
        // AC-M02-06 / UT-SAFE-03: 白名单根目录自身不可作为删除目标
        string root = Path.Combine(_tempTestRoot, "Root");

        Assert.IsFalse(PathSafety.IsUnderRoot(root, root));
        Assert.IsFalse(PathSafety.IsUnderRoot(root + Path.DirectorySeparatorChar, root));
    }

    [TestMethod]
    public void IsUnderRoot_DotDotTraversal_EvaluatesNormalizedPath()
    {
        // AC-M02-07: 包含 .. 且规范化后越界
        string root = Path.Combine(_tempTestRoot, "Root");
        Directory.CreateDirectory(root);

        // 试图通过 Root\..\outside.tmp 穿透到根目录外部
        string traversalPath = Path.Combine(root, "..", "outside.tmp");

        Assert.IsFalse(PathSafety.IsUnderRoot(traversalPath, root));
    }

    [TestMethod]
    public void IsPathSafe_AllowedRoots_ValidatesCorrectly()
    {
        string rootA = Path.Combine(_tempTestRoot, "CategoryA");
        string rootB = Path.Combine(_tempTestRoot, "CategoryB");
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);

        List<string> allowedRoots = new List<string> { rootA, rootB };

        string fileInA = Path.Combine(rootA, "file.tmp");
        string fileOutside = Path.Combine(_tempTestRoot, "other.tmp");

        Assert.IsTrue(PathSafety.IsPathSafe(fileInA, allowedRoots));
        Assert.IsFalse(PathSafety.IsPathSafe(fileOutside, allowedRoots));
    }

    [TestMethod]
    public void IsPathSafe_PathThroughDirectorySymbolicLink_ReturnsFalse()
    {
        // AC-M02-08: 白名单内的链接即使指向白名单外部，也必须拒绝。
        string allowedRoot = Path.Combine(_tempTestRoot, "Allowed");
        string outsideRoot = Path.Combine(_tempTestRoot, "Outside");
        string linkPath = Path.Combine(allowedRoot, "Link");
        string outsideFile = Path.Combine(outsideRoot, "outside.tmp");

        Directory.CreateDirectory(allowedRoot);
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllText(outsideFile, "test");

        if (!TestDirectoryLink.TryCreate(linkPath, outsideRoot, out string failureReason))
        {
            Assert.Inconclusive($"当前环境无法创建目录符号链接或 junction：{failureReason}");
            return;
        }

        Assert.IsTrue(PathSafety.HasReparsePoint(linkPath));

        string candidate = Path.Combine(linkPath, "outside.tmp");
        Assert.IsFalse(PathSafety.IsPathSafe(candidate, new[] { allowedRoot }));
    }
}

using System.IO;
using SystemCacheCleaner.Models;

namespace SystemCacheCleaner.Services;

public class CacheCatalog : ICacheCatalog
{
    private readonly bool _isDemoMode;

    public CacheCatalog(bool isDemoMode = false)
    {
        _isDemoMode = isDemoMode;
    }

    public IReadOnlyList<CacheCategoryDefinition> GetCategories()
    {
        if (_isDemoMode)
        {
            string localAppData = GetRequiredSpecialFolder(
                Environment.SpecialFolder.LocalApplicationData,
                "LocalApplicationData");
            string demoBase = Path.Combine(localAppData, "SystemCacheCleaner", "DemoCache");

            return new List<CacheCategoryDefinition>
            {
                new CacheCategoryDefinition(
                    CategoryId: "user-temp",
                    DisplayName: "用户临时文件",
                    Description: "当前登录用户的系统临时文件目录（演示模式）",
                    RootPaths: new[] { Path.Combine(demoBase, "UserTemp") },
                    IsDefaultSelected: true,
                    RiskLevel: "演示模式（无风险）"
                ),
                new CacheCategoryDefinition(
                    CategoryId: "windows-temp",
                    DisplayName: "Windows 临时文件",
                    Description: "Windows 系统公共临时文件目录（演示模式）",
                    RootPaths: new[] { Path.Combine(demoBase, "WindowsTemp") },
                    IsDefaultSelected: false,
                    RiskLevel: "演示模式（无风险）"
                ),
                new CacheCategoryDefinition(
                    CategoryId: "edge-cache",
                    DisplayName: "Microsoft Edge 缓存",
                    Description: "Microsoft Edge 浏览器 Cache 目录（演示模式）",
                    RootPaths: new[] { Path.Combine(demoBase, "EdgeCache") },
                    IsDefaultSelected: true,
                    RiskLevel: "演示模式（无风险）"
                )
            };
        }

        string userTempPath = GetRequiredAbsolutePath(Path.GetTempPath(), "用户临时目录");
        string windowsPath = GetRequiredSpecialFolder(Environment.SpecialFolder.Windows, "Windows");
        string windowsTempPath = Path.Combine(windowsPath, "Temp");
        string localAppDataPath = GetRequiredSpecialFolder(
            Environment.SpecialFolder.LocalApplicationData,
            "LocalApplicationData");
        string edgeCachePath = Path.Combine(localAppDataPath, "Microsoft", "Edge", "User Data", "Default", "Cache", "Cache_Data");

        return new List<CacheCategoryDefinition>
        {
            new CacheCategoryDefinition(
                CategoryId: "user-temp",
                DisplayName: "用户临时文件",
                Description: "当前登录用户的系统临时文件目录 (Path.GetTempPath())",
                RootPaths: new[] { userTempPath },
                IsDefaultSelected: true,
                RiskLevel: "低风险"
            ),
            new CacheCategoryDefinition(
                CategoryId: "windows-temp",
                DisplayName: "Windows 临时文件",
                Description: "Windows 系统公共临时文件目录 (%WINDIR%\\Temp)",
                RootPaths: new[] { windowsTempPath },
                IsDefaultSelected: false,
                RiskLevel: "中风险 (部分文件可能需要管理员权限)"
            ),
            new CacheCategoryDefinition(
                CategoryId: "edge-cache",
                DisplayName: "Microsoft Edge 缓存",
                Description: "Microsoft Edge 浏览器 Default Profile 缓存目录",
                RootPaths: new[] { edgeCachePath },
                IsDefaultSelected: true,
                RiskLevel: "低风险"
            )
        };
    }

    private static string GetRequiredSpecialFolder(Environment.SpecialFolder folder, string displayName)
    {
        return GetRequiredAbsolutePath(Environment.GetFolderPath(folder), displayName);
    }

    private static string GetRequiredAbsolutePath(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"无法解析 {displayName} 的绝对路径，已停止加载缓存目录。");
        }

        return Path.GetFullPath(path);
    }
}

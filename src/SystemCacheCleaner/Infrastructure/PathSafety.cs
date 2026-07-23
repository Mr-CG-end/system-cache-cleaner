using System.IO;

namespace SystemCacheCleaner.Infrastructure;

public static class PathSafety
{
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        string pathWithoutRoot = fullPath.Substring(root.Length).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrEmpty(pathWithoutRoot))
        {
            return root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) 
                ? root 
                : root + Path.DirectorySeparatorChar;
        }

        return root + pathWithoutRoot;
    }

    public static bool IsUnderRoot(string candidatePath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        try
        {
            string normCandidate = NormalizePath(candidatePath);
            string normRoot = NormalizePath(rootPath);

            // 规则：根目录自身不可作为待处理项
            if (string.Equals(normCandidate, normRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string rootWithSeparator = normRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? normRoot
                : normRoot + Path.DirectorySeparatorChar;

            // 规则：必须包含分隔符边界，防相似前缀越界 (如 Root2 vs Root)
            return normCandidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool HasReparsePoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }

            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            // 在遇到无法读取属性的安全拦截场景时，保守返回 true（视为存在安全风险）
            return true;
        }
    }

    public static bool IsReparsePointOrChildOfReparsePoint(string candidatePath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(rootPath))
        {
            return true;
        }

        try
        {
            string normCandidate = NormalizePath(candidatePath);
            string normRoot = NormalizePath(rootPath);

            if (!IsUnderRoot(normCandidate, normRoot))
            {
                return true;
            }

            DirectoryInfo? current = new DirectoryInfo(normCandidate);
            string normRootWithSep = normRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) 
                ? normRoot 
                : normRoot + Path.DirectorySeparatorChar;

            while (current != null)
            {
                string currentNorm = NormalizePath(current.FullName);

                if (HasReparsePoint(currentNorm))
                {
                    return true;
                }

                if (string.Equals(currentNorm, normRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = current.Parent;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    public static bool IsPathSafe(string candidatePath, IReadOnlyList<string> allowedRoots)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || allowedRoots == null || allowedRoots.Count == 0)
        {
            return false;
        }

        try
        {
            string normCandidate = NormalizePath(candidatePath);

            foreach (string root in allowedRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                string normRoot = NormalizePath(root);

                if (IsUnderRoot(normCandidate, normRoot))
                {
                    // 必须无重解析点
                    if (!IsReparsePointOrChildOfReparsePoint(normCandidate, normRoot))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}

using System.Diagnostics;
using System.IO;

namespace SystemCacheCleaner.Tests;

internal static class TestDirectoryLink
{
    public static bool TryCreate(string linkPath, string targetPath, out string failureReason)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            failureReason = string.Empty;
            return true;
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
            IOException or
            PlatformNotSupportedException)
        {
            failureReason = ex.Message;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("mklink");
            startInfo.ArgumentList.Add("/J");
            startInfo.ArgumentList.Add(linkPath);
            startInfo.ArgumentList.Add(targetPath);

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                failureReason = "无法启动 junction 创建进程。";
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 && Directory.Exists(linkPath))
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = $"{failureReason}; mklink exit={process.ExitCode}: {output} {error}".Trim();
            return false;
        }
        catch (Exception ex)
        {
            failureReason = $"{failureReason}; mklink error: {ex.Message}";
            return false;
        }
    }
}

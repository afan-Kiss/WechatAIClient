using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace WechatAIClient.Services.Wechat;

public sealed record WechatProcessInfo(
    int ProcessId,
    string ProcessName,
    string FilePath,
    string ProductVersion);

/// <summary>
/// Locates Weixin.exe / WeChat.exe and reads ProductVersion from the main module path.
/// </summary>
public static class WechatProcessProbe
{
    private static readonly string[] CandidateNames = ["Weixin", "WeChat"];

    public static bool TryFindRunning([NotNullWhen(true)] out WechatProcessInfo? info)
    {
        info = null;
        foreach (var name in CandidateNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            foreach (var process in processes)
            {
                try
                {
                    using (process)
                    {
                        string? path = null;
                        try
                        {
                            path = process.MainModule?.FileName;
                        }
                        catch
                        {
                            // Access denied on some systems — fall through
                        }

                        if (string.IsNullOrWhiteSpace(path))
                        {
                            continue;
                        }

                        var version = ReadProductVersion(path);
                        info = new WechatProcessInfo(process.Id, process.ProcessName, path, version);
                        return true;
                    }
                }
                catch
                {
                    // try next
                }
            }
        }

        return false;
    }

    public static string ReadProductVersion(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(filePath);
            return info.ProductVersion?.Trim() ?? info.FileVersion?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Classic WeChatFerry targets ~3.9.12.x.
    /// Round 5: Weixin 4.x is supported via existing Hook API (19088), not process version alone.
    /// </summary>
    public static bool IsSupportedVersion(string productVersion, out string? hint)
    {
        hint = null;
        if (string.IsNullOrWhiteSpace(productVersion))
        {
            hint = "无法读取微信版本号";
            return false;
        }

        var normalized = productVersion.Split('+')[0].Trim();
        if (IsClassicFerryTarget(normalized) || IsWeixin4HookTarget(normalized))
        {
            return true;
        }

        hint = $"当前微信版本 {normalized} 暂未在本客户端验证";
        return false;
    }

    public static bool IsWeixin4HookTarget(string productVersion)
    {
        var parts = productVersion.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 1 && parts[0] == "4";
    }

    public static bool IsClassicFerryTarget(string productVersion)
    {
        // Future: WeChatFerry-style adapters for classic 3.9.12.*
        var parts = productVersion.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        return parts[0] == "3" && parts[1] == "9" && parts[2] == "12";
    }
}

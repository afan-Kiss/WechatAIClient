using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.Media;

public interface IMediaCacheService
{
    Task<string?> GetOrFetchAvatarAsync(
        string accountId,
        string contactId,
        string? url,
        CancellationToken cancellationToken = default);

    Task<string?> GetOrFetchImageAsync(
        string accountId,
        string messageId,
        string? localPathOrUrl,
        Func<CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken = default);

    Task<string?> GetOrFetchEmojiAsync(
        string accountId,
        string messageId,
        string? urlOrPath,
        CancellationToken cancellationToken = default);

    string GetAvatarCacheDirectory(string accountId);
    string GetImageCacheDirectory(string accountId);
}

public sealed class MediaCacheService : IMediaCacheService
{
    private readonly ILogger<MediaCacheService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _failUntil = new(StringComparer.Ordinal);
    private readonly string _root;

    public MediaCacheService(ILogger<MediaCacheService> logger)
    {
        _logger = logger;
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WechatAIClient",
            "Cache");
        Directory.CreateDirectory(_root);
    }

    public string GetAvatarCacheDirectory(string accountId)
        => EnsureDir(Path.Combine(_root, "Avatars", Sanitize(accountId)));

    public string GetImageCacheDirectory(string accountId)
        => EnsureDir(Path.Combine(_root, "Media", Sanitize(accountId), "Images"));

    public Task<string?> GetOrFetchAvatarAsync(
        string accountId,
        string contactId,
        string? url,
        CancellationToken cancellationToken = default)
        => FetchHttpOrLocalAsync(
            cacheKey: $"avatar:{accountId}:{contactId}:{Hash(url)}",
            targetPath: Path.Combine(GetAvatarCacheDirectory(accountId), $"{Sanitize(contactId)}_{Hash(url)}.img"),
            urlOrPath: url,
            downloadFactory: null,
            cancellationToken);

    public Task<string?> GetOrFetchImageAsync(
        string accountId,
        string messageId,
        string? localPathOrUrl,
        Func<CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken = default)
        => FetchHttpOrLocalAsync(
            cacheKey: $"img:{accountId}:{messageId}",
            targetPath: Path.Combine(GetImageCacheDirectory(accountId), $"{Sanitize(messageId)}.img"),
            urlOrPath: localPathOrUrl,
            downloadFactory,
            cancellationToken);

    public Task<string?> GetOrFetchEmojiAsync(
        string accountId,
        string messageId,
        string? urlOrPath,
        CancellationToken cancellationToken = default)
        => FetchHttpOrLocalAsync(
            cacheKey: $"emoji:{accountId}:{messageId}:{Hash(urlOrPath)}",
            targetPath: Path.Combine(
                EnsureDir(Path.Combine(_root, "Media", Sanitize(accountId), "Emoji")),
                $"{Sanitize(messageId)}_{Hash(urlOrPath)}.img"),
            urlOrPath,
            downloadFactory: null,
            cancellationToken);

    private async Task<string?> FetchHttpOrLocalAsync(
        string cacheKey,
        string targetPath,
        string? urlOrPath,
        Func<CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken)
    {
        if (_failUntil.TryGetValue(cacheKey, out var until) && until > DateTime.UtcNow)
        {
            return File.Exists(targetPath) ? targetPath : null;
        }

        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
        {
            try
            {
                File.SetLastAccessTimeUtc(targetPath, DateTime.UtcNow);
            }
            catch
            {
                // ignore
            }

            return targetPath;
        }

        if (!string.IsNullOrWhiteSpace(urlOrPath) &&
            !urlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(urlOrPath))
        {
            try
            {
                File.Copy(urlOrPath, targetPath, overwrite: true);
                return targetPath;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Copy local media failed");
            }
        }

        var gate = _gates.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
            {
                return targetPath;
            }

            if (downloadFactory is not null)
            {
                var downloaded = await downloadFactory(cancellationToken);
                if (!string.IsNullOrWhiteSpace(downloaded) && File.Exists(downloaded))
                {
                    File.Copy(downloaded, targetPath, overwrite: true);
                    return targetPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(urlOrPath) &&
                urlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var bytes = await http.GetByteArrayAsync(urlOrPath, cancellationToken);
                await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);
                return targetPath;
            }

            _failUntil[cacheKey] = DateTime.UtcNow.AddMinutes(15);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Media fetch failed key={Key}", cacheKey);
            _failUntil[cacheKey] = DateTime.UtcNow.AddMinutes(15);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private static string Hash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}

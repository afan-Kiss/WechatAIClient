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
        MessageKey key,
        string? localPathOrUrl,
        Func<string, CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken = default);

    Task<string?> GetOrFetchImageAsync(
        string accountId,
        string messageId,
        string? localPathOrUrl,
        Func<CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken = default);

    Task<string?> GetOrFetchEmojiAsync(
        MessageKey key,
        string? urlOrPath,
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
    public const string HttpClientName = "media-cache";

    private const long MaxAvatarBytes = 10L * 1024 * 1024;
    private const long MaxEmojiBytes = 20L * 1024 * 1024;
    private const long MaxImageBytes = 50L * 1024 * 1024;

    private readonly ILogger<MediaCacheService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeyedSemaphoreGate _gates = new();
    private readonly ConcurrentDictionary<string, DateTime> _failUntil = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _globalMediaLimit = new(4, 4);
    private readonly string _root;

    public MediaCacheService(ILogger<MediaCacheService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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
            maxBytes: MaxAvatarBytes,
            cancellationToken);

    public Task<string?> GetOrFetchImageAsync(
        MessageKey key,
        string? localPathOrUrl,
        Func<string, CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken = default)
        => FetchHttpOrLocalAsync(
            cacheKey: $"img:{key.StableKey}",
            targetPath: Path.Combine(
                GetImageCacheDirectory(key.AccountId),
                $"{Sanitize(key.ConversationId)}_{Sanitize(key.MessageId)}.img"),
            urlOrPath: localPathOrUrl,
            downloadFactory,
            maxBytes: MaxImageBytes,
            cancellationToken);

    public Task<string?> GetOrFetchImageAsync(
        string accountId,
        string messageId,
        string? localPathOrUrl,
        Func<CancellationToken, Task<string?>>? downloadFactory,
        CancellationToken cancellationToken = default)
    {
        Func<string, CancellationToken, Task<string?>>? adapted = downloadFactory is null
            ? null
            : async (_, ct) => await downloadFactory(ct);
        return GetOrFetchImageAsync(
            new MessageKey(accountId, string.Empty, messageId),
            localPathOrUrl,
            adapted,
            cancellationToken);
    }

    public Task<string?> GetOrFetchEmojiAsync(
        MessageKey key,
        string? urlOrPath,
        CancellationToken cancellationToken = default)
        => FetchHttpOrLocalAsync(
            cacheKey: $"emoji:{key.StableKey}:{Hash(urlOrPath)}",
            targetPath: Path.Combine(
                EnsureDir(Path.Combine(_root, "Media", Sanitize(key.AccountId), "Emoji")),
                $"{Sanitize(key.ConversationId)}_{Sanitize(key.MessageId)}_{Hash(urlOrPath)}.img"),
            urlOrPath,
            downloadFactory: null,
            maxBytes: MaxEmojiBytes,
            cancellationToken);

    public Task<string?> GetOrFetchEmojiAsync(
        string accountId,
        string messageId,
        string? urlOrPath,
        CancellationToken cancellationToken = default)
        => GetOrFetchEmojiAsync(
            new MessageKey(accountId, string.Empty, messageId),
            urlOrPath,
            cancellationToken);

    private async Task<string?> FetchHttpOrLocalAsync(
        string cacheKey,
        string targetPath,
        string? urlOrPath,
        Func<string, CancellationToken, Task<string?>>? downloadFactory,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        PruneFailCache();

        if (_failUntil.TryGetValue(cacheKey, out var until) && until > DateTime.UtcNow)
        {
            return File.Exists(targetPath) ? targetPath : null;
        }

        if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0 && LooksLikeImageFile(targetPath))
        {
            TryTouch(targetPath);
            return targetPath;
        }

        if (!string.IsNullOrWhiteSpace(urlOrPath) &&
            !urlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(urlOrPath))
        {
            try
            {
                var localLen = new FileInfo(urlOrPath).Length;
                if (localLen <= 0 || localLen > maxBytes)
                {
                    _failUntil[cacheKey] = DateTime.UtcNow.AddMinutes(15);
                    return null;
                }

                File.Copy(urlOrPath, targetPath, overwrite: true);
                if (LooksLikeImageFile(targetPath) && new FileInfo(targetPath).Length <= maxBytes)
                {
                    _failUntil.TryRemove(cacheKey, out _);
                    return targetPath;
                }

                TryDelete(targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Copy local media failed");
            }
        }

        using var gate = await _gates.AcquireAsync(cacheKey, cancellationToken);
        await _globalMediaLimit.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0 && LooksLikeImageFile(targetPath))
            {
                _failUntil.TryRemove(cacheKey, out _);
                return targetPath;
            }

            if (downloadFactory is not null)
            {
                var downloaded = await downloadFactory(targetPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(downloaded) && File.Exists(downloaded))
                {
                    var len = new FileInfo(downloaded).Length;
                    if (len <= 0 || len > maxBytes)
                    {
                        TryDelete(downloaded);
                        _failUntil[cacheKey] = DateTime.UtcNow.AddMinutes(15);
                        return null;
                    }

                    if (!string.Equals(downloaded, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(downloaded, targetPath, overwrite: true);
                    }

                    if (LooksLikeImageFile(targetPath) && new FileInfo(targetPath).Length <= maxBytes)
                    {
                        _failUntil.TryRemove(cacheKey, out _);
                        return targetPath;
                    }

                    TryDelete(targetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(urlOrPath) &&
                urlOrPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var ok = await DownloadHttpAsync(urlOrPath, targetPath, maxBytes, cancellationToken);
                if (ok)
                {
                    _failUntil.TryRemove(cacheKey, out _);
                    return targetPath;
                }
            }

            _failUntil[cacheKey] = DateTime.UtcNow.AddMinutes(15);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Media fetch failed key={Key}", cacheKey);
            _failUntil[cacheKey] = DateTime.UtcNow.AddMinutes(15);
            TryDelete(targetPath);
            return null;
        }
        finally
        {
            _globalMediaLimit.Release();
        }
    }

    private async Task<bool> DownloadHttpAsync(
        string url,
        string targetPath,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("text/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Reject non-image content-type {ContentType} for {Url}", contentType, url);
            return false;
        }

        if (response.Content.Headers.ContentLength is { } known && known > maxBytes)
        {
            return false;
        }

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = targetPath + ".tmp";
        try
        {
            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var local = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                    {
                        return false;
                    }

                    await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            if (!LooksLikeImageFile(tempPath))
            {
                return false;
            }

            File.Copy(tempPath, targetPath, overwrite: true);
            return true;
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private void PruneFailCache()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _failUntil)
        {
            if (pair.Value <= now)
            {
                _failUntil.TryRemove(pair.Key, out _);
            }
        }
    }

    private static bool LooksLikeImageFile(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[12];
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var read = fs.Read(header);
            if (read < 3)
            {
                return false;
            }

            // JPEG
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            {
                return true;
            }

            // PNG
            if (read >= 8 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                return true;
            }

            // GIF
            if (read >= 6 &&
                header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F')
            {
                return true;
            }

            // WEBP (RIFF....WEBP)
            if (read >= 12 &&
                header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F' &&
                header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
            {
                return true;
            }

            // BMP
            if (header[0] == (byte)'B' && header[1] == (byte)'M')
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void TryTouch(string path)
    {
        try
        {
            File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
        }
        catch
        {
            // ignore
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
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

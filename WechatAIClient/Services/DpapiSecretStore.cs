using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WechatAIClient.Services;

/// <summary>
/// Phase-1 secret store using Windows DPAPI (CurrentUser scope).
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly ISettingsStore _settings;
    private readonly ILogger<DpapiSecretStore> _logger;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WechatAIClient.Secret.v1");

    public DpapiSecretStore(ISettingsStore settings, ILogger<DpapiSecretStore> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SetSecretAsync(string key, string plaintext, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext ?? string.Empty),
            Entropy,
            DataProtectionScope.CurrentUser);
        var payload = Convert.ToBase64String(protectedBytes);
        await _settings.SetAsync(SecretKey(key), payload, cancellationToken);
    }

    public async Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var payload = await _settings.GetAsync(SecretKey(key), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(payload),
                Entropy,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect secret {Key}", key);
            return null;
        }
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
        => _settings.SetAsync(SecretKey(key), string.Empty, cancellationToken);

    private static string SecretKey(string key) => $"secret.{key}";
}

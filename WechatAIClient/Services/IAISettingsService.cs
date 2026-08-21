using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IAISettingsService
{
    Task<AIGlobalSettings> GetGlobalAsync(CancellationToken cancellationToken = default);
    Task SaveGlobalAsync(AIGlobalSettings settings, CancellationToken cancellationToken = default);

    Task<AIContactOverride?> GetOverrideAsync(string accountId, string contactId, CancellationToken cancellationToken = default);
    Task SaveOverrideAsync(AIContactOverride overrideSettings, CancellationToken cancellationToken = default);
    Task ClearOverrideAsync(string accountId, string contactId, CancellationToken cancellationToken = default);

    Task<EffectiveAISettings> GetEffectiveAsync(string accountId, string contactId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPinnedIdsAsync(string accountId, string contactId, CancellationToken cancellationToken = default);
    Task SetPinnedIdsAsync(string accountId, string contactId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default);
    Task<bool> TogglePinAsync(string accountId, string contactId, string messageId, CancellationToken cancellationToken = default);

    Task<DateTime?> GetAutoPausedUntilAsync(CancellationToken cancellationToken = default);
    Task SetAutoPausedUntilAsync(DateTime? until, CancellationToken cancellationToken = default);

    Task<AIProviderSettings> GetProviderSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveProviderSettingsAsync(AIProviderSettings settings, CancellationToken cancellationToken = default);
}

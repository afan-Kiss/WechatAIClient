using WechatAIClient.Models;

namespace WechatAIClient.Services;

public interface IAISettingsService
{
    Task<AIGlobalSettings> GetGlobalAsync(CancellationToken cancellationToken = default);
    Task SaveGlobalAsync(AIGlobalSettings settings, CancellationToken cancellationToken = default);

    Task<AIContactOverride?> GetOverrideAsync(string contactId, CancellationToken cancellationToken = default);
    Task SaveOverrideAsync(AIContactOverride overrideSettings, CancellationToken cancellationToken = default);
    Task ClearOverrideAsync(string contactId, CancellationToken cancellationToken = default);

    Task<EffectiveAISettings> GetEffectiveAsync(string contactId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPinnedIdsAsync(string contactId, CancellationToken cancellationToken = default);
    Task SetPinnedIdsAsync(string contactId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default);
    Task<bool> TogglePinAsync(string contactId, string messageId, CancellationToken cancellationToken = default);

    Task<DateTime?> GetAutoPausedUntilAsync(CancellationToken cancellationToken = default);
    Task SetAutoPausedUntilAsync(DateTime? until, CancellationToken cancellationToken = default);

    Task<AIProviderSettings> GetProviderSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveProviderSettingsAsync(AIProviderSettings settings, CancellationToken cancellationToken = default);
}

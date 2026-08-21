using System.Text.Json;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class AISettingsService : IAISettingsService
{
    private const string ReplyModeKey = "ai.replyMode";
    private const string ContextLengthKey = "ai.contextLength";
    private const string AutoGenerateKey = "ai.autoGenerateOnReceive";
    private const string AutoPauseKey = "ai.autoPausedUntil";
    private const int MaxPins = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly SqliteStore _store;
    private readonly ISettingsStore _settings;
    private readonly ILogger<AISettingsService> _logger;
    private readonly object _gate = new();
    private bool _migratedLegacy;

    public AISettingsService(
        SqliteStore store,
        ISettingsStore settings,
        ILogger<AISettingsService> logger)
    {
        _store = store;
        _settings = settings;
        _logger = logger;
    }

    public async Task<AIGlobalSettings> GetGlobalAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLegacyMigratedAsync(cancellationToken);
        try
        {
            var json = await _store.GetAiGlobalJsonAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var parsed = JsonSerializer.Deserialize<AIGlobalSettings>(json, JsonOptions);
                if (parsed is not null)
                {
                    Normalize(parsed);
                    return parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read AI global settings");
        }

        return new AIGlobalSettings();
    }

    public async Task SaveGlobalAsync(AIGlobalSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize(settings);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await _store.SetAiGlobalJsonAsync(json, cancellationToken);

        // Keep legacy keys in sync for theme/other panels that may still read them
        try
        {
            await _settings.SetAsync(ReplyModeKey, settings.ReplyMode.ToString(), cancellationToken);
            await _settings.SetAsync(ContextLengthKey, settings.ContextCount.ToString(), cancellationToken);
            await _settings.SetAsync(AutoGenerateKey, settings.AutoGenerateOnReceive ? "1" : "0", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to mirror AI settings to legacy keys");
        }
    }

    public async Task<AIContactOverride?> GetOverrideAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contactId))
        {
            return null;
        }

        try
        {
            var json = await _store.GetAiOverrideJsonAsync(contactId, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var parsed = JsonSerializer.Deserialize<AIContactOverride>(json, JsonOptions);
            if (parsed is null)
            {
                return null;
            }

            parsed.ContactId = contactId;
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read override for {ContactId}", contactId);
            return null;
        }
    }

    public async Task SaveOverrideAsync(AIContactOverride overrideSettings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(overrideSettings);
        if (string.IsNullOrWhiteSpace(overrideSettings.ContactId))
        {
            throw new ArgumentException("ContactId is required", nameof(overrideSettings));
        }

        overrideSettings.UseOverride = true;
        if (overrideSettings.ContextCount is int count)
        {
            overrideSettings.ContextCount = Math.Clamp(count, 1, 100);
        }

        var json = JsonSerializer.Serialize(overrideSettings, JsonOptions);
        await _store.SetAiOverrideJsonAsync(overrideSettings.ContactId, json, cancellationToken);
    }

    public Task ClearOverrideAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contactId))
        {
            return Task.CompletedTask;
        }

        return _store.DeleteAiOverrideAsync(contactId, cancellationToken);
    }

    public async Task<EffectiveAISettings> GetEffectiveAsync(string contactId, CancellationToken cancellationToken = default)
    {
        var global = await GetGlobalAsync(cancellationToken);
        var ov = string.IsNullOrWhiteSpace(contactId)
            ? null
            : await GetOverrideAsync(contactId, cancellationToken);

        var usingOverride = ov is { UseOverride: true };
        return new EffectiveAISettings
        {
            ContactId = contactId ?? string.Empty,
            IsUsingOverride = usingOverride,
            ReplyMode = usingOverride && ov!.ReplyMode is { } rm ? rm : global.ReplyMode,
            ContextCount = usingOverride && ov!.ContextCount is { } cc ? Math.Clamp(cc, 1, 100) : global.ContextCount,
            IncludeOwnMessages = usingOverride && ov!.IncludeOwnMessages is { } iom ? iom : global.IncludeOwnMessages,
            ReplyStyle = usingOverride && ov!.ReplyStyle is { } rs ? rs : global.ReplyStyle,
            ReplyLength = usingOverride && ov!.ReplyLength is { } rl ? rl : global.ReplyLength,
            AutoGenerateOnReceive = usingOverride && ov!.AutoGenerateOnReceive is { } ag ? ag : global.AutoGenerateOnReceive,
            GroupTriggerMode = usingOverride && ov!.GroupTriggerMode is { } gtm ? gtm : global.GroupTriggerMode
        };
    }

    public Task<IReadOnlyList<string>> GetPinnedIdsAsync(string contactId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contactId))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        return _store.GetPinnedMessageIdsAsync(contactId, cancellationToken);
    }

    public Task SetPinnedIdsAsync(string contactId, IReadOnlyList<string> messageIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contactId))
        {
            return Task.CompletedTask;
        }

        var clipped = (messageIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxPins)
            .ToList();

        return _store.SetPinnedMessageIdsAsync(contactId, clipped, cancellationToken);
    }

    public async Task<bool> TogglePinAsync(string contactId, string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contactId) || string.IsNullOrWhiteSpace(messageId))
        {
            return false;
        }

        var pins = (await GetPinnedIdsAsync(contactId, cancellationToken)).ToList();
        var idx = pins.FindIndex(id => string.Equals(id, messageId, StringComparison.Ordinal));
        if (idx >= 0)
        {
            pins.RemoveAt(idx);
            await SetPinnedIdsAsync(contactId, pins, cancellationToken);
            return false;
        }

        if (pins.Count >= MaxPins)
        {
            pins.RemoveAt(0);
        }

        pins.Add(messageId);
        await SetPinnedIdsAsync(contactId, pins, cancellationToken);
        return true;
    }

    public async Task<DateTime?> GetAutoPausedUntilAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var raw = await _settings.GetAsync(AutoPauseKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var until))
            {
                return until;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read auto-pause");
        }

        return null;
    }

    public async Task SetAutoPausedUntilAsync(DateTime? until, CancellationToken cancellationToken = default)
    {
        var value = until?.ToString("O") ?? string.Empty;
        await _settings.SetAsync(AutoPauseKey, value, cancellationToken);
    }

    private async Task EnsureLegacyMigratedAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_migratedLegacy)
            {
                return;
            }

            _migratedLegacy = true;
        }

        try
        {
            var existing = await _store.GetAiGlobalJsonAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return;
            }

            var settings = new AIGlobalSettings();
            var mode = await _settings.GetAsync(ReplyModeKey, cancellationToken);
            if (Enum.TryParse<AIReplyMode>(mode, true, out var parsedMode))
            {
                settings.ReplyMode = parsedMode;
            }

            var length = await _settings.GetAsync(ContextLengthKey, cancellationToken);
            if (int.TryParse(length, out var contextLength))
            {
                settings.ContextCount = Math.Clamp(contextLength, 1, 100);
            }

            var auto = await _settings.GetAsync(AutoGenerateKey, cancellationToken);
            if (auto is "0" or "1")
            {
                settings.AutoGenerateOnReceive = auto == "1";
            }

            await _store.SetAiGlobalJsonAsync(JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate legacy AI settings");
        }
    }

    private static void Normalize(AIGlobalSettings settings)
    {
        settings.ContextCount = Math.Clamp(settings.ContextCount <= 0 ? 10 : settings.ContextCount, 1, 100);
    }
}

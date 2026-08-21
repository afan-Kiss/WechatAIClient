namespace WechatAIClient.Services;

public sealed class SqliteSettingsStore : ISettingsStore
{
    private readonly SqliteStore _store;

    public SqliteSettingsStore(SqliteStore store)
    {
        _store = store;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => _store.GetSettingAsync(key, cancellationToken);

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        => _store.SetSettingAsync(key, value, cancellationToken);
}

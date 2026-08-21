using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace WechatAIClient.Services;

/// <summary>
/// SQLite bootstrap for future persistence. Phase 1 keeps chat data in memory/mock.
/// </summary>
public sealed class SqliteStore
{
    private readonly ILogger<SqliteStore> _logger;
    private readonly string _dbPath;

    public SqliteStore(ILogger<SqliteStore> logger)
    {
        _logger = logger;
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WechatAIClient");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "wechat-ai.db");
    }

    public string DatabasePath => _dbPath;

    public void Initialize()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ai_reply_history (
                    id TEXT PRIMARY KEY,
                    contact_name TEXT NOT NULL,
                    status TEXT NOT NULL,
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
            _logger.LogInformation("SQLite initialized at {Path}", _dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQLite");
        }
    }
}

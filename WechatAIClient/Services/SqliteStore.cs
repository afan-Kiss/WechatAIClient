using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class SqliteStore
{
    private const int CurrentSchemaVersion = 2;
    private readonly ILogger<SqliteStore> _logger;
    private readonly string _dbPath;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _memorySettings = new(StringComparer.Ordinal);
    private readonly List<AIReplyHistoryItem> _memoryHistory = [];
    private bool _useMemoryFallback;

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
    public bool IsUsingMemoryFallback => _useMemoryFallback;

    public void Initialize()
    {
        try
        {
            using var connection = OpenConnection();
            EnsureSchema(connection);
            _logger.LogInformation("SQLite initialized at {Path}", _dbPath);
        }
        catch (Exception ex)
        {
            _useMemoryFallback = true;
            _logger.LogError(ex, "Failed to initialize SQLite; using in-memory fallback");
        }
    }

    public SqliteConnection CreateConnection()
    {
        if (_useMemoryFallback)
        {
            throw new InvalidOperationException("SQLite unavailable; using memory fallback.");
        }

        return OpenConnection();
    }

    public Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                return Task.FromResult(_memorySettings.TryGetValue(key, out var value) ? value : null);
            }
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM settings WHERE key = $key LIMIT 1;";
            command.Parameters.AddWithValue("$key", key);
            var result = command.ExecuteScalar();
            return result as string;
        }, cancellationToken);
    }

    public Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        value ??= string.Empty;

        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memorySettings[key] = value;
            }

            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO settings(key, value) VALUES($key, $value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }, cancellationToken);
    }

    public Task InsertHistoryAsync(AIReplyHistoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memoryHistory.Insert(0, item);
                TrimMemoryHistory();
            }

            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO ai_reply_history(id, contact_id, contact_name, status, content, created_at)
                VALUES($id, $contactId, $contactName, $status, $content, $createdAt);
                """;
            insert.Parameters.AddWithValue("$id", item.Id);
            insert.Parameters.AddWithValue("$contactId", item.ContactId ?? string.Empty);
            insert.Parameters.AddWithValue("$contactName", item.ContactName ?? string.Empty);
            insert.Parameters.AddWithValue("$status", item.Status ?? string.Empty);
            insert.Parameters.AddWithValue("$content", item.Content ?? string.Empty);
            insert.Parameters.AddWithValue("$createdAt", item.Timestamp.ToString("O"));
            insert.ExecuteNonQuery();

            using var trim = connection.CreateCommand();
            trim.CommandText =
                """
                DELETE FROM ai_reply_history
                WHERE id NOT IN (
                    SELECT id FROM ai_reply_history
                    ORDER BY created_at DESC
                    LIMIT 200
                );
                """;
            trim.ExecuteNonQuery();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AIReplyHistoryItem>> GetHistoryAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                return Task.FromResult<IReadOnlyList<AIReplyHistoryItem>>(_memoryHistory.Take(limit).ToList());
            }
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT id, contact_id, contact_name, status, content, created_at
                FROM ai_reply_history
                ORDER BY created_at DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", limit);

            var list = new List<AIReplyHistoryItem>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AIReplyHistoryItem
                {
                    Id = reader.GetString(0),
                    ContactId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ContactName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    Content = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Timestamp = DateTime.TryParse(reader.GetString(5), out var ts) ? ts : DateTime.Now
                });
            }

            return (IReadOnlyList<AIReplyHistoryItem>)list;
        }, cancellationToken);
    }

    public Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memoryHistory.Clear();
            }

            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ai_reply_history;";
            command.ExecuteNonQuery();
        }, cancellationToken);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        return connection;
    }

    private void EnsureSchema(SqliteConnection connection)
    {
        using (var bootstrap = connection.CreateCommand())
        {
            bootstrap.CommandText =
                """
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ai_reply_history (
                    id TEXT PRIMARY KEY,
                    contact_id TEXT NOT NULL DEFAULT '',
                    contact_name TEXT NOT NULL,
                    status TEXT NOT NULL,
                    content TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );
                """;
            bootstrap.ExecuteNonQuery();
        }

        var version = ReadSchemaVersion(connection);
        if (version < 1)
        {
            using var seed = connection.CreateCommand();
            seed.CommandText = "INSERT INTO schema_version(version) VALUES(1);";
            seed.ExecuteNonQuery();
            version = 1;
        }

        if (version < 2)
        {
            EnsureColumn(connection, "ai_reply_history", "contact_id", "TEXT NOT NULL DEFAULT ''");
            using var bump = connection.CreateCommand();
            bump.CommandText = "UPDATE schema_version SET version = 2;";
            bump.ExecuteNonQuery();
        }

        if (ReadSchemaVersion(connection) < CurrentSchemaVersion)
        {
            using var sync = connection.CreateCommand();
            sync.CommandText = "UPDATE schema_version SET version = $v;";
            sync.Parameters.AddWithValue("$v", CurrentSchemaVersion);
            sync.ExecuteNonQuery();
        }
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        var result = command.ExecuteScalar();
        return result is long l ? (int)l : result is int i ? i : 0;
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        reader.Close();
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    private void TrimMemoryHistory()
    {
        while (_memoryHistory.Count > 200)
        {
            _memoryHistory.RemoveAt(_memoryHistory.Count - 1);
        }
    }
}

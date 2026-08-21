using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services;

public sealed class SqliteStore
{
    private const int CurrentSchemaVersion = 3;
    private readonly ILogger<SqliteStore> _logger;
    private readonly string _dbPath;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _memorySettings = new(StringComparer.Ordinal);
    private readonly List<AIReplyHistoryItem> _memoryHistory = [];
    private string? _memoryGlobalJson;
    private readonly Dictionary<string, string> _memoryOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _memoryPins = new(StringComparer.Ordinal);
    private bool _useMemoryFallback;

    public SqliteStore(ILogger<SqliteStore> logger, string? databasePath = null)
    {
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            var dir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _dbPath = databasePath;
        }
        else
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WechatAIClient");
            Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "wechat-ai.db");
        }
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
            version = 2;
        }

        if (version < 3)
        {
            try
            {
                using var v3 = connection.CreateCommand();
                v3.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS ai_global_settings (
                        id INTEGER PRIMARY KEY CHECK(id=1),
                        json TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS ai_contact_overrides (
                        contact_id TEXT PRIMARY KEY,
                        json TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS ai_pinned_messages (
                        contact_id TEXT NOT NULL,
                        message_id TEXT NOT NULL,
                        PRIMARY KEY(contact_id, message_id)
                    );
                    """;
                v3.ExecuteNonQuery();

                using var bump = connection.CreateCommand();
                bump.CommandText = "UPDATE schema_version SET version = 3;";
                bump.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate SQLite schema to v3; continuing with defaults");
            }
        }

        if (ReadSchemaVersion(connection) < CurrentSchemaVersion)
        {
            try
            {
                using var sync = connection.CreateCommand();
                sync.CommandText = "UPDATE schema_version SET version = $v;";
                sync.Parameters.AddWithValue("$v", CurrentSchemaVersion);
                sync.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync schema version to {Version}", CurrentSchemaVersion);
            }
        }
    }

    public Task<string?> GetAiGlobalJsonAsync(CancellationToken cancellationToken = default)
    {
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                return Task.FromResult(_memoryGlobalJson);
            }
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT json FROM ai_global_settings WHERE id = 1 LIMIT 1;";
                return command.ExecuteScalar() as string;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetAiGlobalJson failed");
                return null;
            }
        }, cancellationToken);
    }

    public Task SetAiGlobalJsonAsync(string json, CancellationToken cancellationToken = default)
    {
        json ??= "{}";
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memoryGlobalJson = json;
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
                INSERT INTO ai_global_settings(id, json) VALUES(1, $json)
                ON CONFLICT(id) DO UPDATE SET json = excluded.json;
                """;
            command.Parameters.AddWithValue("$json", json);
            command.ExecuteNonQuery();
        }, cancellationToken);
    }

    public Task<string?> GetAiOverrideJsonAsync(string contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                return Task.FromResult(_memoryOverrides.TryGetValue(contactId, out var json) ? json : null);
            }
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT json FROM ai_contact_overrides WHERE contact_id = $id LIMIT 1;";
                command.Parameters.AddWithValue("$id", contactId);
                return command.ExecuteScalar() as string;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetAiOverrideJson failed for {ContactId}", contactId);
                return null;
            }
        }, cancellationToken);
    }

    public Task SetAiOverrideJsonAsync(string contactId, string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        json ??= "{}";

        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memoryOverrides[contactId] = json;
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
                INSERT INTO ai_contact_overrides(contact_id, json) VALUES($id, $json)
                ON CONFLICT(contact_id) DO UPDATE SET json = excluded.json;
                """;
            command.Parameters.AddWithValue("$id", contactId);
            command.Parameters.AddWithValue("$json", json);
            command.ExecuteNonQuery();
        }, cancellationToken);
    }

    public Task DeleteAiOverrideAsync(string contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memoryOverrides.Remove(contactId);
            }

            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM ai_contact_overrides WHERE contact_id = $id;";
            command.Parameters.AddWithValue("$id", contactId);
            command.ExecuteNonQuery();
        }, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetPinnedMessageIdsAsync(string contactId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                if (_memoryPins.TryGetValue(contactId, out var set))
                {
                    return Task.FromResult<IReadOnlyList<string>>(set.ToList());
                }

                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT message_id FROM ai_pinned_messages WHERE contact_id = $id;";
                command.Parameters.AddWithValue("$id", contactId);
                var list = new List<string>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(reader.GetString(0));
                }

                return (IReadOnlyList<string>)list;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetPinnedMessageIds failed for {ContactId}", contactId);
                return Array.Empty<string>();
            }
        }, cancellationToken);
    }

    public Task SetPinnedMessageIdsAsync(
        string contactId,
        IReadOnlyList<string> messageIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactId);
        messageIds ??= Array.Empty<string>();

        if (_useMemoryFallback)
        {
            lock (_gate)
            {
                _memoryPins[contactId] = new HashSet<string>(
                    messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Take(8),
                    StringComparer.Ordinal);
            }

            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = OpenConnection();
            using var tx = connection.BeginTransaction();
            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM ai_pinned_messages WHERE contact_id = $id;";
                del.Parameters.AddWithValue("$id", contactId);
                del.ExecuteNonQuery();
            }

            foreach (var messageId in messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Take(8))
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = tx;
                insert.CommandText =
                    "INSERT OR IGNORE INTO ai_pinned_messages(contact_id, message_id) VALUES($cid, $mid);";
                insert.Parameters.AddWithValue("$cid", contactId);
                insert.Parameters.AddWithValue("$mid", messageId);
                insert.ExecuteNonQuery();
            }

            tx.Commit();
        }, cancellationToken);
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

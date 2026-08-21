using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using WechatAIClient.Models;
using WechatAIClient.Services;

namespace WechatAIClient.Tests;

public class Round52SchemaV5Tests
{
    [Fact]
    public void EmptyDb_MigratesToSchemaV5()
    {
        var db = TempDbPath("empty-v5");
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();

        using var connection = store.CreateConnection();
        Assert.Equal(5, ReadSchemaVersion(connection));
        Assert.True(TableExists(connection, "ai_contact_overrides"));
        Assert.True(TableExists(connection, "ai_pinned_messages"));
        Assert.True(TableExists(connection, "wechat_account_profiles"));
        Assert.True(TableHasColumn(connection, "ai_contact_overrides", "account_id"));
        Assert.True(TableHasColumn(connection, "ai_pinned_messages", "account_id"));
        Assert.True(TableHasColumn(connection, "ai_reply_history", "account_id"));
        Assert.True(TableHasColumn(connection, "ai_reply_history", "account_name"));
    }

    [Fact]
    public async Task LegacyOverride_MigratesToAccountIdLegacy()
    {
        var db = TempDbPath("legacy-override");
        CreateV4DatabaseWithOverride(db, contactId: "friend_a", json: """{"contactId":"friend_a","useOverride":true,"contextCount":33}""");

        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(store, new FakeSettings(), NullLogger<AISettingsService>.Instance);

        var ov = await settings.GetOverrideAsync("legacy", "friend_a");
        Assert.NotNull(ov);
        Assert.Equal("legacy", ov!.AccountId);
        Assert.Equal("friend_a", ov.ContactId);
        Assert.Equal(33, ov.ContextCount);

        var missing = await settings.GetOverrideAsync("wxid_other", "friend_a");
        Assert.Null(missing);
    }

    [Fact]
    public async Task TwoAccounts_SameContactId_DifferentOverrides()
    {
        var db = TempDbPath("two-overrides");
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(store, new FakeSettings(), NullLogger<AISettingsService>.Instance);

        await settings.SaveOverrideAsync(new AIContactOverride
        {
            AccountId = "acc_a",
            ContactId = "same_contact",
            UseOverride = true,
            ContextCount = 11
        });
        await settings.SaveOverrideAsync(new AIContactOverride
        {
            AccountId = "acc_b",
            ContactId = "same_contact",
            UseOverride = true,
            ContextCount = 22
        });

        var a = await settings.GetEffectiveAsync("acc_a", "same_contact");
        var b = await settings.GetEffectiveAsync("acc_b", "same_contact");
        Assert.Equal(11, a.ContextCount);
        Assert.Equal(22, b.ContextCount);
        Assert.Equal("acc_a", a.AccountId);
        Assert.Equal("acc_b", b.AccountId);
    }

    [Fact]
    public async Task TwoAccounts_SameMessageId_BothCanPin()
    {
        var db = TempDbPath("two-pins");
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();
        var settings = new AISettingsService(store, new FakeSettings(), NullLogger<AISettingsService>.Instance);

        Assert.True(await settings.TogglePinAsync("acc_a", "c1", "msg_shared"));
        Assert.True(await settings.TogglePinAsync("acc_b", "c1", "msg_shared"));

        var pinsA = await settings.GetPinnedIdsAsync("acc_a", "c1");
        var pinsB = await settings.GetPinnedIdsAsync("acc_b", "c1");
        Assert.Contains("msg_shared", pinsA);
        Assert.Contains("msg_shared", pinsB);

        Assert.False(await settings.TogglePinAsync("acc_a", "c1", "msg_shared"));
        pinsA = await settings.GetPinnedIdsAsync("acc_a", "c1");
        pinsB = await settings.GetPinnedIdsAsync("acc_b", "c1");
        Assert.DoesNotContain("msg_shared", pinsA);
        Assert.Contains("msg_shared", pinsB);
    }

    [Fact]
    public async Task History_WritesAndReadsAccountId()
    {
        var db = TempDbPath("history-account");
        var store = new SqliteStore(NullLogger<SqliteStore>.Instance, db);
        store.Initialize();

        await store.InsertHistoryAsync(new AIReplyHistoryItem
        {
            Id = Guid.NewGuid().ToString("N"),
            ContactId = "c1",
            ContactName = "好友",
            AccountId = "wxid_me",
            AccountName = "我的号",
            Status = "手动确认",
            Content = "你好",
            Timestamp = DateTime.Now
        });

        var history = await store.GetHistoryAsync(10);
        Assert.NotEmpty(history);
        Assert.Equal("wxid_me", history[0].AccountId);
        Assert.Equal("我的号", history[0].AccountName);
    }

    private static string TempDbPath(string label)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WechatAIClientTests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"round52-{label}-{Guid.NewGuid():N}.db");
    }

    private static void CreateV4DatabaseWithOverride(string dbPath, string contactId, string json)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE schema_version (version INTEGER NOT NULL);
            INSERT INTO schema_version(version) VALUES(4);
            CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE ai_reply_history (
                id TEXT PRIMARY KEY,
                contact_id TEXT NOT NULL DEFAULT '',
                contact_name TEXT NOT NULL,
                status TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE ai_global_settings (
                id INTEGER PRIMARY KEY CHECK(id=1),
                json TEXT NOT NULL
            );
            CREATE TABLE ai_provider_settings (
                id INTEGER PRIMARY KEY CHECK(id=1),
                json TEXT NOT NULL
            );
            CREATE TABLE ai_contact_overrides (
                contact_id TEXT PRIMARY KEY,
                json TEXT NOT NULL
            );
            CREATE TABLE ai_pinned_messages (
                contact_id TEXT NOT NULL,
                message_id TEXT NOT NULL,
                PRIMARY KEY(contact_id, message_id)
            );
            INSERT INTO ai_contact_overrides(contact_id, json) VALUES($cid, $json);
            """;
        cmd.Parameters.AddWithValue("$cid", contactId);
        cmd.Parameters.AddWithValue("$json", json);
        cmd.ExecuteNonQuery();
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version LIMIT 1;";
        var result = command.ExecuteScalar();
        return result is long l ? (int)l : result is int i ? i : 0;
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
    }

    private static bool TableHasColumn(SqliteConnection connection, string table, string column)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

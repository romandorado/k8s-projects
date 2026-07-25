using Microsoft.Data.Sqlite;

namespace Terraria.Agent.Api.Services;

public class ChatHistory
{
    private readonly string _dbPath;
    private readonly ILogger<ChatHistory> _logger;

    public ChatHistory(IConfiguration config, ILogger<ChatHistory> logger)
    {
        _dbPath = config["Database:Path"] ?? "/app/data/agent.db";
        _logger = logger;
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS chat_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                player TEXT NOT NULL,
                role TEXT NOT NULL,
                message TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS idx_chat_player ON chat_history(player, created_at);";
        cmd.ExecuteNonQuery();
        _logger.LogInformation("Chat history database initialized at {Path}", _dbPath);
    }

    public async Task SaveMessageAsync(string player, string role, string message)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO chat_history (player, role, message) VALUES (@player, @role, @message)";
        cmd.Parameters.AddWithValue("@player", player);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@message", message);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<(string Role, string Message, DateTime Time)>> GetHistoryAsync(string player, int limit = 20)
    {
        var history = new List<(string Role, string Message, DateTime Time)>();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT role, message, created_at FROM chat_history
            WHERE player = @player
            ORDER BY created_at DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("@player", player);
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDateTime(2)
            ));
        }

        history.Reverse(); // oldest first
        return history;
    }

    public async Task PruneOldMessagesAsync(int maxPerPlayer = 500)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var playersCmd = connection.CreateCommand();
        playersCmd.CommandText = "SELECT DISTINCT player FROM chat_history";
        var players = new List<string>();
        using (var reader = await playersCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                players.Add(reader.GetString(0));
        }

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM chat_history WHERE id IN (
                SELECT id FROM chat_history
                WHERE player = @player
                ORDER BY created_at DESC
                LIMIT -1 OFFSET @max
            )";

        foreach (var player in players)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@player", player);
            cmd.Parameters.AddWithValue("@max", maxPerPlayer);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

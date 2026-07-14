using System.Text.Json;
using Garmetix.AttendanceKiosk.Models;
using Microsoft.Data.Sqlite;

namespace Garmetix.AttendanceKiosk.Services;

public sealed class OfflinePunchQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _databasePath;

    public OfflinePunchQueue()
    {
        var folder = FileSystem.AppDataDirectory;
        Directory.CreateDirectory(folder);
        _databasePath = Path.Combine(folder, "garmetix-kiosk-queue.db");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS pending_punches (
                id TEXT PRIMARY KEY,
                clientPunchId TEXT NOT NULL,
                payloadJson TEXT NOT NULL,
                createdAtUtc TEXT NOT NULL,
                lastAttemptAtUtc TEXT NULL,
                attemptCount INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'Pending',
                lastError TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_pending_punches_status_created
                ON pending_punches(status, createdAtUtc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EnqueueAsync(KioskPunchRequest request, string? lastError = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO pending_punches
                (id, clientPunchId, payloadJson, createdAtUtc, lastAttemptAtUtc, attemptCount, status, lastError)
            VALUES
                ($id, $clientPunchId, $payloadJson, $createdAtUtc, NULL, 0, 'Pending', $lastError);
            """;
        command.Parameters.AddWithValue("$id", request.ClientPunchId);
        command.Parameters.AddWithValue("$clientPunchId", request.ClientPunchId);
        command.Parameters.AddWithValue("$payloadJson", JsonSerializer.Serialize(request, JsonOptions));
        command.Parameters.AddWithValue("$createdAtUtc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$lastError", (object?)lastError ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KioskPunchRequest>> GetPendingPunchesAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var rows = new List<KioskPunchRequest>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT payloadJson
            FROM pending_punches
            WHERE status IN ('Pending', 'Retry')
            ORDER BY createdAtUtc
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var payload = JsonSerializer.Deserialize<KioskPunchRequest>(reader.GetString(0), JsonOptions);
            if (payload is not null) rows.Add(payload);
        }
        return rows;
    }

    public async Task MarkSyncedAsync(IEnumerable<string> clientPunchIds, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        foreach (var clientPunchId in clientPunchIds)
        {
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE pending_punches SET status = 'Accepted', lastError = NULL WHERE clientPunchId = $clientPunchId;";
            command.Parameters.AddWithValue("$clientPunchId", clientPunchId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task MarkRetryAsync(string clientPunchId, string error, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE pending_punches
            SET status = 'Retry',
                lastAttemptAtUtc = $lastAttemptAtUtc,
                attemptCount = attemptCount + 1,
                lastError = $lastError
            WHERE clientPunchId = $clientPunchId;
            """;
        command.Parameters.AddWithValue("$lastAttemptAtUtc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$lastError", error);
        command.Parameters.AddWithValue("$clientPunchId", clientPunchId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pending_punches WHERE status IN ('Pending', 'Retry');";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private SqliteConnection CreateConnection()
        => new($"Data Source={_databasePath}");
}

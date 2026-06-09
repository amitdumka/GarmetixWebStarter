using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Garmetix.Api.SecondarySync;

public static class OracleSecondarySyncLocalStore
{
    public static async Task RepairAsync(GarmetixDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "OracleSyncState" (
                "Key" text NOT NULL,
                "Value" text NULL,
                "UpdatedAtUtc" timestamp without time zone NOT NULL DEFAULT now(),
                CONSTRAINT "PK_OracleSyncState" PRIMARY KEY ("Key")
            );

            CREATE TABLE IF NOT EXISTS "OracleSyncRuns" (
                "Id" uuid NOT NULL,
                "StartedAtUtc" timestamp without time zone NOT NULL,
                "FinishedAtUtc" timestamp without time zone NULL,
                "Success" boolean NOT NULL DEFAULT false,
                "TotalPushed" integer NOT NULL DEFAULT 0,
                "TotalPulled" integer NOT NULL DEFAULT 0,
                "Message" text NULL,
                "Error" text NULL,
                CONSTRAINT "PK_OracleSyncRuns" PRIMARY KEY ("Id")
            );

            ALTER TABLE "OracleSyncRuns" ADD COLUMN IF NOT EXISTS "TotalPulled" integer NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS "OracleSyncInboundEvents" (
                "Id" uuid NOT NULL,
                "OracleEventId" text NOT NULL,
                "TenantId" text NOT NULL,
                "SourceApplication" text NOT NULL,
                "EntityName" text NOT NULL,
                "EntityId" text NOT NULL,
                "Operation" text NOT NULL,
                "VersionUtc" timestamp without time zone NOT NULL,
                "PayloadHash" text NOT NULL,
                "PayloadJson" text NOT NULL,
                "ConflictPolicy" text NOT NULL,
                "Status" text NOT NULL,
                "Note" text NULL,
                "PulledAtUtc" timestamp without time zone NOT NULL,
                "AppliedAtUtc" timestamp without time zone NULL,
                "Error" text NULL,
                CONSTRAINT "PK_OracleSyncInboundEvents" PRIMARY KEY ("Id")
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "UX_OracleSyncInboundEvents_OracleEventId" ON "OracleSyncInboundEvents" ("OracleEventId");
            CREATE INDEX IF NOT EXISTS "IX_OracleSyncInboundEvents_Status" ON "OracleSyncInboundEvents" ("Status", "PulledAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_OracleSyncInboundEvents_Entity" ON "OracleSyncInboundEvents" ("EntityName", "EntityId");

            CREATE TABLE IF NOT EXISTS "OracleSyncDeadLetters" (
                "Id" uuid NOT NULL,
                "Direction" text NOT NULL,
                "OracleEventId" text NULL,
                "SourceApplication" text NOT NULL,
                "EntityName" text NOT NULL,
                "EntityId" text NOT NULL,
                "Reason" text NOT NULL,
                "PayloadJson" text NULL,
                "Error" text NULL,
                "RetryCount" integer NOT NULL DEFAULT 0,
                "Resolved" boolean NOT NULL DEFAULT false,
                "CreatedAtUtc" timestamp without time zone NOT NULL,
                "UpdatedAtUtc" timestamp without time zone NOT NULL,
                CONSTRAINT "PK_OracleSyncDeadLetters" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_OracleSyncRuns_StartedAtUtc" ON "OracleSyncRuns" ("StartedAtUtc");
            CREATE INDEX IF NOT EXISTS "IX_OracleSyncDeadLetters_Open" ON "OracleSyncDeadLetters" ("Resolved", "UpdatedAtUtc");
            """, cancellationToken);
    }

    public static async Task<string?> GetStateAsync(GarmetixDbContext db, string key, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"Value\" FROM \"OracleSyncState\" WHERE \"Key\" = @key";
            AddParameter(command, "key", key);
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is DBNull or null ? null : Convert.ToString(value);
        }
        finally
        {
            if (closeAfter)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static async Task SetStateAsync(GarmetixDbContext db, string key, string? value, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO "OracleSyncState" ("Key", "Value", "UpdatedAtUtc")
                VALUES (@key, @value, now())
                ON CONFLICT ("Key") DO UPDATE SET "Value" = EXCLUDED."Value", "UpdatedAtUtc" = now();
                """;
            AddParameter(command, "key", key);
            AddParameter(command, "value", value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (closeAfter)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static async Task AddRunAsync(
        GarmetixDbContext db,
        Guid id,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        bool success,
        int totalPushed,
        int totalPulled,
        string message,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OracleSyncRuns" ("Id", "StartedAtUtc", "FinishedAtUtc", "Success", "TotalPushed", "TotalPulled", "Message", "Error")
            VALUES ({id}, {startedAtUtc.UtcDateTime}, {finishedAtUtc.UtcDateTime}, {success}, {totalPushed}, {totalPulled}, {message}, {error});
            """, cancellationToken);
    }

    public static async Task AddInboundEventAsync(
        GarmetixDbContext db,
        OracleSyncInboundEventRow row,
        string payloadHash,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OracleSyncInboundEvents" (
                "Id", "OracleEventId", "TenantId", "SourceApplication", "EntityName", "EntityId", "Operation",
                "VersionUtc", "PayloadHash", "PayloadJson", "ConflictPolicy", "Status", "Note", "PulledAtUtc", "AppliedAtUtc", "Error")
            VALUES ({row.Id}, {row.OracleEventId}, {row.TenantId}, {row.SourceApplication}, {row.EntityName}, {row.EntityId}, {row.Operation},
                {row.VersionUtc}, {payloadHash}, {payloadJson}, {row.ConflictPolicy}, {row.Status}, {row.Note}, {row.PulledAtUtc}, {row.AppliedAtUtc}, {row.Error})
            ON CONFLICT ("OracleEventId") DO UPDATE SET
                "PayloadHash" = EXCLUDED."PayloadHash",
                "PayloadJson" = EXCLUDED."PayloadJson",
                "Status" = EXCLUDED."Status",
                "Note" = EXCLUDED."Note",
                "PulledAtUtc" = EXCLUDED."PulledAtUtc",
                "Error" = EXCLUDED."Error";
            """, cancellationToken);
    }

    public static async Task AddDeadLetterAsync(
        GarmetixDbContext db,
        string direction,
        string? oracleEventId,
        string sourceApplication,
        string entityName,
        string entityId,
        string reason,
        string? payloadJson,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OracleSyncDeadLetters" (
                "Id", "Direction", "OracleEventId", "SourceApplication", "EntityName", "EntityId", "Reason", "PayloadJson", "Error", "RetryCount", "Resolved", "CreatedAtUtc", "UpdatedAtUtc")
            VALUES ({Guid.NewGuid()}, {direction}, {oracleEventId}, {sourceApplication}, {entityName}, {entityId}, {reason}, {payloadJson}, {error}, 0, false, {DateTime.UtcNow}, {DateTime.UtcNow});
            """, cancellationToken);
    }

    public static async Task<IReadOnlyList<OracleSyncHistoryRow>> GetRunsAsync(GarmetixDbContext db, int take = 25, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var rows = new List<OracleSyncHistoryRow>();
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT "Id", "StartedAtUtc", "FinishedAtUtc", "Success", "TotalPushed", "TotalPulled", "Message", "Error"
                FROM "OracleSyncRuns"
                ORDER BY "StartedAtUtc" DESC
                LIMIT @take;
                """;
            AddParameter(command, "take", Math.Clamp(take, 1, 200));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new OracleSyncHistoryRow(
                    reader.GetGuid(0),
                    reader.GetDateTime(1),
                    reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                    reader.GetBoolean(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }
        finally
        {
            if (closeAfter) await connection.CloseAsync();
        }

        return rows;
    }

    public static async Task<IReadOnlyList<OracleSyncInboundEventRow>> GetInboundEventsAsync(GarmetixDbContext db, int take = 50, string? status = null, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var rows = new List<OracleSyncInboundEventRow>();
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = string.IsNullOrWhiteSpace(status)
                ? """
                    SELECT "Id", "OracleEventId", "TenantId", "SourceApplication", "EntityName", "EntityId", "Operation", "VersionUtc", "ConflictPolicy", "Status", "Note", "PulledAtUtc", "AppliedAtUtc", "Error"
                    FROM "OracleSyncInboundEvents"
                    ORDER BY "PulledAtUtc" DESC
                    LIMIT @take;
                    """
                : """
                    SELECT "Id", "OracleEventId", "TenantId", "SourceApplication", "EntityName", "EntityId", "Operation", "VersionUtc", "ConflictPolicy", "Status", "Note", "PulledAtUtc", "AppliedAtUtc", "Error"
                    FROM "OracleSyncInboundEvents"
                    WHERE "Status" = @status
                    ORDER BY "PulledAtUtc" DESC
                    LIMIT @take;
                    """;
            AddParameter(command, "take", Math.Clamp(take, 1, 200));
            if (!string.IsNullOrWhiteSpace(status)) AddParameter(command, "status", status);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadInbound(reader));
            }
        }
        finally
        {
            if (closeAfter) await connection.CloseAsync();
        }

        return rows;
    }

    public static async Task<IReadOnlyList<OracleSyncDeadLetterRow>> GetDeadLettersAsync(GarmetixDbContext db, int take = 50, bool includeResolved = false, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var rows = new List<OracleSyncDeadLetterRow>();
        var connection = db.Database.GetDbConnection();
        var closeAfter = connection.State != ConnectionState.Open;
        if (closeAfter) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = includeResolved
                ? """
                    SELECT "Id", "Direction", "OracleEventId", "SourceApplication", "EntityName", "EntityId", "Reason", "Error", "RetryCount", "Resolved", "CreatedAtUtc", "UpdatedAtUtc"
                    FROM "OracleSyncDeadLetters"
                    ORDER BY "UpdatedAtUtc" DESC
                    LIMIT @take;
                    """
                : """
                    SELECT "Id", "Direction", "OracleEventId", "SourceApplication", "EntityName", "EntityId", "Reason", "Error", "RetryCount", "Resolved", "CreatedAtUtc", "UpdatedAtUtc"
                    FROM "OracleSyncDeadLetters"
                    WHERE NOT "Resolved"
                    ORDER BY "UpdatedAtUtc" DESC
                    LIMIT @take;
                    """;
            AddParameter(command, "take", Math.Clamp(take, 1, 200));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new OracleSyncDeadLetterRow(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.GetInt32(8),
                    reader.GetBoolean(9),
                    reader.GetDateTime(10),
                    reader.GetDateTime(11)));
            }
        }
        finally
        {
            if (closeAfter) await connection.CloseAsync();
        }

        return rows;
    }

    public static async Task<bool> MarkDeadLetterResolvedAsync(GarmetixDbContext db, Guid id, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "OracleSyncDeadLetters"
            SET "Resolved" = true, "UpdatedAtUtc" = {DateTime.UtcNow}
            WHERE "Id" = {id};
            """, cancellationToken);
        return affected > 0;
    }

    public static async Task<bool> RetryDeadLetterAsync(GarmetixDbContext db, Guid id, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "OracleSyncDeadLetters"
            SET "RetryCount" = "RetryCount" + 1, "Resolved" = false, "UpdatedAtUtc" = {DateTime.UtcNow}
            WHERE "Id" = {id};
            """, cancellationToken);
        return affected > 0;
    }

    private static OracleSyncInboundEventRow ReadInbound(IDataRecord reader)
    {
        return new OracleSyncInboundEventRow(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetDateTime(7),
            reader.GetString(8),
            reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.GetDateTime(11),
            reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
    }

    private static void AddParameter(IDbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

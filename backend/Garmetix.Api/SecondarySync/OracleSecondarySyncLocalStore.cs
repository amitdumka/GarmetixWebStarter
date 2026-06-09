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
                "Message" text NULL,
                "Error" text NULL,
                CONSTRAINT "PK_OracleSyncRuns" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_OracleSyncRuns_StartedAtUtc" ON "OracleSyncRuns" ("StartedAtUtc");
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
            var parameter = command.CreateParameter();
            parameter.ParameterName = "key";
            parameter.Value = key;
            command.Parameters.Add(parameter);
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
            var keyParameter = command.CreateParameter();
            keyParameter.ParameterName = "key";
            keyParameter.Value = key;
            command.Parameters.Add(keyParameter);

            var valueParameter = command.CreateParameter();
            valueParameter.ParameterName = "value";
            valueParameter.Value = value is null ? DBNull.Value : value;
            command.Parameters.Add(valueParameter);

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
        string message,
        string? error,
        CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OracleSyncRuns" ("Id", "StartedAtUtc", "FinishedAtUtc", "Success", "TotalPushed", "Message", "Error")
            VALUES ({id}, {startedAtUtc.UtcDateTime}, {finishedAtUtc.UtcDateTime}, {success}, {totalPushed}, {message}, {error});
            """, cancellationToken);
    }
}

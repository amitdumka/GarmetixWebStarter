using System.Data;
using System.Text.Json;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Messages;

public sealed class ApplicationMessageLogService(GarmetixDbContext db)
{
    public const string LevelInfo = "Info";
    public const string LevelSuccess = "Success";
    public const string LevelWarning = "Warning";
    public const string LevelError = "Error";

    public async Task EnsureStorageAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "ApplicationMessageLogs" (
                "Id" uuid NOT NULL,
                "CreatedAtUtc" timestamp without time zone NOT NULL,
                "Level" text NOT NULL,
                "Source" text NOT NULL,
                "EventName" text NOT NULL,
                "Message" text NOT NULL,
                "DetailsJson" text NULL,
                "CompanyId" uuid NULL,
                "StoreGroupId" uuid NULL,
                "StoreId" uuid NULL,
                "UserId" uuid NULL,
                "UserName" text NULL,
                "Resource" text NULL,
                "OperationId" uuid NOT NULL,
                "Success" boolean NOT NULL DEFAULT true,
                CONSTRAINT "PK_ApplicationMessageLogs" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_ApplicationMessageLogs_CreatedAtUtc" ON "ApplicationMessageLogs" ("CreatedAtUtc" DESC);
            CREATE INDEX IF NOT EXISTS "IX_ApplicationMessageLogs_Source_Level" ON "ApplicationMessageLogs" ("Source", "Level");
            CREATE INDEX IF NOT EXISTS "IX_ApplicationMessageLogs_CompanyId_StoreId" ON "ApplicationMessageLogs" ("CompanyId", "StoreId");
            CREATE INDEX IF NOT EXISTS "IX_ApplicationMessageLogs_OperationId" ON "ApplicationMessageLogs" ("OperationId");
            """, cancellationToken);
    }

    public async Task<ApplicationMessageLogDto> WriteAsync(ApplicationMessageLogCreateRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);

        var operationId = request.OperationId ?? Guid.NewGuid();
        var entry = new ApplicationMessageLogDto(
            Guid.NewGuid(),
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            NormalizeLevel(request.Level, request.Success),
            NormalizeText(request.Source, "General"),
            NormalizeText(request.EventName, "Message"),
            NormalizeText(request.Message, "Message logged."),
            NormalizeDetails(request.DetailsJson),
            request.CompanyId,
            request.StoreGroupId,
            request.StoreId,
            request.UserId,
            string.IsNullOrWhiteSpace(request.UserName) ? null : request.UserName.Trim(),
            string.IsNullOrWhiteSpace(request.Resource) ? null : request.Resource.Trim(),
            operationId,
            request.Success);

        var sql = """
            INSERT INTO "ApplicationMessageLogs"
                ("Id", "CreatedAtUtc", "Level", "Source", "EventName", "Message", "DetailsJson", "CompanyId", "StoreGroupId", "StoreId", "UserId", "UserName", "Resource", "OperationId", "Success")
            VALUES
                ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14})
            """;

        var parameters = new object[]
        {
            entry.Id, entry.CreatedAtUtc, entry.Level, entry.Source, entry.EventName, entry.Message, entry.DetailsJson ?? (object)DBNull.Value,
            entry.CompanyId.HasValue ? entry.CompanyId.Value : (object)DBNull.Value,
            entry.StoreGroupId.HasValue ? entry.StoreGroupId.Value : (object)DBNull.Value,
            entry.StoreId.HasValue ? entry.StoreId.Value : (object)DBNull.Value,
            entry.UserId.HasValue ? entry.UserId.Value : (object)DBNull.Value,
            entry.UserName ?? (object)DBNull.Value,
            entry.Resource ?? (object)DBNull.Value,
            entry.OperationId, entry.Success
        };

        await db.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);

        return entry;
    }

    public Task<ApplicationMessageLogDto> SuccessAsync(string source, string eventName, string message, object? details = null,
        Guid? companyId = null, Guid? storeGroupId = null, Guid? storeId = null, Guid? userId = null, string? userName = null,
        string? resource = null, Guid? operationId = null, CancellationToken cancellationToken = default)
        => WriteAsync(new ApplicationMessageLogCreateRequest(LevelSuccess, source, eventName, message, Serialize(details), companyId, storeGroupId, storeId, userId, userName, resource, operationId, true), cancellationToken);

    public Task<ApplicationMessageLogDto> ErrorAsync(string source, string eventName, string message, object? details = null,
        Guid? companyId = null, Guid? storeGroupId = null, Guid? storeId = null, Guid? userId = null, string? userName = null,
        string? resource = null, Guid? operationId = null, CancellationToken cancellationToken = default)
        => WriteAsync(new ApplicationMessageLogCreateRequest(LevelError, source, eventName, message, Serialize(details), companyId, storeGroupId, storeId, userId, userName, resource, operationId, false), cancellationToken);

    public async Task<IReadOnlyList<ApplicationMessageLogDto>> SearchAsync(ApplicationMessageLogQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);
        var take = Math.Clamp(query.Take <= 0 ? 100 : query.Take, 1, 500);

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = BuildSearchSql(query);

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            AddParameter(command, "level", query.Level.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            AddParameter(command, "source", query.Source.Trim());
        }
        if (query.FromUtc.HasValue)
        {
            AddParameter(command, "fromUtc", DateTime.SpecifyKind(query.FromUtc.Value, DateTimeKind.Unspecified));
        }
        if (query.ToUtc.HasValue)
        {
            AddParameter(command, "toUtc", DateTime.SpecifyKind(query.ToUtc.Value, DateTimeKind.Unspecified));
        }
        if (query.CompanyId.HasValue && query.CompanyId.Value != Guid.Empty)
        {
            AddParameter(command, "companyId", query.CompanyId.Value);
        }
        if (query.StoreId.HasValue && query.StoreId.Value != Guid.Empty)
        {
            AddParameter(command, "storeId", query.StoreId.Value);
        }
        if (query.Success.HasValue)
        {
            AddParameter(command, "success", query.Success.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            AddParameter(command, "search", $"%{query.Search.Trim()}%");
        }
        AddParameter(command, "take", take);

        var rows = new List<ApplicationMessageLogDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ApplicationMessageLogDto(
                reader.GetGuid(0),
                reader.GetDateTime(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetGuid(7),
                reader.IsDBNull(8) ? null : reader.GetGuid(8),
                reader.IsDBNull(9) ? null : reader.GetGuid(9),
                reader.IsDBNull(10) ? null : reader.GetGuid(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.GetGuid(13),
                reader.GetBoolean(14)));
        }

        return rows;
    }

    private static string BuildSearchSql(ApplicationMessageLogQuery query)
    {
        var clauses = new List<string> { "1 = 1" };

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            clauses.Add("\"Level\" = @level");
        }
        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            clauses.Add("\"Source\" = @source");
        }
        if (query.FromUtc.HasValue)
        {
            clauses.Add("\"CreatedAtUtc\" >= @fromUtc");
        }
        if (query.ToUtc.HasValue)
        {
            clauses.Add("\"CreatedAtUtc\" <= @toUtc");
        }
        if (query.CompanyId.HasValue && query.CompanyId.Value != Guid.Empty)
        {
            clauses.Add("\"CompanyId\" = @companyId");
        }
        if (query.StoreId.HasValue && query.StoreId.Value != Guid.Empty)
        {
            clauses.Add("\"StoreId\" = @storeId");
        }
        if (query.Success.HasValue)
        {
            clauses.Add("\"Success\" = @success");
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            clauses.Add("(\"Message\" ILIKE @search OR \"EventName\" ILIKE @search OR \"Source\" ILIKE @search OR COALESCE(\"DetailsJson\", '') ILIKE @search)");
        }

        return $"""
            SELECT "Id", "CreatedAtUtc", "Level", "Source", "EventName", "Message", "DetailsJson", "CompanyId", "StoreGroupId", "StoreId", "UserId", "UserName", "Resource", "OperationId", "Success"
            FROM "ApplicationMessageLogs"
            WHERE {string.Join(" AND ", clauses)}
            ORDER BY "CreatedAtUtc" DESC
            LIMIT @take
            """;
    }

    public async Task<ApplicationMessageLogOptionsDto> OptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);
        var rows = await SearchAsync(new ApplicationMessageLogQuery(null, null, null, null, null, null, null, null, 500), cancellationToken);
        return new ApplicationMessageLogOptionsDto(
            [LevelInfo, LevelSuccess, LevelWarning, LevelError],
            rows.Select(item => item.Source).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList(),
            rows.Select(item => item.EventName).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList());
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string NormalizeLevel(string level, bool success)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return success ? LevelSuccess : LevelError;
        }

        var normalized = level.Trim();
        return normalized.Equals(LevelSuccess, StringComparison.OrdinalIgnoreCase) ? LevelSuccess
            : normalized.Equals(LevelError, StringComparison.OrdinalIgnoreCase) ? LevelError
            : normalized.Equals(LevelWarning, StringComparison.OrdinalIgnoreCase) ? LevelWarning
            : LevelInfo;
    }

    private static string NormalizeText(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeDetails(string? details)
        => string.IsNullOrWhiteSpace(details) ? null : details.Trim();

    private static string? Serialize(object? details)
    {
        if (details is null)
        {
            return null;
        }

        if (details is string text)
        {
            return text;
        }

        return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
    }
}

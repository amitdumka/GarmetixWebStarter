using System.Data;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Assistant;

/// <summary>
/// Persists Garmetix Assistant conversations and messages.
/// Follows the same self-provisioning raw-SQL pattern as
/// Garmetix.Api.Messages.ApplicationMessageLogService, so no EF Core
/// migration is required - the tables are created on first use.
/// </summary>
public sealed class AssistantConversationStore(GarmetixDbContext db)
{
    private const int MaxContentLength = 8000;

    public async Task EnsureStorageAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "AssistantConversations" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "AppId" text NULL,
                "CompanyId" uuid NULL,
                "StoreGroupId" uuid NULL,
                "StoreId" uuid NULL,
                "CreatedAtUtc" timestamp without time zone NOT NULL,
                "LastMessageAtUtc" timestamp without time zone NOT NULL,
                CONSTRAINT "PK_AssistantConversations" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_AssistantConversations_UserId" ON "AssistantConversations" ("UserId", "LastMessageAtUtc" DESC);

            CREATE TABLE IF NOT EXISTS "AssistantMessages" (
                "Id" uuid NOT NULL,
                "ConversationId" uuid NOT NULL,
                "Role" text NOT NULL,
                "Content" text NOT NULL,
                "ToolCallsJson" text NULL,
                "CreatedAtUtc" timestamp without time zone NOT NULL,
                CONSTRAINT "PK_AssistantMessages" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_AssistantMessages_ConversationId" ON "AssistantMessages" ("ConversationId", "CreatedAtUtc");
            """, cancellationToken);
    }

    /// <summary>
    /// Returns an existing conversation id owned by the user, or creates a new one.
    /// If the caller passes a conversationId that does not belong to this user
    /// (or does not exist), a fresh conversation is created instead of leaking
    /// another user's thread.
    /// </summary>
    public async Task<Guid> GetOrCreateConversationAsync(
        Guid? conversationId,
        Guid userId,
        string? appId,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        if (conversationId.HasValue)
        {
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = """SELECT "Id" FROM "AssistantConversations" WHERE "Id" = @id AND "UserId" = @userId""";
            AddParameter(checkCommand, "id", DbType.Guid, conversationId.Value);
            AddParameter(checkCommand, "userId", DbType.Guid, userId);
            var existing = await checkCommand.ExecuteScalarAsync(cancellationToken);
            if (existing is Guid existingId)
            {
                return existingId;
            }
        }

        var newId = Guid.NewGuid();
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO "AssistantConversations"
                ("Id", "UserId", "AppId", "CompanyId", "StoreGroupId", "StoreId", "CreatedAtUtc", "LastMessageAtUtc")
            VALUES
                (@id, @userId, @appId, @companyId, @storeGroupId, @storeId, @createdAtUtc, @lastMessageAtUtc)
            """;
        AddParameter(insertCommand, "id", DbType.Guid, newId);
        AddParameter(insertCommand, "userId", DbType.Guid, userId);
        AddParameter(insertCommand, "appId", DbType.String, appId);
        AddParameter(insertCommand, "companyId", DbType.Guid, companyId);
        AddParameter(insertCommand, "storeGroupId", DbType.Guid, storeGroupId);
        AddParameter(insertCommand, "storeId", DbType.Guid, storeId);
        AddParameter(insertCommand, "createdAtUtc", DbType.DateTime2, now);
        AddParameter(insertCommand, "lastMessageAtUtc", DbType.DateTime2, now);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        return newId;
    }

    public async Task AppendMessageAsync(
        Guid conversationId,
        string role,
        string content,
        string? toolCallsJson,
        CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var trimmedContent = content.Length > MaxContentLength ? content[..MaxContentLength] : content;

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO "AssistantMessages" ("Id", "ConversationId", "Role", "Content", "ToolCallsJson", "CreatedAtUtc")
            VALUES (@id, @conversationId, @role, @content, @toolCallsJson, @createdAtUtc)
            """;
        AddParameter(insertCommand, "id", DbType.Guid, Guid.NewGuid());
        AddParameter(insertCommand, "conversationId", DbType.Guid, conversationId);
        AddParameter(insertCommand, "role", DbType.String, role);
        AddParameter(insertCommand, "content", DbType.String, trimmedContent);
        AddParameter(insertCommand, "toolCallsJson", DbType.String, toolCallsJson);
        AddParameter(insertCommand, "createdAtUtc", DbType.DateTime2, now);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        await using var touchCommand = connection.CreateCommand();
        touchCommand.CommandText = """UPDATE "AssistantConversations" SET "LastMessageAtUtc" = @now WHERE "Id" = @id""";
        AddParameter(touchCommand, "now", DbType.DateTime2, now);
        AddParameter(touchCommand, "id", DbType.Guid, conversationId);
        await touchCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Oldest-first list of prior turns for replay back into the model as context.
    /// Only returns messages for conversations owned by the given user.
    /// </summary>
    public async Task<IReadOnlyList<AssistantMessageDto>> GetRecentMessagesAsync(
        Guid conversationId,
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m."Id", m."Role", m."Content", m."CreatedAtUtc"
            FROM "AssistantMessages" m
            INNER JOIN "AssistantConversations" c ON c."Id" = m."ConversationId"
            WHERE m."ConversationId" = @conversationId AND c."UserId" = @userId
            ORDER BY m."CreatedAtUtc" DESC
            LIMIT @take
            """;
        AddParameter(command, "conversationId", DbType.Guid, conversationId);
        AddParameter(command, "userId", DbType.Guid, userId);
        AddParameter(command, "take", DbType.Int32, Math.Clamp(take, 1, 200));

        var rows = new List<AssistantMessageDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AssistantMessageDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3)));
        }

        rows.Reverse();
        return rows;
    }

    public async Task<IReadOnlyList<AssistantConversationSummaryDto>> ListConversationsAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c."Id", c."AppId", c."CreatedAtUtc", c."LastMessageAtUtc",
                (SELECT m."Content" FROM "AssistantMessages" m
                 WHERE m."ConversationId" = c."Id"
                 ORDER BY m."CreatedAtUtc" DESC LIMIT 1) AS "LastMessagePreview"
            FROM "AssistantConversations" c
            WHERE c."UserId" = @userId
            ORDER BY c."LastMessageAtUtc" DESC
            LIMIT @take
            """;
        AddParameter(command, "userId", DbType.Guid, userId);
        AddParameter(command, "take", DbType.Int32, Math.Clamp(take, 1, 100));

        var rows = new List<AssistantConversationSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new AssistantConversationSummaryDto(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetDateTime(3),
                reader.IsDBNull(4) ? null : Truncate(reader.GetString(4), 140)));
        }

        return rows;
    }

    private async Task<System.Data.Common.DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max] + "...";

    private static void AddParameter(System.Data.Common.DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

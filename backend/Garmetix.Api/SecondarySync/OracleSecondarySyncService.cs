using Garmetix.Core.Interfaces;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Authentication;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.GstReturns;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Garmetix.Api.SecondarySync;

public sealed class OracleSecondarySyncService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<OracleSecondarySyncOptions> options,
    ILogger<OracleSecondarySyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly Dictionary<string, Type> SupportedEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(Company)] = typeof(Company),
        [nameof(StoreGroup)] = typeof(StoreGroup),
        [nameof(Store)] = typeof(Store),
        [nameof(Customer)] = typeof(Customer),
        [nameof(Vendor)] = typeof(Vendor),
        [nameof(Product)] = typeof(Product),
        [nameof(Stock)] = typeof(Stock),
        [nameof(Invoice)] = typeof(Invoice),
        [nameof(PurchaseInvoice)] = typeof(PurchaseInvoice),
        [nameof(Voucher)] = typeof(Voucher),
        [nameof(CashVoucher)] = typeof(CashVoucher),
        [nameof(CommercialNote)] = typeof(CommercialNote),
        [nameof(CustomerAdvanceReceipt)] = typeof(CustomerAdvanceReceipt),
        [nameof(LoyaltyProgram)] = typeof(LoyaltyProgram),
        [nameof(LoyaltyPointLedger)] = typeof(LoyaltyPointLedger),
        [nameof(GstReturnDraft)] = typeof(GstReturnDraft),
        [nameof(JournalEntry)] = typeof(JournalEntry),
        [nameof(JournalLine)] = typeof(JournalLine),
        [nameof(Transaction)] = typeof(Transaction),
        [nameof(PettyCashSheet)] = typeof(PettyCashSheet),
        [nameof(Employee)] = typeof(Employee),
        [nameof(SalaryPaySlip)] = typeof(SalaryPaySlip)
    };

    private volatile bool isRunning;
    private OracleSecondarySyncRunResult? lastRun;

    public IReadOnlyCollection<string> SupportedEntityNames => SupportedEntities.Keys.OrderBy(item => item).ToArray();

    public OracleSecondarySyncStatusDto GetStatus()
    {
        var current = options.CurrentValue;
        var hasOracleConnection = !string.IsNullOrWhiteSpace(current.ConnectionString);
        var wallet = !string.IsNullOrWhiteSpace(current.WalletDirectory) || !string.IsNullOrWhiteSpace(current.TnsAdmin);
        return new OracleSecondarySyncStatusDto(
            current.Enabled,
            current.Enabled && hasOracleConnection,
            hasOracleConnection,
            current.Direction,
            current.ConflictPolicy,
            current.TenantId,
            current.SourceApplication,
            current.IntervalSeconds,
            current.BatchSize,
            current.Schema,
            wallet,
            current.PullExternalEvents,
            current.ApplyInboundAutomatically,
            GetConfiguredEntityNames(current).ToArray(),
            lastRun?.FinishedAtUtc.ToString("O"),
            lastRun?.Success == true ? lastRun.FinishedAtUtc.ToString("O") : null,
            lastRun?.Error,
            isRunning,
            "PostgreSQL is primary. Oracle Cloud is a secondary shared hub. Push sync is active; inbound Oracle events are pulled into a local review/dead-letter queue before any destructive merge.");
    }

    public async Task<OracleConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var current = options.CurrentValue;
        if (string.IsNullOrWhiteSpace(current.ConnectionString))
        {
            return new OracleConnectionTestResult(false, "Oracle connection string is not configured.");
        }

        try
        {
            await using var connection = CreateConnection(current);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYS_EXTRACT_UTC(SYSTIMESTAMP) FROM DUAL";
            command.CommandTimeout = current.CommandTimeoutSeconds;
            var serverTime = await command.ExecuteScalarAsync(cancellationToken);
            return new OracleConnectionTestResult(true, "Oracle connection succeeded.", Convert.ToString(serverTime));
        }
        catch (Exception ex) when (ex is OracleException or InvalidOperationException or TimeoutException)
        {
            return new OracleConnectionTestResult(false, "Oracle connection failed.", Error: ex.Message);
        }
    }

    public async Task RepairAsync(CancellationToken cancellationToken = default)
    {
        var current = options.CurrentValue;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        await OracleSecondarySyncLocalStore.RepairAsync(db, cancellationToken);

        if (!string.IsNullOrWhiteSpace(current.ConnectionString) && current.CreateOracleSchema)
        {
            await using var connection = CreateConnection(current);
            await connection.OpenAsync(cancellationToken);
            await EnsureOracleSchemaAsync(connection, current, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<OracleSyncHistoryRow>> GetHistoryAsync(int take = 25, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        return await OracleSecondarySyncLocalStore.GetRunsAsync(db, take, cancellationToken);
    }

    public async Task<IReadOnlyList<OracleSyncInboundEventRow>> GetInboundEventsAsync(int take = 50, string? status = null, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        return await OracleSecondarySyncLocalStore.GetInboundEventsAsync(db, take, status, cancellationToken);
    }

    public async Task<IReadOnlyList<OracleSyncDeadLetterRow>> GetDeadLettersAsync(int take = 50, bool includeResolved = false, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        return await OracleSecondarySyncLocalStore.GetDeadLettersAsync(db, take, includeResolved, cancellationToken);
    }

    public async Task<OracleDeadLetterActionResult> RetryDeadLetterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        var changed = await OracleSecondarySyncLocalStore.RetryDeadLetterAsync(db, id, cancellationToken);
        return new OracleDeadLetterActionResult(changed, changed ? "Dead-letter marked for retry/review." : "Dead-letter was not found.");
    }

    public async Task<OracleDeadLetterActionResult> ResolveDeadLetterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        var changed = await OracleSecondarySyncLocalStore.MarkDeadLetterResolvedAsync(db, id, cancellationToken);
        return new OracleDeadLetterActionResult(changed, changed ? "Dead-letter marked resolved." : "Dead-letter was not found.");
    }

    public async Task<OracleSecondarySyncRunResult> RunOnceAsync(
        string? entityName = null,
        bool repairFirst = true,
        string? directionOverride = null,
        CancellationToken cancellationToken = default)
    {
        var current = options.CurrentValue;
        var direction = string.IsNullOrWhiteSpace(directionOverride) ? current.Direction : directionOverride.Trim();
        var startedAt = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        if (isRunning)
        {
            return new OracleSecondarySyncRunResult(false, "Oracle sync is already running.", startedAt, DateTimeOffset.UtcNow, 0, 0, [], "Already running");
        }

        if (string.IsNullOrWhiteSpace(current.ConnectionString))
        {
            var result = new OracleSecondarySyncRunResult(false, "Oracle sync is not configured.", startedAt, DateTimeOffset.UtcNow, 0, 0, [], "Missing Oracle connection string");
            lastRun = result;
            return result;
        }

        if (!IsSupportedDirection(direction))
        {
            var result = new OracleSecondarySyncRunResult(false, $"Unsupported Oracle sync direction '{direction}'.", startedAt, DateTimeOffset.UtcNow, 0, 0, [], "Unsupported direction");
            lastRun = result;
            return result;
        }

        isRunning = true;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
            await OracleSecondarySyncLocalStore.RepairAsync(db, cancellationToken);

            await using var oracleConnection = CreateConnection(current);
            await oracleConnection.OpenAsync(cancellationToken);
            if (repairFirst || current.CreateOracleSchema)
            {
                await EnsureOracleSchemaAsync(oracleConnection, current, cancellationToken);
            }

            var results = new List<OracleEntitySyncResult>();
            if (ShouldPush(direction))
            {
                var entityTypes = ResolveEntityTypes(current, entityName);
                foreach (var entityType in entityTypes)
                {
                    var method = typeof(OracleSecondarySyncService)
                        .GetMethod(nameof(PushEntityAsync), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?? throw new MissingMethodException(nameof(PushEntityAsync));
                    var generic = method.MakeGenericMethod(entityType);
                    var task = (Task<OracleEntitySyncResult>)generic.Invoke(this, [db, oracleConnection, current, cancellationToken])!;
                    results.Add(await task);
                }
            }

            if (ShouldPull(direction) && current.PullExternalEvents)
            {
                var entityNames = string.IsNullOrWhiteSpace(entityName)
                    ? GetConfiguredEntityNames(current).ToArray()
                    : [entityName.Trim()];
                foreach (var item in entityNames)
                {
                    results.Add(await PullEntityEventsAsync(db, oracleConnection, current, item, cancellationToken));
                }
            }

            var totalPushed = results.Sum(item => item.Pushed);
            var totalPulled = results.Sum(item => item.Pulled);
            var finishedAt = DateTimeOffset.UtcNow;
            var success = results.All(item => string.IsNullOrWhiteSpace(item.Error));
            var message = success
                ? $"Oracle sync completed. {totalPushed} pushed, {totalPulled} pulled."
                : "Oracle sync completed with one or more entity errors.";
            var error = success ? null : string.Join(" | ", results.Where(item => !string.IsNullOrWhiteSpace(item.Error)).Select(item => $"{item.EntityName}: {item.Error}"));
            var result = new OracleSecondarySyncRunResult(success, message, startedAt, finishedAt, totalPushed, totalPulled, results, error);
            lastRun = result;

            await OracleSecondarySyncLocalStore.SetStateAsync(db, "oracle:lastRunUtc", finishedAt.ToString("O"), cancellationToken);
            if (success)
            {
                await OracleSecondarySyncLocalStore.SetStateAsync(db, "oracle:lastSuccessUtc", finishedAt.ToString("O"), cancellationToken);
            }
            await OracleSecondarySyncLocalStore.SetStateAsync(db, "oracle:lastError", error, cancellationToken);
            await OracleSecondarySyncLocalStore.AddRunAsync(db, runId, startedAt, finishedAt, success, totalPushed, totalPulled, message, error, cancellationToken);

            return result;
        }
        catch (Exception ex) when (ex is OracleException or InvalidOperationException or DbUpdateException or TimeoutException)
        {
            var finishedAt = DateTimeOffset.UtcNow;
            var result = new OracleSecondarySyncRunResult(false, "Oracle secondary sync failed.", startedAt, finishedAt, 0, 0, [], ex.Message);
            lastRun = result;
            logger.LogError(ex, "Oracle secondary sync failed.");
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
                await OracleSecondarySyncLocalStore.SetStateAsync(db, "oracle:lastRunUtc", finishedAt.ToString("O"), cancellationToken);
                await OracleSecondarySyncLocalStore.SetStateAsync(db, "oracle:lastError", ex.Message, cancellationToken);
                await OracleSecondarySyncLocalStore.AddRunAsync(db, runId, startedAt, finishedAt, false, 0, 0, result.Message, ex.Message, cancellationToken);
            }
            catch (Exception stateEx)
            {
                logger.LogWarning(stateEx, "Could not record failed Oracle sync state.");
            }

            return result;
        }
        finally
        {
            isRunning = false;
        }
    }

    private async Task<OracleEntitySyncResult> PushEntityAsync<T>(
        GarmetixDbContext db,
        OracleConnection oracleConnection,
        OracleSecondarySyncOptions current,
        CancellationToken cancellationToken) where T : class, IEntity
    {
        var entityName = typeof(T).Name;
        try
        {
            var checkpointKey = $"oracle:checkpoint:{entityName}:updatedUtc";
            var checkpointValue = await OracleSecondarySyncLocalStore.GetStateAsync(db, checkpointKey, cancellationToken);
            var checkpointUtc = ParseDateTimeOffset(checkpointValue) ?? DateTimeOffset.MinValue;

            IQueryable<T> query = db.Set<T>().AsNoTracking();
            if (checkpointUtc > DateTimeOffset.MinValue)
            {
                query = query.Where(BuildChangedSinceExpression<T>(checkpointUtc.UtcDateTime));
            }

            query = query.OrderBy(BuildVersionExpression<T>()).Take(Math.Clamp(current.BatchSize, 1, 5000));
            var rows = await query.ToListAsync(cancellationToken);
            var pushed = 0;
            DateTimeOffset? maxVersion = null;

            foreach (var row in rows)
            {
                var versionUtc = GetVersionUtc(row);
                var deleted = GetBooleanProperty(row, "Deleted");
                if (deleted && !current.PushDeletedRows)
                {
                    continue;
                }

                var operation = deleted ? "Delete" : "Upsert";
                var payload = new
                {
                    tenantId = current.TenantId,
                    sourceApplication = current.SourceApplication,
                    entityName,
                    entityId = row.Id,
                    operation,
                    versionUtc,
                    payload = row
                };
                var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
                var payloadHash = Hash(payloadJson);
                var eventId = DeterministicGuid($"{current.TenantId}|{current.SourceApplication}|{entityName}|{row.Id}|{versionUtc:O}|{operation}|{payloadHash}");

                await UpsertOracleEventAsync(
                    oracleConnection,
                    current,
                    eventId,
                    entityName,
                    row.Id,
                    operation,
                    versionUtc,
                    payloadJson,
                    payloadHash,
                    cancellationToken);
                pushed++;
                maxVersion = maxVersion is null || versionUtc > maxVersion.Value ? versionUtc : maxVersion;
            }

            if (maxVersion is not null)
            {
                var nextCheckpoint = maxVersion.Value.AddSeconds(-1);
                await OracleSecondarySyncLocalStore.SetStateAsync(db, checkpointKey, nextCheckpoint.ToString("O"), cancellationToken);
            }

            return new OracleEntitySyncResult(entityName, rows.Count, pushed, 0, maxVersion?.ToString("O"));
        }
        catch (Exception ex) when (ex is OracleException or InvalidOperationException or DbUpdateException)
        {
            logger.LogWarning(ex, "Oracle push sync failed for entity {EntityName}.", entityName);
            await OracleSecondarySyncLocalStore.AddDeadLetterAsync(db, "PushToOracle", null, current.SourceApplication, entityName, string.Empty, "Push failed", null, ex.Message, cancellationToken);
            return new OracleEntitySyncResult(entityName, 0, 0, 0, null, ex.Message);
        }
    }

    private async Task<OracleEntitySyncResult> PullEntityEventsAsync(
        GarmetixDbContext db,
        OracleConnection oracleConnection,
        OracleSecondarySyncOptions current,
        string entityName,
        CancellationToken cancellationToken)
    {
        try
        {
            var checkpointKey = $"oracle:pull-checkpoint:{entityName}:createdUtc";
            var checkpointValue = await OracleSecondarySyncLocalStore.GetStateAsync(db, checkpointKey, cancellationToken);
            var checkpointUtc = ParseDateTimeOffset(checkpointValue) ?? DateTimeOffset.MinValue;
            var batchSize = Math.Clamp(current.BatchSize, 1, 5000);
            var prefix = BuildOracleSchemaPrefix(current.Schema);

            await using var command = oracleConnection.CreateCommand();
            command.BindByName = true;
            command.CommandTimeout = current.CommandTimeoutSeconds;
            command.CommandText = $"""
                SELECT EVENT_ID, TENANT_ID, SOURCE_APPLICATION, ENTITY_NAME, ENTITY_ID, OPERATION, VERSION_UTC, PAYLOAD_HASH, PAYLOAD_JSON, CREATED_UTC
                FROM {prefix}GARMETIX_SYNC_EVENTS
                WHERE TENANT_ID = :P_TENANT_ID
                  AND SOURCE_APPLICATION <> :P_SOURCE_APPLICATION
                  AND ENTITY_NAME = :P_ENTITY_NAME
                  AND CREATED_UTC > :P_CREATED_AFTER
                ORDER BY CREATED_UTC
                FETCH FIRST {batchSize} ROWS ONLY
                """;
            command.Parameters.Add(new OracleParameter("P_TENANT_ID", current.TenantId));
            command.Parameters.Add(new OracleParameter("P_SOURCE_APPLICATION", current.SourceApplication));
            command.Parameters.Add(new OracleParameter("P_ENTITY_NAME", entityName));
            command.Parameters.Add(new OracleParameter("P_CREATED_AFTER", checkpointUtc.UtcDateTime));

            var pulled = 0;
            var scanned = 0;
            DateTimeOffset? maxCreated = null;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                scanned++;
                var oracleEventId = Convert.ToString(reader["EVENT_ID"]) ?? string.Empty;
                var tenantId = Convert.ToString(reader["TENANT_ID"]) ?? current.TenantId;
                var sourceApplication = Convert.ToString(reader["SOURCE_APPLICATION"]) ?? "ExternalApp";
                var rowEntityName = Convert.ToString(reader["ENTITY_NAME"]) ?? entityName;
                var entityId = Convert.ToString(reader["ENTITY_ID"]) ?? string.Empty;
                var operation = Convert.ToString(reader["OPERATION"]) ?? "Upsert";
                var versionUtc = ToUtcDateTime(reader["VERSION_UTC"]);
                var payloadHash = Convert.ToString(reader["PAYLOAD_HASH"]) ?? string.Empty;
                var payloadJson = Convert.ToString(reader["PAYLOAD_JSON"]) ?? string.Empty;
                var createdUtc = ToUtcDateTime(reader["CREATED_UTC"]);

                if (!SupportedEntities.ContainsKey(rowEntityName))
                {
                    await OracleSecondarySyncLocalStore.AddDeadLetterAsync(db, "PullFromOracle", oracleEventId, sourceApplication, rowEntityName, entityId, "Unsupported entity", payloadJson, "Entity is not configured in Garmetix sync map.", cancellationToken);
                    await UpdateOracleApplyStatusAsync(oracleConnection, current, oracleEventId, "DeadLetter", "Unsupported entity in receiving Garmetix app.", cancellationToken);
                    continue;
                }

                var (status, note) = BuildInboundDecision(current, rowEntityName, operation);
                var localRow = new OracleSyncInboundEventRow(
                    Guid.NewGuid(),
                    oracleEventId,
                    tenantId,
                    sourceApplication,
                    rowEntityName,
                    entityId,
                    operation,
                    versionUtc,
                    current.ConflictPolicy,
                    status,
                    note,
                    DateTime.UtcNow,
                    null,
                    null);

                await OracleSecondarySyncLocalStore.AddInboundEventAsync(db, localRow, payloadHash, payloadJson, cancellationToken);
                await UpdateOracleApplyStatusAsync(oracleConnection, current, oracleEventId, status, note, cancellationToken);
                pulled++;
                var createdOffset = new DateTimeOffset(createdUtc, TimeSpan.Zero);
                maxCreated = maxCreated is null || createdOffset > maxCreated.Value ? createdOffset : maxCreated;
            }

            if (maxCreated is not null)
            {
                await OracleSecondarySyncLocalStore.SetStateAsync(db, checkpointKey, maxCreated.Value.AddSeconds(-1).ToString("O"), cancellationToken);
            }

            return new OracleEntitySyncResult(entityName, scanned, 0, pulled, maxCreated?.ToString("O"));
        }
        catch (Exception ex) when (ex is OracleException or InvalidOperationException or DbUpdateException)
        {
            logger.LogWarning(ex, "Oracle pull sync failed for entity {EntityName}.", entityName);
            await OracleSecondarySyncLocalStore.AddDeadLetterAsync(db, "PullFromOracle", null, "OracleHub", entityName, string.Empty, "Pull failed", null, ex.Message, cancellationToken);
            return new OracleEntitySyncResult(entityName, 0, 0, 0, null, ex.Message);
        }
    }

    private static OracleConnection CreateConnection(OracleSecondarySyncOptions current)
    {
        var tnsAdmin = !string.IsNullOrWhiteSpace(current.TnsAdmin) ? current.TnsAdmin : current.WalletDirectory;
        if (!string.IsNullOrWhiteSpace(tnsAdmin))
        {
            Environment.SetEnvironmentVariable("TNS_ADMIN", tnsAdmin);
        }

        return new OracleConnection(current.ConnectionString);
    }

    private static async Task EnsureOracleSchemaAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        CancellationToken cancellationToken)
    {
        var prefix = BuildOracleSchemaPrefix(current.Schema);
        await CreateOracleTableIfMissingAsync(connection, current, "GARMETIX_SYNC_EVENTS", $"""
            CREATE TABLE {prefix}GARMETIX_SYNC_EVENTS (
                EVENT_ID VARCHAR2(36) NOT NULL,
                TENANT_ID VARCHAR2(100) NOT NULL,
                SOURCE_APPLICATION VARCHAR2(100) NOT NULL,
                ENTITY_NAME VARCHAR2(100) NOT NULL,
                ENTITY_ID VARCHAR2(36) NOT NULL,
                OPERATION VARCHAR2(30) NOT NULL,
                VERSION_UTC TIMESTAMP NOT NULL,
                PAYLOAD_HASH VARCHAR2(64) NOT NULL,
                PAYLOAD_JSON CLOB NOT NULL,
                CREATED_UTC TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
                APPLIED_UTC TIMESTAMP NULL,
                APPLY_STATUS VARCHAR2(30) DEFAULT 'Pending' NOT NULL,
                APPLY_ERROR CLOB NULL,
                CONSTRAINT PK_GARMETIX_SYNC_EVENTS PRIMARY KEY (EVENT_ID)
            )
            """, cancellationToken);

        await CreateOracleTableIfMissingAsync(connection, current, "GARMETIX_SYNC_STATE", $"""
            CREATE TABLE {prefix}GARMETIX_SYNC_STATE (
                STATE_KEY VARCHAR2(200) NOT NULL,
                STATE_VALUE CLOB NULL,
                UPDATED_UTC TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
                CONSTRAINT PK_GARMETIX_SYNC_STATE PRIMARY KEY (STATE_KEY)
            )
            """, cancellationToken);

        await CreateOracleTableIfMissingAsync(connection, current, "GARMETIX_SYNC_DEAD_LETTERS", $"""
            CREATE TABLE {prefix}GARMETIX_SYNC_DEAD_LETTERS (
                ID VARCHAR2(36) NOT NULL,
                EVENT_ID VARCHAR2(36) NULL,
                TENANT_ID VARCHAR2(100) NOT NULL,
                SOURCE_APPLICATION VARCHAR2(100) NOT NULL,
                ENTITY_NAME VARCHAR2(100) NOT NULL,
                ENTITY_ID VARCHAR2(36) NULL,
                REASON VARCHAR2(400) NOT NULL,
                ERROR CLOB NULL,
                PAYLOAD_JSON CLOB NULL,
                CREATED_UTC TIMESTAMP DEFAULT SYS_EXTRACT_UTC(SYSTIMESTAMP) NOT NULL,
                RESOLVED_UTC TIMESTAMP NULL,
                CONSTRAINT PK_GARMETIX_SYNC_DEAD PRIMARY KEY (ID)
            )
            """, cancellationToken);

        await CreateOracleIndexIfMissingAsync(connection, current, "IX_GARMETIX_SYNC_EVENTS_01", $"CREATE INDEX {prefix}IX_GARMETIX_SYNC_EVENTS_01 ON {prefix}GARMETIX_SYNC_EVENTS (TENANT_ID, ENTITY_NAME, VERSION_UTC)", cancellationToken);
        await CreateOracleIndexIfMissingAsync(connection, current, "IX_GARMETIX_SYNC_EVENTS_02", $"CREATE INDEX {prefix}IX_GARMETIX_SYNC_EVENTS_02 ON {prefix}GARMETIX_SYNC_EVENTS (TENANT_ID, APPLY_STATUS, CREATED_UTC)", cancellationToken);
        await CreateOracleIndexIfMissingAsync(connection, current, "IX_GARMETIX_SYNC_EVENTS_03", $"CREATE INDEX {prefix}IX_GARMETIX_SYNC_EVENTS_03 ON {prefix}GARMETIX_SYNC_EVENTS (TENANT_ID, SOURCE_APPLICATION, CREATED_UTC)", cancellationToken);
    }

    private static async Task CreateOracleTableIfMissingAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        string tableName,
        string createSql,
        CancellationToken cancellationToken)
    {
        if (await OracleObjectExistsAsync(connection, current, "TABLE", tableName, cancellationToken))
        {
            return;
        }

        await ExecuteOracleNonQueryAsync(connection, current, createSql, cancellationToken);
    }

    private static async Task CreateOracleIndexIfMissingAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        string indexName,
        string createSql,
        CancellationToken cancellationToken)
    {
        if (await OracleObjectExistsAsync(connection, current, "INDEX", indexName, cancellationToken))
        {
            return;
        }

        await ExecuteOracleNonQueryAsync(connection, current, createSql, cancellationToken);
    }

    private static async Task<bool> OracleObjectExistsAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        string objectType,
        string objectName,
        CancellationToken cancellationToken)
    {
        var schema = NormalizeOracleIdentifier(current.Schema);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = current.CommandTimeoutSeconds;
        command.CommandText = string.IsNullOrWhiteSpace(schema)
            ? "SELECT COUNT(*) FROM USER_OBJECTS WHERE OBJECT_TYPE = :P_OBJECT_TYPE AND OBJECT_NAME = :P_OBJECT_NAME"
            : "SELECT COUNT(*) FROM ALL_OBJECTS WHERE OWNER = :P_OWNER AND OBJECT_TYPE = :P_OBJECT_TYPE AND OBJECT_NAME = :P_OBJECT_NAME";
        if (!string.IsNullOrWhiteSpace(schema))
        {
            command.Parameters.Add(new OracleParameter("P_OWNER", schema));
        }
        command.Parameters.Add(new OracleParameter("P_OBJECT_TYPE", objectType.ToUpperInvariant()));
        command.Parameters.Add(new OracleParameter("P_OBJECT_NAME", objectName.ToUpperInvariant()));
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        return count > 0;
    }

    private static async Task ExecuteOracleNonQueryAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = current.CommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertOracleEventAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        Guid eventId,
        string entityName,
        Guid entityId,
        string operation,
        DateTimeOffset versionUtc,
        string payloadJson,
        string payloadHash,
        CancellationToken cancellationToken)
    {
        var prefix = BuildOracleSchemaPrefix(current.Schema);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = current.CommandTimeoutSeconds;
        command.CommandText = $"""
            MERGE INTO {prefix}GARMETIX_SYNC_EVENTS target
            USING (SELECT :P_EVENT_ID AS EVENT_ID FROM DUAL) source
            ON (target.EVENT_ID = source.EVENT_ID)
            WHEN MATCHED THEN UPDATE SET
                PAYLOAD_JSON = :P_PAYLOAD_JSON,
                PAYLOAD_HASH = :P_PAYLOAD_HASH,
                VERSION_UTC = :P_VERSION_UTC,
                OPERATION = :P_OPERATION
            WHEN NOT MATCHED THEN INSERT (
                EVENT_ID,
                TENANT_ID,
                SOURCE_APPLICATION,
                ENTITY_NAME,
                ENTITY_ID,
                OPERATION,
                VERSION_UTC,
                PAYLOAD_HASH,
                PAYLOAD_JSON,
                CREATED_UTC,
                APPLY_STATUS
            ) VALUES (
                :P_EVENT_ID,
                :P_TENANT_ID,
                :P_SOURCE_APPLICATION,
                :P_ENTITY_NAME,
                :P_ENTITY_ID,
                :P_OPERATION,
                :P_VERSION_UTC,
                :P_PAYLOAD_HASH,
                :P_PAYLOAD_JSON,
                SYS_EXTRACT_UTC(SYSTIMESTAMP),
                'Pending'
            )
            """;

        command.Parameters.Add(new OracleParameter("P_EVENT_ID", eventId.ToString("D")));
        command.Parameters.Add(new OracleParameter("P_TENANT_ID", current.TenantId));
        command.Parameters.Add(new OracleParameter("P_SOURCE_APPLICATION", current.SourceApplication));
        command.Parameters.Add(new OracleParameter("P_ENTITY_NAME", entityName));
        command.Parameters.Add(new OracleParameter("P_ENTITY_ID", entityId.ToString("D")));
        command.Parameters.Add(new OracleParameter("P_OPERATION", operation));
        command.Parameters.Add(new OracleParameter("P_VERSION_UTC", versionUtc.UtcDateTime));
        command.Parameters.Add(new OracleParameter("P_PAYLOAD_HASH", payloadHash));
        command.Parameters.Add(new OracleParameter("P_PAYLOAD_JSON", OracleDbType.Clob) { Value = payloadJson });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateOracleApplyStatusAsync(
        OracleConnection connection,
        OracleSecondarySyncOptions current,
        string oracleEventId,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oracleEventId)) return;
        var prefix = BuildOracleSchemaPrefix(current.Schema);
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = current.CommandTimeoutSeconds;
        command.CommandText = $"""
            UPDATE {prefix}GARMETIX_SYNC_EVENTS
            SET APPLY_STATUS = :P_STATUS,
                APPLY_ERROR = :P_ERROR,
                APPLIED_UTC = SYS_EXTRACT_UTC(SYSTIMESTAMP)
            WHERE EVENT_ID = :P_EVENT_ID
            """;
        command.Parameters.Add(new OracleParameter("P_STATUS", status));
        command.Parameters.Add(new OracleParameter("P_ERROR", OracleDbType.Clob) { Value = (object?)error ?? DBNull.Value });
        command.Parameters.Add(new OracleParameter("P_EVENT_ID", oracleEventId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyList<Type> ResolveEntityTypes(OracleSecondarySyncOptions current, string? entityName)
    {
        if (!string.IsNullOrWhiteSpace(entityName))
        {
            return SupportedEntities.TryGetValue(entityName.Trim(), out var type)
                ? [type]
                : throw new InvalidOperationException($"Entity '{entityName}' is not configured for Oracle sync.");
        }

        var configured = GetConfiguredEntityNames(current).ToArray();
        return configured.Select(name => SupportedEntities[name]).ToArray();
    }

    private static IEnumerable<string> GetConfiguredEntityNames(OracleSecondarySyncOptions current)
    {
        var csvNames = string.IsNullOrWhiteSpace(current.EntityNamesCsv)
            ? Array.Empty<string>()
            : current.EntityNamesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var configuredArray = current.EntityNames ?? Array.Empty<string>();
        var names = configuredArray.Length == 0 ? csvNames : configuredArray;
        if (names.Length == 0)
        {
            return SupportedEntities.Keys;
        }

        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name => SupportedEntities.ContainsKey(name));
    }

    private static bool IsSupportedDirection(string value)
    {
        return string.Equals(value, "PushToOracle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "PushOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "PullFromOracle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "PullOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Bidirectional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "TwoWay", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPush(string value)
    {
        return string.Equals(value, "PushToOracle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "PushOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Bidirectional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "TwoWay", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldPull(string value)
    {
        return string.Equals(value, "PullFromOracle", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "PullOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Bidirectional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "TwoWay", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Status, string Note) BuildInboundDecision(OracleSecondarySyncOptions current, string entityName, string operation)
    {
        var policy = string.IsNullOrWhiteSpace(current.ConflictPolicy) ? "ManualReview" : current.ConflictPolicy.Trim();
        return policy.ToLowerInvariant() switch
        {
            "garmetixwins" => ("IgnoredByGarmetix", "GarmetixWins policy: external Oracle event stored for audit but not applied."),
            "oraclewins" => ("PendingReview", "OracleWins policy selected. Event is ready for guided review/apply once ownership mapping is finalized."),
            "latestwins" => ("PendingReview", "LatestWins policy selected. Event is queued until local-vs-Oracle version comparison rules are confirmed."),
            _ => ("PendingReview", $"ManualReview policy: {entityName} {operation} event pulled from Oracle and held for review.")
        };
    }

    private static Expression<Func<T, bool>> BuildChangedSinceExpression<T>(DateTime sinceUtc)
    {
        var parameter = Expression.Parameter(typeof(T), "entity");
        var version = BuildVersionBody(parameter, typeof(T));
        var body = Expression.GreaterThan(version, Expression.Constant(sinceUtc));
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression<Func<T, DateTime>> BuildVersionExpression<T>()
    {
        var parameter = Expression.Parameter(typeof(T), "entity");
        var body = BuildVersionBody(parameter, typeof(T));
        return Expression.Lambda<Func<T, DateTime>>(body, parameter);
    }

    private static Expression BuildVersionBody(ParameterExpression parameter, Type type)
    {
        var updated = type.GetProperty("UpdatedAt");
        var created = type.GetProperty("CreatedAt");
        var fallback = created is not null && created.PropertyType == typeof(DateTime)
            ? Expression.Property(parameter, created)
            : Expression.Constant(DateTime.MinValue);

        if (updated is null)
        {
            return fallback;
        }

        var updatedValue = Expression.Property(parameter, updated);
        if (updated.PropertyType == typeof(DateTime))
        {
            return updatedValue;
        }

        if (updated.PropertyType == typeof(DateTime?))
        {
            return Expression.Coalesce(updatedValue, fallback);
        }

        return fallback;
    }

    private static DateTimeOffset GetVersionUtc(object row)
    {
        var updated = row.GetType().GetProperty("UpdatedAt")?.GetValue(row);
        var created = row.GetType().GetProperty("CreatedAt")?.GetValue(row);
        var value = updated switch
        {
            DateTime updatedAt => updatedAt,
            _ => created is DateTime createdAt ? createdAt : DateTime.UtcNow
        };

        return value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
            : new DateTimeOffset(value.ToUniversalTime());
    }

    private static bool GetBooleanProperty(object row, string propertyName)
    {
        return row.GetType().GetProperty(propertyName)?.GetValue(row) is bool value && value;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : null;
    }

    private static DateTime ToUtcDateTime(object value)
    {
        var dateTime = value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.UtcDateTime,
            _ => DateTime.TryParse(Convert.ToString(value), out var parsed) ? parsed : DateTime.UtcNow
        };

        return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime.ToUniversalTime();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static Guid DeterministicGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes[..16]);
    }

    private static string BuildOracleSchemaPrefix(string? schema)
    {
        var normalized = NormalizeOracleIdentifier(schema);
        return string.IsNullOrWhiteSpace(normalized) ? string.Empty : $"{normalized}.";
    }

    private static string NormalizeOracleIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_' && ch != '$' && ch != '#'))
        {
            throw new InvalidOperationException("Oracle schema contains unsupported characters. Use only letters, digits, _, $, or #.");
        }

        return normalized;
    }
}

using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.ImportExport;

public static class AdminJsonDataEndpoints
{
    private const string RequiredImportConfirmation = "IMPORT JSON";
    private const string RequiredRestoreConfirmation = "RESTORE JSON BACKUP";
    private const string RequiredDeleteConfirmation = "DELETE WITH CASCADE";
    private const string RequiredClearConfirmation = "DELETE TABLE DATA";

    private static readonly string[] SafeTables =
    [
        "Companies", "StoreGroups", "Stores", "Users", "Customers", "Vendors", "Salesmen",
        "Brands", "ProductCategories", "ProductSubCategories", "Products", "ProductDetails", "Taxes",
        "Stocks", "StockMovements", "DocumentSequences",
        "SalesInvoices", "PurchaseInvoices", "InvoiceItems", "InvoicePayments", "CardPayments", "PurchasePayments", "VendorPayments",
        "Vouchers", "CashVouchers", "JournalEntries", "JournalLines", "Ledgers", "LedgerGroups", "Parties",
        "PettyCashSheets", "DayBegins", "DayEnds", "CashDetails",
        "Employees", "EmployeeDetails", "Attendance", "MonthlyAttendance", "AttendancePunches", "AttendanceShifts", "AttendancePolicies", "EmployeeAttendanceShiftRules",
        "SalaryStructures", "SalaryPaySlips", "SalaryPayments", "EmployeePayrollAdjustments",
        "LoyaltyPrograms", "LoyaltyPointLedgers", "CommercialNotes", "CustomerAdvanceReceipts",
        "TailoringServiceItems", "TailoringVendorServiceRates", "TailoringOrders", "TailoringOrderLines", "TailoringCustomerReceipts", "TailoringVendorPayments", "TailoringOrderHistories",
        "PurchaseReturns", "PurchaseReturnItems", "PurchaseReturnItcReversals", "VendorSettlements", "VendorSettlementAllocations",
        "NonGstGoodsDocuments", "NonGstGoodsItems", "StockOperationDocuments", "StockOperationItems",
        "AuditLogEntries", "ApplicationMessageLogs"
    ];

    private static readonly Dictionary<string, string> TableLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SalesInvoices"] = "Sales invoices",
        ["PurchaseInvoices"] = "Purchase inward invoices",
        ["InvoiceItems"] = "Sale/purchase invoice items",
        ["Products"] = "Product master",
        ["Stocks"] = "Stock master",
        ["StockMovements"] = "Stock movements",
        ["Vendors"] = "Vendors",
        ["Customers"] = "Customers",
        ["Brands"] = "Brands",
        ["ProductCategories"] = "Product categories",
        ["ProductSubCategories"] = "Product sub-categories",
        ["Employees"] = "Employees",
        ["AttendancePunches"] = "Attendance punches",
        ["Attendance"] = "Daily attendance",
        ["JournalEntries"] = "Journal entries",
        ["JournalLines"] = "Journal lines"
    };

    public static RouteGroupBuilder MapAdminJsonDataEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin-data")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/tables", TablesAsync);
        group.MapGet("/export", ExportAsync);
        group.MapPost("/validate", ValidateImportAsync).DisableAntiforgery();
        group.MapPost("/import", ImportAsync).DisableAntiforgery();
        group.MapPost("/backup", BackupAsync);
        group.MapGet("/backups", BackupsAsync);
        group.MapPost("/restore/{id:guid}", RestoreAsync);
        group.MapPost("/delete/preview", DeletePreviewAsync);
        group.MapPost("/delete/execute", DeleteExecuteAsync);
        group.MapPost("/clear/preview", ClearPreviewAsync);
        group.MapPost("/clear/execute", ClearExecuteAsync);

        return group;
    }

    private static async Task<IResult> TablesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        await EnsureAdminBackupTableAsync(db, cancellationToken);
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);

        var rows = new List<object>();
        foreach (var table in SafeTables.OrderBy(item => item))
        {
            if (!await TableExistsAsync(connection, table, cancellationToken))
            {
                continue;
            }

            var count = await ScalarLongAsync(connection, $"select count(*) from {Q(table)}", cancellationToken);
            var columns = await ColumnsAsync(connection, table, cancellationToken);
            rows.Add(new
            {
                name = table,
                label = TableLabels.GetValueOrDefault(table, table),
                rows = count,
                columns = columns.Count,
                destructive = IsDestructiveTable(table),
                exportable = true,
                importable = true,
                clearable = true
            });
        }

        return Results.Ok(new
        {
            stage = "Stage 11D-36 Admin JSON Data Maintenance",
            generatedAtUtc = DateTimeOffset.UtcNow,
            requiredConfirmations = new
            {
                importJson = RequiredImportConfirmation,
                restore = RequiredRestoreConfirmation,
                deleteWithCascade = RequiredDeleteConfirmation,
                clearTableData = RequiredClearConfirmation
            },
            tables = rows
        });
    }

    private static async Task<IResult> ExportAsync(string? tables, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var selectedTables = ParseTables(tables);
        if (selectedTables.Count == 0)
        {
            return Results.BadRequest(new { message = "Select at least one table." });
        }

        var payload = await BuildExportPayloadAsync(db, selectedTables, "admin-json-export", cancellationToken);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions());
        var fileName = $"Garmetix-AdminJsonExport-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
        return Results.File(bytes, "application/json", fileName);
    }

    private static async Task<IResult> ValidateImportAsync(IFormFile file, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var package = await ReadPackageAsync(file, cancellationToken);
        if (package.Errors.Count > 0)
        {
            return Results.Ok(package);
        }

        var validation = await ValidatePackageAsync(db, package, cancellationToken);
        return Results.Ok(validation);
    }

    private static async Task<IResult> ImportAsync(
        IFormFile file,
        string? mode,
        string? confirmation,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, RequiredImportConfirmation, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = $"Type {RequiredImportConfirmation} to import JSON." });
        }

        var importMode = string.IsNullOrWhiteSpace(mode) ? "upsert" : mode.Trim().ToLowerInvariant();
        if (importMode is not ("upsert" or "insert-only" or "replace-table"))
        {
            return Results.BadRequest(new { message = "mode must be upsert, insert-only, or replace-table." });
        }

        var package = await ReadPackageAsync(file, cancellationToken);
        if (package.Errors.Count > 0)
        {
            return Results.Ok(package);
        }

        var validation = await ValidatePackageAsync(db, package, cancellationToken);
        if (validation.Errors.Count > 0)
        {
            return Results.Ok(validation with { Commit = true, Message = "Import blocked because validation failed." });
        }

        var selectedTables = package.Tables.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var backup = await SaveSnapshotAsync(db, selectedTables, "before-json-import", $"Before JSON import mode={importMode}", cancellationToken);

        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (importMode == "replace-table")
            {
                foreach (var table in selectedTables.AsEnumerable().Reverse())
                {
                    await ExecuteAsync(connection, transaction, $"truncate table {Q(table)} restart identity cascade", cancellationToken);
                }
            }

            var importReport = new List<object>();
            foreach (var tablePackage in package.Tables)
            {
                var columns = await ColumnsAsync(connection, tablePackage.Name, cancellationToken);
                var tableReport = await ImportTableAsync(connection, transaction, tablePackage, columns, importMode, cancellationToken);
                importReport.Add(tableReport);
            }

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new
            {
                commit = true,
                mode = importMode,
                backupId = backup.Id,
                backupCreatedAtUtc = backup.CreatedAtUtc,
                message = "JSON import completed. Backup snapshot was created before import.",
                tables = importReport
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> BackupAsync(AdminBackupRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var selectedTables = ParseTables(request.Tables);
        if (selectedTables.Count == 0)
        {
            return Results.BadRequest(new { message = "Select at least one table for backup." });
        }

        var backup = await SaveSnapshotAsync(db, selectedTables, request.Operation ?? "manual-json-backup", request.Remarks ?? "Manual admin JSON backup", cancellationToken);
        return Results.Ok(new
        {
            backup.Id,
            backup.CreatedAtUtc,
            tables = selectedTables,
            backup.Operation,
            backup.Remarks,
            message = "JSON snapshot backup created."
        });
    }

    private static async Task<IResult> BackupsAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        await EnsureAdminBackupTableAsync(db, cancellationToken);
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var json = await ScalarStringAsync(connection, """
            select coalesce(jsonb_agg(to_jsonb(t) order by t."CreatedAtUtc" desc), '[]'::jsonb)::text
            from (
              select "Id", "CreatedAtUtc", "Operation", "Tables", "RowCounts", "Remarks", "CreatedBy"
              from "AdminJsonBackups"
              order by "CreatedAtUtc" desc
              limit 50
            ) t
            """, cancellationToken) ?? "[]";
        return Results.Content(json, "application/json");
    }

    private static async Task<IResult> RestoreAsync(Guid id, string? confirmation, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, RequiredRestoreConfirmation, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = $"Type {RequiredRestoreConfirmation} to restore this JSON backup." });
        }

        await EnsureAdminBackupTableAsync(db, cancellationToken);
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);

        var backupJson = await ScalarStringAsync(connection, "select \"Payload\"::text from \"AdminJsonBackups\" where \"Id\" = @id", cancellationToken, ("id", id));
        if (string.IsNullOrWhiteSpace(backupJson))
        {
            return Results.NotFound(new { message = "Backup not found." });
        }

        var package = ParsePackage(backupJson);
        if (package.Errors.Count > 0)
        {
            return Results.BadRequest(new { message = "Backup payload is not valid JSON.", package.Errors });
        }

        var selectedTables = package.Tables.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var restoreSafetyBackup = await SaveSnapshotAsync(db, selectedTables, "before-json-restore", $"Before restore from backup {id}", cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var table in selectedTables.AsEnumerable().Reverse())
            {
                await ExecuteAsync(connection, transaction, $"truncate table {Q(table)} restart identity cascade", cancellationToken);
            }

            var report = new List<object>();
            foreach (var tablePackage in package.Tables)
            {
                var columns = await ColumnsAsync(connection, tablePackage.Name, cancellationToken);
                report.Add(await ImportTableAsync(connection, transaction, tablePackage, columns, "insert-only", cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new
            {
                restoredFromBackupId = id,
                safetyBackupId = restoreSafetyBackup.Id,
                message = "Restore completed from JSON snapshot. A safety backup was created before restore.",
                tables = report
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> DeletePreviewAsync(AdminDeleteRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var table = RequireSafeTable(request.Table);
        var ids = request.Ids.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Results.BadRequest(new { message = "Select one or more row IDs." });
        }

        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var preview = await PreviewCascadeAsync(connection, table, ids, cancellationToken);
        return Results.Ok(new { table, ids = ids.Length, destructive = true, preview });
    }

    private static async Task<IResult> DeleteExecuteAsync(AdminDeleteRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation, RequiredDeleteConfirmation, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = $"Type {RequiredDeleteConfirmation} to delete selected rows with cascade." });
        }

        var table = RequireSafeTable(request.Table);
        var ids = request.Ids.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Results.BadRequest(new { message = "Select one or more row IDs." });
        }

        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var preview = await PreviewCascadeAsync(connection, table, ids, cancellationToken);
        var affectedTables = preview.Select(item => item.Table).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var backup = await SaveSnapshotAsync(db, affectedTables, "before-cascade-delete", $"Before cascade delete from {table}", cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var log = new List<CascadeCount>();
            await DeleteCascadeAsync(connection, transaction, table, ids, log, new HashSet<string>(StringComparer.OrdinalIgnoreCase), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new
            {
                message = "Selected rows deleted with cascade.",
                backupId = backup.Id,
                table,
                log
            });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<IResult> ClearPreviewAsync(AdminClearRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var table = RequireSafeTable(request.Table);
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var related = await CollectRelatedTablesAsync(connection, table, cancellationToken);
        var preview = new List<object>();
        foreach (var item in related)
        {
            if (await TableExistsAsync(connection, item, cancellationToken))
            {
                preview.Add(new { table = item, rows = await ScalarLongAsync(connection, $"select count(*) from {Q(item)}", cancellationToken) });
            }
        }
        return Results.Ok(new { table, destructive = true, mode = "truncate cascade", preview });
    }

    private static async Task<IResult> ClearExecuteAsync(AdminClearRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation, RequiredClearConfirmation, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = $"Type {RequiredClearConfirmation} to clear table data." });
        }

        var table = RequireSafeTable(request.Table);
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var related = await CollectRelatedTablesAsync(connection, table, cancellationToken);
        var backup = await SaveSnapshotAsync(db, related, "before-table-clear", $"Before clearing {table} with cascade", cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction, $"truncate table {Q(table)} restart identity cascade", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(new { message = "Table data cleared with database cascade.", backupId = backup.Id, table, relatedTablesBackedUp = related });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<AdminBackupInfo> SaveSnapshotAsync(GarmetixDbContext db, IReadOnlyList<string> tables, string operation, string remarks, CancellationToken cancellationToken)
    {
        await EnsureAdminBackupTableAsync(db, cancellationToken);
        var selected = tables.Select(RequireSafeTable).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var payload = await BuildExportPayloadAsync(db, selected, operation, cancellationToken);
        var rowCounts = payload.Tables.ToDictionary(item => item.Name, item => item.Count, StringComparer.OrdinalIgnoreCase);
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        await ExecuteAsync(connection, null, """
            insert into "AdminJsonBackups" ("Id", "CreatedAtUtc", "Operation", "Tables", "RowCounts", "Payload", "CreatedBy", "Remarks")
            values (@id, @createdAt, @operation, @tables, @rowCounts::jsonb, @payload::jsonb, current_user, @remarks)
            """, cancellationToken,
            ("id", id),
            ("createdAt", createdAt.UtcDateTime),
            ("operation", operation),
            ("tables", string.Join(',', selected)),
            ("rowCounts", JsonSerializer.Serialize(rowCounts)),
            ("payload", JsonSerializer.Serialize(payload, JsonOptions())),
            ("remarks", remarks));

        return new AdminBackupInfo(id, createdAt, operation, remarks);
    }

    private static async Task<AdminJsonPackage> BuildExportPayloadAsync(GarmetixDbContext db, IReadOnlyList<string> tables, string operation, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var tablePayloads = new List<AdminJsonTablePayload>();
        foreach (var table in tables.Select(RequireSafeTable).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await TableExistsAsync(connection, table, cancellationToken))
            {
                continue;
            }

            var columns = await ColumnsAsync(connection, table, cancellationToken);
            var orderBy = columns.Any(item => item.Name.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase))
                ? " order by \"CreatedAt\" nulls last"
                : columns.Any(item => item.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                    ? " order by \"Id\""
                    : string.Empty;
            var json = await ScalarStringAsync(connection, $"select coalesce(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text from (select * from {Q(table)}{orderBy}) t", cancellationToken) ?? "[]";
            var records = JsonDocument.Parse(json).RootElement.Clone();
            var count = records.ValueKind == JsonValueKind.Array ? records.GetArrayLength() : 0;
            tablePayloads.Add(new AdminJsonTablePayload(table, columns.Select(item => item.Name).ToArray(), count, records));
        }

        return new AdminJsonPackage(
            new AdminJsonMetadata("GarmetixAdminJson", 1, DateTimeOffset.UtcNow, operation, "JSON table export/import package"),
            tablePayloads,
            []);
    }

    private static async Task<AdminJsonValidationResult> ValidatePackageAsync(GarmetixDbContext db, AdminJsonPackage package, CancellationToken cancellationToken)
    {
        var errors = new List<object>();
        var tables = new List<object>();
        var connection = db.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);

        foreach (var table in package.Tables)
        {
            try
            {
                RequireSafeTable(table.Name);
            }
            catch (Exception ex)
            {
                errors.Add(new { table = table.Name, row = 0, field = "Table", message = ex.Message });
                continue;
            }

            if (!await TableExistsAsync(connection, table.Name, cancellationToken))
            {
                errors.Add(new { table = table.Name, row = 0, field = "Table", message = "Table does not exist in this database." });
                continue;
            }

            var columns = await ColumnsAsync(connection, table.Name, cancellationToken);
            var columnSet = columns.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rowErrors = 0;
            var rowNumber = 0;
            if (table.Records.ValueKind != JsonValueKind.Array)
            {
                errors.Add(new { table = table.Name, row = 0, field = "records", message = "records must be a JSON array." });
                rowErrors++;
            }
            else
            {
                foreach (var record in table.Records.EnumerateArray())
                {
                    rowNumber++;
                    if (record.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add(new { table = table.Name, row = rowNumber, field = "record", message = "Each record must be a JSON object." });
                        rowErrors++;
                        continue;
                    }

                    foreach (var property in record.EnumerateObject())
                    {
                        if (!columnSet.Contains(property.Name))
                        {
                            errors.Add(new { table = table.Name, row = rowNumber, field = property.Name, message = "Column does not exist in target table." });
                            rowErrors++;
                        }
                    }
                }
            }

            tables.Add(new { table = table.Name, rows = table.Count, rowErrors, columns = columns.Count });
        }

        return new AdminJsonValidationResult(false, package.Tables.Count, package.Tables.Sum(item => item.Count), tables, errors, errors.Count == 0 ? "JSON package is valid." : "JSON package has validation errors.");
    }

    private static async Task<object> ImportTableAsync(DbConnection connection, DbTransaction transaction, AdminJsonTablePayload tablePackage, IReadOnlyList<ColumnInfo> columns, string mode, CancellationToken cancellationToken)
    {
        var columnByName = columns.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        if (tablePackage.Records.ValueKind != JsonValueKind.Array)
        {
            return new { table = tablePackage.Name, inserted, updated, skipped, error = "records is not an array" };
        }

        foreach (var record in tablePackage.Records.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Object)
            {
                skipped++;
                continue;
            }

            var values = record.EnumerateObject()
                .Where(item => columnByName.ContainsKey(item.Name))
                .ToDictionary(item => item.Name, item => item.Value.Clone(), StringComparer.OrdinalIgnoreCase);

            if (values.Count == 0)
            {
                skipped++;
                continue;
            }

            var hasId = values.TryGetValue("Id", out var idElement) && idElement.ValueKind != JsonValueKind.Null && !string.IsNullOrWhiteSpace(ValueAsString(idElement));
            var exists = false;
            if (hasId)
            {
                exists = await ScalarLongAsync(
                    connection,
                    $"select count(*) from {Q(tablePackage.Name)} where \"Id\" = @id::uuid",
                    cancellationToken,
                    new[] { ("id", (object?)(ValueAsString(idElement) ?? string.Empty)) },
                    transaction) > 0;
            }

            if (exists && mode == "insert-only")
            {
                skipped++;
                continue;
            }

            if (exists)
            {
                var setColumns = values.Keys.Where(item => !item.Equals("Id", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (setColumns.Length == 0)
                {
                    skipped++;
                    continue;
                }

                var sql = new StringBuilder($"update {Q(tablePackage.Name)} set ");
                var parameters = new List<(string Name, object? Value)>();
                for (var i = 0; i < setColumns.Length; i++)
                {
                    var column = columnByName[setColumns[i]];
                    if (i > 0) sql.Append(", ");
                    sql.Append($"{Q(column.Name)} = @p{i}{Cast(column)}");
                    parameters.Add(($"p{i}", ValueAsString(values[column.Name])));
                }
                sql.Append(" where \"Id\" = @id::uuid");
                parameters.Add(("id", ValueAsString(idElement)));
                await ExecuteAsync(connection, transaction, sql.ToString(), cancellationToken, parameters.ToArray());
                updated++;
            }
            else
            {
                var insertColumns = values.Keys.ToArray();
                var sql = new StringBuilder($"insert into {Q(tablePackage.Name)} (");
                sql.Append(string.Join(", ", insertColumns.Select(Q)));
                sql.Append(") values (");
                var parameters = new List<(string Name, object? Value)>();
                for (var i = 0; i < insertColumns.Length; i++)
                {
                    var column = columnByName[insertColumns[i]];
                    if (i > 0) sql.Append(", ");
                    sql.Append($"@p{i}{Cast(column)}");
                    parameters.Add(($"p{i}", ValueAsString(values[column.Name])));
                }
                sql.Append(")");
                await ExecuteAsync(connection, transaction, sql.ToString(), cancellationToken, parameters.ToArray());
                inserted++;
            }
        }

        return new { table = tablePackage.Name, inserted, updated, skipped };
    }

    private static async Task<IReadOnlyList<CascadeCount>> PreviewCascadeAsync(DbConnection connection, string table, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var log = new List<CascadeCount>();
        await PreviewCascadeAsync(connection, table, ids, log, new HashSet<string>(StringComparer.OrdinalIgnoreCase), cancellationToken);
        return log;
    }

    private static async Task PreviewCascadeAsync(DbConnection connection, string table, IReadOnlyList<Guid> ids, List<CascadeCount> log, HashSet<string> visited, CancellationToken cancellationToken)
    {
        var key = table + ":" + string.Join(',', ids.OrderBy(item => item));
        if (!visited.Add(key)) return;

        foreach (var reference in await ReferencesToAsync(connection, table, cancellationToken))
        {
            if (!await TableExistsAsync(connection, reference.Table, cancellationToken) || !await ColumnExistsAsync(connection, reference.Table, reference.Column, cancellationToken)) continue;
            var childIds = await ChildIdsAsync(connection, reference.Table, reference.Column, ids, cancellationToken);
            if (childIds.Count > 0)
            {
                await PreviewCascadeAsync(connection, reference.Table, childIds, log, visited, cancellationToken);
            }
            else
            {
                var count = await CountByColumnAsync(connection, reference.Table, reference.Column, ids, cancellationToken);
                if (count > 0) log.Add(new CascadeCount(reference.Table, reference.Column, count));
            }
        }

        var ownCount = await CountByColumnAsync(connection, table, "Id", ids, cancellationToken);
        log.Add(new CascadeCount(table, "Id", ownCount));
    }

    private static async Task DeleteCascadeAsync(DbConnection connection, DbTransaction transaction, string table, IReadOnlyList<Guid> ids, List<CascadeCount> log, HashSet<string> visited, CancellationToken cancellationToken)
    {
        var key = table + ":" + string.Join(',', ids.OrderBy(item => item));
        if (!visited.Add(key)) return;

        foreach (var reference in await ReferencesToAsync(connection, table, cancellationToken))
        {
            if (!await TableExistsAsync(connection, reference.Table, cancellationToken) || !await ColumnExistsAsync(connection, reference.Table, reference.Column, cancellationToken)) continue;
            var childIds = await ChildIdsAsync(connection, reference.Table, reference.Column, ids, cancellationToken, transaction);
            if (childIds.Count > 0)
            {
                await DeleteCascadeAsync(connection, transaction, reference.Table, childIds, log, visited, cancellationToken);
            }

            var deleted = await DeleteByColumnAsync(connection, transaction, reference.Table, reference.Column, ids, cancellationToken);
            if (deleted > 0) log.Add(new CascadeCount(reference.Table, reference.Column, deleted));
        }

        var ownDeleted = await DeleteByColumnAsync(connection, transaction, table, "Id", ids, cancellationToken);
        log.Add(new CascadeCount(table, "Id", ownDeleted));
    }

    private static async Task<IReadOnlyList<TableReference>> ReferencesToAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var references = new List<TableReference>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                select kcu.table_name, kcu.column_name
                from information_schema.table_constraints tc
                join information_schema.key_column_usage kcu
                  on tc.constraint_name = kcu.constraint_name and tc.table_schema = kcu.table_schema
                join information_schema.constraint_column_usage ccu
                  on ccu.constraint_name = tc.constraint_name and ccu.table_schema = tc.table_schema
                where tc.constraint_type = 'FOREIGN KEY'
                  and tc.table_schema = 'public'
                  and ccu.table_name = @table
                  and ccu.column_name = 'Id'
                """;
            AddParam(cmd, "table", table);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                references.Add(new TableReference(reader.GetString(0), reader.GetString(1)));
            }
        }

        if (table.Equals("SalesInvoices", StringComparison.OrdinalIgnoreCase))
        {
            references.AddRange([new("InvoiceItems", "InvoiceId"), new("InvoicePayments", "InvoiceId"), new("CardPayments", "InvoiceId"), new("VendorPayments", "InvoiceId"), new("StockMovements", "SourceId"), new("JournalEntries", "SourceId")]);
        }
        else if (table.Equals("PurchaseInvoices", StringComparison.OrdinalIgnoreCase))
        {
            references.AddRange([new("InvoiceItems", "InvoiceId"), new("PurchasePayments", "PurchaseInvoiceId"), new("VendorPayments", "PurchaseInvoiceId"), new("StockMovements", "SourceId"), new("JournalEntries", "SourceId"), new("PurchaseReturns", "PurchaseInvoiceId")]);
        }
        else if (table.Equals("JournalEntries", StringComparison.OrdinalIgnoreCase))
        {
            references.Add(new("JournalLines", "JournalEntryId"));
        }
        else if (table.Equals("Products", StringComparison.OrdinalIgnoreCase))
        {
            references.AddRange([new("ProductDetails", "ProductId"), new("Stocks", "ProductId"), new("StockMovements", "ProductId"), new("InvoiceItems", "ProductId")]);
        }
        else if (table.Equals("Vendors", StringComparison.OrdinalIgnoreCase))
        {
            references.AddRange([new("PurchaseInvoices", "VendorId"), new("PurchasePayments", "VendorId"), new("VendorPayments", "VendorId"), new("VendorSettlements", "VendorId")]);
        }
        else if (table.Equals("Customers", StringComparison.OrdinalIgnoreCase))
        {
            references.AddRange([new("SalesInvoices", "CustomerId"), new("CustomerAdvanceReceipts", "CustomerId")]);
        }

        return references
            .Where(item => SafeTables.Contains(item.Table, StringComparer.OrdinalIgnoreCase))
            .DistinctBy(item => item.Table + ":" + item.Column, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<IReadOnlyList<string>> CollectRelatedTablesAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var related = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { table };
        async Task Walk(string parent)
        {
            foreach (var reference in await ReferencesToAsync(connection, parent, cancellationToken))
            {
                if (related.Add(reference.Table)) await Walk(reference.Table);
            }
        }
        await Walk(table);
        return related.ToList();
    }

    private static async Task<IReadOnlyList<Guid>> ChildIdsAsync(DbConnection connection, string table, string column, IReadOnlyList<Guid> ids, CancellationToken cancellationToken, DbTransaction? transaction = null)
    {
        if (!await ColumnExistsAsync(connection, table, "Id", cancellationToken)) return [];
        var sql = $"select \"Id\"::text from {Q(table)} where {Q(column)} = any(@ids)";
        var values = new List<Guid>();
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        AddParam(cmd, "ids", ids.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (Guid.TryParse(reader.GetString(0), out var id)) values.Add(id);
        }
        return values;
    }

    private static async Task<long> CountByColumnAsync(DbConnection connection, string table, string column, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        => await ScalarLongAsync(connection, $"select count(*) from {Q(table)} where {Q(column)} = any(@ids)", cancellationToken, ("ids", ids.ToArray()));

    private static async Task<int> DeleteByColumnAsync(DbConnection connection, DbTransaction transaction, string table, string column, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        => await ExecuteAsync(connection, transaction, $"delete from {Q(table)} where {Q(column)} = any(@ids)", cancellationToken, ("ids", ids.ToArray()));

    private static async Task<AdminJsonPackage> ReadPackageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0) return new AdminJsonPackage(null, [], ["Upload a JSON export file."]);
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return ParsePackage(text);
    }

    private static AdminJsonPackage ParsePackage(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var metadata = root.TryGetProperty("metadata", out var metadataElement)
                ? new AdminJsonMetadata(
                    metadataElement.TryGetProperty("packageType", out var packageType) ? packageType.GetString() ?? "GarmetixAdminJson" : "GarmetixAdminJson",
                    metadataElement.TryGetProperty("version", out var version) && version.TryGetInt32(out var versionValue) ? versionValue : 1,
                    metadataElement.TryGetProperty("generatedAtUtc", out var generatedAt) && generatedAt.TryGetDateTimeOffset(out var generatedAtValue) ? generatedAtValue : DateTimeOffset.UtcNow,
                    metadataElement.TryGetProperty("operation", out var operation) ? operation.GetString() ?? "json-import" : "json-import",
                    metadataElement.TryGetProperty("notes", out var notes) ? notes.GetString() ?? string.Empty : string.Empty)
                : new AdminJsonMetadata("GarmetixAdminJson", 1, DateTimeOffset.UtcNow, "json-import", string.Empty);

            var tables = new List<AdminJsonTablePayload>();
            if (!root.TryGetProperty("tables", out var tablesElement) || tablesElement.ValueKind != JsonValueKind.Array)
            {
                return new AdminJsonPackage(metadata, [], ["Root property 'tables' must be an array."]);
            }

            foreach (var item in tablesElement.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                var columns = item.TryGetProperty("columns", out var columnsElement) && columnsElement.ValueKind == JsonValueKind.Array
                    ? columnsElement.EnumerateArray().Select(column => column.GetString() ?? string.Empty).Where(column => !string.IsNullOrWhiteSpace(column)).ToArray()
                    : [];
                var records = item.TryGetProperty("records", out var recordsElement) ? recordsElement.Clone() : JsonDocument.Parse("[]").RootElement.Clone();
                var count = records.ValueKind == JsonValueKind.Array ? records.GetArrayLength() : 0;
                tables.Add(new AdminJsonTablePayload(name, columns, count, records));
            }

            return new AdminJsonPackage(metadata, tables, []);
        }
        catch (Exception ex)
        {
            return new AdminJsonPackage(null, [], [$"Invalid JSON: {ex.Message}"]);
        }
    }

    private static async Task EnsureAdminBackupTableAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            create table if not exists "AdminJsonBackups" (
                "Id" uuid primary key,
                "CreatedAtUtc" timestamp without time zone not null,
                "Operation" text not null,
                "Tables" text not null,
                "RowCounts" jsonb not null default '{}'::jsonb,
                "Payload" jsonb not null,
                "CreatedBy" text null,
                "Remarks" text null
            );
            create index if not exists "IX_AdminJsonBackups_CreatedAtUtc" on "AdminJsonBackups" ("CreatedAtUtc" desc);
            """, cancellationToken);
    }

    private static List<string> ParseTables(string? tables)
        => (tables ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RequireSafeTable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IReadOnlyList<string> ParseTables(IEnumerable<string>? tables)
        => (tables ?? [])
            .Select(RequireSafeTable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string RequireSafeTable(string table)
    {
        if (string.IsNullOrWhiteSpace(table) || !SafeTables.Contains(table.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Table '{table}' is not allowed for admin JSON maintenance.");
        }
        return SafeTables.First(item => item.Equals(table.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDestructiveTable(string table)
        => table.Contains("Invoice", StringComparison.OrdinalIgnoreCase)
            || table.Contains("Payment", StringComparison.OrdinalIgnoreCase)
            || table.Contains("Journal", StringComparison.OrdinalIgnoreCase)
            || table.Contains("Stock", StringComparison.OrdinalIgnoreCase)
            || table.Contains("Attendance", StringComparison.OrdinalIgnoreCase)
            || table.Contains("Salary", StringComparison.OrdinalIgnoreCase);

    private static async Task OpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
        => await ScalarLongAsync(connection, "select count(*) from information_schema.tables where table_schema = 'public' and table_name = @table", cancellationToken, ("table", table)) > 0;

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
        => await ScalarLongAsync(connection, "select count(*) from information_schema.columns where table_schema = 'public' and table_name = @table and column_name = @column", cancellationToken, ("table", table), ("column", column)) > 0;

    private static async Task<IReadOnlyList<ColumnInfo>> ColumnsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var columns = new List<ColumnInfo>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            select column_name, data_type, udt_name, is_nullable
            from information_schema.columns
            where table_schema = 'public' and table_name = @table
            order by ordinal_position
            """;
        AddParam(cmd, "table", table);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new ColumnInfo(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase)));
        }
        return columns;
    }

    private static async Task<string?> ScalarStringAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) AddParam(cmd, p.Name, p.Value);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    private static async Task<long> ScalarLongAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
        => await ScalarLongAsync(connection, sql, cancellationToken, parameters, null);

    private static async Task<long> ScalarLongAsync(DbConnection connection, string sql, CancellationToken cancellationToken, (string Name, object? Value)[] parameters, DbTransaction? transaction)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        foreach (var p in parameters) AddParam(cmd, p.Name, p.Value);
        var value = await cmd.ExecuteScalarAsync(cancellationToken);
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
    }

    private static async Task<int> ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        foreach (var p in parameters) AddParam(cmd, p.Name, p.Value);
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
    }

    private static string Q(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string Cast(ColumnInfo column)
    {
        var type = column.DataType.ToLowerInvariant();
        var udt = column.UdtName.ToLowerInvariant();
        if (udt == "uuid") return "::uuid";
        if (type.Contains("timestamp")) return "::timestamp without time zone";
        if (type == "date") return "::date";
        if (type is "numeric" or "decimal") return "::numeric";
        if (type is "integer") return "::integer";
        if (type is "bigint") return "::bigint";
        if (type is "smallint") return "::smallint";
        if (type is "boolean") return "::boolean";
        if (type == "json" || type == "jsonb") return "::jsonb";
        return string.Empty;
    }

    private static string? ValueAsString(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText()
        };

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private sealed record ColumnInfo(string Name, string DataType, string UdtName, bool Nullable);
    private sealed record TableReference(string Table, string Column);
    private sealed record CascadeCount(string Table, string Column, long Rows);
    private sealed record AdminBackupInfo(Guid Id, DateTimeOffset CreatedAtUtc, string Operation, string Remarks);
    private sealed record AdminJsonMetadata(string PackageType, int Version, DateTimeOffset GeneratedAtUtc, string Operation, string Notes);
    private sealed record AdminJsonTablePayload(string Name, string[] Columns, int Count, JsonElement Records);
    private sealed record AdminJsonPackage(AdminJsonMetadata? Metadata, IReadOnlyList<AdminJsonTablePayload> Tables, IReadOnlyList<string> Errors);
    private sealed record AdminJsonValidationResult(bool Commit, int TableCount, int RowCount, IReadOnlyList<object> Tables, IReadOnlyList<object> Errors, string Message);
    private sealed record AdminBackupRequest(IEnumerable<string>? Tables, string? Operation, string? Remarks);
    private sealed record AdminDeleteRequest(string Table, Guid[] Ids, string? Confirmation);
    private sealed record AdminClearRequest(string Table, string? Confirmation);
}

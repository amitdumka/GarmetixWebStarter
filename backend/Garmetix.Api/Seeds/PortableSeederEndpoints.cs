using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;

namespace Garmetix.Api.Seeds;

public sealed record PortableSeederTableDto(
    string Schema,
    string Table,
    int RowCount,
    IReadOnlyList<JsonElement> Rows);

public sealed record PortableSeederFileDto(
    string Format,
    int Version,
    DateTimeOffset ExportedAtUtc,
    string Source,
    IReadOnlyList<PortableSeederTableDto> Tables);

public sealed record PortableSeederImportResultDto(
    DateTimeOffset ImportedAtUtc,
    int TablesProcessed,
    int RowsProcessed,
    IReadOnlyList<string> Notes);

public static class PortableSeederEndpoints
{
    private const string FormatName = "GarmetixPortableJsonSeeder";
    private const int FormatVersion = 1;

    public static RouteGroupBuilder MapPortableSeederEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/portable-seeder")
            .WithTags("Portable Seeder")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/export", ExportAsync);
        group.MapPost("/import", ImportAsync);

        return group;
    }

    private static async Task<IResult> ExportAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var tables = new List<PortableSeederTableDto>();
        var tableMetas = db.Model.GetEntityTypes()
            .Select(entity => new
            {
                Schema = entity.GetSchema() ?? "public",
                Table = entity.GetTableName()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Table))
            .Distinct()
            .OrderBy(item => ExportOrder(item!.Table!))
            .ThenBy(item => item!.Table)
            .ToList();

        await EnsureConnectionOpenAsync(db, cancellationToken);
        foreach (var meta in tableMetas)
        {
            var tableSql = QualifiedName(meta.Schema, meta.Table!);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COALESCE(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text FROM {tableSql} t";
            var json = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "[]";
            using var document = JsonDocument.Parse(json);
            var rows = document.RootElement.EnumerateArray()
                .Select(row => row.Clone())
                .Where(row => !IsProtectedDefaultAccountingRow(meta.Table!, row))
                .ToList();
            tables.Add(new PortableSeederTableDto(meta.Schema, meta.Table!, rows.Count, rows));
        }

        var file = new PortableSeederFileDto(
            FormatName,
            FormatVersion,
            DateTimeOffset.UtcNow,
            "Garmetix Admin Export",
            tables);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return Results.File(bytes, "application/json", $"garmetix-portable-seeder-{DateTime.UtcNow:yyyyMMddHHmmss}.json");
    }

    private static async Task<IResult> ImportAsync(
        PortableSeederFileDto file,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(file.Format, FormatName, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "This is not a Garmetix portable JSON seeder file." });
        }

        var notes = new List<string>
        {
            "Import is an upsert: rows with the same Id are updated, missing rows are inserted.",
            "Run Data Consistency after import and verify users/permissions before production use."
        };
        var rowsProcessed = 0;
        var skippedDefaultAccountingRows = 0;
        var protectedLedgerGroupByOldId = new Dictionary<Guid, (Guid CompanyId, string Name)>();
        var defaultsAppliedBeforeLedgers = false;
        var tableMetas = db.Model.GetEntityTypes()
            .Where(entity => !string.IsNullOrWhiteSpace(entity.GetTableName()))
            .Select(entity =>
            {
                var schema = entity.GetSchema() ?? "public";
                var table = entity.GetTableName()!;
                var storeObject = StoreObjectIdentifier.Table(table, schema);
                return new
                {
                    Schema = schema,
                    Table = table,
                    Properties = entity.GetProperties()
                        .Select(property => property.GetColumnName(storeObject))
                        .Where(column => !string.IsNullOrWhiteSpace(column))
                        .Select(column => column!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };
            })
            .GroupBy(item => $"{item.Schema}.{item.Table}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await EnsureConnectionOpenAsync(db, cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var table in file.Tables.OrderBy(table => ExportOrder(table.Table)).ThenBy(table => table.Table))
            {
                if (table.Table.Equals("Ledgers", StringComparison.OrdinalIgnoreCase) && !defaultsAppliedBeforeLedgers)
                {
                    await SeedDefaultsForAllCompaniesAsync(db, cancellationToken);
                    defaultsAppliedBeforeLedgers = true;
                }

                var key = $"{(string.IsNullOrWhiteSpace(table.Schema) ? "public" : table.Schema)}.{table.Table}";
                if (!tableMetas.TryGetValue(key, out var meta))
                {
                    notes.Add($"Skipped unknown table {key}.");
                    continue;
                }

                var rows = table.Rows;
                foreach (var row in rows)
                {
                    var rowJson = row.GetRawText();
                    if (string.IsNullOrWhiteSpace(rowJson) || row.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (IsProtectedDefaultAccountingRow(table.Table, row))
                    {
                        if (table.Table.Equals("LedgerGroups", StringComparison.OrdinalIgnoreCase)
                            && TryGetGuid(row, "Id", out var oldLedgerGroupId)
                            && TryGetGuid(row, "CompanyId", out var companyId))
                        {
                            var groupName = GetString(row, "Name");
                            if (!string.IsNullOrWhiteSpace(groupName))
                            {
                                protectedLedgerGroupByOldId[oldLedgerGroupId] = (companyId, groupName);
                            }
                        }

                        skippedDefaultAccountingRows++;
                        continue;
                    }

                    if (table.Table.Equals("Ledgers", StringComparison.OrdinalIgnoreCase))
                    {
                        rowJson = await NormalizeLedgerGroupReferenceAsync(db, row, rowJson, protectedLedgerGroupByOldId, cancellationToken);
                    }

                    await UpsertRowAsync(db, meta.Schema, meta.Table!, meta.Properties, rowJson, cancellationToken);
                    rowsProcessed++;
                }
            }

            await SeedDefaultsForAllCompaniesAsync(db, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });

        if (skippedDefaultAccountingRows > 0)
        {
            notes.Add($"Skipped {skippedDefaultAccountingRows} protected default ledger/ledger-group row(s); system defaults won the clash.");
        }
        notes.Add("Default Indian accounting ledger groups/ledgers were re-applied after import.");

        return Results.Ok(new PortableSeederImportResultDto(
            DateTimeOffset.UtcNow,
            file.Tables.Count,
            rowsProcessed,
            notes));
    }

private static bool IsProtectedDefaultAccountingRow(string table, JsonElement row)
{
    if (table.Equals("LedgerGroups", StringComparison.OrdinalIgnoreCase))
    {
        var name = GetString(row, "Name");
        var createdBy = GetString(row, "CreatedBy");
        return AccountingDefaultProtection.IsSystemDefault(createdBy)
            || AccountingDefaultProtection.ProtectedLedgerGroupNames.Contains(name ?? string.Empty);
    }

    if (table.Equals("Ledgers", StringComparison.OrdinalIgnoreCase))
    {
        var name = GetString(row, "Name");
        var createdBy = GetString(row, "CreatedBy");
        return AccountingDefaultProtection.IsSystemDefault(createdBy)
            || AccountingDefaultProtection.ProtectedLedgerNames.Contains(name ?? string.Empty);
    }

    return false;
}

private static string? GetString(JsonElement row, string propertyName)
{
    foreach (var property in row.EnumerateObject())
    {
        if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
        {
            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();
        }
    }

    return null;
}

private static bool TryGetGuid(JsonElement row, string propertyName, out Guid value)
{
    value = Guid.Empty;
    var text = GetString(row, propertyName);
    return Guid.TryParse(text, out value);
}

private static async Task<string> NormalizeLedgerGroupReferenceAsync(
    GarmetixDbContext db,
    JsonElement row,
    string rowJson,
    IReadOnlyDictionary<Guid, (Guid CompanyId, string Name)> protectedLedgerGroupByOldId,
    CancellationToken cancellationToken)
{
    if (!TryGetGuid(row, "LedgerGroupId", out var oldLedgerGroupId)
        || !protectedLedgerGroupByOldId.TryGetValue(oldLedgerGroupId, out var protectedGroup))
    {
        return rowJson;
    }

    var replacementId = await db.LedgerGroups.AsNoTracking()
        .Where(group => group.CompanyId == protectedGroup.CompanyId && group.Name == protectedGroup.Name)
        .Select(group => group.Id)
        .FirstOrDefaultAsync(cancellationToken);
    if (replacementId == Guid.Empty)
    {
        return rowJson;
    }

    var node = JsonNode.Parse(rowJson)?.AsObject();
    if (node is null)
    {
        return rowJson;
    }

    node["LedgerGroupId"] = replacementId;
    return node.ToJsonString();
}

private static async Task SeedDefaultsForAllCompaniesAsync(GarmetixDbContext db, CancellationToken cancellationToken)
{
    var companyIds = await db.Companies.AsNoTracking()
        .Select(company => company.Id)
        .ToListAsync(cancellationToken);
    var defaultSeeder = new AfssDefaultSeederService(db);
    foreach (var companyId in companyIds)
    {
        await defaultSeeder.SeedAccountingDefaultsForCompanyAsync(companyId, cancellationToken);
    }
}

    private static async Task UpsertRowAsync(
        GarmetixDbContext db,
        string schema,
        string table,
        IReadOnlyList<string> columns,
        string rowJson,
        CancellationToken cancellationToken)
    {
        var qualified = QualifiedName(schema, table);
        var hasId = columns.Any(column => string.Equals(column, "Id", StringComparison.OrdinalIgnoreCase));
        var updateColumns = columns
            .Where(column => !string.Equals(column, "Id", StringComparison.OrdinalIgnoreCase))
            .Where(column => !string.Equals(column, "CreatedAt", StringComparison.OrdinalIgnoreCase))
            .Select(column => $"{Quote(column)} = EXCLUDED.{Quote(column)}")
            .ToList();

        var conflict = hasId
            ? updateColumns.Count > 0
                ? $"ON CONFLICT ({Quote("Id")}) DO UPDATE SET {string.Join(", ", updateColumns)}"
                : $"ON CONFLICT ({Quote("Id")}) DO NOTHING"
            : "ON CONFLICT DO NOTHING";

        var sql = $"INSERT INTO {qualified} SELECT * FROM jsonb_populate_record(NULL::{qualified}, @row::jsonb) {conflict};";
        var parameter = new NpgsqlParameter("row", NpgsqlDbType.Jsonb) { Value = rowJson };
        await db.Database.ExecuteSqlRawAsync(sql, new object[] { parameter }, cancellationToken);
    }

    private static int ExportOrder(string table) => table switch
    {
        "Companies" => 10,
        "StoreGroups" => 20,
        "Stores" => 30,
        "Users" => 40,
        "Banks" => 50,
        "Taxes" => 60,
        "LedgerGroups" => 70,
        "Ledgers" => 80,
        "BankAccounts" => 90,
        "Parties" => 100,
        "Employees" => 110,
        "Salesmen" => 120,
        "ProductCategories" => 130,
        "ProductSubCategories" => 140,
        "Brands" => 150,
        "Vendors" => 160,
        "Products" => 170,
        "Stocks" => 180,
        _ => 1000
    };

    private static async Task EnsureConnectionOpenAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static string QualifiedName(string? schema, string table)
        => string.IsNullOrWhiteSpace(schema) || string.Equals(schema, "public", StringComparison.OrdinalIgnoreCase)
            ? Quote(table)
            : $"{Quote(schema)}.{Quote(table)}";

    private static string Quote(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"")}\"";
}

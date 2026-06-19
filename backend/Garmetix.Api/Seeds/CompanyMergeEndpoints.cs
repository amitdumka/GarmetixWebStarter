using System.Data;
using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Seeds;

public sealed record AfSmartMergeTablePreviewDto(
    string Table,
    string Column,
    int RowCount,
    string Action);

public sealed record AfSmartMergePreviewDto(
    DateTimeOffset CheckedAtUtc,
    bool TargetCompanyExists,
    Guid? TargetCompanyId,
    Guid? SourceCompanyId,
    Guid? TargetStoreGroupId,
    Guid? SourceStoreGroupId,
    Guid? SmartStoreId,
    string Status,
    IReadOnlyList<AfSmartMergeTablePreviewDto> Tables,
    IReadOnlyList<string> Notes);

public sealed record AfSmartMergeApplyRequest(bool Confirm, string? Reason);

public sealed record AfSmartMergeApplyResponse(
    DateTimeOffset AppliedAtUtc,
    int TablesUpdated,
    int RowsUpdated,
    IReadOnlyList<AfSmartMergeTablePreviewDto> Tables,
    IReadOnlyList<string> Notes);

public static class CompanyMergeEndpoints
{
    private const string TargetCompanyName = "Aadwika Fashion";
    private const string TargetCompanyCode = "AF";
    private const string TargetStoreGroupName = "Aadwika Fashion MBO";
    private const string TargetStoreGroupCode = "MBO";
    private const string SmartStoreName = "Smart Menswear";
    private const string SmartStoreCode = "SM01";

    public static RouteGroupBuilder MapCompanyMergeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/company-merge")
            .WithTags("Company Merge")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/af-smart/preview", PreviewAfSmartAsync);
        group.MapPost("/af-smart/apply", ApplyAfSmartAsync);

        return group;
    }

    private static async Task<IResult> PreviewAfSmartAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var context = await BuildMergeContextAsync(db, cancellationToken);
        var tables = await BuildPreviewAsync(db, context, cancellationToken);
        return Results.Ok(new AfSmartMergePreviewDto(
            DateTimeOffset.UtcNow,
            context.TargetCompany is not null,
            context.TargetCompany?.Id,
            context.SourceCompany?.Id,
            context.TargetStoreGroup?.Id,
            context.SourceStoreGroup?.Id,
            context.SmartStore?.Id,
            context.SourceCompany is null ? "Nothing to merge" : "Ready",
            tables,
            BuildNotes(context, apply: false)));
    }

    private static async Task<IResult> ApplyAfSmartAsync(
        AfSmartMergeApplyRequest request,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!request.Confirm)
        {
            return Results.BadRequest(new { message = "Confirm is required before applying Aadwika Fashion + Smart Menswear merge." });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<IResult>(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var context = await BuildMergeContextAsync(db, cancellationToken);
            var notes = BuildNotes(context, apply: true).ToList();

            var targetCompany = context.TargetCompany ?? await CreateTargetCompanyAsync(db, context, cancellationToken);
            var targetStoreGroup = context.TargetStoreGroup ?? await CreateTargetStoreGroupAsync(db, targetCompany, context, cancellationToken);
            var smartStore = context.SmartStore ?? await CreateSmartStoreAsync(db, targetCompany, targetStoreGroup, context, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            context = context with
            {
                TargetCompany = targetCompany,
                TargetStoreGroup = targetStoreGroup,
                SmartStore = smartStore
            };

            var previews = await BuildPreviewAsync(db, context, cancellationToken);
            var updated = 0;
            var tablesUpdated = 0;

            if (context.SourceCompany is not null && context.SourceCompany.Id != targetCompany.Id)
            {
                var changed = await UpdateByScopeColumnAsync(db, "CompanyId", context.SourceCompany.Id, targetCompany.Id, cancellationToken);
                updated += changed.RowsUpdated;
                tablesUpdated += changed.TablesUpdated;
                context.SourceCompany.Deleted = true;
                context.SourceCompany.Active = false;
                db.Entry(context.SourceCompany).State = EntityState.Modified;
            }

            if (context.SourceStoreGroup is not null && context.SourceStoreGroup.Id != targetStoreGroup.Id)
            {
                var changed = await UpdateByScopeColumnAsync(db, "StoreGroupId", context.SourceStoreGroup.Id, targetStoreGroup.Id, cancellationToken);
                updated += changed.RowsUpdated;
                tablesUpdated += changed.TablesUpdated;
                context.SourceStoreGroup.Deleted = true;
                context.SourceStoreGroup.Active = false;
                db.Entry(context.SourceStoreGroup).State = EntityState.Modified;
            }

            smartStore.CompanyId = targetCompany.Id;
            smartStore.StoreGroupId = targetStoreGroup.Id;
            smartStore.Name = SmartStoreName;
            smartStore.StoreCode = SmartStoreCode;
            smartStore.Active = true;
            db.Entry(smartStore).State = EntityState.Modified;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            notes.Add($"Applied by admin. Reason: {(string.IsNullOrWhiteSpace(request.Reason) ? "Not supplied" : request.Reason.Trim())}");
            notes.Add("Aadwika Fashion - Shalini is intentionally not merged.");

            return Results.Ok(new AfSmartMergeApplyResponse(
                DateTimeOffset.UtcNow,
                tablesUpdated,
                updated,
                previews,
                notes));
        });
    }

    private static async Task<MergeContext> BuildMergeContextAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var targetCompany = await db.Companies.FirstOrDefaultAsync(
            item => item.Code == TargetCompanyCode || item.Name == TargetCompanyName,
            cancellationToken);

        var sourceCompany = await db.Companies.FirstOrDefaultAsync(
            item => (item.Code == "SM" || item.Name.Contains("Smart Menswear") || item.Name.Contains("Samrat Menswear"))
                && !item.Name.Contains("Shalini"),
            cancellationToken);

        var targetCompanyId = targetCompany?.Id ?? sourceCompany?.Id ?? Guid.Empty;

        var targetStoreGroup = targetCompanyId == Guid.Empty
            ? null
            : await db.StoreGroups.FirstOrDefaultAsync(
                item => item.CompanyId == targetCompanyId && (item.GroupCode == TargetStoreGroupCode || item.Name == TargetStoreGroupName),
                cancellationToken);

        var sourceStoreGroup = await db.StoreGroups.FirstOrDefaultAsync(
            item => (item.GroupCode == "MBO-SM" || item.Name.Contains("Smart Menswear") || item.Name.Contains("Samrat Menswear"))
                && (sourceCompany == null || item.CompanyId == sourceCompany.Id),
            cancellationToken);

        var smartStore = await db.Stores.FirstOrDefaultAsync(
            item => item.StoreCode == SmartStoreCode
                || item.Name.Contains("Smart Menswear")
                || item.Name.Contains("Samrat Menswear"),
            cancellationToken);

        return new MergeContext(targetCompany, sourceCompany, targetStoreGroup, sourceStoreGroup, smartStore);
    }

    private static async Task<Company> CreateTargetCompanyAsync(GarmetixDbContext db, MergeContext context, CancellationToken cancellationToken)
    {
        var source = context.SourceCompany;
        var company = new Company
        {
            Name = TargetCompanyName,
            Code = TargetCompanyCode,
            Active = true,
            GSTIN = source?.GSTIN ?? string.Empty,
            Pan = source?.Pan ?? string.Empty,
            ContactPerson = source?.ContactPerson ?? "Amit Kumar",
            ContactNumber = source?.ContactNumber ?? string.Empty,
            ContactMobile = source?.ContactMobile ?? string.Empty,
            Email = source?.Email ?? "aadwikafashion@gmail.com",
            Address = source?.Address ?? "Ground Floor, Bhagalpur Road, Dumka",
            City = source?.City ?? "Dumka",
            State = source?.State ?? "Jharkhand",
            Country = source?.Country ?? "India",
            ZipCode = source?.ZipCode ?? "814101",
            StoreCategory = StoreCategory.Cloths,
            CompanyType = CompanyType.Proprietorship,
            StartDate = source?.StartDate ?? DateTime.Today,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);
        return company;
    }

    private static async Task<StoreGroup> CreateTargetStoreGroupAsync(GarmetixDbContext db, Company company, MergeContext context, CancellationToken cancellationToken)
    {
        var source = context.SourceStoreGroup;
        var group = new StoreGroup
        {
            Name = TargetStoreGroupName,
            GroupCode = TargetStoreGroupCode,
            Active = true,
            CompanyId = company.Id,
            StoreCategory = StoreCategory.Cloths,
            StartDate = source?.StartDate ?? DateTime.Today,
        };
        db.StoreGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return group;
    }

    private static async Task<Store> CreateSmartStoreAsync(GarmetixDbContext db, Company company, StoreGroup group, MergeContext context, CancellationToken cancellationToken)
    {
        var source = context.SmartStore;
        var store = new Store
        {
            Name = SmartStoreName,
            StoreCode = SmartStoreCode,
            Active = true,
            CompanyId = company.Id,
            StoreGroupId = group.Id,
            StoreCategory = StoreCategory.Cloths,
            StartDate = source?.StartDate ?? DateTime.Today,
            ContactNumber = source?.ContactNumber ?? string.Empty,
            Email = source?.Email ?? "smartmenswear@aadwikafashion.in",
            Address = source?.Address ?? "Bhagalpur Road, Dumka",
            City = source?.City ?? "Dumka",
            State = source?.State ?? "Jharkhand",
            Country = source?.Country ?? "India",
            ZipCode = source?.ZipCode ?? "814101",
        };
        db.Stores.Add(store);
        await db.SaveChangesAsync(cancellationToken);
        return store;
    }

    private static async Task<List<AfSmartMergeTablePreviewDto>> BuildPreviewAsync(GarmetixDbContext db, MergeContext context, CancellationToken cancellationToken)
    {
        var rows = new List<AfSmartMergeTablePreviewDto>();

        if (context.SourceCompany is not null && context.TargetCompany is not null && context.SourceCompany.Id != context.TargetCompany.Id)
        {
            rows.AddRange(await CountByScopeColumnAsync(db, "CompanyId", context.SourceCompany.Id, "Move to Aadwika Fashion", cancellationToken));
        }

        if (context.SourceStoreGroup is not null && context.TargetStoreGroup is not null && context.SourceStoreGroup.Id != context.TargetStoreGroup.Id)
        {
            rows.AddRange(await CountByScopeColumnAsync(db, "StoreGroupId", context.SourceStoreGroup.Id, "Move to Aadwika Fashion MBO", cancellationToken));
        }

        if (context.SmartStore is not null)
        {
            rows.Add(new AfSmartMergeTablePreviewDto("Stores", "StoreId", 1, "Attach Smart Menswear store to Aadwika Fashion MBO"));
        }

        return rows
            .GroupBy(item => $"{item.Table}.{item.Column}.{item.Action}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First() with { RowCount = group.Sum(item => item.RowCount) })
            .OrderByDescending(item => item.RowCount)
            .ThenBy(item => item.Table)
            .ToList();
    }

    private static async Task<IReadOnlyList<AfSmartMergeTablePreviewDto>> CountByScopeColumnAsync(
        GarmetixDbContext db,
        string columnName,
        Guid oldId,
        string action,
        CancellationToken cancellationToken)
    {
        var output = new List<AfSmartMergeTablePreviewDto>();
        await EnsureConnectionOpenAsync(db, cancellationToken);
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (entityType.FindProperty(columnName) is null)
            {
                continue;
            }

            var table = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(table))
            {
                continue;
            }

            var qualified = QualifiedName(entityType.GetSchema(), table);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {qualified} WHERE {Quote(columnName)} = @oldId";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "oldId";
            parameter.Value = oldId;
            command.Parameters.Add(parameter);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (count > 0)
            {
                output.Add(new AfSmartMergeTablePreviewDto(table, columnName, count, action));
            }
        }

        return output;
    }

    private static async Task<(int TablesUpdated, int RowsUpdated)> UpdateByScopeColumnAsync(
        GarmetixDbContext db,
        string columnName,
        Guid oldId,
        Guid newId,
        CancellationToken cancellationToken)
    {
        var tables = 0;
        var rows = 0;
        await EnsureConnectionOpenAsync(db, cancellationToken);
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (entityType.FindProperty(columnName) is null)
            {
                continue;
            }

            var table = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(table))
            {
                continue;
            }

            var qualified = QualifiedName(entityType.GetSchema(), table);
            var sql = $"UPDATE {qualified} SET {Quote(columnName)} = {{0}} WHERE {Quote(columnName)} = {{1}}";
            var affected = await db.Database.ExecuteSqlRawAsync(sql, new object[] { newId, oldId }, cancellationToken);
            if (affected > 0)
            {
                tables++;
                rows += affected;
            }
        }

        return (tables, rows);
    }

    private static IReadOnlyList<string> BuildNotes(MergeContext context, bool apply)
    {
        var notes = new List<string>
        {
            "Aadwika Fashion - Shalini is excluded from this merge.",
            "Smart Menswear/Samrat Menswear rows are moved under Aadwika Fashion → Aadwika Fashion MBO.",
            apply ? "Apply mode updates CompanyId and StoreGroupId scoped rows." : "Preview mode only counts rows; no data is changed."
        };

        if (context.SourceCompany is null)
        {
            notes.Add("No separate Smart/Samrat company was found. Merge may already be complete.");
        }

        if (context.TargetCompany is null)
        {
            notes.Add("Target Aadwika Fashion company will be created if apply is confirmed.");
        }

        return notes;
    }

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

    private sealed record MergeContext(
        Company? TargetCompany,
        Company? SourceCompany,
        StoreGroup? TargetStoreGroup,
        StoreGroup? SourceStoreGroup,
        Store? SmartStore);
}

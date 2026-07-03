using System.Reflection;
using System.Text.Json;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Core.Models.Audit;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Audit;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/recent", RecentAsync);
        group.MapGet("/events/{auditLogId:guid}", EventDetailAsync);
        group.MapGet("/{entityId:guid}", DetailAsync);

        return group;
    }

    private static async Task<IResult> RecentAsync(
        int? take,
        string? module,
        string? action,
        string? actor,
        string? entity,
        DateTime? from,
        DateTime? to,
        string? search,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, loggerFactory.CreateLogger("DatabaseSchemaRepair"), cancellationToken);

        var limit = Math.Clamp(take ?? 150, 1, 500);
        var persistentRows = await QueryPersistentAuditRowsAsync(db, limit, module, action, actor, entity, from, to, search, cancellationToken);
        if (persistentRows.Count > 0)
        {
            return Results.Ok(persistentRows);
        }

        // Fallback for fresh databases before any SaveChanges audit event has been captured.
        var rows = await BuildLegacyRowsAsync(db, cancellationToken);
        rows = ApplyInMemoryFilters(rows, module, action, actor, entity, from, to, search);

        return Results.Ok(rows
            .OrderByDescending(item => item.OnDate)
            .ThenBy(item => item.Module)
            .Take(limit)
            .ToList());
    }

    private static async Task<IReadOnlyList<AuditActivityDto>> QueryPersistentAuditRowsAsync(
        GarmetixDbContext db,
        int limit,
        string? module,
        string? action,
        string? actor,
        string? entity,
        DateTime? from,
        DateTime? to,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = db.AuditLogEntries.AsNoTracking().Where(item => !item.Deleted);

        if (!string.IsNullOrWhiteSpace(module) && !module.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var filter = module.Trim();
            query = query.Where(item => item.Module == filter);
        }

        if (!string.IsNullOrWhiteSpace(action) && !action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var filter = action.Trim();
            query = query.Where(item => item.Action == filter);
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            var filter = actor.Trim().ToLower();
            query = query.Where(item => item.UserName != null && item.UserName.ToLower().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            var filter = entity.Trim().ToLower();
            query = query.Where(item => item.EntityDisplayName.ToLower().Contains(filter) || item.EntityName.ToLower().Contains(filter));
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.OccurredAt >= from.Value);
        }

        if (to.HasValue)
        {
            var toDate = to.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(item => item.OccurredAt <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var filter = search.Trim().ToLower();
            query = query.Where(item =>
                item.Module.ToLower().Contains(filter) ||
                item.EntityDisplayName.ToLower().Contains(filter) ||
                item.EntityName.ToLower().Contains(filter) ||
                item.Reference.ToLower().Contains(filter) ||
                item.Action.ToLower().Contains(filter) ||
                (item.UserName != null && item.UserName.ToLower().Contains(filter)) ||
                (item.Reason != null && item.Reason.ToLower().Contains(filter)) ||
                (item.RequestPath != null && item.RequestPath.ToLower().Contains(filter)));
        }

        return await query
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.Module)
            .Take(limit)
            .Select(item => new AuditActivityDto(
                item.Module,
                item.EntityDisplayName,
                item.EntityId,
                item.Reference == string.Empty ? item.EntityId.ToString() : item.Reference,
                item.Action,
                item.OccurredAt,
                string.IsNullOrWhiteSpace(item.UserName) ? "System" : item.UserName!,
                item.Id,
                item.Reason,
                item.RequestPath,
                item.ChangedFieldCount))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<AuditActivityDto>> BuildLegacyRowsAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var rows = new List<AuditActivityDto>();

        rows.AddRange(ToDtos(await db.Companies.AsNoTracking()
            .Select(item => new AuditSource("Company", "Company", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, null))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.StoreGroups.AsNoTracking()
            .Select(item => new AuditSource("Company", "Store Group", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, null))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Stores.AsNoTracking()
            .Select(item => new AuditSource("Company", "Store", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, null))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Products.AsNoTracking()
            .Select(item => new AuditSource("Inventory", "Product", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Stocks.AsNoTracking()
            .Select(item => new AuditSource("Inventory", "Stock", item.Id, item.Barcode, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.StockOperationDocuments.AsNoTracking()
            .Select(item => new AuditSource("Inventory", "Stock Operation", item.Id, item.DocumentNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.SalesInvoices.AsNoTracking()
            .Select(item => new AuditSource("Billing", "Sales Invoice", item.Id, item.InvoiceNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.PurchaseInvoices.AsNoTracking()
            .Select(item => new AuditSource("Purchase", "Purchase Invoice", item.Id, item.InvoiceNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.PurchaseReturns.AsNoTracking()
            .Select(item => new AuditSource("Purchase", "Purchase Return", item.Id, item.ReturnNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Vouchers.AsNoTracking()
            .Select(item => new AuditSource("Vouchers", "Voucher", item.Id, item.VoucherNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Ledgers.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Ledger", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Parties.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Party", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.JournalEntries.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Journal Entry", item.Id, item.EntryNumber, item.CreatedAt, item.UpdatedAt, item.PostedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.TailoringOrders.AsNoTracking()
            .Select(item => new AuditSource("Tailoring", "Tailoring Order", item.Id, item.OrderNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Employees.AsNoTracking()
            .Select(item => new AuditSource("HR", "Employee", item.Id, item.FirstName + " " + item.LastName, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.SalaryPaySlips.AsNoTracking()
            .Select(item => new AuditSource("Payroll", "Payslip", item.Id, item.MonthYear, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));

        try
        {
            rows.AddRange(ToDtos(await db.GstReturnDrafts.AsNoTracking()
                .Select(item => new AuditSource("GST Returns", "GST Return Draft", item.Id, item.Title, item.CreatedAt, item.UpdatedAt, item.UpdatedByUserName, item.Deleted))
                .ToListAsync(cancellationToken)));
        }
        catch (Exception)
        {
            // Keep audit available while older volumes are still being repaired.
        }

        return rows;
    }

    private static List<AuditActivityDto> ApplyInMemoryFilters(
        IEnumerable<AuditActivityDto> rows,
        string? module,
        string? action,
        string? actor,
        string? entity,
        DateTime? from,
        DateTime? to,
        string? search)
    {
        var filtered = rows.ToList();
        if (!string.IsNullOrWhiteSpace(module) && !module.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(item => item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(action) && !action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(item => item.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            filtered = filtered.Where(item => item.Actor.Contains(actor.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            filtered = filtered.Where(item => item.Entity.Contains(entity.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (from.HasValue)
        {
            filtered = filtered.Where(item => item.OnDate >= from.Value).ToList();
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(item => item.OnDate <= to.Value.Date.AddDays(1).AddTicks(-1)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(item =>
                item.Module.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Entity.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Reference.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Actor.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return filtered;
    }

    private static async Task<IResult> EventDetailAsync(Guid auditLogId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.AuditLogEntries.AsNoTracking().FirstOrDefaultAsync(item => item.Id == auditLogId, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound();
        }

        var after = ParseSnapshot(entry.AfterJson);
        var before = ParseSnapshot(entry.BeforeJson);
        var fields = (after.Count > 0 ? after : before)
            .Select(item => new AuditFieldDto(item.Key, item.Value ?? string.Empty))
            .OrderBy(item => item.Name)
            .ToList();
        var changes = ParseChanges(entry.ChangesJson);

        return Results.Ok(new AuditDetailDto(
            entry.EntityDisplayName,
            entry.EntityId,
            ReadDateTimeFromSnapshot(before, "CreatedAt") ?? ReadDateTimeFromSnapshot(after, "CreatedAt"),
            ReadDateTimeFromSnapshot(after, "UpdatedAt") ?? entry.UpdatedAt,
            entry.Action.Equals("Deleted", StringComparison.OrdinalIgnoreCase),
            fields,
            changes,
            entry.Id,
            entry.Action,
            string.IsNullOrWhiteSpace(entry.UserName) ? "System" : entry.UserName,
            entry.RequestPath,
            entry.Reason,
            entry.OccurredAt));
    }

    private static async Task<IResult> DetailAsync(Guid entityId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        foreach (var entityType in db.Model.GetEntityTypes().Where(item => item.ClrType.GetProperty("Id")?.PropertyType == typeof(Guid)))
        {
            var entity = await db.FindAsync(entityType.ClrType, new object?[] { entityId }, cancellationToken);
            if (entity is null)
            {
                continue;
            }

            var fields = entityType.ClrType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => IsSimple(property.PropertyType))
                .Where(property => !IsSensitiveField(property.Name))
                .Select(property => new AuditFieldDto(property.Name, property.GetValue(entity)?.ToString() ?? string.Empty))
                .OrderBy(field => field.Name)
                .ToList();

            var createdAt = ReadDateTime(entity, "CreatedAt");
            var updatedAt = ReadDateTime(entity, "UpdatedAt");
            var deleted = ReadBool(entity, "Deleted");
            var changedFields = new List<AuditFieldChangeDto>
            {
                new("CreatedAt", string.Empty, createdAt?.ToString("s") ?? string.Empty),
                new("UpdatedAt", createdAt?.ToString("s") ?? string.Empty, updatedAt?.ToString("s") ?? string.Empty),
                new("Deleted", "False", deleted.ToString())
            };

            return Results.Ok(new AuditDetailDto(
                entityType.ClrType.Name,
                entityId,
                createdAt,
                updatedAt,
                deleted,
                fields,
                changedFields));
        }

        return Results.NotFound();
    }

    private static bool IsSimple(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime);
    }

    private static bool IsSensitiveField(string propertyName)
        => propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("SigningKey", StringComparison.OrdinalIgnoreCase)
            || propertyName.Contains("ApiKey", StringComparison.OrdinalIgnoreCase);

    private static DateTime? ReadDateTime(object entity, string propertyName)
    {
        var value = entity.GetType().GetProperty(propertyName)?.GetValue(entity);
        return value is DateTime date ? date : null;
    }

    private static DateTime? ReadDateTimeFromSnapshot(IReadOnlyDictionary<string, string?> snapshot, string field)
        => snapshot.TryGetValue(field, out var value) && DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static bool ReadBool(object entity, string propertyName)
    {
        var value = entity.GetType().GetProperty(propertyName)?.GetValue(entity);
        return value is bool flag && flag;
    }

    private static IReadOnlyDictionary<string, string?> ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }

    private static List<AuditFieldChangeDto> ParseChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<AuditFieldChangeDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IEnumerable<AuditActivityDto> ToDtos(IEnumerable<AuditSource> sources)
    {
        return sources.Select(ToDto);
    }

    private static AuditActivityDto ToDto(AuditSource source)
    {
        var updatedAt = source.UpdatedAt ?? source.CreatedAt;
        var action = source.Deleted
            ? "Deleted"
            : Math.Abs((updatedAt - source.CreatedAt).TotalSeconds) <= 2 ? "Created" : "Updated";

        return new AuditActivityDto(
            source.Module,
            source.Entity,
            source.Id,
            string.IsNullOrWhiteSpace(source.Reference) ? source.Id.ToString() : source.Reference,
            action,
            updatedAt,
            string.IsNullOrWhiteSpace(source.Actor) ? "System" : source.Actor);
    }

    private sealed record AuditSource(
        string Module,
        string Entity,
        Guid Id,
        string? Reference,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string? Actor,
        bool Deleted = false);
}

public sealed record AuditActivityDto(
    string Module,
    string Entity,
    Guid EntityId,
    string Reference,
    string Action,
    DateTime OnDate,
    string Actor,
    Guid? AuditLogId = null,
    string? Reason = null,
    string? RequestPath = null,
    int ChangedFieldCount = 0);

public sealed record AuditDetailDto(
    string Entity,
    Guid EntityId,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    bool Deleted,
    IReadOnlyList<AuditFieldDto> Fields,
    IReadOnlyList<AuditFieldChangeDto> ChangedFields,
    Guid? AuditLogId = null,
    string? Action = null,
    string? Actor = null,
    string? RequestPath = null,
    string? Reason = null,
    DateTime? OccurredAt = null);

public sealed record AuditFieldDto(string Name, string Value);

public sealed record AuditFieldChangeDto(string Field, string? Before, string? After);

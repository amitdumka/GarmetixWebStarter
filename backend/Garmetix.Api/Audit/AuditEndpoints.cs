using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Garmetix.Api.Audit;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/recent", RecentAsync);
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
        rows.AddRange(ToDtos(await db.PurchaseReturnItcReversals.AsNoTracking()
            .Select(item => new AuditSource("Purchase", "ITC Reversal", item.Id, item.ReturnNumber + " / " + item.ProductName, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Vouchers.AsNoTracking()
            .Select(item => new AuditSource("Vouchers", "Voucher", item.Id, item.VoucherNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.LedgerGroups.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Ledger Group", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Ledgers.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Ledger", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Parties.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Party", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.BankAccounts.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Bank Account", item.Id, item.AccountNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.BankTransactions.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Bank Transaction", item.Id, item.Reference, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.ChequeLogs.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Cheque Log", item.Id, item.ChequeNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.JournalEntries.AsNoTracking()
            .Select(item => new AuditSource("Accounting", "Journal Entry", item.Id, item.EntryNumber, item.CreatedAt, item.UpdatedAt, item.PostedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos((await db.PettyCashSheets.AsNoTracking().ToListAsync(cancellationToken))
            .Select(item => new AuditSource("Petty Cash", "Cash Sheet", item.Id, item.OnDate.ToString("yyyy-MM-dd"), item.CreatedAt, item.UpdatedAt, item.CreatedBy))));
        rows.AddRange(ToDtos(await db.Employees.AsNoTracking()
            .Select(item => new AuditSource("HR", "Employee", item.Id, item.FirstName + " " + item.LastName, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos((await db.Attendance.AsNoTracking().ToListAsync(cancellationToken))
            .Select(item => new AuditSource("HR", "Attendance", item.Id, item.OnDate.ToString("yyyy-MM-dd"), item.CreatedAt, item.UpdatedAt, item.CreatedBy))));
        rows.AddRange(ToDtos((await db.MonthlyAttendance.AsNoTracking().ToListAsync(cancellationToken))
            .Select(item => new AuditSource("HR", "Monthly Attendance", item.Id, item.OnDate.ToString("yyyy-MM"), item.CreatedAt, item.UpdatedAt, item.CreatedBy))));
        rows.AddRange(ToDtos((await db.SalaryStructures.AsNoTracking().ToListAsync(cancellationToken))
            .Select(item => new AuditSource("Payroll", "Salary Structure", item.Id, item.EmployeeId.ToString(), item.CreatedAt, item.UpdatedAt, item.CreatedBy))));
        rows.AddRange(ToDtos(await db.SalaryPaySlips.AsNoTracking()
            .Select(item => new AuditSource("Payroll", "Payslip", item.Id, item.MonthYear, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.SalaryPayments.AsNoTracking()
            .Select(item => new AuditSource("Payroll", "Salary Payment", item.Id, item.VoucherNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy, item.Deleted))
            .ToListAsync(cancellationToken)));
        try
        {
            rows.AddRange(ToDtos(await db.GstReturnDrafts.AsNoTracking()
                .Select(item => new AuditSource("GST Returns", "GST Return Draft", item.Id, item.Title, item.CreatedAt, item.UpdatedAt, item.UpdatedByUserName, item.Deleted))
                .ToListAsync(cancellationToken)));
            rows.AddRange(ToDtos(await db.GstReturnAuditEntries.AsNoTracking()
                .Select(item => new AuditSource("GST Returns", "GST Return Audit", item.Id, item.Action + " " + item.ReturnPeriod, item.CreatedAt, item.UpdatedAt, item.ActorName, item.Deleted))
                .ToListAsync(cancellationToken)));
        }
        catch (Exception)
        {
            // Keep the audit screen available even if an older database volume is still being repaired.
            // The repair service above creates these tables idempotently for the next request.
        }

        if (!string.IsNullOrWhiteSpace(module) && !module.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(item => item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(action) && !action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(item => item.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(actor))
        {
            rows = rows.Where(item => item.Actor.Contains(actor.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(entity))
        {
            rows = rows.Where(item => item.Entity.Contains(entity.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (from.HasValue)
        {
            rows = rows.Where(item => item.OnDate >= from.Value).ToList();
        }

        if (to.HasValue)
        {
            rows = rows.Where(item => item.OnDate <= to.Value.Date.AddDays(1).AddTicks(-1)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            rows = rows.Where(item =>
                item.Module.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Entity.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Reference.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Actor.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Results.Ok(rows
            .OrderByDescending(item => item.OnDate)
            .ThenBy(item => item.Module)
            .Take(limit)
            .ToList());
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

    private static DateTime? ReadDateTime(object entity, string propertyName)
    {
        var value = entity.GetType().GetProperty(propertyName)?.GetValue(entity);
        return value is DateTime date ? date : null;
    }

    private static bool ReadBool(object entity, string propertyName)
    {
        var value = entity.GetType().GetProperty(propertyName)?.GetValue(entity);
        return value is bool flag && flag;
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
    string Actor);

public sealed record AuditDetailDto(
    string Entity,
    Guid EntityId,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    bool Deleted,
    IReadOnlyList<AuditFieldDto> Fields,
    IReadOnlyList<AuditFieldChangeDto> ChangedFields);

public sealed record AuditFieldDto(string Name, string Value);

public sealed record AuditFieldChangeDto(string Field, string Before, string After);

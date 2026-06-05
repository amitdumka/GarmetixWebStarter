using Garmetix.Api.Auth;
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

        return group;
    }

    private static async Task<IResult> RecentAsync(
        int? take,
        string? module,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 150, 1, 500);
        var rows = new List<AuditActivityDto>();

        rows.AddRange(ToDtos(await db.Companies.AsNoTracking()
            .Select(item => new AuditSource("Setup", "Company", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, null))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.StoreGroups.AsNoTracking()
            .Select(item => new AuditSource("Setup", "Store Group", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, null))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Stores.AsNoTracking()
            .Select(item => new AuditSource("Setup", "Store", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, null))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Products.AsNoTracking()
            .Select(item => new AuditSource("Inventory", "Product", item.Id, item.Name, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Stocks.AsNoTracking()
            .Select(item => new AuditSource("Inventory", "Stock", item.Id, item.Barcode, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.SalesInvoices.AsNoTracking()
            .Select(item => new AuditSource("Billing", "Sales Invoice", item.Id, item.InvoiceNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.PurchaseInvoices.AsNoTracking()
            .Select(item => new AuditSource("Purchase", "Purchase Invoice", item.Id, item.InvoiceNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));
        rows.AddRange(ToDtos(await db.Vouchers.AsNoTracking()
            .Select(item => new AuditSource("Vouchers", "Voucher", item.Id, item.VoucherNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
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
            .Select(item => new AuditSource("Payroll", "Salary Payment", item.Id, item.VoucherNumber, item.CreatedAt, item.UpdatedAt, item.CreatedBy))
            .ToListAsync(cancellationToken)));

        if (!string.IsNullOrWhiteSpace(module) && !module.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Where(item => item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Results.Ok(rows
            .OrderByDescending(item => item.OnDate)
            .ThenBy(item => item.Module)
            .Take(limit)
            .ToList());
    }

    private static IEnumerable<AuditActivityDto> ToDtos(IEnumerable<AuditSource> sources)
    {
        return sources.Select(ToDto);
    }

    private static AuditActivityDto ToDto(AuditSource source)
    {
        var updatedAt = source.UpdatedAt ?? source.CreatedAt;
        var action = Math.Abs((updatedAt - source.CreatedAt).TotalSeconds) <= 2 ? "Created" : "Updated";

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
        string? Actor);
}

public sealed record AuditActivityDto(
    string Module,
    string Entity,
    Guid EntityId,
    string Reference,
    string Action,
    DateTime OnDate,
    string Actor);

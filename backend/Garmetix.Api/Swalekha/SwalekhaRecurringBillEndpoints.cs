using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaRecurringBillDto(
    Guid Id,
    string Name,
    decimal Amount,
    int DueDayOfMonth,
    string? Category,
    bool IsActive,
    string? Notes,
    DateTime? LastPaidDate,
    bool DueThisMonth);

public sealed record SwalekhaRecurringBillPayload(
    string Name,
    decimal Amount,
    int DueDayOfMonth,
    string? Category,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaMarkPaidPayload(DateTime? PaidDate);

/// <summary>
/// PersonalFin_04 - Recurring bill/subscription reminders. Deliberately a lightweight reminder
/// list (due day of month + a manual "mark paid" stamp), not a full recurring-transaction
/// engine - the Owner still logs the actual payment as a normal SwalekhaExpenseEntry.
/// </summary>
public static class SwalekhaRecurringBillEndpoints
{
    public static RouteGroupBuilder MapSwalekhaRecurringBillEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/recurring-bills")
            .WithTags("Swalekha Recurring Bills")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/mark-paid", MarkPaidAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaRecurringBills.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(bill => bill.IsActive);
        }

        var bills = await query.OrderBy(bill => bill.DueDayOfMonth).ThenBy(bill => bill.Name).ToListAsync(cancellationToken);
        return Results.Ok(bills.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateAsync(SwalekhaRecurringBillPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name)) return Results.BadRequest(new { message = "Name is required." });
        if (payload.DueDayOfMonth is < 1 or > 31) return Results.BadRequest(new { message = "Due day of month must be between 1 and 31." });

        var bill = new SwalekhaRecurringBill
        {
            Name = payload.Name.Trim(),
            Amount = payload.Amount,
            DueDayOfMonth = payload.DueDayOfMonth,
            Category = payload.Category,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaRecurringBills.Add(bill);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/recurring-bills/{bill.Id}", ToDto(bill));
    }

    private static async Task<IResult> UpdateAsync(Guid id, SwalekhaRecurringBillPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var bill = await db.SwalekhaRecurringBills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(payload.Name)) return Results.BadRequest(new { message = "Name is required." });
        if (payload.DueDayOfMonth is < 1 or > 31) return Results.BadRequest(new { message = "Due day of month must be between 1 and 31." });

        bill.Name = payload.Name.Trim();
        bill.Amount = payload.Amount;
        bill.DueDayOfMonth = payload.DueDayOfMonth;
        bill.Category = payload.Category;
        bill.IsActive = payload.IsActive;
        bill.Notes = payload.Notes;
        bill.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(bill));
    }

    private static async Task<IResult> DeleteAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var bill = await db.SwalekhaRecurringBills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill is null) return Results.NotFound();

        bill.Deleted = true;
        bill.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkPaidAsync(Guid id, SwalekhaMarkPaidPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var bill = await db.SwalekhaRecurringBills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bill is null) return Results.NotFound();

        bill.LastPaidDate = payload.PaidDate ?? DateTime.UtcNow;
        bill.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(bill));
    }

    private static SwalekhaRecurringBillDto ToDto(SwalekhaRecurringBill bill)
    {
        var today = DateTime.UtcNow.Date;
        var dueThisMonth = bill.LastPaidDate is null
            || bill.LastPaidDate.Value.Year != today.Year
            || bill.LastPaidDate.Value.Month != today.Month;

        return new SwalekhaRecurringBillDto(
            bill.Id, bill.Name, bill.Amount, bill.DueDayOfMonth, bill.Category, bill.IsActive, bill.Notes, bill.LastPaidDate, dueThisMonth);
    }
}

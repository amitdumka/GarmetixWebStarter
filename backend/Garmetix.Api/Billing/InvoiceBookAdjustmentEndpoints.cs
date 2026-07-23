using System.Security.Claims;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

/// <summary>
/// Invoice Books Adjustment: a single-purpose page/endpoint group for correcting a Sale invoice's
/// GST-filing BookDate after the fact (e.g. an advance booking made on 4-Jun-2026 that the business
/// wants to file under the July 2026 GST return instead). BookDate is auto-set to the invoice's own
/// OnDate at creation (GarmetixDbContext.StampInvoiceBookDates) - this endpoint group is the only
/// place it can be changed afterward. It never touches any other invoice field: OnDate (the real
/// sale/payment date), amounts, items and accounting postings are all untouched by a BookDate edit.
/// </summary>
public static class InvoiceBookAdjustmentEndpoints
{
    public static RouteGroupBuilder MapInvoiceBookAdjustmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sale/book-adjustment")
            .WithTags("Invoice Books Adjustment")
            .RequireAuthorization(GarmetixPolicies.InvoiceBookAdjustment);

        group.MapGet("", ListAsync);
        group.MapPut("/{id:guid}", UpdateBookDateAsync);
        return group;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        GarmetixDbContext db,
        int year,
        int month,
        Guid? storeId = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Month must be between 1 and 12." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var term = search?.Trim();

        var query = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.OnDate >= monthStart && item.OnDate < monthEnd);

        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                item.InvoiceNumber.Contains(term) ||
                (item.CustomerName != null && item.CustomerName.Contains(term)) ||
                item.CustomerMobileNumber.Contains(term));
        }

        query = query.OrderByDescending(item => item.OnDate).ThenByDescending(item => item.InvoiceNumber);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var storeIds = rows.Select(item => item.StoreId).Distinct().ToList();
        var storeNames = await db.Stores.AsNoTracking()
            .Where(store => storeIds.Contains(store.Id))
            .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);

        var items = rows.Select(invoice => new InvoiceBookAdjustmentRowDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoice.BookDate,
            invoice.BookDate.Date != invoice.OnDate.Date,
            invoice.StoreId,
            storeNames.GetValueOrDefault(invoice.StoreId, "Store"),
            invoice.CustomerName,
            invoice.CustomerMobileNumber,
            invoice.InvoiceStatus.ToString(),
            invoice.BillAmount,
            invoice.ReturnInvoice,
            invoice.BookDateUpdatedAt,
            invoice.BookDateUpdatedBy)).ToList();

        return Results.Ok(new InvoiceBookAdjustmentListDto(
            year, month, totalCount, page, pageSize,
            items.Count(item => item.IsBookDateAdjusted), items));
    }

    private static async Task<IResult> UpdateBookDateAsync(
        Guid id,
        UpdateInvoiceBookDateRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (request.BookDate == default)
        {
            return Results.BadRequest(new { message = "A valid book date is required." });
        }

        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sale invoice was not found." });
        }

        var userName = context.User.Identity?.Name
            ?? context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst("userName")?.Value;

        invoice.BookDate = request.BookDate.Date;
        invoice.BookDateUpdatedAt = DateTime.Now;
        invoice.BookDateUpdatedBy = userName;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new InvoiceBookAdjustmentRowDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoice.BookDate,
            invoice.BookDate.Date != invoice.OnDate.Date,
            invoice.StoreId,
            null,
            invoice.CustomerName,
            invoice.CustomerMobileNumber,
            invoice.InvoiceStatus.ToString(),
            invoice.BillAmount,
            invoice.ReturnInvoice,
            invoice.BookDateUpdatedAt,
            invoice.BookDateUpdatedBy));
    }
}

public sealed record InvoiceBookAdjustmentRowDto(
    Guid Id,
    string InvoiceNumber,
    DateTime OnDate,
    DateTime BookDate,
    bool IsBookDateAdjusted,
    Guid StoreId,
    string? StoreName,
    string? CustomerName,
    string CustomerMobileNumber,
    string InvoiceStatus,
    decimal BillAmount,
    bool ReturnInvoice,
    DateTime? BookDateUpdatedAt,
    string? BookDateUpdatedBy);

public sealed record InvoiceBookAdjustmentListDto(
    int Year,
    int Month,
    int TotalCount,
    int Page,
    int PageSize,
    int AdjustedCount,
    IReadOnlyList<InvoiceBookAdjustmentRowDto> Items);

public sealed record UpdateInvoiceBookDateRequest(DateTime BookDate);

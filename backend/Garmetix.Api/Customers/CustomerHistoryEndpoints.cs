using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Customers;

/// <summary>Read-only "view customer" data: the customer record plus every sale, return, exchange, payment and credit/debit note.</summary>
public static class CustomerHistoryEndpoints
{
    private const int RowLimit = 500;

    public static RouteGroupBuilder MapCustomerHistoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers")
            .WithTags("Customer History")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("/{id:guid}/history", GetHistoryAsync);
        return group;
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var customer = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (customer is null)
        {
            return Results.NotFound();
        }

        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CustomerId == id)
            .OrderByDescending(item => item.OnDate)
            .Take(RowLimit)
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.StoreId,
                item.ReturnInvoice,
                item.OriginalInvoiceId,
                item.InvoiceStatus,
                item.BillAmount,
                item.PaidAmount,
                item.ItemCount,
                item.Quantity
            })
            .ToListAsync(cancellationToken);

        var originalNumbers = invoices.Where(item => item.OriginalInvoiceId.HasValue).Select(item => item.OriginalInvoiceId!.Value).Distinct().ToList();
        var originalLookup = originalNumbers.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.SalesInvoices.AsNoTracking()
                .Where(item => originalNumbers.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.InvoiceNumber, cancellationToken);
        var storeIds = invoices.Select(item => item.StoreId).Distinct().ToList();
        var storeNames = await db.Stores.AsNoTracking()
            .Where(item => storeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).ToList();
        var payments = invoiceIds.Count == 0
            ? []
            : await db.InvoicePayments.AsNoTracking()
                .Where(item => invoiceIds.Contains(item.InvoiceId))
                .OrderByDescending(item => item.OnDate)
                .Select(item => new { item.Id, item.InvoiceId, item.OnDate, item.Amount, item.PaymentMode, item.ReferenceNumber, item.AdjustmentSourceType })
                .ToListAsync(cancellationToken);
        var invoiceNumberById = invoices.ToDictionary(item => item.Id, item => item.InvoiceNumber);

        var notes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => item.CustomerId == id)
            .OrderByDescending(item => item.OnDate)
            .Take(RowLimit)
            .Select(item => new { item.Id, item.NoteNumber, item.NoteType, item.OnDate, item.Amount, item.IsAdjusted, item.SourceType, item.SourceNumber })
            .ToListAsync(cancellationToken);

        var sales = invoices.Where(item => !item.ReturnInvoice && item.InvoiceStatus != InvoiceStatus.Cancelled).ToList();
        var returns = invoices.Where(item => item.ReturnInvoice).ToList();
        var summary = new
        {
            saleCount = sales.Count,
            salesTotal = sales.Sum(item => item.BillAmount),
            returnCount = returns.Count,
            returnsTotal = returns.Sum(item => item.BillAmount),
            outstandingDue = sales.Sum(item => Math.Max(0, item.BillAmount - item.PaidAmount)),
            lastPurchaseOn = sales.Select(item => (DateTime?)item.OnDate).FirstOrDefault(),
            truncated = invoices.Count >= RowLimit
        };

        return Results.Ok(new
        {
            customer,
            summary,
            invoices = invoices.Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                kind = item.ReturnInvoice ? "Return" : item.OriginalInvoiceId.HasValue ? "Exchange" : "Sale",
                status = item.InvoiceStatus.ToString(),
                store = storeNames.GetValueOrDefault(item.StoreId, string.Empty),
                item.BillAmount,
                item.PaidAmount,
                due = item.ReturnInvoice ? 0m : Math.Max(0, item.BillAmount - item.PaidAmount),
                item.ItemCount,
                item.Quantity,
                originalInvoiceNumber = item.OriginalInvoiceId.HasValue ? originalLookup.GetValueOrDefault(item.OriginalInvoiceId.Value) : null
            }),
            payments = payments.Select(item => new
            {
                item.Id,
                item.OnDate,
                invoiceNumber = invoiceNumberById.GetValueOrDefault(item.InvoiceId, string.Empty),
                paymentMode = item.PaymentMode.ToString(),
                item.Amount,
                item.ReferenceNumber,
                item.AdjustmentSourceType
            }),
            notes = notes.Select(item => new
            {
                item.Id,
                number = item.NoteNumber,
                type = item.NoteType.ToString(),
                item.OnDate,
                item.Amount,
                item.IsAdjusted,
                item.SourceType,
                item.SourceNumber
            })
        });
    }
}

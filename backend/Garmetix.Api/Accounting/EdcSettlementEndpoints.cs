using System.Security.Claims;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

/// <summary>
/// EDC/POS Settlement Reconciliation: matches individual Card/UPI sale-invoice payments posted
/// against a POS/EDC Machine BankAccount (e.g. PhonePePos, PhonePeSpeaker) with the later, delayed,
/// lump-sum bank credit the aggregator actually settles - net of its processing charge.
/// </summary>
public static class EdcSettlementEndpoints
{
    public static RouteGroupBuilder MapEdcSettlementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/edc-settlement")
            .WithTags("EdcSettlement")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/pos-accounts", ListPosMachineAccountsAsync);
        group.MapGet("/outstanding", ListOutstandingAsync);
        group.MapPost("/batches", CreateBatchAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/batches", ListBatchesAsync);
        group.MapGet("/batches/{id:guid}", GetBatchAsync);
        group.MapPost("/batches/{id:guid}/reverse", ReverseBatchAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> ListPosMachineAccountsAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var accounts = await WorkspaceScope.ApplyTo(db.BankAccounts.AsNoTracking(), context)
            .Where(item => item.AccountType == AccountType.PosMachine && item.Active)
            .OrderBy(item => item.AccountHolderName)
            .Select(item => new { item.Id, item.AccountHolderName, item.AccountNumber, item.Branch })
            .ToListAsync(cancellationToken);
        return Results.Ok(accounts);
    }

    private static async Task<IResult> ListOutstandingAsync(
        Guid posMachineAccountId,
        HttpContext context,
        GarmetixDbContext db,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);

        var query = WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
            .Where(item => item.BankAccountId == posMachineAccountId && item.EdcSettlementBatchId == null);
        if (fromDate.HasValue)
        {
            query = query.Where(item => item.OnDate >= fromDate.Value.Date);
        }
        if (toDate.HasValue)
        {
            var inclusiveTo = toDate.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < inclusiveTo);
        }

        var totalGross = await query.SumAsync(item => item.Amount, cancellationToken);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(item => item.OnDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var invoiceIds = rows.Select(item => item.InvoiceId).Distinct().ToList();
        var invoiceLookup = await db.SalesInvoices.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.Id))
            .Select(item => new { item.Id, item.InvoiceNumber, item.CustomerName })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var items = rows.Select(payment =>
        {
            invoiceLookup.TryGetValue(payment.InvoiceId, out var invoice);
            return new EdcOutstandingReceiptDto(
                payment.Id,
                payment.InvoiceId,
                invoice?.InvoiceNumber ?? "-",
                invoice?.CustomerName,
                payment.OnDate,
                payment.Amount,
                payment.PaymentMode.ToString(),
                payment.ReferenceNumber);
        }).ToList();

        return Results.Ok(new EdcOutstandingListDto(totalCount, totalGross, page, pageSize, items));
    }

    private static async Task<IResult> CreateBatchAsync(
        EdcSettlementCreateRequest request,
        HttpContext context,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await accounting.PostEdcSettlementAsync(new EdcSettlementRequest(
                request.CompanyId,
                request.StoreGroupId,
                request.StoreId,
                request.PosMachineAccountId,
                request.RealBankAccountId,
                request.SettlementDate,
                request.NetAmountReceived,
                request.ReferenceNumber,
                request.Remarks,
                request.InvoicePaymentIds), cancellationToken);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ListBatchesAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? posMachineAccountId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var query = WorkspaceScope.ApplyTo(db.EdcSettlementBatches.AsNoTracking(), context);
        if (posMachineAccountId.HasValue && posMachineAccountId.Value != Guid.Empty)
        {
            query = query.Where(item => item.PosMachineAccountId == posMachineAccountId.Value);
        }
        query = query.OrderByDescending(item => item.SettlementDate).ThenByDescending(item => item.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var batches = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var accountIds = batches.SelectMany(item => new[] { item.PosMachineAccountId, item.RealBankAccountId }).Distinct().ToList();
        var accountLookup = await db.BankAccounts.AsNoTracking()
            .Where(item => accountIds.Contains(item.Id))
            .Select(item => new { item.Id, item.AccountHolderName })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var items = batches.Select(batch => new EdcSettlementBatchDto(
            batch.Id,
            batch.PosMachineAccountId,
            accountLookup.TryGetValue(batch.PosMachineAccountId, out var posAcc) ? posAcc.AccountHolderName : "-",
            batch.RealBankAccountId,
            accountLookup.TryGetValue(batch.RealBankAccountId, out var bankAcc) ? bankAcc.AccountHolderName : "-",
            batch.SettlementDate,
            batch.GrossAmount,
            batch.NetAmountReceived,
            batch.ChargeAmount,
            batch.PaymentCount,
            batch.ReferenceNumber,
            batch.Remarks,
            batch.JournalEntryNumber,
            batch.Reversed,
            batch.ReversedAt)).ToList();

        return Results.Ok(new EdcSettlementBatchListDto(totalCount, page, pageSize, items));
    }

    private static async Task<IResult> GetBatchAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.EdcSettlementBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return Results.NotFound(new { message = "Settlement batch was not found." });
        }

        var payments = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.EdcSettlementBatchId == id)
            .OrderBy(item => item.OnDate)
            .ToListAsync(cancellationToken);
        var invoiceIds = payments.Select(item => item.InvoiceId).Distinct().ToList();
        var invoiceLookup = await db.SalesInvoices.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.Id))
            .Select(item => new { item.Id, item.InvoiceNumber, item.CustomerName })
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var receipts = payments.Select(payment =>
        {
            invoiceLookup.TryGetValue(payment.InvoiceId, out var invoice);
            return new EdcOutstandingReceiptDto(
                payment.Id, payment.InvoiceId, invoice?.InvoiceNumber ?? "-", invoice?.CustomerName,
                payment.OnDate, payment.Amount, payment.PaymentMode.ToString(), payment.ReferenceNumber);
        }).ToList();

        return Results.Ok(new
        {
            batch.Id,
            batch.PosMachineAccountId,
            batch.RealBankAccountId,
            batch.SettlementDate,
            batch.GrossAmount,
            batch.NetAmountReceived,
            batch.ChargeAmount,
            batch.PaymentCount,
            batch.ReferenceNumber,
            batch.Remarks,
            batch.JournalEntryId,
            batch.JournalEntryNumber,
            batch.Reversed,
            batch.ReversedAt,
            batch.ReversedBy,
            Receipts = receipts
        });
    }

    private static async Task<IResult> ReverseBatchAsync(
        Guid id,
        HttpContext context,
        AccountingPostingService accounting,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var companyId = WorkspaceScope.ClaimGuid(context, "companyId");
        if (!companyId.HasValue)
        {
            return Results.BadRequest(new { message = "Company scope is required." });
        }

        var reversedBy = context.User.Identity?.Name
            ?? context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst("userName")?.Value;

        try
        {
            await accounting.ReverseEdcSettlementAsync(id, companyId.Value, reversedBy, cancellationToken);
            return Results.Ok(new { message = "Settlement batch reversed." });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}

public sealed record EdcOutstandingReceiptDto(
    Guid Id,
    Guid InvoiceId,
    string InvoiceNumber,
    string? CustomerName,
    DateTime OnDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber);

public sealed record EdcOutstandingListDto(int TotalCount, decimal TotalGross, int Page, int PageSize, IReadOnlyList<EdcOutstandingReceiptDto> Items);

public sealed record EdcSettlementCreateRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid PosMachineAccountId,
    Guid RealBankAccountId,
    DateTime SettlementDate,
    decimal NetAmountReceived,
    string? ReferenceNumber,
    string? Remarks,
    IReadOnlyList<Guid> InvoicePaymentIds);

public sealed record EdcSettlementBatchDto(
    Guid Id,
    Guid PosMachineAccountId,
    string PosMachineAccountName,
    Guid RealBankAccountId,
    string RealBankAccountName,
    DateTime SettlementDate,
    decimal GrossAmount,
    decimal NetAmountReceived,
    decimal ChargeAmount,
    int PaymentCount,
    string? ReferenceNumber,
    string? Remarks,
    string? JournalEntryNumber,
    bool Reversed,
    DateTime? ReversedAt);

public sealed record EdcSettlementBatchListDto(int TotalCount, int Page, int PageSize, IReadOnlyList<EdcSettlementBatchDto> Items);

using Garmetix.Api.Auth;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

public static class AccountingEndpoints
{
    public static RouteGroupBuilder MapAccountingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/accounting")
            .WithTags("Accounting")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapPost("/vouchers", SaveVoucherAsync);
        group.MapPut("/vouchers/{id:guid}", UpdateVoucherAsync);
        group.MapGet("/journal", GetJournalAsync);
        group.MapGet("/trial-balance", GetTrialBalanceAsync);
        group.MapGet("/bank-statement/{bankAccountId:guid}", GetBankStatementAsync);
        group.MapPost("/bank-transactions", SaveBankTransactionAsync);
        group.MapPut("/bank-transactions/{id:guid}", UpdateBankTransactionAsync);
        group.MapDelete("/bank-transactions/{id:guid}", DeleteBankTransactionAsync);

        var vouchers = app.MapGroup("/api/vouchers")
            .WithTags("Voucher")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        vouchers.MapGet("/", ListVouchersAsync);
        vouchers.MapGet("/{id:guid}", GetVoucherAsync);
        vouchers.MapPost("/", SaveVoucherAsync);
        vouchers.MapPut("/{id:guid}", UpdateVoucherAsync);
        vouchers.MapDelete("/{id:guid}", DeleteVoucherAsync);

        return group;
    }

    private static async Task<IResult> ListVouchersAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await db.Vouchers.AsNoTracking()
            .OrderByDescending(voucher => voucher.OnDate)
            .ThenByDescending(voucher => voucher.CreatedAt)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetVoucherAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return voucher is null ? Results.NotFound() : Results.Ok(voucher);
    }

    private static async Task<IResult> SaveVoucherAsync(
        VoucherSaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.SaveVoucherAsync(request, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateVoucherAsync(
        Guid id,
        VoucherSaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        request = request with { Id = id };
        return await SaveVoucherAsync(request, service, cancellationToken);
    }

    private static async Task<IResult> DeleteVoucherAsync(
        Guid id,
        GarmetixDbContext db,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (voucher is null)
        {
            return Results.NotFound();
        }

        var journals = await db.JournalEntries
            .Include(entry => entry.Lines)
            .Where(entry => entry.SourceType == "Voucher" && entry.SourceId == id)
            .ToListAsync(cancellationToken);
        foreach (var journal in journals)
        {
            db.JournalLines.RemoveRange(journal.Lines ?? []);
            db.JournalEntries.Remove(journal);
        }

        var bankTransactions = await db.BankTransactions
            .Where(item => item.Reference == voucher.VoucherNumber)
            .ToListAsync(cancellationToken);
        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToList();
        var statementLines = await db.BankStatementLines
            .Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value))
            .ToListAsync(cancellationToken);
        db.BankStatementLines.RemoveRange(statementLines);
        db.BankTransactions.RemoveRange(bankTransactions);

        var cheques = await db.ChequeLogs
            .Where(item => item.Narration == voucher.VoucherNumber)
            .ToListAsync(cancellationToken);
        db.ChequeLogs.RemoveRange(cheques);

        db.Vouchers.Remove(voucher);
        var bankAccountIds = statementLines.Select(item => item.BankAccountId).Distinct().ToList();
        await db.SaveChangesAsync(cancellationToken);
        foreach (var bankAccountId in bankAccountIds)
        {
            await service.RecalculateBankStatementBalancesAsync(bankAccountId, cancellationToken);
        }

        if (bankAccountIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetJournalAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = await db.JournalEntries.AsNoTracking()
            .OrderByDescending(entry => entry.OnDate)
            .ThenByDescending(entry => entry.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> GetTrialBalanceAsync(
        Guid? companyId,
        DateTime? from,
        DateTime? to,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetTrialBalanceAsync(companyId, from, to, cancellationToken));
    }

    private static async Task<IResult> GetBankStatementAsync(
        Guid bankAccountId,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetBankStatementAsync(bankAccountId, cancellationToken));
    }

    private static async Task<IResult> SaveBankTransactionAsync(
        BankTransactionSaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.SaveBankTransactionAsync(request, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateBankTransactionAsync(
        Guid id,
        BankTransactionSaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        request = request with { Id = id };
        return await SaveBankTransactionAsync(request, service, cancellationToken);
    }

    private static async Task<IResult> DeleteBankTransactionAsync(
        Guid id,
        GarmetixDbContext db,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        var transaction = await db.BankTransactions.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (transaction is null)
        {
            return Results.NotFound();
        }

        var bankAccountId = transaction.BankAccountId;
        var statementLines = await db.BankStatementLines
            .Where(item => item.BankTransactionId == transaction.Id)
            .ToListAsync(cancellationToken);

        db.BankStatementLines.RemoveRange(statementLines);
        db.BankTransactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await service.RecalculateBankStatementBalancesAsync(bankAccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

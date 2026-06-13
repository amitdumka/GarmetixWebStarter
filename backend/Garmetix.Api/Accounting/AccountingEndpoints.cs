using Garmetix.Api.Auth;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Garmetix.Api.Accounting;

public static class AccountingEndpoints
{
    public static RouteGroupBuilder MapAccountingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/accounting")
            .WithTags("Accounting")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapPost("/vouchers", SaveVoucherAsync);
        group.MapPut("/vouchers/{id:guid}", UpdateVoucherAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/journal", GetJournalAsync);
        group.MapGet("/trial-balance", GetTrialBalanceAsync);
        group.MapGet("/ledger-statement/{ledgerId:guid}", GetLedgerStatementAsync);
        group.MapGet("/bank-statement/{bankAccountId:guid}", GetBankStatementAsync);
        group.MapGet("/bank-transactions", ListPostedBankTransactionsAsync);
        group.MapPost("/bank-transactions", SaveBankTransactionAsync);
        group.MapPut("/bank-transactions/{id:guid}", UpdateBankTransactionAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/bank-transactions/{id:guid}", DeleteBankTransactionAsync).RequireAuthorization(GarmetixPolicies.Delete);

        var parties = app.MapGroup("/api/parties")
            .WithTags("Party")
            .RequireAuthorization(GarmetixPolicies.Accounting);
        parties.MapGet("/", ListPartiesAsync);
        parties.MapGet("/{id:guid}", GetPartyAsync);
        parties.MapPost("/", SavePartyAsync);
        parties.MapPut("/{id:guid}", UpdatePartyAsync).RequireAuthorization(GarmetixPolicies.Edit);
        parties.MapDelete("/{id:guid}", DeletePartyAsync).RequireAuthorization(GarmetixPolicies.Delete);

        var bankAccounts = app.MapGroup("/api/bank-accounts")
            .WithTags("BankAccount")
            .RequireAuthorization(GarmetixPolicies.Accounting);
        bankAccounts.MapGet("/", ListBankAccountsAsync);
        bankAccounts.MapGet("/{id:guid}", GetBankAccountAsync);
        bankAccounts.MapPost("/", SaveBankAccountAsync);
        bankAccounts.MapPut("/{id:guid}", UpdateBankAccountAsync).RequireAuthorization(GarmetixPolicies.Edit);
        bankAccounts.MapDelete("/{id:guid}", DeleteBankAccountAsync).RequireAuthorization(GarmetixPolicies.Delete);

        var vouchers = app.MapGroup("/api/vouchers")
            .WithTags("Voucher")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        vouchers.MapGet("/", ListVouchersAsync);
        vouchers.MapGet("/{id:guid}", GetVoucherAsync);
        vouchers.MapGet("/{id:guid}/pdf", DownloadVoucherPdfAsync);
        vouchers.MapPost("/", SaveVoucherAsync);
        vouchers.MapPut("/{id:guid}", UpdateVoucherAsync).RequireAuthorization(GarmetixPolicies.Edit);
        vouchers.MapDelete("/{id:guid}", DeleteVoucherAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> ListPartiesAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await db.Parties.AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetPartyAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return party is null ? Results.NotFound() : Results.Ok(party);
    }

    private static async Task<IResult> SavePartyAsync(
        PartySaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await service.SavePartyAsync(request, null, cancellationToken);
            return Results.Created($"/api/parties/{saved.Id}", saved);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdatePartyAsync(
        Guid id,
        PartySaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await service.SavePartyAsync(request, id, cancellationToken);
            return Results.Ok(saved);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DeletePartyAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var party = await db.Parties.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (party is null)
        {
            return Results.NotFound();
        }

        db.Parties.Remove(party);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListBankAccountsAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await db.BankAccounts.AsNoTracking()
            .OrderBy(item => item.AccountHolderName)
            .ThenBy(item => item.AccountNumber)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetBankAccountAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return account is null ? Results.NotFound() : Results.Ok(account);
    }

    private static async Task<IResult> SaveBankAccountAsync(
        BankAccountSaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await service.SaveBankAccountAsync(request, null, cancellationToken);
            return Results.Created($"/api/bank-accounts/{saved.Id}", saved);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateBankAccountAsync(
        Guid id,
        BankAccountSaveRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var saved = await service.SaveBankAccountAsync(request, id, cancellationToken);
            return Results.Ok(saved);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DeleteBankAccountAsync(
        Guid id,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var account = await db.BankAccounts.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (account is null)
        {
            return Results.NotFound();
        }

        db.BankAccounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListVouchersAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Vouchers.AsNoTracking().AsQueryable();
        query = ApplyUserScope(query, context);
        if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return Results.Ok(await query
            .OrderByDescending(voucher => voucher.OnDate)
            .ThenByDescending(voucher => voucher.CreatedAt)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetVoucherAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var voucher = await ApplyUserScope(db.Vouchers.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return voucher is null ? Results.NotFound() : Results.Ok(voucher);
    }

    private static async Task<IResult> DownloadVoucherPdfAsync(
        Guid id,
        string? format,
        bool? reprint,
        bool? signatures,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var voucher = await ApplyUserScope(db.Vouchers.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (voucher is null)
        {
            return Results.NotFound();
        }

        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == voucher.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == voucher.StoreId, cancellationToken);
        var ledger = voucher.LedgerId.HasValue
            ? await db.Ledgers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == voucher.LedgerId.Value, cancellationToken)
            : null;
        var employee = voucher.EmployeeId.HasValue
            ? await db.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == voucher.EmployeeId.Value, cancellationToken)
            : null;
        var bankAccount = voucher.AccountNumber.HasValue
            ? await db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == voucher.AccountNumber.Value, cancellationToken)
            : null;
        var bank = bankAccount is not null
            ? await db.Banks.AsNoTracking().FirstOrDefaultAsync(item => item.Id == bankAccount.BankId, cancellationToken)
            : null;

        var document = new VoucherPdfModel(
            company?.Name ?? "Garmetix",
            FormatAddress(company?.Address, company?.City, company?.State, company?.ZipCode),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            store?.Name ?? "Store",
            voucher.VoucherNumber,
            voucher.OnDate,
            voucher.VoucherType.ToString(),
            voucher.PartyName,
            voucher.Particulars,
            voucher.Amount,
            voucher.Remarks,
            voucher.SlipNumber,
            voucher.PaymentMode.ToString(),
            voucher.PaymentDetails,
            ledger?.Name ?? "-",
            employee?.StaffName ?? "-",
            bankAccount is null
                ? "-"
                : $"{bank?.Name ?? "Bank"} - {bankAccount.AccountHolderName} - {bankAccount.AccountNumber}",
            Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.Voucher, voucher.Id));

        var pdf = VoucherPdfDocument.Build(
            document,
            string.Equals(format, "a5-one", StringComparison.OrdinalIgnoreCase),
            reprint == true,
            signatures != false);
        var safeNumber = Regex.Replace(voucher.VoucherNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "voucher")}.pdf");
    }

    private static string FormatAddress(params string?[] parts)
    {
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
    }

    private static async Task<IResult> SaveVoucherAsync(
        VoucherSaveRequest request,
        HttpContext context,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureUserScope(request, context);
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
        HttpContext context,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        request = request with { Id = id };
        return await SaveVoucherAsync(request, context, service, cancellationToken);
    }

    private static IQueryable<Voucher> ApplyUserScope(IQueryable<Voucher> query, HttpContext context)
    {
        if (ClaimGuid(context, "companyId") is { } companyId)
        {
            query = query.Where(item => item.CompanyId == companyId);
        }

        if (ClaimGuid(context, "storeGroupId") is { } storeGroupId)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId);
        }

        if (ClaimGuid(context, "storeId") is { } storeId)
        {
            query = query.Where(item => item.StoreId == storeId);
        }

        return query;
    }

    private static void EnsureUserScope(VoucherSaveRequest request, HttpContext context)
    {
        if (ClaimGuid(context, "companyId") is { } companyId && request.CompanyId != companyId)
        {
            throw new InvalidOperationException("The selected company is outside your access scope.");
        }

        if (ClaimGuid(context, "storeGroupId") is { } storeGroupId && request.StoreGroupId != storeGroupId)
        {
            throw new InvalidOperationException("The selected store group is outside your access scope.");
        }

        if (ClaimGuid(context, "storeId") is { } storeId && request.StoreId != storeId)
        {
            throw new InvalidOperationException("The selected store is outside your access scope.");
        }
    }

    private static Guid? ClaimGuid(HttpContext context, string claimName)
    {
        return Guid.TryParse(context.User.FindFirst(claimName)?.Value, out var value) ? value : null;
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

    private static async Task<IResult> GetLedgerStatementAsync(
        Guid ledgerId,
        DateTime? from,
        DateTime? to,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetLedgerStatementAsync(ledgerId, from, to, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
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

    private static async Task<IResult> ListPostedBankTransactionsAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var transactions = await db.BankTransactions.AsNoTracking()
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var transactionIds = transactions.Select(item => item.Id).ToList();
        var bankAccountIds = transactions.Select(item => item.BankAccountId).Distinct().ToList();
        var bankLedgers = await db.BankAccounts.AsNoTracking()
            .Where(item => bankAccountIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.LedgerId, cancellationToken);
        var journals = await db.JournalEntries.AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.SourceType == "BankTransaction" && entry.SourceId.HasValue && transactionIds.Contains(entry.SourceId.Value))
            .ToListAsync(cancellationToken);

        var rows = transactions.Select(transaction =>
        {
            var journal = journals.FirstOrDefault(entry => entry.SourceId == transaction.Id);
            bankLedgers.TryGetValue(transaction.BankAccountId, out var bankLedgerId);
            var contraLine = journal?.Lines?
                .FirstOrDefault(line => bankLedgerId == Guid.Empty || line.LedgerId != bankLedgerId);

            return new BankTransactionRow(
                transaction.Id,
                transaction.CompanyId,
                journal?.StoreGroupId,
                journal?.StoreId,
                transaction.BankAccountId,
                contraLine?.LedgerId,
                contraLine?.PartyId,
                transaction.OnDate,
                transaction.TransactionType,
                transaction.TransactionMode,
                transaction.Narration,
                transaction.Reference,
                transaction.Amount,
                transaction.PersonName);
        });

        return Results.Ok(rows);
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
        var journals = await db.JournalEntries
            .Include(entry => entry.Lines)
            .Where(entry => entry.SourceType == "BankTransaction" && entry.SourceId == transaction.Id)
            .ToListAsync(cancellationToken);
        foreach (var journal in journals)
        {
            db.JournalLines.RemoveRange(journal.Lines ?? []);
            db.JournalEntries.Remove(journal);
        }

        var cheques = await db.ChequeLogs
            .Where(item => item.Narration == transaction.Reference)
            .ToListAsync(cancellationToken);

        db.BankStatementLines.RemoveRange(statementLines);
        db.ChequeLogs.RemoveRange(cheques);
        db.BankTransactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken);
        await service.RecalculateBankStatementBalancesAsync(bankAccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}

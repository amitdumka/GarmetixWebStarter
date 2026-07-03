using Garmetix.Api.Auth;
using Garmetix.Api.Messages;
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
        group.MapGet("/journal/validation", ValidateJournalBalanceAsync);
        group.MapGet("/ledger-sync/status", GetLedgerSyncStatusAsync);
        group.MapPost("/ledger-sync/repair", RepairLedgerSyncAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/financial-year-locks", ListFinancialYearLocksAsync);
        group.MapPost("/financial-year-locks", SaveFinancialYearLockAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/financial-year-locks/{id:guid}/unlock", UnlockFinancialYearAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/audit/recent", ListAccountingAuditAsync);
        group.MapGet("/audit/events/{auditLogId:guid}", GetAccountingAuditEventAsync);
        group.MapGet("/message-logs", ListAccountingMessageLogsAsync);
        group.MapGet("/trial-balance", GetTrialBalanceAsync);
        group.MapGet("/ledger-statement/{ledgerId:guid}", GetLedgerStatementAsync);
        group.MapGet("/bank-statement/{bankAccountId:guid}", GetBankStatementAsync);
        group.MapGet("/bank-reconciliation/{bankAccountId:guid}", GetBankReconciliationAsync);
        group.MapPost("/bank-statement-lines/{id:guid}/reconcile", ReconcileBankStatementLineAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/bank-statement-lines/{id:guid}/unreconcile", UnreconcileBankStatementLineAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/cheque-logs/{id:guid}/lifecycle", UpdateChequeLifecycleAsync).RequireAuthorization(GarmetixPolicies.Edit);
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

    private static async Task<IResult> GetLedgerSyncStatusAsync(
        Guid? companyId,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        var summary = await service.ValidateLedgerSynchronizationAsync(companyId, repair: false, cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> RepairLedgerSyncAsync(
        Guid? companyId,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        var summary = await service.ValidateLedgerSynchronizationAsync(companyId, repair: true, cancellationToken);
        return Results.Ok(summary);
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

    private static string? OperatorName(HttpContext context)
    {
        return context.User.Identity?.Name
            ?? context.User.FindFirst("userName")?.Value
            ?? context.User.FindFirst("sub")?.Value;
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

        if (await db.CashVoucherConversions.AnyAsync(item => item.VoucherId == id, cancellationToken))
        {
            return Results.Conflict(new { message = "Converted vouchers are retained for audit and cannot be deleted." });
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

    private static async Task<IResult> GetBankReconciliationAsync(
        Guid bankAccountId,
        DateTime? from,
        DateTime? to,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetBankReconciliationAsync(bankAccountId, from, to, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ReconcileBankStatementLineAsync(
        Guid id,
        BankStatementReconcileRequest request,
        HttpContext context,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.ReconcileBankStatementLineAsync(id, request, OperatorName(context), cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UnreconcileBankStatementLineAsync(
        Guid id,
        BankStatementReconcileRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UnreconcileBankStatementLineAsync(id, request.Remarks, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateChequeLifecycleAsync(
        Guid id,
        ChequeLifecycleRequest request,
        AccountingPostingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.UpdateChequeLifecycleAsync(id, request, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
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


    private static async Task<IResult> ListFinancialYearLocksAsync(
        Guid? companyId,
        bool? activeOnly,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.FinancialYearLocks.AsNoTracking().AsQueryable();
        if (ClaimGuid(context, "companyId") is { } scopedCompanyId)
        {
            query = query.Where(item => item.CompanyId == scopedCompanyId);
        }
        else if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (activeOnly.GetValueOrDefault())
        {
            query = query.Where(item => item.Active);
        }

        var locks = await query
            .OrderByDescending(item => item.PeriodStart)
            .ThenBy(item => item.StoreGroupId)
            .ThenBy(item => item.StoreId)
            .ToListAsync(cancellationToken);

        return Results.Ok(locks.Select(ToFinancialYearLockRow).ToList());
    }

    private static async Task<IResult> ListAccountingAuditAsync(
        int? take,
        string? module,
        string? action,
        string? entity,
        DateTime? from,
        DateTime? to,
        string? search,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 150, 1, 500);
        var query = db.AuditLogEntries.AsNoTracking()
            .Where(item => !item.Deleted)
            .Where(item => AccountingAuditModules.Contains(item.Module) || AccountingAuditEntities.Contains(item.EntityName));

        if (ClaimGuid(context, "companyId") is { } scopedCompanyId)
        {
            query = query.Where(item => item.CompanyId == null || item.CompanyId == scopedCompanyId);
        }

        if (ClaimGuid(context, "storeGroupId") is { } scopedStoreGroupId)
        {
            query = query.Where(item => item.StoreGroupId == null || item.StoreGroupId == scopedStoreGroupId);
        }

        if (ClaimGuid(context, "storeId") is { } scopedStoreId)
        {
            query = query.Where(item => item.StoreId == null || item.StoreId == scopedStoreId);
        }

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

        if (!string.IsNullOrWhiteSpace(entity))
        {
            var filter = entity.Trim().ToLower();
            query = query.Where(item => item.EntityDisplayName.ToLower().Contains(filter) || item.EntityName.ToLower().Contains(filter));
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.OccurredAt >= from.Value.Date);
        }

        if (to.HasValue)
        {
            var endExclusive = to.Value.Date.AddDays(1);
            query = query.Where(item => item.OccurredAt < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var filter = search.Trim().ToLower();
            query = query.Where(item =>
                item.Module.ToLower().Contains(filter)
                || item.Action.ToLower().Contains(filter)
                || item.EntityName.ToLower().Contains(filter)
                || item.EntityDisplayName.ToLower().Contains(filter)
                || item.Reference.ToLower().Contains(filter)
                || (item.UserName != null && item.UserName.ToLower().Contains(filter))
                || (item.RequestPath != null && item.RequestPath.ToLower().Contains(filter))
                || (item.Reason != null && item.Reason.ToLower().Contains(filter)));
        }

        var rows = await query
            .OrderByDescending(item => item.OccurredAt)
            .ThenBy(item => item.Module)
            .Take(limit)
            .Select(item => new AccountingAuditRow(
                item.Id,
                item.OccurredAt,
                item.Module,
                item.Action,
                item.EntityName,
                item.EntityDisplayName,
                item.EntityId,
                item.Reference,
                item.UserName,
                item.RequestPath,
                item.Reason,
                item.ChangedFieldCount,
                item.TraceIdentifier))
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }

    private static async Task<IResult> GetAccountingAuditEventAsync(
        Guid auditLogId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.AuditLogEntries.AsNoTracking()
            .Where(item => item.Id == auditLogId)
            .Where(item => AccountingAuditModules.Contains(item.Module) || AccountingAuditEntities.Contains(item.EntityName));

        if (ClaimGuid(context, "companyId") is { } scopedCompanyId)
        {
            query = query.Where(item => item.CompanyId == null || item.CompanyId == scopedCompanyId);
        }

        if (ClaimGuid(context, "storeGroupId") is { } scopedStoreGroupId)
        {
            query = query.Where(item => item.StoreGroupId == null || item.StoreGroupId == scopedStoreGroupId);
        }

        if (ClaimGuid(context, "storeId") is { } scopedStoreId)
        {
            query = query.Where(item => item.StoreId == null || item.StoreId == scopedStoreId);
        }

        var item = await query.FirstOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new AccountingAuditDetail(
            item.Id,
            item.OccurredAt,
            item.Module,
            item.Action,
            item.EntityName,
            item.EntityDisplayName,
            item.EntityId,
            item.Reference,
            item.UserName,
            item.RequestPath,
            item.IpAddress,
            item.Reason,
            item.BeforeJson,
            item.AfterJson,
            item.ChangesJson,
            item.ChangedFieldCount,
            item.TraceIdentifier));
    }

    private static async Task<IResult> ListAccountingMessageLogsAsync(
        string? level,
        string? source,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? companyId,
        Guid? storeId,
        bool? success,
        int? take,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var scopedCompanyId = ClaimGuid(context, "companyId") ?? companyId;
        var scopedStoreId = ClaimGuid(context, "storeId") ?? storeId;
        var rows = await logs.SearchAsync(new ApplicationMessageLogQuery(
            level,
            source,
            search,
            fromUtc,
            toUtc,
            scopedCompanyId,
            scopedStoreId,
            success,
            Math.Clamp(take ?? 150, 1, 500)), cancellationToken);

        return Results.Ok(rows.Where(IsAccountingMessageLog).Take(Math.Clamp(take ?? 150, 1, 500)).ToList());
    }

    private static async Task<IResult> SaveFinancialYearLockAsync(
        FinancialYearLockSaveRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            EnsureFinancialYearLockRequestIsValid(request, context);

            var periodStart = request.PeriodStart.Date;
            var periodEnd = request.PeriodEnd.Date;
            var requestId = request.Id ?? Guid.Empty;
            var activeOverlap = await db.FinancialYearLocks.AsNoTracking().AnyAsync(item =>
                item.Active
                && item.CompanyId == request.CompanyId
                && item.Id != requestId
                && item.StoreGroupId == request.StoreGroupId
                && item.StoreId == request.StoreId
                && item.PeriodStart <= periodEnd
                && item.PeriodEnd >= periodStart,
                cancellationToken);
            if (activeOverlap)
            {
                return Results.Conflict(new { message = "An active lock already overlaps this company/store period." });
            }

            var periodLock = request.Id.HasValue
                ? await db.FinancialYearLocks.FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
                : null;
            if (request.Id.HasValue && periodLock is null)
            {
                return Results.NotFound();
            }

            periodLock ??= new FinancialYearLock
            {
                CompanyId = request.CompanyId
            };

            periodLock.CompanyId = request.CompanyId;
            periodLock.StoreGroupId = request.StoreGroupId;
            periodLock.StoreId = request.StoreId;
            periodLock.FinancialYear = request.FinancialYear.Trim();
            periodLock.PeriodStart = periodStart;
            periodLock.PeriodEnd = periodEnd;
            periodLock.LockAccounting = request.LockAccounting;
            periodLock.LockSales = request.LockSales;
            periodLock.LockPurchase = request.LockPurchase;
            periodLock.LockInventory = request.LockInventory;
            periodLock.LockGst = request.LockGst;
            periodLock.LockReason = request.Reason?.Trim();
            periodLock.LockedAt = DateTime.Now;
            periodLock.LockedBy = OperatorName(context);
            periodLock.Active = true;
            periodLock.UnlockedAt = null;
            periodLock.UnlockedBy = null;
            periodLock.UnlockReason = null;
            periodLock.CreatedBy ??= OperatorName(context);

            if (db.Entry(periodLock).State == EntityState.Detached)
            {
                db.FinancialYearLocks.Add(periodLock);
            }

            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToFinancialYearLockRow(periodLock));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UnlockFinancialYearAsync(
        Guid id,
        FinancialYearUnlockRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var periodLock = await db.FinancialYearLocks.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (periodLock is null)
        {
            return Results.NotFound();
        }

        if (ClaimGuid(context, "companyId") is { } scopedCompanyId && periodLock.CompanyId != scopedCompanyId)
        {
            return Results.Forbid();
        }

        periodLock.Active = false;
        periodLock.UnlockedAt = DateTime.Now;
        periodLock.UnlockedBy = OperatorName(context);
        periodLock.UnlockReason = request.Reason?.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToFinancialYearLockRow(periodLock));
    }

    private static async Task<IResult> ValidateJournalBalanceAsync(
        Guid? companyId,
        DateTime? from,
        DateTime? to,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.JournalEntries.AsNoTracking()
            .Include(entry => entry.Lines)
            .AsQueryable();

        if (ClaimGuid(context, "companyId") is { } scopedCompanyId)
        {
            query = query.Where(item => item.CompanyId == scopedCompanyId);
            companyId = scopedCompanyId;
        }
        else if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.OnDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            var endExclusive = to.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < endExclusive);
        }

        var entries = await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var issues = new List<JournalValidationIssue>();
        decimal totalDebit = 0;
        decimal totalCredit = 0;
        foreach (var entry in entries)
        {
            var lines = entry.Lines?.ToList() ?? [];
            var debit = Math.Round(lines.Sum(item => item.Debit), 2, MidpointRounding.AwayFromZero);
            var credit = Math.Round(lines.Sum(item => item.Credit), 2, MidpointRounding.AwayFromZero);
            totalDebit += debit;
            totalCredit += credit;
            var difference = Math.Round(debit - credit, 2, MidpointRounding.AwayFromZero);

            if (lines.Count == 0)
            {
                issues.Add(ToJournalValidationIssue(entry, debit, credit, difference, "Error", "Journal entry has no lines."));
                continue;
            }

            if (difference != 0)
            {
                issues.Add(ToJournalValidationIssue(entry, debit, credit, difference, "Error", "Journal entry debit and credit totals are not equal."));
            }

            if (debit == 0 && credit == 0)
            {
                issues.Add(ToJournalValidationIssue(entry, debit, credit, difference, "Warning", "Journal entry has zero value."));
            }

            if (lines.Any(item => item.Debit < 0 || item.Credit < 0))
            {
                issues.Add(ToJournalValidationIssue(entry, debit, credit, difference, "Error", "Journal line contains a negative debit or credit."));
            }

            if (lines.Any(item => item.Debit > 0 && item.Credit > 0))
            {
                issues.Add(ToJournalValidationIssue(entry, debit, credit, difference, "Error", "Journal line contains both debit and credit values."));
            }
        }

        return Results.Ok(new JournalValidationSummary(
            companyId,
            from?.Date,
            to?.Date,
            entries.Count,
            issues.Count,
            Math.Round(totalDebit, 2, MidpointRounding.AwayFromZero),
            Math.Round(totalCredit, 2, MidpointRounding.AwayFromZero),
            Math.Round(totalDebit - totalCredit, 2, MidpointRounding.AwayFromZero),
            issues));
    }

    private static FinancialYearLockRow ToFinancialYearLockRow(FinancialYearLock item)
        => new(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.FinancialYear,
            item.PeriodStart,
            item.PeriodEnd,
            item.Active,
            item.LockAccounting,
            item.LockSales,
            item.LockPurchase,
            item.LockInventory,
            item.LockGst,
            item.LockedAt,
            item.LockedBy,
            item.LockReason,
            item.UnlockedAt,
            item.UnlockedBy,
            item.UnlockReason);

    private static JournalValidationIssue ToJournalValidationIssue(
        JournalEntry entry,
        decimal debit,
        decimal credit,
        decimal difference,
        string severity,
        string message)
        => new(
            entry.Id,
            entry.EntryNumber,
            entry.OnDate,
            entry.SourceType,
            entry.ReferenceNumber,
            debit,
            credit,
            difference,
            severity,
            message);

    private static void EnsureFinancialYearLockRequestIsValid(FinancialYearLockSaveRequest request, HttpContext context)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new ArgumentException("Company is required.");
        }

        if (ClaimGuid(context, "companyId") is { } scopedCompanyId && request.CompanyId != scopedCompanyId)
        {
            throw new InvalidOperationException("The selected company is outside your access scope.");
        }

        if (ClaimGuid(context, "storeGroupId") is { } scopedStoreGroupId && request.StoreGroupId != scopedStoreGroupId)
        {
            throw new InvalidOperationException("The selected store group is outside your access scope.");
        }

        if (ClaimGuid(context, "storeId") is { } scopedStoreId && request.StoreId != scopedStoreId)
        {
            throw new InvalidOperationException("The selected store is outside your access scope.");
        }

        if (string.IsNullOrWhiteSpace(request.FinancialYear))
        {
            throw new ArgumentException("Financial year name is required.");
        }

        if (request.PeriodStart.Date > request.PeriodEnd.Date)
        {
            throw new ArgumentException("Period start cannot be after period end.");
        }

        if (!request.LockAccounting && !request.LockSales && !request.LockPurchase && !request.LockInventory && !request.LockGst)
        {
            throw new ArgumentException("Select at least one module to lock.");
        }
    }

    private static bool IsAccountingMessageLog(ApplicationMessageLogDto item)
        => AccountingMessageSources.Any(source => item.Source.Contains(source, StringComparison.OrdinalIgnoreCase))
            || AccountingMessageSources.Any(source => item.EventName.Contains(source, StringComparison.OrdinalIgnoreCase))
            || AccountingResourcePrefixes.Any(prefix => !string.IsNullOrWhiteSpace(item.Resource) && item.Resource.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            || AccountingResourcePrefixes.Any(prefix => item.Message.Contains(prefix, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] AccountingAuditModules =
    [
        "Accounting",
        "Vouchers",
        "Purchase",
        "GST Returns",
        "GST",
        "OffBook"
    ];

    private static readonly string[] AccountingAuditEntities =
    [
        "JournalEntry",
        "JournalLine",
        "Ledger",
        "LedgerGroup",
        "Party",
        "Bank",
        "BankAccount",
        "BankAccountDetail",
        "VendorBankAccount",
        "BankTransaction",
        "BankStatementLine",
        "ChequeLog",
        "CommercialNote",
        "FinancialYearLock",
        "Voucher",
        "PettyCashSheet",
        "GstReturnDraft",
        "GstReturnAuditEntry",
        "VendorPayment",
        "VendorSettlement"
    ];

    private static readonly string[] AccountingMessageSources =
    [
        "Accounting",
        "Voucher",
        "PettyCash",
        "GST",
        "Purchase",
        "Vendor",
        "Bank",
        "Cash"
    ];

    private static readonly string[] AccountingResourcePrefixes =
    [
        "/accounting",
        "/vouchers",
        "/petty-cash",
        "/cash-details",
        "/vendor-payments",
        "/vendor-settlements",
        "/gst",
        "accounting/",
        "vouchers/",
        "petty-cash/",
        "gst-"
    ];

}

public sealed record AccountingAuditRow(
    Guid Id,
    DateTime OccurredAt,
    string Module,
    string Action,
    string EntityName,
    string EntityDisplayName,
    Guid EntityId,
    string Reference,
    string? UserName,
    string? RequestPath,
    string? Reason,
    int ChangedFieldCount,
    string? TraceIdentifier);

public sealed record AccountingAuditDetail(
    Guid Id,
    DateTime OccurredAt,
    string Module,
    string Action,
    string EntityName,
    string EntityDisplayName,
    Guid EntityId,
    string Reference,
    string? UserName,
    string? RequestPath,
    string? IpAddress,
    string? Reason,
    string? BeforeJson,
    string? AfterJson,
    string? ChangesJson,
    int ChangedFieldCount,
    string? TraceIdentifier);

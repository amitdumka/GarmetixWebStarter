using Garmetix.Api.Auth;
using Garmetix.Api.Messages;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Authentication;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

public sealed record PettyCashPreparation(
    Guid StoreId,
    DateTime OnDate,
    decimal OpeningBalance,
    decimal Sales,
    decimal Receipts,
    decimal DueReceipts,
    decimal BankWithdrawal,
    decimal Expenses,
    decimal Payments,
    decimal CustomerDue,
    decimal BankDeposit,
    decimal NonCashSale,
    decimal CashInHand,
    string OpeningBalanceSource,
    IReadOnlyList<string> CalculationNotes);

public static class PettyCashEndpoints
{
    private const decimal ReconciliationTolerance = 0.01m;

    public static RouteGroupBuilder MapPettyCashEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/petty-cash-sheets")
            .WithTags("PettyCash")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/", ListAsync);
        group.MapGet("/prepare", PrepareAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/pdf", DownloadPdfAsync);
        group.MapPost("/", SaveAsync);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Delete);
        return group;
    }

    private static async Task<IResult> ListAsync(
        Guid? storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = ApplyUserScope(db.PettyCashSheets.AsNoTracking(), context);
        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return Results.Ok(await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var sheet = await ApplyUserScope(db.PettyCashSheets.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return sheet is null ? Results.NotFound() : Results.Ok(sheet);
    }

    private static async Task<IResult> DownloadPdfAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var sheet = await ApplyUserScope(db.PettyCashSheets.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (sheet is null)
        {
            return Results.NotFound();
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == sheet.StoreId, cancellationToken);
        var company = store is null
            ? null
            : await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == store.CompanyId, cancellationToken);
        var details = await BuildTransactionDetailLinesAsync(db, sheet.StoreId, sheet.OnDate, cancellationToken);
        var pdf = PettyCashPdfDocument.Build(sheet, company?.Name ?? "Garmetix", store?.Name ?? "Store", details);
        return Results.File(pdf, "application/pdf", $"petty-cash-{sheet.OnDate:yyyyMMdd}.pdf");
    }

    private static async Task<IResult> PrepareAsync(
        Guid storeId,
        DateTime onDate,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, storeId))
        {
            return Results.BadRequest(new { message = "The selected store is outside your access scope." });
        }

        if (!await db.Stores.AsNoTracking().AnyAsync(item => item.Id == storeId, cancellationToken))
        {
            return Results.BadRequest(new { message = "Select a valid store." });
        }

        return Results.Ok(await CalculateAsync(db, storeId, onDate.Date, cancellationToken));
    }

    private static Task<IResult> UpdateAsync(
        Guid id,
        PettyCashSheet sheet,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        IEmailSender emailSender,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        sheet.Id = id;
        return SaveCoreAsync(sheet, false, context, db, logs, emailSender, loggerFactory, cancellationToken);
    }

    private static Task<IResult> SaveAsync(
        PettyCashSheet sheet,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        IEmailSender emailSender,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
        => SaveCoreAsync(sheet, true, context, db, logs, emailSender, loggerFactory, cancellationToken);

    private static async Task<IResult> SaveCoreAsync(
        PettyCashSheet sheet,
        bool creating,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        IEmailSender emailSender,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (sheet.StoreId == Guid.Empty || !CanUseStore(context, sheet.StoreId))
        {
            return Results.BadRequest(new { message = "Select a valid store within your access scope." });
        }

        sheet.OnDate = sheet.OnDate.Date;
        var duplicate = await ApplyUserScope(db.PettyCashSheets.AsNoTracking(), context)
            .AnyAsync(item => item.StoreId == sheet.StoreId && item.OnDate == sheet.OnDate && item.Id != sheet.Id, cancellationToken);
        if (duplicate)
        {
            return Results.BadRequest(new { message = "A petty cash sheet already exists for this store and date." });
        }

        PettyCashSheet entity;
        if (creating)
        {
            sheet.Id = sheet.Id == Guid.Empty ? Guid.NewGuid() : sheet.Id;
            entity = sheet;
            db.PettyCashSheets.Add(entity);
        }
        else
        {
            var existing = await ApplyUserScope(db.PettyCashSheets, context)
                .FirstOrDefaultAsync(item => item.Id == sheet.Id, cancellationToken);
            if (existing is null)
            {
                return Results.NotFound();
            }

            entity = existing;
            CopyEditableValues(sheet, entity);
        }

        entity.CashInHand = CalculateCashInHand(entity);
        await db.SaveChangesAsync(cancellationToken);

        var expected = await CalculateAsync(db, entity.StoreId, entity.OnDate, cancellationToken);
        var differences = FindDifferences(entity, expected);
        var alertDeliveryFailed = false;
        if (differences.Count > 0)
        {
            try
            {
                await WriteMismatchAlertAsync(entity, expected, differences, context, db, logs, emailSender, cancellationToken);
            }
            catch (Exception ex)
            {
                alertDeliveryFailed = true;
                loggerFactory.CreateLogger("Garmetix.PettyCash")
                    .LogError(ex, "Petty cash sheet {SheetId} was saved, but its reconciliation alert could not be delivered.", entity.Id);
            }
        }

        return creating
            ? Results.Created($"/api/petty-cash-sheets/{entity.Id}", new { sheet = entity, reconciliationAlert = differences.Count > 0, alertDeliveryFailed, differences })
            : Results.Ok(new { sheet = entity, reconciliationAlert = differences.Count > 0, alertDeliveryFailed, differences });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await ApplyUserScope(db.PettyCashSheets, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        entity.Deleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

private static async Task<IReadOnlyList<PettyCashTransactionLine>> BuildTransactionDetailLinesAsync(
    GarmetixDbContext db,
    Guid storeId,
    DateTime onDate,
    CancellationToken cancellationToken)
{
    var dayStart = onDate.Date;
    var dayEnd = dayStart.AddDays(1);
    var lines = new List<PettyCashTransactionLine>();

    var invoices = await db.SalesInvoices.AsNoTracking()
        .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && !item.ReturnInvoice)
        .Select(item => new { item.Id, item.InvoiceNumber, item.CustomerName, item.BillAmount, item.PaidAmount, item.PaymentMode, item.CreditSale })
        .ToListAsync(cancellationToken);
    var currentInvoiceIds = invoices.Select(item => item.Id).ToHashSet();
    foreach (var invoice in invoices)
    {
        var cashAmount = invoice.PaymentMode == PaymentMode.Cash
            ? invoice.PaidAmount > 0 ? invoice.PaidAmount : invoice.BillAmount
            : 0m;
        if (cashAmount > 0)
        {
            lines.Add(new PettyCashTransactionLine("Income", "Cash Sale", invoice.InvoiceNumber, invoice.CustomerName ?? "Customer", cashAmount));
        }

        var due = Math.Max(0, invoice.BillAmount - invoice.PaidAmount);
        if (due > 0)
        {
            lines.Add(new PettyCashTransactionLine("Adjustment", "Customer Due", invoice.InvoiceNumber, invoice.CustomerName ?? "Customer", due));
        }

        if (invoice.PaymentMode.HasValue && invoice.PaymentMode.Value != PaymentMode.Cash && !invoice.CreditSale)
        {
            var nonCash = invoice.PaidAmount > 0 ? invoice.PaidAmount : invoice.BillAmount;
            if (nonCash > 0)
            {
                lines.Add(new PettyCashTransactionLine("Adjustment", $"Non-cash Sale ({invoice.PaymentMode})", invoice.InvoiceNumber, invoice.CustomerName ?? "Customer", nonCash));
            }
        }
    }

    var cashPayments = await db.InvoicePayments.AsNoTracking()
        .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && item.PaymentMode == PaymentMode.Cash)
        .Select(item => new { item.InvoiceId, item.Amount, item.ReferenceNumber })
        .ToListAsync(cancellationToken);
    foreach (var payment in cashPayments.Where(item => !currentInvoiceIds.Contains(item.InvoiceId)))
    {
        lines.Add(new PettyCashTransactionLine("Income", "Due Receipt", payment.ReferenceNumber ?? payment.InvoiceId.ToString()[..8], "Old invoice cash receipt", payment.Amount));
    }

    var vouchers = await db.Vouchers.AsNoTracking()
        .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && item.PaymentMode == PaymentMode.Cash)
        .Select(item => new { item.VoucherNumber, item.VoucherType, item.PartyName, item.Particulars, item.Amount })
        .ToListAsync(cancellationToken);
    foreach (var voucher in vouchers)
    {
        var category = voucher.VoucherType == VoucherType.Receipt ? "Income" : "Expense";
        lines.Add(new PettyCashTransactionLine(category, $"Voucher {voucher.VoucherType}", voucher.VoucherNumber, $"{voucher.PartyName} {voucher.Particulars}".Trim(), voucher.Amount));
    }

    var cashVouchers = await db.CashVouchers.AsNoTracking()
        .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
        .Select(item => new { item.VoucherNumber, item.VoucherType, item.PartyName, item.Particulars, item.Amount })
        .ToListAsync(cancellationToken);
    foreach (var voucher in cashVouchers)
    {
        var category = voucher.VoucherType == VoucherType.Receipt ? "Income" : "Expense";
        lines.Add(new PettyCashTransactionLine(category, $"Cash Voucher {voucher.VoucherType}", voucher.VoucherNumber, $"{voucher.PartyName} {voucher.Particulars}".Trim(), voucher.Amount));
    }

    var bankCash = await db.BankCashTranscations.AsNoTracking()
        .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
        .Select(item => new { item.TransactionType, item.Amount, item.Naration, item.Reference })
        .ToListAsync(cancellationToken);
    foreach (var entry in bankCash)
    {
        lines.Add(new PettyCashTransactionLine(
            entry.TransactionType == TransactionType.Withdraw ? "Income" : "Expense",
            entry.TransactionType == TransactionType.Withdraw ? "Bank Withdrawal" : "Bank Deposit",
            entry.Reference,
            entry.Naration ?? string.Empty,
            entry.Amount));
    }

    return lines
        .OrderBy(line => line.Category)
        .ThenBy(line => line.Type)
        .ThenBy(line => line.Reference)
        .ToList();
}

    private static async Task<PettyCashPreparation> CalculateAsync(
        GarmetixDbContext db,
        Guid storeId,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var dayStart = onDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var previousDate = dayStart.AddDays(-1);
        var previousSheet = await db.PettyCashSheets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == previousDate, cancellationToken);
        var opening = previousSheet?.CashInHand ?? 0m;

        var invoicePayments = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.InvoiceId, item.PaymentMode, item.Amount })
            .ToListAsync(cancellationToken);

        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && !item.ReturnInvoice)
            .Select(item => new { item.Id, item.BillAmount, item.PaidAmount, item.PaymentMode, item.CreditSale })
            .ToListAsync(cancellationToken);
        var currentInvoiceIds = invoices.Select(item => item.Id).ToHashSet();
        var dueReceipts = invoicePayments
            .Where(item => item.PaymentMode == PaymentMode.Cash && !currentInvoiceIds.Contains(item.InvoiceId))
            .Sum(item => item.Amount);
        var nonCashSales = invoices
            .Where(item => item.PaymentMode.HasValue && item.PaymentMode.Value != PaymentMode.Cash && !item.CreditSale)
            .Sum(item => item.PaidAmount > 0 ? item.PaidAmount : item.BillAmount);
        var customerDue = invoices
            .Where(item => item.CreditSale || item.BillAmount > item.PaidAmount)
            .Sum(item => Math.Max(0, item.BillAmount - item.PaidAmount));

        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && item.PaymentMode == PaymentMode.Cash)
            .Select(item => new { item.VoucherType, item.Amount })
            .ToListAsync(cancellationToken);
        var cashVouchers = await db.CashVouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.VoucherType, item.Amount })
            .ToListAsync(cancellationToken);

        var receipts = vouchers.Where(item => item.VoucherType == VoucherType.Receipt).Sum(item => item.Amount)
            + cashVouchers.Where(item => item.VoucherType == VoucherType.Receipt).Sum(item => item.Amount);
        var payments = vouchers.Where(item => item.VoucherType == VoucherType.Payment).Sum(item => item.Amount)
            + cashVouchers.Where(item => item.VoucherType == VoucherType.Payment).Sum(item => item.Amount);
        var expenses = vouchers.Where(item => item.VoucherType == VoucherType.Expense).Sum(item => item.Amount)
            + cashVouchers.Where(item => item.VoucherType == VoucherType.Expense).Sum(item => item.Amount);

        var bankCash = await db.BankCashTranscations.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.TransactionType, item.Amount })
            .ToListAsync(cancellationToken);
        var bankWithdrawal = bankCash.Where(item => item.TransactionType == TransactionType.Withdraw).Sum(item => item.Amount);
        var bankDeposit = bankCash.Where(item => item.TransactionType == TransactionType.Deposit).Sum(item => item.Amount);

        var sales = invoices.Sum(item => item.BillAmount);
        var cashInHand = opening + sales + receipts + dueReceipts + bankWithdrawal - expenses - payments - customerDue - bankDeposit - nonCashSales;
        var notes = new List<string>
        {
            "Sales use the day's invoice total; credit and non-cash portions are deducted separately.",
            "Cash receipt, payment and expense vouchers are included by voucher type.",
            "Bank cash withdrawals add cash; bank deposits reduce cash."
        };
        if (previousSheet is null)
        {
            notes.Insert(0, $"No petty cash sheet was found for {previousDate:dd MMM yyyy}; opening balance is zero.");
        }

        return new PettyCashPreparation(
            storeId,
            dayStart,
            Round(opening),
            Round(sales),
            Round(receipts),
            Round(dueReceipts),
            Round(bankWithdrawal),
            Round(expenses),
            Round(payments),
            Round(customerDue),
            Round(bankDeposit),
            Round(nonCashSales),
            Round(cashInHand),
            previousSheet is null ? "No previous-day sheet" : $"Closing balance for {previousDate:dd MMM yyyy}",
            notes);
    }

    private static List<object> FindDifferences(PettyCashSheet actual, PettyCashPreparation expected)
    {
        var differences = new List<object>();
        Add(nameof(actual.OpeningBalance), "Opening balance", actual.OpeningBalance, expected.OpeningBalance);
        Add(nameof(actual.Sales), "Sales", actual.Sales, expected.Sales);
        Add(nameof(actual.Receipts), "Receipts", actual.Receipts, expected.Receipts);
        Add(nameof(actual.DueReceipts), "Due receipts", actual.DueReceipts, expected.DueReceipts);
        Add(nameof(actual.BankWithdrawal), "Bank withdrawal", actual.BankWithdrawal, expected.BankWithdrawal);
        Add(nameof(actual.Expenses), "Expenses", actual.Expenses, expected.Expenses);
        Add(nameof(actual.Payments), "Payments", actual.Payments, expected.Payments);
        Add(nameof(actual.CustomerDue), "Customer due", actual.CustomerDue, expected.CustomerDue);
        Add(nameof(actual.BankDeposit), "Bank deposit", actual.BankDeposit, expected.BankDeposit);
        Add(nameof(actual.NonCashSale), "Non-cash sale", actual.NonCashSale, expected.NonCashSale);

        return differences;

        void Add(string field, string label, decimal entered, decimal calculated)
        {
            if (Math.Abs(entered - calculated) > ReconciliationTolerance)
            {
                differences.Add(new { field, label, entered = Round(entered), calculated = Round(calculated), difference = Round(entered - calculated) });
            }
        }
    }

    private static async Task WriteMismatchAlertAsync(
        PettyCashSheet sheet,
        PettyCashPreparation expected,
        IReadOnlyList<object> differences,
        HttpContext context,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        IEmailSender emailSender,
        CancellationToken cancellationToken)
    {
        var store = await db.Stores.AsNoTracking().FirstAsync(item => item.Id == sheet.StoreId, cancellationToken);
        var userName = context.User.Identity?.Name ?? context.User.FindFirst("unique_name")?.Value;
        await logs.WriteAsync(new ApplicationMessageLogCreateRequest(
            ApplicationMessageLogService.LevelWarning,
            "PettyCash",
            "ReconciliationMismatch",
            $"Petty cash values differ from calculated transactions for {store.Name} on {sheet.OnDate:dd MMM yyyy}.",
            System.Text.Json.JsonSerializer.Serialize(new { sheet.Id, sheet.StoreId, sheet.OnDate, expected, differences }),
            store.CompanyId,
            store.StoreGroupId,
            store.Id,
            ClaimGuid(context, "sub"),
            userName,
            $"/petty-cash?sheetId={sheet.Id}",
            Guid.NewGuid(),
            false), cancellationToken);

        if (!emailSender.IsEnabled)
        {
            return;
        }

        var owners = await db.Users.AsNoTracking()
            .Where(user => user.Email != string.Empty
                && (!user.CompanyId.HasValue || user.CompanyId == store.CompanyId)
                && (user.Admin || user.Role == LoginRole.Admin || user.UserType == UserType.Owner))
            .Select(user => new { user.Email, user.Name })
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var owner in owners)
        {
            try
            {
                var subject = $"Garmetix petty cash mismatch - {store.Name} - {sheet.OnDate:dd MMM yyyy}";
                var text = $"Entered petty cash differs from transaction-derived values. Store: {store.Name}. Date: {sheet.OnDate:dd MMM yyyy}. Differences: {System.Text.Json.JsonSerializer.Serialize(differences)}";
                await emailSender.SendAsync(new EmailMessage(owner.Email, owner.Name, subject, $"<p>{System.Net.WebUtility.HtmlEncode(text)}</p>", text), cancellationToken);
            }
            catch (Exception ex)
            {
                await logs.ErrorAsync("PettyCash", "MismatchEmailFailed", "The reconciliation alert was logged, but owner email delivery failed.",
                    new { sheet.Id, owner.Email, error = ex.Message }, store.CompanyId, store.StoreGroupId, store.Id,
                    userName: userName, resource: $"/petty-cash?sheetId={sheet.Id}", cancellationToken: cancellationToken);
            }
        }
    }

    private static decimal CalculateCashInHand(PettyCashSheet sheet)
        => Round(sheet.OpeningBalance + sheet.Sales + sheet.Receipts + sheet.DueReceipts + sheet.BankWithdrawal
            - sheet.Expenses - sheet.Payments - sheet.CustomerDue - sheet.BankDeposit - sheet.NonCashSale);

    private static void CopyEditableValues(PettyCashSheet source, PettyCashSheet target)
    {
        target.StoreId = source.StoreId;
        target.OnDate = source.OnDate;
        target.OpeningBalance = source.OpeningBalance;
        target.Sales = source.Sales;
        target.Receipts = source.Receipts;
        target.DueReceipts = source.DueReceipts;
        target.BankWithdrawal = source.BankWithdrawal;
        target.Expenses = source.Expenses;
        target.Payments = source.Payments;
        target.CustomerDue = source.CustomerDue;
        target.BankDeposit = source.BankDeposit;
        target.NonCashSale = source.NonCashSale;
        target.CreatedBy = source.CreatedBy;
    }

    private static IQueryable<PettyCashSheet> ApplyUserScope(IQueryable<PettyCashSheet> query, HttpContext context)
        => ClaimGuid(context, "storeId") is { } storeId ? query.Where(item => item.StoreId == storeId) : query;

    private static bool CanUseStore(HttpContext context, Guid storeId)
        => ClaimGuid(context, "storeId") is not { } allowedStoreId || allowedStoreId == storeId;

    private static Guid? ClaimGuid(HttpContext context, string claimName)
        => Guid.TryParse(context.User.FindFirst(claimName)?.Value, out var value) ? value : null;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}


using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.StoreDay;

public sealed record CashDetailDto(
    decimal? Amount,
    int N2000,
    int N500,
    int N200,
    int N100,
    int N50,
    int NC20,
    int NC10,
    int NC5,
    int NC2,
    int NC1);

public sealed record StoreDayOpenRequest(
    Guid StoreId,
    DateTime OnDate,
    decimal? OpeningBalance,
    CashDetailDto CashDetail,
    string? Remarks);

public sealed record StoreDayCloseRequest(
    Guid StoreId,
    DateTime OnDate,
    CashDetailDto CashDetail,
    bool UseBookCashIfNoCashDetail = true,
    string? Remarks = null,
    bool ConfirmOpeningBalanceMismatch = false,
    PettyCashSheetDraftDto? PettyCashSheet = null);

public sealed record PettyCashSheetDraftDto(
    decimal OpeningBalance,
    decimal Sales,
    decimal Receipts,
    decimal DueReceipts,
    decimal BankWithdrawal,
    decimal Expenses,
    decimal Payments,
    decimal CustomerDue,
    decimal BankDeposit,
    decimal NonCashSale);

public sealed record StoreHolidayRequest(
    Guid StoreId,
    DateTime OnDate,
    string? Reason);

public sealed record StoreDayReopenRequest(
    Guid StoreId,
    DateTime OnDate,
    string? Reason);

public sealed record StoreDayStatusDto(
    Guid StoreId,
    DateTime OnDate,
    bool IsOpened,
    bool IsClosed,
    bool IsHoliday,
    bool EntryAllowed,
    decimal OpeningBalance,
    decimal BookClosingBalance,
    decimal PhysicalClosingBalance,
    decimal Difference,
    Guid? DayBeginId,
    Guid? DayEndId,
    Guid? OpeningCashDetailId,
    Guid? ClosingCashDetailId,
    Guid? PettyCashSheetId,
    string Message,
    PettyCashBookSummaryDto BookSummary);

public sealed record PettyCashBookSummaryDto(
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
    IReadOnlyList<string> Notes,
    bool HasPreviousPettyCashSheet,
    decimal? PreviousPettyCashClosingBalance,
    DateTime? PreviousPettyCashDate,
    decimal OpeningBalanceDifference,
    bool OpeningBalanceMismatch,
    string OpeningBalanceMismatchMessage);

internal sealed record PreviousPettyCashClosingInfo(
    bool Found,
    decimal? Balance,
    DateTime? OnDate);

public static class StoreDayEndpoints
{
    public static RouteGroupBuilder MapStoreDayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/store-day")
            .WithTags("Store Operations")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("/status", StatusAsync);
        group.MapGet("/book-summary", BookSummaryAsync);
        group.MapPost("/open", OpenAsync).RequireAuthorization();
        group.MapPost("/close", CloseAsync).RequireAuthorization();
        group.MapPost("/reopen", ReopenAsync).RequireAuthorization();
        group.MapPost("/delete-close", DeleteCloseAsync).RequireAuthorization();
        group.MapPost("/holiday", HolidayAsync).RequireAuthorization();

        return group;
    }

    private static async Task<IResult> StatusAsync(
        Guid storeId,
        DateTime? onDate,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, storeId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        return Results.Ok(await BuildStatusAsync(db, storeId, (onDate ?? DateTime.Today).Date, cancellationToken));
    }

    private static async Task<IResult> BookSummaryAsync(
        Guid storeId,
        DateTime onDate,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, storeId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        return Results.Ok(await CalculateBookSummaryAsync(db, storeId, onDate.Date, cancellationToken));
    }

    private static async Task<IResult> OpenAsync(
        StoreDayOpenRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        var day = request.OnDate.Date;
        var existingEnd = await db.DayEnds.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (existingEnd is not null)
        {
            return Results.BadRequest(new { message = "This store day is already closed. Reopen is not allowed from daily operations." });
        }

        var opening = request.OpeningBalance ?? await GetPreviousClosingAsync(db, request.StoreId, day, cancellationToken);
        var cash = await UpsertCashDetailAsync(db, request.StoreId, day, request.CashDetail, "DayOpening", fallbackAmount: opening, cancellationToken);
        var begin = await db.DayBegins
            .FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (begin is null)
        {
            begin = new DayBegin
            {
                StoreId = request.StoreId,
                OnDate = day,
                OpeningBalance = opening,
                CashDetailId = cash.Id,
                CreatedBy = "DayOpening"
            };
            db.DayBegins.Add(begin);
        }
        else
        {
            begin.OpeningBalance = opening;
            begin.CashDetailId = cash.Id;
            begin.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildStatusAsync(db, request.StoreId, day, cancellationToken));
    }

    private static async Task<IResult> CloseAsync(
        StoreDayCloseRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        var day = request.OnDate.Date;
        var begin = await db.DayBegins.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (begin is null)
        {
            return Results.BadRequest(new { message = "Day opening is required before day closing." });
        }

        var summary = await CalculateBookSummaryAsync(db, request.StoreId, day, cancellationToken);
        if (request.PettyCashSheet is not null)
        {
            summary = ApplyPettyCashDraft(summary, request.PettyCashSheet);
        }

        if (summary.OpeningBalanceMismatch && !request.ConfirmOpeningBalanceMismatch)
        {
            return Results.Conflict(new
            {
                message = summary.OpeningBalanceMismatchMessage,
                requiresConfirmation = true,
                summary
            });
        }

        var physicalCash = CashAmount(request.CashDetail);
        if (physicalCash <= 0 && request.UseBookCashIfNoCashDetail)
        {
            physicalCash = summary.CashInHand;
        }

        var cash = await UpsertCashDetailAsync(db, request.StoreId, day, request.CashDetail, "DayClosing", fallbackAmount: physicalCash, cancellationToken);
        var end = await db.DayEnds
            .FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (end is null)
        {
            end = new DayEnd
            {
                StoreId = request.StoreId,
                OnDate = day,
                ClosingBalance = cash.Amount,
                CashDetailId = cash.Id,
                CreatedBy = "DayClosing"
            };
            db.DayEnds.Add(end);
        }
        else
        {
            end.ClosingBalance = cash.Amount;
            end.CashDetailId = cash.Id;
            end.UpdatedAt = DateTime.UtcNow;
        }

        var sheet = await UpsertPettyCashSheetAsync(db, request.StoreId, day, summary, cash.Amount, "DayClosing", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            status = await BuildStatusAsync(db, request.StoreId, day, cancellationToken),
            pettyCashSheetId = sheet.Id,
            printUrl = $"/api/petty-cash-sheets/{sheet.Id}/pdf"
        });
    }

private static Task<IResult> ReopenAsync(
    StoreDayReopenRequest request,
    HttpContext context,
    GarmetixDbContext db,
    CancellationToken cancellationToken)
    => VoidDayCloseAsync(request, context, db, "Reopened", cancellationToken);

private static Task<IResult> DeleteCloseAsync(
    StoreDayReopenRequest request,
    HttpContext context,
    GarmetixDbContext db,
    CancellationToken cancellationToken)
    => VoidDayCloseAsync(request, context, db, "Deleted", cancellationToken);

private static async Task<IResult> VoidDayCloseAsync(
    StoreDayReopenRequest request,
    HttpContext context,
    GarmetixDbContext db,
    string action,
    CancellationToken cancellationToken)
{
    if (!CanUseStore(context, request.StoreId))
    {
        return Results.BadRequest(new { message = "Selected store is outside your access scope." });
    }

    var day = request.OnDate.Date;
    var end = await db.DayEnds.FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
    if (end is null)
    {
        return Results.BadRequest(new { message = "No active day close was found for this store/date." });
    }

    end.Deleted = true;
    end.UpdatedAt = DateTime.UtcNow;
    end.CreatedBy = $"DayClosing{action}";

    var closingCash = await db.CashDetails.FirstOrDefaultAsync(item => item.Id == end.CashDetailId && !item.Deleted, cancellationToken);
    if (closingCash is not null)
    {
        closingCash.Deleted = true;
        closingCash.UpdatedAt = DateTime.UtcNow;
        closingCash.CreatedBy = $"DayClosing{action}";
    }

    var sheet = await db.PettyCashSheets.FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
    if (sheet is not null && (sheet.CreatedBy == "DayClosing" || sheet.CreatedBy == "StoreHoliday"))
    {
        sheet.Deleted = true;
        sheet.UpdatedAt = DateTime.UtcNow;
        sheet.CreatedBy = $"DayClosing{action}";
    }

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await BuildStatusAsync(db, request.StoreId, day, cancellationToken));
}

    private static async Task<IResult> HolidayAsync(
        StoreHolidayRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        var day = request.OnDate.Date;
        var carryForward = await GetPreviousClosingAsync(db, request.StoreId, day, cancellationToken);
        var cashDto = new CashDetailDto(carryForward, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var cash = await UpsertCashDetailAsync(db, request.StoreId, day, cashDto, "StoreHoliday", fallbackAmount: carryForward, cancellationToken);

        var begin = await db.DayBegins.FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (begin is null)
        {
            db.DayBegins.Add(new DayBegin
            {
                StoreId = request.StoreId,
                OnDate = day,
                OpeningBalance = carryForward,
                CashDetailId = cash.Id,
                CreatedBy = "StoreHoliday"
            });
        }
        else
        {
            begin.OpeningBalance = carryForward;
            begin.CashDetailId = cash.Id;
            begin.CreatedBy = "StoreHoliday";
            begin.UpdatedAt = DateTime.UtcNow;
        }

        var previousInfo = await GetPreviousPettyCashClosingInfoAsync(db, request.StoreId, day, cancellationToken);
        var summary = new PettyCashBookSummaryDto(
            carryForward, 0, 0, 0, 0, 0, 0, 0, 0, 0, carryForward,
            "Holiday carry-forward",
            ["Store holiday/closed day: balance carried forward."],
            previousInfo.Found,
            previousInfo.Balance,
            previousInfo.OnDate,
            previousInfo.Balance.HasValue ? Math.Round(carryForward - previousInfo.Balance.Value, 2) : 0m,
            false,
            string.Empty);
        await UpsertPettyCashSheetAsync(db, request.StoreId, day, summary, carryForward, "StoreHoliday", cancellationToken);

        var end = await db.DayEnds.FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (end is null)
        {
            db.DayEnds.Add(new DayEnd
            {
                StoreId = request.StoreId,
                OnDate = day,
                ClosingBalance = carryForward,
                CashDetailId = cash.Id,
                CreatedBy = "StoreHoliday"
            });
        }
        else
        {
            end.ClosingBalance = carryForward;
            end.CashDetailId = cash.Id;
            end.CreatedBy = "StoreHoliday";
            end.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await BuildStatusAsync(db, request.StoreId, day, cancellationToken));
    }

    private static async Task<StoreDayStatusDto> BuildStatusAsync(GarmetixDbContext db, Guid storeId, DateTime day, CancellationToken cancellationToken)
    {
        var begin = await db.DayBegins.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == day && !item.Deleted, cancellationToken);
        var end = await db.DayEnds.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == day && !item.Deleted, cancellationToken);
        var sheet = await db.PettyCashSheets.AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == day && !item.Deleted, cancellationToken);
        var openingCash = begin is null
            ? null
            : await db.CashDetails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == begin.CashDetailId, cancellationToken);
        var closingCash = end is null
            ? null
            : await db.CashDetails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == end.CashDetailId, cancellationToken);
        var summary = await CalculateBookSummaryAsync(db, storeId, day, cancellationToken);
        var isHoliday = string.Equals(begin?.CreatedBy, "StoreHoliday", StringComparison.OrdinalIgnoreCase)
            || string.Equals(end?.CreatedBy, "StoreHoliday", StringComparison.OrdinalIgnoreCase);

        var opened = begin is not null;
        var closed = end is not null;
        var physicalClosing = closingCash?.Amount ?? end?.ClosingBalance ?? 0m;
        var entryAllowed = opened && !closed && !isHoliday;
        var message = isHoliday
            ? "Store holiday/closed day is marked. Entries are blocked for this date."
            : !opened
                ? "Day opening is required before store entries."
                : closed
                    ? "Day is closed. Store entries are blocked."
                    : "Day is open. Store entries are allowed.";

        return new StoreDayStatusDto(
            storeId,
            day,
            opened,
            closed,
            isHoliday,
            entryAllowed,
            begin?.OpeningBalance ?? summary.OpeningBalance,
            summary.CashInHand,
            physicalClosing,
            Math.Round(physicalClosing - summary.CashInHand, 2),
            begin?.Id,
            end?.Id,
            openingCash?.Id,
            closingCash?.Id,
            sheet?.Id,
            message,
            summary);
    }

    private static async Task<CashDetail> UpsertCashDetailAsync(
        GarmetixDbContext db,
        Guid storeId,
        DateTime day,
        CashDetailDto dto,
        string createdBy,
        decimal fallbackAmount,
        CancellationToken cancellationToken)
    {
        var amount = dto.Amount ?? CashAmount(dto);
        if (amount <= 0)
        {
            amount = fallbackAmount;
        }

        var entity = await db.CashDetails.FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == day && item.CreatedBy == createdBy && !item.Deleted, cancellationToken);
        if (entity is null)
        {
            entity = new CashDetail { StoreId = storeId, OnDate = day, CreatedBy = createdBy };
            db.CashDetails.Add(entity);
        }

        entity.Amount = Math.Round(amount, 2);
        entity.N2000 = dto.N2000;
        entity.N500 = dto.N500;
        entity.N200 = dto.N200;
        entity.N100 = dto.N100;
        entity.N50 = dto.N50;
        entity.NC20 = dto.NC20;
        entity.NC10 = dto.NC10;
        entity.NC5 = dto.NC5;
        entity.NC2 = dto.NC2;
        entity.NC1 = dto.NC1;
        entity.UpdatedAt = DateTime.UtcNow;
        return entity;
    }

    private static async Task<PettyCashSheet> UpsertPettyCashSheetAsync(
        GarmetixDbContext db,
        Guid storeId,
        DateTime day,
        PettyCashBookSummaryDto summary,
        decimal physicalCashInHand,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var sheet = await db.PettyCashSheets.FirstOrDefaultAsync(item => item.StoreId == storeId && item.OnDate == day && !item.Deleted, cancellationToken);
        if (sheet is null)
        {
            sheet = new PettyCashSheet { StoreId = storeId, OnDate = day, CreatedBy = createdBy };
            db.PettyCashSheets.Add(sheet);
        }

        sheet.OpeningBalance = summary.OpeningBalance;
        sheet.Sales = summary.Sales;
        sheet.Receipts = summary.Receipts;
        sheet.DueReceipts = summary.DueReceipts;
        sheet.BankWithdrawal = summary.BankWithdrawal;
        sheet.Expenses = summary.Expenses;
        sheet.Payments = summary.Payments;
        sheet.CustomerDue = summary.CustomerDue;
        sheet.BankDeposit = summary.BankDeposit;
        sheet.NonCashSale = summary.NonCashSale;
        sheet.CashInHand = Math.Round(physicalCashInHand, 2);
        sheet.CreatedBy = createdBy;
        sheet.UpdatedAt = DateTime.UtcNow;
        return sheet;
    }

    private static decimal CashAmount(CashDetailDto dto)
        => dto.N2000 * 2000m
           + dto.N500 * 500m
           + dto.N200 * 200m
           + dto.N100 * 100m
           + dto.N50 * 50m
           + dto.NC20 * 20m
           + dto.NC10 * 10m
           + dto.NC5 * 5m
           + dto.NC2 * 2m
           + dto.NC1;

    private static PettyCashBookSummaryDto ApplyPettyCashDraft(PettyCashBookSummaryDto current, PettyCashSheetDraftDto draft)
    {
        var opening = Math.Round(draft.OpeningBalance, 2);
        var sales = Math.Round(draft.Sales, 2);
        var receipts = Math.Round(draft.Receipts, 2);
        var dueReceipts = Math.Round(draft.DueReceipts, 2);
        var bankWithdrawal = Math.Round(draft.BankWithdrawal, 2);
        var expenses = Math.Round(draft.Expenses, 2);
        var payments = Math.Round(draft.Payments, 2);
        var customerDue = Math.Round(draft.CustomerDue, 2);
        var bankDeposit = Math.Round(draft.BankDeposit, 2);
        var nonCashSale = Math.Round(draft.NonCashSale, 2);
        var cashInHand = Math.Round(opening + sales + receipts + dueReceipts + bankWithdrawal - expenses - payments - customerDue - bankDeposit - nonCashSale, 2);
        var difference = current.PreviousPettyCashClosingBalance.HasValue
            ? Math.Round(opening - current.PreviousPettyCashClosingBalance.Value, 2)
            : 0m;
        var mismatch = current.HasPreviousPettyCashSheet && Math.Abs(difference) > 0.01m;
        return current with
        {
            OpeningBalance = opening,
            Sales = sales,
            Receipts = receipts,
            DueReceipts = dueReceipts,
            BankWithdrawal = bankWithdrawal,
            Expenses = expenses,
            Payments = payments,
            CustomerDue = customerDue,
            BankDeposit = bankDeposit,
            NonCashSale = nonCashSale,
            CashInHand = cashInHand,
            OpeningBalanceSource = "Day opening / closing preview override",
            OpeningBalanceDifference = difference,
            OpeningBalanceMismatch = mismatch,
            OpeningBalanceMismatchMessage = BuildOpeningMismatchMessage(current.PreviousPettyCashDate, current.PreviousPettyCashClosingBalance, opening, difference),
            Notes = current.Notes.Concat(["Petty cash sheet values were reviewed/edited in Day Closing preview before save."]).ToArray()
        };
    }

    private static string BuildOpeningMismatchMessage(DateTime? previousDate, decimal? previousBalance, decimal openingBalance, decimal difference)
    {
        if (!previousBalance.HasValue)
        {
            return string.Empty;
        }

        var dateText = previousDate.HasValue ? previousDate.Value.ToString("dd MMM yyyy") : "previous petty cash sheet";
        return $"Today opening balance ₹{openingBalance:N2} differs from previous petty cash closing ₹{previousBalance.Value:N2} ({dateText}) by ₹{difference:N2}. Confirm this difference before day closing.";
    }

    private static async Task<decimal> GetPreviousClosingAsync(GarmetixDbContext db, Guid storeId, DateTime day, CancellationToken cancellationToken)
    {
        var previousEnd = await db.DayEnds.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate < day && !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .Select(item => (decimal?)item.ClosingBalance)
            .FirstOrDefaultAsync(cancellationToken);
        if (previousEnd.HasValue)
        {
            return Math.Round(previousEnd.Value, 2);
        }

        var previousSheet = await GetPreviousPettyCashClosingInfoAsync(db, storeId, day, cancellationToken);
        return Math.Round(previousSheet.Balance ?? 0m, 2);
    }

    private static async Task<PreviousPettyCashClosingInfo> GetPreviousPettyCashClosingInfoAsync(
        GarmetixDbContext db,
        Guid storeId,
        DateTime day,
        CancellationToken cancellationToken)
    {
        var previousSheet = await db.PettyCashSheets.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate < day && !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .Select(item => new { item.OnDate, item.CashInHand })
            .FirstOrDefaultAsync(cancellationToken);

        return previousSheet is null
            ? new PreviousPettyCashClosingInfo(false, null, null)
            : new PreviousPettyCashClosingInfo(true, Math.Round(previousSheet.CashInHand, 2), previousSheet.OnDate.Date);
    }

    private static async Task<PettyCashBookSummaryDto> CalculateBookSummaryAsync(GarmetixDbContext db, Guid storeId, DateTime onDate, CancellationToken cancellationToken)
    {
        var dayStart = onDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var dayBegin = await db.DayBegins.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate == dayStart && !item.Deleted)
            .Select(item => new { item.OpeningBalance })
            .FirstOrDefaultAsync(cancellationToken);
        var previousPettyCash = await GetPreviousPettyCashClosingInfoAsync(db, storeId, dayStart, cancellationToken);
        var opening = Math.Round(dayBegin?.OpeningBalance ?? previousPettyCash.Balance ?? 0m, 2);
        var openingSource = dayBegin is not null
            ? "Today day opening"
            : previousPettyCash.Found
                ? "Previous petty cash closing fallback"
                : "No previous petty cash sheet; opening is zero until day open";
        var openingDifference = previousPettyCash.Balance.HasValue
            ? Math.Round(opening - previousPettyCash.Balance.Value, 2)
            : 0m;
        var openingMismatch = dayBegin is not null && previousPettyCash.Found && Math.Abs(openingDifference) > 0.01m;
        var openingMismatchMessage = openingMismatch
            ? BuildOpeningMismatchMessage(previousPettyCash.OnDate, previousPettyCash.Balance, opening, openingDifference)
            : string.Empty;

        var invoicePayments = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd)
            .Select(item => new { item.InvoiceId, item.PaymentMode, item.Amount })
            .ToListAsync(cancellationToken);

        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= dayStart && item.OnDate < dayEnd && !item.ReturnInvoice)
            .Select(item => new { item.Id, item.BillAmount, item.PaidAmount, item.PaymentMode, item.CreditSale })
            .ToListAsync(cancellationToken);
        var currentInvoiceIds = invoices.Select(item => item.Id).ToHashSet();
        var dueReceipts = invoicePayments.Where(item => item.PaymentMode == PaymentMode.Cash && !currentInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.Amount);
        var nonCashSales = invoices.Where(item => item.PaymentMode.HasValue && item.PaymentMode.Value != PaymentMode.Cash && !item.CreditSale).Sum(item => item.PaidAmount > 0 ? item.PaidAmount : item.BillAmount);
        var customerDue = invoices.Where(item => item.CreditSale || item.BillAmount > item.PaidAmount).Sum(item => Math.Max(0, item.BillAmount - item.PaidAmount));

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
            dayBegin is not null
                ? "Opening balance is taken from today's Day Open entry. Previous petty cash closing is used only as a warning/control check."
                : "No Day Open entry was found; previous petty cash closing is shown only as a fallback until the day is opened.",
            "Day closing shows a petty cash preview before final save so calculated values can be reviewed and corrected."
        };
        if (openingMismatch)
        {
            notes.Add(openingMismatchMessage);
        }
        if (!previousPettyCash.Found)
        {
            notes.Add("No previous petty cash sheet was found; today's Day Open amount is authoritative.");
        }

        return new PettyCashBookSummaryDto(
            Math.Round(opening, 2),
            Math.Round(sales, 2),
            Math.Round(receipts, 2),
            Math.Round(dueReceipts, 2),
            Math.Round(bankWithdrawal, 2),
            Math.Round(expenses, 2),
            Math.Round(payments, 2),
            Math.Round(customerDue, 2),
            Math.Round(bankDeposit, 2),
            Math.Round(nonCashSales, 2),
            Math.Round(cashInHand, 2),
            openingSource,
            notes,
            previousPettyCash.Found,
            previousPettyCash.Balance,
            previousPettyCash.OnDate,
            openingDifference,
            openingMismatch,
            openingMismatchMessage);
    }

    private static bool CanUseStore(HttpContext context, Guid storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return true;
        }

        var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
        return !claimStoreId.HasValue || claimStoreId.Value == storeId;
    }
}

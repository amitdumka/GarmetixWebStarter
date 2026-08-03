using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Garmetix.Api.StoreDay;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Printing;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.DotMatrix;

public sealed class DotMatrixJournalService(GarmetixDbContext db, IOptions<DotMatrixPrintingOptions> options, ILogger<DotMatrixJournalService> logger)
{
    public async Task<DotMatrixPrintQueueEntry> QueueTestPrintAsync(Guid storeId, string? message, CancellationToken cancellationToken)
    {
        var setting = await ResolveSettingAsync(storeId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        var nowUtc = NowUtc();
        var nowIst = ToBusinessTime(nowUtc, setting.TimeZoneId);
        var width = setting.LineWidth;
        var text = string.Join(Environment.NewLine, new[]
        {
            DotMatrixTextFormatter.Rule(width),
            DotMatrixTextFormatter.Center(width, "GARMETIX DOT MATRIX TEST PRINT"),
            DotMatrixTextFormatter.Rule(width, '-'),
            $"Store: {store?.Name ?? "Store"} / {store?.StoreCode ?? storeId.ToString("N")[..6]}",
            $"Printed At: {nowIst:dd-MM-yyyy hh:mm tt} IST",
            $"Printer: {setting.PrinterName}",
            $"Mode: {setting.OutputMode} Width: {width}",
            DotMatrixTextFormatter.Clean(message) is { Length: > 0 } clean ? $"Message: {clean}" : "Message: Test print from Garmetix Dot Matrix Audit Journal",
            DotMatrixTextFormatter.Rule(width)
        });

        return await AddQueueEntryAsync(storeId, nowIst.Date, nowUtc, "Test", "Test", "TEST", Guid.NewGuid(), "TEST", "", "", 0m, text, setting, cancellationToken);
    }

    public async Task QueueDayOpeningAsync(Guid storeId, DateTime businessDate, Guid sourceId, Guid cashDetailId, string? openedBy, CancellationToken cancellationToken)
    {
        var setting = await ResolveSettingAsync(storeId, cancellationToken);
        if (!setting.Enabled || !setting.PrintDayOpeningClosing)
        {
            return;
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        var cash = await db.CashDetails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == cashDetailId, cancellationToken);
        var nowUtc = NowUtc();
        var nowIst = ToBusinessTime(nowUtc, setting.TimeZoneId);
        var width = setting.LineWidth;
        var text = BuildDayOpeningText(width, store?.Name ?? "Store", store?.StoreCode ?? storeId.ToString("N")[..6], businessDate.Date, nowIst, openedBy, ToCash(cash));
        await AddQueueEntryAsync(storeId, businessDate.Date, nowUtc, "DayOpening", "Open", "DAY OPENING", sourceId, businessDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture), store?.Name ?? string.Empty, "Cash", cash?.Amount ?? 0m, text, setting, cancellationToken);
    }

    public async Task QueueDayClosingSummaryAsync(Guid storeId, DateTime businessDate, Guid sourceId, Guid cashDetailId, PettyCashBookSummaryDto cashSummary, string? closedBy, CancellationToken cancellationToken)
    {
        var setting = await ResolveSettingAsync(storeId, cancellationToken);
        if (!setting.Enabled || !setting.PrintDayOpeningClosing)
        {
            return;
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        var cash = await db.CashDetails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == cashDetailId, cancellationToken);
        var nowUtc = NowUtc();
        var nowIst = ToBusinessTime(nowUtc, setting.TimeZoneId);
        var summary = await BuildSummaryModelAsync(storeId, businessDate.Date, cashSummary, setting, cancellationToken);
        var width = setting.LineWidth;
        var text = BuildDayClosingText(width, store?.Name ?? "Store", store?.StoreCode ?? storeId.ToString("N")[..6], businessDate.Date, nowIst, closedBy, ToCash(cash), summary);
        await AddQueueEntryAsync(storeId, businessDate.Date, nowUtc, "DayClosing", "Close", "DAY CLOSING", sourceId, businessDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture), store?.Name ?? string.Empty, "Cash", cash?.Amount ?? cashSummary.CashInHand, text, setting, cancellationToken);
    }

    public async Task<DotMatrixEffectiveSettingDto> GetEffectiveSettingAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var setting = await ResolveSettingAsync(storeId, cancellationToken);
        return new DotMatrixEffectiveSettingDto(storeId, setting.Enabled, setting.PrinterName, setting.OutputMode, setting.SpoolDirectory, setting.TimeZoneId, setting.LineWidth, setting.PrintTransactions, setting.PrintDayOpeningClosing, setting.PrintEditsAndDeletes, setting.PrintAttendanceInDaySummary, setting.PrintBankUpiSummary, setting.PollSeconds, setting.RetryLimit);
    }

    public async Task<DotMatrixEffectiveSettingDto> SaveSettingAsync(DotMatrixSettingSaveRequest request, CancellationToken cancellationToken)
    {
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.StoreId, cancellationToken)
            ?? throw new InvalidOperationException("Store not found.");
        var entity = await db.DotMatrixPrintSettings.FirstOrDefaultAsync(item => item.StoreId == request.StoreId && !item.Deleted, cancellationToken);
        if (entity is null)
        {
            entity = new DotMatrixPrintSetting
            {
                CompanyId = store.CompanyId,
                StoreGroupId = store.StoreGroupId,
                StoreId = store.Id,
                CreatedBy = "DotMatrixSettings"
            };
            db.DotMatrixPrintSettings.Add(entity);
        }

        entity.Enabled = request.Enabled;
        entity.PrinterName = DotMatrixTextFormatter.Clean(request.PrinterName);
        entity.OutputMode = NormalizeOutputMode(request.OutputMode);
        entity.SpoolDirectory = string.IsNullOrWhiteSpace(request.SpoolDirectory) ? options.Value.SpoolDirectory : request.SpoolDirectory.Trim();
        entity.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Asia/Kolkata" : request.TimeZoneId.Trim();
        entity.LineWidth = Math.Clamp(request.LineWidth, 80, 136);
        entity.PrintTransactions = request.PrintTransactions;
        entity.PrintDayOpeningClosing = request.PrintDayOpeningClosing;
        entity.PrintEditsAndDeletes = request.PrintEditsAndDeletes;
        entity.PrintAttendanceInDaySummary = request.PrintAttendanceInDaySummary;
        entity.PrintBankUpiSummary = request.PrintBankUpiSummary;
        entity.PollSeconds = Math.Clamp(request.PollSeconds, 2, 60);
        entity.RetryLimit = Math.Clamp(request.RetryLimit, 1, 100);
        entity.UpdatedAt = NowUtc();
        await db.SaveChangesAsync(cancellationToken);
        return await GetEffectiveSettingAsync(request.StoreId, cancellationToken);
    }

    private async Task<DotMatrixPrintSetting> ResolveSettingAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var existing = await db.DotMatrixPrintSettings.AsNoTracking().FirstOrDefaultAsync(item => item.StoreId == storeId && !item.Deleted, cancellationToken);
        if (existing is not null)
        {
            existing.LineWidth = Math.Clamp(existing.LineWidth <= 0 ? options.Value.LineWidth : existing.LineWidth, 80, 136);
            existing.OutputMode = NormalizeOutputMode(existing.OutputMode);
            existing.TimeZoneId = string.IsNullOrWhiteSpace(existing.TimeZoneId) ? options.Value.TimeZoneId : existing.TimeZoneId;
            existing.SpoolDirectory = string.IsNullOrWhiteSpace(existing.SpoolDirectory) ? options.Value.SpoolDirectory : existing.SpoolDirectory;
            if (string.IsNullOrWhiteSpace(existing.PrinterName)) existing.PrinterName = options.Value.PrinterName;
            return existing;
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        return new DotMatrixPrintSetting
        {
            CompanyId = store?.CompanyId ?? Guid.Empty,
            StoreGroupId = store?.StoreGroupId ?? Guid.Empty,
            StoreId = storeId,
            Enabled = options.Value.Enabled,
            PrinterName = options.Value.PrinterName,
            OutputMode = NormalizeOutputMode(options.Value.OutputMode),
            SpoolDirectory = options.Value.SpoolDirectory,
            TimeZoneId = string.IsNullOrWhiteSpace(options.Value.TimeZoneId) ? "Asia/Kolkata" : options.Value.TimeZoneId,
            LineWidth = Math.Clamp(options.Value.LineWidth, 80, 136),
            PollSeconds = Math.Clamp(options.Value.PollSeconds, 2, 60),
            RetryLimit = Math.Clamp(options.Value.RetryLimit, 1, 100)
        };
    }

    private async Task<DotMatrixPrintQueueEntry> AddQueueEntryAsync(Guid storeId, DateTime businessDate, DateTime nowUtc, string eventType, string actionType, string sourceType, Guid sourceId, string sourceNumber, string partyName, string paymentMode, decimal amount, string printableText, DotMatrixPrintSetting setting, CancellationToken cancellationToken)
    {
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        var normalizedText = printableText.TrimEnd() + Environment.NewLine;
        var deduplicationKey = BuildManualDeduplicationKey(eventType, actionType, sourceType, sourceId, businessDate.Date, normalizedText);
        var existing = await db.DotMatrixPrintQueueEntries.AsNoTracking()
            .FirstOrDefaultAsync(item => item.DeduplicationKey == deduplicationKey && !item.Deleted, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Skipped duplicate dot-matrix print queue entry {EntryId} {SourceType} {ActionType}", existing.Id, sourceType, actionType);
            return existing;
        }

        var entry = new DotMatrixPrintQueueEntry
        {
            CompanyId = store?.CompanyId ?? setting.CompanyId,
            StoreGroupId = store?.StoreGroupId ?? setting.StoreGroupId,
            StoreId = storeId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            BusinessDate = businessDate.Date,
            OperationTimeUtc = nowUtc,
            EventType = eventType,
            ActionType = actionType,
            SourceType = sourceType,
            SourceId = sourceId,
            SourceNumber = ClampToColumn(sourceNumber, 120),
            PartyName = DotMatrixTextFormatter.Fit(partyName, Math.Min(120, Math.Max(10, partyName.Length))).Trim(),
            PaymentMode = ClampToColumn(paymentMode, 120),
            Amount = Math.Round(amount, 2),
            SequenceNo = nowUtc.Ticks,
            LineWidth = setting.LineWidth,
            PrinterName = setting.PrinterName,
            Status = "Pending",
            PrintableText = normalizedText,
            CreatedBy = "DotMatrixJournal",
            DeduplicationKey = deduplicationKey
        };
        db.DotMatrixPrintQueueEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued dot-matrix print entry {EntryId} {SourceType} {ActionType}", entry.Id, sourceType, actionType);
        return entry;
    }

    // SourceNumber/PaymentMode are varchar(120) columns but were previously written unclamped -
    // a long value here throws PostgresException 22001 and fails the whole SaveChanges batch.
    private static string ClampToColumn(string? value, int maxLength)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Length <= maxLength ? value : value[..maxLength];

    public async Task<DotMatrixPrintQueueEntry> QueueReprintAsync(Guid entryId, string? requestedBy, CancellationToken cancellationToken)
    {
        var source = await db.DotMatrixPrintQueueEntries.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == entryId && !item.Deleted, cancellationToken)
            ?? throw new InvalidOperationException("Print queue entry was not found.");
        var setting = await ResolveSettingAsync(source.StoreId, cancellationToken);
        if (!setting.Enabled)
        {
            throw new InvalidOperationException("Dot Matrix printing is disabled for this store. Enable it before reprinting.");
        }

        var nowUtc = NowUtc();
        var nowIst = ToBusinessTime(nowUtc, setting.TimeZoneId);
        var width = Math.Clamp(source.LineWidth <= 0 ? setting.LineWidth : source.LineWidth, 80, 136);
        var header = string.Join(Environment.NewLine, new[]
        {
            DotMatrixTextFormatter.Rule(width),
            DotMatrixTextFormatter.Center(width, "REPRINT COPY"),
            DotMatrixTextFormatter.Rule(width, '-'),
            $"Original Queue Id: {source.Id}",
            $"Original Event: {source.EventType} / {source.ActionType}",
            $"Reprinted At: {nowIst:dd-MM-yyyy hh:mm tt} IST",
            $"Reprinted By: {DisplayUser(requestedBy)}",
            DotMatrixTextFormatter.Rule(width, '-')
        });

        var entry = new DotMatrixPrintQueueEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = source.CompanyId,
            StoreGroupId = source.StoreGroupId,
            StoreId = source.StoreId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            BusinessDate = source.BusinessDate,
            OperationTimeUtc = nowUtc,
            EventType = "Reprint",
            ActionType = "Reprint",
            SourceType = source.SourceType,
            SourceId = source.SourceId,
            SourceNumber = source.SourceNumber,
            PartyName = source.PartyName,
            PaymentMode = source.PaymentMode,
            Amount = source.Amount,
            SequenceNo = nowUtc.Ticks,
            LineWidth = width,
            PrinterName = string.IsNullOrWhiteSpace(source.PrinterName) ? setting.PrinterName : source.PrinterName,
            Status = "Pending",
            PrintableText = header + Environment.NewLine + source.PrintableText.TrimEnd() + Environment.NewLine,
            CreatedBy = DisplayUser(requestedBy),
            DeduplicationKey = $"REPRINT:{source.Id:N}:{nowUtc.Ticks}"
        };
        db.DotMatrixPrintQueueEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Queued dot-matrix reprint entry {EntryId} from {OriginalEntryId}", entry.Id, source.Id);
        return entry;
    }

    private async Task<DaySummaryPrintModel> BuildSummaryModelAsync(Guid storeId, DateTime businessDate, PettyCashBookSummaryDto cashSummary, DotMatrixPrintSetting setting, CancellationToken cancellationToken)
    {
        var start = businessDate.Date;
        var end = start.AddDays(1);
        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end && !item.ReturnInvoice)
            .Select(item => new { item.Id, item.BillAmount, item.PaidAmount, item.PaymentMode })
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(item => item.Id).ToArray();
        var invoicePayments = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end)
            .Select(item => new { item.InvoiceId, item.PaymentMode, item.Amount, item.BankAccountId })
            .ToListAsync(cancellationToken);
        var currentPayments = invoicePayments.Where(item => invoiceIds.Contains(item.InvoiceId)).ToList();
        var cashSales = currentPayments.Where(item => item.PaymentMode == PaymentMode.Cash).Sum(item => item.Amount);
        if (cashSales == 0)
        {
            cashSales = invoices.Where(item => item.PaymentMode == PaymentMode.Cash).Sum(item => item.PaidAmount > 0 ? item.PaidAmount : item.BillAmount);
        }
        var upiSales = currentPayments.Where(item => item.PaymentMode == PaymentMode.UPI || item.PaymentMode == PaymentMode.Wallets).Sum(item => item.Amount);
        var cardBankSales = currentPayments.Where(item => item.PaymentMode is PaymentMode.Card or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft).Sum(item => item.Amount);

        var purchaseInward = await db.PurchaseInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.InwardDate >= start && item.InwardDate < end)
            .SumAsync(item => item.BillAmount, cancellationToken);
        var vendorPayments = await db.PurchasePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end)
            .SumAsync(item => item.Amount, cancellationToken);
        var cashVouchers = await db.CashVouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end)
            .SumAsync(item => item.Amount, cancellationToken);

        var bankUpi = await BuildBankUpiSummaryAsync(storeId, start, end, cancellationToken);
        var attendance = setting.PrintAttendanceInDaySummary
            ? await BuildAttendanceSummaryAsync(storeId, start, end, setting.TimeZoneId, cancellationToken)
            : new AttendanceSummaryPrintModel(Array.Empty<PresentEmployeePrintModel>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        return new DaySummaryPrintModel(
            invoices.Count,
            invoices.Sum(item => item.BillAmount),
            cashSales,
            upiSales,
            cardBankSales,
            cashSummary.Receipts + cashSummary.DueReceipts,
            vendorPayments,
            cashSummary.Expenses,
            cashVouchers,
            purchaseInward,
            cashSummary.OpeningBalance,
            cashSummary.Sales + cashSummary.Receipts + cashSummary.DueReceipts + cashSummary.BankWithdrawal,
            cashSummary.Expenses + cashSummary.Payments + cashSummary.CustomerDue + cashSummary.BankDeposit + cashSummary.NonCashSale,
            cashSummary.CashInHand,
            bankUpi,
            attendance);
    }

    private async Task<IReadOnlyList<(string Name, decimal Amount)>> BuildBankUpiSummaryAsync(Guid storeId, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var rows = new List<(PaymentMode PaymentMode, decimal Amount, Guid? BankAccountId)>();

        var invoicePayments = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end && item.PaymentMode != PaymentMode.Cash)
            .Select(item => new { item.PaymentMode, item.Amount, item.BankAccountId })
            .ToListAsync(cancellationToken);
        rows.AddRange(invoicePayments.Select(item => (item.PaymentMode, item.Amount, item.BankAccountId)));

        var customerAdvances = await db.CustomerAdvanceReceipts.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end && item.PaymentMode != PaymentMode.Cash)
            .Select(item => new { item.PaymentMode, item.Amount, item.BankAccountId })
            .ToListAsync(cancellationToken);
        rows.AddRange(customerAdvances.Select(item => (item.PaymentMode, item.Amount, item.BankAccountId)));

        var purchasePayments = await db.PurchasePayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end && item.PaymentMode != PaymentMode.Cash)
            .Select(item => new { item.PaymentMode, item.Amount, item.BankAccountId })
            .ToListAsync(cancellationToken);
        rows.AddRange(purchasePayments.Select(item => (item.PaymentMode, item.Amount, item.BankAccountId)));

        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= start && item.OnDate < end && item.PaymentMode != PaymentMode.Cash)
            .Select(item => new { item.PaymentMode, item.Amount, BankAccountId = item.AccountNumber })
            .ToListAsync(cancellationToken);
        rows.AddRange(vouchers.Select(item => (item.PaymentMode, item.Amount, item.BankAccountId)));

        var bankIds = rows.Where(item => item.BankAccountId.HasValue).Select(item => item.BankAccountId!.Value).Distinct().ToArray();
        Dictionary<Guid, string> banks;
        if (bankIds.Length == 0)
        {
            banks = new Dictionary<Guid, string>();
        }
        else
        {
            var bankRows = await db.BankAccounts.AsNoTracking()
                .Where(item => bankIds.Contains(item.Id))
                .Select(item => new { item.Id, item.AccountHolderName, item.AccountNumber })
                .ToListAsync(cancellationToken);
            banks = bankRows.ToDictionary(item => item.Id, item => string.IsNullOrWhiteSpace(item.AccountHolderName) ? item.AccountNumber : item.AccountHolderName);
        }

        return rows
            .GroupBy(item => item.BankAccountId.HasValue && banks.TryGetValue(item.BankAccountId.Value, out var bankName)
                ? $"{bankName} {item.PaymentMode}"
                : item.PaymentMode.ToString())
            .Select(group => (group.Key, Math.Round(group.Sum(item => item.Amount), 2)))
            .OrderByDescending(item => item.Item2)
            .ToList();
    }

    private async Task<AttendanceSummaryPrintModel> BuildAttendanceSummaryAsync(Guid storeId, DateTime start, DateTime end, string timeZoneId, CancellationToken cancellationToken)
    {
        var employees = await db.Employees.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.Working && !item.Deleted)
            .Select(item => new { item.Id, item.FirstName, item.LastName, item.EmployeeCode })
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .ToListAsync(cancellationToken);
        var employeeIds = employees.Select(item => item.Id).ToArray();
        var punches = await db.AttendancePunches.AsNoTracking()
            .Where(item => item.StoreId == storeId && employeeIds.Contains(item.EmployeeId) && item.LocalPunchTime >= start && item.LocalPunchTime < end && !item.Deleted)
            .Select(item => new { item.EmployeeId, item.PunchType, item.LocalPunchTime })
            .ToListAsync(cancellationToken);
        var shift = await db.AttendanceShifts.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.Active && !item.Deleted)
            .OrderBy(item => item.StartTimeMinutes)
            .Select(item => new { item.StartTimeMinutes, item.LateAfterMinutes, item.GraceMinutes })
            .FirstOrDefaultAsync(cancellationToken);
        var lateAfter = start.AddMinutes((shift?.StartTimeMinutes ?? 600) + Math.Max(shift?.LateAfterMinutes ?? shift?.GraceMinutes ?? 10, 0));
        var punchGroups = punches.GroupBy(item => item.EmployeeId).ToDictionary(group => group.Key, group => group.OrderBy(item => item.LocalPunchTime).ToList());
        var present = new List<PresentEmployeePrintModel>();
        var absent = new List<string>();
        var late = new List<string>();
        var missingCheckout = new List<string>();

        foreach (var employee in employees)
        {
            var name = DotMatrixTextFormatter.Clean($"{employee.FirstName} {employee.LastName}");
            var displayName = DotMatrixTextFormatter.Clean(string.IsNullOrWhiteSpace(employee.EmployeeCode) ? name : $"{employee.EmployeeCode} {name}");
            if (!punchGroups.TryGetValue(employee.Id, out var rows) || rows.Count == 0)
            {
                absent.Add(displayName);
                continue;
            }

            var checkIn = rows.First().LocalPunchTime;
            var checkOut = rows.Count > 1 ? rows.Last().LocalPunchTime : (DateTime?)null;
            var isLate = checkIn > lateAfter;
            var status = isLate ? "Late" : "Present";
            if (!checkOut.HasValue)
            {
                status = isLate ? "Late/Missing Out" : "Missing Out";
                missingCheckout.Add($"{displayName} - In: {checkIn:hh:mm tt}");
            }
            if (isLate) late.Add($"{displayName} - {checkIn:hh:mm tt}");
            var totalHours = checkOut.HasValue ? FormatDuration(checkOut.Value - checkIn) : "-";
            present.Add(new PresentEmployeePrintModel(displayName, checkIn, checkOut, totalHours, status));
        }

        return new AttendanceSummaryPrintModel(present, absent, late, missingCheckout);
    }

    private static string BuildDayOpeningText(int width, string storeName, string storeCode, DateTime day, DateTime openedAtIst, string? openedBy, CashDetailPrintModel? cash)
    {
        var sb = new StringBuilder();
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        sb.AppendLine(DotMatrixTextFormatter.Center(width, $"DAY OPENING - {day:dd-MM-yyyy}"));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine($"Store: {storeName} / {storeCode}");
        sb.AppendLine($"Opened At: {openedAtIst:dd-MM-yyyy hh:mm tt} IST");
        sb.AppendLine($"Opened By: {DisplayUser(openedBy)}");
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine(DotMatrixTextFormatter.CashDetailsTable(width, "OPENING CASH DETAILS", cash));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        sb.AppendLine(DotMatrixTextFormatter.Center(width, "TRANSACTION JOURNAL STARTED"));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        return sb.ToString();
    }

    private static string BuildDayClosingText(int width, string storeName, string storeCode, DateTime day, DateTime closedAtIst, string? closedBy, CashDetailPrintModel? cash, DaySummaryPrintModel summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        sb.AppendLine(DotMatrixTextFormatter.Center(width, $"DAY CLOSING - {day:dd-MM-yyyy}"));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine($"Store: {storeName} / {storeCode}");
        sb.AppendLine($"Closed At: {closedAtIst:dd-MM-yyyy hh:mm tt} IST");
        sb.AppendLine($"Closed By: {DisplayUser(closedBy)}");
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine(DotMatrixTextFormatter.CashDetailsTable(width, "CLOSING CASH DETAILS", cash));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        sb.AppendLine(DotMatrixTextFormatter.Center(width, $"DAY SUMMARY - {day:dd-MM-yyyy}"));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine(DotMatrixTextFormatter.ThreeBlockLine(("SALES SUMMARY", ""), ("TRANSACTION SUMMARY", ""), ("CASH SUMMARY", ""), width));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine(DotMatrixTextFormatter.ThreeBlockLine(("Sales Count", summary.SalesCount.ToString(CultureInfo.InvariantCulture)), ("Customer Receipts", summary.CustomerReceipts.ToString("N2", CultureInfo.InvariantCulture)), ("Opening Cash", summary.OpeningCash.ToString("N2", CultureInfo.InvariantCulture)), width));
        sb.AppendLine(DotMatrixTextFormatter.ThreeBlockMoneyLine(("Sales Amount", summary.SalesAmount), ("Vendor Payments", summary.VendorPayments), ("Cash In", summary.CashIn), width));
        sb.AppendLine(DotMatrixTextFormatter.ThreeBlockMoneyLine(("Cash Sales", summary.CashSales), ("Expenses", summary.Expenses), ("Cash Out", summary.CashOut), width));
        sb.AppendLine(DotMatrixTextFormatter.ThreeBlockMoneyLine(("UPI Sales", summary.UpiSales), ("Cash Vouchers", summary.CashVouchers), ("Closing Cash", summary.ClosingCash), width));
        sb.AppendLine(DotMatrixTextFormatter.ThreeBlockLine(("Card/Bank Sales", summary.CardBankSales.ToString("N2", CultureInfo.InvariantCulture)), ("Purchase Inward", summary.PurchaseInward.ToString("N2", CultureInfo.InvariantCulture)), ("", ""), width));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine("BANK / UPI SUMMARY");
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        if (summary.BankUpiSummary.Count == 0)
        {
            sb.AppendLine("No non-cash bank/UPI collection found.");
        }
        else
        {
            foreach (var row in BuildBankRows(summary.BankUpiSummary, width)) sb.AppendLine(row);
        }
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine("ATTENDANCE DETAILS");
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine("PRESENT EMPLOYEES");
        if (summary.Attendance.Present.Count == 0)
        {
            sb.AppendLine("None");
        }
        else
        {
            var nameWidth = width >= 120 ? 44 : 30;
            sb.AppendLine(DotMatrixTextFormatter.Fit("Employee", nameWidth) + DotMatrixTextFormatter.Fit("Check In", 12) + DotMatrixTextFormatter.Fit("Check Out", 12) + DotMatrixTextFormatter.Fit("Hours", 10) + DotMatrixTextFormatter.Fit("Status", 18));
            foreach (var employee in summary.Attendance.Present)
            {
                sb.AppendLine(DotMatrixTextFormatter.Fit(employee.Name, nameWidth)
                    + DotMatrixTextFormatter.Fit(employee.CheckIn.ToString("hh:mm tt", CultureInfo.InvariantCulture), 12)
                    + DotMatrixTextFormatter.Fit(employee.CheckOut?.ToString("hh:mm tt", CultureInfo.InvariantCulture) ?? "-", 12)
                    + DotMatrixTextFormatter.Fit(employee.TotalHours, 10)
                    + DotMatrixTextFormatter.Fit(employee.Status, 18));
            }
        }
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine("ABSENT EMPLOYEES");
        sb.AppendLine(summary.Attendance.Absent.Count == 0 ? "None" : string.Join(", ", summary.Attendance.Absent));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine("LATE CHECK-IN EMPLOYEES");
        sb.AppendLine(summary.Attendance.Late.Count == 0 ? "None" : string.Join(", ", summary.Attendance.Late));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width, '-'));
        sb.AppendLine("MISSING CHECK-OUT EMPLOYEES");
        sb.AppendLine(summary.Attendance.MissingCheckout.Count == 0 ? "None" : string.Join(", ", summary.Attendance.MissingCheckout));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        sb.AppendLine(DotMatrixTextFormatter.Center(width, $"DAY CLOSED - {closedAtIst:dd-MM-yyyy hh:mm tt} IST"));
        sb.AppendLine(DotMatrixTextFormatter.Rule(width));
        return sb.ToString();
    }

    private static IEnumerable<string> BuildBankRows(IReadOnlyList<(string Name, decimal Amount)> rows, int width)
    {
        var perLine = width >= 120 ? 3 : 2;
        var block = (width - ((perLine - 1) * 2)) / perLine;
        for (var i = 0; i < rows.Count; i += perLine)
        {
            var parts = rows.Skip(i).Take(perLine).Select(row => DotMatrixTextFormatter.Pair(row.Name, row.Amount, block)).ToList();
            yield return string.Join("  ", parts).PadRight(width);
        }
    }


    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 0) return "-";
        return $"{(int)duration.TotalHours}h {duration.Minutes:00}m";
    }

    private static string BuildManualDeduplicationKey(string eventType, string actionType, string sourceType, Guid sourceId, DateTime businessDate, string printableText)
    {
        var payload = $"{eventType}:{actionType}:{sourceType}:{sourceId:N}:{businessDate:yyyyMMdd}:{ShortHash(printableText)}";
        return DotMatrixTextFormatter.Fit(payload, 240).Trim();
    }

    private static string ShortHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)))[..16];

    private static string DisplayUser(string? value)
    {
        var clean = DotMatrixTextFormatter.Clean(value);
        return string.IsNullOrWhiteSpace(clean) ? "System" : clean;
    }

    private static CashDetailPrintModel? ToCash(CashDetail? cash) => cash is null
        ? null
        : new CashDetailPrintModel(cash.Amount, cash.N2000, cash.N500, cash.N200, cash.N100, cash.N50, cash.NC20, cash.NC10, cash.NC5, cash.NC2, cash.NC1);

    private static DateTime NowUtc() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private static DateTime ToBusinessTime(DateTime utc, string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Kolkata" : timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified).AddHours(5).AddMinutes(30);
        }
    }

    private static string NormalizeOutputMode(string? value)
    {
        if (string.Equals(value, "BridgeService", StringComparison.OrdinalIgnoreCase)) return "BridgeService";
        if (string.Equals(value, "LpCommand", StringComparison.OrdinalIgnoreCase)) return "LpCommand";
        if (string.Equals(value, "Disabled", StringComparison.OrdinalIgnoreCase)) return "Disabled";
        if (string.Equals(value, "SpoolFile", StringComparison.OrdinalIgnoreCase)) return "SpoolFile";
        return "BridgeService";
    }
}

public sealed record DotMatrixSettingSaveRequest(
    Guid StoreId,
    bool Enabled,
    string? PrinterName,
    string? OutputMode,
    string? SpoolDirectory,
    string? TimeZoneId,
    int LineWidth,
    bool PrintTransactions,
    bool PrintDayOpeningClosing,
    bool PrintEditsAndDeletes,
    bool PrintAttendanceInDaySummary,
    bool PrintBankUpiSummary,
    int PollSeconds,
    int RetryLimit);

public sealed record DotMatrixEffectiveSettingDto(
    Guid StoreId,
    bool Enabled,
    string PrinterName,
    string OutputMode,
    string SpoolDirectory,
    string TimeZoneId,
    int LineWidth,
    bool PrintTransactions,
    bool PrintDayOpeningClosing,
    bool PrintEditsAndDeletes,
    bool PrintAttendanceInDaySummary,
    bool PrintBankUpiSummary,
    int PollSeconds,
    int RetryLimit);

public sealed record DaySummaryPrintModel(
    int SalesCount,
    decimal SalesAmount,
    decimal CashSales,
    decimal UpiSales,
    decimal CardBankSales,
    decimal CustomerReceipts,
    decimal VendorPayments,
    decimal Expenses,
    decimal CashVouchers,
    decimal PurchaseInward,
    decimal OpeningCash,
    decimal CashIn,
    decimal CashOut,
    decimal ClosingCash,
    IReadOnlyList<(string Name, decimal Amount)> BankUpiSummary,
    AttendanceSummaryPrintModel Attendance);

public sealed record AttendanceSummaryPrintModel(IReadOnlyList<PresentEmployeePrintModel> Present, IReadOnlyList<string> Absent, IReadOnlyList<string> Late, IReadOnlyList<string> MissingCheckout);
public sealed record PresentEmployeePrintModel(string Name, DateTime CheckIn, DateTime? CheckOut, string TotalHours, string Status);

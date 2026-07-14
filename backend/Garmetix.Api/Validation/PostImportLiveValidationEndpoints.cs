using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Validation;

public static class PostImportLiveValidationEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const decimal QuantityTolerance = 0.001m;

    public static IEndpointRouteBuilder MapPostImportLiveValidationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/post-import-validation")
            .WithTags("Post Import Validation")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/accounting-gst", ReportAsync);
        group.MapGet("/accounting-gst.csv", CsvAsync);

        return app;
    }

    private static async Task<PostImportValidationReportDto> ReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, endExclusive) = ResolveRange(from, to);
        return await BuildReportAsync(context, db, companyId, storeId, fromDate, toDate, endExclusive, cancellationToken);
    }

    private static async Task<IResult> CsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, endExclusive) = ResolveRange(from, to);
        var report = await BuildReportAsync(context, db, companyId, storeId, fromDate, toDate, endExclusive, cancellationToken);
        var csv = new StringBuilder();
        csv.AppendLine("Section,Severity,Code,Title,Reference,Expected,Actual,Difference,Description");
        foreach (var check in report.Checks)
        {
            csv.AppendLine(string.Join(',',
                Csv("Check"),
                Csv(check.Status),
                Csv(check.Code),
                Csv(check.Title),
                Csv(check.Reference ?? string.Empty),
                Csv(FormatDecimal(check.ExpectedValue)),
                Csv(FormatDecimal(check.ActualValue)),
                Csv(FormatDecimal(check.Difference)),
                Csv(check.Description)));
        }

        foreach (var issue in report.Issues)
        {
            csv.AppendLine(string.Join(',',
                Csv(issue.Area),
                Csv(issue.Severity),
                Csv(issue.Code),
                Csv(issue.Title),
                Csv(issue.Reference ?? string.Empty),
                Csv(FormatDecimal(issue.ExpectedValue)),
                Csv(FormatDecimal(issue.ActualValue)),
                Csv(FormatDecimal(issue.Difference)),
                Csv(issue.Description)));
        }

        csv.AppendLine();
        csv.AppendLine("GST Direction,Tax Rate,Taxable Value,Tax Amount,CGST,SGST,IGST,Invoice Count,Line Count");
        foreach (var row in report.GstRows)
        {
            csv.AppendLine(string.Join(',',
                Csv(row.Direction),
                Csv(row.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)),
                Csv(FormatDecimal(row.TaxableValue)),
                Csv(FormatDecimal(row.TaxAmount)),
                Csv(FormatDecimal(row.CgstAmount)),
                Csv(FormatDecimal(row.SgstAmount)),
                Csv(FormatDecimal(row.IgstAmount)),
                Csv(row.InvoiceCount.ToString(CultureInfo.InvariantCulture)),
                Csv(row.LineCount.ToString(CultureInfo.InvariantCulture))));
        }

        var name = $"accounting-gst-post-import-validation-{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", name);
    }

    private static async Task<PostImportValidationReportDto> BuildReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeId,
        DateTime fromDate,
        DateTime toDate,
        DateTime endExclusive,
        CancellationToken cancellationToken)
    {
        var checks = new List<PostImportValidationCheckDto>();
        var issues = new List<PostImportValidationIssueDto>();

        var sales = await ApplyStoreFilter(ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context), companyId), storeId)
            .Where(item => item.OnDate >= fromDate && item.OnDate < endExclusive && item.InvoiceStatus != InvoiceStatus.Cancelled && !item.ReturnInvoice)
            .Select(item => new SaleRow(
                item.Id,
                item.CompanyId,
                item.StoreId,
                item.InvoiceNumber,
                item.OnDate,
                item.BillAmount,
                item.PaidAmount,
                item.BasePrice,
                item.TaxAmount,
                item.CGSTAmount ?? 0m,
                item.SGSTAmount ?? 0m,
                item.IGSTAmount ?? 0m,
                item.Quantity,
                item.ItemCount,
                item.Remarks ?? string.Empty))
            .ToListAsync(cancellationToken);

        var purchaseQuery = ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context), companyId);
        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            purchaseQuery = purchaseQuery.Where(item => item.StoreId == storeId.Value);
        }

        var purchase = await purchaseQuery
            .Where(item => item.InwardDate >= fromDate && item.InwardDate < endExclusive && item.InvoiceStatus != InvoiceStatus.Cancelled && !item.ReturnInvoice)
            .Select(item => new PurchaseRow(
                item.Id,
                item.CompanyId,
                item.StoreGroupId ?? Guid.Empty,
                item.StoreId,
                item.InvoiceNumber,
                item.InwardNumber,
                item.InwardDate,
                item.BillAmount,
                item.BasePrice,
                item.TaxAmount,
                item.CGSTAmount ?? 0m,
                item.SGSTAmount ?? 0m,
                item.IGSTAmount ?? 0m,
                item.FrightAmount,
                item.RoundOff,
                item.Quantity,
                item.ItemCount,
                item.VendorName ?? string.Empty))
            .ToListAsync(cancellationToken);

        var saleIds = sales.Select(item => item.Id).ToHashSet();
        var purchaseIds = purchase.Select(item => item.Id).ToHashSet();

        var saleItems = saleIds.Count == 0
            ? new List<ItemRow>()
            : await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context)
                .Where(item => saleIds.Contains(item.InvoiceId))
                .Select(item => new ItemRow(item.InvoiceId, item.Barcode, item.HSNCode, item.BilledQuantity, item.BasePrice, item.TaxPercentage, item.TaxAmount, item.CGSTAmount ?? 0m, item.SGSTAmount ?? 0m, item.IGSTAmount ?? 0m, item.Amount))
                .ToListAsync(cancellationToken);

        var purchaseItems = purchaseIds.Count == 0
            ? new List<ItemRow>()
            : await WorkspaceScope.ApplyTo(db.PurchaseInvoiceItems.AsNoTracking(), context)
                .Where(item => purchaseIds.Contains(item.InvoiceId))
                .Select(item => new ItemRow(item.InvoiceId, item.Barcode, item.HSNCode, item.BilledQuantity, item.BasePrice, item.TaxPercentage, item.TaxAmount, item.CGSTAmount ?? 0m, item.SGSTAmount ?? 0m, item.IGSTAmount ?? 0m, item.Amount))
                .ToListAsync(cancellationToken);

        var salePayments = saleIds.Count == 0
            ? new List<PaymentRow>()
            : await ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context), companyId)
                .Where(item => saleIds.Contains(item.InvoiceId))
                .Select(item => new PaymentRow(item.InvoiceId, item.PaymentMode, item.Amount, item.BankAccountId, item.ReferenceNumber ?? item.GatewayReference ?? string.Empty))
                .ToListAsync(cancellationToken);

        var purchasePayments = purchaseIds.Count == 0
            ? new List<PaymentRow>()
            : await ApplyStoreFilter(ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context), companyId), storeId)
                .Where(item => purchaseIds.Contains(item.PurchaseInvoiceId))
                .Select(item => new PaymentRow(item.PurchaseInvoiceId, item.PaymentMode, item.Amount, item.BankAccountId, item.ReferenceNumber ?? string.Empty))
                .ToListAsync(cancellationToken);

        var journalSources = await ApplyStoreFilter(ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context), companyId), storeId)
            .Where(item => item.OnDate >= fromDate && item.OnDate < endExclusive && item.Posted && item.SourceId.HasValue)
            .Select(item => new JournalSourceRow(item.SourceType, item.SourceId!.Value, item.EntryNumber, item.ReferenceNumber))
            .ToListAsync(cancellationToken);
        var saleJournalIds = journalSources.Where(item => item.SourceType == "SalesInvoice").Select(item => item.SourceId).ToHashSet();
        var purchaseJournalIds = journalSources.Where(item => item.SourceType == "PurchaseInvoice").Select(item => item.SourceId).ToHashSet();

        var stockMovements = await ApplyStoreFilter(ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context), companyId), storeId)
            .Where(item => item.OnDate >= fromDate.AddDays(-1) && item.OnDate < endExclusive && item.SourceId.HasValue)
            .Select(item => new StockMovementRow(item.SourceId!.Value, item.SourceType ?? string.Empty, item.MovementType ?? string.Empty, item.QuantityIn, item.QuantityOut, item.CostImpact))
            .ToListAsync(cancellationToken);

        var saleMovementRows = stockMovements
            .Where(item => saleIds.Contains(item.SourceId) && IsSaleStockMovement(item))
            .ToList();
        var purchaseMovementRows = stockMovements
            .Where(item => purchaseIds.Contains(item.SourceId) && IsPurchaseStockMovement(item))
            .ToList();

        var postedPurchaseImportBatches = await ApplyStoreFilter(ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context), companyId), storeId)
            .Where(item => item.PostedPurchaseInvoiceId.HasValue && (item.PostedAt ?? item.UpdatedAt ?? item.CreatedAt) >= fromDate && (item.PostedAt ?? item.UpdatedAt ?? item.CreatedAt) < endExclusive)
            .Select(item => new ImportBatchRow(item.Id, item.SourceFileName, item.PostedPurchaseInvoiceId, item.Status, item.AcceptanceStatus, item.CorrectionStatus, item.BillAmount))
            .ToListAsync(cancellationToken);

        var currentStock = await ApplyStoreFilter(ApplyCompanyFilter(WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context), companyId), storeId)
            .Select(item => new { item.Id, item.Barcode, item.CompanyId, item.StoreId, CurrentStock = item.PurchaseQty - item.SoldQty })
            .ToListAsync(cancellationToken);
        var negativeStocks = currentStock.Where(item => item.CurrentStock < -QuantityTolerance).Take(25).ToList();

        AddAmountCheck(checks, issues, "SALE_ITEM_TOTAL", "Sale invoice bill vs item lines", sales.Sum(item => item.BillAmount), saleItems.Sum(item => item.Amount), "Sales", "Sales bill total should match sale item line total after discounts/rounding.", warningOnly: true);
        AddAmountCheck(checks, issues, "SALE_TAX_TOTAL", "Sale output GST vs item tax", sales.Sum(item => item.TaxAmount), saleItems.Sum(item => item.TaxAmount), "GST", "Sale invoice GST should match sum of sale item GST snapshots.", warningOnly: false);
        AddAmountCheck(checks, issues, "SALE_PAYMENT_TOTAL", "Sale paid amount vs receipt rows", sales.Sum(item => item.PaidAmount), salePayments.Sum(item => item.Amount), "Accounting", "Sale invoice paid amount should match customer receipt/payment rows.", warningOnly: false);
        AddCountCheck(checks, issues, "SALE_ACCOUNTING_JOURNALS", "Sale accounting journals", sales.Count, sales.Count(item => saleJournalIds.Contains(item.Id)), "Accounting", "Every active sale invoice should have a posted SalesInvoice journal entry.");
        AddQuantityCheck(checks, issues, "SALE_STOCK_OUT", "Sale stock-out quantity", saleItems.Sum(item => item.Quantity), saleMovementRows.Sum(item => item.QuantityOut), "Inventory", "Sale item quantity should match stock-out movement quantity.");

        var purchaseExpectedBillFromItems = purchaseItems.Sum(item => item.Amount) + purchase.Sum(item => item.FrightAmount + item.RoundOff);
        AddAmountCheck(checks, issues, "PURCHASE_ITEM_TOTAL", "Purchase bill vs item lines", purchase.Sum(item => item.BillAmount), purchaseExpectedBillFromItems, "Purchase", "Purchase bill total should match purchase item lines plus freight and round-off.", warningOnly: true);
        AddAmountCheck(checks, issues, "PURCHASE_TAX_TOTAL", "Purchase input GST vs item tax", purchase.Sum(item => item.TaxAmount), purchaseItems.Sum(item => item.TaxAmount), "GST", "Purchase invoice GST should match sum of purchase item GST snapshots.", warningOnly: false);
        AddPaymentCheck(checks, issues, purchase, purchasePayments);
        AddCountCheck(checks, issues, "PURCHASE_ACCOUNTING_JOURNALS", "Purchase accounting journals", purchase.Count, purchase.Count(item => purchaseJournalIds.Contains(item.Id)), "Accounting", "Every active purchase inward should have a posted PurchaseInvoice journal entry.");
        AddQuantityCheck(checks, issues, "PURCHASE_STOCK_IN", "Purchase stock-in quantity", purchaseItems.Sum(item => item.Quantity), purchaseMovementRows.Sum(item => item.QuantityIn), "Inventory", "Purchase item quantity should match stock-in movement quantity.");

        var unacceptedPostedImports = postedPurchaseImportBatches.Where(item => !string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase)).ToList();
        AddCountCheck(checks, issues, "PURCHASE_IMPORT_ACCEPTANCE", "Posted purchase import acceptance", postedPurchaseImportBatches.Count, postedPurchaseImportBatches.Count - unacceptedPostedImports.Count, "Purchase Import", "Every posted purchase import in this period should be compared with proof and marked Pass.", warningOnly: true);
        var correctionRequired = postedPurchaseImportBatches.Where(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase)).ToList();
        AddCountCheck(checks, issues, "PURCHASE_IMPORT_CORRECTION_QUEUE", "Purchase import correction queue", 0, correctionRequired.Count, "Purchase Import", "CorrectionRequired posted imports must be revised/returned/reversed with proof-safe workflow.", warningOnly: false, expectedIsZero: true);

        foreach (var invoice in sales.Where(item => !saleJournalIds.Contains(item.Id)).Take(20))
        {
            issues.Add(Issue("Critical", "Accounting", "SALE_JOURNAL_MISSING", "Sale journal missing", invoice.InvoiceNumber, 1, 0, -1, $"Sale invoice {invoice.InvoiceNumber} has no posted SalesInvoice journal entry."));
        }
        foreach (var invoice in purchase.Where(item => !purchaseJournalIds.Contains(item.Id)).Take(20))
        {
            issues.Add(Issue("Critical", "Accounting", "PURCHASE_JOURNAL_MISSING", "Purchase journal missing", invoice.InwardNumber, 1, 0, -1, $"Purchase inward {invoice.InwardNumber} has no posted PurchaseInvoice journal entry."));
        }
        foreach (var stock in negativeStocks)
        {
            issues.Add(Issue("Critical", "Inventory", "NEGATIVE_STOCK", "Negative stock after imports/postings", stock.Barcode, 0, stock.CurrentStock, stock.CurrentStock, $"Barcode {stock.Barcode} has negative current stock {stock.CurrentStock:n2}."));
        }
        foreach (var batch in unacceptedPostedImports.Take(20))
        {
            issues.Add(Issue("Warning", "Purchase Import", "POSTED_IMPORT_NOT_PASSED", "Posted import not marked Pass", batch.SourceFileName, 1, 0, -1, $"Posted purchase import {batch.SourceFileName} is {batch.AcceptanceStatus ?? "Not tested"}; compare proof and mark Pass or use correction plan."));
        }

        var gstRows = BuildGstRows(saleItems, purchaseItems);
        var paymentRows = BuildPaymentRows(salePayments, purchasePayments);
        var metrics = BuildMetrics(sales, purchase, saleItems, purchaseItems, salePayments, purchasePayments, saleMovementRows, purchaseMovementRows, postedPurchaseImportBatches, negativeStocks.Count);
        var status = issues.Any(item => item.Severity is "Critical" or "Warning") ? "Not Complete" : "Complete";

        return new PostImportValidationReportDto(
            DateTimeOffset.UtcNow,
            fromDate,
            toDate,
            status,
            metrics,
            checks,
            issues.OrderBy(item => SeverityRank(item.Severity)).ThenBy(item => item.Area).ThenBy(item => item.Code).ToArray(),
            gstRows,
            paymentRows,
            new[]
            {
                "Match imported Vyapar sale totals with the original Vyapar report for the selected period.",
                "Match posted supplier invoices with original supplier proof and purchase import acceptance status.",
                "Confirm sale and purchase GST output/input totals against GST Reports.",
                "Confirm every active sale and purchase has a posted journal entry.",
                "Confirm stock movement quantity equals sale/purchase item quantity.",
                "Confirm customer/vendor payment rows match invoice paid totals.",
                "Export CSV evidence and attach it to month-end closeout records."
            },
            new[]
            {
                "This page is a validation/evidence layer only; it does not auto-repair accounting, GST, stock, or dues.",
                "Purchase import proof correctness still depends on human comparison with supplier invoice proof.",
                "Historical/manual data imported before journal or stock posting existed may require separate controlled repair.",
                "GST totals are book-based snapshots; final statutory return filing still needs CA/accountant review."
            },
            new[]
            {
                "Day Book final export and source-opening polish",
                "Print/PDF final evidence for sale and purchase invoices",
                "Sale/Billing mixed-payment and reversal QA",
                "Purchase inward/vendor payment live reconciliation QA"
            });
    }

    private static PostImportValidationMetricDto[] BuildMetrics(
        List<SaleRow> sales,
        List<PurchaseRow> purchase,
        List<ItemRow> saleItems,
        List<ItemRow> purchaseItems,
        List<PaymentRow> salePayments,
        List<PaymentRow> purchasePayments,
        List<StockMovementRow> saleMovements,
        List<StockMovementRow> purchaseMovements,
        List<ImportBatchRow> purchaseImportBatches,
        int negativeStockCount)
    {
        var vyaparCount = sales.Count(item => item.Remarks.Contains("VyaparSaleImport", StringComparison.OrdinalIgnoreCase));
        var outputTax = saleItems.Sum(item => item.TaxAmount);
        var inputTax = purchaseItems.Sum(item => item.TaxAmount);
        return new[]
        {
            new PostImportValidationMetricDto("Sales invoices", sales.Count, sales.Sum(item => item.BillAmount), $"{vyaparCount} Vyapar imported"),
            new PostImportValidationMetricDto("Purchase inwards", purchase.Count, purchase.Sum(item => item.BillAmount), $"{purchaseImportBatches.Count} purchase imports posted"),
            new PostImportValidationMetricDto("Output GST", saleItems.Count, outputTax, "Sale item GST snapshot"),
            new PostImportValidationMetricDto("Input GST", purchaseItems.Count, inputTax, "Purchase item GST snapshot"),
            new PostImportValidationMetricDto("Net GST payable", 0, Math.Max(0, outputTax - inputTax), outputTax >= inputTax ? "Output less input" : "Input credit is higher"),
            new PostImportValidationMetricDto("Customer receipts", salePayments.Count, salePayments.Sum(item => item.Amount), "Invoice payment rows"),
            new PostImportValidationMetricDto("Vendor payments", purchasePayments.Count, purchasePayments.Sum(item => item.Amount), "Purchase payment rows"),
            new PostImportValidationMetricDto("Stock movement quantity", saleMovements.Count + purchaseMovements.Count, purchaseMovements.Sum(item => item.QuantityIn) - saleMovements.Sum(item => item.QuantityOut), $"In {purchaseMovements.Sum(item => item.QuantityIn):n2} / Out {saleMovements.Sum(item => item.QuantityOut):n2}"),
            new PostImportValidationMetricDto("Negative stock rows", negativeStockCount, 0, negativeStockCount == 0 ? "No negative current stock found" : "Needs stock correction")
        };
    }

    private static PostImportValidationTaxRowDto[] BuildGstRows(List<ItemRow> saleItems, List<ItemRow> purchaseItems)
    {
        return saleItems.Select(item => (Direction: "Output", Item: item))
            .Concat(purchaseItems.Select(item => (Direction: "Input", Item: item)))
            .GroupBy(row => new { row.Direction, TaxRate = row.Item.TaxRate })
            .OrderBy(row => row.Key.Direction)
            .ThenBy(row => row.Key.TaxRate)
            .Select(row => new PostImportValidationTaxRowDto(
                row.Key.Direction,
                row.Key.TaxRate,
                row.Sum(item => item.Item.BasePrice),
                row.Sum(item => item.Item.TaxAmount),
                row.Sum(item => item.Item.CgstAmount),
                row.Sum(item => item.Item.SgstAmount),
                row.Sum(item => item.Item.IgstAmount),
                row.Select(item => item.Item.InvoiceId).Distinct().Count(),
                row.Count()))
            .ToArray();
    }

    private static PostImportValidationPaymentRowDto[] BuildPaymentRows(List<PaymentRow> salePayments, List<PaymentRow> purchasePayments)
    {
        return salePayments.Select(item => (Direction: "Customer Receipt", Payment: item))
            .Concat(purchasePayments.Select(item => (Direction: "Vendor Payment", Payment: item)))
            .GroupBy(row => new { row.Direction, row.Payment.PaymentMode })
            .OrderBy(row => row.Key.Direction)
            .ThenBy(row => row.Key.PaymentMode)
            .Select(row => new PostImportValidationPaymentRowDto(row.Key.Direction, row.Key.PaymentMode.ToString(), row.Count(), row.Sum(item => item.Payment.Amount), row.Count(item => NeedsBankMapping(item.Payment))))
            .ToArray();
    }

    private static void AddPaymentCheck(List<PostImportValidationCheckDto> checks, List<PostImportValidationIssueDto> issues, List<PurchaseRow> purchase, List<PaymentRow> purchasePayments)
    {
        var actual = purchasePayments.Sum(item => item.Amount);
        checks.Add(new PostImportValidationCheckDto(
            "PURCHASE_PAYMENT_ROWS",
            "Purchase vendor payment rows",
            "Pass",
            "PurchaseInvoice does not persist a paid-total field; this evidence lists vendor payment rows and leaves vendor payable recalculation to the vendor reconciliation module.",
            null,
            actual,
            actual,
            0));
    }

    private static void AddAmountCheck(List<PostImportValidationCheckDto> checks, List<PostImportValidationIssueDto> issues, string code, string title, decimal expected, decimal actual, string area, string description, bool warningOnly)
    {
        expected = Round(expected);
        actual = Round(actual);
        var diff = Round(actual - expected);
        var ok = Math.Abs(diff) <= AmountTolerance;
        var status = ok ? "Pass" : warningOnly ? "Warning" : "Critical";
        checks.Add(new PostImportValidationCheckDto(code, title, status, description, null, expected, actual, diff));
        if (!ok)
        {
            issues.Add(Issue(status, area, code, title, null, expected, actual, diff, description));
        }
    }

    private static void AddQuantityCheck(List<PostImportValidationCheckDto> checks, List<PostImportValidationIssueDto> issues, string code, string title, decimal expected, decimal actual, string area, string description)
    {
        expected = Math.Round(expected, 3, MidpointRounding.AwayFromZero);
        actual = Math.Round(actual, 3, MidpointRounding.AwayFromZero);
        var diff = Math.Round(actual - expected, 3, MidpointRounding.AwayFromZero);
        var ok = Math.Abs(diff) <= QuantityTolerance;
        checks.Add(new PostImportValidationCheckDto(code, title, ok ? "Pass" : "Critical", description, null, expected, actual, diff));
        if (!ok)
        {
            issues.Add(Issue("Critical", area, code, title, null, expected, actual, diff, description));
        }
    }

    private static void AddCountCheck(List<PostImportValidationCheckDto> checks, List<PostImportValidationIssueDto> issues, string code, string title, int expected, int actual, string area, string description, bool warningOnly = false, bool expectedIsZero = false)
    {
        var ok = expectedIsZero ? actual == 0 : expected == actual;
        var diff = actual - expected;
        var status = ok ? "Pass" : warningOnly ? "Warning" : "Critical";
        checks.Add(new PostImportValidationCheckDto(code, title, status, description, null, expected, actual, diff));
        if (!ok)
        {
            issues.Add(Issue(status, area, code, title, null, expected, actual, diff, description));
        }
    }

    private static bool IsSaleStockMovement(StockMovementRow row)
    {
        return row.SourceType is "SalesInvoice" or "VyaparSaleImport" or "SalesInvoiceImport" or "SalesExchange"
            || row.MovementType.Contains("Sale", StringComparison.OrdinalIgnoreCase)
            || row.MovementType.Contains("Vyapar", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPurchaseStockMovement(StockMovementRow row)
    {
        return row.SourceType is "PurchaseInvoice" or "PurchaseInvoiceImport"
            || row.MovementType.Contains("PurchaseIn", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsBankMapping(PaymentRow row)
    {
        return row.BankAccountId is null && row.PaymentMode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;
    }

    private static IQueryable<T> ApplyCompanyFilter<T>(IQueryable<T> query, Guid? companyId) where T : class
    {
        return companyId.HasValue && companyId.Value != Guid.Empty ? query.Where(item => EF.Property<Guid>(item, "CompanyId") == companyId.Value) : query;
    }

    private static IQueryable<T> ApplyStoreFilter<T>(IQueryable<T> query, Guid? storeId) where T : class
    {
        return storeId.HasValue && storeId.Value != Guid.Empty ? query.Where(item => EF.Property<Guid>(item, "StoreId") == storeId.Value) : query;
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime EndExclusive) ResolveRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var start = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var endInclusive = (to ?? start.AddMonths(1).AddDays(-1)).Date;
        if (endInclusive < start)
        {
            endInclusive = start;
        }
        return (start, endInclusive, endInclusive.AddDays(1));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "Warning" => 1,
        "Info" => 2,
        "Pass" => 3,
        _ => 4
    };

    private static PostImportValidationIssueDto Issue(string severity, string area, string code, string title, string? reference, decimal? expected, decimal? actual, decimal? difference, string description)
        => new(severity, area, code, title, reference, expected, actual, difference, description);

    private static string FormatDecimal(decimal? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private sealed record SaleRow(Guid Id, Guid CompanyId, Guid StoreId, string InvoiceNumber, DateTime OnDate, decimal BillAmount, decimal PaidAmount, decimal BasePrice, decimal TaxAmount, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal Quantity, int ItemCount, string Remarks);
    private sealed record PurchaseRow(Guid Id, Guid CompanyId, Guid StoreGroupId, Guid? StoreId, string InvoiceNumber, string InwardNumber, DateTime InwardDate, decimal BillAmount, decimal BasePrice, decimal TaxAmount, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal FrightAmount, decimal RoundOff, decimal Quantity, int ItemCount, string VendorName);
    private sealed record ItemRow(Guid InvoiceId, string Barcode, string? HsnCode, decimal Quantity, decimal BasePrice, decimal TaxRate, decimal TaxAmount, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal Amount);
    private sealed record PaymentRow(Guid InvoiceId, PaymentMode PaymentMode, decimal Amount, Guid? BankAccountId, string Reference);
    private sealed record JournalSourceRow(string SourceType, Guid SourceId, string EntryNumber, string? ReferenceNumber);
    private sealed record StockMovementRow(Guid SourceId, string SourceType, string MovementType, decimal QuantityIn, decimal QuantityOut, decimal CostImpact);
    private sealed record ImportBatchRow(Guid Id, string SourceFileName, Guid? PostedPurchaseInvoiceId, string Status, string? AcceptanceStatus, string? CorrectionStatus, decimal BillAmount);
}

public sealed record PostImportValidationReportDto(
    DateTimeOffset GeneratedAtUtc,
    DateTime FromDate,
    DateTime ToDate,
    string Status,
    IReadOnlyList<PostImportValidationMetricDto> Metrics,
    IReadOnlyList<PostImportValidationCheckDto> Checks,
    IReadOnlyList<PostImportValidationIssueDto> Issues,
    IReadOnlyList<PostImportValidationTaxRowDto> GstRows,
    IReadOnlyList<PostImportValidationPaymentRowDto> PaymentRows,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record PostImportValidationMetricDto(string Label, decimal Count, decimal Amount, string Detail);
public sealed record PostImportValidationCheckDto(string Code, string Title, string Status, string Description, string? Reference, decimal? ExpectedValue, decimal? ActualValue, decimal? Difference);
public sealed record PostImportValidationIssueDto(string Severity, string Area, string Code, string Title, string? Reference, decimal? ExpectedValue, decimal? ActualValue, decimal? Difference, string Description);
public sealed record PostImportValidationTaxRowDto(string Direction, decimal TaxRate, decimal TaxableValue, decimal TaxAmount, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, int InvoiceCount, int LineCount);
public sealed record PostImportValidationPaymentRowDto(string Direction, string PaymentMode, int RowCount, decimal Amount, int MissingBankMappingCount);

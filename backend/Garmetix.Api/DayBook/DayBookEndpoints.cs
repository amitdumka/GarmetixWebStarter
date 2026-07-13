using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.DayBook;

public static class DayBookEndpoints
{
    private const int DayBookExportLimit = 5000;
    // GET /api/day-book
    // GET /api/day-book/{documentType}/{id}
    public static RouteGroupBuilder MapDayBookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/day-book")
            .WithTags("Day Book")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("", SearchAsync);
        group.MapGet("/export.csv", ExportCsvAsync);
        group.MapGet("/print", PrintAsync);
        group.MapGet("/{documentType}/{id:guid}", GetDetailAsync);
        return group;
    }

    private static async Task<DayBookResultDto> SearchAsync(
        HttpContext context,
        GarmetixDbContext db,
        string datePreset = "today",
        int? year = null,
        int? month = null,
        DateTime? from = null,
        DateTime? to = null,
        DateTime? date = null,
        string? q = null,
        string? type = null,
        bool includeJournal = false,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var (start, endInclusive) = ResolveRange(datePreset, year, month, from, to, date);
        var endExclusive = endInclusive.Date.AddDays(1);
        var rows = new List<DayBookRowDto>();

        var saleRows = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.CustomerName,
                item.CustomerMobileNumber,
                item.BillAmount,
                item.PaidAmount,
                item.InvoiceStatus,
                item.PaymentMode,
                item.CompanyId,
                StoreGroupId = (Guid?)null,
                StoreId = (Guid?)item.StoreId
            })
            .ToListAsync(cancellationToken);

        rows.AddRange(saleRows.Select(item => new DayBookRowDto(
            item.Id,
            "SaleInvoice",
            "Sales",
            item.InvoiceNumber,
            item.OnDate,
            Clean(item.CustomerName) ?? Clean(item.CustomerMobileNumber) ?? "Customer",
            $"Sale invoice {item.InvoiceNumber}",
            0m,
            item.BillAmount,
            item.BillAmount,
            item.PaymentMode?.ToString() ?? "-",
            item.InvoiceStatus.ToString(),
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            $"/billing?invoiceId={item.Id}",
            $"day-book/SaleInvoice/{item.Id}",
            "Open Sale Invoice")));

        var purchaseRows = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => item.InwardDate >= start && item.InwardDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                Number = item.InwardNumber,
                item.InvoiceNumber,
                OnDate = item.InwardDate,
                item.VendorName,
                item.BillAmount,
                item.InvoiceStatus,
                item.PaymentMode,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId
            })
            .ToListAsync(cancellationToken);

        rows.AddRange(purchaseRows.Select(item => new DayBookRowDto(
            item.Id,
            "PurchaseInward",
            "Purchase",
            item.Number,
            item.OnDate,
            Clean(item.VendorName) ?? "Vendor",
            $"Purchase inward {item.Number} / supplier invoice {item.InvoiceNumber}",
            item.BillAmount,
            0m,
            -item.BillAmount,
            item.PaymentMode?.ToString() ?? "-",
            item.InvoiceStatus.ToString(),
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            $"/purchase?purchaseInvoiceId={item.Id}",
            $"day-book/PurchaseInward/{item.Id}",
            "Open Purchase Inward")));

        var voucherRows = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
            .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                item.VoucherNumber,
                item.OnDate,
                item.VoucherType,
                item.PartyName,
                item.Particulars,
                item.Amount,
                item.PaymentMode,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId
            })
            .ToListAsync(cancellationToken);

        rows.AddRange(voucherRows.Select(item =>
        {
            var isReceipt = item.VoucherType == VoucherType.Receipt;
            return new DayBookRowDto(
                item.Id,
                "Voucher",
                item.VoucherType.ToString(),
                item.VoucherNumber,
                item.OnDate,
                Clean(item.PartyName) ?? "Party",
                Clean(item.Particulars) ?? $"{item.VoucherType} voucher",
                isReceipt ? 0m : item.Amount,
                isReceipt ? item.Amount : 0m,
                isReceipt ? item.Amount : -item.Amount,
                item.PaymentMode.ToString(),
                "Posted",
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                $"/vouchers?voucherId={item.Id}",
                $"day-book/Voucher/{item.Id}",
                "Open Voucher");
        }));

        var cashVoucherRows = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context)
            .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                item.VoucherNumber,
                item.OnDate,
                item.VoucherType,
                item.PartyName,
                item.Particulars,
                item.Amount,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId
            })
            .ToListAsync(cancellationToken);

        rows.AddRange(cashVoucherRows.Select(item =>
        {
            var isReceipt = item.VoucherType == VoucherType.Receipt;
            return new DayBookRowDto(
                item.Id,
                "CashVoucher",
                item.VoucherType.ToString(),
                item.VoucherNumber,
                item.OnDate,
                Clean(item.PartyName) ?? "Cash",
                Clean(item.Particulars) ?? "Cash voucher",
                isReceipt ? 0m : item.Amount,
                isReceipt ? item.Amount : 0m,
                isReceipt ? item.Amount : -item.Amount,
                "Cash",
                "Posted",
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                $"/cash-vouchers?cashVoucherId={item.Id}",
                $"day-book/CashVoucher/{item.Id}",
                "Open Cash Voucher");
        }));

        var customerPayments = await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
            .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                item.InvoiceId,
                item.OnDate,
                item.Amount,
                item.PaymentMode,
                item.ReferenceNumber,
                item.CompanyId,
                StoreGroupId = (Guid?)null,
                StoreId = (Guid?)item.StoreId
            })
            .ToListAsync(cancellationToken);

        rows.AddRange(customerPayments.Select(item => new DayBookRowDto(
            item.Id,
            "CustomerReceipt",
            "Customer Receipt",
            item.ReferenceNumber ?? item.Id.ToString("N")[..10],
            item.OnDate,
            "Customer",
            $"Customer receipt against sale {item.InvoiceId}",
            0m,
            item.Amount,
            item.Amount,
            item.PaymentMode.ToString(),
            "Posted",
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            $"/billing?paymentId={item.Id}&invoiceId={item.InvoiceId}",
            $"day-book/CustomerReceipt/{item.Id}",
            "Open Receipt")));

        var vendorPayments = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                item.PurchaseInvoiceId,
                item.VendorId,
                item.OnDate,
                item.Amount,
                item.PaymentMode,
                item.ReferenceNumber,
                item.Remarks,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId
            })
            .ToListAsync(cancellationToken);
        var vendorPaymentVendorIds = vendorPayments.Select(item => item.VendorId).Distinct().ToArray();
        var vendorPaymentVendors = vendorPaymentVendorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Vendors.AsNoTracking()
                .Where(item => vendorPaymentVendorIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        rows.AddRange(vendorPayments.Select(item =>
        {
            vendorPaymentVendors.TryGetValue(item.VendorId, out var vendorName);
            var sourcePath = item.PurchaseInvoiceId == Guid.Empty
                ? $"/vendor-payments?paymentId={item.Id}"
                : $"/vendor-payments?paymentId={item.Id}&purchaseInvoiceId={item.PurchaseInvoiceId}";
            return new DayBookRowDto(
                item.Id,
                "VendorPayment",
                "Vendor Payment",
                item.ReferenceNumber ?? item.Id.ToString("N")[..10],
                item.OnDate,
                vendorName ?? "Vendor",
                Clean(item.Remarks) ?? (item.PurchaseInvoiceId == Guid.Empty ? $"Vendor advance payment to {vendorName ?? "Vendor"}" : $"Vendor payment against purchase {item.PurchaseInvoiceId}"),
                item.Amount,
                0m,
                -item.Amount,
                item.PaymentMode.ToString(),
                "Posted",
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                sourcePath,
                $"day-book/VendorPayment/{item.Id}",
                "Open Vendor Payment");
        }));

        var salaryPayments = await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= start && item.OnDate < endExclusive)
            .Select(item => new
            {
                item.Id,
                item.EmployeeId,
                item.VoucherNumber,
                item.OnDate,
                item.SalaryComponent,
                item.Amount,
                item.PaymentMode,
                item.Remarks,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId
            })
            .ToListAsync(cancellationToken);
        var salaryPaymentEmployeeIds = salaryPayments.Select(item => item.EmployeeId).Distinct().ToArray();
        var salaryPaymentEmployeeNames = salaryPaymentEmployeeIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Employees.AsNoTracking()
                .Where(item => salaryPaymentEmployeeIds.Contains(item.Id))
                .Select(item => new { item.Id, item.FirstName, item.LastName })
                .ToDictionaryAsync(item => item.Id, item => $"{item.FirstName} {item.LastName}".Trim(), cancellationToken);

        rows.AddRange(salaryPayments.Select(item =>
        {
            salaryPaymentEmployeeNames.TryGetValue(item.EmployeeId, out var employeeName);
            employeeName = Clean(employeeName) ?? "Employee";
            return new DayBookRowDto(
                item.Id,
                "SalaryPayment",
                "Salary Payment",
                item.VoucherNumber,
                item.OnDate,
                employeeName,
                Clean(item.Remarks) ?? $"{item.SalaryComponent} payment to {employeeName}",
                item.Amount,
                0m,
                -item.Amount,
                item.PaymentMode.ToString(),
                "Posted",
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                $"/attendance/salary-payment?paymentId={item.Id}",
                $"day-book/SalaryPayment/{item.Id}",
                "Open Salary Payment");
        }));

        var showJournalEntries = includeJournal || string.Equals(type, "journal", StringComparison.OrdinalIgnoreCase);
        if (showJournalEntries)
        {
            var journalRows = await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
                .Select(item => new
                {
                    item.Id,
                    item.EntryNumber,
                    item.OnDate,
                    item.SourceType,
                    item.SourceId,
                    item.ReferenceNumber,
                    item.Narration,
                    item.Posted,
                    item.CompanyId,
                    item.StoreGroupId,
                    item.StoreId
                })
                .ToListAsync(cancellationToken);

            rows.AddRange(journalRows.Select(item => new DayBookRowDto(
                item.Id,
                "JournalEntry",
                string.IsNullOrWhiteSpace(item.SourceType) ? "Journal" : item.SourceType,
                item.EntryNumber,
                item.OnDate,
                "Ledger",
                Clean(item.Narration) ?? item.ReferenceNumber ?? "Journal entry",
                0m,
                0m,
                0m,
                "Journal",
                item.Posted ? "Posted" : "Draft",
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                $"/accounting?journalEntryId={item.Id}",
                $"day-book/JournalEntry/{item.Id}",
                "Open Journal")));
        }

        rows = ApplyTypeFilter(rows, type).ToList();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            rows = rows.Where(item =>
                item.DocumentNumber.ToLowerInvariant().Contains(term)
                || item.PartyName.ToLowerInvariant().Contains(term)
                || item.Particulars.ToLowerInvariant().Contains(term)
                || item.DocumentType.ToLowerInvariant().Contains(term)
                || item.DocumentSubType.ToLowerInvariant().Contains(term)
                || item.PaymentMode.ToLowerInvariant().Contains(term)
                || item.Status.ToLowerInvariant().Contains(term)).ToList();
        }

        var total = rows.Count;
        var summary = new DayBookSummaryDto(
            total,
            rows.Sum(item => item.DebitAmount),
            rows.Sum(item => item.CreditAmount),
            rows.Sum(item => item.NetAmount),
            rows.Count(item => item.DocumentType == "SaleInvoice"),
            rows.Count(item => item.DocumentType == "PurchaseInward"),
            rows.Count(item => item.DocumentType is "Voucher" or "CashVoucher"),
            rows.Count(item => item.DocumentType is "CustomerReceipt" or "VendorPayment" or "SalaryPayment"),
            rows.Count(item => item.DocumentType == "JournalEntry"));

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, DayBookExportLimit);
        var paged = rows
            .OrderByDescending(item => item.OnDate)
            .ThenBy(item => item.DocumentType)
            .ThenBy(item => item.DocumentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new DayBookResultDto(paged, summary, page, pageSize, total, start.Date, endInclusive.Date);
    }


    private static async Task<IResult> ExportCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        string datePreset = "today",
        int? year = null,
        int? month = null,
        DateTime? from = null,
        DateTime? to = null,
        DateTime? date = null,
        string? q = null,
        string? type = null,
        bool includeJournal = false,
        CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync(context, db, datePreset, year, month, from, to, date, q, type, includeJournal, 1, DayBookExportLimit, cancellationToken);
        var rows = new List<string[]>();
        rows.Add(["Section", "Field", "Value"]);
        rows.Add(["Header", "GeneratedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)]);
        rows.Add(["Header", "Range", $"{result.From:yyyy-MM-dd} to {result.To:yyyy-MM-dd}"]);
        rows.Add(["Header", "FilterType", string.IsNullOrWhiteSpace(type) ? "all" : type.Trim()]);
        rows.Add(["Header", "Search", q?.Trim() ?? string.Empty]);
        rows.Add(["Header", "ExportedRows", result.Rows.Count.ToString(CultureInfo.InvariantCulture)]);
        rows.Add(["Header", "TotalMatchingRows", result.Total.ToString(CultureInfo.InvariantCulture)]);
        rows.Add(["Summary", "DebitAmount", FormatAmount(result.Summary.DebitAmount)]);
        rows.Add(["Summary", "CreditAmount", FormatAmount(result.Summary.CreditAmount)]);
        rows.Add(["Summary", "NetAmount", FormatAmount(result.Summary.NetAmount)]);
        rows.Add(["Summary", "Sales", result.Summary.SaleCount.ToString(CultureInfo.InvariantCulture)]);
        rows.Add(["Summary", "Purchases", result.Summary.PurchaseCount.ToString(CultureInfo.InvariantCulture)]);
        rows.Add(["Summary", "Vouchers", result.Summary.VoucherCount.ToString(CultureInfo.InvariantCulture)]);
        rows.Add(["Summary", "Payments", result.Summary.PaymentCount.ToString(CultureInfo.InvariantCulture)]);
        rows.Add(["Summary", "JournalEntries", result.Summary.JournalCount.ToString(CultureInfo.InvariantCulture)]);
        rows.Add([]);
        rows.Add(["Date", "DocumentType", "DocumentSubType", "DocumentNumber", "PartyName", "Particulars", "DebitAmount", "CreditAmount", "NetAmount", "PaymentMode", "Status", "Id", "SourcePath"]);
        foreach (var row in result.Rows.OrderBy(item => item.OnDate).ThenBy(item => item.DocumentType).ThenBy(item => item.DocumentNumber))
        {
            rows.Add([
                row.OnDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                row.DocumentType,
                row.DocumentSubType,
                row.DocumentNumber,
                row.PartyName,
                row.Particulars,
                FormatAmount(row.DebitAmount),
                FormatAmount(row.CreditAmount),
                FormatAmount(row.NetAmount),
                row.PaymentMode,
                row.Status,
                row.Id.ToString(),
                row.SourcePath
            ]);
        }

        var fileName = $"garmetix-day-book-{result.From:yyyyMMdd}-{result.To:yyyyMMdd}.csv";
        return Results.File(CsvBytes(rows), "text/csv", fileName);
    }

    private static async Task<IResult> PrintAsync(
        HttpContext context,
        GarmetixDbContext db,
        string datePreset = "today",
        int? year = null,
        int? month = null,
        DateTime? from = null,
        DateTime? to = null,
        DateTime? date = null,
        string? q = null,
        string? type = null,
        bool includeJournal = false,
        CancellationToken cancellationToken = default)
    {
        var result = await SearchAsync(context, db, datePreset, year, month, from, to, date, q, type, includeJournal, 1, DayBookExportLimit, cancellationToken);
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Garmetix Day Book</title>");
        html.AppendLine("<style>body{font-family:Arial,sans-serif;margin:24px;color:#111827}h1{margin:0 0 4px}p{margin:4px 0;color:#4b5563}table{border-collapse:collapse;width:100%;font-size:12px;margin-top:16px}th,td{border:1px solid #d1d5db;padding:6px;vertical-align:top}th{background:#f3f4f6;text-align:left}.right{text-align:right}.summary{display:grid;grid-template-columns:repeat(4,1fr);gap:8px;margin:16px 0}.box{border:1px solid #d1d5db;padding:8px}.muted{color:#6b7280}.no-print{margin-bottom:12px}@media print{.no-print{display:none}body{margin:8mm}}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine("<button class=\"no-print\" onclick=\"window.print()\">Print / Save PDF</button>");
        html.AppendLine("<h1>Garmetix Day Book</h1>");
        html.AppendLine($"<p>Range: {Encode(result.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))} to {Encode(result.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}</p>");
        html.AppendLine($"<p>Generated: {Encode(DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))} · Rows: {result.Rows.Count} of {result.Total}</p>");
        html.AppendLine("<div class=\"summary\">");
        html.AppendLine($"<div class=\"box\"><span class=\"muted\">Transactions</span><br><strong>{result.Summary.Count}</strong></div>");
        html.AppendLine($"<div class=\"box\"><span class=\"muted\">Debit</span><br><strong>{Encode(FormatAmount(result.Summary.DebitAmount))}</strong></div>");
        html.AppendLine($"<div class=\"box\"><span class=\"muted\">Credit</span><br><strong>{Encode(FormatAmount(result.Summary.CreditAmount))}</strong></div>");
        html.AppendLine($"<div class=\"box\"><span class=\"muted\">Net</span><br><strong>{Encode(FormatAmount(result.Summary.NetAmount))}</strong></div>");
        html.AppendLine("</div>");
        html.AppendLine("<table><thead><tr><th>Date</th><th>Type</th><th>No.</th><th>Party</th><th>Particulars</th><th class=\"right\">Debit</th><th class=\"right\">Credit</th><th>Mode</th><th>Status</th><th>ID</th></tr></thead><tbody>");
        foreach (var row in result.Rows.OrderBy(item => item.OnDate).ThenBy(item => item.DocumentType).ThenBy(item => item.DocumentNumber))
        {
            html.AppendLine("<tr>" +
                $"<td>{Encode(row.OnDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}</td>" +
                $"<td>{Encode(row.DocumentSubType)}</td>" +
                $"<td>{Encode(row.DocumentNumber)}</td>" +
                $"<td>{Encode(row.PartyName)}</td>" +
                $"<td>{Encode(row.Particulars)}</td>" +
                $"<td class=\"right\">{Encode(row.DebitAmount == 0 ? "-" : FormatAmount(row.DebitAmount))}</td>" +
                $"<td class=\"right\">{Encode(row.CreditAmount == 0 ? "-" : FormatAmount(row.CreditAmount))}</td>" +
                $"<td>{Encode(row.PaymentMode)}</td>" +
                $"<td>{Encode(row.Status)}</td>" +
                $"<td>{Encode(row.Id.ToString())}</td>" +
                "</tr>");
        }
        html.AppendLine("</tbody></table>");
        html.AppendLine("<p class=\"muted\">Day Book print is evidence only. Final GST/accounting closeout must still be signed from the validation pages.</p>");
        html.AppendLine("</body></html>");
        return Results.Content(html.ToString(), "text/html; charset=utf-8");
    }

    private static async Task<IResult> GetDetailAsync(
        HttpContext context,
        GarmetixDbContext db,
        string documentType,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var normalized = documentType.Trim().ToLowerInvariant();
        return normalized switch
        {
            "saleinvoice" => await SaleDetailAsync(context, db, id, cancellationToken),
            "purchaseinward" => await PurchaseDetailAsync(context, db, id, cancellationToken),
            "voucher" => await VoucherDetailAsync(context, db, id, cancellationToken),
            "cashvoucher" => await CashVoucherDetailAsync(context, db, id, cancellationToken),
            "customerreceipt" => await CustomerReceiptDetailAsync(context, db, id, cancellationToken),
            "vendorpayment" => await VendorPaymentDetailAsync(context, db, id, cancellationToken),
            "salarypayment" => await SalaryPaymentDetailAsync(context, db, id, cancellationToken),
            "journalentry" => await JournalDetailAsync(context, db, id, cancellationToken),
            _ => Results.BadRequest(new { message = "Unsupported Day Book document type." })
        };
    }

    private static async Task<IResult> SaleDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null) return Results.NotFound(new { message = "Sale invoice not found." });
        var items = await db.InvoiceItems.AsNoTracking().Where(item => item.InvoiceId == id).Select(item => new { LineType = "Item", item.Barcode, item.ProductName, Quantity = item.BilledQuantity, item.MRP, item.DiscountAmount, item.BasePrice, item.TaxAmount, item.Amount }).ToListAsync(cancellationToken);
        var payments = await db.InvoicePayments.AsNoTracking().Where(item => item.InvoiceId == id).Select(item => new { LineType = "Payment", item.OnDate, item.Amount, item.PaymentMode, item.ReferenceNumber }).ToListAsync(cancellationToken);
        var detail = new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoice.CustomerName,
            invoice.CustomerMobileNumber,
            invoice.CustomerGSTIN,
            invoice.InvoiceType,
            invoice.InvoiceStatus,
            invoice.Quantity,
            invoice.ItemCount,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.TaxAmount,
            invoice.NetAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            invoice.PaymentMode,
            invoice.CreditSale,
            invoice.Remarks
        };
        var row = new DayBookRowDto(id, "SaleInvoice", "Sales", invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName ?? invoice.CustomerMobileNumber, invoice.Remarks ?? $"Sale invoice {invoice.InvoiceNumber}", 0m, invoice.BillAmount, invoice.BillAmount, invoice.PaymentMode?.ToString() ?? "-", invoice.InvoiceStatus.ToString(), invoice.CompanyId, null, invoice.StoreId, $"/billing?invoiceId={id}", $"day-book/SaleInvoice/{id}", "Open Sale Invoice");
        return Results.Ok(new DayBookDetailDto(row, detail, items.Cast<object>().Concat(payments.Cast<object>()).ToList(), row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> PurchaseDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null) return Results.NotFound(new { message = "Purchase inward not found." });
        var items = await db.PurchaseInvoiceItems.AsNoTracking().Where(item => item.InvoiceId == id).Select(item => new { LineType = "Item", item.Barcode, item.ProductName, Quantity = item.BilledQuantity, item.MRP, item.DiscountAmount, item.BasePrice, item.TaxAmount, item.Amount }).ToListAsync(cancellationToken);
        var payments = await db.PurchasePayments.AsNoTracking().Where(item => item.PurchaseInvoiceId == id).Select(item => new { LineType = "Payment", item.OnDate, item.Amount, item.PaymentMode, item.ReferenceNumber, item.Remarks }).ToListAsync(cancellationToken);
        var paidAmount = payments.Sum(item => item.Amount);
        var detail = new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            invoice.OnDate,
            invoice.InwardDate,
            invoice.SupplierInvoiceDate,
            invoice.VendorName,
            invoice.VendorGSTIN,
            invoice.InvoiceType,
            invoice.InvoiceStatus,
            invoice.Quantity,
            invoice.ItemCount,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.FrightAmount,
            invoice.TaxAmount,
            invoice.NetAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            PaidAmount = paidAmount,
            BalanceAmount = invoice.BillAmount - paidAmount,
            invoice.PaymentMode,
            invoice.DueDate
        };
        var row = new DayBookRowDto(id, "PurchaseInward", "Purchase", invoice.InwardNumber, invoice.InwardDate, invoice.VendorName ?? "Vendor", $"Purchase inward {invoice.InwardNumber}", invoice.BillAmount, 0m, -invoice.BillAmount, invoice.PaymentMode?.ToString() ?? "-", invoice.InvoiceStatus.ToString(), invoice.CompanyId, invoice.StoreGroupId, invoice.StoreId, $"/purchase?purchaseInvoiceId={id}", $"day-book/PurchaseInward/{id}", "Open Purchase Inward");
        return Results.Ok(new DayBookDetailDto(row, detail, items.Cast<object>().Concat(payments.Cast<object>()).ToList(), row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> VoucherDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var voucher = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (voucher is null) return Results.NotFound(new { message = "Voucher not found." });
        var isReceipt = voucher.VoucherType == VoucherType.Receipt;
        var detail = new { voucher.Id, voucher.VoucherNumber, voucher.OnDate, voucher.VoucherType, voucher.PartyName, voucher.Particulars, voucher.Amount, voucher.PaymentMode, voucher.PaymentDetails, voucher.SlipNumber, voucher.Remarks };
        var row = new DayBookRowDto(id, "Voucher", voucher.VoucherType.ToString(), voucher.VoucherNumber, voucher.OnDate, voucher.PartyName, voucher.Particulars, isReceipt ? 0m : voucher.Amount, isReceipt ? voucher.Amount : 0m, isReceipt ? voucher.Amount : -voucher.Amount, voucher.PaymentMode.ToString(), "Posted", voucher.CompanyId, voucher.StoreGroupId, voucher.StoreId, $"/vouchers?voucherId={id}", $"day-book/Voucher/{id}", "Open Voucher");
        return Results.Ok(new DayBookDetailDto(row, detail, Array.Empty<object>(), row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> CashVoucherDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var voucher = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (voucher is null) return Results.NotFound(new { message = "Cash voucher not found." });
        var isReceipt = voucher.VoucherType == VoucherType.Receipt;
        var detail = new { voucher.Id, voucher.VoucherNumber, voucher.OnDate, voucher.VoucherType, voucher.PartyName, voucher.Particulars, voucher.Amount, PaymentMode = "Cash", voucher.SlipNumber, voucher.Remarks };
        var row = new DayBookRowDto(id, "CashVoucher", voucher.VoucherType.ToString(), voucher.VoucherNumber, voucher.OnDate, voucher.PartyName, voucher.Particulars, isReceipt ? 0m : voucher.Amount, isReceipt ? voucher.Amount : 0m, isReceipt ? voucher.Amount : -voucher.Amount, "Cash", "Posted", voucher.CompanyId, voucher.StoreGroupId, voucher.StoreId, $"/cash-vouchers?cashVoucherId={id}", $"day-book/CashVoucher/{id}", "Open Cash Voucher");
        return Results.Ok(new DayBookDetailDto(row, detail, Array.Empty<object>(), row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> CustomerReceiptDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var payment = await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (payment is null) return Results.NotFound(new { message = "Customer receipt not found." });
        var invoice = await db.SalesInvoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.InvoiceId, cancellationToken);
        var detail = new { payment.Id, payment.InvoiceId, payment.OnDate, payment.Amount, payment.PaymentMode, payment.ReferenceNumber, payment.AdjustmentSourceType, payment.GatewayReference, payment.SettlementStatus, InvoiceNumber = invoice?.InvoiceNumber, CustomerName = invoice?.CustomerName, CustomerMobileNumber = invoice?.CustomerMobileNumber };
        var related = invoice is null ? Array.Empty<object>() : new object[] { new { LineType = "Sale Invoice", invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerMobileNumber, invoice.BillAmount, invoice.PaidAmount, invoice.InvoiceStatus } };
        var row = new DayBookRowDto(id, "CustomerReceipt", "Customer Receipt", payment.ReferenceNumber ?? id.ToString("N")[..10], payment.OnDate, invoice?.CustomerName ?? "Customer", $"Receipt against sale {invoice?.InvoiceNumber ?? payment.InvoiceId.ToString()}", 0m, payment.Amount, payment.Amount, payment.PaymentMode.ToString(), "Posted", payment.CompanyId, null, payment.StoreId, $"/billing?paymentId={id}&invoiceId={payment.InvoiceId}", $"day-book/CustomerReceipt/{id}", "Open Receipt");
        return Results.Ok(new DayBookDetailDto(row, detail, related, row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> VendorPaymentDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var payment = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (payment is null) return Results.NotFound(new { message = "Vendor payment not found." });
        var invoice = await db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.PurchaseInvoiceId, cancellationToken);
        var vendor = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.VendorId, cancellationToken);
        var vendorName = invoice?.VendorName ?? vendor?.Name ?? "Vendor";
        var sourcePath = payment.PurchaseInvoiceId == Guid.Empty
            ? $"/vendor-payments?paymentId={id}"
            : $"/vendor-payments?paymentId={id}&purchaseInvoiceId={payment.PurchaseInvoiceId}";
        var detail = new { payment.Id, payment.PurchaseInvoiceId, payment.VendorId, payment.OnDate, payment.Amount, payment.PaymentMode, payment.ReferenceNumber, payment.Remarks, InvoiceNumber = invoice?.InvoiceNumber, InwardNumber = invoice?.InwardNumber, VendorName = vendorName };
        var related = invoice is null ? Array.Empty<object>() : new object[] { new { LineType = "Purchase Inward", invoice.Id, invoice.InvoiceNumber, invoice.InwardNumber, invoice.InwardDate, invoice.VendorName, invoice.BillAmount, invoice.InvoiceStatus } };
        var row = new DayBookRowDto(id, "VendorPayment", "Vendor Payment", payment.ReferenceNumber ?? id.ToString("N")[..10], payment.OnDate, vendorName, payment.Remarks ?? (payment.PurchaseInvoiceId == Guid.Empty ? $"Vendor advance payment to {vendorName}" : $"Payment against purchase {invoice?.InwardNumber ?? payment.PurchaseInvoiceId.ToString()}"), payment.Amount, 0m, -payment.Amount, payment.PaymentMode.ToString(), "Posted", payment.CompanyId, payment.StoreGroupId, payment.StoreId, sourcePath, $"day-book/VendorPayment/{id}", "Open Vendor Payment");
        return Results.Ok(new DayBookDetailDto(row, detail, related, row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> SalaryPaymentDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var payment = await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null) return Results.NotFound(new { message = "Salary payment not found." });
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.EmployeeId, cancellationToken);
        var employeeName = Clean($"{employee?.FirstName} {employee?.LastName}".Trim()) ?? "Employee";
        var detail = new { payment.Id, payment.EmployeeId, EmployeeName = employeeName, payment.VoucherNumber, payment.OnDate, payment.SalaryMonth, payment.SalaryComponent, payment.GrossSalary, payment.TotalDeductions, payment.NetSalary, payment.Amount, payment.PaymentMode, payment.Remarks };
        var row = new DayBookRowDto(id, "SalaryPayment", "Salary Payment", payment.VoucherNumber, payment.OnDate, employeeName, Clean(payment.Remarks) ?? $"{payment.SalaryComponent} payment to {employeeName}", payment.Amount, 0m, -payment.Amount, payment.PaymentMode.ToString(), "Posted", payment.CompanyId, payment.StoreGroupId, payment.StoreId, $"/attendance/salary-payment?paymentId={id}", $"day-book/SalaryPayment/{id}", "Open Salary Payment");
        return Results.Ok(new DayBookDetailDto(row, detail, Array.Empty<object>(), row.SourcePath, row.OpenActionLabel));
    }

    private static async Task<IResult> JournalDetailAsync(HttpContext context, GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var journal = await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (journal is null) return Results.NotFound(new { message = "Journal entry not found." });
        var lines = await db.JournalLines.AsNoTracking().Where(item => item.JournalEntryId == id).Select(item => new { item.LedgerId, item.PartyId, item.EmployeeId, item.Debit, item.Credit, item.Narration }).ToListAsync(cancellationToken);
        var detail = new { journal.Id, journal.EntryNumber, journal.OnDate, journal.SourceType, journal.SourceId, journal.ReferenceNumber, journal.Narration, journal.Posted, journal.PostedAt, journal.PostedBy, DebitAmount = lines.Sum(item => item.Debit), CreditAmount = lines.Sum(item => item.Credit) };
        var row = new DayBookRowDto(id, "JournalEntry", string.IsNullOrWhiteSpace(journal.SourceType) ? "Journal" : journal.SourceType, journal.EntryNumber, journal.OnDate, "Ledger", journal.Narration, lines.Sum(item => item.Debit), lines.Sum(item => item.Credit), lines.Sum(item => item.Credit - item.Debit), "Journal", journal.Posted ? "Posted" : "Draft", journal.CompanyId, journal.StoreGroupId, journal.StoreId, $"/accounting?journalEntryId={id}", $"day-book/JournalEntry/{id}", "Open Journal");
        return Results.Ok(new DayBookDetailDto(row, detail, lines.Cast<object>().ToList(), row.SourcePath, row.OpenActionLabel));
    }

    private static IEnumerable<DayBookRowDto> ApplyTypeFilter(IEnumerable<DayBookRowDto> rows, string? type)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Equals("all", StringComparison.OrdinalIgnoreCase)) return rows;
        return type.Trim().ToLowerInvariant() switch
        {
            "sales" => rows.Where(item => item.DocumentType == "SaleInvoice" || item.DocumentType == "CustomerReceipt"),
            "purchase" => rows.Where(item => item.DocumentType == "PurchaseInward" || item.DocumentType == "VendorPayment"),
            "vouchers" => rows.Where(item => item.DocumentType is "Voucher" or "CashVoucher"),
            "payments" => rows.Where(item => item.DocumentType is "CustomerReceipt" or "VendorPayment" or "SalaryPayment"),
            "salary" => rows.Where(item => item.DocumentType == "SalaryPayment"),
            "journal" => rows.Where(item => item.DocumentType == "JournalEntry"),
            _ => rows.Where(item => item.DocumentType.Equals(type, StringComparison.OrdinalIgnoreCase) || item.DocumentSubType.Equals(type, StringComparison.OrdinalIgnoreCase))
        };
    }

    private static (DateTime Start, DateTime EndInclusive) ResolveRange(string preset, int? year, int? month, DateTime? from, DateTime? to, DateTime? date)
    {
        var today = DateTime.Today;
        var normalized = (preset ?? "today").Trim().ToLowerInvariant();
        return normalized switch
        {
            "date" or "day" or "single-day" => (date?.Date ?? from?.Date ?? today, date?.Date ?? from?.Date ?? today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "month" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            "last-month" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "year" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            "month-year" => ResolveMonthYear(year, month, today),
            "custom" => (from?.Date ?? today, to?.Date ?? from?.Date ?? today),
            _ => (today, today)
        };
    }

    private static (DateTime Start, DateTime EndInclusive) ResolveMonthYear(int? year, int? month, DateTime today)
    {
        var y = year.GetValueOrDefault(today.Year);
        var m = month.GetValueOrDefault(today.Month);
        if (m < 1 || m > 12) m = today.Month;
        var start = new DateTime(y, m, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }


    private static byte[] CsvBytes(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Csv)));
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r') ? $"\"{escaped}\"" : escaped;
    }

    private static string FormatAmount(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Audit;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Production;

public sealed record PrintAcceptanceDocumentDto(
    string Key,
    string Label,
    string Area,
    int Count,
    string? LatestNumber,
    Guid? LatestId,
    DateTime? LatestDate,
    string Status,
    string? Endpoint,
    string Message);

public sealed record PrintAcceptanceScenarioDto(
    string Key,
    string Label,
    string Area,
    string PaperSize,
    bool Required,
    string ExpectedEvidence,
    string? SampleEndpoint);

public sealed record PrintAcceptanceEvidenceItemDto(
    string Key,
    string Label,
    string Area,
    string PaperSize,
    bool Checked,
    string Result,
    string? Endpoint,
    string? Remarks);

public sealed record PrintAcceptanceEvidenceRequestDto(
    string? OperatorName,
    string? LiveBaseUrl,
    string? BrowserName,
    string? PrinterName,
    string? Note,
    IReadOnlyList<PrintAcceptanceEvidenceItemDto>? Items);

public sealed record PrintAcceptanceEvidenceResultDto(
    Guid Id,
    string Reference,
    DateTimeOffset AcceptedAtUtc,
    string Status,
    int PassedCount,
    int RequiredCount,
    IReadOnlyList<string> Warnings);

public sealed record PrintAcceptanceEvidenceSummaryDto(
    Guid Id,
    string Reference,
    DateTime OccurredAt,
    string Status,
    string? OperatorName,
    string? LiveBaseUrl,
    string? BrowserName,
    string? PrinterName,
    int PassedCount,
    int RequiredCount,
    string? Note);

public sealed record PrintAcceptanceClosureDto(
    DateTimeOffset CheckedAtUtc,
    string Status,
    bool Complete,
    int CoreSamplesReady,
    int CoreSamplesRequired,
    int MissingSampleCount,
    int LatestPassedCount,
    int RequiredEvidenceCount,
    string? LastAcceptedReference,
    DateTime? LastAcceptedAt,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> FinalCloseoutChecklist,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record PrintAcceptanceStatusDto(
    DateTimeOffset CheckedAtUtc,
    int ReadyCount,
    int TotalCount,
    IReadOnlyList<PrintAcceptanceDocumentDto> Documents,
    IReadOnlyList<PrintAcceptanceScenarioDto> Scenarios,
    IReadOnlyList<PrintAcceptanceEvidenceSummaryDto> RecentEvidence,
    IReadOnlyList<string> Recommendations,
    PrintAcceptanceClosureDto Closure);

public sealed record PrintAcceptanceEvidencePayload(
    string OperatorName,
    string LiveBaseUrl,
    string BrowserName,
    string PrinterName,
    string? Note,
    string Status,
    int PassedCount,
    int RequiredCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PrintAcceptanceEvidenceItemDto> Items);

public static class PrintAcceptanceEndpoints
{
    private const string EvidenceModule = "Print Acceptance";
    private const string EvidenceEntity = "PrintFinalAcceptanceEvidence";

    public static RouteGroupBuilder MapPrintAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/print-acceptance")
            .WithTags("Print Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);
        group.MapGet("/closure", ClosureAsync);
        group.MapGet("/evidence", EvidenceAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);
        group.MapPost("/evidence", SaveEvidenceAsync);

        return group;
    }

    private static async Task<PrintAcceptanceStatusDto> StatusAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var docs = new List<PrintAcceptanceDocumentDto>();

        var salesRows = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.ReturnInvoice)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.InvoiceNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "salesInvoice", "Sales Invoice PDF", "Sales", salesRows.Count, salesRows.FirstOrDefault()?.Number, salesRows.FirstOrDefault()?.Id, salesRows.FirstOrDefault()?.Date, salesRows.FirstOrDefault()?.Id is Guid salesId ? $"/api/billing/sales/{salesId}/pdf" : null);

        var salesReturnRows = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.ReturnInvoice)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.InvoiceNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "salesReturn", "Sales Return PDF", "Sales", salesReturnRows.Count, salesReturnRows.FirstOrDefault()?.Number, salesReturnRows.FirstOrDefault()?.Id, salesReturnRows.FirstOrDefault()?.Date, salesReturnRows.FirstOrDefault()?.Id is Guid salesReturnId ? $"/api/billing/sales/{salesReturnId}/pdf" : null);

        var voucherRows = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.VoucherNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "voucher", "Voucher PDF", "Accounting", voucherRows.Count, voucherRows.FirstOrDefault()?.Number, voucherRows.FirstOrDefault()?.Id, voucherRows.FirstOrDefault()?.Date, voucherRows.FirstOrDefault()?.Id is Guid voucherId ? $"/api/vouchers/{voucherId}/pdf" : null);

        var cashVoucherRows = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.VoucherNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "cashVoucher", "Cash Voucher PDF", "Off Book", cashVoucherRows.Count, cashVoucherRows.FirstOrDefault()?.Number, cashVoucherRows.FirstOrDefault()?.Id, cashVoucherRows.FirstOrDefault()?.Date, cashVoucherRows.FirstOrDefault()?.Id is Guid cashVoucherId ? $"/api/cash-vouchers/{cashVoucherId}/pdf" : null);

        var pettyRows = await db.PettyCashSheets.AsNoTracking()
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.OnDate.ToString("yyyy-MM-dd"), Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "pettyCash", "Petty Cash Sheet PDF", "Accounting", pettyRows.Count, pettyRows.FirstOrDefault()?.Number, pettyRows.FirstOrDefault()?.Id, pettyRows.FirstOrDefault()?.Date, pettyRows.FirstOrDefault()?.Id is Guid pettyId ? $"/api/petty-cash-sheets/{pettyId}/pdf" : null);

        var purchaseRows = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .OrderByDescending(item => item.InwardDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.InwardNumber, Date = item.InwardDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "purchaseInward", "Purchase Inward PDF", "Purchase", purchaseRows.Count, purchaseRows.FirstOrDefault()?.Number, purchaseRows.FirstOrDefault()?.Id, purchaseRows.FirstOrDefault()?.Date, purchaseRows.FirstOrDefault()?.Id is Guid purchaseId ? $"/api/purchase/invoices/{purchaseId}/pdf" : null);

        var purchaseReturnRows = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.ReturnNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "purchaseReturn", "Purchase Return PDF", "Purchase", purchaseReturnRows.Count, purchaseReturnRows.FirstOrDefault()?.Number, purchaseReturnRows.FirstOrDefault()?.Id, purchaseReturnRows.FirstOrDefault()?.Date, purchaseReturnRows.FirstOrDefault()?.Id is Guid purchaseReturnId ? $"/api/purchase/returns/{purchaseReturnId}/pdf" : null);

        var commercialNoteRows = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.NoteNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "commercialNote", "Debit / Credit Note PDF", "Accounting", commercialNoteRows.Count, commercialNoteRows.FirstOrDefault()?.Number, commercialNoteRows.FirstOrDefault()?.Id, commercialNoteRows.FirstOrDefault()?.Date, commercialNoteRows.FirstOrDefault()?.Id is Guid noteId ? $"/api/commercial-notes/{noteId}/pdf" : null);

        var nonGstRows = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.DocumentNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "nonGstGoods", "Non-GST Goods PDF", "Off Book", nonGstRows.Count, nonGstRows.FirstOrDefault()?.Number, nonGstRows.FirstOrDefault()?.Id, nonGstRows.FirstOrDefault()?.Date, nonGstRows.FirstOrDefault()?.Id is Guid nonGstId ? $"/api/non-gst-goods/documents/{nonGstId}/pdf" : null);

        var tailoringRows = await WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.OrderNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "tailoringOrder", "Tailoring Order / Invoice Print", "Tailoring", tailoringRows.Count, tailoringRows.FirstOrDefault()?.Number, tailoringRows.FirstOrDefault()?.Id, tailoringRows.FirstOrDefault()?.Date, tailoringRows.FirstOrDefault()?.Id is Guid tailoringId ? $"/api/tailoring/orders/{tailoringId}/print-order" : null);

        var payslipRows = await WorkspaceScope.ApplyTo(db.SalaryPaySlips.AsNoTracking(), context)
            .OrderByDescending(item => item.PayPeriodStart)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.MonthYear, Date = item.PayPeriodStart })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "salaryPayslip", "Salary Payslip PDF", "Payroll", payslipRows.Count, payslipRows.FirstOrDefault()?.Number, payslipRows.FirstOrDefault()?.Id, payslipRows.FirstOrDefault()?.Date, payslipRows.FirstOrDefault()?.Id is Guid payslipId ? $"/api/payroll/payslips/{payslipId}/pdf" : null);

        var salaryPaymentRows = await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.VoucherNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "salaryPayment", "Salary Payment Voucher PDF", "Payroll", salaryPaymentRows.Count, salaryPaymentRows.FirstOrDefault()?.Number, salaryPaymentRows.FirstOrDefault()?.Id, salaryPaymentRows.FirstOrDefault()?.Date, salaryPaymentRows.FirstOrDefault()?.Id is Guid salaryPaymentId ? $"/api/payroll/{salaryPaymentId}/pdf" : null);

        var gstrRows = await WorkspaceScope.ApplyTo(db.GstReturnDrafts.AsNoTracking(), context)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new { item.Id, Number = item.Form.ToUpper() + " " + item.ReturnPeriod, Date = item.UpdatedAt ?? item.CreatedAt })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "gstReturn", "GST Return Export / CA Review", "GST", gstrRows.Count, gstrRows.FirstOrDefault()?.Number, gstrRows.FirstOrDefault()?.Id, gstrRows.FirstOrDefault()?.Date, gstrRows.FirstOrDefault()?.Id is Guid gstId ? $"/api/gst-returns/drafts/{gstId}/excel" : null);

        var scenarios = BuildScenarios(docs);
        var recentEvidence = await LoadEvidenceSummariesAsync(db, 5, cancellationToken);
        var readyCount = docs.Count(item => string.Equals(item.Status, "Ready", StringComparison.OrdinalIgnoreCase));
        var closure = BuildClosure(docs, recentEvidence);
        var recommendations = new List<string>();
        if (readyCount < docs.Count)
        {
            recommendations.Add("Create at least one sample record in each missing area, then run print acceptance again.");
        }
        recommendations.Add("Open A4 and A5 sale/purchase PDFs from the live URL and verify them in browser print preview before saving evidence.");
        recommendations.Add("Use a large invoice/purchase sample to verify multi-page pagination, per-page summary, footer, signature, and final total blocks.");
        recommendations.Add("Save final acceptance evidence after real print/PDF preview so the handover record is stored in AuditLogEntries.");

        return new PrintAcceptanceStatusDto(
            DateTimeOffset.UtcNow,
            readyCount,
            docs.Count,
            docs,
            scenarios,
            recentEvidence,
            recommendations,
            closure);
    }

    private static async Task<PrintAcceptanceClosureDto> ClosureAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var status = await StatusAsync(context, db, cancellationToken);
        return status.Closure;
    }

    private static async Task<IReadOnlyList<PrintAcceptanceEvidenceSummaryDto>> EvidenceAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
        => await LoadEvidenceSummariesAsync(db, 25, cancellationToken);

    private static async Task<IResult> ExportEvidenceCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var status = await StatusAsync(context, db, cancellationToken);
        var rows = new List<string[]>();
        rows.Add(["Section", "Field", "Value"]);
        rows.Add(["Header", "GeneratedOnUtc", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)]);
        rows.Add(["Header", "ClosureStatus", status.Closure.Status]);
        rows.Add(["Header", "Complete", status.Closure.Complete ? "Yes" : "No"]);
        rows.Add(["Header", "CoreSamples", $"{status.Closure.CoreSamplesReady}/{status.Closure.CoreSamplesRequired}"]);
        rows.Add(["Header", "LatestEvidence", $"{status.Closure.LatestPassedCount}/{status.Closure.RequiredEvidenceCount}"]);
        rows.Add(["Header", "LastAcceptedReference", status.Closure.LastAcceptedReference ?? string.Empty]);
        rows.Add(["Header", "LastAcceptedAt", status.Closure.LastAcceptedAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty]);
        rows.Add([]);
        rows.Add(["DocumentKey", "Label", "Area", "Status", "LatestNumber", "LatestDate", "Count", "Endpoint"]);
        foreach (var doc in status.Documents)
        {
            rows.Add([
                doc.Key,
                doc.Label,
                doc.Area,
                doc.Status,
                doc.LatestNumber ?? string.Empty,
                doc.LatestDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                doc.Count.ToString(CultureInfo.InvariantCulture),
                doc.Endpoint ?? string.Empty
            ]);
        }

        rows.Add([]);
        rows.Add(["ScenarioKey", "Label", "Area", "PaperSize", "Required", "ExpectedEvidence", "SampleEndpoint"]);
        foreach (var scenario in status.Scenarios)
        {
            rows.Add([
                scenario.Key,
                scenario.Label,
                scenario.Area,
                scenario.PaperSize,
                scenario.Required ? "Yes" : "No",
                scenario.ExpectedEvidence,
                scenario.SampleEndpoint ?? string.Empty
            ]);
        }

        rows.Add([]);
        rows.Add(["EvidenceReference", "OccurredAt", "Status", "Operator", "LiveBaseUrl", "Browser", "Printer", "Passed", "Required", "Note"]);
        foreach (var evidence in status.RecentEvidence)
        {
            rows.Add([
                evidence.Reference,
                evidence.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                evidence.Status,
                evidence.OperatorName ?? string.Empty,
                evidence.LiveBaseUrl ?? string.Empty,
                evidence.BrowserName ?? string.Empty,
                evidence.PrinterName ?? string.Empty,
                evidence.PassedCount.ToString(CultureInfo.InvariantCulture),
                evidence.RequiredCount.ToString(CultureInfo.InvariantCulture),
                evidence.Note ?? string.Empty
            ]);
        }

        rows.Add([]);
        rows.Add(["BlockingIssue"]);
        foreach (var issue in status.Closure.BlockingIssues)
        {
            rows.Add([issue]);
        }

        var fileName = $"garmetix-print-final-acceptance-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return Results.File(CsvBytes(rows), "text/csv", fileName);
    }

    private static async Task<IResult> SaveEvidenceAsync(
        PrintAcceptanceEvidenceRequestDto request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var items = (request.Items ?? Array.Empty<PrintAcceptanceEvidenceItemDto>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToList();

        if (items.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one print acceptance evidence item is required." });
        }

        var requiredKeys = RequiredEvidenceKeys();
        var passedKeys = items
            .Where(item => item.Checked && string.Equals(item.Result, "Pass", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var warnings = new List<string>();
        foreach (var requiredKey in requiredKeys)
        {
            if (!passedKeys.Contains(requiredKey))
            {
                warnings.Add($"Required check '{requiredKey}' is not marked Pass.");
            }
        }

        foreach (var item in items.Where(item => item.Checked && string.Equals(item.Result, "Fail", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add($"{item.Label} is marked Fail: {item.Remarks}".Trim());
        }

        var passedCount = requiredKeys.Count(requiredKey => passedKeys.Contains(requiredKey));
        var status = warnings.Count == 0 ? "Accepted" : "Needs Review";
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var id = Guid.NewGuid();
        var reference = $"PRINT-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()}";
        var operatorName = FirstNonBlank(request.OperatorName, context.User.Identity?.Name, context.User.FindFirst(ClaimTypes.Name)?.Value, "Operator");
        var payload = new PrintAcceptanceEvidencePayload(
            operatorName,
            request.LiveBaseUrl?.Trim() ?? string.Empty,
            request.BrowserName?.Trim() ?? string.Empty,
            request.PrinterName?.Trim() ?? string.Empty,
            request.Note,
            status,
            passedCount,
            requiredKeys.Count,
            warnings,
            items);

        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = id,
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Action = status == "Accepted" ? "Accepted" : "EvidenceSaved",
            Module = EvidenceModule,
            EntityName = EvidenceEntity,
            EntityDisplayName = "Final PDF Print Acceptance Evidence",
            EntityId = id,
            Reference = reference,
            UserId = ReadGuidClaim(context, ClaimTypes.NameIdentifier),
            UserName = operatorName,
            Source = "PrintFinalAcceptance",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path.Value,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            TraceIdentifier = context.TraceIdentifier,
            Reason = $"{status}: {passedCount}/{requiredKeys.Count} required print checks passed. {request.Note}".Trim(),
            BeforeJson = null,
            AfterJson = JsonSerializer.Serialize(payload),
            ChangesJson = JsonSerializer.Serialize(items.Select(item => new
            {
                item.Key,
                item.Label,
                item.PaperSize,
                item.Result,
                item.Checked,
                item.Remarks
            })),
            ChangedFieldCount = items.Count
        });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new PrintAcceptanceEvidenceResultDto(
            id,
            reference,
            new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc)),
            status,
            passedCount,
            requiredKeys.Count,
            warnings));
    }

    private static IReadOnlyList<PrintAcceptanceScenarioDto> BuildScenarios(IReadOnlyList<PrintAcceptanceDocumentDto> docs)
    {
        var saleEndpoint = docs.FirstOrDefault(item => item.Key == "salesInvoice")?.Endpoint;
        var purchaseEndpoint = docs.FirstOrDefault(item => item.Key == "purchaseInward")?.Endpoint;
        return new[]
        {
            new PrintAcceptanceScenarioDto("saleA4", "Sale invoice A4", "Sales", "A4", true, "Browser PDF opens, header/footer/signature visible, amount total matches screen.", AppendQuery(saleEndpoint, "format=a4&copy=customer&signatures=true")),
            new PrintAcceptanceScenarioDto("saleA5", "Sale invoice A5", "Sales", "A5", true, "Compact A5 layout opens without clipping header, GST/amount box, footer, and signature.", AppendQuery(saleEndpoint, "format=a5&copy=customer&signatures=true")),
            new PrintAcceptanceScenarioDto("purchaseA4", "Purchase inward A4", "Purchase", "A4", true, "Purchase A4 PDF opens with vendor, GST, totals, footer, and signature blocks.", AppendQuery(purchaseEndpoint, "format=a4&copy=office&signatures=true")),
            new PrintAcceptanceScenarioDto("purchaseA5", "Purchase inward A5", "Purchase", "A5", true, "Purchase A5 compact PDF opens without clipping vendor/tax/total/signature blocks.", AppendQuery(purchaseEndpoint, "format=a5&copy=office&signatures=true")),
            new PrintAcceptanceScenarioDto("largeInvoicePagination", "Large invoice pagination", "Sales/Purchase", "A4/A5", true, "Large invoice or inward creates extra pages and no item rows are truncated.", AppendQuery(saleEndpoint, "format=a4&copy=customer&signatures=true")),
            new PrintAcceptanceScenarioDto("amountBox", "Amount box and totals", "Sales/Purchase", "A4/A5", true, "MRP, discount, taxable amount, GST, round-off, paid, balance, and final total match source screen values.", AppendQuery(saleEndpoint, "format=a4&copy=customer&signatures=true")),
            new PrintAcceptanceScenarioDto("footer", "Footer and branding", "Sales/Purchase", "A4/A5", true, "Logo/company/store address/GSTIN/contact/footer text are aligned and visible.", AppendQuery(saleEndpoint, "format=a4&copy=customer&signatures=true")),
            new PrintAcceptanceScenarioDto("signature", "Signature blocks", "Sales/Purchase", "A4/A5", true, "Customer/receiver/prepared-by/authorized-signatory blocks are visible where required.", AppendQuery(saleEndpoint, "format=a4&copy=customer&signatures=true")),
            new PrintAcceptanceScenarioDto("pageSummary", "Page summary and final total", "Sales/Purchase", "A4/A5", true, "Each page has page-level summary where applicable, and only the final page has grand total/payment summary.", AppendQuery(saleEndpoint, "format=a4&copy=customer&signatures=true"))
        };
    }

    private static IReadOnlyList<string> RequiredEvidenceKeys()
        => new[]
        {
            "saleA4",
            "saleA5",
            "purchaseA4",
            "purchaseA5",
            "largeInvoicePagination",
            "amountBox",
            "footer",
            "signature",
            "pageSummary"
        };

    private static IReadOnlyList<string> CoreSampleKeys()
        => new[]
        {
            "salesInvoice",
            "purchaseInward"
        };

    private static PrintAcceptanceClosureDto BuildClosure(
        IReadOnlyList<PrintAcceptanceDocumentDto> docs,
        IReadOnlyList<PrintAcceptanceEvidenceSummaryDto> recentEvidence)
    {
        var coreKeys = CoreSampleKeys();
        var requiredKeys = RequiredEvidenceKeys();
        var coreDocs = docs.Where(item => coreKeys.Contains(item.Key, StringComparer.OrdinalIgnoreCase)).ToList();
        var coreReady = coreDocs.Count(item => string.Equals(item.Status, "Ready", StringComparison.OrdinalIgnoreCase));
        var latestEvidence = recentEvidence.FirstOrDefault();
        var acceptedEvidence = recentEvidence.FirstOrDefault(item =>
            string.Equals(item.Status, "Accepted", StringComparison.OrdinalIgnoreCase) &&
            item.PassedCount >= item.RequiredCount &&
            item.RequiredCount >= requiredKeys.Count);

        var blocking = new List<string>();
        foreach (var doc in coreDocs.Where(item => !string.Equals(item.Status, "Ready", StringComparison.OrdinalIgnoreCase)))
        {
            blocking.Add($"Required sample missing: {doc.Label}.");
        }

        if (acceptedEvidence is null)
        {
            blocking.Add("No Accepted print evidence record is saved for all required A4/A5 checks.");
        }
        else if (latestEvidence is not null && !string.Equals(latestEvidence.Reference, acceptedEvidence.Reference, StringComparison.OrdinalIgnoreCase) && !string.Equals(latestEvidence.Status, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            blocking.Add($"Latest evidence {latestEvidence.Reference} is {latestEvidence.Status}; review it before final handover.");
        }

        var status = blocking.Count == 0 ? "Complete" : "Not Complete";
        return new PrintAcceptanceClosureDto(
            DateTimeOffset.UtcNow,
            status,
            blocking.Count == 0,
            coreReady,
            coreDocs.Count,
            coreDocs.Count - coreReady,
            latestEvidence?.PassedCount ?? 0,
            requiredKeys.Count,
            acceptedEvidence?.Reference,
            acceptedEvidence?.OccurredAt,
            blocking,
            new[]
            {
                "Open Sale invoice A4 from live URL and verify barcode, item rows, GST, footer, signature and amount box.",
                "Open Sale invoice A5 from live URL and verify no clipping in print preview.",
                "Open Purchase inward A4 and A5 from live URL and verify vendor, supplier invoice, tax, total and signature sections.",
                "Use at least one large invoice or large inward to verify pagination and final-page grand total.",
                "Save final evidence only after physical print or browser Save as PDF verification.",
                "Export CSV evidence and attach it to production handover/backup records."
            },
            new[]
            {
                "The system verifies sample availability and saved operator evidence; it cannot automatically confirm physical printer alignment.",
                "A large-invoice pagination pass depends on the operator opening a real large sample.",
                "Printer margin, scale and paper tray settings remain device-side settings and must be checked on the live printer.",
                "Optional documents like vouchers, payslips, GST export and tailoring print are listed as samples but are not blocking the core sale/purchase print closure."
            },
            new[]
            {
                "Do not mark Pass from development URL if production handover is for live hosted URL.",
                "Do not mark Pass when browser preview is scaled strangely, footer is clipped, barcode is missing, or totals differ from the screen.",
                "Keep evidence remarks specific: paper size, browser, printer/PDF target, and any margin/scale adjustment used.",
                "When a print template is changed later, re-run and save a new evidence record."
            },
            new[]
            {
                "Sale/Billing QA with real invoice replacement and mixed payment cases.",
                "Purchase Inward/Vendor Payment QA with live supplier invoices.",
                "Backup/Maintenance final restore drill and production handover."
            });
    }

    private static async Task<IReadOnlyList<PrintAcceptanceEvidenceSummaryDto>> LoadEvidenceSummariesAsync(
        GarmetixDbContext db,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await db.AuditLogEntries.AsNoTracking()
            .Where(item => !item.Deleted && item.Module == EvidenceModule && item.EntityName == EvidenceEntity)
            .OrderByDescending(item => item.OccurredAt)
            .Take(take)
            .Select(item => new
            {
                item.Id,
                item.Reference,
                item.OccurredAt,
                item.Action,
                item.UserName,
                item.Reason,
                item.AfterJson
            })
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            PrintAcceptanceEvidencePayload? payload = null;
            if (!string.IsNullOrWhiteSpace(row.AfterJson))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<PrintAcceptanceEvidencePayload>(row.AfterJson);
                }
                catch
                {
                    payload = null;
                }
            }

            return new PrintAcceptanceEvidenceSummaryDto(
                row.Id,
                row.Reference,
                row.OccurredAt,
                payload?.Status ?? row.Action,
                payload?.OperatorName ?? row.UserName,
                payload?.LiveBaseUrl,
                payload?.BrowserName,
                payload?.PrinterName,
                payload?.PassedCount ?? 0,
                payload?.RequiredCount ?? RequiredEvidenceKeys().Count,
                payload?.Note ?? row.Reason);
        }).ToList();
    }

    private static void AddDocument(
        List<PrintAcceptanceDocumentDto> docs,
        string key,
        string label,
        string area,
        int count,
        string? latestNumber,
        Guid? latestId,
        DateTime? latestDate,
        string? endpoint)
    {
        var ready = latestId.HasValue && count > 0;
        docs.Add(new PrintAcceptanceDocumentDto(
            key,
            label,
            area,
            count,
            latestNumber,
            latestId,
            latestDate,
            ready ? "Ready" : "Missing sample",
            endpoint,
            ready
                ? $"Latest sample {latestNumber} is available for print verification."
                : $"No sample {label} record is available yet."));
    }

    private static string? AppendQuery(string? endpoint, string query)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        return endpoint.Contains('?') ? $"{endpoint}&{query}" : $"{endpoint}?{query}";
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
        return escaped.Contains(',') || escaped.Contains('\"') || escaped.Contains('\n') || escaped.Contains('\r')
            ? $"\"{escaped}\""
            : escaped;
    }

    private static Guid? ReadGuidClaim(HttpContext context, string claimType)
        => Guid.TryParse(context.User.FindFirst(claimType)?.Value, out var value) ? value : null;

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "Operator";
}

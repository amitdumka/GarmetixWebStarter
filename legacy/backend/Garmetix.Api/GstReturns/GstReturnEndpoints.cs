using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Garmetix.Api.Accounting;
using Garmetix.Api.Database;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.GstReturns;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstReturns;

public static class GstReturnEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };


    private static async Task EnsureGstDraftStorageAsync(
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairGstReturnStorageAsync(
            db,
            loggerFactory.CreateLogger("GstDraftStorageRepair"),
            cancellationToken);
    }

    private static bool IsMissingRelation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("42P01", StringComparison.OrdinalIgnoreCase)
                || message.Contains("relation \"GstReturnDrafts\" does not exist", StringComparison.OrdinalIgnoreCase)
                || message.Contains("relation \"GstReturnAuditEntries\" does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static RouteGroupBuilder MapGstReturnEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst-returns")
            .WithTags("GST Returns")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/schema-review", () =>
            Results.Ok(GstReturnSchemaReviewService.Build()));

        group.MapGet("/schema-review/excel", () =>
        {
            var bytes = GstReturnSchemaReviewService.BuildExcel();
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Garmetix-GST-Schema-Review.xlsx");
        });

        group.MapGet("/from-books/gstr1", BuildGstr1FromBooksAsync);
        group.MapGet("/from-books/gstr3b", BuildGstr3BFromBooksAsync);
        group.MapGet("/reports/hsn-summary", GetHsnSummaryReportAsync);
        group.MapGet("/reports/hsn-summary/csv", DownloadHsnSummaryCsvAsync);
        group.MapGet("/reports/tax-summary", GetTaxRateSummaryReportAsync);
        group.MapGet("/reports/tax-summary/csv", DownloadTaxRateSummaryCsvAsync);
        group.MapGet("/reports/invoice-register", GetInvoiceRegisterReportAsync);
        group.MapGet("/reports/invoice-register/csv", DownloadInvoiceRegisterCsvAsync);
        group.MapPost("/reports/send-review", SendGstReportsReviewAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/accounting-summary", GetAccountingSummaryAsync);
        group.MapPost("/accounting-posting", PostAccountingAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/drafts/{id:guid}/accounting-posting", PostDraftAccountingAsync).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/drafts", ListDraftsAsync);
        group.MapGet("/drafts/{id:guid}", GetDraftAsync);
        group.MapPost("/drafts", SaveDraftAsync);
        group.MapPut("/drafts/{id:guid}", UpdateDraftAsync);
        group.MapDelete("/drafts/{id:guid}", DeleteDraftAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapPost("/drafts/{id:guid}/filed", MarkFiledAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/drafts/{id:guid}/audit", GetDraftAuditAsync);
        group.MapGet("/drafts/{id:guid}/json", ExportDraftJsonAsync);
        group.MapGet("/drafts/{id:guid}/excel", ExportDraftExcelAsync);
        group.MapPost("/drafts/{id:guid}/send-review", SendDraftReviewAsync).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapPost("/gstr1/preview", ([FromBody] Gstr1ExportRequest request) =>
            Results.Ok(GstReturnExportService.PreviewGstr1(Normalize(request))));

        group.MapPost("/gstr1/json", ([FromBody] Gstr1ExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr1(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr1Json(normalized);
            return Results.File(bytes, "application/json", FileName("GSTR1", normalized.Header, "json"));
        });

        group.MapPost("/gstr1/excel", ([FromBody] Gstr1ExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr1(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr1Excel(normalized);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName("GSTR1", normalized.Header, "xlsx"));
        });

        group.MapPost("/gstr3b/preview", ([FromBody] Gstr3BExportRequest request) =>
            Results.Ok(GstReturnExportService.PreviewGstr3B(Normalize(request))));

        group.MapPost("/gstr3b/json", ([FromBody] Gstr3BExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr3B(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr3BJson(normalized);
            return Results.File(bytes, "application/json", FileName("GSTR3B", normalized.Header, "json"));
        });

        group.MapPost("/gstr3b/excel", ([FromBody] Gstr3BExportRequest request) =>
        {
            var normalized = Normalize(request);
            var issues = GstReturnExportService.PreviewGstr3B(normalized).Issues;
            if (issues.Count > 0)
            {
                return Results.BadRequest(new { message = "Fix GST validation issues before export.", issues });
            }

            var bytes = GstReturnExportService.BuildGstr3BExcel(normalized);
            return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName("GSTR3B", normalized.Header, "xlsx"));
        });

        return group;
    }


    private static async Task<IResult> GetAccountingSummaryAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        AccountingPostingService accounting,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, loggerFactory.CreateLogger("DatabaseSchemaRepair"), cancellationToken);
        if (!TryPeriodRange(returnPeriod, out var start, out var end))
        {
            return Results.BadRequest(new { message = "Return period must be MMYYYY, for example 042026." });
        }

        var selectedCompanyId = companyId ?? WorkspaceScope.ClaimGuid(context, "companyId");
        if (!selectedCompanyId.HasValue)
        {
            return Results.BadRequest(new { message = "Select a company before GST accounting reconciliation." });
        }

        var companyExists = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .AnyAsync(item => item.Id == selectedCompanyId.Value, cancellationToken);
        if (!companyExists)
        {
            return Results.BadRequest(new { message = "Selected company is outside your access scope." });
        }

        return Results.Ok(await accounting.GetGstAccountingBridgeSummaryAsync(selectedCompanyId.Value, returnPeriod.Trim(), start, end, cancellationToken));
    }

    private static async Task<IResult> PostAccountingAsync(
        [FromBody] GstAccountingPostRequest request,
        GarmetixDbContext db,
        HttpContext context,
        AccountingPostingService accounting,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, loggerFactory.CreateLogger("DatabaseSchemaRepair"), cancellationToken);
        if (!TryPeriodRange(request.ReturnPeriod, out _, out _))
        {
            return Results.BadRequest(new { message = "Return period must be MMYYYY, for example 042026." });
        }

        var companyExists = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .AnyAsync(item => item.Id == request.CompanyId, cancellationToken);
        if (!companyExists)
        {
            return Results.BadRequest(new { message = "Selected company is outside your access scope." });
        }

        var scopeProbe = new Garmetix.Core.Models.Accounting.JournalEntry
        {
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId ?? Guid.Empty,
            StoreId = request.StoreId ?? Guid.Empty
        };
        if (!WorkspaceScope.CanWrite(scopeProbe, context, out var scopeMessage))
        {
            return Results.BadRequest(new { message = scopeMessage });
        }

        try
        {
            return Results.Ok(await accounting.PostGstAccountingSettlementAsync(request, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> PostDraftAccountingAsync(
        Guid id,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? onDate,
        GarmetixDbContext db,
        HttpContext context,
        AccountingPostingService accounting,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairKnownSchemaDriftAsync(db, loggerFactory.CreateLogger("DatabaseSchemaRepair"), cancellationToken);
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (draft.Form != "gstr3b")
        {
            return Results.BadRequest(new { message = "Accounting settlement posting is supported for GSTR-3B drafts only." });
        }

        Gstr3BExportRequest? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Gstr3BExportRequest>(draft.PayloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { message = $"Saved draft payload is invalid JSON: {ex.Message}" });
        }

        if (parsed is null)
        {
            return Results.BadRequest(new { message = "Saved GSTR-3B draft payload is empty." });
        }

        var normalized = Normalize(parsed);
        var request = new GstAccountingPostRequest(
            draft.CompanyId,
            storeGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            storeId ?? WorkspaceScope.ClaimGuid(context, "storeId"),
            draft.ReturnPeriod,
            onDate,
            TotalOutputTax(normalized),
            TotalInputTax(normalized),
            TotalInterestLateFee(normalized),
            $"GST accounting settlement from {DisplayForm(draft.Form)} draft {draft.Title}",
            draft.Id);

        var scopeProbe = new Garmetix.Core.Models.Accounting.JournalEntry
        {
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId ?? Guid.Empty,
            StoreId = request.StoreId ?? Guid.Empty
        };
        if (!WorkspaceScope.CanWrite(scopeProbe, context, out var scopeMessage))
        {
            return Results.BadRequest(new { message = scopeMessage });
        }

        try
        {
            var result = await accounting.PostGstAccountingSettlementAsync(request, cancellationToken);
            draft.Status = draft.LockedAt.HasValue ? draft.Status : "Accounting Posted";
            var actor = Actor(context);
            draft.UpdatedByUserId = actor.Id;
            draft.UpdatedByUserName = actor.Name;
            AddAudit(db, draft, "Posted Accounting", $"Posted GST accounting settlement journal {result.EntryNumber} for {draft.ReturnPeriod}.", context, result);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }


    private static async Task<IResult> GetHsnSummaryReportAsync(
        Guid? companyId,
        string returnPeriod,
        string? direction,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await BuildHsnSummaryReportAsync(companyId, returnPeriod, direction, db, context, cancellationToken);
        return report.Match;
    }

    private static async Task<IResult> DownloadHsnSummaryCsvAsync(
        Guid? companyId,
        string returnPeriod,
        string? direction,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var reportResult = await BuildHsnSummaryReportAsync(companyId, returnPeriod, direction, db, context, cancellationToken);
        if (reportResult.Error is not null)
        {
            return reportResult.Error;
        }

        var report = reportResult.Report!;
        var rows = new List<string[]>
        {
            new[] { "Serial", "Direction", "HSN", "Description", "UQC", "Rate", "Quantity", "Taxable", "CGST", "SGST", "IGST", "Tax", "Total" }
        };
        rows.AddRange(report.Rows.Select(row => new[]
        {
            row.SerialNumber.ToString(CultureInfo.InvariantCulture), row.Direction, row.HsnCode, row.Description, row.Uqc,
            row.Rate.ToString("0.##", CultureInfo.InvariantCulture), row.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
            row.TaxableValue.ToString("0.00", CultureInfo.InvariantCulture), row.CgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.SgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture), row.TotalValue.ToString("0.00", CultureInfo.InvariantCulture)
        }));
        return CsvFile(rows, $"Garmetix-HSN-Summary-{report.Direction}-{report.ReturnPeriod}.csv");
    }

    private static async Task<IResult> GetTaxRateSummaryReportAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await BuildTaxRateSummaryReportAsync(companyId, returnPeriod, db, context, cancellationToken);
        return report.Match;
    }

    private static async Task<IResult> DownloadTaxRateSummaryCsvAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var reportResult = await BuildTaxRateSummaryReportAsync(companyId, returnPeriod, db, context, cancellationToken);
        if (reportResult.Error is not null)
        {
            return reportResult.Error;
        }

        var report = reportResult.Report!;
        var rows = new List<string[]>
        {
            new[] { "Rate", "Sales Taxable", "Sales CGST", "Sales SGST", "Sales IGST", "Purchase Taxable", "Purchase CGST", "Purchase SGST", "Purchase IGST", "Net Tax Payable" }
        };
        rows.AddRange(report.Rows.Select(row => new[]
        {
            row.Rate.ToString("0.##", CultureInfo.InvariantCulture), row.SalesTaxableValue.ToString("0.00", CultureInfo.InvariantCulture),
            row.SalesCgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.SalesSgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.SalesIgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.PurchaseTaxableValue.ToString("0.00", CultureInfo.InvariantCulture),
            row.PurchaseCgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.PurchaseSgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.PurchaseIgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.NetTaxPayable.ToString("0.00", CultureInfo.InvariantCulture)
        }));
        return CsvFile(rows, $"Garmetix-GST-Tax-Summary-{report.ReturnPeriod}.csv");
    }

    private static async Task<IResult> GetInvoiceRegisterReportAsync(
        Guid? companyId,
        string returnPeriod,
        string? direction,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var report = await BuildInvoiceRegisterReportAsync(companyId, returnPeriod, direction, db, context, cancellationToken);
        return report.Match;
    }

    private static async Task<IResult> DownloadInvoiceRegisterCsvAsync(
        Guid? companyId,
        string returnPeriod,
        string? direction,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var reportResult = await BuildInvoiceRegisterReportAsync(companyId, returnPeriod, direction, db, context, cancellationToken);
        if (reportResult.Error is not null)
        {
            return reportResult.Error;
        }

        var report = reportResult.Report!;
        var rows = new List<string[]>
        {
            new[] { "Direction", "Invoice", "Reference", "Date", "Party", "GSTIN", "Status", "Return", "Taxable", "CGST", "SGST", "IGST", "Tax", "Bill" }
        };
        rows.AddRange(report.Rows.Select(row => new[]
        {
            row.Direction, row.InvoiceNumber, row.ReferenceNumber ?? string.Empty, row.OnDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            row.PartyName, row.PartyGstin ?? string.Empty, row.InvoiceStatus, row.IsReturn ? "Yes" : "No",
            row.TaxableValue.ToString("0.00", CultureInfo.InvariantCulture), row.CgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.SgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture), row.BillAmount.ToString("0.00", CultureInfo.InvariantCulture)
        }));
        return CsvFile(rows, $"Garmetix-GST-Invoice-Register-{report.Direction}-{report.ReturnPeriod}.csv");
    }

    private static async Task<ReportResult<GstHsnSummaryReport>> BuildHsnSummaryReportAsync(
        Guid? companyId,
        string returnPeriod,
        string? direction,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveGstReportScopeAsync(companyId, returnPeriod, db, context, cancellationToken);
        if (scope.Error is not null)
        {
            return ReportResult<GstHsnSummaryReport>.Fail(scope.Error);
        }

        var normalizedDirection = NormalizeReportDirection(direction);
        var rows = new List<GstHsnSummaryReportRow>();
        if (normalizedDirection is "sales" or "both")
        {
            rows.AddRange(await BuildSalesHsnRowsAsync(scope.CompanyId, scope.Start, scope.End, db, context, cancellationToken));
        }
        if (normalizedDirection is "purchase" or "both")
        {
            rows.AddRange(await BuildPurchaseHsnRowsAsync(scope.CompanyId, scope.Start, scope.End, db, context, cancellationToken));
        }

        var ordered = rows
            .GroupBy(row => new { row.Direction, row.HsnCode, row.Description, row.Uqc, row.Rate })
            .OrderBy(group => group.Key.Direction)
            .ThenBy(group => group.Key.HsnCode)
            .ThenBy(group => group.Key.Rate)
            .Select((group, index) => new GstHsnSummaryReportRow(
                index + 1,
                group.Key.Direction,
                group.Key.HsnCode,
                group.Key.Description,
                group.Key.Uqc,
                group.Key.Rate,
                Round(group.Sum(row => row.Quantity)),
                Round(group.Sum(row => row.TaxableValue)),
                Round(group.Sum(row => row.CgstAmount)),
                Round(group.Sum(row => row.SgstAmount)),
                Round(group.Sum(row => row.IgstAmount)),
                Round(group.Sum(row => row.TaxAmount)),
                Round(group.Sum(row => row.TotalValue))))
            .ToList();

        var report = new GstHsnSummaryReport(
            returnPeriod.Trim(), normalizedDirection, scope.Start, scope.End.AddDays(-1), ordered.Count,
            Round(ordered.Sum(row => row.Quantity)), Round(ordered.Sum(row => row.TaxableValue)),
            Round(ordered.Sum(row => row.CgstAmount)), Round(ordered.Sum(row => row.SgstAmount)), Round(ordered.Sum(row => row.IgstAmount)),
            Round(ordered.Sum(row => row.TaxAmount)), Round(ordered.Sum(row => row.TotalValue)), ordered);
        return ReportResult<GstHsnSummaryReport>.Ok(report);
    }

    private static async Task<ReportResult<GstTaxRateSummaryReport>> BuildTaxRateSummaryReportAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveGstReportScopeAsync(companyId, returnPeriod, db, context, cancellationToken);
        if (scope.Error is not null)
        {
            return ReportResult<GstTaxRateSummaryReport>.Fail(scope.Error);
        }

        var salesRows = await BuildSalesHsnRowsAsync(scope.CompanyId, scope.Start, scope.End, db, context, cancellationToken);
        var purchaseRows = await BuildPurchaseHsnRowsAsync(scope.CompanyId, scope.Start, scope.End, db, context, cancellationToken);
        var rates = salesRows.Select(row => row.Rate).Concat(purchaseRows.Select(row => row.Rate)).Distinct().OrderBy(rate => rate);
        var rows = rates.Select(rate =>
        {
            var sales = salesRows.Where(row => row.Rate == rate).ToList();
            var purchases = purchaseRows.Where(row => row.Rate == rate).ToList();
            var salesTax = sales.Sum(row => row.CgstAmount + row.SgstAmount + row.IgstAmount);
            var purchaseTax = purchases.Sum(row => row.CgstAmount + row.SgstAmount + row.IgstAmount);
            return new GstTaxRateSummaryRow(
                rate,
                Round(sales.Sum(row => row.TaxableValue)), Round(sales.Sum(row => row.CgstAmount)), Round(sales.Sum(row => row.SgstAmount)), Round(sales.Sum(row => row.IgstAmount)),
                Round(purchases.Sum(row => row.TaxableValue)), Round(purchases.Sum(row => row.CgstAmount)), Round(purchases.Sum(row => row.SgstAmount)), Round(purchases.Sum(row => row.IgstAmount)),
                Round(salesTax - purchaseTax));
        }).ToList();

        var report = new GstTaxRateSummaryReport(
            returnPeriod.Trim(), scope.Start, scope.End.AddDays(-1), rows.Count,
            Round(rows.Sum(row => row.SalesTaxableValue)), Round(rows.Sum(row => row.SalesCgstAmount)), Round(rows.Sum(row => row.SalesSgstAmount)), Round(rows.Sum(row => row.SalesIgstAmount)),
            Round(rows.Sum(row => row.PurchaseTaxableValue)), Round(rows.Sum(row => row.PurchaseCgstAmount)), Round(rows.Sum(row => row.PurchaseSgstAmount)), Round(rows.Sum(row => row.PurchaseIgstAmount)),
            Round(rows.Sum(row => row.NetTaxPayable)), rows);
        return ReportResult<GstTaxRateSummaryReport>.Ok(report);
    }

    private static async Task<ReportResult<GstInvoiceRegisterReport>> BuildInvoiceRegisterReportAsync(
        Guid? companyId,
        string returnPeriod,
        string? direction,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveGstReportScopeAsync(companyId, returnPeriod, db, context, cancellationToken);
        if (scope.Error is not null)
        {
            return ReportResult<GstInvoiceRegisterReport>.Fail(scope.Error);
        }

        var normalizedDirection = NormalizeReportDirection(direction);
        var rows = new List<GstInvoiceRegisterRow>();
        if (normalizedDirection is "sales" or "both")
        {
            var sales = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => item.CompanyId == scope.CompanyId && item.OnDate >= scope.Start && item.OnDate < scope.End && item.InvoiceStatus != InvoiceStatus.Cancelled)
                .OrderBy(item => item.OnDate)
                .ThenBy(item => item.InvoiceNumber)
                .ToListAsync(cancellationToken);
            rows.AddRange(sales.Select(invoice =>
            {
                var sign = invoice.ReturnInvoice ? -1m : 1m;
                return new GstInvoiceRegisterRow("Sales", invoice.InvoiceNumber, null, invoice.OnDate, invoice.CustomerName ?? "Walk-in Customer", invoice.CustomerGSTIN,
                    invoice.InvoiceStatus.ToString(), invoice.ReturnInvoice, Round(sign * invoice.NetAmount), Round(sign * (invoice.CGSTAmount ?? invoice.TaxAmount / 2)),
                    Round(sign * (invoice.SGSTAmount ?? invoice.TaxAmount / 2)), Round(sign * (invoice.IGSTAmount ?? 0)), Round(sign * invoice.TaxAmount), Round(sign * invoice.BillAmount));
            }));
        }
        if (normalizedDirection is "purchase" or "both")
        {
            var purchases = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .Where(item => item.CompanyId == scope.CompanyId && item.OnDate >= scope.Start && item.OnDate < scope.End && item.InvoiceStatus != InvoiceStatus.Cancelled)
                .OrderBy(item => item.OnDate)
                .ThenBy(item => item.InvoiceNumber)
                .ToListAsync(cancellationToken);
            rows.AddRange(purchases.Select(invoice => new GstInvoiceRegisterRow("Purchase", invoice.InvoiceNumber, invoice.InwardNumber, invoice.OnDate, invoice.VendorName ?? "Vendor", invoice.VendorGSTIN,
                invoice.InvoiceStatus.ToString(), invoice.ReturnInvoice, Round(invoice.NetAmount), Round(invoice.CGSTAmount ?? invoice.TaxAmount / 2),
                Round(invoice.SGSTAmount ?? invoice.TaxAmount / 2), Round(invoice.IGSTAmount ?? 0), Round(invoice.TaxAmount), Round(invoice.BillAmount))));
        }

        var orderedRows = rows.OrderBy(row => row.OnDate).ThenBy(row => row.Direction).ThenBy(row => row.InvoiceNumber).ToList();
        var report = new GstInvoiceRegisterReport(
            returnPeriod.Trim(), normalizedDirection, scope.Start, scope.End.AddDays(-1), orderedRows.Count,
            Round(orderedRows.Sum(row => row.TaxableValue)), Round(orderedRows.Sum(row => row.CgstAmount)), Round(orderedRows.Sum(row => row.SgstAmount)),
            Round(orderedRows.Sum(row => row.IgstAmount)), Round(orderedRows.Sum(row => row.TaxAmount)), Round(orderedRows.Sum(row => row.BillAmount)), orderedRows);
        return ReportResult<GstInvoiceRegisterReport>.Ok(report);
    }

    private static async Task<List<GstHsnSummaryReportRow>> BuildSalesHsnRowsAsync(
        Guid companyId,
        DateTime start,
        DateTime end,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == companyId && item.OnDate >= start && item.OnDate < end && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(item => item.Id).ToHashSet();
        var invoiceLookup = invoices.ToDictionary(item => item.Id);
        var items = await db.InvoiceItems.AsNoTracking()
            .Include(item => item.Product)
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .ToListAsync(cancellationToken);
        return items.Select(item =>
        {
            var invoice = invoiceLookup[item.InvoiceId];
            var sign = invoice.ReturnInvoice ? -1m : 1m;
            var igst = item.IGSTAmount ?? 0;
            var cgst = item.CGSTAmount ?? 0;
            var sgst = item.SGSTAmount ?? 0;
            if (igst == 0 && cgst == 0 && sgst == 0)
            {
                SplitTax(item.TaxAmount, invoice.InterState, null, null, null, out igst, out cgst, out sgst);
            }
            return new GstHsnSummaryReportRow(0, "Sales", NormalizeHsn(item.HSNCode ?? item.Product?.HSNCode), item.ProductName ?? item.Product?.Name ?? item.Barcode,
                UnitToUqc(item.Unit?.ToString()), item.TaxPercentage, Round(sign * item.BilledQuantity), Round(sign * item.BasePrice),
                Round(sign * cgst), Round(sign * sgst), Round(sign * igst), Round(sign * item.TaxAmount), Round(sign * item.Amount));
        }).ToList();
    }

    private static async Task<List<GstHsnSummaryReportRow>> BuildPurchaseHsnRowsAsync(
        Guid companyId,
        DateTime start,
        DateTime end,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == companyId && item.OnDate >= start && item.OnDate < end && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(item => item.Id).ToHashSet();
        var invoiceLookup = invoices.ToDictionary(item => item.Id);
        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Include(item => item.Product)
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .ToListAsync(cancellationToken);
        return items.Select(item =>
        {
            var invoice = invoiceLookup[item.InvoiceId];
            var igst = item.IGSTAmount ?? 0;
            var cgst = item.CGSTAmount ?? 0;
            var sgst = item.SGSTAmount ?? 0;
            if (igst == 0 && cgst == 0 && sgst == 0)
            {
                SplitTax(item.TaxAmount, invoice.InterState, null, null, null, out igst, out cgst, out sgst);
            }
            return new GstHsnSummaryReportRow(0, "Purchase", NormalizeHsn(item.HSNCode ?? item.Product?.HSNCode), item.ProductName ?? item.Product?.Name ?? item.Barcode,
                UnitToUqc(item.Unit?.ToString()), item.TaxPercentage, Round(item.BilledQuantity), Round(item.BasePrice),
                Round(cgst), Round(sgst), Round(igst), Round(item.TaxAmount), Round(item.Amount));
        }).ToList();
    }

    private static async Task<GstReportScope> ResolveGstReportScopeAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryPeriodRange(returnPeriod, out var start, out var end))
        {
            return GstReportScope.Fail(Results.BadRequest(new { message = "Return period must be MMYYYY, for example 042026." }));
        }

        var selectedCompanyId = companyId ?? WorkspaceScope.ClaimGuid(context, "companyId");
        if (!selectedCompanyId.HasValue)
        {
            return GstReportScope.Fail(Results.BadRequest(new { message = "Select a company before generating GST reports." }));
        }

        var companyExists = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .AnyAsync(item => item.Id == selectedCompanyId.Value, cancellationToken);
        if (!companyExists)
        {
            return GstReportScope.Fail(Results.BadRequest(new { message = "Selected company is outside your access scope." }));
        }

        return GstReportScope.Ok(selectedCompanyId.Value, start, end);
    }

    private static string NormalizeReportDirection(string? value)
    {
        var normalized = (value ?? "both").Trim().ToLowerInvariant();
        return normalized is "sale" or "sales" or "outward" ? "sales"
            : normalized is "purchase" or "purchases" or "inward" ? "purchase"
            : "both";
    }

    private static string NormalizeHsn(string? value)
    {
        var cleaned = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "0000" : cleaned;
    }

    private static string UnitToUqc(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "meter" or "meters" or "metre" or "metres" or "mtr" => "MTR",
            "kg" or "kilogram" or "kilograms" => "KGS",
            "piece" or "pieces" or "pcs" or "pc" => "PCS",
            "pair" or "pairs" => "PRS",
            "set" or "sets" => "SET",
            "nos" or "number" or "numbers" => "NOS",
            _ => "NOS"
        };
    }

    private static IResult CsvFile(IEnumerable<string[]> rows, string fileName) =>
        Results.File(CsvBytes(rows), "text/csv", fileName);

    private static byte[] CsvBytes(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(CsvCell)));
        }
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string CsvCell(string? value)
    {
        value ??= string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r') ? $"\"{escaped}\"" : escaped;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record GstReportScope(Guid CompanyId, DateTime Start, DateTime End, IResult? Error)
    {
        public static GstReportScope Ok(Guid companyId, DateTime start, DateTime end) => new(companyId, start, end, null);
        public static GstReportScope Fail(IResult error) => new(Guid.Empty, default, default, error);
    }

    private sealed record ReportResult<T>(T? Report, IResult? Error)
    {
        public IResult Match => Error ?? Results.Ok(Report);
        public static ReportResult<T> Ok(T report) => new(report, null);
        public static ReportResult<T> Fail(IResult error) => new(default, error);
    }

    private static async Task<IResult> BuildGstr1FromBooksAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryPeriodRange(returnPeriod, out var start, out var end))
        {
            return Results.BadRequest(new { message = "Return period must be MMYYYY, for example 042026." });
        }

        var selectedCompanyId = companyId ?? WorkspaceScope.ClaimGuid(context, "companyId");
        if (!selectedCompanyId.HasValue)
        {
            return Results.BadRequest(new { message = "Select a company before generating GST from books." });
        }

        var company = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == selectedCompanyId.Value, cancellationToken);
        if (company is null)
        {
            return Results.BadRequest(new { message = "Selected company is outside your access scope." });
        }

        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == company.Id && item.OnDate >= start && item.OnDate < end && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(item => item.Id).ToList();
        var items = await db.InvoiceItems.AsNoTracking()
            .Include(item => item.Product)
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .ToListAsync(cancellationToken);
        var itemLookup = items.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var companyStateCode = StateCode(company.GSTIN);

        var b2b = new List<Gstr1B2BInvoiceRow>();
        var b2cAccumulator = new Dictionary<string, Gstr1B2CSummaryRow>();
        var hsnAccumulator = new Dictionary<string, Gstr1HsnSummaryRow>();

        foreach (var invoice in invoices)
        {
            itemLookup.TryGetValue(invoice.Id, out var invoiceItems);
            invoiceItems ??= new List<Garmetix.Core.Models.Inventory.InvoiceItem>();
            var recipientState = StateCode(invoice.CustomerGSTIN) ?? companyStateCode ?? "20";
            var interState = companyStateCode is not null && recipientState != companyStateCode;

            foreach (var line in invoiceItems)
            {
                SplitTax(line.TaxAmount, interState, line.IGSTAmount, line.CGSTAmount, line.SGSTAmount, out var igst, out var cgst, out var sgst);
                var taxable = line.BasePrice;
                if (!string.IsNullOrWhiteSpace(invoice.CustomerGSTIN))
                {
                    b2b.Add(new Gstr1B2BInvoiceRow(
                        invoice.CustomerGSTIN!, invoice.CustomerName ?? "Customer", invoice.InvoiceNumber, invoice.OnDate, recipientState,
                        "N", invoice.ReturnInvoice ? "Credit Note" : "Regular", invoice.BillAmount, line.TaxPercentage,
                        taxable, igst, cgst, sgst, 0, string.Empty));
                }
                else
                {
                    var key = $"B2C|{recipientState}|{line.TaxPercentage}";
                    b2cAccumulator.TryGetValue(key, out var existing);
                    b2cAccumulator[key] = new Gstr1B2CSummaryRow("B2CS", recipientState, string.Empty, line.TaxPercentage,
                        (existing?.TaxableValue ?? 0) + taxable, (existing?.IntegratedTax ?? 0) + igst,
                        (existing?.CentralTax ?? 0) + cgst, (existing?.StateTax ?? 0) + sgst, 0);
                }

                var hsnCode = NormalizeHsn(line.HSNCode ?? line.Product?.HSNCode);
                var uqc = UnitToUqc(line.Unit?.ToString());
                var hsnKey = $"{hsnCode}|{line.TaxPercentage}|{uqc}";
                hsnAccumulator.TryGetValue(hsnKey, out var hsn);
                hsnAccumulator[hsnKey] = new Gstr1HsnSummaryRow(hsnAccumulator.Count + 1, hsnCode, line.ProductName ?? line.Product?.Name ?? line.Barcode, uqc,
                    (hsn?.TotalQuantity ?? 0) + line.BilledQuantity, (hsn?.TotalValue ?? 0) + line.Amount,
                    (hsn?.TaxableValue ?? 0) + taxable, (hsn?.IntegratedTax ?? 0) + igst,
                    (hsn?.CentralTax ?? 0) + cgst, (hsn?.StateTax ?? 0) + sgst, 0);
            }
        }

        var orderedInvoices = invoices.OrderBy(item => item.InvoiceNumber).ToList();
        return Results.Ok(new Gstr1ExportRequest(
            HeaderFromCompany(company.GSTIN, returnPeriod, company.Name, company.Name, invoices.Sum(item => item.BillAmount)),
            b2b,
            b2cAccumulator.Values.ToList(),
            hsnAccumulator.Values.Select((row, index) => row with { SerialNumber = index + 1 }).ToList(),
            new[] { new Gstr1DocumentIssuedRow(1, "Invoices for outward supply", orderedInvoices.FirstOrDefault()?.InvoiceNumber ?? string.Empty, orderedInvoices.LastOrDefault()?.InvoiceNumber ?? string.Empty, invoices.Count, invoices.Count(item => item.InvoiceStatus == InvoiceStatus.Cancelled)) },
            Array.Empty<Gstr1NilRatedRow>()));
    }

    private static async Task<IResult> BuildGstr3BFromBooksAsync(
        Guid? companyId,
        string returnPeriod,
        GarmetixDbContext db,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryPeriodRange(returnPeriod, out var start, out var end))
        {
            return Results.BadRequest(new { message = "Return period must be MMYYYY, for example 042026." });
        }

        var selectedCompanyId = companyId ?? WorkspaceScope.ClaimGuid(context, "companyId");
        if (!selectedCompanyId.HasValue)
        {
            return Results.BadRequest(new { message = "Select a company before generating GST from books." });
        }

        var company = await WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == selectedCompanyId.Value, cancellationToken);
        if (company is null)
        {
            return Results.BadRequest(new { message = "Selected company is outside your access scope." });
        }

        var sales = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == company.Id && item.OnDate >= start && item.OnDate < end && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .ToListAsync(cancellationToken);
        var purchases = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == company.Id && item.OnDate >= start && item.OnDate < end && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .ToListAsync(cancellationToken);

        return Results.Ok(new Gstr3BExportRequest(
            HeaderFromCompany(company.GSTIN, returnPeriod, company.Name, company.Name, sales.Sum(item => item.BillAmount)),
            new Gstr3BSuppliesSummary(sales.Sum(item => item.NetAmount), sales.Sum(item => item.IGSTAmount ?? 0), sales.Sum(item => item.CGSTAmount ?? item.TaxAmount / 2), sales.Sum(item => item.SGSTAmount ?? item.TaxAmount / 2), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Gstr3BInterStateSupply(0, 0, 0, 0, 0, 0),
            new Gstr3BItcSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, purchases.Sum(item => item.IGSTAmount ?? 0), purchases.Sum(item => item.CGSTAmount ?? item.TaxAmount / 2), purchases.Sum(item => item.SGSTAmount ?? item.TaxAmount / 2), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Gstr3BInwardSummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
            new Gstr3BInterestLateFee(0, 0, 0, 0, 0, 0)));
    }

    private static GstReturnPeriodRequest HeaderFromCompany(string? gstin, string returnPeriod, string legalName, string tradeName, decimal currentTurnover) =>
        new(gstin ?? string.Empty, returnPeriod, currentTurnover, currentTurnover, legalName, tradeName);

    private static bool TryPeriodRange(string period, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        if (string.IsNullOrWhiteSpace(period) || period.Length != 6 || !int.TryParse(period[..2], out var month) || !int.TryParse(period[2..], out var year) || month is < 1 or > 12)
        {
            return false;
        }
        start = new DateTime(year, month, 1);
        end = start.AddMonths(1);
        return true;
    }

    private static string? StateCode(string? gstin) => !string.IsNullOrWhiteSpace(gstin) && gstin.Trim().Length >= 2 ? gstin.Trim()[..2] : null;

    private static void SplitTax(decimal tax, bool interState, decimal? storedIgst, decimal? storedCgst, decimal? storedSgst, out decimal igst, out decimal cgst, out decimal sgst)
    {
        var hasStoredSplit = (storedIgst ?? 0) != 0 || (storedCgst ?? 0) != 0 || (storedSgst ?? 0) != 0;
        if (hasStoredSplit)
        {
            igst = Math.Round(storedIgst ?? 0, 2);
            cgst = Math.Round(storedCgst ?? 0, 2);
            sgst = Math.Round(storedSgst ?? 0, 2);
            return;
        }

        if (interState)
        {
            igst = Math.Round(tax, 2);
            cgst = 0;
            sgst = 0;
            return;
        }
        igst = 0;
        cgst = Math.Round(tax / 2, 2);
        sgst = Math.Round(tax - cgst, 2);
    }

    private static async Task<IResult> ListDraftsAsync(
        string? form,
        string? returnPeriod,
        string? gstin,
        int? take,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var query = WorkspaceScope.ApplyTo(db.GstReturnDrafts.AsNoTracking(), context);

        if (!string.IsNullOrWhiteSpace(form) && !form.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedForm = NormalizeForm(form);
            query = query.Where(draft => draft.Form == normalizedForm);
        }

        if (!string.IsNullOrWhiteSpace(returnPeriod))
        {
            var period = returnPeriod.Trim();
            query = query.Where(draft => draft.ReturnPeriod == period);
        }

        if (!string.IsNullOrWhiteSpace(gstin))
        {
            var normalizedGstin = gstin.Trim().ToUpperInvariant();
            query = query.Where(draft => draft.Gstin == normalizedGstin);
        }

        var limit = Math.Clamp(take ?? 50, 1, 200);
        List<GstReturnDraft> rows;
        try
        {
            rows = await query
                .OrderByDescending(draft => draft.UpdatedAt ?? draft.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsMissingRelation(ex))
        {
            await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
            rows = await WorkspaceScope.ApplyTo(db.GstReturnDrafts.AsNoTracking(), context)
                .OrderByDescending(draft => draft.UpdatedAt ?? draft.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        return Results.Ok(rows.Select(ToSummary).ToList());
    }

    private static async Task<IResult> GetDraftAsync(Guid id, GarmetixDbContext db, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return draft is null ? Results.NotFound() : Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> SaveDraftAsync(
        GstReturnDraftSaveRequest request,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        if (!TryBuildDraftState(request, out var state, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        var companyId = request.CompanyId ?? WorkspaceScope.ClaimGuid(context, "companyId");
        if (!companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select a company before saving GST return draft." });
        }

        var actor = Actor(context);
        var draft = new GstReturnDraft
        {
            CompanyId = companyId.Value,
            Form = state.Form,
            Gstin = state.Header.Gstin,
            ReturnPeriod = state.Header.ReturnPeriod,
            Title = DraftTitle(request.Title, state.Form, state.Header),
            Status = "Draft",
            PayloadJson = state.PayloadJson,
            LastPreviewIssuesJson = JsonSerializer.Serialize(state.Preview.Issues, JsonOptions),
            RowCount = state.Preview.RowCount,
            TaxableValue = state.Preview.TaxableValue,
            IntegratedTax = state.Preview.IntegratedTax,
            CentralTax = state.Preview.CentralTax,
            StateTax = state.Preview.StateTax,
            Cess = state.Preview.Cess,
            CreatedBy = actor.Name,
            CreatedByUserId = actor.Id,
            CreatedByUserName = actor.Name,
            UpdatedByUserId = actor.Id,
            UpdatedByUserName = actor.Name
        };

        if (!WorkspaceScope.CanWrite(draft, context, out var message))
        {
            return Results.BadRequest(new { message });
        }

        db.GstReturnDrafts.Add(draft);
        AddAudit(db, draft, "Created", $"Created {DisplayForm(draft.Form)} draft for {draft.ReturnPeriod}.", context, new { draft.RowCount, draft.TaxableValue, draft.IntegratedTax, draft.CentralTax, draft.StateTax, draft.Cess });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/gst-returns/drafts/{draft.Id}", ToDetail(draft));
    }

    private static async Task<IResult> UpdateDraftAsync(
        Guid id,
        GstReturnDraftSaveRequest request,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        if (!TryBuildDraftState(request, out var state, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (draft.LockedAt.HasValue)
        {
            return Results.BadRequest(new { message = "This GST return draft is locked/filed and cannot be edited. Create a new draft for corrections." });
        }

        var actor = Actor(context);
        var before = new { draft.Form, draft.Gstin, draft.ReturnPeriod, draft.Title, draft.RowCount, draft.TaxableValue, draft.Status };
        draft.Form = state.Form;
        draft.Gstin = state.Header.Gstin;
        draft.ReturnPeriod = state.Header.ReturnPeriod;
        draft.Title = DraftTitle(request.Title, state.Form, state.Header);
        draft.PayloadJson = state.PayloadJson;
        draft.LastPreviewIssuesJson = JsonSerializer.Serialize(state.Preview.Issues, JsonOptions);
        draft.RowCount = state.Preview.RowCount;
        draft.TaxableValue = state.Preview.TaxableValue;
        draft.IntegratedTax = state.Preview.IntegratedTax;
        draft.CentralTax = state.Preview.CentralTax;
        draft.StateTax = state.Preview.StateTax;
        draft.Cess = state.Preview.Cess;
        draft.UpdatedByUserId = actor.Id;
        draft.UpdatedByUserName = actor.Name;

        if (!WorkspaceScope.CanWrite(draft, context, out var message))
        {
            return Results.BadRequest(new { message });
        }

        AddAudit(db, draft, "Updated", $"Updated {DisplayForm(draft.Form)} draft for {draft.ReturnPeriod}.", context, new { before, after = new { draft.Form, draft.Gstin, draft.ReturnPeriod, draft.Title, draft.RowCount, draft.TaxableValue, draft.Status } });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> DeleteDraftAsync(Guid id, GarmetixDbContext db, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (draft.LockedAt.HasValue)
        {
            return Results.BadRequest(new { message = "Filed GST return drafts cannot be deleted. Keep them for audit history." });
        }

        draft.Deleted = true;
        AddAudit(db, draft, "Deleted", $"Deleted {DisplayForm(draft.Form)} draft for {draft.ReturnPeriod}.", context, new { draft.Title, draft.RowCount, draft.TaxableValue });
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkFiledAsync(Guid id, GarmetixDbContext db, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (draft.LockedAt.HasValue)
        {
            return Results.Ok(ToDetail(draft));
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        draft.Status = "Filed";
        draft.FiledAt = now;
        draft.LockedAt = now;
        var actor = Actor(context);
        draft.UpdatedByUserId = actor.Id;
        draft.UpdatedByUserName = actor.Name;
        AddAudit(db, draft, "Filed", $"Marked {DisplayForm(draft.Form)} {draft.ReturnPeriod} as filed/locked.", context, new { draft.FiledAt, draft.LockedAt });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> GetDraftAuditAsync(Guid id, GarmetixDbContext db, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var canReadDraft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts.AsNoTracking(), context)
            .AnyAsync(item => item.Id == id, cancellationToken);
        if (!canReadDraft)
        {
            return Results.NotFound();
        }

        var rows = await WorkspaceScope.ApplyTo(db.GstReturnAuditEntries.AsNoTracking(), context)
            .Where(item => item.DraftId == id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(ToAuditDto).ToList());
    }

    private static async Task<IResult> ExportDraftJsonAsync(Guid id, GarmetixDbContext db, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (!TryBuildExportFromDraft(draft, out var bytes, out var contentType, out var extension, out var error, excel: false))
        {
            return Results.BadRequest(new { message = error });
        }

        AddAudit(db, draft, "Exported JSON", $"Downloaded {DisplayForm(draft.Form)} JSON for {draft.ReturnPeriod}.", context, new { format = "json" });
        await db.SaveChangesAsync(cancellationToken);
        return Results.File(bytes!, contentType!, FileName(DisplayForm(draft.Form).Replace("-", string.Empty), new GstReturnPeriodRequest(draft.Gstin, draft.ReturnPeriod, 0, 0, string.Empty, string.Empty), extension!));
    }

    private static async Task<IResult> ExportDraftExcelAsync(Guid id, GarmetixDbContext db, HttpContext context, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound();
        }

        if (!TryBuildExportFromDraft(draft, out var bytes, out var contentType, out var extension, out var error, excel: true))
        {
            return Results.BadRequest(new { message = error });
        }

        AddAudit(db, draft, "Exported Excel", $"Downloaded {DisplayForm(draft.Form)} Excel for {draft.ReturnPeriod}.", context, new { format = "xlsx" });
        await db.SaveChangesAsync(cancellationToken);
        return Results.File(bytes!, contentType!, FileName(DisplayForm(draft.Form).Replace("-", string.Empty), new GstReturnPeriodRequest(draft.Gstin, draft.ReturnPeriod, 0, 0, string.Empty, string.Empty), extension!));
    }


private static async Task<IResult> SendGstReportsReviewAsync(
    [FromBody] GstReportReviewSendRequest request,
    GarmetixDbContext db,
    HttpContext context,
    IEmailSender emailSender,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.ToEmail))
    {
        return Results.BadRequest(new { message = "Accountant/CA email address is required." });
    }

    if (!TryPeriodRange(request.ReturnPeriod, out _, out _))
    {
        return Results.BadRequest(new { message = "Return period must be MMYYYY, for example 042026." });
    }

    var period = request.ReturnPeriod.Trim();
    var direction = NormalizeReportDirection(request.Direction);
    var includeHsn = request.IncludeHsnSummaryCsv;
    var includeTax = request.IncludeTaxSummaryCsv;
    var includeRegister = request.IncludeInvoiceRegisterCsv;
    if (!includeHsn && !includeTax && !includeRegister)
    {
        includeHsn = true;
        includeTax = true;
        includeRegister = true;
    }

    var attachments = new List<EmailAttachment>();
    var attachmentNames = new List<string>();

    if (includeHsn)
    {
        var hsnReport = await BuildHsnSummaryReportAsync(request.CompanyId, period, direction, db, context, cancellationToken);
        if (hsnReport.Error is not null)
        {
            return hsnReport.Error;
        }

        var name = $"Garmetix-HSN-Summary-{direction}-{period}.csv";
        attachments.Add(new EmailAttachment(name, "text/csv", CsvBytes(HsnSummaryCsvRows(hsnReport.Report!))));
        attachmentNames.Add(name);
    }

    if (includeTax)
    {
        var taxReport = await BuildTaxRateSummaryReportAsync(request.CompanyId, period, db, context, cancellationToken);
        if (taxReport.Error is not null)
        {
            return taxReport.Error;
        }

        var name = $"Garmetix-GST-Tax-Summary-{period}.csv";
        attachments.Add(new EmailAttachment(name, "text/csv", CsvBytes(TaxRateSummaryCsvRows(taxReport.Report!))));
        attachmentNames.Add(name);
    }

    if (includeRegister)
    {
        var registerReport = await BuildInvoiceRegisterReportAsync(request.CompanyId, period, direction, db, context, cancellationToken);
        if (registerReport.Error is not null)
        {
            return registerReport.Error;
        }

        var name = $"Garmetix-GST-Invoice-Register-{direction}-{period}.csv";
        attachments.Add(new EmailAttachment(name, "text/csv", CsvBytes(InvoiceRegisterCsvRows(registerReport.Report!))));
        attachmentNames.Add(name);
    }

    var toEmail = request.ToEmail.Trim();
    var note = string.IsNullOrWhiteSpace(request.Note)
        ? "Please review the attached GST book reports generated from Garmetix billing, purchase and accounting records."
        : request.Note.Trim();
    var subject = $"Garmetix GST book reports review - {period} - {direction}";
    var summaryText = BuildGstReportsSummaryText(period, direction, note, attachmentNames);
    var summaryHtml = BuildGstReportsSummaryHtml(period, direction, note, attachmentNames);

    await emailSender.SendAsync(new EmailMessage(
        toEmail,
        request.ToName?.Trim() ?? "Accountant/CA",
        subject,
        summaryHtml,
        summaryText,
        attachments), cancellationToken);

    var whatsAppText = $"Garmetix GST book reports for {period} / {direction} have been emailed to {toEmail}. Attachments: {string.Join(", ", attachmentNames)}. Please review and confirm.";
    var whatsAppUrl = BuildWhatsAppShareUrl(request.WhatsAppNumber, whatsAppText);

    return Results.Ok(new GstReportReviewSendResponse(
        period,
        direction,
        toEmail,
        true,
        "GST book reports sent to Accountant/CA.",
        whatsAppText,
        whatsAppUrl,
        attachmentNames));
}

    private static async Task<IResult> SendDraftReviewAsync(
        Guid id,
        [FromBody] GstReturnReviewSendRequest request,
        GarmetixDbContext db,
        HttpContext context,
        IEmailSender emailSender,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureGstDraftStorageAsync(db, loggerFactory, cancellationToken);
        var draft = await WorkspaceScope.ApplyTo(db.GstReturnDrafts, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (draft is null)
        {
            return Results.NotFound(new { message = "GST return draft was not found." });
        }

        if (string.IsNullOrWhiteSpace(request.ToEmail))
        {
            return Results.BadRequest(new { message = "Accountant/CA email address is required." });
        }

        var attachmentNames = new List<string>();
        var attachments = new List<EmailAttachment>();
        var formName = DisplayForm(draft.Form);
        var subject = $"Garmetix {formName} review package - {draft.ReturnPeriod} - {draft.Gstin}";

        if (request.IncludeJson)
        {
            if (!TryBuildExportFromDraft(draft, out var jsonBytes, out var jsonContentType, out var jsonExtension, out var jsonError, excel: false))
            {
                return Results.BadRequest(new { message = jsonError ?? "GST JSON export could not be created. Review and fix validation issues first." });
            }
            var jsonName = FileName(formName.Replace("-", string.Empty), new GstReturnPeriodRequest(draft.Gstin, draft.ReturnPeriod, 0, 0, string.Empty, string.Empty), jsonExtension!);
            attachments.Add(new EmailAttachment(jsonName, jsonContentType!, jsonBytes!));
            attachmentNames.Add(jsonName);
        }

        if (request.IncludeExcel)
        {
            if (!TryBuildExportFromDraft(draft, out var excelBytes, out var excelContentType, out var excelExtension, out var excelError, excel: true))
            {
                return Results.BadRequest(new { message = excelError ?? "GST Excel export could not be created. Review and fix validation issues first." });
            }
            var excelName = FileName(formName.Replace("-", string.Empty), new GstReturnPeriodRequest(draft.Gstin, draft.ReturnPeriod, 0, 0, string.Empty, string.Empty), excelExtension!);
            attachments.Add(new EmailAttachment(excelName, excelContentType!, excelBytes!));
            attachmentNames.Add(excelName);
        }

        if (request.IncludeHsnSummaryCsv)
        {
            var hsnReport = await BuildHsnSummaryReportAsync(draft.CompanyId, draft.ReturnPeriod, "both", db, context, cancellationToken);
            if (hsnReport.Error is not null)
            {
                return hsnReport.Error;
            }
            var name = $"Garmetix-HSN-Summary-both-{draft.ReturnPeriod}.csv";
            attachments.Add(new EmailAttachment(name, "text/csv", CsvBytes(HsnSummaryCsvRows(hsnReport.Report!))));
            attachmentNames.Add(name);
        }

        if (request.IncludeTaxSummaryCsv)
        {
            var taxReport = await BuildTaxRateSummaryReportAsync(draft.CompanyId, draft.ReturnPeriod, db, context, cancellationToken);
            if (taxReport.Error is not null)
            {
                return taxReport.Error;
            }
            var name = $"Garmetix-GST-Tax-Summary-{draft.ReturnPeriod}.csv";
            attachments.Add(new EmailAttachment(name, "text/csv", CsvBytes(TaxRateSummaryCsvRows(taxReport.Report!))));
            attachmentNames.Add(name);
        }

        if (request.IncludeInvoiceRegisterCsv)
        {
            var registerReport = await BuildInvoiceRegisterReportAsync(draft.CompanyId, draft.ReturnPeriod, "both", db, context, cancellationToken);
            if (registerReport.Error is not null)
            {
                return registerReport.Error;
            }
            var name = $"Garmetix-GST-Invoice-Register-both-{draft.ReturnPeriod}.csv";
            attachments.Add(new EmailAttachment(name, "text/csv", CsvBytes(InvoiceRegisterCsvRows(registerReport.Report!))));
            attachmentNames.Add(name);
        }

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? "Please review the attached GST return and book reports."
            : request.Note.Trim();
        var summaryText = BuildGstReviewSummaryText(draft, formName, note, attachmentNames);
        var summaryHtml = BuildGstReviewSummaryHtml(draft, formName, note, attachmentNames);

        await emailSender.SendAsync(new EmailMessage(
            request.ToEmail.Trim(),
            request.ToName?.Trim() ?? "Accountant/CA",
            subject,
            summaryHtml,
            summaryText,
            attachments), cancellationToken);

        var actor = Actor(context);
        if (!draft.LockedAt.HasValue)
        {
            draft.Status = "Reviewed";
        }
        draft.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        draft.UpdatedByUserId = actor.Id;
        draft.UpdatedByUserName = actor.Name;
        AddAudit(db, draft, "Shared for CA Review", $"Sent {formName} {draft.ReturnPeriod} review package to {request.ToEmail.Trim()}.", context,
            new { request.ToEmail, request.ToName, request.WhatsAppNumber, attachmentNames, note });
        await db.SaveChangesAsync(cancellationToken);

        var whatsAppText = BuildWhatsAppText(draft, formName, request.ToEmail.Trim(), attachmentNames);
        var whatsAppUrl = BuildWhatsAppShareUrl(request.WhatsAppNumber, whatsAppText);

        return Results.Ok(new GstReturnReviewSendResponse(
            draft.Id,
            formName,
            draft.ReturnPeriod,
            request.ToEmail.Trim(),
            true,
            $"{formName} review package sent to Accountant/CA.",
            whatsAppText,
            whatsAppUrl,
            attachmentNames));
    }

    private static IEnumerable<string[]> HsnSummaryCsvRows(GstHsnSummaryReport report)
    {
        yield return new[] { "Serial", "Direction", "HSN", "Description", "UQC", "Rate", "Quantity", "Taxable", "CGST", "SGST", "IGST", "Tax", "Total" };
        foreach (var row in report.Rows)
        {
            yield return new[]
            {
                row.SerialNumber.ToString(CultureInfo.InvariantCulture), row.Direction, row.HsnCode, row.Description, row.Uqc,
                row.Rate.ToString("0.##", CultureInfo.InvariantCulture), row.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
                row.TaxableValue.ToString("0.00", CultureInfo.InvariantCulture), row.CgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                row.SgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                row.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture), row.TotalValue.ToString("0.00", CultureInfo.InvariantCulture)
            };
        }
    }

    private static IEnumerable<string[]> TaxRateSummaryCsvRows(GstTaxRateSummaryReport report)
    {
        yield return new[] { "Rate", "Sales Taxable", "Sales CGST", "Sales SGST", "Sales IGST", "Purchase Taxable", "Purchase CGST", "Purchase SGST", "Purchase IGST", "Net Tax Payable" };
        foreach (var row in report.Rows)
        {
            yield return new[]
            {
                row.Rate.ToString("0.##", CultureInfo.InvariantCulture), row.SalesTaxableValue.ToString("0.00", CultureInfo.InvariantCulture),
                row.SalesCgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.SalesSgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                row.SalesIgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.PurchaseTaxableValue.ToString("0.00", CultureInfo.InvariantCulture),
                row.PurchaseCgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.PurchaseSgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                row.PurchaseIgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.NetTaxPayable.ToString("0.00", CultureInfo.InvariantCulture)
            };
        }
    }

    private static IEnumerable<string[]> InvoiceRegisterCsvRows(GstInvoiceRegisterReport report)
    {
        yield return new[] { "Direction", "Invoice", "Reference", "Date", "Party", "GSTIN", "Status", "Return", "Taxable", "CGST", "SGST", "IGST", "Tax", "Bill" };
        foreach (var row in report.Rows)
        {
            yield return new[]
            {
                row.Direction, row.InvoiceNumber, row.ReferenceNumber ?? string.Empty, row.OnDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.PartyName, row.PartyGstin ?? string.Empty, row.InvoiceStatus, row.IsReturn ? "Yes" : "No",
                row.TaxableValue.ToString("0.00", CultureInfo.InvariantCulture), row.CgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                row.SgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                row.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture), row.BillAmount.ToString("0.00", CultureInfo.InvariantCulture)
            };
        }
    }

    private static string BuildGstReviewSummaryText(GstReturnDraft draft, string formName, string note, IReadOnlyList<string> attachments)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"GST Review Package: {formName} {draft.ReturnPeriod}");
        builder.AppendLine($"GSTIN: {draft.Gstin}");
        builder.AppendLine($"Taxable value: {draft.TaxableValue:0.00}");
        builder.AppendLine($"IGST: {draft.IntegratedTax:0.00}");
        builder.AppendLine($"CGST: {draft.CentralTax:0.00}");
        builder.AppendLine($"SGST: {draft.StateTax:0.00}");
        builder.AppendLine($"Cess: {draft.Cess:0.00}");
        builder.AppendLine($"Rows: {draft.RowCount}");
        builder.AppendLine();
        builder.AppendLine(note);
        builder.AppendLine();
        builder.AppendLine("Attachments:");
        foreach (var attachment in attachments)
        {
            builder.AppendLine($"- {attachment}");
        }
        return builder.ToString();
    }

    private static string BuildGstReviewSummaryHtml(GstReturnDraft draft, string formName, string note, IReadOnlyList<string> attachments)
    {
        static string H(string value) => WebUtility.HtmlEncode(value);
        var items = string.Join(string.Empty, attachments.Select(item => $"<li>{H(item)}</li>"));
        return $"""
            <h2>GST Review Package: {H(formName)} {H(draft.ReturnPeriod)}</h2>
            <p>{H(note)}</p>
            <table cellpadding="6" cellspacing="0" border="1">
              <tr><td>GSTIN</td><td>{H(draft.Gstin)}</td></tr>
              <tr><td>Taxable Value</td><td>{draft.TaxableValue:0.00}</td></tr>
              <tr><td>IGST</td><td>{draft.IntegratedTax:0.00}</td></tr>
              <tr><td>CGST</td><td>{draft.CentralTax:0.00}</td></tr>
              <tr><td>SGST</td><td>{draft.StateTax:0.00}</td></tr>
              <tr><td>Cess</td><td>{draft.Cess:0.00}</td></tr>
              <tr><td>Rows</td><td>{draft.RowCount}</td></tr>
            </table>
            <h3>Attachments</h3>
            <ul>{items}</ul>
            <p>Sent from Garmetix after GST review confirmation.</p>
            """;
    }


    private static string BuildGstReportsSummaryText(string period, string direction, string note, IReadOnlyList<string> attachments)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"GST Book Reports: {period} / {direction}");
        builder.AppendLine(note);
        builder.AppendLine();
        builder.AppendLine("Attachments:");
        foreach (var attachment in attachments)
        {
            builder.AppendLine($"- {attachment}");
        }
        return builder.ToString();
    }

    private static string BuildGstReportsSummaryHtml(string period, string direction, string note, IReadOnlyList<string> attachments)
    {
        static string H(string value) => WebUtility.HtmlEncode(value);
        var items = string.Join(string.Empty, attachments.Select(item => $"<li>{H(item)}</li>"));
        return $"""
            <h2>GST Book Reports: {H(period)} / {H(direction)}</h2>
            <p>{H(note)}</p>
            <h3>Attachments</h3>
            <ul>{items}</ul>
            <p>Sent from Garmetix after GST report review confirmation.</p>
            """;
    }

    private static string BuildWhatsAppText(GstReturnDraft draft, string formName, string toEmail, IReadOnlyList<string> attachments) =>
        $"Garmetix GST review package for {formName} {draft.ReturnPeriod} ({draft.Gstin}) has been emailed to {toEmail}. Attachments: {string.Join(", ", attachments)}. Please review and confirm.";

    private static string BuildWhatsAppShareUrl(string? phone, string text)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        var encoded = Uri.EscapeDataString(text);
        return string.IsNullOrWhiteSpace(digits)
            ? $"https://wa.me/?text={encoded}"
            : $"https://wa.me/{digits}?text={encoded}";
    }

    private static bool TryBuildExportFromDraft(GstReturnDraft draft, out byte[]? bytes, out string? contentType, out string? extension, out string? error, bool excel)
    {
        bytes = null;
        contentType = null;
        extension = null;
        error = null;

        try
        {
            if (draft.Form == "gstr1")
            {
                var request = JsonSerializer.Deserialize<Gstr1ExportRequest>(draft.PayloadJson, JsonOptions);
                if (request is null)
                {
                    error = "Saved GSTR-1 draft payload is empty.";
                    return false;
                }

                var normalized = Normalize(request);
                var issues = GstReturnExportService.PreviewGstr1(normalized).Issues;
                if (issues.Count > 0)
                {
                    error = "Fix GST validation issues before export.";
                    return false;
                }

                bytes = excel ? GstReturnExportService.BuildGstr1Excel(normalized) : GstReturnExportService.BuildGstr1Json(normalized);
                contentType = excel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/json";
                extension = excel ? "xlsx" : "json";
                return true;
            }

            if (draft.Form == "gstr3b")
            {
                var request = JsonSerializer.Deserialize<Gstr3BExportRequest>(draft.PayloadJson, JsonOptions);
                if (request is null)
                {
                    error = "Saved GSTR-3B draft payload is empty.";
                    return false;
                }

                var normalized = Normalize(request);
                var issues = GstReturnExportService.PreviewGstr3B(normalized).Issues;
                if (issues.Count > 0)
                {
                    error = "Fix GST validation issues before export.";
                    return false;
                }

                bytes = excel ? GstReturnExportService.BuildGstr3BExcel(normalized) : GstReturnExportService.BuildGstr3BJson(normalized);
                contentType = excel ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/json";
                extension = excel ? "xlsx" : "json";
                return true;
            }

            error = "Unsupported GST return form.";
            return false;
        }
        catch (JsonException ex)
        {
            error = $"Saved draft payload is invalid JSON: {ex.Message}";
            return false;
        }
    }

    private static bool TryBuildDraftState(GstReturnDraftSaveRequest request, out DraftState state, out string? error)
    {
        state = default!;
        error = null;
        var form = NormalizeForm(request.Form);
        if (form is not "gstr1" and not "gstr3b")
        {
            error = "GST return form must be gstr1 or gstr3b.";
            return false;
        }

        if (request.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            error = "GST return payload is required.";
            return false;
        }

        try
        {
            if (form == "gstr1")
            {
                var parsed = JsonSerializer.Deserialize<Gstr1ExportRequest>(request.Payload.GetRawText(), JsonOptions);
                if (parsed is null)
                {
                    error = "GSTR-1 payload is empty.";
                    return false;
                }

                var normalized = Normalize(parsed);
                var preview = GstReturnExportService.PreviewGstr1(normalized);
                state = new DraftState(form, normalized.Header, preview, JsonSerializer.Serialize(normalized, JsonOptions));
                return true;
            }

            var gstr3b = JsonSerializer.Deserialize<Gstr3BExportRequest>(request.Payload.GetRawText(), JsonOptions);
            if (gstr3b is null)
            {
                error = "GSTR-3B payload is empty.";
                return false;
            }

            var normalized3B = Normalize(gstr3b);
            var preview3B = GstReturnExportService.PreviewGstr3B(normalized3B);
            state = new DraftState(form, normalized3B.Header, preview3B, JsonSerializer.Serialize(normalized3B, JsonOptions));
            return true;
        }
        catch (JsonException ex)
        {
            error = $"GST return payload is invalid JSON: {ex.Message}";
            return false;
        }
    }

    private static Gstr1ExportRequest Normalize(Gstr1ExportRequest request) => request with
    {
        Header = Normalize(request.Header),
        B2BInvoices = request.B2BInvoices ?? [],
        B2CSummaries = request.B2CSummaries ?? [],
        HsnSummaries = request.HsnSummaries ?? [],
        DocumentsIssued = request.DocumentsIssued ?? [],
        NilRatedSupplies = request.NilRatedSupplies ?? []
    };

    private static Gstr3BExportRequest Normalize(Gstr3BExportRequest request) => request with
    {
        Header = Normalize(request.Header),
        Supplies = request.Supplies ?? EmptySupplies,
        InterStateSupplies = request.InterStateSupplies ?? EmptyInterState,
        Itc = request.Itc ?? EmptyItc,
        InwardSupplies = request.InwardSupplies ?? EmptyInward,
        InterestLateFee = request.InterestLateFee ?? EmptyFee
    };

    private static GstReturnPeriodRequest Normalize(GstReturnPeriodRequest? header) => header is null
        ? new GstReturnPeriodRequest(string.Empty, string.Empty, 0, 0, string.Empty, string.Empty)
        : header with
        {
            Gstin = (header.Gstin ?? string.Empty).Trim().ToUpperInvariant(),
            ReturnPeriod = (header.ReturnPeriod ?? string.Empty).Trim(),
            LegalName = (header.LegalName ?? string.Empty).Trim(),
            TradeName = (header.TradeName ?? string.Empty).Trim()
        };

    private static string FileName(string form, GstReturnPeriodRequest header, string extension)
    {
        var gstin = string.IsNullOrWhiteSpace(header.Gstin) ? "GSTIN" : header.Gstin.Trim().ToUpperInvariant();
        var period = string.IsNullOrWhiteSpace(header.ReturnPeriod) ? "MMYYYY" : header.ReturnPeriod.Trim();
        return $"Garmetix-{form}-{gstin}-{period}.{extension}";
    }

    private static string NormalizeForm(string? form) => (form ?? string.Empty).Trim().ToLowerInvariant().Replace("-", string.Empty);
    private static string DisplayForm(string form) => NormalizeForm(form) == "gstr3b" ? "GSTR-3B" : "GSTR-1";

    private static string DraftTitle(string? title, string form, GstReturnPeriodRequest header)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return $"{DisplayForm(form)} {header.ReturnPeriod} {header.Gstin}".Trim();
    }

    private static ActorInfo Actor(HttpContext context)
    {
        var name = context.User.Identity?.Name
            ?? context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst("userName")?.Value
            ?? context.User.FindFirst(ClaimTypes.Email)?.Value
            ?? "System";
        var idValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return new ActorInfo(Guid.TryParse(idValue, out var id) ? id : null, name);
    }

    private static void AddAudit(GarmetixDbContext db, GstReturnDraft draft, string action, string summary, HttpContext context, object details)
    {
        var actor = Actor(context);
        db.GstReturnAuditEntries.Add(new GstReturnAuditEntry
        {
            CompanyId = draft.CompanyId,
            DraftId = draft.Id,
            Form = draft.Form,
            ReturnPeriod = draft.ReturnPeriod,
            Gstin = draft.Gstin,
            Action = action,
            Summary = summary,
            ActorUserId = actor.Id,
            ActorName = actor.Name,
            CreatedBy = actor.Name,
            DetailsJson = JsonSerializer.Serialize(details, JsonOptions)
        });
    }

    private static GstReturnDraftSummaryDto ToSummary(GstReturnDraft draft) => new(
        draft.Id,
        draft.Form,
        draft.Gstin,
        draft.ReturnPeriod,
        draft.Title,
        draft.Status,
        draft.RowCount,
        draft.TaxableValue,
        draft.IntegratedTax,
        draft.CentralTax,
        draft.StateTax,
        draft.Cess,
        draft.CreatedAt,
        draft.UpdatedAt,
        draft.UpdatedByUserName);

    private static GstReturnDraftDetailDto ToDetail(GstReturnDraft draft) => new(
        draft.Id,
        draft.Form,
        draft.Gstin,
        draft.ReturnPeriod,
        draft.Title,
        draft.Status,
        draft.CompanyId,
        draft.PayloadJson,
        draft.LastPreviewIssuesJson,
        draft.RowCount,
        draft.TaxableValue,
        draft.IntegratedTax,
        draft.CentralTax,
        draft.StateTax,
        draft.Cess,
        draft.CreatedAt,
        draft.UpdatedAt,
        draft.CreatedByUserName,
        draft.UpdatedByUserName,
        draft.FiledAt,
        draft.LockedAt);

    private static GstReturnAuditDto ToAuditDto(GstReturnAuditEntry entry) => new(
        entry.Id,
        entry.DraftId,
        entry.Form,
        entry.ReturnPeriod,
        entry.Gstin,
        entry.Action,
        entry.Summary,
        entry.ActorName,
        entry.CreatedAt,
        entry.DetailsJson);


    private static decimal TotalOutputTax(Gstr3BExportRequest request) =>
        Math.Round(
            request.Supplies.OutwardIntegratedTax + request.Supplies.OutwardCentralTax + request.Supplies.OutwardStateTax + request.Supplies.OutwardCess +
            request.Supplies.ReverseChargeIntegratedTax + request.Supplies.ReverseChargeCentralTax + request.Supplies.ReverseChargeStateTax + request.Supplies.ReverseChargeCess,
            2,
            MidpointRounding.AwayFromZero);

    private static decimal TotalInputTax(Gstr3BExportRequest request)
    {
        var available =
            request.Itc.ImportGoodsIntegratedTax + request.Itc.ImportGoodsCess +
            request.Itc.ImportServicesIntegratedTax +
            request.Itc.ReverseChargeIntegratedTax + request.Itc.ReverseChargeCentralTax + request.Itc.ReverseChargeStateTax + request.Itc.ReverseChargeCess +
            request.Itc.IsdIntegratedTax + request.Itc.IsdCentralTax + request.Itc.IsdStateTax + request.Itc.IsdCess +
            request.Itc.OtherIntegratedTax + request.Itc.OtherCentralTax + request.Itc.OtherStateTax + request.Itc.OtherCess;
        var reversals =
            request.Itc.ReversalRule42IntegratedTax + request.Itc.ReversalRule42CentralTax + request.Itc.ReversalRule42StateTax + request.Itc.ReversalRule42Cess +
            request.Itc.ReversalOtherIntegratedTax + request.Itc.ReversalOtherCentralTax + request.Itc.ReversalOtherStateTax + request.Itc.ReversalOtherCess +
            request.Itc.IneligibleIntegratedTax + request.Itc.IneligibleCentralTax + request.Itc.IneligibleStateTax + request.Itc.IneligibleCess;
        return Math.Round(Math.Max(0, available - reversals), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal TotalInterestLateFee(Gstr3BExportRequest request) =>
        Math.Round(
            request.InterestLateFee.IntegratedTaxInterest + request.InterestLateFee.CentralTaxInterest + request.InterestLateFee.StateTaxInterest + request.InterestLateFee.CessInterest +
            request.InterestLateFee.CentralLateFee + request.InterestLateFee.StateLateFee,
            2,
            MidpointRounding.AwayFromZero);

    private static readonly Gstr3BSuppliesSummary EmptySupplies = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BInterStateSupply EmptyInterState = new(0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BItcSummary EmptyItc = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BInwardSummary EmptyInward = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Gstr3BInterestLateFee EmptyFee = new(0, 0, 0, 0, 0, 0);

    private sealed record DraftState(string Form, GstReturnPeriodRequest Header, GstExportPreview Preview, string PayloadJson);
    private sealed record ActorInfo(Guid? Id, string Name);
}

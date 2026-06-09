using System.Security.Claims;
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
        var logger = loggerFactory.CreateLogger("GstDraftStorageRepair");
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "GstReturnDrafts" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "Form" text NOT NULL DEFAULT '',
                    "Gstin" text NOT NULL DEFAULT '',
                    "ReturnPeriod" text NOT NULL DEFAULT '',
                    "Title" text NOT NULL DEFAULT '',
                    "Status" text NOT NULL DEFAULT 'Draft',
                    "PayloadJson" text NOT NULL DEFAULT '{}',
                    "LastPreviewIssuesJson" text NOT NULL DEFAULT '[]',
                    "RowCount" integer NOT NULL DEFAULT 0,
                    "TaxableValue" numeric(18,2) NOT NULL DEFAULT 0,
                    "IntegratedTax" numeric(18,2) NOT NULL DEFAULT 0,
                    "CentralTax" numeric(18,2) NOT NULL DEFAULT 0,
                    "StateTax" numeric(18,2) NOT NULL DEFAULT 0,
                    "Cess" numeric(18,2) NOT NULL DEFAULT 0,
                    "CreatedByUserId" uuid NULL,
                    "CreatedByUserName" text NOT NULL DEFAULT '',
                    "UpdatedByUserId" uuid NULL,
                    "UpdatedByUserName" text NOT NULL DEFAULT '',
                    "FiledAt" timestamp without time zone NULL,
                    "LockedAt" timestamp without time zone NULL,
                    CONSTRAINT "PK_GstReturnDrafts" PRIMARY KEY ("Id")
                );

                CREATE TABLE IF NOT EXISTS "GstReturnAuditEntries" (
                    "Id" uuid NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp without time zone NULL,
                    "Synced" boolean NOT NULL DEFAULT false,
                    "Deleted" boolean NOT NULL DEFAULT false,
                    "CompanyId" uuid NOT NULL,
                    "CreatedBy" text NULL,
                    "DraftId" uuid NOT NULL,
                    "Form" text NOT NULL DEFAULT '',
                    "ReturnPeriod" text NOT NULL DEFAULT '',
                    "Gstin" text NOT NULL DEFAULT '',
                    "Action" text NOT NULL DEFAULT '',
                    "Summary" text NOT NULL DEFAULT '',
                    "ActorUserId" uuid NULL,
                    "ActorName" text NOT NULL DEFAULT '',
                    "DetailsJson" text NOT NULL DEFAULT '{}',
                    CONSTRAINT "PK_GstReturnAuditEntries" PRIMARY KEY ("Id")
                );

                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "Title" text NOT NULL DEFAULT '';
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "LastPreviewIssuesJson" text NOT NULL DEFAULT '[]';
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "CreatedByUserId" uuid NULL;
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "CreatedByUserName" text NOT NULL DEFAULT '';
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "UpdatedByUserId" uuid NULL;
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "UpdatedByUserName" text NOT NULL DEFAULT '';
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "FiledAt" timestamp without time zone NULL;
                ALTER TABLE "GstReturnDrafts" ADD COLUMN IF NOT EXISTS "LockedAt" timestamp without time zone NULL;

                ALTER TABLE "GstReturnAuditEntries" ADD COLUMN IF NOT EXISTS "ActorUserId" uuid NULL;
                ALTER TABLE "GstReturnAuditEntries" ADD COLUMN IF NOT EXISTS "ActorName" text NOT NULL DEFAULT '';
                ALTER TABLE "GstReturnAuditEntries" ADD COLUMN IF NOT EXISTS "DetailsJson" text NOT NULL DEFAULT '{}';

                CREATE INDEX IF NOT EXISTS "IX_GstReturnDrafts_CompanyId_Form_ReturnPeriod_Gstin" ON "GstReturnDrafts" ("CompanyId", "Form", "ReturnPeriod", "Gstin");
                CREATE INDEX IF NOT EXISTS "IX_GstReturnDrafts_CompanyId_Status_UpdatedAt" ON "GstReturnDrafts" ("CompanyId", "Status", "UpdatedAt");
                CREATE INDEX IF NOT EXISTS "IX_GstReturnAuditEntries_CompanyId_DraftId_CreatedAt" ON "GstReturnAuditEntries" ("CompanyId", "DraftId", "CreatedAt");
                CREATE INDEX IF NOT EXISTS "IX_GstReturnAuditEntries_CompanyId_Form_ReturnPeriod" ON "GstReturnAuditEntries" ("CompanyId", "Form", "ReturnPeriod");
                """, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GST draft storage repair failed. The endpoint may still fail until database migrations are applied manually.");
        }
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
                SplitTax(line.TaxAmount, interState, out var igst, out var cgst, out var sgst);
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

                var hsnCode = string.IsNullOrWhiteSpace(line.Product?.Barcode) ? "0000" : (line.Product?.Barcode ?? "0000");
                var hsnKey = $"{hsnCode}|{line.TaxPercentage}";
                hsnAccumulator.TryGetValue(hsnKey, out var hsn);
                hsnAccumulator[hsnKey] = new Gstr1HsnSummaryRow(hsnAccumulator.Count + 1, hsnCode, line.Product?.Name ?? line.Barcode, "NOS",
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

    private static void SplitTax(decimal tax, bool interState, out decimal igst, out decimal cgst, out decimal sgst)
    {
        if (interState)
        {
            igst = tax;
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
        var rows = await query
            .OrderByDescending(draft => draft.UpdatedAt ?? draft.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

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

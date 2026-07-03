using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Garmetix.Api.Accounting;
using Garmetix.Api.Gstin;
using Garmetix.Api.Inventory;
using Garmetix.Api.Numbering;
using Garmetix.Api.Purchase;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.PurchaseImport;

public sealed class PurchaseInvoiceImportService(
    GarmetixDbContext db,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    DocumentNumberService documentNumbers,
    AccountingPostingService accounting,
    GstinLookupService gstinLookup,
    StockLedgerService stockLedger,
    ILogger<PurchaseInvoiceImportService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private const long MaxUploadBytes = 20L * 1024L * 1024L;
    private static readonly Regex GstinRegex = new(@"\b[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountRegex = new(@"(?<label>grand\s*total|invoice\s*total|net\s*payable|amount\s*payable|total\s*amount|bill\s*amount)\s*[:\-]?\s*(?:rs\.?|inr|₹)?\s*(?<amount>[0-9,]+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InvoiceRegex = new(@"\b(?:invoice|inv\.?|bill)\s*(?:no\.?|number|#)?\s*[:\-]?\s*(?<number>[A-Z0-9][A-Z0-9\-/]{2,30})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DateRegex = new(@"\b(?:invoice\s*date|bill\s*date|date)\s*[:\-]?\s*(?<date>\d{1,2}[\-/\.]\d{1,2}[\-/\.]\d{2,4}|\d{4}[\-/\.]\d{1,2}[\-/\.]\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ComponentTaxRegex = new(@"\b(?<label>cgst|sgst|igst)\b\s*[:\-]?\s*(?:rs\.?|inr|₹)?\s*(?<amount>[0-9,]+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LineRegex = new(@"^\s*(?:\d+\s*[).\-]\s*)?(?<name>[A-Za-z][A-Za-z0-9 /&.,_+\-()]{3,90}?)\s+(?<hsn>\d{4,8})?\s*(?<qty>\d+(?:\.\d{1,3})?)\s+(?<rate>\d+(?:\.\d{1,2})?)\s+(?<amount>\d+(?:\.\d{1,2})?)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PercentRegex = new(@"(?<rate>0|3|5|6|12|18|28)(?:\.0{1,2})?\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HsnRegex = new(@"\b(?<hsn>\d{4,8})\b", RegexOptions.Compiled);
    private static readonly Regex MoneyTokenRegex = new(@"(?<![A-Z0-9])(?<value>\d{1,3}(?:,\d{3})*(?:\.\d{1,3})?|\d+(?:\.\d{1,3})?)(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FreightRegex = new(@"\b(?:freight|transport|transportation|shipping|delivery|packing|courier|forwarding)\b\s*[:\-]?\s*(?:rs\.?|inr|₹)?\s*(?<amount>[0-9,]+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DiscountRegex = new(@"\b(?:discount|less|scheme\s*discount|cash\s*discount)\b\s*[:\-]?\s*(?:rs\.?|inr|₹)?\s*(?<amount>[0-9,]+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RoundOffRegex = new(@"\b(?:round\s*off|rounded\s*off|rounding)\b\s*[:\-]?\s*(?:rs\.?|inr|₹)?\s*(?<amount>-?[0-9,]+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MobileRegex = new(@"(?<!\d)(?:\+91[-\s]?)?(?<mobile>[6-9]\d{9})(?!\d)", RegexOptions.Compiled);
    private sealed record BatchFileStorageStats(Guid BatchId, int FileCount, long FileBytes);

    private static readonly IReadOnlyList<PurchaseInvoiceImportParserTemplateDto> ParserTemplates = new[]
    {
        new PurchaseInvoiceImportParserTemplateDto(
            "auto",
            "Auto detect",
            "Default mode. Uses vendor learning first, then Tally/column/header detection, then conservative generic fallback.",
            false,
            new[] { "Mixed vendors", "Unknown supplier invoices", "Normal day-to-day import" }),
        new PurchaseInvoiceImportParserTemplateDto(
            "tally-prime",
            "Tally Prime / common GST invoice",
            "Best for common Tally Prime invoices where item rows have HSN, quantity, rate, discount, GST and amount columns.",
            false,
            new[] { "Tally Prime", "GST item table", "CGST/SGST/IGST summary", "HSN-wise invoices" }),
        new PurchaseInvoiceImportParserTemplateDto(
            "garment-column",
            "Garment article/brand/size column layout",
            "Best for garment supplier invoices where article/model, brand, size, HSN, qty, rate, discount %, GST % and taxable amount are separated by columns.",
            true,
            new[] { "S.K APPARELS", "Brand + Art No invoices", "Size-wise garment bills" }),
        new PurchaseInvoiceImportParserTemplateDto(
            "tally-prime-mrp",
            "Tally Prime with MRP / CGST-SGST QA",
            "QA-focused Tally template option for invoices that include MRP columns, CGST/SGST tax split, discount and freight/packing rows.",
            true,
            new[] { "Tally Prime", "MRP column", "CGST/SGST", "Discount amount", "Freight or packing charges" }),
        new PurchaseInvoiceImportParserTemplateDto(
            "generic-conservative",
            "Generic conservative parser",
            "Safe fallback that only accepts rows where product text, quantity, rate and amount reconcile clearly.",
            false,
            new[] { "Unknown layouts", "Manual pasted text", "Poor OCR where false positives are risky" })
    };


    private static readonly IReadOnlyList<string> FinalClosureKnownLimitations = new[]
    {
        "OCR/parser accuracy still depends on supplier invoice scan quality. Every import must be compared with the original supplier proof before posting.",
        "Direct undo/delete remains intentionally blocked for posted imports. Use controlled purchase inward revision, purchase return or reversal so stock, vendor balance, GST and accounting stay consistent.",
        "Unusual supplier layouts may still need vendor learning rules or manual draft correction even when auto/tally/garment templates are available.",
        "The database backup alone is not enough. Supplier proof files under purchase-imports must be backed up and restored with the PostgreSQL dump.",
        "The backup/restore card shows commands and verification steps, but the final restore drill still has to be run on a disposable/test system by the operator.",
        "Purchase import does not replace accountant review for GST input eligibility, supplier credit notes, debit notes or statutory filing decisions."
    };

    private static readonly IReadOnlyList<string> FinalClosureOperatorRules = new[]
    {
        "Do not post a supplier import until vendor, invoice number/date, item quantities, MRP/cost, discount, GST split and grand total match the supplier proof.",
        "Mark an import Pass only after the real posted purchase inward and supplier proof have been compared.",
        "If a posted import is wrong, flag Correction Required and correct through the purchase module; do not delete the import batch or its proof.",
        "Before cleanup or correction work, create a fresh database backup plus purchase-import proof archive.",
        "After restore, verify at least one posted purchase inward can open its original supplier proof and audit files."
    };

    private static readonly IReadOnlyList<string> FinalClosureNextModuleCandidates = new[]
    {
        "Vyapar Sale Import final summary PDF/Excel, import rollback report and GST/payment/customer/stock reconciliation.",
        "Attendance/Payroll real-month validation, left-employee cutoff checks and attendance correction audit history.",
        "Day Book Excel/PDF export and deeper source-page anchors for direct edit/view from transaction rows.",
        "Print/PDF final evidence on real sale and purchase invoices with large-invoice pagination.",
        "Accounting/GST validation after real imports: purchase register, input tax report, vendor payable, stock valuation and profit calculation."
    };

    public IReadOnlyList<PurchaseInvoiceImportParserTemplateDto> ListParserTemplates() => ParserTemplates;

    public async Task<PurchaseInvoiceImportBatchDto> CreateUploadAsync(HttpContext context, IFormFile file, Guid companyId, Guid storeGroupId, Guid storeId, string? pastedText, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Upload a supplier invoice PDF or image file.");
        }
        if (file.Length > MaxUploadBytes)
        {
            throw new InvalidOperationException("Supplier invoice file is too large. Maximum allowed size is 20 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".txt" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only PDF, PNG, JPG, JPEG, WEBP, and TXT supplier invoice files are supported.");
        }

        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == storeId && item.CompanyId == companyId && item.StoreGroupId == storeGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Selected store is outside your access scope.");

        var batchId = Guid.NewGuid();
        var now = DateTime.Now;
        var safeOriginalName = SafeFileName(file.FileName);
        var storageDir = Path.Combine(StorageRoot(), companyId.ToString("N"), now.Year.ToString("0000"), now.Month.ToString("00"), batchId.ToString("N"));
        Directory.CreateDirectory(storageDir);

        var storedFileName = $"original{extension}";
        var storedFilePath = Path.Combine(storageDir, storedFileName);
        await using (var output = File.Create(storedFilePath))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        var sha = await Sha256Async(storedFilePath, cancellationToken);
        var duplicateByFile = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => item.CompanyId == companyId && item.Sha256Hash == sha && item.Id != batchId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var extraction = !string.IsNullOrWhiteSpace(pastedText)
            ? new TextExtractionResult(pastedText.Trim(), "ManualPastedText", "TextProvided", Array.Empty<string>())
            : await ExtractTextAsync(storedFilePath, file.ContentType, extension, cancellationToken);
        var extractedText = extraction.Text;
        var rawTextPath = Path.Combine(storageDir, "extracted-text.txt");
        await File.WriteAllTextAsync(rawTextPath, extractedText, cancellationToken);
        var diagnosticsPath = Path.Combine(storageDir, "ocr-diagnostics.json");
        await File.WriteAllTextAsync(diagnosticsPath, JsonSerializer.Serialize(extraction, JsonOptions), cancellationToken);

        var parserProfile = await FindVendorProfileAsync(companyId, extractedText, null, null, cancellationToken);
        var parsed = ParseInvoiceText(extractedText, parserProfile);
        var parsedLineDiscountTotal = Round(parsed.Lines.Sum(line => line.LineDiscount));
        var remainingHeaderDiscount = parsed.DiscountAmount > 0 && parsedLineDiscountTotal > 0 && Math.Abs(parsed.DiscountAmount - parsedLineDiscountTotal) <= Math.Max(2m, parsed.DiscountAmount * 0.02m)
            ? 0m
            : parsed.DiscountAmount;
        var parserDiagnosticsPath = Path.Combine(storageDir, "parser-line-decisions.json");
        await File.WriteAllTextAsync(parserDiagnosticsPath, JsonSerializer.Serialize(parsed.LineDecisions, JsonOptions), cancellationToken);
        var vendor = await MatchVendorAsync(companyId, parsed.VendorGstin, parsed.VendorName, cancellationToken);
        var duplicateInvoice = await FindDuplicatePurchaseInvoiceAsync(companyId, storeId, vendor?.Id, parsed.VendorGstin, parsed.InvoiceNumber, cancellationToken);

        var batch = new PurchaseInvoiceImportBatch
        {
            Id = batchId,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId,
            Status = parsed.Lines.Count > 0 ? "NeedsReview" : "NeedsReview",
            SourceFileName = safeOriginalName,
            StoredFilePath = storedFilePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? ContentTypeFromExtension(extension) : file.ContentType,
            FileSizeBytes = file.Length,
            Sha256Hash = sha,
            OcrProvider = extraction.Provider,
            OcrStatus = extraction.Status,
            RawTextPath = rawTextPath,
            ConfidenceScore = parsed.ConfidenceScore,
            ParserTemplate = parsed.ParserTemplate,
            ParserTemplateReason = parsed.ParserTemplateReason,
            ImportQaNotes = null,
            VendorId = vendor?.Id,
            VendorNameRaw = parsed.VendorName,
            VendorNameFinal = vendor?.Name ?? parsed.VendorName,
            VendorGstinRaw = parsed.VendorGstin,
            VendorGstinFinal = vendor?.GSTIN ?? parsed.VendorGstin,
            VendorMobileNumber = vendor?.MobileNumber ?? parsed.VendorMobileNumber,
            VendorAddress = vendor?.Address ?? parsed.VendorAddress,
            SupplierInvoiceNumber = parsed.InvoiceNumber,
            SupplierInvoiceDate = parsed.InvoiceDate,
            DueDate = parsed.InvoiceDate?.AddDays(45) ?? DateTime.Today.AddDays(45),
            TaxableAmount = parsed.TaxableAmount,
            CgstAmount = parsed.CgstAmount,
            SgstAmount = parsed.SgstAmount,
            IgstAmount = parsed.IgstAmount,
            FreightAmount = parsed.FreightAmount,
            DiscountAmount = remainingHeaderDiscount,
            RoundOff = parsed.RoundOff,
            BillAmount = parsed.BillAmount,
            DuplicatePurchaseInvoiceId = duplicateInvoice,
            ErrorMessage = BuildInitialErrorMessage(duplicateByFile, extraction.Diagnostics)
        };

        if (!WorkspaceScope.CanWrite(batch, context, out var scopeMessage))
        {
            throw new InvalidOperationException(scopeMessage ?? "Selected company/store is outside your access scope.");
        }

        var importFile = new PurchaseInvoiceImportFile
        {
            Id = Guid.NewGuid(),
            BatchId = batch.Id,
            FileKind = "OriginalUpload",
            OriginalFileName = safeOriginalName,
            StoredFilePath = storedFilePath,
            ContentType = batch.ContentType,
            FileSizeBytes = file.Length,
            Sha256Hash = sha,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };

        db.PurchaseInvoiceImportBatches.Add(batch);
        db.PurchaseInvoiceImportFiles.Add(importFile);
        db.PurchaseInvoiceImportFiles.Add(new PurchaseInvoiceImportFile
        {
            Id = Guid.NewGuid(), BatchId = batch.Id, FileKind = "ExtractedText", OriginalFileName = "extracted-text.txt",
            StoredFilePath = rawTextPath, ContentType = "text/plain", FileSizeBytes = new FileInfo(rawTextPath).Length,
            Sha256Hash = await Sha256Async(rawTextPath, cancellationToken), CompanyId = companyId, StoreGroupId = storeGroupId, StoreId = storeId
        });
        db.PurchaseInvoiceImportFiles.Add(new PurchaseInvoiceImportFile
        {
            Id = Guid.NewGuid(), BatchId = batch.Id, FileKind = "OcrDiagnostics", OriginalFileName = "ocr-diagnostics.json",
            StoredFilePath = diagnosticsPath, ContentType = "application/json", FileSizeBytes = new FileInfo(diagnosticsPath).Length,
            Sha256Hash = await Sha256Async(diagnosticsPath, cancellationToken), CompanyId = companyId, StoreGroupId = storeGroupId, StoreId = storeId
        });
        db.PurchaseInvoiceImportFiles.Add(new PurchaseInvoiceImportFile
        {
            Id = Guid.NewGuid(), BatchId = batch.Id, FileKind = "ParserLineDecisions", OriginalFileName = "parser-line-decisions.json",
            StoredFilePath = parserDiagnosticsPath, ContentType = "application/json", FileSizeBytes = new FileInfo(parserDiagnosticsPath).Length,
            Sha256Hash = await Sha256Async(parserDiagnosticsPath, cancellationToken), CompanyId = companyId, StoreGroupId = storeGroupId, StoreId = storeId
        });

        var lines = new List<PurchaseInvoiceImportLine>();
        var lineNumber = 1;
        foreach (var parsedLine in parsed.Lines)
        {
            var product = await MatchProductAsync(companyId, parsedLine.Barcode, parsedLine.Name, cancellationToken);
            var tax = await MatchTaxAsync(parsedLine.TaxRate, cancellationToken);
            var line = new PurchaseInvoiceImportLine
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                LineNumber = lineNumber++,
                ProductId = product?.Id,
                ProductNameRaw = parsedLine.Name,
                ProductNameFinal = product?.Name ?? parsedLine.Name,
                BarcodeRaw = parsedLine.Barcode,
                BarcodeFinal = product?.Barcode ?? parsedLine.Barcode,
                HsnCode = parsedLine.HsnCode,
                Unit = Unit.Pcs,
                Quantity = parsedLine.Quantity,
                CostPrice = parsedLine.Rate,
                Mrp = parsedLine.Mrp > 0 ? parsedLine.Mrp : parsedLine.Rate,
                UnitDiscount = parsedLine.UnitDiscount,
                LineDiscount = parsedLine.LineDiscount,
                TaxRate = parsedLine.TaxRate,
                GstPriceMode = parsedLine.GstPriceMode,
                TaxId = tax?.Id,
                TaxableAmount = parsedLine.TaxableAmount,
                TaxAmount = parsedLine.TaxAmount,
                LineTotal = parsedLine.LineTotal,
                ConfidenceScore = parsedLine.ConfidenceScore,
                MatchStatus = product is null ? "NewProductDraft" : "MatchedExistingProduct",
                ReviewRequired = product is null || string.IsNullOrWhiteSpace(product.Barcode) || tax is null,
                ReviewMessage = BuildLineReviewMessage(parsedLine, product is null, tax is null),
                ProductCategoryId = product?.ProductCategoryId,
                ProductSubCategoryId = product?.ProductSubCategoryId,
                ProductType = product?.ProductType ?? ProductType.Apparels,
                ProductGroup = product?.ProductGroup ?? ProductGroup.Shirting,
                CompanyId = companyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId
            };
            RecalculateLine(line);
            lines.Add(line);
        }
        await EnsureMissingBarcodesAsync(batch, lines, true, cancellationToken);
        db.PurchaseInvoiceImportLines.AddRange(lines);

        var rawJsonPath = Path.Combine(storageDir, "initial-draft.json");
        batch.RawJsonPath = rawJsonPath;
        await File.WriteAllTextAsync(rawJsonPath, JsonSerializer.Serialize(new { batch, lines }, JsonOptions), cancellationToken);
        db.PurchaseInvoiceImportFiles.Add(new PurchaseInvoiceImportFile
        {
            Id = Guid.NewGuid(), BatchId = batch.Id, FileKind = "InitialDraftJson", OriginalFileName = "initial-draft.json",
            StoredFilePath = rawJsonPath, ContentType = "application/json", FileSizeBytes = new FileInfo(rawJsonPath).Length,
            Sha256Hash = await Sha256Async(rawJsonPath, cancellationToken), CompanyId = companyId, StoreGroupId = storeGroupId, StoreId = storeId
        });

        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, batch.Id, cancellationToken) ?? throw new InvalidOperationException("Draft was created but could not be loaded.");
    }

    public async Task<IReadOnlyList<PurchaseInvoiceImportListItemDto>> ListAsync(HttpContext context, int take, CancellationToken cancellationToken)
    {
        var batches = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
        var ids = batches.Select(item => item.Id).ToArray();
        var counts = await db.PurchaseInvoiceImportLines.AsNoTracking()
            .Where(item => ids.Contains(item.BatchId) && !item.Deleted)
            .GroupBy(item => item.BatchId)
            .Select(group => new { BatchId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.BatchId, item => item.Count, cancellationToken);

        return batches.Select(item => new PurchaseInvoiceImportListItemDto(
            item.Id,
            item.Status,
            item.SourceFileName,
            item.OcrStatus,
            item.VendorNameFinal ?? item.VendorNameRaw,
            item.SupplierInvoiceNumber,
            item.SupplierInvoiceDate,
            item.BillAmount,
            counts.TryGetValue(item.Id, out var count) ? count : 0,
            item.PostedPurchaseInvoiceId,
            item.DuplicatePurchaseInvoiceId,
            item.CreatedAt,
            item.ParserTemplate,
            item.AcceptanceStatus,
            item.CorrectionStatus)).ToList();
    }


    public async Task<PurchaseInvoiceImportAcceptanceSummaryDto> GetAcceptanceSummaryAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var storage = await GetStorageSummaryAsync(context, cancellationToken);
        var batches = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(250)
            .ToListAsync(cancellationToken);
        var batchIds = batches.Select(item => item.Id).ToArray();

        var lineCounts = batchIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await db.PurchaseInvoiceImportLines.AsNoTracking()
                .Where(item => batchIds.Contains(item.BatchId) && !item.Deleted)
                .GroupBy(item => item.BatchId)
                .Select(group => new { BatchId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.BatchId, item => item.Count, cancellationToken);

        var fileBytes = batchIds.Length == 0
            ? new Dictionary<Guid, long>()
            : await db.PurchaseInvoiceImportFiles.AsNoTracking()
                .Where(item => batchIds.Contains(item.BatchId) && !item.Deleted)
                .GroupBy(item => item.BatchId)
                .Select(group => new { BatchId = group.Key, Bytes = group.Sum(file => file.FileSizeBytes) })
                .ToDictionaryAsync(item => item.BatchId, item => item.Bytes, cancellationToken);

        var profileRows = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .ToListAsync(cancellationToken);

        var now = DateTime.Now;
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var postedThisMonth = batches.Where(item => item.PostedAt.HasValue && item.PostedAt.Value >= monthStart).ToList();
        var statusRows = batches
            .GroupBy(item => SafeStatus(item.Status), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ids = group.Select(item => item.Id).ToArray();
                return new PurchaseInvoiceImportAcceptanceStatusDto(
                    group.Key,
                    group.Count(),
                    group.Sum(item => item.BillAmount),
                    ids.Sum(id => lineCounts.TryGetValue(id, out var count) ? count : 0),
                    ids.Sum(id => fileBytes.TryGetValue(id, out var bytes) ? bytes : 0L));
            })
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Status)
            .ToList();

        var checklist = new List<PurchaseInvoiceImportChecklistItemDto>
        {
            new("posted-proof", "Posted proof retention", storage.PostedProofBytes > 0 || storage.PostedBatches == 0 ? "Pass" : "Warn", storage.PostedBatches == 0 ? "No posted supplier imports yet." : $"{storage.PostedBatches} posted imports have protected proof storage."),
            new("ready-drafts", "Ready drafts", storage.Statuses.Any(item => string.Equals(item.Status, "ReadyToPost", StringComparison.OrdinalIgnoreCase)) ? "Info" : "Pass", storage.Statuses.FirstOrDefault(item => string.Equals(item.Status, "ReadyToPost", StringComparison.OrdinalIgnoreCase)) is { } ready ? $"{ready.BatchCount} draft(s) are ready to post." : "No ready drafts waiting."),
            new("needs-review", "Needs review queue", storage.Statuses.Any(item => string.Equals(item.Status, "NeedsReview", StringComparison.OrdinalIgnoreCase)) ? "Warn" : "Pass", storage.Statuses.FirstOrDefault(item => string.Equals(item.Status, "NeedsReview", StringComparison.OrdinalIgnoreCase)) is { } review ? $"{review.BatchCount} draft(s) need correction before posting." : "No review backlog."),
            new("failed-rejected", "Failed/rejected cleanup", storage.FailedOrRejectedBatches > 0 ? "Warn" : "Pass", storage.FailedOrRejectedBatches > 0 ? $"{storage.FailedOrRejectedBatches} failed/rejected import(s) can be cleaned if not needed." : "No failed/rejected backlog."),
            new("learning", "Vendor learning profiles", profileRows.Count > 0 ? "Pass" : "Info", profileRows.Count > 0 ? $"{profileRows.Count} vendor profile(s) are available for repeated layouts." : "No vendor learning has been captured yet."),
            new("parser-template", "Parser template tracking", batches.Any(item => !string.IsNullOrWhiteSpace(item.ParserTemplate)) ? "Pass" : "Info", batches.Any(item => !string.IsNullOrWhiteSpace(item.ParserTemplate)) ? "Recent imports record the parser/template used for audit and future vendor tuning." : "No parser template history yet. Upload/reparse a supplier invoice to capture it."),
            new("real-invoice-acceptance", "Real invoice acceptance", batches.Any(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase)) ? "Pass" : "Info", batches.Any(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase)) ? $"{batches.Count(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase))} import(s) have been marked as real-invoice passed." : "Mark at least one successful real supplier invoice import as Pass before production acceptance."),
            new("posted-correction-safety", "Posted correction safety", batches.Any(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase)) ? "Warn" : "Pass", batches.Any(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase)) ? $"{batches.Count(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase))} posted import(s) require controlled correction." : "No posted import is currently flagged for correction."),
            new("storage", "Storage visibility", storage.TotalFiles > 0 ? "Pass" : "Info", storage.TotalFiles > 0 ? $"{storage.TotalFiles} import proof/audit file(s), {FormatBytes(storage.TotalFileBytes)} total." : "No supplier invoice files uploaded yet.")
        };

        return new PurchaseInvoiceImportAcceptanceSummaryDto(
            storage.TotalBatches,
            storage.PostedBatches,
            storage.Statuses.FirstOrDefault(item => string.Equals(item.Status, "ReadyToPost", StringComparison.OrdinalIgnoreCase))?.BatchCount ?? 0,
            storage.Statuses.FirstOrDefault(item => string.Equals(item.Status, "NeedsReview", StringComparison.OrdinalIgnoreCase))?.BatchCount ?? 0,
            storage.FailedOrRejectedBatches,
            batches.Count(item => item.DuplicatePurchaseInvoiceId.HasValue && string.IsNullOrWhiteSpace(item.DuplicateOverrideReason)),
            batches.Count(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase)),
            batches.Count(item => string.Equals(item.AcceptanceStatus, "Fail", StringComparison.OrdinalIgnoreCase)),
            batches.Count(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase)),
            batches.Count(item => item.PostedAt.HasValue && item.PostedAt.Value.Date == today),
            postedThisMonth.Count,
            postedThisMonth.Sum(item => item.BillAmount),
            profileRows.Count,
            profileRows.Sum(item => ParseAliasCount(item.ProductAliasesJson)),
            profileRows.Sum(item => ParsePatternCount(item.IgnoredLinePatternsJson)),
            storage.TotalFileBytes,
            storage.PostedProofBytes,
            storage.UnpostedFileBytes,
            statusRows,
            checklist,
            batches.Take(20).Select(item => new PurchaseInvoiceImportListItemDto(
                item.Id,
                item.Status,
                item.SourceFileName,
                item.OcrStatus,
                item.VendorNameFinal ?? item.VendorNameRaw,
                item.SupplierInvoiceNumber,
                item.SupplierInvoiceDate,
                item.BillAmount,
                lineCounts.TryGetValue(item.Id, out var lineCount) ? lineCount : 0,
                item.PostedPurchaseInvoiceId,
                item.DuplicatePurchaseInvoiceId,
                item.CreatedAt,
                item.ParserTemplate,
                item.AcceptanceStatus,
                item.CorrectionStatus)).ToList());
    }

    public async Task<PurchaseInvoiceImportPostingReportDto?> GetPostingReportAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null) return null;

        var lines = await db.PurchaseInvoiceImportLines.AsNoTracking()
            .Where(item => item.BatchId == id && !item.Deleted)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);
        var warnings = ValidateBatch(batch, lines);
        return BuildPostingReport(batch, lines, warnings);
    }




    public async Task<PurchaseInvoiceImportBatchDto?> UpdateAcceptanceAsync(HttpContext context, Guid id, PurchaseInvoiceImportAcceptanceUpdateRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null) return null;

        var status = NormalizeAcceptanceStatus(request.Status);
        batch.AcceptanceStatus = status;
        batch.AcceptanceNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        batch.AcceptanceTestedAt = DateTime.Now;
        batch.AcceptanceTestedBy = CurrentUserName(context);
        batch.UpdatedAt = DateTime.Now;

        if (status == "Fail" && string.IsNullOrWhiteSpace(batch.CorrectionStatus))
        {
            batch.CorrectionStatus = batch.PostedPurchaseInvoiceId.HasValue ? "CorrectionRequired" : "DraftNeedsCorrection";
            batch.CorrectionNotes = batch.AcceptanceNotes;
            batch.CorrectionRequestedAt = DateTime.Now;
            batch.CorrectionRequestedBy = batch.AcceptanceTestedBy;
        }
        else if (status == "Pass" && !batch.PostedPurchaseInvoiceId.HasValue && string.Equals(batch.CorrectionStatus, "DraftNeedsCorrection", StringComparison.OrdinalIgnoreCase))
        {
            batch.CorrectionStatus = null;
            batch.CorrectionNotes = null;
            batch.CorrectionRequestedAt = null;
            batch.CorrectionRequestedBy = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }

    public async Task<PurchaseInvoiceImportCorrectionSafetyDto?> GetCorrectionSafetyAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null) return null;
        return BuildCorrectionSafety(batch);
    }


    public async Task<PurchaseInvoiceImportCorrectionPlanDto?> GetCorrectionPlanAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null) return null;

        var isPosted = batch.PostedPurchaseInvoiceId.HasValue || string.Equals(batch.Status, "Posted", StringComparison.OrdinalIgnoreCase);
        var checklist = new List<PurchaseInvoiceImportChecklistItemDto>
        {
            new("proof-protected", "Keep supplier proof", "Pass", "Original uploaded supplier invoice and audit files must stay protected for audit."),
            new("posted-inward", "Posted purchase inward", isPosted ? "Pass" : "Info", isPosted ? $"Posted purchase invoice id: {batch.PostedPurchaseInvoiceId}" : "Draft is not posted yet; correct the draft and re-run posting report."),
            new("direct-delete", "Direct delete/undo", isPosted ? "Block" : "Pass", isPosted ? "Posted imports must not be deleted or directly undone because stock, vendor balance and accounting have already been posted." : "Unposted drafts can be corrected, rejected or deleted."),
            new("revision-path", "Revision path", isPosted ? "Warn" : "Info", isPosted ? "Use purchase inward revision/return/reversal flow with owner/admin approval; then keep this import flagged as Correction Required." : "Open the draft, correct OCR/vendor/items/tax and save again."),
            new("acceptance", "Real-invoice acceptance", string.Equals(batch.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase) ? "Pass" : "Warn", string.IsNullOrWhiteSpace(batch.AcceptanceStatus) ? "This import is not yet accepted against the real supplier invoice." : $"Current acceptance status: {batch.AcceptanceStatus}.")
        };

        var steps = isPosted
            ? new[]
            {
                "Open Purchase → Purchase Invoices and locate the posted inward linked to this import.",
                "Open the original supplier proof and compare vendor, invoice number/date, items, discounts, GST and total.",
                "If stock/accounting is wrong, create a controlled purchase inward revision/return/reversal from the purchase module; do not delete the import proof.",
                "After correction, mark this import as Pass only when stock, vendor balance, accounting and proof all match."
            }
            : new[]
            {
                "Open the supplier import draft and fix parser/vendor/product/tax/discount issues.",
                "Run the posting report until all blocking checklist items pass.",
                "Post inward only after the scanned total and calculated total match within round-off tolerance.",
                "After posting, mark the real invoice test as Pass from Import Acceptance."
            };

        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(batch.CorrectionNotes)) warnings.Add(batch.CorrectionNotes);
        if (isPosted) warnings.Add("Direct undo is intentionally unavailable for posted imports; reversal must keep stock ledger, vendor balance and accounting consistent.");

        return new PurchaseInvoiceImportCorrectionPlanDto(
            batch.Id,
            batch.PostedPurchaseInvoiceId,
            SafeStatus(batch.Status),
            string.IsNullOrWhiteSpace(batch.CorrectionStatus) ? "None" : batch.CorrectionStatus!,
            isPosted,
            isPosted ? "Controlled purchase inward revision/return/reversal with proof retained." : "Correct draft and repost after checklist passes.",
            checklist,
            steps,
            warnings);
    }

    public async Task<PurchaseInvoiceImportBackupRestoreChecklistDto> GetBackupRestoreChecklistAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var storage = await GetStorageSummaryAsync(context, cancellationToken);
        var storageRoot = StorageRoot();
        var dockerVolume = "${COMPOSE_PROJECT_NAME:-garmetix}_garmetix_app_data";
        var checklist = new List<PurchaseInvoiceImportChecklistItemDto>
        {
            new("database-backup", "Database dump", "Pass", "Use scripts/linux/create-database-backup-now.sh or the Maintenance backup flow to create a PostgreSQL dump."),
            new("proof-archive", "Supplier proof archive", storage.TotalFiles > 0 ? "Pass" : "Info", storage.TotalFiles > 0 ? $"{storage.TotalFiles} supplier import proof/audit file(s) are tracked; backup must include purchase-imports." : "No supplier import proof files exist yet."),
            new("posted-proof", "Posted proof protection", storage.PostedProofBytes > 0 || storage.PostedBatches == 0 ? "Pass" : "Warn", storage.PostedBatches == 0 ? "No posted imports yet." : $"Posted proof storage: {FormatBytes(storage.PostedProofBytes)}."),
            new("restore-drill", "Restore drill", "Warn", "Run restore drill on a disposable database and verify at least one posted import proof opens after restore."),
            new("storage-path", "Storage root", Directory.Exists(storageRoot) || storage.TotalFiles == 0 ? "Pass" : "Warn", $"Configured PurchaseImport storage root: {storageRoot}")
        };

        var backupCommands = new List<string>
        {
            "./scripts/linux/create-database-backup-now.sh .env.production",
            "ls -lh backups/*purchase-import-proofs.tar.gz",
            "sha256sum -c backups/*.purchase-import-proofs.tar.gz.sha256"
        };

        var restoreSteps = new List<string>
        {
            "Restore the PostgreSQL dump into a disposable database or fresh server.",
            "Restore purchase-imports proof archive into the app data volume under /app/data/purchase-imports.",
            "Open Purchase → Purchase Invoices, choose an import-posted inward, and click Proof.",
            "Confirm original PDF/image, extracted text, parser decisions and final-posted snapshot are present.",
            "Run Purchase → Import Acceptance and confirm posted proof storage is still protected."
        };

        return new PurchaseInvoiceImportBackupRestoreChecklistDto(
            storageRoot,
            dockerVolume,
            storage.TotalBatches,
            storage.PostedBatches,
            storage.TotalFiles,
            storage.TotalFileBytes,
            storage.PostedProofBytes,
            storage.UnpostedFileBytes,
            storage.PostedProofBytes > 0,
            checklist,
            backupCommands,
            restoreSteps);
    }

    public async Task<PurchaseInvoiceImportParserQaSummaryDto> GetParserQaSummaryAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var batches = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var profiles = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .ToListAsync(cancellationToken);

        var templateStats = batches
            .GroupBy(item => NormalizeParserTemplateKey(item.ParserTemplate), StringComparer.OrdinalIgnoreCase)
            .Select(group => new PurchaseInvoiceImportParserTemplateStatDto(
                group.Key,
                group.Count(),
                group.Count(item => item.PostedPurchaseInvoiceId.HasValue || string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase)),
                group.Count(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase)),
                group.Count(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase)),
                group.Sum(item => item.BillAmount),
                group.Max(item => item.CreatedAt)))
            .OrderByDescending(item => item.BatchCount)
            .ThenBy(item => item.ParserTemplate)
            .ToList();

        var vendorStats = batches
            .Where(item => !string.IsNullOrWhiteSpace(item.VendorNameFinal ?? item.VendorNameRaw ?? item.VendorGstinFinal ?? item.VendorGstinRaw))
            .GroupBy(item => new
            {
                Name = item.VendorNameFinal ?? item.VendorNameRaw,
                Gstin = item.VendorGstinFinal ?? item.VendorGstinRaw
            })
            .Select(group =>
            {
                var profile = profiles.FirstOrDefault(item =>
                    (!string.IsNullOrWhiteSpace(group.Key.Gstin) && string.Equals(item.VendorGstin, group.Key.Gstin, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(group.Key.Name) && string.Equals(item.VendorName, group.Key.Name, StringComparison.OrdinalIgnoreCase)));
                return new PurchaseInvoiceImportVendorParserSummaryDto(
                    group.Key.Name,
                    group.Key.Gstin,
                    NormalizeParserTemplateKey(profile?.PreferredParserTemplate),
                    group.Count(),
                    group.Count(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase)),
                    group.Count(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase)),
                    group.Max(item => item.CreatedAt));
            })
            .OrderByDescending(item => item.BatchCount)
            .ThenBy(item => item.VendorName)
            .Take(50)
            .ToList();

        var posted = batches.Count(item => item.PostedPurchaseInvoiceId.HasValue || string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase));
        var pass = batches.Count(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase));
        var correctionRequired = batches.Count(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase));
        var untestedPosted = batches.Count(item => (item.PostedPurchaseInvoiceId.HasValue || string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase)) && !string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase));
        var checklist = new List<PurchaseInvoiceImportChecklistItemDto>
        {
            new("template-coverage", "Parser template coverage", templateStats.Count > 1 ? "Pass" : "Info", templateStats.Count > 1 ? $"{templateStats.Count} parser templates have real import history." : "Upload/test more vendor layouts to build parser coverage."),
            new("accepted-real-invoices", "Accepted real invoices", pass > 0 ? "Pass" : "Warn", pass > 0 ? $"{pass} import(s) are marked Pass." : "Mark at least one real invoice as Pass before production acceptance."),
            new("untested-posted", "Untested posted imports", untestedPosted > 0 ? "Warn" : "Pass", untestedPosted > 0 ? $"{untestedPosted} posted import(s) still need real-invoice acceptance." : "All posted imports in the recent sample are accepted or no posted imports exist."),
            new("corrections", "Correction queue", correctionRequired > 0 ? "Warn" : "Pass", correctionRequired > 0 ? $"{correctionRequired} posted import(s) require controlled correction." : "No posted import correction queue."),
            new("tally-cgst-sgst", "Tally CGST/SGST + MRP QA", "Info", "Test at least one Tally Prime invoice with CGST/SGST, MRP column, discount and freight before closing purchase import.")
        };

        return new PurchaseInvoiceImportParserQaSummaryDto(
            batches.Count,
            posted,
            pass,
            untestedPosted,
            correctionRequired,
            templateStats,
            vendorStats,
            checklist);
    }


    public async Task<PurchaseInvoiceImportFinalClosureStatusDto> GetFinalClosureStatusAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var storage = await GetStorageSummaryAsync(context, cancellationToken);
        var batches = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var postedBatches = batches.Count(item => item.PostedPurchaseInvoiceId.HasValue || string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase));
        var acceptedPassBatches = batches.Count(item => string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase));
        var untestedPostedBatches = batches.Count(item => (item.PostedPurchaseInvoiceId.HasValue || string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase)) && !string.Equals(item.AcceptanceStatus, "Pass", StringComparison.OrdinalIgnoreCase));
        var correctionRequiredBatches = batches.Count(item => string.Equals(item.CorrectionStatus, "CorrectionRequired", StringComparison.OrdinalIgnoreCase));
        var openDraftBatches = batches.Count(item =>
            !item.PostedPurchaseInvoiceId.HasValue &&
            !string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Status, "Rejected", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase));
        var needsReviewBatches = batches.Count(item => string.Equals(item.Status, "NeedsReview", StringComparison.OrdinalIgnoreCase));
        var readyToPostBatches = batches.Count(item => string.Equals(item.Status, "ReadyToPost", StringComparison.OrdinalIgnoreCase));
        var failedOrRejectedBatches = storage.FailedOrRejectedBatches;
        var hasParserHistory = batches.Any(item => !string.IsNullOrWhiteSpace(item.ParserTemplate));
        var hasAnyBatch = batches.Count > 0;
        var hasBackupCoverage = storage.TotalFiles == 0 || Directory.Exists(StorageRoot()) || storage.TotalFileBytes > 0;
        var hasPostedProofProtection = postedBatches == 0 || storage.PostedProofBytes > 0;

        var closeoutChecklist = new List<PurchaseInvoiceImportChecklistItemDto>
        {
            new("feature-scope", "Feature scope closed", "Pass", "Upload, OCR/text extraction, parser templates, review, product matching, posting report, posting, audit export, parser QA, correction plan and backup checklist are present."),
            new("real-invoice-pass", "Real invoice Pass evidence", acceptedPassBatches > 0 ? "Pass" : "Warn", acceptedPassBatches > 0 ? $"{acceptedPassBatches} import(s) are marked Pass." : "Mark at least one real supplier invoice import as Pass before calling the module operationally complete."),
            new("posted-untested", "Posted imports tested", untestedPostedBatches == 0 ? "Pass" : "Warn", untestedPostedBatches == 0 ? "No posted import is waiting for real-invoice acceptance in the recent sample." : $"{untestedPostedBatches} posted import(s) still need Pass/Fail acceptance."),
            new("correction-queue", "Correction queue clear", correctionRequiredBatches == 0 ? "Pass" : "Block", correctionRequiredBatches == 0 ? "No posted import is flagged Correction Required." : $"{correctionRequiredBatches} posted import(s) require controlled purchase correction."),
            new("draft-queue", "Draft queue visible", needsReviewBatches == 0 ? "Pass" : "Warn", needsReviewBatches == 0 ? "No NeedsReview draft is blocking closure." : $"{needsReviewBatches} draft(s) still need parser/vendor/item/tax correction."),
            new("ready-drafts", "Ready drafts decision", readyToPostBatches == 0 ? "Pass" : "Info", readyToPostBatches == 0 ? "No ReadyToPost draft is waiting for posting." : $"{readyToPostBatches} ready draft(s) can be posted or left as open operational work."),
            new("failed-cleanup", "Failed/rejected cleanup", failedOrRejectedBatches == 0 ? "Pass" : "Info", failedOrRejectedBatches == 0 ? "No failed/rejected import backlog." : $"{failedOrRejectedBatches} failed/rejected import(s) remain for optional cleanup."),
            new("parser-history", "Parser QA history", hasParserHistory || !hasAnyBatch ? "Pass" : "Warn", hasParserHistory ? "Parser template is being recorded on import batches." : hasAnyBatch ? "Existing batches do not show parser template history; reparse/new imports will record it." : "No import batches yet; parser history starts after first upload."),
            new("proof-storage", "Proof storage protected", hasPostedProofProtection ? "Pass" : "Block", hasPostedProofProtection ? "Posted supplier proof storage is protected or no posted imports exist yet." : "Posted import proof files are missing from tracked storage; verify app-data volume before closure."),
            new("backup-restore", "Backup/restore checklist available", hasBackupCoverage ? "Pass" : "Warn", hasBackupCoverage ? "Backup commands and restore verification steps are available in Import Acceptance." : "Purchase-import storage root could not be verified; inspect mounted app-data volume."),
            new("known-limitations", "Known limitations shown", "Pass", $"{FinalClosureKnownLimitations.Count} limitation(s) are shown to the operator before moving outside purchase import."),
            new("outside-module", "Next outside module selected", "Info", "Use the recommended next module card to move out of Purchase Import without losing remaining operational QA work.")
        };

        var hasBlocker = closeoutChecklist.Any(item => string.Equals(item.Status, "Block", StringComparison.OrdinalIgnoreCase));
        var hasWarning = closeoutChecklist.Any(item => string.Equals(item.Status, "Warn", StringComparison.OrdinalIgnoreCase));
        var moduleStatus = hasBlocker || hasWarning ? "Not Complete" : "Complete";
        var isComplete = !hasBlocker && !hasWarning;
        var completionMode = hasBlocker
            ? "Blocked by posted correction/proof issue"
            : hasWarning
                ? "Feature complete; finish listed real-data QA before production sign-off"
                : "Feature and visible operational closure checklist complete";

        return new PurchaseInvoiceImportFinalClosureStatusDto(
            moduleStatus,
            isComplete,
            completionMode,
            batches.Count,
            postedBatches,
            openDraftBatches,
            untestedPostedBatches,
            correctionRequiredBatches,
            acceptedPassBatches,
            DateTime.Now,
            closeoutChecklist,
            FinalClosureKnownLimitations,
            FinalClosureOperatorRules,
            FinalClosureNextModuleCandidates,
            "Vyapar Sale Import final summary/reconciliation or Attendance-Payroll real-month validation, depending on which live-data QA is urgent first.");
    }

    public async Task<PurchaseInvoiceImportCorrectionSafetyDto?> RequestCorrectionAsync(HttpContext context, Guid id, PurchaseInvoiceImportCorrectionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new InvalidOperationException("Correction reason is required.");
        }

        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null) return null;

        var reason = request.Reason.Trim();
        var action = string.IsNullOrWhiteSpace(request.RequestedAction) ? null : request.RequestedAction.Trim();
        batch.CorrectionStatus = batch.PostedPurchaseInvoiceId.HasValue ? "CorrectionRequired" : "DraftNeedsCorrection";
        batch.CorrectionNotes = action is null ? reason : $"{reason} | Requested action: {action}";
        batch.CorrectionRequestedAt = DateTime.Now;
        batch.CorrectionRequestedBy = CurrentUserName(context);
        batch.AcceptanceStatus = "Fail";
        batch.AcceptanceNotes = reason;
        batch.AcceptanceTestedAt = DateTime.Now;
        batch.AcceptanceTestedBy = batch.CorrectionRequestedBy;
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return BuildCorrectionSafety(batch);
    }



    public async Task<(byte[] Bytes, string FileName, string ContentType)?> ExportBatchAuditJsonAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await GetBatchAsync(context, id, cancellationToken);
        if (batch is null) return null;

        var report = await GetPostingReportAsync(context, id, cancellationToken);
        var files = await ListFilesAsync(context, id, cancellationToken) ?? Array.Empty<PurchaseInvoiceImportFileDto>();
        var export = new
        {
            exportedAt = DateTime.Now,
            exportType = "PurchaseImportAudit",
            batch,
            postingReport = report,
            files
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, JsonOptions);
        return (bytes, $"purchase-import-audit-{SafeFileName(batch.SupplierInvoiceNumber ?? batch.Id.ToString())}.json", "application/json");
    }

    public async Task<(byte[] Bytes, string FileName, string ContentType)?> ExportBatchLinesCsvAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null) return null;

        var lines = await db.PurchaseInvoiceImportLines.AsNoTracking()
            .Where(item => item.BatchId == id && !item.Deleted)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("Line,Ignored,MatchStatus,ProductName,Barcode,HSN,Unit,Quantity,MRP,CostPrice,UnitDiscount,LineDiscount,TaxRate,GstPriceMode,TaxableAmount,TaxAmount,LineTotal,ReviewRequired,ReviewMessage");
        foreach (var line in lines)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(line.LineNumber),
                Csv(line.Ignored),
                Csv(line.MatchStatus),
                Csv(line.ProductNameFinal ?? line.ProductNameRaw),
                Csv(line.BarcodeFinal ?? line.BarcodeRaw),
                Csv(line.HsnCode),
                Csv(line.Unit),
                Csv(line.Quantity),
                Csv(line.Mrp),
                Csv(line.CostPrice),
                Csv(line.UnitDiscount),
                Csv(line.LineDiscount),
                Csv(line.TaxRate),
                Csv(line.GstPriceMode),
                Csv(line.TaxableAmount),
                Csv(line.TaxAmount),
                Csv(line.LineTotal),
                Csv(line.ReviewRequired),
                Csv(line.ReviewMessage)
            }));
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return (bytes, $"purchase-import-lines-{SafeFileName(batch.SupplierInvoiceNumber ?? batch.Id.ToString())}.csv", "text/csv");
    }

    public async Task<(byte[] Bytes, string FileName, string ContentType)> ExportVendorProfilesJsonAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var profiles = await ListVendorProfilesAsync(context, 500, cancellationToken);
        var export = new
        {
            exportedAt = DateTime.Now,
            exportType = "PurchaseImportVendorLearningProfiles",
            profiles
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, JsonOptions);
        return (bytes, $"purchase-import-vendor-learning-{DateTime.Now:yyyyMMdd-HHmmss}.json", "application/json");
    }

    public async Task<PurchaseInvoiceImportStorageSummaryDto> GetStorageSummaryAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var batches = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .ToListAsync(cancellationToken);

        var batchIds = batches.Select(item => item.Id).ToArray();
        var lineCounts = batchIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await db.PurchaseInvoiceImportLines.AsNoTracking()
                .Where(item => batchIds.Contains(item.BatchId) && !item.Deleted)
                .GroupBy(item => item.BatchId)
                .Select(group => new { BatchId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.BatchId, item => item.Count, cancellationToken);

        var fileStats = batchIds.Length == 0
            ? new Dictionary<Guid, BatchFileStorageStats>()
            : await db.PurchaseInvoiceImportFiles.AsNoTracking()
                .Where(item => batchIds.Contains(item.BatchId) && !item.Deleted)
                .GroupBy(item => item.BatchId)
                .Select(group => new BatchFileStorageStats(group.Key, group.Count(), group.Sum(file => file.FileSizeBytes)))
                .ToDictionaryAsync(item => item.BatchId, item => item, cancellationToken);

        var statusRows = batches
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Status) ? "Unknown" : item.Status.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var groupIds = group.Select(item => item.Id).ToArray();
                var lines = groupIds.Sum(id => lineCounts.TryGetValue(id, out var count) ? count : 0);
                var files = groupIds.Sum(id => fileStats.TryGetValue(id, out var stats) ? stats.FileCount : 0);
                var bytes = groupIds.Sum(id => fileStats.TryGetValue(id, out var stats) ? stats.FileBytes : 0L);
                return new PurchaseInvoiceImportStorageStatusDto(
                    group.Key,
                    group.Count(),
                    lines,
                    files,
                    bytes,
                    group.Sum(item => item.BillAmount));
            })
            .OrderByDescending(item => item.BatchCount)
            .ThenBy(item => item.Status)
            .ToList();

        var postedIds = batches.Where(item => item.PostedPurchaseInvoiceId.HasValue || string.Equals(item.Status, "Posted", StringComparison.OrdinalIgnoreCase)).Select(item => item.Id).ToHashSet();
        var unpostedIds = batches.Where(item => !postedIds.Contains(item.Id)).Select(item => item.Id).ToHashSet();

        return new PurchaseInvoiceImportStorageSummaryDto(
            batches.Count,
            postedIds.Count,
            unpostedIds.Count,
            batches.Count(item => string.Equals(item.Status, "Failed", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Status, "Rejected", StringComparison.OrdinalIgnoreCase)),
            lineCounts.Values.Sum(),
            fileStats.Values.Sum(item => item.FileCount),
            fileStats.Values.Sum(item => item.FileBytes),
            postedIds.Sum(id => fileStats.TryGetValue(id, out var stats) ? stats.FileBytes : 0L),
            unpostedIds.Sum(id => fileStats.TryGetValue(id, out var stats) ? stats.FileBytes : 0L),
            batches.Where(item => unpostedIds.Contains(item.Id)).OrderBy(item => item.CreatedAt).Select(item => (DateTime?)item.CreatedAt).FirstOrDefault(),
            batches.OrderByDescending(item => item.CreatedAt).Select(item => (DateTime?)item.CreatedAt).FirstOrDefault(),
            statusRows);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceImportVendorProfileDto>> ListVendorProfilesAsync(HttpContext context, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 200);
        var profiles = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.LastLearnedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return profiles.Select(ToVendorProfileDto).ToList();
    }

    public async Task<PurchaseInvoiceImportVendorProfileDto?> GetVendorProfileAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var profile = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        return profile is null ? null : ToVendorProfileDto(profile);
    }

    public async Task<PurchaseInvoiceImportVendorProfileDto?> ResetVendorProfileAsync(HttpContext context, Guid id, PurchaseInvoiceImportVendorProfileResetRequest request, CancellationToken cancellationToken)
    {
        var profile = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        if (request.ResetIgnoredLinePatterns)
        {
            profile.IgnoredLinePatternsJson = JsonSerializer.Serialize(new List<string>(), JsonOptions);
        }

        if (request.ResetProductAliases)
        {
            profile.ProductAliasesJson = JsonSerializer.Serialize(new List<LearnedProductAlias>(), JsonOptions);
        }

        profile.LearningNotes = "Learning reset manually from vendor invoice profile settings.";
        profile.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return ToVendorProfileDto(profile);
    }

    public async Task<PurchaseInvoiceImportVendorProfileDto?> UpdateVendorProfileRulesAsync(HttpContext context, Guid id, PurchaseInvoiceImportVendorProfileRulesRequest request, CancellationToken cancellationToken)
    {
        var profile = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var ignoredPatterns = ReadJsonList<string>(profile.IgnoredLinePatternsJson)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var pattern in request.RemoveIgnoredLinePatterns ?? Array.Empty<string>())
        {
            var signature = LearningSignature(pattern);
            ignoredPatterns.RemoveAll(item =>
                string.Equals(item, pattern?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item, signature, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var pattern in request.AddIgnoredLinePatterns ?? Array.Empty<string>())
        {
            var signature = LearningSignature(pattern);
            if (!string.IsNullOrWhiteSpace(signature) && !ignoredPatterns.Contains(signature, StringComparer.OrdinalIgnoreCase))
            {
                ignoredPatterns.Add(signature);
            }
        }

        var aliases = ReadJsonList<LearnedProductAlias>(profile.ProductAliasesJson)
            .Where(item => !string.IsNullOrWhiteSpace(item.RawSignature))
            .GroupBy(item => item.RawSignature, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LearnedAt).First())
            .ToList();

        foreach (var alias in request.RemoveProductAliases ?? Array.Empty<string>())
        {
            var signature = LearningSignature(alias);
            aliases.RemoveAll(item =>
                string.Equals(item.RawSignature, alias?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.RawSignature, signature, StringComparison.OrdinalIgnoreCase));
        }

        profile.IgnoredLinePatternsJson = JsonSerializer.Serialize(ignoredPatterns.OrderBy(item => item).Take(300).ToList(), JsonOptions);
        profile.ProductAliasesJson = JsonSerializer.Serialize(aliases.OrderByDescending(item => item.LearnedAt).Take(500).ToList(), JsonOptions);
        if (request.PreferredParserTemplate is not null)
        {
            profile.PreferredParserTemplate = NormalizeParserTemplateKey(request.PreferredParserTemplate);
        }
        if (request.LearningNotes is not null)
        {
            profile.LearningNotes = string.IsNullOrWhiteSpace(request.LearningNotes) ? null : request.LearningNotes.Trim();
        }
        else
        {
            profile.LearningNotes = "Learning rules were updated manually from vendor invoice profile settings.";
        }
        profile.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return ToVendorProfileDto(profile);
    }

    public async Task<bool> DeleteVendorProfileAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var profile = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportVendorProfiles, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        profile.Deleted = true;
        profile.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PurchaseInvoiceImportBatchDto?> GetBatchAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }

        var lines = await db.PurchaseInvoiceImportLines.AsNoTracking()
            .Where(item => item.BatchId == id && !item.Deleted)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);

        var warnings = ValidateBatch(batch, lines);
        return ToDto(batch, lines, warnings);
    }

    public async Task<PurchaseInvoiceImportBatchDto?> UpdateBatchAsync(HttpContext context, Guid id, PurchaseInvoiceImportUpdateRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be edited.");
        }

        batch.VendorId = request.VendorId;
        batch.VendorNameFinal = request.VendorNameFinal?.Trim();
        batch.VendorGstinFinal = GstinLookupService.NormalizeGstin(request.VendorGstinFinal);
        batch.VendorMobileNumber = request.VendorMobileNumber?.Trim();
        batch.VendorAddress = request.VendorAddress?.Trim();
        batch.SupplierInvoiceNumber = request.SupplierInvoiceNumber?.Trim();
        batch.SupplierInvoiceDate = request.SupplierInvoiceDate;
        batch.DueDate = request.DueDate;
        batch.FreightAmount = Round(request.FreightAmount);
        batch.DiscountAmount = Round(request.DiscountAmount);
        batch.RoundOff = Round(request.RoundOff);
        batch.BillAmount = Round(request.BillAmount);
        batch.PaidAmount = Round(request.PaidAmount);
        batch.PaymentMode = request.PaymentMode;
        batch.BankAccountId = request.BankAccountId;
        batch.ImportQaNotes = string.IsNullOrWhiteSpace(request.ImportQaNotes) ? null : request.ImportQaNotes.Trim();
        await RefreshDuplicateWarningAsync(batch, cancellationToken);
        batch.UpdatedAt = DateTime.Now;

        var existingLines = await db.PurchaseInvoiceImportLines
            .Where(item => item.BatchId == id)
            .ToListAsync(cancellationToken);
        foreach (var line in existingLines)
        {
            line.Deleted = true;
            line.UpdatedAt = DateTime.Now;
        }

        var updatedLines = new List<PurchaseInvoiceImportLine>();
        foreach (var lineRequest in request.Lines.OrderBy(item => item.LineNumber))
        {
            var line = existingLines.FirstOrDefault(item => lineRequest.Id.HasValue && item.Id == lineRequest.Id.Value) ?? new PurchaseInvoiceImportLine
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                CompanyId = batch.CompanyId,
                StoreGroupId = batch.StoreGroupId,
                StoreId = batch.StoreId
            };
            if (line.Id != Guid.Empty && !existingLines.Contains(line))
            {
                db.PurchaseInvoiceImportLines.Add(line);
            }
            var previousReviewMessage = line.ReviewMessage;
            line.Deleted = false;
            line.LineNumber = lineRequest.LineNumber;
            line.ProductId = lineRequest.ProductId;
            line.ProductNameFinal = lineRequest.ProductNameFinal?.Trim();
            line.BarcodeFinal = lineRequest.BarcodeFinal?.Trim();
            line.HsnCode = lineRequest.HsnCode?.Trim();
            line.Unit = lineRequest.Unit;
            line.Quantity = Round(lineRequest.Quantity);
            line.Mrp = Round(lineRequest.Mrp);
            line.CostPrice = Round(lineRequest.CostPrice);
            line.UnitDiscount = Round(lineRequest.UnitDiscount);
            line.LineDiscount = Round(lineRequest.LineDiscount);
            line.TaxRate = Round(lineRequest.TaxRate);
            line.GstPriceMode = NormalizeGstPriceMode(lineRequest.GstPriceMode);
            line.TaxId = lineRequest.TaxId;
            line.ProductCategoryId = lineRequest.ProductCategoryId;
            line.ProductSubCategoryId = lineRequest.ProductSubCategoryId;
            line.ProductType = lineRequest.ProductType;
            line.ProductGroup = lineRequest.ProductGroup;
            line.Ignored = lineRequest.Ignored;
            RecalculateLine(line);
            line.MatchStatus = line.ProductId.HasValue ? "MatchedExistingProduct" : "NewProductDraft";
            line.ReviewRequired = !line.Ignored && (line.Quantity <= 0 || string.IsNullOrWhiteSpace(line.ProductNameFinal) || string.IsNullOrWhiteSpace(line.BarcodeFinal) || !line.TaxId.HasValue);
            line.ReviewMessage = BuildSavedLineReviewMessage(previousReviewMessage, line.ReviewRequired);
            line.UpdatedAt = DateTime.Now;
            updatedLines.Add(line);
        }

        await EnsureMissingBarcodesAsync(batch, updatedLines, true, cancellationToken);
        RecalculateBatchTotals(batch, updatedLines.Where(item => !item.Deleted && !item.Ignored).ToList());
        var lines = updatedLines.Where(item => !item.Deleted).OrderBy(item => item.LineNumber).ToList();
        var warnings = ValidateBatch(batch, lines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        await LearnFromCorrectionsAsync(batch, lines, false, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var snapshotPath = CorrectedDraftPath(batch);
        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(new { batch, lines }, JsonOptions), cancellationToken);
        await UpsertImportFileAsync(batch, "CorrectedDraftJson", "corrected-draft.json", snapshotPath, "application/json", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }

    public async Task<PurchaseInvoiceImportPostResponse?> PostBatchAsync(HttpContext context, Guid id, PurchaseInvoiceImportPostRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("This supplier invoice import draft is already posted.");
        }

        var lines = await db.PurchaseInvoiceImportLines
            .Where(item => item.BatchId == id && !item.Deleted && !item.Ignored)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);
        await EnsureMissingBarcodesAsync(batch, lines, true, cancellationToken);
        var warnings = ValidateBatch(batch, lines);
        if (warnings.Count > 0)
        {
            batch.Status = "NeedsReview";
            batch.ErrorMessage = string.Join(" | ", warnings);
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Draft cannot be posted: " + batch.ErrorMessage);
        }

        var inward = request.Inward ?? await BuildInwardRequestFromBatchAsync(batch, lines, cancellationToken);
        var response = await PostPurchaseInwardAsync(context, inward, cancellationToken);
        batch.PostedPurchaseInvoiceId = response.PurchaseInvoiceId;
        batch.Status = "Posted";
        batch.PostedAt = DateTime.Now;
        batch.VerifiedBy = context.User.Identity?.Name ?? context.User.FindFirst("userName")?.Value ?? context.User.FindFirst("email")?.Value;
        batch.ErrorMessage = null;
        batch.UpdatedAt = DateTime.Now;
        var finalSnapshotPath = Path.Combine(Path.GetDirectoryName(batch.StoredFilePath) ?? StorageRoot(), "final-posted-snapshot.json");
        await File.WriteAllTextAsync(finalSnapshotPath, JsonSerializer.Serialize(new { batch, lines, response }, JsonOptions), cancellationToken);
        await UpsertImportFileAsync(batch, "FinalPostedSnapshot", "final-posted-snapshot.json", finalSnapshotPath, "application/json", cancellationToken);
        await LearnFromCorrectionsAsync(batch, lines, true, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new PurchaseInvoiceImportPostResponse(batch.Id, response.PurchaseInvoiceId, response.InvoiceNumber, response.InwardNumber, response.BillAmount, response.ItemCount, batch.Status);
    }

    public async Task<bool> RejectAsync(HttpContext context, Guid id, string? reason, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return false;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted drafts cannot be rejected.");
        }
        batch.Status = "Rejected";
        batch.RejectedAt = DateTime.Now;
        batch.ErrorMessage = string.IsNullOrWhiteSpace(reason) ? "Rejected by user." : reason.Trim();
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(string Path, string ContentType, string DownloadName)?> GetOriginalFileAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return OriginalFileResult(batch);
    }

    public async Task<(string Text, string DownloadName)?> GetExtractedTextAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null || string.IsNullOrWhiteSpace(batch.RawTextPath) || !File.Exists(batch.RawTextPath))
        {
            return null;
        }
        var text = await File.ReadAllTextAsync(batch.RawTextPath, cancellationToken);
        return (text, $"{Path.GetFileNameWithoutExtension(batch.SourceFileName)}-extracted-text.txt");
    }

    public async Task<(string Path, string ContentType, string DownloadName)?> GetOriginalFileForPurchaseInvoiceAsync(HttpContext context, Guid purchaseInvoiceId, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => item.PostedPurchaseInvoiceId == purchaseInvoiceId)
            .OrderByDescending(item => item.PostedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return OriginalFileResult(batch);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceImportFileDto>?> ListFilesAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batchExists = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .AnyAsync(item => item.Id == id, cancellationToken);
        if (!batchExists)
        {
            return null;
        }

        return await db.PurchaseInvoiceImportFiles.AsNoTracking()
            .Where(item => item.BatchId == id && !item.Deleted)
            .OrderBy(item => item.FileKind)
            .ThenBy(item => item.CreatedAt)
            .Select(item => new PurchaseInvoiceImportFileDto(
                item.Id,
                item.FileKind,
                item.OriginalFileName,
                item.ContentType,
                item.FileSizeBytes,
                item.Sha256Hash,
                item.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<(string Path, string ContentType, string DownloadName)?> GetStoredFileAsync(HttpContext context, Guid id, Guid fileId, CancellationToken cancellationToken)
    {
        var batchExists = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .AnyAsync(item => item.Id == id, cancellationToken);
        if (!batchExists)
        {
            return null;
        }

        var file = await db.PurchaseInvoiceImportFiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == fileId && item.BatchId == id && !item.Deleted, cancellationToken);
        if (file is null || string.IsNullOrWhiteSpace(file.StoredFilePath) || !File.Exists(file.StoredFilePath))
        {
            return null;
        }

        return (file.StoredFilePath, string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, file.OriginalFileName);
    }

    private static (string Path, string ContentType, string DownloadName)? OriginalFileResult(PurchaseInvoiceImportBatch? batch)
    {
        if (batch is null || string.IsNullOrWhiteSpace(batch.StoredFilePath) || !File.Exists(batch.StoredFilePath))
        {
            return null;
        }
        return (batch.StoredFilePath, string.IsNullOrWhiteSpace(batch.ContentType) ? "application/octet-stream" : batch.ContentType, batch.SourceFileName);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceImportProductMatchOptionDto>> SearchProductMatchesAsync(HttpContext context, string? query, Guid? storeId, int take, CancellationToken cancellationToken)
    {
        var term = query?.Trim() ?? string.Empty;
        var normalizedTerm = NormalizeSearchText(term);
        var tokens = TokenizeForMatch(term).Take(8).ToArray();
        take = Math.Clamp(take, 1, 30);

        var productQuery = WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Include(item => item.ProductCategory)
            .Include(item => item.ProductSubCategory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowerTerm = term.ToLowerInvariant();
            var first = tokens.ElementAtOrDefault(0) ?? string.Empty;
            var second = tokens.ElementAtOrDefault(1) ?? string.Empty;
            var third = tokens.ElementAtOrDefault(2) ?? string.Empty;
            var hasFirst = first.Length > 0;
            var hasSecond = second.Length > 0;
            var hasThird = third.Length > 0;
            productQuery = productQuery.Where(item =>
                item.Name.ToLower().Contains(lowerTerm) ||
                item.Barcode.ToLower().Contains(lowerTerm) ||
                (item.HSNCode != null && item.HSNCode.ToLower().Contains(lowerTerm)) ||
                (hasFirst && item.Name.ToLower().Contains(first)) ||
                (hasSecond && item.Name.ToLower().Contains(second)) ||
                (hasThird && item.Name.ToLower().Contains(third)));
        }

        var products = await productQuery
            .OrderBy(item => item.Name)
            .Take(Math.Max(take * 8, 80))
            .ToListAsync(cancellationToken);
        var productIds = products.Select(item => item.Id).ToArray();
        var stockQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => productIds.Contains(item.ProductId) && !item.IsOFB);
        if (storeId.HasValue)
        {
            stockQuery = stockQuery.Where(item => item.StoreId == storeId.Value);
        }
        var stocks = await stockQuery
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ToListAsync(cancellationToken);

        return products
            .Select(product =>
            {
                var stock = stocks.FirstOrDefault(item => item.ProductId == product.Id);
                var score = ScoreProductMatch(normalizedTerm, tokens, product.Name, product.Barcode, product.HSNCode);
                var barcode = string.IsNullOrWhiteSpace(stock?.Barcode) ? product.Barcode : stock!.Barcode;
                var hsn = !string.IsNullOrWhiteSpace(stock?.HSNCode) ? stock!.HSNCode : product.HSNCode;
                var labelPrefix = score >= 90 ? "Exact" : score >= 70 ? "Strong" : score >= 45 ? "Similar" : "Candidate";
                return new
                {
                    Score = score,
                    Dto = new PurchaseInvoiceImportProductMatchOptionDto(
                        product.Id,
                        product.Name,
                        barcode,
                        hsn,
                        stock?.Unit ?? product.Unit,
                        stock?.MRP ?? product.MRP,
                        stock?.TaxRate ?? product.TaxRate,
                        stock?.TaxId,
                        product.ProductCategoryId,
                        product.ProductSubCategoryId,
                        product.ProductType,
                        product.ProductGroup,
                        $"{labelPrefix}: {product.Name} | {barcode}")
                };
            })
            .Where(item => string.IsNullOrWhiteSpace(term) || item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Dto.Name)
            .Take(take)
            .Select(item => item.Dto)
            .ToList();
    }

    public async Task<PurchaseInvoiceImportBatchDto?> ApplyProductMatchAsync(HttpContext context, Guid batchId, Guid lineId, PurchaseInvoiceImportProductMatchRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be edited.");
        }

        var line = await db.PurchaseInvoiceImportLines.FirstOrDefaultAsync(item => item.Id == lineId && item.BatchId == batchId && !item.Deleted, cancellationToken)
            ?? throw new InvalidOperationException("Import draft line was not found.");
        var product = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Selected product was not found in your workspace.");
        var latestStock = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => item.ProductId == product.Id && item.StoreId == batch.StoreId && !item.IsOFB)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        line.ProductId = product.Id;
        line.ProductNameFinal = product.Name;
        line.BarcodeFinal = latestStock?.Barcode ?? product.Barcode;
        line.HsnCode = latestStock?.HSNCode ?? product.HSNCode ?? line.HsnCode;
        line.Unit = latestStock?.Unit ?? product.Unit;
        line.Mrp = line.Mrp > 0 ? line.Mrp : latestStock?.MRP ?? product.MRP;
        line.TaxRate = latestStock?.TaxRate ?? product.TaxRate;
        line.TaxId = latestStock?.TaxId ?? await db.Taxes.AsNoTracking()
            .Where(item => item.CompositeRate == product.TaxRate && item.TaxType == product.TaxType)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        line.ProductCategoryId = product.ProductCategoryId;
        line.ProductSubCategoryId = product.ProductSubCategoryId;
        line.ProductType = product.ProductType;
        line.ProductGroup = product.ProductGroup;
        line.MatchStatus = "MatchedExistingProduct";
        line.ReviewRequired = !line.TaxId.HasValue || string.IsNullOrWhiteSpace(line.BarcodeFinal);
        line.ReviewMessage = line.ReviewRequired ? "Matched product still needs barcode/GST review before posting." : null;
        line.UpdatedAt = DateTime.Now;
        RecalculateLine(line);

        var lines = await db.PurchaseInvoiceImportLines.Where(item => item.BatchId == batchId && !item.Deleted).ToListAsync(cancellationToken);
        RecalculateBatchTotals(batch, lines.Where(item => !item.Ignored).ToList());
        var warnings = ValidateBatch(batch, lines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, batchId, cancellationToken);
    }


    public async Task<PurchaseInvoiceImportBatchDto?> SplitLineBySizeAsync(HttpContext context, Guid batchId, Guid lineId, PurchaseInvoiceImportSplitLineRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == batchId, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be edited.");
        }

        var source = await db.PurchaseInvoiceImportLines
            .FirstOrDefaultAsync(item => item.Id == lineId && item.BatchId == batchId && !item.Deleted, cancellationToken)
            ?? throw new InvalidOperationException("Import draft line was not found.");

        var sizeLabels = request.SizeLabels
            .Select(item => (item ?? string.Empty).Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(200)
            .ToList();
        if (sizeLabels.Count == 0)
        {
            throw new InvalidOperationException("Enter at least one size/color label before splitting the invoice line.");
        }
        if (source.Quantity > 0 && sizeLabels.Count != (int)Math.Round(source.Quantity, 0) && Math.Abs(source.Quantity - Math.Round(source.Quantity, 0)) < 0.001m)
        {
            throw new InvalidOperationException($"Line quantity is {source.Quantity:0.##}. Enter exactly {source.Quantity:0.##} size/color labels, or first adjust quantity manually.");
        }

        var existingLines = await db.PurchaseInvoiceImportLines
            .Where(item => item.BatchId == batchId && !item.Deleted)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);

        var sourceIndex = existingLines.FindIndex(item => item.Id == source.Id);
        var insertAtLineNumber = source.LineNumber <= 0 ? sourceIndex + 1 : source.LineNumber;
        var perLineQuantity = request.QuantityOnePerLine ? 1m : Round(source.Quantity / Math.Max(sizeLabels.Count, 1));
        var perLineDiscount = source.LineDiscount > 0 ? Round(source.LineDiscount / sizeLabels.Count) : 0m;
        var newLines = new List<PurchaseInvoiceImportLine>();

        source.Deleted = true;
        source.UpdatedAt = DateTime.Now;

        for (var index = 0; index < sizeLabels.Count; index++)
        {
            var sizeLabel = sizeLabels[index];
            var splitName = request.AppendSizeToProductName
                ? AppendVariantToProductName(source.ProductNameFinal ?? source.ProductNameRaw ?? "Imported product", sizeLabel)
                : source.ProductNameFinal ?? source.ProductNameRaw ?? "Imported product";
            var line = new PurchaseInvoiceImportLine
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                LineNumber = insertAtLineNumber + index,
                ProductId = request.ClearProductMatchAndBarcode ? null : source.ProductId,
                ProductNameRaw = source.ProductNameRaw,
                ProductNameFinal = splitName,
                BarcodeRaw = request.ClearProductMatchAndBarcode ? null : source.BarcodeRaw,
                BarcodeFinal = request.ClearProductMatchAndBarcode ? null : source.BarcodeFinal,
                HsnCode = source.HsnCode,
                Unit = source.Unit,
                Quantity = perLineQuantity,
                Mrp = source.Mrp,
                CostPrice = source.CostPrice,
                UnitDiscount = source.UnitDiscount,
                LineDiscount = perLineDiscount,
                TaxRate = source.TaxRate,
                GstPriceMode = source.GstPriceMode,
                TaxId = source.TaxId,
                ConfidenceScore = source.ConfidenceScore,
                MatchStatus = "SplitVariantDraft",
                ReviewRequired = true,
                ReviewMessage = $"Split from invoice line {source.LineNumber} for size/color '{sizeLabel}'. Review or match existing product, then generate barcode.",
                ProductCategoryId = source.ProductCategoryId,
                ProductSubCategoryId = source.ProductSubCategoryId,
                ProductType = source.ProductType,
                ProductGroup = source.ProductGroup,
                Ignored = false,
                CompanyId = batch.CompanyId,
                StoreGroupId = batch.StoreGroupId,
                StoreId = batch.StoreId
            };
            RecalculateLine(line);
            db.PurchaseInvoiceImportLines.Add(line);
            newLines.Add(line);
        }

        var linesForRenumber = existingLines.Where(item => item.Id != source.Id).Concat(newLines).OrderBy(item => item.LineNumber).ThenBy(item => item.CreatedAt).ToList();
        for (var index = 0; index < linesForRenumber.Count; index++)
        {
            linesForRenumber[index].LineNumber = index + 1;
            linesForRenumber[index].UpdatedAt = DateTime.Now;
        }

        await EnsureMissingBarcodesAsync(batch, linesForRenumber, true, cancellationToken);
        RecalculateBatchTotals(batch, linesForRenumber.Where(item => !item.Ignored).ToList());
        var warnings = ValidateBatch(batch, linesForRenumber);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, batchId, cancellationToken);
    }

    public async Task<bool> DeleteBatchAsync(HttpContext context, Guid id, bool deleteFiles, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (batch is null)
        {
            return false;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted supplier invoice imports cannot be deleted because they are purchase proof. Use reject/delete only for failed or unposted drafts.");
        }

        var files = await SoftDeleteBatchAsync(batch, "Deleted from supplier invoice import history.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (deleteFiles)
        {
            TryDeleteImportFiles(batch, files);
        }
        return true;
    }

    public async Task<PurchaseInvoiceImportCleanupResultDto> CleanupHistoryAsync(HttpContext context, PurchaseInvoiceImportCleanupRequest request, CancellationToken cancellationToken)
    {
        var statuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (request.DeleteFailed) statuses.Add("Failed");
        if (request.DeleteRejected) statuses.Add("Rejected");
        if (request.DeleteNeedsReview) statuses.Add("NeedsReview");
        if (request.DeleteReadyToPost) statuses.Add("ReadyToPost");
        if (statuses.Count == 0)
        {
            return new PurchaseInvoiceImportCleanupResultDto(0, 0, 0, new[] { "No import statuses were selected for cleanup." });
        }

        var cutoff = DateTime.Now.Date.AddDays(-Math.Max(request.OlderThanDays, 0)).AddDays(1).AddTicks(-1);
        var candidates = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .Where(item => !item.Deleted && !item.PostedPurchaseInvoiceId.HasValue && item.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        candidates = candidates
            .Where(item => statuses.Contains(item.Status ?? string.Empty))
            .OrderBy(item => item.CreatedAt)
            .Take(200)
            .ToList();

        var deletedBatches = 0;
        var deletedFiles = 0;
        long deletedBytes = 0;
        foreach (var batch in candidates)
        {
            var files = await SoftDeleteBatchAsync(batch, $"Bulk cleanup removed {batch.Status} supplier invoice import history.", cancellationToken);
            deletedBatches++;
            deletedFiles += files.Count + (string.IsNullOrWhiteSpace(batch.StoredFilePath) ? 0 : 1);
            deletedBytes += SafeFileSize(batch.StoredFilePath);
            deletedBytes += files.Sum(file => SafeFileSize(file.StoredFilePath));
            if (request.DeleteFiles)
            {
                TryDeleteImportFiles(batch, files);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var message = deletedBatches == 0
            ? "No failed/rejected supplier invoice import history matched the cleanup filters."
            : $"Deleted {deletedBatches} unposted supplier invoice import draft(s) and {deletedFiles} stored proof/audit file reference(s). Posted purchase proofs were protected.";
        return new PurchaseInvoiceImportCleanupResultDto(deletedBatches, deletedFiles, deletedBytes, new[] { message });
    }

    private async Task<IReadOnlyList<PurchaseInvoiceImportFile>> SoftDeleteBatchAsync(PurchaseInvoiceImportBatch batch, string message, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        batch.Deleted = true;
        batch.UpdatedAt = now;
        batch.ErrorMessage = message;

        var lines = await db.PurchaseInvoiceImportLines.Where(item => item.BatchId == batch.Id && !item.Deleted).ToListAsync(cancellationToken);
        foreach (var line in lines)
        {
            line.Deleted = true;
            line.UpdatedAt = now;
        }

        var files = await db.PurchaseInvoiceImportFiles.Where(item => item.BatchId == batch.Id && !item.Deleted).ToListAsync(cancellationToken);
        foreach (var file in files)
        {
            file.Deleted = true;
            file.UpdatedAt = now;
        }

        return files;
    }

    public async Task<PurchaseInvoiceImportBatchDto?> GenerateMissingBarcodesAsync(HttpContext context, Guid id, PurchaseInvoiceImportGenerateBarcodeRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be edited.");
        }

        var lines = await db.PurchaseInvoiceImportLines
            .Where(item => item.BatchId == id && !item.Deleted)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);

        await EnsureMissingBarcodesAsync(batch, lines, request.OnlyMissing, cancellationToken);
        RecalculateBatchTotals(batch, lines.Where(item => !item.Ignored).ToList());
        var warnings = ValidateBatch(batch, lines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }

    public async Task<PurchaseInvoiceImportBatchDto?> OverrideDuplicateAsync(HttpContext context, Guid id, PurchaseInvoiceImportDuplicateOverrideRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (!batch.DuplicatePurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("This draft does not have a duplicate purchase invoice warning.");
        }
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 8)
        {
            throw new InvalidOperationException("Enter a clear duplicate override reason before posting.");
        }
        batch.DuplicateOverrideReason = request.Reason.Trim();
        batch.DuplicateOverrideBy = context.User.Identity?.Name ?? context.User.FindFirst("userName")?.Value ?? context.User.FindFirst("email")?.Value;
        batch.DuplicateOverrideAt = DateTime.Now;
        batch.UpdatedAt = DateTime.Now;
        var lines = await db.PurchaseInvoiceImportLines.Where(item => item.BatchId == id && !item.Deleted).ToListAsync(cancellationToken);
        var warnings = ValidateBatch(batch, lines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }

    public async Task<PurchaseInvoiceImportBatchDto?> RecheckDuplicateAsync(HttpContext context, Guid id, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be rechecked.");
        }

        await RefreshDuplicateWarningAsync(batch, cancellationToken);
        var lines = await db.PurchaseInvoiceImportLines.Where(item => item.BatchId == id && !item.Deleted).ToListAsync(cancellationToken);
        var warnings = ValidateBatch(batch, lines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }

    public async Task<PurchaseInvoiceImportBatchDto?> DistributeHeaderDiscountAsync(HttpContext context, Guid id, PurchaseInvoiceImportDistributeDiscountRequest request, CancellationToken cancellationToken)
    {
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be edited.");
        }

        var discountAmount = Round(request.DiscountAmount > 0 ? request.DiscountAmount : batch.DiscountAmount);
        if (discountAmount <= 0)
        {
            throw new InvalidOperationException("Enter header discount amount before distributing it into item lines.");
        }

        var lines = await db.PurchaseInvoiceImportLines
            .Where(item => item.BatchId == id && !item.Deleted && !item.Ignored)
            .OrderBy(item => item.LineNumber)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0)
        {
            throw new InvalidOperationException("At least one active item line is required before distributing discount.");
        }

        var grossTotal = lines.Sum(item => Math.Max(item.CostPrice * item.Quantity, 0));
        if (grossTotal <= 0)
        {
            throw new InvalidOperationException("Item cost and quantity are required before distributing discount.");
        }

        decimal allocated = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineGross = Math.Max(line.CostPrice * line.Quantity, 0);
            var lineDiscount = index == lines.Count - 1
                ? Round(discountAmount - allocated)
                : Round(discountAmount * lineGross / grossTotal);
            line.LineDiscount = Math.Max(lineDiscount, 0);
            line.UnitDiscount = 0;
            RecalculateLine(line);
            line.UpdatedAt = DateTime.Now;
            allocated += line.LineDiscount;
        }

        batch.DiscountAmount = 0;
        RecalculateBatchTotals(batch, lines);
        await RefreshDuplicateWarningAsync(batch, cancellationToken);
        var allLines = await db.PurchaseInvoiceImportLines.Where(item => item.BatchId == id && !item.Deleted).ToListAsync(cancellationToken);
        var warnings = ValidateBatch(batch, allLines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        batch.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync(cancellationToken);

        var snapshotPath = CorrectedDraftPath(batch);
        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(new { batch, lines = allLines }, JsonOptions), cancellationToken);
        await UpsertImportFileAsync(batch, "CorrectedDraftJson", "corrected-draft.json", snapshotPath, "application/json", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }

    public async Task<PurchaseInvoiceImportBatchDto?> ReparseTextAsync(HttpContext context, Guid id, PurchaseInvoiceImportReparseTextRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawText))
        {
            throw new InvalidOperationException("Paste OCR/text content before reparsing the supplier invoice.");
        }
        var batch = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (batch is null)
        {
            return null;
        }
        if (batch.PostedPurchaseInvoiceId.HasValue)
        {
            throw new InvalidOperationException("Posted import drafts cannot be reparsed.");
        }

        var parserProfile = await FindVendorProfileAsync(batch.CompanyId, request.RawText, batch.VendorGstinFinal ?? batch.VendorGstinRaw, batch.VendorNameFinal ?? batch.VendorNameRaw, cancellationToken);
        var parsed = ParseInvoiceText(request.RawText, parserProfile);
        var parsedLineDiscountTotal = Round(parsed.Lines.Sum(line => line.LineDiscount));
        var remainingHeaderDiscount = parsed.DiscountAmount > 0 && parsedLineDiscountTotal > 0 && Math.Abs(parsed.DiscountAmount - parsedLineDiscountTotal) <= Math.Max(2m, parsed.DiscountAmount * 0.02m)
            ? 0m
            : parsed.DiscountAmount;
        var parserDiagnosticsPath = Path.Combine(Path.GetDirectoryName(batch.StoredFilePath) ?? StorageRoot(), "parser-line-decisions.json");
        await File.WriteAllTextAsync(parserDiagnosticsPath, JsonSerializer.Serialize(parsed.LineDecisions, JsonOptions), cancellationToken);
        await UpsertImportFileAsync(batch, "ParserLineDecisions", "parser-line-decisions.json", parserDiagnosticsPath, "application/json", cancellationToken);
        batch.RawTextPath ??= Path.Combine(Path.GetDirectoryName(batch.StoredFilePath) ?? StorageRoot(), "extracted-text.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(batch.RawTextPath) ?? StorageRoot());
        await File.WriteAllTextAsync(batch.RawTextPath, request.RawText.Trim(), cancellationToken);
        await UpsertImportFileAsync(batch, "ExtractedText", "extracted-text.txt", batch.RawTextPath, "text/plain", cancellationToken);
        batch.OcrProvider = "ManualPastedText";
        batch.OcrStatus = "TextReparsed";
        batch.ConfidenceScore = parsed.ConfidenceScore;
        batch.ParserTemplate = parsed.ParserTemplate;
        batch.ParserTemplateReason = parsed.ParserTemplateReason;
        batch.VendorNameRaw = parsed.VendorName ?? batch.VendorNameRaw;
        if (string.IsNullOrWhiteSpace(batch.VendorNameFinal)) batch.VendorNameFinal = parsed.VendorName;
        batch.VendorGstinRaw = parsed.VendorGstin ?? batch.VendorGstinRaw;
        if (string.IsNullOrWhiteSpace(batch.VendorGstinFinal)) batch.VendorGstinFinal = parsed.VendorGstin;
        batch.SupplierInvoiceNumber = string.IsNullOrWhiteSpace(batch.SupplierInvoiceNumber) ? parsed.InvoiceNumber : batch.SupplierInvoiceNumber;
        batch.SupplierInvoiceDate ??= parsed.InvoiceDate;
        batch.DueDate ??= parsed.InvoiceDate?.AddDays(45);
        if (batch.BillAmount <= 0) batch.BillAmount = parsed.BillAmount;
        if (parsed.DiscountAmount > 0) batch.DiscountAmount = remainingHeaderDiscount;
        batch.UpdatedAt = DateTime.Now;

        var existingLines = await db.PurchaseInvoiceImportLines.Where(item => item.BatchId == id).ToListAsync(cancellationToken);
        if (request.ReplaceLines)
        {
            foreach (var existingLine in existingLines.Where(item => !item.Deleted))
            {
                existingLine.Deleted = true;
                existingLine.UpdatedAt = DateTime.Now;
            }
        }
        var nextLineNumber = request.ReplaceLines ? 1 : existingLines.Where(item => !item.Deleted).Select(item => item.LineNumber).DefaultIfEmpty(0).Max() + 1;
        var newLines = new List<PurchaseInvoiceImportLine>();
        foreach (var parsedLine in parsed.Lines)
        {
            var product = await MatchProductAsync(batch.CompanyId, parsedLine.Barcode, parsedLine.Name, cancellationToken);
            var tax = await MatchTaxAsync(parsedLine.TaxRate, cancellationToken);
            var line = new PurchaseInvoiceImportLine
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                LineNumber = nextLineNumber++,
                ProductId = product?.Id,
                ProductNameRaw = parsedLine.Name,
                ProductNameFinal = product?.Name ?? parsedLine.Name,
                BarcodeRaw = parsedLine.Barcode,
                BarcodeFinal = product?.Barcode ?? parsedLine.Barcode,
                HsnCode = parsedLine.HsnCode,
                Unit = Unit.Pcs,
                Quantity = parsedLine.Quantity,
                CostPrice = parsedLine.Rate,
                Mrp = parsedLine.Mrp > 0 ? parsedLine.Mrp : parsedLine.Rate,
                UnitDiscount = parsedLine.UnitDiscount,
                LineDiscount = parsedLine.LineDiscount,
                TaxRate = parsedLine.TaxRate,
                GstPriceMode = parsedLine.GstPriceMode,
                TaxId = tax?.Id,
                TaxableAmount = parsedLine.TaxableAmount,
                TaxAmount = parsedLine.TaxAmount,
                LineTotal = parsedLine.LineTotal,
                ConfidenceScore = parsedLine.ConfidenceScore,
                MatchStatus = product is null ? "NewProductDraft" : "MatchedExistingProduct",
                ReviewRequired = product is null || string.IsNullOrWhiteSpace(product.Barcode) || tax is null,
                ReviewMessage = BuildLineReviewMessage(parsedLine, product is null, tax is null),
                ProductCategoryId = product?.ProductCategoryId,
                ProductSubCategoryId = product?.ProductSubCategoryId,
                ProductType = product?.ProductType ?? ProductType.Apparels,
                ProductGroup = product?.ProductGroup ?? ProductGroup.Shirting,
                CompanyId = batch.CompanyId,
                StoreGroupId = batch.StoreGroupId,
                StoreId = batch.StoreId
            };
            RecalculateLine(line);
            db.PurchaseInvoiceImportLines.Add(line);
            newLines.Add(line);
        }

        var allLines = existingLines.Where(item => !item.Deleted).Concat(newLines).ToList();
        await EnsureMissingBarcodesAsync(batch, allLines, true, cancellationToken);
        RecalculateBatchTotals(batch, allLines.Where(item => !item.Ignored).ToList());
        var warnings = ValidateBatch(batch, allLines);
        batch.Status = warnings.Count == 0 ? "ReadyToPost" : "NeedsReview";
        batch.ErrorMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings.Take(3));
        var snapshotPath = CorrectedDraftPath(batch);
        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(new { batch, lines = allLines, replaceLines = request.ReplaceLines }, JsonOptions), cancellationToken);
        await UpsertImportFileAsync(batch, "CorrectedDraftJson", "corrected-draft.json", snapshotPath, "application/json", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetBatchAsync(context, id, cancellationToken);
    }


    private static string AppendVariantToProductName(string productName, string variantLabel)
    {
        var name = string.IsNullOrWhiteSpace(productName) ? "Imported product" : productName.Trim();
        var label = variantLabel.Trim();
        return name.Contains(label, StringComparison.OrdinalIgnoreCase) ? name : $"{name} - {label}";
    }

    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return Regex.Replace(normalized, @"\s+", " ");
    }

    private static IReadOnlyList<string> TokenizeForMatch(string? value)
    {
        return NormalizeSearchText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length >= 2 && !IsWeakMatchToken(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsWeakMatchToken(string value)
    {
        return value is "the" or "and" or "for" or "with" or "pcs" or "pc" or "std" or "size" or "brand" or "hsn" or "gst";
    }

    private static int ScoreProductMatch(string normalizedQuery, IReadOnlyList<string> queryTokens, string? productName, string? barcode, string? hsnCode)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery) && queryTokens.Count == 0) return 1;
        var name = NormalizeSearchText(productName);
        var code = NormalizeSearchText(barcode);
        var hsn = NormalizeSearchText(hsnCode);
        if (!string.IsNullOrWhiteSpace(code) && code == normalizedQuery) return 100;
        if (!string.IsNullOrWhiteSpace(name) && name == normalizedQuery) return 95;
        if (!string.IsNullOrWhiteSpace(code) && code.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) return 90;
        if (!string.IsNullOrWhiteSpace(hsn) && hsn == normalizedQuery) return 80;
        if (!string.IsNullOrWhiteSpace(name) && name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)) return 78;
        if (queryTokens.Count == 0) return 0;

        var productTokens = new HashSet<string>(TokenizeForMatch(productName), StringComparer.OrdinalIgnoreCase);
        if (productTokens.Count == 0) return 0;
        var hits = queryTokens.Count(productTokens.Contains);
        var score = (int)Math.Round((decimal)hits / Math.Max(queryTokens.Count, 1) * 70m);
        if (!string.IsNullOrWhiteSpace(hsn) && queryTokens.Contains(hsn)) score += 10;
        return Math.Min(score, 74);
    }

    private static void TryDeleteImportFiles(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportFile> files)
    {
        foreach (var file in files)
        {
            TryDeleteFile(file.StoredFilePath);
        }
        TryDeleteFile(batch.StoredFilePath);
        var folder = Path.GetDirectoryName(batch.StoredFilePath);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch
            {
                // Best-effort cleanup only. Database history is already soft-deleted.
            }
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { File.Delete(path); } catch { }
    }

    private static long SafeFileSize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return 0;
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private async Task EnsureMissingBarcodesAsync(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines, bool onlyMissing, CancellationToken cancellationToken)
    {
        var activeLines = lines
            .Where(item => !item.Deleted && !item.Ignored)
            .OrderBy(item => item.LineNumber)
            .ToList();
        if (activeLines.Count == 0)
        {
            return;
        }

        var monthSequence = await GetMonthlyImportSequenceAsync(batch, cancellationToken);
        var usedBarcodes = new HashSet<string>(activeLines
            .Select(item => item.BarcodeFinal)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim()), StringComparer.OrdinalIgnoreCase);

        foreach (var line in activeLines)
        {
            if (onlyMissing && !ShouldAutoGenerateBarcode(line.BarcodeFinal))
            {
                continue;
            }

            var serialLine = Math.Clamp(line.LineNumber <= 0 ? activeLines.IndexOf(line) + 1 : line.LineNumber, 1, 999);
            var barcode = await NextAvailableAutoBarcodeAsync(batch, monthSequence, serialLine, usedBarcodes, cancellationToken);
            line.BarcodeFinal = barcode;
            line.MatchStatus = line.ProductId.HasValue ? "MatchedExistingProduct" : "NewProductDraft";
            line.ReviewRequired = !line.TaxId.HasValue || line.Quantity <= 0 || string.IsNullOrWhiteSpace(line.ProductNameFinal);
            line.ReviewMessage = line.ReviewRequired ? "Review product, quantity and GST before posting." : null;
            line.UpdatedAt = DateTime.Now;
            usedBarcodes.Add(barcode);
        }
    }


    private static bool ShouldAutoGenerateBarcode(string? barcode)
    {
        return string.IsNullOrWhiteSpace(barcode) || barcode.Trim().StartsWith("IMP-", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> GetMonthlyImportSequenceAsync(PurchaseInvoiceImportBatch batch, CancellationToken cancellationToken)
    {
        var date = batch.CreatedAt == default ? DateTime.UtcNow : batch.CreatedAt;
        var monthStart = new DateTime(date.Year, date.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var previousCount = await db.PurchaseInvoiceImportBatches.AsNoTracking()
            .Where(item => item.CompanyId == batch.CompanyId
                && item.StoreId == batch.StoreId
                && !item.Deleted
                && item.CreatedAt >= monthStart
                && item.CreatedAt < monthEnd
                && item.Id != batch.Id
                && item.CreatedAt < date)
            .CountAsync(cancellationToken);
        return previousCount + 1;
    }

    private async Task<string> NextAvailableAutoBarcodeAsync(PurchaseInvoiceImportBatch batch, int monthSequence, int lineNumber, HashSet<string> usedBarcodes, CancellationToken cancellationToken)
    {
        var sequence = Math.Max(monthSequence, 1);
        var line = Math.Clamp(lineNumber, 1, 999);
        for (var offset = 0; offset < 9000; offset++)
        {
            var candidateLine = ((line - 1 + offset) % 999) + 1;
            var candidate = BuildAutoBarcode(batch, sequence, candidateLine);
            if (usedBarcodes.Contains(candidate))
            {
                continue;
            }
            if (!await BarcodeExistsAsync(batch.CompanyId, candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return BuildAutoBarcode(batch, sequence, line) + DateTime.UtcNow.ToString("ss");
    }

    private async Task<bool> BarcodeExistsAsync(Guid companyId, string barcode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return false;
        }

        var code = barcode.Trim();
        return await db.Products.AsNoTracking().AnyAsync(item => item.CompanyId == companyId && item.Barcode == code, cancellationToken)
            || await db.Stocks.AsNoTracking().AnyAsync(item => item.CompanyId == companyId && item.Barcode == code, cancellationToken);
    }

    private static string BuildAutoBarcode(PurchaseInvoiceImportBatch batch, int monthSequence, int lineNumber)
    {
        var date = batch.CreatedAt == default ? DateTime.UtcNow : batch.CreatedAt;
        var prefix = date.ToString("yyMM");
        var suffixNumber = Math.Max(monthSequence, 1) * 1000 + Math.Clamp(lineNumber, 1, 999);
        return $"{prefix}{suffixNumber:0000}";
    }

    private async Task<PurchaseInwardRequest> BuildInwardRequestFromBatchAsync(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines, CancellationToken cancellationToken)
    {
        await EnsureMissingBarcodesAsync(batch, lines, true, cancellationToken);
        var items = lines.Select(line => new PurchaseInwardItemRequest(
            line.ProductId,
            line.ProductNameFinal ?? line.ProductNameRaw ?? "Imported product",
            line.BarcodeFinal!.Trim(),
            line.Quantity,
            CostPriceForPurchasePosting(line),
            line.Mrp,
            DiscountForPurchasePosting(line),
            line.TaxId,
            line.ProductCategoryId,
            line.ProductSubCategoryId,
            line.HsnCode,
            line.Unit,
            line.ProductType,
            line.ProductGroup)).ToList();

        return new PurchaseInwardRequest(
            batch.CompanyId,
            batch.StoreGroupId,
            batch.StoreId,
            batch.VendorNameFinal ?? batch.VendorNameRaw ?? "Imported Supplier",
            batch.VendorMobileNumber,
            batch.VendorGstinFinal,
            batch.SupplierInvoiceNumber,
            null,
            batch.PaidAmount,
            batch.PaymentMode,
            batch.BankAccountId,
            batch.FreightAmount,
            items,
            batch.SupplierInvoiceDate?.Date ?? DateTime.Today,
            batch.SupplierInvoiceDate,
            batch.DueDate,
            batch.VendorId);
    }

    private static decimal CostPriceForPurchasePosting(PurchaseInvoiceImportLine line)
    {
        if (!string.Equals(NormalizeGstPriceMode(line.GstPriceMode), "Exclusive", StringComparison.OrdinalIgnoreCase))
        {
            return Round(line.CostPrice);
        }

        return Round(line.CostPrice * (1 + (line.TaxRate / 100m)));
    }

    private static decimal DiscountForPurchasePosting(PurchaseInvoiceImportLine line)
    {
        var unitDiscount = line.UnitDiscount > 0 ? line.UnitDiscount : line.Quantity > 0 ? Round(line.LineDiscount / line.Quantity) : 0;
        if (!string.Equals(NormalizeGstPriceMode(line.GstPriceMode), "Exclusive", StringComparison.OrdinalIgnoreCase))
        {
            return Round(unitDiscount);
        }

        return Round(unitDiscount * (1 + (line.TaxRate / 100m)));
    }

    private async Task<PurchaseInwardResponse> PostPurchaseInwardAsync(HttpContext context, PurchaseInwardRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VendorName)) throw new InvalidOperationException("Vendor name is required.");
        if (request.Items.Count == 0) throw new InvalidOperationException("At least one purchase item is required.");
        if (request.Items.Any(item => item.Quantity <= 0 || item.CostPrice < 0 || item.Mrp < 0)) throw new InvalidOperationException("Quantity, cost price, and MRP must be valid.");

        var purchaseStore = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking().Include(store => store.Company), context)
            .Where(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId)
            .Select(store => new { CompanyGstin = store.Company != null ? store.Company.GSTIN : string.Empty })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Selected purchase store is outside your access scope.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var vendorValidation = !string.IsNullOrWhiteSpace(request.VendorGstin)
            ? await gstinLookup.ValidatePartyAsync("Vendor", request.VendorGstin, request.VendorName, null, cancellationToken)
            : null;
        var vendor = await GetOrCreateVendorAsync(request, vendorValidation, cancellationToken);
        var interState = IsInterStateSupply(purchaseStore.CompanyGstin, vendor.GSTIN);
        var inwardDate = request.InwardDate?.Date ?? request.SupplierInvoiceDate?.Date ?? DateTime.Today;
        var invoiceDate = request.SupplierInvoiceDate?.Date ?? inwardDate;
        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? await documentNumbers.NextPurchaseInvoiceAsync(request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken)
            : request.InvoiceNumber.Trim();
        var inwardNumber = await documentNumbers.NextPurchaseInwardAsync(request.CompanyId, request.StoreGroupId, request.StoreId, inwardDate, cancellationToken);
        var invoiceId = Guid.NewGuid();

        var invoiceItems = new List<PurchaseInvoiceItem>();
        decimal grossMrp = 0, discountAmount = 0, taxableAmount = 0, taxAmount = 0, cgstAmount = 0, sgstAmount = 0, igstAmount = 0, totalQuantity = 0;

        foreach (var requestItem in request.Items)
        {
            var product = await GetOrCreateProductAsync(request, requestItem, cancellationToken)
                ?? throw new InvalidOperationException("Run quick setup and select product category, subcategory, and tax before purchasing new products.");
            var tax = requestItem.TaxId.HasValue
                ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == requestItem.TaxId.Value, cancellationToken)
                : await db.Taxes.FirstOrDefaultAsync(item => item.CompositeRate == product.TaxRate && item.TaxType == product.TaxType, cancellationToken);
            tax ??= await db.Taxes.FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Tax setup is required before purchase inward.");

            var barcode = string.IsNullOrWhiteSpace(requestItem.Barcode) ? product.Barcode : requestItem.Barcode.Trim();
            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var lineCost = requestItem.CostPrice * requestItem.Quantity;
            var lineNet = Math.Max(lineCost - lineDiscount, 0);
            var taxable = Math.Round(lineNet / (1 + (tax.CompositeRate / 100)), 2);
            var taxValue = Math.Round(taxable * (tax.CompositeRate / 100), 2);
            var lineAmount = taxable + taxValue;
            var split = SplitGst(taxValue, tax.TaxType, interState);
            var hsnCode = string.IsNullOrWhiteSpace(requestItem.HsnCode) ? product.HSNCode : requestItem.HsnCode.Trim();
            var unit = requestItem.ProductUnit ?? product.Unit;

            invoiceItems.Add(new PurchaseInvoiceItem
            {
                InvoiceId = invoiceId,
                ProductId = product.Id,
                Barcode = barcode,
                ProductName = product.Name,
                HSNCode = hsnCode,
                Unit = unit,
                ProductCategoryId = product.ProductCategoryId,
                ProductSubCategoryId = product.ProductSubCategoryId,
                MRP = requestItem.Mrp,
                DiscountAmount = lineDiscount,
                BasePrice = taxable,
                TaxPercentage = tax.CompositeRate,
                TaxAmount = taxValue,
                CGSTAmount = split.Cgst,
                SGSTAmount = split.Sgst,
                IGSTAmount = split.Igst,
                Amount = lineAmount,
                TaxType = interState ? TaxType.IGST : tax.TaxType,
                TaxId = tax.Id,
                BilledQuantity = requestItem.Quantity,
                CompanyId = request.CompanyId
            });

            await DocumentNumberGenerator.LockStockKeyAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, product.Id, barcode, cancellationToken);
            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context).FirstOrDefaultAsync(item => item.ProductId == product.Id && item.Barcode == barcode && item.StoreId == request.StoreId && !item.IsOFB, cancellationToken);
            if (stock is null)
            {
                stock = new Stock
                {
                    ProductId = product.Id,
                    Barcode = barcode,
                    Unit = unit,
                    HSNCode = hsnCode,
                    PurchaseQty = 0,
                    CostPrice = 0,
                    MRP = requestItem.Mrp,
                    TaxRate = tax.CompositeRate,
                    TaxType = tax.TaxType,
                    TaxId = tax.Id,
                    IsOFB = false,
                    CompanyId = request.CompanyId,
                    StoreGroupId = request.StoreGroupId,
                    StoreId = request.StoreId
                };
                db.Stocks.Add(stock);
            }
            else
            {
                stock.MRP = requestItem.Mrp;
                stock.TaxRate = tax.CompositeRate;
                stock.TaxType = tax.TaxType;
                stock.TaxId = tax.Id;
                stock.HSNCode = string.IsNullOrWhiteSpace(hsnCode) ? stock.HSNCode : hsnCode;
                stock.Unit = unit;
            }

            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "PurchaseIn",
                QuantityIn = requestItem.Quantity,
                CostPrice = requestItem.CostPrice,
                MRP = requestItem.Mrp,
                TaxRate = tax.CompositeRate,
                HSNCode = hsnCode,
                SourceType = "PurchaseInvoiceImport",
                SourceId = invoiceId,
                SourceNumber = invoiceNumber,
                Remarks = $"Purchase inward from supplier invoice import dated {(request.InwardDate.HasValue ? request.InwardDate.Value.Date.ToString("yyyy-MM-dd") : "today")}",
                OnDate = request.InwardDate?.Date ?? request.SupplierInvoiceDate?.Date ?? DateTime.Today,
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
            }, cancellationToken);

            product.MRP = requestItem.Mrp;
            product.TaxRate = tax.CompositeRate;
            product.TaxType = tax.TaxType;
            if (!string.IsNullOrWhiteSpace(hsnCode)) product.HSNCode = hsnCode;
            product.Unit = unit;
            if (requestItem.ProductType.HasValue) product.ProductType = requestItem.ProductType.Value;
            if (requestItem.ProductGroup.HasValue) product.ProductGroup = requestItem.ProductGroup.Value;

            grossMrp += lineMrp;
            discountAmount += lineDiscount;
            taxableAmount += taxable;
            taxAmount += taxValue;
            cgstAmount += split.Cgst;
            sgstAmount += split.Sgst;
            igstAmount += split.Igst;
            totalQuantity += requestItem.Quantity;
        }

        var netAmount = taxableAmount + taxAmount + request.FrightAmount;
        var billAmount = Math.Round(netAmount, 0);
        var paidAmount = Math.Min(Math.Max(request.PaidAmount, 0), billAmount);
        var invoice = new PurchaseInvoice
        {
            Id = invoiceId,
            InvoiceNumber = invoiceNumber,
            InwardNumber = inwardNumber,
            InwardDate = inwardDate,
            OnDate = invoiceDate,
            InvoiceType = InvoiceType.Regular,
            InvoiceStatus = paidAmount <= 0 ? InvoiceStatus.Pending : paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            CGSTAmount = cgstAmount,
            SGSTAmount = sgstAmount,
            IGSTAmount = igstAmount,
            InterState = interState,
            NetAmount = taxableAmount + taxAmount,
            RoundOff = billAmount - netAmount,
            BillAmount = billAmount,
            Quantity = totalQuantity,
            ItemCount = invoiceItems.Count,
            PaymentMode = paidAmount > 0 ? request.PaymentMode : null,
            VendorId = vendor.Id,
            VendorName = vendor.Name,
            VendorGSTIN = vendor.GSTIN,
            FrightAmount = request.FrightAmount,
            SupplierInvoiceDate = request.SupplierInvoiceDate?.Date,
            DueDate = request.DueDate?.Date ?? invoiceDate.AddDays(45),
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            CompanyId = request.CompanyId,
            OriginalInvoiceId = request.OriginalInvoiceId
        };
        if (!WorkspaceScope.CanWrite(invoice, context, out var invoiceScopeMessage)) throw new InvalidOperationException(invoiceScopeMessage ?? "Selected company is outside your access scope.");

        db.PurchaseInvoices.Add(invoice);
        db.PurchaseInvoiceItems.AddRange(invoiceItems);
        if (paidAmount > 0)
        {
            db.PurchasePayments.Add(new PurchasePayment
            {
                PurchaseInvoiceId = invoice.Id,
                VendorId = vendor.Id,
                OnDate = inwardDate,
                Amount = paidAmount,
                PaymentMode = request.PaymentMode,
                BankAccountId = request.BankAccountId,
                ReferenceNumber = invoice.InvoiceNumber,
                Remarks = "Purchase inward payment from supplier invoice import",
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
            });
        }

        vendor.BillCount += 1;
        vendor.BillAmount += billAmount;
        vendor.Paid += paidAmount;
        await accounting.PostPurchaseInvoiceAsync(invoice, vendor, paidAmount, request.StoreGroupId, request.StoreId, request.BankAccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PurchaseInwardResponse(invoice.Id, invoice.InvoiceNumber, invoice.InwardNumber, vendor.Id, invoice.BillAmount, paidAmount, invoice.ItemCount, invoice.Quantity, vendorValidation?.Alerts ?? Array.Empty<string>());
    }

    private async Task<Vendor> GetOrCreateVendorAsync(PurchaseInwardRequest request, PartyGstinValidationResponse? validation, CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(request.VendorMobileNumber) ? "NA" : request.VendorMobileNumber.Trim();
        var name = request.VendorName.Trim();
        var gstin = GstinLookupService.NormalizeGstin(request.VendorGstin);
        Vendor? vendor = null;
        if (request.VendorId.HasValue && request.VendorId.Value != Guid.Empty)
        {
            vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == request.VendorId.Value && item.CompanyId == request.CompanyId, cancellationToken);
        }
        vendor ??= await db.Vendors.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && ((!string.IsNullOrWhiteSpace(gstin) && item.GSTIN == gstin) || item.MobileNumber == mobile || item.Name == name), cancellationToken);
        if (vendor is not null)
        {
            if (validation is not null)
            {
                gstinLookup.ApplyVerification(vendor, validation);
                if (!string.IsNullOrWhiteSpace(validation.Lookup.PrincipalAddress) && (string.IsNullOrWhiteSpace(vendor.Address) || vendor.Address == "Dumka")) vendor.Address = validation.Lookup.PrincipalAddress;
            }
            else if (!string.IsNullOrWhiteSpace(gstin)) vendor.GSTIN = gstin;
            return vendor;
        }
        vendor = new Vendor
        {
            Name = name,
            Address = validation?.Lookup.PrincipalAddress ?? "Dumka",
            City = "Dumka",
            ZipCode = "814101",
            MobileNumber = mobile,
            GSTIN = string.IsNullOrWhiteSpace(gstin) ? null : gstin,
            Active = true,
            CompanyId = request.CompanyId
        };
        if (validation is not null) gstinLookup.ApplyVerification(vendor, validation);
        db.Vendors.Add(vendor);
        return vendor;
    }

    private async Task<Product?> GetOrCreateProductAsync(PurchaseInwardRequest request, PurchaseInwardItemRequest requestItem, CancellationToken cancellationToken)
    {
        if (requestItem.ProductId.HasValue)
        {
            return await db.Products.FirstOrDefaultAsync(item => item.Id == requestItem.ProductId.Value, cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(requestItem.ProductName) || string.IsNullOrWhiteSpace(requestItem.Barcode)) return null;
        var barcode = requestItem.Barcode.Trim();
        var existing = await db.Products.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && item.Barcode == barcode, cancellationToken);
        if (existing is not null) return existing;
        var categoryId = requestItem.ProductCategoryId ?? await db.ProductCategories.Where(item => item.CompanyId == request.CompanyId).Select(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        var subCategoryId = requestItem.ProductSubCategoryId ?? await db.ProductSubCategories.Where(item => item.CompanyId == request.CompanyId).Select(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        var tax = requestItem.TaxId.HasValue ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == requestItem.TaxId.Value, cancellationToken) : await db.Taxes.FirstOrDefaultAsync(cancellationToken);
        if (categoryId == Guid.Empty || subCategoryId == Guid.Empty || tax is null) return null;
        var product = new Product
        {
            Name = requestItem.ProductName.Trim(),
            Barcode = barcode,
            MRP = requestItem.Mrp,
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            HSNCode = string.IsNullOrWhiteSpace(requestItem.HsnCode) ? null : requestItem.HsnCode.Trim(),
            Unit = requestItem.ProductUnit ?? Unit.Pcs,
            ProductType = requestItem.ProductType ?? ProductType.Apparels,
            ProductGroup = requestItem.ProductGroup ?? ProductGroup.Shirting,
            ProductCategoryId = categoryId,
            ProductSubCategoryId = subCategoryId,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId
        };
        db.Products.Add(product);
        return product;
    }

    private static void RecalculateBatchTotals(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines)
    {
        batch.TaxableAmount = Round(lines.Sum(item => item.TaxableAmount));
        batch.CgstAmount = Round(lines.Sum(item => item.CgstAmount));
        batch.SgstAmount = Round(lines.Sum(item => item.SgstAmount));
        batch.IgstAmount = Round(lines.Sum(item => item.IgstAmount));
        var lineTotal = Round(lines.Sum(item => item.LineTotal));
        if (batch.BillAmount <= 0)
        {
            batch.BillAmount = Math.Round(lineTotal + batch.FreightAmount + batch.RoundOff, 0);
        }
    }

    private static void RecalculateLine(PurchaseInvoiceImportLine line)
    {
        line.GstPriceMode = NormalizeGstPriceMode(line.GstPriceMode);
        var lineDiscount = line.LineDiscount > 0 ? line.LineDiscount : line.UnitDiscount * line.Quantity;
        line.LineDiscount = Round(lineDiscount);
        var gross = Math.Max((line.CostPrice * line.Quantity) - line.LineDiscount, 0);
        var rateFactor = 1 + (line.TaxRate / 100m);
        if (string.Equals(line.GstPriceMode, "Exclusive", StringComparison.OrdinalIgnoreCase))
        {
            line.TaxableAmount = Round(gross);
            line.TaxAmount = Round(line.TaxableAmount * (line.TaxRate / 100m));
        }
        else
        {
            line.TaxableAmount = Round(rateFactor <= 0 ? gross : gross / rateFactor);
            line.TaxAmount = Round(line.TaxableAmount * (line.TaxRate / 100m));
        }
        line.CgstAmount = Round(line.TaxAmount / 2m);
        line.SgstAmount = line.TaxAmount - line.CgstAmount;
        line.IgstAmount = 0;
        line.LineTotal = Round(line.TaxableAmount + line.TaxAmount);
    }


    private static PurchaseInvoiceImportPostingReportDto BuildPostingReport(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines, IReadOnlyList<string> warnings)
    {
        var activeLines = lines.Where(item => !item.Ignored).ToList();
        var reconciliation = BuildReconciliation(batch, activeLines);
        var duplicateBarcodeGroupCount = activeLines
            .Where(item => !string.IsNullOrWhiteSpace(item.BarcodeFinal))
            .GroupBy(item => item.BarcodeFinal!.Trim().ToUpperInvariant())
            .Count(group => group.Count() > 1);
        var missingBarcodeCount = activeLines.Count(item => string.IsNullOrWhiteSpace(item.BarcodeFinal) && !item.ProductId.HasValue);
        var missingTaxCount = activeLines.Count(item => !item.TaxId.HasValue && item.TaxRate <= 0);
        var missingDefaultsCount = activeLines.Count(item => !item.ProductId.HasValue && (string.IsNullOrWhiteSpace(item.ProductNameFinal) || item.Mrp <= 0 || !item.ProductCategoryId.HasValue || !item.ProductSubCategoryId.HasValue));
        var canPost = warnings.Count == 0 && !batch.PostedPurchaseInvoiceId.HasValue && !string.Equals(batch.Status, "Rejected", StringComparison.OrdinalIgnoreCase);
        var checklist = new List<PurchaseInvoiceImportChecklistItemDto>
        {
            new("vendor", "Vendor resolved", string.IsNullOrWhiteSpace(batch.VendorNameFinal) ? "Block" : "Pass", string.IsNullOrWhiteSpace(batch.VendorNameFinal) ? "Vendor name is required." : batch.VendorNameFinal!),
            new("invoice-number", "Supplier invoice number", string.IsNullOrWhiteSpace(batch.SupplierInvoiceNumber) ? "Block" : "Pass", string.IsNullOrWhiteSpace(batch.SupplierInvoiceNumber) ? "Enter supplier invoice number." : batch.SupplierInvoiceNumber!),
            new("invoice-date", "Supplier invoice date", batch.SupplierInvoiceDate.HasValue ? "Pass" : "Warn", batch.SupplierInvoiceDate.HasValue ? batch.SupplierInvoiceDate.Value.ToString("dd-MMM-yyyy") : "Invoice date is recommended before posting."),
            new("lines", "Active item rows", activeLines.Count == 0 ? "Block" : "Pass", activeLines.Count == 0 ? "At least one item row is required." : $"{activeLines.Count} active row(s), {lines.Count(item => item.Ignored)} ignored row(s)."),
            new("barcode", "Barcode readiness", missingBarcodeCount > 0 || duplicateBarcodeGroupCount > 0 ? "Block" : "Pass", missingBarcodeCount > 0 || duplicateBarcodeGroupCount > 0 ? $"{missingBarcodeCount} missing barcode row(s), {duplicateBarcodeGroupCount} duplicate group(s)." : "No missing/duplicate barcodes."),
            new("tax", "GST/tax readiness", missingTaxCount > 0 ? "Block" : "Pass", missingTaxCount > 0 ? $"{missingTaxCount} row(s) need GST/tax." : "All active rows have GST/tax."),
            new("product-defaults", "New product defaults", missingDefaultsCount > 0 ? "Block" : "Pass", missingDefaultsCount > 0 ? $"{missingDefaultsCount} new-product row(s) need MRP/category/subcategory/name." : "New product defaults are complete."),
            new("discount", "Supplier discount", batch.DiscountAmount > 0 && Math.Abs(reconciliation.HeaderDiscountDifference) > 1 ? "Block" : "Pass", batch.DiscountAmount > 0 ? $"Header discount {batch.DiscountAmount:0.##}; line discount {reconciliation.LineDiscountTotal:0.##}; difference {reconciliation.HeaderDiscountDifference:0.##}." : "No header discount to reconcile."),
            new("total", "Grand total match", batch.BillAmount > 0 && Math.Abs(reconciliation.BillDifference) > 1 ? "Block" : "Pass", batch.BillAmount > 0 ? $"Scanned {batch.BillAmount:0.##}; calculated {reconciliation.CalculatedGrandTotal:0.##}; difference {reconciliation.BillDifference:0.##}." : "No scanned bill total was captured."),
            new("payment", "Supplier payment", batch.PaidAmount > 0 && batch.PaymentMode != PaymentMode.Cash && !batch.BankAccountId.HasValue ? "Block" : "Pass", batch.PaidAmount > 0 ? $"Paid {batch.PaidAmount:0.##} via {batch.PaymentMode}." : "Unpaid/due inward."),
            new("status", "Draft status", canPost ? "Pass" : warnings.Count > 0 ? "Block" : "Info", canPost ? "Draft can be posted." : string.Join(" | ", warnings.Take(3)))
        };

        return new PurchaseInvoiceImportPostingReportDto(
            batch.Id,
            SafeStatus(batch.Status),
            canPost,
            activeLines.Count,
            lines.Count(item => item.Ignored),
            activeLines.Count(item => !item.ProductId.HasValue),
            activeLines.Count(item => item.ProductId.HasValue),
            missingBarcodeCount,
            duplicateBarcodeGroupCount,
            missingTaxCount,
            missingDefaultsCount,
            warnings.Count,
            ToReconciliationDto(batch, reconciliation),
            checklist,
            warnings);
    }

    private static PurchaseInvoiceImportReconciliationDto ToReconciliationDto(PurchaseInvoiceImportBatch batch, ImportReconciliation reconciliation)
        => new(
            reconciliation.GrossTotal,
            reconciliation.LineDiscountTotal,
            reconciliation.GrossMinusDiscount,
            reconciliation.TaxableTotal,
            reconciliation.TaxTotal,
            reconciliation.LineTotal,
            reconciliation.FreightAmount,
            reconciliation.RoundOff,
            reconciliation.CalculatedGrandTotal,
            batch.BillAmount,
            reconciliation.BillDifference,
            batch.DiscountAmount,
            reconciliation.HeaderDiscountDifference);

    private IReadOnlyList<string> ValidateBatch(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines)
    {
        var warnings = new List<string>();
        if (batch.PostedPurchaseInvoiceId.HasValue) warnings.Add("Draft is already posted.");
        if (string.Equals(batch.Status, "Rejected", StringComparison.OrdinalIgnoreCase)) warnings.Add("Draft is rejected.");
        if (string.IsNullOrWhiteSpace(batch.VendorNameFinal)) warnings.Add("Vendor name is required.");
        if (string.IsNullOrWhiteSpace(batch.SupplierInvoiceNumber)) warnings.Add("Supplier invoice number is required.");
        if (batch.DuplicatePurchaseInvoiceId.HasValue && string.IsNullOrWhiteSpace(batch.DuplicateOverrideReason)) warnings.Add("Possible duplicate supplier invoice found. Verify or use admin override before posting.");
        var activeLines = lines.Where(item => !item.Ignored).ToList();
        if (activeLines.Count == 0) warnings.Add("At least one item line is required.");
        foreach (var line in activeLines)
        {
            if (line.Quantity <= 0) warnings.Add($"Line {line.LineNumber}: quantity is required.");
            if (line.CostPrice <= 0) warnings.Add($"Line {line.LineNumber}: cost price is required before posting.");
            if (line.Mrp <= 0 && !line.ProductId.HasValue) warnings.Add($"Line {line.LineNumber}: MRP is required for new product creation.");
            if (string.IsNullOrWhiteSpace(line.ProductNameFinal)) warnings.Add($"Line {line.LineNumber}: product name is required.");
            if (string.IsNullOrWhiteSpace(line.BarcodeFinal) && !line.ProductId.HasValue) warnings.Add($"Line {line.LineNumber}: barcode is missing. Enter barcode or product mapping.");
            if (!line.TaxId.HasValue && line.TaxRate <= 0) warnings.Add($"Line {line.LineNumber}: GST/tax is required.");
            if (!line.ProductId.HasValue && !line.ProductCategoryId.HasValue) warnings.Add($"Line {line.LineNumber}: category is required for new product creation.");
            if (!line.ProductId.HasValue && !line.ProductSubCategoryId.HasValue) warnings.Add($"Line {line.LineNumber}: sub category is required for new product creation.");

            var gross = Round(line.CostPrice * line.Quantity);
            var discount = Round(line.LineDiscount > 0 ? line.LineDiscount : line.UnitDiscount * line.Quantity);
            var netBeforeTax = Math.Max(gross - discount, 0);
            var expectedTaxable = string.Equals(NormalizeGstPriceMode(line.GstPriceMode), "Exclusive", StringComparison.OrdinalIgnoreCase)
                ? netBeforeTax
                : Round(netBeforeTax / (1 + (line.TaxRate / 100m)));
            var expectedTax = Round(expectedTaxable * (line.TaxRate / 100m));
            var expectedLineTotal = Round(expectedTaxable + expectedTax);
            if (line.TaxRate > 0 && Math.Abs(expectedLineTotal - line.LineTotal) > 1)
            {
                warnings.Add($"Line {line.LineNumber}: scanned/calculated line total differs. Expected {expectedLineTotal:0.##}, draft has {line.LineTotal:0.##}. Review discount/GST mode.");
            }
        }
        var duplicateBarcodeGroups = activeLines
            .Where(item => !string.IsNullOrWhiteSpace(item.BarcodeFinal))
            .GroupBy(item => item.BarcodeFinal!.Trim().ToUpperInvariant())
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key} on lines {string.Join(", ", group.Select(item => item.LineNumber).OrderBy(item => item))}")
            .Take(5)
            .ToList();
        foreach (var duplicate in duplicateBarcodeGroups)
        {
            warnings.Add($"Duplicate barcode in draft: {duplicate}. Generate/review barcodes before posting.");
        }

        var reconciliation = BuildReconciliation(batch, activeLines);
        if (batch.BillAmount > 0 && Math.Abs(reconciliation.BillDifference) > 1)
        {
            warnings.Add($"Bill amount mismatch. Draft bill is {batch.BillAmount:0.##}, calculated total is {reconciliation.CalculatedGrandTotal:0.##}. Difference {reconciliation.BillDifference:0.##}.");
        }
        if (Math.Abs(reconciliation.GrossMinusDiscount - reconciliation.TaxableTotal) > 1)
        {
            warnings.Add($"Taxable mismatch. Gross {reconciliation.GrossTotal:0.##} - discount {reconciliation.LineDiscountTotal:0.##} = {reconciliation.GrossMinusDiscount:0.##}, but draft taxable total is {reconciliation.TaxableTotal:0.##}.");
        }
        if (batch.DiscountAmount > 0 && Math.Abs(reconciliation.HeaderDiscountDifference) > 1)
        {
            warnings.Add($"Header discount {batch.DiscountAmount:0.##} does not match distributed line discount {reconciliation.LineDiscountTotal:0.##}. Distribute/review supplier discount before posting.");
        }
        if (batch.PaidAmount > batch.BillAmount && batch.BillAmount > 0) warnings.Add("Paid amount cannot be greater than bill amount.");
        if (batch.PaidAmount > 0 && batch.PaymentMode != PaymentMode.Cash && !batch.BankAccountId.HasValue) warnings.Add("Bank account is required for non-cash supplier payment.");
        return warnings.Distinct().Take(80).ToList();
    }

    private sealed record ImportReconciliation(
        decimal GrossTotal,
        decimal LineDiscountTotal,
        decimal GrossMinusDiscount,
        decimal TaxableTotal,
        decimal TaxTotal,
        decimal LineTotal,
        decimal FreightAmount,
        decimal RoundOff,
        decimal CalculatedGrandTotal,
        decimal BillDifference,
        decimal HeaderDiscountDifference);

    private static ImportReconciliation BuildReconciliation(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> activeLines)
    {
        var grossTotal = Round(activeLines.Sum(item => item.CostPrice * item.Quantity));
        var lineDiscountTotal = Round(activeLines.Sum(item => item.LineDiscount > 0 ? item.LineDiscount : item.UnitDiscount * item.Quantity));
        var grossMinusDiscount = Round(Math.Max(grossTotal - lineDiscountTotal, 0));
        var taxableTotal = Round(activeLines.Sum(item => item.TaxableAmount));
        var taxTotal = Round(activeLines.Sum(item => item.TaxAmount));
        var lineTotal = Round(activeLines.Sum(item => item.LineTotal));
        var calculatedGrand = Math.Round(lineTotal + batch.FreightAmount + batch.RoundOff, 0);
        var billDifference = batch.BillAmount > 0 ? Round(batch.BillAmount - calculatedGrand) : 0;
        var headerDiscountDifference = batch.DiscountAmount > 0 ? Round(batch.DiscountAmount - lineDiscountTotal) : 0;
        return new ImportReconciliation(
            grossTotal,
            lineDiscountTotal,
            grossMinusDiscount,
            taxableTotal,
            taxTotal,
            lineTotal,
            batch.FreightAmount,
            batch.RoundOff,
            calculatedGrand,
            billDifference,
            headerDiscountDifference);
    }

    private async Task<Vendor?> MatchVendorAsync(Guid companyId, string? gstin, string? vendorName, CancellationToken cancellationToken)
    {
        var normalizedGstin = GstinLookupService.NormalizeGstin(gstin);
        if (!string.IsNullOrWhiteSpace(normalizedGstin))
        {
            var byGstin = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(item => item.CompanyId == companyId && item.GSTIN == normalizedGstin, cancellationToken);
            if (byGstin is not null) return byGstin;
        }
        if (!string.IsNullOrWhiteSpace(vendorName))
        {
            var name = vendorName.Trim().ToLowerInvariant();
            return await db.Vendors.AsNoTracking().Where(item => item.CompanyId == companyId && item.Name.ToLower().Contains(name)).OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        }
        return null;
    }

    private async Task<Product?> MatchProductAsync(Guid companyId, string? barcode, string? name, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var code = barcode.Trim();
            var byBarcode = await db.Products.AsNoTracking().FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Barcode == code, cancellationToken);
            if (byBarcode is not null) return byBarcode;
        }
        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToLowerInvariant();
            return await db.Products.AsNoTracking().Where(item => item.CompanyId == companyId && item.Name.ToLower().Contains(term)).OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        }
        return null;
    }

    private async Task<Tax?> MatchTaxAsync(decimal rate, CancellationToken cancellationToken)
    {
        var taxes = await db.Taxes.AsNoTracking().ToListAsync(cancellationToken);
        return taxes.OrderBy(item => Math.Abs(item.CompositeRate - rate)).FirstOrDefault();
    }

    private async Task RefreshDuplicateWarningAsync(PurchaseInvoiceImportBatch batch, CancellationToken cancellationToken)
    {
        var duplicate = await FindDuplicatePurchaseInvoiceAsync(batch.CompanyId, batch.StoreId, batch.VendorId, batch.VendorGstinFinal, batch.SupplierInvoiceNumber, cancellationToken);
        if (duplicate != batch.DuplicatePurchaseInvoiceId)
        {
            batch.DuplicatePurchaseInvoiceId = duplicate;
            batch.DuplicateOverrideReason = null;
            batch.DuplicateOverrideBy = null;
            batch.DuplicateOverrideAt = null;
        }
        if (!duplicate.HasValue)
        {
            batch.DuplicateOverrideReason = null;
            batch.DuplicateOverrideBy = null;
            batch.DuplicateOverrideAt = null;
        }
    }

    private async Task UpsertImportFileAsync(PurchaseInvoiceImportBatch batch, string fileKind, string originalFileName, string storedFilePath, string contentType, CancellationToken cancellationToken)
    {
        if (!File.Exists(storedFilePath))
        {
            return;
        }

        var file = await db.PurchaseInvoiceImportFiles
            .FirstOrDefaultAsync(item => item.BatchId == batch.Id && item.FileKind == fileKind && !item.Deleted, cancellationToken);
        if (file is null)
        {
            file = new PurchaseInvoiceImportFile
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                FileKind = fileKind,
                CompanyId = batch.CompanyId,
                StoreGroupId = batch.StoreGroupId,
                StoreId = batch.StoreId
            };
            db.PurchaseInvoiceImportFiles.Add(file);
        }

        file.OriginalFileName = originalFileName;
        file.StoredFilePath = storedFilePath;
        file.ContentType = contentType;
        file.FileSizeBytes = new FileInfo(storedFilePath).Length;
        file.Sha256Hash = await Sha256Async(storedFilePath, cancellationToken);
        file.UpdatedAt = DateTime.Now;
    }

    private async Task<Guid?> FindDuplicatePurchaseInvoiceAsync(Guid companyId, Guid storeId, Guid? vendorId, string? vendorGstin, string? invoiceNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return null;
        var normalizedInvoice = invoiceNumber.Trim().ToLowerInvariant();
        var normalizedGstin = GstinLookupService.NormalizeGstin(vendorGstin);
        var query = db.PurchaseInvoices.AsNoTracking().Where(item => item.CompanyId == companyId && item.StoreId == storeId && item.InvoiceNumber.ToLower() == normalizedInvoice);
        if (vendorId.HasValue) query = query.Where(item => item.VendorId == vendorId.Value);
        else if (!string.IsNullOrWhiteSpace(normalizedGstin)) query = query.Where(item => item.VendorGSTIN == normalizedGstin);
        return await query.Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
    }

    private ParsedInvoice ParseInvoiceText(string text, InvoiceParserProfile? profile = null)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new ParsedInvoice(null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, "auto", "No text was available for parsing.", Array.Empty<ParsedInvoiceLine>(), Array.Empty<ParserLineDecision>());
        }
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var gstin = GstinRegex.Match(normalized) is { Success: true } gstMatch ? gstMatch.Value.ToUpperInvariant() : null;
        var invoiceNo = ExtractInvoiceNumber(normalized);
        var invoiceDate = ExtractInvoiceDate(normalized);
        var vendorName = GuessVendorName(lines);
        var vendorMobile = MobileRegex.Match(normalized) is { Success: true } mobileMatch ? mobileMatch.Groups["mobile"].Value.Trim() : null;
        var vendorAddress = GuessVendorAddress(lines);
        var billAmount = AmountRegex.Matches(normalized).Cast<Match>().Select(match => ParseMoney(match.Groups["amount"].Value)).Where(value => value > 0).DefaultIfEmpty(0).Max();
        var freight = ExtractBestAmount(normalized, FreightRegex);
        var discount = ExtractBestAmount(normalized, DiscountRegex);
        var roundOff = ExtractBestAmount(normalized, RoundOffRegex);
        decimal cgst = 0, sgst = 0, igst = 0;
        foreach (Match match in ComponentTaxRegex.Matches(normalized))
        {
            var amount = ParseMoney(match.Groups["amount"].Value);
            switch (match.Groups["label"].Value.ToLowerInvariant())
            {
                case "cgst": cgst += amount; break;
                case "sgst": sgst += amount; break;
                case "igst": igst += amount; break;
            }
        }
        var defaultTaxRate = GuessDefaultTaxRate(normalized);
        var parserSelection = SelectParserTemplate(normalized, lines, profile);
        var lineGuess = GuessLines(lines, defaultTaxRate, profile, parserSelection.Key);
        var parsedLines = lineGuess.Lines;
        var taxable = parsedLines.Sum(item => item.TaxableAmount);
        if (billAmount <= 0 && parsedLines.Count > 0)
        {
            billAmount = Math.Round(parsedLines.Sum(item => item.LineTotal) + freight + roundOff, 0);
        }
        var confidence = 20m;
        if (!string.IsNullOrWhiteSpace(gstin)) confidence += 20m;
        if (!string.IsNullOrWhiteSpace(invoiceNo)) confidence += 15m;
        if (invoiceDate.HasValue) confidence += 15m;
        if (billAmount > 0) confidence += 15m;
        if (parsedLines.Count > 0) confidence += 15m;
        return new ParsedInvoice(vendorName, gstin, vendorMobile, vendorAddress, invoiceNo, invoiceDate, Round(taxable), Round(cgst), Round(sgst), Round(igst), Round(freight), Round(discount), Round(roundOff), Round(billAmount), Math.Min(confidence, 100m), parserSelection.Key, parserSelection.Reason, parsedLines, lineGuess.Decisions);
    }

    private static ParserTemplateSelection SelectParserTemplate(string normalized, IReadOnlyList<string> lines, InvoiceParserProfile? profile)
    {
        var preferred = NormalizeParserTemplateKey(profile?.PreferredParserTemplate);
        if (!string.Equals(preferred, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return new ParserTemplateSelection(preferred, $"Vendor profile override selected parser template '{preferred}'.");
        }

        var lower = normalized.ToLowerInvariant();
        var joinedHeader = string.Join(" ", lines.Take(80)).ToLowerInvariant();
        if ((joinedHeader.Contains("art no") || joinedHeader.Contains("brand")) && joinedHeader.Contains("size") && joinedHeader.Contains("hsn"))
        {
            return new ParserTemplateSelection("garment-column", "Auto-detected garment article/brand/size column layout.");
        }

        if ((lower.Contains("tally") || lower.Contains("tax invoice")) && lower.Contains("hsn") && (lower.Contains("qty") || lower.Contains("quantity")) && lower.Contains("rate"))
        {
            return new ParserTemplateSelection("tally-prime", "Auto-detected common Tally/GST item table layout.");
        }

        return new ParserTemplateSelection("auto", "Auto-detect mode used; no vendor override or strong template signature was found.");
    }

    private static string NormalizeParserTemplateKey(string? value)
    {
        var key = (value ?? "auto").Trim().ToLowerInvariant();
        key = key switch
        {
            "tally" or "tallyprime" or "tally-prime-gst" => "tally-prime",
            "garment" or "sk-apparels" or "column" or "apparel-column" => "garment-column",
            "generic" or "conservative" => "generic-conservative",
            _ => key
        };
        return ParserTemplates.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)) ? key : "auto";
    }

    private static bool IsValidParserTemplateKey(string? value)
    {
        return ParserTemplates.Any(item => string.Equals(item.Key, NormalizeParserTemplateKey(value), StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractInvoiceNumber(string normalized)
    {
        var patterns = new[]
        {
            @"\binvoice\s*(?:no\.?|number|#)\s*[:\-]?\s*(?<number>[A-Z0-9][A-Z0-9\-/]{2,40})",
            @"\binv\.?\s*(?:no\.?|number|#)\s*[:\-]?\s*(?<number>[A-Z0-9][A-Z0-9\-/]{2,40})",
            @"\btax\s*invoice\s*(?:no\.?|number|#)\s*[:\-]?\s*(?<number>[A-Z0-9][A-Z0-9\-/]{2,40})"
        };
        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(normalized, pattern, RegexOptions.IgnoreCase))
            {
                var value = CleanInvoiceNumber(match.Groups["number"].Value);
                if (IsGoodInvoiceNumber(value)) return value;
            }
        }

        return null;
    }

    private static string CleanInvoiceNumber(string value)
    {
        value = Regex.Replace(value.Trim(), @"\s+", string.Empty);
        value = value.Trim(':', '-', '.', ',', ';', '|');
        return value;
    }

    private static bool IsGoodInvoiceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 3) return false;
        var lower = value.ToLowerInvariant();
        var bad = new[] { "original", "recipient", "duplicate", "transport", "carrier", "supplier", "date", "tax", "invoice" };
        if (bad.Any(item => lower.Contains(item))) return false;
        return value.Any(char.IsDigit) && Regex.IsMatch(value, @"^[A-Z0-9][A-Z0-9\-/]{2,40}$", RegexOptions.IgnoreCase);
    }

    private static DateTime? ExtractInvoiceDate(string normalized)
    {
        foreach (Match match in DateRegex.Matches(normalized))
        {
            if (TryParseDate(match.Groups["date"].Value, out var parsed)) return parsed;
        }

        var invoiceLine = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.Contains("invoice", StringComparison.OrdinalIgnoreCase) && line.Contains("date", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(invoiceLine))
        {
            var dateMatch = Regex.Match(invoiceLine, @"(?<date>\d{1,2}[\-/\.]\d{1,2}[\-/\.]\d{2,4}|\d{4}[\-/\.]\d{1,2}[\-/\.]\d{1,2})");
            if (dateMatch.Success && TryParseDate(dateMatch.Groups["date"].Value, out var parsed)) return parsed;
        }

        return null;
    }

    private LineGuessResult GuessLines(IReadOnlyList<string> lines, decimal defaultTaxRate, InvoiceParserProfile? profile, string parserTemplate)
    {
        var result = new List<ParsedInvoiceLine>();
        var decisions = new List<ParserLineDecision>
        {
            new(0, parserTemplate, "Template", $"Parser template used: {parserTemplate}.")
        };
        var candidates = SelectItemTableLines(lines, parserTemplate);
        foreach (var candidate in candidates)
        {
            var clean = Regex.Replace(candidate.Text, @"\s+", " ").Trim();
            if (clean.Length < 12)
            {
                decisions.Add(new ParserLineDecision(candidate.SourceLineNumber, clean, "Ignored", "Line is too short to be a product row."));
                continue;
            }
            if (IsSummaryLine(clean) || IsNonItemLine(clean))
            {
                decisions.Add(new ParserLineDecision(candidate.SourceLineNumber, clean, "Ignored", "Header, vendor, tax summary, payment, bank, or footer row."));
                continue;
            }
            if (IsLearnedIgnoredLine(clean, profile))
            {
                decisions.Add(new ParserLineDecision(candidate.SourceLineNumber, clean, "Ignored", "Skipped by vendor learning profile."));
                continue;
            }

            var parsed = TryParseKnownColumnLine(clean, defaultTaxRate) ?? TryParseStructuredLine(clean, defaultTaxRate) ?? TryParseTailNumberLine(clean, defaultTaxRate);
            if (parsed is null)
            {
                decisions.Add(new ParserLineDecision(candidate.SourceLineNumber, clean, "Ignored", "Could not validate item amount pattern. Expected item text plus qty/rate/amount."));
                continue;
            }
            var learned = FindLearnedAlias(profile, parsed.Name);
            if (learned is not null)
            {
                parsed = parsed with
                {
                    Name = string.IsNullOrWhiteSpace(learned.ProductName) ? parsed.Name : learned.ProductName,
                    Barcode = string.IsNullOrWhiteSpace(learned.Barcode) ? parsed.Barcode : learned.Barcode,
                    HsnCode = string.IsNullOrWhiteSpace(learned.HsnCode) ? parsed.HsnCode : learned.HsnCode,
                    TaxRate = learned.TaxRate > 0 ? learned.TaxRate : parsed.TaxRate,
                    ConfidenceScore = Math.Min(95, parsed.ConfidenceScore + 15),
                    DetectionReason = parsed.DetectionReason + " Vendor learning alias applied."
                };
            }
            decisions.Add(new ParserLineDecision(candidate.SourceLineNumber, clean, "Detected", parsed.DetectionReason));
            result.Add(parsed);
        }
        return new LineGuessResult(result.Take(100).ToList(), decisions.Take(500).ToList());
    }

    private static IReadOnlyList<ItemLineCandidate> SelectItemTableLines(IReadOnlyList<string> lines, string parserTemplate)
    {
        var cleaned = lines
            .Select((line, index) => new ItemLineCandidate(index + 1, Regex.Replace(line, @"\s+", " ").Trim(), "Raw"))
            .Where(line => line.Text.Length > 0)
            .ToList();
        var selected = new List<ItemLineCandidate>();
        var conservativeGenericOnly = string.Equals(parserTemplate, "generic-conservative", StringComparison.OrdinalIgnoreCase);
        var activeTable = false;
        var afterHeaderCount = 0;

        if (!conservativeGenericOnly)
        {
        for (var index = 0; index < cleaned.Count; index++)
        {
            var item = cleaned[index];
            var line = item.Text;
            var skipHeaderLines = 0;
            if (LooksLikeItemTableHeader(line) || LooksLikeMultiLineItemHeader(cleaned, index, out skipHeaderLines))
            {
                activeTable = true;
                afterHeaderCount = 0;
                if (skipHeaderLines > 0)
                {
                    index += skipHeaderLines - 1;
                }
                continue;
            }

            if (!activeTable) continue;
            if (line.Length < 3) continue;
            if (LooksLikeRepeatedPageHeader(line) || LooksLikeHeaderOnlyLine(line)) continue;
            if (LooksLikeFooterStart(line) || IsSummaryLine(line))
            {
                // Once an item table has started, a total/tax/bank/footer row normally means the table ended.
                activeTable = false;
                continue;
            }

            afterHeaderCount++;
            selected.Add(item with { Section = "Detected table after item header" });
            if (afterHeaderCount >= 220) activeTable = false;
        }
        }

        if (selected.Count > 0)
        {
            return MergeWrappedItemLines(GroupSerialItemRows(selected));
        }

        // Conservative fallback: only take lines that already look like real product rows.
        var fallback = cleaned
            .Where(item => !LooksLikeHeaderOnlyLine(item.Text) && !LooksLikeFooterStart(item.Text) && !IsSummaryLine(item.Text) && LooksLikeStandaloneItemCandidate(item.Text))
            .Select(item => item with { Section = "No table header found; high-confidence standalone row" })
            .ToList();
        return MergeWrappedItemLines(GroupSerialItemRows(fallback));
    }

    private static bool LooksLikeMultiLineItemHeader(IReadOnlyList<ItemLineCandidate> cleaned, int startIndex, out int skipHeaderLines)
    {
        skipHeaderLines = 0;
        var windowItems = cleaned.Skip(startIndex).Take(18).ToList();
        if (windowItems.Count == 0) return false;
        var window = string.Join(" ", windowItems.Select(item => item.Text)).ToLowerInvariant();
        var score = 0;
        var mustHaveGroups = new[] { "description", "item", "particular", "hsn", "qty", "quantity", "rate", "gross", "amount", "taxable", "gst" };
        foreach (var word in mustHaveGroups)
        {
            if (Regex.IsMatch(window, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase)) score++;
        }
        var looksLikeHeader = score >= 5 && (window.Contains("sl no") || window.Contains("s no") || window.Contains("sr no") || window.Contains("item description") || window.Contains("particular"));
        if (!looksLikeHeader) return false;

        var firstRowOffset = windowItems.FindIndex(item => Regex.IsMatch(item.Text, @"^\s*\d{1,4}\s+"));
        skipHeaderLines = firstRowOffset > 0 ? firstRowOffset : Math.Min(8, windowItems.Count);
        return true;
    }

    private static IReadOnlyList<ItemLineCandidate> GroupSerialItemRows(IReadOnlyList<ItemLineCandidate> candidates)
    {
        var grouped = new List<ItemLineCandidate>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var current = candidates[index];
            if (!Regex.IsMatch(current.Text, @"^\s*\d{1,4}\s+"))
            {
                grouped.Add(current);
                continue;
            }

            var parts = new List<string> { current.Text };
            var sourceLine = current.SourceLineNumber;
            var section = current.Section + "; serial row grouped";
            while (index + 1 < candidates.Count)
            {
                var next = candidates[index + 1];
                if (Regex.IsMatch(next.Text, @"^\s*\d{1,4}\s+")) break;
                if (LooksLikeFooterStart(next.Text) || IsSummaryLine(next.Text)) break;
                if (LooksLikeItemTableHeader(next.Text)) break;
                parts.Add(next.Text);
                index++;
            }

            grouped.Add(new ItemLineCandidate(sourceLine, Regex.Replace(string.Join(" ", parts), @"\s+", " ").Trim(), section));
        }
        return grouped;
    }

    private static IReadOnlyList<ItemLineCandidate> MergeWrappedItemLines(IReadOnlyList<ItemLineCandidate> candidates)
    {
        var merged = new List<ItemLineCandidate>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var current = candidates[index];
            var currentNumbers = MoneyTokenRegex.Matches(current.Text).Count;
            if (currentNumbers < 3 && current.Text.Count(char.IsLetter) >= 3 && index + 1 < candidates.Count)
            {
                var next = candidates[index + 1];
                var nextNumbers = MoneyTokenRegex.Matches(next.Text).Count;
                if (nextNumbers >= 3 && !LooksLikeHeaderOnlyLine(next.Text) && !LooksLikeFooterStart(next.Text))
                {
                    merged.Add(new ItemLineCandidate(current.SourceLineNumber, (current.Text + " " + next.Text).Trim(), current.Section + "; wrapped name merged with next line"));
                    index++;
                    continue;
                }
            }
            merged.Add(current);
        }
        return merged;
    }

    private static bool LooksLikeStandaloneItemCandidate(string clean)
    {
        if (clean.Length < 12 || clean.Count(char.IsLetter) < 3) return false;
        if (GstinRegex.IsMatch(clean) || MobileRegex.IsMatch(clean)) return false;
        if (IsNonItemLine(clean)) return false;
        var numbers = MoneyTokenRegex.Matches(clean).Cast<Match>().Where(match => !IsPartOfGstin(clean, match.Index)).ToList();
        if (numbers.Count < 3) return false;
        var lastValues = numbers.TakeLast(3).Select(match => ParseMoney(match.Groups["value"].Value)).ToList();
        if (lastValues.Any(value => value <= 0)) return false;
        var qty = lastValues[0];
        var rate = lastValues[1];
        var amount = lastValues[2];
        return LooksLikePlausibleItemAmounts(qty, rate, amount, GuessLineTaxRate(clean, 0));
    }

    private static bool LooksLikeItemTableHeader(string clean)
    {
        var lower = clean.ToLowerInvariant();
        var score = 0;
        var headerWords = new[] { "description", "particular", "item", "product", "article", "style", "sku", "hsn", "qty", "quantity", "rate", "mrp", "amount", "taxable", "value" };
        foreach (var word in headerWords)
        {
            if (Regex.IsMatch(lower, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase)) score++;
        }
        return score >= 3 && (lower.Contains("qty") || lower.Contains("quantity") || lower.Contains("hsn") || lower.Contains("article")) && (lower.Contains("amount") || lower.Contains("value") || lower.Contains("rate") || lower.Contains("mrp"));
    }

    private static bool LooksLikeHeaderOnlyLine(string clean)
    {
        var lower = clean.ToLowerInvariant();
        if (GstinRegex.IsMatch(clean) || MobileRegex.IsMatch(clean)) return true;
        var words = new[] { "tax invoice", "invoice no", "invoice number", "invoice date", "bill no", "bill date", "seller", "supplier", "buyer", "billed to", "ship to", "shipped to", "place of supply", "reverse charge", "transport", "vehicle no", "lr no", "challan", "dispatch", "email", "phone", "mobile", "address", "state code", "pan", "cin" };
        return words.Any(word => lower.Contains(word));
    }

    private static bool LooksLikeRepeatedPageHeader(string clean)
    {
        var lower = clean.ToLowerInvariant();
        if (lower.Contains("page ") || lower.Contains("continued") || lower.Contains("continue")) return true;
        if (lower.Contains("tax invoice") || lower.Contains("original for") || lower.Contains("duplicate for")) return true;
        return false;
    }

    private static bool LooksLikeFooterStart(string clean)
    {
        var lower = clean.ToLowerInvariant();
        var words = new[] { "total", "sub total", "subtotal", "tax summary", "hsn summary", "taxable value", "cgst", "sgst", "igst", "round off", "roundoff", "grand total", "net payable", "amount payable", "bank details", "bank name", "account no", "a/c no", "ifsc", "upi", "terms", "condition", "declaration", "authorised", "authorized", "signature", "eway", "e-way", "irn", "ack no", "acknowledgement" };
        return words.Any(word => lower.Contains(word)) || lower.StartsWith("for ");
    }

    private static bool IsNonItemLine(string clean)
    {
        var lower = clean.ToLowerInvariant();
        if (LooksLikeHeaderOnlyLine(clean) || LooksLikeFooterStart(clean)) return true;
        if (Regex.IsMatch(lower, @"\b(?:gstin|pan|cin|ifsc|account|bank|upi|address|phone|mobile|email|website)\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(lower, @"\b(?:invoice|bill|date|due date|order no|po no|dc no|challan|transport|vehicle|state|place of supply)\b", RegexOptions.IgnoreCase)) return true;
        if (Regex.IsMatch(lower, @"\b(?:freight|packing|courier|round|discount|taxable|tax amount|cess|tds|balance|paid|received|payable)\b", RegexOptions.IgnoreCase)) return true;
        if (clean.Count(char.IsLetter) < 3) return true;
        return false;
    }

    private static bool IsLearnedIgnoredLine(string clean, InvoiceParserProfile? profile)
    {
        if (profile is null || profile.IgnoredLinePatterns.Count == 0) return false;
        var signature = LearningSignature(clean);
        return profile.IgnoredLinePatterns.Any(pattern => signature.Contains(pattern, StringComparison.OrdinalIgnoreCase) || pattern.Contains(signature, StringComparison.OrdinalIgnoreCase));
    }

    private static LearnedProductAlias? FindLearnedAlias(InvoiceParserProfile? profile, string rawName)
    {
        if (profile is null || profile.ProductAliases.Count == 0) return null;
        var signature = LearningSignature(rawName);
        return profile.ProductAliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias.RawSignature) && (signature.Contains(alias.RawSignature, StringComparison.OrdinalIgnoreCase) || alias.RawSignature.Contains(signature, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(alias => alias.RawSignature.Length)
            .FirstOrDefault();
    }

    private static ParsedInvoiceLine? TryParseKnownColumnLine(string clean, decimal defaultTaxRate)
    {
        var hsnMatches = HsnRegex.Matches(clean).Cast<Match>()
            .Where(match => match.Groups["hsn"].Value.Length is >= 6 and <= 8)
            .ToList();
        if (hsnMatches.Count == 0) return null;

        foreach (var hsnMatch in hsnMatches)
        {
            var afterHsn = clean[(hsnMatch.Index + hsnMatch.Length)..];
            var afterNumbers = MoneyTokenRegex.Matches(afterHsn).Cast<Match>()
                .Where(match => !IsPartOfGstin(afterHsn, match.Index))
                .Select(match => ParseMoney(match.Groups["value"].Value))
                .Where(value => value >= 0)
                .ToList();
            if (afterNumbers.Count < 4) continue;

            var beforeHsn = clean[..hsnMatch.Index].Trim();
            beforeHsn = Regex.Replace(beforeHsn, @"^\s*\d{1,4}\s*[).\-]?\s*", string.Empty).Trim();
            var productName = BuildColumnProductName(beforeHsn);
            if (productName.Length < 3 || productName.Count(char.IsLetter) < 2) continue;

            // Tally Prime and many garment invoices normally print columns as:
            // HSN | Qty | Rate | Gross/Amount | Discount % | Add. Discount % | GST % | Taxable/Amount
            // Some Tally layouts print: HSN | GST % | Qty | Rate | Amount.
            var candidate = TryBuildGarmentColumnLine(productName, hsnMatch.Groups["hsn"].Value, afterNumbers, defaultTaxRate);
            if (candidate is not null) return candidate;
        }

        return null;
    }

    private static string JoinNonEmpty(params string?[] parts)
    {
        return string.Join(" - ", parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
            .Where(part => part.Length > 0));
    }

    private static string BuildColumnProductName(string rawName)
    {
        var name = Regex.Replace(rawName ?? string.Empty, @"\s+", " ").Trim(' ', '-', '|', ':', ',');
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var sizeMatch = Regex.Match(name, @"\b(?<size>STD|FREE|FS|XS|S|M|L|XL|XXL|XXXL|[0-9]{2,3}(?:CM)?)\b\s*$", RegexOptions.IgnoreCase);
        var size = sizeMatch.Success ? sizeMatch.Groups["size"].Value.ToUpperInvariant() : null;
        var withoutSize = sizeMatch.Success ? name[..sizeMatch.Index].Trim() : name;

        // S.K APPARELS / S.S SETH SAAB layout: Item Description and Brand can repeat,
        // while Art No./model text is usually the meaningful differentiator. Keep the
        // brand once and append any remaining article/model text plus size.
        var normalized = Regex.Replace(withoutSize, @"S\s*\.?\s*S\s+SETH(?:\s+SAAB)?", "S.S SETH SAAB", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim(' ', '-', '|', ':', ',');
        var brand = Regex.Match(normalized, @"S\.S\s+SETH(?:\s+SAAB)?", RegexOptions.IgnoreCase).Success ? "S.S SETH SAAB" : null;
        if (brand is not null)
        {
            var remainder = Regex.Replace(normalized, @"S\.S\s+SETH(?:\s+SAAB)?", " ", RegexOptions.IgnoreCase);
            remainder = Regex.Replace(remainder, @"\s+", " ").Trim(' ', '-', '|', ':', ',');
            return JoinNonEmpty(brand, remainder, size);
        }

        return JoinNonEmpty(withoutSize, size);
    }

    private static ParsedInvoiceLine? TryBuildGarmentColumnLine(string productName, string hsn, IReadOnlyList<decimal> numbers, decimal defaultTaxRate)
    {
        if (numbers.Count >= 6)
        {
            var qty = numbers[0];
            var printedRate = numbers[1];
            var gross = numbers[2];
            var lineAmount = numbers[^1];
            var taxRateIndex = FindTrailingGstRateIndex(numbers, defaultTaxRate);
            var taxRate = taxRateIndex >= 0 ? numbers[taxRateIndex] : defaultTaxRate;
            if (qty > 0 && printedRate > 0 && gross > 0 && lineAmount > 0 && taxRate > 0 && Math.Abs((qty * printedRate) - gross) <= Math.Max(2m, gross * 0.08m))
            {
                var discountTokens = taxRateIndex > 3
                    ? numbers.Skip(3).Take(taxRateIndex - 3).ToList()
                    : numbers.Skip(3).Take(Math.Max(0, numbers.Count - 5)).ToList();
                var lineAmountLooksTaxInclusive = taxRate > 0 && lineAmount > gross + Math.Max(2m, gross * 0.02m);
                var taxable = lineAmountLooksTaxInclusive ? Round(lineAmount / (1 + taxRate / 100m)) : Round(lineAmount);
                var grossByRate = Round(qty * printedRate);
                var lineDiscount = Round(Math.Max(grossByRate - taxable, 0));

                // Some vendors print discount percentage, some discount amount, and some print both.
                // We use the printed final line amount as the source of truth and keep the visible discount
                // as gross-before-discount minus taxable-after-discount. This makes the calculated line total
                // reconcile with the scanned supplier invoice instead of losing the discount column.
                var unitDiscount = qty > 0 ? Round(lineDiscount / qty) : 0;
                var tax = Round(taxable * taxRate / 100m);
                var discountSummary = BuildDiscountSummary(grossByRate, taxable, lineDiscount, discountTokens, lineAmountLooksTaxInclusive);
                return new ParsedInvoiceLine(
                    productName,
                    null,
                    hsn,
                    qty,
                    printedRate,
                    printedRate,
                    unitDiscount,
                    lineDiscount,
                    taxRate,
                    "Exclusive",
                    taxable,
                    tax,
                    Round(taxable + tax),
                    90,
                    $"Detected by garment/Tally column parser; HSN, qty, rate, gross, discount, GST and taxable columns were mapped. {discountSummary}");
            }
        }

        if (numbers.Count >= 4)
        {
            var taxFirst = IsCommonGstRate(numbers[0]);
            var qty = taxFirst ? numbers[1] : numbers[0];
            var rate = taxFirst ? numbers[2] : numbers[1];
            var amount = numbers[^1];
            var taxRate = taxFirst ? numbers[0] : numbers.Count >= 5 && IsCommonGstRate(numbers[^2]) ? numbers[^2] : defaultTaxRate;
            if (qty > 0 && rate > 0 && amount > 0 && LooksLikePlausibleItemAmounts(qty, rate, amount, taxRate))
            {
                var mode = taxRate > 0 ? "Exclusive" : GuessGstPriceMode(qty, rate, amount, taxRate);
                var taxable = string.Equals(mode, "Exclusive", StringComparison.OrdinalIgnoreCase) ? amount : Round(amount / (1 + taxRate / 100m));
                var effectiveRate = Round(taxable / qty);
                var tax = taxRate > 0 ? Round(taxable * taxRate / 100m) : 0;
                return new ParsedInvoiceLine(productName, null, hsn, qty, effectiveRate, rate, 0, 0, taxRate, mode, Round(taxable), tax, Round(taxable + tax), 78, "Detected by Tally-style HSN/GST/qty/rate/amount parser.");
            }
        }

        return TryBuildFlexibleTallyColumnLine(productName, hsn, numbers, defaultTaxRate);
    }

    private static ParsedInvoiceLine? TryBuildFlexibleTallyColumnLine(string productName, string hsn, IReadOnlyList<decimal> numbers, decimal defaultTaxRate)
    {
        if (numbers.Count < 4) return null;
        var taxRate = numbers.Reverse().FirstOrDefault(IsCommonGstRate);
        if (taxRate <= 0) taxRate = defaultTaxRate;
        var amount = numbers[^1];
        if (amount <= 0) return null;

        for (var qtyIndex = 0; qtyIndex < Math.Min(numbers.Count - 2, 5); qtyIndex++)
        {
            var qty = numbers[qtyIndex];
            if (qty <= 0 || qty > 10000 || IsCommonGstRate(qty)) continue;
            for (var rateIndex = qtyIndex + 1; rateIndex < numbers.Count - 1; rateIndex++)
            {
                var rate = numbers[rateIndex];
                if (rate <= 0 || IsCommonGstRate(rate)) continue;
                var gross = Round(qty * rate);
                if (gross <= 0) continue;
                var plausible = amount <= gross + Math.Max(2m, gross * 0.25m) && amount >= gross * 0.40m;
                if (!plausible && taxRate > 0)
                {
                    plausible = Math.Abs((gross * (1 + taxRate / 100m)) - amount) <= Math.Max(2m, amount * 0.08m);
                }
                if (!plausible) continue;

                var mode = amount > gross + Math.Max(2m, gross * 0.02m) ? "Inclusive" : "Exclusive";
                var taxable = string.Equals(mode, "Inclusive", StringComparison.OrdinalIgnoreCase) && taxRate > 0
                    ? Round(amount / (1 + taxRate / 100m))
                    : Round(amount);
                var lineDiscount = Round(Math.Max(gross - taxable, 0));
                var unitDiscount = qty > 0 ? Round(lineDiscount / qty) : 0;
                var effectiveRate = qty > 0 ? Round((taxable + lineDiscount) / qty) : rate;
                var tax = taxRate > 0 ? Round(taxable * taxRate / 100m) : 0;
                return new ParsedInvoiceLine(productName, null, hsn, qty, effectiveRate, rate, unitDiscount, lineDiscount, taxRate, mode, taxable, tax, Round(taxable + tax), 70, "Detected by flexible Tally column parser; qty/rate/amount combination was selected by reconciliation.");
            }
        }

        return null;
    }

    private static int FindTrailingGstRateIndex(IReadOnlyList<decimal> numbers, decimal defaultTaxRate)
    {
        for (var index = numbers.Count - 2; index >= Math.Max(0, numbers.Count - 5); index--)
        {
            if (IsCommonGstRate(numbers[index])) return index;
        }
        if (IsCommonGstRate(defaultTaxRate)) return -1;
        return -1;
    }

    private static bool IsCommonGstRate(decimal value)
    {
        return value is 0 or 3 or 5 or 6 or 12 or 18 or 28;
    }

    private static string BuildDiscountSummary(decimal gross, decimal taxable, decimal lineDiscount, IReadOnlyList<decimal> discountTokens, bool lineAmountLooksTaxInclusive)
    {
        if (lineDiscount <= 0)
        {
            return lineAmountLooksTaxInclusive
                ? "No basic discount detected; printed amount looked GST-inclusive and was normalized before review."
                : "No line discount detected."
                ;
        }

        var pct = gross > 0 ? Round(lineDiscount * 100m / gross) : 0;
        var printedTokens = discountTokens.Count > 0
            ? $" Printed discount column values: {string.Join(", ", discountTokens.Select(value => value.ToString("0.##")))}."
            : string.Empty;
        var mode = lineAmountLooksTaxInclusive ? " Printed final amount looked GST-inclusive, so discount was normalized against basic/taxable value." : string.Empty;
        return $"Line discount captured as {lineDiscount:0.##} ({pct:0.##}% of gross basic).{printedTokens}{mode}";
    }

    private static ParsedInvoiceLine? TryParseStructuredLine(string clean, decimal defaultTaxRate)
    {
        var match = LineRegex.Match(clean);
        if (!match.Success) return null;
        var name = match.Groups["name"].Value.Trim();
        if (name.Length < 4) return null;
        var qty = ParseMoney(match.Groups["qty"].Value);
        var rate = ParseMoney(match.Groups["rate"].Value);
        var amount = ParseMoney(match.Groups["amount"].Value);
        if (qty <= 0 || rate <= 0 || amount <= 0) return null;
        var hsn = match.Groups["hsn"].Success ? match.Groups["hsn"].Value : null;
        var taxRate = GuessLineTaxRate(clean, defaultTaxRate);
        var mode = GuessGstPriceMode(qty, rate, amount, taxRate);
        if (!LooksLikePlausibleItemAmounts(qty, rate, amount, taxRate)) return null;
        var taxBreakup = CalculateLineTax(qty, rate, amount, taxRate, mode);
        return new ParsedInvoiceLine(name, null, hsn, qty, rate, rate, 0, 0, taxRate, mode, taxBreakup.Taxable, taxBreakup.Tax, taxBreakup.Total, 55, "Detected from structured item table row; quantity × rate matched amount.");
    }

    private static ParsedInvoiceLine? TryParseTailNumberLine(string clean, decimal defaultTaxRate)
    {
        var numberMatches = MoneyTokenRegex.Matches(clean).Cast<Match>()
            .Where(match => !IsPartOfGstin(clean, match.Index))
            .ToList();
        if (numberMatches.Count < 3) return null;

        var amountMatch = numberMatches[^1];
        var rateMatch = numberMatches[^2];
        var qtyMatch = numberMatches[^3];
        var qty = ParseMoney(qtyMatch.Groups["value"].Value);
        var rate = ParseMoney(rateMatch.Groups["value"].Value);
        var amount = ParseMoney(amountMatch.Groups["value"].Value);
        if (qty <= 0 || rate <= 0 || amount <= 0 || qty > 100000 || amount < 1) return null;

        var hsn = HsnRegex.Matches(clean).Cast<Match>()
            .Select(match => match.Groups["hsn"].Value)
            .FirstOrDefault(value => value.Length is >= 4 and <= 8 && !string.Equals(value, qtyMatch.Groups["value"].Value, StringComparison.Ordinal));
        var rawName = clean[..Math.Max(0, qtyMatch.Index)].Trim();
        rawName = Regex.Replace(rawName, @"^\s*\d+\s*[).\-]?\s*", string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(hsn)) rawName = Regex.Replace(rawName, $@"\b{Regex.Escape(hsn)}\b", string.Empty).Trim();
        rawName = Regex.Replace(rawName, @"\b(?:pcs|nos|qty|hsn|mrp|rate|amount|value|unit)\b", string.Empty, RegexOptions.IgnoreCase).Trim(' ', '-', '|', ':');
        if (rawName.Length < 4 || rawName.Count(char.IsLetter) < 3) return null;

        var taxRate = GuessLineTaxRate(clean, defaultTaxRate);
        if (!LooksLikePlausibleItemAmounts(qty, rate, amount, taxRate)) return null;
        var mode = GuessGstPriceMode(qty, rate, amount, taxRate);
        var taxBreakup = CalculateLineTax(qty, rate, amount, taxRate, mode);
        var mrp = rate;
        return new ParsedInvoiceLine(rawName, null, hsn, qty, rate, mrp, 0, 0, taxRate, mode, taxBreakup.Taxable, taxBreakup.Tax, taxBreakup.Total, 50, "Detected from tail-number item row; last numeric values validated as qty/rate/amount.");
    }

    private static bool LooksLikePlausibleItemAmounts(decimal qty, decimal rate, decimal amount, decimal taxRate)
    {
        if (qty <= 0 || rate <= 0 || amount <= 0) return false;
        var baseTotal = qty * rate;
        if (baseTotal <= 0) return false;
        var taxMultiplier = 1 + Math.Max(0, taxRate) / 100m;
        var inclusiveTotal = baseTotal * taxMultiplier;
        var tolerance = Math.Max(2m, Math.Max(baseTotal, inclusiveTotal) * 0.35m);
        if (Math.Abs(amount - baseTotal) <= tolerance || Math.Abs(amount - inclusiveTotal) <= tolerance) return true;
        return amount >= baseTotal * 0.65m && amount <= inclusiveTotal * 1.35m;
    }

    private static bool IsSummaryLine(string clean)
    {
        var lower = clean.ToLowerInvariant();
        return lower.Contains("grand total") || lower.Contains("invoice total") || lower.Contains("round off") || lower.Contains("roundoff") ||
               lower.Contains("total amount") || lower.Contains("amount payable") || lower.Contains("net payable") || lower.Contains("taxable value") ||
               lower.StartsWith("cgst") || lower.StartsWith("sgst") || lower.StartsWith("igst") || lower.StartsWith("gst") || lower.Contains("bank details") ||
               lower.Contains("terms") || lower.Contains("eway") || lower.Contains("e-way") || lower.Contains("irn") || lower.Contains("ack no");
    }

    private static decimal GuessLineTaxRate(string clean, decimal defaultTaxRate)
    {
        var percent = PercentRegex.Matches(clean).Cast<Match>()
            .Select(match => ParseMoney(match.Groups["rate"].Value))
            .FirstOrDefault(value => value is 0 or 3 or 5 or 6 or 12 or 18 or 28);
        return percent > 0 ? percent : defaultTaxRate;
    }

    private static decimal GuessDefaultTaxRate(string text)
    {
        return PercentRegex.Matches(text).Cast<Match>()
            .Select(match => ParseMoney(match.Groups["rate"].Value))
            .Where(value => value is 3 or 5 or 6 or 12 or 18 or 28)
            .GroupBy(value => value)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    private static string GuessGstPriceMode(decimal qty, decimal rate, decimal amount, decimal taxRate)
    {
        if (taxRate <= 0 || qty <= 0 || rate <= 0) return "Inclusive";
        var baseTotal = qty * rate;
        var exclusiveTotal = baseTotal * (1 + taxRate / 100m);
        var inclusiveDiff = Math.Abs(amount - baseTotal);
        var exclusiveDiff = Math.Abs(amount - exclusiveTotal);
        return exclusiveDiff + 0.50m < inclusiveDiff ? "Exclusive" : "Inclusive";
    }

    private static (decimal Taxable, decimal Tax, decimal Total) CalculateLineTax(decimal qty, decimal rate, decimal amount, decimal taxRate, string gstMode)
    {
        var gross = amount > 0 ? amount : qty * rate;
        if (taxRate <= 0) return (Round(gross), 0, Round(gross));
        if (string.Equals(gstMode, "Exclusive", StringComparison.OrdinalIgnoreCase))
        {
            var taxable = Round(qty * rate);
            var tax = Round(taxable * taxRate / 100m);
            return (taxable, tax, Round(taxable + tax));
        }
        var inclusiveTaxable = Round(gross / (1 + taxRate / 100m));
        var inclusiveTax = Round(gross - inclusiveTaxable);
        return (inclusiveTaxable, inclusiveTax, Round(gross));
    }

    private static bool IsPartOfGstin(string clean, int index)
    {
        var start = Math.Max(0, index - 15);
        var length = Math.Min(clean.Length - start, 30);
        return GstinRegex.IsMatch(clean.Substring(start, length));
    }

    private static string? GuessVendorName(IReadOnlyList<string> lines)
    {
        foreach (var line in lines.Take(12))
        {
            var clean = line.Trim();
            if (clean.Length < 3 || clean.Length > 90) continue;
            if (clean.Contains("invoice", StringComparison.OrdinalIgnoreCase) || clean.Contains("tax", StringComparison.OrdinalIgnoreCase) || clean.Contains("gstin", StringComparison.OrdinalIgnoreCase) || clean.Contains("bill", StringComparison.OrdinalIgnoreCase)) continue;
            if (clean.Contains("buyer", StringComparison.OrdinalIgnoreCase) || clean.Contains("ship to", StringComparison.OrdinalIgnoreCase) || clean.Contains("sold to", StringComparison.OrdinalIgnoreCase)) continue;
            if (GstinRegex.IsMatch(clean)) continue;
            if (clean.Count(char.IsLetter) < 3) continue;
            return clean;
        }
        return null;
    }

    private static string? GuessVendorAddress(IReadOnlyList<string> lines)
    {
        var addressLines = new List<string>();
        foreach (var line in lines.Take(18).Skip(1))
        {
            var clean = line.Trim();
            if (clean.Length < 8 || clean.Length > 160) continue;
            if (clean.Contains("invoice", StringComparison.OrdinalIgnoreCase) || clean.Contains("bill", StringComparison.OrdinalIgnoreCase) || clean.Contains("gstin", StringComparison.OrdinalIgnoreCase)) break;
            if (clean.Contains("buyer", StringComparison.OrdinalIgnoreCase) || clean.Contains("ship to", StringComparison.OrdinalIgnoreCase) || clean.Contains("sold to", StringComparison.OrdinalIgnoreCase)) break;
            if (clean.Count(char.IsLetter) < 4) continue;
            addressLines.Add(clean);
            if (addressLines.Count >= 3) break;
        }
        return addressLines.Count == 0 ? null : string.Join(", ", addressLines);
    }

    private async Task<TextExtractionResult> ExtractTextAsync(string path, string contentType, string extension, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        try
        {
            if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            {
                var text = await File.ReadAllTextAsync(path, cancellationToken);
                return new TextExtractionResult(text, "PlainTextUpload", string.IsNullOrWhiteSpace(text) ? "NoTextExtracted" : "TextExtracted", diagnostics);
            }

            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // Prefer Poppler with -layout for invoice imports. Built-in PDF text can be usable, but it often loses
                // the column positions required for Tally Prime and garment supplier item tables.
                var pdftotext = await TryPdfToTextAsync(path, cancellationToken);
                diagnostics.AddRange(pdftotext.Diagnostics);
                if (HasUsefulInvoiceText(pdftotext.Text))
                {
                    return new TextExtractionResult(pdftotext.Text, "PopplerPdfToTextLayout", "TextExtracted", diagnostics);
                }

                var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                var builtInText = ExtractPdfText(bytes);
                if (HasUsefulInvoiceText(builtInText))
                {
                    diagnostics.Add("Built-in PDF text extraction produced usable invoice text, but Poppler layout text was unavailable.");
                    return new TextExtractionResult(builtInText, "BuiltInPdfText", "TextExtracted", diagnostics);
                }

                var pdfOcr = await TryScannedPdfOcrAsync(path, cancellationToken);
                diagnostics.AddRange(pdfOcr.Diagnostics);
                return new TextExtractionResult(pdfOcr.Text, pdfOcr.Text.Length > 0 ? "TesseractPdfOcr" : "ManualReviewRequired", pdfOcr.Text.Length > 0 ? "OcrTextExtracted" : "NoTextExtracted", diagnostics);
            }

            var imageOcr = await TryImageOcrAsync(path, cancellationToken);
            diagnostics.AddRange(imageOcr.Diagnostics);
            return new TextExtractionResult(imageOcr.Text, imageOcr.Text.Length > 0 ? "TesseractImageOcr" : "ManualReviewRequired", imageOcr.Text.Length > 0 ? "OcrTextExtracted" : "NoTextExtracted", diagnostics);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Supplier invoice text extraction failed for {Path}", path);
            diagnostics.Add(ex.Message);
            return new TextExtractionResult(string.Empty, "ExtractionFailed", "NoTextExtracted", diagnostics);
        }
    }

    private async Task<TextExtractionResult> TryPdfToTextAsync(string path, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var command = configuration["PurchaseImport:PdfToTextPath"] ?? "pdftotext";
        var result = await RunProcessAsync(command, new[] { "-layout", "-enc", "UTF-8", path, "-" }, TimeSpan.FromSeconds(30), cancellationToken);
        diagnostics.Add(result.Diagnostics);
        return new TextExtractionResult(NormalizeText(result.StdOut), "PopplerPdfToText", string.IsNullOrWhiteSpace(result.StdOut) ? "NoTextExtracted" : "TextExtracted", diagnostics);
    }

    private async Task<TextExtractionResult> TryImageOcrAsync(string path, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var language = configuration["PurchaseImport:OcrLanguage"] ?? "eng";
        var command = configuration["PurchaseImport:TesseractPath"] ?? "tesseract";
        var result = await RunProcessAsync(command, new[] { path, "stdout", "-l", language, "--psm", "6" }, TimeSpan.FromSeconds(45), cancellationToken);
        diagnostics.Add(result.Diagnostics);
        return new TextExtractionResult(NormalizeText(result.StdOut), "TesseractImageOcr", string.IsNullOrWhiteSpace(result.StdOut) ? "NoTextExtracted" : "OcrTextExtracted", diagnostics);
    }

    private async Task<TextExtractionResult> TryScannedPdfOcrAsync(string path, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "garmetix-purchase-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var pdfToPpm = configuration["PurchaseImport:PdfToPpmPath"] ?? "pdftoppm";
            var prefix = Path.Combine(tempDir, "page");
            var render = await RunProcessAsync(pdfToPpm, new[] { "-png", "-r", "200", path, prefix }, TimeSpan.FromSeconds(45), cancellationToken);
            diagnostics.Add(render.Diagnostics);
            var pageImages = Directory.GetFiles(tempDir, "page-*.png").OrderBy(item => item).Take(8).ToList();
            if (pageImages.Count == 0)
            {
                diagnostics.Add("No rendered PDF page images were created for OCR.");
                return new TextExtractionResult(string.Empty, "TesseractPdfOcr", "NoTextExtracted", diagnostics);
            }

            var pageTexts = new List<string>();
            foreach (var image in pageImages)
            {
                var pageOcr = await TryImageOcrAsync(image, cancellationToken);
                diagnostics.AddRange(pageOcr.Diagnostics);
                if (!string.IsNullOrWhiteSpace(pageOcr.Text)) pageTexts.Add(pageOcr.Text);
            }
            var text = NormalizeText(string.Join("\n", pageTexts));
            return new TextExtractionResult(text, "TesseractPdfOcr", string.IsNullOrWhiteSpace(text) ? "NoTextExtracted" : "OcrTextExtracted", diagnostics);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore cleanup failures */ }
        }
    }

    private async Task<ProcessRunResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = fileName;
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            if (!process.Start())
            {
                return new ProcessRunResult(string.Empty, string.Empty, $"{fileName} did not start.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new ProcessRunResult(stdout, stderr, $"{fileName} exit {process.ExitCode}. {TrimDiagnostic(stderr)}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            logger.LogInformation(ex, "Optional OCR helper {FileName} is not available or timed out.", fileName);
            return new ProcessRunResult(string.Empty, ex.Message, $"{fileName} unavailable or timed out: {ex.Message}");
        }
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var pieces = new List<string>();
        foreach (Match match in Regex.Matches(raw, @"\((?<text>(?:\\.|[^\\)]){2,})\)\s*Tj", RegexOptions.Singleline))
        {
            pieces.Add(UnescapePdfText(match.Groups["text"].Value));
        }
        foreach (Match match in Regex.Matches(raw, @"\[(?<arr>.*?)\]\s*TJ", RegexOptions.Singleline))
        {
            foreach (Match textMatch in Regex.Matches(match.Groups["arr"].Value, @"\((?<text>(?:\\.|[^\\)]){2,})\)"))
            {
                pieces.Add(UnescapePdfText(textMatch.Groups["text"].Value));
            }
        }
        var joined = string.Join("\n", pieces.Where(item => item.Any(char.IsLetterOrDigit)));
        if (joined.Length > 50) return NormalizeText(joined);
        var printable = new string(raw.Select(ch => char.IsControl(ch) ? '\n' : ch).Where(ch => ch == '\n' || ch == '\r' || ch == '\t' || (ch >= 32 && ch <= 126)).ToArray());
        return NormalizeText(printable);
    }

    private static string UnescapePdfText(string text)
    {
        return text.Replace("\\(", "(").Replace("\\)", ")").Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\t", " ").Replace("\\\\", "\\");
    }

    private static bool HasUsefulInvoiceText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 40) return false;
        var normalized = text.ToLowerInvariant();
        return normalized.Contains("invoice") || normalized.Contains("gstin") || normalized.Contains("total") || normalized.Contains("amount") || normalized.Contains("bill");
    }

    private static string? BuildInitialErrorMessage(Guid duplicateByFile, IReadOnlyList<string> diagnostics)
    {
        var messages = new List<string>();
        if (duplicateByFile != Guid.Empty) messages.Add($"This file hash was uploaded before in draft {duplicateByFile}. Review duplicate before posting.");
        var lastUsefulDiagnostic = diagnostics.LastOrDefault(item => !string.IsNullOrWhiteSpace(item));
        if (!string.IsNullOrWhiteSpace(lastUsefulDiagnostic) && lastUsefulDiagnostic.Contains("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("Optional OCR helper is not installed. Paste OCR/text manually or install tesseract/poppler on the server.");
        }
        return messages.Count == 0 ? null : string.Join(" | ", messages);
    }

    private static decimal ExtractBestAmount(string text, Regex regex)
    {
        return regex.Matches(text).Cast<Match>()
            .Select(match => ParseMoney(match.Groups["amount"].Value))
            .Where(value => value != 0)
            .OrderByDescending(Math.Abs)
            .FirstOrDefault();
    }

    private static string TrimDiagnostic(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        value = Regex.Replace(value.Trim(), @"\s+", " ");
        return value.Length <= 240 ? value : value[..240] + "...";
    }

    private async Task<InvoiceParserProfile?> FindVendorProfileAsync(Guid companyId, string rawText, string? vendorGstin, string? vendorName, CancellationToken cancellationToken)
    {
        var normalizedText = NormalizeText(rawText);
        var gstin = GstinLookupService.NormalizeGstin(vendorGstin);
        if (string.IsNullOrWhiteSpace(gstin))
        {
            gstin = GstinRegex.Match(normalizedText) is { Success: true } match ? match.Value.ToUpperInvariant() : null;
        }
        var name = vendorName;
        if (string.IsNullOrWhiteSpace(name))
        {
            var lines = normalizedText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            name = GuessVendorName(lines);
        }

        PurchaseInvoiceImportVendorProfile? entity = null;
        if (!string.IsNullOrWhiteSpace(gstin))
        {
            entity = await db.PurchaseInvoiceImportVendorProfiles.AsNoTracking()
                .Where(item => item.CompanyId == companyId && !item.Deleted && item.VendorGstin == gstin)
                .OrderByDescending(item => item.LastLearnedAt ?? item.UpdatedAt ?? item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (entity is null && !string.IsNullOrWhiteSpace(name))
        {
            var lowerName = name.Trim().ToLower();
            entity = await db.PurchaseInvoiceImportVendorProfiles.AsNoTracking()
                .Where(item => item.CompanyId == companyId && !item.Deleted && item.VendorName != null && item.VendorName.ToLower() == lowerName)
                .OrderByDescending(item => item.LastLearnedAt ?? item.UpdatedAt ?? item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        return entity is null ? null : ToParserProfile(entity);
    }

    private async Task LearnFromCorrectionsAsync(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines, bool countAsSuccessfulDraft, CancellationToken cancellationToken)
    {
        var gstin = GstinLookupService.NormalizeGstin(batch.VendorGstinFinal ?? batch.VendorGstinRaw);
        var vendorName = (batch.VendorNameFinal ?? batch.VendorNameRaw)?.Trim();
        if (string.IsNullOrWhiteSpace(gstin) && string.IsNullOrWhiteSpace(vendorName) && !batch.VendorId.HasValue)
        {
            return;
        }

        var profile = await db.PurchaseInvoiceImportVendorProfiles
            .Where(item => item.CompanyId == batch.CompanyId && !item.Deleted)
            .Where(item => (batch.VendorId.HasValue && item.VendorId == batch.VendorId.Value) || (!string.IsNullOrWhiteSpace(gstin) && item.VendorGstin == gstin))
            .OrderByDescending(item => item.LastLearnedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            profile = new PurchaseInvoiceImportVendorProfile
            {
                Id = Guid.NewGuid(),
                CompanyId = batch.CompanyId,
                StoreGroupId = batch.StoreGroupId,
                StoreId = batch.StoreId,
                VendorId = batch.VendorId,
                VendorGstin = gstin,
                VendorName = vendorName
            };
            db.PurchaseInvoiceImportVendorProfiles.Add(profile);
        }

        var ignoredPatterns = ReadJsonList<string>(profile.IgnoredLinePatternsJson)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var aliases = ReadJsonList<LearnedProductAlias>(profile.ProductAliasesJson)
            .Where(item => !string.IsNullOrWhiteSpace(item.RawSignature))
            .GroupBy(item => item.RawSignature, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.LearnedAt).First())
            .ToList();

        foreach (var line in lines)
        {
            var raw = line.ProductNameRaw ?? line.ProductNameFinal;
            var signature = LearningSignature(raw ?? string.Empty);
            if (string.IsNullOrWhiteSpace(signature) || signature.Length < 4) continue;

            if (line.Ignored)
            {
                if (!ignoredPatterns.Contains(signature, StringComparer.OrdinalIgnoreCase)) ignoredPatterns.Add(signature);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(line.ProductNameFinal) && (line.ProductId.HasValue || !string.IsNullOrWhiteSpace(line.BarcodeFinal)))
            {
                aliases.RemoveAll(item => string.Equals(item.RawSignature, signature, StringComparison.OrdinalIgnoreCase));
                aliases.Add(new LearnedProductAlias(
                    signature,
                    line.ProductId,
                    line.ProductNameFinal,
                    line.BarcodeFinal,
                    line.HsnCode,
                    line.TaxRate,
                    DateTime.Now));
            }
        }

        profile.VendorId = batch.VendorId ?? profile.VendorId;
        profile.VendorGstin = string.IsNullOrWhiteSpace(gstin) ? profile.VendorGstin : gstin;
        profile.VendorName = string.IsNullOrWhiteSpace(vendorName) ? profile.VendorName : vendorName;
        if (string.IsNullOrWhiteSpace(profile.PreferredParserTemplate) && !string.IsNullOrWhiteSpace(batch.ParserTemplate) && !string.Equals(batch.ParserTemplate, "auto", StringComparison.OrdinalIgnoreCase))
        {
            profile.PreferredParserTemplate = NormalizeParserTemplateKey(batch.ParserTemplate);
        }
        profile.IgnoredLinePatternsJson = JsonSerializer.Serialize(ignoredPatterns.TakeLast(300).ToList(), JsonOptions);
        profile.ProductAliasesJson = JsonSerializer.Serialize(aliases.OrderByDescending(item => item.LearnedAt).Take(500).ToList(), JsonOptions);
        profile.LastLearnedAt = DateTime.Now;
        if (countAsSuccessfulDraft) profile.SuccessfulDraftCount += 1;
        profile.LearningNotes = "Auto-learned from supplier invoice import corrections: ignored non-item rows and product aliases.";
        profile.UpdatedAt = DateTime.Now;
    }

    private static InvoiceParserProfile ToParserProfile(PurchaseInvoiceImportVendorProfile entity)
    {
        return new InvoiceParserProfile(
            ReadJsonList<string>(entity.IgnoredLinePatternsJson).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReadJsonList<LearnedProductAlias>(entity.ProductAliasesJson).Where(item => !string.IsNullOrWhiteSpace(item.RawSignature)).ToList(),
            NormalizeParserTemplateKey(entity.PreferredParserTemplate));
    }

    private static List<T> ReadJsonList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private static string LearningSignature(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = Regex.Replace(value.ToLowerInvariant(), @"[0-9]+(?:\.[0-9]+)?", "#");
        normalized = Regex.Replace(normalized, @"[^a-z#]+", " ");
        normalized = Regex.Replace(normalized, @"\b(?:pcs|nos|qty|quantity|hsn|mrp|rate|amount|value|unit|rs|inr)\b", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized.Length <= 120 ? normalized : normalized[..120];
    }

    private string StorageRoot()
    {
        var configured = configuration["PurchaseImport:StorageRoot"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "data", "purchase-imports")
            : configured;
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }


    private static PurchaseInvoiceImportVendorProfileDto ToVendorProfileDto(PurchaseInvoiceImportVendorProfile profile)
    {
        var ignored = ReadJsonList<string>(profile.IgnoredLinePatternsJson)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();
        var aliases = ReadJsonList<LearnedProductAlias>(profile.ProductAliasesJson)
            .Where(item => !string.IsNullOrWhiteSpace(item.RawSignature))
            .OrderByDescending(item => item.LearnedAt)
            .ToList();

        return new PurchaseInvoiceImportVendorProfileDto(
            profile.Id,
            profile.VendorId,
            profile.VendorName,
            profile.VendorGstin,
            ignored.Count,
            aliases.Count,
            profile.SuccessfulDraftCount,
            profile.LastLearnedAt,
            profile.LearningNotes,
            NormalizeParserTemplateKey(profile.PreferredParserTemplate),
            ignored.Take(80).ToList(),
            aliases.Take(120).Select(item => new PurchaseInvoiceImportVendorProductAliasDto(
                item.RawSignature,
                item.ProductId,
                item.ProductName,
                item.Barcode,
                item.HsnCode,
                item.TaxRate,
                item.LearnedAt)).ToList());
    }

    private static string? BuildSavedLineReviewMessage(string? previousMessage, bool reviewRequired)
    {
        var detection = previousMessage;
        if (!string.IsNullOrWhiteSpace(detection))
        {
            var cutMarkers = new[] { "Product not matched.", "GST/tax not matched.", "Review product," };
            foreach (var marker in cutMarkers)
            {
                var index = detection.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0) detection = detection[..index].Trim();
            }
        }
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(detection) && detection.Contains("Detected", StringComparison.OrdinalIgnoreCase)) parts.Add(detection);
        if (reviewRequired) parts.Add("Review product, barcode, quantity and GST before posting.");
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string? BuildLineReviewMessage(ParsedInvoiceLine parsedLine, bool productMissing, bool taxMissing)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsedLine.DetectionReason)) parts.Add(parsedLine.DetectionReason);
        if (productMissing) parts.Add("Product not matched. Confirm or create product before posting.");
        if (taxMissing) parts.Add("GST/tax not matched. Select GST before posting.");
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static string NormalizeAcceptanceStatus(string? status)
    {
        var value = string.IsNullOrWhiteSpace(status) ? "Untested" : status.Trim();
        return value.ToLowerInvariant() switch
        {
            "pass" or "passed" or "accepted" => "Pass",
            "fail" or "failed" or "rejected" => "Fail",
            "needsretest" or "needs-retest" or "retest" => "NeedsRetest",
            "skip" or "skipped" => "Skipped",
            _ => "Untested"
        };
    }

    private static string? CurrentUserName(HttpContext context)
    {
        return context.User.Identity?.Name
            ?? context.User.FindFirst("userName")?.Value
            ?? context.User.FindFirst("email")?.Value
            ?? context.User.FindFirst("name")?.Value;
    }

    private static PurchaseInvoiceImportCorrectionSafetyDto BuildCorrectionSafety(PurchaseInvoiceImportBatch batch)
    {
        var isPosted = batch.PostedPurchaseInvoiceId.HasValue || string.Equals(batch.Status, "Posted", StringComparison.OrdinalIgnoreCase);
        var safeActions = new List<string>();
        var warnings = new List<string>();
        if (isPosted)
        {
            safeActions.Add("Do not delete the import proof. Open the posted purchase inward, create a controlled revision/return/reversal, and keep this import as audit proof.");
            safeActions.Add("Use the Correction Required status to flag the posted inward for owner/admin review before any stock/accounting adjustment.");
            if (batch.PostedPurchaseInvoiceId.HasValue) safeActions.Add($"Posted purchase invoice id: {batch.PostedPurchaseInvoiceId}");
            warnings.Add("Direct undo is intentionally disabled for posted imports because stock ledger, vendor balance and accounting entries must be reversed together.");
        }
        else
        {
            safeActions.Add("Open the draft, correct vendor/items/tax/discount, save, and re-run the posting report before posting.");
            safeActions.Add("If this scan is unusable, reject or delete the unposted draft and upload the supplier invoice again.");
        }
        if (!string.IsNullOrWhiteSpace(batch.CorrectionNotes)) warnings.Add(batch.CorrectionNotes);

        return new PurchaseInvoiceImportCorrectionSafetyDto(
            batch.Id,
            batch.PostedPurchaseInvoiceId,
            batch.Status,
            string.IsNullOrWhiteSpace(batch.CorrectionStatus) ? "None" : batch.CorrectionStatus,
            isPosted,
            !isPosted,
            !isPosted,
            false,
            safeActions,
            warnings);
    }


    private static PurchaseInvoiceImportBatchDto ToDto(PurchaseInvoiceImportBatch batch, IReadOnlyList<PurchaseInvoiceImportLine> lines, IReadOnlyList<string> warnings)
    {
        return new PurchaseInvoiceImportBatchDto(
            batch.Id, batch.CompanyId, batch.StoreGroupId, batch.StoreId, batch.Status, batch.SourceFileName, batch.ContentType,
            batch.FileSizeBytes, batch.Sha256Hash, batch.OcrProvider, batch.OcrStatus, batch.ConfidenceScore,
            batch.ParserTemplate, batch.ParserTemplateReason, batch.ImportQaNotes,
            batch.AcceptanceStatus, batch.AcceptanceNotes, batch.AcceptanceTestedAt, batch.AcceptanceTestedBy,
            batch.CorrectionStatus, batch.CorrectionNotes, batch.CorrectionRequestedAt, batch.CorrectionRequestedBy,
            batch.VendorId, batch.VendorNameRaw, batch.VendorNameFinal, batch.VendorGstinRaw, batch.VendorGstinFinal,
            batch.VendorMobileNumber, batch.VendorAddress, batch.SupplierInvoiceNumber, batch.SupplierInvoiceDate,
            batch.DueDate, batch.TaxableAmount, batch.CgstAmount, batch.SgstAmount, batch.IgstAmount,
            batch.FreightAmount, batch.DiscountAmount, batch.RoundOff, batch.BillAmount, batch.PaidAmount,
            batch.PaymentMode, batch.BankAccountId, batch.DuplicatePurchaseInvoiceId, batch.PostedPurchaseInvoiceId,
            batch.DuplicateOverrideReason, batch.DuplicateOverrideBy, batch.DuplicateOverrideAt,
            batch.ErrorMessage, batch.CreatedAt, batch.UpdatedAt, batch.PostedAt,
            lines.Select(line => new PurchaseInvoiceImportLineDto(line.Id, line.LineNumber, line.ProductId, line.ProductNameRaw,
                line.ProductNameFinal, line.BarcodeRaw, line.BarcodeFinal, line.HsnCode, line.Unit, line.Quantity,
                line.Mrp, line.CostPrice, line.UnitDiscount, line.LineDiscount, line.TaxRate, line.GstPriceMode, line.TaxId,
                line.TaxableAmount, line.TaxAmount, line.CgstAmount, line.SgstAmount, line.IgstAmount, line.LineTotal,
                line.ConfidenceScore, line.MatchStatus, line.ReviewRequired, line.ReviewMessage, line.ProductCategoryId,
                line.ProductSubCategoryId, line.ProductType, line.ProductGroup, line.Ignored)).ToList(),
            warnings,
            warnings.Count == 0 && !batch.PostedPurchaseInvoiceId.HasValue && !string.Equals(batch.Status, "Rejected", StringComparison.OrdinalIgnoreCase));
    }

    private static string CorrectedDraftPath(PurchaseInvoiceImportBatch batch)
    {
        var dir = string.IsNullOrWhiteSpace(batch.StoredFilePath) ? Directory.GetCurrentDirectory() : Path.GetDirectoryName(batch.StoredFilePath) ?? Directory.GetCurrentDirectory();
        return Path.Combine(dir, "corrected-draft.json");
    }

    private static string Csv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss"),
            decimal number => number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            double number => number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            float number => number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Contains(',') || text.Contains('"') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    private static string SafeStatus(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024m:0.#} KB";
        return $"{bytes / 1024m / 1024m:0.##} MB";
    }
    private static int ParseAliasCount(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try { return JsonSerializer.Deserialize<List<LearnedProductAlias>>(json, JsonOptions)?.Count ?? 0; } catch { return 0; }
    }
    private static int ParsePatternCount(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)?.Count ?? 0; } catch { return 0; }
    }

    private static string NormalizeGstPriceMode(string? value) => string.Equals(value, "Exclusive", StringComparison.OrdinalIgnoreCase) ? "Exclusive" : "Inclusive";
    private static string NormalizeText(string text) => Regex.Replace(text.Replace("\r", "\n"), @"[ \t]+", " ").Trim();
    private static string SafeFileName(string value) => string.Join("_", Path.GetFileName(value).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
    private static string SafeCode(string value) => Regex.Replace(value.ToUpperInvariant(), @"[^A-Z0-9]", "");
    private static string ContentTypeFromExtension(string extension) => extension.ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".txt" => "text/plain", _ => "application/octet-stream" };
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal ParseMoney(string value) => decimal.TryParse(value.Replace(",", ""), out var parsed) ? parsed : 0;
    private static bool TryParseDate(string value, out DateTime date)
    {
        var normalized = value.Trim().Replace('.', '/').Replace('-', '/');
        var formats = new[] { "d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy", "yyyy/M/d", "yyyy/MM/dd" };
        return DateTime.TryParseExact(normalized, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date)
               || DateTime.TryParse(normalized, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }
    private static bool IsInterStateSupply(string? companyGstin, string? partyGstin)
    {
        var companyState = GstStateCode(companyGstin);
        var partyState = GstStateCode(partyGstin);
        return companyState is not null && partyState is not null && !string.Equals(companyState, partyState, StringComparison.Ordinal);
    }
    private static string? GstStateCode(string? gstin)
    {
        var normalized = new string((gstin ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return normalized.Length == 15 && normalized[..2].All(char.IsDigit) ? normalized[..2] : null;
    }
    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitGst(decimal totalTax, TaxType taxType, bool interState = false)
    {
        totalTax = Math.Round(totalTax, 2);
        if (interState && taxType is TaxType.GST or TaxType.CGST or TaxType.SGST or TaxType.IGST) return (0, 0, totalTax);
        return taxType switch
        {
            TaxType.IGST => (0, 0, totalTax),
            TaxType.CGST => (totalTax, 0, 0),
            TaxType.SGST => (0, totalTax, 0),
            TaxType.GST => (Math.Round(totalTax / 2m, 2), totalTax - Math.Round(totalTax / 2m, 2), 0),
            _ => (0, 0, 0)
        };
    }

    private sealed record InvoiceParserProfile(IReadOnlyList<string> IgnoredLinePatterns, IReadOnlyList<LearnedProductAlias> ProductAliases, string? PreferredParserTemplate);
    private sealed record ParserTemplateSelection(string Key, string Reason);
    private sealed record LearnedProductAlias(string RawSignature, Guid? ProductId, string? ProductName, string? Barcode, string? HsnCode, decimal TaxRate, DateTime LearnedAt);
    private sealed record TextExtractionResult(string Text, string Provider, string Status, IReadOnlyList<string> Diagnostics);
    private sealed record ProcessRunResult(string StdOut, string StdErr, string Diagnostics);
    private sealed record ItemLineCandidate(int SourceLineNumber, string Text, string Section);
    private sealed record ParserLineDecision(int SourceLineNumber, string Text, string Decision, string Reason);
    private sealed record LineGuessResult(IReadOnlyList<ParsedInvoiceLine> Lines, IReadOnlyList<ParserLineDecision> Decisions);
    private sealed record ParsedInvoice(string? VendorName, string? VendorGstin, string? VendorMobileNumber, string? VendorAddress, string? InvoiceNumber, DateTime? InvoiceDate, decimal TaxableAmount, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount, decimal FreightAmount, decimal DiscountAmount, decimal RoundOff, decimal BillAmount, decimal ConfidenceScore, string ParserTemplate, string ParserTemplateReason, IReadOnlyList<ParsedInvoiceLine> Lines, IReadOnlyList<ParserLineDecision> LineDecisions);
    private sealed record ParsedInvoiceLine(string Name, string? Barcode, string? HsnCode, decimal Quantity, decimal Rate, decimal Mrp, decimal UnitDiscount, decimal LineDiscount, decimal TaxRate, string GstPriceMode, decimal TaxableAmount, decimal TaxAmount, decimal LineTotal, decimal ConfidenceScore, string DetectionReason);
}

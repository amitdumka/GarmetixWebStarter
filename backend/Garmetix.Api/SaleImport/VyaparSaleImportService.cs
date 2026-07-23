using System.Globalization;
using System.Security.Claims;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Garmetix.Api.Accounting;
using Garmetix.Api.Inventory;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Audit;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.SaleImport;

public sealed class VyaparSaleImportService(
    GarmetixDbContext db,
    DocumentNumberService documentNumbers,
    AccountingPostingService accounting,
    StockLedgerService stockLedger,
    ILogger<VyaparSaleImportService> logger)
{
    private const long MaxUploadBytes = 25L * 1024L * 1024L;
    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy"];

    public async Task<VyaparSaleImportPreviewDto> PreviewAsync(
        HttpContext context,
        IFormFile file,
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Upload a Vyapar sale report Excel file.");
        }
        if (file.Length > MaxUploadBytes)
        {
            throw new InvalidOperationException("Vyapar sale report is too large. Maximum allowed size is 25 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".xlsx" and not ".csv")
        {
            throw new InvalidOperationException("Only Vyapar .xlsx or .csv sale report files are supported.");
        }

        _ = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == storeId && item.CompanyId == companyId && item.StoreGroupId == storeGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Selected store is outside your access scope.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        var workbook = extension == ".csv"
            ? VyaparWorkbook.FromCsv(stream)
            : VyaparWorkbook.FromXlsx(stream);

        var saleRows = ParseSaleReport(workbook);
        var itemRows = ParseItemDetails(workbook);
        if (itemRows.Count == 0)
        {
            throw new InvalidOperationException("Item Details sheet was not found or does not contain sale item rows.");
        }

        var sourceInvoiceNumbers = itemRows
            .Select(item => item.InvoiceNumber)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var minInvoiceDate = itemRows.Min(item => item.InvoiceDate).Date;
        var maxInvoiceDateExclusive = itemRows.Max(item => item.InvoiceDate).Date.AddDays(1);
        var existingImportMatches = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item =>
                item.CompanyId == companyId &&
                item.StoreId == storeId &&
                item.InvoiceStatus != InvoiceStatus.Cancelled &&
                item.OnDate >= minInvoiceDate &&
                item.OnDate < maxInvoiceDateExclusive &&
                (sourceInvoiceNumbers.Contains(item.InvoiceNumber) || (item.Remarks != null && item.Remarks.Contains("VyaparSaleImport"))))
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.CustomerName,
                item.CustomerMobileNumber,
                item.BillAmount,
                item.PaidAmount,
                InvoiceStatus = item.InvoiceStatus.ToString(),
                item.Remarks
            })
            .ToListAsync(cancellationToken);

        var duplicateInvoices = existingImportMatches
            .Where(item => sourceInvoiceNumbers.Contains(item.InvoiceNumber))
            .Select(item => item.InvoiceNumber)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var duplicateSet = duplicateInvoices.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingBySourceAndDate = existingImportMatches
            .Select(item => new { Existing = item, Source = ExtractVyaparSourceInvoice(item.Remarks) ?? item.InvoiceNumber })
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .GroupBy(item => ImportKey(item.Source!, item.Existing.OnDate), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.First().Existing, StringComparer.OrdinalIgnoreCase);
        var autoHiddenInvoices = new List<VyaparSaleImportImportedInvoiceDto>();

        var candidateBarcodes = itemRows
            .Select(item => item.ItemCode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<Stock> stocks;
        if (candidateBarcodes.Length == 0)
        {
            stocks = [];
        }
        else
        {
            stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking().Include(item => item.Product), context)
                .Where(item => item.StoreId == storeId && !item.IsOFB && candidateBarcodes.Contains(item.Barcode))
                .ToListAsync(cancellationToken);
        }
        var stockByBarcode = stocks
            .GroupBy(item => item.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.First(), StringComparer.OrdinalIgnoreCase);

        var saleByInvoice = saleRows
            .GroupBy(item => item.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.First(), StringComparer.OrdinalIgnoreCase);

        var warnings = new List<string>();
        var invoices = new List<VyaparSaleImportInvoiceDto>();
        var missingIndex = new Dictionary<string, VyaparSaleImportMissingProductDto>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = 0;

        foreach (var group in itemRows.GroupBy(item => item.InvoiceNumber, StringComparer.OrdinalIgnoreCase).OrderBy(item => item.Min(row => row.InvoiceDate)))
        {
            var invoiceNo = group.Key;
            saleByInvoice.TryGetValue(invoiceNo, out var saleHeader);
            var first = group.First();
            var invoiceDate = (saleHeader?.InvoiceDate ?? first.InvoiceDate).Date;
            if (existingBySourceAndDate.TryGetValue(ImportKey(invoiceNo, invoiceDate), out var alreadyImported))
            {
                autoHiddenInvoices.Add(new VyaparSaleImportImportedInvoiceDto(
                    alreadyImported.Id,
                    alreadyImported.InvoiceNumber,
                    ExtractVyaparSourceInvoice(alreadyImported.Remarks) ?? invoiceNo,
                    alreadyImported.OnDate,
                    alreadyImported.CustomerName ?? "Walk-in Customer",
                    alreadyImported.CustomerMobileNumber ?? string.Empty,
                    alreadyImported.BillAmount,
                    alreadyImported.PaidAmount,
                    alreadyImported.BillAmount - alreadyImported.PaidAmount,
                    alreadyImported.InvoiceStatus,
                    alreadyImported.Remarks));
                continue;
            }

            var duplicate = duplicateSet.Contains(invoiceNo);
            var lineDtos = new List<VyaparSaleImportLineDto>();

            foreach (var row in group)
            {
                lineNumber++;
                var barcode = Clean(row.ItemCode);
                stockByBarcode.TryGetValue(barcode ?? string.Empty, out var stock);
                var available = stock is not null
                    ? (await GetImportAvailabilitySnapshotAsync(stock, row.InvoiceDate.Date, cancellationToken)).Quantity
                    : 0m;
                var matchStatus = stock is null
                    ? "MissingProductOrStock"
                    : available >= row.Quantity ? "Matched" : "InsufficientStock";
                var review = duplicate || stock is null || available < row.Quantity || row.Quantity <= 0 || row.LineTotal < 0;
                var reviewMessage = duplicate
                    ? "Invoice number already exists. It will be skipped unless you remove/rename it."
                    : stock is null
                        ? "Barcode/item code was not found in current Garmetix stock. Fill Garmetix barcode or enable create product/stock."
                        : available < row.Quantity
                            ? $"Insufficient stock. Available {available:n2}; required {row.Quantity:n2}."
                            : null;

                lineDtos.Add(new VyaparSaleImportLineDto(
                    lineNumber,
                    row.InvoiceNumber,
                    row.InvoiceDate,
                    row.PartyName,
                    row.ItemName,
                    barcode,
                    stock?.Barcode,
                    null,
                    stock?.ProductId,
                    stock?.Product?.Name,
                    FirstNonEmpty(row.HsnCode, stock?.HSNCode, stock?.Product?.HSNCode),
                    row.Category,
                    row.Description,
                    row.Size,
                    row.Quantity,
                    row.Unit,
                    row.UnitPrice,
                    row.DiscountPercent,
                    row.DiscountAmount,
                    row.TaxRate,
                    row.TaxAmount,
                    row.LineTotal,
                    available,
                    matchStatus,
                    review,
                    stock is null,
                    true,
                    reviewMessage));

                if (stock is null)
                {
                    var key = $"{barcode}|{row.ItemName}";
                    if (missingIndex.TryGetValue(key, out var existing))
                    {
                        missingIndex[key] = existing with
                        {
                            Quantity = existing.Quantity + row.Quantity,
                            Amount = existing.Amount + row.LineTotal,
                            InvoiceCount = existing.InvoiceCount + 1
                        };
                    }
                    else
                    {
                        missingIndex[key] = new VyaparSaleImportMissingProductDto(
                            barcode,
                            row.ItemName,
                            row.Category,
                            row.HsnCode,
                            row.Size,
                            row.Quantity,
                            row.LineTotal,
                            row.TaxRate,
                            1,
                            barcode ?? string.Empty,
                            null);
                    }
                }
            }

            var lineTotal = Math.Round(lineDtos.Where(item => item.ImportLine).Sum(item => item.LineTotal), 2, MidpointRounding.AwayFromZero);
            var totalAmount = saleHeader?.TotalAmount > 0 ? saleHeader.TotalAmount : Math.Round(lineTotal, 0, MidpointRounding.AwayFromZero);
            var parsedPayments = saleHeader?.Payments ?? Array.Empty<VyaparSaleImportPaymentDto>();
            var paymentColumnTotal = Math.Round(parsedPayments.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
            var paidWithZeroBalance = IsVyaparPaidWithZeroBalance(saleHeader?.PaymentStatus, saleHeader?.BalanceDue);
            var paidAmount = paymentColumnTotal > 0
                ? paymentColumnTotal
                : saleHeader?.ReceivedAmount > 0
                    ? saleHeader.ReceivedAmount
                    : saleHeader is null || paidWithZeroBalance
                        ? totalAmount
                        : 0m;
            var balanceDue = paidWithZeroBalance ? 0m : saleHeader?.BalanceDue ?? Math.Max(totalAmount - paidAmount, 0);
            var paidZeroBalanceDiscount = paidWithZeroBalance && totalAmount > paidAmount && paidAmount > 0
                ? Math.Round(totalAmount - paidAmount, 2, MidpointRounding.AwayFromZero)
                : 0m;
            var isReturnOrAdjustment = IsReturnOrAdjustmentTransactionType(saleHeader?.TransactionTypeRaw)
                || group.Any(row => IsReturnOrAdjustmentTransactionType(row.TransactionTypeRaw));
            var headerWarnings = new List<string>();
            if (duplicate)
            {
                headerWarnings.Add("Duplicate invoice already exists in Garmetix.");
            }
            if (isReturnOrAdjustment)
            {
                headerWarnings.Add("Looks like a Sale Return / Credit Note adjustment (Vyapar Transaction Type). Excluded from auto-import buckets - match it against the original invoice and post it manually (e.g. via Sales Return) after import.");
            }
            if (Math.Abs(totalAmount - Math.Round(lineTotal, 0, MidpointRounding.AwayFromZero)) > 1m)
            {
                headerWarnings.Add($"Header total {totalAmount:n2} differs from item total {lineTotal:n2}. Confirm before posting.");
            }
            if (lineDtos.Any(item => item.ReviewRequired))
            {
                headerWarnings.Add("One or more lines require barcode/product/stock review.");
            }
            if (paidZeroBalanceDiscount > 0)
            {
                headerWarnings.Add($"Vyapar marks this invoice Paid with zero balance, but payment columns total {paidAmount:n2} against bill {totalAmount:n2}. Difference {paidZeroBalanceDiscount:n2} will be imported as bill discount so the invoice remains paid.");
            }

            var payments = parsedPayments.Count > 0
                ? parsedPayments
                : [new VyaparSaleImportPaymentDto("Imported payment", PaymentMode.Cash, paidAmount, null, "Vyapar import paid")];
            var fullyMatched = !duplicate && !isReturnOrAdjustment && lineDtos.Count > 0 && lineDtos.All(item => item.MatchStatus == "Matched" && !item.ReviewRequired && item.ImportLine);
            var customerName = ExtractCustomerNameFromParty(saleHeader?.PartyName ?? first.PartyName);
            var customerMobile = CleanMobile(saleHeader?.MobileNumber);
            var sourceDescription = saleHeader?.Description;

            invoices.Add(new VyaparSaleImportInvoiceDto(
                invoiceNo,
                saleHeader?.InvoiceDate ?? first.InvoiceDate,
                customerName,
                customerMobile,
                Clean(saleHeader?.Gstin),
                saleHeader?.PaymentStatus,
                saleHeader?.PaymentTypeRaw,
                totalAmount,
                paidAmount,
                balanceDue,
                duplicate,
                !duplicate && !isReturnOrAdjustment && lineDtos.All(item => !item.ReviewRequired || item.MatchStatus == "InsufficientStock"),
                headerWarnings,
                payments,
                lineDtos,
                fullyMatched,
                false,
                null,
                null,
                sourceDescription,
                BuildImportInvoiceRemark(invoiceNo, saleHeader?.InvoiceDate ?? first.InvoiceDate, sourceDescription, payments),
                isReturnOrAdjustment));
        }

        if (saleRows.Count == 0)
        {
            warnings.Add("Sale Report sheet was not found. Preview was built from Item Details only; payments default to cash/paid.");
        }
        if (missingIndex.Count > 0)
        {
            warnings.Add($"{missingIndex.Count} unique Vyapar product/barcode rows are missing from Garmetix stock. Export the missing barcode sheet, fill mapping, or enable product/stock creation.");
        }
        if (duplicateSet.Count > 0)
        {
            warnings.Add($"{duplicateSet.Count} invoices already exist and will be skipped by confirm.");
        }
        if (autoHiddenInvoices.Count > 0)
        {
            warnings.Add($"{autoHiddenInvoices.Count} invoices already imported for the same Vyapar invoice/date were auto-hidden from the current preview.");
        }
        var returnOrAdjustmentCount = invoices.Count(item => item.IsReturnOrAdjustment);
        if (returnOrAdjustmentCount > 0)
        {
            warnings.Add($"{returnOrAdjustmentCount} invoice(s) look like Sale Return / Credit Note adjustments and were excluded from the auto-import buckets - review and match each one to its original invoice individually.");
        }

        var paymentSources = invoices
            .SelectMany(invoice => invoice.Payments.Select(payment => new { invoice.SourceInvoiceNumber, Payment = payment }))
            .Where(item => item.Payment.Amount > 0)
            .GroupBy(item => item.Payment.SourceName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new VyaparSaleImportPaymentSourceDto(
                group.Key,
                group.First().Payment.PaymentMode,
                group.Sum(item => item.Payment.Amount),
                group.Select(item => item.SourceInvoiceNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.Select(item => item.Payment.SourceDescription).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
                null,
                GuessPaymentKindLabel(group.Key, group.Select(item => item.Payment.SourceDescription).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)))))
            .OrderBy(item => item.SourceName)
            .ToList();
        var uniqueCustomers = invoices
            .Select(item => CleanMobile(item.CustomerMobileNumber))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new VyaparSaleImportPreviewDto(
            file.FileName,
            file.Length,
            companyId,
            storeGroupId,
            storeId,
            invoices.Count,
            invoices.Sum(item => item.Lines.Count),
            invoices.Sum(item => item.Lines.Count(line => line.MatchStatus == "Matched")),
            invoices.Sum(item => item.Lines.Count(line => line.MatchStatus == "MissingProductOrStock")),
            invoices.Sum(item => item.Lines.Count(line => line.MatchStatus == "InsufficientStock")),
            invoices.Count(item => item.DuplicateInvoice),
            invoices.Sum(item => item.TotalAmount),
            invoices.Sum(item => item.Lines.Sum(line => line.LineTotal)),
            warnings,
            invoices,
            missingIndex.Values.OrderBy(item => item.ItemName).ToList(),
            invoices.Count(item => item.FullyMatched),
            invoices.Count(item => item.ReadyForImport && !item.DuplicateInvoice),
            autoHiddenInvoices.Count,
            uniqueCustomers,
            paymentSources.Count,
            paymentSources,
            autoHiddenInvoices);
    }

    public async Task<VyaparSaleImportConfirmResponse> ConfirmAsync(
        HttpContext context,
        VyaparSaleImportConfirmRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        if (request.Invoices.Count == 0)
        {
            throw new InvalidOperationException("Preview first, then confirm invoices for import.");
        }

        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == request.StoreId && item.CompanyId == request.CompanyId && item.StoreGroupId == request.StoreGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Selected store is outside your access scope.");

        var salesman = await ResolveSalesmanAsync(context, request, cancellationToken)
            ?? throw new InvalidOperationException("Select a salesman or create an active salesman for this store before importing sales.");

        var paymentBankMap = BuildPaymentBankMap(request.PaymentBankMappings);
        var unmappedNonCashSources = request.Invoices
            .SelectMany(item => item.Payments)
            .Where(item => item.Amount > 0 && item.PaymentMode != PaymentMode.Cash)
            .Where(item => !item.BankAccountId.HasValue && !paymentBankMap.ContainsKey(item.SourceName))
            .Select(item => item.SourceName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();
        if (unmappedNonCashSources.Count > 0)
        {
            throw new InvalidOperationException($"Map each Vyapar bank/POS/UPI column to the correct Garmetix bank account before import. Missing mapping: {string.Join(", ", unmappedNonCashSources)}.");
        }

        var importBatchId = request.ImportBatchId.HasValue && request.ImportBatchId.Value != Guid.Empty
            ? request.ImportBatchId.Value
            : Guid.NewGuid();
        var importBatchReference = $"VYAPAR-SALE-{DateTime.UtcNow:yyyyMMddHHmmss}-{importBatchId.ToString("N")[..8]}";

        if (!request.FinalApprovalConfirmed)
        {
            throw new InvalidOperationException("Final approval is required before posting Vyapar sale invoices. Review matched invoices, bank/POS mapping, missing stock creation and duplicate warnings, then confirm again.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var importedNumbers = new List<string>();
        var skipped = new List<string>();
        var warnings = new List<string>();
        var createdProductCount = 0;
        var createdStockCount = 0;
        var bridgeCount = 0;
        var importedLineCount = 0;
        var importedBillAmount = 0m;

        foreach (var incoming in request.Invoices.OrderBy(item => item.InvoiceDate).ThenBy(item => item.SourceInvoiceNumber))
        {
            if (string.IsNullOrWhiteSpace(incoming.SourceInvoiceNumber))
            {
                skipped.Add("Blank invoice number skipped.");
                continue;
            }

            var incomingDate = incoming.InvoiceDate == default ? DateTime.Today : incoming.InvoiceDate.Date;
            var incomingDateExclusive = incomingDate.AddDays(1);
            var existingImport = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => item.CompanyId == request.CompanyId && item.StoreId == request.StoreId && item.InvoiceStatus != InvoiceStatus.Cancelled && item.OnDate >= incomingDate && item.OnDate < incomingDateExclusive)
                .FirstOrDefaultAsync(item =>
                    item.InvoiceNumber == incoming.SourceInvoiceNumber ||
                    (item.Remarks != null && item.Remarks.Contains("VyaparSaleImport") && item.Remarks.Contains($"VyaparSourceInvoice={incoming.SourceInvoiceNumber}")), cancellationToken);
            if (existingImport is not null)
            {
                skipped.Add($"{incoming.SourceInvoiceNumber}: already imported on {incomingDate:yyyy-MM-dd} as {existingImport.InvoiceNumber}.");
                continue;
            }

            var activeLines = incoming.Lines.Where(item => item.ImportLine && item.Quantity > 0).ToList();
            if (activeLines.Count == 0)
            {
                skipped.Add($"{incoming.SourceInvoiceNumber}: no importable item lines.");
                continue;
            }

            var invoiceId = Guid.NewGuid();
            var onDate = incoming.InvoiceDate == default ? DateTime.Today : incoming.InvoiceDate.Date;
            var invoiceNumber = request.UseVyaparInvoiceNumbers
                ? incoming.SourceInvoiceNumber.Trim()
                : await documentNumbers.NextSaleInvoiceAsync(request.CompanyId, request.StoreGroupId, request.StoreId, onDate, cancellationToken);
            var customer = await GetOrCreateCustomerAsync(request.CompanyId, incoming.CustomerName, incoming.CustomerMobileNumber, incoming.CustomerGstin, cancellationToken);

            var invoiceItems = new List<InvoiceItem>();
            decimal grossMrp = 0m;
            decimal taxableAmount = 0m;
            decimal taxAmount = 0m;
            decimal cgstAmount = 0m;
            decimal sgstAmount = 0m;
            decimal totalQuantity = 0m;

            foreach (var line in activeLines)
            {
                var finalBarcode = FirstNonEmpty(line.OverrideBarcode, line.GarmetixBarcode, line.VyaparItemCode);
                if (string.IsNullOrWhiteSpace(finalBarcode))
                {
                    throw new InvalidOperationException($"{incoming.SourceInvoiceNumber}: barcode is required for line {line.LineNumber}.");
                }

                var stock = await WorkspaceScope.ApplyTo(db.Stocks.Include(item => item.Product), context)
                    .FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.Barcode == finalBarcode && !item.IsOFB, cancellationToken);

                if (stock is null)
                {
                    if (!request.CreateMissingProductsAndStock && !line.CreateProductAndStock)
                    {
                        throw new InvalidOperationException($"{incoming.SourceInvoiceNumber}: barcode {finalBarcode} is not found. Link barcode or enable create product/stock.");
                    }

                    var createResult = await CreateImportedProductAndStockAsync(request, store, line, finalBarcode, onDate, cancellationToken);
                    stock = createResult.Stock;
                    createdProductCount += createResult.ProductCreated ? 1 : 0;
                    createdStockCount += createResult.StockCreated ? 1 : 0;
                    bridgeCount += createResult.BridgeMovementCreated ? 1 : 0;
                }

                var snapshot = await GetImportAvailabilitySnapshotAsync(stock, onDate, cancellationToken);
                if (snapshot.Quantity < line.Quantity)
                {
                    if (!request.AllowStockBridgeForInsufficientStock)
                    {
                        throw new InvalidOperationException($"{incoming.SourceInvoiceNumber}: insufficient stock for {finalBarcode} on sale date {onDate:yyyy-MM-dd}. Ledger available {snapshot.Quantity:n2}; required {line.Quantity:n2}. Enable 'Bridge insufficient/historical stock' or create/correct opening stock before import.");
                    }

                    var missingQty = Math.Round(line.Quantity - snapshot.Quantity, 2, MidpointRounding.AwayFromZero);
                    await stockLedger.PostAsync(stock, new StockMovement
                    {
                        Barcode = stock.Barcode,
                        MovementType = "VyaparImportHistoricalStockBridgeIn",
                        QuantityIn = missingQty,
                        CostPrice = snapshot.AverageCost > 0 ? snapshot.AverageCost : Math.Max(stock.CostPrice, 0),
                        MRP = Math.Max(stock.MRP, UnitSalePrice(line)),
                        TaxRate = stock.TaxRate,
                        HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
                        SourceType = "VyaparSaleImport",
                        SourceId = invoiceId,
                        SourceNumber = invoiceNumber,
                        Remarks = $"Historical stock bridge before Vyapar sale import {invoiceNumber}. Ledger available {snapshot.Quantity:n2}; required {line.Quantity:n2}.",
                        OnDate = onDate.AddSeconds(-1),
                        CompanyId = request.CompanyId,
                        StoreGroupId = request.StoreGroupId,
                        StoreId = request.StoreId
                    }, cancellationToken);
                    bridgeCount++;
                    snapshot = await GetImportAvailabilitySnapshotAsync(stock, onDate, cancellationToken);
                }

                var unitPrice = UnitSalePrice(line);
                var lineMrp = unitPrice * line.Quantity;
                var taxable = Math.Round(lineMrp / (1 + (stock.TaxRate / 100m)), 2, MidpointRounding.AwayFromZero);
                var tax = Math.Round(lineMrp - taxable, 2, MidpointRounding.AwayFromZero);
                var split = SplitGst(tax, stock.TaxType);

                invoiceItems.Add(new InvoiceItem
                {
                    InvoiceId = invoiceId,
                    ProductId = stock.ProductId,
                    Barcode = stock.Barcode,
                    ProductName = stock.Product?.Name ?? line.ItemName,
                    HSNCode = stock.HSNCode ?? stock.Product?.HSNCode ?? line.HsnCode,
                    Unit = stock.Unit,
                    ProductCategoryId = stock.Product?.ProductCategoryId,
                    ProductSubCategoryId = stock.Product?.ProductSubCategoryId,
                    MRP = unitPrice,
                    DiscountAmount = 0m,
                    BasePrice = taxable,
                    TaxPercentage = stock.TaxRate,
                    TaxAmount = tax,
                    CGSTAmount = split.Cgst,
                    SGSTAmount = split.Sgst,
                    IGSTAmount = split.Igst,
                    Amount = taxable + tax,
                    TaxType = stock.TaxType,
                    TaxId = stock.TaxId,
                    BilledQuantity = line.Quantity,
                    CompanyId = request.CompanyId
                });

                stock.SoldValue += taxable + tax;
                try
                {
                    await stockLedger.PostAsync(stock, new StockMovement
                    {
                        Barcode = stock.Barcode,
                        MovementType = "VyaparSaleImportOut",
                        QuantityOut = line.Quantity,
                        CostPrice = snapshot.AverageCost,
                        MRP = unitPrice,
                        TaxRate = stock.TaxRate,
                        HSNCode = stock.HSNCode ?? stock.Product?.HSNCode ?? line.HsnCode,
                        SourceType = "VyaparSaleImport",
                        SourceId = invoiceId,
                        SourceNumber = invoiceNumber,
                        Remarks = $"Imported Vyapar sale invoice {incoming.SourceInvoiceNumber}",
                        OnDate = onDate,
                        CompanyId = request.CompanyId,
                        StoreGroupId = request.StoreGroupId,
                        StoreId = request.StoreId
                    }, cancellationToken);
                }
                catch (ArgumentException ex) when (ex.Message.Contains("Insufficient ledger stock", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"{incoming.SourceInvoiceNumber}: stock ledger could not post sale for {finalBarcode} on {onDate:yyyy-MM-dd}. Enable 'Bridge insufficient/historical stock' and import again. Details: {ex.Message}", ex);
                }

                grossMrp += lineMrp;
                taxableAmount += taxable;
                taxAmount += tax;
                cgstAmount += split.Cgst;
                sgstAmount += split.Sgst;
                totalQuantity += line.Quantity;
                importedLineCount++;
            }

            var settlement = ResolveVyaparBillSettlement(incoming, grossMrp);
            var billAmount = settlement.BillAmount;
            if (settlement.BillDiscountAmount > 0)
            {
                var adjustedTotals = ApplyVyaparBillDiscountToInvoiceItems(invoiceItems, settlement.BillDiscountAmount);
                taxableAmount = adjustedTotals.TaxableAmount;
                taxAmount = adjustedTotals.TaxAmount;
                cgstAmount = adjustedTotals.CgstAmount;
                sgstAmount = adjustedTotals.SgstAmount;
                billAmount = Math.Round(invoiceItems.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
                warnings.Add($"{incoming.SourceInvoiceNumber}: PaymentStatus is Paid and Balance is zero; {settlement.BillDiscountAmount:n2} shortfall imported as bill discount.");
            }

            var payments = NormalizeConfirmPayments(incoming, billAmount, request.DefaultBankAccountId, paymentBankMap);
            var paidAmount = Math.Min(payments.Sum(item => item.Amount), billAmount);
            var invoiceStatus = paidAmount >= billAmount
                ? InvoiceStatus.Paid
                : paidAmount > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Pending;
            var paymentMode = payments.Count > 1 ? PaymentMode.MixPayments : payments.FirstOrDefault()?.PaymentMode ?? PaymentMode.Cash;

            var invoice = new Invoice
            {
                Id = invoiceId,
                InvoiceNumber = invoiceNumber,
                OnDate = onDate,
                InvoiceType = InvoiceType.Regular,
                InvoiceStatus = invoiceStatus,
                MRP = grossMrp,
                BasePrice = taxableAmount,
                DiscountAmount = settlement.BillDiscountAmount,
                TaxAmount = taxAmount,
                CGSTAmount = cgstAmount,
                SGSTAmount = sgstAmount,
                IGSTAmount = 0m,
                InterState = false,
                NetAmount = taxableAmount,
                RoundOff = billAmount - (taxableAmount + taxAmount),
                BillAmount = billAmount,
                Quantity = totalQuantity,
                ItemCount = invoiceItems.Count,
                PaymentMode = paymentMode,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CustomerMobileNumber = customer.MobileNumber,
                CustomerGSTIN = customer.GSTIN,
                B2BSale = !string.IsNullOrWhiteSpace(customer.GSTIN),
                SaleInvoiceType = !string.IsNullOrWhiteSpace(customer.GSTIN) ? SaleInvoiceType.B2B : SaleInvoiceType.B2C,
                SalemanId = salesman.Id,
                CreditSale = paidAmount < billAmount,
                PaidAmount = paidAmount,
                BillDiscountAmount = settlement.BillDiscountAmount,
                StoreId = request.StoreId,
                CompanyId = request.CompanyId,
                Remarks = BuildImportInvoiceRemark(incoming.SourceInvoiceNumber, onDate, incoming.SourceDescription, incoming.Payments, importBatchId, importBatchReference)
            };

            db.SalesInvoices.Add(invoice);
            db.InvoiceItems.AddRange(invoiceItems);
            foreach (var payment in payments.Where(item => item.Amount > 0))
            {
                db.InvoicePayments.Add(new InvoicePayment
                {
                    InvoiceId = invoice.Id,
                    OnDate = onDate,
                    Amount = payment.Amount,
                    PaymentMode = payment.PaymentMode,
                    BankAccountId = payment.BankAccountId,
                    ReferenceNumber = payment.ReferenceNumber,
                    GatewayReference = payment.GatewayReference,
                    SettlementStatus = payment.SettlementStatus,
                    AdjustmentSourceType = payment.AdjustmentSourceType,
                    AdjustmentSourceId = payment.AdjustmentSourceId,
                    PaymentDetailsJson = payment.PaymentDetailsJson,
                    StoreId = request.StoreId,
                    CompanyId = request.CompanyId
                });
            }

            customer.BillCount += 1;
            customer.Amount += billAmount;
            await accounting.PostSalesInvoiceAsync(invoice, customer, request.StoreGroupId, payments, cancellationToken);
            importedNumbers.Add(invoiceNumber);
            importedBillAmount += billAmount;
        }

        AddVyaparImportAudit(
            context,
            "Confirmed",
            importBatchId,
            importBatchReference,
            request.CompanyId,
            request.StoreGroupId,
            request.StoreId,
            importedNumbers.Count,
            skipped.Count,
            importedLineCount,
            importedBillAmount,
            new
            {
                request.SourceFileName,
                request.UseVyaparInvoiceNumbers,
                request.CreateMissingProductsAndStock,
                request.AllowStockBridgeForInsufficientStock,
                ImportedInvoiceNumbers = importedNumbers,
                SkippedInvoices = skipped,
                Warnings = warnings
            });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        logger.LogInformation("Imported {Count} Vyapar sale invoices with {Lines} lines in batch {BatchId}.", importedNumbers.Count, importedLineCount, importBatchId);

        return new VyaparSaleImportConfirmResponse(
            importBatchId,
            importBatchReference,
            importedNumbers.Count,
            skipped.Count,
            importedLineCount,
            createdProductCount,
            createdStockCount,
            bridgeCount,
            importedBillAmount,
            importedNumbers,
            skipped,
            warnings);
    }


    public async Task<VyaparSaleImportImportedInvoiceListDto> ListImportedAsync(
        HttpContext context,
        Guid companyId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 200);
        var query = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == companyId && item.Remarks != null && item.Remarks.Contains("VyaparSaleImport"));
        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(item => item.OnDate >= from.Value.Date);
        }
        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < toExclusive);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(item =>
                item.InvoiceNumber.Contains(term) ||
                (item.CustomerName != null && item.CustomerName.Contains(term)) ||
                item.CustomerMobileNumber.Contains(term) ||
                (item.Remarks != null && item.Remarks.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Bill = group.Sum(item => item.BillAmount),
                Paid = group.Sum(item => item.PaidAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.CustomerName,
                item.CustomerMobileNumber,
                item.BillAmount,
                item.PaidAmount,
                InvoiceStatus = item.InvoiceStatus.ToString(),
                item.Remarks
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(item => new VyaparSaleImportImportedInvoiceDto(
            item.Id,
            item.InvoiceNumber,
            ExtractVyaparSourceInvoice(item.Remarks),
            item.OnDate,
            item.CustomerName ?? "Walk-in Customer",
            item.CustomerMobileNumber ?? string.Empty,
            item.BillAmount,
            item.PaidAmount,
            item.BillAmount - item.PaidAmount,
            item.InvoiceStatus,
            item.Remarks)).ToList();

        return new VyaparSaleImportImportedInvoiceListDto(
            items,
            total,
            page,
            pageSize,
            totals?.Bill ?? 0,
            totals?.Paid ?? 0,
            (totals?.Bill ?? 0) - (totals?.Paid ?? 0));
    }


    public async Task<VyaparSaleImportBatchListDto> ListBatchesAsync(
        HttpContext context,
        Guid companyId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? q,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var query = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == companyId && item.Remarks != null && item.Remarks.Contains("VyaparSaleImport") && item.Remarks.Contains("VyaparImportBatchId="));
        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(item => item.OnDate >= from.Value.Date);
        }
        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < toExclusive);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(item => item.InvoiceNumber.Contains(term) || (item.Remarks != null && item.Remarks.Contains(term)) || (item.CustomerName != null && item.CustomerName.Contains(term)));
        }

        var rows = await query
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.InvoiceStatus,
                item.BillAmount,
                item.PaidAmount,
                item.ItemCount,
                item.CustomerName,
                item.CustomerMobileNumber,
                item.Remarks,
                item.StoreId,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var grouped = rows
            .Select(item => new { Row = item, BatchId = ExtractVyaparImportBatchId(item.Remarks), BatchReference = ExtractVyaparImportBatchReference(item.Remarks) })
            .Where(item => item.BatchId.HasValue)
            .GroupBy(item => item.BatchId!.Value)
            .Select(group =>
            {
                var first = group.OrderBy(item => item.Row.OnDate).First();
                var latest = group.OrderByDescending(item => item.Row.CreatedAt).First();
                var invoices = group.Select(item => item.Row).ToList();
                return new VyaparSaleImportBatchDto(
                    group.Key,
                    first.BatchReference ?? $"VYAPAR-SALE-{group.Key.ToString("N")[..8]}",
                    invoices.Min(item => item.OnDate),
                    invoices.Max(item => item.OnDate),
                    invoices.Count,
                    invoices.Count(item => item.InvoiceStatus == InvoiceStatus.Cancelled),
                    invoices.Count(item => item.InvoiceStatus != InvoiceStatus.Cancelled),
                    invoices.Sum(item => item.BillAmount),
                    invoices.Sum(item => item.PaidAmount),
                    latest.Row.CreatedAt,
                    invoices.Where(item => item.InvoiceStatus != InvoiceStatus.Cancelled).Select(item => item.InvoiceNumber).Take(8).ToList(),
                    invoices.All(item => item.InvoiceStatus == InvoiceStatus.Cancelled) ? "AlreadyUndone" : "CanUndo");
            })
            .OrderByDescending(item => item.ImportedAt)
            .ToList();

        var total = grouped.Count;
        var pageItems = grouped.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new VyaparSaleImportBatchListDto(pageItems, total, page, pageSize);
    }

    public async Task<VyaparSaleImportUndoResponse> UndoBatchAsync(
        HttpContext context,
        VyaparSaleImportUndoRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        if (request.BatchId == Guid.Empty)
        {
            throw new InvalidOperationException("Batch id is required for undo.");
        }
        if (!request.ConfirmUndo)
        {
            throw new InvalidOperationException("Confirm undo before reversing a Vyapar import batch.");
        }
        var reason = FirstNonEmpty(request.Reason, $"Admin undo of Vyapar sale import batch {request.BatchId}")!;

        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .Where(item => item.CompanyId == request.CompanyId && item.Remarks != null && item.Remarks.Contains($"VyaparImportBatchId={request.BatchId}"))
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.InvoiceNumber)
            .ToListAsync(cancellationToken);
        if (invoices.Count == 0)
        {
            throw new InvalidOperationException("No imported Vyapar sale invoices were found for this batch.");
        }
        if (request.StoreId.HasValue && request.StoreId.Value != Guid.Empty)
        {
            invoices = invoices.Where(item => item.StoreId == request.StoreId.Value).ToList();
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var cancelled = new List<string>();
        var skipped = new List<string>();
        decimal reversedQuantity = 0;
        decimal reversedAmount = 0;
        foreach (var invoice in invoices)
        {
            if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
            {
                skipped.Add($"{invoice.InvoiceNumber}: already cancelled.");
                continue;
            }
            var result = await CancelImportedInvoiceAsync(context, invoice, reason, cancellationToken);
            cancelled.Add(invoice.InvoiceNumber);
            reversedQuantity += result.ReversedQuantity;
            reversedAmount += result.ReversedAmount;
        }

        AddVyaparImportAudit(
            context,
            "UndoBatch",
            request.BatchId,
            ExtractVyaparImportBatchReference(invoices.FirstOrDefault()?.Remarks) ?? $"VYAPAR-SALE-{request.BatchId.ToString("N")[..8]}",
            request.CompanyId,
            request.StoreGroupId,
            request.StoreId ?? invoices.FirstOrDefault()?.StoreId,
            cancelled.Count,
            skipped.Count,
            0,
            reversedAmount,
            new { CancelledInvoices = cancelled, SkippedInvoices = skipped, ReversedQuantity = reversedQuantity, Reason = reason });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new VyaparSaleImportUndoResponse(request.BatchId, cancelled.Count, skipped.Count, reversedQuantity, reversedAmount, cancelled, skipped);
    }

    public async Task<VyaparSaleImportClearHistoryResponse> ClearImportHistoryAsync(
        HttpContext context,
        VyaparSaleImportClearHistoryRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        if (request.CompanyId == Guid.Empty)
        {
            throw new InvalidOperationException("Company id is required to clear Vyapar import history.");
        }
        if (!request.ConfirmClear)
        {
            throw new InvalidOperationException("Confirm clear before deleting stale Vyapar import history.");
        }
        if (!request.CancelledOnly && !request.AllowActiveHistoryClear)
        {
            throw new InvalidOperationException("Active Vyapar import history can be cleared only when AllowActiveHistoryClear is true. Keep CancelledOnly enabled for normal reimport cleanup.");
        }

        var query = WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .Where(item => item.CompanyId == request.CompanyId && item.Remarks != null && item.Remarks.Contains("VyaparSaleImport"));
        if (request.StoreId.HasValue && request.StoreId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreId == request.StoreId.Value);
        }
        if (request.From.HasValue)
        {
            query = query.Where(item => item.OnDate >= request.From.Value.Date);
        }
        if (request.To.HasValue)
        {
            var toExclusive = request.To.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < toExclusive);
        }

        var invoices = await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var cleared = new List<string>();
        var skippedActive = new List<string>();
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var reason = FirstNonEmpty(request.Reason, "Clear stale/cancelled Vyapar sale import history so file can be previewed/imported again")!;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            var isActive = invoice.InvoiceStatus != InvoiceStatus.Cancelled;
            if (request.CancelledOnly && isActive)
            {
                skippedActive.Add($"{invoice.InvoiceNumber}: active invoice history was not cleared.");
                continue;
            }

            var before = invoice.Remarks;
            invoice.Remarks = ClearVyaparImportMarkers(before, now, reason);
            cleared.Add(invoice.InvoiceNumber);

            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                OccurredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                Action = "ClearHistory",
                Module = "SaleImport",
                EntityName = "VyaparSaleImportHistory",
                EntityDisplayName = $"Vyapar import history cleared for {invoice.InvoiceNumber}",
                EntityId = invoice.Id,
                Reference = invoice.InvoiceNumber,
                CompanyId = invoice.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = invoice.StoreId,
                UserId = Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null,
                UserName = context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value ?? "System",
                Source = "VyaparSaleImportHistoryClear",
                RequestMethod = context.Request.Method,
                RequestPath = context.Request.Path.Value,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                Reason = reason,
                BeforeJson = JsonSerializer.Serialize(new { Remarks = before, invoice.InvoiceStatus }),
                AfterJson = JsonSerializer.Serialize(new { Remarks = invoice.Remarks, invoice.InvoiceStatus }),
                ChangesJson = JsonSerializer.Serialize(new { RemovedVyaparImportMarkers = true, request.CancelledOnly, request.AllowActiveHistoryClear }),
                ChangedFieldCount = 1,
                TraceIdentifier = context.TraceIdentifier
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var message = cleared.Count == 0
            ? "No stale Vyapar import history was cleared."
            : $"Cleared Vyapar import history from {cleared.Count} invoice(s). These invoices will no longer auto-hide matching rows on the next preview.";
        return new VyaparSaleImportClearHistoryResponse(invoices.Count, cleared.Count, skippedActive.Count, cleared, skippedActive, message);
    }


    public async Task<VyaparSaleImportFinalSummaryDto> GetFinalSummaryAsync(
        HttpContext context,
        Guid companyId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        await EnsureSalesInvoiceRemarksColumnAsync(cancellationToken);

        if (companyId == Guid.Empty)
        {
            throw new InvalidOperationException("Company id is required for Vyapar sale import final summary.");
        }

        var query = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.CompanyId == companyId && item.Remarks != null && item.Remarks.Contains("VyaparSaleImport"));
        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }
        if (from.HasValue)
        {
            query = query.Where(item => item.OnDate >= from.Value.Date);
        }
        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < toExclusive);
        }

        var invoiceRows = await query
            .Select(item => new
            {
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.InvoiceStatus,
                item.BillAmount,
                item.PaidAmount,
                item.BasePrice,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.Quantity,
                item.ItemCount,
                item.StoreId,
                item.Remarks
            })
            .ToListAsync(cancellationToken);

        var invoiceIds = invoiceRows.Select(item => item.Id).ToHashSet();
        var activeInvoiceIds = invoiceRows
            .Where(item => item.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(item => item.Id)
            .ToHashSet();

        var itemRows = await db.InvoiceItems.AsNoTracking()
            .Where(item => item.CompanyId == companyId && invoiceIds.Contains(item.InvoiceId))
            .Select(item => new
            {
                item.InvoiceId,
                item.TaxPercentage,
                item.BilledQuantity,
                item.BasePrice,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.Amount
            })
            .ToListAsync(cancellationToken);

        var paymentRows = await db.InvoicePayments.AsNoTracking()
            .Where(item => item.CompanyId == companyId && invoiceIds.Contains(item.InvoiceId))
            .Select(item => new
            {
                item.InvoiceId,
                item.PaymentMode,
                item.Amount,
                item.BankAccountId
            })
            .ToListAsync(cancellationToken);

        var movementRows = await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(item =>
                item.CompanyId == companyId &&
                item.SourceId.HasValue &&
                invoiceIds.Contains(item.SourceId.Value) &&
                item.SourceType != null &&
                item.SourceType.Contains("Vyapar"))
            .Select(item => new
            {
                item.SourceId,
                item.MovementType,
                item.QuantityIn,
                item.QuantityOut,
                item.CostImpact
            })
            .ToListAsync(cancellationToken);

        var activeInvoiceRows = invoiceRows.Where(item => item.InvoiceStatus != InvoiceStatus.Cancelled).ToList();
        var itemTotalsByInvoice = itemRows
            .Where(item => activeInvoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(
                item => item.Key,
                item => new
                {
                    LineCount = item.Count(),
                    Amount = item.Sum(row => row.Amount),
                    Taxable = item.Sum(row => row.BasePrice),
                    Tax = item.Sum(row => row.TaxAmount),
                    Cgst = item.Sum(row => row.CGSTAmount ?? 0m),
                    Sgst = item.Sum(row => row.SGSTAmount ?? 0m),
                    Igst = item.Sum(row => row.IGSTAmount ?? 0m),
                    Quantity = item.Sum(row => row.BilledQuantity)
                });
        var paymentTotalsByInvoice = paymentRows
            .Where(item => activeInvoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(item => item.Key, item => item.Sum(row => row.Amount));

        var issues = new List<VyaparSaleImportReconciliationIssueDto>();
        if (invoiceRows.Count == 0)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Error", "NO_IMPORT", "No Vyapar sale import invoices were found for the selected range/store.", null));
        }

        var missingBatchMarkerCount = activeInvoiceRows.Count(item => !ExtractVyaparImportBatchId(item.Remarks).HasValue);
        if (missingBatchMarkerCount > 0)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "MISSING_BATCH_MARKER", $"{missingBatchMarkerCount} active imported invoice(s) do not have a Vyapar import batch marker. Undo/report by batch may not cover them.", null));
        }

        var billItemMismatchCount = 0;
        foreach (var invoice in activeInvoiceRows)
        {
            itemTotalsByInvoice.TryGetValue(invoice.Id, out var itemTotals);
            var itemAmount = itemTotals?.Amount ?? 0m;
            var difference = Math.Round(invoice.BillAmount - itemAmount, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(difference) > 1m)
            {
                billItemMismatchCount++;
                if (issues.Count < 15)
                {
                    issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "BILL_ITEM_MISMATCH", $"Invoice {invoice.InvoiceNumber} bill amount does not match imported item line total.", invoice.InvoiceNumber, difference));
                }
            }
        }
        if (billItemMismatchCount > 15)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "BILL_ITEM_MISMATCH_MORE", $"{billItemMismatchCount - 15} additional bill/item mismatch rows were suppressed in this summary.", null));
        }

        var paidPaymentMismatchCount = 0;
        foreach (var invoice in activeInvoiceRows)
        {
            paymentTotalsByInvoice.TryGetValue(invoice.Id, out var paymentTotal);
            var difference = Math.Round(invoice.PaidAmount - paymentTotal, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(difference) > 1m)
            {
                paidPaymentMismatchCount++;
                if (issues.Count < 25)
                {
                    issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "PAID_PAYMENT_MISMATCH", $"Invoice {invoice.InvoiceNumber} paid amount does not match payment rows.", invoice.InvoiceNumber, difference));
                }
            }
        }
        if (paidPaymentMismatchCount > 25)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "PAID_PAYMENT_MISMATCH_MORE", $"{paidPaymentMismatchCount - 25} additional paid/payment mismatch rows were suppressed in this summary.", null));
        }

        var activeStockOutInvoiceIds = movementRows
            .Where(item => item.SourceId.HasValue && item.QuantityOut > 0 && (item.MovementType ?? string.Empty).Contains("VyaparSaleImportOut", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.SourceId!.Value)
            .ToHashSet();
        var stockCoverageMissingCount = activeInvoiceIds.Count(item => !activeStockOutInvoiceIds.Contains(item));
        if (stockCoverageMissingCount > 0)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "STOCK_COVERAGE_MISSING", $"{stockCoverageMissingCount} active imported invoice(s) have no matching Vyapar sale stock-out movement. Check stock ledger before closing.", null));
        }

        var nonCashMissingBankRows = paymentRows.Count(item =>
            activeInvoiceIds.Contains(item.InvoiceId) &&
            item.Amount > 0 &&
            item.PaymentMode != PaymentMode.Cash &&
            !item.BankAccountId.HasValue);
        if (nonCashMissingBankRows > 0)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Warning", "NON_CASH_BANK_MISSING", $"{nonCashMissingBankRows} non-cash payment row(s) have no bank account mapped.", null));
        }

        var duplicateSourceGroups = activeInvoiceRows
            .Select(item => new
            {
                Source = ExtractVyaparSourceInvoice(item.Remarks) ?? item.InvoiceNumber,
                item.OnDate,
                item.InvoiceNumber
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Source))
            .GroupBy(item => ImportKey(item.Source, item.OnDate.Date), StringComparer.OrdinalIgnoreCase)
            .Where(item => item.Count() > 1)
            .ToList();
        var duplicateSourceInvoiceCount = duplicateSourceGroups.Sum(item => item.Count());
        foreach (var group in duplicateSourceGroups.Take(8))
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto(
                "Warning",
                "DUPLICATE_SOURCE_INVOICE",
                $"Source invoice/date appears more than once: {string.Join(", ", group.Select(item => item.InvoiceNumber).Take(5))}.",
                group.First().Source));
        }

        var batchIds = activeInvoiceRows
            .Select(item => ExtractVyaparImportBatchId(item.Remarks))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .Distinct()
            .ToList();
        var historicalBridgeMovements = movementRows.Where(item => (item.MovementType ?? string.Empty).Contains("Bridge", StringComparison.OrdinalIgnoreCase)).ToList();
        var stockOutRows = movementRows.Where(item => item.QuantityOut > 0).ToList();
        var cancelledCount = invoiceRows.Count - activeInvoiceRows.Count;
        if (cancelledCount > 0)
        {
            issues.Add(new VyaparSaleImportReconciliationIssueDto("Info", "CANCELLED_IMPORT_ROWS", $"{cancelledCount} imported invoice(s) in this range are cancelled/undone and excluded from active totals.", null));
        }

        var paymentModes = paymentRows
            .Where(item => activeInvoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => item.PaymentMode)
            .Select(item => new VyaparSaleImportPaymentModeSummaryDto(
                item.Key.ToString(),
                item.Count(),
                item.Sum(row => row.Amount),
                item.Count(row => row.BankAccountId.HasValue),
                item.Count(row => row.Amount > 0 && row.PaymentMode != PaymentMode.Cash && !row.BankAccountId.HasValue)))
            .OrderByDescending(item => item.TotalAmount)
            .ToList();

        var gstSummary = itemRows
            .Where(item => activeInvoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => item.TaxPercentage)
            .Select(item => new VyaparSaleImportGstSummaryDto(
                item.Key,
                item.Count(),
                item.Sum(row => row.BilledQuantity),
                item.Sum(row => row.BasePrice),
                item.Sum(row => row.TaxAmount),
                item.Sum(row => row.CGSTAmount ?? 0m),
                item.Sum(row => row.SGSTAmount ?? 0m),
                item.Sum(row => row.IGSTAmount ?? 0m),
                item.Sum(row => row.Amount)))
            .OrderBy(item => item.TaxRate)
            .ToList();

        var severeIssueCount = issues.Count(item => item.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var warningIssueCount = issues.Count(item => item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
        var status = severeIssueCount == 0 && activeInvoiceRows.Count > 0 && warningIssueCount == 0
            ? "Complete"
            : "Not Complete";

        var closeoutChecklist = new List<string>
        {
            "Preview Vyapar file and import only fully matched / ready invoices after final approval.",
            "Compare imported invoice count, bill total and payment total with the original Vyapar report for the same date range.",
            "Review GST summary by tax rate and confirm it matches expected B2C/B2B sale output.",
            "Check payment-mode totals, especially non-cash bank/POS/UPI mappings, before day closing.",
            "Open sample imported sale invoices and verify barcode, customer, GST, payment split and stock-out ledger.",
            "Keep the import batch id/reference visible so any wrong file can be reversed by batch instead of hard-deleted.",
            "After sign-off, run database backup and keep the original Vyapar Excel file with the backup notes."
        };
        var knownLimitations = new List<string>
        {
            "This summary can reconcile only data already imported into Garmetix; it cannot read the original Excel file after the browser upload is gone.",
            "Batch undo cancels/reverses imported invoices; it is not a hard delete and audit history is intentionally preserved.",
            "Historical stock bridge rows indicate old sale data imported before opening stock was fully corrected; review them before trusting item-wise profit.",
            "If active import history was manually cleared, future preview auto-hide/duplicate checks may not detect that invoice as already imported.",
            "Final financial sign-off still needs accountant review of GST, bank receipts, stock valuation and day closing totals."
        };
        var nextModuleCandidates = new List<string>
        {
            "Attendance/Payroll real-month validation and salary payment posting QA",
            "Day Book export/deep-link final polish",
            "Accounting/GST post-import validation on live data",
            "Sale/Billing QA for mixed payment and replacement approval flow"
        };

        return new VyaparSaleImportFinalSummaryDto(
            DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            companyId,
            storeId,
            from?.Date,
            to?.Date,
            status,
            invoiceRows.Count,
            activeInvoiceRows.Count,
            cancelledCount,
            batchIds.Count,
            activeInvoiceRows.Select(item => ExtractVyaparSourceInvoice(item.Remarks) ?? item.InvoiceNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            duplicateSourceInvoiceCount,
            activeInvoiceRows.Sum(item => item.BillAmount),
            activeInvoiceRows.Sum(item => item.PaidAmount),
            activeInvoiceRows.Sum(item => item.BillAmount - item.PaidAmount),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.Amount),
            paymentRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.Amount),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.BasePrice),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.TaxAmount),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.CGSTAmount ?? 0m),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.SGSTAmount ?? 0m),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.IGSTAmount ?? 0m),
            itemRows.Where(item => activeInvoiceIds.Contains(item.InvoiceId)).Sum(item => item.BilledQuantity),
            itemRows.Count(item => activeInvoiceIds.Contains(item.InvoiceId)),
            paymentRows.Count(item => activeInvoiceIds.Contains(item.InvoiceId)),
            paymentRows.Count(item => activeInvoiceIds.Contains(item.InvoiceId) && item.PaymentMode == PaymentMode.Cash),
            paymentRows.Count(item => activeInvoiceIds.Contains(item.InvoiceId) && item.PaymentMode != PaymentMode.Cash),
            nonCashMissingBankRows,
            missingBatchMarkerCount,
            billItemMismatchCount,
            paidPaymentMismatchCount,
            stockCoverageMissingCount,
            historicalBridgeMovements.Count,
            historicalBridgeMovements.Sum(item => item.QuantityIn),
            stockOutRows.Sum(item => item.QuantityOut),
            stockOutRows.Sum(item => item.CostImpact),
            paymentModes,
            gstSummary,
            issues,
            closeoutChecklist,
            knownLimitations,
            nextModuleCandidates);
    }

    public async Task<VyaparBarcodeMappingUploadResponse> PreviewBarcodeMappingUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Upload a filled barcode mapping Excel/CSV file.");
        }
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var workbook = extension == ".csv" ? VyaparWorkbook.FromCsv(stream) : VyaparWorkbook.FromXlsx(stream);
        var rows = workbook.GetSheet("Blank Barcode Mapping");
        if (rows.Count == 0) rows = workbook.GetSheet("Mapping");
        if (rows.Count == 0) rows = workbook.GetSheet("Sheet1");
        var table = HeaderTable.FromRows(rows, ["VyaparItemCode"])
            ?? HeaderTable.FromRows(rows, ["ItemName"])
            ?? throw new InvalidOperationException("Mapping file must contain VyaparItemCode or ItemName plus GarmetixBarcodeToFill/GarmetixBarcode/Barcode.");
        var mappings = new List<VyaparBarcodeMappingDto>();
        var ignored = 0;
        foreach (var row in table.Records)
        {
            var vyaparCode = Clean(row.Get("VyaparItemCode"));
            var itemName = Clean(row.Get("ItemName"));
            var barcode = FirstNonEmpty(row.Get("GarmetixBarcodeToFill"), row.Get("GarmetixBarcode"), row.Get("Barcode"), row.Get("OverrideBarcode"));
            if (string.IsNullOrWhiteSpace(barcode))
            {
                ignored++;
                continue;
            }
            mappings.Add(new VyaparBarcodeMappingDto(vyaparCode, itemName, barcode.Trim(), row.Get("Action")));
        }
        return new VyaparBarcodeMappingUploadResponse(file.FileName, table.Records.Count, mappings.Count, ignored, mappings);
    }

    private async Task EnsureSalesInvoiceRemarksColumnAsync(CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "Remarks" text NULL;
            """, cancellationToken);
    }

    private async Task<(decimal ReversedQuantity, decimal ReversedAmount)> CancelImportedInvoiceAsync(
        HttpContext context,
        Invoice invoice,
        string reason,
        CancellationToken cancellationToken)
    {
        var originalPaidAmount = invoice.PaidAmount;
        var originalPaymentMode = invoice.PaymentMode;
        var originalPaymentRows = await db.InvoicePayments
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id && item.CompanyId == invoice.CompanyId)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var originalBankAccountId = await db.BankTransactions
            .Where(item => item.CompanyId == invoice.CompanyId &&
                (item.Reference == $"SI-{invoice.InvoiceNumber}" || item.Reference.StartsWith($"SI-{invoice.InvoiceNumber}-PAY-")))
            .Select(item => (Guid?)item.BankAccountId)
            .FirstOrDefaultAsync(cancellationToken);
        var storeGroupId = await db.Stores
            .Where(item => item.Id == invoice.StoreId)
            .Select(item => item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);
        var items = await db.InvoiceItems.Where(item => item.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
        decimal reversedQuantity = 0;
        decimal reversedAmount = 0;
        foreach (var item in items)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(stockItem =>
                stockItem.ProductId == item.ProductId &&
                stockItem.Barcode == item.Barcode &&
                stockItem.StoreId == invoice.StoreId &&
                !stockItem.IsOFB,
                cancellationToken);
            if (stock is null)
            {
                continue;
            }
            stock.SoldValue = Math.Max(0, stock.SoldValue - item.Amount);
            var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "VyaparImportUndoIn",
                QuantityIn = item.BilledQuantity,
                CostPrice = snapshot.AverageCost,
                MRP = item.MRP,
                TaxRate = item.TaxPercentage,
                HSNCode = item.HSNCode ?? stock.HSNCode,
                SourceType = "VyaparSaleImportUndo",
                SourceId = invoice.Id,
                SourceNumber = invoice.InvoiceNumber,
                Remarks = reason,
                OnDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                CompanyId = invoice.CompanyId,
                StoreGroupId = storeGroupId,
                StoreId = invoice.StoreId
            }, cancellationToken);
            reversedQuantity += item.BilledQuantity;
            reversedAmount += item.Amount;
        }
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Id == invoice.CustomerId, cancellationToken);
        if (customer is not null)
        {
            customer.BillCount = Math.Max(0, customer.BillCount - 1);
            customer.Amount = Math.Max(0, customer.Amount - invoice.BillAmount);
        }
        invoice.InvoiceStatus = InvoiceStatus.Cancelled;
        invoice.PaidAmount = 0;
        invoice.PaymentMode = null;
        invoice.CreditSale = false;
        invoice.Remarks = string.Join(" | ", new[] { invoice.Remarks, $"VyaparImportUndoAt={DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}", $"UndoReason={reason}" }.Where(item => !string.IsNullOrWhiteSpace(item)));
        await accounting.PostSalesInvoiceCancellationAsync(invoice, customer, storeGroupId, originalPaidAmount, originalPaymentMode, originalBankAccountId, ToAccountingPaymentPostings(originalPaymentRows), cancellationToken);
        return (reversedQuantity, reversedAmount);
    }

    private static IReadOnlyList<SalesInvoicePaymentPosting> ToAccountingPaymentPostings(IReadOnlyList<InvoicePayment> payments)
        => payments
            .Where(item => item.Amount > 0)
            .Select(item => new SalesInvoicePaymentPosting(
                item.PaymentMode,
                item.Amount,
                item.BankAccountId,
                item.ReferenceNumber,
                item.GatewayReference,
                item.SettlementStatus,
                item.AdjustmentSourceType,
                item.AdjustmentSourceId,
                item.PaymentDetailsJson))
            .ToList();

    private void AddVyaparImportAudit(
        HttpContext context,
        string action,
        Guid batchId,
        string batchReference,
        Guid companyId,
        Guid? storeGroupId,
        Guid? storeId,
        int invoiceCount,
        int skippedCount,
        int lineCount,
        decimal amount,
        object detail)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Action = action,
            Module = "SaleImport",
            EntityName = "VyaparSaleImportBatch",
            EntityDisplayName = "Vyapar Sale Import Batch",
            EntityId = batchId,
            Reference = batchReference,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId,
            UserId = Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null,
            UserName = context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value ?? "System",
            Source = "VyaparSaleImport",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path.Value,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            Reason = action == "UndoBatch" ? "Admin batch undo/reversal" : "Vyapar sale import confirmed",
            BeforeJson = null,
            AfterJson = JsonSerializer.Serialize(detail),
            ChangesJson = JsonSerializer.Serialize(new { invoiceCount, skippedCount, lineCount, amount }),
            ChangedFieldCount = 1,
            TraceIdentifier = context.TraceIdentifier
        });
    }

    private async Task<Salesman?> ResolveSalesmanAsync(HttpContext context, VyaparSaleImportConfirmRequest request, CancellationToken cancellationToken)
    {
        if (request.DefaultSalesmanId.HasValue && request.DefaultSalesmanId.Value != Guid.Empty)
        {
            var requested = await WorkspaceScope.ApplyTo(db.Salesmen, context)
                .FirstOrDefaultAsync(item => item.Id == request.DefaultSalesmanId.Value && item.CompanyId == request.CompanyId && item.StoreId == request.StoreId && item.Active, cancellationToken);
            if (requested is not null)
            {
                return requested;
            }
        }

        return await WorkspaceScope.ApplyTo(db.Salesmen, context)
            .Where(item => item.CompanyId == request.CompanyId && item.StoreId == request.StoreId && item.Active)
            .OrderBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Customer> GetOrCreateCustomerAsync(Guid companyId, string? nameRaw, string? mobileRaw, string? gstinRaw, CancellationToken cancellationToken)
    {
        var name = ExtractCustomerNameFromParty(nameRaw);
        var mobile = CleanMobile(mobileRaw) ?? FallbackMobileForCustomer(name);
        var gstin = Clean(gstinRaw);
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.MobileNumber == mobile, cancellationToken);
        if (customer is not null)
        {
            if (!string.IsNullOrWhiteSpace(name) && (string.IsNullOrWhiteSpace(customer.Name) || customer.Name.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase)))
            {
                customer.Name = name;
            }
            if (!string.IsNullOrWhiteSpace(gstin) && string.IsNullOrWhiteSpace(customer.GSTIN))
            {
                customer.GSTIN = gstin;
            }
            return customer;
        }

        customer = new Customer
        {
            Name = name,
            MobileNumber = mobile,
            GSTIN = gstin,
            Address = "Dumka",
            City = "Dumka",
            State = "Jharkhand",
            Country = "India",
            CompanyId = companyId
        };
        db.Customers.Add(customer);
        return customer;
    }

    private async Task<ImportStockCreateResult> CreateImportedProductAndStockAsync(
        VyaparSaleImportConfirmRequest request,
        Store store,
        VyaparSaleImportLineDto line,
        string barcode,
        DateTime onDate,
        CancellationToken cancellationToken)
    {
        var tax = await GetOrCreateTaxAsync(line.TaxRate, cancellationToken);
        var category = await GetOrCreateCategoryAsync(request.CompanyId, line.Category, cancellationToken);
        var subCategory = await GetOrCreateSubCategoryAsync(request.CompanyId, category.Id, line.Category, cancellationToken);
        var product = await db.Products.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && item.Barcode == barcode, cancellationToken);
        var productCreated = false;
        if (product is null)
        {
            product = new Product
            {
                Name = FirstNonEmpty(line.ItemName, line.ProductName, $"Vyapar Item {barcode}") ?? $"Vyapar Item {barcode}",
                Barcode = barcode,
                Descriptions = BuildProductDescription(line),
                MRP = UnitSalePrice(line),
                TaxRate = line.TaxRate,
                HSNCode = line.HsnCode ?? string.Empty,
                Unit = ParseUnit(line.Unit),
                TaxType = TaxType.GST,
                ProductType = ProductType.Readymade,
                ProductGroup = GuessProductGroup(line.Category, line.ItemName),
                ProductCategoryId = category.Id,
                ProductSubCategoryId = subCategory.Id,
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId
            };
            db.Products.Add(product);
            productCreated = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        var stock = await db.Stocks.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && item.StoreId == request.StoreId && item.Barcode == barcode && !item.IsOFB, cancellationToken);
        var stockCreated = false;
        if (stock is null)
        {
            stock = new Stock
            {
                ProductId = product.Id,
                Barcode = barcode,
                HSNCode = line.HsnCode ?? product.HSNCode,
                Unit = ParseUnit(line.Unit),
                MRP = UnitSalePrice(line),
                TaxRate = line.TaxRate,
                TaxType = TaxType.GST,
                TaxId = tax.Id,
                BrandedProduct = true,
                IsOFB = false,
                StockType = StockType.Opening,
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
            };
            db.Stocks.Add(stock);
            stockCreated = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        await stockLedger.PostAsync(stock, new StockMovement
        {
            Barcode = stock.Barcode,
            MovementType = "VyaparImportOpeningBridgeIn",
            QuantityIn = line.Quantity,
            CostPrice = 0,
            MRP = UnitSalePrice(line),
            TaxRate = line.TaxRate,
            HSNCode = line.HsnCode ?? product.HSNCode,
            SourceType = "VyaparSaleImport",
            SourceNumber = line.SourceInvoiceNumber,
            Remarks = $"Opening bridge for imported Vyapar sale line {line.SourceInvoiceNumber}",
            OnDate = onDate.AddSeconds(-1),
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        }, cancellationToken);

        return new ImportStockCreateResult(product, stock, productCreated, stockCreated, true);
    }

    private async Task<Tax> GetOrCreateTaxAsync(decimal rate, CancellationToken cancellationToken)
    {
        var rounded = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
        var minRate = rounded - 0.01m;
        var maxRate = rounded + 0.01m;
        var tax = await db.Taxes.FirstOrDefaultAsync(item => item.CompositeRate >= minRate && item.CompositeRate <= maxRate && item.TaxType == TaxType.GST, cancellationToken);
        if (tax is not null)
        {
            return tax;
        }

        tax = new Tax { Name = $"GST {rounded:0.##}%", CompositeRate = rounded, TaxType = TaxType.GST };
        db.Taxes.Add(tax);
        await db.SaveChangesAsync(cancellationToken);
        return tax;
    }

    private async Task<InventoryProductCategory> GetOrCreateCategoryAsync(Guid companyId, string? categoryRaw, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(categoryRaw) ? "Vyapar Import" : categoryRaw.Trim();
        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name.ToLower() == name.ToLower(), cancellationToken);
        if (category is not null)
        {
            return category;
        }

        category = new InventoryProductCategory { CompanyId = companyId, Name = name, ProductGroup = GuessProductGroup(name, name), IsActive = true };
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category;
    }

    private async Task<ProductSubCategory> GetOrCreateSubCategoryAsync(Guid companyId, Guid categoryId, string? categoryRaw, CancellationToken cancellationToken)
    {
        var name = string.IsNullOrWhiteSpace(categoryRaw) ? "Vyapar Import" : categoryRaw.Trim();
        var sub = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.CategoryId == categoryId && item.Name.ToLower() == name.ToLower(), cancellationToken);
        if (sub is not null)
        {
            return sub;
        }

        sub = new ProductSubCategory { CompanyId = companyId, CategoryId = categoryId, Name = name };
        db.ProductSubCategories.Add(sub);
        await db.SaveChangesAsync(cancellationToken);
        return sub;
    }

    private static VyaparBillSettlement ResolveVyaparBillSettlement(VyaparSaleImportInvoiceDto invoice, decimal grossBillAmount)
    {
        var grossBillExact = Math.Round(Math.Max(0m, grossBillAmount), 2, MidpointRounding.AwayFromZero);
        var defaultRoundedBill = Math.Round(grossBillExact, 0, MidpointRounding.AwayFromZero);
        if (grossBillExact <= 0)
        {
            return new VyaparBillSettlement(0m, 0m, false);
        }

        var paymentTotal = Math.Round(invoice.Payments.Where(item => item.Amount > 0).Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        var receivedOrPaid = Math.Round(Math.Max(paymentTotal, invoice.PaidAmount), 2, MidpointRounding.AwayFromZero);
        var paidWithZeroBalance = IsVyaparPaidWithZeroBalance(invoice.PaymentStatus, invoice.BalanceDue);
        if (!paidWithZeroBalance || receivedOrPaid <= 0 || receivedOrPaid >= grossBillExact)
        {
            return new VyaparBillSettlement(defaultRoundedBill, 0m, paidWithZeroBalance);
        }

        var discount = Math.Round(grossBillExact - receivedOrPaid, 2, MidpointRounding.AwayFromZero);
        if (discount <= 0)
        {
            return new VyaparBillSettlement(defaultRoundedBill, 0m, paidWithZeroBalance);
        }

        return new VyaparBillSettlement(receivedOrPaid, Math.Min(discount, grossBillExact), true);
    }

    private static (decimal TaxableAmount, decimal TaxAmount, decimal CgstAmount, decimal SgstAmount) ApplyVyaparBillDiscountToInvoiceItems(
        List<InvoiceItem> items,
        decimal billDiscountAmount)
    {
        billDiscountAmount = Math.Round(Math.Max(0m, billDiscountAmount), 2, MidpointRounding.AwayFromZero);
        var totalInclusive = Math.Round(items.Sum(item => item.Amount), 2, MidpointRounding.AwayFromZero);
        if (items.Count == 0 || billDiscountAmount <= 0 || totalInclusive <= 0)
        {
            return (
                items.Sum(item => item.BasePrice),
                items.Sum(item => item.TaxAmount),
                items.Sum(item => item.CGSTAmount ?? 0),
                items.Sum(item => item.SGSTAmount ?? 0));
        }

        decimal allocatedSoFar = 0m;
        decimal taxableTotal = 0m;
        decimal taxTotal = 0m;
        decimal cgstTotal = 0m;
        decimal sgstTotal = 0m;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var allocatedDiscount = index == items.Count - 1
                ? billDiscountAmount - allocatedSoFar
                : Math.Round(billDiscountAmount * item.Amount / totalInclusive, 2, MidpointRounding.AwayFromZero);
            allocatedDiscount = Math.Min(Math.Max(0m, allocatedDiscount), item.Amount);
            allocatedSoFar += allocatedDiscount;

            var adjustedInclusive = Math.Round(Math.Max(0m, item.Amount - allocatedDiscount), 2, MidpointRounding.AwayFromZero);
            var taxable = Math.Round(adjustedInclusive / (1 + (item.TaxPercentage / 100m)), 2, MidpointRounding.AwayFromZero);
            var tax = Math.Round(adjustedInclusive - taxable, 2, MidpointRounding.AwayFromZero);
            var split = SplitGst(tax, item.TaxType);
            var quantity = item.BilledQuantity <= 0 ? 1 : item.BilledQuantity;

            item.DiscountAmount = Math.Round(item.DiscountAmount + (allocatedDiscount / quantity), 2, MidpointRounding.AwayFromZero);
            item.BasePrice = taxable;
            item.TaxAmount = tax;
            item.CGSTAmount = split.Cgst;
            item.SGSTAmount = split.Sgst;
            item.IGSTAmount = split.Igst;
            item.Amount = adjustedInclusive;

            taxableTotal += taxable;
            taxTotal += tax;
            cgstTotal += split.Cgst;
            sgstTotal += split.Sgst;
        }

        return (taxableTotal, taxTotal, cgstTotal, sgstTotal);
    }

    private static bool IsVyaparPaidWithZeroBalance(string? paymentStatus, decimal? balanceDue)
        => !string.IsNullOrWhiteSpace(paymentStatus)
           && paymentStatus.Trim().Equals("Paid", StringComparison.OrdinalIgnoreCase)
           && Math.Abs(balanceDue ?? 0m) <= 0.01m;

    private static IReadOnlyList<SalesInvoicePaymentPosting> NormalizeConfirmPayments(
        VyaparSaleImportInvoiceDto invoice,
        decimal billAmount,
        Guid? defaultBankAccountId,
        IReadOnlyDictionary<string, Guid> paymentBankMap)
    {
        // DefaultBankAccountId remains in the request for older/fallback imports, but named Vyapar
        // bank/POS/UPI columns must be mapped individually so money posts to the exact bank account.
        _ = defaultBankAccountId;
        var payments = invoice.Payments
            .Where(item => item.Amount > 0)
            .Select(item =>
            {
                var mappedBankId = item.BankAccountId ?? (paymentBankMap.TryGetValue(item.SourceName, out var mapped) ? mapped : (Guid?)null);
                if (item.PaymentMode != PaymentMode.Cash && !mappedBankId.HasValue)
                {
                    throw new InvalidOperationException($"Vyapar payment source '{item.SourceName}' is non-cash but is not mapped to a Garmetix bank account.");
                }
                // Cash is the one source that stays optional to map. If the operator explicitly maps it
                // to a bank/POS account (e.g. cash banked same-day, or a cash drawer tracked as its own
                // account), the payment has to post as a bank receipt instead of Cash - PaymentMode.Cash
                // always resolves to the Cash-In-Hand ledger regardless of BankAccountId downstream
                // (AccountingPostingService.ResolveSalesInvoiceSettlementLedgerAsync/ResolveSettlementLedgerAsync),
                // so the mode itself has to change to route through the mapped account. Left unmapped,
                // Cash keeps posting to Cash-In-Hand exactly as before.
                var postingMode = item.PaymentMode == PaymentMode.Cash && mappedBankId.HasValue
                    ? PaymentMode.NEFT
                    : item.PaymentMode;
                var paymentDetails = JsonSerializer.Serialize(new
                {
                    source = "VyaparSaleImport",
                    sourceInvoiceNumber = invoice.SourceInvoiceNumber,
                    sourcePaymentName = item.SourceName,
                    sourceDescription = item.SourceDescription,
                    referenceNote = item.PaymentReferenceNote
                });
                return new SalesInvoicePaymentPosting(
                    postingMode,
                    Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero),
                    mappedBankId,
                    FirstNonEmpty(item.ReferenceNumber, item.PaymentReferenceNote, item.SourceName),
                    null,
                    null,
                    "VyaparSaleImport",
                    null,
                    paymentDetails);
            })
            .Where(item => item.Amount > 0)
            .ToList();
        if (payments.Count == 0 && billAmount > 0)
        {
            payments.Add(new SalesInvoicePaymentPosting(PaymentMode.Cash, billAmount, null, "Vyapar import paid", null, null, "VyaparSaleImport", null, null));
        }
        var paidTotal = payments.Sum(item => item.Amount);
        if (paidTotal > billAmount && billAmount > 0)
        {
            var factor = billAmount / paidTotal;
            payments = payments.Select(item => item with { Amount = Math.Round(item.Amount * factor, 2, MidpointRounding.AwayFromZero) }).ToList();
        }
        return payments;
    }

    private static List<VyaparParsedSaleRow> ParseSaleReport(VyaparWorkbook workbook)
    {
        var rows = workbook.GetSheet("Sale Report");
        var table = HeaderTable.FromRows(rows, ["Date", "Invoice No"]);
        if (table is null)
        {
            return [];
        }

        var paymentColumns = table.Headers
            .SkipWhile(header => !HeaderEquals(header, "Description"))
            .Skip(1)
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();

        var result = new List<VyaparParsedSaleRow>();
        foreach (var record in table.Records)
        {
            var invoiceNo = Clean(record.Get("Invoice No"));
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                continue;
            }
            var transactionType = Clean(record.Get("Transaction Type"));
            if (!string.IsNullOrWhiteSpace(transactionType)
                && !transactionType.Contains("Sale", StringComparison.OrdinalIgnoreCase)
                && !IsReturnOrAdjustmentTransactionType(transactionType))
            {
                continue;
            }

            var description = Clean(record.Get("Description"));
            var payments = new List<VyaparSaleImportPaymentDto>();
            foreach (var column in paymentColumns)
            {
                var amount = ParseDecimal(record.Get(column));
                if (amount <= 0)
                {
                    continue;
                }
                payments.Add(new VyaparSaleImportPaymentDto(column, GuessPaymentMode(column), amount, null, BuildPaymentReference(column, description), description, BuildPaymentReference(column, description)));
            }
            if (payments.Count == 0 && ParseDecimal(record.Get("Received/Paid Amount")) > 0)
            {
                var paymentType = Clean(record.Get("Payment Type")) ?? "Cash";
                payments.Add(new VyaparSaleImportPaymentDto(paymentType, GuessPaymentMode(paymentType), ParseDecimal(record.Get("Received/Paid Amount")), null, BuildPaymentReference(paymentType, description), description, BuildPaymentReference(paymentType, description)));
            }

            result.Add(new VyaparParsedSaleRow(
                invoiceNo,
                ParseDate(record.Get("Date")),
                Clean(record.Get("Party Name")) ?? "Cash Sale",
                Clean(record.Get("Party Phone No.")),
                Clean(record.Get("GSTIN")),
                Clean(record.Get("Payment Type")),
                Clean(record.Get("Payment Status")),
                ParseDecimal(record.Get("Total Amount")),
                ParseDecimal(record.Get("Received/Paid Amount")),
                ParseDecimal(FirstNonEmpty(record.Get("Balance Due"), record.Get("Balance"))),
                payments,
                description,
                transactionType));
        }
        return result;
    }

    private static List<VyaparParsedItemRow> ParseItemDetails(VyaparWorkbook workbook)
    {
        var rows = workbook.GetSheet("Item Details");
        var table = HeaderTable.FromRows(rows, ["Date", "Invoice No./Txn No.", "Item Name", "Item Code"]);
        if (table is null)
        {
            return [];
        }

        var result = new List<VyaparParsedItemRow>();
        foreach (var record in table.Records)
        {
            var invoiceNo = Clean(record.Get("Invoice No./Txn No."));
            var itemName = Clean(record.Get("Item Name"));
            if (string.IsNullOrWhiteSpace(invoiceNo) || string.IsNullOrWhiteSpace(itemName))
            {
                continue;
            }
            var transactionType = Clean(record.Get("Transaction Type"));
            if (!string.IsNullOrWhiteSpace(transactionType)
                && !transactionType.Contains("Sale", StringComparison.OrdinalIgnoreCase)
                && !IsReturnOrAdjustmentTransactionType(transactionType))
            {
                continue;
            }
            result.Add(new VyaparParsedItemRow(
                invoiceNo,
                ParseDate(record.Get("Date")),
                Clean(record.Get("Party Name")) ?? "Cash Sale",
                itemName,
                Clean(record.Get("Item Code")),
                Clean(record.Get("HSN/SAC")),
                Clean(record.Get("Category")),
                Clean(record.Get("Description")),
                Clean(record.Get("Size")),
                ParseDecimal(record.Get("Quantity")),
                Clean(record.Get("Unit")),
                ParseDecimal(record.Get("UnitPrice")),
                ParseDecimal(record.Get("Discount Percent")),
                ParseDecimal(record.Get("Discount")),
                ParseDecimal(record.Get("Tax Percent")),
                ParseDecimal(record.Get("Tax")),
                ParseDecimal(record.Get("Amount")),
                transactionType));
        }
        return result;
    }

    private static PaymentMode GuessPaymentMode(string sourceName)
    {
        var value = sourceName.ToLowerInvariant();
        if (value.Contains("cash")) return PaymentMode.Cash;
        if (value.Contains("cheque") || value.Contains("check")) return PaymentMode.Cheque;
        if (value.Contains("pos") || value.Contains("card") || value.Contains("cc")) return PaymentMode.Card;
        if (value.Contains("upi") || value.Contains("amy") || value.Contains("phone") || value.Contains("paytm")) return PaymentMode.UPI;
        return PaymentMode.UPI;
    }

    // Purely descriptive label for Step 3 of the import UI - splits the generic PaymentMode.Card
    // bucket into Credit Card/Debit Card and tags Cash/Cheque/UPI/Bank Transfer, guessed from the
    // Vyapar source name/description text. Never affects accounting posting (only PaymentMode does).
    private static string GuessPaymentKindLabel(string sourceName, string? description)
    {
        var text = $"{sourceName} {description}".ToLowerInvariant();
        if (text.Contains("cash")) return "Cash";
        if (text.Contains("cheque") || text.Contains("check")) return "Cheque";
        if (text.Contains("credit card") || text.Contains("credit-card") || text.Contains(" cc ") || text.EndsWith(" cc")) return "Credit Card";
        if (text.Contains("debit card") || text.Contains("debit-card") || text.Contains("rupay debit") || text.Contains(" dc ") || text.EndsWith(" dc")) return "Debit Card";
        if (text.Contains("upi") || text.Contains("gpay") || text.Contains("g-pay") || text.Contains("phonepe") || text.Contains("paytm") || text.Contains("bhim")) return "UPI";
        if (text.Contains("card") || text.Contains("pos") || text.Contains("edc") || text.Contains("swipe")) return "Debit Card";
        if (text.Contains("neft") || text.Contains("rtgs") || text.Contains("imps") || text.Contains("bank transfer") || text.Contains("bank")) return "Bank Transfer";
        return "Other";
    }

    // Vyapar's "Transaction Type" column is normally just "Sale", but a return/adjustment can show
    // up as "Sale Return" or "Credit Note" in the same sheet - these must never be silently dropped
    // (they were previously excluded whenever the text didn't contain "Sale") nor silently imported
    // as a normal positive sale. They are parsed and flagged instead; see IsReturnOrAdjustment on
    // VyaparSaleImportInvoiceDto.
    private static bool IsReturnOrAdjustmentTransactionType(string? transactionType)
        => !string.IsNullOrWhiteSpace(transactionType)
            && (transactionType.Contains("Return", StringComparison.OrdinalIgnoreCase)
                || transactionType.Contains("Credit Note", StringComparison.OrdinalIgnoreCase));

    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitGst(decimal taxAmount, TaxType taxType)
    {
        if (taxAmount <= 0) return (0, 0, 0);
        if (taxType == TaxType.IGST) return (0, 0, taxAmount);
        var half = Math.Round(taxAmount / 2m, 2, MidpointRounding.AwayFromZero);
        return (half, taxAmount - half, 0);
    }

    private static decimal UnitSalePrice(VyaparSaleImportLineDto line)
        => line.Quantity > 0 ? Math.Round(line.LineTotal / line.Quantity, 2, MidpointRounding.AwayFromZero) : Math.Round(line.LineTotal, 2, MidpointRounding.AwayFromZero);

    private static Unit ParseUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return Unit.Pcs;
        var value = unit.Trim().ToLowerInvariant();
        if (value.Contains("meter") || value == "mtr") return Unit.Meters;
        if (value.Contains("kg")) return Unit.Kgs;
        if (value.Contains("gram")) return Unit.Grams;
        if (value.Contains("packet")) return Unit.Packets;
        if (value.Contains("box")) return Unit.Boxes;
        if (value.Contains("nos") || value == "no") return Unit.Nos;
        return Unit.Pcs;
    }

    private static ProductGroup GuessProductGroup(string? category, string? name)
    {
        var text = $"{category} {name}".ToLowerInvariant();
        if (text.Contains("sherwani")) return ProductGroup.Sherwani;
        if (text.Contains("blazer")) return ProductGroup.Blazers;
        if (text.Contains("suit")) return ProductGroup.Suits;
        if (text.Contains("jodh") || text.Contains("jdp")) return ProductGroup.Jodhpuri;
        if (text.Contains("kurta") || text.Contains("kp")) return ProductGroup.KurtaPajama;
        if (text.Contains("pagadi") || text.Contains("pagdi")) return ProductGroup.Pagadi;
        if (text.Contains("shoe") || text.Contains("nagra")) return ProductGroup.Shoes;
        if (text.Contains("accessor")) return ProductGroup.Accessories;
        return ProductGroup.Readymade;
    }

    private static string BuildProductDescription(VyaparSaleImportLineDto line)
    {
        var parts = new[]
        {
            $"Vyapar item code: {line.VyaparItemCode}",
            string.IsNullOrWhiteSpace(line.Description) ? null : $"Description: {line.Description}",
            string.IsNullOrWhiteSpace(line.Size) ? null : $"Size: {line.Size}",
            string.IsNullOrWhiteSpace(line.Category) ? null : $"Category: {line.Category}",
            string.IsNullOrWhiteSpace(line.SourceInvoiceNumber) ? null : $"Imported from invoice: {line.SourceInvoiceNumber}"
        };
        return string.Join(" | ", parts.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private async Task<StockLedgerSnapshot> GetImportAvailabilitySnapshotAsync(Stock stock, DateTime onDate, CancellationToken cancellationToken)
    {
        var cutoff = onDate == default ? DateTime.Today : onDate;
        var movements = await db.StockMovements.AsNoTracking()
            .Where(item => item.StockId == stock.Id && item.OnDate <= cutoff)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new StockLedgerMovement(
                item.Id,
                item.OnDate,
                item.CreatedAt,
                item.QuantityIn,
                item.QuantityOut,
                item.CostPrice))
            .ToListAsync(cancellationToken);

        movements.AddRange(db.ChangeTracker.Entries<StockMovement>()
            .Where(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.StockId == stock.Id &&
                entry.Entity.OnDate <= cutoff)
            .Select(entry => new StockLedgerMovement(
                entry.Entity.Id,
                entry.Entity.OnDate,
                entry.Entity.CreatedAt,
                entry.Entity.QuantityIn,
                entry.Entity.QuantityOut,
                entry.Entity.CostPrice)));

        return StockLedgerCalculator.Replay(movements);
    }

    private static IReadOnlyDictionary<string, Guid> BuildPaymentBankMap(IReadOnlyList<VyaparPaymentBankMappingDto>? mappings)
        => (mappings ?? Array.Empty<VyaparPaymentBankMappingDto>())
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceName) && item.BankAccountId.HasValue && item.BankAccountId.Value != Guid.Empty)
            .GroupBy(item => item.SourceName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Key, item => item.First().BankAccountId!.Value, StringComparer.OrdinalIgnoreCase);

    private static string ImportKey(string sourceInvoiceNumber, DateTime date)
        => $"{sourceInvoiceNumber.Trim().ToUpperInvariant()}|{date:yyyyMMdd}";

    private static string? ExtractVyaparSourceInvoice(string? remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks)) return null;
        var match = Regex.Match(remarks, @"VyaparSourceInvoice=([^|\r\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static Guid? ExtractVyaparImportBatchId(string? remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks)) return null;
        var match = Regex.Match(remarks, @"VyaparImportBatchId=([0-9a-fA-F-]{32,36})", RegexOptions.IgnoreCase);
        return match.Success && Guid.TryParse(match.Groups[1].Value.Trim(), out var batchId) ? batchId : null;
    }

    private static string? ExtractVyaparImportBatchReference(string? remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks)) return null;
        var match = Regex.Match(remarks, @"VyaparImportBatchRef=([^|\r\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string ExtractCustomerNameFromParty(string? partyName)
    {
        var value = Clean(partyName) ?? "Cash Sale";
        var match = Regex.Match(value, @"\(([^)]+)\)");
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
        {
            return match.Groups[1].Value.Trim();
        }
        if (value.StartsWith("Cash Sale", StringComparison.OrdinalIgnoreCase)) return "Cash Sale";
        return value;
    }

    private static string FallbackMobileForCustomer(string customerName)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(customerName.Trim().ToUpperInvariant()));
        var number = BitConverter.ToUInt32(hashBytes, 0) % 100000000;
        return $"90{number:00000000}";
    }

    private static string BuildPaymentReference(string sourceName, string? description)
    {
        var desc = Clean(description);
        return string.IsNullOrWhiteSpace(desc) ? sourceName.Trim() : $"{sourceName.Trim()} | {desc}";
    }

    private static string BuildImportInvoiceRemark(
        string sourceInvoiceNumber,
        DateTime invoiceDate,
        string? description,
        IReadOnlyList<VyaparSaleImportPaymentDto> payments,
        Guid? importBatchId = null,
        string? importBatchReference = null)
    {
        var parts = new List<string>
        {
            "VyaparSaleImport"
        };
        if (importBatchId.HasValue) parts.Add($"VyaparImportBatchId={importBatchId.Value}");
        if (!string.IsNullOrWhiteSpace(importBatchReference)) parts.Add($"VyaparImportBatchRef={importBatchReference.Trim()}");
        parts.Add($"VyaparSourceInvoice={sourceInvoiceNumber.Trim()}");
        parts.Add($"VyaparInvoiceDate={invoiceDate:yyyy-MM-dd}");
        var desc = Clean(description);
        if (!string.IsNullOrWhiteSpace(desc)) parts.Add($"Description={desc}");
        var paymentRefs = payments
            .Where(item => item.Amount > 0 && item.PaymentMode != PaymentMode.Cash)
            .Select(item => FirstNonEmpty(item.PaymentReferenceNote, item.ReferenceNumber, item.SourceName))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paymentRefs.Count > 0) parts.Add($"PaymentRefs={string.Join("; ", paymentRefs)}");
        return string.Join(" | ", parts);
    }

    private static string? ClearVyaparImportMarkers(string? remarks, DateTime clearedAtUtc, string reason)
    {
        var kept = (remarks ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !IsVyaparImportMarker(item))
            .ToList();
        kept.Add($"ClearedImportHistoryAt={clearedAtUtc:yyyy-MM-ddTHH:mm:ssZ}");
        kept.Add($"ClearedImportHistoryReason={reason.Trim()}");
        return kept.Count == 0 ? null : string.Join(" | ", kept);
    }

    private static bool IsVyaparImportMarker(string value)
    {
        var item = value.Trim();
        if (item.Equals("VyaparSaleImport", StringComparison.OrdinalIgnoreCase)) return true;
        return item.StartsWith("VyaparSourceInvoice=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparInvoiceDate=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparImportBatchId=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparImportBatchRef=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparImportSourceFile=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparImportUndoAt=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparPaymentStatus=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparBalanceDue=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparBillDiscount=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("VyaparPaidZeroBalanceDiscount=", StringComparison.OrdinalIgnoreCase)
            || item.StartsWith("PaymentRefs=", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ParseDate(string? raw)
    {
        var value = Clean(raw);
        if (string.IsNullOrWhiteSpace(value)) return DateTime.Today;
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial) && serial > 30000 && serial < 90000)
        {
            return DateTime.FromOADate(serial).Date;
        }
        if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact.Date;
        }
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("en-IN"), DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }
        return DateTime.Today;
    }

    private static decimal ParseDecimal(string? raw)
    {
        var value = Clean(raw);
        if (string.IsNullOrWhiteSpace(value)) return 0m;
        value = value.Replace("₹", string.Empty).Replace("INR", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(",", string.Empty).Trim();
        return decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();
        return string.Equals(clean, "None", StringComparison.OrdinalIgnoreCase) ? null : clean;
    }

    private static string? CleanMobile(string? value)
    {
        var digits = Regex.Replace(value ?? string.Empty, "\\D+", string.Empty);
        if (digits.Length > 10 && digits.StartsWith("91", StringComparison.Ordinal))
        {
            digits = digits[^10..];
        }
        return digits.Length >= 10 ? digits[^10..] : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim();

    private sealed record VyaparBillSettlement(decimal BillAmount, decimal BillDiscountAmount, bool PaidWithZeroBalance);

    private sealed record VyaparParsedSaleRow(
        string InvoiceNumber,
        DateTime InvoiceDate,
        string PartyName,
        string? MobileNumber,
        string? Gstin,
        string? PaymentTypeRaw,
        string? PaymentStatus,
        decimal TotalAmount,
        decimal ReceivedAmount,
        decimal BalanceDue,
        IReadOnlyList<VyaparSaleImportPaymentDto> Payments,
        string? Description,
        string? TransactionTypeRaw = null);

    private sealed record VyaparParsedItemRow(
        string InvoiceNumber,
        DateTime InvoiceDate,
        string PartyName,
        string ItemName,
        string? ItemCode,
        string? HsnCode,
        string? Category,
        string? Description,
        string? Size,
        decimal Quantity,
        string? Unit,
        decimal UnitPrice,
        decimal DiscountPercent,
        decimal DiscountAmount,
        decimal TaxRate,
        decimal TaxAmount,
        decimal LineTotal,
        string? TransactionTypeRaw = null);

    private sealed record ImportStockCreateResult(Product Product, Stock Stock, bool ProductCreated, bool StockCreated, bool BridgeMovementCreated);

    private sealed class HeaderTable
    {
        private HeaderTable(IReadOnlyList<string> headers, IReadOnlyList<RowRecord> records)
        {
            Headers = headers;
            Records = records;
        }

        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<RowRecord> Records { get; }

        public static HeaderTable? FromRows(IReadOnlyList<IReadOnlyList<string?>> rows, IReadOnlyList<string> requiredHeaders)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                var normalized = rows[index].Select(item => item?.Trim() ?? string.Empty).ToList();
                if (!requiredHeaders.All(required => normalized.Any(header => HeaderEquals(header, required))))
                {
                    continue;
                }

                var records = new List<RowRecord>();
                foreach (var row in rows.Skip(index + 1))
                {
                    if (row.All(item => string.IsNullOrWhiteSpace(item)))
                    {
                        continue;
                    }
                    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    for (var column = 0; column < normalized.Count; column++)
                    {
                        var header = normalized[column];
                        if (string.IsNullOrWhiteSpace(header))
                        {
                            continue;
                        }
                        values[header] = column < row.Count ? row[column] : null;
                    }
                    records.Add(new RowRecord(values));
                }
                return new HeaderTable(normalized, records);
            }
            return null;
        }
    }

    private sealed class RowRecord(Dictionary<string, string?> values)
    {
        public string? Get(string header)
        {
            if (values.TryGetValue(header, out var value))
            {
                return value;
            }
            var pair = values.FirstOrDefault(item => HeaderEquals(item.Key, header));
            return pair.Value;
        }
    }

    private sealed class VyaparWorkbook
    {
        private readonly Dictionary<string, IReadOnlyList<IReadOnlyList<string?>>> sheets;
        private VyaparWorkbook(Dictionary<string, IReadOnlyList<IReadOnlyList<string?>>> sheets) => this.sheets = sheets;
        public IReadOnlyList<IReadOnlyList<string?>> GetSheet(string name)
            => sheets.TryGetValue(name, out var rows)
                ? rows
                : sheets.FirstOrDefault(item => item.Key.Contains(name, StringComparison.OrdinalIgnoreCase)).Value ?? [];

        public static VyaparWorkbook FromCsv(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var rows = new List<IReadOnlyList<string?>>();
            while (!reader.EndOfStream)
            {
                rows.Add(ParseCsvLine(reader.ReadLine() ?? string.Empty));
            }
            return new VyaparWorkbook(new Dictionary<string, IReadOnlyList<IReadOnlyList<string?>>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Item Details"] = rows,
                ["Sale Report"] = rows
            });
        }

        public static VyaparWorkbook FromXlsx(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            var sharedStrings = ReadSharedStrings(archive, main);
            var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidOperationException("Invalid XLSX: workbook.xml not found.");
            var workbookDoc = XDocument.Load(workbookEntry.Open());
            var relEntry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidOperationException("Invalid XLSX: workbook rels not found.");
            var relDoc = XDocument.Load(relEntry.Open());
            var rels = relDoc.Root?.Elements(packageRelNs + "Relationship")
                .ToDictionary(item => item.Attribute("Id")?.Value ?? string.Empty, item => item.Attribute("Target")?.Value ?? string.Empty)
                ?? new Dictionary<string, string>();

            var result = new Dictionary<string, IReadOnlyList<IReadOnlyList<string?>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheet in workbookDoc.Descendants(main + "sheet"))
            {
                var sheetName = sheet.Attribute("name")?.Value ?? "Sheet";
                var relId = sheet.Attribute(relNs + "id")?.Value ?? string.Empty;
                if (!rels.TryGetValue(relId, out var target) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }
                var sheetPath = target.StartsWith("/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target;
                var sheetEntry = archive.GetEntry(sheetPath);
                if (sheetEntry is null)
                {
                    continue;
                }
                result[sheetName] = ReadSheet(sheetEntry, sharedStrings, main);
            }
            return new VyaparWorkbook(result);
        }

        private static List<string> ReadSharedStrings(ZipArchive archive, XNamespace main)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null) return [];
            var doc = XDocument.Load(entry.Open());
            return doc.Descendants(main + "si")
                .Select(si => string.Concat(si.Descendants(main + "t").Select(t => t.Value)))
                .ToList();
        }

        private static IReadOnlyList<IReadOnlyList<string?>> ReadSheet(ZipArchiveEntry entry, IReadOnlyList<string> sharedStrings, XNamespace main)
        {
            var doc = XDocument.Load(entry.Open());
            var rows = new List<IReadOnlyList<string?>>();
            foreach (var row in doc.Descendants(main + "row"))
            {
                var values = new SortedDictionary<int, string?>();
                foreach (var cell in row.Elements(main + "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? "A1";
                    var column = ColumnIndex(reference);
                    var type = cell.Attribute("t")?.Value;
                    var raw = cell.Element(main + "v")?.Value;
                    var value = raw;
                    if (type == "s" && int.TryParse(raw, out var stringIndex) && stringIndex >= 0 && stringIndex < sharedStrings.Count)
                    {
                        value = sharedStrings[stringIndex];
                    }
                    else if (type == "inlineStr")
                    {
                        value = string.Concat(cell.Descendants(main + "t").Select(item => item.Value));
                    }
                    values[column] = value;
                }
                if (values.Count == 0)
                {
                    rows.Add([]);
                    continue;
                }
                var max = values.Keys.Max();
                var rowValues = new string?[max + 1];
                foreach (var pair in values)
                {
                    rowValues[pair.Key] = pair.Value;
                }
                rows.Add(rowValues);
            }
            return rows;
        }

        private static int ColumnIndex(string cellReference)
        {
            var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
            var total = 0;
            foreach (var letter in letters)
            {
                total = total * 26 + (letter - 'A' + 1);
            }
            return Math.Max(total - 1, 0);
        }

        private static IReadOnlyList<string?> ParseCsvLine(string line)
        {
            var values = new List<string?>();
            var current = new StringBuilder();
            var quoted = false;
            for (var index = 0; index < line.Length; index++)
            {
                var ch = line[index];
                if (ch == '"')
                {
                    if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (ch == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }
            values.Add(current.ToString());
            return values;
        }
    }

    private static bool HeaderEquals(string? actual, string expected)
        => string.Equals(NormalizeHeader(actual), NormalizeHeader(expected), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeHeader(string? value)
        => Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9]", string.Empty).Trim().ToLowerInvariant();
}

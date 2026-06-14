using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Commercial;
using Garmetix.Api.Gstin;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Purchase;

public static class PurchaseEndpoints
{
    public static RouteGroupBuilder MapPurchaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase")
            .WithTags("Purchase")
            .RequireAuthorization(GarmetixPolicies.Purchase);

        group.MapGet("/lookup-options", GetLookupOptionsAsync);
        group.MapGet("/invoices/recent", GetRecentPurchaseInvoicesAsync);
        group.MapGet("/invoices/{id:guid}/receipt", GetReceiptAsync);
        group.MapGet("/invoices/{id:guid}/returnable", GetReturnablePurchaseInvoiceAsync);
        group.MapGet("/invoices/{id:guid}/pdf", DownloadPurchasePdfAsync);
        group.MapPost("/inward", CreateInwardAsync);
        group.MapPost("/invoices/{id:guid}/partial-return", CreatePartialPurchaseReturnAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/invoices/{id:guid}/payment-voucher", CreateVendorPaymentVoucherAsync);
        group.MapPost("/invoices/{id:guid}/cancel", CancelPurchaseAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<PurchaseLookupOptionsDto> GetLookupOptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var categories = await WorkspaceScope.ApplyTo(db.ProductCategories.AsNoTracking(), context)
            .Where(item => item.IsActive)
            .OrderBy(item => item.ProductGroup)
            .ThenBy(item => item.Name)
            .Select(item => new PurchaseLookupOptionDto(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var subCategories = await WorkspaceScope.ApplyTo(db.ProductSubCategories.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new PurchaseSubCategoryOptionDto(item.Id, item.Name, item.CategoryId))
            .ToListAsync(cancellationToken);

        var taxes = await WorkspaceScope.ApplyTo(db.Taxes.AsNoTracking(), context)
            .OrderBy(item => item.TaxType)
            .ThenBy(item => item.CompositeRate)
            .Select(item => new PurchaseTaxOptionDto(item.Id, string.IsNullOrWhiteSpace(item.Name) ? $"GST {item.CompositeRate:N2}%" : item.Name, item.CompositeRate, item.TaxType.ToString()))
            .ToListAsync(cancellationToken);

        var vendors = await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
            .Where(item => item.Active)
            .OrderBy(item => item.Name)
            .Select(item => new PurchaseVendorOptionDto(
                item.Id,
                item.Name,
                item.MobileNumber,
                item.GSTIN,
                item.BillAmount,
                item.Paid,
                item.BillAmount - item.Paid))
            .ToListAsync(cancellationToken);

        return new PurchaseLookupOptionsDto(
            categories,
            subCategories,
            taxes,
            vendors,
            EnumOptions<Unit>(),
            EnumOptions<ProductType>(),
            EnumOptions<ProductGroup>());
    }

    private static async Task<IReadOnlyList<RecentPurchaseInvoiceDto>> GetRecentPurchaseInvoicesAsync(HttpContext context, GarmetixDbContext db, int take = 50, CancellationToken cancellationToken = default)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).ToArray();
        var paidLookup = await GetPaidAmountLookupAsync(invoiceIds, db, cancellationToken);

        return invoices.Select(invoice =>
        {
            paidLookup.TryGetValue(invoice.Id, out var paidAmount);
            paidAmount = invoice.InvoiceStatus == InvoiceStatus.Cancelled ? 0 : paidAmount;
            return new RecentPurchaseInvoiceDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InwardNumber,
                invoice.OnDate,
                invoice.InwardDate,
                invoice.SupplierInvoiceDate,
                invoice.DueDate,
                invoice.VendorId,
                invoice.VendorName ?? "Supplier",
                invoice.VendorGSTIN,
                invoice.BillAmount,
                paidAmount,
                Math.Max(invoice.BillAmount - paidAmount, 0),
                invoice.FrightAmount,
                invoice.ItemCount,
                invoice.Quantity,
                invoice.InvoiceStatus.ToString(),
                invoice.PaymentMode?.ToString() ?? "-"
            );
        }).ToList();
    }

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var receipt = await LoadReceiptAsync(id, context, db, cancellationToken);
        return receipt is null ? Results.NotFound() : Results.Ok(receipt);
    }

    private static async Task<IResult> DownloadPurchasePdfAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        string? format,
        string? copy,
        bool reprint = false,
        bool signatures = true,
        CancellationToken cancellationToken = default)
    {
        var receipt = await LoadReceiptAsync(id, context, db, cancellationToken);
        if (receipt is null)
        {
            return Results.NotFound();
        }

        var pdf = PurchasePdfDocument.Build(
            new PurchasePdfModel(
                receipt.CompanyName,
                receipt.CompanyAddress,
                receipt.CompanyPhone,
                receipt.CompanyGstin,
                receipt.StoreName,
                receipt.InvoiceNumber,
                receipt.InwardNumber,
                receipt.OnDate,
                receipt.InvoiceStatus,
                receipt.VendorName,
                receipt.VendorGstin,
                receipt.MRP,
                receipt.DiscountAmount,
                receipt.NetAmount,
                receipt.TaxAmount,
                receipt.FreightAmount,
                receipt.RoundOff,
                receipt.BillAmount,
                receipt.PaidAmount,
                receipt.BalanceAmount,
                receipt.Items,
                Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.PurchaseInvoice, receipt.Id)),
            format,
            copy,
            reprint,
            signatures);

        var safeNumber = new string(receipt.InvoiceNumber.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "purchase-invoice")}-{NormalizePdfFormat(format)}.pdf");
    }


    private static async Task<IResult> GetReturnablePurchaseInvoiceAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var dto = await BuildReturnablePurchaseInvoiceAsync(id, context, db, cancellationToken);
        return dto is null ? Results.NotFound(new { message = "Purchase invoice was not found." }) : Results.Ok(dto);
    }

    private static async Task<ReturnablePurchaseInvoiceDto?> BuildReturnablePurchaseInvoiceAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        var returnedRows = await db.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == invoice.CompanyId
                && movement.SourceType == "PurchaseReturn"
                && movement.SourceId == invoice.Id
                && movement.MovementType == "PurchaseReturnOut")
            .GroupBy(movement => new { movement.ProductId, movement.Barcode })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(row => row.QuantityOut)
            })
            .ToListAsync(cancellationToken);

        var returnedLookup = returnedRows.ToDictionary(
            row => PurchaseReturnKey(row.ProductId, row.Barcode),
            row => row.Quantity);

        var paidAmount = invoice.InvoiceStatus == InvoiceStatus.Cancelled
            ? 0
            : await GetPaidAmountAsync(invoice.Id, db, cancellationToken);

        var dtoItems = items.Select(item =>
        {
            returnedLookup.TryGetValue(PurchaseReturnKey(item.ProductId, item.Barcode), out var alreadyReturned);
            alreadyReturned = Math.Min(Math.Max(alreadyReturned, 0), item.BilledQuantity);
            var returnable = Math.Max(item.BilledQuantity - alreadyReturned, 0);
            var quantity = item.BilledQuantity <= 0 ? 1 : item.BilledQuantity;
            return new ReturnablePurchaseItemDto(
                item.Id,
                item.ProductId,
                item.ProductName ?? item.Barcode,
                item.Barcode,
                item.HSNCode,
                item.Unit?.ToString() ?? Unit.Pcs.ToString(),
                item.BilledQuantity,
                alreadyReturned,
                invoice.InvoiceStatus == InvoiceStatus.Cancelled ? 0 : returnable,
                Math.Round(item.Amount / quantity, 2),
                Math.Round(item.BasePrice / quantity, 2),
                Math.Round(item.TaxAmount / quantity, 2),
                Math.Round(item.DiscountAmount / quantity, 2),
                item.MRP,
                item.TaxPercentage,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount);
        }).ToList();

        return new ReturnablePurchaseInvoiceDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            invoice.OnDate,
            invoice.InwardDate,
            invoice.VendorId,
            invoice.VendorName ?? "Supplier",
            invoice.VendorGSTIN,
            invoice.BillAmount,
            paidAmount,
            Math.Max(invoice.BillAmount - paidAmount, 0),
            invoice.InvoiceStatus.ToString(),
            dtoItems);
    }

    private static async Task<IResult> CreatePartialPurchaseReturnAsync(
        Guid id,
        PartialPurchaseReturnRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "Select at least one item to return." });
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return Results.BadRequest(new { message = "Return quantity must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cannot create partial return for a cancelled purchase invoice." });
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == invoice.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Purchase vendor was not found." });
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Could not determine purchase store for stock return." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == storeId && store.CompanyId == invoice.CompanyId && store.StoreGroupId == storeGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Purchase store is outside your access scope." });
        }

        var requestedLookup = request.Items
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var requestedItemIds = requestedLookup.Keys.ToArray();
        var invoiceItems = await db.PurchaseInvoiceItems
            .Where(item => item.InvoiceId == invoice.Id && requestedItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (invoiceItems.Count != requestedLookup.Count)
        {
            return Results.BadRequest(new { message = "One or more selected return items were not found in this purchase invoice." });
        }

        var returnedRows = await db.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == invoice.CompanyId
                && movement.SourceType == "PurchaseReturn"
                && movement.SourceId == invoice.Id
                && movement.MovementType == "PurchaseReturnOut")
            .GroupBy(movement => new { movement.ProductId, movement.Barcode })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(row => row.QuantityOut)
            })
            .ToListAsync(cancellationToken);

        var returnedLookup = returnedRows.ToDictionary(
            row => PurchaseReturnKey(row.ProductId, row.Barcode),
            row => row.Quantity);

        var returnDate = request.ReturnDate ?? DateTime.Now;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Partial purchase return" : request.Reason.Trim();
        decimal returnedQuantity = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal returnAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        var movementRemarks = new List<string>();

        foreach (var item in invoiceItems)
        {
            var requestedQuantity = requestedLookup[item.Id];
            var key = PurchaseReturnKey(item.ProductId, item.Barcode);
            returnedLookup.TryGetValue(key, out var alreadyReturned);
            var returnableQuantity = Math.Max(item.BilledQuantity - alreadyReturned, 0);

            if (requestedQuantity > returnableQuantity)
            {
                return Results.BadRequest(new { message = $"Return quantity for {item.ProductName ?? item.Barcode} exceeds returnable quantity {returnableQuantity:N2}." });
            }

            await DocumentNumberGenerator.LockStockKeyAsync(db, invoice.CompanyId, storeGroupId, storeId, item.ProductId, item.Barcode, cancellationToken);

            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == invoice.CompanyId &&
                stock.StoreGroupId == storeGroupId &&
                stock.StoreId == storeId &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode &&
                !stock.IsOFB,
                cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock row was not found for {item.ProductName ?? item.Barcode}." });
            }

            if (stock.PurchaseQty < requestedQuantity)
            {
                return Results.BadRequest(new { message = $"Current inward quantity for {item.ProductName ?? item.Barcode} is lower than requested return quantity." });
            }

            if (stock.CurrentStock < requestedQuantity)
            {
                return Results.BadRequest(new { message = $"Available stock for {item.ProductName ?? item.Barcode} is lower than requested return quantity." });
            }

            var ratio = item.BilledQuantity <= 0 ? 0 : requestedQuantity / item.BilledQuantity;
            var lineTaxable = Math.Round(item.BasePrice * ratio, 2);
            var lineTax = Math.Round(item.TaxAmount * ratio, 2);
            var lineAmount = Math.Round(item.Amount * ratio, 2);
            var lineCgst = Math.Round((item.CGSTAmount ?? 0) * ratio, 2);
            var lineSgst = Math.Round((item.SGSTAmount ?? 0) * ratio, 2);
            var lineIgst = Math.Round((item.IGSTAmount ?? 0) * ratio, 2);

            stock.PurchaseQty = Math.Max(stock.PurchaseQty - requestedQuantity, 0);

            db.StockMovements.Add(new StockMovement
            {
                StockId = stock.Id,
                ProductId = stock.ProductId,
                Barcode = stock.Barcode,
                MovementType = "PurchaseReturnOut",
                QuantityOut = requestedQuantity,
                CostPrice = stock.CostPrice,
                MRP = stock.MRP,
                TaxRate = item.TaxPercentage,
                HSNCode = item.HSNCode ?? stock.HSNCode,
                SourceType = "PurchaseReturn",
                SourceId = invoice.Id,
                SourceNumber = invoice.InvoiceNumber,
                Remarks = reason,
                OnDate = returnDate,
                CompanyId = invoice.CompanyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId
            });

            returnedQuantity += requestedQuantity;
            taxableAmount += lineTaxable;
            taxAmount += lineTax;
            returnAmount += lineAmount;
            cgstAmount += lineCgst;
            sgstAmount += lineSgst;
            igstAmount += lineIgst;
            movementRemarks.Add($"{item.ProductName ?? item.Barcode} x {requestedQuantity:N2}");
            returnedLookup[key] = alreadyReturned + requestedQuantity;
        }

        returnAmount = Math.Round(taxableAmount + taxAmount, 2);

        if (returnedQuantity <= 0 || returnAmount <= 0)
        {
            return Results.BadRequest(new { message = "Selected return has no billable value." });
        }

        var debitNote = await CommercialEndpoints.CreateDebitNoteFromPurchaseReturnAsync(
            invoice,
            vendor,
            reason,
            storeGroupId,
            storeId,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            string.Join(", ", movementRemarks),
            db,
            cancellationToken);

        debitNote.Remarks = $"Partial purchase return: {string.Join(", ", movementRemarks)}";
        vendor.BillAmount = Math.Max(vendor.BillAmount - Math.Round(returnAmount, 2), 0);

        var allItems = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .Select(item => new { item.ProductId, item.Barcode, item.BilledQuantity })
            .ToListAsync(cancellationToken);
        var fullyReturned = allItems.All(item =>
        {
            returnedLookup.TryGetValue(PurchaseReturnKey(item.ProductId, item.Barcode), out var quantity);
            return quantity >= item.BilledQuantity;
        });

        invoice.InvoiceStatus = fullyReturned ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded;

        await accounting.PostPurchaseReturnAsync(
            invoice,
            vendor,
            debitNote.Id,
            debitNote.NoteNumber,
            storeGroupId,
            storeId,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            reason,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new PartialPurchaseReturnResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            debitNote.Id,
            debitNote.NoteNumber,
            returnedQuantity,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            invoice.InvoiceStatus.ToString()));
    }

    private static async Task<PurchaseReceiptDto?> LoadReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoice.CompanyId, cancellationToken);
        var sourceJournal = await db.JournalEntries.AsNoTracking().FirstOrDefaultAsync(item => item.SourceType == "PurchaseInvoice" && item.SourceId == invoice.Id, cancellationToken);
        var resolvedStoreId = invoice.StoreId ?? sourceJournal?.StoreId;
        var storeName = resolvedStoreId.HasValue
            ? await db.Stores.AsNoTracking().Where(item => item.Id == resolvedStoreId.Value).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken) ?? "Purchase Store"
            : "Purchase Store";

        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .Join(db.Products.AsNoTracking(), item => item.ProductId, product => product.Id, (item, product) => new PurchaseReceiptItemDto(
                item.ProductName ?? product.Name,
                item.Barcode,
                string.IsNullOrWhiteSpace(item.HSNCode) ? product.HSNCode : item.HSNCode,
                (item.Unit ?? product.Unit).ToString(),
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxPercentage,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.Amount))
            .ToListAsync(cancellationToken);

        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(payment => payment.PurchaseInvoiceId == invoice.Id)
            .OrderBy(payment => payment.OnDate)
            .Select(payment => new PurchasePaymentDto(
                payment.Id,
                payment.OnDate,
                payment.Amount,
                payment.PaymentMode.ToString(),
                payment.ReferenceNumber,
                payment.VoucherId))
            .ToListAsync(cancellationToken);

        var paidAmount = payments.Sum(payment => payment.Amount);
        if (paidAmount <= 0)
        {
            paidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        }
        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            paidAmount = 0;
        }

        return new PurchaseReceiptDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            invoice.OnDate,
            invoice.InwardDate,
            invoice.SupplierInvoiceDate,
            invoice.DueDate,
            company?.Name ?? "Garmetix",
            BuildCompanyAddress(company),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            storeName,
            invoice.VendorId,
            invoice.VendorName ?? "Supplier",
            invoice.VendorGSTIN,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.FrightAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            paidAmount,
            Math.Max(invoice.BillAmount - paidAmount, 0),
            invoice.InvoiceStatus.ToString(),
            invoice.PaymentMode?.ToString() ?? "-",
            items,
            payments);
    }

    private static async Task<IResult> CreateInwardAsync(
        PurchaseInwardRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        GstinLookupService gstinLookup,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VendorName))
        {
            return Results.BadRequest(new { message = "Vendor name is required." });
        }

        if (request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one purchase item is required." });
        }

        if (request.Items.Any(item => item.Quantity <= 0 || item.CostPrice < 0 || item.Mrp < 0))
        {
            return Results.BadRequest(new { message = "Quantity, cost price, and MRP must be valid." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected purchase store is outside your access scope." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var vendorValidation = !string.IsNullOrWhiteSpace(request.VendorGstin)
            ? await gstinLookup.ValidatePartyAsync("Vendor", request.VendorGstin, request.VendorName, null, cancellationToken)
            : null;
        var vendor = await GetOrCreateVendorAsync(request, db, gstinLookup, vendorValidation, cancellationToken);
        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? await documentNumbers.NextPurchaseInvoiceAsync(request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken)
            : request.InvoiceNumber.Trim();
        var inwardNumber = string.IsNullOrWhiteSpace(request.InwardNumber)
            ? await documentNumbers.NextPurchaseInwardAsync(request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken)
            : request.InwardNumber.Trim();
        var invoiceId = Guid.NewGuid();

        var invoiceItems = new List<PurchaseInvoiceItem>();
        decimal grossMrp = 0;
        decimal discountAmount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        decimal totalQuantity = 0;

        foreach (var requestItem in request.Items)
        {
            var product = await GetOrCreateProductAsync(request, requestItem, db, cancellationToken);
            if (product is null)
            {
                return Results.BadRequest(new { message = "Run quick setup and select product category, subcategory, and tax before purchasing new products." });
            }

            var tax = requestItem.TaxId.HasValue
                ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == requestItem.TaxId.Value, cancellationToken)
                : await db.Taxes.FirstOrDefaultAsync(item => item.CompositeRate == product.TaxRate && item.TaxType == product.TaxType, cancellationToken);
            tax ??= await db.Taxes.FirstOrDefaultAsync(cancellationToken);

            if (tax is null)
            {
                return Results.BadRequest(new { message = "Tax setup is required before purchase inward." });
            }

            var barcode = string.IsNullOrWhiteSpace(requestItem.Barcode) ? product.Barcode : requestItem.Barcode.Trim();
            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var lineCost = requestItem.CostPrice * requestItem.Quantity;
            var lineNet = Math.Max(lineCost - lineDiscount, 0);
            var taxable = Math.Round(lineNet / (1 + (tax.CompositeRate / 100)), 2);
            var taxValue = Math.Round(taxable * (tax.CompositeRate / 100), 2);
            var lineAmount = taxable + taxValue;
            var split = SplitGst(taxValue, tax.TaxType);
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
                TaxType = tax.TaxType,
                TaxId = tax.Id,
                BilledQuantity = requestItem.Quantity,
                CompanyId = request.CompanyId
            });

            await DocumentNumberGenerator.LockStockKeyAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, product.Id, barcode, cancellationToken);

            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context).FirstOrDefaultAsync(item =>
                item.ProductId == product.Id &&
                item.Barcode == barcode &&
                item.StoreId == request.StoreId &&
                !item.IsOFB,
                cancellationToken);

            if (stock is null)
            {
                stock = new Stock
                {
                    ProductId = product.Id,
                    Barcode = barcode,
                    Unit = unit,
                    HSNCode = hsnCode,
                    PurchaseQty = requestItem.Quantity,
                    CostPrice = requestItem.CostPrice,
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
                stock.PurchaseQty += requestItem.Quantity;
                stock.CostPrice = requestItem.CostPrice;
                stock.MRP = requestItem.Mrp;
                stock.TaxRate = tax.CompositeRate;
                stock.TaxType = tax.TaxType;
                stock.TaxId = tax.Id;
                stock.HSNCode = string.IsNullOrWhiteSpace(hsnCode) ? stock.HSNCode : hsnCode;
                stock.Unit = unit;
            }

            db.StockMovements.Add(new StockMovement
            {
                StockId = stock.Id,
                ProductId = product.Id,
                Barcode = barcode,
                MovementType = "PurchaseIn",
                QuantityIn = requestItem.Quantity,
                CostPrice = requestItem.CostPrice,
                MRP = requestItem.Mrp,
                TaxRate = tax.CompositeRate,
                HSNCode = hsnCode,
                SourceType = "PurchaseInvoice",
                SourceId = invoiceId,
                SourceNumber = invoiceNumber,
                Remarks = "Purchase inward",
                OnDate = DateTime.Now,
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
            });

            product.MRP = requestItem.Mrp;
            product.TaxRate = tax.CompositeRate;
            product.TaxType = tax.TaxType;
            if (!string.IsNullOrWhiteSpace(hsnCode))
            {
                product.HSNCode = hsnCode;
            }
            product.Unit = unit;
            if (requestItem.ProductType.HasValue)
            {
                product.ProductType = requestItem.ProductType.Value;
            }
            if (requestItem.ProductGroup.HasValue)
            {
                product.ProductGroup = requestItem.ProductGroup.Value;
            }

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
            InwardDate = DateTime.Now,
            OnDate = DateTime.Now,
            InvoiceType = InvoiceType.Regular,
            InvoiceStatus = paidAmount <= 0
                ? InvoiceStatus.Pending
                : paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            CGSTAmount = cgstAmount,
            SGSTAmount = sgstAmount,
            IGSTAmount = igstAmount,
            InterState = igstAmount > 0,
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
            SupplierInvoiceDate = request.SupplierInvoiceDate,
            DueDate = request.DueDate ?? DateTime.Today.AddDays(45),
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            CompanyId = request.CompanyId
        };

        if (!WorkspaceScope.CanWrite(invoice, context, out var invoiceScopeMessage))
        {
            return Results.BadRequest(new { message = invoiceScopeMessage ?? "Selected company is outside your access scope." });
        }

        db.PurchaseInvoices.Add(invoice);
        db.PurchaseInvoiceItems.AddRange(invoiceItems);
        if (paidAmount > 0)
        {
            db.PurchasePayments.Add(new PurchasePayment
            {
                PurchaseInvoiceId = invoice.Id,
                VendorId = vendor.Id,
                OnDate = DateTime.Now,
                Amount = paidAmount,
                PaymentMode = request.PaymentMode,
                BankAccountId = request.BankAccountId,
                ReferenceNumber = invoice.InvoiceNumber,
                Remarks = "Purchase inward payment",
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

        return Results.Created($"/api/purchase/invoices/{invoice.Id}/receipt", new PurchaseInwardResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            vendor.Id,
            invoice.BillAmount,
            paidAmount,
            invoice.ItemCount,
            invoice.Quantity,
            vendorValidation?.Alerts ?? Array.Empty<string>()));
    }

    private static async Task<IResult> CreateVendorPaymentVoucherAsync(
        Guid id,
        VendorPaymentVoucherRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cannot pay a cancelled purchase invoice." });
        }

        var existingPaidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        var balanceAmount = Math.Max(invoice.BillAmount - existingPaidAmount, 0);
        if (balanceAmount <= 0)
        {
            return Results.Conflict(new { message = "Purchase invoice is already fully paid." });
        }

        if (request.Amount > balanceAmount)
        {
            return Results.BadRequest(new { message = $"Payment amount cannot exceed purchase balance {balanceAmount:N2}." });
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Could not determine purchase store for vendor payment." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == storeId && store.CompanyId == invoice.CompanyId && store.StoreGroupId == storeGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected purchase store is outside your access scope." });
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == invoice.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Purchase vendor was not found." });
        }

        var paymentAmount = request.Amount;
        var voucherNumber = await documentNumbers.NextVendorPaymentVoucherAsync(invoice.CompanyId, storeGroupId, storeId, cancellationToken);
        var particulars = string.IsNullOrWhiteSpace(request.PaymentDetails)
            ? $"Vendor payment against purchase invoice {invoice.InvoiceNumber}"
            : request.PaymentDetails.Trim();

        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            VoucherNumber = voucherNumber,
            OnDate = DateTime.Now,
            VoucherType = VoucherType.Payment,
            PartyName = vendor.Name,
            Particulars = particulars,
            Amount = paymentAmount,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? $"Purchase invoice {invoice.InvoiceNumber}" : request.Remarks.Trim(),
            SlipNumber = string.IsNullOrWhiteSpace(request.SlipNumber) ? null : request.SlipNumber.Trim(),
            PaymentMode = request.PaymentMode,
            PaymentDetails = request.PaymentDetails,
            AccountNumber = request.BankAccountId,
            IsParty = true,
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };

        if (!WorkspaceScope.CanWrite(voucher, context, out var scopeMessage))
        {
            return Results.BadRequest(new { message = scopeMessage ?? "Selected company/store is outside your access scope." });
        }

        db.Vouchers.Add(voucher);
        db.PurchasePayments.Add(new PurchasePayment
        {
            PurchaseInvoiceId = invoice.Id,
            VendorId = vendor.Id,
            OnDate = DateTime.Now,
            Amount = paymentAmount,
            PaymentMode = request.PaymentMode,
            BankAccountId = request.BankAccountId,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.SlipNumber) ? voucher.VoucherNumber : request.SlipNumber.Trim(),
            VoucherId = voucher.Id,
            Remarks = voucher.Remarks,
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        });
        vendor.Paid += paymentAmount;

        var newPaidAmount = existingPaidAmount + paymentAmount;
        invoice.PaymentMode = request.PaymentMode;
        invoice.InvoiceStatus = newPaidAmount >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        await accounting.PostVendorPaymentVoucherAsync(voucher, vendor, storeGroupId, storeId, request.BankAccountId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new VendorPaymentVoucherResponse(
            voucher.Id,
            voucher.VoucherNumber,
            invoice.Id,
            invoice.InvoiceNumber,
            paymentAmount,
            newPaidAmount,
            Math.Max(invoice.BillAmount - newPaidAmount, 0),
            invoice.InvoiceStatus.ToString()));
    }

    private static async Task<IResult> CancelPurchaseAsync(
        Guid id,
        CancelPurchaseInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Purchase invoice is already cancelled." });
        }

        if (invoice.InvoiceStatus is InvoiceStatus.PartiallyRefunded or InvoiceStatus.Refunded)
        {
            return Results.Conflict(new { message = "This purchase invoice already has item-wise purchase returns. Use the partial return screen for remaining quantities instead of full cancel." });
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Could not determine purchase store for stock reversal." });
        }

        var invoiceItems = await db.PurchaseInvoiceItems
            .Where(item => item.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        decimal reversedQuantity = 0;
        foreach (var item in invoiceItems)
        {
            await DocumentNumberGenerator.LockStockKeyAsync(db, invoice.CompanyId, storeGroupId, storeId, item.ProductId, item.Barcode, cancellationToken);

            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == invoice.CompanyId &&
                stock.StoreGroupId == storeGroupId &&
                stock.StoreId == storeId &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode &&
                !stock.IsOFB,
                cancellationToken);

            if (stock is not null)
            {
                stock.PurchaseQty = Math.Max(stock.PurchaseQty - item.BilledQuantity, 0);
                db.StockMovements.Add(new StockMovement
                {
                    StockId = stock.Id,
                    ProductId = stock.ProductId,
                    Barcode = stock.Barcode,
                    MovementType = "PurchaseReturnOut",
                    QuantityOut = item.BilledQuantity,
                    CostPrice = stock.CostPrice,
                    MRP = stock.MRP,
                    TaxRate = stock.TaxRate,
                    HSNCode = item.HSNCode ?? stock.HSNCode,
                    SourceType = "PurchaseReturn",
                    SourceId = invoice.Id,
                    SourceNumber = invoice.InvoiceNumber,
                    Remarks = string.IsNullOrWhiteSpace(request.Reason) ? "Purchase return/cancel" : request.Reason,
                    OnDate = DateTime.Now,
                    CompanyId = invoice.CompanyId,
                    StoreGroupId = storeGroupId,
                    StoreId = storeId
                });
                reversedQuantity += item.BilledQuantity;
            }
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == invoice.VendorId, cancellationToken);
        var originalPaidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        var originalPaymentMode = invoice.PaymentMode;
        var bankAccountId = await db.BankTransactions.AsNoTracking()
            .Where(item => item.CompanyId == invoice.CompanyId && item.Reference == $"PI-{invoice.InvoiceNumber}")
            .Select(item => (Guid?)item.BankAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (vendor is not null)
        {
            vendor.BillCount = Math.Max(vendor.BillCount - 1, 0);
            vendor.BillAmount = Math.Max(vendor.BillAmount - invoice.BillAmount, 0);
            vendor.Paid = Math.Max(vendor.Paid - originalPaidAmount, 0);
        }

        if (vendor is not null)
        {
            await CommercialEndpoints.CreateDebitNoteFromPurchaseReturnAsync(invoice, vendor, request.Reason, storeGroupId, storeId, db, cancellationToken);
        }

        await accounting.PostPurchaseInvoiceCancellationAsync(invoice, vendor, storeGroupId, storeId, originalPaidAmount, originalPaymentMode, bankAccountId, cancellationToken);

        // Keep original purchase values for audit and print history. Reversal is represented by status, stock movement, debit note, and accounting reversal.
        invoice.InvoiceStatus = InvoiceStatus.Cancelled;
        invoice.PaymentMode = null;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new CancelPurchaseInvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceStatus.ToString(),
            reversedQuantity,
            invoiceItems.Sum(item => item.Amount)));
    }

    private static async Task<Vendor> GetOrCreateVendorAsync(
        PurchaseInwardRequest request,
        GarmetixDbContext db,
        GstinLookupService gstinLookup,
        PartyGstinValidationResponse? validation,
        CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(request.VendorMobileNumber) ? "NA" : request.VendorMobileNumber.Trim();
        var name = request.VendorName.Trim();
        var gstin = GstinLookupService.NormalizeGstin(request.VendorGstin);

        Vendor? vendor = null;
        if (request.VendorId.HasValue && request.VendorId.Value != Guid.Empty)
        {
            vendor = await db.Vendors.FirstOrDefaultAsync(
                item => item.Id == request.VendorId.Value && item.CompanyId == request.CompanyId,
                cancellationToken);
        }

        vendor ??= await db.Vendors.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId &&
                ((!string.IsNullOrWhiteSpace(gstin) && item.GSTIN == gstin) || item.MobileNumber == mobile || item.Name == name),
            cancellationToken);

        if (vendor is not null)
        {
            if (validation is not null)
            {
                gstinLookup.ApplyVerification(vendor, validation);
                if (!string.IsNullOrWhiteSpace(validation.Lookup.PrincipalAddress) && (string.IsNullOrWhiteSpace(vendor.Address) || vendor.Address == "Dumka"))
                {
                    vendor.Address = validation.Lookup.PrincipalAddress;
                }
            }
            else if (!string.IsNullOrWhiteSpace(gstin))
            {
                vendor.GSTIN = gstin;
            }

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

        if (validation is not null)
        {
            gstinLookup.ApplyVerification(vendor, validation);
        }

        db.Vendors.Add(vendor);
        return vendor;
    }

    private static async Task<Product?> GetOrCreateProductAsync(
        PurchaseInwardRequest request,
        PurchaseInwardItemRequest requestItem,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (requestItem.ProductId.HasValue)
        {
            return await db.Products.FirstOrDefaultAsync(item => item.Id == requestItem.ProductId.Value, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(requestItem.ProductName) || string.IsNullOrWhiteSpace(requestItem.Barcode))
        {
            return null;
        }

        var barcode = requestItem.Barcode.Trim();
        var existing = await db.Products.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId && item.Barcode == barcode,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var categoryId = requestItem.ProductCategoryId ?? await db.ProductCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var subCategoryId = requestItem.ProductSubCategoryId ?? await db.ProductSubCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var tax = requestItem.TaxId.HasValue
            ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == requestItem.TaxId.Value, cancellationToken)
            : await db.Taxes.FirstOrDefaultAsync(cancellationToken);

        if (categoryId == Guid.Empty || subCategoryId == Guid.Empty || tax is null)
        {
            return null;
        }

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

    private static async Task<Dictionary<Guid, decimal>> GetPaidAmountLookupAsync(Guid[] invoiceIds, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (invoiceIds.Length == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var purchasePayments = await db.PurchasePayments.AsNoTracking()
            .Where(payment => invoiceIds.Contains(payment.PurchaseInvoiceId))
            .Select(payment => new { InvoiceId = payment.PurchaseInvoiceId, Amount = payment.Amount })
            .ToListAsync(cancellationToken);

        if (purchasePayments.Count > 0)
        {
            return purchasePayments.GroupBy(row => row.InvoiceId).ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
        }

        var rows = await db.JournalLines.AsNoTracking()
            .Where(line => line.JournalEntry != null
                && line.JournalEntry.SourceType == "PurchaseInvoice"
                && line.JournalEntry.SourceId.HasValue
                && invoiceIds.Contains(line.JournalEntry.SourceId.Value)
                && line.Debit > 0
                && (line.Narration ?? string.Empty).Contains("Purchase payment"))
            .Select(line => new
            {
                InvoiceId = line.JournalEntry!.SourceId!.Value,
                Amount = line.Debit
            })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(row => row.InvoiceId).ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
    }

    private static async Task<decimal> GetPaidAmountAsync(Guid invoiceId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var lookup = await GetPaidAmountLookupAsync([invoiceId], db, cancellationToken);
        return lookup.TryGetValue(invoiceId, out var paidAmount) ? paidAmount : 0;
    }

    private static string BuildCompanyAddress(Garmetix.Core.Models.Stores.Company? company)
    {
        if (company is null)
        {
            return string.Empty;
        }

        return string.Join(", ", new[] { company.Address, company.City, company.State, company.ZipCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }


    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitGst(decimal totalTax, TaxType taxType)
    {
        totalTax = Math.Round(totalTax, 2);
        return taxType switch
        {
            TaxType.IGST => (0, 0, totalTax),
            TaxType.CGST => (totalTax, 0, 0),
            TaxType.SGST => (0, totalTax, 0),
            TaxType.GST => (Math.Round(totalTax / 2m, 2), totalTax - Math.Round(totalTax / 2m, 2), 0),
            _ => (0, 0, 0)
        };
    }

    private static string PurchaseReturnKey(Guid productId, string barcode) => $"{productId:N}|{barcode.Trim().ToUpperInvariant()}";

    private static IReadOnlyList<PurchaseEnumOptionDto> EnumOptions<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => new PurchaseEnumOptionDto(Convert.ToInt32(value), value.ToString()))
            .GroupBy(option => option.Value)
            .Select(group => group.First())
            .ToList();

    private static string NormalizePdfFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "a5" or "a5-one" => "a5",
        "thermal-2" or "2-inch" or "thermal2" => "thermal-2",
        "thermal-3" or "3-inch" or "thermal3" => "thermal-3",
        _ => "a4"
    };
}

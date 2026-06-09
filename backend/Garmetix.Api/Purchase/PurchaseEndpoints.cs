using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Gstin;
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
        group.MapGet("/invoices/{id:guid}/pdf", DownloadPurchasePdfAsync);
        group.MapPost("/inward", CreateInwardAsync);
        group.MapPost("/invoices/{id:guid}/payment-voucher", CreateVendorPaymentVoucherAsync);
        group.MapPost("/invoices/{id:guid}/cancel", CancelPurchaseAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<PurchaseLookupOptionsDto> GetLookupOptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var categories = await WorkspaceScope.ApplyTo(db.ProductCategories.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new PurchaseLookupOptionDto(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var subCategories = await WorkspaceScope.ApplyTo(db.ProductSubCategories.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new PurchaseLookupOptionDto(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var taxes = await WorkspaceScope.ApplyTo(db.Taxes.AsNoTracking(), context)
            .OrderBy(item => item.TaxType)
            .ThenBy(item => item.CompositeRate)
            .Select(item => new PurchaseTaxOptionDto(item.Id, string.IsNullOrWhiteSpace(item.Name) ? $"GST {item.CompositeRate:N2}%" : item.Name, item.CompositeRate, item.TaxType.ToString()))
            .ToListAsync(cancellationToken);

        return new PurchaseLookupOptionsDto(categories, subCategories, taxes);
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
                receipt.Items),
            format,
            copy,
            reprint,
            signatures);

        var safeNumber = new string(receipt.InvoiceNumber.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "purchase-invoice")}-{NormalizePdfFormat(format)}.pdf");
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
        var storeName = sourceJournal is null
            ? "Purchase Store"
            : await db.Stores.AsNoTracking().Where(item => item.Id == sourceJournal.StoreId).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken) ?? "Purchase Store";

        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .Join(db.Products.AsNoTracking(), item => item.ProductId, product => product.Id, (item, product) => new PurchaseReceiptItemDto(
                product.Name,
                item.Barcode,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxPercentage,
                item.TaxAmount,
                item.Amount))
            .ToListAsync(cancellationToken);

        var paidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
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
            company?.Name ?? "Garmetix",
            BuildCompanyAddress(company),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            storeName,
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
            items);
    }

    private static async Task<IResult> CreateInwardAsync(
        PurchaseInwardRequest request,
        HttpContext context,
        GarmetixDbContext db,
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
            ? await CreatePurchaseInvoiceNumberAsync(request.CompanyId, db, cancellationToken)
            : request.InvoiceNumber.Trim();
        var inwardNumber = string.IsNullOrWhiteSpace(request.InwardNumber)
            ? await CreateInwardNumberAsync(request.CompanyId, db, cancellationToken)
            : request.InwardNumber.Trim();
        var invoiceId = Guid.NewGuid();

        var invoiceItems = new List<PurchaseInvoiceItem>();
        decimal grossMrp = 0;
        decimal discountAmount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
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

            invoiceItems.Add(new PurchaseInvoiceItem
            {
                InvoiceId = invoiceId,
                ProductId = product.Id,
                Barcode = barcode,
                MRP = requestItem.Mrp,
                DiscountAmount = lineDiscount,
                BasePrice = taxable,
                TaxPercentage = tax.CompositeRate,
                TaxAmount = taxValue,
                Amount = lineAmount,
                TaxType = tax.TaxType,
                TaxId = tax.Id,
                BilledQuantity = requestItem.Quantity,
                CompanyId = request.CompanyId
            });

            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context).FirstOrDefaultAsync(item =>
                item.ProductId == product.Id &&
                item.Barcode == barcode &&
                item.StoreId == request.StoreId,
                cancellationToken);

            if (stock is null)
            {
                stock = new Stock
                {
                    ProductId = product.Id,
                    Barcode = barcode,
                    Unit = Unit.Pcs,
                    PurchaseQty = requestItem.Quantity,
                    CostPrice = requestItem.CostPrice,
                    MRP = requestItem.Mrp,
                    TaxRate = tax.CompositeRate,
                    TaxType = tax.TaxType,
                    TaxId = tax.Id,
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
            }

            product.MRP = requestItem.Mrp;
            product.TaxRate = tax.CompositeRate;
            product.TaxType = tax.TaxType;

            grossMrp += lineMrp;
            discountAmount += lineDiscount;
            taxableAmount += taxable;
            taxAmount += taxValue;
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
            DueDate = DateTime.Today.AddDays(45),
            CompanyId = request.CompanyId
        };

        if (!WorkspaceScope.CanWrite(invoice, context, out var invoiceScopeMessage))
        {
            return Results.BadRequest(new { message = invoiceScopeMessage ?? "Selected company is outside your access scope." });
        }

        db.PurchaseInvoices.Add(invoice);
        db.PurchaseInvoiceItems.AddRange(invoiceItems);

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
        var storeGroupId = sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
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
        var voucherNumber = await CreateVendorPaymentVoucherNumberAsync(invoice.CompanyId, db, cancellationToken);
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

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
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
            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == invoice.CompanyId &&
                stock.StoreGroupId == storeGroupId &&
                stock.StoreId == storeId &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode,
                cancellationToken);

            if (stock is not null)
            {
                stock.PurchaseQty = Math.Max(stock.PurchaseQty - item.BilledQuantity, 0);
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

        await accounting.PostPurchaseInvoiceCancellationAsync(invoice, vendor, storeGroupId, storeId, originalPaidAmount, originalPaymentMode, bankAccountId, cancellationToken);

        invoice.InvoiceStatus = InvoiceStatus.Cancelled;
        invoice.PaymentMode = null;
        invoice.BillAmount = 0;
        invoice.NetAmount = 0;
        invoice.TaxAmount = 0;
        invoice.RoundOff = 0;
        invoice.FrightAmount = 0;

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

        var vendor = await db.Vendors.FirstOrDefaultAsync(
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
            Unit = Unit.Pcs,
            ProductType = ProductType.Apparels,
            ProductCategoryId = categoryId,
            ProductSubCategoryId = subCategoryId,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId
        };

        db.Products.Add(product);
        return product;
    }

    private static async Task<string> CreatePurchaseInvoiceNumberAsync(Guid companyId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var count = await db.PurchaseInvoices.CountAsync(
            item => item.CompanyId == companyId && item.OnDate >= today && item.OnDate < tomorrow,
            cancellationToken);

        return $"P-{today:yyyyMMdd}-{count + 1:0000}";
    }

    private static async Task<string> CreateInwardNumberAsync(Guid companyId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var count = await db.PurchaseInvoices.CountAsync(
            item => item.CompanyId == companyId && item.InwardDate >= today && item.InwardDate < tomorrow,
            cancellationToken);

        return $"INW-{today:yyyyMMdd}-{count + 1:0000}";
    }

    private static async Task<Dictionary<Guid, decimal>> GetPaidAmountLookupAsync(Guid[] invoiceIds, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (invoiceIds.Length == 0)
        {
            return new Dictionary<Guid, decimal>();
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
                line.Debit
            })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(row => row.InvoiceId).ToDictionary(group => group.Key, group => group.Sum(row => row.Debit));
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


    private static async Task<string> CreateVendorPaymentVoucherNumberAsync(Guid companyId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var count = await db.Vouchers.CountAsync(
            item => item.CompanyId == companyId && item.OnDate >= today && item.OnDate < tomorrow && item.VoucherNumber.StartsWith("PV-"),
            cancellationToken);

        return $"PV-{today:yyyyMMdd}-{count + 1:0000}";
    }

    private static string NormalizePdfFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "a5" or "a5-one" => "a5",
        "thermal-2" or "2-inch" or "thermal2" => "thermal-2",
        "thermal-3" or "3-inch" or "thermal3" => "thermal-3",
        _ => "a4"
    };
}

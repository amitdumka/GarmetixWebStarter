using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Gstin;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
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

        group.MapPost("/inward", CreateInwardAsync);

        return group;
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
                return Results.BadRequest(new { message = "Run quick setup before purchasing new products." });
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

        return Results.Created($"/api/purchase-invoices/{invoice.Id}", new PurchaseInwardResponse(
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

        var categoryId = await db.ProductCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var subCategoryId = await db.ProductSubCategories
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

}

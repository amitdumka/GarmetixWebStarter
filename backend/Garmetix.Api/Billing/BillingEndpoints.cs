using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Gstin;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Garmetix.Api.Billing;

public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/billing")
            .WithTags("Billing")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapPost("/sales", CreateSaleAsync);
        group.MapGet("/sales/recent", GetRecentSalesAsync);
        group.MapGet("/sales/{id:guid}/receipt", GetReceiptAsync);
        group.MapGet("/sales/{id:guid}/pdf", DownloadInvoicePdfAsync);
        group.MapPost("/sales/{id:guid}/returns", CreateSalesReturnAsync);
        group.MapPost("/sales/{id:guid}/exchange", CreateSalesExchangeAsync);
        group.MapPost("/sales/{id:guid}/cancel", CancelSaleAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IReadOnlyList<RecentInvoiceDto>> GetRecentSalesAsync(HttpContext context, GarmetixDbContext db, int take = 25, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        return await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .OrderByDescending(invoice => invoice.OnDate)
            .ThenByDescending(invoice => invoice.CreatedAt)
            .Take(take)
            .Select(invoice => new RecentInvoiceDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                invoice.CustomerName ?? "Walk-in Customer",
                invoice.CustomerMobileNumber,
                invoice.BillAmount,
                invoice.PaidAmount,
                invoice.BillAmount - invoice.PaidAmount,
                invoice.InvoiceStatus.ToString(),
                invoice.PaymentMode.HasValue ? invoice.PaymentMode.Value.ToString() : string.Empty))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var receipt = await LoadReceiptAsync(id, context, db, cancellationToken);
        return receipt is null ? Results.NotFound() : Results.Ok(receipt);
    }

    private static async Task<IResult> DownloadInvoicePdfAsync(
        Guid id,
        string? format,
        string? copy,
        bool? reprint,
        bool? signatures,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invoice.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invoice.StoreId, cancellationToken);

        var items = await db.InvoiceItems
            .AsNoTracking()
            .Include(item => item.Product)
            .Where(item => item.InvoiceId == id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ReceiptItemDto(
                item.Id,
                item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxPercentage,
                item.TaxAmount,
                item.Amount))
            .ToListAsync(cancellationToken);

        var payments = await db.InvoicePayments
            .AsNoTracking()
            .Where(item => item.InvoiceId == id)
            .OrderBy(item => item.OnDate)
            .Select(item => new ReceiptPaymentDto(
                item.OnDate,
                item.Amount,
                item.PaymentMode.ToString(),
                item.ReferenceNumber))
            .ToListAsync(cancellationToken);

        var model = new InvoicePdfModel(
            company?.Name ?? "Garmetix",
            FormatAddress(company?.Address, company?.City, company?.State, company?.ZipCode),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            store?.Name ?? "Store",
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoice.InvoiceStatus.ToString(),
            invoice.CustomerName ?? "Walk-in Customer",
            invoice.CustomerMobileNumber,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            items,
            payments);

        var pdf = InvoicePdfDocument.Build(
            model,
            format ?? "a4",
            copy ?? "customer",
            reprint == true,
            signatures != false);
        var safeNumber = Regex.Replace(invoice.InvoiceNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "invoice")}-{NormalizePdfFormat(format)}.pdf");
    }

    private static async Task<ReceiptDto?> LoadReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var companyName = await db.Companies
            .AsNoTracking()
            .Where(item => item.Id == invoice.CompanyId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Garmetix";

        var storeName = await db.Stores
            .AsNoTracking()
            .Where(item => item.Id == invoice.StoreId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Store";

        var items = await db.InvoiceItems
            .AsNoTracking()
            .Include(item => item.Product)
            .Where(item => item.InvoiceId == id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ReceiptItemDto(
                item.Id,
                item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxPercentage,
                item.TaxAmount,
                item.Amount))
            .ToListAsync(cancellationToken);

        var payments = await db.InvoicePayments
            .AsNoTracking()
            .Where(item => item.InvoiceId == id)
            .OrderBy(item => item.OnDate)
            .Select(item => new ReceiptPaymentDto(
                item.OnDate,
                item.Amount,
                item.PaymentMode.ToString(),
                item.ReferenceNumber))
            .ToListAsync(cancellationToken);

        return new ReceiptDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            companyName,
            storeName,
            invoice.CustomerName ?? "Walk-in Customer",
            invoice.CustomerMobileNumber,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            items,
            payments);
    }

    private static string FormatAddress(params string?[] parts)
    {
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
    }

    private static string NormalizePdfFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "a5" or "a5-one" => "a5",
        "thermal-2" or "2-inch" or "thermal2" => "thermal-2",
        "thermal-3" or "3-inch" or "thermal3" => "thermal-3",
        _ => "a4"
    };

    private static async Task<IResult> CreateSaleAsync(
        PosSaleRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        GstinLookupService gstinLookup,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one item is required." });
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return Results.BadRequest(new { message = "Item quantity must be greater than zero." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected billing store is outside your access scope." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var customerValidation = !string.IsNullOrWhiteSpace(request.CustomerGstin)
            ? await gstinLookup.ValidatePartyAsync("Customer", request.CustomerGstin, request.CustomerName, null, cancellationToken)
            : null;
        var customer = await GetOrCreateCustomerAsync(request, db, gstinLookup, customerValidation, cancellationToken);
        var invoiceNumber = await CreateInvoiceNumberAsync(request.StoreId, db, cancellationToken);
        var invoiceId = Guid.NewGuid();

        var invoiceItems = new List<InvoiceItem>();
        decimal grossMrp = 0;
        decimal itemDiscount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal totalQuantity = 0;

        foreach (var requestItem in request.Items)
        {
            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context)
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item =>
                    item.ProductId == requestItem.ProductId &&
                    item.Barcode == requestItem.Barcode &&
                    item.StoreId == request.StoreId,
                    cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock not found for barcode {requestItem.Barcode}." });
            }

            if (stock.CurrentStock < requestItem.Quantity)
            {
                return Results.BadRequest(new { message = $"Insufficient stock for {requestItem.Barcode}. Available: {stock.CurrentStock}." });
            }

            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var taxable = Math.Round((lineMrp - lineDiscount) / (1 + (stock.TaxRate / 100)), 2);
            var tax = Math.Round(taxable * (stock.TaxRate / 100), 2);
            var lineAmount = taxable + tax;

            invoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoiceId,
                ProductId = requestItem.ProductId,
                Barcode = requestItem.Barcode,
                MRP = requestItem.Mrp,
                DiscountAmount = requestItem.DiscountAmount,
                BasePrice = taxable,
                TaxPercentage = stock.TaxRate,
                TaxAmount = tax,
                Amount = lineAmount,
                TaxType = stock.TaxType,
                TaxId = stock.TaxId,
                BilledQuantity = requestItem.Quantity,
                CompanyId = request.CompanyId
            });

            stock.SoldQty += requestItem.Quantity;
            stock.SoldValue += lineAmount;

            grossMrp += lineMrp;
            itemDiscount += lineDiscount;
            taxableAmount += taxable;
            taxAmount += tax;
            totalQuantity += requestItem.Quantity;
        }

        var totalDiscount = itemDiscount + request.BillDiscountAmount;
        var billAmount = Math.Round(grossMrp - totalDiscount, 0);
        var paidAmount = Math.Min(request.PaidAmount, billAmount);

        var invoice = new Invoice
        {
            Id = invoiceId,
            InvoiceNumber = invoiceNumber,
            OnDate = DateTime.Now,
            InvoiceType = InvoiceType.Regular,
            InvoiceStatus = paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = totalDiscount,
            TaxAmount = taxAmount,
            NetAmount = taxableAmount,
            RoundOff = billAmount - (taxableAmount + taxAmount),
            BillAmount = billAmount,
            Quantity = totalQuantity,
            ItemCount = invoiceItems.Count,
            PaymentMode = request.PaymentMode,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerMobileNumber = customer.MobileNumber,
            CustomerGSTIN = customer.GSTIN,
            CreditSale = paidAmount < billAmount,
            PaidAmount = paidAmount,
            BillDiscountAmount = request.BillDiscountAmount,
            StoreId = request.StoreId,
            CompanyId = request.CompanyId
        };

        db.SalesInvoices.Add(invoice);
        db.InvoiceItems.AddRange(invoiceItems);

        if (paidAmount > 0)
        {
            db.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                OnDate = DateTime.Now,
                Amount = paidAmount,
                PaymentMode = request.PaymentMode,
                StoreId = request.StoreId,
                CompanyId = request.CompanyId
            });
        }

        customer.BillCount += 1;
        customer.Amount += billAmount;
        await accounting.PostSalesInvoiceAsync(invoice, customer, request.StoreGroupId, request.BankAccountId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/sales-invoices/{invoice.Id}", new PosSaleResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.BalanceAmount,
            invoice.ItemCount,
            invoice.Quantity,
            customerValidation?.Alerts ?? Array.Empty<string>()));
    }

    private static async Task<IResult> CancelSaleAsync(
        Guid id,
        CancelInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound();
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Invoice is already cancelled." });
        }

        var originalPaidAmount = invoice.PaidAmount;
        var originalPaymentMode = invoice.PaymentMode;
        var originalBankAccountId = await db.BankTransactions
            .Where(item => item.CompanyId == invoice.CompanyId && item.Reference == $"SI-{invoice.InvoiceNumber}")
            .Select(item => (Guid?)item.BankAccountId)
            .FirstOrDefaultAsync(cancellationToken);
        var storeGroupId = await db.Stores
            .Where(item => item.Id == invoice.StoreId)
            .Select(item => item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);

        var items = await db.InvoiceItems
            .Where(item => item.InvoiceId == id)
            .ToListAsync(cancellationToken);

        decimal reversedQuantity = 0;
        decimal reversedAmount = 0;

        foreach (var item in items)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(stockItem =>
                stockItem.ProductId == item.ProductId &&
                stockItem.Barcode == item.Barcode &&
                stockItem.StoreId == invoice.StoreId,
                cancellationToken);

            if (stock is null)
            {
                continue;
            }

            stock.SoldQty = Math.Max(0, stock.SoldQty - item.BilledQuantity);
            stock.SoldValue = Math.Max(0, stock.SoldValue - item.Amount);
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

        await accounting.PostSalesInvoiceCancellationAsync(invoice, customer, storeGroupId, originalPaidAmount, originalPaymentMode, originalBankAccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new CancelInvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceStatus.ToString(),
            reversedQuantity,
            reversedAmount));
    }

    private static async Task<IResult> CreateSalesReturnAsync(
        Guid id,
        SalesReturnRequest request,
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
        var result = await CreateReturnCoreAsync(
            id,
            request.Items,
            request.RefundAmount,
            request.RefundPaymentMode,
            request.BankAccountId,
            request.Reason,
            context,
            db,
            accounting,
            cancellationToken);

        if (!result.Success)
        {
            return result.ErrorStatus == StatusCodes.Status404NotFound
                ? Results.NotFound()
                : Results.BadRequest(new { message = result.ErrorMessage });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/billing/sales/{result.ReturnInvoice!.Id}/receipt", new SalesReturnResponse(
            result.ReturnInvoice.Id,
            result.ReturnInvoice.InvoiceNumber,
            result.OriginalInvoice!.Id,
            result.OriginalInvoice.InvoiceNumber,
            result.CreditAmount,
            result.RefundedAmount,
            result.StoreCreditAmount,
            result.ReversedQuantity,
            result.OriginalInvoice.InvoiceStatus.ToString()));
    }

    private static async Task<IResult> CreateSalesExchangeAsync(
        Guid id,
        SalesExchangeRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.ReturnItems.Count == 0)
        {
            return Results.BadRequest(new { message = "Select returned item quantity before creating exchange." });
        }

        if (request.NewItems.Count == 0)
        {
            return Results.BadRequest(new { message = "Select replacement item before creating exchange." });
        }

        if (request.ReturnItems.Any(item => item.Quantity <= 0) || request.NewItems.Any(item => item.Quantity <= 0))
        {
            return Results.BadRequest(new { message = "Exchange quantities must be greater than zero." });
        }

        if (request.AdditionalPaidAmount > 0 && !request.AdditionalPaymentMode.HasValue)
        {
            return Results.BadRequest(new { message = "Select payment mode for additional exchange amount." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var returnResult = await CreateReturnCoreAsync(
            id,
            request.ReturnItems,
            0,
            null,
            null,
            string.IsNullOrWhiteSpace(request.Reason) ? "Exchange return" : request.Reason,
            context,
            db,
            accounting,
            cancellationToken);

        if (!returnResult.Success)
        {
            return returnResult.ErrorStatus == StatusCodes.Status404NotFound
                ? Results.NotFound()
                : Results.BadRequest(new { message = returnResult.ErrorMessage });
        }

        var original = returnResult.OriginalInvoice!;
        var customer = returnResult.Customer!;
        var storeGroupId = returnResult.StoreGroupId;
        var exchangeInvoiceId = Guid.NewGuid();
        var invoiceNumber = await CreatePrefixedInvoiceNumberAsync(original.StoreId, "EX", db, cancellationToken);
        var exchangeItems = new List<InvoiceItem>();
        decimal grossMrp = 0;
        decimal itemDiscount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal totalQuantity = 0;

        foreach (var requestItem in request.NewItems)
        {
            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context)
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item =>
                    item.ProductId == requestItem.ProductId &&
                    item.Barcode == requestItem.Barcode &&
                    item.StoreId == original.StoreId,
                    cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock not found for replacement barcode {requestItem.Barcode}." });
            }

            if (stock.CurrentStock < requestItem.Quantity)
            {
                return Results.BadRequest(new { message = $"Insufficient replacement stock for {requestItem.Barcode}. Available: {stock.CurrentStock}." });
            }

            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var taxable = Math.Round((lineMrp - lineDiscount) / (1 + (stock.TaxRate / 100)), 2);
            var tax = Math.Round(taxable * (stock.TaxRate / 100), 2);
            var lineAmount = taxable + tax;

            exchangeItems.Add(new InvoiceItem
            {
                InvoiceId = exchangeInvoiceId,
                ProductId = requestItem.ProductId,
                Barcode = requestItem.Barcode,
                MRP = requestItem.Mrp,
                DiscountAmount = requestItem.DiscountAmount,
                BasePrice = taxable,
                TaxPercentage = stock.TaxRate,
                TaxAmount = tax,
                Amount = lineAmount,
                TaxType = stock.TaxType,
                TaxId = stock.TaxId,
                BilledQuantity = requestItem.Quantity,
                CompanyId = original.CompanyId
            });

            stock.SoldQty += requestItem.Quantity;
            stock.SoldValue += lineAmount;
            grossMrp += lineMrp;
            itemDiscount += lineDiscount;
            taxableAmount += taxable;
            taxAmount += tax;
            totalQuantity += requestItem.Quantity;
        }

        var billAmount = Math.Round(grossMrp - itemDiscount, 0);
        var creditApplied = Math.Min(customer.CreditBalance, billAmount);
        var additionalDue = Math.Max(billAmount - creditApplied, 0);
        var additionalPaid = Math.Min(Math.Max(request.AdditionalPaidAmount, 0), additionalDue);
        customer.CreditBalance = Math.Max(0, customer.CreditBalance - creditApplied);
        customer.Amount += Math.Max(billAmount - creditApplied, 0);
        customer.BillCount += 1;

        var exchangeInvoice = new Invoice
        {
            Id = exchangeInvoiceId,
            InvoiceNumber = invoiceNumber,
            OnDate = DateTime.Now,
            ReturnInvoice = false,
            OriginalInvoiceId = original.Id,
            InvoiceType = InvoiceType.Regular,
            InvoiceStatus = additionalPaid >= additionalDue ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = itemDiscount,
            TaxAmount = taxAmount,
            NetAmount = taxableAmount,
            RoundOff = billAmount - (taxableAmount + taxAmount),
            BillAmount = billAmount,
            Quantity = totalQuantity,
            ItemCount = exchangeItems.Count,
            PaymentMode = additionalPaid > 0 ? request.AdditionalPaymentMode : null,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerMobileNumber = customer.MobileNumber,
            CustomerGSTIN = customer.GSTIN,
            CreditSale = additionalPaid < additionalDue,
            PaidAmount = additionalPaid,
            BillDiscountAmount = 0,
            StoreId = original.StoreId,
            CompanyId = original.CompanyId
        };

        db.SalesInvoices.Add(exchangeInvoice);
        db.InvoiceItems.AddRange(exchangeItems);
        if (creditApplied > 0)
        {
            db.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = exchangeInvoice.Id,
                OnDate = DateTime.Now,
                Amount = creditApplied,
                PaymentMode = PaymentMode.CreditBalance,
                ReferenceNumber = $"Exchange credit from {returnResult.ReturnInvoice!.InvoiceNumber}",
                StoreId = original.StoreId,
                CompanyId = original.CompanyId
            });
        }

        if (additionalPaid > 0 && request.AdditionalPaymentMode.HasValue)
        {
            db.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = exchangeInvoice.Id,
                OnDate = DateTime.Now,
                Amount = additionalPaid,
                PaymentMode = request.AdditionalPaymentMode.Value,
                ReferenceNumber = $"Exchange additional payment for {invoiceNumber}",
                StoreId = original.StoreId,
                CompanyId = original.CompanyId
            });
        }

        await accounting.PostSalesInvoiceAsync(exchangeInvoice, customer, storeGroupId, request.BankAccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/billing/sales/{exchangeInvoice.Id}/receipt", new SalesExchangeResponse(
            returnResult.ReturnInvoice!.Id,
            returnResult.ReturnInvoice.InvoiceNumber,
            exchangeInvoice.Id,
            exchangeInvoice.InvoiceNumber,
            returnResult.CreditAmount,
            creditApplied,
            additionalPaid,
            exchangeInvoice.BillAmount,
            customer.CreditBalance));
    }

    private static async Task<SalesReturnCoreResult> CreateReturnCoreAsync(
        Guid originalInvoiceId,
        IReadOnlyList<SalesReturnItemRequest> requestItems,
        decimal refundAmount,
        PaymentMode? refundPaymentMode,
        Guid? bankAccountId,
        string? reason,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var original = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == originalInvoiceId, cancellationToken);
        if (original is null)
        {
            return SalesReturnCoreResult.NotFound();
        }

        if (original.InvoiceStatus == InvoiceStatus.Cancelled || original.ReturnInvoice)
        {
            return SalesReturnCoreResult.BadRequest("Cannot return or exchange this invoice.");
        }

        if (refundAmount > 0 && !refundPaymentMode.HasValue)
        {
            return SalesReturnCoreResult.BadRequest("Select refund payment mode.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Id == original.CustomerId, cancellationToken);
        if (customer is null)
        {
            return SalesReturnCoreResult.BadRequest("Original invoice customer was not found.");
        }

        var storeGroupId = await db.Stores
            .Where(item => item.Id == original.StoreId)
            .Select(item => item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);

        var originalItems = await db.InvoiceItems
            .Where(item => item.InvoiceId == original.Id)
            .ToListAsync(cancellationToken);

        var returnInvoices = await db.SalesInvoices
            .AsNoTracking()
            .Where(item => item.OriginalInvoiceId == original.Id && item.ReturnInvoice && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var alreadyReturned = returnInvoices.Count == 0
            ? new Dictionary<string, decimal>()
            : await db.InvoiceItems
                .Where(item => returnInvoices.Contains(item.InvoiceId))
                .GroupBy(item => item.ProductId.ToString() + "|" + item.Barcode)
                .Select(group => new { Key = group.Key, Quantity = group.Sum(item => item.BilledQuantity) })
                .ToDictionaryAsync(item => item.Key, item => item.Quantity, cancellationToken);

        var returnInvoiceId = Guid.NewGuid();
        var returnItems = new List<InvoiceItem>();
        decimal grossMrp = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal discountAmount = 0;
        decimal creditAmount = 0;
        decimal reversedQuantity = 0;

        foreach (var requestItem in requestItems)
        {
            var originalItem = originalItems.FirstOrDefault(item => item.Id == requestItem.InvoiceItemId);
            if (originalItem is null)
            {
                return SalesReturnCoreResult.BadRequest("One or more return items are not part of the selected invoice.");
            }

            var key = originalItem.ProductId.ToString() + "|" + originalItem.Barcode;
            alreadyReturned.TryGetValue(key, out var previousReturnedQuantity);
            var remainingQuantity = originalItem.BilledQuantity - previousReturnedQuantity;
            if (requestItem.Quantity > remainingQuantity)
            {
                return SalesReturnCoreResult.BadRequest($"Return quantity for {originalItem.Barcode} exceeds remaining sold quantity {remainingQuantity}.");
            }

            var ratio = originalItem.BilledQuantity == 0 ? 0 : requestItem.Quantity / originalItem.BilledQuantity;
            var lineMrp = originalItem.MRP * requestItem.Quantity;
            var lineDiscount = originalItem.DiscountAmount * requestItem.Quantity;
            var lineBase = Math.Round(originalItem.BasePrice * ratio, 2);
            var lineTax = Math.Round(originalItem.TaxAmount * ratio, 2);
            var lineAmount = Math.Round(originalItem.Amount * ratio, 2);

            returnItems.Add(new InvoiceItem
            {
                InvoiceId = returnInvoiceId,
                ProductId = originalItem.ProductId,
                Barcode = originalItem.Barcode,
                MRP = originalItem.MRP,
                DiscountAmount = originalItem.DiscountAmount,
                BasePrice = lineBase,
                TaxPercentage = originalItem.TaxPercentage,
                TaxAmount = lineTax,
                Amount = lineAmount,
                TaxType = originalItem.TaxType,
                TaxId = originalItem.TaxId,
                BilledQuantity = requestItem.Quantity,
                CompanyId = original.CompanyId
            });

            var stock = await db.Stocks.FirstOrDefaultAsync(stockItem =>
                stockItem.ProductId == originalItem.ProductId &&
                stockItem.Barcode == originalItem.Barcode &&
                stockItem.StoreId == original.StoreId,
                cancellationToken);
            if (stock is not null)
            {
                stock.SoldQty = Math.Max(0, stock.SoldQty - requestItem.Quantity);
                stock.SoldValue = Math.Max(0, stock.SoldValue - lineAmount);
            }

            grossMrp += lineMrp;
            discountAmount += lineDiscount;
            taxableAmount += lineBase;
            taxAmount += lineTax;
            creditAmount += lineAmount;
            reversedQuantity += requestItem.Quantity;
        }

        var billAmount = Math.Round(creditAmount, 0);
        var refund = Math.Min(Math.Max(refundAmount, 0), billAmount);
        var storeCredit = Math.Max(billAmount - refund, 0);
        var creditNoteNumber = await CreatePrefixedInvoiceNumberAsync(original.StoreId, "SR", db, cancellationToken);
        var returnInvoice = new Invoice
        {
            Id = returnInvoiceId,
            InvoiceNumber = creditNoteNumber,
            OnDate = DateTime.Now,
            ReturnInvoice = true,
            OriginalInvoiceId = original.Id,
            InvoiceType = InvoiceType.Return,
            InvoiceStatus = refund >= billAmount ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            NetAmount = taxableAmount,
            RoundOff = billAmount - (taxableAmount + taxAmount),
            BillAmount = billAmount,
            Quantity = reversedQuantity,
            ItemCount = returnItems.Count,
            PaymentMode = refund > 0 ? refundPaymentMode : null,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerMobileNumber = customer.MobileNumber,
            CustomerGSTIN = customer.GSTIN,
            CreditSale = false,
            PaidAmount = refund,
            BillDiscountAmount = 0,
            StoreId = original.StoreId,
            CompanyId = original.CompanyId
        };

        db.SalesInvoices.Add(returnInvoice);
        db.InvoiceItems.AddRange(returnItems);
        if (refund > 0 && refundPaymentMode.HasValue)
        {
            db.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = returnInvoice.Id,
                OnDate = DateTime.Now,
                Amount = refund,
                PaymentMode = refundPaymentMode.Value,
                StoreId = original.StoreId,
                CompanyId = original.CompanyId,
                ReferenceNumber = string.IsNullOrWhiteSpace(reason) ? "Sales return refund" : reason
            });
        }

        customer.Amount = Math.Max(0, customer.Amount - billAmount);
        customer.CreditBalance += storeCredit;
        await UpdateOriginalReturnStatusAsync(original, originalItems, reversedQuantity, db, cancellationToken);
        await accounting.PostSalesReturnAsync(returnInvoice, customer, storeGroupId, refund, refundPaymentMode, bankAccountId, cancellationToken);

        return SalesReturnCoreResult.Ok(original, returnInvoice, customer, storeGroupId, billAmount, refund, storeCredit, reversedQuantity);
    }

    private static async Task UpdateOriginalReturnStatusAsync(
        Invoice original,
        IReadOnlyList<InvoiceItem> originalItems,
        decimal currentReturnQuantity,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var returnInvoiceIds = await db.SalesInvoices
            .AsNoTracking()
            .Where(item => item.OriginalInvoiceId == original.Id && item.ReturnInvoice && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var previousReturnedQty = returnInvoiceIds.Count == 0
            ? 0
            : await db.InvoiceItems
                .Where(item => returnInvoiceIds.Contains(item.InvoiceId))
                .SumAsync(item => item.BilledQuantity, cancellationToken);
        var returnedQty = previousReturnedQty + currentReturnQuantity;
        var originalQty = originalItems.Sum(item => item.BilledQuantity);
        original.InvoiceStatus = returnedQty >= originalQty ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded;
    }

    private static async Task<string> CreatePrefixedInvoiceNumberAsync(Guid storeId, string prefix, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var count = await db.SalesInvoices.CountAsync(
            item => item.StoreId == storeId && item.OnDate >= today && item.OnDate < tomorrow && item.InvoiceNumber.StartsWith(prefix + "-"),
            cancellationToken);

        return $"{prefix}-{today:yyyyMMdd}-{count + 1:0000}";
    }

    private sealed record SalesReturnCoreResult(
        bool Success,
        int ErrorStatus,
        string ErrorMessage,
        Invoice? OriginalInvoice,
        Invoice? ReturnInvoice,
        Customer? Customer,
        Guid StoreGroupId,
        decimal CreditAmount,
        decimal RefundedAmount,
        decimal StoreCreditAmount,
        decimal ReversedQuantity)
    {
        public static SalesReturnCoreResult NotFound() => new(false, StatusCodes.Status404NotFound, "Original invoice was not found.", null, null, null, Guid.Empty, 0, 0, 0, 0);
        public static SalesReturnCoreResult BadRequest(string message) => new(false, StatusCodes.Status400BadRequest, message, null, null, null, Guid.Empty, 0, 0, 0, 0);
        public static SalesReturnCoreResult Ok(Invoice originalInvoice, Invoice returnInvoice, Customer customer, Guid storeGroupId, decimal creditAmount, decimal refundedAmount, decimal storeCreditAmount, decimal reversedQuantity) => new(true, StatusCodes.Status200OK, string.Empty, originalInvoice, returnInvoice, customer, storeGroupId, creditAmount, refundedAmount, storeCreditAmount, reversedQuantity);
    }

    private static async Task<Customer> GetOrCreateCustomerAsync(
        PosSaleRequest request,
        GarmetixDbContext db,
        GstinLookupService gstinLookup,
        PartyGstinValidationResponse? validation,
        CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(request.CustomerMobileNumber) ? "WALKIN" : request.CustomerMobileNumber.Trim();
        var gstin = GstinLookupService.NormalizeGstin(request.CustomerGstin);
        var customer = await db.Customers.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId &&
                ((!string.IsNullOrWhiteSpace(gstin) && item.GSTIN == gstin) || item.MobileNumber == mobile),
            cancellationToken);

        if (customer is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.CustomerName) && customer.Name == "Walk-in Customer")
            {
                customer.Name = request.CustomerName.Trim();
            }

            if (validation is not null)
            {
                gstinLookup.ApplyVerification(customer, validation);
                if (!string.IsNullOrWhiteSpace(validation.Lookup.PrincipalAddress) && (string.IsNullOrWhiteSpace(customer.Address) || customer.Address == "Dumka"))
                {
                    customer.Address = validation.Lookup.PrincipalAddress;
                }
            }

            return customer;
        }

        customer = new Customer
        {
            Name = string.IsNullOrWhiteSpace(request.CustomerName) ? (validation?.Lookup.TradeName ?? validation?.Lookup.LegalName ?? "Walk-in Customer") : request.CustomerName.Trim(),
            Address = validation?.Lookup.PrincipalAddress ?? "Dumka",
            MobileNumber = mobile,
            GSTIN = string.IsNullOrWhiteSpace(gstin) ? null : gstin,
            CompanyId = request.CompanyId
        };

        if (validation is not null)
        {
            gstinLookup.ApplyVerification(customer, validation);
        }

        db.Customers.Add(customer);
        return customer;
    }

    private static async Task<string> CreateInvoiceNumberAsync(Guid storeId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var count = await db.SalesInvoices.CountAsync(
            item => item.StoreId == storeId && item.OnDate >= today && item.OnDate < tomorrow,
            cancellationToken);

        return $"S-{today:yyyyMMdd}-{count + 1:0000}";
    }
}

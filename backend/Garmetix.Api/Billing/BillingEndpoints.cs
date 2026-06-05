using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
        group.MapPost("/sales/{id:guid}/cancel", CancelSaleAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IReadOnlyList<RecentInvoiceDto>> GetRecentSalesAsync(GarmetixDbContext db, int take = 25, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        return await db.SalesInvoices
            .AsNoTracking()
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

    private static async Task<IResult> GetReceiptAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound();
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

        return Results.Ok(new ReceiptDto(
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
            payments));
    }

    private static async Task<IResult> CreateSaleAsync(
        PosSaleRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
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

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var customer = await GetOrCreateCustomerAsync(request, db, cancellationToken);
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
            var stock = await db.Stocks
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
            invoice.Quantity));
    }

    private static async Task<IResult> CancelSaleAsync(
        Guid id,
        CancelInvoiceRequest request,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await db.SalesInvoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
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

    private static async Task<Customer> GetOrCreateCustomerAsync(PosSaleRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(request.CustomerMobileNumber) ? "WALKIN" : request.CustomerMobileNumber.Trim();
        var customer = await db.Customers.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId && item.MobileNumber == mobile,
            cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            Name = string.IsNullOrWhiteSpace(request.CustomerName) ? "Walk-in Customer" : request.CustomerName.Trim(),
            MobileNumber = mobile,
            CompanyId = request.CompanyId
        };

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

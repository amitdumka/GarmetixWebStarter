using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Commercial;
using Garmetix.Api.Inventory;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

// Sale invoice edit: the basic edit (date / number / customer text / salesman / remarks) re-dates everything the sale
// created, and the extended edit rebuilds items and payments. Kept in a partial class so it can reuse the same tax and
// payment helpers the sale screen uses.
public static partial class BillingEndpoints
{
    private static void MapSaleEditEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/sales/{id:guid}", UpdateSaleInvoiceAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/sales/{id:guid}/edit-model", GetSaleEditModelAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/sales/{id:guid}/extended", UpdateSaleInvoiceExtendedAsync).RequireAuthorization(GarmetixPolicies.Edit);
    }

    private static bool IsAdjustmentMode(PaymentMode mode) => mode is PaymentMode.CreditBalance or PaymentMode.CreditNote or PaymentMode.SaleReturn or PaymentMode.Coupons;

    private static async Task<IResult> GetSaleEditModelAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        SalesInvoiceEditService edit,
        CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sales invoice was not found." });
        }

        var items = await db.InvoiceItems.AsNoTracking().Where(item => item.InvoiceId == id).OrderBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var payments = await db.InvoicePayments.AsNoTracking().Where(item => item.InvoiceId == id).OrderBy(item => item.OnDate).ToListAsync(cancellationToken);
        var blockReason = await edit.CheckEditableAsync(invoice, cancellationToken);
        string? extendedBlockReason = blockReason;
        if (extendedBlockReason is null && payments.Any(item => IsAdjustmentMode(item.PaymentMode) || !string.IsNullOrWhiteSpace(item.AdjustmentSourceType)))
        {
            extendedBlockReason = "This bill was paid partly with store credit, a credit note, coupons or loyalty. Items and payments cannot be edited; cancel and re-bill instead. Date and customer details can still be edited.";
        }

        return Results.Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            invoiceStatus = invoice.InvoiceStatus.ToString(),
            invoice.StoreId,
            invoice.CompanyId,
            invoice.CustomerId,
            invoice.CustomerName,
            invoice.CustomerMobileNumber,
            customerGstin = invoice.CustomerGSTIN,
            salesmanId = invoice.SalemanId,
            invoice.Remarks,
            invoice.BillDiscountAmount,
            invoice.BillAmount,
            invoice.PaidAmount,
            editable = blockReason is null,
            blockReason,
            extendedEditable = extendedBlockReason is null,
            extendedBlockReason,
            items = items.Select(item => new
            {
                item.Id,
                item.ProductId,
                item.Barcode,
                productName = item.ProductName,
                quantity = item.BilledQuantity,
                mrp = item.MRP,
                discountAmount = item.DiscountAmount,
                amount = item.Amount
            }),
            payments = payments.Select(item => new
            {
                item.Id,
                paymentMode = (int)item.PaymentMode,
                paymentModeName = item.PaymentMode.ToString(),
                item.Amount,
                item.BankAccountId,
                item.ReferenceNumber,
                item.OnDate,
                isAdjustment = IsAdjustmentMode(item.PaymentMode) || !string.IsNullOrWhiteSpace(item.AdjustmentSourceType)
            })
        });
    }

    private static Task<IResult> UpdateSaleInvoiceAsync(
        Guid id,
        UpdateSaleInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        SalesInvoiceEditService edit,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => UpdateSaleInvoiceCoreAsync(id, request, context, db, edit, accounting, cancellationToken));
    }

    private static async Task<IResult> UpdateSaleInvoiceCoreAsync(
        Guid id,
        UpdateSaleInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        SalesInvoiceEditService edit,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sales invoice was not found." });
        }

        var blocked = await edit.CheckEditableAsync(invoice, cancellationToken);
        if (blocked is not null)
        {
            return Results.Conflict(new { message = blocked });
        }

        var oldNumber = invoice.InvoiceNumber;
        var oldOnDate = invoice.OnDate;
        var newNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? oldNumber : request.InvoiceNumber.Trim();
        if (!string.Equals(newNumber, oldNumber, StringComparison.Ordinal)
            && await db.SalesInvoices.AsNoTracking().AnyAsync(item => item.Id != id && item.CompanyId == invoice.CompanyId && item.InvoiceNumber == newNumber, cancellationToken))
        {
            return Results.Conflict(new { message = $"Sales invoice number {newNumber} already exists." });
        }

        var newOnDate = request.OnDate.HasValue && request.OnDate.Value.Date != oldOnDate.Date
            ? request.OnDate.Value.Date + oldOnDate.TimeOfDay
            : oldOnDate;
        if (newOnDate != oldOnDate)
        {
            if (newOnDate.Date > DateTime.Today)
            {
                return Results.BadRequest(new { message = "Sale date cannot be in the future." });
            }

            var locked = await edit.CheckPeriodOpenAsync(invoice.CompanyId, invoice.StoreId, oldOnDate, cancellationToken)
                ?? await edit.CheckPeriodOpenAsync(invoice.CompanyId, invoice.StoreId, newOnDate, cancellationToken);
            if (locked is not null)
            {
                return Results.Conflict(new { message = locked });
            }
        }

        var before = new { invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerMobileNumber, invoice.CustomerGSTIN };
        invoice.InvoiceNumber = newNumber;
        invoice.OnDate = newOnDate;
        if (!string.IsNullOrWhiteSpace(request.CustomerName)) invoice.CustomerName = request.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(request.CustomerMobileNumber)) invoice.CustomerMobileNumber = request.CustomerMobileNumber.Trim();
        invoice.CustomerGSTIN = string.IsNullOrWhiteSpace(request.CustomerGstin) ? null : request.CustomerGstin.Trim().ToUpperInvariant();
        if (request.SalesmanId.HasValue && request.SalesmanId.Value != Guid.Empty) invoice.SalemanId = request.SalesmanId.Value;
        invoice.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        invoice.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (newOnDate != oldOnDate || newNumber != oldNumber)
        {
            await edit.RedateAsync(invoice, oldOnDate, oldNumber, newOnDate, newNumber, cancellationToken);
            await RepostSaleJournalAsync(invoice, db, accounting, cancellationToken);
        }

        var storeGroupId = await db.Stores.AsNoTracking().Where(item => item.Id == invoice.StoreId).Select(item => item.StoreGroupId).FirstOrDefaultAsync(cancellationToken);
        AddBillingAudit(db, context, "Edited", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, storeGroupId, invoice.StoreId,
            newOnDate != oldOnDate ? $"Sale date changed {oldOnDate:yyyy-MM-dd} to {newOnDate:yyyy-MM-dd}; stock, payments, journal re-dated." : "Sales invoice details edited.",
            before, new { invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerMobileNumber, invoice.CustomerGSTIN });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new { invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerMobileNumber, invoice.CustomerGSTIN, salesmanId = invoice.SalemanId, invoice.Remarks });
    }

    private static async Task RepostSaleJournalAsync(Invoice invoice, GarmetixDbContext db, AccountingPostingService accounting, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Id == invoice.CustomerId, cancellationToken);
        if (customer is null)
        {
            return;
        }

        var storeGroupId = await db.Stores.AsNoTracking().Where(item => item.Id == invoice.StoreId).Select(item => item.StoreGroupId).FirstOrDefaultAsync(cancellationToken);
        var payments = await db.InvoicePayments.AsNoTracking().Where(item => item.InvoiceId == invoice.Id && item.Amount > 0).OrderBy(item => item.OnDate).ToListAsync(cancellationToken);
        await accounting.PostSalesInvoiceAsync(invoice, customer, storeGroupId, ToAccountingPaymentPostings(payments), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Task<IResult> UpdateSaleInvoiceExtendedAsync(
        Guid id,
        UpdateSaleInvoiceExtendedRequest request,
        HttpContext context,
        GarmetixDbContext db,
        SalesInvoiceEditService edit,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => UpdateSaleInvoiceExtendedCoreAsync(id, request, context, db, edit, accounting, stockLedger, cancellationToken));
    }

    private static async Task<IResult> UpdateSaleInvoiceExtendedCoreAsync(
        Guid id,
        UpdateSaleInvoiceExtendedRequest request,
        HttpContext context,
        GarmetixDbContext db,
        SalesInvoiceEditService edit,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one item is required." });
        }

        if (request.Items.Any(item => item.Quantity <= 0 || item.Mrp < 0 || item.DiscountAmount < 0))
        {
            return Results.BadRequest(new { message = "Item quantity must be greater than zero and price / discount cannot be negative." });
        }

        if (request.BillDiscountAmount < 0)
        {
            return Results.BadRequest(new { message = "Bill discount cannot be negative." });
        }

        var requestPayments = (request.Payments ?? []).Where(item => item.Amount > 0).ToList();
        if (requestPayments.Any(item => IsAdjustmentMode(item.PaymentMode)))
        {
            return Results.BadRequest(new { message = "Store credit, credit note and coupon payments cannot be added while editing. Use Cash, Card, UPI or a bank mode." });
        }

        var missingBank = requestPayments.FirstOrDefault(item => RequiresBankAccount(item.PaymentMode) && !item.BankAccountId.HasValue);
        if (missingBank is not null)
        {
            return Results.BadRequest(new { message = $"Select bank account for {missingBank.PaymentMode} payment." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sales invoice was not found." });
        }

        var blocked = await edit.CheckEditableAsync(invoice, cancellationToken);
        if (blocked is not null)
        {
            return Results.Conflict(new { message = blocked });
        }

        var existingPayments = await db.InvoicePayments.AsNoTracking().Where(item => item.InvoiceId == id).ToListAsync(cancellationToken);
        if (existingPayments.Any(item => IsAdjustmentMode(item.PaymentMode) || !string.IsNullOrWhiteSpace(item.AdjustmentSourceType)))
        {
            return Results.Conflict(new { message = "This bill was paid partly with store credit, a credit note, coupons or loyalty. Items and payments cannot be edited; cancel and re-bill instead." });
        }

        var store = await db.Stores.AsNoTracking().Include(item => item.Company).FirstOrDefaultAsync(item => item.Id == invoice.StoreId, cancellationToken);
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Id == invoice.CustomerId, cancellationToken);
        if (store is null || customer is null)
        {
            return Results.Conflict(new { message = "Store or customer of this invoice was not found." });
        }

        var oldNumber = invoice.InvoiceNumber;
        var oldOnDate = invoice.OnDate;
        var newNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? oldNumber : request.InvoiceNumber.Trim();
        if (!string.Equals(newNumber, oldNumber, StringComparison.Ordinal)
            && await db.SalesInvoices.AsNoTracking().AnyAsync(item => item.Id != id && item.CompanyId == invoice.CompanyId && item.InvoiceNumber == newNumber, cancellationToken))
        {
            return Results.Conflict(new { message = $"Sales invoice number {newNumber} already exists." });
        }

        var newOnDate = request.OnDate.HasValue && request.OnDate.Value.Date != oldOnDate.Date
            ? request.OnDate.Value.Date + oldOnDate.TimeOfDay
            : oldOnDate;
        if (newOnDate.Date > DateTime.Today)
        {
            return Results.BadRequest(new { message = "Sale date cannot be in the future." });
        }

        var locked = await edit.CheckPeriodOpenAsync(invoice.CompanyId, invoice.StoreId, oldOnDate, cancellationToken)
            ?? (newOnDate.Date != oldOnDate.Date ? await edit.CheckPeriodOpenAsync(invoice.CompanyId, invoice.StoreId, newOnDate, cancellationToken) : null);
        if (locked is not null)
        {
            return Results.Conflict(new { message = locked });
        }

        var before = new { invoice.InvoiceNumber, invoice.OnDate, invoice.BillAmount, invoice.PaidAmount, invoice.ItemCount, invoice.Quantity };
        var oldBill = invoice.BillAmount;

        // Take back everything the invoice posted; it is rebuilt below from the corrected data.
        var earnedPoints = await edit.ResetPostingsAsync(invoice, cancellationToken);
        customer.Amount = Math.Max(0, customer.Amount - oldBill);
        customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - earnedPoints);

        var gstin = string.IsNullOrWhiteSpace(request.CustomerGstin) ? null : Garmetix.Api.Gstin.GstinLookupService.NormalizeGstin(request.CustomerGstin);
        var interState = IsInterStateSupply(store.Company?.GSTIN, gstin);
        var invoiceItems = new List<InvoiceItem>();
        decimal grossMrp = 0, itemDiscount = 0, taxableAmount = 0, taxAmount = 0, cgstAmount = 0, sgstAmount = 0, igstAmount = 0, totalQuantity = 0;

        foreach (var requestItem in request.Items)
        {
            await DocumentNumberGenerator.LockStockKeyAsync(db, invoice.CompanyId, store.StoreGroupId, invoice.StoreId, requestItem.ProductId, requestItem.Barcode, cancellationToken);
            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context)
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item => item.ProductId == requestItem.ProductId && item.Barcode == requestItem.Barcode && item.StoreId == invoice.StoreId && !item.IsOFB, cancellationToken);
            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock not found for barcode {requestItem.Barcode}." });
            }

            var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            if (snapshot.Quantity < requestItem.Quantity)
            {
                return Results.BadRequest(new { message = $"Insufficient stock for {requestItem.Barcode}. Available: {snapshot.Quantity}." });
            }

            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var taxable = Math.Round((lineMrp - lineDiscount) / (1 + (stock.TaxRate / 100)), 2);
            var tax = Math.Round(taxable * (stock.TaxRate / 100), 2);
            var lineAmount = taxable + tax;
            var split = SplitGst(tax, stock.TaxType, interState);

            invoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ProductId = requestItem.ProductId,
                Barcode = requestItem.Barcode,
                ProductName = stock.Product?.Name,
                HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
                Unit = stock.Unit,
                ProductCategoryId = stock.Product?.ProductCategoryId,
                ProductSubCategoryId = stock.Product?.ProductSubCategoryId,
                MRP = requestItem.Mrp,
                DiscountAmount = requestItem.DiscountAmount,
                BasePrice = taxable,
                TaxPercentage = stock.TaxRate,
                TaxAmount = tax,
                CGSTAmount = split.Cgst,
                SGSTAmount = split.Sgst,
                IGSTAmount = split.Igst,
                Amount = lineAmount,
                TaxType = interState ? TaxType.IGST : stock.TaxType,
                TaxId = stock.TaxId,
                BilledQuantity = requestItem.Quantity,
                CompanyId = invoice.CompanyId
            });

            stock.SoldValue += lineAmount;
            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "SaleOut",
                QuantityOut = requestItem.Quantity,
                CostPrice = snapshot.AverageCost,
                MRP = requestItem.Mrp,
                TaxRate = stock.TaxRate,
                HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
                SourceType = "SalesInvoice",
                SourceId = invoice.Id,
                SourceNumber = newNumber,
                Remarks = "POS sale (edited)",
                OnDate = newOnDate,
                CompanyId = invoice.CompanyId,
                StoreGroupId = store.StoreGroupId,
                StoreId = invoice.StoreId
            }, cancellationToken);

            grossMrp += lineMrp;
            itemDiscount += lineDiscount;
            taxableAmount += taxable;
            taxAmount += tax;
            cgstAmount += split.Cgst;
            sgstAmount += split.Sgst;
            igstAmount += split.Igst;
            totalQuantity += requestItem.Quantity;
        }

        var totalDiscount = itemDiscount + request.BillDiscountAmount;
        if (totalDiscount > grossMrp)
        {
            return Results.BadRequest(new { message = "Total discount cannot be greater than gross MRP." });
        }

        if (request.BillDiscountAmount > 0)
        {
            var allocated = ApplyBillDiscountToInvoiceItems(invoiceItems, request.BillDiscountAmount, interState);
            taxableAmount = allocated.TaxableAmount;
            taxAmount = allocated.TaxAmount;
            cgstAmount = allocated.CgstAmount;
            sgstAmount = allocated.SgstAmount;
            igstAmount = allocated.IgstAmount;
        }

        var billAmount = Math.Round(grossMrp - totalDiscount, 0);
        var paidAmount = requestPayments.Sum(item => Math.Round(item.Amount, 2));
        if (paidAmount > billAmount)
        {
            return Results.BadRequest(new { message = $"Payments ({paidAmount:0.00}) cannot be more than the bill amount ({billAmount:0.00})." });
        }

        var salesmanId = request.SalesmanId.HasValue && request.SalesmanId.Value != Guid.Empty ? request.SalesmanId.Value : invoice.SalemanId;
        invoice.InvoiceNumber = newNumber;
        invoice.OnDate = newOnDate;
        invoice.MRP = grossMrp;
        invoice.BasePrice = taxableAmount;
        invoice.DiscountAmount = totalDiscount;
        invoice.TaxAmount = taxAmount;
        invoice.CGSTAmount = cgstAmount;
        invoice.SGSTAmount = sgstAmount;
        invoice.IGSTAmount = igstAmount;
        invoice.InterState = interState;
        invoice.NetAmount = taxableAmount;
        invoice.RoundOff = billAmount - (taxableAmount + taxAmount);
        invoice.BillAmount = billAmount;
        invoice.Quantity = totalQuantity;
        invoice.ItemCount = invoiceItems.Count;
        invoice.PaymentMode = requestPayments.Count > 1 ? PaymentMode.MixPayments : requestPayments.FirstOrDefault()?.PaymentMode;
        invoice.PaidAmount = paidAmount;
        invoice.CreditSale = paidAmount < billAmount;
        invoice.InvoiceStatus = paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        invoice.BillDiscountAmount = request.BillDiscountAmount;
        invoice.SalemanId = salesmanId;
        if (!string.IsNullOrWhiteSpace(request.CustomerName)) invoice.CustomerName = request.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(request.CustomerMobileNumber)) invoice.CustomerMobileNumber = request.CustomerMobileNumber.Trim();
        invoice.CustomerGSTIN = gstin;
        invoice.B2BSale = !string.IsNullOrWhiteSpace(gstin);
        invoice.SaleInvoiceType = !string.IsNullOrWhiteSpace(gstin) ? SaleInvoiceType.B2B : SaleInvoiceType.B2C;
        invoice.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        invoice.UpdatedAt = DateTime.UtcNow;
        if (invoice.BookDateUpdatedAt is null)
        {
            invoice.BookDate = newOnDate.Date;
        }

        db.InvoiceItems.AddRange(invoiceItems);
        foreach (var payment in requestPayments)
        {
            db.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                OnDate = payment.OnDate.HasValue ? payment.OnDate.Value.Date + newOnDate.TimeOfDay : newOnDate,
                Amount = Math.Round(payment.Amount, 2),
                PaymentMode = payment.PaymentMode,
                ReferenceNumber = Clean(payment.ReferenceNumber),
                BankAccountId = payment.BankAccountId,
                StoreId = invoice.StoreId,
                CompanyId = invoice.CompanyId
            });
        }

        customer.Amount += billAmount;
        await LoyaltyService.AwardSalePointsAsync(invoice, customer, db, cancellationToken);
        foreach (var entry in db.ChangeTracker.Entries<LoyaltyPointLedger>().Where(item => item.State == EntityState.Added && item.Entity.SourceId == invoice.Id))
        {
            entry.Entity.OnDate = newOnDate;
        }

        var postings = requestPayments.Select(item => new SalesInvoicePaymentPosting(item.PaymentMode, Math.Round(item.Amount, 2), item.BankAccountId, Clean(item.ReferenceNumber), null, null, null, null, null)).ToList();
        await accounting.PostSalesInvoiceAsync(invoice, customer, store.StoreGroupId, postings, cancellationToken);

        AddBillingAudit(db, context, "ExtendedEdit", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, store.StoreGroupId, invoice.StoreId,
            string.IsNullOrWhiteSpace(request.Reason) ? "Sales invoice items / payments edited." : request.Reason.Trim(),
            before, new { invoice.InvoiceNumber, invoice.OnDate, invoice.BillAmount, invoice.PaidAmount, invoice.ItemCount, invoice.Quantity });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new
        {
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            previousBillAmount = before.BillAmount,
            invoice.BillAmount,
            invoice.PaidAmount,
            invoice.ItemCount,
            invoice.Quantity
        });
    }
}

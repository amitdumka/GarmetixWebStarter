using Garmetix.Api.Auth;
using Garmetix.Api.Accounting;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

public static class SaleReviewEndpoints
{
    private const decimal ApparelGstThreshold = 2499m;
    private const decimal LowerApparelGstRate = 5m;
    private const decimal HigherApparelGstRate = 18m;

    public static RouteGroupBuilder MapSaleReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sale-review")
            .WithTags("Sale Review")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("", GetSaleReviewAsync);
        group.MapPost("/adjustments/apply", ApplyAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        return group;
    }

    private static async Task<SaleReviewResponseDto> GetSaleReviewAsync(
        HttpContext context,
        GarmetixDbContext db,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        string? search = null,
        bool onlyIssues = false,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var from = (fromDate ?? today.AddDays(-30)).Date;
        var to = (toDate ?? today).Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);
        var inclusiveTo = to.AddDays(1);
        var term = search?.Trim();

        var invoiceQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.OnDate >= from && item.OnDate < inclusiveTo && item.InvoiceStatus != InvoiceStatus.Cancelled);

        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            invoiceQuery = invoiceQuery.Where(item => item.StoreId == storeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            invoiceQuery = invoiceQuery.Where(item =>
                item.InvoiceNumber.Contains(term) ||
                (item.CustomerName != null && item.CustomerName.Contains(term)) ||
                item.CustomerMobileNumber.Contains(term));
        }

        var invoices = await invoiceQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.InvoiceNumber)
            .Select(item => new SaleInvoiceSeed(
                item.Id,
                item.InvoiceNumber,
                item.OnDate,
                item.CustomerName ?? "Walk-in Customer",
                item.CustomerMobileNumber,
                item.BillAmount,
                item.StoreId,
                item.Store != null ? item.Store.Name : string.Empty))
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return new SaleReviewResponseDto(from, to, storeId, term, page, pageSize, 0, EmptySummary(), Array.Empty<SaleReviewInvoiceDto>(), Array.Empty<SaleReviewItemDto>());
        }

        var invoiceIds = invoices.Select(item => item.Id).ToHashSet();
        var invoiceMap = invoices.ToDictionary(item => item.Id);

        var itemRows = await db.InvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .Select(item => new SaleItemSeed(
                item.Id,
                item.InvoiceId,
                item.ProductId,
                item.ProductName ?? "Item",
                item.Barcode,
                item.HSNCode,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.BasePrice,
                item.TaxPercentage,
                item.TaxAmount,
                item.Amount))
            .ToListAsync(cancellationToken);

        var movementRows = await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(item => item.SourceId.HasValue && invoiceIds.Contains(item.SourceId.Value) &&
                item.SourceType == "SalesInvoice" && item.QuantityOut > 0)
            .Select(item => new
            {
                InvoiceId = item.SourceId!.Value,
                item.ProductId,
                item.Barcode,
                item.QuantityOut,
                item.CostPrice
            })
            .ToListAsync(cancellationToken);

        var costMap = movementRows
            .GroupBy(item => (item.InvoiceId, item.ProductId, Barcode: NormalizeBarcode(item.Barcode)))
            .Select(group => new SaleCostSeed(
                group.Key.InvoiceId,
                group.Key.ProductId,
                group.Key.Barcode,
                group.Sum(item => item.QuantityOut),
                group.Sum(item => Math.Round(item.QuantityOut * item.CostPrice, 2))))
            .ToDictionary(item => (item.InvoiceId, item.ProductId, NormalizeBarcode(item.Barcode)));
        var allRows = new List<SaleReviewItemDto>(itemRows.Count);

        foreach (var item in itemRows)
        {
            if (!invoiceMap.TryGetValue(item.InvoiceId, out var invoice))
            {
                continue;
            }

            var quantity = item.Quantity <= 0 ? 1m : item.Quantity;
            var unitBasic = Math.Round(item.TaxableAmount / quantity, 2);
            var expectedRate = unitBasic <= ApparelGstThreshold ? LowerApparelGstRate : HigherApparelGstRate;
            var expectedTax = Math.Round(item.TaxableAmount * (expectedRate / 100m), 2);
            var expectedAmount = item.TaxableAmount + expectedTax;
            var taxDifference = item.TaxAmount - expectedTax;
            var extraAmount = Math.Max(0m, item.Amount - expectedAmount);
            var status = BuildTaxStatus(item.TaxRate, expectedRate, unitBasic);
            var suggestedAction = BuildSuggestedAction(status, extraAmount, unitBasic, item.TaxRate, expectedRate);

            var costKey = (item.InvoiceId, item.ProductId, NormalizeBarcode(item.Barcode));
            var cost = costMap.TryGetValue(costKey, out var costSeed)
                ? AllocateCost(costSeed, quantity)
                : 0m;
            var costRate = quantity > 0 ? Math.Round(cost / quantity, 2) : 0m;
            var profit = item.TaxableAmount - cost;
            var profitPercentage = item.TaxableAmount == 0 ? 0m : Math.Round((profit / item.TaxableAmount) * 100m, 2);

            var row = new SaleReviewItemDto(
                invoice.Id,
                item.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                string.IsNullOrWhiteSpace(invoice.StoreName) ? "Store" : invoice.StoreName,
                invoice.CustomerName,
                invoice.CustomerMobileNumber,
                item.ProductId,
                item.ProductName,
                item.Barcode,
                item.HsnCode,
                item.Quantity,
                item.Mrp,
                item.DiscountAmount,
                unitBasic,
                item.TaxableAmount,
                item.TaxRate,
                expectedRate,
                item.TaxAmount,
                expectedTax,
                taxDifference,
                item.Amount,
                expectedAmount,
                extraAmount,
                costRate,
                cost,
                profit,
                profitPercentage,
                status,
                suggestedAction);

            if (!onlyIssues || !string.Equals(row.Status, "OK", StringComparison.OrdinalIgnoreCase))
            {
                allRows.Add(row);
            }
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            allRows = allRows.Where(item =>
                item.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.CustomerMobileNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.Barcode.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var invoiceSummaries = allRows
            .GroupBy(item => item.InvoiceId)
            .Select(group =>
            {
                var first = group.First();
                var taxable = group.Sum(item => item.TaxableAmount);
                var profit = group.Sum(item => item.ProfitAmount);
                var issueCount = group.Count(item => item.Status != "OK");
                return new SaleReviewInvoiceDto(
                    first.InvoiceId,
                    first.InvoiceNumber,
                    first.OnDate,
                    first.StoreName,
                    first.CustomerName,
                    first.CustomerMobileNumber,
                    invoices.FirstOrDefault(item => item.Id == first.InvoiceId)?.BillAmount ?? group.Sum(item => item.Amount),
                    taxable,
                    group.Sum(item => item.TaxAmount),
                    group.Sum(item => item.ExpectedTaxAmount),
                    group.Sum(item => item.TaxDifferenceAmount),
                    group.Sum(item => item.ExtraAmountToReview),
                    group.Sum(item => item.CostAmount),
                    profit,
                    taxable == 0 ? 0m : Math.Round((profit / taxable) * 100m, 2),
                    group.Count(),
                    issueCount,
                    issueCount == 0 ? "OK" : "Needs Review");
            })
            .OrderByDescending(item => item.OnDate)
            .ThenBy(item => item.InvoiceNumber)
            .ToList();

        var totalItems = allRows.Count;
        var pagedItems = allRows
            .OrderByDescending(item => item.OnDate)
            .ThenBy(item => item.InvoiceNumber)
            .ThenBy(item => item.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var summary = new SaleReviewSummaryDto(
            invoiceSummaries.Count,
            allRows.Count,
            allRows.Count(item => item.Status != "OK"),
            invoiceSummaries.Sum(item => item.BillAmount),
            allRows.Sum(item => item.TaxableAmount),
            allRows.Sum(item => item.TaxAmount),
            allRows.Sum(item => item.ExpectedTaxAmount),
            allRows.Sum(item => item.TaxDifferenceAmount),
            allRows.Sum(item => item.ExtraAmountToReview),
            allRows.Sum(item => item.CostAmount),
            allRows.Sum(item => item.ProfitAmount),
            allRows.Sum(item => item.TaxableAmount) == 0 ? 0m : Math.Round((allRows.Sum(item => item.ProfitAmount) / allRows.Sum(item => item.TaxableAmount)) * 100m, 2));

        return new SaleReviewResponseDto(from, to, storeId, term, page, pageSize, totalItems, summary, invoiceSummaries, pagedItems);
    }


    private static async Task<IResult> ApplyAdjustmentAsync(
        SaleReviewApplyAdjustmentRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.InvoiceId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select a valid sale invoice." });
        }

        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .Include(item => item.InvoiceItems)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sale invoice was not found." });
        }
        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cancelled invoices cannot be adjusted from Sale Review." });
        }

        var store = await db.Stores.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == invoice.StoreId && item.CompanyId == invoice.CompanyId, cancellationToken);
        if (store is null)
        {
            return Results.BadRequest(new { message = "Invoice store was not found." });
        }

        var selectedItemIds = request.InvoiceItemIds is { Count: > 0 }
            ? request.InvoiceItemIds.Where(id => id != Guid.Empty).ToHashSet()
            : null;

        var issueItems = (invoice.InvoiceItems ?? new List<InvoiceItem>())
            .Where(item => selectedItemIds is null || selectedItemIds.Contains(item.Id))
            .Select(item => new { Item = item, Quantity = item.BilledQuantity <= 0 ? 1m : item.BilledQuantity, UnitBasic = Math.Round(item.BasePrice / (item.BilledQuantity <= 0 ? 1m : item.BilledQuantity), 2) })
            .Where(row => row.Item.TaxPercentage > LowerApparelGstRate + 0.01m && row.UnitBasic <= ApparelGstThreshold)
            .ToList();

        if (issueItems.Count == 0)
        {
            return Results.BadRequest(new { message = "No 18% below-threshold sale item was found for this invoice. Refresh Sale Review and try again." });
        }

        var oldBillAmount = invoice.BillAmount;
        var oldPaidAmount = invoice.PaidAmount;
        var adjustments = new List<SaleReviewAdjustmentItemDto>();
        decimal totalExtraAmount = 0m;

        foreach (var row in issueItems)
        {
            var item = row.Item;
            var quantity = row.Quantity;
            var oldTaxable = item.BasePrice;
            var oldTax = item.TaxAmount;
            var oldLineAmount = item.Amount;
            var maxAllowedTaxable = ApparelGstThreshold * quantity;
            var nearestTaxable = Math.Round(Math.Min(maxAllowedTaxable, oldLineAmount / (1 + (LowerApparelGstRate / 100m))), 2, MidpointRounding.AwayFromZero);
            nearestTaxable = Math.Max(0m, nearestTaxable);
            var newTax = Math.Round(nearestTaxable * (LowerApparelGstRate / 100m), 2, MidpointRounding.AwayFromZero);
            var newLineAmount = Math.Round(nearestTaxable + newTax, 2, MidpointRounding.AwayFromZero);
            var extraAmount = Math.Round(Math.Max(0m, oldLineAmount - newLineAmount), 2, MidpointRounding.AwayFromZero);

            adjustments.Add(new SaleReviewAdjustmentItemDto(
                item.Id,
                item.ProductName ?? "Item",
                item.Barcode,
                quantity,
                item.TaxPercentage,
                LowerApparelGstRate,
                oldTaxable,
                nearestTaxable,
                oldTax,
                newTax,
                oldLineAmount,
                newLineAmount,
                extraAmount));
            totalExtraAmount += extraAmount;
        }

        totalExtraAmount = Math.Round(totalExtraAmount, 2, MidpointRounding.AwayFromZero);
        var newBillAmount = Math.Round(Math.Max(0m, oldBillAmount - totalExtraAmount), 2, MidpointRounding.AwayFromZero);
        var resultMessage = $"Corrected {adjustments.Count} item(s) to 5% GST. Invoice reduced by ₹{totalExtraAmount:0.00}; Extra Amount receipt will be created for the same amount.";

        if (request.DryRun)
        {
            return Results.Ok(new SaleReviewAdjustmentResultDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                adjustments.Count,
                oldBillAmount,
                newBillAmount,
                totalExtraAmount,
                totalExtraAmount,
                null,
                null,
                true,
                adjustments,
                resultMessage));
        }

        var duplicateMarker = $"SaleReviewAdjustment:{invoice.Id:N}";
        var existingAdjustmentVoucher = await db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.CompanyId == invoice.CompanyId && item.Remarks.Contains(duplicateMarker), cancellationToken);
        if (existingAdjustmentVoucher is not null)
        {
            return Results.Conflict(new
            {
                message = $"This invoice already has Sale Review adjustment voucher {existingAdjustmentVoucher.VoucherNumber}. Open voucher register instead of posting a duplicate.",
                voucherId = existingAdjustmentVoucher.Id,
                voucherNumber = existingAdjustmentVoucher.VoucherNumber
            });
        }

        var employee = await ResolveAdjustmentEmployeeAsync(db, invoice.CompanyId, store.StoreGroupId, invoice.StoreId, request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Results.BadRequest(new { message = "No active employee found for posting the Extra Amount voucher. Create an employee first or pass employeeId." });
        }

        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Id == invoice.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException("Invoice customer is missing. Cannot repost sale accounting safely.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var fivePercentTaxByType = await db.Taxes.AsNoTracking()
            .Where(item => item.CompositeRate == LowerApparelGstRate)
            .ToListAsync(cancellationToken);

        foreach (var adjustment in adjustments)
        {
            var item = issueItems.First(row => row.Item.Id == adjustment.InvoiceItemId).Item;
            var split = SplitGst(adjustment.NewTaxAmount, item.TaxType, invoice.InterState);
            item.BasePrice = adjustment.NewTaxableAmount;
            item.TaxPercentage = LowerApparelGstRate;
            item.TaxAmount = adjustment.NewTaxAmount;
            item.CGSTAmount = split.Cgst;
            item.SGSTAmount = split.Sgst;
            item.IGSTAmount = split.Igst;
            item.Amount = adjustment.NewLineAmount;
            var unitInclusive = adjustment.Quantity <= 0 ? adjustment.NewLineAmount : Math.Round(adjustment.NewLineAmount / adjustment.Quantity, 2, MidpointRounding.AwayFromZero);
            if (unitInclusive > item.MRP)
            {
                item.MRP = unitInclusive;
                item.DiscountAmount = 0m;
            }
            else
            {
                item.DiscountAmount = Math.Round(Math.Max(0m, item.MRP - unitInclusive), 2, MidpointRounding.AwayFromZero);
            }

            var replacementTax = fivePercentTaxByType.FirstOrDefault(tax => tax.TaxType == item.TaxType)
                ?? fivePercentTaxByType.FirstOrDefault(tax => tax.TaxType == TaxType.GST)
                ?? fivePercentTaxByType.FirstOrDefault();
            if (replacementTax is not null)
            {
                item.TaxId = replacementTax.Id;
            }
        }

        RecalculateInvoiceTotals(invoice);
        var paymentAdjustment = ReduceInvoicePayments(invoice, totalExtraAmount);
        invoice.PaidAmount = Math.Round(Math.Max(0m, oldPaidAmount - paymentAdjustment.ReducedAmount), 2, MidpointRounding.AwayFromZero);
        if (invoice.PaidAmount > invoice.BillAmount)
        {
            invoice.PaidAmount = invoice.BillAmount;
        }
        invoice.InvoiceStatus = invoice.PaidAmount >= invoice.BillAmount
            ? InvoiceStatus.Paid
            : invoice.PaidAmount > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Pending;
        invoice.Remarks = AppendRemark(invoice.Remarks, $"Sale Review GST correction applied on {DateTime.Now:yyyy-MM-dd HH:mm}. Extra Amount ₹{totalExtraAmount:0.00}. {request.Remarks}".Trim());

        await db.SaveChangesAsync(cancellationToken);

        var remainingPayments = (invoice.Payments ?? new List<InvoicePayment>())
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
        await accounting.PostSalesInvoiceAsync(invoice, customer, store.StoreGroupId, remainingPayments, cancellationToken);

        Guid? voucherId = null;
        string? voucherNumber = null;
        if (totalExtraAmount > 0)
        {
            var extraLedger = await EnsureExtraAmountLedgerAsync(db, invoice.CompanyId, cancellationToken);
            var sourcePayment = paymentAdjustment.SourcePayment;
            var voucherPaymentMode = sourcePayment?.PaymentMode == PaymentMode.MixPayments
                ? PaymentMode.Cash
                : sourcePayment?.PaymentMode ?? invoice.PaymentMode ?? PaymentMode.Cash;
            var voucherBankAccountId = RequiresBank(voucherPaymentMode) ? sourcePayment?.BankAccountId : null;
            if (RequiresBank(voucherPaymentMode) && !voucherBankAccountId.HasValue)
            {
                voucherPaymentMode = PaymentMode.Cash;
            }

            var barcodes = string.Join(", ", adjustments.Select(item => item.Barcode).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct());
            var particulars = $"EXTRA Amount receipt against GST correction for invoice {invoice.InvoiceNumber}; barcode(s): {barcodes}";
            var posting = await accounting.SaveVoucherInCurrentTransactionAsync(new VoucherSaveRequest(
                null,
                string.Empty,
                invoice.OnDate.Date,
                VoucherType.Receipt,
                invoice.CustomerName ?? customer.Name,
                particulars,
                totalExtraAmount,
                $"{duplicateMarker}; Invoice {invoice.InvoiceNumber}; Item barcode(s): {barcodes}; Tax corrected from 18% to 5%. {request.Remarks}".Trim(),
                invoice.InvoiceNumber,
                voucherPaymentMode,
                sourcePayment?.ReferenceNumber ?? sourcePayment?.GatewayReference ?? "Sale Review Extra Amount",
                false,
                null,
                extraLedger.Id,
                employee.Id,
                voucherBankAccountId,
                invoice.CompanyId,
                store.StoreGroupId,
                invoice.StoreId), cancellationToken);
            voucherId = posting.VoucherId;
            voucherNumber = await db.Vouchers.AsNoTracking()
                .Where(item => item.Id == posting.VoucherId)
                .Select(item => item.VoucherNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new SaleReviewAdjustmentResultDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.OnDate,
            adjustments.Count,
            oldBillAmount,
            invoice.BillAmount,
            totalExtraAmount,
            totalExtraAmount,
            voucherId,
            voucherNumber,
            false,
            adjustments,
            voucherNumber is null
                ? $"Invoice {invoice.InvoiceNumber} corrected to 5% GST. No Extra Amount voucher was required."
                : $"Invoice {invoice.InvoiceNumber} corrected to 5% GST and Extra Amount receipt {voucherNumber} posted for ₹{totalExtraAmount:0.00}."));
    }

    private static SaleReviewSummaryDto EmptySummary()
        => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static string BuildTaxStatus(decimal actualRate, decimal expectedRate, decimal unitBasic)
    {
        if (Math.Abs(actualRate - expectedRate) < 0.01m)
        {
            return "OK";
        }

        if (actualRate > expectedRate && unitBasic <= ApparelGstThreshold)
        {
            return "18% charged below threshold";
        }

        if (actualRate < expectedRate && unitBasic > ApparelGstThreshold)
        {
            return "5% charged above threshold";
        }

        return "GST rate mismatch";
    }

    private static string BuildSuggestedAction(string status, decimal extraAmount, decimal unitBasic, decimal actualRate, decimal expectedRate)
    {
        if (status == "OK")
        {
            return "No GST threshold action required.";
        }

        if (status == "18% charged below threshold")
        {
            return $"Unit basic ₹{unitBasic:0.00} is up to ₹{ApparelGstThreshold:0.00}. Expected GST {expectedRate:0.##}%, but charged {actualRate:0.##}%. Review tax correction; if customer paid the old total, move approx. ₹{extraAmount:0.00} to Extra Amount receipt after accountant approval.";
        }

        if (status == "5% charged above threshold")
        {
            return $"Unit basic ₹{unitBasic:0.00} is above ₹{ApparelGstThreshold:0.00}. Expected GST {expectedRate:0.##}%, but charged {actualRate:0.##}%. Review short-tax/price correction with accountant.";
        }

        return $"Expected GST {expectedRate:0.##}% for unit basic ₹{unitBasic:0.00}; charged {actualRate:0.##}%. Review tax setup before final accounts/GSTR.";
    }

    private static decimal AllocateCost(SaleCostSeed cost, decimal quantity)
    {
        if (cost.Quantity <= 0 || quantity <= 0)
        {
            return 0m;
        }

        return Math.Round((cost.CostAmount / cost.Quantity) * quantity, 2);
    }

    private static string NormalizeBarcode(string? barcode)
        => (barcode ?? string.Empty).Trim().ToUpperInvariant();


    private static void RecalculateInvoiceTotals(Invoice invoice)
    {
        var items = invoice.InvoiceItems ?? new List<InvoiceItem>();
        invoice.MRP = Math.Round(items.Sum(item => item.MRP * (item.BilledQuantity <= 0 ? 1m : item.BilledQuantity)), 2, MidpointRounding.AwayFromZero);
        invoice.BasePrice = Math.Round(items.Sum(item => item.BasePrice), 2, MidpointRounding.AwayFromZero);
        invoice.DiscountAmount = Math.Round(items.Sum(item => item.DiscountAmount * (item.BilledQuantity <= 0 ? 1m : item.BilledQuantity)), 2, MidpointRounding.AwayFromZero);
        invoice.TaxAmount = Math.Round(items.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero);
        invoice.CGSTAmount = Math.Round(items.Sum(item => item.CGSTAmount ?? 0m), 2, MidpointRounding.AwayFromZero);
        invoice.SGSTAmount = Math.Round(items.Sum(item => item.SGSTAmount ?? 0m), 2, MidpointRounding.AwayFromZero);
        invoice.IGSTAmount = Math.Round(items.Sum(item => item.IGSTAmount ?? 0m), 2, MidpointRounding.AwayFromZero);
        invoice.NetAmount = invoice.BasePrice;
        invoice.RoundOff = 0m;
        invoice.BillAmount = Math.Round(invoice.BasePrice + invoice.TaxAmount, 2, MidpointRounding.AwayFromZero);
        invoice.Quantity = Math.Round(items.Sum(item => item.BilledQuantity), 2, MidpointRounding.AwayFromZero);
        invoice.ItemCount = items.Count;
    }

    private static PaymentReductionResult ReduceInvoicePayments(Invoice invoice, decimal reductionAmount)
    {
        var remaining = Math.Round(Math.Max(0m, reductionAmount), 2, MidpointRounding.AwayFromZero);
        InvoicePayment? sourcePayment = null;
        if (remaining <= 0)
        {
            return new PaymentReductionResult(0m, null);
        }

        var payments = (invoice.Payments ?? new List<InvoicePayment>())
            .Where(item => item.Amount > 0)
            .OrderByDescending(item => item.Amount)
            .ToList();
        foreach (var payment in payments)
        {
            if (remaining <= 0)
            {
                break;
            }

            sourcePayment ??= payment;
            var reduce = Math.Min(payment.Amount, remaining);
            payment.Amount = Math.Round(payment.Amount - reduce, 2, MidpointRounding.AwayFromZero);
            remaining = Math.Round(remaining - reduce, 2, MidpointRounding.AwayFromZero);
        }

        var reduced = Math.Round(reductionAmount - remaining, 2, MidpointRounding.AwayFromZero);
        return new PaymentReductionResult(reduced, sourcePayment);
    }

    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitGst(decimal taxAmount, TaxType taxType, bool interState)
    {
        var tax = Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero);
        if (interState || taxType == TaxType.IGST)
        {
            return (0m, 0m, tax);
        }

        if (taxType == TaxType.CGST)
        {
            return (tax, 0m, 0m);
        }

        if (taxType == TaxType.SGST)
        {
            return (0m, tax, 0m);
        }

        var cgst = Math.Round(tax / 2m, 2, MidpointRounding.AwayFromZero);
        return (cgst, tax - cgst, 0m);
    }

    private static async Task<Ledger> EnsureExtraAmountLedgerAsync(
        GarmetixDbContext db,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var ledger = await db.Ledgers.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name == "Extra Amount", cancellationToken);
        if (ledger is not null)
        {
            return ledger;
        }

        var group = await db.LedgerGroups.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name == "Indirect Income", cancellationToken)
            ?? await db.LedgerGroups.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name == "Sales", cancellationToken);
        if (group is null)
        {
            group = new LedgerGroup
            {
                CompanyId = companyId,
                Name = "Indirect Income",
                Category = LedgerCategory.IndirectIncome,
                Remarks = "Income ledgers for non-sale receipts and adjustments.",
                CreatedBy = AccountingDefaultProtection.CreatedByMarker
            };
            db.LedgerGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);
        }

        ledger = new Ledger
        {
            CompanyId = companyId,
            Name = "Extra Amount",
            LedgerGroupId = group.Id,
            LedgerType = LedgerType.Income,
            OpeningBalance = 0m,
            OpeningDate = DateTime.Today,
            IsParty = false,
            CreatedBy = AccountingDefaultProtection.CreatedByMarker
        };
        db.Ledgers.Add(ledger);
        await db.SaveChangesAsync(cancellationToken);
        return ledger;
    }

    private static async Task<Employee?> ResolveAdjustmentEmployeeAsync(
        GarmetixDbContext db,
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        if (employeeId.HasValue && employeeId.Value != Guid.Empty)
        {
            return await db.Employees.FirstOrDefaultAsync(item => item.Id == employeeId.Value && item.CompanyId == companyId, cancellationToken);
        }

        return await db.Employees
            .OrderByDescending(item => item.StoreId == storeId)
            .ThenByDescending(item => item.StoreGroupId == storeGroupId)
            .ThenBy(item => item.FirstName)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && !item.Deleted && item.EmployeeStatus == "Active", cancellationToken)
            ?? await db.Employees
                .OrderByDescending(item => item.StoreId == storeId)
                .ThenBy(item => item.FirstName)
                .FirstOrDefaultAsync(item => item.CompanyId == companyId && !item.Deleted, cancellationToken);
    }

    private static bool RequiresBank(PaymentMode paymentMode)
        => paymentMode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;

    private static string AppendRemark(string? existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return existing ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(existing)
            ? addition.Trim()
            : $"{existing.Trim()} | {addition.Trim()}";
    }

    private sealed record PaymentReductionResult(decimal ReducedAmount, InvoicePayment? SourcePayment);

    private sealed record SaleInvoiceSeed(
        Guid Id,
        string InvoiceNumber,
        DateTime OnDate,
        string CustomerName,
        string CustomerMobileNumber,
        decimal BillAmount,
        Guid StoreId,
        string StoreName);

    private sealed record SaleItemSeed(
        Guid Id,
        Guid InvoiceId,
        Guid ProductId,
        string ProductName,
        string Barcode,
        string? HsnCode,
        decimal Quantity,
        decimal Mrp,
        decimal DiscountAmount,
        decimal TaxableAmount,
        decimal TaxRate,
        decimal TaxAmount,
        decimal Amount);

    private sealed record SaleCostSeed(
        Guid InvoiceId,
        Guid ProductId,
        string Barcode,
        decimal Quantity,
        decimal CostAmount);
}

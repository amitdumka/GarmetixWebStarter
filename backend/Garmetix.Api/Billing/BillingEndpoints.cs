using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Commercial;
using Garmetix.Api.Gstin;
using Garmetix.Api.Inventory;
using Garmetix.Api.InvoiceReplacement;
using Garmetix.Api.Marketing;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Garmetix.Api.Billing;

public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/billing")
            .WithTags("Billing")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("/options", GetBillingOptionsAsync);
        group.MapGet("/customers/search", SearchCustomersAsync);
        group.MapGet("/customers/{customerId:guid}/profile", GetCustomerBillingProfileAsync);
        group.MapPost("/sales", CreateSaleAsync);
        group.MapGet("/sales", SearchSalesAsync);
        group.MapGet("/sales/recent", GetRecentSalesAsync);
        group.MapGet("/sales/{id:guid}/receipt", GetReceiptAsync);
        group.MapPost("/sales/{id:guid}/digital-bill", EnsureSaleDigitalBillAsync);
        group.MapPost("/sales/{id:guid}/digital-bill/send-whatsapp", SendSaleDigitalBillWhatsAppAsync);
        group.MapGet("/sales/{id:guid}/pdf", DownloadInvoicePdfAsync);
        group.MapPut("/sales/{id:guid}", UpdateSaleInvoiceAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/sales/{id:guid}", DeleteSaleInvoiceAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapDelete("/sales/{id:guid}/hard-delete", HardDeleteSaleInvoiceAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/sales/{id:guid}/returns", CreateSalesReturnAsync);
        group.MapPost("/sales/{id:guid}/exchange", CreateSalesExchangeAsync);
        group.MapPost("/sales/{id:guid}/cancel", CancelSaleAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }


    private static async Task<BillingOptionsDto> GetBillingOptionsAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeId = null,
        string? customerQuery = null,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var customers = await LoadCustomerOptionsAsync(context, db, companyId, customerQuery, Math.Clamp(take, 1, 100), cancellationToken);
        var salesmenQuery = WorkspaceScope.ApplyTo(db.Salesmen.AsNoTracking(), context).Where(item => item.Active);
        if (companyId.HasValue)
        {
            salesmenQuery = salesmenQuery.Where(item => item.CompanyId == companyId.Value);
        }
        if (storeId.HasValue)
        {
            salesmenQuery = salesmenQuery.Where(item => item.StoreId == storeId.Value);
        }

        var salesmen = await salesmenQuery
            .OrderBy(item => item.Name)
            .Take(100)
            .Select(item => new BillingSalesmanOptionDto(item.Id, item.Name, item.StoreId, item.Active))
            .ToListAsync(cancellationToken);

        var loyaltyProgram = storeId.HasValue
            ? await LoadLoyaltyProgramAsync(context, db, storeId.Value, cancellationToken)
            : null;

        return new BillingOptionsDto(customers, salesmen, loyaltyProgram);
    }

    private static async Task<IReadOnlyList<BillingCustomerOptionDto>> SearchCustomersAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        string? q = null,
        int take = 25,
        CancellationToken cancellationToken = default)
    {
        return await LoadCustomerOptionsAsync(context, db, companyId, q, Math.Clamp(take, 1, 100), cancellationToken);
    }

    private static async Task<IResult> GetCustomerBillingProfileAsync(
        Guid customerId,
        HttpContext context,
        GarmetixDbContext db,
        Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        var customer = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context)
            .Where(item => item.Id == customerId)
            .Select(item => new BillingCustomerOptionDto(
                item.Id,
                item.Name,
                item.MobileNumber,
                item.GSTIN,
                item.CreditBalance,
                item.LoyaltyPoints,
                item.Amount,
                item.BillCount,
                item.Name + " | " + item.MobileNumber + (item.GSTIN != null ? " | GSTIN " + item.GSTIN : string.Empty)))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return Results.NotFound();
        }

        var creditNotesQuery = WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => item.CustomerId == customerId && item.NoteType == NoteType.CreditNote && !item.IsAdjusted && item.Amount > item.AdjustedAmount);
        if (storeId.HasValue)
        {
            creditNotesQuery = creditNotesQuery.Where(item => item.StoreId == storeId.Value);
        }

        var creditNotes = await creditNotesQuery
            .OrderBy(item => item.OnDate)
            .Take(50)
            .Select(item => new BillingAdjustmentOptionDto(
                item.Id,
                item.NoteNumber,
                item.OnDate,
                item.Amount,
                item.AdjustedAmount,
                item.Amount - item.AdjustedAmount,
                item.SourceType,
                item.SourceNumber))
            .ToListAsync(cancellationToken);

        var advanceQuery = WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts.AsNoTracking(), context)
            .Where(item => item.CustomerId == customerId && item.AvailableAmount > 0);
        if (storeId.HasValue)
        {
            advanceQuery = advanceQuery.Where(item => item.StoreId == storeId.Value);
        }

        var advanceReceipts = await advanceQuery
            .OrderBy(item => item.OnDate)
            .Take(50)
            .Select(item => new BillingAdjustmentOptionDto(
                item.Id,
                item.ReceiptNumber,
                item.OnDate,
                item.Amount,
                item.AdjustedAmount,
                item.AvailableAmount,
                "CustomerAdvanceReceipt",
                item.ReferenceNumber))
            .ToListAsync(cancellationToken);

        var loyaltyProgram = storeId.HasValue
            ? await LoadLoyaltyProgramAsync(context, db, storeId.Value, cancellationToken)
            : null;

        return Results.Ok(new BillingCustomerProfileDto(customer, creditNotes, advanceReceipts, loyaltyProgram));
    }

    private static async Task<IReadOnlyList<BillingCustomerOptionDto>> LoadCustomerOptionsAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        string? query,
        int take,
        CancellationToken cancellationToken)
    {
        var customerQuery = WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context);
        if (companyId.HasValue)
        {
            customerQuery = customerQuery.Where(item => item.CompanyId == companyId.Value);
        }

        var term = query?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var lowered = term.ToLower();
            customerQuery = customerQuery.Where(item =>
                item.Name.ToLower().Contains(lowered) ||
                item.MobileNumber.ToLower().Contains(lowered) ||
                (item.GSTIN != null && item.GSTIN.ToLower().Contains(lowered)));
        }

        return await customerQuery
            .OrderBy(item => item.Name)
            .ThenBy(item => item.MobileNumber)
            .Take(take)
            .Select(item => new BillingCustomerOptionDto(
                item.Id,
                item.Name,
                item.MobileNumber,
                item.GSTIN,
                item.CreditBalance,
                item.LoyaltyPoints,
                item.Amount,
                item.BillCount,
                item.Name + " | " + item.MobileNumber + (item.GSTIN != null ? " | GSTIN " + item.GSTIN : string.Empty)))
            .ToListAsync(cancellationToken);
    }

    private static async Task<BillingLoyaltyProgramDto?> LoadLoyaltyProgramAsync(HttpContext context, GarmetixDbContext db, Guid storeId, CancellationToken cancellationToken)
    {
        return await WorkspaceScope.ApplyTo(db.LoyaltyPrograms.AsNoTracking(), context)
            .Where(item => item.StoreId == storeId && item.Enabled)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new BillingLoyaltyProgramDto(item.Enabled, item.RedeemValuePerPoint, item.EarnPointsPerRupee, item.MinimumBillAmount))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<PagedSaleInvoicesDto> SearchSalesAsync(
        HttpContext context,
        GarmetixDbContext db,
        string? datePreset = "today",
        int? year = null,
        int? month = null,
        int page = 1,
        int pageSize = 50,
        string? q = null,
        string? status = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 25, 200);

        var (fromDate, toDateExclusive, resolvedPreset) = ResolveSalesDateRange(datePreset, year, month, from, to);
        var term = q?.Trim().ToLowerInvariant();

        var query = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(invoice => invoice.OnDate >= fromDate && invoice.OnDate < toDateExclusive);

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(invoice => invoice.InvoiceStatus == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.ToLower().Contains(term) ||
                (invoice.CustomerName != null && invoice.CustomerName.ToLower().Contains(term)) ||
                (invoice.CustomerMobileNumber != null && invoice.CustomerMobileNumber.ToLower().Contains(term)) ||
                (invoice.CustomerGSTIN != null && invoice.CustomerGSTIN.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                BillAmount = group.Sum(invoice => invoice.BillAmount),
                PaidAmount = group.Sum(invoice => invoice.PaidAmount),
                CancelledCount = group.Count(invoice => invoice.InvoiceStatus == InvoiceStatus.Cancelled)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var invoiceRows = await query
            .OrderByDescending(invoice => invoice.OnDate)
            .ThenByDescending(invoice => invoice.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                CustomerName = invoice.CustomerName ?? "Walk-in Customer",
                invoice.CustomerMobileNumber,
                invoice.BillAmount,
                invoice.PaidAmount,
                BalanceAmount = invoice.BillAmount - invoice.PaidAmount,
                InvoiceStatus = invoice.InvoiceStatus.ToString(),
                PaymentMode = invoice.PaymentMode.HasValue ? invoice.PaymentMode.Value.ToString() : string.Empty,
                invoice.Remarks
            })
            .ToListAsync(cancellationToken);

        var digitalBills = await LoadDigitalBillSummariesAsync(context, db, invoiceRows.Select(item => item.Id), cancellationToken);

        var items = invoiceRows
            .Select(invoice =>
            {
                digitalBills.TryGetValue(invoice.Id, out var digitalBill);
                return ToRecentInvoiceDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.OnDate,
                    invoice.CustomerName,
                    invoice.CustomerMobileNumber,
                    invoice.BillAmount,
                    invoice.PaidAmount,
                    invoice.BalanceAmount,
                    invoice.InvoiceStatus,
                    invoice.PaymentMode,
                    digitalBill,
                    invoice.Remarks);
            })
            .ToList();

        return new PagedSaleInvoicesDto(
            items,
            total,
            page,
            pageSize,
            resolvedPreset,
            fromDate,
            toDateExclusive.AddDays(-1),
            totals?.BillAmount ?? 0,
            totals?.PaidAmount ?? 0,
            (totals?.BillAmount ?? 0) - (totals?.PaidAmount ?? 0),
            totals?.CancelledCount ?? 0);
    }

    private static (DateTime FromDate, DateTime ToDateExclusive, string ResolvedPreset) ResolveSalesDateRange(
        string? datePreset,
        int? year,
        int? month,
        DateTime? from,
        DateTime? to)
    {
        var today = DateTime.Today;
        var preset = string.IsNullOrWhiteSpace(datePreset) ? "today" : datePreset.Trim().ToLowerInvariant();

        if (preset is "custom")
        {
            var fromDate = from?.Date ?? today;
            var toDate = to?.Date ?? fromDate;
            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            return (fromDate, toDate.AddDays(1), "custom");
        }

        if (preset is "month-year" or "monthyear")
        {
            var selectedYear = year.GetValueOrDefault(today.Year);
            var selectedMonth = Math.Clamp(month.GetValueOrDefault(today.Month), 1, 12);
            var start = new DateTime(selectedYear, selectedMonth, 1);
            return (start, start.AddMonths(1), "month-year");
        }

        if (preset is "year" or "yearly")
        {
            var selectedYear = year.GetValueOrDefault(today.Year);
            var start = new DateTime(selectedYear, 1, 1);
            return (start, start.AddYears(1), "year");
        }

        if (preset is "last-month" or "lastmonth")
        {
            var start = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            return (start, start.AddMonths(1), "last-month");
        }

        if (preset is "month" or "current-month" or "currentmonth")
        {
            var start = new DateTime(today.Year, today.Month, 1);
            return (start, start.AddMonths(1), "month");
        }

        if (preset is "yesterday")
        {
            var start = today.AddDays(-1);
            return (start, today, "yesterday");
        }

        return (today, today.AddDays(1), "today");
    }

    private static async Task<IReadOnlyList<RecentInvoiceDto>> GetRecentSalesAsync(HttpContext context, GarmetixDbContext db, int take = 25, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var invoiceRows = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .OrderByDescending(invoice => invoice.OnDate)
            .ThenByDescending(invoice => invoice.CreatedAt)
            .Take(take)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                CustomerName = invoice.CustomerName ?? "Walk-in Customer",
                invoice.CustomerMobileNumber,
                invoice.BillAmount,
                invoice.PaidAmount,
                BalanceAmount = invoice.BillAmount - invoice.PaidAmount,
                InvoiceStatus = invoice.InvoiceStatus.ToString(),
                PaymentMode = invoice.PaymentMode.HasValue ? invoice.PaymentMode.Value.ToString() : string.Empty,
                invoice.Remarks
            })
            .ToListAsync(cancellationToken);

        var digitalBills = await LoadDigitalBillSummariesAsync(context, db, invoiceRows.Select(item => item.Id), cancellationToken);

        return invoiceRows
            .Select(invoice =>
            {
                digitalBills.TryGetValue(invoice.Id, out var digitalBill);
                return ToRecentInvoiceDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.OnDate,
                    invoice.CustomerName,
                    invoice.CustomerMobileNumber,
                    invoice.BillAmount,
                    invoice.PaidAmount,
                    invoice.BalanceAmount,
                    invoice.InvoiceStatus,
                    invoice.PaymentMode,
                    digitalBill,
                    invoice.Remarks);
            })
            .ToList();
    }

    private sealed record DigitalBillSaleSummary(
        Guid Id,
        Guid InvoiceId,
        string PublicToken,
        bool IsActive,
        string WhatsAppStatus,
        int OpenCount,
        int PdfDownloadCount,
        int ReviewClickCount,
        DateTime? LastWhatsAppSentAt);

    private static async Task<Dictionary<Guid, DigitalBillSaleSummary>> LoadDigitalBillSummariesAsync(
        HttpContext context,
        GarmetixDbContext db,
        IEnumerable<Guid> invoiceIds,
        CancellationToken cancellationToken)
    {
        var ids = invoiceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, DigitalBillSaleSummary>();
        }

        var rows = await WorkspaceScope.ApplyTo(db.DigitalInvoices.AsNoTracking(), context)
            .Where(item => ids.Contains(item.InvoiceId) && item.InvoiceType == "Sale" && !item.Deleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new DigitalBillSaleSummary(
                item.Id,
                item.InvoiceId,
                item.PublicToken,
                item.IsActive,
                item.WhatsAppStatus,
                item.OpenCount,
                item.PdfDownloadCount,
                item.ReviewClickCount,
                item.LastWhatsAppSentAt))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static RecentInvoiceDto ToRecentInvoiceDto(
        Guid id,
        string invoiceNumber,
        DateTime onDate,
        string customerName,
        string? customerMobileNumber,
        decimal billAmount,
        decimal paidAmount,
        decimal balanceAmount,
        string invoiceStatus,
        string paymentMode,
        DigitalBillSaleSummary? digitalBill,
        string? remarks = null)
        => new(
            id,
            invoiceNumber,
            onDate,
            customerName,
            customerMobileNumber ?? string.Empty,
            billAmount,
            paidAmount,
            balanceAmount,
            invoiceStatus,
            paymentMode,
            digitalBill?.Id,
            digitalBill is null ? null : BuildDigitalBillPublicPath(digitalBill.PublicToken),
            digitalBill?.PublicToken,
            digitalBill?.IsActive,
            digitalBill?.WhatsAppStatus,
            digitalBill?.OpenCount ?? 0,
            digitalBill?.PdfDownloadCount ?? 0,
            digitalBill?.ReviewClickCount ?? 0,
            digitalBill?.LastWhatsAppSentAt,
            remarks);

    private static string BuildDigitalBillPublicPath(string token) => $"/i/{Uri.EscapeDataString(token)}";

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var receipt = await LoadReceiptAsync(id, context, db, cancellationToken);
        return receipt is null ? Results.NotFound() : Results.Ok(receipt);
    }

    private static async Task<IResult> EnsureSaleDigitalBillAsync(
        Guid id,
        HttpContext context,
        DigitalBillCrmService digitalBills,
        CancellationToken cancellationToken)
    {
        var response = await digitalBills.EnsureForSaleInvoiceAsync(id, context, cancellationToken);
        return response is null
            ? Results.NotFound(new { message = "Sale invoice was not found in your workspace." })
            : Results.Ok(response);
    }

    private static async Task<IResult> SendSaleDigitalBillWhatsAppAsync(
        Guid id,
        HttpContext context,
        DigitalBillCrmService digitalBills,
        DigitalBillWhatsAppService whatsApp,
        CancellationToken cancellationToken)
    {
        var digitalBill = await digitalBills.EnsureForSaleInvoiceAsync(id, context, cancellationToken);
        if (digitalBill is null)
        {
            return Results.NotFound(new { message = "Sale invoice was not found in your workspace." });
        }

        var response = await whatsApp.SendForDigitalInvoiceAsync(digitalBill.Id, context, cancellationToken, force: true);
        return response is null
            ? Results.NotFound(new { message = "Digital bill was not found in your workspace." })
            : Results.Ok(response);
    }

    private static async Task<IResult> DownloadInvoicePdfAsync(
        Guid id,
        string? format,
        string? copy,
        bool? reprint,
        bool? signatures,
        HttpContext context,
        GarmetixDbContext db,
        IConfiguration configuration,
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
                item.ProductId,
                item.ProductName ?? (item.Product != null ? item.Product.Name : item.Barcode),
                item.Barcode,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxPercentage,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.HSNCode ?? (item.Product != null ? item.Product.HSNCode : null),
                item.Unit.HasValue ? item.Unit.Value.ToString() : null,
                item.Amount))
            .ToListAsync(cancellationToken);

        var payments = await db.InvoicePayments
            .AsNoTracking()
            .Where(item => item.InvoiceId == id)
            .OrderBy(item => item.OnDate)
            .Select(item => new ReceiptPaymentDto(
                item.Id,
                item.OnDate,
                item.Amount,
                item.PaymentMode.ToString(),
                item.ReferenceNumber,
                item.GatewayReference,
                item.SettlementStatus,
                item.AdjustmentSourceType))
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
            payments,
            Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.SaleInvoice, invoice.Id),
            invoice.Remarks,
            BuildGoodsReturnPolicyUrl(context, configuration));

        var pdf = InvoicePdfDocument.Build(
            model,
            format ?? "a4",
            copy ?? "customer",
            reprint == true,
            signatures != false);
        var safeNumber = Regex.Replace(invoice.InvoiceNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "invoice")}-{NormalizePdfFormat(format)}.pdf");
    }


    private static string BuildGoodsReturnPolicyUrl(HttpContext context, IConfiguration configuration)
    {
        var configuredBase = configuration["DigitalBills:PublicBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            return $"{configuredBase}/goods-return-policy";
        }

        var scheme = string.IsNullOrWhiteSpace(context.Request.Scheme) ? "https" : context.Request.Scheme;
        var host = context.Request.Host.HasValue ? context.Request.Host.Value : "garmetix.aadwikafashion.in";
        return $"{scheme}://{host}/goods-return-policy";
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
                item.ProductId,
                item.ProductName ?? (item.Product != null ? item.Product.Name : item.Barcode),
                item.Barcode,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxPercentage,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.HSNCode ?? (item.Product != null ? item.Product.HSNCode : null),
                item.Unit.HasValue ? item.Unit.Value.ToString() : null,
                item.Amount))
            .ToListAsync(cancellationToken);

        var payments = await db.InvoicePayments
            .AsNoTracking()
            .Where(item => item.InvoiceId == id)
            .OrderBy(item => item.OnDate)
            .Select(item => new ReceiptPaymentDto(
                item.Id,
                item.OnDate,
                item.Amount,
                item.PaymentMode.ToString(),
                item.ReferenceNumber,
                item.GatewayReference,
                item.SettlementStatus,
                item.AdjustmentSourceType))
            .ToListAsync(cancellationToken);

        var digitalBills = await LoadDigitalBillSummariesAsync(context, db, new[] { invoice.Id }, cancellationToken);
        digitalBills.TryGetValue(invoice.Id, out var digitalBill);

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
            payments,
            digitalBill?.Id,
            digitalBill is null ? null : BuildDigitalBillPublicPath(digitalBill.PublicToken),
            digitalBill?.PublicToken,
            digitalBill?.IsActive,
            digitalBill?.WhatsAppStatus,
            digitalBill?.OpenCount ?? 0,
            digitalBill?.PdfDownloadCount ?? 0,
            digitalBill?.ReviewClickCount ?? 0,
            digitalBill?.LastWhatsAppSentAt,
            invoice.Remarks);
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

    private static Task<IResult> CreateSaleAsync(
        PosSaleRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        GstinLookupService gstinLookup,
        StockLedgerService stockLedger,
        DigitalBillCrmService digitalBills,
        DigitalBillWhatsAppService digitalBillWhatsApp,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => CreateSaleCoreAsync(
            request,
            context,
            db,
            documentNumbers,
            accounting,
            gstinLookup,
            stockLedger,
            digitalBills,
            digitalBillWhatsApp,
            cancellationToken));
    }

    private static async Task<IResult> CreateSaleCoreAsync(
        PosSaleRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        GstinLookupService gstinLookup,
        StockLedgerService stockLedger,
        DigitalBillCrmService digitalBills,
        DigitalBillWhatsAppService digitalBillWhatsApp,
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

        var billingStore = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId)
            .Select(store => new
            {
                SupplierGstin = store.Company != null ? store.Company.GSTIN : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (billingStore is null)
        {
            return Results.BadRequest(new { message = "Selected billing store is outside your access scope." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var customerValidation = !string.IsNullOrWhiteSpace(request.CustomerGstin)
            ? await gstinLookup.ValidatePartyAsync("Customer", request.CustomerGstin, request.CustomerName, null, cancellationToken)
            : null;
        var customer = await GetOrCreateCustomerAsync(request, db, gstinLookup, customerValidation, cancellationToken);
        var interState = IsInterStateSupply(billingStore.SupplierGstin, customer.GSTIN);
        var invoiceNumber = await documentNumbers.NextSaleInvoiceAsync(request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken);
        var invoiceId = Guid.NewGuid();

        var invoiceItems = new List<InvoiceItem>();
        decimal grossMrp = 0;
        decimal itemDiscount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        decimal totalQuantity = 0;

        foreach (var requestItem in request.Items)
        {
            await DocumentNumberGenerator.LockStockKeyAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, requestItem.ProductId, requestItem.Barcode, cancellationToken);

            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context)
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item =>
                    item.ProductId == requestItem.ProductId &&
                    item.Barcode == requestItem.Barcode &&
                    item.StoreId == request.StoreId &&
                    !item.IsOFB,
                    cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock not found for barcode {requestItem.Barcode}." });
            }

            var stockSnapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            if (stockSnapshot.Quantity < requestItem.Quantity)
            {
                return Results.BadRequest(new { message = $"Insufficient stock for {requestItem.Barcode}. Available: {stockSnapshot.Quantity}." });
            }

            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var taxable = Math.Round((lineMrp - lineDiscount) / (1 + (stock.TaxRate / 100)), 2);
            var tax = Math.Round(taxable * (stock.TaxRate / 100), 2);
            var lineAmount = taxable + tax;
            var split = SplitGst(tax, stock.TaxType, interState);

            invoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoiceId,
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
                CompanyId = request.CompanyId
            });

            stock.SoldValue += lineAmount;
            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "SaleOut",
                QuantityOut = requestItem.Quantity,
                CostPrice = stockSnapshot.AverageCost,
                MRP = requestItem.Mrp,
                TaxRate = stock.TaxRate,
                HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
                SourceType = "SalesInvoice",
                SourceId = invoiceId,
                SourceNumber = invoiceNumber,
                Remarks = "POS sale",
                OnDate = DateTime.Now,
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
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

        if (request.BillDiscountAmount < 0)
        {
            return Results.BadRequest(new { message = "Bill discount cannot be negative." });
        }

        var totalDiscount = itemDiscount + request.BillDiscountAmount;
        if (totalDiscount > grossMrp)
        {
            return Results.BadRequest(new { message = "Total discount cannot be greater than gross MRP." });
        }

        if (request.BillDiscountAmount > 0)
        {
            var allocatedTotals = ApplyBillDiscountToInvoiceItems(invoiceItems, request.BillDiscountAmount, interState);
            taxableAmount = allocatedTotals.TaxableAmount;
            taxAmount = allocatedTotals.TaxAmount;
            cgstAmount = allocatedTotals.CgstAmount;
            sgstAmount = allocatedTotals.SgstAmount;
            igstAmount = allocatedTotals.IgstAmount;
        }

        var billAmount = Math.Round(grossMrp - totalDiscount, 0);
        var paymentDetails = NormalizeInvoicePayments(request, billAmount);
        var paidAmount = paymentDetails.Sum(item => item.Amount);
        if (paidAmount > billAmount)
        {
            return Results.BadRequest(new { message = "Payment total cannot be greater than bill amount." });
        }

        var invalidBankPayment = paymentDetails.FirstOrDefault(item => RequiresBankAccount(item.PaymentMode) && !item.BankAccountId.HasValue);
        if (invalidBankPayment is not null)
        {
            return Results.BadRequest(new { message = $"Select bank account/reference account for {invalidBankPayment.PaymentMode} payment." });
        }

        var salesmanId = await ResolveRequiredSalesmanIdAsync(
            request.SalesmanId,
            request.CompanyId,
            request.StoreId,
            context,
            db,
            cancellationToken);
        if (salesmanId is null)
        {
            return Results.BadRequest(new { message = request.SalesmanId.HasValue && request.SalesmanId.Value != Guid.Empty
                ? "Selected salesman is outside the billing store scope."
                : "Create or activate at least one salesman for this billing store before saving invoices." });
        }

        var invoicePaymentMode = paymentDetails.Count > 1
            ? PaymentMode.MixPayments
            : paymentDetails.FirstOrDefault()?.PaymentMode ?? request.PaymentMode;

        if (request.OriginalInvoiceId.HasValue)
        {
            var originalInvoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .FirstOrDefaultAsync(item => item.Id == request.OriginalInvoiceId.Value, cancellationToken);
            if (originalInvoice is null)
            {
                return Results.BadRequest(new { message = "Original sale invoice for replacement was not found." });
            }
            if (originalInvoice.CompanyId != request.CompanyId || originalInvoice.StoreId != request.StoreId)
            {
                return Results.BadRequest(new { message = "Replacement sale invoice must use the same company and store as the original invoice." });
            }
            if (originalInvoice.InvoiceStatus == InvoiceStatus.Cancelled)
            {
                return Results.Conflict(new { message = "Original sale invoice is already cancelled." });
            }
        }

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
            CGSTAmount = cgstAmount,
            SGSTAmount = sgstAmount,
            IGSTAmount = igstAmount,
            InterState = interState,
            NetAmount = taxableAmount,
            RoundOff = billAmount - (taxableAmount + taxAmount),
            BillAmount = billAmount,
            Quantity = totalQuantity,
            ItemCount = invoiceItems.Count,
            PaymentMode = invoicePaymentMode,
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CustomerMobileNumber = customer.MobileNumber,
            CustomerGSTIN = customer.GSTIN,
            B2BSale = !string.IsNullOrWhiteSpace(customer.GSTIN),
            SaleInvoiceType = !string.IsNullOrWhiteSpace(customer.GSTIN) ? SaleInvoiceType.B2B : SaleInvoiceType.B2C,
            SalemanId = salesmanId.Value,
            CreditSale = paidAmount < billAmount,
            PaidAmount = paidAmount,
            BillDiscountAmount = request.BillDiscountAmount,
            StoreId = request.StoreId,
            CompanyId = request.CompanyId,
            OriginalInvoiceId = request.OriginalInvoiceId
        };

        var paymentAdjustmentError = await ApplyInvoicePaymentAdjustmentsAsync(invoice, customer, paymentDetails, request.StoreGroupId, db, cancellationToken);
        if (!string.IsNullOrWhiteSpace(paymentAdjustmentError))
        {
            return Results.BadRequest(new { message = paymentAdjustmentError });
        }

        db.SalesInvoices.Add(invoice);
        db.InvoiceItems.AddRange(invoiceItems);
        AddInvoicePayments(invoice, paymentDetails, db);
        if (request.OriginalInvoiceId.HasValue || request.ReplacementApprovalRequested)
        {
            InvoiceReplacementAudit.Add(
                db,
                context,
                "Requested",
                nameof(Invoice),
                invoice.Id,
                "Sales Invoice Replacement",
                invoice.InvoiceNumber,
                invoice.CompanyId,
                request.StoreGroupId,
                invoice.StoreId,
                string.IsNullOrWhiteSpace(request.ReplacementReason)
                    ? $"Revised sale invoice {invoice.InvoiceNumber} created for replacement approval."
                    : request.ReplacementReason.Trim(),
                before: new { originalInvoiceId = request.OriginalInvoiceId },
                after: new { revisedInvoiceId = invoice.Id, revisedInvoiceNumber = invoice.InvoiceNumber, invoice.BillAmount });
        }

        customer.BillCount += 1;
        customer.Amount += billAmount;
        await LoyaltyService.AwardSalePointsAsync(invoice, customer, db, cancellationToken);
        await accounting.PostSalesInvoiceAsync(invoice, customer, request.StoreGroupId, ToAccountingPaymentPostings(paymentDetails), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        DigitalBillActionResponseDto? digitalBill = null;
        WhatsAppSendResultDto? digitalBillWhatsAppResult = null;
        try
        {
            digitalBill = await digitalBills.EnsureForSaleInvoiceAsync(invoice.Id, context, cancellationToken);
            if (digitalBill is not null)
            {
                digitalBillWhatsAppResult = await digitalBillWhatsApp.TryAutoSendForDigitalInvoiceAsync(digitalBill.Id, context, cancellationToken);
            }
        }
        catch
        {
            // Digital bill generation/WhatsApp sending must never fail or roll back a completed sale invoice.
            // The admin can regenerate the link or resend WhatsApp manually from Marketing & CRM → Digital Bills.
        }

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
            customerValidation?.Alerts ?? Array.Empty<string>(),
            digitalBill?.PublicPath,
            digitalBill?.PublicToken,
            digitalBillWhatsAppResult?.Status,
            digitalBillWhatsAppResult?.ErrorMessage));
    }


    private static async Task<IResult> UpdateSaleInvoiceAsync(
        Guid id,
        UpdateSaleInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sales invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cancelled sales invoices cannot be edited." });
        }

        var invoiceNumber = request.InvoiceNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var exists = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .AnyAsync(item => item.Id != id && item.CompanyId == invoice.CompanyId && item.InvoiceNumber == invoiceNumber, cancellationToken);
            if (exists)
            {
                return Results.Conflict(new { message = $"Sales invoice number {invoiceNumber} already exists." });
            }
            invoice.InvoiceNumber = invoiceNumber;
        }

        if (request.OnDate.HasValue) invoice.OnDate = request.OnDate.Value.Date;
        if (!string.IsNullOrWhiteSpace(request.CustomerName)) invoice.CustomerName = request.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(request.CustomerMobileNumber)) invoice.CustomerMobileNumber = request.CustomerMobileNumber.Trim();
        invoice.CustomerGSTIN = string.IsNullOrWhiteSpace(request.CustomerGstin) ? null : request.CustomerGstin.Trim().ToUpperInvariant();
        if (request.SalesmanId.HasValue && request.SalesmanId.Value != Guid.Empty) invoice.SalemanId = request.SalesmanId.Value;
        invoice.Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerMobileNumber, invoice.CustomerGSTIN, salesmanId = invoice.SalemanId, invoice.Remarks });
    }

    private static Task<IResult> DeleteSaleInvoiceAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
        => CancelSaleAsync(
            id,
            new CancelInvoiceRequest("Deleted/cancelled from sales invoice register"),
            context,
            db,
            accounting,
            stockLedger,
            cancellationToken);


    private static Task<IResult> HardDeleteSaleInvoiceAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        string? confirmInvoiceNumber = null,
        string? reason = null,
        bool deleteAudit = false,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => HardDeleteSaleInvoiceCoreAsync(
            id,
            context,
            db,
            confirmInvoiceNumber,
            reason,
            deleteAudit,
            cancellationToken));
    }

    private static async Task<IResult> HardDeleteSaleInvoiceCoreAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        string? confirmInvoiceNumber,
        string? reason,
        bool deleteAudit,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Sales invoice was not found." });
        }

        if (!string.Equals(confirmInvoiceNumber?.Trim(), invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = $"Type invoice number {invoice.InvoiceNumber} to confirm hard delete." });
        }

        if (invoice.InvoiceStatus != InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Hard delete is allowed only after invoice is cancelled/reversed. First use Delete/Cancel so stock, payment and accounting are reversed safely." });
        }

        var linkedInvoices = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.OriginalInvoiceId == invoice.Id && item.CompanyId == invoice.CompanyId)
            .Select(item => item.InvoiceNumber)
            .Take(5)
            .ToListAsync(cancellationToken);
        if (linkedInvoices.Count > 0)
        {
            return Results.Conflict(new { message = $"This invoice has linked revised/return/exchange documents: {string.Join(", ", linkedInvoices)}. Hard delete those linked documents first or keep the audit chain." });
        }

        var invoiceNumber = invoice.InvoiceNumber;
        var companyId = invoice.CompanyId;
        var storeId = invoice.StoreId;
        var storeGroupId = await db.Stores.AsNoTracking()
            .Where(item => item.Id == storeId)
            .Select(item => item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);

        var saleBankReference = $"SI-{invoiceNumber}";
        var saleCancelBankReference = $"SIC-{invoiceNumber}";
        var saleReturnBankReference = $"SR-{invoiceNumber}";
        var saleExchangeBankReference = $"SX-{invoiceNumber}";

        var bankTransactions = await db.BankTransactions
            .Where(item => item.CompanyId == companyId && item.Reference != null &&
                (item.Reference == saleBankReference || item.Reference.StartsWith(saleBankReference + "-") ||
                 item.Reference == saleCancelBankReference || item.Reference.StartsWith(saleCancelBankReference + "-") ||
                 item.Reference == saleReturnBankReference || item.Reference.StartsWith(saleReturnBankReference + "-") ||
                 item.Reference == saleExchangeBankReference || item.Reference.StartsWith(saleExchangeBankReference + "-")))
            .ToListAsync(cancellationToken);
        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToList();

        var bankStatementLines = bankTransactionIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.BankStatementLine>()
            : await db.BankStatementLines.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)).ToListAsync(cancellationToken);
        var chequeLogs = bankTransactionIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.ChequeLog>()
            : await db.ChequeLogs.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)).ToListAsync(cancellationToken);

        var journalEntries = await db.JournalEntries
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoice.Id) ||
                 item.ReferenceNumber == $"SI-{invoiceNumber}" ||
                 item.ReferenceNumber == $"SIC-{invoiceNumber}" ||
                 item.ReferenceNumber == $"SR-{invoiceNumber}" ||
                 item.ReferenceNumber == $"SX-{invoiceNumber}"))
            .ToListAsync(cancellationToken);
        var journalEntryIds = journalEntries.Select(item => item.Id).ToList();
        var journalLines = journalEntryIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.JournalLine>()
            : await db.JournalLines.Where(item => journalEntryIds.Contains(item.JournalEntryId)).ToListAsync(cancellationToken);

        var invoiceItems = await db.InvoiceItems.Where(item => item.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
        var invoicePayments = await db.InvoicePayments.Where(item => item.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
        var cardPayments = await db.CardPayments.Where(item => item.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
        var stockMovements = await db.StockMovements
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoice.Id) ||
                 item.SourceNumber == invoiceNumber ||
                 item.SourceNumber == $"SI-{invoiceNumber}" ||
                 item.SourceNumber == $"SIC-{invoiceNumber}"))
            .ToListAsync(cancellationToken);
        var commercialNotes = await db.CommercialNotes
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoice.Id) || item.SourceNumber == invoiceNumber))
            .ToListAsync(cancellationToken);
        var loyaltyLedgers = await db.LoyaltyPointLedgers
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoice.Id) || item.SourceNumber == invoiceNumber))
            .ToListAsync(cancellationToken);
        var auditEntries = deleteAudit
            ? await db.AuditLogEntries.Where(item => item.EntityId == invoice.Id || item.Reference == invoiceNumber || item.Reference == $"SI-{invoiceNumber}" || item.Reference == $"SIC-{invoiceNumber}").ToListAsync(cancellationToken)
            : new List<AuditLogEntry>();
        var digitalInvoices = await db.DigitalInvoices
            .Where(item => item.CompanyId == companyId && item.InvoiceId == invoice.Id && !item.Deleted)
            .ToListAsync(cancellationToken);

        var response = new AdminHardDeleteSaleResponse(
            invoice.Id,
            invoiceNumber,
            "HardDeleted",
            invoiceItems.Count,
            invoicePayments.Count,
            cardPayments.Count,
            stockMovements.Count,
            journalEntries.Count,
            journalLines.Count,
            bankTransactions.Count,
            bankStatementLines.Count,
            chequeLogs.Count,
            commercialNotes.Count,
            loyaltyLedgers.Count,
            auditEntries.Count);

        db.BankStatementLines.RemoveRange(bankStatementLines);
        db.ChequeLogs.RemoveRange(chequeLogs);
        db.BankTransactions.RemoveRange(bankTransactions);
        db.JournalLines.RemoveRange(journalLines);
        db.JournalEntries.RemoveRange(journalEntries);
        db.InvoicePayments.RemoveRange(invoicePayments);
        db.CardPayments.RemoveRange(cardPayments);
        db.InvoiceItems.RemoveRange(invoiceItems);
        db.StockMovements.RemoveRange(stockMovements);
        db.CommercialNotes.RemoveRange(commercialNotes);
        db.LoyaltyPointLedgers.RemoveRange(loyaltyLedgers);
        db.SalesInvoices.Remove(invoice);
        if (auditEntries.Count > 0)
        {
            db.AuditLogEntries.RemoveRange(auditEntries);
        }
        foreach (var digitalInvoice in digitalInvoices)
        {
            // Soft-delete rather than remove: keeps WhatsApp log / feedback / campaign
            // recipient history intact while stopping the CRM Digital Bills list from
            // still showing a live-looking link for an invoice that no longer exists.
            digitalInvoice.Deleted = true;
            digitalInvoice.IsActive = false;
            digitalInvoice.DisabledAt = DateTime.UtcNow;
            digitalInvoice.DisableReason = "Source sale invoice was hard-deleted";
            digitalInvoice.UpdatedAt = DateTime.UtcNow;
        }

        AddBillingAudit(
            db,
            context,
            "HardDeleted",
            invoice.Id,
            invoiceNumber,
            companyId,
            storeGroupId,
            storeId,
            string.IsNullOrWhiteSpace(reason) ? "Admin hard delete after cancellation/reversal" : reason.Trim(),
            before: new { invoice.Id, invoice.InvoiceNumber, invoice.BillAmount, invoice.PaidAmount, invoice.InvoiceStatus },
            after: response);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(response);
    }

    private static void AddBillingAudit(
        GarmetixDbContext db,
        HttpContext context,
        string action,
        Guid entityId,
        string invoiceNumber,
        Guid companyId,
        Guid storeGroupId,
        Guid storeId,
        string reason,
        object? before,
        object? after)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Action = action,
            Module = "Billing",
            EntityName = nameof(Invoice),
            EntityDisplayName = "Sales Invoice",
            EntityId = entityId,
            Reference = invoiceNumber,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId,
            UserId = Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) ? userId : null,
            UserName = context.User.Identity?.Name ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "System",
            Source = "BillingAdminHardDelete",
            RequestMethod = context.Request.Method,
            RequestPath = context.Request.Path.Value,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after),
            ChangesJson = JsonSerializer.Serialize(new[] { new { field = "HardDelete", before = "Cancelled invoice", after = "Removed with linked rows" } }),
            ChangedFieldCount = 1,
            TraceIdentifier = context.TraceIdentifier
        });
    }

    internal static Task<IResult> CancelSaleForReplacementApprovalAsync(
        Guid id,
        CancelInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
        => CancelSaleAsync(id, request, context, db, accounting, stockLedger, cancellationToken);

    private static Task<IResult> CancelSaleAsync(
        Guid id,
        CancelInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => CancelSaleCoreAsync(
            id,
            request,
            context,
            db,
            accounting,
            stockLedger,
            cancellationToken));
    }

    private static async Task<IResult> CancelSaleCoreAsync(
        Guid id,
        CancelInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
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
                MovementType = "SalesCancellationIn",
                QuantityIn = item.BilledQuantity,
                CostPrice = snapshot.AverageCost,
                MRP = item.MRP,
                TaxRate = item.TaxPercentage,
                HSNCode = item.HSNCode ?? stock.HSNCode,
                SourceType = "SalesInvoiceCancellation",
                SourceId = invoice.Id,
                SourceNumber = invoice.InvoiceNumber,
                Remarks = string.IsNullOrWhiteSpace(request.Reason) ? "Sales invoice cancellation" : request.Reason,
                OnDate = DateTime.Now
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

        await accounting.PostSalesInvoiceCancellationAsync(invoice, customer, storeGroupId, originalPaidAmount, originalPaymentMode, originalBankAccountId, ToAccountingPaymentPostings(originalPaymentRows), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new CancelInvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceStatus.ToString(),
            reversedQuantity,
            reversedAmount));
    }

    private static Task<IResult> CreateSalesReturnAsync(
        Guid id,
        SalesReturnRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => CreateSalesReturnCoreAsync(
            id,
            request,
            context,
            db,
            documentNumbers,
            accounting,
            stockLedger,
            cancellationToken));
    }

    private static async Task<IResult> CreateSalesReturnCoreAsync(
        Guid id,
        SalesReturnRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
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
            documentNumbers,
            accounting,
            stockLedger,
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

    private static Task<IResult> CreateSalesExchangeAsync(
        Guid id,
        SalesExchangeRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(() => CreateSalesExchangeCoreAsync(
            id,
            request,
            context,
            db,
            documentNumbers,
            accounting,
            stockLedger,
            cancellationToken));
    }

    private static async Task<IResult> CreateSalesExchangeCoreAsync(
        Guid id,
        SalesExchangeRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
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
            documentNumbers,
            accounting,
            stockLedger,
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
        var invoiceNumber = await documentNumbers.NextSalesExchangeAsync(original.CompanyId, storeGroupId, original.StoreId, cancellationToken);
        var exchangeSalesmanId = await ResolveExistingOrFallbackSalesmanIdAsync(
            original.SalemanId,
            original.CompanyId,
            original.StoreId,
            context,
            db,
            cancellationToken);
        if (exchangeSalesmanId is null)
        {
            return Results.BadRequest(new { message = "Original invoice salesman is missing and no active fallback salesman exists for this store." });
        }

        var exchangeItems = new List<InvoiceItem>();
        decimal grossMrp = 0;
        decimal itemDiscount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        decimal totalQuantity = 0;

        foreach (var requestItem in request.NewItems)
        {
            await DocumentNumberGenerator.LockStockKeyAsync(db, original.CompanyId, storeGroupId, original.StoreId, requestItem.ProductId, requestItem.Barcode, cancellationToken);

            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context)
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item =>
                    item.ProductId == requestItem.ProductId &&
                    item.Barcode == requestItem.Barcode &&
                    item.StoreId == original.StoreId &&
                    !item.IsOFB,
                    cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock not found for replacement barcode {requestItem.Barcode}." });
            }

            var stockSnapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            if (stockSnapshot.Quantity < requestItem.Quantity)
            {
                return Results.BadRequest(new { message = $"Insufficient replacement stock for {requestItem.Barcode}. Available: {stockSnapshot.Quantity}." });
            }

            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var taxable = Math.Round((lineMrp - lineDiscount) / (1 + (stock.TaxRate / 100)), 2);
            var tax = Math.Round(taxable * (stock.TaxRate / 100), 2);
            var lineAmount = taxable + tax;
            var split = SplitGst(tax, stock.TaxType, original.InterState);

            exchangeItems.Add(new InvoiceItem
            {
                InvoiceId = exchangeInvoiceId,
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
                TaxType = stock.TaxType,
                TaxId = stock.TaxId,
                BilledQuantity = requestItem.Quantity,
                CompanyId = original.CompanyId
            });

            stock.SoldValue += lineAmount;
            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "ExchangeSaleOut",
                QuantityOut = requestItem.Quantity,
                CostPrice = stockSnapshot.AverageCost,
                MRP = requestItem.Mrp,
                TaxRate = stock.TaxRate,
                HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
                SourceType = "SalesExchange",
                SourceId = exchangeInvoiceId,
                SourceNumber = invoiceNumber,
                Remarks = "Exchange replacement sale",
                OnDate = DateTime.Now,
                CompanyId = original.CompanyId,
                StoreGroupId = storeGroupId,
                StoreId = original.StoreId
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
            InvoiceStatus = (creditApplied + additionalPaid) >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = itemDiscount,
            TaxAmount = taxAmount,
            CGSTAmount = cgstAmount,
            SGSTAmount = sgstAmount,
            IGSTAmount = igstAmount,
            InterState = igstAmount > 0,
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
            SalemanId = exchangeSalesmanId.Value,
            CreditSale = additionalPaid < additionalDue,
            PaidAmount = creditApplied + additionalPaid,
            BillDiscountAmount = 0,
            StoreId = original.StoreId,
            CompanyId = original.CompanyId
        };

        var exchangePaymentDetails = new List<NormalizedInvoicePayment>();
        if (creditApplied > 0)
        {
            var reference = $"Exchange credit from {returnResult.ReturnInvoice!.InvoiceNumber}";
            exchangePaymentDetails.Add(new NormalizedInvoicePayment(
                PaymentMode.CreditBalance,
                creditApplied,
                null,
                reference,
                null,
                null,
                "SalesReturnCredit",
                returnResult.ReturnInvoice!.Id,
                JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["paymentMode"] = PaymentMode.CreditBalance.ToString(),
                    ["referenceNumber"] = reference,
                    ["adjustmentSourceType"] = "SalesReturnCredit",
                    ["adjustmentSourceId"] = returnResult.ReturnInvoice!.Id
                })));
        }

        if (additionalPaid > 0 && request.AdditionalPaymentMode.HasValue)
        {
            var reference = $"Exchange additional payment for {invoiceNumber}";
            exchangePaymentDetails.Add(new NormalizedInvoicePayment(
                request.AdditionalPaymentMode.Value,
                additionalPaid,
                request.BankAccountId,
                reference,
                null,
                null,
                null,
                null,
                JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["paymentMode"] = request.AdditionalPaymentMode.Value.ToString(),
                    ["bankAccountId"] = request.BankAccountId,
                    ["referenceNumber"] = reference
                })));
        }

        db.SalesInvoices.Add(exchangeInvoice);
        db.InvoiceItems.AddRange(exchangeItems);
        AddInvoicePayments(exchangeInvoice, exchangePaymentDetails, db);

        await accounting.PostSalesInvoiceAsync(exchangeInvoice, customer, storeGroupId, ToAccountingPaymentPostings(exchangePaymentDetails), cancellationToken);
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
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
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
        var returnSalesmanId = await ResolveExistingOrFallbackSalesmanIdAsync(
            original.SalemanId,
            original.CompanyId,
            original.StoreId,
            context,
            db,
            cancellationToken);
        if (returnSalesmanId is null)
        {
            return SalesReturnCoreResult.BadRequest("Original invoice salesman is missing and no active fallback salesman exists for this store.");
        }

        var returnItems = new List<InvoiceItem>();
        decimal grossMrp = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal discountAmount = 0;
        decimal creditAmount = 0;
        decimal reversedQuantity = 0;
        var creditNoteNumber = await documentNumbers.NextSalesReturnAsync(original.CompanyId, storeGroupId, original.StoreId, cancellationToken);

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
                ProductName = originalItem.ProductName,
                HSNCode = originalItem.HSNCode,
                Unit = originalItem.Unit,
                ProductCategoryId = originalItem.ProductCategoryId,
                ProductSubCategoryId = originalItem.ProductSubCategoryId,
                MRP = originalItem.MRP,
                DiscountAmount = originalItem.DiscountAmount,
                BasePrice = lineBase,
                TaxPercentage = originalItem.TaxPercentage,
                TaxAmount = lineTax,
                CGSTAmount = Math.Round((originalItem.CGSTAmount ?? 0) * ratio, 2),
                SGSTAmount = Math.Round((originalItem.SGSTAmount ?? 0) * ratio, 2),
                IGSTAmount = Math.Round((originalItem.IGSTAmount ?? 0) * ratio, 2),
                Amount = lineAmount,
                TaxType = originalItem.TaxType,
                TaxId = originalItem.TaxId,
                BilledQuantity = requestItem.Quantity,
                CompanyId = original.CompanyId
            });

            await DocumentNumberGenerator.LockStockKeyAsync(db, original.CompanyId, storeGroupId, original.StoreId, originalItem.ProductId, originalItem.Barcode, cancellationToken);

            var stock = await db.Stocks.FirstOrDefaultAsync(stockItem =>
                stockItem.ProductId == originalItem.ProductId &&
                stockItem.Barcode == originalItem.Barcode &&
                stockItem.StoreId == original.StoreId &&
                !stockItem.IsOFB,
                cancellationToken);
            if (stock is not null)
            {
                stock.SoldValue = Math.Max(0, stock.SoldValue - lineAmount);
                var stockSnapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
                await stockLedger.PostAsync(stock, new StockMovement
                {
                    Barcode = stock.Barcode,
                    MovementType = "SalesReturnIn",
                    QuantityIn = requestItem.Quantity,
                    CostPrice = stockSnapshot.AverageCost,
                    MRP = originalItem.MRP,
                    TaxRate = originalItem.TaxPercentage,
                    HSNCode = originalItem.HSNCode ?? stock.HSNCode,
                    SourceType = "SalesReturn",
                    SourceId = returnInvoiceId,
                    SourceNumber = creditNoteNumber,
                    Remarks = string.IsNullOrWhiteSpace(reason) ? "Sales return" : reason,
                    OnDate = DateTime.Now,
                    CompanyId = original.CompanyId,
                    StoreGroupId = storeGroupId,
                    StoreId = original.StoreId
                }, cancellationToken);
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
            CGSTAmount = returnItems.Sum(item => item.CGSTAmount ?? 0),
            SGSTAmount = returnItems.Sum(item => item.SGSTAmount ?? 0),
            IGSTAmount = returnItems.Sum(item => item.IGSTAmount ?? 0),
            InterState = returnItems.Sum(item => item.IGSTAmount ?? 0) > 0,
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
            SalemanId = returnSalesmanId.Value,
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
                ReferenceNumber = string.IsNullOrWhiteSpace(reason) ? "Sales return refund" : reason,
                BankAccountId = bankAccountId,
                AdjustmentSourceType = "SalesReturn",
                AdjustmentSourceId = returnInvoice.Id
            });
        }

        customer.Amount = Math.Max(0, customer.Amount - billAmount);
        customer.CreditBalance += storeCredit;
        var originalQuantity = originalItems.Sum(item => item.BilledQuantity);
        await LoyaltyService.ReverseSalePointsAsync(original, customer, originalQuantity <= 0 ? 0 : reversedQuantity / originalQuantity, db, cancellationToken);
        await CommercialEndpoints.CreateCreditNoteFromSalesReturnAsync(returnInvoice, customer, reason, db, cancellationToken);
        await UpdateOriginalReturnStatusAsync(original, originalItems, reversedQuantity, db, cancellationToken);
        await accounting.PostSalesReturnAsync(returnInvoice, customer, storeGroupId, refund, refundPaymentMode, bankAccountId, cancellationToken);

        return SalesReturnCoreResult.Ok(original, returnInvoice, customer, storeGroupId, billAmount, refund, storeCredit, reversedQuantity);
    }

    private static async Task<Guid?> ResolveRequiredSalesmanIdAsync(
        Guid? requestedSalesmanId,
        Guid companyId,
        Guid storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (requestedSalesmanId.HasValue && requestedSalesmanId.Value != Guid.Empty)
        {
            var salesmanAllowed = await WorkspaceScope.ApplyTo(db.Salesmen.AsNoTracking(), context)
                .AnyAsync(item => item.Id == requestedSalesmanId.Value && item.Active && item.CompanyId == companyId && item.StoreId == storeId, cancellationToken);
            return salesmanAllowed ? requestedSalesmanId.Value : null;
        }

        return await GetDefaultSalesmanIdAsync(companyId, storeId, context, db, cancellationToken);
    }

    private static async Task<Guid?> ResolveExistingOrFallbackSalesmanIdAsync(
        Guid salesmanId,
        Guid companyId,
        Guid storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (salesmanId != Guid.Empty)
        {
            var existingSalesman = await db.Salesmen
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(item => item.Id == salesmanId && item.CompanyId == companyId && item.StoreId == storeId, cancellationToken);
            if (existingSalesman)
            {
                return salesmanId;
            }
        }

        return await GetDefaultSalesmanIdAsync(companyId, storeId, context, db, cancellationToken);
    }

    private static async Task<Guid?> GetDefaultSalesmanIdAsync(
        Guid companyId,
        Guid storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var managerId = await WorkspaceScope.ApplyTo(db.Salesmen.AsNoTracking(), context)
            .Where(item => item.Active && item.CompanyId == companyId && item.StoreId == storeId && item.Name == "Manager")
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (managerId.HasValue)
        {
            return managerId;
        }

        var activeSalesmanId = await WorkspaceScope.ApplyTo(db.Salesmen.AsNoTracking(), context)
            .Where(item => item.Active && item.CompanyId == companyId && item.StoreId == storeId)
            .OrderBy(item => item.Name)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeSalesmanId.HasValue)
        {
            return activeSalesmanId;
        }

        var storeScope = await db.Stores.AsNoTracking()
            .Where(item => item.Id == storeId && item.CompanyId == companyId && !item.Deleted)
            .Select(item => new { item.StoreGroupId })
            .FirstOrDefaultAsync(cancellationToken);
        if (storeScope is null)
        {
            return null;
        }

        var manager = await db.Salesmen
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.StoreId == storeId && item.Name == "Manager", cancellationToken);
        if (manager is null)
        {
            manager = new Salesman
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                StoreGroupId = storeScope.StoreGroupId,
                StoreId = storeId,
                Name = "Manager",
                Active = true,
                Deleted = false
            };
            db.Salesmen.Add(manager);
        }
        else
        {
            manager.CompanyId = companyId;
            manager.StoreGroupId = storeScope.StoreGroupId;
            manager.StoreId = storeId;
            manager.Active = true;
            manager.Deleted = false;
            manager.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return manager.Id;
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
        var customer = request.CustomerId.HasValue
            ? await db.Customers.FirstOrDefaultAsync(item => item.CompanyId == request.CompanyId && item.Id == request.CustomerId.Value, cancellationToken)
            : await db.Customers.FirstOrDefaultAsync(
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



    private sealed record NormalizedInvoicePayment(
        PaymentMode PaymentMode,
        decimal Amount,
        Guid? BankAccountId,
        string? ReferenceNumber,
        string? GatewayReference,
        string? SettlementStatus,
        string? AdjustmentSourceType,
        Guid? AdjustmentSourceId,
        string? PaymentDetailsJson);

    private static List<NormalizedInvoicePayment> NormalizeInvoicePayments(PosSaleRequest request, decimal billAmount)
    {
        IEnumerable<InvoicePaymentDetailRequest> sourcePayments = request.Payments is { Count: > 0 }
            ? request.Payments
            : request.PaidAmount > 0
                ? new[] { new InvoicePaymentDetailRequest(request.PaymentMode, request.PaidAmount, request.BankAccountId, null, null, null, null, null) }
                : Array.Empty<InvoicePaymentDetailRequest>();

        var payments = new List<NormalizedInvoicePayment>();
        foreach (var payment in sourcePayments.Where(item => item.Amount > 0))
        {
            var amount = Math.Round(payment.Amount, 2);
            payments.Add(new NormalizedInvoicePayment(
                payment.PaymentMode,
                amount,
                payment.BankAccountId,
                Clean(payment.ReferenceNumber),
                Clean(payment.GatewayReference),
                Clean(payment.SettlementStatus),
                Clean(payment.AdjustmentSourceType),
                payment.AdjustmentSourceId,
                BuildPaymentDetailsJson(payment)));
        }

        return payments;
    }

    private static async Task<string?> ApplyInvoicePaymentAdjustmentsAsync(
        Invoice invoice,
        Customer customer,
        IReadOnlyList<NormalizedInvoicePayment> payments,
        Guid storeGroupId,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var duplicateAdjustmentError = ValidateSingleUseAdjustmentRows(payments);
        if (!string.IsNullOrWhiteSpace(duplicateAdjustmentError))
        {
            return duplicateAdjustmentError;
        }

        foreach (var payment in payments.Where(IsAdjustmentPayment))
        {
            var sourceType = CanonicalAdjustmentSource(payment);
            var amount = payment.Amount;
            if (amount <= 0)
            {
                continue;
            }

            if (SourceMatches(sourceType, "CustomerAdvanceReceipt"))
            {
                if (!payment.AdjustmentSourceId.HasValue)
                {
                    return "Select an advance receipt before applying advance payment.";
                }

                var receipt = await db.CustomerAdvanceReceipts.FirstOrDefaultAsync(item =>
                    item.Id == payment.AdjustmentSourceId.Value &&
                    item.CompanyId == invoice.CompanyId &&
                    item.CustomerId == customer.Id &&
                    item.StoreId == invoice.StoreId,
                    cancellationToken);
                if (receipt is null)
                {
                    return "Selected customer advance receipt was not found.";
                }

                if (receipt.AvailableAmount < amount)
                {
                    return $"Advance receipt {receipt.ReceiptNumber} has only {receipt.AvailableAmount:N2} available.";
                }

                receipt.AdjustedAmount += amount;
                receipt.AvailableAmount = Math.Max(0, receipt.AvailableAmount - amount);
                customer.CreditBalance = Math.Max(0, customer.CreditBalance - amount);
                continue;
            }

            if (SourceMatches(sourceType, "CustomerCreditBalance") || SourceMatches(sourceType, "StoreCredit") || SourceMatches(sourceType, "SalesReturnCredit") || payment.PaymentMode == PaymentMode.CreditBalance)
            {
                if (customer.CreditBalance < amount)
                {
                    return $"Customer credit balance is only {customer.CreditBalance:N2}.";
                }

                customer.CreditBalance -= amount;
                continue;
            }

            if (SourceMatches(sourceType, "CreditNote"))
            {
                if (!payment.AdjustmentSourceId.HasValue)
                {
                    return "Select a credit note before applying credit note payment.";
                }

                var note = await db.CommercialNotes.FirstOrDefaultAsync(item =>
                    item.Id == payment.AdjustmentSourceId.Value &&
                    item.CompanyId == invoice.CompanyId &&
                    item.CustomerId == customer.Id &&
                    item.StoreId == invoice.StoreId &&
                    item.NoteType == NoteType.CreditNote,
                    cancellationToken);
                if (note is null)
                {
                    return "Selected credit note was not found.";
                }

                var available = Math.Max(0, note.Amount - note.AdjustedAmount);
                if (available < amount)
                {
                    return $"Credit note {note.NoteNumber} has only {available:N2} available.";
                }

                note.AdjustedAmount += amount;
                note.IsAdjusted = note.AdjustedAmount >= note.Amount;
                customer.CreditBalance = Math.Max(0, customer.CreditBalance - amount);
                continue;
            }

            if (SourceMatches(sourceType, "LoyaltyRedemption"))
            {
                var program = await db.LoyaltyPrograms
                    .Where(item => item.CompanyId == invoice.CompanyId && item.StoreId == invoice.StoreId && item.Enabled)
                    .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (program is null || program.RedeemValuePerPoint <= 0)
                {
                    return "Loyalty redemption is not enabled for this store.";
                }

                var pointsToRedeem = Math.Round(amount / program.RedeemValuePerPoint, 2);
                if (pointsToRedeem <= 0)
                {
                    continue;
                }

                if (customer.LoyaltyPoints < pointsToRedeem)
                {
                    return $"Customer has only {customer.LoyaltyPoints:N2} loyalty points.";
                }

                customer.LoyaltyPoints -= pointsToRedeem;
                db.LoyaltyPointLedgers.Add(new LoyaltyPointLedger
                {
                    CustomerId = customer.Id,
                    CustomerName = customer.Name,
                    OnDate = DateTime.Now,
                    SourceType = "SaleInvoiceRedemption",
                    SourceId = invoice.Id,
                    SourceNumber = invoice.InvoiceNumber,
                    PointsIn = 0,
                    PointsOut = pointsToRedeem,
                    BalanceAfter = customer.LoyaltyPoints,
                    Remarks = "POS invoice loyalty redemption",
                    CompanyId = invoice.CompanyId,
                    StoreGroupId = storeGroupId,
                    StoreId = invoice.StoreId
                });
                continue;
            }
        }

        return null;
    }

    private static void AddInvoicePayments(Invoice invoice, IReadOnlyList<NormalizedInvoicePayment> payments, GarmetixDbContext db)
    {
        foreach (var payment in payments.Where(item => item.Amount > 0))
        {
            db.InvoicePayments.Add(new InvoicePayment
            {
                InvoiceId = invoice.Id,
                OnDate = DateTime.Now,
                Amount = payment.Amount,
                PaymentMode = payment.PaymentMode,
                ReferenceNumber = payment.ReferenceNumber,
                BankAccountId = payment.BankAccountId,
                GatewayReference = payment.GatewayReference,
                SettlementStatus = payment.SettlementStatus,
                PaymentDetailsJson = payment.PaymentDetailsJson,
                AdjustmentSourceType = payment.AdjustmentSourceType,
                AdjustmentSourceId = payment.AdjustmentSourceId,
                StoreId = invoice.StoreId,
                CompanyId = invoice.CompanyId
            });
        }
    }

    private static bool IsAdjustmentPayment(NormalizedInvoicePayment payment)
    {
        return !string.IsNullOrWhiteSpace(payment.AdjustmentSourceType) ||
            payment.PaymentMode is PaymentMode.CreditBalance or PaymentMode.CreditNote or PaymentMode.SaleReturn or PaymentMode.Coupons;
    }

    private static bool SourceMatches(string? sourceType, string expected)
    {
        return string.Equals(sourceType, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresBankAccount(PaymentMode paymentMode)
    {
        return paymentMode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;
    }

    private static IReadOnlyList<SalesInvoicePaymentPosting> ToAccountingPaymentPostings(IReadOnlyList<NormalizedInvoicePayment> payments)
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

    private static string? ValidateSingleUseAdjustmentRows(IReadOnlyList<NormalizedInvoicePayment> payments)
    {
        var seen = new Dictionary<string, NormalizedInvoicePayment>(StringComparer.OrdinalIgnoreCase);
        foreach (var payment in payments.Where(IsAdjustmentPayment))
        {
            var sourceType = CanonicalAdjustmentSource(payment);
            if (!AdjustmentSourceRequiresSingleUse(sourceType))
            {
                continue;
            }

            var sourceId = payment.AdjustmentSourceId?.ToString("N") ?? "customer-balance";
            var key = $"{sourceType}:{sourceId}";
            if (seen.ContainsKey(key))
            {
                return $"Do not apply the same {FriendlyAdjustmentSource(sourceType)} more than once in one invoice.";
            }

            seen[key] = payment;
        }

        return null;
    }

    private static bool AdjustmentSourceRequiresSingleUse(string sourceType)
    {
        return SourceMatches(sourceType, "CustomerAdvanceReceipt") ||
            SourceMatches(sourceType, "CreditNote") ||
            SourceMatches(sourceType, "CustomerCreditBalance") ||
            SourceMatches(sourceType, "StoreCredit") ||
            SourceMatches(sourceType, "SalesReturnCredit") ||
            SourceMatches(sourceType, "LoyaltyRedemption");
    }

    private static string CanonicalAdjustmentSource(NormalizedInvoicePayment payment)
    {
        if (!string.IsNullOrWhiteSpace(payment.AdjustmentSourceType))
        {
            return payment.AdjustmentSourceType.Trim();
        }

        return payment.PaymentMode switch
        {
            PaymentMode.CreditNote => "CreditNote",
            PaymentMode.CreditBalance => "CustomerCreditBalance",
            PaymentMode.SaleReturn => "SalesReturnCredit",
            PaymentMode.Coupons => "LoyaltyRedemption",
            _ => payment.PaymentMode.ToString()
        };
    }

    private static string FriendlyAdjustmentSource(string sourceType)
    {
        if (SourceMatches(sourceType, "CustomerAdvanceReceipt")) return "advance receipt";
        if (SourceMatches(sourceType, "CreditNote")) return "credit note";
        if (SourceMatches(sourceType, "LoyaltyRedemption")) return "loyalty redemption";
        return "customer credit/store credit";
    }

    private static string? BuildPaymentDetailsJson(InvoicePaymentDetailRequest payment)
    {
        var details = new Dictionary<string, object?>();
        AddDetail(details, "paymentMode", payment.PaymentMode.ToString());
        AddDetail(details, "bankAccountId", payment.BankAccountId);
        AddDetail(details, "referenceNumber", Clean(payment.ReferenceNumber));
        AddDetail(details, "gatewayReference", Clean(payment.GatewayReference));
        AddDetail(details, "settlementStatus", Clean(payment.SettlementStatus));
        AddDetail(details, "adjustmentSourceType", Clean(payment.AdjustmentSourceType));
        AddDetail(details, "adjustmentSourceId", payment.AdjustmentSourceId);
        AddDetail(details, "cardLastFour", Clean(payment.CardLastFour));
        AddDetail(details, "cardAuthorizationCode", Clean(payment.CardAuthorizationCode));
        AddDetail(details, "cardNetwork", Clean(payment.CardNetwork));
        AddDetail(details, "upiVpa", Clean(payment.UpiVpa));
        AddDetail(details, "walletProvider", Clean(payment.WalletProvider));
        AddDetail(details, "bankReferenceNumber", Clean(payment.BankReferenceNumber));
        AddDetail(details, "chequeNumber", Clean(payment.ChequeNumber));
        AddDetail(details, "chequeDate", payment.ChequeDate?.ToString("O"));
        AddDetail(details, "drawerBankName", Clean(payment.DrawerBankName));
        AddDetail(details, "accountReference", Clean(payment.AccountReference));

        return details.Count == 0 ? null : JsonSerializer.Serialize(details);
    }

    private static void AddDetail(IDictionary<string, object?> details, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        details[key] = value;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }


    private static (decimal TaxableAmount, decimal TaxAmount, decimal CgstAmount, decimal SgstAmount, decimal IgstAmount) ApplyBillDiscountToInvoiceItems(
        IReadOnlyList<InvoiceItem> items,
        decimal billDiscountAmount,
        bool interState)
    {
        if (items.Count == 0)
        {
            return (0, 0, 0, 0, 0);
        }

        billDiscountAmount = Math.Round(Math.Max(0, billDiscountAmount), 2);
        var totalInclusiveBeforeBillDiscount = items.Sum(item => item.Amount);
        if (billDiscountAmount <= 0 || totalInclusiveBeforeBillDiscount <= 0)
        {
            return (
                items.Sum(item => item.BasePrice),
                items.Sum(item => item.TaxAmount),
                items.Sum(item => item.CGSTAmount ?? 0),
                items.Sum(item => item.SGSTAmount ?? 0),
                items.Sum(item => item.IGSTAmount ?? 0));
        }

        decimal allocatedSoFar = 0;
        decimal taxableTotal = 0;
        decimal taxTotal = 0;
        decimal cgstTotal = 0;
        decimal sgstTotal = 0;
        decimal igstTotal = 0;

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var allocatedDiscount = index == items.Count - 1
                ? billDiscountAmount - allocatedSoFar
                : Math.Round(billDiscountAmount * item.Amount / totalInclusiveBeforeBillDiscount, 2);

            allocatedDiscount = Math.Min(Math.Max(0, allocatedDiscount), item.Amount);
            allocatedSoFar += allocatedDiscount;

            var adjustedInclusive = Math.Max(0, item.Amount - allocatedDiscount);
            var taxable = Math.Round(adjustedInclusive / (1 + (item.TaxPercentage / 100)), 2);
            var tax = Math.Round(adjustedInclusive - taxable, 2);
            var split = SplitGst(tax, item.TaxType, interState);
            var quantity = item.BilledQuantity <= 0 ? 1 : item.BilledQuantity;

            item.DiscountAmount = Math.Round(item.DiscountAmount + (allocatedDiscount / quantity), 2);
            item.BasePrice = taxable;
            item.TaxAmount = tax;
            item.CGSTAmount = split.Cgst;
            item.SGSTAmount = split.Sgst;
            item.IGSTAmount = split.Igst;
            item.Amount = taxable + tax;

            taxableTotal += taxable;
            taxTotal += tax;
            cgstTotal += split.Cgst;
            sgstTotal += split.Sgst;
            igstTotal += split.Igst;
        }

        return (taxableTotal, taxTotal, cgstTotal, sgstTotal, igstTotal);
    }

    private static bool IsInterStateSupply(string? supplierGstin, string? customerGstin)
    {
        var supplierState = GstStateCode(supplierGstin);
        var customerState = GstStateCode(customerGstin);
        return supplierState is not null
            && customerState is not null
            && !string.Equals(supplierState, customerState, StringComparison.Ordinal);
    }

    private static string? GstStateCode(string? gstin)
    {
        var normalized = Regex.Replace(gstin ?? string.Empty, @"\s+", string.Empty).ToUpperInvariant();
        return normalized.Length == 15 && normalized[0..2].All(char.IsDigit)
            ? normalized[0..2]
            : null;
    }

    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitGst(decimal totalTax, TaxType taxType, bool interState = false)
    {
        totalTax = Math.Round(totalTax, 2);
        if (interState && taxType is TaxType.GST or TaxType.CGST or TaxType.SGST or TaxType.IGST)
        {
            return (0, 0, totalTax);
        }

        return taxType switch
        {
            TaxType.IGST => (0, 0, totalTax),
            TaxType.CGST => (totalTax, 0, 0),
            TaxType.SGST => (0, totalTax, 0),
            TaxType.GST => (Math.Round(totalTax / 2m, 2), totalTax - Math.Round(totalTax / 2m, 2), 0),
            _ => (0, 0, 0)
        };
    }
}

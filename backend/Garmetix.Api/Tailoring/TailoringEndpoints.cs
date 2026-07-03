using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Garmetix.Api.Tailoring;

public static class TailoringEndpoints
{
    private const decimal ServiceGstRate = 5m;
    public static RouteGroupBuilder MapTailoringEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tailoring")
            .WithTags("Tailoring")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("/dashboard", DashboardAsync);
        group.MapGet("/schedule", ScheduleAsync);
        group.MapGet("/service-items", ListServiceItemsAsync);
        group.MapPost("/service-items", SaveServiceItemAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/service-items/{id:guid}", UpdateServiceItemAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/service-items/{id:guid}", DeleteServiceItemAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapGet("/vendors", TailoringVendorsAsync);
        group.MapPost("/vendors", SaveTailoringVendorAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/vendor-rates", ListVendorRatesAsync);
        group.MapPost("/vendor-rates", SaveVendorRateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/vendor-rates/{id:guid}", UpdateVendorRateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/vendor-rates/{id:guid}", DeleteVendorRateAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapGet("/customers/by-mobile", CustomerByMobileAsync);
        group.MapGet("/alteration/source-invoices", AlterationSourceInvoicesAsync);
        group.MapGet("/alteration/source-invoices/{invoiceId:guid}/items", AlterationSourceInvoiceItemsAsync);
        group.MapGet("/deliveries/overview", DeliveryOverviewAsync);
        group.MapGet("/orders", ListOrdersAsync);
        group.MapGet("/orders/{id:guid}", GetOrderAsync);
        group.MapPost("/orders", CreateOrderAsync);
        group.MapPost("/orders/{id:guid}/status", UpdateStatusAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/orders/{id:guid}/deliver", DeliverOrderAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/orders/{id:guid}/print-order", PrintOrderAsync);
        group.MapGet("/orders/{id:guid}/print-invoice", PrintInvoiceAsync);
        group.MapPost("/orders/{id:guid}/receive-payment", ReceiveCustomerPaymentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/orders/{id:guid}/pay-vendor", PayVendorAsync).RequireAuthorization(GarmetixPolicies.Accounting);
        group.MapPost("/orders/{id:guid}/convert-to-service-invoice", ConvertToServiceInvoiceAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/orders/{id:guid}/cancel", CancelOrderAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IReadOnlyList<TailoringOrderSummaryDto>> ListOrdersAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? storeId = null,
        TailoringOrderStatus? status = null,
        TailoringOrderType? orderType = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        if (status.HasValue) query = query.Where(item => item.Status == status.Value);
        if (orderType.HasValue) query = query.Where(item => item.OrderType == orderType.Value);
        if (from.HasValue) query = query.Where(item => item.OnDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(item => item.OnDate < to.Value.Date.AddDays(1));

        var orders = await query
            .OrderBy(item => item.ExpectedDeliveryDate ?? DateTime.MaxValue)
            .ThenByDescending(item => item.OnDate)
            .Take(250)
            .ToListAsync(cancellationToken);
        return orders.Select(ToSummary).ToList();
    }

    private static async Task<IResult> GetOrderAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null)
        {
            return Results.NotFound();
        }

        var lines = await db.TailoringOrderLines.AsNoTracking()
            .Where(line => line.TailoringOrderId == order.Id)
            .OrderBy(line => line.CreatedAt)
            .ToListAsync(cancellationToken);
        var receipts = await db.TailoringCustomerReceipts.AsNoTracking()
            .Where(receipt => receipt.TailoringOrderId == order.Id)
            .OrderBy(receipt => receipt.OnDate)
            .ToListAsync(cancellationToken);
        var vendorPayments = await db.TailoringVendorPayments.AsNoTracking()
            .Where(payment => payment.TailoringOrderId == order.Id)
            .OrderBy(payment => payment.OnDate)
            .ToListAsync(cancellationToken);
        var history = await db.TailoringOrderHistories.AsNoTracking()
            .Where(history => history.TailoringOrderId == order.Id)
            .OrderByDescending(history => history.EventDate)
            .ToListAsync(cancellationToken);

        return Results.Ok(new { order, lines, receipts, vendorPayments, history });
    }

    private static async Task<TailoringDashboardDto> DashboardAsync(HttpContext context, GarmetixDbContext db, Guid? storeId = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var query = WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context).Where(item => item.Status != TailoringOrderStatus.Cancelled);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);

        var orders = await query.ToListAsync(cancellationToken);
        var pendingStatuses = new[] { TailoringOrderStatus.Ordered, TailoringOrderStatus.SentToVendor, TailoringOrderStatus.InProgress };
        var upcoming = orders
            .Where(item => item.ExpectedDeliveryDate.HasValue && item.DeliveredAt == null && item.ExpectedDeliveryDate.Value.Date >= today)
            .OrderBy(item => item.ExpectedDeliveryDate)
            .Take(20)
            .Select(ToSummary)
            .ToList();
        var overdue = orders
            .Where(item => item.ExpectedDeliveryDate.HasValue && item.DeliveredAt == null && item.ExpectedDeliveryDate.Value.Date < today)
            .OrderBy(item => item.ExpectedDeliveryDate)
            .Take(20)
            .Select(ToSummary)
            .ToList();

        return new TailoringDashboardDto(
            orders.Count(item => pendingStatuses.Contains(item.Status)),
            orders.Count(item => item.ExpectedDeliveryDate.HasValue && item.ExpectedDeliveryDate.Value.Date == today && item.DeliveredAt == null),
            overdue.Count,
            orders.Count(item => item.Status == TailoringOrderStatus.ReadyForDelivery),
            orders.Count(item => item.DeliveredAt.HasValue && item.ServiceInvoiceId == null),
            orders.Sum(item => item.CustomerBalanceAmount),
            orders.Sum(item => item.VendorBalanceAmount),
            orders.Sum(item => item.InHouseExpenseAmount),
            upcoming,
            overdue);
    }

    private static async Task<IReadOnlyList<TailoringOrderSummaryDto>> ScheduleAsync(HttpContext context, GarmetixDbContext db, DateTime? from = null, DateTime? to = null, Guid? storeId = null, CancellationToken cancellationToken = default)
    {
        var start = (from ?? DateTime.Today.AddDays(-7)).Date;
        var end = (to ?? DateTime.Today.AddDays(14)).Date.AddDays(1);
        var query = WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context)
            .Where(item => item.ExpectedDeliveryDate >= start && item.ExpectedDeliveryDate < end && item.Status != TailoringOrderStatus.Cancelled);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        var orders = await query.OrderBy(item => item.ExpectedDeliveryDate).ToListAsync(cancellationToken);
        return orders.Select(ToSummary).ToList();
    }

    private static async Task<IReadOnlyList<TailoringServiceItem>> ListServiceItemsAsync(HttpContext context, GarmetixDbContext db, Guid? storeId = null, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = WorkspaceScope.ApplyTo(db.TailoringServiceItems.AsNoTracking(), context);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        if (activeOnly) query = query.Where(item => item.Active);
        return await query.OrderBy(item => item.Category).ThenBy(item => item.Name).ToListAsync(cancellationToken);
    }

    private static async Task<IResult> SaveServiceItemAsync(TailoringServiceItemRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = new TailoringServiceItem();
        ApplyServiceItemRequest(item, request);
        if (!WorkspaceScope.CanWrite(item, context, out var message)) return Results.BadRequest(new { message });
        db.TailoringServiceItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tailoring/service-items/{item.Id}", item);
    }

    private static async Task<IResult> UpdateServiceItemAsync(Guid id, TailoringServiceItemRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.TailoringServiceItems, context).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();
        ApplyServiceItemRequest(item, request);
        if (!WorkspaceScope.CanWrite(item, context, out var message)) return Results.BadRequest(new { message });
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static async Task<IResult> DeleteServiceItemAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.TailoringServiceItems, context).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return Results.NotFound();
        item.Deleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<TailoringVendorDto>> TailoringVendorsAsync(HttpContext context, GarmetixDbContext db, Guid? companyId = null, CancellationToken cancellationToken = default)
    {
        // The current Vendor master does not yet have a VendorType column.
        // Until a dedicated tailoring-vendor flag is added to the master data screen,
        // expose active vendors for tailoring/alteration assignment and label them safely.
        var query = WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
            .Where(item => item.Active && !item.Deleted);
        if (companyId.HasValue) query = query.Where(item => item.CompanyId == companyId.Value);
        return await query.OrderBy(item => item.Name)
            .Select(item => new TailoringVendorDto(item.Id, item.Name, item.MobileNumber, "Vendor", item.Active, item.PartyId))
            .ToListAsync(cancellationToken);
    }


    private static async Task<IResult> SaveTailoringVendorAsync(TailoringVendorRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Vendor name is required." });
        if (string.IsNullOrWhiteSpace(request.MobileNumber)) return Results.BadRequest(new { message = "Vendor mobile number is required." });

        var vendor = new Vendor
        {
            CompanyId = request.CompanyId,
            Name = request.Name.Trim(),
            MobileNumber = request.MobileNumber.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? "Tailoring vendor" : request.Address.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? "Local" : request.City.Trim(),
            Email = request.Email?.Trim(),
            GSTIN = request.GSTIN?.Trim(),
            Active = request.Active
        };
        if (!WorkspaceScope.CanWrite(vendor, context, out var message)) return Results.BadRequest(new { message });
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/tailoring/vendors/{vendor.Id}", new TailoringVendorDto(vendor.Id, vendor.Name, vendor.MobileNumber, "Tailoring / Alteration", vendor.Active, vendor.PartyId));
    }

    private static async Task<IReadOnlyList<TailoringVendorRateDto>> ListVendorRatesAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? vendorId = null,
        Guid? serviceItemId = null,
        Guid? storeId = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = WorkspaceScope.ApplyTo(db.TailoringVendorServiceRates.AsNoTracking(), context);
        if (vendorId.HasValue) query = query.Where(item => item.VendorId == vendorId.Value);
        if (serviceItemId.HasValue) query = query.Where(item => item.ServiceItemId == serviceItemId.Value);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        if (activeOnly) query = query.Where(item => item.Active);

        var rates = await query.OrderBy(item => item.VendorId).ThenBy(item => item.ServiceItemId).ThenByDescending(item => item.EffectiveFrom).ToListAsync(cancellationToken);
        return await BuildVendorRateDtosAsync(db, rates, cancellationToken);
    }

    private static async Task<IResult> SaveVendorRateAsync(TailoringVendorRateRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var validation = await ValidateVendorRateRequestAsync(request, context, db, cancellationToken);
        if (validation is not null) return validation;

        var existing = await WorkspaceScope.ApplyTo(db.TailoringVendorServiceRates, context)
            .FirstOrDefaultAsync(item => item.StoreId == request.StoreId && item.VendorId == request.VendorId && item.ServiceItemId == request.ServiceItemId && !item.Deleted, cancellationToken);
        var rate = existing ?? new TailoringVendorServiceRate();
        ApplyVendorRateRequest(rate, request);
        if (!WorkspaceScope.CanWrite(rate, context, out var message)) return Results.BadRequest(new { message });
        if (existing is null) db.TailoringVendorServiceRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
        var dto = (await BuildVendorRateDtosAsync(db, new[] { rate }, cancellationToken)).First();
        return existing is null ? Results.Created($"/api/tailoring/vendor-rates/{rate.Id}", dto) : Results.Ok(dto);
    }

    private static async Task<IResult> UpdateVendorRateAsync(Guid id, TailoringVendorRateRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var rate = await WorkspaceScope.ApplyTo(db.TailoringVendorServiceRates, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rate is null) return Results.NotFound();
        var validation = await ValidateVendorRateRequestAsync(request, context, db, cancellationToken);
        if (validation is not null) return validation;
        ApplyVendorRateRequest(rate, request);
        if (!WorkspaceScope.CanWrite(rate, context, out var message)) return Results.BadRequest(new { message });
        await db.SaveChangesAsync(cancellationToken);
        var dto = (await BuildVendorRateDtosAsync(db, new[] { rate }, cancellationToken)).First();
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteVendorRateAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var rate = await WorkspaceScope.ApplyTo(db.TailoringVendorServiceRates, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rate is null) return Results.NotFound();
        rate.Deleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult?> ValidateVendorRateRequestAsync(TailoringVendorRateRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (request.VendorId == Guid.Empty) return Results.BadRequest(new { message = "Select a tailoring/alteration vendor." });
        if (request.ServiceItemId == Guid.Empty) return Results.BadRequest(new { message = "Select a service item." });
        if (request.VendorRate < 0 || request.CustomerRate < 0) return Results.BadRequest(new { message = "Rates cannot be negative." });
        var vendorExists = await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context).AnyAsync(item => item.Id == request.VendorId && item.Active && !item.Deleted, cancellationToken);
        if (!vendorExists) return Results.BadRequest(new { message = "Selected vendor is not active or is outside your workspace." });
        var serviceExists = await WorkspaceScope.ApplyTo(db.TailoringServiceItems.AsNoTracking(), context).AnyAsync(item => item.Id == request.ServiceItemId && item.Active && !item.Deleted, cancellationToken);
        if (!serviceExists) return Results.BadRequest(new { message = "Selected service item is not active or is outside your workspace." });
        return null;
    }

    private static void ApplyVendorRateRequest(TailoringVendorServiceRate rate, TailoringVendorRateRequest request)
    {
        rate.CompanyId = request.CompanyId;
        rate.StoreGroupId = request.StoreGroupId;
        rate.StoreId = request.StoreId;
        rate.VendorId = request.VendorId;
        rate.ServiceItemId = request.ServiceItemId;
        rate.CustomerRate = Math.Round(request.CustomerRate, 2, MidpointRounding.AwayFromZero);
        rate.VendorRate = Math.Round(request.VendorRate, 2, MidpointRounding.AwayFromZero);
        rate.EffectiveFrom = request.EffectiveFrom;
        rate.Active = request.Active;
        rate.Remarks = request.Remarks?.Trim();
    }

    private static async Task<IReadOnlyList<TailoringVendorRateDto>> BuildVendorRateDtosAsync(GarmetixDbContext db, IReadOnlyList<TailoringVendorServiceRate> rates, CancellationToken cancellationToken)
    {
        if (rates.Count == 0) return Array.Empty<TailoringVendorRateDto>();
        var vendorIds = rates.Select(item => item.VendorId).Distinct().ToList();
        var serviceIds = rates.Select(item => item.ServiceItemId).Distinct().ToList();
        var vendorMap = await db.Vendors.AsNoTracking().Where(item => vendorIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var serviceMap = await db.TailoringServiceItems.AsNoTracking().Where(item => serviceIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => new { item.Name, item.Category }, cancellationToken);
        return rates.Select(item => new TailoringVendorRateDto(
            item.Id,
            item.CompanyId,
            item.StoreGroupId,
            item.StoreId,
            item.VendorId,
            vendorMap.TryGetValue(item.VendorId, out var vendorName) ? vendorName : "Vendor",
            item.ServiceItemId,
            serviceMap.TryGetValue(item.ServiceItemId, out var service) ? service.Name : "Service",
            serviceMap.TryGetValue(item.ServiceItemId, out var serviceCategory) ? serviceCategory.Category : TailoringServiceCategory.Other,
            item.CustomerRate,
            item.VendorRate,
            item.EffectiveFrom,
            item.Active,
            item.Remarks)).ToList();
    }

    private static async Task<IResult> CreateOrderAsync(
        TailoringOrderRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        CancellationToken cancellationToken)
    {
        if (request.Lines.Count == 0) return Results.BadRequest(new { message = "Add at least one tailoring or alteration service line." });
        var customer = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == request.CustomerId, cancellationToken);
        if (customer is null) return Results.BadRequest(new { message = "Select a valid customer." });
        var vendor = request.VendorId.HasValue
            ? await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == request.VendorId.Value, cancellationToken)
            : null;

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var order = new TailoringOrder
            {
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId,
                OrderNumber = await DocumentNumberGenerator.NextAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, "TailoringOrder", request.OrderType == TailoringOrderType.Alteration ? "ALT" : "TLR", request.ExpectedDeliveryDate ?? DateTime.Today, cancellationToken),
                OnDate = DateTime.Now,
                OrderType = request.OrderType,
                Status = vendor is null ? TailoringOrderStatus.Ordered : TailoringOrderStatus.SentToVendor,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CustomerMobileNumber = customer.MobileNumber,
                VendorId = vendor?.Id,
                VendorName = vendor?.Name,
                SourceInvoiceId = request.SourceInvoiceId,
                SourceInvoiceItemId = request.SourceInvoiceItemId,
                SourceProductId = request.SourceProductId,
                SourceProductName = request.SourceProductName,
                SourceBarcode = request.SourceBarcode,
                ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                MeasurementsJson = request.MeasurementsJson,
                CustomerInstructions = request.CustomerInstructions,
                InternalRemarks = request.InternalRemarks
            };
            if (!WorkspaceScope.CanWrite(order, context, out var message)) return Results.BadRequest(new { message });

            db.TailoringOrders.Add(order);
            foreach (var lineRequest in request.Lines)
            {
                var line = BuildLine(order, lineRequest);
                db.TailoringOrderLines.Add(line);
            }

            RecalculateOrder(order, request.Lines.Select(line => BuildLine(order, line)).ToList(), Array.Empty<TailoringCustomerReceipt>(), Array.Empty<TailoringVendorPayment>());
            AddHistory(db, order, null, order.Status, "Order created", context.User.Identity?.Name, request.InternalRemarks);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Results.Created($"/api/tailoring/orders/{order.Id}", ToSummary(order));
        });
    }

    private static async Task<IResult> DeliverOrderAsync(Guid id, TailoringDeliveryRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();
        var from = order.Status;
        order.DeliveredAt = request.DeliveredAt ?? DateTime.Now;
        order.Status = order.ServiceInvoiceId.HasValue ? TailoringOrderStatus.Invoiced : TailoringOrderStatus.Delivered;
        var lines = await db.TailoringOrderLines.Where(line => line.TailoringOrderId == id).ToListAsync(cancellationToken);
        foreach (var line in lines)
        {
            line.DeliveredAt ??= order.DeliveredAt;
            line.Status = TailoringOrderStatus.Delivered;
        }
        AddHistory(db, order, from, order.Status, "Delivered", context.User.Identity?.Name, request.Remarks);
        await ApplyInHouseAlterationCostImpactAsync(db, order, context.User.Identity?.Name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToSummary(order));
    }

    private static async Task<IResult> ReceiveCustomerPaymentAsync(Guid id, TailoringPaymentRequest request, HttpContext context, GarmetixDbContext db, AccountingPostingService accounting, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();

        var receipt = new TailoringCustomerReceipt
        {
            TailoringOrderId = order.Id,
            CompanyId = order.CompanyId,
            StoreGroupId = order.StoreGroupId,
            StoreId = order.StoreId,
            OnDate = request.OnDate,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            PaymentMode = request.PaymentMode,
            BankAccountId = request.BankAccountId,
            ReferenceNumber = request.ReferenceNumber,
            Remarks = request.Remarks
        };
        db.TailoringCustomerReceipts.Add(receipt);

        if (order.ServiceInvoiceId.HasValue)
        {
            var invoice = await db.SalesInvoices.FirstAsync(item => item.Id == order.ServiceInvoiceId.Value, cancellationToken);
            var payment = new InvoicePayment
            {
                InvoiceId = invoice.Id,
                CompanyId = invoice.CompanyId,
                StoreId = invoice.StoreId,
                OnDate = request.OnDate,
                Amount = receipt.Amount,
                PaymentMode = request.PaymentMode,
                BankAccountId = request.BankAccountId,
                ReferenceNumber = request.ReferenceNumber,
                PaymentDetailsJson = JsonSerializer.Serialize(new { source = "TailoringCustomerReceipt", order.OrderNumber, request.ReferenceNumber })
            };
            db.InvoicePayments.Add(payment);
            receipt.InvoicePaymentId = payment.Id;
            invoice.PaidAmount = Math.Round(invoice.PaidAmount + payment.Amount, 2, MidpointRounding.AwayFromZero);
            invoice.InvoiceStatus = invoice.PaidAmount >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            await db.SaveChangesAsync(cancellationToken);
            var customer = await db.Customers.FirstAsync(item => item.Id == order.CustomerId, cancellationToken);
            await accounting.PostSalesInvoiceAsync(invoice, customer, order.StoreGroupId, await LoadInvoicePaymentPostingsAsync(db, invoice.Id, cancellationToken), cancellationToken);
        }

        await RecalculateAndSaveAsync(db, order, context.User.Identity?.Name, "Customer payment received", request.Remarks, cancellationToken);
        return Results.Ok(ToSummary(order));
    }

    private static async Task<IResult> PayVendorAsync(Guid id, TailoringPaymentRequest request, HttpContext context, GarmetixDbContext db, DocumentNumberService documentNumbers, AccountingPostingService accounting, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();
        if (!order.VendorId.HasValue) return Results.BadRequest(new { message = "Select a tailoring vendor before recording vendor payment." });
        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == order.VendorId.Value, cancellationToken);
        if (vendor is null) return Results.BadRequest(new { message = "Tailoring vendor was not found." });

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var voucher = new Voucher
            {
                VoucherNumber = await documentNumbers.NextVoucherAsync(order.CompanyId, order.StoreGroupId, order.StoreId, VoucherType.Payment, request.OnDate, cancellationToken),
                OnDate = request.OnDate,
                VoucherType = VoucherType.Payment,
                PartyName = vendor.Name,
                Particulars = $"Tailoring vendor payment {order.OrderNumber}",
                Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
                Remarks = request.Remarks ?? string.Empty,
                SlipNumber = request.ReferenceNumber,
                PaymentMode = request.PaymentMode,
                PaymentDetails = request.ReferenceNumber,
                CompanyId = order.CompanyId,
                StoreGroupId = order.StoreGroupId,
                StoreId = order.StoreId,
                CreatedBy = context.User.Identity?.Name
            };
            db.Vouchers.Add(voucher);
            await accounting.PostVendorPaymentVoucherAsync(voucher, vendor, order.StoreGroupId, order.StoreId, request.BankAccountId, cancellationToken);
            var payment = new TailoringVendorPayment
            {
                TailoringOrderId = order.Id,
                VendorId = vendor.Id,
                CompanyId = order.CompanyId,
                StoreGroupId = order.StoreGroupId,
                StoreId = order.StoreId,
                OnDate = request.OnDate,
                Amount = voucher.Amount,
                PaymentMode = request.PaymentMode,
                BankAccountId = request.BankAccountId,
                ReferenceNumber = request.ReferenceNumber,
                VoucherId = voucher.Id,
                Remarks = request.Remarks
            };
            db.TailoringVendorPayments.Add(payment);
            await RecalculateAndSaveAsync(db, order, context.User.Identity?.Name, "Vendor payment recorded", request.Remarks, cancellationToken, save: false);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Results.Ok(ToSummary(order));
        });
    }

    private static async Task<IResult> ConvertToServiceInvoiceAsync(Guid id, TailoringInvoiceRequest request, HttpContext context, GarmetixDbContext db, DocumentNumberService documentNumbers, AccountingPostingService accounting, CancellationToken cancellationToken)
    {
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();
        if (order.ServiceInvoiceId.HasValue) return Results.Conflict(new { message = "Tailoring order is already converted to a service invoice." });
        var lines = await db.TailoringOrderLines.AsNoTracking().Where(line => line.TailoringOrderId == id).ToListAsync(cancellationToken);
        if (lines.Count == 0) return Results.BadRequest(new { message = "Cannot invoice an order without service lines." });
        var customer = await db.Customers.FirstOrDefaultAsync(item => item.Id == order.CustomerId, cancellationToken);
        if (customer is null) return Results.BadRequest(new { message = "Customer was not found." });
        var salesmanId = request.SalesmanId ?? await db.Salesmen.AsNoTracking()
            .Where(item => item.CompanyId == order.CompanyId && item.StoreId == order.StoreId && item.Active)
            .OrderBy(item => item.Name)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (salesmanId == Guid.Empty) return Results.BadRequest(new { message = "Add an active salesman for this store before creating service invoices." });

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var invoiceDate = request.InvoiceDate ?? DateTime.Now;
            var invoice = new Invoice
            {
                CompanyId = order.CompanyId,
                StoreId = order.StoreId,
                InvoiceNumber = await documentNumbers.NextSaleInvoiceAsync(order.CompanyId, order.StoreGroupId, order.StoreId, cancellationToken),
                OnDate = invoiceDate,
                InvoiceType = InvoiceType.Service,
                InvoiceStatus = InvoiceStatus.Pending,
                CustomerId = customer.Id,
                SalemanId = salesmanId,
                CustomerName = customer.Name,
                CustomerMobileNumber = customer.MobileNumber,
                CustomerGSTIN = customer.GSTIN,
                CreditSale = order.CustomerChargeAmount > order.CustomerReceivedAmount,
                SaleInvoiceType = string.IsNullOrWhiteSpace(customer.GSTIN) ? SaleInvoiceType.B2C : SaleInvoiceType.B2B,
                MRP = lines.Sum(line => line.CustomerChargeAmount + line.DiscountAmount),
                DiscountAmount = lines.Sum(line => line.DiscountAmount),
                BasePrice = lines.Sum(line => CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Taxable),
                TaxAmount = lines.Sum(line => CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Tax),
                NetAmount = lines.Sum(line => CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Taxable),
                RoundOff = 0,
                BillAmount = order.CustomerChargeAmount,
                PaidAmount = order.CustomerReceivedAmount + Math.Max(0, request.AdditionalPaidAmount),
                Quantity = lines.Sum(line => line.Quantity),
                ItemCount = lines.Count,
                PaymentMode = request.AdditionalPaymentMode ?? PaymentMode.Cash
            };
            invoice.InvoiceStatus = invoice.PaidAmount <= 0
                ? InvoiceStatus.Pending
                : invoice.PaidAmount >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
            db.SalesInvoices.Add(invoice);

            foreach (var line in lines)
            {
                db.InvoiceItems.Add(new InvoiceItem
                {
                    InvoiceId = invoice.Id,
                    CompanyId = invoice.CompanyId,
                    ProductId = line.ServiceItemId ?? line.Id,
                    Barcode = string.IsNullOrWhiteSpace(line.Barcode) ? $"TAILORING-{order.OrderNumber}" : line.Barcode,
                    ProductName = line.ServiceName,
                    HSNCode = null,
                    Unit = Unit.Pcs,
                    MRP = line.CustomerChargeAmount + line.DiscountAmount,
                    DiscountAmount = line.DiscountAmount,
                    BasePrice = CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Taxable,
                    TaxPercentage = ServiceGstRate,
                    TaxAmount = CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Tax,
                    CGSTAmount = Math.Round(CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Tax / 2m, 2, MidpointRounding.AwayFromZero),
                    SGSTAmount = Math.Round(CalculateTaxBreakup(line.CustomerChargeAmount, ServiceGstRate).Tax / 2m, 2, MidpointRounding.AwayFromZero),
                    IGSTAmount = 0,
                    Amount = line.CustomerChargeAmount,
                    TaxType = TaxType.GST,
                    TaxId = Guid.Empty,
                    BilledQuantity = line.Quantity
                });
            }

            var receipts = await db.TailoringCustomerReceipts.Where(receipt => receipt.TailoringOrderId == order.Id).ToListAsync(cancellationToken);
            foreach (var receipt in receipts.Where(receipt => receipt.Amount > 0 && receipt.InvoicePaymentId == null))
            {
                var payment = new InvoicePayment
                {
                    InvoiceId = invoice.Id,
                    CompanyId = invoice.CompanyId,
                    StoreId = invoice.StoreId,
                    OnDate = receipt.OnDate,
                    Amount = receipt.Amount,
                    PaymentMode = receipt.PaymentMode,
                    BankAccountId = receipt.BankAccountId,
                    ReferenceNumber = receipt.ReferenceNumber,
                    PaymentDetailsJson = JsonSerializer.Serialize(new { source = "TailoringAdvanceReceipt", order.OrderNumber, receipt.ReferenceNumber })
                };
                db.InvoicePayments.Add(payment);
                receipt.InvoicePaymentId = payment.Id;
            }

            if (request.AdditionalPaidAmount > 0)
            {
                db.InvoicePayments.Add(new InvoicePayment
                {
                    InvoiceId = invoice.Id,
                    CompanyId = invoice.CompanyId,
                    StoreId = invoice.StoreId,
                    OnDate = invoice.OnDate,
                    Amount = request.AdditionalPaidAmount,
                    PaymentMode = request.AdditionalPaymentMode ?? PaymentMode.Cash,
                    BankAccountId = request.BankAccountId,
                    ReferenceNumber = request.ReferenceNumber,
                    PaymentDetailsJson = JsonSerializer.Serialize(new { source = "TailoringInvoicePayment", order.OrderNumber, request.ReferenceNumber })
                });
                db.TailoringCustomerReceipts.Add(new TailoringCustomerReceipt
                {
                    TailoringOrderId = order.Id,
                    CompanyId = order.CompanyId,
                    StoreGroupId = order.StoreGroupId,
                    StoreId = order.StoreId,
                    OnDate = invoice.OnDate,
                    Amount = request.AdditionalPaidAmount,
                    PaymentMode = request.AdditionalPaymentMode ?? PaymentMode.Cash,
                    BankAccountId = request.BankAccountId,
                    ReferenceNumber = request.ReferenceNumber,
                    Remarks = request.Remarks
                });
            }

            order.ServiceInvoiceId = invoice.Id;
            order.ServiceInvoiceNumber = invoice.InvoiceNumber;
            var from = order.Status;
            order.Status = invoice.InvoiceStatus == InvoiceStatus.Paid ? TailoringOrderStatus.Paid : TailoringOrderStatus.Invoiced;
            AddHistory(db, order, from, order.Status, "Converted to service invoice", context.User.Identity?.Name, request.Remarks);
            await RecalculateAndSaveAsync(db, order, context.User.Identity?.Name, "Recalculated after service invoice", request.Remarks, cancellationToken, save: false);
            await db.SaveChangesAsync(cancellationToken);
            await accounting.PostSalesInvoiceAsync(invoice, customer, order.StoreGroupId, await LoadInvoicePaymentPostingsAsync(db, invoice.Id, cancellationToken), cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Results.Ok(new { invoice.Id, invoice.InvoiceNumber, order = ToSummary(order) });
        });
    }

    private static async Task<IResult> CancelOrderAsync(Guid id, TailoringDeliveryRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();
        if (order.ServiceInvoiceId.HasValue) return Results.Conflict(new { message = "Invoice-created tailoring orders cannot be cancelled. Cancel the service invoice first." });
        var from = order.Status;
        order.Status = TailoringOrderStatus.Cancelled;
        order.ClosedAt = DateTime.Now;
        AddHistory(db, order, from, order.Status, "Cancelled", context.User.Identity?.Name, request.Remarks);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToSummary(order));
    }


    private static async Task<IResult> CustomerByMobileAsync(string mobile, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken = default)
    {
        var normalized = (mobile ?? string.Empty).Trim();
        if (normalized.Length < 4) return Results.BadRequest(new { message = "Enter at least 4 digits of customer mobile number." });
        var customer = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context)
            .Where(item => item.MobileNumber.Contains(normalized))
            .OrderBy(item => item.Name)
            .Select(item => new TailoringCustomerLookupDto(item.Id, item.Name, item.MobileNumber, item.Email, item.GSTIN, item.CreditBalance))
            .FirstOrDefaultAsync(cancellationToken);
        return customer is null ? Results.NotFound(new { message = "No customer found for this mobile number." }) : Results.Ok(customer);
    }

    private static async Task<IReadOnlyList<TailoringSourceInvoiceDto>> AlterationSourceInvoicesAsync(HttpContext context, GarmetixDbContext db, Guid? customerId = null, string? mobile = null, CancellationToken cancellationToken = default)
    {
        var query = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).Where(item => !item.ReturnInvoice);
        if (customerId.HasValue) query = query.Where(item => item.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(mobile)) query = query.Where(item => item.CustomerMobileNumber.Contains(mobile.Trim()));
        return await query.OrderByDescending(item => item.OnDate).Take(50)
            .Select(item => new TailoringSourceInvoiceDto(item.Id, item.InvoiceNumber, item.OnDate, item.CustomerName, item.CustomerMobileNumber, item.BillAmount))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<TailoringSourceInvoiceItemDto>> AlterationSourceInvoiceItemsAsync(Guid invoiceId, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken = default)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null) return Array.Empty<TailoringSourceInvoiceItemDto>();
        return await db.InvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId)
            .OrderBy(item => item.ProductName)
            .Select(item => new TailoringSourceInvoiceItemDto(item.Id, item.InvoiceId, item.ProductId, item.ProductName, item.Barcode, item.BilledQuantity, item.MRP, item.Amount, item.TaxPercentage))
            .ToListAsync(cancellationToken);
    }

    private static async Task<TailoringDeliveryOverviewDto> DeliveryOverviewAsync(HttpContext context, GarmetixDbContext db, Guid? storeId = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var query = WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context).Where(item => item.Status != TailoringOrderStatus.Cancelled);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        var orders = await query.OrderBy(item => item.ExpectedDeliveryDate ?? DateTime.MaxValue).ThenBy(item => item.CustomerName).Take(500).ToListAsync(cancellationToken);
        static bool Open(TailoringOrder item) => item.DeliveredAt == null && item.Status != TailoringOrderStatus.Completed && item.Status != TailoringOrderStatus.Paid;
        return new TailoringDeliveryOverviewDto(
            orders.Where(item => item.ExpectedDeliveryDate.HasValue && item.ExpectedDeliveryDate.Value.Date == today && Open(item)).Select(ToSummary).ToList(),
            orders.Where(item => item.ExpectedDeliveryDate.HasValue && item.ExpectedDeliveryDate.Value.Date == tomorrow && Open(item)).Select(ToSummary).ToList(),
            orders.Where(item => item.ExpectedDeliveryDate.HasValue && item.ExpectedDeliveryDate.Value.Date < today && Open(item)).Select(ToSummary).ToList(),
            orders.Where(item => item.Status == TailoringOrderStatus.ReadyForDelivery).Select(ToSummary).ToList(),
            orders.Where(item => item.DeliveredAt.HasValue || item.Status == TailoringOrderStatus.Completed || item.Status == TailoringOrderStatus.Paid).OrderByDescending(item => item.DeliveredAt ?? item.OnDate).Take(50).Select(ToSummary).ToList());
    }

    private static async Task<IResult> UpdateStatusAsync(Guid id, TailoringStatusUpdateRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();
        if (order.Status == TailoringOrderStatus.Cancelled) return Results.Conflict(new { message = "Cancelled order status cannot be changed." });
        var from = order.Status;
        order.Status = request.Status;
        if (request.Status == TailoringOrderStatus.Delivered || request.Status == TailoringOrderStatus.Completed) order.DeliveredAt ??= request.EventDate ?? DateTime.Now;
        if (request.Status == TailoringOrderStatus.Completed || request.Status == TailoringOrderStatus.Paid) order.ClosedAt ??= request.EventDate ?? DateTime.Now;
        var lines = await db.TailoringOrderLines.Where(line => line.TailoringOrderId == id).ToListAsync(cancellationToken);
        foreach (var line in lines)
        {
            line.Status = request.Status;
            if (order.DeliveredAt.HasValue && line.DeliveredAt == null) line.DeliveredAt = order.DeliveredAt;
        }
        AddHistory(db, order, from, order.Status, $"Status changed to {request.Status}", context.User.Identity?.Name, request.Remarks);
        await ApplyInHouseAlterationCostImpactAsync(db, order, context.User.Identity?.Name, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToSummary(order));
    }

    private static async Task<IResult> PrintOrderAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
        => await BuildPrintPayloadAsync(id, "Tailoring Service Order", "Order", context, db, cancellationToken);

    private static async Task<IResult> PrintInvoiceAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
        => await BuildPrintPayloadAsync(id, "Tailoring Service Invoice", "Invoice", context, db, cancellationToken);

    private static async Task<IResult> BuildPrintPayloadAsync(Guid id, string documentType, string copyMode, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var order = await WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null) return Results.NotFound();
        var lines = await db.TailoringOrderLines.AsNoTracking().Where(line => line.TailoringOrderId == id).OrderBy(line => line.CreatedAt).ToListAsync(cancellationToken);
        var printLines = lines.Select(line =>
        {
            var gross = line.CustomerChargeAmount;
            var tax = CalculateTaxBreakup(gross, ServiceGstRate);
            return new TailoringPrintLineDto(line.ServiceName, line.GarmentName, line.Barcode, line.Quantity, line.CustomerRate, line.DiscountAmount, ServiceGstRate, tax.Taxable, tax.Tax, gross, line.Instructions);
        }).ToList();
        return Results.Ok(new TailoringPrintDto(
            documentType,
            copyMode,
            order.OrderNumber,
            order.ServiceInvoiceNumber,
            order.OnDate,
            order.ExpectedDeliveryDate,
            order.DeliveredAt,
            order.OrderType.ToString(),
            order.Status.ToString(),
            order.CustomerName,
            order.CustomerMobileNumber,
            order.VendorName,
            lines.Sum(line => line.DiscountAmount),
            printLines.Sum(line => line.TaxableAmount),
            printLines.Sum(line => line.TaxAmount),
            order.CustomerChargeAmount,
            order.CustomerReceivedAmount,
            order.CustomerBalanceAmount,
            order.CustomerInstructions,
            printLines,
            new[] { "Customer Copy", "Store Copy" }));
    }

    private static (decimal Taxable, decimal Tax) CalculateTaxBreakup(decimal gross, decimal rate)
    {
        var taxable = Math.Round(gross / (1 + (rate / 100m)), 2, MidpointRounding.AwayFromZero);
        var tax = Math.Round(gross - taxable, 2, MidpointRounding.AwayFromZero);
        return (taxable, tax);
    }

    private static async Task ApplyInHouseAlterationCostImpactAsync(GarmetixDbContext db, TailoringOrder order, string? actor, CancellationToken cancellationToken)
    {
        if (order.OrderType != TailoringOrderType.Alteration || order.InHouseExpenseAmount <= 0) return;
        var alreadyApplied = await db.TailoringOrderHistories.AsNoTracking().AnyAsync(item => item.TailoringOrderId == order.Id && item.Action == "In-house alteration cost applied to stock", cancellationToken);
        if (alreadyApplied) return;
        var barcode = string.IsNullOrWhiteSpace(order.SourceBarcode) ? null : order.SourceBarcode.Trim();
        var stock = await db.Stocks.FirstOrDefaultAsync(item => item.CompanyId == order.CompanyId && item.StoreId == order.StoreId && !item.Deleted && ((order.SourceProductId.HasValue && item.ProductId == order.SourceProductId.Value) || (barcode != null && item.Barcode == barcode)), cancellationToken);
        if (stock is null) return;
        var quantity = Math.Max(1m, stock.SoldQty > 0 ? stock.SoldQty : 1m);
        stock.CostPrice = Math.Round(stock.CostPrice + (order.InHouseExpenseAmount / quantity), 2, MidpointRounding.AwayFromZero);
        AddHistory(db, order, order.Status, order.Status, "In-house alteration cost applied to stock", actor, $"Cost impact {order.InHouseExpenseAmount} added to source item cost price.");
    }

    private static TailoringOrderLine BuildLine(TailoringOrder order, TailoringOrderLineRequest request)
    {
        var quantity = request.Quantity <= 0 ? 1 : request.Quantity;
        var customerCharge = Math.Max(0, Math.Round((request.CustomerRate * quantity) - request.DiscountAmount, 2, MidpointRounding.AwayFromZero));
        var vendorCost = Math.Max(0, Math.Round(request.VendorRate * quantity, 2, MidpointRounding.AwayFromZero));
        return new TailoringOrderLine
        {
            TailoringOrderId = order.Id,
            CompanyId = order.CompanyId,
            ServiceItemId = request.ServiceItemId,
            ServiceName = request.ServiceName.Trim(),
            Category = request.Category,
            GarmentName = request.GarmentName,
            Barcode = request.Barcode,
            Quantity = quantity,
            CustomerRate = request.CustomerRate,
            VendorRate = request.VendorRate,
            DiscountAmount = request.DiscountAmount,
            CustomerChargeAmount = customerCharge,
            VendorCostAmount = vendorCost,
            CostResponsibility = request.CostResponsibility,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate ?? order.ExpectedDeliveryDate,
            MeasurementsJson = request.MeasurementsJson,
            Instructions = request.Instructions,
            VendorRemarks = request.VendorRemarks,
            Status = order.Status
        };
    }

    private static async Task RecalculateAndSaveAsync(GarmetixDbContext db, TailoringOrder order, string? actor, string action, string? remarks, CancellationToken cancellationToken, bool save = true)
    {
        var lines = await db.TailoringOrderLines.Where(line => line.TailoringOrderId == order.Id).ToListAsync(cancellationToken);
        var receipts = await db.TailoringCustomerReceipts.Where(receipt => receipt.TailoringOrderId == order.Id).ToListAsync(cancellationToken);
        var vendorPayments = await db.TailoringVendorPayments.Where(payment => payment.TailoringOrderId == order.Id).ToListAsync(cancellationToken);
        RecalculateOrder(order, lines, receipts, vendorPayments);
        AddHistory(db, order, order.Status, order.Status, action, actor, remarks);
        if (save) await db.SaveChangesAsync(cancellationToken);
    }

    private static void RecalculateOrder(TailoringOrder order, IReadOnlyList<TailoringOrderLine> lines, IReadOnlyList<TailoringCustomerReceipt> receipts, IReadOnlyList<TailoringVendorPayment> vendorPayments)
    {
        order.CustomerChargeAmount = lines.Sum(line => line.CustomerChargeAmount);
        order.VendorCostAmount = lines.Sum(line => line.VendorCostAmount);
        order.InHouseExpenseAmount = lines.Where(line => line.CostResponsibility == TailoringCostResponsibility.InHouseExpense).Sum(line => line.VendorCostAmount);
        order.CustomerReceivedAmount = receipts.Sum(receipt => receipt.Amount);
        order.VendorPaidAmount = vendorPayments.Sum(payment => payment.Amount);
        order.CustomerBalanceAmount = Math.Max(0, order.CustomerChargeAmount - order.CustomerReceivedAmount);
        order.VendorBalanceAmount = Math.Max(0, order.VendorCostAmount - order.VendorPaidAmount);
        order.ProfitImpactAmount = order.CustomerChargeAmount - order.VendorCostAmount;
        if (order.InHouseExpenseAmount > 0 && order.CustomerChargeAmount == 0)
        {
            order.ProfitImpactAmount = -order.InHouseExpenseAmount;
        }
    }

    private static async Task<IReadOnlyList<SalesInvoicePaymentPosting>> LoadInvoicePaymentPostingsAsync(GarmetixDbContext db, Guid invoiceId, CancellationToken cancellationToken)
    {
        return await db.InvoicePayments.AsNoTracking()
            .Where(payment => payment.InvoiceId == invoiceId && payment.Amount > 0)
            .OrderBy(payment => payment.OnDate)
            .Select(payment => new SalesInvoicePaymentPosting(
                payment.PaymentMode,
                payment.Amount,
                payment.BankAccountId,
                payment.ReferenceNumber,
                payment.GatewayReference,
                payment.SettlementStatus,
                payment.AdjustmentSourceType,
                payment.AdjustmentSourceId,
                payment.PaymentDetailsJson))
            .ToListAsync(cancellationToken);
    }

    private static void ApplyServiceItemRequest(TailoringServiceItem item, TailoringServiceItemRequest request)
    {
        item.CompanyId = request.CompanyId;
        item.StoreGroupId = request.StoreGroupId;
        item.StoreId = request.StoreId;
        item.ServiceCode = request.ServiceCode.Trim();
        item.Name = request.Name.Trim();
        item.Category = request.Category;
        item.DefaultCustomerRate = request.DefaultCustomerRate;
        item.DefaultVendorRate = request.DefaultVendorRate;
        item.TaxRate = request.TaxRate <= 0 ? 5m : request.TaxRate;
        item.HSNCode = request.HSNCode?.Trim();
        item.ProductId = request.ProductId;
        item.Active = request.Active;
        item.Remarks = request.Remarks?.Trim();
    }

    private static void AddHistory(GarmetixDbContext db, TailoringOrder order, TailoringOrderStatus? from, TailoringOrderStatus? to, string action, string? actor, string? remarks)
    {
        db.TailoringOrderHistories.Add(new TailoringOrderHistory
        {
            TailoringOrderId = order.Id,
            CompanyId = order.CompanyId,
            EventDate = DateTime.Now,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Actor = actor,
            Remarks = remarks,
            DetailsJson = JsonSerializer.Serialize(new { order.OrderNumber, order.Status, order.ServiceInvoiceNumber })
        });
    }

    private static TailoringOrderSummaryDto ToSummary(TailoringOrder item)
        => new(
            item.Id,
            item.OrderNumber,
            item.OnDate,
            item.OrderType.ToString(),
            item.Status.ToString(),
            item.CustomerName,
            item.CustomerMobileNumber,
            item.VendorName,
            item.ExpectedDeliveryDate,
            item.DeliveredAt,
            item.CustomerChargeAmount,
            item.VendorCostAmount,
            item.InHouseExpenseAmount,
            item.CustomerReceivedAmount,
            item.CustomerBalanceAmount,
            item.VendorPaidAmount,
            item.VendorBalanceAmount,
            item.ProfitImpactAmount,
            item.ServiceInvoiceNumber);
}

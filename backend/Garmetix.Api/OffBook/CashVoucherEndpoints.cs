using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.OffBook;

public sealed record CashVoucherSaveRequest(
    Guid? Id,
    string VoucherNumber,
    DateTime OnDate,
    VoucherType VoucherType,
    string PartyName,
    string Particulars,
    decimal Amount,
    string? Remarks,
    string? SlipNumber,
    Guid TransactionId,
    Guid? EmployeeId,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public static class CashVoucherEndpoints
{
    public static RouteGroupBuilder MapCashVoucherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cash-vouchers")
            .WithTags("Cash Voucher")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> ListAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = ApplyUserScope(db.CashVouchers.AsNoTracking(), context);

        if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return Results.Ok(await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var voucher = await ApplyUserScope(db.CashVouchers.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return voucher is null ? Results.NotFound() : Results.Ok(voucher);
    }

    private static async Task<IResult> CreateAsync(
        CashVoucherSaveRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        return await SaveAsync(request with { Id = null }, context, db, cancellationToken, true);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        CashVoucherSaveRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        return await SaveAsync(request with { Id = id }, context, db, cancellationToken, false);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var voucher = await ApplyUserScope(db.CashVouchers, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (voucher is null)
        {
            return Results.NotFound();
        }

        db.CashVouchers.Remove(voucher);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SaveAsync(
        CashVoucherSaveRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken,
        bool created)
    {
        try
        {
            ValidateRequest(request);
            EnsureUserScope(request, context);
            await ValidateReferencesAsync(request, db, cancellationToken);

            CashVoucher voucher;
            if (request.Id.HasValue)
            {
                voucher = await ApplyUserScope(db.CashVouchers, context)
                    .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
                    ?? throw new KeyNotFoundException("Cash voucher was not found.");
            }
            else
            {
                voucher = new CashVoucher();
                db.CashVouchers.Add(voucher);
            }

            voucher.VoucherNumber = request.VoucherNumber.Trim();
            voucher.OnDate = request.OnDate;
            voucher.VoucherType = request.VoucherType;
            voucher.PartyName = request.PartyName.Trim();
            voucher.Particulars = request.Particulars.Trim();
            voucher.Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
            voucher.Remarks = request.Remarks?.Trim() ?? string.Empty;
            voucher.SlipNumber = request.SlipNumber?.Trim();
            voucher.TransactionId = request.TransactionId;
            voucher.EmployeeId = request.EmployeeId;
            voucher.CompanyId = request.CompanyId;
            voucher.StoreGroupId = request.StoreGroupId;
            voucher.StoreId = request.StoreId;

            // Cash vouchers are off-book records and must never link to a ledger.
            voucher.LedgerId = null;

            await db.SaveChangesAsync(cancellationToken);
            return created
                ? Results.Created($"/api/cash-vouchers/{voucher.Id}", voucher)
                : Results.Ok(voucher);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task ValidateReferencesAsync(
        CashVoucherSaveRequest request,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var store = await db.Stores.AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.Id == request.StoreId
                && item.CompanyId == request.CompanyId
                && item.StoreGroupId == request.StoreGroupId,
                cancellationToken);
        if (store is null)
        {
            throw new InvalidOperationException("Select a valid company, store group, and store.");
        }

        var transactionExists = await db.Transactions.AsNoTracking()
            .AnyAsync(item => item.Id == request.TransactionId && item.CompanyId == request.CompanyId, cancellationToken);
        if (!transactionExists)
        {
            throw new InvalidOperationException("Select a valid cash transaction category.");
        }

        if (request.EmployeeId.HasValue)
        {
            var employeeExists = await db.Employees.AsNoTracking()
                .AnyAsync(item =>
                    item.Id == request.EmployeeId.Value
                    && item.CompanyId == request.CompanyId
                    && item.StoreId == request.StoreId,
                    cancellationToken);
            if (!employeeExists)
            {
                throw new InvalidOperationException("Select a valid employee for the working store.");
            }
        }
    }

    private static void ValidateRequest(CashVoucherSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VoucherNumber))
        {
            throw new ArgumentException("Voucher number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PartyName))
        {
            throw new ArgumentException("Paid to or received from is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Particulars))
        {
            throw new ArgumentException("Particulars are required.");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.");
        }
    }

    private static IQueryable<CashVoucher> ApplyUserScope(
        IQueryable<CashVoucher> query,
        HttpContext context)
    {
        if (ClaimGuid(context, "companyId") is { } companyId)
        {
            query = query.Where(item => item.CompanyId == companyId);
        }

        if (ClaimGuid(context, "storeGroupId") is { } storeGroupId)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId);
        }

        if (ClaimGuid(context, "storeId") is { } storeId)
        {
            query = query.Where(item => item.StoreId == storeId);
        }

        return query;
    }

    private static void EnsureUserScope(CashVoucherSaveRequest request, HttpContext context)
    {
        if (ClaimGuid(context, "companyId") is { } companyId && request.CompanyId != companyId)
        {
            throw new InvalidOperationException("The selected company is outside your access scope.");
        }

        if (ClaimGuid(context, "storeGroupId") is { } storeGroupId && request.StoreGroupId != storeGroupId)
        {
            throw new InvalidOperationException("The selected store group is outside your access scope.");
        }

        if (ClaimGuid(context, "storeId") is { } storeId && request.StoreId != storeId)
        {
            throw new InvalidOperationException("The selected store is outside your access scope.");
        }
    }

    private static Guid? ClaimGuid(HttpContext context, string claimName)
    {
        return Guid.TryParse(context.User.FindFirst(claimName)?.Value, out var value) ? value : null;
    }
}

using Garmetix.Api.Auth;
using Garmetix.Api.Accounting;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Base;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

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

public sealed record ConvertCashVoucherToBooksRequest(
    Guid LedgerId,
    Guid EmployeeId,
    string Reason);

public sealed record ConvertVoucherToCashRequest(
    Guid TransactionId,
    string Reason);

public static class CashVoucherEndpoints
{
    public static RouteGroupBuilder MapCashVoucherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cash-vouchers")
            .WithTags("Cash Voucher")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/", ListAsync);
        group.MapGet("/conversions", ListConversionsAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/eligible-on-book", ListEligibleOnBookAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/pdf", DownloadPdfAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{id:guid}/convert-to-books", ConvertToBooksAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/from-voucher/{voucherId:guid}", ConvertFromVoucherAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<IResult> ListConversionsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = ApplyUserScope(db.CashVoucherConversions.AsNoTracking(), context);
        if (companyId.HasValue) query = query.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);

        return Results.Ok(await query
            .OrderByDescending(item => item.ConvertedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ListEligibleOnBookAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = ApplyUserScope(db.Vouchers.AsNoTracking(), context)
            .Where(item => item.PaymentMode == PaymentMode.Cash);
        if (companyId.HasValue) query = query.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);

        return Results.Ok(await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken));
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


    private static async Task<IResult> DownloadPdfAsync(
        Guid id,
        string? format,
        bool? reprint,
        bool? signatures,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var voucher = await ApplyUserScope(db.CashVouchers.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (voucher is null)
        {
            return Results.NotFound();
        }

        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == voucher.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == voucher.StoreId, cancellationToken);
        var transaction = await db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == voucher.TransactionId, cancellationToken);
        var employee = voucher.EmployeeId.HasValue
            ? await db.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == voucher.EmployeeId.Value, cancellationToken)
            : null;

        var document = new VoucherPdfModel(
            company?.Name ?? "Garmetix",
            FormatAddress(company?.Address, company?.City, company?.State, company?.ZipCode),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            store?.Name ?? "Store",
            voucher.VoucherNumber,
            voucher.OnDate,
            $"{voucher.VoucherType} Cash",
            voucher.PartyName,
            voucher.Particulars,
            voucher.Amount,
            voucher.Remarks,
            voucher.SlipNumber,
            "Cash",
            "Off-book cash voucher",
            transaction?.Name ?? "Cash category",
            employee?.StaffName ?? "-",
            "-",
            Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.CashVoucher, voucher.Id));

        var pdf = VoucherPdfDocument.Build(
            document,
            string.Equals(format, "a5-one", StringComparison.OrdinalIgnoreCase),
            reprint == true,
            signatures != false);
        var safeNumber = Regex.Replace(voucher.VoucherNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "cash-voucher")}.pdf");
    }

    private static string FormatAddress(params string?[] parts)
    {
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
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

        if (await db.CashVoucherConversions.AnyAsync(item => item.CashVoucherId == id, cancellationToken))
        {
            return Results.Conflict(new { message = "Converted cash vouchers are retained for audit and cannot be deleted." });
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
                if (await db.CashVoucherConversions.AnyAsync(item => item.CashVoucherId == request.Id.Value, cancellationToken))
                {
                    throw new InvalidOperationException("Converted cash vouchers are immutable. Create a new corrective record.");
                }

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

    private static async Task<IResult> ConvertToBooksAsync(
        Guid id,
        ConvertCashVoucherToBooksRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        try
        {
            ValidateConversionReason(request.Reason);
            var strategy = db.Database.CreateExecutionStrategy();
            var conversion = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var source = await ApplyUserScope(db.CashVouchers, context)
                    .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
                    ?? throw new KeyNotFoundException("Cash voucher was not found.");

                var result = await accounting.SaveVoucherInCurrentTransactionAsync(
                    new VoucherSaveRequest(
                        null,
                        string.Empty,
                        source.OnDate,
                        source.VoucherType,
                        source.PartyName,
                        source.Particulars,
                        source.Amount,
                        AppendConversionRemark(source.Remarks, source.VoucherNumber, request.Reason),
                        source.SlipNumber,
                        PaymentMode.Cash,
                        $"Converted from Off Book cash voucher {source.VoucherNumber}.",
                        false,
                        null,
                        request.LedgerId,
                        request.EmployeeId,
                        null,
                        source.CompanyId,
                        source.StoreGroupId,
                        source.StoreId),
                    cancellationToken);

                var destination = await db.Vouchers
                    .FirstAsync(item => item.Id == result.VoucherId, cancellationToken);
                source.Deleted = true;
                var audit = CreateConversion(
                    "OffBookToOnBook",
                    source,
                    destination,
                    request.Reason,
                    context);
                db.CashVoucherConversions.Add(audit);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return audit;
            });

            return Results.Ok(new
            {
                message = "Cash voucher moved to the accounting books.",
                conversion
            });
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

    private static async Task<IResult> ConvertFromVoucherAsync(
        Guid voucherId,
        ConvertVoucherToCashRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        try
        {
            ValidateConversionReason(request.Reason);
            var strategy = db.Database.CreateExecutionStrategy();
            var conversion = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var source = await ApplyUserScope(db.Vouchers, context)
                    .FirstOrDefaultAsync(item => item.Id == voucherId, cancellationToken)
                    ?? throw new KeyNotFoundException("Accounting voucher was not found.");
                if (source.PaymentMode != PaymentMode.Cash)
                {
                    throw new InvalidOperationException("Only cash payment, receipt, or expense vouchers can be moved Off Book.");
                }

                var transactionExists = await db.Transactions.AsNoTracking()
                    .AnyAsync(item => item.Id == request.TransactionId && item.CompanyId == source.CompanyId, cancellationToken);
                if (!transactionExists)
                {
                    throw new InvalidOperationException("Select a valid cash transaction category.");
                }

                var destination = new CashVoucher
                {
                    VoucherNumber = $"CV-{source.OnDate:yyyyMMdd}-{source.VoucherNumber.Split('/').Last()}",
                    OnDate = source.OnDate,
                    VoucherType = source.VoucherType,
                    PartyName = source.PartyName,
                    Particulars = source.Particulars,
                    Amount = source.Amount,
                    Remarks = AppendConversionRemark(source.Remarks, source.VoucherNumber, request.Reason),
                    SlipNumber = source.SlipNumber,
                    TransactionId = request.TransactionId,
                    EmployeeId = source.EmployeeId,
                    LedgerId = null,
                    CompanyId = source.CompanyId,
                    StoreGroupId = source.StoreGroupId,
                    StoreId = source.StoreId
                };
                db.CashVouchers.Add(destination);

                await RemoveVoucherPostingsAsync(source, db, accounting, cancellationToken);
                source.Deleted = true;
                var audit = CreateConversion(
                    "OnBookToOffBook",
                    destination,
                    source,
                    request.Reason,
                    context);
                db.CashVoucherConversions.Add(audit);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return audit;
            });

            return Results.Ok(new
            {
                message = "Accounting voucher moved to the Off Book cash register.",
                conversion
            });
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

    private static CashVoucherConversion CreateConversion(
        string direction,
        CashVoucher cashVoucher,
        Voucher voucher,
        string reason,
        HttpContext context)
    {
        var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, out var userId))
        {
            throw new InvalidOperationException("The signed-in administrator could not be identified.");
        }

        return new CashVoucherConversion
        {
            Direction = direction,
            CashVoucherId = cashVoucher.Id,
            VoucherId = voucher.Id,
            CashVoucherNumber = cashVoucher.VoucherNumber,
            VoucherNumber = voucher.VoucherNumber,
            VoucherType = voucher.VoucherType,
            Amount = voucher.Amount,
            PartyName = voucher.PartyName,
            Particulars = voucher.Particulars,
            Reason = reason.Trim(),
            ConvertedByUserId = userId,
            ConvertedByUserName = context.User.Identity?.Name ?? "Administrator",
            ConvertedAt = DateTime.Now,
            CompanyId = voucher.CompanyId,
            StoreGroupId = voucher.StoreGroupId,
            StoreId = voucher.StoreId
        };
    }

    private static async Task RemoveVoucherPostingsAsync(
        Voucher voucher,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        var journals = await db.JournalEntries
            .Include(entry => entry.Lines)
            .Where(entry => entry.SourceType == "Voucher" && entry.SourceId == voucher.Id)
            .ToListAsync(cancellationToken);
        foreach (var journal in journals)
        {
            db.JournalLines.RemoveRange(journal.Lines ?? []);
            db.JournalEntries.Remove(journal);
        }

        var bankTransactions = await db.BankTransactions
            .Where(item => item.Reference == voucher.VoucherNumber)
            .ToListAsync(cancellationToken);
        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToList();
        var statementLines = await db.BankStatementLines
            .Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value))
            .ToListAsync(cancellationToken);
        var bankAccountIds = statementLines.Select(item => item.BankAccountId).Distinct().ToList();
        db.BankStatementLines.RemoveRange(statementLines);
        db.BankTransactions.RemoveRange(bankTransactions);

        var cheques = await db.ChequeLogs
            .Where(item => item.Narration == voucher.VoucherNumber)
            .ToListAsync(cancellationToken);
        db.ChequeLogs.RemoveRange(cheques);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var bankAccountId in bankAccountIds)
        {
            await accounting.RecalculateBankStatementBalancesAsync(bankAccountId, cancellationToken);
        }
    }

    private static void ValidateConversionReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8)
        {
            throw new ArgumentException("Enter an audit reason of at least 8 characters.");
        }
    }

    private static string AppendConversionRemark(string? remarks, string sourceNumber, string reason)
    {
        var prefix = string.IsNullOrWhiteSpace(remarks) ? string.Empty : $"{remarks.Trim()} | ";
        return $"{prefix}Converted from {sourceNumber}. Reason: {reason.Trim()}";
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

    private static IQueryable<T> ApplyUserScope<T>(
        IQueryable<T> query,
        HttpContext context)
        where T : StoreBase
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

using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaMutualFundDto(
    Guid Id,
    string SchemeName,
    string? Amc,
    string? FolioNumber,
    Guid? AccountId,
    string InvestmentMode,
    decimal? SipAmount,
    int? SipDayOfMonth,
    DateTime? LastSipInstallmentDate,
    bool SipDueThisMonth,
    decimal? CurrentNav,
    DateTime? CurrentNavUpdatedAt,
    decimal CurrentUnits,
    decimal TotalInvested,
    decimal? CurrentValue,
    decimal? AbsoluteReturn,
    decimal? ReturnPercent,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaMutualFundPayload(
    string SchemeName,
    string? Amc,
    string? FolioNumber,
    Guid? AccountId,
    string InvestmentMode,
    decimal? SipAmount,
    int? SipDayOfMonth,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaUpdateNavPayload(decimal CurrentNav);

public sealed record SwalekhaMutualFundTransactionDto(
    Guid Id,
    Guid FundId,
    string TransactionType,
    DateTime TransactionDate,
    decimal Units,
    decimal NavAtTransaction,
    decimal Amount,
    string Narration,
    DateTime CreatedAt);

public sealed record SwalekhaMutualFundTransactionPayload(
    string TransactionType,
    DateTime TransactionDate,
    decimal? Amount,
    decimal? Units,
    decimal NavAtTransaction,
    string? Narration);

public sealed record SwalekhaMutualFundTransactionList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaMutualFundTransactionDto> Rows);

public sealed record SwalekhaMutualFundReturnsDto(
    decimal CurrentUnits,
    decimal? CurrentNav,
    decimal? CurrentValue,
    decimal TotalInvested,
    decimal? AbsoluteReturn,
    decimal? ReturnPercent,
    decimal? Xirr,
    bool XirrAvailable,
    string? XirrNote);

/// <summary>
/// PersonalFin_09 - Investments II (Mutual Funds + SIP tracker). CurrentUnits/TotalInvested on the
/// fund are running totals kept in lockstep with every transaction, matching PersonalFin_08's
/// FD/RD integration pattern: Purchase/SipInstallment debits the linked account, Redemption
/// credits it. CurrentNav is manually updated (no live market-data provider in this stage) and
/// drives CurrentValue/AbsoluteReturn/ReturnPercent/XIRR.
/// </summary>
public static class SwalekhaMutualFundEndpoints
{
    public static RouteGroupBuilder MapSwalekhaMutualFundEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/mutual-funds")
            .WithTags("Swalekha Investments")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListFundsAsync);
        group.MapGet("/{id:guid}", GetFundAsync);
        group.MapPost("/", CreateFundAsync);
        group.MapPut("/{id:guid}", UpdateFundAsync);
        group.MapDelete("/{id:guid}", DeleteFundAsync);
        group.MapPut("/{id:guid}/nav", UpdateNavAsync);
        group.MapGet("/{id:guid}/returns", GetReturnsAsync);

        group.MapGet("/{id:guid}/transactions", ListTransactionsAsync);
        group.MapPost("/{id:guid}/transactions", AddTransactionAsync);
        group.MapDelete("/{id:guid}/transactions/{transactionId:guid}", DeleteTransactionAsync);

        return group;
    }

    private static async Task<IResult> ListFundsAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaMutualFunds.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(f => f.IsActive);
        }

        var funds = await query.OrderBy(f => f.SchemeName).ToListAsync(cancellationToken);
        return Results.Ok(funds.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetFundAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var fund = await db.SwalekhaMutualFunds.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        return fund is null ? Results.NotFound() : Results.Ok(ToDto(fund));
    }

    private static async Task<IResult> CreateFundAsync(SwalekhaMutualFundPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.SchemeName))
        {
            return Results.BadRequest(new { message = "Scheme name is required." });
        }

        if (!Enum.TryParse<SwalekhaMutualFundInvestmentMode>(payload.InvestmentMode, true, out var mode))
        {
            return Results.BadRequest(new { message = $"Unknown investment mode '{payload.InvestmentMode}'." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        var fund = new SwalekhaMutualFund
        {
            SchemeName = payload.SchemeName.Trim(),
            Amc = payload.Amc,
            FolioNumber = payload.FolioNumber,
            AccountId = payload.AccountId,
            InvestmentMode = mode,
            SipAmount = payload.SipAmount,
            SipDayOfMonth = payload.SipDayOfMonth,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaMutualFunds.Add(fund);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/mutual-funds/{fund.Id}", ToDto(fund));
    }

    private static async Task<IResult> UpdateFundAsync(Guid id, SwalekhaMutualFundPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var fund = await db.SwalekhaMutualFunds.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fund is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.SchemeName))
        {
            return Results.BadRequest(new { message = "Scheme name is required." });
        }

        if (!Enum.TryParse<SwalekhaMutualFundInvestmentMode>(payload.InvestmentMode, true, out var mode))
        {
            return Results.BadRequest(new { message = $"Unknown investment mode '{payload.InvestmentMode}'." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        fund.SchemeName = payload.SchemeName.Trim();
        fund.Amc = payload.Amc;
        fund.FolioNumber = payload.FolioNumber;
        fund.AccountId = payload.AccountId;
        fund.InvestmentMode = mode;
        fund.SipAmount = payload.SipAmount;
        fund.SipDayOfMonth = payload.SipDayOfMonth;
        fund.IsActive = payload.IsActive;
        fund.Notes = payload.Notes;
        fund.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(fund));
    }

    private static async Task<IResult> DeleteFundAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var fund = await db.SwalekhaMutualFunds.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fund is null) return Results.NotFound();

        fund.Deleted = true;
        fund.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateNavAsync(Guid id, SwalekhaUpdateNavPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var fund = await db.SwalekhaMutualFunds.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fund is null) return Results.NotFound();

        if (payload.CurrentNav <= 0)
        {
            return Results.BadRequest(new { message = "NAV must be greater than zero." });
        }

        fund.CurrentNav = payload.CurrentNav;
        fund.CurrentNavUpdatedAt = DateTime.UtcNow;
        fund.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(fund));
    }

    private static async Task<IResult> ListTransactionsAsync(Guid id, SwalekhaDbContext db, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var fundExists = await db.SwalekhaMutualFunds.AsNoTracking().AnyAsync(f => f.Id == id, cancellationToken);
        if (!fundExists) return Results.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        var query = db.SwalekhaMutualFundTransactions.AsNoTracking()
            .Where(t => t.FundId == id)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize).ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaMutualFundTransactionList(effectivePage, effectivePageSize, totalCount, rows.Select(ToDto).ToList()));
    }

    private static async Task<IResult> AddTransactionAsync(Guid id, SwalekhaMutualFundTransactionPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SwalekhaMutualFundTransactionType>(payload.TransactionType, true, out var type))
        {
            return Results.BadRequest(new { message = $"Unknown transaction type '{payload.TransactionType}'." });
        }

        if (payload.NavAtTransaction <= 0)
        {
            return Results.BadRequest(new { message = "NAV must be greater than zero." });
        }

        var fund = await db.SwalekhaMutualFunds.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fund is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var entry = new SwalekhaMutualFundTransaction
        {
            FundId = fund.Id,
            TransactionType = type,
            TransactionDate = payload.TransactionDate,
            NavAtTransaction = payload.NavAtTransaction,
            Narration = payload.Narration ?? string.Empty
        };

        if (type == SwalekhaMutualFundTransactionType.Redemption)
        {
            if (payload.Units is not (> 0))
            {
                return Results.BadRequest(new { message = "Units to redeem must be greater than zero." });
            }

            if (payload.Units.Value > fund.CurrentUnits)
            {
                return Results.BadRequest(new { message = $"Cannot redeem {payload.Units.Value} units - only {fund.CurrentUnits} available." });
            }

            var amount = payload.Units.Value * payload.NavAtTransaction;
            var investedRemoved = fund.CurrentUnits > 0 ? fund.TotalInvested * (payload.Units.Value / fund.CurrentUnits) : 0m;

            entry.Units = payload.Units.Value;
            entry.Amount = amount;
            entry.InvestedAmountRemoved = investedRemoved;

            fund.CurrentUnits -= payload.Units.Value;
            fund.TotalInvested -= investedRemoved;

            if (fund.AccountId.HasValue)
            {
                var accountError = await CreditAccountAsync(fund.AccountId.Value, amount, payload.TransactionDate,
                    string.IsNullOrWhiteSpace(payload.Narration) ? $"MF redemption - {fund.SchemeName}" : payload.Narration.Trim(), db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }
        else
        {
            if (payload.Amount is not (> 0))
            {
                return Results.BadRequest(new { message = "Amount must be greater than zero." });
            }

            var units = payload.Amount.Value / payload.NavAtTransaction;

            entry.Units = units;
            entry.Amount = payload.Amount.Value;

            fund.CurrentUnits += units;
            fund.TotalInvested += payload.Amount.Value;

            if (type == SwalekhaMutualFundTransactionType.SipInstallment)
            {
                fund.LastSipInstallmentDate = payload.TransactionDate;
            }

            if (fund.AccountId.HasValue)
            {
                var accountError = await DebitAccountAsync(fund.AccountId.Value, payload.Amount.Value, payload.TransactionDate,
                    string.IsNullOrWhiteSpace(payload.Narration) ? $"MF {type} - {fund.SchemeName}" : payload.Narration.Trim(), db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }

        fund.UpdatedAt = DateTime.UtcNow;
        db.SwalekhaMutualFundTransactions.Add(entry);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/swalekha/mutual-funds/{id}/transactions/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> DeleteTransactionAsync(Guid id, Guid transactionId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaMutualFundTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.FundId == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        var fund = await db.SwalekhaMutualFunds.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fund is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (entry.TransactionType == SwalekhaMutualFundTransactionType.Redemption)
        {
            fund.CurrentUnits += entry.Units;
            fund.TotalInvested += entry.InvestedAmountRemoved ?? 0m;

            if (fund.AccountId.HasValue)
            {
                var accountError = await DebitAccountAsync(fund.AccountId.Value, entry.Amount, DateTime.UtcNow,
                    $"Reversal of MF redemption - {fund.SchemeName}", db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }
        else
        {
            fund.CurrentUnits -= entry.Units;
            fund.TotalInvested -= entry.Amount;

            if (fund.AccountId.HasValue)
            {
                var accountError = await CreditAccountAsync(fund.AccountId.Value, entry.Amount, DateTime.UtcNow,
                    $"Reversal of MF {entry.TransactionType} - {fund.SchemeName}", db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }

        fund.UpdatedAt = DateTime.UtcNow;
        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetReturnsAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var fund = await db.SwalekhaMutualFunds.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (fund is null) return Results.NotFound();

        var currentValue = fund.CurrentNav.HasValue ? fund.CurrentUnits * fund.CurrentNav.Value : (decimal?)null;
        var absoluteReturn = currentValue.HasValue ? currentValue.Value - fund.TotalInvested : (decimal?)null;
        var returnPercent = currentValue.HasValue && fund.TotalInvested > 0 ? absoluteReturn!.Value / fund.TotalInvested * 100 : (decimal?)null;

        var transactions = await db.SwalekhaMutualFundTransactions.AsNoTracking()
            .Where(t => t.FundId == id)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(cancellationToken);

        var cashFlows = transactions
            .Select(t => (t.TransactionDate, t.TransactionType == SwalekhaMutualFundTransactionType.Redemption ? t.Amount : -t.Amount))
            .ToList();

        if (fund.CurrentUnits > 0 && currentValue.HasValue)
        {
            cashFlows.Add((DateTime.UtcNow, currentValue.Value));
        }

        var xirrAvailable = SwalekhaXirrCalculator.TryCalculate(cashFlows, out var xirr);

        return Results.Ok(new SwalekhaMutualFundReturnsDto(
            fund.CurrentUnits,
            fund.CurrentNav,
            currentValue,
            fund.TotalInvested,
            absoluteReturn,
            returnPercent,
            xirrAvailable ? xirr * 100 : null,
            xirrAvailable,
            xirrAvailable ? null : "Not enough transaction history yet to compute XIRR - add at least one investment and either a redemption or a current NAV."));
    }

    private static async Task<IResult?> ValidateAccountAsync(Guid? accountId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!accountId.HasValue) return null;

        var exists = await db.SwalekhaAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId.Value, cancellationToken);
        return exists ? null : Results.BadRequest(new { message = "Linked account not found." });
    }

    private static async Task<IResult?> DebitAccountAsync(Guid accountId, decimal amount, DateTime date, string narration, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

        account.CurrentBalance -= amount;
        account.UpdatedAt = DateTime.UtcNow;
        db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
        {
            AccountId = account.Id,
            TransactionType = SwalekhaTransactionType.Withdrawal,
            Amount = amount,
            TransactionDate = date,
            Narration = narration,
            RunningBalance = account.CurrentBalance
        });

        return null;
    }

    private static async Task<IResult?> CreditAccountAsync(Guid accountId, decimal amount, DateTime date, string narration, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

        account.CurrentBalance += amount;
        account.UpdatedAt = DateTime.UtcNow;
        db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
        {
            AccountId = account.Id,
            TransactionType = SwalekhaTransactionType.Deposit,
            Amount = amount,
            TransactionDate = date,
            Narration = narration,
            RunningBalance = account.CurrentBalance
        });

        return null;
    }

    private static SwalekhaMutualFundDto ToDto(SwalekhaMutualFund fund)
    {
        var currentValue = fund.CurrentNav.HasValue ? fund.CurrentUnits * fund.CurrentNav.Value : (decimal?)null;
        var absoluteReturn = currentValue.HasValue ? currentValue.Value - fund.TotalInvested : (decimal?)null;
        var returnPercent = currentValue.HasValue && fund.TotalInvested > 0 ? absoluteReturn!.Value / fund.TotalInvested * 100 : (decimal?)null;

        var today = DateTime.UtcNow;
        var sipDue = fund.InvestmentMode == SwalekhaMutualFundInvestmentMode.SIP
            && (fund.LastSipInstallmentDate is null
                || fund.LastSipInstallmentDate.Value.Year != today.Year
                || fund.LastSipInstallmentDate.Value.Month != today.Month);

        return new SwalekhaMutualFundDto(
            fund.Id,
            fund.SchemeName,
            fund.Amc,
            fund.FolioNumber,
            fund.AccountId,
            fund.InvestmentMode.ToString(),
            fund.SipAmount,
            fund.SipDayOfMonth,
            fund.LastSipInstallmentDate,
            sipDue,
            fund.CurrentNav,
            fund.CurrentNavUpdatedAt,
            fund.CurrentUnits,
            fund.TotalInvested,
            currentValue,
            absoluteReturn,
            returnPercent,
            fund.IsActive,
            fund.Notes,
            fund.CreatedAt);
    }

    private static SwalekhaMutualFundTransactionDto ToDto(SwalekhaMutualFundTransaction entry) => new(
        entry.Id,
        entry.FundId,
        entry.TransactionType.ToString(),
        entry.TransactionDate,
        entry.Units,
        entry.NavAtTransaction,
        entry.Amount,
        entry.Narration,
        entry.CreatedAt);
}

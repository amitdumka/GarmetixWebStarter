using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaShareHoldingDto(
    Guid Id,
    string Symbol,
    string? CompanyName,
    string? Exchange,
    string? DematAccount,
    string? Broker,
    Guid? AccountId,
    decimal? CurrentPrice,
    DateTime? CurrentPriceUpdatedAt,
    decimal CurrentQuantity,
    decimal TotalInvested,
    decimal RealizedPnL,
    decimal? CurrentValue,
    decimal? UnrealizedPnL,
    decimal? UnrealizedPnLPercent,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaShareHoldingPayload(
    string Symbol,
    string? CompanyName,
    string? Exchange,
    string? DematAccount,
    string? Broker,
    Guid? AccountId,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaUpdateSharePricePayload(decimal CurrentPrice);

public sealed record SwalekhaShareTransactionDto(
    Guid Id,
    Guid HoldingId,
    string TransactionType,
    DateTime TransactionDate,
    decimal Quantity,
    decimal PricePerShare,
    decimal Amount,
    string Narration,
    decimal? RealizedPnLOnSale,
    DateTime CreatedAt);

public sealed record SwalekhaShareTransactionPayload(
    string TransactionType,
    DateTime TransactionDate,
    decimal Quantity,
    decimal PricePerShare,
    string? Narration);

public sealed record SwalekhaShareTransactionList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaShareTransactionDto> Rows);

/// <summary>
/// PersonalFin_10 - Investments III (Shares/Stocks). Same integration pattern as PersonalFin_09's
/// Mutual Funds: Buy debits the linked account and grows the position, Sell credits it back,
/// reduces TotalInvested at average cost per share, and adds the realized gain/loss to
/// RealizedPnL. CurrentPrice is manually updated and drives unrealized P&amp;L.
/// </summary>
public static class SwalekhaShareEndpoints
{
    public static RouteGroupBuilder MapSwalekhaShareEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/shares")
            .WithTags("Swalekha Investments")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListHoldingsAsync);
        group.MapGet("/{id:guid}", GetHoldingAsync);
        group.MapPost("/", CreateHoldingAsync);
        group.MapPut("/{id:guid}", UpdateHoldingAsync);
        group.MapDelete("/{id:guid}", DeleteHoldingAsync);
        group.MapPut("/{id:guid}/price", UpdatePriceAsync);

        group.MapGet("/{id:guid}/transactions", ListTransactionsAsync);
        group.MapPost("/{id:guid}/transactions", AddTransactionAsync);
        group.MapDelete("/{id:guid}/transactions/{transactionId:guid}", DeleteTransactionAsync);

        return group;
    }

    private static async Task<IResult> ListHoldingsAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaShareHoldings.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(h => h.IsActive);
        }

        var holdings = await query.OrderBy(h => h.Symbol).ToListAsync(cancellationToken);
        return Results.Ok(holdings.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetHoldingAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var holding = await db.SwalekhaShareHoldings.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        return holding is null ? Results.NotFound() : Results.Ok(ToDto(holding));
    }

    private static async Task<IResult> CreateHoldingAsync(SwalekhaShareHoldingPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Symbol))
        {
            return Results.BadRequest(new { message = "Symbol is required." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        var holding = new SwalekhaShareHolding
        {
            Symbol = payload.Symbol.Trim().ToUpperInvariant(),
            CompanyName = payload.CompanyName,
            Exchange = payload.Exchange,
            DematAccount = payload.DematAccount,
            Broker = payload.Broker,
            AccountId = payload.AccountId,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaShareHoldings.Add(holding);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/shares/{holding.Id}", ToDto(holding));
    }

    private static async Task<IResult> UpdateHoldingAsync(Guid id, SwalekhaShareHoldingPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var holding = await db.SwalekhaShareHoldings.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (holding is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Symbol))
        {
            return Results.BadRequest(new { message = "Symbol is required." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        holding.Symbol = payload.Symbol.Trim().ToUpperInvariant();
        holding.CompanyName = payload.CompanyName;
        holding.Exchange = payload.Exchange;
        holding.DematAccount = payload.DematAccount;
        holding.Broker = payload.Broker;
        holding.AccountId = payload.AccountId;
        holding.IsActive = payload.IsActive;
        holding.Notes = payload.Notes;
        holding.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(holding));
    }

    private static async Task<IResult> DeleteHoldingAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var holding = await db.SwalekhaShareHoldings.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (holding is null) return Results.NotFound();

        holding.Deleted = true;
        holding.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdatePriceAsync(Guid id, SwalekhaUpdateSharePricePayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var holding = await db.SwalekhaShareHoldings.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (holding is null) return Results.NotFound();

        if (payload.CurrentPrice <= 0)
        {
            return Results.BadRequest(new { message = "Price must be greater than zero." });
        }

        holding.CurrentPrice = payload.CurrentPrice;
        holding.CurrentPriceUpdatedAt = DateTime.UtcNow;
        holding.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(holding));
    }

    private static async Task<IResult> ListTransactionsAsync(Guid id, SwalekhaDbContext db, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var holdingExists = await db.SwalekhaShareHoldings.AsNoTracking().AnyAsync(h => h.Id == id, cancellationToken);
        if (!holdingExists) return Results.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        var query = db.SwalekhaShareTransactions.AsNoTracking()
            .Where(t => t.HoldingId == id)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize).ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaShareTransactionList(effectivePage, effectivePageSize, totalCount, rows.Select(ToDto).ToList()));
    }

    private static async Task<IResult> AddTransactionAsync(Guid id, SwalekhaShareTransactionPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SwalekhaShareTransactionType>(payload.TransactionType, true, out var type))
        {
            return Results.BadRequest(new { message = $"Unknown transaction type '{payload.TransactionType}'." });
        }

        if (payload.Quantity <= 0)
        {
            return Results.BadRequest(new { message = "Quantity must be greater than zero." });
        }

        if (payload.PricePerShare <= 0)
        {
            return Results.BadRequest(new { message = "Price per share must be greater than zero." });
        }

        var holding = await db.SwalekhaShareHoldings.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (holding is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var amount = payload.Quantity * payload.PricePerShare;
        var entry = new SwalekhaShareTransaction
        {
            HoldingId = holding.Id,
            TransactionType = type,
            TransactionDate = payload.TransactionDate,
            Quantity = payload.Quantity,
            PricePerShare = payload.PricePerShare,
            Amount = amount,
            Narration = payload.Narration ?? string.Empty
        };

        if (type == SwalekhaShareTransactionType.Sell)
        {
            if (payload.Quantity > holding.CurrentQuantity)
            {
                return Results.BadRequest(new { message = $"Cannot sell {payload.Quantity} shares - only {holding.CurrentQuantity} held." });
            }

            var investedRemoved = holding.CurrentQuantity > 0 ? holding.TotalInvested * (payload.Quantity / holding.CurrentQuantity) : 0m;
            var realizedPnl = amount - investedRemoved;

            entry.InvestedAmountRemoved = investedRemoved;
            entry.RealizedPnLOnSale = realizedPnl;

            holding.CurrentQuantity -= payload.Quantity;
            holding.TotalInvested -= investedRemoved;
            holding.RealizedPnL += realizedPnl;

            if (holding.AccountId.HasValue)
            {
                var accountError = await CreditAccountAsync(holding.AccountId.Value, amount, payload.TransactionDate,
                    string.IsNullOrWhiteSpace(payload.Narration) ? $"Share sale - {holding.Symbol}" : payload.Narration.Trim(), db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }
        else
        {
            holding.CurrentQuantity += payload.Quantity;
            holding.TotalInvested += amount;

            if (holding.AccountId.HasValue)
            {
                var accountError = await DebitAccountAsync(holding.AccountId.Value, amount, payload.TransactionDate,
                    string.IsNullOrWhiteSpace(payload.Narration) ? $"Share purchase - {holding.Symbol}" : payload.Narration.Trim(), db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }

        holding.UpdatedAt = DateTime.UtcNow;
        db.SwalekhaShareTransactions.Add(entry);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/swalekha/shares/{id}/transactions/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> DeleteTransactionAsync(Guid id, Guid transactionId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaShareTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.HoldingId == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        var holding = await db.SwalekhaShareHoldings.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (holding is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (entry.TransactionType == SwalekhaShareTransactionType.Sell)
        {
            holding.CurrentQuantity += entry.Quantity;
            holding.TotalInvested += entry.InvestedAmountRemoved ?? 0m;
            holding.RealizedPnL -= entry.RealizedPnLOnSale ?? 0m;

            if (holding.AccountId.HasValue)
            {
                var accountError = await DebitAccountAsync(holding.AccountId.Value, entry.Amount, DateTime.UtcNow,
                    $"Reversal of share sale - {holding.Symbol}", db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }
        else
        {
            holding.CurrentQuantity -= entry.Quantity;
            holding.TotalInvested -= entry.Amount;

            if (holding.AccountId.HasValue)
            {
                var accountError = await CreditAccountAsync(holding.AccountId.Value, entry.Amount, DateTime.UtcNow,
                    $"Reversal of share purchase - {holding.Symbol}", db, cancellationToken);
                if (accountError is not null) return accountError;
            }
        }

        holding.UpdatedAt = DateTime.UtcNow;
        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.NoContent();
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

    private static SwalekhaShareHoldingDto ToDto(SwalekhaShareHolding holding)
    {
        var currentValue = holding.CurrentPrice.HasValue ? holding.CurrentQuantity * holding.CurrentPrice.Value : (decimal?)null;
        var unrealizedPnl = currentValue.HasValue ? currentValue.Value - holding.TotalInvested : (decimal?)null;
        var unrealizedPnlPercent = currentValue.HasValue && holding.TotalInvested > 0 ? unrealizedPnl!.Value / holding.TotalInvested * 100 : (decimal?)null;

        return new SwalekhaShareHoldingDto(
            holding.Id,
            holding.Symbol,
            holding.CompanyName,
            holding.Exchange,
            holding.DematAccount,
            holding.Broker,
            holding.AccountId,
            holding.CurrentPrice,
            holding.CurrentPriceUpdatedAt,
            holding.CurrentQuantity,
            holding.TotalInvested,
            holding.RealizedPnL,
            currentValue,
            unrealizedPnl,
            unrealizedPnlPercent,
            holding.IsActive,
            holding.Notes,
            holding.CreatedAt);
    }

    private static SwalekhaShareTransactionDto ToDto(SwalekhaShareTransaction entry) => new(
        entry.Id,
        entry.HoldingId,
        entry.TransactionType.ToString(),
        entry.TransactionDate,
        entry.Quantity,
        entry.PricePerShare,
        entry.Amount,
        entry.Narration,
        entry.RealizedPnLOnSale,
        entry.CreatedAt);
}

using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaAccountDto(
    Guid Id,
    string Name,
    string AccountType,
    string? BankName,
    string? AccountNumberMasked,
    string? Ifsc,
    decimal? CreditLimit,
    int? StatementDayOfMonth,
    int? DueDayOfMonth,
    decimal OpeningBalance,
    decimal CurrentBalance,
    string Currency,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaAccountPayload(
    string Name,
    string AccountType,
    string? BankName,
    string? AccountNumberMasked,
    string? Ifsc,
    decimal? CreditLimit,
    int? StatementDayOfMonth,
    int? DueDayOfMonth,
    decimal OpeningBalance,
    string? Currency,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaTransactionDto(
    Guid Id,
    Guid AccountId,
    string TransactionType,
    decimal Amount,
    DateTime TransactionDate,
    string Narration,
    Guid? CounterAccountId,
    string? CounterAccountName,
    decimal RunningBalance,
    Guid? TransferGroupId,
    DateTime CreatedAt);

public sealed record SwalekhaTransactionPayload(
    decimal Amount,
    DateTime TransactionDate,
    string Narration,
    string TransactionType);

public sealed record SwalekhaTransactionList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaTransactionDto> Rows);

public sealed record SwalekhaTransferPayload(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    DateTime TransactionDate,
    string? Narration);

public sealed record SwalekhaTransferResult(SwalekhaAccountDto FromAccount, SwalekhaAccountDto ToAccount);

/// <summary>
/// PersonalFin_02 - Accounts Hub Core. CRUD for bank/cash/credit-card accounts, a per-account
/// transaction ledger (deposit/withdrawal), and transfers between two Swalekha accounts.
/// CurrentBalance on SwalekhaAccount is the live source of truth, always kept in lockstep with
/// every transaction insert/delete inside a single DB transaction - never recomputed from
/// history on read.
/// </summary>
public static class SwalekhaAccountsEndpoints
{
    public static RouteGroupBuilder MapSwalekhaAccountsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/accounts")
            .WithTags("Swalekha Accounts")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListAccountsAsync);
        group.MapGet("/{id:guid}", GetAccountAsync);
        group.MapPost("/", CreateAccountAsync);
        group.MapPut("/{id:guid}", UpdateAccountAsync);
        group.MapDelete("/{id:guid}", DeleteAccountAsync);

        group.MapGet("/{id:guid}/transactions", ListTransactionsAsync);
        group.MapPost("/{id:guid}/transactions", AddTransactionAsync);
        group.MapDelete("/{id:guid}/transactions/{transactionId:guid}", DeleteTransactionAsync);

        app.MapPost("/api/swalekha/transfers", CreateTransferAsync)
            .WithTags("Swalekha Accounts")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        return group;
    }

    private static async Task<IResult> ListAccountsAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaAccounts.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(account => account.IsActive);
        }

        var accounts = await query.OrderBy(account => account.Name).ToListAsync(cancellationToken);
        return Results.Ok(accounts.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetAccountAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var account = await db.SwalekhaAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return account is null ? Results.NotFound() : Results.Ok(ToDto(account));
    }

    private static async Task<IResult> CreateAccountAsync(SwalekhaAccountPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Account name is required." });
        }

        if (!Enum.TryParse<SwalekhaAccountType>(payload.AccountType, true, out var accountType))
        {
            return Results.BadRequest(new { message = $"Unknown account type '{payload.AccountType}'." });
        }

        var account = new SwalekhaAccount
        {
            Name = payload.Name.Trim(),
            AccountType = accountType,
            BankName = payload.BankName,
            AccountNumberMasked = payload.AccountNumberMasked,
            Ifsc = payload.Ifsc,
            CreditLimit = payload.CreditLimit,
            StatementDayOfMonth = payload.StatementDayOfMonth,
            DueDayOfMonth = payload.DueDayOfMonth,
            OpeningBalance = payload.OpeningBalance,
            CurrentBalance = payload.OpeningBalance,
            Currency = string.IsNullOrWhiteSpace(payload.Currency) ? "INR" : payload.Currency,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/accounts/{account.Id}", ToDto(account));
    }

    private static async Task<IResult> UpdateAccountAsync(Guid id, SwalekhaAccountPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Account name is required." });
        }

        if (!Enum.TryParse<SwalekhaAccountType>(payload.AccountType, true, out var accountType))
        {
            return Results.BadRequest(new { message = $"Unknown account type '{payload.AccountType}'." });
        }

        // Correcting the opening balance after transactions already exist shouldn't silently
        // discard the ledger history - shift CurrentBalance by the same delta instead of
        // overwriting it, so every past transaction's running balance stays meaningful.
        var openingBalanceDelta = payload.OpeningBalance - account.OpeningBalance;

        account.Name = payload.Name.Trim();
        account.AccountType = accountType;
        account.BankName = payload.BankName;
        account.AccountNumberMasked = payload.AccountNumberMasked;
        account.Ifsc = payload.Ifsc;
        account.CreditLimit = payload.CreditLimit;
        account.StatementDayOfMonth = payload.StatementDayOfMonth;
        account.DueDayOfMonth = payload.DueDayOfMonth;
        account.OpeningBalance = payload.OpeningBalance;
        account.CurrentBalance += openingBalanceDelta;
        account.Currency = string.IsNullOrWhiteSpace(payload.Currency) ? "INR" : payload.Currency;
        account.IsActive = payload.IsActive;
        account.Notes = payload.Notes;
        account.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(account));
    }

    private static async Task<IResult> DeleteAccountAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null) return Results.NotFound();

        account.Deleted = true;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListTransactionsAsync(Guid id, SwalekhaDbContext db, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var accountExists = await db.SwalekhaAccounts.AsNoTracking().AnyAsync(a => a.Id == id, cancellationToken);
        if (!accountExists) return Results.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        var query = db.SwalekhaAccountTransactions.AsNoTracking()
            .Where(t => t.AccountId == id)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        var counterAccountIds = rows.Where(r => r.CounterAccountId.HasValue).Select(r => r.CounterAccountId!.Value).Distinct().ToList();
        var counterNames = counterAccountIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.SwalekhaAccounts.AsNoTracking()
                .Where(a => counterAccountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        var dtoRows = rows.Select(t => ToDto(t, t.CounterAccountId.HasValue && counterNames.TryGetValue(t.CounterAccountId.Value, out var name) ? name : null)).ToList();
        return Results.Ok(new SwalekhaTransactionList(effectivePage, effectivePageSize, totalCount, dtoRows));
    }

    private static async Task<IResult> AddTransactionAsync(Guid id, SwalekhaTransactionPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SwalekhaTransactionType>(payload.TransactionType, true, out var transactionType)
            || (transactionType != SwalekhaTransactionType.Deposit && transactionType != SwalekhaTransactionType.Withdrawal))
        {
            return Results.BadRequest(new { message = "TransactionType must be Deposit or Withdrawal. Use POST /api/swalekha/transfers for transfers." });
        }

        if (payload.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Amount must be greater than zero." });
        }

        var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        account.CurrentBalance += transactionType == SwalekhaTransactionType.Deposit ? payload.Amount : -payload.Amount;
        account.UpdatedAt = DateTime.UtcNow;

        var entry = new SwalekhaAccountTransaction
        {
            AccountId = account.Id,
            TransactionType = transactionType,
            Amount = payload.Amount,
            TransactionDate = payload.TransactionDate,
            Narration = payload.Narration ?? string.Empty,
            RunningBalance = account.CurrentBalance
        };
        db.SwalekhaAccountTransactions.Add(entry);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/swalekha/accounts/{id}/transactions/{entry.Id}", ToDto(entry, null));
    }

    private static async Task<IResult> DeleteTransactionAsync(Guid id, Guid transactionId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaAccountTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.AccountId == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (account is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        ReverseTransactionBalance(account, entry);
        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;

        // A transfer has two linked legs (TransferOut on the source account, TransferIn on the
        // destination account) sharing TransferGroupId - deleting one leg without the other
        // would leave one account's balance permanently wrong, so both are reversed together.
        if (entry.TransferGroupId.HasValue)
        {
            var pairedEntry = await db.SwalekhaAccountTransactions
                .FirstOrDefaultAsync(t => t.TransferGroupId == entry.TransferGroupId && t.Id != entry.Id, cancellationToken);
            if (pairedEntry is not null)
            {
                var pairedAccount = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == pairedEntry.AccountId, cancellationToken);
                if (pairedAccount is not null)
                {
                    ReverseTransactionBalance(pairedAccount, pairedEntry);
                    pairedAccount.UpdatedAt = DateTime.UtcNow;
                }
                pairedEntry.Deleted = true;
                pairedEntry.UpdatedAt = DateTime.UtcNow;
            }
        }

        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static void ReverseTransactionBalance(SwalekhaAccount account, SwalekhaAccountTransaction entry)
    {
        account.CurrentBalance += entry.TransactionType switch
        {
            SwalekhaTransactionType.Deposit => -entry.Amount,
            SwalekhaTransactionType.Withdrawal => entry.Amount,
            SwalekhaTransactionType.TransferOut => entry.Amount,
            SwalekhaTransactionType.TransferIn => -entry.Amount,
            _ => 0
        };
    }

    private static async Task<IResult> CreateTransferAsync(SwalekhaTransferPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (payload.FromAccountId == payload.ToAccountId)
        {
            return Results.BadRequest(new { message = "From and To accounts must be different." });
        }

        if (payload.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Amount must be greater than zero." });
        }

        var fromAccount = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == payload.FromAccountId, cancellationToken);
        var toAccount = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == payload.ToAccountId, cancellationToken);
        if (fromAccount is null || toAccount is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var transferGroupId = Guid.NewGuid();
        var narration = string.IsNullOrWhiteSpace(payload.Narration) ? "Transfer" : payload.Narration.Trim();

        fromAccount.CurrentBalance -= payload.Amount;
        fromAccount.UpdatedAt = DateTime.UtcNow;
        db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
        {
            AccountId = fromAccount.Id,
            TransactionType = SwalekhaTransactionType.TransferOut,
            Amount = payload.Amount,
            TransactionDate = payload.TransactionDate,
            Narration = narration,
            CounterAccountId = toAccount.Id,
            RunningBalance = fromAccount.CurrentBalance,
            TransferGroupId = transferGroupId
        });

        toAccount.CurrentBalance += payload.Amount;
        toAccount.UpdatedAt = DateTime.UtcNow;
        db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
        {
            AccountId = toAccount.Id,
            TransactionType = SwalekhaTransactionType.TransferIn,
            Amount = payload.Amount,
            TransactionDate = payload.TransactionDate,
            Narration = narration,
            CounterAccountId = fromAccount.Id,
            RunningBalance = toAccount.CurrentBalance,
            TransferGroupId = transferGroupId
        });

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(new SwalekhaTransferResult(ToDto(fromAccount), ToDto(toAccount)));
    }

    private static SwalekhaAccountDto ToDto(SwalekhaAccount account) => new(
        account.Id,
        account.Name,
        account.AccountType.ToString(),
        account.BankName,
        account.AccountNumberMasked,
        account.Ifsc,
        account.CreditLimit,
        account.StatementDayOfMonth,
        account.DueDayOfMonth,
        account.OpeningBalance,
        account.CurrentBalance,
        account.Currency,
        account.IsActive,
        account.Notes,
        account.CreatedAt);

    private static SwalekhaTransactionDto ToDto(SwalekhaAccountTransaction entry, string? counterAccountName) => new(
        entry.Id,
        entry.AccountId,
        entry.TransactionType.ToString(),
        entry.Amount,
        entry.TransactionDate,
        entry.Narration,
        entry.CounterAccountId,
        counterAccountName,
        entry.RunningBalance,
        entry.TransferGroupId,
        entry.CreatedAt);
}

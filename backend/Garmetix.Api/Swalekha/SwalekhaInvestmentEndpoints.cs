using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaFixedDepositDto(
    Guid Id,
    string BankName,
    string? FdNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    bool AutoRenew,
    decimal? TdsDeducted,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaFixedDepositPayload(
    string BankName,
    string? FdNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    bool AutoRenew,
    decimal? TdsDeducted,
    string? Notes);

public sealed record SwalekhaMarkMaturedPayload(decimal MaturityAmount, DateTime MaturityCreditedDate, string? Narration);

public sealed record SwalekhaRecurringDepositDto(
    Guid Id,
    string BankName,
    string? RdNumber,
    Guid? AccountId,
    decimal MonthlyInstallment,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    int InstallmentsPaid,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaRecurringDepositPayload(
    string BankName,
    string? RdNumber,
    Guid? AccountId,
    decimal MonthlyInstallment,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    string? Notes);

public sealed record SwalekhaRecordInstallmentPayload(DateTime InstallmentDate, string? Narration);

/// <summary>
/// PersonalFin_08 - Investments I (Fixed Deposits + Recurring Deposits). Both link optionally to
/// one of the Owner's own SwalekhaAccounts - MarkMatured credits that account with the maturity
/// payout (matching PersonalFin_05's Trip-close roll-up pattern), and RD's RecordInstallment
/// debits it per installment, so these genuinely integrate with the Accounts Hub ledger instead
/// of being disconnected trackers.
/// </summary>
public static class SwalekhaInvestmentEndpoints
{
    public static RouteGroupBuilder MapSwalekhaInvestmentEndpoints(this WebApplication app)
    {
        var fdGroup = app.MapGroup("/api/swalekha/fixed-deposits")
            .WithTags("Swalekha Investments")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        fdGroup.MapGet("/", ListFixedDepositsAsync);
        fdGroup.MapGet("/{id:guid}", GetFixedDepositAsync);
        fdGroup.MapPost("/", CreateFixedDepositAsync);
        fdGroup.MapPut("/{id:guid}", UpdateFixedDepositAsync);
        fdGroup.MapDelete("/{id:guid}", DeleteFixedDepositAsync);
        fdGroup.MapPost("/{id:guid}/mark-matured", MarkFixedDepositMaturedAsync);

        var rdGroup = app.MapGroup("/api/swalekha/recurring-deposits")
            .WithTags("Swalekha Investments")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        rdGroup.MapGet("/", ListRecurringDepositsAsync);
        rdGroup.MapGet("/{id:guid}", GetRecurringDepositAsync);
        rdGroup.MapPost("/", CreateRecurringDepositAsync);
        rdGroup.MapPut("/{id:guid}", UpdateRecurringDepositAsync);
        rdGroup.MapDelete("/{id:guid}", DeleteRecurringDepositAsync);
        rdGroup.MapPost("/{id:guid}/record-installment", RecordInstallmentAsync);
        rdGroup.MapPost("/{id:guid}/mark-matured", MarkRecurringDepositMaturedAsync);

        return fdGroup;
    }

    // ----- Fixed Deposits -----

    private static async Task<IResult> ListFixedDepositsAsync(SwalekhaDbContext db, bool? includeClosed, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaFixedDeposits.AsNoTracking().AsQueryable();
        if (includeClosed != true)
        {
            query = query.Where(fd => !fd.IsClosed);
        }

        var deposits = await query.OrderBy(fd => fd.MaturityDate).ToListAsync(cancellationToken);
        return Results.Ok(deposits.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetFixedDepositAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaFixedDeposits.AsNoTracking().FirstOrDefaultAsync(fd => fd.Id == id, cancellationToken);
        return deposit is null ? Results.NotFound() : Results.Ok(ToDto(deposit));
    }

    private static async Task<IResult> CreateFixedDepositAsync(SwalekhaFixedDepositPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.BankName))
        {
            return Results.BadRequest(new { message = "Bank name is required." });
        }

        if (payload.PrincipalAmount <= 0)
        {
            return Results.BadRequest(new { message = "Principal amount must be greater than zero." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        var deposit = new SwalekhaFixedDeposit
        {
            BankName = payload.BankName.Trim(),
            FdNumber = payload.FdNumber,
            AccountId = payload.AccountId,
            PrincipalAmount = payload.PrincipalAmount,
            InterestRatePercent = payload.InterestRatePercent,
            TenureMonths = payload.TenureMonths,
            StartDate = payload.StartDate,
            MaturityDate = payload.MaturityDate,
            MaturityAmount = payload.MaturityAmount,
            AutoRenew = payload.AutoRenew,
            TdsDeducted = payload.TdsDeducted,
            Notes = payload.Notes
        };

        db.SwalekhaFixedDeposits.Add(deposit);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/fixed-deposits/{deposit.Id}", ToDto(deposit));
    }

    private static async Task<IResult> UpdateFixedDepositAsync(Guid id, SwalekhaFixedDepositPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaFixedDeposits.FirstOrDefaultAsync(fd => fd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.BankName))
        {
            return Results.BadRequest(new { message = "Bank name is required." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        deposit.BankName = payload.BankName.Trim();
        deposit.FdNumber = payload.FdNumber;
        deposit.AccountId = payload.AccountId;
        deposit.PrincipalAmount = payload.PrincipalAmount;
        deposit.InterestRatePercent = payload.InterestRatePercent;
        deposit.TenureMonths = payload.TenureMonths;
        deposit.StartDate = payload.StartDate;
        deposit.MaturityDate = payload.MaturityDate;
        deposit.MaturityAmount = payload.MaturityAmount;
        deposit.AutoRenew = payload.AutoRenew;
        deposit.TdsDeducted = payload.TdsDeducted;
        deposit.Notes = payload.Notes;
        deposit.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(deposit));
    }

    private static async Task<IResult> DeleteFixedDepositAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaFixedDeposits.FirstOrDefaultAsync(fd => fd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        deposit.Deleted = true;
        deposit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkFixedDepositMaturedAsync(Guid id, SwalekhaMarkMaturedPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaFixedDeposits.FirstOrDefaultAsync(fd => fd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        if (deposit.IsClosed)
        {
            return Results.BadRequest(new { message = "This fixed deposit is already marked matured." });
        }

        if (payload.MaturityAmount <= 0)
        {
            return Results.BadRequest(new { message = "Maturity amount must be greater than zero." });
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        deposit.IsClosed = true;
        deposit.ClosedAt = DateTime.UtcNow;
        deposit.MaturityAmount = payload.MaturityAmount;
        deposit.UpdatedAt = DateTime.UtcNow;

        if (deposit.AccountId.HasValue)
        {
            var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == deposit.AccountId.Value, cancellationToken);
            if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

            account.CurrentBalance += payload.MaturityAmount;
            account.UpdatedAt = DateTime.UtcNow;
            db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
            {
                AccountId = account.Id,
                TransactionType = SwalekhaTransactionType.Deposit,
                Amount = payload.MaturityAmount,
                TransactionDate = payload.MaturityCreditedDate,
                Narration = string.IsNullOrWhiteSpace(payload.Narration) ? $"FD maturity - {deposit.BankName} {deposit.FdNumber}".Trim() : payload.Narration.Trim(),
                RunningBalance = account.CurrentBalance
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(ToDto(deposit));
    }

    // ----- Recurring Deposits -----

    private static async Task<IResult> ListRecurringDepositsAsync(SwalekhaDbContext db, bool? includeClosed, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaRecurringDeposits.AsNoTracking().AsQueryable();
        if (includeClosed != true)
        {
            query = query.Where(rd => !rd.IsClosed);
        }

        var deposits = await query.OrderBy(rd => rd.MaturityDate).ToListAsync(cancellationToken);
        return Results.Ok(deposits.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetRecurringDepositAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaRecurringDeposits.AsNoTracking().FirstOrDefaultAsync(rd => rd.Id == id, cancellationToken);
        return deposit is null ? Results.NotFound() : Results.Ok(ToDto(deposit));
    }

    private static async Task<IResult> CreateRecurringDepositAsync(SwalekhaRecurringDepositPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.BankName))
        {
            return Results.BadRequest(new { message = "Bank name is required." });
        }

        if (payload.MonthlyInstallment <= 0)
        {
            return Results.BadRequest(new { message = "Monthly installment must be greater than zero." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        var deposit = new SwalekhaRecurringDeposit
        {
            BankName = payload.BankName.Trim(),
            RdNumber = payload.RdNumber,
            AccountId = payload.AccountId,
            MonthlyInstallment = payload.MonthlyInstallment,
            InterestRatePercent = payload.InterestRatePercent,
            TenureMonths = payload.TenureMonths,
            StartDate = payload.StartDate,
            MaturityDate = payload.MaturityDate,
            MaturityAmount = payload.MaturityAmount,
            Notes = payload.Notes
        };

        db.SwalekhaRecurringDeposits.Add(deposit);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/recurring-deposits/{deposit.Id}", ToDto(deposit));
    }

    private static async Task<IResult> UpdateRecurringDepositAsync(Guid id, SwalekhaRecurringDepositPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaRecurringDeposits.FirstOrDefaultAsync(rd => rd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.BankName))
        {
            return Results.BadRequest(new { message = "Bank name is required." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        deposit.BankName = payload.BankName.Trim();
        deposit.RdNumber = payload.RdNumber;
        deposit.AccountId = payload.AccountId;
        deposit.MonthlyInstallment = payload.MonthlyInstallment;
        deposit.InterestRatePercent = payload.InterestRatePercent;
        deposit.TenureMonths = payload.TenureMonths;
        deposit.StartDate = payload.StartDate;
        deposit.MaturityDate = payload.MaturityDate;
        deposit.MaturityAmount = payload.MaturityAmount;
        deposit.Notes = payload.Notes;
        deposit.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(deposit));
    }

    private static async Task<IResult> DeleteRecurringDepositAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaRecurringDeposits.FirstOrDefaultAsync(rd => rd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        deposit.Deleted = true;
        deposit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> RecordInstallmentAsync(Guid id, SwalekhaRecordInstallmentPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaRecurringDeposits.FirstOrDefaultAsync(rd => rd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        if (deposit.IsClosed)
        {
            return Results.BadRequest(new { message = "This recurring deposit is already closed." });
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        deposit.InstallmentsPaid += 1;
        deposit.UpdatedAt = DateTime.UtcNow;

        if (deposit.AccountId.HasValue)
        {
            var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == deposit.AccountId.Value, cancellationToken);
            if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

            account.CurrentBalance -= deposit.MonthlyInstallment;
            account.UpdatedAt = DateTime.UtcNow;
            db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
            {
                AccountId = account.Id,
                TransactionType = SwalekhaTransactionType.Withdrawal,
                Amount = deposit.MonthlyInstallment,
                TransactionDate = payload.InstallmentDate,
                Narration = string.IsNullOrWhiteSpace(payload.Narration)
                    ? $"RD installment #{deposit.InstallmentsPaid} - {deposit.BankName} {deposit.RdNumber}".Trim()
                    : payload.Narration.Trim(),
                RunningBalance = account.CurrentBalance
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(ToDto(deposit));
    }

    private static async Task<IResult> MarkRecurringDepositMaturedAsync(Guid id, SwalekhaMarkMaturedPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var deposit = await db.SwalekhaRecurringDeposits.FirstOrDefaultAsync(rd => rd.Id == id, cancellationToken);
        if (deposit is null) return Results.NotFound();

        if (deposit.IsClosed)
        {
            return Results.BadRequest(new { message = "This recurring deposit is already marked matured." });
        }

        if (payload.MaturityAmount <= 0)
        {
            return Results.BadRequest(new { message = "Maturity amount must be greater than zero." });
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        deposit.IsClosed = true;
        deposit.ClosedAt = DateTime.UtcNow;
        deposit.MaturityAmount = payload.MaturityAmount;
        deposit.UpdatedAt = DateTime.UtcNow;

        if (deposit.AccountId.HasValue)
        {
            var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == deposit.AccountId.Value, cancellationToken);
            if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

            account.CurrentBalance += payload.MaturityAmount;
            account.UpdatedAt = DateTime.UtcNow;
            db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
            {
                AccountId = account.Id,
                TransactionType = SwalekhaTransactionType.Deposit,
                Amount = payload.MaturityAmount,
                TransactionDate = payload.MaturityCreditedDate,
                Narration = string.IsNullOrWhiteSpace(payload.Narration) ? $"RD maturity - {deposit.BankName} {deposit.RdNumber}".Trim() : payload.Narration.Trim(),
                RunningBalance = account.CurrentBalance
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(ToDto(deposit));
    }

    // ----- Shared -----

    private static async Task<IResult?> ValidateAccountAsync(Guid? accountId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!accountId.HasValue) return null;

        var exists = await db.SwalekhaAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId.Value, cancellationToken);
        return exists ? null : Results.BadRequest(new { message = "Linked account not found." });
    }

    private static SwalekhaFixedDepositDto ToDto(SwalekhaFixedDeposit deposit) => new(
        deposit.Id,
        deposit.BankName,
        deposit.FdNumber,
        deposit.AccountId,
        deposit.PrincipalAmount,
        deposit.InterestRatePercent,
        deposit.TenureMonths,
        deposit.StartDate,
        deposit.MaturityDate,
        deposit.MaturityAmount,
        deposit.AutoRenew,
        deposit.TdsDeducted,
        deposit.IsClosed,
        deposit.ClosedAt,
        deposit.Notes,
        deposit.CreatedAt);

    private static SwalekhaRecurringDepositDto ToDto(SwalekhaRecurringDeposit deposit) => new(
        deposit.Id,
        deposit.BankName,
        deposit.RdNumber,
        deposit.AccountId,
        deposit.MonthlyInstallment,
        deposit.InterestRatePercent,
        deposit.TenureMonths,
        deposit.StartDate,
        deposit.MaturityDate,
        deposit.MaturityAmount,
        deposit.InstallmentsPaid,
        deposit.IsClosed,
        deposit.ClosedAt,
        deposit.Notes,
        deposit.CreatedAt);
}

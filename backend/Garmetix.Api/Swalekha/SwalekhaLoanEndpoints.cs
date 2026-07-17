using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaLoanDto(
    Guid Id,
    string LoanType,
    string LenderName,
    string? LoanNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    decimal EmiAmount,
    DateTime StartDate,
    decimal OutstandingPrincipal,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaLoanPayload(
    string LoanType,
    string LenderName,
    string? LoanNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    decimal EmiAmount,
    DateTime StartDate,
    string? Notes);

public sealed record SwalekhaCalculateEmiPayload(decimal PrincipalAmount, decimal InterestRatePercent, int TenureMonths);

public sealed record SwalekhaCalculateEmiResult(decimal EmiAmount);

public sealed record SwalekhaLoanPaymentDto(
    Guid Id,
    Guid LoanId,
    string PaymentType,
    DateTime PaymentDate,
    decimal Amount,
    decimal PrincipalComponent,
    decimal InterestComponent,
    decimal OutstandingAfter,
    string Narration,
    DateTime CreatedAt);

public sealed record SwalekhaLoanPaymentPayload(
    string PaymentType,
    DateTime PaymentDate,
    decimal Amount,
    string? Narration);

public sealed record SwalekhaLoanPaymentList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaLoanPaymentDto> Rows);

/// <summary>
/// PersonalFin_11 - Loans Taken. EMI/Prepayment debits the linked account, matching the money-
/// integration pattern PersonalFin_08/09/10 established. "Loans Given" is deliberately not here -
/// the design scoped it to reuse PersonalFin_03's Person Ledger (LoanGiven entries on a Contact)
/// rather than a second, duplicated ledger.
/// </summary>
public static class SwalekhaLoanEndpoints
{
    public static RouteGroupBuilder MapSwalekhaLoanEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/loans")
            .WithTags("Swalekha Loans")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListLoansAsync);
        group.MapGet("/{id:guid}", GetLoanAsync);
        group.MapPost("/", CreateLoanAsync);
        group.MapPut("/{id:guid}", UpdateLoanAsync);
        group.MapDelete("/{id:guid}", DeleteLoanAsync);
        group.MapPost("/calculate-emi", CalculateEmiAsync);
        group.MapGet("/{id:guid}/amortization-schedule", GetAmortizationScheduleAsync);

        group.MapGet("/{id:guid}/payments", ListPaymentsAsync);
        group.MapPost("/{id:guid}/payments", AddPaymentAsync);
        group.MapDelete("/{id:guid}/payments/{paymentId:guid}", DeletePaymentAsync);

        return group;
    }

    private static async Task<IResult> ListLoansAsync(SwalekhaDbContext db, bool? includeClosed, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaLoans.AsNoTracking().AsQueryable();
        if (includeClosed != true)
        {
            query = query.Where(l => !l.IsClosed);
        }

        var loans = await query.OrderBy(l => l.LenderName).ToListAsync(cancellationToken);
        return Results.Ok(loans.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetLoanAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var loan = await db.SwalekhaLoans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        return loan is null ? Results.NotFound() : Results.Ok(ToDto(loan));
    }

    private static async Task<IResult> CreateLoanAsync(SwalekhaLoanPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.LenderName))
        {
            return Results.BadRequest(new { message = "Lender name is required." });
        }

        if (!Enum.TryParse<SwalekhaLoanType>(payload.LoanType, true, out var loanType))
        {
            return Results.BadRequest(new { message = $"Unknown loan type '{payload.LoanType}'." });
        }

        if (payload.PrincipalAmount <= 0)
        {
            return Results.BadRequest(new { message = "Principal amount must be greater than zero." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        var loan = new SwalekhaLoan
        {
            LoanType = loanType,
            LenderName = payload.LenderName.Trim(),
            LoanNumber = payload.LoanNumber,
            AccountId = payload.AccountId,
            PrincipalAmount = payload.PrincipalAmount,
            InterestRatePercent = payload.InterestRatePercent,
            TenureMonths = payload.TenureMonths,
            EmiAmount = payload.EmiAmount,
            StartDate = payload.StartDate,
            OutstandingPrincipal = payload.PrincipalAmount,
            Notes = payload.Notes
        };

        db.SwalekhaLoans.Add(loan);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/loans/{loan.Id}", ToDto(loan));
    }

    private static async Task<IResult> UpdateLoanAsync(Guid id, SwalekhaLoanPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var loan = await db.SwalekhaLoans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.LenderName))
        {
            return Results.BadRequest(new { message = "Lender name is required." });
        }

        if (!Enum.TryParse<SwalekhaLoanType>(payload.LoanType, true, out var loanType))
        {
            return Results.BadRequest(new { message = $"Unknown loan type '{payload.LoanType}'." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        // Deliberately does not touch PrincipalAmount/OutstandingPrincipal - those are only ever
        // changed via recorded payments, not by editing the loan's static details.
        loan.LoanType = loanType;
        loan.LenderName = payload.LenderName.Trim();
        loan.LoanNumber = payload.LoanNumber;
        loan.AccountId = payload.AccountId;
        loan.InterestRatePercent = payload.InterestRatePercent;
        loan.TenureMonths = payload.TenureMonths;
        loan.EmiAmount = payload.EmiAmount;
        loan.StartDate = payload.StartDate;
        loan.Notes = payload.Notes;
        loan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(loan));
    }

    private static async Task<IResult> DeleteLoanAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var loan = await db.SwalekhaLoans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null) return Results.NotFound();

        loan.Deleted = true;
        loan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Task<IResult> CalculateEmiAsync(SwalekhaCalculateEmiPayload payload, CancellationToken cancellationToken)
    {
        if (payload.PrincipalAmount <= 0 || payload.TenureMonths <= 0)
        {
            return Task.FromResult(Results.BadRequest(new { message = "Principal and tenure must be greater than zero." }));
        }

        var emi = SwalekhaLoanCalculator.CalculateEmi(payload.PrincipalAmount, payload.InterestRatePercent, payload.TenureMonths);
        return Task.FromResult(Results.Ok(new SwalekhaCalculateEmiResult(emi)));
    }

    private static async Task<IResult> GetAmortizationScheduleAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var loan = await db.SwalekhaLoans.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null) return Results.NotFound();

        var schedule = SwalekhaLoanCalculator.ProjectSchedule(loan.OutstandingPrincipal, loan.InterestRatePercent, loan.EmiAmount);
        return Results.Ok(schedule);
    }

    private static async Task<IResult> ListPaymentsAsync(Guid id, SwalekhaDbContext db, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var loanExists = await db.SwalekhaLoans.AsNoTracking().AnyAsync(l => l.Id == id, cancellationToken);
        if (!loanExists) return Results.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        var query = db.SwalekhaLoanPayments.AsNoTracking()
            .Where(p => p.LoanId == id)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize).ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaLoanPaymentList(effectivePage, effectivePageSize, totalCount, rows.Select(ToDto).ToList()));
    }

    private static async Task<IResult> AddPaymentAsync(Guid id, SwalekhaLoanPaymentPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SwalekhaLoanPaymentType>(payload.PaymentType, true, out var type))
        {
            return Results.BadRequest(new { message = $"Unknown payment type '{payload.PaymentType}'." });
        }

        if (payload.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Amount must be greater than zero." });
        }

        var loan = await db.SwalekhaLoans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null) return Results.NotFound();

        if (loan.IsClosed)
        {
            return Results.BadRequest(new { message = "This loan is already closed - reopen it first if you need to record another payment." });
        }

        decimal principalComponent;
        decimal interestComponent;

        if (type == SwalekhaLoanPaymentType.Prepayment)
        {
            principalComponent = payload.Amount;
            interestComponent = 0m;
        }
        else
        {
            var monthlyRate = loan.InterestRatePercent / 100m / 12m;
            interestComponent = Math.Round(loan.OutstandingPrincipal * monthlyRate, 2);
            principalComponent = payload.Amount - interestComponent;

            if (principalComponent < 0)
            {
                return Results.BadRequest(new { message = $"This EMI amount doesn't even cover the accrued interest of {interestComponent:0.00} for this period." });
            }
        }

        if (principalComponent > loan.OutstandingPrincipal)
        {
            principalComponent = loan.OutstandingPrincipal;
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        loan.OutstandingPrincipal -= principalComponent;
        var closesLoan = loan.OutstandingPrincipal <= 0.01m;
        if (closesLoan)
        {
            loan.OutstandingPrincipal = 0m;
            loan.IsClosed = true;
            loan.ClosedAt = DateTime.UtcNow;
        }
        loan.UpdatedAt = DateTime.UtcNow;

        var entry = new SwalekhaLoanPayment
        {
            LoanId = loan.Id,
            PaymentType = type,
            PaymentDate = payload.PaymentDate,
            Amount = payload.Amount,
            PrincipalComponent = principalComponent,
            InterestComponent = interestComponent,
            OutstandingAfter = loan.OutstandingPrincipal,
            Narration = payload.Narration ?? string.Empty
        };
        db.SwalekhaLoanPayments.Add(entry);

        if (loan.AccountId.HasValue)
        {
            var accountError = await DebitAccountAsync(loan.AccountId.Value, payload.Amount, payload.PaymentDate,
                string.IsNullOrWhiteSpace(payload.Narration) ? $"Loan {type} - {loan.LenderName}" : payload.Narration.Trim(), db, cancellationToken);
            if (accountError is not null) return accountError;
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/swalekha/loans/{id}/payments/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> DeletePaymentAsync(Guid id, Guid paymentId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaLoanPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.LoanId == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        var loan = await db.SwalekhaLoans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        loan.OutstandingPrincipal += entry.PrincipalComponent;
        if (loan.IsClosed && loan.OutstandingPrincipal > 0.01m)
        {
            loan.IsClosed = false;
            loan.ClosedAt = null;
        }
        loan.UpdatedAt = DateTime.UtcNow;

        if (loan.AccountId.HasValue)
        {
            var accountError = await CreditAccountAsync(loan.AccountId.Value, entry.Amount, DateTime.UtcNow,
                $"Reversal of loan {entry.PaymentType} - {loan.LenderName}", db, cancellationToken);
            if (accountError is not null) return accountError;
        }

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

    private static SwalekhaLoanDto ToDto(SwalekhaLoan loan) => new(
        loan.Id,
        loan.LoanType.ToString(),
        loan.LenderName,
        loan.LoanNumber,
        loan.AccountId,
        loan.PrincipalAmount,
        loan.InterestRatePercent,
        loan.TenureMonths,
        loan.EmiAmount,
        loan.StartDate,
        loan.OutstandingPrincipal,
        loan.IsClosed,
        loan.ClosedAt,
        loan.Notes,
        loan.CreatedAt);

    private static SwalekhaLoanPaymentDto ToDto(SwalekhaLoanPayment entry) => new(
        entry.Id,
        entry.LoanId,
        entry.PaymentType.ToString(),
        entry.PaymentDate,
        entry.Amount,
        entry.PrincipalComponent,
        entry.InterestComponent,
        entry.OutstandingAfter,
        entry.Narration,
        entry.CreatedAt);
}

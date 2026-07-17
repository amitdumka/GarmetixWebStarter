using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaInsurancePolicyDto(
    Guid Id,
    string PolicyType,
    string Insurer,
    string? PolicyNumber,
    Guid? AccountId,
    decimal? SumAssured,
    decimal PremiumAmount,
    string PremiumFrequency,
    DateTime StartDate,
    string? NomineeName,
    string? NomineeRelationship,
    DateTime? MaturityDate,
    decimal? MaturityAmount,
    DateTime? LastPremiumPaidDate,
    DateTime NextPremiumDueDate,
    bool PremiumDueNow,
    bool IsMatured,
    DateTime? MaturedAt,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaInsurancePolicyPayload(
    string PolicyType,
    string Insurer,
    string? PolicyNumber,
    Guid? AccountId,
    decimal? SumAssured,
    decimal PremiumAmount,
    string PremiumFrequency,
    DateTime StartDate,
    string? NomineeName,
    string? NomineeRelationship,
    DateTime? MaturityDate,
    decimal? MaturityAmount,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaPayPremiumPayload(DateTime PaymentDate, string? Narration);

/// <summary>
/// PersonalFin_12 - Insurance. PayPremium debits the linked account per premium (matching
/// PersonalFin_08's FD/RD money-integration pattern), MarkMatured credits it with the maturity
/// payout for endowment/ULIP-style policies. NextPremiumDueDate/PremiumDueNow are computed at
/// read time from PremiumFrequency, not stored.
/// </summary>
public static class SwalekhaInsuranceEndpoints
{
    public static RouteGroupBuilder MapSwalekhaInsuranceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/insurance-policies")
            .WithTags("Swalekha Insurance")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListPoliciesAsync);
        group.MapGet("/{id:guid}", GetPolicyAsync);
        group.MapPost("/", CreatePolicyAsync);
        group.MapPut("/{id:guid}", UpdatePolicyAsync);
        group.MapDelete("/{id:guid}", DeletePolicyAsync);
        group.MapPost("/{id:guid}/pay-premium", PayPremiumAsync);
        group.MapPost("/{id:guid}/mark-matured", MarkMaturedAsync);

        return group;
    }

    private static async Task<IResult> ListPoliciesAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaInsurancePolicies.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(p => p.IsActive);
        }

        var policies = await query.OrderBy(p => p.Insurer).ToListAsync(cancellationToken);
        return Results.Ok(policies.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetPolicyAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var policy = await db.SwalekhaInsurancePolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return policy is null ? Results.NotFound() : Results.Ok(ToDto(policy));
    }

    private static async Task<IResult> CreatePolicyAsync(SwalekhaInsurancePolicyPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Insurer))
        {
            return Results.BadRequest(new { message = "Insurer is required." });
        }

        if (!Enum.TryParse<SwalekhaInsurancePolicyType>(payload.PolicyType, true, out var policyType))
        {
            return Results.BadRequest(new { message = $"Unknown policy type '{payload.PolicyType}'." });
        }

        if (!Enum.TryParse<SwalekhaPremiumFrequency>(payload.PremiumFrequency, true, out var frequency))
        {
            return Results.BadRequest(new { message = $"Unknown premium frequency '{payload.PremiumFrequency}'." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        var policy = new SwalekhaInsurancePolicy
        {
            PolicyType = policyType,
            Insurer = payload.Insurer.Trim(),
            PolicyNumber = payload.PolicyNumber,
            AccountId = payload.AccountId,
            SumAssured = payload.SumAssured,
            PremiumAmount = payload.PremiumAmount,
            PremiumFrequency = frequency,
            StartDate = payload.StartDate,
            NomineeName = payload.NomineeName,
            NomineeRelationship = payload.NomineeRelationship,
            MaturityDate = payload.MaturityDate,
            MaturityAmount = payload.MaturityAmount,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaInsurancePolicies.Add(policy);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/insurance-policies/{policy.Id}", ToDto(policy));
    }

    private static async Task<IResult> UpdatePolicyAsync(Guid id, SwalekhaInsurancePolicyPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var policy = await db.SwalekhaInsurancePolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Insurer))
        {
            return Results.BadRequest(new { message = "Insurer is required." });
        }

        if (!Enum.TryParse<SwalekhaInsurancePolicyType>(payload.PolicyType, true, out var policyType))
        {
            return Results.BadRequest(new { message = $"Unknown policy type '{payload.PolicyType}'." });
        }

        if (!Enum.TryParse<SwalekhaPremiumFrequency>(payload.PremiumFrequency, true, out var frequency))
        {
            return Results.BadRequest(new { message = $"Unknown premium frequency '{payload.PremiumFrequency}'." });
        }

        var accountError = await ValidateAccountAsync(payload.AccountId, db, cancellationToken);
        if (accountError is not null) return accountError;

        policy.PolicyType = policyType;
        policy.Insurer = payload.Insurer.Trim();
        policy.PolicyNumber = payload.PolicyNumber;
        policy.AccountId = payload.AccountId;
        policy.SumAssured = payload.SumAssured;
        policy.PremiumAmount = payload.PremiumAmount;
        policy.PremiumFrequency = frequency;
        policy.StartDate = payload.StartDate;
        policy.NomineeName = payload.NomineeName;
        policy.NomineeRelationship = payload.NomineeRelationship;
        policy.MaturityDate = payload.MaturityDate;
        policy.MaturityAmount = payload.MaturityAmount;
        policy.IsActive = payload.IsActive;
        policy.Notes = payload.Notes;
        policy.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(policy));
    }

    private static async Task<IResult> DeletePolicyAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var policy = await db.SwalekhaInsurancePolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null) return Results.NotFound();

        policy.Deleted = true;
        policy.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> PayPremiumAsync(Guid id, SwalekhaPayPremiumPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var policy = await db.SwalekhaInsurancePolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null) return Results.NotFound();

        if (policy.IsMatured)
        {
            return Results.BadRequest(new { message = "This policy has already matured - no further premiums are due." });
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        policy.LastPremiumPaidDate = payload.PaymentDate;
        policy.UpdatedAt = DateTime.UtcNow;

        if (policy.AccountId.HasValue)
        {
            var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == policy.AccountId.Value, cancellationToken);
            if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

            account.CurrentBalance -= policy.PremiumAmount;
            account.UpdatedAt = DateTime.UtcNow;
            db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
            {
                AccountId = account.Id,
                TransactionType = SwalekhaTransactionType.Withdrawal,
                Amount = policy.PremiumAmount,
                TransactionDate = payload.PaymentDate,
                Narration = string.IsNullOrWhiteSpace(payload.Narration) ? $"Insurance premium - {policy.Insurer}" : payload.Narration.Trim(),
                RunningBalance = account.CurrentBalance
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(ToDto(policy));
    }

    private static async Task<IResult> MarkMaturedAsync(Guid id, SwalekhaMarkMaturedPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var policy = await db.SwalekhaInsurancePolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null) return Results.NotFound();

        if (policy.IsMatured)
        {
            return Results.BadRequest(new { message = "This policy is already marked matured." });
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        policy.IsMatured = true;
        policy.MaturedAt = DateTime.UtcNow;
        policy.MaturityAmount = payload.MaturityAmount;
        policy.UpdatedAt = DateTime.UtcNow;

        if (policy.AccountId.HasValue)
        {
            var account = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == policy.AccountId.Value, cancellationToken);
            if (account is null) return Results.BadRequest(new { message = "Linked account not found." });

            account.CurrentBalance += payload.MaturityAmount;
            account.UpdatedAt = DateTime.UtcNow;
            db.SwalekhaAccountTransactions.Add(new SwalekhaAccountTransaction
            {
                AccountId = account.Id,
                TransactionType = SwalekhaTransactionType.Deposit,
                Amount = payload.MaturityAmount,
                TransactionDate = payload.MaturityCreditedDate,
                Narration = string.IsNullOrWhiteSpace(payload.Narration) ? $"Insurance maturity - {policy.Insurer}" : payload.Narration.Trim(),
                RunningBalance = account.CurrentBalance
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(ToDto(policy));
    }

    private static async Task<IResult?> ValidateAccountAsync(Guid? accountId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!accountId.HasValue) return null;

        var exists = await db.SwalekhaAccounts.AsNoTracking().AnyAsync(a => a.Id == accountId.Value, cancellationToken);
        return exists ? null : Results.BadRequest(new { message = "Linked account not found." });
    }

    private static int FrequencyMonths(SwalekhaPremiumFrequency frequency) => frequency switch
    {
        SwalekhaPremiumFrequency.Monthly => 1,
        SwalekhaPremiumFrequency.Quarterly => 3,
        SwalekhaPremiumFrequency.HalfYearly => 6,
        SwalekhaPremiumFrequency.Yearly => 12,
        _ => 12
    };

    private static SwalekhaInsurancePolicyDto ToDto(SwalekhaInsurancePolicy policy)
    {
        var baseDate = policy.LastPremiumPaidDate ?? policy.StartDate;
        var nextDue = baseDate.AddMonths(FrequencyMonths(policy.PremiumFrequency));
        var dueNow = !policy.IsMatured && nextDue <= DateTime.UtcNow;

        return new SwalekhaInsurancePolicyDto(
            policy.Id,
            policy.PolicyType.ToString(),
            policy.Insurer,
            policy.PolicyNumber,
            policy.AccountId,
            policy.SumAssured,
            policy.PremiumAmount,
            policy.PremiumFrequency.ToString(),
            policy.StartDate,
            policy.NomineeName,
            policy.NomineeRelationship,
            policy.MaturityDate,
            policy.MaturityAmount,
            policy.LastPremiumPaidDate,
            nextDue,
            dueNow,
            policy.IsMatured,
            policy.MaturedAt,
            policy.IsActive,
            policy.Notes,
            policy.CreatedAt);
    }
}

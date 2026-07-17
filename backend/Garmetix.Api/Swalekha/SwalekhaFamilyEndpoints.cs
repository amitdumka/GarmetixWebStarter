using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaFamilyMemberDto(
    Guid Id,
    string Name,
    string? Relationship,
    string? Mobile,
    string? Email,
    DateTime? DateOfBirth,
    Guid? LinkedOwnerId,
    string? LinkedOwnerName,
    bool LinkConfirmed,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaFamilyMemberPayload(
    string Name,
    string? Relationship,
    string? Mobile,
    string? Email,
    DateTime? DateOfBirth,
    Guid? LinkedOwnerId,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaLinkableOwnerDto(Guid Id, string Name);

public sealed record SwalekhaFamilyTransferPayload(
    Guid FromAccountId,
    decimal Amount,
    DateTime TransactionDate,
    string? Narration);

public sealed record SwalekhaFamilyTransferResult(string Message, decimal FromAccountBalance);

/// <summary>
/// PersonalFin_07 - Family Connections + Transaction Sync. A family-member list, each optionally
/// linked to another real Owner login (LinkedOwnerId), plus a one-entry "family transfer" that
/// posts straight into the linked Owner's own account/data, e.g. a father paying a son needs only
/// one entry on the father's side. Sync requires the link to be reciprocal - both Owners must have
/// named each other - checked at transfer time, not just at link-creation time, since either side
/// could remove the link later.
/// </summary>
public static class SwalekhaFamilyEndpoints
{
    public static RouteGroupBuilder MapSwalekhaFamilyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/family")
            .WithTags("Swalekha Family")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListFamilyMembersAsync);
        group.MapGet("/linkable-owners", ListLinkableOwnersAsync);
        group.MapGet("/{id:guid}", GetFamilyMemberAsync);
        group.MapPost("/", CreateFamilyMemberAsync);
        group.MapPut("/{id:guid}", UpdateFamilyMemberAsync);
        group.MapDelete("/{id:guid}", DeleteFamilyMemberAsync);
        group.MapPost("/{id:guid}/transfer", TransferToFamilyMemberAsync);

        return group;
    }

    private static async Task<IResult> ListFamilyMembersAsync(
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        bool? includeInactive,
        CancellationToken cancellationToken)
    {
        var query = db.SwalekhaFamilyMembers.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(m => m.IsActive);
        }

        var members = await query.OrderBy(m => m.Name).ToListAsync(cancellationToken);
        return Results.Ok(await ToDtosAsync(members, db, garmetixDb, ownerContext, cancellationToken));
    }

    private static async Task<IResult> ListLinkableOwnersAsync(
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var owners = await garmetixDb.Users.AsNoTracking()
            .Where(u => u.UserType == UserType.Owner && u.Id != ownerContext.OwnerId && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new SwalekhaLinkableOwnerDto(u.Id, u.Name))
            .ToListAsync(cancellationToken);

        return Results.Ok(owners);
    }

    private static async Task<IResult> GetFamilyMemberAsync(
        Guid id,
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var member = await db.SwalekhaFamilyMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null) return Results.NotFound();

        var dto = (await ToDtosAsync([member], db, garmetixDb, ownerContext, cancellationToken)).Single();
        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateFamilyMemberAsync(
        SwalekhaFamilyMemberPayload payload,
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Family member name is required." });
        }

        var linkError = await ValidateLinkedOwnerAsync(payload.LinkedOwnerId, garmetixDb, ownerContext, cancellationToken);
        if (linkError is not null) return linkError;

        var member = new SwalekhaFamilyMember
        {
            Name = payload.Name.Trim(),
            Relationship = payload.Relationship,
            Mobile = payload.Mobile,
            Email = payload.Email,
            DateOfBirth = payload.DateOfBirth,
            LinkedOwnerId = payload.LinkedOwnerId,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaFamilyMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);

        var dto = (await ToDtosAsync([member], db, garmetixDb, ownerContext, cancellationToken)).Single();
        return Results.Created($"/api/swalekha/family/{member.Id}", dto);
    }

    private static async Task<IResult> UpdateFamilyMemberAsync(
        Guid id,
        SwalekhaFamilyMemberPayload payload,
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var member = await db.SwalekhaFamilyMembers.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Family member name is required." });
        }

        var linkError = await ValidateLinkedOwnerAsync(payload.LinkedOwnerId, garmetixDb, ownerContext, cancellationToken);
        if (linkError is not null) return linkError;

        member.Name = payload.Name.Trim();
        member.Relationship = payload.Relationship;
        member.Mobile = payload.Mobile;
        member.Email = payload.Email;
        member.DateOfBirth = payload.DateOfBirth;
        member.LinkedOwnerId = payload.LinkedOwnerId;
        member.IsActive = payload.IsActive;
        member.Notes = payload.Notes;
        member.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var dto = (await ToDtosAsync([member], db, garmetixDb, ownerContext, cancellationToken)).Single();
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteFamilyMemberAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var member = await db.SwalekhaFamilyMembers.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null) return Results.NotFound();

        member.Deleted = true;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// One entry on the sender's side moves money out of one of their own accounts and straight
    /// into the linked family member's own default receiving account, in the same transaction -
    /// both legs live in the same swalekha_db, just under different OwnerId values, so one
    /// SaveChanges/DB transaction keeps both sides atomic even though it crosses owners.
    /// </summary>
    private static async Task<IResult> TransferToFamilyMemberAsync(
        Guid id,
        SwalekhaFamilyTransferPayload payload,
        SwalekhaDbContext db,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        if (payload.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Amount must be greater than zero." });
        }

        var member = await db.SwalekhaFamilyMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null) return Results.NotFound();

        if (member.LinkedOwnerId is not { } recipientOwnerId)
        {
            return Results.BadRequest(new { message = "This family member is not linked to a platform login yet - add their Owner login under Linked Owner first." });
        }

        var reciprocalLinkExists = await db.SwalekhaFamilyMembers.IgnoreQueryFilters()
            .AnyAsync(m => !m.Deleted && m.OwnerId == recipientOwnerId && m.LinkedOwnerId == ownerContext.OwnerId, cancellationToken);
        if (!reciprocalLinkExists)
        {
            return Results.BadRequest(new { message = "The link isn't confirmed yet - ask them to add you back as a family member in their own Swalekha and link your login too, then try again." });
        }

        var fromAccount = await db.SwalekhaAccounts.FirstOrDefaultAsync(a => a.Id == payload.FromAccountId, cancellationToken);
        if (fromAccount is null) return Results.NotFound(new { message = "Source account not found." });

        var recipientProfile = await db.SwalekhaOwnerProfiles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => !p.Deleted && p.OwnerId == recipientOwnerId, cancellationToken);

        var toAccount = recipientProfile?.LinkedAccountId is { } linkedId
            ? await db.SwalekhaAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(a => a.Id == linkedId && !a.Deleted && a.OwnerId == recipientOwnerId, cancellationToken)
            : null;

        toAccount ??= await db.SwalekhaAccounts.IgnoreQueryFilters()
            .Where(a => !a.Deleted && a.OwnerId == recipientOwnerId && a.IsActive)
            .OrderBy(a => a.AccountType == SwalekhaAccountType.Bank ? 0 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        if (toAccount is null)
        {
            // The recipient has no account yet to receive into - auto-create one rather than
            // failing the transfer, so a first-ever family transfer to someone still works.
            toAccount = new SwalekhaAccount
            {
                OwnerId = recipientOwnerId,
                Name = "Family Transfers (Auto)",
                AccountType = SwalekhaAccountType.Cash,
                IsActive = true,
                Notes = "Auto-created to receive a linked family transfer."
            };
            db.SwalekhaAccounts.Add(toAccount);
        }

        var transferGroupId = Guid.NewGuid();
        var narration = string.IsNullOrWhiteSpace(payload.Narration)
            ? $"Family transfer to {member.Name}"
            : payload.Narration.Trim();

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
            OwnerId = recipientOwnerId,
            AccountId = toAccount.Id,
            TransactionType = SwalekhaTransactionType.TransferIn,
            Amount = payload.Amount,
            TransactionDate = payload.TransactionDate,
            Narration = $"Received from family (linked transfer): {narration}",
            CounterAccountId = fromAccount.Id,
            RunningBalance = toAccount.CurrentBalance,
            TransferGroupId = transferGroupId
        });

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(new SwalekhaFamilyTransferResult(
            $"Sent to {member.Name} - their linked account was updated automatically.",
            fromAccount.CurrentBalance));
    }

    private static async Task<IResult?> ValidateLinkedOwnerAsync(
        Guid? linkedOwnerId,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        if (!linkedOwnerId.HasValue) return null;

        if (linkedOwnerId.Value == ownerContext.OwnerId)
        {
            return Results.BadRequest(new { message = "You cannot link a family member to your own login." });
        }

        var isOwner = await garmetixDb.Users.AsNoTracking()
            .AnyAsync(u => u.Id == linkedOwnerId.Value && u.UserType == UserType.Owner, cancellationToken);
        if (!isOwner)
        {
            return Results.BadRequest(new { message = "Linked login must be an existing Owner-type login." });
        }

        return null;
    }

    private static async Task<List<SwalekhaFamilyMemberDto>> ToDtosAsync(
        IReadOnlyList<SwalekhaFamilyMember> members,
        SwalekhaDbContext db,
        GarmetixDbContext garmetixDb,
        SwalekhaOwnerContext ownerContext,
        CancellationToken cancellationToken)
    {
        var linkedIds = members.Where(m => m.LinkedOwnerId.HasValue).Select(m => m.LinkedOwnerId!.Value).Distinct().ToList();
        var linkedOwnerNames = linkedIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await garmetixDb.Users.AsNoTracking()
                .Where(u => linkedIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        // A single query against every candidate recipient owner's family-member rows, checking
        // which of them link back to the current Owner - avoids N+1 lookups per row.
        var confirmedOwnerIds = linkedIds.Count == 0
            ? []
            : await db.SwalekhaFamilyMembers.IgnoreQueryFilters()
                .Where(m => !m.Deleted && linkedIds.Contains(m.OwnerId) && m.LinkedOwnerId == ownerContext.OwnerId)
                .Select(m => m.OwnerId)
                .Distinct()
                .ToListAsync(cancellationToken);
        var confirmedOwnerIdSet = confirmedOwnerIds.ToHashSet();

        return [.. members.Select(m => new SwalekhaFamilyMemberDto(
            m.Id,
            m.Name,
            m.Relationship,
            m.Mobile,
            m.Email,
            m.DateOfBirth,
            m.LinkedOwnerId,
            m.LinkedOwnerId.HasValue && linkedOwnerNames.TryGetValue(m.LinkedOwnerId.Value, out var name) ? name : null,
            m.LinkedOwnerId.HasValue && confirmedOwnerIdSet.Contains(m.LinkedOwnerId.Value),
            m.IsActive,
            m.Notes,
            m.CreatedAt))];
    }
}

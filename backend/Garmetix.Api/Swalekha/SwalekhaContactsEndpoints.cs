using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaContactDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Relationship,
    decimal Balance,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaContactPayload(
    string Name,
    string? Phone,
    string? Email,
    string? Relationship,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaPersonLedgerEntryDto(
    Guid Id,
    Guid ContactId,
    string EntryType,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    decimal RunningBalance,
    DateTime CreatedAt);

public sealed record SwalekhaPersonLedgerEntryPayload(
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    string EntryType);

public sealed record SwalekhaPersonLedgerList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaPersonLedgerEntryDto> Rows);

public sealed record SwalekhaSettlePayload(DateTime EntryDate, string? Narration);

/// <summary>
/// PersonalFin_03 - Contacts + Person Ledger. CRUD for personal contacts (separate from the
/// CRM app's business customer directory) and a per-contact dr/cr ledger for money lent or
/// borrowed outside formal loan accounts. Balance is signed from the Owner's point of view:
/// positive means the contact owes the Owner, negative means the Owner owes the contact.
/// </summary>
public static class SwalekhaContactsEndpoints
{
    public static RouteGroupBuilder MapSwalekhaContactsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/contacts")
            .WithTags("Swalekha Contacts")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListContactsAsync);
        group.MapGet("/{id:guid}", GetContactAsync);
        group.MapPost("/", CreateContactAsync);
        group.MapPut("/{id:guid}", UpdateContactAsync);
        group.MapDelete("/{id:guid}", DeleteContactAsync);

        group.MapGet("/{id:guid}/ledger", ListLedgerAsync);
        group.MapPost("/{id:guid}/ledger", AddLedgerEntryAsync);
        group.MapDelete("/{id:guid}/ledger/{entryId:guid}", DeleteLedgerEntryAsync);
        group.MapPost("/{id:guid}/settle", SettleAsync);

        return group;
    }

    private static async Task<IResult> ListContactsAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaContacts.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(contact => contact.IsActive);
        }

        var contacts = await query.OrderBy(contact => contact.Name).ToListAsync(cancellationToken);
        return Results.Ok(contacts.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetContactAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var contact = await db.SwalekhaContacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return contact is null ? Results.NotFound() : Results.Ok(ToDto(contact));
    }

    private static async Task<IResult> CreateContactAsync(SwalekhaContactPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Contact name is required." });
        }

        var contact = new SwalekhaContact
        {
            Name = payload.Name.Trim(),
            Phone = payload.Phone,
            Email = payload.Email,
            Relationship = payload.Relationship,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/contacts/{contact.Id}", ToDto(contact));
    }

    private static async Task<IResult> UpdateContactAsync(Guid id, SwalekhaContactPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var contact = await db.SwalekhaContacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contact is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Contact name is required." });
        }

        contact.Name = payload.Name.Trim();
        contact.Phone = payload.Phone;
        contact.Email = payload.Email;
        contact.Relationship = payload.Relationship;
        contact.IsActive = payload.IsActive;
        contact.Notes = payload.Notes;
        contact.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(contact));
    }

    private static async Task<IResult> DeleteContactAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var contact = await db.SwalekhaContacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contact is null) return Results.NotFound();

        contact.Deleted = true;
        contact.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListLedgerAsync(Guid id, SwalekhaDbContext db, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var contactExists = await db.SwalekhaContacts.AsNoTracking().AnyAsync(c => c.Id == id, cancellationToken);
        if (!contactExists) return Results.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        var query = db.SwalekhaPersonLedgerEntries.AsNoTracking()
            .Where(entry => entry.ContactId == id)
            .OrderByDescending(entry => entry.EntryDate)
            .ThenByDescending(entry => entry.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaPersonLedgerList(effectivePage, effectivePageSize, totalCount, rows.Select(ToDto).ToList()));
    }

    private static async Task<IResult> AddLedgerEntryAsync(Guid id, SwalekhaPersonLedgerEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SwalekhaPersonLedgerEntryType>(payload.EntryType, true, out var entryType))
        {
            return Results.BadRequest(new { message = $"Unknown entry type '{payload.EntryType}'." });
        }

        if (payload.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Amount must be greater than zero." });
        }

        var contact = await db.SwalekhaContacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contact is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        contact.Balance += BalanceEffect(entryType, payload.Amount);
        contact.UpdatedAt = DateTime.UtcNow;

        var entry = new SwalekhaPersonLedgerEntry
        {
            ContactId = contact.Id,
            EntryType = entryType,
            Amount = payload.Amount,
            EntryDate = payload.EntryDate,
            Narration = payload.Narration ?? string.Empty,
            RunningBalance = contact.Balance
        };
        db.SwalekhaPersonLedgerEntries.Add(entry);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/swalekha/contacts/{id}/ledger/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> DeleteLedgerEntryAsync(Guid id, Guid entryId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaPersonLedgerEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ContactId == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        var contact = await db.SwalekhaContacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contact is null) return Results.NotFound();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        contact.Balance -= BalanceEffect(entry.EntryType, entry.Amount);
        contact.UpdatedAt = DateTime.UtcNow;
        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> SettleAsync(Guid id, SwalekhaSettlePayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var contact = await db.SwalekhaContacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (contact is null) return Results.NotFound();

        if (contact.Balance == 0)
        {
            return Results.Ok(new { message = "Already settled - balance is zero.", contact = ToDto(contact) });
        }

        // Post whichever repayment direction brings the balance to exactly zero, rather than
        // asking the Owner to work out the amount and direction by hand.
        var entryType = contact.Balance > 0 ? SwalekhaPersonLedgerEntryType.RepaymentReceived : SwalekhaPersonLedgerEntryType.RepaymentPaid;
        var amount = Math.Abs(contact.Balance);

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        contact.Balance += BalanceEffect(entryType, amount);
        contact.UpdatedAt = DateTime.UtcNow;

        var entry = new SwalekhaPersonLedgerEntry
        {
            ContactId = contact.Id,
            EntryType = entryType,
            Amount = amount,
            EntryDate = payload.EntryDate,
            Narration = string.IsNullOrWhiteSpace(payload.Narration) ? "Settled" : payload.Narration.Trim(),
            RunningBalance = contact.Balance
        };
        db.SwalekhaPersonLedgerEntries.Add(entry);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.Ok(new { message = "Settled.", contact = ToDto(contact), entry = ToDto(entry) });
    }

    private static decimal BalanceEffect(SwalekhaPersonLedgerEntryType entryType, decimal amount) => entryType switch
    {
        SwalekhaPersonLedgerEntryType.LoanGiven => amount,
        SwalekhaPersonLedgerEntryType.RepaymentReceived => -amount,
        SwalekhaPersonLedgerEntryType.LoanTaken => -amount,
        SwalekhaPersonLedgerEntryType.RepaymentPaid => amount,
        _ => 0
    };

    private static SwalekhaContactDto ToDto(SwalekhaContact contact) => new(
        contact.Id,
        contact.Name,
        contact.Phone,
        contact.Email,
        contact.Relationship,
        contact.Balance,
        contact.IsActive,
        contact.Notes,
        contact.CreatedAt);

    private static SwalekhaPersonLedgerEntryDto ToDto(SwalekhaPersonLedgerEntry entry) => new(
        entry.Id,
        entry.ContactId,
        entry.EntryType.ToString(),
        entry.Amount,
        entry.EntryDate,
        entry.Narration,
        entry.RunningBalance,
        entry.CreatedAt);
}

using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaJournalEntryDto(
    Guid Id,
    DateTime EntryDate,
    string? Title,
    string Content,
    string? Mood,
    DateTime CreatedAt);

public sealed record SwalekhaJournalEntryPayload(
    DateTime EntryDate,
    string? Title,
    string Content,
    string? Mood);

/// <summary>
/// PersonalFin_13 - Diary/Journal. Dated entries, already private by construction (Owner-only
/// data isolation from PersonalFin_06).
/// </summary>
public static class SwalekhaJournalEndpoints
{
    public static RouteGroupBuilder MapSwalekhaJournalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/journal")
            .WithTags("Swalekha Organizer")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListEntriesAsync);
        group.MapGet("/{id:guid}", GetEntryAsync);
        group.MapPost("/", CreateEntryAsync);
        group.MapPut("/{id:guid}", UpdateEntryAsync);
        group.MapDelete("/{id:guid}", DeleteEntryAsync);

        return group;
    }

    private static async Task<IResult> ListEntriesAsync(SwalekhaDbContext db, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaJournalEntries.AsNoTracking().AsQueryable();
        if (fromDate.HasValue)
        {
            query = query.Where(e => e.EntryDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            query = query.Where(e => e.EntryDate <= toDate.Value);
        }

        var entries = await query.OrderByDescending(e => e.EntryDate).ToListAsync(cancellationToken);
        return Results.Ok(entries.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetEntryAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaJournalEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entry is null ? Results.NotFound() : Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> CreateEntryAsync(SwalekhaJournalEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Content))
        {
            return Results.BadRequest(new { message = "Journal content is required." });
        }

        var entry = new SwalekhaJournalEntry
        {
            EntryDate = payload.EntryDate,
            Title = payload.Title,
            Content = payload.Content.Trim(),
            Mood = payload.Mood
        };

        db.SwalekhaJournalEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/journal/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> UpdateEntryAsync(Guid id, SwalekhaJournalEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaJournalEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Content))
        {
            return Results.BadRequest(new { message = "Journal content is required." });
        }

        entry.EntryDate = payload.EntryDate;
        entry.Title = payload.Title;
        entry.Content = payload.Content.Trim();
        entry.Mood = payload.Mood;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> DeleteEntryAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaJournalEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static SwalekhaJournalEntryDto ToDto(SwalekhaJournalEntry entry) => new(
        entry.Id,
        entry.EntryDate,
        entry.Title,
        entry.Content,
        entry.Mood,
        entry.CreatedAt);
}

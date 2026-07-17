using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaPersonalNoteDto(
    Guid Id,
    string Title,
    string Content,
    string? Folder,
    string? Tags,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SwalekhaPersonalNotePayload(
    string Title,
    string Content,
    string? Folder,
    string? Tags,
    bool IsPinned);

/// <summary>
/// PersonalFin_13 - Personal Notes. Topic-based free-form notes, distinct from the dated Journal -
/// organized by an optional Folder and freeform Tags rather than a timeline.
/// </summary>
public static class SwalekhaNoteEndpoints
{
    public static RouteGroupBuilder MapSwalekhaNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/notes")
            .WithTags("Swalekha Organizer")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListNotesAsync);
        group.MapGet("/{id:guid}", GetNoteAsync);
        group.MapPost("/", CreateNoteAsync);
        group.MapPut("/{id:guid}", UpdateNoteAsync);
        group.MapDelete("/{id:guid}", DeleteNoteAsync);

        return group;
    }

    private static async Task<IResult> ListNotesAsync(SwalekhaDbContext db, string? folder, string? search, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaPersonalNotes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            query = query.Where(n => n.Folder == folder);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Content.Contains(term) || (n.Tags != null && n.Tags.Contains(term)));
        }

        var notes = await query
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(notes.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetNoteAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var note = await db.SwalekhaPersonalNotes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        return note is null ? Results.NotFound() : Results.Ok(ToDto(note));
    }

    private static async Task<IResult> CreateNoteAsync(SwalekhaPersonalNotePayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            return Results.BadRequest(new { message = "Note title is required." });
        }

        var note = new SwalekhaPersonalNote
        {
            Title = payload.Title.Trim(),
            Content = payload.Content ?? string.Empty,
            Folder = payload.Folder,
            Tags = payload.Tags,
            IsPinned = payload.IsPinned
        };

        db.SwalekhaPersonalNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/notes/{note.Id}", ToDto(note));
    }

    private static async Task<IResult> UpdateNoteAsync(Guid id, SwalekhaPersonalNotePayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var note = await db.SwalekhaPersonalNotes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (note is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            return Results.BadRequest(new { message = "Note title is required." });
        }

        note.Title = payload.Title.Trim();
        note.Content = payload.Content ?? string.Empty;
        note.Folder = payload.Folder;
        note.Tags = payload.Tags;
        note.IsPinned = payload.IsPinned;
        note.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(note));
    }

    private static async Task<IResult> DeleteNoteAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var note = await db.SwalekhaPersonalNotes.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (note is null) return Results.NotFound();

        note.Deleted = true;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static SwalekhaPersonalNoteDto ToDto(SwalekhaPersonalNote note) => new(
        note.Id,
        note.Title,
        note.Content,
        note.Folder,
        note.Tags,
        note.IsPinned,
        note.CreatedAt,
        note.UpdatedAt);
}

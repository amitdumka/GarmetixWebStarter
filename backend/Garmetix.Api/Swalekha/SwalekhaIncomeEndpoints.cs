using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaIncomeEntryDto(
    Guid Id,
    string Source,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    DateTime CreatedAt);

public sealed record SwalekhaIncomeEntryPayload(
    string Source,
    decimal Amount,
    DateTime EntryDate,
    string Narration);

public sealed record SwalekhaIncomeList(int Page, int PageSize, int TotalCount, decimal TotalAmount, IReadOnlyList<SwalekhaIncomeEntryDto> Rows);

/// <summary>PersonalFin_04 - Income entries (salary/rental/interest/dividend/other), standalone from Accounts Hub in v1.</summary>
public static class SwalekhaIncomeEndpoints
{
    public static RouteGroupBuilder MapSwalekhaIncomeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/income")
            .WithTags("Swalekha Income")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(SwalekhaDbContext db, DateTime? from, DateTime? to, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        var query = db.SwalekhaIncomeEntries.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(e => e.EntryDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.EntryDate <= to.Value);

        var totalAmount = await query.SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaIncomeList(effectivePage, effectivePageSize, totalCount, totalAmount, rows.Select(ToDto).ToList()));
    }

    private static async Task<IResult> CreateAsync(SwalekhaIncomeEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Source)) return Results.BadRequest(new { message = "Source is required." });
        if (payload.Amount <= 0) return Results.BadRequest(new { message = "Amount must be greater than zero." });

        var entry = new SwalekhaIncomeEntry
        {
            Source = payload.Source.Trim(),
            Amount = payload.Amount,
            EntryDate = payload.EntryDate,
            Narration = payload.Narration ?? string.Empty
        };

        db.SwalekhaIncomeEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/income/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> UpdateAsync(Guid id, SwalekhaIncomeEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaIncomeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(payload.Source)) return Results.BadRequest(new { message = "Source is required." });
        if (payload.Amount <= 0) return Results.BadRequest(new { message = "Amount must be greater than zero." });

        entry.Source = payload.Source.Trim();
        entry.Amount = payload.Amount;
        entry.EntryDate = payload.EntryDate;
        entry.Narration = payload.Narration ?? string.Empty;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> DeleteAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaIncomeEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static SwalekhaIncomeEntryDto ToDto(SwalekhaIncomeEntry entry) => new(
        entry.Id, entry.Source, entry.Amount, entry.EntryDate, entry.Narration, entry.CreatedAt);
}

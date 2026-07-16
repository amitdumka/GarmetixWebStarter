using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaExpenseSheetDto(
    Guid Id,
    string Name,
    string SheetType,
    decimal? Budget,
    bool IsActive,
    string? Notes,
    decimal SpentTotal,
    DateTime CreatedAt);

public sealed record SwalekhaExpenseSheetPayload(
    string Name,
    string SheetType,
    decimal? Budget,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaExpenseEntryDto(
    Guid Id,
    Guid SheetId,
    string Category,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    bool IsHidden,
    DateTime CreatedAt);

public sealed record SwalekhaExpenseEntryPayload(
    string Category,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    bool IsHidden);

public sealed record SwalekhaExpenseEntryList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaExpenseEntryDto> Rows);

public sealed record SwalekhaExpenseSummaryRow(string Key, decimal Total, int Count);

public sealed record SwalekhaExpenseSummary(
    decimal TotalVisible,
    decimal TotalHidden,
    IReadOnlyList<SwalekhaExpenseSummaryRow> BySheetType,
    IReadOnlyList<SwalekhaExpenseSummaryRow> ByCategory);

/// <summary>
/// PersonalFin_04 - Expense Sheets (grouped by a free-text SheetType - Personal/House/Medical/
/// Gifts/Hidden/custom) and their entries. Entries can be individually flagged IsHidden so a
/// sensitive line can be excluded from default summaries regardless of which sheet it's on.
/// Standalone from Accounts Hub in v1 - see the domain model file for why.
/// </summary>
public static class SwalekhaExpenseEndpoints
{
    public static RouteGroupBuilder MapSwalekhaExpenseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/expense-sheets")
            .WithTags("Swalekha Expenses")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListSheetsAsync);
        group.MapGet("/{id:guid}", GetSheetAsync);
        group.MapPost("/", CreateSheetAsync);
        group.MapPut("/{id:guid}", UpdateSheetAsync);
        group.MapDelete("/{id:guid}", DeleteSheetAsync);

        group.MapGet("/{id:guid}/entries", ListEntriesAsync);
        group.MapPost("/{id:guid}/entries", AddEntryAsync);
        group.MapPut("/{id:guid}/entries/{entryId:guid}", UpdateEntryAsync);
        group.MapDelete("/{id:guid}/entries/{entryId:guid}", DeleteEntryAsync);

        app.MapGet("/api/swalekha/expenses/summary", GetSummaryAsync)
            .WithTags("Swalekha Expenses")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        return group;
    }

    private static async Task<IResult> ListSheetsAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaExpenseSheets.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(sheet => sheet.IsActive);
        }

        var sheets = await query.OrderBy(sheet => sheet.Name).ToListAsync(cancellationToken);
        var sheetIds = sheets.Select(s => s.Id).ToList();
        var totals = await db.SwalekhaExpenseEntries.AsNoTracking()
            .Where(entry => sheetIds.Contains(entry.SheetId))
            .GroupBy(entry => entry.SheetId)
            .Select(g => new { SheetId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.SheetId, x => x.Total, cancellationToken);

        return Results.Ok(sheets.Select(sheet => ToDto(sheet, totals.GetValueOrDefault(sheet.Id))).ToList());
    }

    private static async Task<IResult> GetSheetAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var sheet = await db.SwalekhaExpenseSheets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sheet is null) return Results.NotFound();

        var total = await db.SwalekhaExpenseEntries.AsNoTracking().Where(e => e.SheetId == id).SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;
        return Results.Ok(ToDto(sheet, total));
    }

    private static async Task<IResult> CreateSheetAsync(SwalekhaExpenseSheetPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name)) return Results.BadRequest(new { message = "Sheet name is required." });
        if (string.IsNullOrWhiteSpace(payload.SheetType)) return Results.BadRequest(new { message = "Sheet type is required." });

        var sheet = new SwalekhaExpenseSheet
        {
            Name = payload.Name.Trim(),
            SheetType = payload.SheetType.Trim(),
            Budget = payload.Budget,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaExpenseSheets.Add(sheet);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/expense-sheets/{sheet.Id}", ToDto(sheet, 0));
    }

    private static async Task<IResult> UpdateSheetAsync(Guid id, SwalekhaExpenseSheetPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var sheet = await db.SwalekhaExpenseSheets.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sheet is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(payload.Name)) return Results.BadRequest(new { message = "Sheet name is required." });
        if (string.IsNullOrWhiteSpace(payload.SheetType)) return Results.BadRequest(new { message = "Sheet type is required." });

        sheet.Name = payload.Name.Trim();
        sheet.SheetType = payload.SheetType.Trim();
        sheet.Budget = payload.Budget;
        sheet.IsActive = payload.IsActive;
        sheet.Notes = payload.Notes;
        sheet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        var total = await db.SwalekhaExpenseEntries.AsNoTracking().Where(e => e.SheetId == id).SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0;
        return Results.Ok(ToDto(sheet, total));
    }

    private static async Task<IResult> DeleteSheetAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var sheet = await db.SwalekhaExpenseSheets.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sheet is null) return Results.NotFound();

        sheet.Deleted = true;
        sheet.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListEntriesAsync(Guid id, SwalekhaDbContext db, bool? includeHidden, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var sheetExists = await db.SwalekhaExpenseSheets.AsNoTracking().AnyAsync(s => s.Id == id, cancellationToken);
        if (!sheetExists) return Results.NotFound();

        var effectivePage = page is > 0 ? page.Value : 1;
        var effectivePageSize = pageSize is > 0 and <= 200 ? pageSize.Value : 50;

        IQueryable<SwalekhaExpenseEntry> query = db.SwalekhaExpenseEntries.AsNoTracking().Where(entry => entry.SheetId == id);
        if (includeHidden != true)
        {
            query = query.Where(entry => !entry.IsHidden);
        }
        query = query.OrderByDescending(entry => entry.EntryDate).ThenByDescending(entry => entry.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize).ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaExpenseEntryList(effectivePage, effectivePageSize, totalCount, rows.Select(ToDto).ToList()));
    }

    private static async Task<IResult> AddEntryAsync(Guid id, SwalekhaExpenseEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (payload.Amount <= 0) return Results.BadRequest(new { message = "Amount must be greater than zero." });

        var sheet = await db.SwalekhaExpenseSheets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sheet is null) return Results.NotFound();

        var entry = new SwalekhaExpenseEntry
        {
            SheetId = id,
            Category = string.IsNullOrWhiteSpace(payload.Category) ? "General" : payload.Category.Trim(),
            Amount = payload.Amount,
            EntryDate = payload.EntryDate,
            Narration = payload.Narration ?? string.Empty,
            IsHidden = payload.IsHidden
        };

        db.SwalekhaExpenseEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/expense-sheets/{id}/entries/{entry.Id}", ToDto(entry));
    }

    private static async Task<IResult> UpdateEntryAsync(Guid id, Guid entryId, SwalekhaExpenseEntryPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaExpenseEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.SheetId == id, cancellationToken);
        if (entry is null) return Results.NotFound();
        if (payload.Amount <= 0) return Results.BadRequest(new { message = "Amount must be greater than zero." });

        entry.Category = string.IsNullOrWhiteSpace(payload.Category) ? "General" : payload.Category.Trim();
        entry.Amount = payload.Amount;
        entry.EntryDate = payload.EntryDate;
        entry.Narration = payload.Narration ?? string.Empty;
        entry.IsHidden = payload.IsHidden;
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> DeleteEntryAsync(Guid id, Guid entryId, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.SwalekhaExpenseEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.SheetId == id, cancellationToken);
        if (entry is null) return Results.NotFound();

        entry.Deleted = true;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSummaryAsync(SwalekhaDbContext db, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaExpenseEntries.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(e => e.EntryDate >= from.Value);
        if (to.HasValue) query = query.Where(e => e.EntryDate <= to.Value);

        var entries = await query.ToListAsync(cancellationToken);
        var visible = entries.Where(e => !e.IsHidden).ToList();

        var sheetTypes = await db.SwalekhaExpenseSheets.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.SheetType, cancellationToken);

        var bySheetType = visible
            .GroupBy(e => sheetTypes.GetValueOrDefault(e.SheetId, "Unknown"))
            .Select(g => new SwalekhaExpenseSummaryRow(g.Key, g.Sum(e => e.Amount), g.Count()))
            .OrderByDescending(row => row.Total)
            .ToList();

        var byCategory = visible
            .GroupBy(e => e.Category)
            .Select(g => new SwalekhaExpenseSummaryRow(g.Key, g.Sum(e => e.Amount), g.Count()))
            .OrderByDescending(row => row.Total)
            .ToList();

        return Results.Ok(new SwalekhaExpenseSummary(
            visible.Sum(e => e.Amount),
            entries.Where(e => e.IsHidden).Sum(e => e.Amount),
            bySheetType,
            byCategory));
    }

    private static SwalekhaExpenseSheetDto ToDto(SwalekhaExpenseSheet sheet, decimal spentTotal) => new(
        sheet.Id, sheet.Name, sheet.SheetType, sheet.Budget, sheet.IsActive, sheet.Notes, spentTotal, sheet.CreatedAt);

    private static SwalekhaExpenseEntryDto ToDto(SwalekhaExpenseEntry entry) => new(
        entry.Id, entry.SheetId, entry.Category, entry.Amount, entry.EntryDate, entry.Narration, entry.IsHidden, entry.CreatedAt);
}

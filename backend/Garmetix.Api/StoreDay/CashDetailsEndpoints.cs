
using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Garmetix.Models.DayOperations;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.StoreDay;

public sealed record CashDetailSaveRequest(
    Guid StoreId,
    DateTime OnDate,
    decimal? Amount,
    int N2000,
    int N500,
    int N200,
    int N100,
    int N50,
    int NC20,
    int NC10,
    int NC5,
    int NC2,
    int NC1,
    string? Source);

public sealed record CashDetailResponse(
    Guid Id,
    Guid StoreId,
    string StoreName,
    DateTime OnDate,
    decimal Amount,
    int N2000,
    int N500,
    int N200,
    int N100,
    int N50,
    int NC20,
    int NC10,
    int NC5,
    int NC2,
    int NC1,
    string Source,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool LinkedToDayOpening,
    bool LinkedToDayClosing);

public sealed record CashDetailHistoryResponse(
    Guid StoreId,
    DateTime? From,
    DateTime? To,
    int RecordCount,
    decimal TotalAmount,
    decimal AverageAmount,
    int N2000,
    int N500,
    int N200,
    int N100,
    int N50,
    int NC20,
    int NC10,
    int NC5,
    int NC2,
    int NC1,
    IReadOnlyList<CashDetailResponse> Items);

public static class CashDetailsEndpoints
{
    public static RouteGroupBuilder MapCashDetailsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cash-details")
            .WithTags("Cash Details")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/history", HistoryAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? source,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.CashDetails.AsNoTracking().Where(item => !item.Deleted);
        if (storeId.HasValue)
        {
            if (!CanUseStore(context, storeId.Value))
            {
                return Results.BadRequest(new { message = "Selected store is outside your access scope." });
            }

            query = query.Where(item => item.StoreId == storeId.Value);
        }
        else if (!WorkspaceScope.HasFullAccess(context))
        {
            var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
            if (claimStoreId.HasValue)
            {
                query = query.Where(item => item.StoreId == claimStoreId.Value);
            }
        }

        if (from.HasValue)
        {
            query = query.Where(item => item.OnDate >= from.Value.Date);
        }

        if (to.HasValue)
        {
            var next = to.Value.Date.AddDays(1);
            query = query.Where(item => item.OnDate < next);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(item => (item.CreatedBy ?? string.Empty) == source);
        }

        var rows = await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return Results.Ok(await ToResponsesAsync(db, rows, cancellationToken));
    }

    private static async Task<IResult> HistoryAsync(
        Guid storeId,
        DateTime? from,
        DateTime? to,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, storeId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        var start = from?.Date;
        var end = to?.Date.AddDays(1);
        var query = db.CashDetails.AsNoTracking().Where(item => item.StoreId == storeId && !item.Deleted);
        if (start.HasValue)
        {
            query = query.Where(item => item.OnDate >= start.Value);
        }
        if (end.HasValue)
        {
            query = query.Where(item => item.OnDate < end.Value);
        }

        var rows = await query.OrderByDescending(item => item.OnDate).ThenBy(item => item.CreatedBy).ToListAsync(cancellationToken);
        var responses = await ToResponsesAsync(db, rows, cancellationToken);
        var total = responses.Sum(item => item.Amount);
        return Results.Ok(new CashDetailHistoryResponse(
            storeId,
            from?.Date,
            to?.Date,
            responses.Count,
            Math.Round(total, 2),
            responses.Count == 0 ? 0 : Math.Round(total / responses.Count, 2),
            responses.Sum(item => item.N2000),
            responses.Sum(item => item.N500),
            responses.Sum(item => item.N200),
            responses.Sum(item => item.N100),
            responses.Sum(item => item.N50),
            responses.Sum(item => item.NC20),
            responses.Sum(item => item.NC10),
            responses.Sum(item => item.NC5),
            responses.Sum(item => item.NC2),
            responses.Sum(item => item.NC1),
            responses));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entity = await db.CashDetails.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (!CanUseStore(context, entity.StoreId))
        {
            return Results.BadRequest(new { message = "Selected cash detail is outside your access scope." });
        }

        return Results.Ok((await ToResponsesAsync(db, [entity], cancellationToken)).First());
    }

    private static async Task<IResult> CreateAsync(CashDetailSaveRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId))
        {
            return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        }

        var entity = new CashDetail
        {
            StoreId = request.StoreId,
            OnDate = request.OnDate.Date,
            CreatedBy = NormalizeSource(request.Source)
        };
        Copy(request, entity);
        db.CashDetails.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/cash-details/{entity.Id}", (await ToResponsesAsync(db, [entity], cancellationToken)).First());
    }

    private static async Task<IResult> UpdateAsync(Guid id, CashDetailSaveRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entity = await db.CashDetails.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (!CanUseStore(context, entity.StoreId) || !CanUseStore(context, request.StoreId))
        {
            return Results.BadRequest(new { message = "Selected cash detail is outside your access scope." });
        }

        entity.StoreId = request.StoreId;
        entity.OnDate = request.OnDate.Date;
        entity.CreatedBy = NormalizeSource(request.Source);
        Copy(request, entity);
        entity.UpdatedAt = DateTime.UtcNow;

        await SyncLinkedDayRecordsAsync(db, entity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok((await ToResponsesAsync(db, [entity], cancellationToken)).First());
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entity = await db.CashDetails.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (!CanUseStore(context, entity.StoreId))
        {
            return Results.BadRequest(new { message = "Selected cash detail is outside your access scope." });
        }

        var linked = await IsLinkedAsync(db, entity.Id, cancellationToken);
        if (linked.LinkedToDayOpening || linked.LinkedToDayClosing)
        {
            return Results.BadRequest(new { message = "This cash detail is linked to Day Opening/Closing. Edit it instead of deleting, or reopen/delete the day close first." });
        }

        entity.Deleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static void Copy(CashDetailSaveRequest request, CashDetail entity)
    {
        entity.N2000 = request.N2000;
        entity.N500 = request.N500;
        entity.N200 = request.N200;
        entity.N100 = request.N100;
        entity.N50 = request.N50;
        entity.NC20 = request.NC20;
        entity.NC10 = request.NC10;
        entity.NC5 = request.NC5;
        entity.NC2 = request.NC2;
        entity.NC1 = request.NC1;
        var calculated = CalculateAmount(request);
        entity.Amount = Math.Round(calculated > 0 ? calculated : request.Amount ?? 0m, 2);
    }

    private static decimal CalculateAmount(CashDetailSaveRequest request)
        => request.N2000 * 2000m
           + request.N500 * 500m
           + request.N200 * 200m
           + request.N100 * 100m
           + request.N50 * 50m
           + request.NC20 * 20m
           + request.NC10 * 10m
           + request.NC5 * 5m
           + request.NC2 * 2m
           + request.NC1;

    private static string NormalizeSource(string? source)
        => string.IsNullOrWhiteSpace(source) ? "ManualCashFlow" : source.Trim();

    private static async Task SyncLinkedDayRecordsAsync(GarmetixDbContext db, CashDetail entity, CancellationToken cancellationToken)
    {
        var begins = await db.DayBegins.Where(item => item.CashDetailId == entity.Id && !item.Deleted).ToListAsync(cancellationToken);
        foreach (var begin in begins)
        {
            begin.StoreId = entity.StoreId;
            begin.OnDate = entity.OnDate.Date;
            begin.OpeningBalance = entity.Amount;
            begin.UpdatedAt = DateTime.UtcNow;
        }

        var ends = await db.DayEnds.Where(item => item.CashDetailId == entity.Id && !item.Deleted).ToListAsync(cancellationToken);
        foreach (var end in ends)
        {
            end.StoreId = entity.StoreId;
            end.OnDate = entity.OnDate.Date;
            end.ClosingBalance = entity.Amount;
            end.UpdatedAt = DateTime.UtcNow;

            var sheet = await db.PettyCashSheets.FirstOrDefaultAsync(item => item.StoreId == end.StoreId && item.OnDate == end.OnDate && !item.Deleted, cancellationToken);
            if (sheet is not null && (sheet.CreatedBy == "DayClosing" || sheet.CreatedBy == "StoreHoliday"))
            {
                sheet.CashInHand = entity.Amount;
                sheet.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private static async Task<(bool LinkedToDayOpening, bool LinkedToDayClosing)> IsLinkedAsync(GarmetixDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var opening = await db.DayBegins.AsNoTracking().AnyAsync(item => item.CashDetailId == id && !item.Deleted, cancellationToken);
        var closing = await db.DayEnds.AsNoTracking().AnyAsync(item => item.CashDetailId == id && !item.Deleted, cancellationToken);
        return (opening, closing);
    }

    private static async Task<IReadOnlyList<CashDetailResponse>> ToResponsesAsync(GarmetixDbContext db, IReadOnlyList<CashDetail> rows, CancellationToken cancellationToken)
    {
        var storeIds = rows.Select(item => item.StoreId).Distinct().ToArray();
        var storeNames = await db.Stores.AsNoTracking()
            .Where(store => storeIds.Contains(store.Id))
            .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);
        var ids = rows.Select(item => item.Id).ToArray();
        var openingIds = await db.DayBegins.AsNoTracking()
            .Where(item => ids.Contains(item.CashDetailId) && !item.Deleted)
            .Select(item => item.CashDetailId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var closingIds = await db.DayEnds.AsNoTracking()
            .Where(item => ids.Contains(item.CashDetailId) && !item.Deleted)
            .Select(item => item.CashDetailId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var opening = openingIds.ToHashSet();
        var closing = closingIds.ToHashSet();

        return rows.Select(item => new CashDetailResponse(
            item.Id,
            item.StoreId,
            storeNames.TryGetValue(item.StoreId, out var storeName) ? storeName : "Store",
            item.OnDate.Date,
            Math.Round(item.Amount, 2),
            item.N2000,
            item.N500,
            item.N200,
            item.N100,
            item.N50,
            item.NC20,
            item.NC10,
            item.NC5,
            item.NC2,
            item.NC1,
            item.CreatedBy ?? "ManualCashFlow",
            item.CreatedAt,
            item.UpdatedAt,
            opening.Contains(item.Id),
            closing.Contains(item.Id))).ToList();
    }

    private static bool CanUseStore(HttpContext context, Guid storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return true;
        }

        var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
        return !claimStoreId.HasValue || claimStoreId.Value == storeId;
    }
}

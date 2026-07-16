using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaTripDto(
    Guid Id,
    Guid SheetId,
    string Name,
    string? Destination,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? Budget,
    decimal SpentTotal,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaTripPayload(
    string Name,
    string? Destination,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? Budget,
    string? Notes);

/// <summary>
/// PersonalFin_05 - Travel Expense Sheets. A Trip is metadata wrapped around a dedicated
/// SwalekhaExpenseSheet (SheetType "Travel") created automatically on trip creation. Expense
/// entries are added via the existing SwalekhaExpenseEndpoints routes against the trip's
/// SheetId - not duplicated here. See SwalekhaTrips.cs for why Close doesn't need to copy data.
/// </summary>
public static class SwalekhaTripEndpoints
{
    private const string TravelSheetType = "Travel";

    public static RouteGroupBuilder MapSwalekhaTripEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/trips")
            .WithTags("Swalekha Trips")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListTripsAsync);
        group.MapGet("/{id:guid}", GetTripAsync);
        group.MapPost("/", CreateTripAsync);
        group.MapPut("/{id:guid}", UpdateTripAsync);
        group.MapDelete("/{id:guid}", DeleteTripAsync);
        group.MapPost("/{id:guid}/close", CloseTripAsync);
        group.MapPost("/{id:guid}/reopen", ReopenTripAsync);

        return group;
    }

    private static async Task<IResult> ListTripsAsync(SwalekhaDbContext db, bool? includeClosed, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaTrips.AsNoTracking().AsQueryable();
        if (includeClosed != true)
        {
            query = query.Where(trip => !trip.IsClosed);
        }

        var trips = await query.OrderByDescending(trip => trip.StartDate ?? trip.CreatedAt).ToListAsync(cancellationToken);
        return Results.Ok(await ToDtosAsync(trips, db, cancellationToken));
    }

    private static async Task<IResult> GetTripAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var trip = await db.SwalekhaTrips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (trip is null) return Results.NotFound();

        var dtos = await ToDtosAsync([trip], db, cancellationToken);
        return Results.Ok(dtos[0]);
    }

    private static async Task<IResult> CreateTripAsync(SwalekhaTripPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name)) return Results.BadRequest(new { message = "Trip name is required." });

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var sheet = new SwalekhaExpenseSheet
        {
            Name = payload.Name.Trim(),
            SheetType = TravelSheetType,
            Budget = payload.Budget,
            IsActive = true
        };
        db.SwalekhaExpenseSheets.Add(sheet);

        var trip = new SwalekhaTrip
        {
            SheetId = sheet.Id,
            Name = payload.Name.Trim(),
            Destination = payload.Destination,
            StartDate = payload.StartDate,
            EndDate = payload.EndDate,
            Notes = payload.Notes
        };
        db.SwalekhaTrips.Add(trip);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        var dtos = await ToDtosAsync([trip], db, cancellationToken);
        return Results.Created($"/api/swalekha/trips/{trip.Id}", dtos[0]);
    }

    private static async Task<IResult> UpdateTripAsync(Guid id, SwalekhaTripPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var trip = await db.SwalekhaTrips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (trip is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(payload.Name)) return Results.BadRequest(new { message = "Trip name is required." });

        var sheet = await db.SwalekhaExpenseSheets.FirstOrDefaultAsync(s => s.Id == trip.SheetId, cancellationToken);
        if (sheet is null) return Results.NotFound(new { message = "The trip's linked expense sheet is missing." });

        trip.Name = payload.Name.Trim();
        trip.Destination = payload.Destination;
        trip.StartDate = payload.StartDate;
        trip.EndDate = payload.EndDate;
        trip.Notes = payload.Notes;
        trip.UpdatedAt = DateTime.UtcNow;

        // Keep the linked sheet's name/budget in sync so PersonalFin_04's expense-sheets list
        // and summary endpoints always show the trip under its current name and budget.
        sheet.Name = trip.Name;
        sheet.Budget = payload.Budget;
        sheet.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        var dtos = await ToDtosAsync([trip], db, cancellationToken);
        return Results.Ok(dtos[0]);
    }

    private static async Task<IResult> DeleteTripAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var trip = await db.SwalekhaTrips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (trip is null) return Results.NotFound();

        var sheet = await db.SwalekhaExpenseSheets.FirstOrDefaultAsync(s => s.Id == trip.SheetId, cancellationToken);

        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        trip.Deleted = true;
        trip.UpdatedAt = DateTime.UtcNow;
        if (sheet is not null)
        {
            // Soft-delete the sheet alongside the trip, same as every other Swalekha delete -
            // its entries are kept intact for the record, just no longer listed as active.
            sheet.Deleted = true;
            sheet.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> CloseTripAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var trip = await db.SwalekhaTrips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (trip is null) return Results.NotFound();

        var sheet = await db.SwalekhaExpenseSheets.FirstOrDefaultAsync(s => s.Id == trip.SheetId, cancellationToken);

        trip.IsClosed = true;
        trip.ClosedAt = DateTime.UtcNow;
        trip.UpdatedAt = DateTime.UtcNow;
        if (sheet is not null)
        {
            sheet.IsActive = false;
            sheet.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        var dtos = await ToDtosAsync([trip], db, cancellationToken);
        return Results.Ok(dtos[0]);
    }

    private static async Task<IResult> ReopenTripAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var trip = await db.SwalekhaTrips.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (trip is null) return Results.NotFound();

        var sheet = await db.SwalekhaExpenseSheets.FirstOrDefaultAsync(s => s.Id == trip.SheetId, cancellationToken);

        trip.IsClosed = false;
        trip.ClosedAt = null;
        trip.UpdatedAt = DateTime.UtcNow;
        if (sheet is not null)
        {
            sheet.IsActive = true;
            sheet.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        var dtos = await ToDtosAsync([trip], db, cancellationToken);
        return Results.Ok(dtos[0]);
    }

    private static async Task<IReadOnlyList<SwalekhaTripDto>> ToDtosAsync(IReadOnlyList<SwalekhaTrip> trips, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var sheetIds = trips.Select(t => t.SheetId).Distinct().ToList();
        var sheets = await db.SwalekhaExpenseSheets.AsNoTracking()
            .Where(s => sheetIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var totals = await db.SwalekhaExpenseEntries.AsNoTracking()
            .Where(entry => sheetIds.Contains(entry.SheetId))
            .GroupBy(entry => entry.SheetId)
            .Select(g => new { SheetId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.SheetId, x => x.Total, cancellationToken);

        return trips.Select(trip =>
        {
            sheets.TryGetValue(trip.SheetId, out var sheet);
            return new SwalekhaTripDto(
                trip.Id,
                trip.SheetId,
                trip.Name,
                trip.Destination,
                trip.StartDate,
                trip.EndDate,
                sheet?.Budget,
                totals.GetValueOrDefault(trip.SheetId),
                trip.IsClosed,
                trip.ClosedAt,
                trip.Notes,
                trip.CreatedAt);
        }).ToList();
    }
}

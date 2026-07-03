using System.Security.Claims;
using System.Text.Json;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

public static class PosHeldBillEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static RouteGroupBuilder MapPosHeldBillEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/pos/held-bills")
            .WithTags("POS Held Bills")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("/", ListAsync);
        group.MapPost("/", SaveAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapDelete("/", ClearAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(
        Guid? storeId,
        int take,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);
        take = Math.Clamp(take <= 0 ? 100 : take, 1, 200);

        var query = WorkspaceScope.ApplyTo(db.PosHeldBills.AsNoTracking(), context)
            .Where(item => item.Status == "Held");

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        var rows = await query
            .OrderByDescending(item => item.HeldAt)
            .ThenByDescending(item => item.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(ToDto));
    }

    private static async Task<IResult> SaveAsync(
        PosHeldBillSaveRequest request,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        if (request.Draft.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Results.BadRequest(new { message = "Held bill draft is missing." });
        }

        var entity = request.Id.HasValue
            ? await WorkspaceScope.ApplyTo(db.PosHeldBills, context)
                .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken)
            : null;

        if (entity is null && !string.IsNullOrWhiteSpace(request.ClientHeldBillId))
        {
            entity = await WorkspaceScope.ApplyTo(db.PosHeldBills, context)
                .FirstOrDefaultAsync(item => item.ClientHeldBillId == request.ClientHeldBillId && item.Status == "Held", cancellationToken);
        }

        var creating = entity is null;
        entity ??= new PosHeldBill
        {
            Id = request.Id.GetValueOrDefault(Guid.NewGuid())
        };

        entity.ClientHeldBillId = FirstNonBlank(request.ClientHeldBillId, request.Id?.ToString(), entity.Id.ToString());
        entity.CompanyId = request.CompanyId;
        entity.StoreGroupId = request.StoreGroupId;
        entity.StoreId = request.StoreId;
        entity.HeldAt = request.HeldAt ?? DateTime.Now;
        entity.CustomerName = FirstNonBlank(request.CustomerName, "Walk-in Customer");
        entity.CustomerMobileNumber = request.CustomerMobileNumber?.Trim() ?? string.Empty;
        entity.ItemCount = Math.Max(request.ItemCount, 0);
        entity.Quantity = Math.Max(request.Quantity, 0);
        entity.PayableTotal = Math.Max(request.PayableTotal, 0);
        entity.Note = request.Note?.Trim() ?? string.Empty;
        entity.DraftJson = JsonSerializer.Serialize(request.Draft, JsonOptions);
        entity.Status = "Held";
        entity.HeldByUserId = CurrentUserId(context);
        entity.HeldByUserName = FirstNonBlank(context.User.Identity?.Name, context.User.FindFirstValue(ClaimTypes.Name), context.User.FindFirstValue("userName"), "POS Operator");
        entity.ResumedAt = null;
        entity.Deleted = false;

        if (!WorkspaceScope.CanWrite(entity, context, out var message))
        {
            return Results.BadRequest(new { message });
        }

        if (entity.CompanyId == Guid.Empty || entity.StoreGroupId == Guid.Empty || entity.StoreId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select company, store group and store before holding a bill." });
        }

        if (creating)
        {
            db.PosHeldBills.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(entity));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);
        var row = await WorkspaceScope.ApplyTo(db.PosHeldBills, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null)
        {
            return Results.NotFound();
        }

        row.Status = "Resumed";
        row.ResumedAt = DateTime.Now;
        row.Deleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ClearAsync(
        Guid? storeId,
        GarmetixDbContext db,
        HttpContext context,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);
        var query = WorkspaceScope.ApplyTo(db.PosHeldBills, context)
            .Where(item => item.Status == "Held");
        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        var rows = await query.ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Status = "Cleared";
            row.ResumedAt = DateTime.Now;
            row.Deleted = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { cleared = rows.Count });
    }

    private static async Task EnsureStorageAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairPosHeldBillStorageAsync(
            db,
            loggerFactory.CreateLogger("PosHeldBillStorageRepair"),
            cancellationToken);
    }

    private static PosHeldBillDto ToDto(PosHeldBill row)
    {
        var draft = JsonSerializer.Deserialize<JsonElement>(
            string.IsNullOrWhiteSpace(row.DraftJson) ? "{}" : row.DraftJson,
            JsonOptions);

        return new PosHeldBillDto(
            row.Id,
            row.ClientHeldBillId,
            row.HeldAt,
            row.CustomerName,
            row.CustomerMobileNumber,
            row.ItemCount,
            row.Quantity,
            row.PayableTotal,
            row.Note,
            row.Status,
            row.CompanyId,
            row.StoreGroupId,
            row.StoreId,
            row.HeldByUserName,
            draft);
    }

    private static Guid? CurrentUserId(HttpContext context)
        => Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static string FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

public sealed record PosHeldBillSaveRequest(
    Guid? Id,
    string? ClientHeldBillId,
    DateTime? HeldAt,
    string? CustomerName,
    string? CustomerMobileNumber,
    int ItemCount,
    decimal Quantity,
    decimal PayableTotal,
    string? Note,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    JsonElement Draft);

public sealed record PosHeldBillDto(
    Guid Id,
    string ClientHeldBillId,
    DateTime HeldAt,
    string CustomerName,
    string CustomerMobileNumber,
    int ItemCount,
    decimal Quantity,
    decimal PayableTotal,
    string Note,
    string Status,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string HeldByUserName,
    JsonElement Draft);

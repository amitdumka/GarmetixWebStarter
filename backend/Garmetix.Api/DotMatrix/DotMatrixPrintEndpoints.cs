using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Printing;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.DotMatrix;

public static class DotMatrixPrintEndpoints
{
    public static RouteGroupBuilder MapDotMatrixPrintEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dot-matrix-print")
            .WithTags("Dot Matrix Print")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("/settings", GetSettingsAsync);
        group.MapPost("/settings", SaveSettingsAsync);
        group.MapPost("/test", TestPrintAsync);
        group.MapGet("/queue", QueueAsync);
        group.MapPost("/queue/{id:guid}/retry", RetryAsync);
        group.MapPost("/queue/{id:guid}/skip", SkipAsync);
        group.MapPost("/queue/{id:guid}/reprint", ReprintAsync);
        group.MapPost("/queue/retry-failed", RetryFailedAsync);
        group.MapPost("/queue/reset-stuck-printing", ResetStuckPrintingAsync);
        group.MapGet("/queue/stats", QueueStatsAsync);
        group.MapGet("/queue/{id:guid}/text", TextAsync);
        group.MapPost("/settings/pause", PauseAsync);
        group.MapPost("/settings/resume", ResumeAsync);
        return group;
    }

    private static async Task<IResult> GetSettingsAsync(Guid storeId, HttpContext context, DotMatrixJournalService service, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, storeId)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        return Results.Ok(await service.GetEffectiveSettingAsync(storeId, cancellationToken));
    }

    private static async Task<IResult> SaveSettingsAsync(DotMatrixSettingSaveRequest request, HttpContext context, DotMatrixJournalService service, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        return Results.Ok(await service.SaveSettingAsync(request, cancellationToken));
    }

    private static async Task<IResult> TestPrintAsync(DotMatrixTestPrintRequest request, HttpContext context, DotMatrixJournalService service, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        var entry = await service.QueueTestPrintAsync(request.StoreId, request.Message, cancellationToken);
        return Results.Ok(new { entry.Id, entry.Status, entry.SourceType, entry.CreatedAt, entry.PrinterName });
    }

    private static async Task<IResult> QueueAsync(Guid? storeId, string? status, DateTime? from, DateTime? to, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var query = db.DotMatrixPrintQueueEntries.AsNoTracking().Where(item => !item.Deleted);
        if (storeId.HasValue)
        {
            if (!CanUseStore(context, storeId.Value)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
            query = query.Where(item => item.StoreId == storeId.Value);
        }
        else if (!WorkspaceScope.HasFullAccess(context))
        {
            var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
            if (claimStoreId.HasValue) query = query.Where(item => item.StoreId == claimStoreId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status.Trim());
        if (from.HasValue) query = query.Where(item => item.BusinessDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(item => item.BusinessDate < to.Value.Date.AddDays(1));

        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .Select(item => new DotMatrixQueueDto(item.Id, item.BusinessDate, item.EventType, item.ActionType, item.SourceType, item.SourceNumber, item.PartyName, item.Amount, item.PaymentMode, item.Status, item.RetryCount, item.PrinterName, item.ErrorMessage, item.CreatedAt, item.PrintedAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }

    private static async Task<IResult> RetryAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.DotMatrixPrintQueueEntries.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (entry is null) return Results.NotFound();
        if (!CanUseStore(context, entry.StoreId)) return Results.BadRequest(new { message = "Selected print entry is outside your access scope." });
        entry.Status = "Pending";
        entry.ErrorMessage = null;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entry.Id, entry.Status });
    }

    private static async Task<IResult> SkipAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.DotMatrixPrintQueueEntries.FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (entry is null) return Results.NotFound();
        if (!CanUseStore(context, entry.StoreId)) return Results.BadRequest(new { message = "Selected print entry is outside your access scope." });
        entry.Status = "Skipped";
        entry.ErrorMessage = "Skipped manually.";
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { entry.Id, entry.Status });
    }

    private static async Task<IResult> ReprintAsync(Guid id, HttpContext context, DotMatrixJournalService service, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var source = await db.DotMatrixPrintQueueEntries.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (source is null) return Results.NotFound();
        if (!CanUseStore(context, source.StoreId)) return Results.BadRequest(new { message = "Selected print entry is outside your access scope." });
        try
        {
            var entry = await service.QueueReprintAsync(id, context.User?.Identity?.Name, cancellationToken);
            return Results.Ok(new { entry.Id, entry.Status, entry.SourceType, entry.SourceNumber });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> RetryFailedAsync(DotMatrixQueueBulkRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        var entries = await db.DotMatrixPrintQueueEntries
            .Where(item => item.StoreId == request.StoreId && !item.Deleted && item.Status == "Failed")
            .Take(500)
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.Status = "Pending";
            entry.RetryCount = 0;
            entry.ErrorMessage = null;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { count = entries.Count });
    }

    private static async Task<IResult> ResetStuckPrintingAsync(DotMatrixQueueBulkRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, request.StoreId)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        var entries = await db.DotMatrixPrintQueueEntries
            .Where(item => item.StoreId == request.StoreId && !item.Deleted && item.Status == "Printing" && (item.UpdatedAt ?? item.CreatedAt) < cutoff)
            .Take(500)
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.Status = "Pending";
            entry.ErrorMessage = "Reset from stuck Printing state by user.";
            entry.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { count = entries.Count });
    }

    private static async Task<IResult> QueueStatsAsync(Guid? storeId, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var query = db.DotMatrixPrintQueueEntries.AsNoTracking().Where(item => !item.Deleted);
        if (storeId.HasValue)
        {
            if (!CanUseStore(context, storeId.Value)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
            query = query.Where(item => item.StoreId == storeId.Value);
        }
        else if (!WorkspaceScope.HasFullAccess(context))
        {
            var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
            if (claimStoreId.HasValue) query = query.Where(item => item.StoreId == claimStoreId.Value);
        }

        // Keep this endpoint intentionally simple and EF-safe. The previous GroupBy
        // projection into a record constructor failed translation in production, which
        // blocked the Dot Matrix page. Count the small fixed status set directly.
        var statuses = new[] { "Pending", "Printing", "Printed", "Failed", "Skipped" };
        var rows = new List<DotMatrixQueueStatDto>(statuses.Length);
        foreach (var statusName in statuses)
        {
            var count = await query.CountAsync(item => item.Status == statusName, cancellationToken);
            rows.Add(new DotMatrixQueueStatDto(statusName, count));
        }

        return Results.Ok(rows);
    }

    private static Task<IResult> PauseAsync(DotMatrixQueueBulkRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
        => SetEnabledAsync(request.StoreId, false, context, db, cancellationToken);

    private static Task<IResult> ResumeAsync(DotMatrixQueueBulkRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
        => SetEnabledAsync(request.StoreId, true, context, db, cancellationToken);

    private static async Task<IResult> SetEnabledAsync(Guid storeId, bool enabled, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (!CanUseStore(context, storeId)) return Results.BadRequest(new { message = "Selected store is outside your access scope." });
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        if (store is null) return Results.NotFound(new { message = "Store was not found." });
        var setting = await db.DotMatrixPrintSettings.FirstOrDefaultAsync(item => item.StoreId == storeId && !item.Deleted, cancellationToken);
        if (setting is null)
        {
            setting = new DotMatrixPrintSetting
            {
                CompanyId = store.CompanyId,
                StoreGroupId = store.StoreGroupId,
                StoreId = store.Id,
                PrinterName = "EPSON_LX810",
                OutputMode = "BridgeService",
                TimeZoneId = "Asia/Kolkata",
                LineWidth = 136,
                CreatedBy = "DotMatrixSettings"
            };
            db.DotMatrixPrintSettings.Add(setting);
        }

        setting.Enabled = enabled;
        setting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { setting.StoreId, setting.Enabled });
    }

    private static async Task<IResult> TextAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var entry = await db.DotMatrixPrintQueueEntries.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (entry is null) return Results.NotFound();
        if (!CanUseStore(context, entry.StoreId)) return Results.BadRequest(new { message = "Selected print entry is outside your access scope." });
        return Results.Text(entry.PrintableText, "text/plain");
    }

    private static bool CanUseStore(HttpContext context, Guid storeId)
    {
        if (WorkspaceScope.HasFullAccess(context)) return true;
        var claimStoreId = WorkspaceScope.ClaimGuid(context, "storeId");
        return !claimStoreId.HasValue || claimStoreId.Value == storeId;
    }
}

public sealed record DotMatrixTestPrintRequest(Guid StoreId, string? Message);
public sealed record DotMatrixQueueBulkRequest(Guid StoreId);
public sealed record DotMatrixQueueStatDto(string Status, int Count);
public sealed record DotMatrixQueueDto(Guid Id, DateTime BusinessDate, string EventType, string ActionType, string SourceType, string SourceNumber, string PartyName, decimal Amount, string PaymentMode, string Status, int RetryCount, string PrinterName, string? ErrorMessage, DateTime CreatedAt, DateTime? PrintedAtUtc);

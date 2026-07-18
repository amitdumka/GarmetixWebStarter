using Garmetix.Api.Auth;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Admin management over the suppression list webhook events populate automatically.
/// CompanyId scoping means one tenant's suppressed addresses are never visible to another -
/// read filters to the caller's own scope unless they have full access (WorkspaceScope
/// semantics), and removal always requires a reason, recorded for audit.
/// </summary>
public static class EmailSuppressionEndpoints
{
    public static RouteGroupBuilder MapEmailSuppressionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/communication/suppression")
            .WithTags("Communication & Mail - Suppression")
            .RequireAuthorization(GarmetixPolicies.CommunicationSuppression);

        group.MapGet("/", ListAsync);
        group.MapPost("/manual", ManualAddAsync);
        group.MapPost("/{id:guid}/remove", RemoveAsync);

        return group;
    }

    private static async Task<IResult> ListAsync(HttpContext context, GarmetixDbContext db, string? search, bool? activeOnly, CancellationToken cancellationToken)
    {
        var query = Workspace.WorkspaceScope.HasFullAccess(context)
            ? db.EmailSuppressionEntries.AsNoTracking()
            : db.EmailSuppressionEntries.AsNoTracking().Where(s => s.CompanyId == null || s.CompanyId == ClaimGuid(context, "companyId"));

        if (activeOnly == true)
        {
            query = query.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(s => s.EmailAddress.Contains(term));
        }

        var entries = await query.OrderByDescending(s => s.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return Results.Ok(entries.Select(ToDto));
    }

    private static async Task<IResult> ManualAddAsync(EmailSuppressionManualAddRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmailAddress))
        {
            return Results.BadRequest(new { message = "Email address is required." });
        }

        var normalized = request.EmailAddress.Trim().ToLowerInvariant();
        var existing = await db.EmailSuppressionEntries.FirstOrDefaultAsync(
            s => s.CompanyId == request.CompanyId && s.EmailAddress == normalized && s.IsActive, cancellationToken);
        if (existing is not null)
        {
            return Results.Ok(ToDto(existing));
        }

        var entry = new EmailSuppressionEntry
        {
            CompanyId = request.CompanyId,
            EmailAddress = normalized,
            Reason = EmailCatalog.SuppressionReasons.Manual,
            IsActive = true,
        };
        db.EmailSuppressionEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> RemoveAsync(Guid id, EmailSuppressionRemoveRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { message = "A reason is required to remove a suppression entry." });
        }

        var entry = await db.EmailSuppressionEntries.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entry is null)
        {
            return Results.NotFound(new { message = "Suppression entry not found." });
        }

        entry.IsActive = false;
        entry.RemovedByUserId = Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
        entry.RemovedAtUtc = DateTime.UtcNow;
        entry.RemovalReason = request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Suppression entry removed." });
    }

    private static Guid? ClaimGuid(HttpContext context, string claimType) =>
        Guid.TryParse(context.User.FindFirst(claimType)?.Value, out var id) ? id : null;

    private static EmailSuppressionEntryDto ToDto(EmailSuppressionEntry entry) => new(
        entry.Id, entry.CompanyId, entry.EmailAddress, entry.Reason, entry.IsActive,
        entry.CreatedAt, entry.RemovedAtUtc, entry.RemovalReason);
}

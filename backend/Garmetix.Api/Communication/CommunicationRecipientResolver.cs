using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Resolves a compose/reply request's recipient list (individual users and/or roles - this
/// codebase has no Department entity, so "departments" from the master prompt is scoped down
/// to Role, which is the closest real concept here) into a concrete, deduplicated set of user
/// ids at send time. Role-based ("broadcast") resolution is scoped to users within the
/// resolver's own WorkspaceScope-authorized company/store, so a broadcast can never reach
/// users outside the sender's own scope. Individual user ids are trusted as explicitly chosen
/// by the composer and are not further scope-filtered (a direct message to a specific known
/// colleague is not a broadcast).
/// </summary>
public sealed class CommunicationRecipientResolver(GarmetixDbContext db)
{
    public async Task<HashSet<Guid>> ResolveAsync(
        HttpContext context,
        IReadOnlyCollection<Guid> explicitUserIds,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken)
    {
        var resolved = new HashSet<Guid>(explicitUserIds);

        if (roleNames.Count > 0)
        {
            var roleSet = roleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var scopedUsers = WorkspaceScope.ApplyTo(db.Users.AsNoTracking(), context);
            var roleMatches = await scopedUsers
                .Where(u => roleSet.Contains(u.Role.ToString()))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            foreach (var id in roleMatches)
            {
                resolved.Add(id);
            }
        }

        var callerId = Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var parsed) ? parsed : (Guid?)null;
        if (callerId.HasValue)
        {
            resolved.Remove(callerId.Value); // a sender is never their own recipient
        }

        return resolved;
    }
}

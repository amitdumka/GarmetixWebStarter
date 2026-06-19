using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Production;

public sealed record PermissionRoleCoverageDto(
    string Role,
    int UserCount,
    int ActiveUserCount,
    int AdminUserCount,
    string Status,
    string Message);

public sealed record PermissionAcceptanceStatusDto(
    DateTimeOffset CheckedAtUtc,
    int TotalUsers,
    int ActiveUsers,
    int AdminUsers,
    int ScopedUsers,
    bool HasAdmin,
    bool HasScopedUsers,
    IReadOnlyList<PermissionRoleCoverageDto> Roles,
    IReadOnlyList<string> Recommendations);

public static class PermissionAcceptanceEndpoints
{
    public static RouteGroupBuilder MapPermissionAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/permission-acceptance")
            .WithTags("Permission Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);

        return group;
    }

    private static async Task<PermissionAcceptanceStatusDto> StatusAsync(
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .Select(user => new
            {
                user.Id,
                user.Name,
                user.UserName,
                user.Email,
                user.Role,
                user.UserType,
                user.Admin,
                user.IsActive,
                user.CompanyId,
                user.StoreGroupId,
                user.StoreId,
                user.AppOperation
            })
            .ToListAsync(cancellationToken);

        var roleRows = users
            .GroupBy(user => user.Role.ToString())
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var count = group.Count();
                var active = group.Count(user => user.IsActive);
                var admins = group.Count(user => user.Admin);
                var status = active > 0 ? "Ready" : "No active users";
                return new PermissionRoleCoverageDto(
                    group.Key,
                    count,
                    active,
                    admins,
                    status,
                    active > 0
                        ? $"{active} active user(s) are available for {group.Key} testing."
                        : $"No active user is available for {group.Key} testing.");
            })
            .ToList();

        var adminUsers = users.Count(user => user.Admin || string.Equals(user.Role.ToString(), "Admin", StringComparison.OrdinalIgnoreCase));
        var scopedUsers = users.Count(user => user.CompanyId.HasValue || user.StoreGroupId.HasValue || user.StoreId.HasValue);
        var recommendations = new List<string>
        {
            "Before handover, login once as Admin/Owner, Store Manager, Billing, Purchase, Accountant and HR/Payroll user.",
            "Verify scoped users cannot see other company/store data.",
            "Verify non-admin users cannot open Data, Maintenance, Roles & Users, Factory Reset or backup restore actions."
        };

        if (adminUsers == 0)
        {
            recommendations.Insert(0, "Create at least one active Admin/Owner user before production use.");
        }

        if (scopedUsers == 0)
        {
            recommendations.Add("Create at least one store-scoped user to test tenant/store isolation.");
        }

        return new PermissionAcceptanceStatusDto(
            DateTimeOffset.UtcNow,
            users.Count,
            users.Count(user => user.IsActive),
            adminUsers,
            scopedUsers,
            adminUsers > 0,
            scopedUsers > 0,
            roleRows,
            recommendations);
    }
}

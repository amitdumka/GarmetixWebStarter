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

public sealed record PermissionModuleCoverageDto(
    string Role,
    bool AdminWorkspace,
    bool Entry,
    bool Edit,
    bool Delete,
    IReadOnlyList<string> Modules,
    string Notes,
    bool HasActiveUser,
    bool RequiresScopedUser,
    bool HasScopedActiveUser,
    string AcceptanceStatus);

public sealed record PermissionRouteExpectationDto(
    string Role,
    string LandingRoute,
    IReadOnlyList<string> AllowedRoutes,
    IReadOnlyList<string> BlockedRoutes,
    string TestUserHint);

public sealed record PermissionAcceptanceStatusDto(
    DateTimeOffset CheckedAtUtc,
    int TotalUsers,
    int ActiveUsers,
    int AdminUsers,
    int ScopedUsers,
    bool HasAdmin,
    bool HasScopedUsers,
    IReadOnlyList<PermissionRoleCoverageDto> Roles,
    IReadOnlyList<PermissionModuleCoverageDto> Matrix,
    IReadOnlyList<PermissionRouteExpectationDto> RouteExpectations,
    int RequiredRoleCount,
    int ReadyRoleCount,
    bool HasRoleMatrixCoverage,
    IReadOnlyList<string> Recommendations);

public static class PermissionAcceptanceEndpoints
{
    private static readonly string[] StoreScopedRoles = ["StoreManager", "Salesman", "HR", "Payroll"];

    public static RouteGroupBuilder MapPermissionAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/permission-acceptance")
            .WithTags("Permission Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);
        group.MapGet("/matrix", () => Results.Ok(BuildRouteExpectations()));

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

        var matrix = AccessPermissionMatrix.Profiles
            .Select(profile =>
            {
                var roleUsers = string.Equals(profile.Role, "Owner", StringComparison.OrdinalIgnoreCase)
                    ? users.Where(user => string.Equals(user.UserType.ToString(), "Owner", StringComparison.OrdinalIgnoreCase)).ToList()
                    : users.Where(user => string.Equals(user.Role.ToString(), profile.Role, StringComparison.OrdinalIgnoreCase)).ToList();
                var activeRoleUsers = roleUsers.Where(user => user.IsActive).ToList();
                var requiresScope = StoreScopedRoles.Contains(profile.Role, StringComparer.OrdinalIgnoreCase);
                var hasScopedActive = activeRoleUsers.Any(user => user.CompanyId.HasValue || user.StoreGroupId.HasValue || user.StoreId.HasValue);
                var status = activeRoleUsers.Count == 0
                    ? "Missing test user"
                    : requiresScope && !hasScopedActive ? "Needs scoped test user" : "Ready";

                return new PermissionModuleCoverageDto(
                    profile.Role,
                    profile.AdminWorkspace,
                    profile.Entry,
                    profile.Edit,
                    profile.Delete,
                    profile.Modules,
                    profile.Notes,
                    activeRoleUsers.Count > 0,
                    requiresScope,
                    hasScopedActive,
                    status);
            })
            .ToList();

        var adminUsers = users.Count(user => user.Admin || string.Equals(user.Role.ToString(), "Admin", StringComparison.OrdinalIgnoreCase));
        var scopedUsers = users.Count(user => user.CompanyId.HasValue || user.StoreGroupId.HasValue || user.StoreId.HasValue);
        var routeExpectations = BuildRouteExpectations();
        var requiredRoles = routeExpectations.Select(item => item.Role).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var readyRoles = matrix.Count(row => requiredRoles.Contains(row.Role, StringComparer.OrdinalIgnoreCase)
            && string.Equals(row.AcceptanceStatus, "Ready", StringComparison.OrdinalIgnoreCase));
        var recommendations = new List<string>
        {
            "Before handover, login once as Admin/Owner, Store Manager, Billing/Salesman, Purchase-capable user, Accountant and HR/Payroll user.",
            "For each role, verify allowed routes open and blocked routes show Access Denied instead of data leakage.",
            "Verify scoped users cannot see other company/store data in Billing, Purchase, Cash Details, Store Day and HR/Payroll pages.",
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

        if (readyRoles < requiredRoles.Length)
        {
            recommendations.Add($"Create/activate test users for all required acceptance roles. Ready roles: {readyRoles}/{requiredRoles.Length}.");
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
            matrix,
            routeExpectations,
            requiredRoles.Length,
            readyRoles,
            readyRoles == requiredRoles.Length,
            recommendations);
    }

    private static IReadOnlyList<PermissionRouteExpectationDto> BuildRouteExpectations() =>
    [
        new(
            "Admin",
            "/dashboard/business",
            ["/access", "/backup-maintenance", "/production-readiness", "/system-health", "/cash-details"],
            [],
            "Use Owner/Admin account. Confirm admin pages and operational pages open."),
        new(
            "StoreManager",
            "/dashboard/store-manager",
            ["/billing", "/inventory", "/purchase", "/store-day", "/cash-details", "/hr"],
            ["/access", "/backup-maintenance", "/production-readiness"],
            "Use a store-scoped Store Manager. Confirm other-store data is hidden."),
        new(
            "Salesman",
            "/dashboard/store-manager",
            ["/billing", "/customers", "/sales-return"],
            ["/access", "/backup-maintenance", "/accounting", "/payroll", "/purchase"],
            "Use a billing user. Confirm sale entry works but admin/accounting pages are blocked."),
        new(
            "Accountant",
            "/dashboard/business",
            ["/accounting", "/vouchers", "/cash-vouchers", "/petty-cash", "/cash-details", "/gst-returns", "/payroll"],
            ["/access", "/backup-maintenance"],
            "Use Accountant or RemoteAccountant. Confirm cash/accounting reports work and admin pages are blocked."),
        new(
            "HR",
            "/hr",
            ["/hr"],
            ["/access", "/backup-maintenance", "/accounting", "/payroll"],
            "Use HR role. Confirm HR access only."),
        new(
            "Payroll",
            "/payroll",
            ["/payroll"],
            ["/access", "/backup-maintenance", "/accounting", "/hr"],
            "Use Payroll role. Confirm payroll access only."),
        new(
            "PowerUser",
            "/dashboard/business",
            ["/billing", "/inventory", "/purchase", "/accounting", "/hr", "/payroll"],
            ["/access", "/backup-maintenance", "/production-readiness"],
            "Use Power User. Confirm broad entry/edit access without admin maintenance.")
    ];
}

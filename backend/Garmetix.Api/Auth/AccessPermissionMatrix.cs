using System.Security.Claims;
using Garmetix.Core.Enums;

namespace Garmetix.Api.Auth;

public sealed record AccessPermissionProfile(
    string Role,
    bool AdminWorkspace,
    bool Entry,
    bool Edit,
    bool Delete,
    IReadOnlyList<string> Modules,
    string Notes);

public static class AccessPermissionMatrix
{
    private static readonly string[] AllModules =
    [
        GarmetixPolicies.Billing,
        GarmetixPolicies.Inventory,
        GarmetixPolicies.Purchase,
        GarmetixPolicies.Accounting,
        GarmetixPolicies.Hr,
        GarmetixPolicies.Payroll,
        GarmetixPolicies.Attendance,
        "Reports",
        "GST"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> ModuleRoles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [GarmetixPolicies.Billing] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager),
                Role(LoginRole.Salesman)
            ],
            [GarmetixPolicies.Inventory] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager)
            ],
            [GarmetixPolicies.Purchase] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager)
            ],
            [GarmetixPolicies.Accounting] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.Accountant),
                Role(LoginRole.RemoteAccountant),
                Role(LoginRole.StoreManager)
            ],
            [GarmetixPolicies.Hr] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager),
                Role(LoginRole.HR)
            ],
            [GarmetixPolicies.Payroll] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.Accountant),
                Role(LoginRole.RemoteAccountant),
                Role(LoginRole.StoreManager),
                Role(LoginRole.Payroll)
            ],
            [GarmetixPolicies.Attendance] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager),
                Role(LoginRole.HR),
                Role(LoginRole.Payroll)
            ]
        };

    public static IReadOnlyList<AccessPermissionProfile> Profiles { get; } =
    [
        Profile("Owner", true, true, true, AllModules, "Full business and administration control."),
        Profile(Role(LoginRole.Admin), true, true, true, AllModules, "Full administration control."),
        Profile(Role(LoginRole.PowerUser), false, true, false, AllModules, "All operational modules without Admin or delete rights."),
        Profile(Role(LoginRole.Accountant), false, true, false, [GarmetixPolicies.Accounting, GarmetixPolicies.Payroll, "Reports", "GST"], "Accounting, payroll, reports, and GST operations."),
        Profile(Role(LoginRole.RemoteAccountant), false, false, false, [GarmetixPolicies.Accounting, GarmetixPolicies.Payroll, "Reports", "GST"], "Accounting review, salary payment review, reports, and GST without global edit/delete rights."),
        Profile(Role(LoginRole.StoreManager), false, false, false, [GarmetixPolicies.Billing, GarmetixPolicies.Inventory, GarmetixPolicies.Purchase, GarmetixPolicies.Accounting, GarmetixPolicies.Hr, GarmetixPolicies.Payroll, GarmetixPolicies.Attendance, "Reports"], "Store views, HR attendance, payslip/salary payment visibility, and new entries; no Admin, edit, or delete rights."),
        Profile(Role(LoginRole.Salesman), false, false, false, [GarmetixPolicies.Billing], "Billing and customer-facing entries."),
        Profile(Role(LoginRole.HR), false, false, false, [GarmetixPolicies.Hr, GarmetixPolicies.Attendance], "HR and attendance entries."),
        Profile(Role(LoginRole.Payroll), false, false, false, [GarmetixPolicies.Payroll, GarmetixPolicies.Attendance], "Payroll and salary processing entries."),
        Profile(Role(LoginRole.Member), false, false, false, [], "Authenticated account with no operational module assignment.")
    ];

    public static bool IsAdminOrOwner(ClaimsPrincipal user)
        => user.IsInRole(Role(LoginRole.Admin)) || IsOwner(user);

    public static bool CanEdit(ClaimsPrincipal user)
        => IsAdminOrOwner(user)
            || user.IsInRole(Role(LoginRole.PowerUser))
            || user.IsInRole(Role(LoginRole.Accountant));

    public static bool CanDelete(ClaimsPrincipal user)
        => IsAdminOrOwner(user);

    public static bool CanAccessPolicy(ClaimsPrincipal user, string policy)
    {
        if (IsAdminOrOwner(user))
        {
            return true;
        }

        return policy switch
        {
            GarmetixPolicies.Edit => CanEdit(user),
            GarmetixPolicies.Delete => CanDelete(user),
            GarmetixPolicies.Admin or GarmetixPolicies.CompanySetup => false,
            _ when ModuleRoles.TryGetValue(policy, out var roles) => roles.Any(user.IsInRole),
            _ => false
        };
    }

    public static AccessPermissionProfile? ResolveProfile(ClaimsPrincipal user)
    {
        if (IsOwner(user))
        {
            return Profiles.First(profile => profile.Role == "Owner");
        }

        var role = user.FindFirstValue(ClaimTypes.Role);
        return Profiles.FirstOrDefault(profile => string.Equals(profile.Role, role, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOwner(ClaimsPrincipal user)
        => string.Equals(
            user.FindFirstValue("userType"),
            UserType.Owner.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static AccessPermissionProfile Profile(
        string role,
        bool adminWorkspace,
        bool edit,
        bool delete,
        IReadOnlyList<string> modules,
        string notes)
        => new(role, adminWorkspace, modules.Count > 0, edit, delete, modules, notes);

    private static string Role(LoginRole role) => role.ToString();
}

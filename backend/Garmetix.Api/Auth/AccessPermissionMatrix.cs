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
        GarmetixPolicies.StockAudit,
        GarmetixPolicies.InvoiceBookAdjustment,
        GarmetixPolicies.Purchase,
        GarmetixPolicies.Accounting,
        GarmetixPolicies.Hr,
        GarmetixPolicies.Payroll,
        GarmetixPolicies.Attendance,
        GarmetixPolicies.Marketing,
        GarmetixPolicies.Gst,
        GarmetixPolicies.Communication,
        GarmetixPolicies.CommunicationBroadcast,
        GarmetixPolicies.CommunicationTemplates,
        GarmetixPolicies.CommunicationProviders,
        GarmetixPolicies.CommunicationQueue,
        GarmetixPolicies.CommunicationSuppression,
        "Reports"
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
            // Deliberately wider than Inventory itself: Stock Audit is meant to be operable by whoever can
            // physically count stock day to day, including Accountant (who has Edit rights but no broader
            // Inventory module access) and StoreManager (who has Inventory read access but not Edit). Kept
            // as its own policy so granting it never widens access to Products/Stocks/Categories/Brands or
            // the blanket Edit capability elsewhere.
            [GarmetixPolicies.StockAudit] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager),
                Role(LoginRole.Accountant)
            ],
            // Owner/SuperAdmin/Admin already pass every policy via IsAdminOrOwner; Accountant is the
            // only additional role granted here, matching Amit's explicit "owner, admin, accountant level
            // access" for this feature. PowerUser/StoreManager/Salesman are deliberately excluded.
            [GarmetixPolicies.InvoiceBookAdjustment] =
            [
                Role(LoginRole.Accountant)
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
                Role(LoginRole.Payroll)
            ],
            [GarmetixPolicies.Attendance] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager),
                Role(LoginRole.HR),
                Role(LoginRole.Payroll)
            ],
            [GarmetixPolicies.Marketing] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.StoreManager)
            ],
            [GarmetixPolicies.Gst] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.Accountant),
                Role(LoginRole.RemoteAccountant),
                Role(LoginRole.StoreManager)
            ],
            // Communication (personal inbox/compose/read) is for every staff role - it is the
            // narrower CommunicationBroadcast/Templates/Providers/Queue/Suppression policies
            // that are Admin/PowerUser-tier, matching the master prompt's "general
            // communication access must not grant... other sensitive-record access" rule.
            [GarmetixPolicies.Communication] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser),
                Role(LoginRole.Accountant),
                Role(LoginRole.RemoteAccountant),
                Role(LoginRole.StoreManager),
                Role(LoginRole.Salesman),
                Role(LoginRole.HR),
                Role(LoginRole.Payroll)
            ],
            [GarmetixPolicies.CommunicationBroadcast] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser)
            ],
            [GarmetixPolicies.CommunicationTemplates] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser)
            ],
            [GarmetixPolicies.CommunicationProviders] =
            [
                Role(LoginRole.Admin)
            ],
            [GarmetixPolicies.CommunicationQueue] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser)
            ],
            [GarmetixPolicies.CommunicationSuppression] =
            [
                Role(LoginRole.Admin),
                Role(LoginRole.PowerUser)
            ]
        };

    public static IReadOnlyList<AccessPermissionProfile> Profiles { get; } =
    [
        Profile("Super Admin", true, true, true, AllModules, "App-level administration across every company and store."),
        Profile("Owner", true, true, true, AllModules, "Full business and administration control."),
        Profile(Role(LoginRole.Admin), true, true, true, AllModules, "Full administration control."),
        Profile(Role(LoginRole.PowerUser), false, true, false, AllModules, "All operational modules without Admin or delete rights."),
        Profile(Role(LoginRole.Accountant), false, true, false, [GarmetixPolicies.Accounting, GarmetixPolicies.Payroll, "Reports", GarmetixPolicies.Gst, GarmetixPolicies.Communication, GarmetixPolicies.StockAudit, GarmetixPolicies.InvoiceBookAdjustment], "Accounting, payroll, reports, GST, Stock Audit, Invoice Books Adjustment, and internal communication."),
        Profile(Role(LoginRole.RemoteAccountant), false, false, false, [GarmetixPolicies.Accounting, GarmetixPolicies.Payroll, "Reports", GarmetixPolicies.Gst, GarmetixPolicies.Communication], "Accounting review, salary payment review, reports, GST, and internal communication without global edit/delete rights."),
        Profile(Role(LoginRole.StoreManager), false, false, false, [GarmetixPolicies.Billing, GarmetixPolicies.Inventory, GarmetixPolicies.StockAudit, GarmetixPolicies.Purchase, GarmetixPolicies.Accounting, GarmetixPolicies.Hr, GarmetixPolicies.Attendance, GarmetixPolicies.Marketing, GarmetixPolicies.Gst, GarmetixPolicies.Communication, "Reports"], "Store views, Stock Audit, HR attendance, new entries, and internal communication; no Admin, payroll, edit, or delete rights."),
        Profile(Role(LoginRole.Salesman), false, false, false, [GarmetixPolicies.Billing, GarmetixPolicies.Communication], "Billing and customer-facing digital bill entries from sale invoice screens, plus internal communication."),
        Profile(Role(LoginRole.HR), false, false, false, [GarmetixPolicies.Hr, GarmetixPolicies.Attendance, GarmetixPolicies.Communication], "HR and attendance entries, plus internal communication."),
        Profile(Role(LoginRole.Payroll), false, false, false, [GarmetixPolicies.Payroll, GarmetixPolicies.Attendance, GarmetixPolicies.Communication], "Payroll and salary processing entries, plus internal communication."),
        Profile(Role(LoginRole.Member), false, false, false, [], "Authenticated account with no operational module assignment.")
    ];

    public static bool IsAdminOrOwner(ClaimsPrincipal user)
        => IsSuperAdmin(user) || user.IsInRole(Role(LoginRole.Admin)) || IsOwner(user);

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

        if (IsSuperAdmin(user))
        {
            return Profiles.First(profile => profile.Role == "Super Admin");
        }

        var role = user.FindFirstValue(ClaimTypes.Role);
        return Profiles.FirstOrDefault(profile => string.Equals(profile.Role, role, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsOwner(ClaimsPrincipal user)
        => string.Equals(
            user.FindFirstValue("userType"),
            UserType.Owner.ToString(),
            StringComparison.OrdinalIgnoreCase);

    public static bool IsSuperAdmin(ClaimsPrincipal user)
        => bool.TryParse(user.FindFirstValue("superAdmin"), out var superAdmin) && superAdmin;

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

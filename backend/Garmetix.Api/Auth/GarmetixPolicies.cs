using Garmetix.Core.Enums;

namespace Garmetix.Api.Auth;

public static class GarmetixPolicies
{
    public const string Admin = "Admin";
    public const string CompanySetup = "CompanySetup";
    public const string Billing = "Billing";
    public const string Inventory = "Inventory";
    public const string Purchase = "Purchase";
    public const string Accounting = "Accounting";
    public const string Hr = "Hr";
    public const string Payroll = "Payroll";

    public static readonly string[] AdminRoles =
    [
        LoginRole.Admin.ToString(),
        LoginRole.PowerUser.ToString()
    ];

    public static readonly string[] BillingRoles =
    [
        LoginRole.Admin.ToString(),
        LoginRole.PowerUser.ToString(),
        LoginRole.StoreManager.ToString(),
        LoginRole.Salesman.ToString()
    ];

    public static readonly string[] InventoryRoles =
    [
        LoginRole.Admin.ToString(),
        LoginRole.PowerUser.ToString(),
        LoginRole.StoreManager.ToString()
    ];

    public static readonly string[] AccountingRoles =
    [
        LoginRole.Admin.ToString(),
        LoginRole.PowerUser.ToString(),
        LoginRole.Accountant.ToString(),
        LoginRole.RemoteAccountant.ToString()
    ];

    public static readonly string[] HrRoles =
    [
        LoginRole.Admin.ToString(),
        LoginRole.PowerUser.ToString(),
        LoginRole.StoreManager.ToString()
    ];

    public static readonly string[] PayrollRoles =
    [
        LoginRole.Admin.ToString(),
        LoginRole.PowerUser.ToString(),
        LoginRole.Accountant.ToString()
    ];
}

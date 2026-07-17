namespace Garmetix.Api.Auth;

public static class GarmetixPolicies
{
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string CompanySetup = "CompanySetup";
    public const string Edit = "Edit";
    public const string Delete = "Delete";
    public const string Billing = "Billing";
    public const string Inventory = "Inventory";
    public const string Purchase = "Purchase";
    public const string Accounting = "Accounting";
    public const string FinalAccounts = "FinalAccounts";
    public const string Hr = "Hr";
    public const string Payroll = "Payroll";
    public const string Attendance = "Attendance";
    public const string Marketing = "Marketing";
    public const string Gst = "Gst";
    public const string Communication = "Communication";
    public const string CommunicationBroadcast = "CommunicationBroadcast";
    public const string CommunicationTemplates = "CommunicationTemplates";
    public const string CommunicationProviders = "CommunicationProviders";
    public const string CommunicationQueue = "CommunicationQueue";
    public const string CommunicationSuppression = "CommunicationSuppression";

    /// <summary>
    /// Owner userType only - strictly, with no SuperAdmin/Admin fallback. Registered as a
    /// standalone RequireAssertion policy in Program.cs (bypassing AddMatrixPolicy/CanAccessPolicy,
    /// which treats SuperAdmin/Admin/Owner as equivalent), same pattern as GarmetixPolicies.SuperAdmin.
    /// Gates the fully isolated Swalekha personal-finance module.
    /// </summary>
    public const string SwalekhaOwner = "SwalekhaOwner";
}

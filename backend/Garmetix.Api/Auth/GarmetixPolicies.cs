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
    /// <summary>
    /// Dedicated sub-policy for the Stock Audit feature (physical counting by barcode scan), covering both
    /// read and write - StoreManager and Accountant can add scans here even though they don't have (and
    /// shouldn't gain) the broader Inventory module's Products/Stocks/Categories/Brands read/write access or
    /// the blanket Edit capability. Same "dedicated sub-policy" pattern as CommunicationBroadcast/Templates/etc.
    /// </summary>
    public const string StockAudit = "StockAudit";
    /// <summary>
    /// Dedicated sub-policy for the Invoice Books Adjustment page (editing a Sale invoice's GST-filing
    /// BookDate). Owner/SuperAdmin/Admin pass via the standard IsAdminOrOwner bypass; Accountant is the
    /// only other role granted here - PowerUser/StoreManager/Salesman do not get it, since this can move
    /// an invoice's tax liability into a different GST return period. Same dedicated-policy pattern as
    /// StockAudit/CommunicationBroadcast.
    /// </summary>
    public const string InvoiceBookAdjustment = "InvoiceBookAdjustment";
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

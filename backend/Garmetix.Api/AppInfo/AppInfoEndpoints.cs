using System.Diagnostics;
using System.Reflection;

namespace Garmetix.Api.AppInfo;

public static class AppInfoEndpoints
{
    public const string ProductName = "Garmetix";
    public const string Version = "4.7.5";
    public const string Stage = "Stage 8H Package 6 Purchase Inward Pro";
    public const string ReleaseName = "Purchase Inward Pro";
    public const string BuildDate = "2026-06-17";
    public const string BuildCode = "GARMETIX-8H-20260617-4750";

    public static RouteGroupBuilder MapAppInfoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/app-info")
            .WithTags("App Info")
            .AllowAnonymous();

        group.MapGet("", Info);
        group.MapGet("/version", VersionOnly);
        group.MapGet("/faq", Faq);
        group.MapGet("/contacts", Contacts);
        group.MapGet("/system", SystemInfo);

        return group;
    }

    private static IResult Info(IHostEnvironment environment)
        => Results.Ok(Create(environment.EnvironmentName));

    private static IResult VersionOnly(IHostEnvironment environment)
        => Results.Ok(new
        {
            productName = ProductName,
            version = Version,
            stage = Stage,
            releaseName = ReleaseName,
            buildDate = BuildDate,
            buildCode = BuildCode,
            environment = environment.EnvironmentName,
            assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        });


    private static IResult SystemInfo(IHostEnvironment environment)
    {
        var startedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var now = DateTime.UtcNow;
        return Results.Ok(new AppSystemInfoDto(
            ProductName,
            Version,
            Stage,
            ReleaseName,
            BuildDate,
            BuildCode,
            environment.EnvironmentName,
            now.ToString("O"),
            startedAt.ToString("O"),
            Convert.ToInt64((now - startedAt).TotalSeconds),
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty));
    }

    private static IResult Faq() => Results.Ok(FaqItems);

    private static IResult Contacts() => Results.Ok(ContactItems);

    private static AppVersionDto Create(string environment)
        => new(
            ProductName,
            Version,
            Stage,
            ReleaseName,
            BuildDate,
            BuildCode,
            environment,
            Highlights,
            ContactItems,
            FaqItems);

    private static readonly string[] Highlights =
    [
        "Automated backend, frontend and production smoke-test scripts now ship with the release package.",
        "The API exposes a test-automation manifest and runtime smoke endpoint for deployment validation.",
        "Frontend smoke checks verify the public login page and proxied app-info endpoint without a browser driver.",
        "Linux, WSL and Windows test runners can execute backend unit tests, Nuxt build checks and optional Docker health checks.",
        "Tailoring and alteration orders now follow order-delivery-invoice-payment SOP with delivery schedule, pending, delivered, invoiced, paid and completed status tracking.",
        "Readymade garment alteration can be assigned to tailoring vendors with customer-chargeable, in-house expense, or complimentary cost responsibility.",
        "Tailoring service items are reusable in orders and service invoice conversion, with default customer rate, vendor rate, HSN/SAC and active status.",
        "Customer receipts and vendor payments are linked back to the tailoring order so order history, balances and profit impact remain visible.",
        "Bank reconciliation now tracks open and reconciled statement lines with operator, date, reference, and remarks.",
        "Cheque logs now support an auditable lifecycle from issued/deposited through cleared, bounced, or cancelled with action timestamps.",
        "Fresh production installs continue to create one clean schema baseline from the current DbContext model instead of replaying historical branch migrations.",
        "Sales split payments now post separate settlement ledger and customer-credit rows for every collected payment line.",
        "Cash, bank, cheque, card, UPI, credit note, advance, store-credit and loyalty rows are routed to their own settlement ledgers instead of one mixed-payment ledger.",
        "Invoice payment rows now retain a structured payment-details JSON snapshot with mode, account, reference, gateway, cheque, card, UPI, and adjustment metadata.",
        "Duplicate use of the same advance receipt, credit note, store credit, or loyalty redemption is rejected before balances are changed.",
        "Nuxt font resolution now runs in local/no-provider mode so offline builds no longer depend on external font metadata.",
        "Owner and Admin users can move eligible cash records between Off Book Cash Vouchers and regular accounting vouchers without duplicating the amount.",
        "Every conversion retains source and destination numbers, direction, amount, reason, operator, and timestamp in an immutable audit history.",
        "Moving a regular cash voucher Off Book removes its journal postings and preserves the original source as an audit-only record.",
        "Sales Invoice creation now uses a stable full page with draft recovery, mobile customer lookup, contextual payments, rounded totals, and automatic print after save.",
        "Billing create, cancellation, return, and exchange transactions now run through the configured PostgreSQL retry execution strategy.",
        "B2B interstate GST is derived from supplier and customer GSTIN state codes, with IGST applied to interstate GST supplies.",
        "The sidebar footer again presents Status above Notifications as two full-width vertical actions.",
        "Inventory now includes a dedicated Stock Reports workspace for receipt-age indicators, configurable low-stock risk, weighted-average valuation, and ledger reconciliation.",
        "Stock report age bands cover 0-30, 31-60, 61-90, 91-180, and 180+ days, plus no-receipt-history and out-of-stock states.",
        "Stock Reports exposes Critical, Low, Watch, and Healthy risk bands, pending stock-accounting documents, searchable filters, and CSV export.",
        "Ledger quantity and average cost are compared with the operational stock projection so rebuild mismatches remain visible by product and store.",
        "Stock excess, shortage, write-off, and inter-store transfer documents now post balanced double-entry accounting at weighted-average cost.",
        "Store-wise Stock-in-Hand ledgers are created internally, with stock gains in Indirect Income and shortages/write-offs in Indirect Expenses.",
        "Stock Operations now includes a dedicated Write-off workflow with StoreCode/YYYYMM/WO/series numbering and linked movement and journal audit.",
        "Formal stock-operation documents now show Posted, Not Required, or Pending accounting status and their linked journal number.",
        "Status and Notifications are available as separate vertical actions in the sidebar footer.",
        "Stock adjustments, transfers, and physical counts now create formal header and item documents with immutable product, store, quantity, and value snapshots.",
        "Every posted stock-operation document links its movement-ledger rows and uses server-owned StoreCode/YYYYMM/ADJ, ST, or PHY numbering.",
        "The Stock Operations workspace now includes a searchable document register, operation filter, wide audit detail, and direct QR download.",
        "Universal document scanning now opens permitted stock-operation details by stable QR token or document number.",
        "Purchase returns now persist an immutable ITC reversal row for every returned item, including HSN and exact CGST, SGST, and IGST values.",
        "Purchase return details now reconcile the return header, item snapshots, stock movement, debit note, Input GST journal, and settlement state.",
        "Full purchase cancellations use the same item-level ITC reversal and reconciliation links as partial returns.",
        "The dashboard shell now has one header bar, one collapse control, one account menu, and notifications in the sidebar footer.",
        "Vendor debit notes can now be allocated across one or more outstanding purchase invoices without duplicating their accounting journal.",
        "Actual supplier refunds create receipt vouchers and balanced cash or bank entries linked to the vendor, return, debit note, journal, and bank transaction.",
        "Vendor settlements support adjustment-only, refund-only, and mixed workflows with server-owned StoreCode/YYYYMM/VSET/series numbering.",
        "The dedicated Vendor Settlements workspace shows open debit-note credit, invoice allocation, refund receipt, settlement history, and audit links.",
        "Non-cash vendor refunds require a receiving bank account, while generated refund vouchers use StoreCode/YYYYMM/VREF/series numbering and printable QR receipts.",
        "Purchase returns now generate colored A4 or A5 Purchase Return and Debit Note PDFs containing every returned item and exact GST reversal totals.",
        "Each purchase-return document carries a stable QR code that opens the permitted return detail from Document Scanner.",
        "New returns open their first print automatically, while print count, reprint state, and last print time remain auditable.",
        "The Purchase Return register now filters print state and provides direct print, reprint, download, and detail actions.",
        "Purchase returns now persist formal return headers and item-level product, HSN, GST, quantity, rate, vendor, and original-purchase snapshots.",
        "New purchase return numbers use a server-owned StoreCode/YYYYMM/PR/series sequence.",
        "Partial returns and full purchase cancellations now link stock movements and debit notes to the formal return document.",
        "The Purchase Return workspace now includes a searchable return register and snapshot detail view.",
        "Legacy stock-movement-only purchase returns remain included when calculating returnable quantities.",
        "One server-owned permission matrix now drives Admin, entry, edit, delete, and operational module authorization.",
        "The Access workspace displays the effective role matrix returned by the API.",
        "Store Manager retains store-module view and entry access without Admin, global edit, or delete rights.",
        "Dedicated HR and Payroll roles open only their assigned people module and land there after login.",
        "Automated role tests cover Owner, Admin, Power User, Accountant, Store Manager, Salesman, HR, and Payroll permissions.",
        "User activation, deactivation, role assignment, password administration, and deletion are now explicit audited workflows.",
        "Inactive users are denied at login and immediately blocked from authenticated API requests.",
        "Admin access is derived from the Admin role and can no longer be enabled through a separate user-facing flag.",
        "Administrative password resets use a dedicated endpoint and revoke outstanding reset tokens.",
        "The last active admin and the currently signed-in account are protected from accidental deactivation or deletion.",
        "The Stage 8A page audit queue is complete across operational, administration, maintenance, profile, and help workspaces.",
        "GST Returns now preserves local invoice and accounting dates and retains setup, draft, and review failures with direct retry actions.",
        "Profile, About Garmetix, Contact Us, and Help and FAQ now use consistent headers, loading states, and business-facing guidance.",
        "FAQ category filtering now uses a safe internal all-value sentinel instead of an invalid empty selector value.",
        "Company Setup and Roles and Users now use retryable registers, responsive tables and wide modal workspaces.",
        "Company, store-group and store dates preserve the selected local calendar date without UTC conversion.",
        "Company Onboarding and AF/SS Defaults now provide retained load failures, retry actions and responsive loading states.",
        "Internal migration, source-file and legacy implementation notes are no longer exposed on Admin business pages.",
        "Reports Center, GST Reports, Import Export, Audit Trail and Message Logs now use consistent retryable register states.",
        "Reports Center date defaults preserve the local calendar date instead of converting through UTC.",
        "Report, audit and data-operation failures remain visible with a direct retry action.",
        "Large report tables stay inside responsive scroll containers while actions wrap across smaller screens.",
        "Message Logs use compact operational panels and expandable technical details.",
        "Party and bank-account ledgers remain server-owned and synchronized by the accounting service.",
        "Salary payments now pre-calculate advance deductions, previous company dues and already-paid amounts.",
        "Printable business documents now carry stable permission-aware QR codes.",
        "Non-GST sale, purchase, stock, settlement and reporting now operate as an independent Off Book subsystem.",
        "Regular billing, purchase, stock operations, imports and dashboard totals explicitly exclude Off Book stock.",
        "Frontend, backend, npm package and .NET assembly versions are synchronized for every release."
    ];

    private static readonly AppContactDto[] ContactItems =
    [
        new("Business owner", "Amit Kumar", "person", "Primary business owner and authorization contact."),
        new("Email", "ameetkrsah@gmail.com", "email", "Use for account and support communication."),
        new("Location", "Dumka / Jamshedpur, Jharkhand, India", "location", "Business operating region."),
        new("Product", "Garmetix", "product", "Garment store management, billing, purchase, inventory, GST and controlled administration.")
    ];

    private static readonly AppFaqDto[] FaqItems =
    [
        new("How do I know which Garmetix version is running?", "Open About Garmetix and check the Version, Stage, Build Date and Build Code.", "Version"),
        new("Where do I create the first company?", "Use Admin > Company Onboarding. It creates the company, store group, store, initial users, employees and optional basic masters.", "Company"),
        new("Where do I prepare AF/SS default data?", "Use Admin > AF/SS Defaults, select the target company and profile, then apply the required defaults.", "Company"),
        new("Where can I see success or failure messages?", "Use Data > Message Logs. You can filter by source, level, success or failure, search text and date range.", "Logs"),
        new("How do I edit my own profile?", "Use Account > My Profile. You can change your name, username, email and password. Role, permissions and workspace assignment remain admin-controlled.", "Profile"),
        new("How do I prepare a GST return?", "Use GST > GST Returns, choose Load From Books, review the prepared values, save a draft if needed and confirm the return before export or filing.", "GST"),
        new("Where do I record Off Book Non-GST goods?", "Use Off Book > Non-GST Goods. Its sales, purchases, stock, settlements and PDFs are independent and do not create regular invoices, purchase bills, GST rows, ledgers, journals, vouchers or bank transactions.", "Non-GST Goods"),
        new("What details should I include when requesting support?", "Include the page name, operation performed, approximate time, visible message, and the Version and Build Code shown in About Garmetix.", "Support")
    ];
}

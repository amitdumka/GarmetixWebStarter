from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks: list[tuple[str, bool]] = []


def read(path: str) -> str:
    return (root / path).read_text(encoding="utf-8")


def add(name: str, ok: bool):
    checks.append((name, ok))


invoice_page = read("frontend/garmetix-web/pages/billing/new.vue")
app_shell = read("frontend/garmetix-web/components/AppShell.vue")
legacy_shell = read("frontend/garmetix-web/components/AppShellLegacy.vue")
billing_endpoints = read("backend/Garmetix.Api/Billing/BillingEndpoints.cs")
app_info = read("backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs")
app_version = read("frontend/garmetix-web/utils/appVersion.ts")
csproj = read("backend/Garmetix.Api/Garmetix.Api.csproj")

version_identity = (
    all(token in app_info for token in ['Version = "4.10.24"', "GARMETIX-10J-20260620-4124"])
    and "APP_VERSION = '4.10.24'" in app_version
    and "<Version>4.10.24</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.25"', "GARMETIX-10J-20260620-4125"])
    and "APP_VERSION = '4.10.25'" in app_version
    and "<Version>4.10.25</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.26"', "GARMETIX-10J-20260620-4126"])
    and "APP_VERSION = '4.10.26'" in app_version
    and "<Version>4.10.26</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.27"', "GARMETIX-10J-20260620-4127"])
    and "APP_VERSION = '4.10.27'" in app_version
    and "<Version>4.10.27</Version>" in csproj
) or (
    all(token in app_info for token in ['Version = "4.10.28"', "GARMETIX-10J-20260620-4128"])
    and "APP_VERSION = '4.10.28'" in app_version
    and "<Version>4.10.28</Version>" in csproj
)
add("version identity", version_identity)
add(
    "dedicated full page route",
    "<AppShell" in invoice_page
    and "title=\"New Sales Invoice\"" in invoice_page
    and "UiModulePageHeader" in invoice_page
    and "UModal" not in invoice_page
    and "USlideover" not in invoice_page,
)
add(
    "draft persistence and reset",
    all(token in invoice_page for token in [
        "draftKey = 'garmetix.billing.new.draft.v1'",
        "persistDraft",
        "restoreDraft",
        "clearDraft",
        "resetForNextInvoice",
        "watch([form, adjustments, cart, payments], persistDraft, { deep: true })",
    ]),
)
add(
    "mobile first customer lookup",
    all(token in invoice_page for token in [
        "form.customerMobileNumber",
        "searchCustomer",
        "billing/customers/search",
        "New customer",
        "This customer will be added automatically when the invoice is saved.",
        "billing/customers/${customerId}/profile",
    ]),
)
add(
    "manager salesman fallback",
    all(token in invoice_page for token in ["setDefaultSalesman", "placeholder=\"Manager\"", "item.name || '').trim().toLowerCase() === 'manager'"])
    and all(token in billing_endpoints for token in [
        "ResolveRequiredSalesmanIdAsync",
        "GetDefaultSalesmanIdAsync",
        'item.Name == "Manager"',
        "db.Salesmen.Add(manager)",
        "manager.Active = true",
    ]),
)
add(
    "compact invoice controls",
    all(token in invoice_page for token in [
        'label="Save & Print"',
        'label="Add Item"',
        'label="Add Payment"',
        "class=\"add-item-button\"",
        "class=\"compact-field\"",
        "grid-template-columns: minmax(11rem, .8fr) minmax(15rem, 1.6fr) 5rem 8rem auto",
        "@media (max-width: 1400px)",
    ]),
)
add(
    "payment details and bank safety",
    all(token in invoice_page for token in [
        "paymentRequiresBank",
        "bankAccountOptions",
        "Select bank account",
        "paymentReferenceLabel",
        "Gateway / app",
        "Settlement status",
        "paymentModeValue.mixPayments",
    ]),
)
add(
    "save print then next invoice",
    all(token in invoice_page for token in [
        "api.create<any>('billing/sales'",
        "documentPrint.printPdf(`billing/sales/${response.invoiceId}/pdf?format=a4&copy=customer&reprint=false&signatures=true`)",
        "Invoice saved and next bill is ready",
        "resetForNextInvoice()",
    ]),
)
add(
    "navigation exposes new invoice page",
    "{ to: '/billing/new', label: 'New Sale Invoice'" in app_shell
    and "{ to: '/billing/new', label: 'New Sale Invoice'" in legacy_shell,
)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(("PASS" if ok else "FAIL") + f": {name}")
if failed:
    raise SystemExit("Sale invoice acceptance validation failed: " + ", ".join(failed))
print("Sale invoice acceptance validation passed.")

using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsAccountingMasterAuditService(GarmetixDbContext db)
{
    public async Task<FinalAccountsAccountingMasterAuditResponse> RunAsync(
        FinalAccountsAccountingMasterAuditQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var masterCounts = new List<FinalAccountsAccountingMasterCountDto>();
        var issues = new List<FinalAccountsAccountingMasterIssueDto>();
        var duplicates = new List<FinalAccountsAccountingDuplicateDto>();

        var ledgerGroups = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.LedgerGroups.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var ledgers = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Ledgers.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var parties = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Parties.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var customers = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var vendors = ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var sales = ApplyStoreScope(WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var purchases = ApplyStoreScope(WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var vouchers = ApplyStoreScope(WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var cashVouchers = ApplyStoreScope(WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var salaryPayments = ApplyStoreScope(WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var mappings = ApplyFinalAccountsScope(db.FinalAccountsAccountMappings.AsNoTracking().Where(item => !item.Deleted), scope);
        var faAccounts = ApplyFinalAccountsScope(db.FinalAccountsAccounts.AsNoTracking().Where(item => !item.Deleted), scope);
        var sourceLinks = ApplyFinalAccountsScope(db.FinalAccountsSourcePostingLinks.AsNoTracking().Where(item => !item.Deleted), scope);

        var ledgerGroupCount = await ledgerGroups.CountAsync(cancellationToken);
        var ledgerCount = await ledgers.CountAsync(cancellationToken);
        var partyCount = await parties.CountAsync(cancellationToken);
        var customerCount = await customers.CountAsync(cancellationToken);
        var vendorCount = await vendors.CountAsync(cancellationToken);
        var finalAccountsAccountCount = await faAccounts.CountAsync(cancellationToken);
        var mappingCount = await mappings.CountAsync(cancellationToken);

        masterCounts.Add(new("Books", "Ledger groups", ledgerGroupCount, "Existing Books/accounting ledger group master."));
        masterCounts.Add(new("Books", "Ledgers", ledgerCount, "Existing Books/accounting ledger master; target canonical source for Final Accounts."));
        masterCounts.Add(new("Books", "Parties", partyCount, "Existing accounting party records linked to ledgers."));
        masterCounts.Add(new("CRM/Purchase", "Customers", customerCount, "Operational customer records that should resolve to canonical party ledgers."));
        masterCounts.Add(new("Purchase", "Vendors", vendorCount, "Operational vendor records that should resolve to canonical party ledgers."));
        masterCounts.Add(new("Final Accounts", "Accounts", finalAccountsAccountCount, "Current Final Accounts account master; should converge to canonical Books ledgers in BS-20."));
        masterCounts.Add(new("Final Accounts", "Mappings", mappingCount, "Current Final Accounts mapping rows; should become exception-only after unification."));

        if (ledgerGroupCount == 0)
        {
            issues.Add(Issue("Error", "NoLedgerGroups", "Books", ledgerGroupCount, "No ledger groups were found in scope.", "Create or migrate Indian/Tally-style ledger groups before unifying Final Accounts."));
        }

        if (ledgerCount == 0)
        {
            issues.Add(Issue("Error", "NoLedgers", "Books", ledgerCount, "No ledgers were found in scope.", "Do not backfill Final Accounts until canonical ledgers exist."));
        }

        var ledgersMissingGroup = await ledgers
            .Where(ledger => !ledgerGroups.Any(group => group.Id == ledger.LedgerGroupId))
            .CountAsync(cancellationToken);
        if (ledgersMissingGroup > 0)
        {
            issues.Add(Issue("Error", "LedgerMissingGroup", "Books", ledgersMissingGroup, "Ledgers point to missing ledger groups.", "Fix or remap these ledgers before COA normalization."));
        }

        var partiesMissingLedger = await parties
            .Where(party => party.LedgerId == Guid.Empty || !ledgers.Any(ledger => ledger.Id == party.LedgerId))
            .CountAsync(cancellationToken);
        if (partiesMissingLedger > 0)
        {
            issues.Add(Issue("Error", "PartyMissingLedger", "Books", partiesMissingLedger, "Parties are not linked to a valid ledger.", "Relink parties to canonical ledgers in BS-18."));
        }

        var customersMissingParty = await customers.Where(customer => customer.PartyId == null).CountAsync(cancellationToken);
        if (customersMissingParty > 0)
        {
            issues.Add(Issue("Warning", "CustomerMissingParty", "CRM", customersMissingParty, "Customers are not linked to accounting parties.", "Design Customer -> Party -> Ledger linking in BS-18."));
        }

        var vendorsMissingParty = await vendors.Where(vendor => vendor.PartyId == null).CountAsync(cancellationToken);
        if (vendorsMissingParty > 0)
        {
            issues.Add(Issue("Warning", "VendorMissingParty", "Purchase", vendorsMissingParty, "Vendors are not linked to accounting parties.", "Design Vendor -> Party -> Ledger linking in BS-18."));
        }

        var vouchersMissingLedger = await vouchers.Where(voucher => voucher.LedgerId == null).CountAsync(cancellationToken);
        if (vouchersMissingLedger > 0)
        {
            issues.Add(Issue("Warning", "VoucherMissingLedger", "Books", vouchersMissingLedger, "Vouchers do not point to a ledger.", "Classify vouchers before direct ledger integration."));
        }

        var cashVouchersMissingLedger = await cashVouchers.Where(voucher => voucher.LedgerId == null).CountAsync(cancellationToken);
        if (cashVouchersMissingLedger > 0)
        {
            issues.Add(Issue("Warning", "CashVoucherMissingLedger", "Books/POS", cashVouchersMissingLedger, "Cash vouchers do not point to a ledger.", "Classify cash voucher rows before backfill."));
        }

        var salaryPaymentsMissingVoucherNumber = await salaryPayments.Where(payment => payment.VoucherNumber == null || payment.VoucherNumber == string.Empty).CountAsync(cancellationToken);
        if (salaryPaymentsMissingVoucherNumber > 0)
        {
            issues.Add(Issue("Warning", "SalaryPaymentMissingVoucherNumber", "HR/Payroll", salaryPaymentsMissingVoucherNumber, "Salary payments are missing voucher numbers.", "Stabilize payroll source references before backfill."));
        }

        duplicates.AddRange(await DuplicateLedgersAsync(ledgers, cancellationToken));
        duplicates.AddRange(await DuplicatePartiesAsync(parties, cancellationToken));
        duplicates.AddRange(await DuplicateCustomersAsync(customers, cancellationToken));
        duplicates.AddRange(await DuplicateVendorsAsync(vendors, cancellationToken));

        var sourceCoverage = await BuildSourceCoverageAsync(sales, purchases, vouchers, cashVouchers, salaryPayments, sourceLinks, cancellationToken);
        foreach (var coverage in sourceCoverage.Where(item => item.SourceRows > 0 && item.PendingRows > 0))
        {
            issues.Add(Issue(
                FinalAccountsAccountingMasterAuditRules.SeverityForMissingLinks(coverage.PendingRows, coverage.SourceRows),
                $"Pending{coverage.SourceModule}FinalAccountsLinks",
                coverage.SourceModule,
                coverage.PendingRows,
                $"{coverage.SourceModule} source rows are not linked to Final Accounts posting links.",
                "Run dry-run reconciliation first; do not auto-post until mapping/unification is reviewed."));
        }

        return new FinalAccountsAccountingMasterAuditResponse(
            DateTimeOffset.UtcNow,
            scope,
            FinalAccountsAccountingMasterAuditRules.StageName,
            WritesData: false,
            FinalAccountsAccountingMasterAuditRules.BackupRequirement,
            masterCounts,
            sourceCoverage,
            issues.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.Area).ThenBy(item => item.Code).ToList(),
            duplicates,
            FinalAccountsAccountingMasterAuditRules.Recommendations);
    }

    private async Task<IReadOnlyList<FinalAccountsAccountingSourceCoverageDto>> BuildSourceCoverageAsync(
        IQueryable<Invoice> sales,
        IQueryable<PurchaseInvoice> purchases,
        IQueryable<Voucher> vouchers,
        IQueryable<CashVoucher> cashVouchers,
        IQueryable<SalaryPayment> salaryPayments,
        IQueryable<FinalAccountsSourcePostingLink> sourceLinks,
        CancellationToken cancellationToken)
    {
        var salesRows = await sales.CountAsync(cancellationToken);
        var salesTotal = await SumAsync(sales.Select(item => item.BillAmount), cancellationToken);
        var purchaseRows = await purchases.CountAsync(cancellationToken);
        var purchaseTotal = await SumAsync(purchases.Select(item => item.BillAmount), cancellationToken);
        var cashBankRows = await vouchers.CountAsync(cancellationToken) + await cashVouchers.CountAsync(cancellationToken);
        var cashBankTotal = await SumAsync(vouchers.Select(item => item.Amount), cancellationToken) + await SumAsync(cashVouchers.Select(item => item.Amount), cancellationToken);
        var payrollRows = await salaryPayments.CountAsync(cancellationToken);
        var payrollTotal = await SumAsync(salaryPayments.Select(item => item.Amount), cancellationToken);
        var expenseRows = await vouchers.Where(item => item.VoucherType == VoucherType.Expense).CountAsync(cancellationToken)
            + await cashVouchers.Where(item => item.VoucherType == VoucherType.Expense).CountAsync(cancellationToken);
        var expenseTotal = await SumAsync(vouchers.Where(item => item.VoucherType == VoucherType.Expense).Select(item => item.Amount), cancellationToken)
            + await SumAsync(cashVouchers.Where(item => item.VoucherType == VoucherType.Expense).Select(item => item.Amount), cancellationToken);

        var linkCounts = await sourceLinks
            .GroupBy(item => item.SourceType)
            .Select(group => new { SourceType = group.Key, Count = group.Select(item => item.SourceId).Distinct().Count() })
            .ToListAsync(cancellationToken);
        var lookup = linkCounts.ToDictionary(item => FinalAccountsPostingRules.NormalizeSourceType(item.SourceType), item => item.Count, StringComparer.OrdinalIgnoreCase);

        return
        [
            Coverage("Sales", salesRows, lookup, salesTotal, "Sales invoices from POS/Main billing."),
            Coverage("Purchase", purchaseRows, lookup, purchaseTotal, "Purchase invoices and inward entries."),
            Coverage("CashBank", cashBankRows, lookup, cashBankTotal, "Vouchers, cash vouchers and settlement payments using cash/bank rails."),
            Coverage("Payroll", payrollRows, lookup, payrollTotal, "Salary payments and payroll settlement rows."),
            Coverage("Expense", expenseRows, lookup, expenseTotal, "Expense vouchers that require direct/indirect expense classification.")
        ];
    }

    private static FinalAccountsAccountingSourceCoverageDto Coverage(
        string module,
        int sourceRows,
        IReadOnlyDictionary<string, int> linkCounts,
        decimal total,
        string notes)
    {
        var normalized = FinalAccountsPostingRules.NormalizeSourceType(module);
        linkCounts.TryGetValue(normalized, out var linkedRows);
        return new(module, sourceRows, linkedRows, FinalAccountsAccountingMasterAuditRules.PendingCount(sourceRows, linkedRows), FinalAccountsJournalRules.RoundAmount(total), notes);
    }

    private static async Task<IReadOnlyList<FinalAccountsAccountingDuplicateDto>> DuplicateLedgersAsync(IQueryable<Ledger> ledgers, CancellationToken cancellationToken)
        => await DuplicateAsync(
            ledgers.Where(item => item.Name != string.Empty)
                .GroupBy(item => new { item.CompanyId, item.Name })
                .Where(group => group.Count() > 1)
                .Select(group => new { Key = group.Key.Name, Count = group.Count(), Sample = group.Select(item => item.Id.ToString()).FirstOrDefault() ?? string.Empty }),
            "Ledger",
            "Merge or rename duplicate ledger masters before canonical COA migration.",
            cancellationToken);

    private static async Task<IReadOnlyList<FinalAccountsAccountingDuplicateDto>> DuplicatePartiesAsync(IQueryable<Party> parties, CancellationToken cancellationToken)
        => await DuplicateAsync(
            parties.Where(item => item.Name != string.Empty)
                .GroupBy(item => new { item.CompanyId, item.Name, item.Category })
                .Where(group => group.Count() > 1)
                .Select(group => new { Key = $"{group.Key.Category}:{group.Key.Name}", Count = group.Count(), Sample = group.Select(item => item.Id.ToString()).FirstOrDefault() ?? string.Empty }),
            "Party",
            "Merge or relink duplicate parties before Customer/Vendor/Employee unification.",
            cancellationToken);

    private static async Task<IReadOnlyList<FinalAccountsAccountingDuplicateDto>> DuplicateCustomersAsync(IQueryable<Customer> customers, CancellationToken cancellationToken)
        => await DuplicateAsync(
            customers.Where(item => item.MobileNumber != string.Empty)
                .GroupBy(item => new { item.CompanyId, item.MobileNumber })
                .Where(group => group.Count() > 1)
                .Select(group => new { Key = group.Key.MobileNumber, Count = group.Count(), Sample = group.Select(item => item.Id.ToString()).FirstOrDefault() ?? string.Empty }),
            "Customer mobile",
            "Review duplicate customer identities before party-ledger linking.",
            cancellationToken);

    private static async Task<IReadOnlyList<FinalAccountsAccountingDuplicateDto>> DuplicateVendorsAsync(IQueryable<Vendor> vendors, CancellationToken cancellationToken)
        => await DuplicateAsync(
            vendors.Where(item => item.GSTIN != null && item.GSTIN != string.Empty)
                .GroupBy(item => new { item.CompanyId, item.GSTIN })
                .Where(group => group.Count() > 1)
                .Select(group => new { Key = group.Key.GSTIN ?? string.Empty, Count = group.Count(), Sample = group.Select(item => item.Id.ToString()).FirstOrDefault() ?? string.Empty }),
            "Vendor GSTIN",
            "Review duplicate vendor identities before party-ledger linking.",
            cancellationToken);

    private static async Task<IReadOnlyList<FinalAccountsAccountingDuplicateDto>> DuplicateAsync<T>(
        IQueryable<T> query,
        string area,
        string suggestedAction,
        CancellationToken cancellationToken)
    {
        var rows = await query.Take(25).ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var key = row?.GetType().GetProperty("Key")?.GetValue(row)?.ToString() ?? string.Empty;
            var count = row?.GetType().GetProperty("Count")?.GetValue(row) is int value ? value : 0;
            var sample = row?.GetType().GetProperty("Sample")?.GetValue(row)?.ToString() ?? string.Empty;
            return new FinalAccountsAccountingDuplicateDto(area, key, count, sample, suggestedAction);
        }).ToList();
    }

    private static FinalAccountsAccountingMasterIssueDto Issue(string severity, string code, string area, int count, string message, string suggestedAction)
        => new(severity, code, area, count, message, suggestedAction);

    private static async Task<decimal> SumAsync(IQueryable<decimal> query, CancellationToken cancellationToken)
        => await query.DefaultIfEmpty().SumAsync(cancellationToken);

    private static int SeverityRank(string severity)
        => severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 3
            : severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 2
            : 1;

    private static IQueryable<T> ApplyCompanyScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope)
        where T : class
    {
        if (scope.CompanyId.HasValue && HasProperty<T>("CompanyId"))
        {
            query = typeof(T).GetProperty("CompanyId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "CompanyId") == scope.CompanyId.Value)
                : query.Where(item => EF.Property<Guid>(item, "CompanyId") == scope.CompanyId.Value);
        }

        return query;
    }

    private static IQueryable<T> ApplyStoreScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope)
        where T : class
    {
        query = ApplyCompanyScope(query, scope);
        if (scope.StoreGroupId.HasValue && HasProperty<T>("StoreGroupId"))
        {
            query = typeof(T).GetProperty("StoreGroupId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "StoreGroupId") == scope.StoreGroupId.Value)
                : query.Where(item => EF.Property<Guid>(item, "StoreGroupId") == scope.StoreGroupId.Value);
        }

        if (scope.StoreId.HasValue && HasProperty<T>("StoreId"))
        {
            query = typeof(T).GetProperty("StoreId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "StoreId") == scope.StoreId.Value)
                : query.Where(item => EF.Property<Guid>(item, "StoreId") == scope.StoreId.Value);
        }

        return query;
    }

    private static IQueryable<T> ApplyFinalAccountsScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope)
        where T : class
    {
        query = ApplyCompanyScope(query, scope);
        if (scope.StoreGroupId.HasValue && HasProperty<T>("StoreGroupId"))
        {
            query = typeof(T).GetProperty("StoreGroupId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "StoreGroupId") == scope.StoreGroupId.Value)
                : query.Where(item => EF.Property<Guid>(item, "StoreGroupId") == scope.StoreGroupId.Value);
        }

        if (scope.StoreId.HasValue && HasProperty<T>("StoreId"))
        {
            query = typeof(T).GetProperty("StoreId")?.PropertyType == typeof(Guid?)
                ? query.Where(item => EF.Property<Guid?>(item, "StoreId") == scope.StoreId.Value)
                : query.Where(item => EF.Property<Guid>(item, "StoreId") == scope.StoreId.Value);
        }

        return query;
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(companyId, storeGroupId, storeId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static bool HasProperty<T>(string propertyName)
        => typeof(T).GetProperty(propertyName) is not null;
}

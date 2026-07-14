# GarmetixWebStarter — Final Accounts, Balance Sheet & Projection Module

**Document:** `roadmap-balancesheet.md`  
**Target Git branch:** `balancesheet`  
**Future merge target:** `version6`  
**Module name:** Final Accounts & Projections  
**Recommended route root:** `/final-accounts/`  
**Recommended API root:** `/api/final-accounts/`  
**Database isolation:** PostgreSQL schema `final_accounts`  
**Default feature state:** Disabled  
**Status:** Implementation specification and agent handoff

---

## 1. Executive mandate

Build a new, isolated Garmetix module capable of producing accountant-grade financial reports from Garmetix transactions:

1. General Ledger and Chart of Accounts.
2. Ledger report and Day Book accounting view.
3. Trial Balance.
4. Trading Account.
5. Profit & Loss Account.
6. Balance Sheet.
7. Cash Flow Statement.
8. Schedules, notes and comparative statements.
9. CA adjustment and review workspace.
10. Financial-year closing and opening-balance carry forward.
11. Projected P&L, projected Balance Sheet and projected Cash Flow.
12. Ratio analysis, working-capital analysis and bank-loan projections.
13. TallyPrime data exchange and CA review package.
14. Complete audit trail, permissions, validation and reconciliation.

This module must be implemented like the existing HR or POS modules: separately organised, separately permissioned, separately routable, disabled by default, and unable to change the behaviour of the current deployed application until explicitly enabled after merge.

---

## 2. Non-negotiable safety and isolation rules

### 2.1 Git rules

- Work only on branch `balancesheet`.
- Start from the latest accepted `version6` branch.
- Never commit directly to `version6`, `main`, `master`, or a release branch.
- Never merge `balancesheet` into `version6` automatically.
- Never force-push.
- Use small, reviewable stage commits.
- Keep the branch buildable at every completed stage.
- Record each commit hash in `todo-balancesheet.md`.
- Do not rewrite existing branch history.

Recommended branch setup:

```bash
git status
git fetch --all --prune
git switch version6
git pull --ff-only origin version6

if git show-ref --verify --quiet refs/heads/balancesheet; then
  git switch balancesheet
  git rebase version6
else
  git switch -c balancesheet
fi

git push -u origin balancesheet
```

If the repository has uncommitted work, do not discard it. Stop branch switching, record the files, and preserve them by following the repository owner’s established workflow.

### 2.2 Production rules

The agent must not:

- SSH to the current production server.
- execute deployment scripts;
- run `docker compose up`, `down`, `restart`, `pull`, or `build` on production;
- apply migrations to the production database;
- change Cloudflare Tunnel, DNS, reverse proxy, production secrets, or production `.env`;
- enable the module on the deployed server;
- modify CI/CD so feature-branch pushes deploy to production;
- replace the current production image;
- run historical backfill against the production database;
- alter, delete, or rewrite current operational records.

All development and testing must use a local development database, isolated test database, disposable container database, or approved staging clone.

### 2.3 Code isolation rules

- New backend code belongs under a dedicated `FinalAccounts` module namespace/folder.
- New frontend code belongs under a dedicated `final-accounts` feature folder and route root.
- New database objects belong to PostgreSQL schema `final_accounts`.
- Migrations must be additive.
- Do not rename or drop existing columns, tables, indexes, endpoints, routes, permissions or menu items.
- Do not add required columns to existing operational tables during the first implementation.
- Do not create cascade deletes from Final Accounts tables to operational tables.
- Store operational source IDs as external references.
- Existing sales, purchase, stock, GST, payroll, voucher and POS writes must continue even if financial posting is unavailable.
- Use an outbox/synchronisation mechanism so accounting posting failures cannot block operational billing.
- Hide menu items and reject module API access while the feature is disabled.

---

## 3. Known project context and implementation assumptions

Use these as the starting design assumptions unless the repository proves otherwise:

- Frontend: Nuxt 4, Vue 3, TypeScript.
- Backend: ASP.NET Core on .NET 10.
- Database: PostgreSQL 16.
- Deployment: Docker Compose.
- Existing modules include sales, purchase, inventory, GST, vouchers, cash/bank, payroll, HR, POS, returns and Day Book.
- Existing tenancy dimensions include some or all of Tenant, Company, StoreGroup and Store.
- Existing API uses JWT authentication and role/permission checks.
- Existing operational records may predate a complete double-entry ledger.
- Existing deployment branch is `version6`.
- The Final Accounts module must support store-level reporting and company-consolidated reporting.

The agent is allowed one structured repository-discovery pass to identify exact project paths, naming conventions, base entity fields, endpoint conventions, migrations, permission registration, sidebar registration, test projects and module patterns. It must then implement this specification rather than redesigning the module from scratch.

---

## 4. Definition of success

The module is successful only when all of these are true:

- Every posted journal entry balances.
- Trial Balance total debit equals total credit.
- Balance Sheet assets equal equity plus liabilities.
- Current-year profit or loss flows correctly to equity/capital.
- Closing balances carry forward correctly as next-year opening balances.
- Operational source totals reconcile to ledger postings.
- Historical backfill is idempotent.
- Re-running a posting job does not duplicate entries.
- Reversals are linked and auditable.
- Posted journals cannot be edited silently.
- Locked financial periods reject postings unless reopened through an authorised workflow.
- Store consolidation is correct and inter-store clearing can be identified.
- Projection statements remain mathematically integrated for each month.
- The existing application builds and behaves as before when the feature flag is disabled.
- No production deployment occurs from the feature branch.
- A CA can export a complete review package and trace any statement amount to ledger and source transaction.

---

## 5. Module boundary

### 5.1 Recommended backend structure

Adapt paths to the repository convention, but preserve the logical boundary:

```text
backend/
  Garmetix.Domain/
    FinalAccounts/
      Accounts/
      Journals/
      FiscalPeriods/
      Statements/
      Adjustments/
      Projections/
      Exchanges/
      Reconciliation/
      Common/

  Garmetix.Infrastructure/
    FinalAccounts/
      Persistence/
      Configurations/
      Migrations/
      Posting/
      Reporting/
      ProjectionEngine/
      Tally/
      Exports/
      Jobs/

  Garmetix.Api/
    FinalAccounts/
      Endpoints/
      Contracts/
      Authorization/
      ModuleRegistration/
      Validators/
      BackgroundJobs/
```

### 5.2 Recommended frontend structure

```text
frontend/
  app/
    pages/
      final-accounts/
    components/
      final-accounts/
    composables/
      final-accounts/
    stores/
      final-accounts/
    types/
      final-accounts/
    utils/
      final-accounts/
```

Use the actual Nuxt structure found in the repository.

### 5.3 Feature registration

Create a module descriptor such as:

```text
ModuleKey: FINAL_ACCOUNTS
DisplayName: Final Accounts & Projections
RouteRoot: /final-accounts
ApiRoot: /api/final-accounts
DefaultEnabled: false
```

The feature flag should be configurable from the application’s existing setup/settings UI and persisted in the application database. Do not make an environment variable the only way to enable it.

Suggested settings:

- `FinalAccounts.Enabled`
- `FinalAccounts.AllowHistoricalBackfill`
- `FinalAccounts.PostingMode` = `ManualSync | ScheduledSync | EventOutbox`
- `FinalAccounts.DefaultStatementTemplate`
- `FinalAccounts.DefaultInventoryValuation`
- `FinalAccounts.DefaultRoundingScale`
- `FinalAccounts.AllowTallyExport`
- `FinalAccounts.AllowProjection`
- `FinalAccounts.AllowPeriodReopen`

---

## 6. Accounting design principles

### 6.1 Double-entry is mandatory

Every journal entry consists of a header and two or more lines.

```text
Sum(Debit) = Sum(Credit)
```

Do not use signed amounts as a substitute for explicit debit and credit columns in the persistence model.

### 6.2 Monetary precision and rounding

Recommended approach:

- Calculation precision: decimal with at least four decimal places.
- Posted monetary values: two decimal places unless the existing system uses a different legal currency precision.
- Database: `numeric(20,4)` for calculation fields and `numeric(20,2)` for final debit/credit values.
- Use a single documented midpoint rounding rule.
- Allocate rounding differences deterministically.
- Post final residual paise to a configured `Rounding Off` ledger only when necessary.
- Never use binary floating-point types for money.

### 6.3 Immutable posted entries

Journal states:

```text
Draft
Validated
Posted
Reversed
CancelledBeforePosting
```

Rules:

- Drafts may be edited.
- Posted entries are immutable.
- Corrections require reversal plus replacement.
- A reversal must reference the original entry.
- Source transaction changes must create a reversal/repost or a delta adjustment according to the posting policy.
- Every state transition is audited.

### 6.4 Natural account types

At minimum:

```text
Asset
Liability
Equity
Income
CostOfGoodsSold
Expense
Contra
Statistical
```

Natural balance:

- Assets and expenses: debit.
- Liabilities, equity and income: credit.
- Contra accounts explicitly declare their natural balance and parent category.

### 6.5 Dimensions

Every journal entry and line must support the dimensions relevant to the source:

- TenantId
- CompanyId
- StoreGroupId
- StoreId
- FinancialYearId
- FiscalPeriodId
- CostCentreId
- DepartmentId
- BrandId
- ProductCategoryId
- ProductId
- CustomerId
- VendorId
- EmployeeId
- BankAccountId
- GST registration/GSTIN dimension when applicable
- SourceModule
- SourceDocumentType
- SourceDocumentId
- SourceDocumentNumber
- SourceLineId

Do not require every dimension on every line. Define validation by posting rule.

### 6.6 Source idempotency

Every source posting must have a unique idempotency key, for example:

```text
{TenantId}:{CompanyId}:{SourceModule}:{SourceDocumentType}:{SourceDocumentId}:{SourceVersion}
```

Persist:

- source ID;
- source version or updated timestamp;
- source content hash;
- posting rule version;
- journal entry ID;
- posting status;
- last attempt;
- failure reason.

A duplicate key must return the existing result rather than create another journal.

---

## 7. Chart of Accounts

### 7.1 Account hierarchy

Support unlimited nested groups but prevent circular parent relationships.

Recommended root groups:

```text
Assets
  Non-current assets
  Current assets

Liabilities
  Non-current liabilities
  Current liabilities

Equity and capital

Income
  Revenue from operations
  Other income

Cost of goods sold and direct expenses

Operating expenses

Finance costs

Tax and provisions

Statistical and memorandum accounts
```

### 7.2 Default garment-retail ledger pack

Seed only when requested and make all mappings editable.

Assets:

- Cash in Hand
- Petty Cash
- Bank Accounts
- UPI Clearing
- Card Settlement Receivable
- Trade Receivables
- Employee Advances
- Vendor Advances
- Prepaid Expenses
- Input CGST
- Input SGST
- Input IGST
- GST Refund Receivable
- TDS Receivable
- Inventory – Garments
- Inventory – Accessories
- Inventory – Tailoring Material
- Goods in Transit
- Stock Adjustment
- Furniture and Fixtures
- Computers and Equipment
- Vehicles
- Leasehold Improvements
- Accumulated Depreciation by asset class
- Security Deposits

Liabilities:

- Trade Payables
- Salary Payable
- Expense Payable
- Customer Advances
- Output CGST
- Output SGST
- Output IGST
- GST Payable
- TDS Payable
- Short-term Borrowings
- Term Loans
- Unsecured Loans
- Interest Accrued
- Provision for Expenses
- Provision for Tax

Equity:

- Proprietor/Partner Capital
- Partner Current Accounts
- Drawings
- Retained Earnings
- Current Year Profit and Loss
- Reserves and Surplus

Income:

- Garment Sales
- Accessory Sales
- Tailoring Service Income
- Alteration Service Income
- Delivery Charges Recovered
- Discount Received
- Commission Income
- Interest Income
- Miscellaneous Income

Cost and expense:

- Cost of Goods Sold
- Purchase Freight
- Direct Labour
- Tailoring Job Work
- Sales Discount
- Purchase Discount
- Salary and Wages
- Rent
- Electricity
- Internet and Telephone
- Marketing
- Printing and Stationery
- Repairs and Maintenance
- Software Subscription
- Professional Fees
- Bank Charges
- Payment Gateway Charges
- Bad Debts
- Inventory Write-down
- Depreciation
- Interest Expense
- Tax Expense
- Rounding Off

### 7.3 Account controls

Account fields:

- Code
- Name
- Alias
- Group
- AccountType
- NaturalBalance
- IsControlAccount
- AllowsManualPosting
- RequiresCustomer
- RequiresVendor
- RequiresEmployee
- RequiresBank
- RequiresStore
- RequiresCostCentre
- GST treatment metadata
- Tally group and ledger mapping
- Active dates
- IsSystemProtected

Control accounts must reject invalid manual postings according to policy.

---

## 8. General Ledger model

### 8.1 Journal header

Recommended fields:

- Id
- TenantId
- CompanyId
- StoreGroupId
- StoreId
- EntryNumber
- EntryDate
- PostingDate
- FinancialYearId
- FiscalPeriodId
- JournalType
- Status
- Narration
- SourceModule
- SourceDocumentType
- SourceDocumentId
- SourceDocumentNumber
- SourceVersion
- IdempotencyKey
- PostingRuleCode
- PostingRuleVersion
- CurrencyCode
- ExchangeRate
- TotalDebit
- TotalCredit
- ReversalOfEntryId
- ReversedByEntryId
- PostedAt
- PostedBy
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy
- RowVersion

### 8.2 Journal line

Recommended fields:

- Id
- JournalEntryId
- LineNumber
- AccountId
- DebitAmount
- CreditAmount
- BaseDebitAmount
- BaseCreditAmount
- Description
- all optional dimensions listed earlier
- tax code and tax component metadata
- source line reference
- CreatedAt
- CreatedBy

Constraints:

- Debit and credit cannot both be positive.
- At least one must be positive.
- No negative debit or credit.
- Header total equals sum of lines.
- Entry must balance before posting.
- Posted entry cannot be hard deleted.

### 8.3 Journal types

```text
Opening
Sales
SalesReturn
Purchase
PurchaseReturn
Receipt
Payment
Contra
Expense
Payroll
Inventory
Tax
Adjustment
Depreciation
Closing
Reversal
Migration
ProjectionOpening
```

---

## 9. Posting rule engine

### 9.1 Rule architecture

Implement versioned posting rules. Do not hardcode all decisions directly in endpoints.

A rule resolves:

- source eligibility;
- posting date;
- financial period;
- accounts;
- debit and credit amounts;
- dimensions;
- narration;
- validation;
- reversal behaviour;
- dependency on account mappings.

Suggested interfaces:

```csharp
public interface IFinancialPostingAdapter
{
    string SourceModule { get; }
    string SourceDocumentType { get; }

    Task<PostingPreview> PreviewAsync(
        SourcePostingRequest request,
        CancellationToken cancellationToken);

    Task<PostingResult> PostAsync(
        SourcePostingRequest request,
        CancellationToken cancellationToken);

    Task<PostingResult> ReverseAsync(
        SourceReversalRequest request,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IPostingRuleResolver
{
    Task<PostingRuleDefinition> ResolveAsync(
        PostingContext context,
        CancellationToken cancellationToken);
}
```

### 9.2 Posting preview

Before posting, allow preview showing:

- source totals;
- selected rule and version;
- debit/credit lines;
- missing mappings;
- warnings;
- period status;
- expected balance;
- existing posting link;
- whether posting would create, skip, reverse or replace.

### 9.3 Source posting examples

#### Sale invoice

Customer credit sale:

```text
Trade Receivables              Dr  Gross invoice total
Sales Discount                 Dr  Discount, when separately presented
    Garment Sales                  Net taxable revenue
    Output CGST                    CGST
    Output SGST                    SGST
    Output IGST                    IGST
    Other charges income           Applicable charges
```

Inventory impact:

```text
Cost of Goods Sold             Dr  Cost
    Inventory – Garments           Cost
```

Cash/UPI/card sale replaces Trade Receivables with the relevant settlement account. Mixed payments create separate debit lines.

#### Sale return

Reverse revenue, tax and receivable/settlement according to the accepted return/credit-note document. Reverse COGS and restore inventory only when stock is actually accepted back.

#### Purchase invoice

Stock-based purchase:

```text
Inventory – Garments           Dr  Taxable value/cost policy
Input CGST                     Dr
Input SGST                     Dr
Input IGST                     Dr
Purchase Freight               Dr  When expensed separately
    Trade Payables                 Invoice payable
    TDS Payable                    When applicable
```

#### Purchase return

Reverse inventory/purchase and input tax only according to the accepted supplier debit/credit-note and ITC policy.

#### Customer receipt

```text
Cash/Bank/UPI                  Dr
Discount Allowed               Dr  When approved
    Trade Receivables
```

#### Vendor payment

```text
Trade Payables                 Dr
    Cash/Bank/UPI
```

#### Expense

```text
Expense Account                Dr
Input GST                      Dr  When eligible
    Cash/Bank/Payable
    TDS Payable                Cr  When applicable
```

#### Payroll

At payroll finalisation:

```text
Salary and Wages               Dr
Employer Contributions         Dr
    Salary Payable
    Statutory Payables
```

At salary payment:

```text
Salary Payable                 Dr
    Bank/Cash
```

#### Stock adjustment

Shortage:

```text
Inventory Loss/Adjustment      Dr
    Inventory
```

Excess:

```text
Inventory                      Dr
    Inventory Gain/Adjustment
```

#### Depreciation

```text
Depreciation Expense           Dr
    Accumulated Depreciation
```

### 9.4 Posting-rule mapping settings

Create configuration screens for mapping:

- sales categories to revenue ledgers;
- product categories to inventory and COGS ledgers;
- GST components to tax ledgers;
- payment modes and bank accounts;
- expense categories;
- payroll components;
- stock adjustment reasons;
- returns and discounts;
- inter-store clearing;
- rounding;
- suspense/error ledger.

Do not silently use a suspense ledger in final posting. A suspense option may exist only for controlled migration with a visible exception report.

---

## 10. Operational integration without destabilising existing modules

### 10.1 Phase-one integration method

Use read-only adapters and a Final Accounts sync queue:

1. Existing source transaction completes normally.
2. A source event/outbox record is detected or a scheduled sync identifies new/changed records.
3. Final Accounts creates a posting preview.
4. Valid records are posted.
5. Invalid records enter an exception queue.
6. Operational source remains unchanged.

If an existing outbox already exists, extend it without changing existing event semantics. If no outbox exists, create a Final Accounts-owned sync cursor and polling job first. Direct changes to every operational endpoint should be postponed until the ledger is proven.

### 10.2 Reconciliation dashboard

For each source module show:

- source document count;
- source amount;
- posted count;
- posted amount;
- pending count;
- failed count;
- reversed count;
- amount difference;
- oldest pending record;
- last successful sync;
- posting-rule version.

### 10.3 Drift detection

Recalculate a source hash. If an already-posted source changes:

- flag drift;
- do not silently edit the journal;
- offer reversal and repost;
- require period-open validation;
- preserve old and new source hashes.

---

## 11. Historical backfill

### 11.1 Required modes

- Dry run.
- Selected module.
- Selected financial year.
- Selected date range.
- Selected store/company.
- Preview only.
- Post valid only.
- Stop on first error.
- Continue and collect errors.
- Reconcile only.
- Resume from checkpoint.

### 11.2 Backfill safeguards

- Disabled by default.
- Explicit permission required.
- Never enabled automatically after migration.
- Idempotent.
- Batch checkpoints.
- Read-only access to source records.
- No source writes.
- No production execution from this branch.
- Every batch has a run ID and immutable summary.
- Ability to remove only unapproved migration entries in a disposable/dev environment.
- Posted and approved entries require reversal, not deletion.

### 11.3 Reconciliation requirements

For each source class:

```text
Source gross total = related ledger control-account movement
Source tax total = related tax-ledger movement
Source payment total = cash/bank/clearing movement
Source cost total = inventory and COGS movement
```

Differences must be categorised:

- missing mapping;
- unsupported legacy status;
- cancelled/revised source;
- rounding;
- partial payment;
- tax mismatch;
- stock cost missing;
- duplicate source;
- source changed after posting;
- opening balance;
- unsupported historical record.

---

## 12. Fiscal years, periods and closing

### 12.1 Fiscal calendar

Support:

- financial year;
- monthly periods;
- optional adjustment period;
- open, soft-closed and locked states;
- store-level operational lock and company-level accounting lock.

### 12.2 Period controls

- Open: normal posting allowed.
- Soft closed: normal users blocked; accountant override allowed.
- Locked: no posting or reversal.
- Reopened: authorised workflow with reason, approver and audit event.

### 12.3 Year-end workflow

Checklist categories:

1. Transaction completeness.
2. Cash verification.
3. Bank reconciliation.
4. Customer reconciliation.
5. Vendor reconciliation.
6. GST reconciliation.
7. TDS reconciliation.
8. Payroll reconciliation.
9. Inventory valuation and physical stock.
10. Fixed assets and depreciation.
11. Accruals and prepayments.
12. Provisions and write-offs.
13. CA adjustments.
14. Final Trial Balance.
15. Final statements and schedules.
16. Approval.
17. Lock.
18. Opening-balance carry forward.

Closing action must:

- verify all mandatory checklist items;
- verify balanced Trial Balance;
- verify balanced Balance Sheet;
- snapshot report data and template versions;
- transfer current-year result to configured equity/capital account;
- create next-year opening journals;
- record source closing IDs;
- lock the year;
- prevent duplicate close.

Reopening must reverse or supersede the closing package in a controlled process.

---

## 13. Financial statements

### 13.1 Report engine principle

Reports read from posted General Ledger entries and approved statement mappings, not directly from operational source tables.

All report lines support:

- hierarchical nodes;
- account/group mappings;
- debit/credit sign rules;
- formula nodes;
- subtotal nodes;
- current period;
- previous period;
- variance amount;
- variance percent;
- percentage of revenue;
- drill-down;
- note/schedule references;
- display order;
- hide-zero option;
- rounding unit: rupees, thousands, lakhs, crores;
- template version.

### 13.2 Trial Balance

Route:

```text
/final-accounts/trial-balance
```

Views:

- group summary;
- ledger detail;
- opening, period movement and closing;
- debit/credit columns;
- monthly/quarterly comparison;
- store/company consolidated;
- zero-balance toggle;
- source drill-down;
- unbalanced diagnostic.

Acceptance:

```text
Total closing debit = Total closing credit
```

### 13.3 General Ledger

Route:

```text
/final-accounts/ledger
```

Features:

- account/date/store filters;
- opening balance;
- running balance;
- source document link;
- journal link;
- narration;
- dimension filters;
- export;
- reversal indicators;
- audit event access.

### 13.4 Trading and Profit & Loss Account

Route:

```text
/final-accounts/profit-loss
```

Structure:

```text
Revenue from operations
Less: sales returns and direct discounts
Net revenue

Opening inventory
Purchases and direct cost
Purchase freight/direct expenses
Less: purchase returns
Less: closing inventory
Cost of goods sold

Gross profit

Other income

Employee cost
Occupancy cost
Selling and distribution
Administrative expense
Technology and subscriptions
Bad debts and provisions
Inventory write-down
Other operating expense

EBITDA
Depreciation
EBIT
Finance cost
Profit before tax
Tax/provision
Profit after tax
```

Support both horizontal Tally-style and vertical CA-style presentations.

### 13.5 Balance Sheet

Route:

```text
/final-accounts/balance-sheet
```

Structure must be template-driven by entity type and reporting year. Provide:

- proprietorship/partnership/non-corporate template;
- LLP template;
- company Schedule III template only when applicable;
- management/simple Tally-style view;
- comparative figures;
- current/non-current classification;
- schedules and notes;
- consolidation.

Core validation:

```text
Assets = Equity + Liabilities
```

### 13.6 Cash Flow Statement

Route:

```text
/final-accounts/cash-flow
```

Implement indirect method first:

- profit before tax;
- non-cash adjustments;
- working-capital changes;
- operating cash flow;
- investing cash flow;
- financing cash flow;
- net movement;
- opening cash and cash equivalents;
- closing cash and cash equivalents.

Map cash-equivalent ledgers explicitly. Reconcile the closing balance to Balance Sheet cash and bank accounts.

### 13.7 Schedules and notes

Route:

```text
/final-accounts/schedules
```

Support:

- account schedules;
- debtor ageing;
- creditor ageing;
- fixed-asset schedule;
- inventory schedule;
- GST balances;
- loans;
- capital accounts;
- related notes;
- accounting policies;
- contingent items entered manually by authorised users;
- supporting attachments;
- signatory details;
- preparer/reviewer details.

The software can prepare draft financial statements. It must not label a report “audited” or “certified” unless an authorised user explicitly records that status and uploads the signed document.

---

## 14. Inventory valuation integration

### 14.1 Supported valuation methods

At minimum:

- Weighted Average.
- FIFO.
- Standard or imported historical cost only if the existing inventory module supports it.
- Management comparison view.

Do not change the operational stock valuation method during implementation.

### 14.2 Closing inventory

Closing inventory must:

- come from a frozen valuation snapshot;
- reconcile quantity and value to the inventory module;
- be linked to store and product/category;
- support goods in transit;
- identify negative stock and missing cost;
- support damaged, obsolete and slow-moving provisions;
- appear in the Balance Sheet;
- affect Cost of Goods Sold consistently;
- be immutable after year close unless the year is reopened.

### 14.3 Important accounting design choice

Select one consistent ledger method:

- perpetual inventory postings, or
- periodic inventory closing adjustment.

Garmetix should prefer perpetual inventory if reliable cost is available per sale line. Do not mix perpetual COGS posting and a second full closing-stock adjustment that duplicates the effect. If historical data lacks cost, use a documented migration/periodic approach for those periods.

---

## 15. CA adjustment and review workspace

Route:

```text
/final-accounts/ca-workspace
```

### 15.1 Adjustment batches

Batch types:

- depreciation;
- accrual;
- prepayment;
- provision;
- bad debt;
- inventory provision;
- tax adjustment;
- GST adjustment;
- TDS adjustment;
- partner/proprietor adjustment;
- reclassification;
- prior-period adjustment;
- other.

Workflow:

```text
Draft -> Submitted -> UnderReview -> Approved -> Posted
                         \-> Rejected
Posted -> Reversed
```

Fields:

- batch number;
- financial year;
- adjustment date;
- description;
- supporting basis;
- journal lines;
- P&L effect;
- Balance Sheet effect;
- preparer;
- reviewer;
- approver;
- comments;
- attachments;
- reversal date;
- recurring/reversing flag;
- status history.

### 15.2 Review comments

Comments may attach to:

- account;
- journal;
- statement line;
- reconciliation item;
- adjustment;
- closing checklist;
- supporting document.

Support open/resolved status and audit trail.

### 15.3 Report versions

Preserve:

- provisional report;
- pre-adjustment report;
- adjustment impact;
- adjusted report;
- final locked report;
- signed report attachment.

---

## 16. Projection engine

### 16.1 Scope

Generate integrated monthly projections for one to five years:

- projected Profit & Loss;
- projected Balance Sheet;
- projected Cash Flow;
- loan schedule;
- working-capital requirement;
- ratio analysis;
- break-even;
- scenario comparison.

### 16.2 Scenario states

```text
Draft
Calculated
Reviewed
Approved
Archived
```

Scenario types:

- Conservative.
- Base.
- Optimistic.
- Custom.
- Bank loan/DPR.
- New store.
- Expansion.

### 16.3 Assumption categories

Revenue:

- baseline actual period;
- store growth;
- category growth;
- monthly seasonality;
- new store start date;
- customer count;
- average bill value;
- sales return percentage;
- discount percentage;
- tailoring/alteration growth.

Gross margin and stock:

- gross margin by category;
- purchase inflation;
- shrinkage;
- markdown;
- inventory days;
- minimum stock;
- closing-stock provision.

Operating expense:

- fixed monthly amount;
- annual escalation;
- percentage of sales;
- employee headcount and salary;
- rent escalation;
- utilities;
- marketing;
- software;
- professional fees;
- payment charges.

Working capital:

- debtor days;
- creditor days;
- inventory days;
- GST payment lag;
- minimum cash;
- customer/vendor advances.

Finance and capex:

- owner contribution;
- debt drawdown;
- repayment;
- interest rate;
- moratorium;
- capital expenditure;
- depreciation;
- drawings/dividend;
- tax provision.

### 16.4 Integrated calculation order

For each monthly period:

1. Revenue.
2. Direct cost and gross profit.
3. Operating expenses.
4. EBITDA.
5. Depreciation and interest.
6. Profit before tax and tax.
7. Receivables, inventory and payables.
8. Other assets/liabilities.
9. Capital expenditure and debt.
10. Cash Flow.
11. Closing cash.
12. Balance Sheet.
13. Balance check and ratios.

The engine must calculate a balancing error and fail the scenario if it exceeds tolerance. Do not insert an unexplained balancing figure. A deliberate `Cash/Overdraft` plug is allowed only when the scenario explicitly selects that financing policy and exposes it to the user.

### 16.5 Projection formulas

Examples:

```text
Revenue = Units × Average Selling Price
Gross Profit = Revenue - Cost of Goods Sold
Receivables = Revenue × DebtorDays / DaysInPeriod
Inventory = Cost of Goods Sold × InventoryDays / DaysInPeriod
Payables = Purchases × CreditorDays / DaysInPeriod
Closing Cash = Opening Cash + Net Cash Movement
```

All assumptions and formulas must be versioned and visible in the scenario audit output.

---

## 17. Ratios and management analysis

Provide:

- Gross Profit Margin.
- EBITDA Margin.
- Net Profit Margin.
- Current Ratio.
- Quick Ratio.
- Debt-to-Equity.
- Inventory Turnover.
- Inventory Days.
- Debtor Days.
- Creditor Days.
- Working Capital.
- Return on Capital Employed.
- Return on Equity.
- Interest Coverage.
- DSCR.
- Break-even sales.
- Cash conversion cycle.

Every ratio must expose:

- formula;
- numerator;
- denominator;
- contributing accounts;
- period;
- actual/projected indicator;
- warning when denominator is zero or negative.

---

## 18. TallyPrime and CA exchange

### 18.1 Export formats

Implement versioned adapters for:

- Excel workbook.
- XML.
- JSON.
- CSV working-paper files.
- PDF reports.

Build Excel first if the repository already has reliable spreadsheet export infrastructure. Add XML and JSON with fixtures validated against an approved TallyPrime test company.

### 18.2 Export content

Masters:

- account groups;
- ledgers;
- customers;
- vendors;
- tax ledgers;
- bank/cash ledgers;
- stock groups/items when requested;
- cost centres.

Transactions:

- sales;
- purchases;
- returns;
- receipts;
- payments;
- contra;
- journals;
- payroll accounting entries;
- stock journals if supported;
- opening balances.

### 18.3 Mapping screen

Map Garmetix accounts and voucher types to Tally names. Store:

- Tally release/profile;
- company mapping;
- ledger mapping;
- group mapping;
- voucher type mapping;
- tax mapping;
- stock mapping;
- cost-centre mapping;
- last validation;
- conflict status.

### 18.4 Export safeguards

- Preview.
- Validation.
- Masters before transactions.
- Duplicate strategy.
- Existing-master behaviour.
- date range and store/company selection;
- export batch ID;
- row counts and control totals;
- checksum;
- exception report;
- no direct production Tally write in the first release.

### 18.5 CA review package

Generate a ZIP containing at least:

```text
01-Trial-Balance.xlsx
02-General-Ledger.xlsx
03-Profit-And-Loss.xlsx
04-Balance-Sheet.xlsx
05-Cash-Flow.xlsx
06-Debtors-Ageing.xlsx
07-Creditors-Ageing.xlsx
08-Inventory-Valuation.xlsx
09-Fixed-Asset-Schedule.xlsx
10-Bank-Reconciliation.xlsx
11-GST-Reconciliation.xlsx
12-TDS-Summary.xlsx
13-Payroll-Summary.xlsx
14-Adjustment-Journals.xlsx
15-Financial-Ratios.xlsx
16-Exception-Report.xlsx
17-Source-Control-Totals.xlsx
Supporting-Documents/
README.txt
```

---

## 19. Database design

### 19.1 Schema

```sql
CREATE SCHEMA IF NOT EXISTS final_accounts;
```

Use the project’s normal migration mechanism. Do not execute the migration on production.

### 19.2 Core tables

Recommended table set:

```text
fa_module_settings
fa_account_groups
fa_accounts
fa_account_mappings
fa_fiscal_years
fa_fiscal_periods
fa_journal_entries
fa_journal_lines
fa_posting_rules
fa_posting_rule_versions
fa_source_posting_links
fa_sync_jobs
fa_sync_job_items
fa_posting_exceptions
fa_reconciliation_runs
fa_reconciliation_items
fa_inventory_valuation_snapshots
fa_inventory_valuation_lines
fa_statement_templates
fa_statement_template_versions
fa_statement_nodes
fa_statement_mappings
fa_report_runs
fa_report_snapshots
fa_report_snapshot_lines
fa_adjustment_batches
fa_adjustment_comments
fa_close_checklists
fa_close_checklist_items
fa_year_closures
fa_opening_balance_runs
fa_projection_scenarios
fa_projection_assumptions
fa_projection_period_results
fa_projection_statement_lines
fa_ratio_definitions
fa_ratio_results
fa_tally_profiles
fa_tally_mappings
fa_exchange_batches
fa_exchange_items
fa_attachments
fa_audit_events
```

### 19.3 Common fields

All tenant-owned tables should use the project’s standard fields, including:

- TenantId.
- CompanyId where applicable.
- StoreGroupId where applicable.
- StoreId where applicable.
- CreatedAt.
- CreatedBy.
- UpdatedAt.
- UpdatedBy.
- RowVersion/concurrency token.
- IsDeleted only for draft/configuration entities when existing conventions require it.

Financial postings, snapshots and audit events must not be soft-deleted as a substitute for reversal.

### 19.4 Indexes and constraints

Create indexes for:

- tenant/company/date;
- account/date;
- fiscal year/period;
- source module/type/ID;
- idempotency key unique;
- entry number unique within company/financial year;
- status;
- sync pending/failure;
- report template/version;
- scenario/period.

Database checks:

- debit >= 0;
- credit >= 0;
- not both positive;
- no orphan journal lines;
- unique line number per journal;
- valid date range;
- no duplicate active account code within company;
- no circular group hierarchy, enforced in application validation if needed.

Do not rely only on frontend validation.

---

## 20. API surface

Use existing endpoint conventions and permission middleware. Suggested endpoints:

### Module and settings

```text
GET    /api/final-accounts/status
GET    /api/final-accounts/settings
PUT    /api/final-accounts/settings
POST   /api/final-accounts/validate-configuration
```

### Chart of Accounts

```text
GET    /api/final-accounts/account-groups
POST   /api/final-accounts/account-groups
PUT    /api/final-accounts/account-groups/{id}
GET    /api/final-accounts/accounts
GET    /api/final-accounts/accounts/{id}
POST   /api/final-accounts/accounts
PUT    /api/final-accounts/accounts/{id}
POST   /api/final-accounts/accounts/seed-default
POST   /api/final-accounts/accounts/validate
```

### Journals

```text
GET    /api/final-accounts/journals
GET    /api/final-accounts/journals/{id}
POST   /api/final-accounts/journals/preview
POST   /api/final-accounts/journals
POST   /api/final-accounts/journals/{id}/validate
POST   /api/final-accounts/journals/{id}/post
POST   /api/final-accounts/journals/{id}/reverse
GET    /api/final-accounts/ledger
```

### Posting and sync

```text
POST   /api/final-accounts/posting/preview-source
POST   /api/final-accounts/posting/post-source
POST   /api/final-accounts/posting/reverse-source
GET    /api/final-accounts/posting/exceptions
POST   /api/final-accounts/posting/exceptions/{id}/retry
POST   /api/final-accounts/sync/run
GET    /api/final-accounts/sync/runs
GET    /api/final-accounts/sync/runs/{id}
POST   /api/final-accounts/backfill/preview
POST   /api/final-accounts/backfill/run
POST   /api/final-accounts/reconcile
```

### Reports

```text
GET    /api/final-accounts/reports/trial-balance
GET    /api/final-accounts/reports/profit-loss
GET    /api/final-accounts/reports/balance-sheet
GET    /api/final-accounts/reports/cash-flow
GET    /api/final-accounts/reports/account-schedule
GET    /api/final-accounts/reports/ratios
POST   /api/final-accounts/reports/snapshot
GET    /api/final-accounts/reports/snapshots
GET    /api/final-accounts/reports/snapshots/{id}
GET    /api/final-accounts/reports/{report}/export
```

### CA workspace and close

```text
GET    /api/final-accounts/adjustments
POST   /api/final-accounts/adjustments
PUT    /api/final-accounts/adjustments/{id}
POST   /api/final-accounts/adjustments/{id}/submit
POST   /api/final-accounts/adjustments/{id}/approve
POST   /api/final-accounts/adjustments/{id}/reject
POST   /api/final-accounts/adjustments/{id}/post
POST   /api/final-accounts/adjustments/{id}/reverse

GET    /api/final-accounts/closing/{financialYearId}/checklist
PUT    /api/final-accounts/closing/{financialYearId}/checklist
POST   /api/final-accounts/closing/{financialYearId}/validate
POST   /api/final-accounts/closing/{financialYearId}/close
POST   /api/final-accounts/closing/{financialYearId}/reopen
```

### Projections

```text
GET    /api/final-accounts/projections
POST   /api/final-accounts/projections
GET    /api/final-accounts/projections/{id}
PUT    /api/final-accounts/projections/{id}
POST   /api/final-accounts/projections/{id}/calculate
POST   /api/final-accounts/projections/{id}/clone
POST   /api/final-accounts/projections/{id}/approve
GET    /api/final-accounts/projections/{id}/statements
GET    /api/final-accounts/projections/{id}/export
```

### Tally and CA package

```text
GET    /api/final-accounts/tally/profiles
POST   /api/final-accounts/tally/profiles
GET    /api/final-accounts/tally/mappings
PUT    /api/final-accounts/tally/mappings
POST   /api/final-accounts/tally/validate
POST   /api/final-accounts/tally/export/preview
POST   /api/final-accounts/tally/export
GET    /api/final-accounts/tally/export/{batchId}
POST   /api/final-accounts/ca-package
GET    /api/final-accounts/ca-package/{batchId}
```

All list endpoints need server-side pagination, sorting and filtering.

---

## 21. Frontend pages

Recommended pages:

```text
/final-accounts
/final-accounts/setup
/final-accounts/chart-of-accounts
/final-accounts/account-mapping
/final-accounts/journals
/final-accounts/journals/new
/final-accounts/journals/[id]
/final-accounts/ledger
/final-accounts/trial-balance
/final-accounts/profit-loss
/final-accounts/balance-sheet
/final-accounts/cash-flow
/final-accounts/schedules
/final-accounts/reconciliation
/final-accounts/posting-exceptions
/final-accounts/backfill
/final-accounts/ca-workspace
/final-accounts/year-end-close
/final-accounts/projections
/final-accounts/projections/new
/final-accounts/projections/[id]
/final-accounts/ratios
/final-accounts/tally-exchange
/final-accounts/ca-package
/final-accounts/audit-log
```

UI rules:

- Use normal application layout, sidebar and header.
- No full-screen orphan pages unless the existing app deliberately uses that pattern.
- Preserve filter state when navigating to drill-down and returning.
- Use autocomplete for accounts, customers, vendors, products and dimensions.
- Avoid large unsearchable dropdowns.
- Provide loading, empty, permission-denied and error states.
- Use server pagination for ledger/journal tables.
- Support print-friendly and export views.
- Display provisional/final/projected/audited status prominently.
- Show report period, entity, store scope, valuation method and generation time.
- Show a visible warning when books contain unposted transactions or reconciliation differences.
- Drill-down chain: statement line → accounts → ledger entries → journal → source transaction.
- Do not expose raw JSON as the normal user view.

---

## 22. Roles and permissions

Suggested permission keys:

```text
FinalAccounts.ViewDashboard
FinalAccounts.ManageSettings
FinalAccounts.ViewChartOfAccounts
FinalAccounts.ManageChartOfAccounts
FinalAccounts.ViewJournals
FinalAccounts.CreateManualJournal
FinalAccounts.PostJournal
FinalAccounts.ReverseJournal
FinalAccounts.ViewLedger
FinalAccounts.RunSync
FinalAccounts.RunBackfill
FinalAccounts.ViewReconciliation
FinalAccounts.ResolveExceptions
FinalAccounts.ViewTrialBalance
FinalAccounts.ViewProfitLoss
FinalAccounts.ViewBalanceSheet
FinalAccounts.ViewCashFlow
FinalAccounts.ExportReports
FinalAccounts.ManageStatementTemplates
FinalAccounts.CreateAdjustments
FinalAccounts.ReviewAdjustments
FinalAccounts.ApproveAdjustments
FinalAccounts.ClosePeriod
FinalAccounts.ReopenPeriod
FinalAccounts.CreateProjection
FinalAccounts.ApproveProjection
FinalAccounts.ManageTallyMapping
FinalAccounts.ExportTally
FinalAccounts.GenerateCAPackage
FinalAccounts.ViewAuditLog
```

Default role guidance:

- Owner/Admin: all except optional CA certification status.
- Accountant: setup, journals, reconciliation, reports, adjustments and close preparation.
- CA: review, adjustments, final statements, comments and package.
- Store Manager: store-level management reports only.
- Auditor: read-only reports, ledger, evidence and audit trail.
- Biller/POS user: no Final Accounts access by default.

Use existing role seeding and permission UI. Do not grant broad access automatically to current users.

---

## 23. Audit and security

Audit events must record:

- actor;
- action;
- timestamp;
- tenant/company/store;
- entity type and ID;
- old/new values where allowed;
- reason;
- request/operation ID;
- source IP/device metadata according to existing policy;
- approval chain;
- related attachment.

Sensitive controls:

- account mapping changes;
- manual journals;
- posting;
- reversal;
- period close/reopen;
- report status;
- projection approval;
- Tally export;
- CA package generation.

Use optimistic concurrency for editable configuration. Avoid leaking another tenant’s accounts or reports through IDs. Apply tenant filters at repository/query level and test them.

---

## 24. Testing strategy

### 24.1 Unit tests

- journal balance;
- natural-balance sign;
- rounding;
- posting rule resolution;
- account mapping;
- reversal;
- fiscal period validation;
- report aggregation;
- retained earnings transfer;
- cash-flow classification;
- projection formulas;
- ratio formulas;
- idempotency.

### 24.2 Property/invariant tests

Generate random balanced entries and verify:

- debit equals credit;
- reversal nets to zero;
- consolidated totals equal store totals excluding eliminations;
- Balance Sheet balances;
- opening plus movement equals closing;
- projection cash roll-forward is consistent.

### 24.3 Integration tests

Use PostgreSQL, not an in-memory substitute, for:

- schema migration;
- numeric precision;
- unique idempotency constraint;
- concurrency;
- posting transaction;
- report queries;
- period lock;
- backfill resume;
- tenant isolation.

### 24.4 Contract tests

For each operational adapter:

- representative sale;
- mixed payment;
- sale return;
- interstate/intrastate GST;
- purchase;
- purchase return;
- expense with and without input tax;
- payroll;
- stock adjustment;
- revised/cancelled source;
- historical malformed record.

### 24.5 Golden report tests

Store small approved fixtures and expected output for:

- Trial Balance;
- P&L;
- Balance Sheet;
- Cash Flow;
- projections;
- Tally export.

Normalise generated timestamps before snapshot comparison.

### 24.6 Regression tests

Run all existing backend and frontend tests. Add checks that:

- existing routes still work;
- existing sidebar is unchanged when the module is disabled;
- existing APIs return unchanged contracts;
- production compose files are unchanged unless a non-breaking optional worker registration is required;
- no migration alters existing tables.

### 24.7 Performance targets

Validate with realistic test data:

- ledger pagination does not load all entries;
- report queries use indexed set-based SQL;
- backfill streams/batches;
- exports stream large files;
- no N+1 account/source queries;
- background posting is retry-safe.

Do not hardcode an arbitrary response-time claim; record measured results and test dataset size.

---

## 25. Implementation roadmap

### Stage BS-00 — Branch, documentation and baseline

Deliver:

- branch `balancesheet`;
- `roadmap-balancesheet.md`;
- `todo-balancesheet.md`;
- baseline build/test results;
- discovered project path map;
- production-isolation statement.

Commit:

```text
docs(final-accounts): add roadmap and isolated implementation plan
```

### Stage BS-01 — Module skeleton and feature flag

Deliver:

- backend module registration;
- frontend route shell;
- module settings;
- permission constants;
- menu hidden by default;
- health/status endpoint;
- no operational integration.

Commit:

```text
feat(final-accounts): add isolated module skeleton and feature flag
```

### Stage BS-02 — Schema, fiscal periods and Chart of Accounts

Deliver:

- `final_accounts` schema migration;
- account groups/accounts;
- fiscal years/periods;
- default garment-retail COA seed preview;
- setup UI;
- validation and tests.

Commit:

```text
feat(final-accounts): add chart of accounts and fiscal periods
```

### Stage BS-03 — General Ledger engine

Deliver:

- journal header/lines;
- validation;
- posting transaction;
- reversal;
- idempotency;
- journal UI and API;
- unit/integration tests.

Commit:

```text
feat(final-accounts): implement double-entry general ledger
```

### Stage BS-04 — Posting mappings and adapters

Implement adapters in controlled order:

1. Receipts, payments, cash/bank and expenses.
2. Sales and sales returns.
3. Purchases and purchase returns.
4. Inventory and COGS.
5. Payroll.
6. GST/TDS and adjustments.
7. Inter-store entries where applicable.

Deliver posting preview and exception queue.

Commit by coherent adapter group rather than one huge commit.

### Stage BS-05 — Sync, backfill and reconciliation

Deliver:

- sync job;
- source posting links;
- dry-run historical backfill;
- checkpoints;
- drift detection;
- reconciliation dashboard;
- exceptions;
- no production execution.

Commit:

```text
feat(final-accounts): add idempotent sync and reconciliation
```

### Stage BS-06 — Ledger and Trial Balance

Deliver:

- General Ledger report;
- running balances;
- Trial Balance;
- drill-down;
- exports;
- balanced controls;
- golden tests.

### Stage BS-07 — Trading and Profit & Loss

Deliver:

- statement template engine;
- account mapping;
- horizontal/vertical P&L;
- gross profit split;
- comparisons;
- drill-down;
- export.

### Stage BS-08 — Balance Sheet, Cash Flow and schedules

Deliver:

- template-driven Balance Sheet;
- comparative figures;
- retained earnings/current-year result;
- indirect Cash Flow;
- schedules;
- fixed-asset and inventory schedules;
- balance validation.

### Stage BS-09 — CA workspace

Deliver:

- adjustment batches;
- review comments;
- approval;
- posting/reversal;
- attachments;
- provisional/adjusted/final report versions.

### Stage BS-10 — Period and year closing

Deliver:

- checklist;
- validation;
- snapshot;
- close;
- carry forward;
- lock;
- authorised reopen;
- tests for repeated close and reopening.

### Stage BS-11 — Projection engine

Deliver:

- scenario and assumptions;
- monthly integrated model;
- P&L, Balance Sheet and Cash Flow;
- working capital;
- debt schedule;
- ratios;
- scenario comparison;
- Excel/PDF export.

### Stage BS-12 — TallyPrime and CA package

Deliver:

- mapping profiles;
- Excel export;
- validated XML/JSON adapters if fixtures are available;
- preview/control totals;
- CA package ZIP;
- exception report.

### Stage BS-13 — Security, audit and documentation

Deliver:

- full permission matrix;
- tenant-isolation tests;
- audit log UI;
- operational runbook;
- accountant user guide;
- developer guide;
- restore/rollback notes.

### Stage BS-14 — Full QA and hardening

Deliver:

- all builds/tests;
- migration test from clean and existing DB;
- performance measurements;
- large export test;
- browser QA;
- no-feature regression;
- security review;
- unresolved issue list.

### Stage BS-15 — Merge-readiness package

Do not merge. Prepare:

- commit list;
- change summary;
- migration summary;
- feature-flag instructions;
- test evidence;
- reconciliation evidence;
- known limitations;
- staging enablement steps;
- production rollout and rollback proposal;
- recommended merge command for a human reviewer.

---

## 26. Merge and rollout gates

The branch may be proposed for merge only when:

- branch is rebased/merged from latest `version6` and conflicts are resolved;
- all existing tests pass;
- new tests pass;
- frontend and backend production builds pass;
- migrations are additive and reviewed;
- feature remains disabled by default;
- no production secrets or files are committed;
- no production deployment workflow triggers on this branch;
- sample source totals reconcile;
- Trial Balance and Balance Sheet invariants pass;
- historical backfill is demonstrated only on test/staging data;
- permissions are verified;
- rollback is documented;
- a human approves the merge.

Suggested human-controlled merge preparation:

```bash
git switch version6
git pull --ff-only origin version6
git switch balancesheet
git merge --no-ff version6
# rerun full validation
git push origin balancesheet
```

Do not force-push. Prefer a normal merge/update workflow when uncertain.

The actual merge must be performed by the repository owner or authorised reviewer.

---

## 27. Rollback design

Before enablement:

- disable feature flag;
- module routes disappear;
- APIs return module-disabled response;
- no operational process depends on the module;
- background sync does not run.

After enablement:

- disable new sync jobs;
- preserve ledger/audit data;
- revert application deployment if required;
- do not drop schema;
- do not delete posted journals;
- use corrective migrations only;
- operational modules continue independently.

For a failed backfill:

- stop batch;
- retain batch diagnostics;
- in dev/staging, remove only entries belonging to an unapproved migration run using a purpose-built cleanup command;
- in approved/production books, reverse instead of delete.

---

## 28. Learning path for developers and agents

The implementation team should understand these subjects in this order:

1. **Project conventions**
   - module registration;
   - tenancy;
   - permissions;
   - EF/PostgreSQL migrations;
   - frontend API client;
   - current operational document lifecycle.

2. **Double-entry accounting**
   - accounts;
   - debit/credit;
   - journal;
   - ledger;
   - control accounts;
   - accrual versus cash basis;
   - reversal and correction.

3. **Retail accounting**
   - sales and returns;
   - purchases and returns;
   - GST input/output;
   - inventory and COGS;
   - mixed payments;
   - debtor/creditor settlement;
   - discounts.

4. **Financial statements**
   - Trial Balance;
   - Trading/P&L;
   - Balance Sheet;
   - Cash Flow;
   - comparative and schedules;
   - closing and opening.

5. **Adjustments**
   - depreciation;
   - accruals;
   - prepayments;
   - provisions;
   - bad debts;
   - reclassification;
   - prior-period items.

6. **Projection modelling**
   - integrated three-statement model;
   - working-capital drivers;
   - debt schedule;
   - scenario assumptions;
   - balancing controls.

7. **Tally exchange**
   - masters before vouchers;
   - mapping;
   - duplicate handling;
   - control totals;
   - exception validation.

Do not let the agent invent accounting treatment when source meaning is unclear. Route ambiguity into a configurable mapping or visible exception.

---

## 29. Glossary

- **General Ledger:** Complete set of posted account entries.
- **Journal:** Balanced debit/credit transaction.
- **Trial Balance:** Closing debit and credit balances by account.
- **Trading Account:** Revenue and direct cost view used to derive gross profit.
- **P&L:** Income and expenses for a period.
- **Balance Sheet:** Assets, equity and liabilities at a date.
- **Control Account:** Ledger summarising subsidiary records such as debtors.
- **Posting Rule:** Versioned mapping from source transaction to journal lines.
- **Idempotency:** Repeating an operation does not create duplicates.
- **Backfill:** Posting historical operational records into the ledger.
- **Reconciliation:** Comparing source records to ledger balances.
- **Soft Close:** Restricted period that privileged users may adjust.
- **Hard Lock:** Period that requires formal reopening.
- **Projection:** Future financial statements calculated from assumptions.
- **CA Workspace:** Controlled review and adjustment area for accountant/CA use.

---

## 30. Regulatory and product-reference basis

The implementation must use versioned report templates rather than hardcoded “permanent” formats.

- ICAI’s announcement dated 31 March 2026 states phased applicability of its Guidance Notes for Non-Corporate Entities and LLPs: periods beginning on or after 1 April 2025 for entities with turnover exceeding ₹5 crore, and periods beginning on or after 1 April 2026 for all entities.
- ICAI publishes the Guidance Note and illustrative Excel formats for Non-Corporate Entities and LLPs.
- TallyPrime supports financial statements for corporate and non-corporate entities through mapped financial-statement templates.
- Current TallyPrime documentation supports Excel mapping and import, and JSON/XML import in supported releases.

Official references:

- https://www.icai.org/post/asb-announ-310326
- https://www.icai.org/post/guidance-notes
- https://help.tallysolutions.com/prepare-financial-statements/
- https://help.tallysolutions.com/import-data-using-any-excel-file/
- https://help.tallysolutions.com/import-data-from-xml-or-json/

Treat these as reference inputs, not as a substitute for review by the user’s CA.

---

## 31. Final instruction to the coding agent

Implement in stages. At the beginning of every stage:

1. read this roadmap;
2. read `todo-balancesheet.md`;
3. inspect only the files relevant to that stage;
4. confirm the current branch is `balancesheet`;
5. confirm the feature remains disabled by default;
6. implement;
7. run targeted tests;
8. run affected builds;
9. update the todo with evidence;
10. commit the completed stage.

Never mark a task complete without code, tests and recorded evidence. Never merge or deploy.

import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const modularRoot = fileURLToPath(new URL('..', import.meta.url))
const repoRoot = resolve(modularRoot, '../..')
const checks = []
const failures = []

console.log('Garmetix Final Accounts BS-06 readiness')

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs', [
  'MapFinalAccountsEndpoints',
  'FinalAccountsSettingsDefaults.ApiRoot',
  'RequireAuthorization(GarmetixPolicies.FinalAccounts)',
  'MapGet("/status"',
  'MapGet("/settings"',
  'MapPut("/settings"',
  'AddEndpointFilter<FinalAccountsEnabledFilter>',
  'MapGet("/dashboard"',
  'MapGet("/account-groups"',
  'MapPost("/accounts"',
  'MapGet("/accounts/search"',
  'MapGet("/fiscal-years"',
  'MapGet("/fiscal-periods"',
  'MapGet("/journals"',
  'MapPost("/journals/preview"',
  'MapPost("/journals/{id:guid}/post"',
  'MapPost("/journals/{id:guid}/reverse"',
  'MapGet("/posting-rules"',
  'MapGet("/posting-rules/mapping-validation"',
  'MapPost("/posting/preview"',
  'MapGet("/posting/adapters"',
  'PreviewAdapterPostingAsync',
  'MapGet("/sync/options"',
  'MapGet("/sync/jobs"',
  'MapPost("/sync/manual"',
  'MapDelete("/sync/jobs/{id:guid}/dry-run"',
  'CleanupDryRunSyncJobAsync',
  'MapPost("/backfill/dry-run"',
  'MapPost("/reconciliation/summary"',
  'MapPost("/reconciliation/export"',
  'ExportReconciliationAsync',
  'MapGet("/reports/general-ledger"',
  'MapGet("/reports/general-ledger/export"',
  'MapGet("/reports/trial-balance"',
  'MapGet("/reports/trial-balance/export"',
  'MapGet("/coa/seed-preview"',
  'MapGet("/validation/summary"'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogService.cs', [
  'FinalAccountsCatalogService',
  'CreateAccountGroupAsync',
  'CreateAccountAsync',
  'CreateFiscalYearAsync',
  'CreateFiscalPeriodAsync',
  'SearchAccountsAsync',
  'GetSeedPreviewAsync',
  'GetValidationSummaryAsync',
  'WorkspaceScope.CanWrite',
  'FinalAccountsCatalogRules.CreatesCycle'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogRules.cs', [
  'TryParseAccountShape',
  'ExpectedNaturalBalance',
  'CreatesCycle',
  'RangesOverlap',
  'IsAllowedStatusTransition'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCatalogContracts.cs', [
  'FinalAccountsAccountGroupRequest',
  'FinalAccountsAccountRequest',
  'FinalAccountsFiscalYearRequest',
  'FinalAccountsFiscalPeriodRequest',
  'FinalAccountsValidationSummaryResponse'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsJournalService.cs', [
  'FinalAccountsJournalService',
  'CreateDraftAsync',
  'PostJournalAsync',
  'ReverseJournalAsync',
  'DeleteDraftAsync',
  'WorkspaceScope.CanWrite'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsJournalRules.cs', [
  'ValidateJournal',
  'BuildReversalLines',
  'NormalizeIdempotencyKey',
  'CanPostPeriodStatus',
  'CanEditStatus'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsJournalContracts.cs', [
  'FinalAccountsJournalSaveRequest',
  'FinalAccountsJournalDto',
  'FinalAccountsJournalListResponse',
  'FinalAccountsJournalValidationResponse'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPostingRuleService.cs', [
  'FinalAccountsPostingRuleService',
  'ListPostingRulesAsync',
  'GetMappingValidationAsync',
  'PreviewPostingAsync',
  'MissingRequestedMapping',
  'UnbalancedPreview',
  'BuildMappingVersion'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPostingRules.cs', [
  'StandardRules',
  'SalesInvoice',
  'SalesReturn',
  'SalesCancellation',
  'PurchaseInvoice',
  'PurchaseReturn',
  'PurchaseCancellation',
  'SaleCogs',
  'SaleReturnStockRestoration',
  'PurchaseInventory',
  'PurchaseReturnInventory',
  'StockAdjustment',
  'StockTransfer',
  'PayrollFinalization',
  'SalaryPayment',
  'PayrollStatutoryPayment',
  'GstPayment',
  'GstAdjustment',
  'TdsPayment',
  'TailoringIncome',
  'TailoringVendorCost',
  'CustomerAdvanceApplication',
  'VendorAdvanceApplication',
  'OtherIncome',
  'GST.OUTPUT_CGST',
  'GST.OUTPUT_SGST',
  'GST.OUTPUT_IGST',
  'GST.PAYABLE',
  'GST.INPUT_CGST',
  'GST.INPUT_SGST',
  'GST.INPUT_IGST',
  'PAYROLL.ADVANCE',
  'PAYROLL.STATUTORY_PAYABLE',
  'TAILORING.INCOME',
  'TAILORING.VENDOR_COST',
  'OTHER.INCOME',
  'SALES.OTHER_CHARGES',
  'VENDOR.ADVANCE',
  'VendorAdvancePayment',
  'INVENTORY.TRANSFER_CLEARING',
  'CustomerReceipt',
  'VendorPayment',
  'GeneralPayment',
  'ContraTransfer',
  'ExpensePayment',
  'ValidateRequiredMappings',
  'ValidateMappingAccount',
  'BuildSourceHash',
  'BuildMappingVersion'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPostingContracts.cs', [
  'FinalAccountsPostingRuleDto',
  'FinalAccountsMappingValidationResponse',
  'FinalAccountsPostingPreviewRequest',
  'FinalAccountsPostingAdapterDto',
  'FinalAccountsPostingPreviewResponse'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsSyncContracts.cs', [
  'FinalAccountsBackfillRequest',
  'FinalAccountsSyncOptionsResponse',
  'FinalAccountsBackfillPreviewResponse',
  'FinalAccountsSyncJobDto',
  'FinalAccountsReconciliationResponse',
  'WritesSourceData'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsSyncRules.cs', [
  'DefaultModules',
  'NormalizeModules',
  'NormalizeIdempotencyKey',
  'BuildResumeCheckpoint',
  'BuildJobNumber',
  'StatusForSource',
  'ShouldContinueAfterFailure',
  'NextRetryAt'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsSyncService.cs', [
  'FinalAccountsSyncService',
  'ExistingOutboxDetected: false',
  'ScheduledModeEnabled',
  'DryRunBackfillAsync',
  'CreateManualSyncJobAsync',
  'GetReconciliationAsync',
  'CleanupDryRunJobAsync',
  'LoadCandidatesAsync',
  'LoadLinksAsync',
  'UpsertCheckpointAsync',
  'SourceHashDrift',
  'WritesSourceData: false'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsReportService.cs', [
  'FinalAccountsReportService',
  'GetGeneralLedgerAsync',
  'GetTrialBalanceAsync',
  'ExportGeneralLedgerAsync',
  'ExportTrialBalanceAsync',
  'OpeningSignedBalance',
  'BuildComparisonsAsync',
  'JournalDrillDownPath',
  'SourceDrillDownPath',
  'SimplePdf'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsReportContracts.cs', [
  'FinalAccountsGeneralLedgerReportQuery',
  'FinalAccountsGeneralLedgerReportResponse',
  'FinalAccountsTrialBalanceReportQuery',
  'FinalAccountsTrialBalanceReportResponse',
  'FinalAccountsTrialBalanceComparisonDto',
  'FinalAccountsReportExport'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsReportRules.cs', [
  'OpeningSignedBalance',
  'SplitSignedBalance',
  'BalanceType',
  'NormalizeTrialBalanceView',
  'NormalizeComparison',
  'PeriodKey',
  'BalanceStatus'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPostingAdapters.cs', [
  'IFinalAccountsPostingAdapter',
  'AdapterKey',
  'BuildPreviewAsync'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCashBankPostingAdapters.cs', [
  'FinalAccountsPaymentAdapterLines',
  'InvoicePaymentReceiptAdapter',
  'CustomerAdvanceReceiptAdapter',
  'PurchasePaymentAdapter',
  'VendorPaymentAdapter',
  'VoucherPaymentAdapter',
  'VoucherExpenseAdapter',
  'CashVoucherReceiptAdapter',
  'CashVoucherExpenseAdapter',
  'BankCashTransferAdapter',
  'cannot be silently allocated'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPurchasePostingAdapters.cs', [
  'FinalAccountsPurchaseAdapterLines',
  'PurchaseInvoiceAdapter',
  'PurchaseInvoiceCancellationAdapter',
  'PurchaseReturnAdapter',
  'VendorDebitNoteAdapter',
  'VendorAdvancePaymentAdapter',
  'PURCHASE.DIRECT',
  'PURCHASE.RETURN',
  'PURCHASE.FREIGHT',
  'GST.INPUT_CGST',
  'GST.INPUT_SGST',
  'GST.INPUT_IGST',
  'VENDOR.PAYABLE',
  'Purchase posting preview is not balanced'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsInventoryPostingAdapters.cs', [
  'FinalAccountsInventoryAdapterLines',
  'SaleCogsAdapter',
  'SaleReturnStockRestorationAdapter',
  'PurchaseInventoryAdapter',
  'PurchaseReturnInventoryAdapter',
  'StockAdjustmentAdapter',
  'StockTransferAdapter',
  'WeightedAverage',
  'INVENTORY.STOCK',
  'INVENTORY.COGS',
  'INVENTORY.SHORTAGE',
  'INVENTORY.EXCESS',
  'INVENTORY.TRANSFER_CLEARING',
  'Negative stock movement evidence',
  'Missing stock cost evidence',
  'Inventory posting preview is not balanced'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPayrollTaxPostingAdapters.cs', [
  'FinalAccountsPayrollTaxAdapterLines',
  'PayrollDraftFinalizationAdapter',
  'SalaryPaySlipFinalizationAdapter',
  'SalaryPaymentAdapter',
  'PayrollStatutoryPaymentAdapter',
  'GstPaymentAdapter',
  'TdsPaymentAdapter',
  'GstReturnSettlementAdapter',
  'TailoringIncomeAdapter',
  'TailoringVendorCostAdapter',
  'TailoringCustomerReceiptAdapter',
  'CustomerAdvanceApplicationAdapter',
  'VendorAdvanceApplicationAdapter',
  'OtherIncomeReceiptAdapter',
  'PAYROLL.EXPENSE',
  'PAYROLL.ADVANCE',
  'GST.PAYABLE',
  'TDS.PAYABLE',
  'TAILORING.INCOME',
  'TAILORING.VENDOR_COST',
  'OTHER.INCOME',
  'TailoringServiceGstRate'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsSalesPostingAdapters.cs', [
  'FinalAccountsSalesAdapterLines',
  'SalesInvoiceAdapter',
  'SalesReturnAdapter',
  'SalesInvoiceCancellationAdapter',
  'SalesCreditNoteAdapter',
  'CUSTOMER.RECEIVABLE',
  'SALES.REVENUE',
  'SALES.RETURN',
  'SALES.DISCOUNT',
  'GST.OUTPUT_CGST',
  'GST.OUTPUT_SGST',
  'GST.OUTPUT_IGST',
  'DimensionSummary',
  'Sales posting preview is not balanced'
])

checkFile('backend/Garmetix.Api/Program.cs', [
  'FinalAccountsPostingAdapterService',
  'FinalAccountsSyncService',
  'FinalAccountsReportService',
  'SalesInvoiceAdapter',
  'SalesReturnAdapter',
  'SalesInvoiceCancellationAdapter',
  'SalesCreditNoteAdapter',
  'PurchaseInvoiceAdapter',
  'PurchaseReturnAdapter',
  'VendorDebitNoteAdapter',
  'VendorAdvancePaymentAdapter',
  'SaleCogsAdapter',
  'SaleReturnStockRestorationAdapter',
  'PurchaseInventoryAdapter',
  'PurchaseReturnInventoryAdapter',
  'StockAdjustmentAdapter',
  'StockTransferAdapter',
  'PayrollDraftFinalizationAdapter',
  'SalaryPaySlipFinalizationAdapter',
  'SalaryPaymentAdapter',
  'PayrollStatutoryPaymentAdapter',
  'GstPaymentAdapter',
  'TdsPaymentAdapter',
  'GstReturnSettlementAdapter',
  'TailoringIncomeAdapter',
  'TailoringVendorCostAdapter',
  'TailoringCustomerReceiptAdapter',
  'CustomerAdvanceApplicationAdapter',
  'VendorAdvanceApplicationAdapter',
  'OtherIncomeReceiptAdapter',
  'InvoicePaymentReceiptAdapter',
  'BankCashTransferAdapter'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsPostingRulesTests.cs', [
  'CashReceiptAdapterLinesBalanceCustomerReceipt',
  'VendorPaymentAdapterLinesCreditPaymentRail',
  'ContraTransferDepositDebitsBankAndCreditsCash',
  'NegativeAdapterAmountReversesDebitAndCredit',
  'MixedPaymentModeRequiresSourceBreakdown',
  'SalesInvoiceAdapterLinesPresentReceivableRevenueDiscountGstAndRounding',
  'SalesReturnAdapterLinesReverseReceivableTaxDiscountAndRounding',
  'SalesCancellationAdapterLinesReverseOriginalSale',
  'InterstateSalesUseIgstOutputMapping',
  'LocalSalesFallbackTaxSplitsCgstAndSgst',
  'SalesCreditNoteUsesReturnShape',
  'PurchaseInvoiceAdapterLinesPostExpenseItcFreightRoundingAndPayable',
  'PurchaseReturnAdapterLinesReversePayablePurchaseItcAndFreight',
  'PurchaseCancellationAdapterLinesReverseOriginalPurchase',
  'InterstatePurchaseUsesInputIgstMapping',
  'VendorAdvancePaymentAdapterLinesUseAdvanceAsset',
  'SaleCogsLinesDebitCogsAndCreditInventory',
  'SaleReturnStockRestorationReversesCogs',
  'PurchaseInventoryOffsetsTemporaryPurchaseExpense',
  'PurchaseReturnInventoryReversesInventoryAndPurchaseReturn',
  'StockAdjustmentLinesSupportExcessAndShortage',
  'StockTransferLinesUseTransferClearing',
  'InventoryMovementEvidenceBlocksNegativeStock',
  'InventoryMovementEvidenceBlocksMissingCost',
  'PostingAdapterRequestNormalizesDuplicateMappingKeys',
  'PayrollFinalizationLinesSplitNetPayAndStatutoryDeductions',
  'SalaryPaymentLinesUsePayableOrAdvanceByComponent',
  'StatutoryGstAndTdsPaymentsCreditPaymentRail',
  'TailoringIncomeLinesSeparateServiceIncomeAndGst',
  'TailoringVendorCostAndAdvanceApplicationsBalance',
  'OtherIncomeReceiptLinesDebitPaymentAndCreditIncome',
  'Bs04eRulesAreDiscoverable'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsSyncRulesTests.cs', [
  'NormalizeModulesDefaultsToSupportedSet',
  'NormalizeModulesDeduplicatesAliases',
  'IdempotencyKeyIsStableAndBounded',
  'JobNumberUsesIdempotencyKeyForRerun',
  'StatusForSourceMarksExistingAndDrift',
  'PartialFailurePolicySupportsStopOrContinue',
  'RetryDelayBacksOffByAttempt'
])

checkFile('docs/final-accounts-bs-04d-inventory-cogs.md', [
  'perpetual weighted-average',
  'No closing-stock journal',
  'Negative stock movement evidence',
  'Missing cost evidence',
  'FIFO is not enabled'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsCatalog.cs', [
  'FinalAccountsAccountGroup',
  'FinalAccountsAccount',
  'FinalAccountsAccountMapping',
  'FinalAccountsFiscalYear',
  'FinalAccountsFiscalPeriod'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsGeneralLedger.cs', [
  'FinalAccountsJournalEntry',
  'FinalAccountsJournalLine',
  'FinalAccountsSourcePostingLink',
  'FinalAccountsJournalStatus'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsSync.cs', [
  'FinalAccountsSyncJob',
  'FinalAccountsSyncJobItem',
  'FinalAccountsSyncCheckpoint',
  'FinalAccountsSyncException',
  'FinalAccountsSyncJobStatus',
  'FinalAccountsSyncItemStatus',
  'IdempotencyKey'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsPostingRules.cs', [
  'FinalAccountsPostingRule',
  'FinalAccountsPostingRuleLine',
  'RuleCode',
  'MappingCategory'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260713162000_AddFinalAccountsCatalogAndFiscalPeriods.cs', [
  'fa_account_groups',
  'fa_accounts',
  'fa_account_mappings',
  'fa_fiscal_years',
  'fa_fiscal_periods'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260713170000_AddFinalAccountsGeneralLedger.cs', [
  'fa_journal_entries',
  'fa_journal_lines',
  'fa_source_posting_links',
  'CK_fa_journal_lines_single_side',
  'IX_fa_journal_entries_scope_idempotency'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260713173000_AddFinalAccountsPostingRules.cs', [
  'fa_posting_rules',
  'fa_posting_rule_lines',
  'IX_fa_posting_rules_scope_code_version',
  'FK_fa_posting_rule_lines_rule'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260713190000_AddFinalAccountsSyncJobs.cs', [
  'fa_sync_jobs',
  'fa_sync_job_items',
  'fa_sync_checkpoints',
  'fa_sync_exceptions',
  'IX_fa_sync_jobs_scope_idempotency',
  'IX_fa_sync_job_items_job_source',
  'IX_fa_sync_checkpoints_scope_module_key',
  'IX_fa_sync_exceptions_source_resolved'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsEnabledFilter.cs', [
  'StatusCodes.Status403Forbidden',
  'Final Accounts module is disabled',
  'setupPath = "/final-accounts/setup"'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsOptions.cs', [
  'public bool DefaultEnabled',
  'FinalAccountsSettingsDefaults.PostingMode'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsContracts.cs', [
  'FeatureKey = "FINAL_ACCOUNTS"',
  'ApiRoot = "/api/final-accounts"',
  'RouteRoot = "/final-accounts"'
])

checkFile('backend/Garmetix.Api.Tests/Auth/AccessPermissionMatrixTests.cs', [
  'FinalAccountsIsNotGrantedToExistingNonAdminRoles',
  'GarmetixPolicies.FinalAccounts'
])

checkFile('frontend/modular/config/routes.ts', [
  "id: 'final-accounts-home'",
  "id: 'final-accounts-setup'",
  "id: 'final-accounts-chart-of-accounts'",
  "id: 'final-accounts-fiscal-periods'",
  "id: 'final-accounts-general-ledger'",
  "id: 'final-accounts-reports'",
  "id: 'final-accounts-posting-rules'",
  "showInMenu: false",
  "targetApp: 'final-accounts'"
])

checkFile('frontend/modular/packages/shared-ui/components/ModularAppShell.vue', [
  "'final-accounts'",
  "href: '/chart-of-accounts'",
  "href: '/fiscal-periods'",
  "href: '/general-ledger'",
  "href: '/reports'",
  "href: '/posting-rules'",
  "href: '/setup'",
  "'final-accounts': '/final-accounts/'"
])

checkFile('frontend/modular/apps/final-accounts/pages/index.vue', [
  'Status unavailable',
  ':loading="loading"',
  'statusCards'
])

checkFile('frontend/modular/apps/final-accounts/pages/setup.vue', [
  'USwitch',
  'api.get<FinalAccountsSettings>',
  'api.put<FinalAccountsSettings>'
])

checkFile('frontend/modular/apps/final-accounts/pages/chart-of-accounts.vue', [
  'Chart Of Accounts',
  'account-groups',
  'account-mappings',
  'coa/seed-preview',
  'validation/summary'
])

checkFile('frontend/modular/apps/final-accounts/pages/fiscal-periods.vue', [
  'Fiscal Periods',
  'fiscal-years',
  'fiscal-periods',
  'FinalAccountsPeriodStatus'
])

checkFile('frontend/modular/apps/final-accounts/pages/general-ledger.vue', [
  'General Ledger',
  'journals/preview',
  'Post',
  'Reverse',
  'Audit'
])

checkFile('frontend/modular/apps/final-accounts/pages/reports.vue', [
  'Reports',
  'reports/general-ledger',
  'reports/trial-balance',
  'reports/general-ledger/export',
  'reports/trial-balance/export',
  'Zero balances',
  'Reversed audit rows',
  'Comparison'
])

checkFile('frontend/modular/apps/final-accounts/pages/posting-rules.vue', [
  'Posting Rules',
  'posting-rules/mapping-validation',
  'posting/preview',
  'mappingCategory',
  'mappingVersion'
])

checkFile('frontend/modular/apps/final-accounts/utils/final-accounts-api.ts', [
  'FinalAccountsAccountGroup',
  'FinalAccountsFiscalYear',
  'FinalAccountsJournal',
  'FinalAccountsJournalPayload',
  'FinalAccountsPostingRule',
  'FinalAccountsPostingAdapter',
  'FinalAccountsMappingValidation',
  'FinalAccountsPostingPreview',
  'FinalAccountsValidationSummary',
  'FinalAccountsGeneralLedgerReport',
  'FinalAccountsTrialBalanceReport',
  'async function download',
  'async function post',
  'async function remove'
])

checkFile('frontend/modular/apps/final-accounts/middleware/auth.global.ts', [
  'isFinalAccountsSetupSession',
  '/access-denied'
])

for (const check of checks) {
  console.log(`CHECK ${check}`)
}

if (failures.length > 0) {
  console.error('\nFinal Accounts readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nFinal Accounts BS-06 readiness passed.')

function checkFile(relativePath, markers) {
  const absolutePath = resolve(repoRoot, relativePath)
  if (!existsSync(absolutePath)) {
    failures.push(`Missing ${relativePath}`)
    return
  }

  const source = readFileSync(absolutePath, 'utf8')
  for (const marker of markers) {
    if (!source.includes(marker)) failures.push(`${relativePath} missing marker: ${marker}`)
  }
  checks.push(`${relativePath} -> ${markers.length} marker(s)`)
}

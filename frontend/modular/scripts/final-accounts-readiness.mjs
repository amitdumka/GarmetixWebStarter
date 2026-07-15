import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const modularRoot = fileURLToPath(new URL('..', import.meta.url))
const repoRoot = resolve(modularRoot, '../..')
const checks = []
const failures = []

console.log('Garmetix Final Accounts BS-20 readiness')

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs', [
  'MapFinalAccountsEndpoints',
  'FinalAccountsSettingsDefaults.ApiRoot',
  'RequireAuthorization(GarmetixPolicies.FinalAccounts)',
  'MapGet("/status"',
  'MapGet("/settings"',
  'MapPut("/settings"',
  'MapGet("/audit/accounting-master"',
  'MapGet("/audit/coa-normalization"',
  'MapGet("/audit/party-ledger-unification"',
  'MapGet("/audit/transaction-backfill-reconciliation"',
  'MapGet("/audit/direct-ledger-integration"',
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
  'MapGet("/reports/profit-loss"',
  'MapGet("/reports/profit-loss/export"',
  'MapGet("/reports/balance-sheet"',
  'MapGet("/reports/balance-sheet/export"',
  'MapGet("/reports/cash-flow"',
  'MapGet("/reports/cash-flow/export"',
  'MapGet("/reports/schedules"',
  'MapGet("/reports/schedules/export"',
  'MapGet("/ca/adjustments"',
  'MapPost("/ca/adjustments/preview"',
  'MapPost("/ca/adjustments/{id:guid}/submit"',
  'MapPost("/ca/adjustments/{id:guid}/approve"',
  'MapPost("/ca/adjustments/{id:guid}/post"',
  'MapPost("/ca/adjustments/{id:guid}/reverse"',
  'MapPost("/ca/adjustments/{id:guid}/comments"',
  'MapPost("/ca/adjustments/{id:guid}/attachments"',
  'MapGet("/ca/statement-line-comments"',
  'MapGet("/ca/report-versions"',
  'MapGet("/period-close/runs"',
  'MapPost("/period-close/preview"',
  'MapPost("/period-close/close"',
  'MapPost("/period-close/runs/{id:guid}/reopen"',
  'MapGet("/projections/scenarios"',
  'MapPost("/projections/scenarios"',
  'MapPost("/projections/scenarios/{id:guid}/clone"',
  'MapPost("/projections/scenarios/{id:guid}/approve"',
  'MapPost("/projections/baseline/actuals"',
  'MapPost("/projections/compare"',
  'MapGet("/tally/profiles"',
  'MapPost("/tally/preview"',
  'MapPost("/tally/export"',
  'MapGet("/exchange/runs"',
  'MapPost("/ca-package/preview"',
  'MapPost("/ca-package/export"',
  'MapGet("/coa/seed-preview"',
  'MapGet("/validation/summary"'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsExchangeService.cs', [
  'FinalAccountsExchangeService',
  'ListProfilesAsync',
  'CreateProfileAsync',
  'UpdateProfileAsync',
  'PreviewTallyAsync',
  'ExportTallyAsync',
  'PreviewCaPackageAsync',
  'ExportCaPackageAsync',
  'ListRunsAsync',
  'ZipArchive',
  'ZipChecksum',
  'NoDirectTallyPosting',
  'AuditLogEntry'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsExchangeRules.cs', [
  'FinalAccountsExchangeRules',
  'NoDirectProductionTallyPosting',
  'ParseDuplicatePolicy',
  'BuildTallyXmlFixture',
  'BuildJsonFixture',
  'Sha256Hex'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsExchangeContracts.cs', [
  'FinalAccountsTallyProfileRequest',
  'FinalAccountsExchangeRequest',
  'FinalAccountsCaPackageRequest',
  'FinalAccountsExchangePreviewResponse',
  'FinalAccountsCaPackageItemDto',
  'FinalAccountsExchangeRunDto'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsProjectionService.cs', [
  'FinalAccountsProjectionService',
  'ListScenariosAsync',
  'ImportActualBaselineAsync',
  'CreateScenarioAsync',
  'UpdateScenarioAsync',
  'CloneScenarioAsync',
  'SubmitScenarioAsync',
  'ApproveScenarioAsync',
  'ArchiveScenarioAsync',
  'CompareAsync',
  'ExportScenarioAsync',
  'FinalAccountsProjectionRules.BuildProjection',
  'AuditLogEntry'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsProjectionRules.cs', [
  'FinalAccountsProjectionRules',
  'MinimumHorizonMonths',
  'MaximumHorizonMonths',
  'NormalizeSeasonality',
  'BuildProjection',
  'Summarize',
  'CanSubmit',
  'CanApprove'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsProjectionContracts.cs', [
  'FinalAccountsProjectionScenarioSaveRequest',
  'FinalAccountsProjectionActualBaselineRequest',
  'FinalAccountsProjectionCompareRequest',
  'FinalAccountsProjectionScenarioDto',
  'FinalAccountsProjectionMonthDto',
  'FinalAccountsProjectionSummaryDto'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCaWorkspaceService.cs', [
  'FinalAccountsCaWorkspaceService',
  'ListAdjustmentsAsync',
  'CreateAdjustmentAsync',
  'UpdateAdjustmentAsync',
  'MoveAdjustmentAsync',
  'PostAdjustmentAsync',
  'ReverseAdjustmentAsync',
  'AddCommentAsync',
  'AddAttachmentAsync',
  'AddStatementLineCommentAsync',
  'CreateReportVersionAsync',
  'FinalAccountsCaWorkspaceRules.AuditStatusUnaudited',
  'FinalAccountsCaWorkspaceRules.CaAdjustmentSourceType',
  'AuditLogEntry',
  'BuildPreviewAsync'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCaWorkspaceRules.cs', [
  'FinalAccountsCaWorkspaceRules',
  'CanTransition',
  'ReportVersionFor',
  'AuditStatusUnaudited',
  'CaAdjustmentSourceType',
  'CaAdjustmentReversalSourceType'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCaWorkspaceContracts.cs', [
  'FinalAccountsAdjustmentSaveRequest',
  'FinalAccountsAdjustmentWorkflowRequest',
  'FinalAccountsAdjustmentPostRequest',
  'FinalAccountsAdjustmentReverseRequest',
  'FinalAccountsAdjustmentPreviewResponse',
  'FinalAccountsStatementLineCommentRequest',
  'FinalAccountsReportVersionRequest'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPeriodCloseService.cs', [
  'FinalAccountsPeriodCloseService',
  'ListRunsAsync',
  'PreviewCloseAsync',
  'CommitCloseAsync',
  'ReopenAsync',
  'PendingPostingCountAsync',
  'AddSnapshotsAsync',
  'ClosingBalancesAsync',
  'FinancialYearLock',
  'duplicate',
  'FinalAccountsPeriodCloseRules.Prepared',
  'FinalAccountsCloseRunStatus.Reopened'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPeriodCloseRules.cs', [
  'FinalAccountsPeriodCloseRules',
  'CanClose',
  'CanReopen',
  'OverallStatus',
  'BuildRunNumberPrefix'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPeriodCloseContracts.cs', [
  'FinalAccountsClosePreviewRequest',
  'FinalAccountsCloseCommitRequest',
  'FinalAccountsCloseReopenRequest',
  'FinalAccountsClosePreviewResponse',
  'FinalAccountsCloseRunDto',
  'FinalAccountsCloseChecklistItemDto'
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
  'GetProfitLossAsync',
  'GetBalanceSheetAsync',
  'GetCashFlowAsync',
  'GetSchedulesAsync',
  'ExportGeneralLedgerAsync',
  'ExportTrialBalanceAsync',
  'ExportProfitLossAsync',
  'ExportBalanceSheetAsync',
  'ExportCashFlowAsync',
  'ExportSchedulesAsync',
  'OpeningSignedBalance',
  'LoadProfitLossCategoryValuesAsync',
  'LoadBalanceSheetCategoryValuesAsync',
  'CashEquivalentTotal',
  'WorkingCapitalChange',
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
  'FinalAccountsProfitLossReportQuery',
  'FinalAccountsProfitLossReportResponse',
  'FinalAccountsBalanceSheetReportQuery',
  'FinalAccountsBalanceSheetReportResponse',
  'FinalAccountsCashFlowReportQuery',
  'FinalAccountsCashFlowReportResponse',
  'FinalAccountsSchedulesReportQuery',
  'FinalAccountsSchedulesReportResponse',
  'FinalAccountsStatementTemplateDto',
  'FinalAccountsStatementTemplateNodeDto',
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

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsStatementRules.cs', [
  'ProfitLossTemplate',
  'DefaultTemplateVersion',
  'Formula',
  'PercentOfSales',
  'NormalizeRoundingUnit',
  'ClassifyProfitLossCategory',
  'ValidateProfitLossMappings',
  'EvaluateFormula',
  'Opening inventory',
  'Closing inventory',
  'ProfitAfterTax',
  'BalanceSheetTemplate',
  'NormalizeEntityType',
  'ClassifyBalanceSheetCategory',
  'ClassifySchedule',
  'ClassifyCashFlowActivity',
  'IsCashEquivalent'
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
  'FinalAccountsCaWorkspaceService',
  'FinalAccountsPeriodCloseService',
  'FinalAccountsProjectionService',
  'FinalAccountsExchangeService',
  'FinalAccountsAccountingMasterAuditService',
  'FinalAccountsCoaNormalizationService',
  'FinalAccountsPartyLedgerUnificationService',
  'FinalAccountsTransactionBackfillReconciliationService',
  'FinalAccountsDirectLedgerIntegrationService',
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

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsReportRulesTests.cs', [
  'ProfitLossTemplateExposesVersionedFormulaNodes',
  'FormulaEvaluationSupportsAddAndSubtract',
  'ProfitLossClassificationUsesAccountShape',
  'StatementPercentagesAndRoundingAreStable',
  'BalanceSheetTemplateSupportsEntityTypesAndBalanceFormula',
  'BalanceSheetClassificationFindsCurrentNonCurrentAndEquity',
  'ScheduleAndCashFlowClassificationsUseAccountShape'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsCaWorkspaceRulesTests.cs', [
  'WorkflowAllowsExpectedCaTransitions',
  'WorkflowBlocksSkippedOrBackwardTransitions',
  'WorkflowMapsReportVersionsWithoutAuditedStatus',
  'PostedAndReversedAdjustmentsAreImmutable'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsPeriodCloseRulesTests.cs', [
  'RequiredBlockedChecklistPreventsClose',
  'OptionalWarningsDoNotBlockClose',
  'OnlyClosedRunsCanReopen',
  'RunNumberPrefixSeparatesPeriodAndYearClose'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsProjectionRulesTests.cs', [
  'ProjectionBuildsRequestedMonthlyHorizon',
  'ProjectionProducesStatementsCashFlowAndRatios',
  'HorizonMustBeOneToFiveYears',
  'SeasonalityRequiresTwelveFactors',
  'WorkflowGatesScenarioApproval'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsExchangeRulesTests.cs', [
  'DuplicatePolicyParsesSupportedValues',
  'RunNumberPrefixSeparatesTallyAndCaPackage',
  'MappingJsonRoundTrips',
  'TallyXmlFixtureContainsNoDirectPostingEnvelope',
  'ChecksumChangesWhenPayloadChanges'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsSecurityRules.cs', [
  'FinalAccountsSecurityRules',
  'PermissionMatrix',
  'DeniedDefaultRoleNames',
  'Biller',
  'POS',
  'IsScopeAllowed',
  'ScopedNotFoundMessage',
  'AuditRequirements',
  'Account mapping',
  'Journal',
  'Close/reopen',
  'Export/package',
  'ValidateAttachment',
  'MaxAttachmentBytes'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsSecurityRulesTests.cs', [
  'DefaultAccessRejectsBillerPosAndStoreRoles',
  'PermissionMatrixKeepsFinalAccountsOwnerAdminOnlyByDefault',
  'TenantScopeBlocksCrossCompanyStoreGroupAndStoreAccess',
  'IdEnumerationMessagesDoNotLeakRequestedIdentifier',
  'AuditCoverageIncludesMappingJournalCloseAndExport',
  'AttachmentValidationAllowsEvidenceFilesAndBlocksExecutablePayloads'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsQaRules.cs', [
  'FinalAccountsQaRules',
  'RequiredBrowserRoutes',
  'RequiredKeyboardRoutes',
  'ForbiddenMigrationTokens',
  'LargeExportWarningBytes',
  'FeatureDisabledStatus',
  'IsProductionHostChange'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsAccountingMasterAuditService.cs', [
  'FinalAccountsAccountingMasterAuditService',
  'RunAsync',
  'WritesData: false',
  'LedgerMissingGroup',
  'PartyMissingLedger',
  'CustomerMissingParty',
  'VendorMissingParty',
  'BuildSourceCoverageAsync',
  'DuplicateLedgersAsync',
  'DuplicateCustomersAsync'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsAccountingMasterAuditRules.cs', [
  'FinalAccountsAccountingMasterAuditRules',
  'BS16AccountingMasterAudit',
  'AuditEndpointPath',
  'BackupRequirement',
  'PendingCount',
  'SeverityForMissingLinks',
  'Recommendations'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsAccountingMasterAuditContracts.cs', [
  'FinalAccountsAccountingMasterAuditResponse',
  'FinalAccountsAccountingMasterCountDto',
  'FinalAccountsAccountingSourceCoverageDto',
  'FinalAccountsAccountingMasterIssueDto',
  'FinalAccountsAccountingDuplicateDto'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsAccountingMasterAuditRulesTests.cs', [
  'AuditEndpointIsReadOnlyAndStageNamed',
  'DuplicateKeysNormalizeForComparison',
  'PendingCountNeverGoesNegative',
  'MissingLinkSeveritySeparatesPartialAndTotalGaps',
  'RecommendationsCoverUnificationStages'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCoaNormalizationService.cs', [
  'FinalAccountsCoaNormalizationService',
  'PreviewAsync',
  'WritesData: false',
  'BuildControlAccounts',
  'MissingControlAccounts',
  'DuplicateLedgerGroups',
  'UnmappedLedgerGroups',
  'FinalAccountsCoaNormalizationRules.MigrationPlan'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCoaNormalizationRules.cs', [
  'FinalAccountsCoaNormalizationRules',
  'BS17IndianCOANormalization',
  'PreviewEndpointPath',
  'IndianPrimaryGroups',
  'Duties & Taxes',
  'Sundry Debtors',
  'ControlAccounts',
  'RollbackPlan'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsCoaNormalizationContracts.cs', [
  'FinalAccountsCoaNormalizationPreviewResponse',
  'FinalAccountsCoaGroupMappingDto',
  'FinalAccountsCoaDuplicateGroupDto',
  'FinalAccountsCoaControlAccountDto',
  'FinalAccountsCoaNormalizationStepDto'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsCoaNormalizationRulesTests.cs', [
  'PreviewEndpointIsReadOnlyAndStageNamed',
  'LedgerCategoryMapsToTallyPrimaryGroup',
  'KeywordOverridesGenericCategory',
  'UnknownGroupRequiresManualClassification',
  'ControlAccountsIncludeCriticalOperationalMappings',
  'MigrationAndRollbackPlansAreReviewFirst'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPartyLedgerUnificationService.cs', [
  'FinalAccountsPartyLedgerUnificationService',
  'PreviewAsync',
  'WritesData: false',
  'BuildRoleLink',
  'BuildIdentities',
  'BuildDuplicateParties',
  'MissingPartyLinks',
  'PartyFlagMismatch'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPartyLedgerUnificationRules.cs', [
  'FinalAccountsPartyLedgerUnificationRules',
  'BS18PartyLedgerUnification',
  'PreviewEndpointPath',
  'NormalizeIdentityKey',
  'StatusForRoleLink',
  'UnificationPlan',
  'RollbackPlan'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsPartyLedgerUnificationContracts.cs', [
  'FinalAccountsPartyLedgerUnificationPreviewResponse',
  'FinalAccountsPartyRoleLinkDto',
  'FinalAccountsPartyIdentityDto',
  'FinalAccountsPartyLedgerDuplicateDto',
  'FinalAccountsPartyLedgerStepDto'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsPartyLedgerUnificationRulesTests.cs', [
  'PreviewEndpointIsReadOnlyAndStageNamed',
  'IdentityKeyUsesStrongestAvailableIdentifier',
  'NormalizeNameCreatesStableKeys',
  'DuplicateKeyIncludesPartyCategory',
  'RoleLinkStatusSeparatesMissingPartyAndMissingLedger',
  'PlansRequireReviewAndRollback'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsTransactionBackfillReconciliationService.cs', [
  'FinalAccountsTransactionBackfillReconciliationService',
  'PreviewAsync',
  'WritesData: false',
  'DryRunBackfillAsync',
  'GetReconciliationAsync',
  'GetTrialBalanceAsync',
  'GetBalanceSheetAsync',
  'GetProfitLossAsync',
  'BuildModuleEvidence',
  'TrialBalanceDifference',
  'BalanceSheetDifference'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsTransactionBackfillReconciliationRules.cs', [
  'FinalAccountsTransactionBackfillReconciliationRules',
  'BS19TransactionBackfillReconciliation',
  'EvidenceEndpointPath',
  'ParseModules',
  'StatusForDifference',
  'ApprovalGates',
  'RollbackPlan'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsTransactionBackfillReconciliationContracts.cs', [
  'FinalAccountsTransactionBackfillReconciliationResponse',
  'FinalAccountsTransactionBackfillStatementEvidenceDto',
  'FinalAccountsTransactionBackfillModuleEvidenceDto',
  'FinalAccountsTransactionBackfillIssueDto',
  'FinalAccountsTransactionBackfillStepDto'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsTransactionBackfillReconciliationRulesTests.cs', [
  'EvidenceEndpointIsReadOnlyAndStageNamed',
  'ParseModulesAllowsCommaSeparatedDistinctValues',
  'ParseModulesReturnsNullForDefaultModuleSet',
  'StatusForDifferenceSeparatesBalancedReviewAndDifference',
  'SuggestedActionMatchesModuleStatus',
  'ApprovalAndRollbackPlansRequireBackupAndTrialBalance'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsDirectLedgerIntegrationService.cs', [
  'FinalAccountsDirectLedgerIntegrationService',
  'PreviewAsync',
  'WritesData: false',
  'JournalEntries',
  'JournalLines',
  'FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup',
  'GetTrialBalanceAsync',
  'GetProfitLossAsync',
  'GetBalanceSheetAsync',
  'ExceptionMappingsRemain',
  'SourceTypes'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsDirectLedgerIntegrationRules.cs', [
  'FinalAccountsDirectLedgerIntegrationRules',
  'BS20FinalAccountsLedgerIntegration',
  'EvidenceEndpointPath',
  'StatusForDifference',
  'ClassificationStatus',
  'StatementAmount',
  'Smoke source postings'
])

checkFile('backend/Garmetix.Api/FinalAccounts/FinalAccountsDirectLedgerIntegrationContracts.cs', [
  'FinalAccountsDirectLedgerIntegrationResponse',
  'FinalAccountsDirectLedgerTrialBalanceDto',
  'FinalAccountsDirectLedgerStatementComparisonDto',
  'FinalAccountsDirectLedgerGroupClassificationDto',
  'FinalAccountsDirectLedgerSourceTypeDto',
  'FinalAccountsDirectLedgerIssueDto'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsDirectLedgerIntegrationRulesTests.cs', [
  'EvidenceEndpointIsReadOnlyAndStageNamed',
  'StatusForDifferenceUsesAccountingTolerance',
  'ClassificationStatusSeparatesAutoReviewAndManual',
  'SuggestedActionMatchesClassificationRisk',
  'StatementAmountRespectsNaturalStatementSide',
  'PlansRequireBackupAndSourcePostingSmoke'
])

checkFile('backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsQaRulesTests.cs', [
  'LargeLedgerPaginationIsBoundedForQaRuns',
  'BackfillBatchResumeCheckpointIsStableAcrossModuleOrder',
  'LargeExportStreamingQaFlagsOversizedInMemoryPackages',
  'ConcurrentPostingQaUsesStableIdempotencyKey',
  'FeatureDisabledRegressionExpectsForbiddenSetupRedirect',
  'BrowserRouteAndKeyboardQaCoverFinalAccountsWorkspaces',
  'MigrationReviewRejectsDestructiveOperations',
  'ProductionHostChangesAreOutOfScopeForBalanceSheetQa'
])

checkFile('docs/final-accounts-security-audit.md', [
  'Permission Matrix',
  'Tenant Isolation',
  'ID Enumeration',
  'Audit Coverage',
  'Attachment Validation',
  'Biller/Cashier/POS/Sales'
])

checkFile('docs/final-accounts-accountant-guide.md', [
  'Final Accounts Accountant Guide',
  'Posted journals are immutable',
  'Evidence To Keep'
])

checkFile('docs/final-accounts-ca-review-guide.md', [
  'Final Accounts CA Review Guide',
  'Review Flow',
  'Report Versions'
])

checkFile('docs/final-accounts-developer-architecture.md', [
  'Final Accounts Developer Architecture',
  'FinalAccountsSecurityRules',
  'feature-flagged'
])

checkFile('docs/final-accounts-posting-rule-guide.md', [
  'Final Accounts Posting Rule Guide',
  'idempotency key',
  'Mapping Changes'
])

checkFile('docs/final-accounts-backfill-runbook.md', [
  'Final Accounts Backfill Runbook',
  'dry-run backfill',
  'reversal-based'
])

checkFile('docs/final-accounts-reconciliation-runbook.md', [
  'Final Accounts Reconciliation Runbook',
  'Trial Balance',
  'Cross-company rows'
])

checkFile('docs/final-accounts-closing-runbook.md', [
  'Final Accounts Closing Runbook',
  'period close preview',
  'Reopen'
])

checkFile('docs/final-accounts-rollback-runbook.md', [
  'Final Accounts Rollback Runbook',
  'Disable `FINAL_ACCOUNTS`',
  'Do not drop tables'
])

checkFile('docs/final-accounts-bs-14-qa-hardening.md', [
  'Final Accounts BS-14 QA And Hardening',
  'Automated Evidence',
  'Migration Review',
  'Stress And Regression Checks',
  'Browser And Accessibility QA',
  'Known Limitations'
])

checkFile('docs/final-accounts-bs-15-merge-readiness.md', [
  'Final Accounts BS-15 Merge Readiness Package',
  'Commit List',
  'Migration Summary',
  'Feature Flag Activation Steps',
  'Staging-Only Backfill Plan',
  'Reconciliation Evidence Required',
  'Production Rollout Proposal',
  'Rollback Proposal',
  'No auto-merge',
  'No auto-deploy',
  'Do not merge into `version6` until human review approves'
])

checkFile('docs/final-accounts-bs-16-accounting-master-audit.md', [
  'Final Accounts BS-16 Accounting Master Audit',
  'BS16AccountingMasterAudit',
  'GET /api/final-accounts/audit/accounting-master',
  'WritesData = false',
  'No source table mutation',
  'BS-17: Indian/Tally-compatible Chart of Accounts normalization'
])

checkFile('docs/final-accounts-bs-17-coa-normalization.md', [
  'Final Accounts BS-17 COA Normalization Preview',
  'BS17IndianCOANormalization',
  'GET /api/final-accounts/audit/coa-normalization',
  'WritesData = false',
  'Indian/Tally-style primary groups',
  'No live mutation'
])

checkFile('docs/final-accounts-bs-18-party-ledger-unification.md', [
  'Final Accounts BS-18 Party Ledger Unification Preview',
  'BS18PartyLedgerUnification',
  'GET /api/final-accounts/audit/party-ledger-unification',
  'WritesData = false',
  'Identity Key Priority',
  'No live mutation'
])

checkFile('docs/final-accounts-bs-19-transaction-backfill-reconciliation.md', [
  'Final Accounts BS-19 Transaction Backfill Reconciliation Evidence',
  'BS19TransactionBackfillReconciliation',
  'GET /api/final-accounts/audit/transaction-backfill-reconciliation',
  'WritesData = false',
  'Trial Balance difference must be zero',
  'No live mutation'
])

checkFile('docs/final-accounts-bs-20-direct-ledger-integration.md', [
  'BS-20 - Final Accounts Direct Ledger Integration',
  'BS20FinalAccountsLedgerIntegration',
  'GET /api/final-accounts/audit/direct-ledger-integration',
  'WritesData = false',
  'canonical Books ledger',
  'Do not switch Final Accounts report source'
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

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsCaWorkspace.cs', [
  'FinalAccountsAdjustmentBatch',
  'FinalAccountsAdjustmentLine',
  'FinalAccountsAdjustmentAttachment',
  'FinalAccountsAdjustmentComment',
  'FinalAccountsStatementLineComment',
  'FinalAccountsReportVersion',
  'FinalAccountsAdjustmentStatus',
  'FinalAccountsReportVersionKind'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsPeriodClose.cs', [
  'FinalAccountsCloseRun',
  'FinalAccountsCloseChecklistItem',
  'FinalAccountsCloseReportSnapshot',
  'FinalAccountsCloseBalanceSnapshot',
  'FinalAccountsCloseRunStatus',
  'FinalAccountsCloseType'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsProjection.cs', [
  'FinalAccountsProjectionScenario',
  'FinalAccountsProjectionAssumptionVersion',
  'FinalAccountsProjectionMonth',
  'FinalAccountsProjectionScenarioType',
  'FinalAccountsProjectionScenarioStatus',
  'FinalAccountsProjectionBaselineSource'
])

checkFile('backend/Garmetix.Domain/Generated/Models/FinalAccounts/FinalAccountsExchange.cs', [
  'FinalAccountsTallyProfile',
  'FinalAccountsExchangeRun',
  'FinalAccountsExchangeException',
  'FinalAccountsExchangeRunKind',
  'FinalAccountsExchangeRunStatus',
  'FinalAccountsTallyDuplicatePolicy'
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

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260714120000_AddFinalAccountsCaWorkspace.cs', [
  'fa_ca_adjustment_batches',
  'fa_ca_adjustment_lines',
  'fa_ca_adjustment_comments',
  'fa_ca_adjustment_attachments',
  'fa_statement_line_comments',
  'fa_report_versions',
  'CK_fa_ca_adjustment_lines_single_side'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260714130000_AddFinalAccountsPeriodClose.cs', [
  'fa_close_runs',
  'fa_close_checklist_items',
  'fa_close_report_snapshots',
  'fa_close_balance_snapshots',
  'IX_fa_close_runs_scope_period_status'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260714140000_AddFinalAccountsProjectionEngine.cs', [
  'fa_projection_scenarios',
  'fa_projection_assumption_versions',
  'fa_projection_months',
  'IX_fa_projection_scenarios_scope_number',
  'IX_fa_projection_assumptions_scenario_version',
  'IX_fa_projection_months_scenario_month'
])

checkFile('backend/Garmetix.Infrastructure/Data/Migrations/20260714150000_AddFinalAccountsExchangePackage.cs', [
  'fa_tally_profiles',
  'fa_exchange_runs',
  'fa_exchange_exceptions',
  'IX_fa_tally_profiles_scope_code',
  'IX_fa_exchange_runs_scope_number',
  'IX_fa_exchange_exceptions_run_code'
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
  "id: 'final-accounts-ca-workspace'",
  "id: 'final-accounts-closeout'",
  "id: 'final-accounts-projections'",
  "id: 'final-accounts-exchange'",
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
  "href: '/ca-workspace'",
  "href: '/closeout'",
  "href: '/projections'",
  "href: '/exchange'",
  "href: '/posting-rules'",
  "href: '/setup'",
  "'final-accounts': '/final-accounts/'"
])

checkFile('frontend/modular/apps/final-accounts/pages/index.vue', [
  'Status unavailable',
  ':loading="loading"',
  'to="/ca-workspace"',
  'to="/closeout"',
  'to="/projections"',
  'to="/exchange"',
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
  'reports/profit-loss',
  'reports/balance-sheet',
  'reports/cash-flow',
  'reports/schedules',
  'reports/general-ledger/export',
  'reports/trial-balance/export',
  'reports/profit-loss/export',
  'reports/balance-sheet/export',
  'reports/cash-flow/export',
  'reports/schedules/export',
  'Zero balances',
  'Reversed audit rows',
  'Hide zero P&L lines',
  'Profit & Loss',
  'Balance Sheet',
  'Cash Flow',
  'Schedules',
  'Comparison'
])

checkFile('frontend/modular/apps/final-accounts/pages/ca-workspace.vue', [
  'CA Workspace',
  'ca/adjustments',
  'ca/adjustments/preview',
  "'submit'",
  "'review'",
  "'approve'",
  '/post',
  '/reverse',
  '/comments',
  '/attachments',
  'Impact Preview',
  'Audit Status'
])

checkFile('frontend/modular/apps/final-accounts/pages/closeout.vue', [
  'Period Closeout',
  'period-close/runs',
  'period-close/preview',
  'period-close/close',
  '/reopen',
  'Preview Checklist',
  'Close Metrics',
  'Audit Trail',
  'confirmAllGates'
])

checkFile('frontend/modular/apps/final-accounts/pages/projections.vue', [
  'Projection Engine',
  'projections/scenarios',
  'projections/baseline/actuals',
  'projections/compare',
  '/clone',
  "'approve'",
  "'archive'",
  'Scenario Comparison',
  'seasonalityFactors'
])

checkFile('frontend/modular/apps/final-accounts/pages/exchange.vue', [
  'Tally Exchange & CA Package',
  'tally/profiles',
  'tally/preview',
  'tally/export',
  'ca-package/preview',
  'ca-package/export',
  'Exchange Runs',
  'zipChecksum',
  'Direct Tally posting disabled'
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
  'FinalAccountsProfitLossReport',
  'FinalAccountsBalanceSheetReport',
  'FinalAccountsCashFlowReport',
  'FinalAccountsSchedulesReport',
  'FinalAccountsAdjustment',
  'FinalAccountsAdjustmentPayload',
  'FinalAccountsAdjustmentPreview',
  'FinalAccountsStatementLineComment',
  'FinalAccountsReportVersion',
  'FinalAccountsCloseRun',
  'FinalAccountsClosePreview',
  'FinalAccountsCloseCommitRequest',
  'FinalAccountsProjectionScenario',
  'FinalAccountsProjectionScenarioPayload',
  'FinalAccountsProjectionBaseline',
  'FinalAccountsProjectionComparison',
  'FinalAccountsTallyProfile',
  'FinalAccountsExchangePreview',
  'FinalAccountsCaPackageRequest',
  'FinalAccountsExchangeRun',
  'downloadPost',
  'FinalAccountsStatementTemplate',
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

console.log('\nFinal Accounts BS-20 readiness passed.')

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

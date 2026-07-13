import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, type StoredAuthUser } from '@garmetix/shared-auth'

export interface FinalAccountsSettings {
  enabled: boolean
  featureKey: string
  apiRoot: string
  routeRoot: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  postingMode: string
  statementTemplate: string
  inventoryValuationMethod: string
  roundingScale: number
  allowHistoricalBackfill: boolean
  allowTallyExport: boolean
  allowProjections: boolean
  allowPeriodReopen: boolean
  checkedAtUtc: string
  message: string
}

export interface FinalAccountsSaveSettings {
  enabled: boolean
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  postingMode: string
  statementTemplate: string
  inventoryValuationMethod: string
  roundingScale: number
  allowHistoricalBackfill: boolean
  allowTallyExport: boolean
  allowProjections: boolean
  allowPeriodReopen: boolean
}

export type FinalAccountsAccountType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense' | 'ContraAsset' | 'ContraLiability'
export type FinalAccountsNaturalBalance = 'Debit' | 'Credit'
export type FinalAccountsPeriodStatus = 'Draft' | 'Open' | 'Closed' | 'Locked'
export type FinalAccountsMappingSourceType = 'Sales' | 'Purchase' | 'Inventory' | 'Gst' | 'Payroll' | 'CashBank' | 'Customer' | 'Vendor' | 'Adjustment' | 'Expense' | 'InterStore'
export type FinalAccountsJournalStatus = 'Draft' | 'Posted' | 'Reversed'
export type FinalAccountsAdjustmentStatus = 'Draft' | 'Submitted' | 'Review' | 'Approved' | 'Rejected' | 'Posted' | 'Reversed'
export type FinalAccountsReportVersionKind = 'Provisional' | 'Adjusted' | 'Final'

export interface FinalAccountsAccountGroup {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  parentGroupId?: string | null
  code: string
  name: string
  accountType: FinalAccountsAccountType
  naturalBalance: FinalAccountsNaturalBalance
  sortOrder: number
  isSystem: boolean
  isActive: boolean
  description?: string | null
  revision: number
  createdAt: string
  updatedAt?: string | null
}

export interface FinalAccountsAccount {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  accountGroupId: string
  accountGroupName?: string | null
  parentAccountId?: string | null
  code: string
  name: string
  accountType: FinalAccountsAccountType
  naturalBalance: FinalAccountsNaturalBalance
  openingBalance: number
  isControlAccount: boolean
  isSystem: boolean
  isActive: boolean
  description?: string | null
  sortOrder: number
  revision: number
  createdAt: string
  updatedAt?: string | null
}

export interface FinalAccountsAccountMapping {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  sourceType: FinalAccountsMappingSourceType
  mappingKey: string
  displayName: string
  accountId: string
  accountCode?: string | null
  accountName?: string | null
  isRequired: boolean
  isSystem: boolean
  isActive: boolean
  notes?: string | null
  revision: number
}

export interface FinalAccountsFiscalYear {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  name: string
  startDate: string
  endDate: string
  status: FinalAccountsPeriodStatus
  closedAt?: string | null
  revision: number
}

export interface FinalAccountsFiscalPeriod {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  fiscalYearId: string
  periodNumber: number
  name: string
  startDate: string
  endDate: string
  status: FinalAccountsPeriodStatus
  closedAt?: string | null
  revision: number
}

export interface FinalAccountsValidationIssue {
  severity: 'Info' | 'Warning' | 'Error'
  code: string
  message: string
  entityId?: string | null
}

export interface FinalAccountsValidationSummary {
  groupCount: number
  accountCount: number
  mappingCount: number
  fiscalYearCount: number
  fiscalPeriodCount: number
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsJournalLine {
  id: string
  accountId: string
  accountCode?: string | null
  accountName?: string | null
  lineNumber: number
  debit: number
  credit: number
  narration?: string | null
}

export interface FinalAccountsJournalEvent {
  at: string
  event: string
  actor?: string | null
  detail: string
}

export interface FinalAccountsJournal {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  entryNumber: string
  onDate: string
  fiscalPeriodId?: string | null
  status: FinalAccountsJournalStatus
  sourceType: string
  sourceId?: string | null
  referenceNumber?: string | null
  narration: string
  idempotencyKey?: string | null
  reversalOfJournalEntryId?: string | null
  reversalJournalEntryId?: string | null
  postedAt?: string | null
  postedBy?: string | null
  reversedAt?: string | null
  reversedBy?: string | null
  totalDebit: number
  totalCredit: number
  difference: number
  revision: number
  lines: FinalAccountsJournalLine[]
  events: FinalAccountsJournalEvent[]
}

export interface FinalAccountsJournalListRow {
  id: string
  entryNumber: string
  onDate: string
  status: FinalAccountsJournalStatus
  sourceType: string
  sourceId?: string | null
  referenceNumber?: string | null
  narration: string
  totalDebit: number
  totalCredit: number
  lineCount: number
  createdAt: string
  postedAt?: string | null
  reversedAt?: string | null
}

export interface FinalAccountsJournalList {
  page: number
  pageSize: number
  totalCount: number
  rows: FinalAccountsJournalListRow[]
}

export interface FinalAccountsJournalLinePayload {
  accountId: string
  debit: number
  credit: number
  narration?: string | null
}

export interface FinalAccountsJournalPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  onDate: string
  fiscalPeriodId?: string | null
  referenceNumber?: string | null
  narration?: string | null
  sourceType?: string | null
  sourceId?: string | null
  idempotencyKey?: string | null
  lines: FinalAccountsJournalLinePayload[]
}

export interface FinalAccountsJournalValidation {
  canPost: boolean
  totalDebit: number
  totalCredit: number
  difference: number
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsAdjustmentLinePayload {
  accountId: string
  debit: number
  credit: number
  narration?: string | null
  statementLineKey?: string | null
}

export interface FinalAccountsAdjustmentPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  title: string
  description?: string | null
  adjustmentDate: string
  fiscalPeriodId?: string | null
  referenceNumber?: string | null
  autoReverse: boolean
  autoReverseDate?: string | null
  lines: FinalAccountsAdjustmentLinePayload[]
}

export interface FinalAccountsAdjustmentLine {
  id: string
  accountId: string
  accountCode?: string | null
  accountName?: string | null
  lineNumber: number
  debit: number
  credit: number
  narration?: string | null
  statementLineKey?: string | null
}

export interface FinalAccountsAdjustmentComment {
  id: string
  body: string
  visibility: string
  createdBy?: string | null
  createdAt: string
}

export interface FinalAccountsAdjustmentAttachment {
  id: string
  fileName: string
  contentType?: string | null
  storageReference: string
  notes?: string | null
  uploadedBy?: string | null
  createdAt: string
}

export interface FinalAccountsAdjustmentEvent {
  at: string
  event: string
  actor?: string | null
  detail: string
}

export interface FinalAccountsAdjustment {
  id: string
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  batchNumber: string
  title: string
  description?: string | null
  adjustmentDate: string
  fiscalPeriodId?: string | null
  status: FinalAccountsAdjustmentStatus
  reportVersion: FinalAccountsReportVersionKind | string
  auditStatus: string
  autoReverse: boolean
  autoReverseDate?: string | null
  referenceNumber?: string | null
  journalEntryId?: string | null
  reversalJournalEntryId?: string | null
  decisionNotes?: string | null
  totalDebit: number
  totalCredit: number
  difference: number
  revision: number
  lines: FinalAccountsAdjustmentLine[]
  comments: FinalAccountsAdjustmentComment[]
  attachments: FinalAccountsAdjustmentAttachment[]
  events: FinalAccountsAdjustmentEvent[]
}

export interface FinalAccountsAdjustmentListRow {
  id: string
  batchNumber: string
  title: string
  adjustmentDate: string
  status: FinalAccountsAdjustmentStatus
  reportVersion: FinalAccountsReportVersionKind | string
  totalDebit: number
  totalCredit: number
  difference: number
  autoReverse: boolean
  autoReverseDate?: string | null
  journalEntryId?: string | null
  reversalJournalEntryId?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface FinalAccountsAdjustmentList {
  page: number
  pageSize: number
  totalCount: number
  rows: FinalAccountsAdjustmentListRow[]
}

export interface FinalAccountsAdjustmentPreviewLine {
  accountId: string
  accountCode: string
  accountName: string
  accountType: string
  debit: number
  credit: number
  netImpact: number
  statementImpact: string
  statementLineKey?: string | null
}

export interface FinalAccountsAdjustmentPreview {
  canPost: boolean
  totalDebit: number
  totalCredit: number
  difference: number
  reportVersionBefore: string
  reportVersionAfter: string
  profitLossImpact: number
  balanceSheetImpact: number
  lines: FinalAccountsAdjustmentPreviewLine[]
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsStatementLineComment {
  id: string
  statementType: string
  statementLineKey: string
  reportVersion: string
  periodFrom?: string | null
  periodTo?: string | null
  body: string
  createdBy?: string | null
  createdAt: string
}

export interface FinalAccountsReportVersion {
  id: string
  reportType: string
  versionKind: string
  periodFrom?: string | null
  periodTo?: string | null
  status: string
  auditStatus: string
  generatedAt: string
  generatedBy?: string | null
  notes?: string | null
}

export interface FinalAccountsGeneralLedgerReportRow {
  journalEntryId: string
  journalLineId: string
  entryNumber: string
  onDate: string
  accountId: string
  accountCode: string
  accountName: string
  accountType: FinalAccountsAccountType | string
  naturalBalance: FinalAccountsNaturalBalance | string
  storeId?: string | null
  status: FinalAccountsJournalStatus | string
  sourceType: string
  sourceId?: string | null
  referenceNumber?: string | null
  narration?: string | null
  debit: number
  credit: number
  runningDebit: number
  runningCredit: number
  runningBalance: number
  balanceType: string
  isReversal: boolean
  isReversed: boolean
  journalDrillDownPath: string
  sourceDrillDownPath: string
}

export interface FinalAccountsGeneralLedgerReport {
  page: number
  pageSize: number
  totalCount: number
  from?: string | null
  to?: string | null
  accountId?: string | null
  openingDebit: number
  openingCredit: number
  periodDebit: number
  periodCredit: number
  closingDebit: number
  closingCredit: number
  rows: FinalAccountsGeneralLedgerReportRow[]
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsTrialBalanceRow {
  groupId?: string | null
  groupCode: string
  groupName: string
  accountId?: string | null
  accountCode: string
  accountName: string
  accountType: FinalAccountsAccountType | string
  naturalBalance: FinalAccountsNaturalBalance | string
  openingDebit: number
  openingCredit: number
  periodDebit: number
  periodCredit: number
  closingDebit: number
  closingCredit: number
  balanceType: string
  drillDownPath: string
}

export interface FinalAccountsTrialBalanceComparison {
  periodKey: string
  from: string
  to: string
  debit: number
  credit: number
  difference: number
  status: string
}

export interface FinalAccountsTrialBalanceReport {
  view: 'Ledger' | 'Group' | string
  comparison: 'Monthly' | 'Quarterly' | string
  from?: string | null
  to?: string | null
  includeZeroBalances: boolean
  totalOpeningDebit: number
  totalOpeningCredit: number
  totalPeriodDebit: number
  totalPeriodCredit: number
  totalClosingDebit: number
  totalClosingCredit: number
  difference: number
  status: string
  rows: FinalAccountsTrialBalanceRow[]
  comparisons: FinalAccountsTrialBalanceComparison[]
  diagnostics: FinalAccountsValidationIssue[]
}

export interface FinalAccountsStatementTemplateNode {
  key: string
  label: string
  nodeType: string
  sortOrder: number
  parentKey?: string | null
  formula?: string | null
  signRule: string
  required: boolean
  drillDown: boolean
  note: string
  scheduleReference: string
  mappingRule: string
}

export interface FinalAccountsStatementTemplate {
  templateCode: string
  version: string
  name: string
  statementType: string
  defaultView: string
  defaultRoundingUnit: string
  hideZeroDefault: boolean
  nodes: FinalAccountsStatementTemplateNode[]
}

export interface FinalAccountsStatementMapping {
  accountId: string
  accountCode: string
  accountName: string
  groupId?: string | null
  groupName: string
  current: number
  previous: number
  drillDownPath: string
}

export interface FinalAccountsProfitLossLine {
  key: string
  label: string
  nodeType: string
  sortOrder: number
  parentKey?: string | null
  signRule: string
  current: number
  previous: number
  variance: number
  variancePercent?: number | null
  percentOfSales?: number | null
  note: string
  scheduleReference: string
  drillDownPath: string
  mappings: FinalAccountsStatementMapping[]
}

export interface FinalAccountsProfitLossHorizontal {
  metric: string
  current: number
  previous: number
  variance: number
  variancePercent?: number | null
}

export interface FinalAccountsProfitLossReport {
  template: FinalAccountsStatementTemplate
  view: 'Vertical' | 'Horizontal' | string
  roundingUnit: 'Ones' | 'Thousands' | 'Lakhs' | string
  from?: string | null
  to?: string | null
  hideZero: boolean
  revenue: number
  grossProfit: number
  ebitda: number
  profitBeforeTax: number
  profitAfterTax: number
  lines: FinalAccountsProfitLossLine[]
  horizontal: FinalAccountsProfitLossHorizontal[]
  mappingIssues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsBalanceSheetLine {
  key: string
  label: string
  section: string
  classification: string
  sortOrder: number
  current: number
  previous: number
  variance: number
  drillDown: boolean
  note: string
  drillDownPath: string
  mappings: FinalAccountsStatementMapping[]
}

export interface FinalAccountsBalanceSheetReport {
  template: FinalAccountsStatementTemplate
  entityType: string
  roundingUnit: string
  asOf: string
  previousAsOf?: string | null
  hideZero: boolean
  totalAssets: number
  totalLiabilities: number
  totalEquity: number
  currentYearProfit: number
  difference: number
  status: string
  lines: FinalAccountsBalanceSheetLine[]
  diagnostics: FinalAccountsValidationIssue[]
}

export interface FinalAccountsCashFlowLine {
  section: string
  key: string
  label: string
  amount: number
  note: string
  drillDownPath: string
}

export interface FinalAccountsCashFlowReport {
  method: string
  roundingUnit: string
  from?: string | null
  to?: string | null
  profitAfterTax: number
  nonCashAdjustments: number
  workingCapitalChanges: number
  operatingActivities: number
  investingActivities: number
  financingActivities: number
  netCashFlow: number
  openingCash: number
  closingCash: number
  reconciliationDifference: number
  status: string
  lines: FinalAccountsCashFlowLine[]
  diagnostics: FinalAccountsValidationIssue[]
}

export interface FinalAccountsScheduleRow {
  accountId: string
  accountCode: string
  accountName: string
  accountType: string
  naturalBalance: string
  ageBucket: string
  balance: number
  note: string
  drillDownPath: string
}

export interface FinalAccountsScheduleSection {
  key: string
  label: string
  total: number
  rows: FinalAccountsScheduleRow[]
}

export interface FinalAccountsSchedulesReport {
  asOf: string
  schedule: string
  total: number
  sections: FinalAccountsScheduleSection[]
  diagnostics: FinalAccountsValidationIssue[]
}

export interface FinalAccountsPostingRuleLine {
  mappingKey: string
  displayName: string
  mappingCategory: string
  direction: string
  expectedAccountType?: FinalAccountsAccountType | string | null
  isRequired: boolean
  allowControlAccount: boolean
  sortOrder: number
  notes?: string | null
}

export interface FinalAccountsPostingRule {
  sourceType: FinalAccountsMappingSourceType | string
  ruleCode: string
  version: string
  name: string
  description?: string | null
  lines: FinalAccountsPostingRuleLine[]
}

export interface FinalAccountsPostingAdapter {
  adapterKey: string
  sourceType: FinalAccountsMappingSourceType | string
  ruleCode: string
  ruleVersion: string
  displayName: string
  sourceTable: string
  description: string
}

export interface FinalAccountsMappingRequirement {
  sourceType: FinalAccountsMappingSourceType | string
  ruleCode: string
  ruleVersion: string
  mappingKey: string
  displayName: string
  mappingCategory: string
  direction: string
  expectedAccountType?: string | null
  isRequired: boolean
  allowControlAccount: boolean
  mappingId?: string | null
  accountId?: string | null
  accountCode?: string | null
  accountName?: string | null
  accountType?: string | null
  accountIsControl?: boolean | null
  mappingActive: boolean
  status: 'Mapped' | 'Missing' | 'Invalid' | 'Inactive' | 'Optional' | string
  issueCode?: string | null
  issueMessage?: string | null
}

export interface FinalAccountsMappingValidation {
  ruleCount: number
  requirementCount: number
  mappedCount: number
  missingCount: number
  issueCount: number
  requirements: FinalAccountsMappingRequirement[]
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsPostingPreviewLinePayload {
  mappingKey: string
  debit: number
  credit: number
  narration?: string | null
}

export interface FinalAccountsPostingPreviewPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  sourceType: string
  ruleCode?: string | null
  ruleVersion?: string | null
  sourceId?: string | null
  sourceReference?: string | null
  sourceHash?: string | null
  mappingKeys?: string[] | null
  lines?: FinalAccountsPostingPreviewLinePayload[] | null
}

export interface FinalAccountsPostingPreviewLine {
  mappingKey: string
  displayName: string
  direction: string
  accountId?: string | null
  accountCode?: string | null
  accountName?: string | null
  debit: number
  credit: number
  narration?: string | null
}

export interface FinalAccountsPostingPreview {
  canPost: boolean
  sourceType: string
  ruleCode: string
  ruleVersion: string
  sourceHash: string
  mappingVersion: string
  lines: FinalAccountsPostingPreviewLine[]
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsSeedPreview {
  template: string
  groups: Array<{ code: string; name: string; accountType: FinalAccountsAccountType; naturalBalance: FinalAccountsNaturalBalance; parentCode?: string | null }>
  accounts: Array<{ code: string; name: string; groupCode: string; accountType: FinalAccountsAccountType; naturalBalance: FinalAccountsNaturalBalance; isControlAccount: boolean }>
  issues: FinalAccountsValidationIssue[]
}

export interface FinalAccountsAccountGroupPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  parentGroupId?: string | null
  code: string
  name: string
  accountType: FinalAccountsAccountType
  naturalBalance: FinalAccountsNaturalBalance
  sortOrder?: number
  isActive?: boolean
  description?: string | null
}

export interface FinalAccountsAccountPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  accountGroupId: string
  parentAccountId?: string | null
  code: string
  name: string
  accountType: FinalAccountsAccountType
  naturalBalance: FinalAccountsNaturalBalance
  openingBalance?: number
  isControlAccount?: boolean
  isActive?: boolean
  description?: string | null
  sortOrder?: number
}

export interface FinalAccountsAccountMappingPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  sourceType: FinalAccountsMappingSourceType
  mappingKey: string
  displayName: string
  accountId: string
  isRequired?: boolean
  isActive?: boolean
  notes?: string | null
}

export interface FinalAccountsFiscalYearPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  name: string
  startDate: string
  endDate: string
  status?: FinalAccountsPeriodStatus
}

export interface FinalAccountsFiscalPeriodPayload {
  companyId?: string | null
  storeGroupId?: string | null
  storeId?: string | null
  fiscalYearId: string
  periodNumber: number
  name: string
  startDate: string
  endDate: string
  status?: FinalAccountsPeriodStatus
}

export function useFinalAccountsApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function client() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  async function get<T>(path: string) {
    return await client().get<T>(normalizeFinalAccountsPath(path))
  }

  async function put<T>(path: string, body?: unknown) {
    return await client().put<T>(normalizeFinalAccountsPath(path), body)
  }

  async function post<T>(path: string, body?: unknown) {
    return await client().post<T>(normalizeFinalAccountsPath(path), body)
  }

  async function remove<T = void>(path: string) {
    return await client().delete<T>(normalizeFinalAccountsPath(path))
  }

  async function download(path: string, fileName: string) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const base = String(apiBaseUrl.value).replace(/\/+$/, '')
    const response = await fetch(`${base}/${normalizeFinalAccountsPath(path)}`, {
      headers: getStoredToken(window.localStorage) ? { Authorization: `Bearer ${getStoredToken(window.localStorage)}` } : {}
    })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  }

  return { apiBaseUrl, get, put, post, remove, download }
}

export function isFinalAccountsSetupSession(user: StoredAuthUser | null | undefined) {
  if (!user) return false
  const values = [
    user.role,
    user.userType,
    user.appOperation,
    user.isSuperAdmin ? 'SuperAdmin' : '',
    user.admin ? 'Admin' : ''
  ].filter(Boolean).map(value => String(value).toLowerCase().replace(/\s+/g, ''))
  return values.some(value => ['superadmin', 'owner', 'admin', 'all'].includes(value))
}

export function formatFinalAccountsDate(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

export function formatFinalAccountsDateOnly(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

function normalizeFinalAccountsPath(path: string) {
  const clean = String(path || '').replace(/^\/+/, '')
  return clean.startsWith('final-accounts/') ? clean : `final-accounts/${clean}`
}

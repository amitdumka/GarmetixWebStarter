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
export type FinalAccountsMappingSourceType = 'Sales' | 'Purchase' | 'Inventory' | 'Gst' | 'Payroll' | 'CashBank' | 'Customer' | 'Vendor' | 'Adjustment'

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

  return { apiBaseUrl, get, put, post, remove }
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

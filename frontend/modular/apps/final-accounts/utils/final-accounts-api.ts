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

  return { apiBaseUrl, get, put }
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

function normalizeFinalAccountsPath(path: string) {
  const clean = String(path || '').replace(/^\/+/, '')
  return clean.startsWith('final-accounts/') ? clean : `final-accounts/${clean}`
}

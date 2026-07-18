import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, type StoredAuthUser } from '@garmetix/shared-auth'

export type ApiRecord = Record<string, unknown>

export function useCommunicationApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function createClient() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    return await createClient().get<T>(path, { query })
  }

  async function post<T>(path: string, body?: unknown) {
    return await createClient().post<T>(path, body)
  }

  async function put<T>(path: string, body?: unknown) {
    return await createClient().put<T>(path, body)
  }

  async function del<T>(path: string) {
    return await createClient().delete<T>(path)
  }

  return { apiBaseUrl, get, post, put, del }
}

export function readText(source: ApiRecord | null | undefined, keys: string[] | null | undefined, fallback = '-') {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

export function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

// Everyone who can hold a personal inbox - matches GarmetixPolicies.Communication's ModuleRoles
// on the backend (AccessPermissionMatrix.cs). Admin/PowerUser/Owner/SuperAdmin always pass via
// isAdminOrOwner-style claims handled below.
const COMMUNICATION_ROLES = new Set([
  'superadmin', 'owner', 'admin', 'poweruser', 'accountant', 'remoteaccountant',
  'storemanager', 'salesman', 'hr', 'payroll'
])

// Admin-tier only - matches GarmetixPolicies.CommunicationProviders/Templates/Queue/Suppression.
const COMMUNICATION_ADMIN_ROLES = new Set(['superadmin', 'owner', 'admin', 'poweruser'])

function sessionRoleValues(user: StoredAuthUser | null | undefined) {
  if (!user) return []
  return [
    user.role,
    user.userType,
    user.appOperation,
    user.isSuperAdmin ? 'SuperAdmin' : '',
    user.admin ? 'Admin' : ''
  ].filter(Boolean).map(value => String(value).toLowerCase().replace(/\s+/g, ''))
}

export function isCommunicationSession(user: StoredAuthUser | null | undefined) {
  return sessionRoleValues(user).some(value => COMMUNICATION_ROLES.has(value))
}

export function isCommunicationAdminSession(user: StoredAuthUser | null | undefined) {
  return sessionRoleValues(user).some(value => COMMUNICATION_ADMIN_ROLES.has(value))
}

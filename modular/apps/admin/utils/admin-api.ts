import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, type StoredAuthUser } from '@garmetix/shared-auth'

export type ApiRecord = Record<string, unknown>

export function useAdminApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const api = createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
    return await api.get<T>(path, { query })
  }

  return { apiBaseUrl, get }
}

export function readText(source: ApiRecord | null | undefined, keys: string[], fallback = '-') {
  for (const key of keys) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

export function readNumber(source: ApiRecord | null | undefined, keys: string[]) {
  for (const key of keys) {
    const value = source?.[key]
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))) return Number(value)
  }
  return 0
}

export function readArray(source: ApiRecord | null | undefined, keys: string[]) {
  for (const key of keys) {
    const value = source?.[key]
    if (Array.isArray(value)) return value as ApiRecord[]
  }
  return []
}

export function toRows(value: unknown): ApiRecord[] {
  if (Array.isArray(value)) return value as ApiRecord[]
  if (value && typeof value === 'object') return readArray(value as ApiRecord, ['items', 'rows', 'data', 'results', 'checks', 'probes', 'modules'])
  return []
}

export function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

export function isAdminSession(user: StoredAuthUser | null | undefined) {
  if (!user) return false
  const values = [
    user.role,
    user.userType,
    user.appOperation,
    user.isSuperAdmin ? 'SuperAdmin' : '',
    user.admin ? 'Admin' : ''
  ].filter(Boolean).map(value => String(value).toLowerCase())
  return values.some(value => ['superadmin', 'owner', 'admin'].includes(value.replace(/\s+/g, '')))
}

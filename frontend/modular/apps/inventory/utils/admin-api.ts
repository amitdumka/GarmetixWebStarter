import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, type StoredAuthUser } from '@garmetix/shared-auth'
import { stripServerUrl } from '@garmetix/shared-utils'

export type ApiRecord = Record<string, unknown>

export function useAdminApiClient() {
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

  function apiUrl(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost'
    const url = new URL(createApiUrl(apiBaseUrl.value, path), origin)
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined) url.searchParams.set(key, String(value))
    }
    return url.toString()
  }

  async function download(path: string, query?: Record<string, string | number | boolean | null | undefined>, fallbackFileName = 'garmetix-document.csv') {
    const token = getStoredToken(window.localStorage)
    const headers = new Headers()
    if (token) headers.set('Authorization', `Bearer ${token}`)

    const response = await fetch(apiUrl(path, query), { method: 'GET', headers })
    if (!response.ok) {
      const message = await response.text()
      throw new Error(stripServerUrl(message || `Download failed with ${response.status}`))
    }

    const blob = await response.blob()
    const disposition = response.headers.get('content-disposition') ?? ''
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
    const fileName = match?.[1] ? decodeURIComponent(match[1]) : fallbackFileName
    const objectUrl = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = objectUrl
    anchor.download = fileName
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(objectUrl)
  }

  return { apiBaseUrl, get, post, put, del, apiUrl, download }
}

export function readText(source: ApiRecord | null | undefined, keys: string[] | null | undefined, fallback = '-') {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

export function readNumber(source: ApiRecord | null | undefined, keys: string[] | null | undefined) {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))) return Number(value)
  }
  return 0
}

export function readArray(source: ApiRecord | null | undefined, keys: string[] | null | undefined) {
  for (const key of keys ?? []) {
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

export const pageSizeOptions = [
  { label: '25 / page', value: 25 },
  { label: '50 / page', value: 50 },
  { label: '100 / page', value: 100 }
] as const

export function paginateRows<T>(rows: T[], page: number, pageSize: number): T[] {
  const size = Math.max(1, Number(pageSize) || 25)
  const start = Math.max(0, (Math.max(1, Number(page) || 1) - 1) * size)
  return rows.slice(start, start + size)
}

export function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

const INVENTORY_ROLES = new Set(['superadmin', 'owner', 'admin', 'poweruser', 'storemanager'])

export function isInventorySession(user: StoredAuthUser | null | undefined) {
  if (!user) return false
  const values = [
    user.role,
    user.userType,
    user.appOperation,
    user.isSuperAdmin ? 'SuperAdmin' : '',
    user.admin ? 'Admin' : ''
  ].filter(Boolean).map(value => String(value).toLowerCase().replace(/\s+/g, ''))
  return values.some(value => INVENTORY_ROLES.has(value))
}

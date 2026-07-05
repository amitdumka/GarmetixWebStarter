import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { stripServerUrl } from '@garmetix/shared-utils'

export type ApiRecord = Record<string, unknown>

export function useCrmApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function normalizeCrmApiPath(path: string) {
    return String(path || '').replace(/^\/+/, '').replace(/^api\/+/i, '')
  }

  function createClient() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  function createUrl(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const fallbackOrigin = typeof window !== 'undefined' && window.location?.origin ? window.location.origin : 'http://localhost'
    const url = new URL(createApiUrl(apiBaseUrl.value, normalizeCrmApiPath(path)), fallbackOrigin)
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined && value !== '') url.searchParams.set(key, String(value))
    }

    return url
  }

  function createAuthHeaders() {
    const headers = new Headers()
    const token = typeof window !== 'undefined' ? getStoredToken(window.localStorage) : null
    if (token) headers.set('Authorization', `Bearer ${token}`)
    return headers
  }

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    const api = createClient()
    return await api.get<T>(normalizeCrmApiPath(path), { query })
  }

  async function post<T>(path: string, body?: unknown) {
    const api = createClient()
    return await api.post<T>(normalizeCrmApiPath(path), body)
  }

  async function put<T>(path: string, body?: unknown) {
    const api = createClient()
    return await api.put<T>(normalizeCrmApiPath(path), body)
  }

  async function remove<T>(path: string) {
    const api = createClient()
    return await api.delete<T>(normalizeCrmApiPath(path))
  }

  async function getBlob(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    const response = await fetch(createUrl(path, query), {
      method: 'GET',
      headers: createAuthHeaders()
    })
    if (!response.ok) {
      const message = await response.text()
      throw new Error(stripServerUrl(message || `Garmetix API request failed with ${response.status}`))
    }

    return await response.blob()
  }

  return { apiBaseUrl, createUrl, get, post, put, remove, getBlob }
}

export function readNumber(source: ApiRecord | null | undefined, keys: string[] | null | undefined) {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))) return Number(value)
  }
  return 0
}

export function readText(source: ApiRecord | null | undefined, keys: string[] | null | undefined, fallback = '-') {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

export function readArray(source: ApiRecord | null | undefined, keys: string[] | null | undefined) {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (Array.isArray(value)) return value as ApiRecord[]
  }
  return []
}

export function readRecord(source: unknown, keys: string[] = ['item', 'row', 'data', 'customer', 'result']) {
  if (!source || typeof source !== 'object' || Array.isArray(source)) return null
  const record = source as ApiRecord
  for (const key of keys) {
    const value = record[key]
    if (value && typeof value === 'object' && !Array.isArray(value)) return value as ApiRecord
  }
  return record
}

export function toRows(value: unknown, keys: string[] = ['items', 'rows', 'data', 'results']) {
  if (Array.isArray(value)) return value as ApiRecord[]
  if (value && typeof value === 'object') return readArray(value as ApiRecord, keys)
  return []
}

export function formatDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  }).format(date)
}

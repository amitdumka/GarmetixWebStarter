import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'

export type ApiRecord = Record<string, unknown>

export function useHrApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function normalizeHrApiPath(path: string) {
    return String(path || '').replace(/^\/+/, '').replace(/^api\/+/i, '')
  }

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const api = createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
    return await api.get<T>(normalizeHrApiPath(path), { query })
  }

  function createClient() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  async function post<T>(path: string, body?: unknown) {
    const api = createClient()
    return await api.post<T>(normalizeHrApiPath(path), body)
  }

  async function put<T>(path: string, body?: unknown) {
    const api = createClient()
    return await api.put<T>(normalizeHrApiPath(path), body)
  }

  async function del<T>(path: string) {
    const api = createClient()
    return await api.delete<T>(normalizeHrApiPath(path))
  }

  async function downloadFile(path: string, fileName: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const url = new URL(`${apiBaseUrl.value.replace(/\/+$/, '')}/${normalizeHrApiPath(path)}`, window.location.origin)
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined) url.searchParams.set(key, String(value))
    }

    const token = getStoredToken(window.localStorage)
    const response = await fetch(url, {
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })
    if (!response.ok) {
      throw new Error(await response.text() || `Download failed with ${response.status}`)
    }

    const blob = await response.blob()
    const objectUrl = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = objectUrl
    anchor.download = fileName
    anchor.click()
    URL.revokeObjectURL(objectUrl)
  }

  return { apiBaseUrl, get, post, put, del, downloadFile }
}

export function toLocalDateInput(value = new Date()) {
  const date = value instanceof Date ? value : new Date(value)
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10)
}

export function currentYearMonth() {
  const now = new Date()
  return {
    year: now.getFullYear(),
    month: now.getMonth() + 1
  }
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

export function readBoolean(source: ApiRecord | null | undefined, keys: string[] | null | undefined, fallback = false) {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (typeof value === 'boolean') return value
    if (typeof value === 'number') return value !== 0
    if (typeof value === 'string') {
      const normalized = value.trim().toLowerCase()
      if (['true', 'yes', 'y', '1'].includes(normalized)) return true
      if (['false', 'no', 'n', '0'].includes(normalized)) return false
    }
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

export interface CsvColumn {
  key: string
  label: string
}

export function downloadCsvFile(fileName: string, rows: ApiRecord[], columns: CsvColumn[]) {
  const header = columns.map(column => csvCell(column.label)).join(',')
  const body = rows.map(row => columns.map(column => csvCell(row[column.key])).join(','))
  const csv = [header, ...body].join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  anchor.click()
  URL.revokeObjectURL(objectUrl)
}

function csvCell(value: unknown) {
  const text = value === null || value === undefined ? '' : String(value)
  return `"${text.replace(/"/g, '""')}"`
}

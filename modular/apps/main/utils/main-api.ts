import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'

export type ApiRecord = Record<string, unknown>

export function useMainApiClient() {
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

export function readNumber(source: ApiRecord | null | undefined, keys: string[]) {
  for (const key of keys) {
    const value = source?.[key]
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))) return Number(value)
  }
  return 0
}

export function readText(source: ApiRecord | null | undefined, keys: string[], fallback = '-') {
  for (const key of keys) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

export function readArray(source: ApiRecord | null | undefined, keys: string[]) {
  for (const key of keys) {
    const value = source?.[key]
    if (Array.isArray(value)) return value as ApiRecord[]
  }
  return []
}

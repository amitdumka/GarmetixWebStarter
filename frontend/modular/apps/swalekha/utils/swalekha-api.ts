import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'

export interface SwalekhaHealth {
  ok: boolean
  database: string
  ownerName: string
  generatedAtUtc: string
}

export function useSwalekhaApiClient() {
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
    return await client().get<T>(normalizeSwalekhaPath(path))
  }

  async function post<T>(path: string, body?: unknown) {
    return await client().post<T>(normalizeSwalekhaPath(path), body)
  }

  return { apiBaseUrl, get, post }
}

function normalizeSwalekhaPath(path: string) {
  const clean = String(path || '').replace(/^\/+/, '')
  return clean.startsWith('swalekha/') ? clean : `swalekha/${clean}`
}

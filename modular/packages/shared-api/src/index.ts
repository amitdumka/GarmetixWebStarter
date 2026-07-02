import { stripServerUrl } from '@garmetix/shared-utils'

export interface GarmetixApiClientOptions {
  baseUrl: string
  getToken?: () => string | null | undefined
}

export interface ApiRequestOptions extends RequestInit {
  query?: Record<string, string | number | boolean | null | undefined>
}

export type ApiHealthState = 'checking' | 'live' | 'offline'

export interface ApiHealthResult {
  state: ApiHealthState
  label: string
  message: string
  checkedAt?: string
}

export interface AuthLoginRequest {
  userName: string
  password: string
}

export interface AuthLoginResponse<TUser = unknown> {
  token: string
  expiresAtUtc: string
  user: TUser
}

export function createApiUrl(baseUrl: string, path: string) {
  const base = String(baseUrl || '').replace(/\/+$/, '')
  const nextPath = String(path || '').replace(/^\/+/, '')
  return nextPath ? `${base}/${nextPath}` : base
}

function createBrowserSafeUrl(baseUrl: string, path: string) {
  const apiUrl = createApiUrl(baseUrl, path)
  const fallbackOrigin = typeof window !== 'undefined' && window.location?.origin
    ? window.location.origin
    : 'http://localhost'
  return new URL(apiUrl, fallbackOrigin)
}

export function createApiHealthUrl(baseUrl: string, healthPath = '/health') {
  return createApiUrl(baseUrl, healthPath)
}

export function createAuthUrl(baseUrl: string, path: string) {
  return createApiUrl(baseUrl, `auth/${String(path).replace(/^\/+/, '')}`)
}

export async function checkApiHealth(baseUrl: string, healthPath = '/health'): Promise<ApiHealthResult> {
  const checkedAt = new Date().toISOString()
  if (!baseUrl) {
    return {
      state: 'offline',
      label: 'API not configured',
      message: 'Set the API base URL in the app environment.',
      checkedAt
    }
  }

  try {
    const response = await fetch(createApiHealthUrl(baseUrl, healthPath), {
      method: 'GET',
      cache: 'no-store'
    })

    if (!response.ok) {
      return {
        state: 'offline',
        label: 'API error',
        message: stripServerUrl(`Health check failed with ${response.status}`),
        checkedAt
      }
    }

    return {
      state: 'live',
      label: 'API live',
      message: 'Service health endpoint responded.',
      checkedAt
    }
  } catch (error) {
    return {
      state: 'offline',
      label: 'API unreachable',
      message: stripServerUrl(error instanceof Error ? error.message : 'Health check failed.'),
      checkedAt
    }
  }
}

export function createGarmetixApiClient(options: GarmetixApiClientOptions) {
  const baseUrl = options.baseUrl.replace(/\/$/, '')

  async function request<T>(path: string, requestOptions: ApiRequestOptions = {}): Promise<T> {
    const url = createBrowserSafeUrl(baseUrl, path)
    for (const [key, value] of Object.entries(requestOptions.query ?? {})) {
      if (value !== null && value !== undefined) url.searchParams.set(key, String(value))
    }

    const headers = new Headers(requestOptions.headers)
    const token = options.getToken?.()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    if (requestOptions.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')

    const response = await fetch(url, { ...requestOptions, headers })
    if (!response.ok) {
      const message = await response.text()
      throw new Error(stripServerUrl(message || `Garmetix API request failed with ${response.status}`))
    }

    if (response.status === 204) return undefined as T
    return await response.json() as T
  }

  return {
    get: <T>(path: string, options?: ApiRequestOptions) => request<T>(path, { ...options, method: 'GET' }),
    post: <T>(path: string, body?: unknown, options?: ApiRequestOptions) => request<T>(path, { ...options, method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
    put: <T>(path: string, body?: unknown, options?: ApiRequestOptions) => request<T>(path, { ...options, method: 'PUT', body: body === undefined ? undefined : JSON.stringify(body) }),
    delete: <T>(path: string, options?: ApiRequestOptions) => request<T>(path, { ...options, method: 'DELETE' })
  }
}

export async function loginToGarmetix<TUser = unknown>(baseUrl: string, request: AuthLoginRequest): Promise<AuthLoginResponse<TUser>> {
  const response = await fetch(createAuthUrl(baseUrl, 'login'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request)
  })

  if (!response.ok) {
    const message = await response.text()
    throw new Error(stripServerUrl(message || 'Login failed. Check the username and password.'))
  }

  return await response.json() as AuthLoginResponse<TUser>
}

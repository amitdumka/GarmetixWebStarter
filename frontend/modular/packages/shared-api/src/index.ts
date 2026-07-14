import { stripServerUrl } from '@garmetix/shared-utils'

const STATUS_FALLBACK_MESSAGES: Record<number, string> = {
  400: 'The request was invalid. Please check the details and try again.',
  401: 'Your session has expired. Please sign in again.',
  403: "You don't have permission to do that.",
  404: 'That was not found. It may have been moved or deleted.',
  405: 'This action is not available right now. Please try again in a moment.',
  408: 'The request timed out. Please try again.',
  409: 'This conflicts with existing data. Please refresh and try again.',
  429: 'Too many requests. Please wait a moment and try again.',
  500: 'The server hit an unexpected error. Please try again in a moment.',
  502: 'The server is temporarily unavailable. Please try again in a moment.',
  503: 'The server is temporarily unavailable. Please try again in a moment.',
  504: 'The server took too long to respond. Please try again in a moment.'
}

function statusFallbackMessage(status: number) {
  return STATUS_FALLBACK_MESSAGES[status] || `Request failed with status ${status}. Please try again.`
}

/**
 * Backend errors normally arrive as JSON ({ error }, { message }, or ASP.NET
 * ProblemDetails' { title }/{ detail }) - those are already written for end users, so
 * surface them as-is. Anything else (an infra layer like nginx/Cloudflare returning its
 * own HTML error page, an empty body, or unparseable text) falls back to a clean,
 * status-code-based message instead of dumping raw markup into the UI.
 */
function parseErrorMessage(status: number, rawBody: string, fallback?: string) {
  const trimmed = rawBody.trim()
  const defaultMessage = fallback || statusFallbackMessage(status)
  if (!trimmed) return defaultMessage

  try {
    const parsed = JSON.parse(trimmed) as Record<string, unknown>
    const candidate = parsed.error ?? parsed.message ?? parsed.detail ?? parsed.title
    if (typeof candidate === 'string' && candidate.trim()) return stripServerUrl(candidate)
  } catch {
    // Not JSON - fall through to the HTML/plain-text handling below.
  }

  const looksLikeMarkup = /^<(!doctype|html)/i.test(trimmed) || /<\/?(html|body|head)\b/i.test(trimmed)
  if (looksLikeMarkup) return defaultMessage

  return stripServerUrl(trimmed)
}

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

export function normalizeApiBaseUrl(baseUrl: string) {
  const rawBase = String(baseUrl || '').trim().replace(/\/+$/, '')
  if (!rawBase || typeof window === 'undefined') return rawBase

  try {
    const currentUrl = window.location
    const parsed = new URL(rawBase, currentUrl.origin)
    const sameHost = parsed.hostname === currentUrl.hostname
    const localHost = ['localhost', '127.0.0.1', '::1'].includes(parsed.hostname)

    if (currentUrl.protocol === 'https:' && parsed.protocol === 'http:' && (sameHost || localHost)) {
      return `${currentUrl.origin}${parsed.pathname.replace(/\/+$/, '')}`
    }

    return parsed.toString().replace(/\/+$/, '')
  } catch {
    return rawBase
  }
}

export function createApiUrl(baseUrl: string, path: string) {
  const base = normalizeApiBaseUrl(baseUrl)
  let nextPath = String(path || '').replace(/^\/+/, '')
  if (base.replace(/\/+$/, '').toLowerCase().endsWith('/api')) {
    nextPath = nextPath.replace(/^api\/+/i, '')
  }
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
  const baseUrl = normalizeApiBaseUrl(options.baseUrl)

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
      throw new Error(parseErrorMessage(response.status, await response.text()))
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
    throw new Error(parseErrorMessage(response.status, await response.text(), 'Login failed. Check the username and password.'))
  }

  return await response.json() as AuthLoginResponse<TUser>
}

export interface GarmetixApiClientOptions {
  baseUrl: string
  getToken?: () => string | null | undefined
}

export interface ApiRequestOptions extends RequestInit {
  query?: Record<string, string | number | boolean | null | undefined>
}

export function createGarmetixApiClient(options: GarmetixApiClientOptions) {
  const baseUrl = options.baseUrl.replace(/\/$/, '')

  async function request<T>(path: string, requestOptions: ApiRequestOptions = {}): Promise<T> {
    const url = new URL(`${baseUrl}/${path.replace(/^\//, '')}`)
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
      throw new Error(message || `Garmetix API request failed with ${response.status}`)
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


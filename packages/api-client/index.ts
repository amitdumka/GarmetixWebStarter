export type ApiClientConfig = {
  baseUrl: string
  getToken?: () => string | null | undefined
}

export type ApiRequestOptions<TBody = unknown> = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  path: string
  body?: TBody
}

export function createApiUrl(baseUrl: string, path: string) {
  const base = String(baseUrl || '').replace(/\/+$/, '')
  const nextPath = String(path || '').replace(/^\/+/, '')
  return nextPath ? `${base}/${nextPath}` : base
}


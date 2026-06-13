export type GarmetixEntity = {
  id?: string
  [key: string]: unknown
}

const responseCache = new Map<string, { expiresAt: number, value: unknown }>()
const pendingRequests = new Map<string, Promise<unknown>>()
const maxCacheEntries = 200
const longCacheResources = new Set([
  'companies',
  'stores',
  'store-groups',
  'workspace/options',
  'product-categories',
  'product-sub-categories',
  'brands',
  'taxes',
  'ledgers',
  'parties',
  'employees',
  'bank-accounts'
])

export function useGarmetixApi() {
  const config = useRuntimeConfig()
  const base = config.public.apiBase

  function authHeaders() {
    if (!import.meta.client) {
      return {}
    }

    const auth = useAuth()
    auth.restore()
    const sessionToken = auth.token.value || localStorage.getItem('garmetix.token')
    return sessionToken ? { Authorization: `Bearer ${sessionToken}` } : {}
  }

  async function list<T>(resource: string) {
    return await request<T[]>(resource, 'GET', undefined, true)
  }

  async function get<T>(resource: string) {
    return await request<T>(resource, 'GET', undefined, !resource.includes('/prepare'))
  }

  async function create<T extends GarmetixEntity>(resource: string, body: T) {
    const result = await request<T>(resource, 'POST', sanitizeCreateBody(body))
    clearCache()
    return result
  }

  async function update<T extends GarmetixEntity>(resource: string, id: string, body: T) {
    const result = await request<T>(`${resource}/${id}`, 'PUT', body)
    clearCache()
    return result
  }

  async function remove(resource: string, id: string) {
    const result = await request(`${resource}/${id}`, 'DELETE')
    clearCache()
    return result
  }

  function sanitizeCreateBody<T extends GarmetixEntity>(body: T) {
    if (!body || body.id) {
      return body
    }

    const { id, ...rest } = body
    return rest
  }

  async function request<T>(resource: string, method: string, body?: unknown, cacheable = false) {
    const path = `${base}/${resource}`
    const headers = authHeaders() as Record<string, string>
    const cacheKey = cacheable ? `${headers.Authorization || 'anonymous'}|${path}` : ''
    if (cacheKey) {
      pruneCache()
      const cached = responseCache.get(cacheKey)
      if (cached && cached.expiresAt > Date.now()) {
        return clone(cached.value) as T
      }

      const pending = pendingRequests.get(cacheKey)
      if (pending) {
        return clone(await pending) as T
      }
    }

    try {
      const requestPromise = $fetch<T>(path, {
        method,
        headers,
        body
      })
      if (cacheKey) {
        pendingRequests.set(cacheKey, requestPromise)
      }

      const result = await requestPromise
      if (cacheKey) {
        if (responseCache.size >= maxCacheEntries) {
          const oldestKey = responseCache.keys().next().value
          if (oldestKey) responseCache.delete(oldestKey)
        }
        responseCache.set(cacheKey, {
          expiresAt: Date.now() + cacheDuration(resource),
          value: clone(result)
        })
      }
      return result
    } catch (error: any) {
      error.garmetixRequest = {
        method,
        resource,
        path,
        body: summarizeBody(body)
      }

      if (error?.status === 401 || error?.statusCode === 401 || error?.response?.status === 401) {
        useAuth().handleUnauthorized(true)
      }

      throw error
    } finally {
      if (cacheKey) {
        pendingRequests.delete(cacheKey)
      }
    }
  }

  function cacheDuration(resource: string) {
    const root = resource.split('?')[0]
    return longCacheResources.has(root) ? 5 * 60 * 1000 : 20 * 1000
  }

  function clearCache() {
    responseCache.clear()
    pendingRequests.clear()
  }

  function pruneCache() {
    const now = Date.now()
    for (const [key, entry] of responseCache) {
      if (entry.expiresAt <= now) responseCache.delete(key)
    }
  }

  function clone<T>(value: T): T {
    return typeof structuredClone === 'function' ? structuredClone(value) : JSON.parse(JSON.stringify(value))
  }

  function summarizeBody(body?: unknown) {
    if (!body || typeof body !== 'object') {
      return body
    }

    const record = body as Record<string, unknown>
    const summary: Record<string, unknown> = {}
    for (const key of Object.keys(record).slice(0, 20)) {
      summary[key] = key.toLowerCase().includes('password') ? '[hidden]' : record[key]
    }

    return summary
  }

  return { list, get, create, update, remove, authHeaders, clearCache }
}

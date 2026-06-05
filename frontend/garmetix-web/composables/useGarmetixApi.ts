export type GarmetixEntity = {
  id?: string
  [key: string]: unknown
}

export function useGarmetixApi() {
  const config = useRuntimeConfig()
  const base = config.public.apiBase

  function authHeaders() {
    if (!import.meta.client) {
      return {}
    }

    const token = localStorage.getItem('garmetix.token')
    return token ? { Authorization: `Bearer ${token}` } : {}
  }

  async function list<T>(resource: string) {
    return await request<T[]>(resource, 'GET')
  }

  async function get<T>(resource: string) {
    return await request<T>(resource, 'GET')
  }

  async function create<T extends GarmetixEntity>(resource: string, body: T) {
    return await request<T>(resource, 'POST', sanitizeCreateBody(body))
  }

  async function update<T extends GarmetixEntity>(resource: string, id: string, body: T) {
    return await request<T>(`${resource}/${id}`, 'PUT', body)
  }

  async function remove(resource: string, id: string) {
    return await request(`${resource}/${id}`, 'DELETE')
  }

  function sanitizeCreateBody<T extends GarmetixEntity>(body: T) {
    if (!body || body.id) {
      return body
    }

    const { id, ...rest } = body
    return rest
  }

  async function request<T>(resource: string, method: string, body?: unknown) {
    const path = `${base}/${resource}`

    try {
      return await $fetch<T>(path, {
        method,
        headers: authHeaders(),
        body
      })
    } catch (error: any) {
      error.garmetixRequest = {
        method,
        resource,
        path,
        body: summarizeBody(body)
      }
      throw error
    }
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

  return { list, get, create, update, remove, authHeaders }
}

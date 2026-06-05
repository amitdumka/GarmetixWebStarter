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
    return await $fetch<T[]>(`${base}/${resource}`, {
      headers: authHeaders()
    })
  }

  async function get<T>(resource: string) {
    return await $fetch<T>(`${base}/${resource}`, {
      headers: authHeaders()
    })
  }

  async function create<T extends GarmetixEntity>(resource: string, body: T) {
    return await $fetch<T>(`${base}/${resource}`, {
      method: 'POST',
      headers: authHeaders(),
      body: sanitizeCreateBody(body)
    })
  }

  async function update<T extends GarmetixEntity>(resource: string, id: string, body: T) {
    return await $fetch<T>(`${base}/${resource}/${id}`, {
      method: 'PUT',
      headers: authHeaders(),
      body
    })
  }

  async function remove(resource: string, id: string) {
    return await $fetch(`${base}/${resource}/${id}`, {
      method: 'DELETE',
      headers: authHeaders()
    })
  }

  function sanitizeCreateBody<T extends GarmetixEntity>(body: T) {
    if (!body || body.id) {
      return body
    }

    const { id, ...rest } = body
    return rest
  }

  return { list, get, create, update, remove, authHeaders }
}

import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { stripServerUrl } from '@garmetix/shared-utils'

export type ApiRecord = Record<string, unknown>

export function useMainApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function createClient() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    return await createClient().get<T>(path, { query })
  }

  async function post<T>(path: string, body?: unknown) {
    return await createClient().post<T>(path, body)
  }

  async function put<T>(path: string, body?: unknown) {
    return await createClient().put<T>(path, body)
  }

  async function del<T>(path: string) {
    return await createClient().delete<T>(path)
  }

  function apiUrl(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost'
    const url = new URL(createApiUrl(apiBaseUrl.value, path), origin)
    for (const [key, value] of Object.entries(query ?? {})) {
      if (value !== null && value !== undefined) url.searchParams.set(key, String(value))
    }
    return url.toString()
  }

  async function postForm<T>(path: string, form: FormData) {
    const token = getStoredToken(window.localStorage)
    const headers = new Headers()
    if (token) headers.set('Authorization', `Bearer ${token}`)

    const response = await fetch(apiUrl(path), { method: 'POST', headers, body: form })
    const text = await response.text()
    if (!response.ok) {
      throw new Error(stripServerUrl(text || `Upload failed with ${response.status}`))
    }
    return (text ? JSON.parse(text) : null) as T
  }

  async function getText(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    const token = getStoredToken(window.localStorage)
    const headers = new Headers()
    if (token) headers.set('Authorization', `Bearer ${token}`)

    const response = await fetch(apiUrl(path, query), { method: 'GET', headers })
    const text = await response.text()
    if (!response.ok) throw new Error(stripServerUrl(text || `Request failed with ${response.status}`))
    return text
  }

  async function openBlob(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    const token = getStoredToken(window.localStorage)
    const headers = new Headers()
    if (token) headers.set('Authorization', `Bearer ${token}`)

    const response = await fetch(apiUrl(path, query), { method: 'GET', headers })
    if (!response.ok) {
      const message = await response.text()
      throw new Error(stripServerUrl(message || `Request failed with ${response.status}`))
    }

    const blob = await response.blob()
    const objectUrl = URL.createObjectURL(blob)
    window.open(objectUrl, '_blank')
    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
  }

  async function download(path: string, query?: Record<string, string | number | boolean | null | undefined>, fallbackFileName = 'garmetix-document.pdf', body?: unknown) {
    const token = getStoredToken(window.localStorage)
    const headers = new Headers()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    if (body !== undefined) headers.set('Content-Type', 'application/json')

    const response = await fetch(apiUrl(path, query), {
      method: body !== undefined ? 'POST' : 'GET',
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined
    })
    if (!response.ok) {
      const message = await response.text()
      throw new Error(stripServerUrl(message || `Download failed with ${response.status}`))
    }

    const blob = await response.blob()
    const disposition = response.headers.get('content-disposition') ?? ''
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
    const fileName = match?.[1] ? decodeURIComponent(match[1]) : fallbackFileName
    const objectUrl = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = objectUrl
    anchor.download = fileName
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(objectUrl)
  }

  return { apiBaseUrl, apiUrl, del, download, get, getText, openBlob, post, postForm, put }
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

export function readArray(source: ApiRecord | null | undefined, keys: string[] | null | undefined) {
  for (const key of keys ?? []) {
    const value = source?.[key]
    if (Array.isArray(value)) return value as ApiRecord[]
  }
  return []
}

export function toRows(value: unknown, keys: string[] = ['items', 'rows', 'data', 'results']) {
  if (Array.isArray(value)) return value as ApiRecord[]
  if (value && typeof value === 'object') {
    return readArray(value as ApiRecord, keys)
  }
  return []
}

export function formatDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  }).format(date)
}

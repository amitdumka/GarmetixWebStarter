import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { stripServerUrl } from '@garmetix/shared-utils'

export type ApiRecord = Record<string, unknown>

export function useBooksApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function normalizeBooksApiPath(path: string) {
    return String(path || '').replace(/^\/+/, '').replace(/^api\/+/i, '')
  }

  function createClient() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    const api = createClient()
    return await api.get<T>(normalizeBooksApiPath(path), { query })
  }

  async function post<T>(path: string, body?: unknown) {
    const api = createClient()
    return await api.post<T>(normalizeBooksApiPath(path), body)
  }

  async function put<T>(path: string, body?: unknown) {
    const api = createClient()
    return await api.put<T>(normalizeBooksApiPath(path), body)
  }

  async function del<T>(path: string) {
    const api = createClient()
    return await api.delete<T>(normalizeBooksApiPath(path))
  }

  function apiUrl(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost'
    const url = new URL(createApiUrl(apiBaseUrl.value, normalizeBooksApiPath(path)), origin)
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

  return { apiBaseUrl, apiUrl, del, download, get, openBlob, post, postForm, put }
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

export function toRows(value: unknown): ApiRecord[] {
  if (Array.isArray(value)) return value as ApiRecord[]
  if (value && typeof value === 'object') {
    return readArray(value as ApiRecord, ['items', 'rows', 'data', 'results'])
  }
  return []
}

export const pageSizeOptions = [
  { label: '25 / page', value: 25 },
  { label: '50 / page', value: 50 },
  { label: '100 / page', value: 100 }
] as const

export function paginateRows<T>(rows: T[], page: number, pageSize: number): T[] {
  const size = Math.max(1, Number(pageSize) || 25)
  const start = Math.max(0, (Math.max(1, Number(page) || 1) - 1) * size)
  return rows.slice(start, start + size)
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

export function optionLabel(options: ReadonlyArray<{ value: number, label: string }>, value: unknown, fallback = '-') {
  if (typeof value === 'string' && value.trim() !== '' && Number.isNaN(Number(value))) return value
  const numericValue = typeof value === 'number' ? value : Number(value)
  return options.find(item => item.value === numericValue)?.label ?? fallback
}

export const ledgerTypeOptions = [
  { value: 0, label: 'Asset' },
  { value: 1, label: 'Cash' },
  { value: 2, label: 'Bank Account' },
  { value: 3, label: 'Loan' },
  { value: 4, label: 'Expenses' },
  { value: 7, label: 'Income' },
  { value: 11, label: 'Sale' },
  { value: 12, label: 'Stock Item' },
  { value: 15, label: 'Capital Account' },
  { value: 16, label: 'Current Asset' },
  { value: 18, label: 'Current Liability' },
  { value: 20, label: 'Sundry Debtor' },
  { value: 21, label: 'Sundry Creditor' },
  { value: 23, label: 'Suspense' }
] as const

export const partyTypeOptions = [
  { value: 0, label: 'Customer' },
  { value: 1, label: 'Supplier' },
  { value: 2, label: 'Employee' },
  { value: 3, label: 'Vendor' },
  { value: 4, label: 'Debtor' },
  { value: 5, label: 'Creditor' },
  { value: 6, label: 'Others' }
] as const

export const accountTypeOptions = [
  { value: 0, label: 'Saving' },
  { value: 1, label: 'Current' },
  { value: 2, label: 'Cash Credit' },
  { value: 3, label: 'Over Draft' },
  { value: 4, label: 'Others' },
  { value: 5, label: 'Loan' },
  { value: 6, label: 'CF' }
] as const

export const voucherTypeOptions = [
  { value: 0, label: 'Payment' },
  { value: 1, label: 'Receipt' },
  { value: 2, label: 'Expense' }
] as const

export const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 3, label: 'Wallets' },
  { value: 4, label: 'IMPS' },
  { value: 5, label: 'RTGS' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' },
  { value: 8, label: 'Demand Draft' },
  { value: 9, label: 'Credit Note' },
  { value: 10, label: 'Debit Note' },
  { value: 11, label: 'Coupons' },
  { value: 12, label: 'Mix Payments' },
  { value: 13, label: 'Sale Return' },
  { value: 14, label: 'Others' },
  { value: 15, label: 'Credit Balance' }
] as const

export const transactionTypeOptions = [
  { value: 0, label: 'Deposit' },
  { value: 1, label: 'Withdraw' }
] as const

export const transactionModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Cheque' },
  { value: 2, label: 'NEFT' },
  { value: 3, label: 'RTGS' },
  { value: 4, label: 'UPI' },
  { value: 5, label: 'Net Banking' },
  { value: 6, label: 'IMPS' },
  { value: 7, label: 'DD' },
  { value: 8, label: 'ATM' },
  { value: 9, label: 'Swipe' },
  { value: 10, label: 'Other' }
] as const

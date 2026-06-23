import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'

export type ApiRecord = Record<string, unknown>

export function useBooksApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  async function get<T>(path: string, query?: Record<string, string | number | boolean | null | undefined>) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const api = createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
    return await api.get<T>(path, { query })
  }

  return { apiBaseUrl, get }
}

export function readNumber(source: ApiRecord | null | undefined, keys: string[]) {
  for (const key of keys) {
    const value = source?.[key]
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))) return Number(value)
  }
  return 0
}

export function readText(source: ApiRecord | null | undefined, keys: string[], fallback = '-') {
  for (const key of keys) {
    const value = source?.[key]
    if (value !== null && value !== undefined && String(value).trim() !== '') return String(value)
  }
  return fallback
}

export function readArray(source: ApiRecord | null | undefined, keys: string[]) {
  for (const key of keys) {
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

import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'

export interface SwalekhaHealth {
  ok: boolean
  database: string
  ownerName: string
  generatedAtUtc: string
}

export type SwalekhaAccountType = 'Bank' | 'Cash' | 'CreditCard'
export type SwalekhaTransactionType = 'Deposit' | 'Withdrawal' | 'TransferIn' | 'TransferOut'

export interface SwalekhaAccount {
  id: string
  name: string
  accountType: SwalekhaAccountType
  bankName?: string | null
  accountNumberMasked?: string | null
  ifsc?: string | null
  creditLimit?: number | null
  statementDayOfMonth?: number | null
  dueDayOfMonth?: number | null
  openingBalance: number
  currentBalance: number
  currency: string
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaAccountPayload {
  name: string
  accountType: SwalekhaAccountType
  bankName?: string | null
  accountNumberMasked?: string | null
  ifsc?: string | null
  creditLimit?: number | null
  statementDayOfMonth?: number | null
  dueDayOfMonth?: number | null
  openingBalance: number
  currency?: string | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaTransaction {
  id: string
  accountId: string
  transactionType: SwalekhaTransactionType
  amount: number
  transactionDate: string
  narration: string
  counterAccountId?: string | null
  counterAccountName?: string | null
  runningBalance: number
  transferGroupId?: string | null
  createdAt: string
}

export interface SwalekhaTransactionList {
  page: number
  pageSize: number
  totalCount: number
  rows: SwalekhaTransaction[]
}

export interface SwalekhaTransactionPayload {
  amount: number
  transactionDate: string
  narration: string
  transactionType: 'Deposit' | 'Withdrawal'
}

export interface SwalekhaTransferPayload {
  fromAccountId: string
  toAccountId: string
  amount: number
  transactionDate: string
  narration?: string | null
}

export interface SwalekhaTransferResult {
  fromAccount: SwalekhaAccount
  toAccount: SwalekhaAccount
}

export type SwalekhaPersonLedgerEntryType = 'LoanGiven' | 'LoanTaken' | 'RepaymentReceived' | 'RepaymentPaid'

export interface SwalekhaContact {
  id: string
  name: string
  phone?: string | null
  email?: string | null
  relationship?: string | null
  balance: number
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaContactPayload {
  name: string
  phone?: string | null
  email?: string | null
  relationship?: string | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaPersonLedgerEntry {
  id: string
  contactId: string
  entryType: SwalekhaPersonLedgerEntryType
  amount: number
  entryDate: string
  narration: string
  runningBalance: number
  createdAt: string
}

export interface SwalekhaPersonLedgerList {
  page: number
  pageSize: number
  totalCount: number
  rows: SwalekhaPersonLedgerEntry[]
}

export interface SwalekhaPersonLedgerEntryPayload {
  amount: number
  entryDate: string
  narration: string
  entryType: SwalekhaPersonLedgerEntryType
}

export interface SwalekhaExpenseSheet {
  id: string
  name: string
  sheetType: string
  budget?: number | null
  isActive: boolean
  notes?: string | null
  spentTotal: number
  createdAt: string
}

export interface SwalekhaExpenseSheetPayload {
  name: string
  sheetType: string
  budget?: number | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaExpenseEntry {
  id: string
  sheetId: string
  category: string
  amount: number
  entryDate: string
  narration: string
  isHidden: boolean
  createdAt: string
}

export interface SwalekhaExpenseEntryPayload {
  category: string
  amount: number
  entryDate: string
  narration: string
  isHidden: boolean
}

export interface SwalekhaExpenseEntryList {
  page: number
  pageSize: number
  totalCount: number
  rows: SwalekhaExpenseEntry[]
}

export interface SwalekhaExpenseSummaryRow {
  key: string
  total: number
  count: number
}

export interface SwalekhaExpenseSummary {
  totalVisible: number
  totalHidden: number
  bySheetType: SwalekhaExpenseSummaryRow[]
  byCategory: SwalekhaExpenseSummaryRow[]
}

export interface SwalekhaIncomeEntry {
  id: string
  source: string
  amount: number
  entryDate: string
  narration: string
  createdAt: string
}

export interface SwalekhaIncomeEntryPayload {
  source: string
  amount: number
  entryDate: string
  narration: string
}

export interface SwalekhaIncomeList {
  page: number
  pageSize: number
  totalCount: number
  totalAmount: number
  rows: SwalekhaIncomeEntry[]
}

export interface SwalekhaRecurringBill {
  id: string
  name: string
  amount: number
  dueDayOfMonth: number
  category?: string | null
  isActive: boolean
  notes?: string | null
  lastPaidDate?: string | null
  dueThisMonth: boolean
}

export interface SwalekhaRecurringBillPayload {
  name: string
  amount: number
  dueDayOfMonth: number
  category?: string | null
  isActive: boolean
  notes?: string | null
}

export function useSwalekhaApiClient() {
  const runtimeConfig = useRuntimeConfig()
  const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))

  function client() {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    return createGarmetixApiClient({
      baseUrl: apiBaseUrl.value,
      getToken: () => getStoredToken(window.localStorage)
    })
  }

  async function get<T>(path: string) {
    return await client().get<T>(normalizeSwalekhaPath(path))
  }

  async function post<T>(path: string, body?: unknown) {
    return await client().post<T>(normalizeSwalekhaPath(path), body)
  }

  async function put<T>(path: string, body?: unknown) {
    return await client().put<T>(normalizeSwalekhaPath(path), body)
  }

  async function del<T = void>(path: string) {
    return await client().delete<T>(normalizeSwalekhaPath(path))
  }

  return { apiBaseUrl, get, post, put, del }
}

function normalizeSwalekhaPath(path: string) {
  const clean = String(path || '').replace(/^\/+/, '')
  return clean.startsWith('swalekha/') ? clean : `swalekha/${clean}`
}

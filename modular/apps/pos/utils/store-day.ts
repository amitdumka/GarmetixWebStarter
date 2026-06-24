export interface CashDetail {
  amount: number | null
  n2000: number
  n500: number
  n200: number
  n100: number
  n50: number
  nC20: number
  nC10: number
  nC5: number
  nC2: number
  nC1: number
}

export interface PettyCashDraft {
  openingBalance: number
  sales: number
  receipts: number
  dueReceipts: number
  bankWithdrawal: number
  expenses: number
  payments: number
  customerDue: number
  bankDeposit: number
  nonCashSale: number
}

export const cashDenominations = [
  { key: 'n2000', label: '2000', value: 2000 },
  { key: 'n500', label: '500', value: 500 },
  { key: 'n200', label: '200', value: 200 },
  { key: 'n100', label: '100', value: 100 },
  { key: 'n50', label: '50', value: 50 },
  { key: 'nC20', label: '20', value: 20 },
  { key: 'nC10', label: '10', value: 10 },
  { key: 'nC5', label: '5', value: 5 },
  { key: 'nC2', label: '2', value: 2 },
  { key: 'nC1', label: '1', value: 1 }
] as const

export function cashBlank(): CashDetail {
  return {
    amount: null,
    n2000: 0,
    n500: 0,
    n200: 0,
    n100: 0,
    n50: 0,
    nC20: 0,
    nC10: 0,
    nC5: 0,
    nC2: 0,
    nC1: 0
  }
}

export function pettyCashBlank(): PettyCashDraft {
  return {
    openingBalance: 0,
    sales: 0,
    receipts: 0,
    dueReceipts: 0,
    bankWithdrawal: 0,
    expenses: 0,
    payments: 0,
    customerDue: 0,
    bankDeposit: 0,
    nonCashSale: 0
  }
}

export function calculateCash(source: CashDetail) {
  return cashDenominations.reduce((sum, note) => sum + Number(source[note.key] || 0) * note.value, 0)
}

export function toCashPayload(source: CashDetail, fallbackAmount?: number | null) {
  const calculated = calculateCash(source)
  return {
    amount: source.amount ?? fallbackAmount ?? calculated,
    n2000: Number(source.n2000 || 0),
    n500: Number(source.n500 || 0),
    n200: Number(source.n200 || 0),
    n100: Number(source.n100 || 0),
    n50: Number(source.n50 || 0),
    nC20: Number(source.nC20 || 0),
    nC10: Number(source.nC10 || 0),
    nC5: Number(source.nC5 || 0),
    nC2: Number(source.nC2 || 0),
    nC1: Number(source.nC1 || 0)
  }
}

export function localDateValue(date = new Date()) {
  const offset = date.getTimezoneOffset()
  const local = new Date(date.getTime() - offset * 60000)
  return local.toISOString().slice(0, 10)
}

export function calculatePettyCashBookCash(source: PettyCashDraft) {
  return Number(source.openingBalance || 0)
    + Number(source.sales || 0)
    + Number(source.receipts || 0)
    + Number(source.dueReceipts || 0)
    + Number(source.bankWithdrawal || 0)
    - Number(source.expenses || 0)
    - Number(source.payments || 0)
    - Number(source.customerDue || 0)
    - Number(source.bankDeposit || 0)
    - Number(source.nonCashSale || 0)
}

export function copyBookSummaryToPettyCashDraft(target: PettyCashDraft, summary: any) {
  Object.assign(target, {
    openingBalance: Number(summary?.openingBalance || 0),
    sales: Number(summary?.sales || 0),
    receipts: Number(summary?.receipts || 0),
    dueReceipts: Number(summary?.dueReceipts || 0),
    bankWithdrawal: Number(summary?.bankWithdrawal || 0),
    expenses: Number(summary?.expenses || 0),
    payments: Number(summary?.payments || 0),
    customerDue: Number(summary?.customerDue || 0),
    bankDeposit: Number(summary?.bankDeposit || 0),
    nonCashSale: Number(summary?.nonCashSale || 0)
  })
}

export function extractStoreDayStatus(response: any) {
  return response?.status && typeof response.status === 'object'
    ? response.status
    : response
}

export function extractPettyCashSheetId(response: any, fallbackStatus?: any) {
  return String(response?.pettyCashSheetId || response?.status?.pettyCashSheetId || fallbackStatus?.pettyCashSheetId || '')
}

export function parseStoreDayErrorPayload(error: unknown) {
  try {
    const message = error instanceof Error ? error.message : ''
    const start = message.indexOf('{')
    if (start >= 0) return JSON.parse(message.slice(start))
  } catch {
    return null
  }
  return null
}

export interface PosSaleDraft {
  form?: Record<string, unknown>
  cart?: unknown[]
  payments?: unknown[]
  adjustments?: Record<string, unknown>
}

export interface PosHeldBill {
  id: string
  heldAt: string
  customerName: string
  customerMobileNumber: string
  itemCount: number
  quantity: number
  payableTotal: number
  note?: string
  draft: PosSaleDraft
}

export interface PosPrintQueueItem {
  invoiceId: string
  invoiceNumber: string
  customerName: string
  billAmount: number
  savedAt: string
  printedAt?: string
}

export const posStorageKeys = {
  saleDraft: 'garmetix.pos.sale.draft.v1',
  printQueue: 'garmetix.pos.print.queue.v1',
  heldBills: 'garmetix.pos.held-bills.v1'
} as const

const heldBillLimit = 100
const printQueueLimit = 50

export function createLocalPosId(prefix: string) {
  return globalThis.crypto?.randomUUID?.() || `${prefix}-${Date.now()}`
}

export function readSaleDraft(): PosSaleDraft | null {
  return readStorageObject<PosSaleDraft>(posStorageKeys.saleDraft)
}

export function writeSaleDraft(draft: PosSaleDraft) {
  writeStorageObject(posStorageKeys.saleDraft, draft)
}

export function clearSaleDraft() {
  removeStorageValue(posStorageKeys.saleDraft)
}

export function readHeldBills() {
  return readStorageArray<PosHeldBill>(posStorageKeys.heldBills)
    .filter(item => Boolean(item.id && item.heldAt && item.draft))
    .slice(0, heldBillLimit)
}

export function writeHeldBills(rows: PosHeldBill[]) {
  writeStorageArray(posStorageKeys.heldBills, rows, heldBillLimit)
}

export function upsertHeldBill(item: PosHeldBill) {
  const rows = readHeldBills().filter(row => row.id !== item.id)
  rows.unshift(item)
  writeHeldBills(rows)
}

export function removeHeldBill(id: string) {
  writeHeldBills(readHeldBills().filter(item => item.id !== id))
}

export function clearHeldBills() {
  removeStorageValue(posStorageKeys.heldBills)
}

export function readPrintQueue() {
  return readStorageArray<PosPrintQueueItem>(posStorageKeys.printQueue)
    .filter(item => Boolean(item.invoiceId))
    .slice(0, printQueueLimit)
}

export function writePrintQueue(rows: PosPrintQueueItem[]) {
  writeStorageArray(posStorageKeys.printQueue, rows, printQueueLimit)
}

export function upsertPrintQueueItem(item: PosPrintQueueItem) {
  if (!item.invoiceId) return
  const rows = readPrintQueue().filter(row => row.invoiceId !== item.invoiceId)
  rows.unshift(item)
  writePrintQueue(rows)
}

export function clearPrintQueue() {
  removeStorageValue(posStorageKeys.printQueue)
}

function readStorageArray<T>(key: string): T[] {
  if (!canUseLocalStorage()) return []
  try {
    const rows = JSON.parse(window.localStorage.getItem(key) || '[]')
    if (Array.isArray(rows)) return rows as T[]
    removeStorageValue(key)
  } catch {
    removeStorageValue(key)
  }
  return []
}

function writeStorageArray<T>(key: string, rows: T[], limit: number) {
  if (!canUseLocalStorage()) return
  window.localStorage.setItem(key, JSON.stringify(rows.slice(0, limit)))
}

function readStorageObject<T>(key: string): T | null {
  if (!canUseLocalStorage()) return null
  try {
    const value = JSON.parse(window.localStorage.getItem(key) || 'null')
    if (value && typeof value === 'object' && !Array.isArray(value)) return value as T
    if (value !== null) removeStorageValue(key)
  } catch {
    removeStorageValue(key)
  }
  return null
}

function writeStorageObject<T>(key: string, value: T) {
  if (!canUseLocalStorage()) return
  window.localStorage.setItem(key, JSON.stringify(value))
}

function removeStorageValue(key: string) {
  if (canUseLocalStorage()) window.localStorage.removeItem(key)
}

function canUseLocalStorage() {
  return typeof window !== 'undefined' && Boolean(window.localStorage)
}

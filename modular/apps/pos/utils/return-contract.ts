export const salesReturnRequestKeys = [
  'refundAmount',
  'refundPaymentMode',
  'bankAccountId',
  'reason',
  'items'
] as const

export const salesReturnItemKeys = [
  'invoiceItemId',
  'quantity'
] as const

export const salesExchangeRequestKeys = [
  'additionalPaidAmount',
  'additionalPaymentMode',
  'bankAccountId',
  'reason',
  'returnItems',
  'newItems'
] as const

export const exchangeSaleItemKeys = [
  'productId',
  'barcode',
  'quantity',
  'mrp',
  'discountAmount'
] as const

export interface SalesReturnItemPayload {
  invoiceItemId: string
  quantity: number
}

export interface SalesReturnRequestPayload {
  refundAmount: number
  refundPaymentMode: number | null
  bankAccountId: string | null
  reason: string | null
  items: SalesReturnItemPayload[]
}

export interface CreateSalesReturnRequestInput {
  refundAmount: number
  refundPaymentMode: number | null
  bankAccountId: string | null
  reason: string
  items: SalesReturnItemPayload[]
}

export function createSalesReturnRequest(input: CreateSalesReturnRequestInput): SalesReturnRequestPayload {
  const refundAmount = Math.max(Number(input.refundAmount || 0), 0)

  return {
    refundAmount,
    refundPaymentMode: refundAmount > 0 ? input.refundPaymentMode : null,
    bankAccountId: refundAmount > 0 ? input.bankAccountId : null,
    reason: input.reason?.trim() || null,
    items: input.items
      .filter(item => Number(item.quantity || 0) > 0)
      .map(item => ({
        invoiceItemId: item.invoiceItemId,
        quantity: Number(item.quantity || 0)
      }))
  }
}

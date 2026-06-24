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

export interface ExchangeSaleItemPayload {
  productId: string
  barcode: string
  quantity: number
  mrp: number
  discountAmount: number
}

export interface SalesExchangeRequestPayload {
  additionalPaidAmount: number
  additionalPaymentMode: number | null
  bankAccountId: string | null
  reason: string | null
  returnItems: SalesReturnItemPayload[]
  newItems: ExchangeSaleItemPayload[]
}

export interface CreateSalesReturnRequestInput {
  refundAmount: number
  refundPaymentMode: number | null
  bankAccountId: string | null
  reason: string
  items: SalesReturnItemPayload[]
}

export interface CreateSalesExchangeRequestInput {
  additionalPaidAmount: number
  additionalPaymentMode: number | null
  bankAccountId: string | null
  reason: string
  returnItems: SalesReturnItemPayload[]
  newItems: ExchangeSaleItemPayload[]
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

export function createSalesExchangeRequest(input: CreateSalesExchangeRequestInput): SalesExchangeRequestPayload {
  const additionalPaidAmount = Math.max(Number(input.additionalPaidAmount || 0), 0)

  return {
    additionalPaidAmount,
    additionalPaymentMode: additionalPaidAmount > 0 ? input.additionalPaymentMode : null,
    bankAccountId: additionalPaidAmount > 0 ? input.bankAccountId : null,
    reason: input.reason?.trim() || null,
    returnItems: input.returnItems
      .filter(item => Number(item.quantity || 0) > 0)
      .map(item => ({
        invoiceItemId: item.invoiceItemId,
        quantity: Number(item.quantity || 0)
      })),
    newItems: input.newItems
      .filter(item => Number(item.quantity || 0) > 0)
      .map(item => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: Number(item.quantity || 0),
        mrp: Number(item.mrp || 0),
        discountAmount: Number(item.discountAmount || 0)
      }))
  }
}

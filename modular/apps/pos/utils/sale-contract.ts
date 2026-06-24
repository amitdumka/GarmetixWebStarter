export const posSaleRequestKeys = [
  'companyId',
  'storeGroupId',
  'storeId',
  'customerName',
  'customerMobileNumber',
  'customerGstin',
  'paymentMode',
  'bankAccountId',
  'paidAmount',
  'billDiscountAmount',
  'items',
  'customerId',
  'salesmanId',
  'payments'
] as const

export const posSaleItemKeys = [
  'productId',
  'barcode',
  'quantity',
  'mrp',
  'discountAmount'
] as const

export const posSalePaymentKeys = [
  'paymentMode',
  'amount',
  'bankAccountId',
  'referenceNumber',
  'gatewayReference',
  'settlementStatus',
  'adjustmentSourceType',
  'adjustmentSourceId',
  'cardLastFour',
  'cardAuthorizationCode',
  'cardNetwork',
  'upiVpa',
  'walletProvider',
  'bankReferenceNumber',
  'chequeNumber',
  'chequeDate',
  'drawerBankName',
  'accountReference'
] as const

export interface PosSaleItemPayload {
  productId: string
  barcode: string
  quantity: number
  mrp: number
  discountAmount: number
}

export interface PosSalePaymentPayload {
  paymentMode: number
  amount: number
  bankAccountId: string | null
  referenceNumber: string | null
  gatewayReference: string | null
  settlementStatus: string | null
  adjustmentSourceType: string | null
  adjustmentSourceId: string | null
  cardLastFour?: string | null
  cardAuthorizationCode?: string | null
  cardNetwork?: string | null
  upiVpa?: string | null
  walletProvider?: string | null
  bankReferenceNumber?: string | null
  chequeNumber?: string | null
  chequeDate?: string | null
  drawerBankName?: string | null
  accountReference?: string | null
}

export interface PosSaleRequestPayload {
  companyId: string
  storeGroupId: string
  storeId: string
  customerName: string
  customerMobileNumber: string
  customerGstin: string
  paymentMode: number
  bankAccountId: string | null
  paidAmount: number
  billDiscountAmount: number
  items: PosSaleItemPayload[]
  customerId: string | null
  salesmanId: string | null
  payments: PosSalePaymentPayload[]
}

export interface CreatePosSaleRequestInput {
  companyId: string
  storeGroupId: string
  storeId: string
  customerId: string | null
  salesmanId: string | null
  customerName: string
  customerMobileNumber: string
  customerGstin: string
  fallbackPaymentMode: number
  mixedPaymentMode: number
  billDiscountAmount: number
  payments: PosSalePaymentPayload[]
  items: PosSaleItemPayload[]
}

export function createPosSaleRequest(input: CreatePosSaleRequestInput): PosSaleRequestPayload {
  const paidAmount = input.payments.reduce((sum, item) => sum + Number(item.amount || 0), 0)
  const bankAccountId = input.payments.find(item => item.bankAccountId)?.bankAccountId || null

  return {
    companyId: input.companyId,
    storeGroupId: input.storeGroupId,
    storeId: input.storeId,
    customerName: input.customerName || 'Walk-in Customer',
    customerMobileNumber: input.customerMobileNumber || '',
    customerGstin: input.customerGstin || '',
    paymentMode: input.payments.length > 1 ? input.mixedPaymentMode : Number(input.payments[0]?.paymentMode ?? input.fallbackPaymentMode),
    bankAccountId,
    paidAmount,
    billDiscountAmount: Number(input.billDiscountAmount || 0),
    items: input.items,
    customerId: input.customerId || null,
    salesmanId: input.salesmanId || null,
    payments: input.payments
  }
}

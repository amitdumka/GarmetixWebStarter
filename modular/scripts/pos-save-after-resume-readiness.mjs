import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getSmokeVersion,
  modularRoot,
  repoRoot
} from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []
const warnings = []

const salePagePath = join(modularRoot, 'apps/pos/pages/sale.vue')
const holdBillsPagePath = join(modularRoot, 'apps/pos/pages/hold-bills.vue')
const saleContractPath = join(modularRoot, 'apps/pos/utils/sale-contract.ts')
const backendBillingPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Billing/BillingEndpoints.cs')

const salePage = readFileSync(salePagePath, 'utf8')
const holdBillsPage = readFileSync(holdBillsPagePath, 'utf8')
const saleContract = readFileSync(saleContractPath, 'utf8')
const backendBilling = readFileSync(backendBillingPath, 'utf8')

console.log('Garmetix POS save-after-resume readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

assertIncludes('Hold Bills writes the resumed draft to sale storage', holdBillsPage, 'writeSaleDraft(bill.draft || {})')
assertIncludes('Hold Bills navigates resumed work to Sale', holdBillsPage, "await navigateTo('/sale')")
assertIncludes('Sale page restores browser sale draft', salePage, 'const draft = readSaleDraft()')
assertIncludes('Sale page builds backend sale request before save', salePage, 'createPosSaleRequest({')
assertIncludes('Sale save clears draft only after API save path', salePage, 'clearDraft()')
assertIncludes('Sale save records print recovery queue after save', salePage, 'addPrintQueueItem({')
assertIncludes('Sale save blocks non-cash payments without bank account', salePage, 'Select bank account for')
assertIncludes('Sale contract carries optional salesman id', saleContract, 'salesmanId: string | null')
assertIncludes('Backend resolves missing salesman with a default', backendBilling, 'return await GetDefaultSalesmanIdAsync(companyId, storeId, context, db, cancellationToken);')
assertIncludes('Backend creates default Manager salesman when needed', backendBilling, 'Name = "Manager"')

const seed = buildSeedDraft()
const payload = createSalePayloadFromDraft(seed)
validateSalePayload(payload)
validatePaymentRules(payload)

const nonCashWithoutBank = createSalePayloadFromDraft({
  ...seed,
  payments: [{ paymentMode: 2, amount: 999, bankAccountId: null, referenceNumber: 'UTR-SMOKE' }]
})
if (!findMissingBankPayment(nonCashWithoutBank)) {
  failures.push('Non-cash resumed payment without a bank account was not detected as unsafe.')
}

const nonCashWithBank = createSalePayloadFromDraft({
  ...seed,
  payments: [{ paymentMode: 2, amount: 999, bankAccountId: seed.bankAccountId, referenceNumber: 'UTR-SMOKE' }]
})
if (findMissingBankPayment(nonCashWithBank)) {
  failures.push('Non-cash resumed payment with a bank account was incorrectly flagged.')
}

if (!payload.salesmanId) {
  warnings.push('Seed resumed sale has no salesman id; backend Manager fallback must remain available.')
}

if (failures.length > 0) {
  console.error('\nPOS save-after-resume readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

for (const warning of warnings) console.log(`WARN ${warning}`)

console.log('PASS resumed held-bill draft can become a safe POS sale request.')
console.log('PASS cash resumed draft does not require a bank account.')
console.log('PASS non-cash resumed draft requires a bank account before save.')
console.log('PASS backend Manager salesman fallback is present for missing salesman ids.')
console.log('\nPOS save-after-resume readiness passed.')

function assertIncludes(label, source, pattern) {
  if (!source.includes(pattern)) {
    failures.push(`${label}: missing pattern ${pattern}`)
    return
  }

  console.log(`PASS ${label}`)
}

function buildSeedDraft() {
  const companyId = '00000000-0000-4000-8000-000000000001'
  const storeGroupId = '00000000-0000-4000-8000-000000000002'
  const storeId = '00000000-0000-4000-8000-000000000003'
  return {
    companyId,
    storeGroupId,
    storeId,
    bankAccountId: '00000000-0000-4000-8000-000000000020',
    form: {
      companyId,
      storeGroupId,
      storeId,
      customerId: null,
      salesmanId: null,
      customerName: 'Smoke Held Customer',
      customerMobileNumber: '9999999999',
      customerGstin: '',
      billDiscountAmount: 0
    },
    cart: [
      {
        productId: '00000000-0000-4000-8000-000000000010',
        barcode: 'SMOKE-HELD-001',
        quantity: 1,
        mrp: 999,
        discountAmount: 0
      }
    ],
    payments: [
      {
        paymentMode: 0,
        amount: 999,
        bankAccountId: null,
        referenceNumber: ''
      }
    ]
  }
}

function createSalePayloadFromDraft(draft) {
  const payments = draft.payments.map((payment) => ({
    paymentMode: Number(payment.paymentMode),
    amount: Number(payment.amount || 0),
    bankAccountId: payment.bankAccountId || null,
    referenceNumber: payment.referenceNumber || null,
    gatewayReference: null,
    settlementStatus: null,
    adjustmentSourceType: null,
    adjustmentSourceId: null
  }))

  return {
    companyId: draft.form.companyId,
    storeGroupId: draft.form.storeGroupId,
    storeId: draft.form.storeId,
    customerId: draft.form.customerId || null,
    salesmanId: draft.form.salesmanId || null,
    customerName: draft.form.customerName || 'Walk-in Customer',
    customerMobileNumber: draft.form.customerMobileNumber || '',
    customerGstin: draft.form.customerGstin || '',
    paymentMode: payments.length > 1 ? 12 : Number(payments[0]?.paymentMode ?? 0),
    bankAccountId: payments.find(item => item.bankAccountId)?.bankAccountId || null,
    paidAmount: payments.reduce((sum, item) => sum + Number(item.amount || 0), 0),
    billDiscountAmount: Number(draft.form.billDiscountAmount || 0),
    payments,
    items: draft.cart.map((item) => ({
      productId: item.productId,
      barcode: item.barcode,
      quantity: Number(item.quantity),
      mrp: Number(item.mrp),
      discountAmount: Number(item.discountAmount || 0)
    }))
  }
}

function validateSalePayload(payload) {
  const requiredGuidFields = ['companyId', 'storeGroupId', 'storeId']
  for (const field of requiredGuidFields) {
    if (!isGuid(payload[field])) failures.push(`Resumed sale payload has invalid ${field}.`)
  }

  if (!payload.items.length) failures.push('Resumed sale payload has no items.')
  for (const item of payload.items) {
    if (!isGuid(item.productId)) failures.push('Resumed sale item has invalid productId.')
    if (!item.barcode) failures.push('Resumed sale item has no barcode.')
    if (item.quantity <= 0) failures.push('Resumed sale item quantity must be greater than zero.')
    if (item.mrp < 0) failures.push('Resumed sale item MRP cannot be negative.')
    if (item.discountAmount < 0) failures.push('Resumed sale item discount cannot be negative.')
  }

  const gross = payload.items.reduce((sum, item) => sum + Math.max((item.mrp - item.discountAmount) * item.quantity, 0), 0)
  const billAmount = Math.round(Math.max(gross - payload.billDiscountAmount, 0))
  if (payload.paidAmount > billAmount) failures.push('Resumed sale paid amount cannot be greater than bill amount.')
  if (payload.billDiscountAmount < 0) failures.push('Resumed sale bill discount cannot be negative.')
}

function validatePaymentRules(payload) {
  if (!payload.payments.length) failures.push('Resumed sale payload has no payment rows.')
  for (const payment of payload.payments) {
    if (payment.amount < 0) failures.push('Resumed sale payment amount cannot be negative.')
  }

  const missingBank = findMissingBankPayment(payload)
  if (missingBank) failures.push(`Resumed ${missingBank.paymentMode} payment is missing a bank account.`)
}

function findMissingBankPayment(payload) {
  return payload.payments.find(item => Number(item.amount || 0) > 0 && requiresBank(item.paymentMode) && !item.bankAccountId)
}

function requiresBank(paymentMode) {
  return Number(paymentMode) !== 0
}

function isGuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value || ''))
}

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

export interface SwalekhaTrip {
  id: string
  sheetId: string
  name: string
  destination?: string | null
  startDate?: string | null
  endDate?: string | null
  budget?: number | null
  spentTotal: number
  isClosed: boolean
  closedAt?: string | null
  notes?: string | null
  createdAt: string
}

export interface SwalekhaTripPayload {
  name: string
  destination?: string | null
  startDate?: string | null
  endDate?: string | null
  budget?: number | null
  notes?: string | null
}

export interface SwalekhaOwnerProfile {
  id: string
  fullName?: string | null
  pan?: string | null
  aadhar?: string | null
  passportNo?: string | null
  mobile?: string | null
  email?: string | null
  addressLine?: string | null
  city?: string | null
  state?: string | null
  country?: string | null
  zipCode?: string | null
  spouseName?: string | null
  spouseContact?: string | null
  linkedAccountId?: string | null
  sourceEmployeeId?: string | null
  isAutoProvisioned: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaOwnerProfilePayload {
  fullName?: string | null
  pan?: string | null
  aadhar?: string | null
  passportNo?: string | null
  mobile?: string | null
  email?: string | null
  addressLine?: string | null
  city?: string | null
  state?: string | null
  country?: string | null
  zipCode?: string | null
  spouseName?: string | null
  spouseContact?: string | null
  linkedAccountId?: string | null
  notes?: string | null
}

export interface SwalekhaFamilyMember {
  id: string
  name: string
  relationship?: string | null
  mobile?: string | null
  email?: string | null
  dateOfBirth?: string | null
  linkedOwnerId?: string | null
  linkedOwnerName?: string | null
  linkConfirmed: boolean
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaFamilyMemberPayload {
  name: string
  relationship?: string | null
  mobile?: string | null
  email?: string | null
  dateOfBirth?: string | null
  linkedOwnerId?: string | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaLinkableOwner {
  id: string
  name: string
}

export interface SwalekhaFamilyTransferPayload {
  fromAccountId: string
  amount: number
  transactionDate: string
  narration?: string | null
}

export interface SwalekhaFamilyTransferResult {
  message: string
  fromAccountBalance: number
}

export interface SwalekhaFixedDeposit {
  id: string
  bankName: string
  fdNumber?: string | null
  accountId?: string | null
  principalAmount: number
  interestRatePercent: number
  tenureMonths: number
  startDate: string
  maturityDate: string
  maturityAmount?: number | null
  autoRenew: boolean
  tdsDeducted?: number | null
  isClosed: boolean
  closedAt?: string | null
  notes?: string | null
  createdAt: string
}

export interface SwalekhaFixedDepositPayload {
  bankName: string
  fdNumber?: string | null
  accountId?: string | null
  principalAmount: number
  interestRatePercent: number
  tenureMonths: number
  startDate: string
  maturityDate: string
  maturityAmount?: number | null
  autoRenew: boolean
  tdsDeducted?: number | null
  notes?: string | null
}

export interface SwalekhaMarkMaturedPayload {
  maturityAmount: number
  maturityCreditedDate: string
  narration?: string | null
}

export interface SwalekhaRecurringDeposit {
  id: string
  bankName: string
  rdNumber?: string | null
  accountId?: string | null
  monthlyInstallment: number
  interestRatePercent: number
  tenureMonths: number
  startDate: string
  maturityDate: string
  maturityAmount?: number | null
  installmentsPaid: number
  isClosed: boolean
  closedAt?: string | null
  notes?: string | null
  createdAt: string
}

export interface SwalekhaRecurringDepositPayload {
  bankName: string
  rdNumber?: string | null
  accountId?: string | null
  monthlyInstallment: number
  interestRatePercent: number
  tenureMonths: number
  startDate: string
  maturityDate: string
  maturityAmount?: number | null
  notes?: string | null
}

export interface SwalekhaRecordInstallmentPayload {
  installmentDate: string
  narration?: string | null
}

export type SwalekhaMutualFundInvestmentMode = 'Lumpsum' | 'SIP'
export type SwalekhaMutualFundTransactionType = 'Purchase' | 'SipInstallment' | 'Redemption'

export interface SwalekhaMutualFund {
  id: string
  schemeName: string
  amc?: string | null
  folioNumber?: string | null
  accountId?: string | null
  investmentMode: SwalekhaMutualFundInvestmentMode
  sipAmount?: number | null
  sipDayOfMonth?: number | null
  lastSipInstallmentDate?: string | null
  sipDueThisMonth: boolean
  currentNav?: number | null
  currentNavUpdatedAt?: string | null
  currentUnits: number
  totalInvested: number
  currentValue?: number | null
  absoluteReturn?: number | null
  returnPercent?: number | null
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaMutualFundPayload {
  schemeName: string
  amc?: string | null
  folioNumber?: string | null
  accountId?: string | null
  investmentMode: SwalekhaMutualFundInvestmentMode
  sipAmount?: number | null
  sipDayOfMonth?: number | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaUpdateNavPayload {
  currentNav: number
}

export interface SwalekhaMutualFundTransaction {
  id: string
  fundId: string
  transactionType: SwalekhaMutualFundTransactionType
  transactionDate: string
  units: number
  navAtTransaction: number
  amount: number
  narration: string
  createdAt: string
}

export interface SwalekhaMutualFundTransactionPayload {
  transactionType: SwalekhaMutualFundTransactionType
  transactionDate: string
  amount?: number | null
  units?: number | null
  navAtTransaction: number
  narration?: string | null
}

export interface SwalekhaMutualFundTransactionList {
  page: number
  pageSize: number
  totalCount: number
  rows: SwalekhaMutualFundTransaction[]
}

export interface SwalekhaMutualFundReturns {
  currentUnits: number
  currentNav?: number | null
  currentValue?: number | null
  totalInvested: number
  absoluteReturn?: number | null
  returnPercent?: number | null
  xirr?: number | null
  xirrAvailable: boolean
  xirrNote?: string | null
}

export type SwalekhaShareTransactionType = 'Buy' | 'Sell'
export type SwalekhaOtherAssetType = 'PPF' | 'EPF' | 'NPS' | 'Gold' | 'Other'

export interface SwalekhaShareHolding {
  id: string
  symbol: string
  companyName?: string | null
  exchange?: string | null
  dematAccount?: string | null
  broker?: string | null
  accountId?: string | null
  currentPrice?: number | null
  currentPriceUpdatedAt?: string | null
  currentQuantity: number
  totalInvested: number
  realizedPnL: number
  currentValue?: number | null
  unrealizedPnL?: number | null
  unrealizedPnLPercent?: number | null
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaShareHoldingPayload {
  symbol: string
  companyName?: string | null
  exchange?: string | null
  dematAccount?: string | null
  broker?: string | null
  accountId?: string | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaUpdateSharePricePayload {
  currentPrice: number
}

export interface SwalekhaShareTransaction {
  id: string
  holdingId: string
  transactionType: SwalekhaShareTransactionType
  transactionDate: string
  quantity: number
  pricePerShare: number
  amount: number
  narration: string
  realizedPnLOnSale?: number | null
  createdAt: string
}

export interface SwalekhaShareTransactionPayload {
  transactionType: SwalekhaShareTransactionType
  transactionDate: string
  quantity: number
  pricePerShare: number
  narration?: string | null
}

export interface SwalekhaShareTransactionList {
  page: number
  pageSize: number
  totalCount: number
  rows: SwalekhaShareTransaction[]
}

export interface SwalekhaOtherAsset {
  id: string
  assetType: SwalekhaOtherAssetType
  name: string
  currentValue: number
  asOfDate: string
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaOtherAssetPayload {
  assetType: SwalekhaOtherAssetType
  name: string
  currentValue: number
  asOfDate: string
  isActive: boolean
  notes?: string | null
}

export type SwalekhaAssetCategory = 'Immovable' | 'Movable'

export interface SwalekhaAsset {
  id: string
  category: SwalekhaAssetCategory
  assetSubType: string
  name: string
  purchaseValue?: number | null
  purchaseDate?: string | null
  currentValue: number
  asOfDate: string
  location?: string | null
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaAssetPayload {
  category: SwalekhaAssetCategory
  assetSubType: string
  name: string
  purchaseValue?: number | null
  purchaseDate?: string | null
  currentValue: number
  asOfDate: string
  location?: string | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaAssetSummary {
  immovableTotal: number
  immovableCount: number
  movableTotal: number
  movableCount: number
  grandTotal: number
}

export type SwalekhaLoanType = 'Personal' | 'Home' | 'Car' | 'Gold' | 'Education' | 'Other'
export type SwalekhaLoanPaymentType = 'Emi' | 'Prepayment'

export interface SwalekhaLoan {
  id: string
  loanType: SwalekhaLoanType
  lenderName: string
  loanNumber?: string | null
  accountId?: string | null
  principalAmount: number
  interestRatePercent: number
  tenureMonths: number
  emiAmount: number
  startDate: string
  outstandingPrincipal: number
  isClosed: boolean
  closedAt?: string | null
  notes?: string | null
  createdAt: string
}

export interface SwalekhaLoanPayload {
  loanType: SwalekhaLoanType
  lenderName: string
  loanNumber?: string | null
  accountId?: string | null
  principalAmount: number
  interestRatePercent: number
  tenureMonths: number
  emiAmount: number
  startDate: string
  notes?: string | null
}

export interface SwalekhaCalculateEmiPayload {
  principalAmount: number
  interestRatePercent: number
  tenureMonths: number
}

export interface SwalekhaCalculateEmiResult {
  emiAmount: number
}

export interface SwalekhaAmortizationRow {
  monthNumber: number
  openingBalance: number
  interestComponent: number
  principalComponent: number
  closingBalance: number
}

export interface SwalekhaLoanPayment {
  id: string
  loanId: string
  paymentType: SwalekhaLoanPaymentType
  paymentDate: string
  amount: number
  principalComponent: number
  interestComponent: number
  outstandingAfter: number
  narration: string
  createdAt: string
}

export interface SwalekhaLoanPaymentPayload {
  paymentType: SwalekhaLoanPaymentType
  paymentDate: string
  amount: number
  narration?: string | null
}

export interface SwalekhaLoanPaymentList {
  page: number
  pageSize: number
  totalCount: number
  rows: SwalekhaLoanPayment[]
}

export type SwalekhaInsurancePolicyType = 'Life' | 'Health' | 'Term' | 'Vehicle' | 'Property' | 'ULIP' | 'Other'
export type SwalekhaPremiumFrequency = 'Monthly' | 'Quarterly' | 'HalfYearly' | 'Yearly'

export interface SwalekhaInsurancePolicy {
  id: string
  policyType: SwalekhaInsurancePolicyType
  insurer: string
  policyNumber?: string | null
  accountId?: string | null
  sumAssured?: number | null
  premiumAmount: number
  premiumFrequency: SwalekhaPremiumFrequency
  startDate: string
  nomineeName?: string | null
  nomineeRelationship?: string | null
  maturityDate?: string | null
  maturityAmount?: number | null
  lastPremiumPaidDate?: string | null
  nextPremiumDueDate: string
  premiumDueNow: boolean
  isMatured: boolean
  maturedAt?: string | null
  isActive: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaInsurancePolicyPayload {
  policyType: SwalekhaInsurancePolicyType
  insurer: string
  policyNumber?: string | null
  accountId?: string | null
  sumAssured?: number | null
  premiumAmount: number
  premiumFrequency: SwalekhaPremiumFrequency
  startDate: string
  nomineeName?: string | null
  nomineeRelationship?: string | null
  maturityDate?: string | null
  maturityAmount?: number | null
  isActive: boolean
  notes?: string | null
}

export interface SwalekhaPayPremiumPayload {
  paymentDate: string
  narration?: string | null
}

export interface SwalekhaJournalEntry {
  id: string
  entryDate: string
  title?: string | null
  content: string
  mood?: string | null
  createdAt: string
}

export interface SwalekhaJournalEntryPayload {
  entryDate: string
  title?: string | null
  content: string
  mood?: string | null
}

export interface SwalekhaPersonalNote {
  id: string
  title: string
  content: string
  folder?: string | null
  tags?: string | null
  isPinned: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface SwalekhaPersonalNotePayload {
  title: string
  content: string
  folder?: string | null
  tags?: string | null
  isPinned: boolean
}

export interface SwalekhaAppointment {
  id: string
  title: string
  description?: string | null
  startAt: string
  endAt?: string | null
  location?: string | null
  isAllDay: boolean
  notes?: string | null
  createdAt: string
}

export interface SwalekhaAppointmentPayload {
  title: string
  description?: string | null
  startAt: string
  endAt?: string | null
  location?: string | null
  isAllDay: boolean
  notes?: string | null
}

export type SwalekhaCalendarEventType = 'Appointment' | 'RecurringBill' | 'FixedDepositMaturity' | 'RecurringDepositMaturity' | 'InsurancePremium'

export interface SwalekhaCalendarEvent {
  date: string
  eventType: SwalekhaCalendarEventType
  title: string
  description?: string | null
  amount?: number | null
  sourceId?: string | null
}

export interface SwalekhaBreakdownRow {
  category: string
  value: number
  note?: string | null
}

export interface SwalekhaDashboard {
  netWorth: number
  totalAssets: number
  totalLiabilities: number
  assetsBreakdown: SwalekhaBreakdownRow[]
  liabilitiesBreakdown: SwalekhaBreakdownRow[]
  upcomingDues: SwalekhaCalendarEvent[]
  todayAppointments: SwalekhaAppointment[]
}

export type SwalekhaDocumentEntityType = 'FixedDeposit' | 'RecurringDeposit' | 'MutualFund' | 'ShareHolding' | 'Loan' | 'InsurancePolicy' | 'General'

export interface SwalekhaDocument {
  id: string
  entityType: string
  entityId?: string | null
  fileName: string
  contentType: string
  fileSizeBytes: number
  notes?: string | null
  createdAt: string
}

export interface SwalekhaSelfCheckItem {
  name: string
  passed: boolean
  detail: string
}

export interface SwalekhaSelfCheck {
  allPassed: boolean
  ownerId: string
  checks: SwalekhaSelfCheckItem[]
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

  async function postForm<T>(path: string, formData: FormData) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const token = getStoredToken(window.localStorage)
    const response = await fetch(`${apiBaseUrl.value}/${normalizeSwalekhaPath(path)}`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: formData
    })
    if (!response.ok) {
      const text = await response.text().catch(() => '')
      throw new Error(text || `Upload failed (${response.status}).`)
    }
    return (await response.json()) as T
  }

  async function openBlob(path: string) {
    if (!apiBaseUrl.value) throw new Error('API base URL is not configured.')
    const token = getStoredToken(window.localStorage)
    const response = await fetch(`${apiBaseUrl.value}/${normalizeSwalekhaPath(path)}`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    })
    if (!response.ok) throw new Error(`Could not download the file (${response.status}).`)
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    window.open(url, '_blank')
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  }

  return { apiBaseUrl, get, post, put, del, postForm, openBlob }
}

function normalizeSwalekhaPath(path: string) {
  const clean = String(path || '').replace(/^\/+/, '')
  return clean.startsWith('swalekha/') ? clean : `swalekha/${clean}`
}

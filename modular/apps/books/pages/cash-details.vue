<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Bank and cash audit</p>
          <h2 class="mt-1 text-2xl font-semibold">Bank Operations</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Read-only bank transactions, statements, reconciliation, cheque lifecycle, vendor bank accounts and bank access details. Posting and reconciliation actions stay disabled.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="selectedBankAccountId" :items="bankAccountOptions" class="min-w-64" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-3">
      <div class="border border-default bg-muted/10 p-4 xl:col-span-2">
        <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="text-base font-semibold">Bank Statement</h3>
            <p class="text-xs text-muted">{{ statementRows.length }} lines for {{ selectedBankAccountLabel }}</p>
          </div>
          <UBadge :color="statementLoading ? 'warning' : 'primary'" variant="subtle">{{ statementLoading ? 'Loading' : 'Read only' }}</UBadge>
        </div>
        <BooksMasterTable :columns="statementColumns" :rows="statementRows" empty-text="No bank statement lines found." />
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <h3 class="text-base font-semibold">Reconciliation Summary</h3>
        <div class="mt-3 space-y-3">
          <div v-for="item in reconciliationCards" :key="item.label" class="border border-default bg-default/40 p-3">
            <p class="text-xs text-muted">{{ item.label }}</p>
            <p class="mt-1 text-lg font-semibold">{{ item.value }}</p>
          </div>
        </div>
      </div>
    </section>

    <div class="flex flex-wrap gap-2">
      <UButton
        v-for="tab in tabs"
        :key="tab.key"
        :icon="tab.icon"
        size="sm"
        color="neutral"
        :variant="activeTab === tab.key ? 'soft' : 'ghost'"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </UButton>
    </div>

    <section class="border border-default bg-muted/10 p-4">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="text-base font-semibold">{{ currentTab.label }}</h3>
          <p class="text-xs text-muted">{{ currentTab.description }}</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search bank operation rows" class="sm:w-72" />
      </div>
      <BooksMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No bank operation rows found." />
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  accountTypeOptions,
  formatDate,
  optionLabel,
  readArray,
  readNumber,
  readText,
  toRows,
  transactionModeOptions,
  transactionTypeOptions,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'Cash Details - Garmetix Books' })

type BankTab = 'transactions' | 'cheques' | 'vendorBanks' | 'accountDetails' | 'bankAccounts'

const { get } = useBooksApiClient()
const loading = ref(true)
const statementLoading = ref(false)
const error = ref('')
const search = ref('')
const activeTab = ref<BankTab>('transactions')
const selectedBankAccountId = ref('')
const banks = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const bankTransactions = ref<ApiRecord[]>([])
const chequeLogs = ref<ApiRecord[]>([])
const vendorBankAccounts = ref<ApiRecord[]>([])
const bankAccountDetails = ref<ApiRecord[]>([])
const bankStatement = ref<ApiRecord[]>([])
const bankReconciliation = ref<ApiRecord | null>(null)

const tabs = [
  { key: 'transactions' as const, label: 'Transactions', icon: 'i-lucide-arrow-left-right', description: 'Posted bank transactions from accounting.' },
  { key: 'cheques' as const, label: 'Cheques', icon: 'i-lucide-scroll-text', description: 'Issued and deposited cheque lifecycle log.' },
  { key: 'vendorBanks' as const, label: 'Vendor Banks', icon: 'i-lucide-wallet-cards', description: 'Vendor bank account records and linked ledgers.' },
  { key: 'accountDetails' as const, label: 'Account Details', icon: 'i-lucide-key-round', description: 'Bank account access/detail records.' },
  { key: 'bankAccounts' as const, label: 'Bank Accounts', icon: 'i-lucide-landmark', description: 'Company bank account master list.' }
]
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])
const bankName = (id: unknown) => readText(banks.value.find(item => item.id === id), ['name'])
const ledgerName = (id: unknown) => readText(ledgers.value.find(item => item.id === id), ['name'])
const partyName = (id: unknown) => readText(parties.value.find(item => item.id === id), ['name'])
const vendorName = (id: unknown) => readText(vendors.value.find(item => item.id === id), ['name', 'vendorName'])
const bankAccountLabel = (item: ApiRecord | undefined) => {
  if (!item) return '-'
  return `${readText(item, ['accountHolderName'], 'Bank')} - ${readText(item, ['accountNumber'])}`.trim()
}
const bankAccountName = (id: unknown) => bankAccountLabel(bankAccounts.value.find(item => item.id === id))
const bankAccountOptions = computed(() => {
  const rows = bankAccounts.value.map(item => ({
    label: bankAccountLabel(item),
    value: String(item.id)
  }))
  return rows.length ? rows : [{ label: 'No bank accounts', value: '' }]
})
const selectedBankAccountLabel = computed(() => bankAccountName(selectedBankAccountId.value))
const statementLines = computed(() => readArray(bankReconciliation.value, ['lines']))
const cards = computed(() => [
  { label: 'Bank Accounts', value: bankAccounts.value.length, detail: 'Company bank accounts' },
  { label: 'Transactions', value: bankTransactions.value.length, detail: 'Posted bank transactions' },
  { label: 'Cheques', value: chequeLogs.value.length, detail: 'Cheque lifecycle rows' },
  { label: 'Open Lines', value: readNumber(bankReconciliation.value, ['openLineCount']), detail: 'Pending reconciliation' }
])
const reconciliationCards = computed(() => [
  { label: 'Statement Balance', value: formatIndianMoney(readNumber(bankReconciliation.value, ['statementBalance'])) },
  { label: 'Open Debit', value: formatIndianMoney(readNumber(bankReconciliation.value, ['openDebit'])) },
  { label: 'Open Credit', value: formatIndianMoney(readNumber(bankReconciliation.value, ['openCredit'])) },
  { label: 'Reconciled Lines', value: readNumber(bankReconciliation.value, ['reconciledLineCount']) }
])
const statementRows = computed(() => (statementLines.value.length ? statementLines.value : bankStatement.value).map(item => ({
  date: formatDate(item.onDate),
  description: readText(item, ['description']),
  reference: readText(item, ['reference']),
  bankTransactionId: readText(item, ['bankTransactionId']),
  debit: formatIndianMoney(readNumber(item, ['debit'])),
  credit: formatIndianMoney(readNumber(item, ['credit'])),
  balance: formatIndianMoney(readNumber(item, ['balance'])),
  status: item.reconciled ? 'Reconciled' : 'Open',
  reconciledAt: formatDate(item.reconciledAt)
})))
const tableRows = computed<Record<BankTab, ApiRecord[]>>(() => ({
  transactions: bankTransactions.value.map(item => ({
    date: formatDate(item.onDate),
    bank: bankAccountName(item.bankAccountId),
    type: optionLabel(transactionTypeOptions, item.transactionType),
    mode: optionLabel(transactionModeOptions, item.transactionMode),
    ledger: ledgerName(item.ledgerId),
    party: partyName(item.partyId),
    reference: readText(item, ['reference']),
    person: readText(item, ['personName']),
    amount: formatIndianMoney(readNumber(item, ['amount']))
  })),
  cheques: chequeLogs.value.map(item => ({
    date: formatDate(item.onDate),
    cheque: readText(item, ['chequeNumber', 'cheequeNumber']),
    bank: bankAccountName(item.bankAccountId),
    person: readText(item, ['personName']),
    narration: readText(item, ['narration']),
    status: readText(item, ['status']),
    amount: formatIndianMoney(readNumber(item, ['amount']))
  })),
  vendorBanks: vendorBankAccounts.value.map(item => ({
    holder: readText(item, ['accountHolderName']),
    account: readText(item, ['accountNumber']),
    vendor: vendorName(item.vendorId),
    bank: bankName(item.bankId),
    ledger: ledgerName(item.ledgerId),
    ifsc: readText(item, ['ifsCode', 'ifscCode', 'ifSCode']),
    status: item.active === false ? 'Inactive' : 'Active'
  })),
  accountDetails: bankAccountDetails.value.map(item => ({
    bank: bankAccountName(item.bankAccountId),
    customerId: readText(item, ['customerId']),
    userName: readText(item, ['userName']),
    atmCard: readText(item, ['atmCard']),
    status: readText(item, ['status'])
  })),
  bankAccounts: bankAccounts.value.map(item => ({
    holder: readText(item, ['accountHolderName']),
    account: readText(item, ['accountNumber']),
    bank: bankName(item.bankId),
    type: optionLabel(accountTypeOptions, item.accountType),
    opening: formatIndianMoney(readNumber(item, ['openingBalance'])),
    closing: formatIndianMoney(readNumber(item, ['closingBalance'])),
    status: item.active === false ? 'Inactive' : 'Active'
  }))
}))
const columns: Record<BankTab, Array<{ key: string, label: string }>> = {
  transactions: [
    { key: 'date', label: 'Date' },
    { key: 'bank', label: 'Bank Account' },
    { key: 'type', label: 'Type' },
    { key: 'mode', label: 'Mode' },
    { key: 'ledger', label: 'Against Ledger' },
    { key: 'party', label: 'Party' },
    { key: 'reference', label: 'Reference' },
    { key: 'person', label: 'Person' },
    { key: 'amount', label: 'Amount' }
  ],
  cheques: [
    { key: 'date', label: 'Date' },
    { key: 'cheque', label: 'Cheque' },
    { key: 'bank', label: 'Bank Account' },
    { key: 'person', label: 'Person' },
    { key: 'narration', label: 'Narration' },
    { key: 'status', label: 'Status' },
    { key: 'amount', label: 'Amount' }
  ],
  vendorBanks: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'vendor', label: 'Vendor' },
    { key: 'bank', label: 'Bank' },
    { key: 'ledger', label: 'Ledger' },
    { key: 'ifsc', label: 'IFSC' },
    { key: 'status', label: 'Status' }
  ],
  accountDetails: [
    { key: 'bank', label: 'Bank Account' },
    { key: 'customerId', label: 'Customer ID' },
    { key: 'userName', label: 'User Name' },
    { key: 'atmCard', label: 'ATM Card' },
    { key: 'status', label: 'Status' }
  ],
  bankAccounts: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'bank', label: 'Bank' },
    { key: 'type', label: 'Type' },
    { key: 'opening', label: 'Opening' },
    { key: 'closing', label: 'Closing' },
    { key: 'status', label: 'Status' }
  ]
}
const statementColumns = [
  { key: 'date', label: 'Date' },
  { key: 'description', label: 'Description' },
  { key: 'reference', label: 'Reference' },
  { key: 'bankTransactionId', label: 'Bank Txn' },
  { key: 'debit', label: 'Debit' },
  { key: 'credit', label: 'Credit' },
  { key: 'balance', label: 'Balance' },
  { key: 'status', label: 'Status' },
  { key: 'reconciledAt', label: 'Reconciled On' }
]
const currentColumns = computed(() => columns[activeTab.value])
const currentRows = computed(() => tableRows.value[activeTab.value])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return currentRows.value
  return currentRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [bankData, ledgerData, partyData, vendorData, bankAccountData, transactionData, chequeData, vendorBankData, detailData] = await Promise.allSettled([
      get<unknown>('banks'),
      get<unknown>('ledgers'),
      get<unknown>('parties'),
      get<unknown>('vendors'),
      get<unknown>('bank-accounts'),
      get<unknown>('accounting/bank-transactions'),
      get<unknown>('cheque-logs'),
      get<unknown>('vendor-bank-accounts'),
      get<unknown>('bank-account-details')
    ])
    if (bankData.status === 'fulfilled') banks.value = toRows(bankData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)
    if (bankAccountData.status === 'fulfilled') bankAccounts.value = toRows(bankAccountData.value)
    if (transactionData.status === 'fulfilled') bankTransactions.value = toRows(transactionData.value)
    if (chequeData.status === 'fulfilled') chequeLogs.value = toRows(chequeData.value)
    if (vendorBankData.status === 'fulfilled') vendorBankAccounts.value = toRows(vendorBankData.value)
    if (detailData.status === 'fulfilled') bankAccountDetails.value = toRows(detailData.value)
    if (!selectedBankAccountId.value && bankAccounts.value.length) {
      selectedBankAccountId.value = String(bankAccounts.value[0]?.id ?? '')
    } else {
      await loadBankStatement()
    }
    const failed = [bankData, ledgerData, partyData, vendorData, bankAccountData, transactionData, chequeData, vendorBankData, detailData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} bank operation request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load bank operations.'
  } finally {
    loading.value = false
  }
}

async function loadBankStatement() {
  if (!selectedBankAccountId.value) {
    bankStatement.value = []
    bankReconciliation.value = null
    return
  }

  statementLoading.value = true
  try {
    const [statementData, reconciliationData] = await Promise.all([
      get<unknown>(`accounting/bank-statement/${selectedBankAccountId.value}`),
      get<unknown>(`accounting/bank-reconciliation/${selectedBankAccountId.value}`)
    ])
    bankStatement.value = toRows(statementData)
    bankReconciliation.value = reconciliationData && typeof reconciliationData === 'object' ? reconciliationData as ApiRecord : null
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load bank statement.'
  } finally {
    statementLoading.value = false
  }
}

watch(selectedBankAccountId, () => {
  loadBankStatement()
})

onMounted(refresh)
</script>

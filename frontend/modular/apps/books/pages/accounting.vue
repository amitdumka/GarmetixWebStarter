<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-book-open-check" class="size-4" /> Books master data</p>
          <h2 class="garmetix-dashboard-title">Accounting</h2>
          <p class="garmetix-dashboard-subtitle">
            Bank accounts, bank transactions, reconciliation, cheques, vendor banks, account details and trial balance. Ledgers moved to <NuxtLink to="/ledgers" class="underline">Ledgers</NuxtLink>, Parties moved to <NuxtLink to="/parties" class="underline">Parties</NuxtLink>.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton v-if="canCreate" icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">{{ `New ${singularLabel(activeTab)}` }}</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
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

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ currentTab.label }}</h3>
          <p class="garmetix-panel-subtitle">{{ currentTab.description }}</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect
            v-if="activeTab === 'reconciliation'"
            v-model="selectedBankAccountId"
            :items="bankAccountOptions"
            placeholder="Select bank account"
            class="sm:w-64"
          />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search master data" class="sm:w-72" />
        </div>
      </div>

      <BooksMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No accounting rows found.">
        <template v-if="hasRowActions" #actions="{ row }">
          <div class="flex flex-wrap items-center gap-1">
            <UButton v-if="canEditRow" icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(findRowById(row.id))" />
            <UButton
              v-if="activeTab === 'cheques'"
              icon="i-lucide-badge-check"
              size="xs"
              color="success"
              variant="ghost"
              :loading="reconciling"
              @click="updateChequeStatus(findRowById(row.id), 'Cleared')"
            />
            <UButton
              v-if="activeTab === 'cheques'"
              icon="i-lucide-ban"
              size="xs"
              color="warning"
              variant="ghost"
              :loading="reconciling"
              @click="updateChequeStatus(findRowById(row.id), 'Bounced')"
            />
            <UButton
              v-if="activeTab === 'reconciliation' && !readBool(findRowById(row.id), 'reconciled')"
              icon="i-lucide-check-circle-2"
              size="xs"
              color="success"
              variant="ghost"
              :loading="reconciling"
              @click="markStatementReconciled(findRowById(row.id))"
            />
            <UButton
              v-if="activeTab === 'reconciliation' && readBool(findRowById(row.id), 'reconciled')"
              icon="i-lucide-rotate-ccw"
              size="xs"
              color="warning"
              variant="ghost"
              :loading="reconciling"
              @click="undoStatementReconciliation(findRowById(row.id))"
            />
            <UButton v-if="canDeleteRow" icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(findRowById(row.id))" />
          </div>
        </template>
      </BooksMasterTable>
    </section>

    <UModal
      v-model:open="formOpen"
      :title="formTitle"
      :ui="{ content: formContentClass }"
    >
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveActiveForm">
          <template v-if="activeTab === 'bankAccounts'">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Account Holder</span>
              <UInput v-model="bankAccountForm.accountHolderName" placeholder="Account holder name" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Account Number</span>
              <UInput v-model="bankAccountForm.accountNumber" placeholder="Account number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Bank</span>
              <USelect v-model="bankAccountForm.bankId" :items="bankSelectItems" placeholder="Select bank" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Account Type</span>
              <USelect v-model="bankAccountForm.accountType" :items="accountTypeSelectItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Opening Date</span>
              <UInput v-model="bankAccountForm.openingDate" type="date" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Branch</span>
              <UInput v-model="bankAccountForm.branch" placeholder="Branch" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">IFSC</span>
              <UInput v-model="bankAccountForm.ifsCode" placeholder="IFSC code" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Closing Date</span>
              <UInput v-model="bankAccountForm.closingDate" type="date" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Opening Balance</span>
              <UInput v-model="bankAccountForm.openingBalance" type="number" step="0.01" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Closing Balance</span>
              <UInput v-model="bankAccountForm.closingBalance" type="number" step="0.01" />
            </label>
            <label class="flex items-center gap-2 text-sm sm:col-span-2">
              <UCheckbox v-model="bankAccountForm.active" />
              <span class="text-muted">Active</span>
            </label>
          </template>

          <template v-else-if="activeTab === 'bankTransactions'">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Bank Account</span>
              <USelect v-model="transactionForm.bankAccountId" :items="bankAccountSelectItems" placeholder="Select bank account" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Against Ledger</span>
              <USelectMenu v-model="transactionForm.ledgerId" value-key="value" :items="transactionLedgerSelectItems" placeholder="Search ledger..." />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Date</span>
              <UInput v-model="transactionForm.onDate" type="date" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Amount</span>
              <UInput v-model="transactionForm.amount" type="number" min="0" step="0.01" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Type</span>
              <USelect v-model="transactionForm.transactionType" :items="transactionTypeSelectItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Mode</span>
              <USelect v-model="transactionForm.transactionMode" :items="transactionModeSelectItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Reference</span>
              <UInput v-model="transactionForm.reference" placeholder="Reference" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Person</span>
              <UInput v-model="transactionForm.personName" placeholder="Person name" />
            </label>
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Narration</span>
              <UTextarea v-model="transactionForm.narration" :rows="2" placeholder="Narration" />
            </label>
          </template>

          <template v-else-if="activeTab === 'cheques'">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Bank Account</span>
              <USelect v-model="chequeForm.bankAccountId" :items="bankAccountSelectItems" placeholder="Select bank account" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Cheque Number</span>
              <UInput v-model="chequeForm.chequeNumber" placeholder="Cheque number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Entry Date</span>
              <UInput v-model="chequeForm.onDate" type="date" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Cheque Date</span>
              <UInput v-model="chequeForm.chequeDate" type="date" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Person</span>
              <UInput v-model="chequeForm.personName" placeholder="Person name" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Amount</span>
              <UInput v-model="chequeForm.amount" type="number" step="0.01" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Cheque Bank</span>
              <UInput v-model="chequeForm.chequeBank" placeholder="Cheque bank" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Status</span>
              <UInput v-model="chequeForm.status" placeholder="Issued / Cleared / Bounced" />
            </label>
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Narration</span>
              <UTextarea v-model="chequeForm.narration" :rows="2" placeholder="Narration" />
            </label>
            <label class="flex items-center gap-2 text-sm sm:col-span-2">
              <UCheckbox v-model="chequeForm.inHouse" />
              <span class="text-muted">In house cheque</span>
            </label>
          </template>

          <template v-else-if="activeTab === 'vendorBanks'">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Vendor</span>
              <USelect v-model="vendorBankForm.vendorId" :items="vendorSelectItems" placeholder="Select vendor" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Account Holder</span>
              <UInput v-model="vendorBankForm.accountHolderName" placeholder="Account holder name" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Account Number</span>
              <UInput v-model="vendorBankForm.accountNumber" placeholder="Account number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Bank</span>
              <USelect v-model="vendorBankForm.bankId" :items="bankSelectItems" placeholder="Select bank" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Ledger</span>
              <USelectMenu v-model="vendorBankForm.ledgerId" value-key="value" :items="ledgerSelectItems" placeholder="Search ledger..." />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Account Type</span>
              <USelect v-model="vendorBankForm.accountType" :items="accountTypeSelectItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">IFSC</span>
              <UInput v-model="vendorBankForm.ifsCode" placeholder="IFSC code" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Branch</span>
              <UInput v-model="vendorBankForm.branch" placeholder="Branch" />
            </label>
            <label class="flex items-center gap-2 text-sm sm:col-span-2">
              <UCheckbox v-model="vendorBankForm.active" />
              <span class="text-muted">Active</span>
            </label>
          </template>

          <template v-else-if="activeTab === 'accountDetails'">
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Bank Account</span>
              <USelect v-model="accountDetailForm.bankAccountId" :items="bankAccountSelectItems" placeholder="Select bank account" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Customer ID</span>
              <UInput v-model="accountDetailForm.customerId" placeholder="Customer ID" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">User Name</span>
              <UInput v-model="accountDetailForm.userName" placeholder="User name" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">ATM Card</span>
              <UInput v-model="accountDetailForm.atmCard" placeholder="ATM card" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Expire Date</span>
              <UInput v-model="accountDetailForm.expireDate" type="date" />
            </label>
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Status</span>
              <UInput v-model="accountDetailForm.status" placeholder="Active" />
            </label>
          </template>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">
              {{ editMode === 'edit' ? 'Update' : 'Save' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  accountTypeOptions,
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

useHead({ title: 'Accounting - Garmetix Books' })

type AccountingTab = 'bankAccounts' | 'bankTransactions' | 'reconciliation' | 'cheques' | 'vendorBanks' | 'accountDetails' | 'trialBalance'

interface BankAccountForm {
  id: string
  accountNumber: string
  accountHolderName: string
  bankId: string
  accountType: number
  branch: string
  ifsCode: string
  openingDate: string
  active: boolean
  closingDate: string
  openingBalance: number | string
  closingBalance: number | string
}

interface TransactionForm {
  id: string
  bankAccountId: string
  onDate: string
  transactionType: number
  transactionMode: number
  ledgerId: string
  narration: string
  reference: string
  amount: number | string
  personName: string
}

interface ChequeForm {
  id: string
  bankAccountId: string
  chequeNumber: string
  onDate: string
  chequeDate: string
  narration: string
  chequeBank: string
  amount: number | string
  personName: string
  status: string
  inHouse: boolean
}

interface VendorBankForm extends BankAccountForm {
  ledgerId: string
  vendorId: string
}

interface AccountDetailForm {
  id: string
  bankAccountId: string
  customerId: string
  userName: string
  atmCard: string
  expireDate: string
  status: string
}

function localDateInput(value: unknown = new Date()) {
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) return value.slice(0, 10)
  const date = value instanceof Date ? value : new Date(String(value || new Date()))
  if (Number.isNaN(date.getTime())) return localDateInput(new Date())
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10)
}

function toApiDate(value: string) {
  return `${localDateInput(value)}T00:00:00`
}

function nullableApiDate(value: string) {
  return value ? toApiDate(value) : null
}

function emptyBankAccountForm(): BankAccountForm {
  return { id: '', accountNumber: '', accountHolderName: '', bankId: '', accountType: 1, branch: '', ifsCode: '', openingDate: localDateInput(), active: true, closingDate: '', openingBalance: 0, closingBalance: 0 }
}

function emptyTransactionForm(): TransactionForm {
  return { id: '', bankAccountId: '', onDate: localDateInput(), transactionType: 0, transactionMode: 4, ledgerId: '', narration: '', reference: '', amount: 0, personName: '' }
}

function emptyChequeForm(): ChequeForm {
  return { id: '', bankAccountId: '', chequeNumber: '', onDate: localDateInput(), chequeDate: localDateInput(), narration: '', chequeBank: '', amount: 0, personName: '', status: 'Issued', inHouse: false }
}

function emptyVendorBankForm(): VendorBankForm {
  return { ...emptyBankAccountForm(), ledgerId: '', vendorId: '' }
}

function emptyAccountDetailForm(): AccountDetailForm {
  return { id: '', bankAccountId: '', customerId: '', userName: '', atmCard: '', expireDate: '', status: 'Active' }
}

const { get, post, put, del } = useBooksApiClient()
const loading = ref(true)
const statementLoading = ref(false)
const saving = ref(false)
const reconciling = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const activeTab = ref<AccountingTab>('bankAccounts')
const selectedBankAccountId = ref('')
const editMode = ref<'create' | 'edit'>('create')
const formOpen = ref(false)

const ledgers = ref<ApiRecord[]>([])
const banks = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const bankTransactions = ref<ApiRecord[]>([])
const chequeLogs = ref<ApiRecord[]>([])
const vendorBankAccounts = ref<ApiRecord[]>([])
const bankAccountDetails = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const trialBalance = ref<ApiRecord[]>([])
const bankStatement = ref<ApiRecord[]>([])
const bankReconciliation = ref<ApiRecord | null>(null)
const setupStatus = ref<ApiRecord | null>(null)
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])

const bankAccountForm = reactive<BankAccountForm>(emptyBankAccountForm())
const transactionForm = reactive<TransactionForm>(emptyTransactionForm())
const chequeForm = reactive<ChequeForm>(emptyChequeForm())
const vendorBankForm = reactive<VendorBankForm>(emptyVendorBankForm())
const accountDetailForm = reactive<AccountDetailForm>(emptyAccountDetailForm())

const accountTypeSelectItems = accountTypeOptions.map(item => ({ label: item.label, value: item.value }))
const transactionTypeSelectItems = transactionTypeOptions.map(item => ({ label: item.label, value: item.value }))
const transactionModeSelectItems = transactionModeOptions.map(item => ({ label: item.label, value: item.value }))
const bankSelectItems = computed(() => banks.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))
const ledgerSelectItems = computed(() => ledgers.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))
const bankAccountSelectItems = computed(() => bankAccounts.value
  .map(item => ({ label: bankAccountLabel(item), value: readText(item, ['id'], '') }))
  .filter(item => item.value))
const vendorSelectItems = computed(() => vendors.value
  .map(item => ({ label: readText(item, ['name', 'vendorName'], 'Vendor'), value: readText(item, ['id'], '') }))
  .filter(item => item.value))
const bankAccountOptions = computed(() => bankAccountSelectItems.value)
const transactionLedgerSelectItems = computed(() => {
  const selectedBank = bankAccounts.value.find(item => readText(item, ['id'], '') === transactionForm.bankAccountId)
  const bankLedgerId = readText(selectedBank, ['ledgerId'], '')
  return ledgerSelectItems.value.filter(item => item.value !== bankLedgerId)
})

const canCreate = computed(() => !['trialBalance', 'reconciliation'].includes(activeTab.value))
const canEditRow = computed(() => !['reconciliation'].includes(activeTab.value))
const canDeleteRow = computed(() => !['reconciliation'].includes(activeTab.value))
const hasRowActions = computed(() => ['bankAccounts', 'bankTransactions', 'cheques', 'vendorBanks', 'accountDetails', 'reconciliation'].includes(activeTab.value))
const formTitle = computed(() => `${editMode.value === 'edit' ? 'Edit' : 'New'} ${singularLabel(activeTab.value)}`)
const formContentClass = computed(() => {
  if (['bankTransactions', 'cheques', 'vendorBanks', 'accountDetails'].includes(activeTab.value)) {
    return 'w-[calc(100vw-2rem)] sm:max-w-3xl'
  }
  return undefined
})

function singularLabel(tab: AccountingTab) {
  return {
    bankAccounts: 'Bank Account',
    bankTransactions: 'Bank Transaction',
    reconciliation: 'Reconciliation',
    cheques: 'Cheque Log',
    vendorBanks: 'Vendor Bank Account',
    accountDetails: 'Bank Account Detail',
    trialBalance: 'Trial Balance'
  }[tab]
}

const bankName = (id: unknown) => readText(banks.value.find(item => item.id === id), ['name'])
const ledgerName = (id: unknown) => readText(ledgers.value.find(item => item.id === id), ['name'])
const vendorName = (id: unknown) => {
  if (!id) return '-'
  return readText(vendors.value.find(item => item.id === id), ['name', 'vendorName'])
}
const bankAccountLabel = (account: ApiRecord | null | undefined) => {
  if (!account) return '-'
  return `${readText(account, ['accountHolderName'], bankName(account.bankId))} - ${readText(account, ['accountNumber'])}`.trim()
}
const bankAccountName = (id: unknown) => bankAccountLabel(bankAccounts.value.find(item => item.id === id))
const ledgerExists = (id: unknown) => Boolean(id && ledgers.value.some(item => item.id === id))
const readBool = (item: ApiRecord | null, key: string) => Boolean(item?.[key])

function findRowById(id: unknown): ApiRecord | null {
  const source: Record<AccountingTab, ApiRecord[]> = {
    bankAccounts: bankAccounts.value,
    bankTransactions: bankTransactions.value,
    reconciliation: bankReconciliation.value?.lines as ApiRecord[] ?? bankStatement.value,
    cheques: chequeLogs.value,
    vendorBanks: vendorBankAccounts.value,
    accountDetails: bankAccountDetails.value,
    trialBalance: []
  }
  return (source[activeTab.value] ?? []).find(item => readText(item, ['id'], '') === id) ?? null
}

const cards = computed(() => [
  { label: 'Bank Accounts', value: bankAccounts.value.length, detail: 'Linked to ledgers' },
  { label: 'Bank Balance', value: formatIndianMoney(bankAccounts.value.reduce((sum, item) => sum + readNumber(item, ['closingBalance', 'openingBalance']), 0)), detail: 'All bank accounts' },
  { label: 'Cheques', value: chequeLogs.value.length, detail: 'Issued and deposited' },
  { label: 'Vendor Banks', value: vendorBankAccounts.value.length, detail: 'Vendor payout accounts' }
])

const tabs: Array<{ key: AccountingTab, label: string, icon: string, description: string }> = [
  { key: 'bankAccounts', label: 'Bank Accounts', icon: 'i-lucide-landmark', description: 'Bank accounts with ledger link status.' },
  { key: 'bankTransactions', label: 'Bank Transactions', icon: 'i-lucide-arrow-left-right', description: 'Deposits and withdrawals posted against a bank account.' },
  { key: 'reconciliation', label: 'Bank Reconciliation', icon: 'i-lucide-list-checks', description: 'Reconcile or reopen bank statement lines for the selected account.' },
  { key: 'cheques', label: 'Cheque Log', icon: 'i-lucide-scroll-text', description: 'Issued and deposited cheques with clear/bounce lifecycle.' },
  { key: 'vendorBanks', label: 'Vendor Banks', icon: 'i-lucide-wallet-cards', description: 'Vendor-owned bank accounts used for payouts.' },
  { key: 'accountDetails', label: 'Account Details', icon: 'i-lucide-key-round', description: 'Net-banking login references per bank account.' },
  { key: 'trialBalance', label: 'Trial Balance', icon: 'i-lucide-scale', description: 'Read-only trial balance from posted journals.' }
]
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])

const tableRows = computed<Record<AccountingTab, ApiRecord[]>>(() => ({
  bankAccounts: bankAccounts.value.map(item => ({
    id: readText(item, ['id'], ''),
    holder: readText(item, ['accountHolderName']),
    account: readText(item, ['accountNumber']),
    bank: bankName(item.bankId),
    type: optionLabel(accountTypeOptions, item.accountType),
    balance: formatIndianMoney(readNumber(item, ['closingBalance', 'openingBalance'])),
    ledger: ledgerExists(item.ledgerId) ? 'Linked' : 'Missing',
    status: item.active === false ? 'Inactive' : 'Active'
  })),
  bankTransactions: bankTransactions.value.map(item => ({
    id: readText(item, ['id'], ''),
    date: readText(item, ['onDate']),
    bank: bankAccountName(item.bankAccountId),
    type: optionLabel(transactionTypeOptions, item.transactionType),
    mode: optionLabel(transactionModeOptions, item.transactionMode),
    ledger: ledgerName(item.ledgerId),
    reference: readText(item, ['reference']),
    person: readText(item, ['personName']),
    amount: formatIndianMoney(readNumber(item, ['amount']))
  })),
  reconciliation: (readArray(bankReconciliation.value, ['lines']).length ? readArray(bankReconciliation.value, ['lines']) : bankStatement.value).map(item => ({
    id: readText(item, ['id'], ''),
    date: readText(item, ['onDate']),
    description: readText(item, ['description']),
    reference: readText(item, ['reference']),
    debit: formatIndianMoney(readNumber(item, ['debit'])),
    credit: formatIndianMoney(readNumber(item, ['credit'])),
    balance: formatIndianMoney(readNumber(item, ['balance'])),
    status: item.reconciled ? 'Reconciled' : 'Open'
  })),
  cheques: chequeLogs.value.map(item => ({
    id: readText(item, ['id'], ''),
    date: readText(item, ['onDate']),
    cheque: readText(item, ['chequeNumber']),
    bank: bankAccountName(item.bankAccountId),
    person: readText(item, ['personName']),
    status: readText(item, ['status']),
    amount: formatIndianMoney(readNumber(item, ['amount']))
  })),
  vendorBanks: vendorBankAccounts.value.map(item => ({
    id: readText(item, ['id'], ''),
    holder: readText(item, ['accountHolderName']),
    account: readText(item, ['accountNumber']),
    vendor: vendorName(item.vendorId),
    bank: bankName(item.bankId),
    ledger: ledgerName(item.ledgerId),
    status: item.active === false ? 'Inactive' : 'Active'
  })),
  accountDetails: bankAccountDetails.value.map(item => ({
    id: readText(item, ['id'], ''),
    bank: bankAccountName(item.bankAccountId),
    customerId: readText(item, ['customerId']),
    userName: readText(item, ['userName']),
    card: readText(item, ['atmCard']),
    status: readText(item, ['status'])
  })),
  trialBalance: trialBalance.value.map(item => ({
    ledger: readText(item, ['ledgerName']),
    group: readText(item, ['ledgerGroup']),
    debit: formatIndianMoney(readNumber(item, ['debit'])),
    credit: formatIndianMoney(readNumber(item, ['credit'])),
    closingDebit: formatIndianMoney(readNumber(item, ['closingDebit'])),
    closingCredit: formatIndianMoney(readNumber(item, ['closingCredit']))
  }))
}))

const columns: Record<AccountingTab, Array<{ key: string, label: string }>> = {
  bankAccounts: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'bank', label: 'Bank' },
    { key: 'type', label: 'Type' },
    { key: 'balance', label: 'Balance' },
    { key: 'ledger', label: 'Ledger Link' },
    { key: 'status', label: 'Status' }
  ],
  bankTransactions: [
    { key: 'date', label: 'Date' },
    { key: 'bank', label: 'Bank Account' },
    { key: 'type', label: 'Type' },
    { key: 'mode', label: 'Mode' },
    { key: 'ledger', label: 'Against Ledger' },
    { key: 'reference', label: 'Reference' },
    { key: 'person', label: 'Person' },
    { key: 'amount', label: 'Amount' }
  ],
  reconciliation: [
    { key: 'date', label: 'Date' },
    { key: 'description', label: 'Description' },
    { key: 'reference', label: 'Reference' },
    { key: 'debit', label: 'Debit' },
    { key: 'credit', label: 'Credit' },
    { key: 'balance', label: 'Balance' },
    { key: 'status', label: 'Status' }
  ],
  cheques: [
    { key: 'date', label: 'Date' },
    { key: 'cheque', label: 'Cheque' },
    { key: 'bank', label: 'Bank Account' },
    { key: 'person', label: 'Person' },
    { key: 'status', label: 'Status' },
    { key: 'amount', label: 'Amount' }
  ],
  vendorBanks: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'vendor', label: 'Vendor' },
    { key: 'bank', label: 'Bank' },
    { key: 'ledger', label: 'Ledger' },
    { key: 'status', label: 'Status' }
  ],
  accountDetails: [
    { key: 'bank', label: 'Bank Account' },
    { key: 'customerId', label: 'Customer ID' },
    { key: 'userName', label: 'User Name' },
    { key: 'card', label: 'ATM Card' },
    { key: 'status', label: 'Status' }
  ],
  trialBalance: [
    { key: 'ledger', label: 'Ledger' },
    { key: 'group', label: 'Group' },
    { key: 'debit', label: 'Debit' },
    { key: 'credit', label: 'Credit' },
    { key: 'closingDebit', label: 'Closing Debit' },
    { key: 'closingCredit', label: 'Closing Credit' }
  ]
}
const currentColumns = computed(() => columns[activeTab.value])
const currentRows = computed(() => tableRows.value[activeTab.value])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return currentRows.value
  return currentRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

function resolveCompanyId() {
  const companyId = readText(setupStatus.value, ['companyId'], '')
    || readText(stores.value[0], ['companyId'], '')
    || readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving accounting masters.')
  return companyId
}

function resolveStoreIds() {
  const store = stores.value[0]
  const storeGroupId = readText(setupStatus.value, ['storeGroupId'], '') || readText(store, ['storeGroupId'], '')
  const storeId = readText(setupStatus.value, ['storeId'], '') || readText(store, ['id'], '')
  if (!storeGroupId || !storeId) throw new Error('Run quick setup before saving bank transactions.')
  return { storeGroupId, storeId }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [
      setupData, companyData, storeData, ledgerData, bankData, bankAccountData,
      transactionData, chequeData, vendorBankData, accountDetailData, vendorData, trialData
    ] = await Promise.allSettled([
      get<unknown>('setup/status'),
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('ledgers'),
      get<unknown>('banks'),
      get<unknown>('bank-accounts'),
      get<unknown>('accounting/bank-transactions'),
      get<unknown>('cheque-logs'),
      get<unknown>('vendor-bank-accounts'),
      get<unknown>('bank-account-details'),
      get<unknown>('vendors'),
      get<unknown>('accounting/trial-balance')
    ])
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (bankData.status === 'fulfilled') banks.value = toRows(bankData.value)
    if (bankAccountData.status === 'fulfilled') bankAccounts.value = toRows(bankAccountData.value)
    if (transactionData.status === 'fulfilled') bankTransactions.value = toRows(transactionData.value)
    if (chequeData.status === 'fulfilled') chequeLogs.value = toRows(chequeData.value)
    if (vendorBankData.status === 'fulfilled') vendorBankAccounts.value = toRows(vendorBankData.value)
    if (accountDetailData.status === 'fulfilled') bankAccountDetails.value = toRows(accountDetailData.value)
    if (vendorData.status === 'fulfilled') vendors.value = toRows(vendorData.value)
    if (trialData.status === 'fulfilled') trialBalance.value = toRows(trialData.value)

    if (!selectedBankAccountId.value && bankAccounts.value.length) selectedBankAccountId.value = readText(bankAccounts.value[0], ['id'], '')

    const failedReasons = [ledgerData, bankData, bankAccountData, transactionData, chequeData, vendorBankData, accountDetailData, trialData]
      .filter((item): item is PromiseRejectedResult => item.status === 'rejected')
      .map(item => item.reason instanceof Error ? item.reason.message : String(item.reason))
    if (failedReasons.length) error.value = failedReasons[0]
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load accounting masters.'
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

  try {
    bankStatement.value = toRows(await get<unknown>(`accounting/bank-statement/${selectedBankAccountId.value}`))
    bankReconciliation.value = await get<ApiRecord>(`accounting/bank-reconciliation/${selectedBankAccountId.value}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load bank statement.'
  }
}

function startCreate() {
  editMode.value = 'create'
  resetActiveForm()
  message.value = ''
  error.value = ''
  formOpen.value = true
}

function startEdit(item: ApiRecord | null) {
  if (!item) return
  editMode.value = 'edit'
  resetActiveForm(item)
  message.value = ''
  error.value = ''
  formOpen.value = true
}

function resetActiveForm(item: ApiRecord | null = null) {
  if (activeTab.value === 'bankAccounts') {
    Object.assign(bankAccountForm, emptyBankAccountForm(), {
      id: readText(item, ['id'], ''),
      accountNumber: readText(item, ['accountNumber'], ''),
      accountHolderName: readText(item, ['accountHolderName'], ''),
      bankId: readText(item, ['bankId'], '') || bankSelectItems.value[0]?.value || '',
      accountType: Number(item?.accountType ?? 1),
      branch: readText(item, ['branch'], ''),
      ifsCode: readText(item, ['ifsCode'], ''),
      openingDate: localDateInput(item?.openingDate),
      active: item ? item.active !== false : true,
      closingDate: item?.closingDate ? localDateInput(item.closingDate) : '',
      openingBalance: readNumber(item, ['openingBalance']),
      closingBalance: readNumber(item, ['closingBalance'])
    })
  } else if (activeTab.value === 'bankTransactions') {
    const bankAccountId = readText(item, ['bankAccountId'], '') || selectedBankAccountId.value || bankAccountSelectItems.value[0]?.value || ''
    Object.assign(transactionForm, emptyTransactionForm(), {
      id: readText(item, ['id'], ''),
      bankAccountId,
      onDate: localDateInput(item?.onDate),
      transactionType: Number(item?.transactionType ?? 0),
      transactionMode: Number(item?.transactionMode ?? 4),
      ledgerId: readText(item, ['ledgerId'], '') || transactionLedgerSelectItems.value[0]?.value || '',
      narration: readText(item, ['narration'], ''),
      reference: readText(item, ['reference'], ''),
      amount: readNumber(item, ['amount']),
      personName: readText(item, ['personName'], '')
    })
  } else if (activeTab.value === 'cheques') {
    Object.assign(chequeForm, emptyChequeForm(), {
      id: readText(item, ['id'], ''),
      bankAccountId: readText(item, ['bankAccountId'], '') || selectedBankAccountId.value || bankAccountSelectItems.value[0]?.value || '',
      chequeNumber: readText(item, ['chequeNumber'], ''),
      onDate: localDateInput(item?.onDate),
      chequeDate: item?.chequeDate ? localDateInput(item.chequeDate) : localDateInput(),
      narration: readText(item, ['narration'], ''),
      chequeBank: readText(item, ['chequeBank'], ''),
      amount: readNumber(item, ['amount']),
      personName: readText(item, ['personName'], ''),
      status: readText(item, ['status'], 'Issued'),
      inHouse: Boolean(item?.inHouse)
    })
  } else if (activeTab.value === 'vendorBanks') {
    Object.assign(vendorBankForm, emptyVendorBankForm(), {
      id: readText(item, ['id'], ''),
      accountNumber: readText(item, ['accountNumber'], ''),
      accountHolderName: readText(item, ['accountHolderName'], ''),
      bankId: readText(item, ['bankId'], '') || bankSelectItems.value[0]?.value || '',
      accountType: Number(item?.accountType ?? 1),
      branch: readText(item, ['branch'], ''),
      ifsCode: readText(item, ['ifsCode'], ''),
      openingDate: localDateInput(item?.openingDate),
      active: item ? item.active !== false : true,
      closingDate: item?.closingDate ? localDateInput(item.closingDate) : '',
      openingBalance: readNumber(item, ['openingBalance']),
      closingBalance: readNumber(item, ['closingBalance']),
      ledgerId: readText(item, ['ledgerId'], '') || ledgerSelectItems.value[0]?.value || '',
      vendorId: readText(item, ['vendorId'], '')
    })
  } else if (activeTab.value === 'accountDetails') {
    Object.assign(accountDetailForm, emptyAccountDetailForm(), {
      id: readText(item, ['id'], ''),
      bankAccountId: readText(item, ['bankAccountId'], '') || selectedBankAccountId.value || bankAccountSelectItems.value[0]?.value || '',
      customerId: readText(item, ['customerId'], ''),
      userName: readText(item, ['userName'], ''),
      atmCard: readText(item, ['atmCard'], ''),
      expireDate: item?.expireDate ? localDateInput(item.expireDate) : '',
      status: readText(item, ['status'], 'Active')
    })
  }
}

function buildPayload(): { endpoint: string, payload: Record<string, unknown> } {
  const companyId = resolveCompanyId()

  if (activeTab.value === 'bankAccounts') {
    if (!bankAccountForm.accountHolderName.trim()) throw new Error('Enter account holder name.')
    if (!bankAccountForm.accountNumber.trim()) throw new Error('Enter account number.')
    if (!bankAccountForm.bankId) throw new Error('Select bank.')
    return {
      endpoint: 'bank-accounts',
      payload: {
        id: bankAccountForm.id || null,
        companyId,
        accountNumber: bankAccountForm.accountNumber.trim(),
        accountHolderName: bankAccountForm.accountHolderName.trim(),
        bankId: bankAccountForm.bankId,
        accountType: Number(bankAccountForm.accountType),
        branch: bankAccountForm.branch.trim() || null,
        ifsCode: bankAccountForm.ifsCode.trim().toUpperCase() || null,
        openingBalance: Number(bankAccountForm.openingBalance || 0),
        closingBalance: Number(bankAccountForm.closingBalance || 0),
        openingDate: toApiDate(bankAccountForm.openingDate),
        active: Boolean(bankAccountForm.active),
        closingDate: nullableApiDate(bankAccountForm.closingDate)
      }
    }
  }

  if (activeTab.value === 'bankTransactions') {
    if (!transactionForm.bankAccountId) throw new Error('Select bank account.')
    if (!transactionForm.ledgerId) throw new Error('Select contra ledger.')
    const { storeGroupId, storeId } = resolveStoreIds()
    return {
      endpoint: 'accounting/bank-transactions',
      payload: {
        id: transactionForm.id || null,
        companyId,
        storeGroupId,
        storeId,
        bankAccountId: transactionForm.bankAccountId,
        ledgerId: transactionForm.ledgerId,
        onDate: toApiDate(transactionForm.onDate),
        transactionType: Number(transactionForm.transactionType),
        transactionMode: Number(transactionForm.transactionMode),
        amount: Number(transactionForm.amount || 0),
        reference: transactionForm.reference.trim() || null,
        personName: transactionForm.personName.trim() || null,
        narration: transactionForm.narration.trim() || null
      }
    }
  }

  if (activeTab.value === 'cheques') {
    if (!chequeForm.bankAccountId) throw new Error('Select bank account.')
    if (!chequeForm.chequeNumber.trim()) throw new Error('Enter cheque number.')
    return {
      endpoint: 'cheque-logs',
      payload: {
        id: chequeForm.id || null,
        companyId,
        bankAccountId: chequeForm.bankAccountId,
        chequeNumber: chequeForm.chequeNumber.trim(),
        cheequeNumber: chequeForm.chequeNumber.trim(),
        onDate: toApiDate(chequeForm.onDate),
        chequeDate: nullableApiDate(chequeForm.chequeDate),
        narration: chequeForm.narration.trim() || null,
        chequeBank: chequeForm.chequeBank.trim() || null,
        amount: Number(chequeForm.amount || 0),
        personName: chequeForm.personName.trim() || null,
        status: chequeForm.status.trim() || 'Issued',
        inHouse: Boolean(chequeForm.inHouse)
      }
    }
  }

  if (activeTab.value === 'vendorBanks') {
    if (!vendorBankForm.accountHolderName.trim()) throw new Error('Enter account holder name.')
    if (!vendorBankForm.accountNumber.trim()) throw new Error('Enter account number.')
    if (!vendorBankForm.bankId) throw new Error('Select bank.')
    if (!vendorBankForm.ledgerId) throw new Error('Select ledger.')
    return {
      endpoint: 'vendor-bank-accounts',
      payload: {
        id: vendorBankForm.id || null,
        companyId,
        accountNumber: vendorBankForm.accountNumber.trim(),
        accountHolderName: vendorBankForm.accountHolderName.trim(),
        bankId: vendorBankForm.bankId,
        ledgerId: vendorBankForm.ledgerId,
        accountType: Number(vendorBankForm.accountType),
        branch: vendorBankForm.branch.trim() || null,
        ifsCode: vendorBankForm.ifsCode.trim().toUpperCase() || null,
        openingDate: toApiDate(vendorBankForm.openingDate),
        closingDate: nullableApiDate(vendorBankForm.closingDate),
        openingBalance: Number(vendorBankForm.openingBalance || 0),
        closingBalance: Number(vendorBankForm.closingBalance || 0),
        active: Boolean(vendorBankForm.active),
        vendorId: vendorBankForm.vendorId || null
      }
    }
  }

  if (!accountDetailForm.bankAccountId) throw new Error('Select bank account.')
  return {
    endpoint: 'bank-account-details',
    payload: {
      id: accountDetailForm.id || null,
      companyId,
      bankAccountId: accountDetailForm.bankAccountId,
      customerId: accountDetailForm.customerId.trim() || null,
      userName: accountDetailForm.userName.trim() || null,
      atmCard: accountDetailForm.atmCard.trim() || null,
      expireDate: nullableApiDate(accountDetailForm.expireDate),
      status: accountDetailForm.status.trim() || 'Active'
    }
  }
}

async function saveActiveForm() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    const { endpoint, payload } = buildPayload()
    const id = payload.id as string | null

    if (editMode.value === 'edit' && id) {
      await put<unknown>(`${endpoint}/${id}`, payload)
      message.value = `${singularLabel(activeTab.value)} updated.`
    } else {
      await post<unknown>(endpoint, payload)
      message.value = `${singularLabel(activeTab.value)} saved.`
    }

    formOpen.value = false
    await refresh()
    if (activeTab.value === 'reconciliation' || activeTab.value === 'bankTransactions') await loadBankStatement()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to save ${singularLabel(activeTab.value).toLowerCase()}.`
  } finally {
    saving.value = false
  }
}

function askDelete(item: ApiRecord | null) {
  if (!item) return
  const id = readText(item, ['id'], '')
  if (!id) return
  if (!window.confirm(`Delete this ${singularLabel(activeTab.value).toLowerCase()}?`)) return
  confirmDelete(id)
}

async function confirmDelete(id: string) {
  error.value = ''
  message.value = ''
  try {
    const endpoint: Record<string, string> = {
      bankAccounts: 'bank-accounts',
      bankTransactions: 'accounting/bank-transactions',
      cheques: 'cheque-logs',
      vendorBanks: 'vendor-bank-accounts',
      accountDetails: 'bank-account-details'
    }
    const resource = endpoint[activeTab.value]
    if (!resource) return
    await del<unknown>(`${resource}/${id}`)
    message.value = `${singularLabel(activeTab.value)} deleted.`
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to delete ${singularLabel(activeTab.value).toLowerCase()}.`
  }
}

async function markStatementReconciled(item: ApiRecord | null) {
  if (!item?.id) return
  reconciling.value = true
  error.value = ''
  try {
    await post<unknown>(`accounting/bank-statement-lines/${item.id}/reconcile`, {
      bankTransactionId: item.bankTransactionId || null,
      reconciledAt: toApiDate(localDateInput()),
      reconciliationReference: item.reference || '',
      remarks: 'Marked reconciled from Books accounting workspace'
    })
    message.value = 'Bank line reconciled.'
    await loadBankStatement()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reconcile bank line.'
  } finally {
    reconciling.value = false
  }
}

async function undoStatementReconciliation(item: ApiRecord | null) {
  if (!item?.id) return
  reconciling.value = true
  error.value = ''
  try {
    await post<unknown>(`accounting/bank-statement-lines/${item.id}/unreconcile`, { remarks: 'Reconciliation reopened from Books accounting workspace' })
    message.value = 'Reconciliation reopened.'
    await loadBankStatement()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reopen bank line.'
  } finally {
    reconciling.value = false
  }
}

async function updateChequeStatus(item: ApiRecord | null, status: string) {
  if (!item?.id) return
  reconciling.value = true
  error.value = ''
  try {
    await post<unknown>(`accounting/cheque-logs/${item.id}/lifecycle`, {
      status,
      actionDate: toApiDate(localDateInput()),
      remarks: `Marked ${status.toLowerCase()} from Books accounting workspace`,
      bankTransactionId: item.bankTransactionId || null
    })
    message.value = `Cheque marked ${status.toLowerCase()}.`
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update cheque lifecycle.'
  } finally {
    reconciling.value = false
  }
}

watch(selectedBankAccountId, () => {
  if (activeTab.value === 'reconciliation') loadBankStatement()
})

watch(activeTab, (tab) => {
  search.value = ''
  if (tab === 'reconciliation') loadBankStatement()
})

onMounted(refresh)
</script>

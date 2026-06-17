<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type AccountingTab = 'trial' | 'ledgerBook' | 'ledgers' | 'parties' | 'bankAccounts' | 'transactions' | 'reconciliation' | 'ledgerSync' | 'cheques' | 'vendorBanks' | 'accountDetails'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const ledgerGroups = ref<any[]>([])
const ledgers = ref<any[]>([])
const parties = ref<any[]>([])
const banks = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const bankTransactions = ref<any[]>([])
const chequeLogs = ref<any[]>([])
const vendorBankAccounts = ref<any[]>([])
const bankAccountDetails = ref<any[]>([])
const vendors = ref<any[]>([])
const trialBalance = ref<any[]>([])
const ledgerStatement = ref<any[]>([])
const bankStatement = ref<any[]>([])
const bankReconciliation = ref<any | null>(null)
const ledgerSyncSummary = ref<any | null>(null)
const reconciling = ref(false)
const selectedLedgerId = ref('')
const selectedBankAccountId = ref('')
const activeTab = ref<AccountingTab>('trial')
const search = ref('')
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const deleting = ref(false)
const defaultsLoading = ref(false)
const formOpen = ref(false)
const editMode = ref<'create' | 'edit'>('create')
const deleteOpen = ref(false)
const pendingDelete = ref<any | null>(null)

const ledgerTypeOptions = [
  { value: 0, label: 'Asset' },
  { value: 1, label: 'Cash' },
  { value: 2, label: 'Bank Account' },
  { value: 3, label: 'Loan' },
  { value: 4, label: 'Expenses' },
  { value: 7, label: 'Income' },
  { value: 11, label: 'Sale' },
  { value: 12, label: 'Stock Item' },
  { value: 15, label: 'Capital Account' },
  { value: 16, label: 'Current Asset' },
  { value: 18, label: 'Current Liability' },
  { value: 20, label: 'Sundry Debtor' },
  { value: 21, label: 'Sundry Creditor' },
  { value: 23, label: 'Suspense' }
]

const partyTypeOptions = [
  { value: 0, label: 'Customer' },
  { value: 1, label: 'Supplier' },
  { value: 2, label: 'Employee' },
  { value: 3, label: 'Vendor' },
  { value: 4, label: 'Debtor' },
  { value: 5, label: 'Creditor' },
  { value: 6, label: 'Others' }
]

const accountTypeOptions = [
  { value: 0, label: 'Saving' },
  { value: 1, label: 'Current' },
  { value: 2, label: 'Cash Credit' },
  { value: 3, label: 'Over Draft' },
  { value: 4, label: 'Others' },
  { value: 5, label: 'Loan' },
  { value: 6, label: 'CF' }
]

const transactionTypeOptions = [
  { value: 0, label: 'Deposit' },
  { value: 1, label: 'Withdraw' }
]

const transactionModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Cheque' },
  { value: 2, label: 'NEFT' },
  { value: 3, label: 'RTGS' },
  { value: 4, label: 'UPI' },
  { value: 5, label: 'Net Banking' },
  { value: 6, label: 'IMPS' },
  { value: 7, label: 'DD' },
  { value: 8, label: 'ATM' },
  { value: 9, label: 'Swipe' },
  { value: 10, label: 'Other' }
]

const tabs = [
  { key: 'trial' as const, label: 'Trial Balance', icon: 'i-lucide-scale' },
  { key: 'ledgerBook' as const, label: 'Ledger Book', icon: 'i-lucide-list-tree' },
  { key: 'ledgers' as const, label: 'Ledgers', icon: 'i-lucide-book-open' },
  { key: 'parties' as const, label: 'Parties', icon: 'i-lucide-users' },
  { key: 'bankAccounts' as const, label: 'Bank Accounts', icon: 'i-lucide-landmark' },
  { key: 'transactions' as const, label: 'Bank Transactions', icon: 'i-lucide-arrow-left-right' },
  { key: 'reconciliation' as const, label: 'Bank Reconciliation', icon: 'i-lucide-list-checks' },
  { key: 'ledgerSync' as const, label: 'Ledger Sync', icon: 'i-lucide-link' },
  { key: 'cheques' as const, label: 'Cheque Log', icon: 'i-lucide-scroll-text' },
  { key: 'vendorBanks' as const, label: 'Vendor Banks', icon: 'i-lucide-wallet-cards' },
  { key: 'accountDetails' as const, label: 'Account Details', icon: 'i-lucide-key-round' }
]

const ledgerForm = reactive<any>(emptyLedger())
const partyForm = reactive<any>(emptyParty())
const bankAccountForm = reactive<any>(emptyBankAccount())
const transactionForm = reactive<any>(emptyTransaction())
const chequeForm = reactive<any>(emptyCheque())
const vendorBankForm = reactive<any>(emptyVendorBank())
const accountDetailForm = reactive<any>(emptyAccountDetail())

const companyId = computed(() => workspace.companyId.value || setupStatus.value?.companyId || companies.value[0]?.id || '')
const currentTab = computed(() => tabs.find((tab) => tab.key === activeTab.value) || tabs[0])
const canCreate = computed(() => !['trial', 'ledgerBook', 'reconciliation', 'ledgerSync'].includes(activeTab.value))
const formTitle = computed(() => `${editMode.value === 'edit' ? 'Edit' : 'New'} ${singularLabel(activeTab.value)}`)

const ledgerGroupOptions = computed(() => ledgerGroups.value.map((item) => ({ value: item.id, label: item.name || 'Ledger Group' })))
const ledgerOptions = computed(() => ledgers.value.map((item) => ({ value: item.id, label: item.name || 'Ledger' })))
const bankOptions = computed(() => banks.value.map((item) => ({ value: item.id, label: item.name || 'Bank' })))
const bankAccountOptions = computed(() => bankAccounts.value.map((item) => ({ value: item.id, label: bankAccountLabel(item) })))
const vendorOptions = computed(() => vendors.value.map((item) => ({ value: item.id, label: item.name || item.vendorName || 'Vendor' })))
const currentStore = computed(() => stores.value.find((item) => item.id === workspace.storeId.value) || stores.value.find((item) => item.id === setupStatus.value?.storeId) || stores.value[0])
const currentStoreGroupId = computed(() => workspace.storeGroupId.value || setupStatus.value?.storeGroupId || currentStore.value?.storeGroupId || '')
const currentStoreId = computed(() => workspace.storeId.value || setupStatus.value?.storeId || currentStore.value?.id || '')
const formContentClass = computed(() => {
  if (['transactions', 'cheques', 'vendorBanks', 'accountDetails'].includes(activeTab.value)) {
    return 'w-[calc(100vw-2rem)] sm:max-w-5xl lg:max-w-6xl'
  }
  return 'w-[calc(100vw-2rem)] sm:max-w-3xl'
})
const transactionLedgerOptions = computed(() => {
  const selectedBank = bankAccounts.value.find((item) => item.id === transactionForm.bankAccountId)
  return ledgerOptions.value.filter((item) => item.value !== selectedBank?.ledgerId)
})

const metrics = computed(() => [
  { label: 'Ledgers', value: ledgers.value.length, meta: 'Chart of accounts', icon: 'i-lucide-book-open', color: 'primary' },
  { label: 'Parties', value: parties.value.length, meta: 'Customer, vendor, employee', icon: 'i-lucide-users', color: 'success' },
  { label: 'Bank Balance', value: money(bankAccounts.value.reduce((sum, item) => sum + Number(item.closingBalance || item.openingBalance || 0), 0)), meta: 'All active accounts', icon: 'i-lucide-landmark', color: 'warning' },
  { label: 'Cheques', value: chequeLogs.value.length, meta: 'Issued and deposited', icon: 'i-lucide-scroll-text', color: 'neutral' },
  { label: 'Open Bank Lines', value: bankReconciliation.value?.openLineCount || 0, meta: 'Pending reconciliation', icon: 'i-lucide-list-checks', color: 'error' },
  { label: 'Ledger Sync Issues', value: ledgerSyncSummary.value?.issueCount || 0, meta: 'Party/bank ledger links', icon: 'i-lucide-link', color: ledgerSyncSummary.value?.issueCount ? 'error' : 'success' }
])

const rows = computed(() => {
  if (activeTab.value === 'trial') {
    return trialBalance.value.map((item) => ({
      id: item.ledgerId,
      ledger: item.ledgerName,
      group: item.ledgerGroup,
      debit: money(item.debit),
      credit: money(item.credit),
      closingDebit: money(item.closingDebit),
      closingCredit: money(item.closingCredit),
      raw: item
    }))
  }

  if (activeTab.value === 'ledgerBook') {
    return ledgerStatement.value.map((item) => ({
      id: item.id || `${item.entryNumber}-${item.onDate}`,
      date: formatDate(item.onDate),
      entry: item.entryNumber || '-',
      source: item.sourceType || '-',
      reference: item.referenceNumber || '-',
      particulars: item.particulars || '-',
      debit: money(item.debit),
      credit: money(item.credit),
      balance: `${money(item.balance)} ${item.balanceType || ''}`.trim(),
      raw: item
    }))
  }

  if (activeTab.value === 'ledgers') {
    return ledgers.value.map((item) => ({
      id: item.id,
      name: item.name,
      group: ledgerGroupName(item.ledgerGroupId),
      type: optionLabel(ledgerTypeOptions, item.ledgerType),
      opening: money(item.openingBalance),
      raw: item
    }))
  }

  if (activeTab.value === 'parties') {
    return parties.value.map((item) => ({
      id: item.id,
      name: item.name,
      category: optionLabel(partyTypeOptions, item.category),
      phone: item.phone || '-',
      tax: item.gstin || item.pan || '-',
      raw: item
    }))
  }

  if (activeTab.value === 'bankAccounts') {
    return bankAccounts.value.map((item) => ({
      id: item.id,
      holder: item.accountHolderName,
      account: item.accountNumber,
      bank: bankName(item.bankId),
      type: optionLabel(accountTypeOptions, item.accountType),
      balance: money(item.closingBalance || item.openingBalance),
      status: item.active ? 'Active' : 'Inactive',
      raw: item
    }))
  }

  if (activeTab.value === 'transactions') {
    return bankTransactions.value.map((item) => ({
      id: item.id,
      date: formatDate(item.onDate),
      bank: bankAccountName(item.bankAccountId),
      type: optionLabel(transactionTypeOptions, item.transactionType),
      mode: optionLabel(transactionModeOptions, item.transactionMode),
      ledger: ledgerName(item.ledgerId),
      reference: item.reference || '-',
      person: item.personName || '-',
      amount: money(item.amount),
      raw: item
    }))
  }

  if (activeTab.value === 'reconciliation') {
    return (bankReconciliation.value?.lines || bankStatement.value).map((item: any) => ({
      id: item.id,
      date: formatDate(item.onDate),
      description: item.description || '-',
      reference: item.reference || '-',
      debit: money(item.debit),
      credit: money(item.credit),
      balance: money(item.balance),
      status: item.reconciled ? 'Reconciled' : 'Open',
      reconciledAt: item.reconciledAt ? formatDate(item.reconciledAt) : '-',
      raw: item
    }))
  }

  if (activeTab.value === 'ledgerSync') {
    return (ledgerSyncSummary.value?.issues || []).map((item: any) => ({
      id: item.entityId || `${item.area}-${item.entityName}`,
      area: item.area || '-',
      entity: item.entityName || '-',
      severity: item.severity || '-',
      message: item.message || '-',
      action: item.fixAction || '-',
      raw: item
    }))
  }

  if (activeTab.value === 'cheques') {
    return chequeLogs.value.map((item) => ({
      id: item.id,
      date: formatDate(item.onDate),
      cheque: item.chequeNumber || item.cheequeNumber || '-',
      bank: bankAccountName(item.bankAccountId),
      person: item.personName || '-',
      status: item.status || '-',
      amount: money(item.amount),
      raw: item
    }))
  }

  if (activeTab.value === 'vendorBanks') {
    return vendorBankAccounts.value.map((item) => ({
      id: item.id,
      holder: item.accountHolderName,
      account: item.accountNumber,
      vendor: vendorName(item.vendorId),
      bank: bankName(item.bankId),
      ledger: ledgerName(item.ledgerId),
      status: item.active ? 'Active' : 'Inactive',
      raw: item
    }))
  }

  return bankAccountDetails.value.map((item) => ({
    id: item.id,
    bank: bankAccountName(item.bankAccountId),
    customerId: item.customerId || '-',
    userName: item.userName || '-',
    card: item.atmCard || '-',
    status: item.status || '-',
    raw: item
  }))
})

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return rows.value
  }

  return rows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const columns = computed<TableColumn<any>[]>(() => {
  const action = actionColumn()
  if (activeTab.value === 'trial') {
    return [
      { accessorKey: 'ledger', header: 'Ledger' },
      { accessorKey: 'group', header: 'Group' },
      { accessorKey: 'debit', header: 'Debit' },
      { accessorKey: 'credit', header: 'Credit' },
      { accessorKey: 'closingDebit', header: 'Closing Debit' },
      { accessorKey: 'closingCredit', header: 'Closing Credit' }
    ]
  }

  if (activeTab.value === 'ledgerBook') {
    return [
      { accessorKey: 'date', header: 'Date' },
      { accessorKey: 'entry', header: 'Entry' },
      { accessorKey: 'source', header: 'Source' },
      { accessorKey: 'reference', header: 'Reference' },
      { accessorKey: 'particulars', header: 'Particulars' },
      { accessorKey: 'debit', header: 'Debit' },
      { accessorKey: 'credit', header: 'Credit' },
      { accessorKey: 'balance', header: 'Balance' }
    ]
  }

  if (activeTab.value === 'ledgers') {
    return [
      { accessorKey: 'name', header: 'Ledger' },
      { accessorKey: 'group', header: 'Group' },
      { accessorKey: 'type', header: 'Type' },
      { accessorKey: 'opening', header: 'Opening' },
      action
    ]
  }

  if (activeTab.value === 'parties') {
    return [
      { accessorKey: 'name', header: 'Party' },
      { accessorKey: 'category', header: 'Category' },
      { accessorKey: 'phone', header: 'Phone' },
      { accessorKey: 'tax', header: 'GST/PAN' },
      action
    ]
  }

  if (activeTab.value === 'bankAccounts') {
    return [
      { accessorKey: 'holder', header: 'Holder' },
      { accessorKey: 'account', header: 'Account' },
      { accessorKey: 'bank', header: 'Bank' },
      { accessorKey: 'balance', header: 'Balance' },
      statusColumn(),
      action
    ]
  }

  if (activeTab.value === 'transactions') {
    return [
      { accessorKey: 'date', header: 'Date' },
      { accessorKey: 'bank', header: 'Bank Account' },
      { accessorKey: 'type', header: 'Type' },
      { accessorKey: 'mode', header: 'Mode' },
      { accessorKey: 'ledger', header: 'Against Ledger' },
      { accessorKey: 'reference', header: 'Reference' },
      { accessorKey: 'person', header: 'Person' },
      { accessorKey: 'amount', header: 'Amount' },
      action
    ]
  }

  if (activeTab.value === 'reconciliation') {
    return [
      { accessorKey: 'date', header: 'Date' },
      { accessorKey: 'description', header: 'Description' },
      { accessorKey: 'reference', header: 'Reference' },
      { accessorKey: 'debit', header: 'Debit' },
      { accessorKey: 'credit', header: 'Credit' },
      { accessorKey: 'balance', header: 'Balance' },
      { accessorKey: 'status', header: 'Status' },
      { accessorKey: 'reconciledAt', header: 'Reconciled On' },
      reconciliationActionColumn()
    ]
  }

  if (activeTab.value === 'ledgerSync') {
    return [
      { accessorKey: 'area', header: 'Area' },
      { accessorKey: 'entity', header: 'Party / Bank Account' },
      { accessorKey: 'severity', header: 'Severity' },
      { accessorKey: 'message', header: 'Issue' },
      { accessorKey: 'action', header: 'Fix Action' }
    ]
  }

  if (activeTab.value === 'cheques') {
    return [
      { accessorKey: 'date', header: 'Date' },
      { accessorKey: 'cheque', header: 'Cheque' },
      { accessorKey: 'bank', header: 'Bank Account' },
      { accessorKey: 'person', header: 'Person' },
      { accessorKey: 'status', header: 'Status' },
      { accessorKey: 'amount', header: 'Amount' },
      action
    ]
  }

  if (activeTab.value === 'vendorBanks') {
    return [
      { accessorKey: 'holder', header: 'Holder' },
      { accessorKey: 'account', header: 'Account' },
      { accessorKey: 'vendor', header: 'Vendor' },
      { accessorKey: 'bank', header: 'Bank' },
      { accessorKey: 'ledger', header: 'Ledger' },
      statusColumn(),
      action
    ]
  }

  return [
    { accessorKey: 'bank', header: 'Bank Account' },
    { accessorKey: 'customerId', header: 'Customer ID' },
    { accessorKey: 'userName', header: 'User Name' },
    { accessorKey: 'card', header: 'ATM Card' },
    { accessorKey: 'status', header: 'Status' },
    action
  ]
})

const statementRows = computed(() => bankStatement.value.map((item) => ({
  id: item.id,
  date: formatDate(item.onDate),
  description: item.description || '-',
  reference: item.reference || '-',
  debit: money(item.debit),
  credit: money(item.credit),
  balance: money(item.balance),
  status: item.reconciled ? 'Reconciled' : 'Open',
  reconciledAt: item.reconciledAt ? formatDate(item.reconciledAt) : '-',
  raw: item
})))

const statementColumns = computed<TableColumn<any>[]>(() => [
  { accessorKey: 'date', header: 'Date' },
  { accessorKey: 'description', header: 'Description' },
  { accessorKey: 'reference', header: 'Reference' },
  { accessorKey: 'debit', header: 'Debit' },
  { accessorKey: 'credit', header: 'Credit' },
  { accessorKey: 'balance', header: 'Balance' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'reconciledAt', header: 'Reconciled On' },
  reconciliationActionColumn()
])

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, groupRows, ledgerRows, partyRows, bankRows, bankAccountRows, transactionRows, chequeRows, vendorBankRows, detailRows, vendorRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('ledger-groups'),
      api.list<any>('ledgers'),
      api.list<any>('parties'),
      api.list<any>('banks'),
      api.list<any>('bank-accounts'),
      api.list<any>('accounting/bank-transactions'),
      api.list<any>('cheque-logs'),
      api.list<any>('vendor-bank-accounts'),
      api.list<any>('bank-account-details'),
      api.list<any>('vendors')
    ])

    companies.value = companyRows
    stores.value = storeRows
    ledgerGroups.value = groupRows
    ledgers.value = ledgerRows
    parties.value = partyRows
    banks.value = bankRows
    bankAccounts.value = bankAccountRows
    bankTransactions.value = transactionRows
    chequeLogs.value = chequeRows
    vendorBankAccounts.value = vendorBankRows
    bankAccountDetails.value = detailRows
    vendors.value = vendorRows

    if (!selectedBankAccountId.value && bankAccounts.value.length) {
      selectedBankAccountId.value = bankAccounts.value[0].id
    }

    if (!selectedLedgerId.value && ledgers.value.length) {
      selectedLedgerId.value = ledgers.value[0].id
    }

    await Promise.all([loadTrialBalance(), loadLedgerStatement(), loadBankStatement(), loadLedgerSyncStatus()])
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Accounting refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function loadTrialBalance() {
  const query = companyId.value ? `?companyId=${companyId.value}` : ''
  trialBalance.value = await api.get<any[]>(`accounting/trial-balance${query}`)
}

async function loadLedgerStatement() {
  if (!selectedLedgerId.value) {
    ledgerStatement.value = []
    return
  }

  ledgerStatement.value = await api.get<any[]>(`accounting/ledger-statement/${selectedLedgerId.value}`)
}

async function loadBankStatement() {
  if (!selectedBankAccountId.value) {
    bankStatement.value = []
    bankReconciliation.value = null
    return
  }

  bankStatement.value = await api.get<any[]>(`accounting/bank-statement/${selectedBankAccountId.value}`)
  bankReconciliation.value = await api.get<any>(`accounting/bank-reconciliation/${selectedBankAccountId.value}`)
}

async function loadLedgerSyncStatus() {
  const query = companyId.value ? `?companyId=${companyId.value}` : ''
  ledgerSyncSummary.value = await api.get<any>(`accounting/ledger-sync/status${query}`)
}

async function repairLedgerSync() {
  saving.value = true
  try {
    const query = companyId.value ? `?companyId=${companyId.value}` : ''
    ledgerSyncSummary.value = await api.create<any>(`accounting/ledger-sync/repair${query}`, {})
    feedback.saved(`Ledger sync repaired ${ledgerSyncSummary.value?.repairedCount || 0} link(s)`)
    await refresh()
  } catch (error) {
    feedback.failed('Ledger sync repair failed', error)
  } finally {
    saving.value = false
  }
}

async function seedDefaults() {
  defaultsLoading.value = true
  try {
    await api.create<any>('setup/accounting-defaults', {})
    feedback.saved('Accounting defaults')
    await refresh()
  } catch (error) {
    feedback.failed('Could not create accounting defaults', error)
  } finally {
    defaultsLoading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  resetActiveForm()
  formOpen.value = true
}

function startEdit(item: any) {
  editMode.value = 'edit'
  resetActiveForm(item)
  formOpen.value = true
}

function resetActiveForm(item: any = null) {
  if (activeTab.value === 'ledgers') {
    Object.assign(ledgerForm, emptyLedger(), item || {}, {
      openingDate: dateInput(item?.openingDate),
      ledgerGroupId: item?.ledgerGroupId || ledgerGroups.value[0]?.id || null
    })
  } else if (activeTab.value === 'parties') {
    Object.assign(partyForm, emptyParty(), item || {})
  } else if (activeTab.value === 'bankAccounts') {
    Object.assign(bankAccountForm, emptyBankAccount(), item || {}, {
      openingDate: dateInput(item?.openingDate),
      closingDate: dateInput(item?.closingDate),
      bankId: item?.bankId || banks.value[0]?.id || null
    })
  } else if (activeTab.value === 'transactions') {
    const bankAccountId = item?.bankAccountId || selectedBankAccountId.value || bankAccounts.value[0]?.id || null
    const bankLedger = bankAccounts.value.find((account) => account.id === bankAccountId)?.ledgerId
    Object.assign(transactionForm, emptyTransaction(), item || {}, {
      onDate: dateInput(item?.onDate),
      bankAccountId,
      ledgerId: item?.ledgerId || ledgers.value.find((ledger) => ledger.id !== bankLedger)?.id || null,
      partyId: nullableGuid(item?.partyId) || null
    })
  } else if (activeTab.value === 'cheques') {
    Object.assign(chequeForm, emptyCheque(), item || {}, {
      onDate: dateInput(item?.onDate),
      chequeDate: dateInput(item?.chequeDate),
      bankAccountId: item?.bankAccountId || selectedBankAccountId.value || bankAccounts.value[0]?.id || null
    })
  } else if (activeTab.value === 'vendorBanks') {
    Object.assign(vendorBankForm, emptyVendorBank(), item || {}, {
      openingDate: dateInput(item?.openingDate),
      closingDate: dateInput(item?.closingDate),
      vendorId: nullableGuid(item?.vendorId),
      bankId: item?.bankId || banks.value[0]?.id || null,
      ledgerId: item?.ledgerId || ledgerIdByName('Sundry Creditors Control') || ledgers.value[0]?.id || null
    })
  } else if (activeTab.value === 'accountDetails') {
    Object.assign(accountDetailForm, emptyAccountDetail(), item || {}, {
      expireDate: dateInput(item?.expireDate),
      bankAccountId: item?.bankAccountId || selectedBankAccountId.value || bankAccounts.value[0]?.id || null
    })
  }
}

async function saveActiveForm() {
  saving.value = true
  try {
    const payload = buildPayload()
    const endpoint = endpointFor(activeTab.value)
    const id = payload.id

    if (activeTab.value === 'transactions') {
      if (editMode.value === 'edit' && id) {
        await api.update<any>('accounting/bank-transactions', id, payload)
      } else {
        await api.create<any>('accounting/bank-transactions', payload)
      }
    } else if (editMode.value === 'edit' && id) {
      await api.update<any>(endpoint, id, payload)
    } else {
      await api.create<any>(endpoint, payload)
    }

    feedback.saved(singularLabel(activeTab.value))
    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed(`Could not save ${singularLabel(activeTab.value).toLowerCase()}`, error)
  } finally {
    saving.value = false
  }
}

function buildPayload() {
  if (!companyId.value) {
    throw new Error('Run quick setup before saving accounting records.')
  }

  if (activeTab.value === 'ledgers') {
    return {
      ...ledgerForm,
      companyId: companyId.value,
      ledgerType: Number(ledgerForm.ledgerType),
      openingBalance: Number(ledgerForm.openingBalance || 0),
      openingDate: toApiDate(ledgerForm.openingDate),
      isParty: Boolean(ledgerForm.isParty),
      ledgerGroup: null
    }
  }

  if (activeTab.value === 'parties') {
    return {
      companyId: companyId.value,
      name: String(partyForm.name || '').trim(),
      address: String(partyForm.address || '').trim() || null,
      emailId: String(partyForm.emailId || '').trim() || null,
      phone: String(partyForm.phone || '').trim() || null,
      gstin: String(partyForm.gstin || '').trim().toUpperCase() || null,
      pan: String(partyForm.pan || '').trim().toUpperCase() || null,
      category: Number(partyForm.category),
    }
  }

  if (activeTab.value === 'bankAccounts') {
    return {
      companyId: companyId.value,
      accountNumber: String(bankAccountForm.accountNumber || '').trim(),
      accountHolderName: String(bankAccountForm.accountHolderName || '').trim(),
      bankId: requiredGuid(bankAccountForm.bankId, 'Select bank.'),
      accountType: Number(bankAccountForm.accountType),
      branch: String(bankAccountForm.branch || '').trim() || null,
      ifsCode: String(bankAccountForm.ifsCode || '').trim().toUpperCase() || null,
      openingBalance: Number(bankAccountForm.openingBalance || 0),
      closingBalance: Number(bankAccountForm.closingBalance || 0),
      openingDate: toApiDate(bankAccountForm.openingDate),
      active: Boolean(bankAccountForm.active),
      closingDate: nullableDate(bankAccountForm.closingDate)
    }
  }

  if (activeTab.value === 'transactions') {
    return {
      ...transactionForm,
      companyId: companyId.value,
      storeGroupId: requiredGuid(currentStoreGroupId.value, 'Select store group.'),
      storeId: requiredGuid(currentStoreId.value, 'Select store.'),
      bankAccountId: requiredGuid(transactionForm.bankAccountId, 'Select bank account.'),
      ledgerId: requiredGuid(transactionForm.ledgerId, 'Select contra ledger.'),
      partyId: null,
      onDate: toApiDate(transactionForm.onDate),
      transactionType: Number(transactionForm.transactionType),
      transactionMode: Number(transactionForm.transactionMode),
      amount: Number(transactionForm.amount || 0),
      bankAccount: null
    }
  }

  if (activeTab.value === 'cheques') {
    return {
      ...chequeForm,
      companyId: companyId.value,
      bankAccountId: requiredGuid(chequeForm.bankAccountId, 'Select bank account.'),
      onDate: toApiDate(chequeForm.onDate),
      chequeDate: nullableDate(chequeForm.chequeDate),
      amount: Number(chequeForm.amount || 0),
      cheequeNumber: chequeForm.chequeNumber,
      bankAccount: null
    }
  }

  if (activeTab.value === 'vendorBanks') {
    return {
      ...vendorBankForm,
      companyId: companyId.value,
      vendorId: nullableGuid(vendorBankForm.vendorId) || null,
      bankId: requiredGuid(vendorBankForm.bankId, 'Select bank.'),
      ledgerId: requiredGuid(vendorBankForm.ledgerId, 'Select ledger.'),
      accountType: Number(vendorBankForm.accountType),
      openingDate: toApiDate(vendorBankForm.openingDate),
      closingDate: nullableDate(vendorBankForm.closingDate),
      openingBalance: Number(vendorBankForm.openingBalance || 0),
      closingBalance: Number(vendorBankForm.closingBalance || 0),
      bank: null,
      ledger: null
    }
  }

  return {
    ...accountDetailForm,
    companyId: companyId.value,
    bankAccountId: requiredGuid(accountDetailForm.bankAccountId, 'Select bank account.'),
    expireDate: nullableDate(accountDetailForm.expireDate),
    atmPin: Number(accountDetailForm.atmPin || 0),
    mPin: Number(accountDetailForm.mPin || 0),
    tpin: Number(accountDetailForm.tpin || 0),
    epin: Number(accountDetailForm.epin || 0),
    bankAccount: null
  }
}

function askDelete(item: any) {
  pendingDelete.value = item
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    if (activeTab.value === 'transactions') {
      await api.remove('accounting/bank-transactions', pendingDelete.value.id)
    } else {
      await api.remove(endpointFor(activeTab.value), pendingDelete.value.id)
    }

    feedback.deleted(singularLabel(activeTab.value))
    pendingDelete.value = null
    deleteOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed(`Could not delete ${singularLabel(activeTab.value).toLowerCase()}`, error)
  } finally {
    deleting.value = false
  }
}

async function markStatementReconciled(item: any) {
  if (!item?.id) {
    return
  }

  reconciling.value = true
  try {
    await api.create<any>(`accounting/bank-statement-lines/${item.id}/reconcile`, {
      bankTransactionId: item.bankTransactionId || null,
      reconciledAt: toApiDate(localDateInput()),
      reconciliationReference: item.reference || '',
      remarks: 'Marked reconciled from accounting workspace'
    })
    feedback.saved('Bank reconciliation')
    await loadBankStatement()
  } catch (error) {
    feedback.failed('Could not reconcile bank line', error)
  } finally {
    reconciling.value = false
  }
}

async function undoStatementReconciliation(item: any) {
  if (!item?.id) {
    return
  }

  reconciling.value = true
  try {
    await api.create<any>(`accounting/bank-statement-lines/${item.id}/unreconcile`, { remarks: 'Reconciliation reopened from accounting workspace' })
    feedback.saved('Reconciliation reopened')
    await loadBankStatement()
  } catch (error) {
    feedback.failed('Could not reopen bank line', error)
  } finally {
    reconciling.value = false
  }
}

async function updateChequeStatus(item: any, status: string) {
  if (!item?.id) {
    return
  }

  reconciling.value = true
  try {
    await api.create<any>(`accounting/cheque-logs/${item.id}/lifecycle`, {
      status,
      actionDate: toApiDate(localDateInput()),
      remarks: `Marked ${status.toLowerCase()} from accounting workspace`,
      bankTransactionId: item.bankTransactionId || null
    })
    feedback.saved('Cheque lifecycle')
    await refresh()
  } catch (error) {
    feedback.failed('Could not update cheque lifecycle', error)
  } finally {
    reconciling.value = false
  }
}

function reconciliationActionColumn(): TableColumn<any> {
  return {
    id: 'reconciliationActions',
    header: '',
    cell: ({ row }) => {
      const item = row.original.raw
      return h('div', { class: 'table-action-buttons' }, [
        canEdit.value && !item.reconciled ? h(UButton, {
          color: 'success',
          variant: 'ghost',
          icon: 'i-lucide-check-circle-2',
          label: 'Reconcile',
          loading: reconciling.value,
          onClick: () => markStatementReconciled(item)
        }) : null,
        canEdit.value && item.reconciled ? h(UButton, {
          color: 'warning',
          variant: 'ghost',
          icon: 'i-lucide-rotate-ccw',
          label: 'Reopen',
          loading: reconciling.value,
          onClick: () => undoStatementReconciliation(item)
        }) : null
      ].filter(Boolean))
    }
  }
}

function statusColumn(): TableColumn<any> {
  return {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Active' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.status)
  }
}

function actionColumn(): TableColumn<any> {
  return {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }) : null,
      activeTab.value === 'cheques' && canEdit.value ? h(UButton, {
        color: 'success',
        variant: 'ghost',
        icon: 'i-lucide-badge-check',
        label: 'Clear',
        loading: reconciling.value,
        onClick: () => updateChequeStatus(row.original.raw, 'Cleared')
      }) : null,
      activeTab.value === 'cheques' && canEdit.value ? h(UButton, {
        color: 'warning',
        variant: 'ghost',
        icon: 'i-lucide-ban',
        label: 'Bounce',
        loading: reconciling.value,
        onClick: () => updateChequeStatus(row.original.raw, 'Bounced')
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      }) : null
    ].filter(Boolean))
  }
}

function emptyLedger() {
  return {
    id: '',
    name: '',
    ledgerGroupId: null,
    ledgerType: 4,
    openingDate: localDateInput(),
    openingBalance: 0,
    isParty: false
  }
}

function emptyParty() {
  return {
    id: '',
    name: '',
    address: '',
    emailId: '',
    phone: '',
    gstin: '',
    pan: '',
    category: 6,
  }
}

function emptyBankAccount() {
  return {
    id: '',
    accountNumber: '',
    accountHolderName: '',
    bankId: null,
    accountType: 1,
    branch: '',
    ifsCode: '',
    openingDate: localDateInput(),
    active: true,
    closingDate: '',
    openingBalance: 0,
    closingBalance: 0,
  }
}

function emptyTransaction() {
  return {
    id: '',
    bankAccountId: null,
    onDate: localDateInput(),
    transactionType: 0,
    transactionMode: 4,
    ledgerId: null,
    narration: '',
    reference: '',
    amount: 0,
    personName: ''
  }
}

function emptyCheque() {
  return {
    id: '',
    bankAccountId: null,
    chequeNumber: '',
    onDate: localDateInput(),
    chequeDate: localDateInput(),
    narration: '',
    chequeBank: '',
    amount: 0,
    personName: '',
    status: 'Issued',
    inHouse: false
  }
}

function emptyVendorBank() {
  return {
    id: '',
    accountNumber: '',
    accountHolderName: '',
    bankId: null,
    accountType: 1,
    branch: '',
    ifsCode: '',
    openingDate: localDateInput(),
    active: true,
    closingDate: '',
    openingBalance: 0,
    closingBalance: 0,
    ledgerId: null,
    vendorId: null
  }
}

function emptyAccountDetail() {
  return {
    id: '',
    bankAccountId: null,
    customerId: '',
    userName: '',
    password: '',
    transcationPassword: '',
    extraPassword: '',
    atmPin: 0,
    mPin: 0,
    tpin: 0,
    epin: 0,
    atmCard: '',
    expireDate: '',
    cvv: '',
    status: 'Active'
  }
}

function endpointFor(tab: AccountingTab) {
  return {
    ledgerBook: '',
    ledgers: 'ledgers',
    parties: 'parties',
    bankAccounts: 'bank-accounts',
    transactions: 'bank-transactions',
    reconciliation: '',
    ledgerSync: '',
    cheques: 'cheque-logs',
    vendorBanks: 'vendor-bank-accounts',
    accountDetails: 'bank-account-details',
    trial: ''
  }[tab]
}

function singularLabel(tab: AccountingTab) {
  return {
    trial: 'Trial Balance',
    ledgerBook: 'Ledger Book',
    ledgers: 'Ledger',
    parties: 'Party',
    bankAccounts: 'Bank Account',
    transactions: 'Bank Transaction',
    reconciliation: 'Bank Reconciliation',
    ledgerSync: 'Ledger Sync',
    cheques: 'Cheque Log',
    vendorBanks: 'Vendor Bank Account',
    accountDetails: 'Bank Account Detail'
  }[tab]
}

function bankLedgerId() {
  return ledgerIdByName('Primary Bank Account') || ledgerIdByName('Bank Clearing') || ledgers.value.find((item) => Number(item.ledgerType) === 2)?.id || ledgers.value[0]?.id || null
}

function ledgerIdByName(name: string) {
  return ledgers.value.find((item) => item.name === name)?.id || ''
}

function ledgerGroupName(id: string) {
  return ledgerGroups.value.find((item) => item.id === id)?.name || '-'
}

function ledgerName(id: string) {
  return ledgers.value.find((item) => item.id === id)?.name || '-'
}

function bankName(id: string) {
  return banks.value.find((item) => item.id === id)?.name || '-'
}

function bankAccountName(id: string) {
  const account = bankAccounts.value.find((item) => item.id === id)
  return account ? bankAccountLabel(account) : '-'
}

function bankAccountLabel(account: any) {
  return `${account.accountHolderName || bankName(account.bankId)} - ${account.accountNumber || ''}`.trim()
}

function vendorName(id: string | null | undefined) {
  if (!id) {
    return '-'
  }

  const vendor = vendors.value.find((item) => item.id === id)
  return vendor?.name || vendor?.vendorName || '-'
}

function optionLabel(options: { value: number, label: string }[], value: unknown) {
  return options.find((item) => item.value === Number(value))?.label || '-'
}

function dateInput(value: string | null | undefined) {
  return value ? String(value).slice(0, 10) : localDateInput()
}

function localDateInput(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 10)
}

function toApiDate(value: string) {
  return `${value || localDateInput()}T00:00:00`
}

function nullableDate(value: string | null | undefined) {
  return value ? toApiDate(value) : null
}

function nullableGuid(value: unknown) {
  const text = String(value || '').trim()
  return text && text !== '00000000-0000-0000-0000-000000000000' ? text : ''
}

function requiredGuid(value: unknown, message: string) {
  const id = nullableGuid(value)
  if (!id) {
    throw new Error(message)
  }

  return id
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(Number(value || 0))
}

onMounted(async () => {
  auth.restore()
  await refresh()
})

watch(selectedBankAccountId, async () => {
  await loadBankStatement()
})

watch(selectedLedgerId, async () => {
  await loadLedgerStatement()
})

watch(() => transactionForm.bankAccountId, () => {
  const selectedBank = bankAccounts.value.find((item) => item.id === transactionForm.bankAccountId)
  if (selectedBank?.ledgerId && transactionForm.ledgerId === selectedBank.ledgerId) {
    transactionForm.ledgerId = transactionLedgerOptions.value[0]?.value || null
  }
})

</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Accounting"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Accounting"
        description="Ledgers, parties, bank accounts, transactions, cheques, and statements."
        icon="i-lucide-landmark"
        :primary-label="canCreate ? `New ${singularLabel(activeTab)}` : 'Seed Defaults'"
        :primary-icon="canCreate ? 'i-lucide-plus' : 'i-lucide-sparkles'"
        @primary="canCreate ? startCreate() : seedDefaults()"
      >
        <template #actions>
          <UButton icon="i-lucide-sparkles" color="neutral" variant="subtle" label="Seed Defaults" :loading="defaultsLoading" @click="seedDefaults" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" label="Refresh" :loading="loading" @click="refresh" />
          <UButton v-if="canCreate" icon="i-lucide-plus" :label="`New ${singularLabel(activeTab)}`" @click="startCreate" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.value }}</strong>
              <span>{{ metric.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UiRegisterPanel
        :title="currentTab.label"
        :description="`${filteredRows.length} accounting records`"
        :loading="loading"
        :error="loadError"
        :empty="filteredRows.length === 0"
        empty-title="No accounting records found"
        empty-description="Create accounting defaults or add the first record."
        empty-icon="i-lucide-landmark"
        @retry="refresh"
      >
        <template #actions>
          <div class="planner-card-header">
            <div class="setup-tabs">
              <UButton
                v-for="tab in tabs"
                :key="tab.key"
                :icon="tab.icon"
                :label="tab.label"
                :color="activeTab === tab.key ? 'primary' : 'neutral'"
                :variant="activeTab === tab.key ? 'solid' : 'subtle'"
                size="sm"
                @click="activeTab = tab.key"
              />
            </div>
            <USelect
              v-if="activeTab === 'ledgerBook'"
              v-model="selectedLedgerId"
              :items="ledgerOptions"
              class="w-72 max-w-full"
              placeholder="Select ledger"
            />
            <USelect
              v-if="activeTab === 'reconciliation'"
              v-model="selectedBankAccountId"
              :items="bankAccountOptions"
              class="w-72 max-w-full"
              placeholder="Select bank account"
            />
          </div>
        </template>

        <div v-if="activeTab === 'ledgerSync'" class="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-default bg-muted/30 p-3 text-sm">
          <div>
            <strong>Ledger synchronization</strong>
            <p class="text-muted">{{ ledgerSyncSummary?.partyCount || 0 }} parties · {{ ledgerSyncSummary?.bankAccountCount || 0 }} bank accounts · {{ ledgerSyncSummary?.issueCount || 0 }} issue(s)</p>
          </div>
          <UButton icon="i-lucide-wrench" label="Repair ledger links" :loading="saving" :disabled="!canEdit || !(ledgerSyncSummary?.issueCount || 0)" @click="repairLedgerSync" />
        </div>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search accounting records"
          :loading="loading"
          refresh-label="Sync"
          :create-label="canCreate ? `New ${singularLabel(activeTab)}` : undefined"
          @refresh="refresh"
          @create="canCreate ? startCreate() : undefined"
        />

        <div class="planner-table-wrap">
          <UTable :data="filteredRows" :columns="columns" />
        </div>
      </UiRegisterPanel>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Bank Statement</h2>
              <p>{{ statementRows.length }} lines · {{ bankReconciliation?.openLineCount || 0 }} open · {{ bankReconciliation?.reconciledLineCount || 0 }} reconciled</p>
            </div>
            <USelect v-model="selectedBankAccountId" :items="bankAccountOptions" class="w-72 max-w-full" />
          </div>
        </template>

        <UTable
          v-if="statementRows.length"
          :data="statementRows"
          :columns="statementColumns"
        />

        <UiCrudEmptyState
          v-else
          title="No statement lines"
          description="Voucher and bank transaction entries will appear here."
          icon="i-lucide-list"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="formTitle"
        :description="`Save ${singularLabel(activeTab).toLowerCase()} details.`"
        :submit-label="editMode === 'edit' ? 'Update' : 'Save'"
        layout="modal"
        :content-class="formContentClass"
        :loading="saving"
        @submit="saveActiveForm"
      >
        <template v-if="activeTab === 'ledgers'">
          <UFormField label="Ledger name" required>
            <UInput v-model="ledgerForm.name" required />
          </UFormField>
          <UFormField label="Group" required>
            <USelect v-model="ledgerForm.ledgerGroupId" :items="ledgerGroupOptions" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Type">
              <USelect v-model="ledgerForm.ledgerType" :items="ledgerTypeOptions" />
            </UFormField>
            <UFormField label="Opening date">
              <UInput v-model="ledgerForm.openingDate" type="date" />
            </UFormField>
          </div>
          <UFormField label="Opening balance">
            <UInput v-model="ledgerForm.openingBalance" type="number" step="0.01" />
          </UFormField>
        </template>

        <template v-else-if="activeTab === 'parties'">
          <UFormField label="Name" required>
            <UInput v-model="partyForm.name" required />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Category">
              <USelect v-model="partyForm.category" :items="partyTypeOptions" />
            </UFormField>
            <UFormField label="Phone">
              <UInput v-model="partyForm.phone" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Email">
              <UInput v-model="partyForm.emailId" type="email" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="GSTIN">
              <UInput v-model="partyForm.gstin" />
            </UFormField>
            <UFormField label="PAN">
              <UInput v-model="partyForm.pan" />
            </UFormField>
          </div>
          <UFormField label="Address">
            <UTextarea v-model="partyForm.address" autoresize />
          </UFormField>
        </template>

        <template v-else-if="activeTab === 'bankAccounts'">
          <UFormField label="Account holder" required>
            <UInput v-model="bankAccountForm.accountHolderName" required />
          </UFormField>
          <UFormField label="Account number" required>
            <UInput v-model="bankAccountForm.accountNumber" required />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Bank" required>
              <USelect v-model="bankAccountForm.bankId" :items="bankOptions" />
            </UFormField>
            <UFormField label="Account type">
              <USelect v-model="bankAccountForm.accountType" :items="accountTypeOptions" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Opening date">
              <UInput v-model="bankAccountForm.openingDate" type="date" />
            </UFormField>
            <UFormField label="Branch">
              <UInput v-model="bankAccountForm.branch" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="IFSC">
              <UInput v-model="bankAccountForm.ifsCode" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Opening balance">
              <UInput v-model="bankAccountForm.openingBalance" type="number" step="0.01" />
            </UFormField>
            <UFormField label="Closing balance">
              <UInput v-model="bankAccountForm.closingBalance" type="number" step="0.01" />
            </UFormField>
          </div>
          <UCheckbox v-model="bankAccountForm.active" label="Active" />
        </template>

        <template v-else-if="activeTab === 'transactions'">
          <UFormField label="Bank account" required>
            <USelect v-model="transactionForm.bankAccountId" :items="bankAccountOptions" />
          </UFormField>
          <UFormField label="Against ledger" required>
            <USelect v-model="transactionForm.ledgerId" :items="transactionLedgerOptions" placeholder="Select ledger" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Date" required>
              <UInput v-model="transactionForm.onDate" type="date" required />
            </UFormField>
            <UFormField label="Amount" required>
              <UInput v-model="transactionForm.amount" type="number" min="0" step="0.01" required />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Type">
              <USelect v-model="transactionForm.transactionType" :items="transactionTypeOptions" />
            </UFormField>
            <UFormField label="Mode">
              <USelect v-model="transactionForm.transactionMode" :items="transactionModeOptions" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Reference">
              <UInput v-model="transactionForm.reference" />
            </UFormField>
            <UFormField label="Person">
              <UInput v-model="transactionForm.personName" />
            </UFormField>
          </div>
          <UFormField label="Narration">
            <UTextarea v-model="transactionForm.narration" autoresize />
          </UFormField>
        </template>

        <template v-else-if="activeTab === 'cheques'">
          <UFormField label="Bank account" required>
            <USelect v-model="chequeForm.bankAccountId" :items="bankAccountOptions" />
          </UFormField>
          <UFormField label="Cheque number" required>
            <UInput v-model="chequeForm.chequeNumber" required />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Entry date">
              <UInput v-model="chequeForm.onDate" type="date" />
            </UFormField>
            <UFormField label="Cheque date">
              <UInput v-model="chequeForm.chequeDate" type="date" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Person">
              <UInput v-model="chequeForm.personName" />
            </UFormField>
            <UFormField label="Amount">
              <UInput v-model="chequeForm.amount" type="number" step="0.01" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Cheque bank">
              <UInput v-model="chequeForm.chequeBank" />
            </UFormField>
            <UFormField label="Status">
              <UInput v-model="chequeForm.status" />
            </UFormField>
          </div>
          <UFormField label="Narration">
            <UTextarea v-model="chequeForm.narration" autoresize />
          </UFormField>
          <UCheckbox v-model="chequeForm.inHouse" label="In house cheque" />
        </template>

        <template v-else-if="activeTab === 'vendorBanks'">
          <UFormField label="Vendor">
            <USelect v-model="vendorBankForm.vendorId" :items="vendorOptions" placeholder="Select vendor" />
          </UFormField>
          <UFormField label="Account holder" required>
            <UInput v-model="vendorBankForm.accountHolderName" required />
          </UFormField>
          <UFormField label="Account number" required>
            <UInput v-model="vendorBankForm.accountNumber" required />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Bank" required>
              <USelect v-model="vendorBankForm.bankId" :items="bankOptions" />
            </UFormField>
            <UFormField label="Ledger" required>
              <USelect v-model="vendorBankForm.ledgerId" :items="ledgerOptions" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Account type">
              <USelect v-model="vendorBankForm.accountType" :items="accountTypeOptions" />
            </UFormField>
            <UFormField label="IFSC">
              <UInput v-model="vendorBankForm.ifsCode" />
            </UFormField>
          </div>
          <UFormField label="Branch">
            <UInput v-model="vendorBankForm.branch" />
          </UFormField>
          <UCheckbox v-model="vendorBankForm.active" label="Active" />
        </template>

        <template v-else-if="activeTab === 'accountDetails'">
          <UFormField label="Bank account" required>
            <USelect v-model="accountDetailForm.bankAccountId" :items="bankAccountOptions" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Customer ID">
              <UInput v-model="accountDetailForm.customerId" />
            </UFormField>
            <UFormField label="User name">
              <UInput v-model="accountDetailForm.userName" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="ATM card">
              <UInput v-model="accountDetailForm.atmCard" />
            </UFormField>
            <UFormField label="Expire date">
              <UInput v-model="accountDetailForm.expireDate" type="date" />
            </UFormField>
          </div>
          <UFormField label="Status">
            <UInput v-model="accountDetailForm.status" />
          </UFormField>
        </template>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        :title="`Delete ${singularLabel(activeTab)}`"
        :description="`Delete this ${singularLabel(activeTab).toLowerCase()}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>

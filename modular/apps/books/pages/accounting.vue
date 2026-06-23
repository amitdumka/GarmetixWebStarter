<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Books master data</p>
          <h2 class="mt-1 text-2xl font-semibold">Accounting Masters</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Read-only view of ledger groups, ledgers, parties, bank accounts, ledger sync and trial balance. Add/edit actions remain in later audited slices.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UBadge :color="syncTone" variant="subtle">{{ syncLabel }}</UBadge>
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
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search master data" class="sm:w-72" />
      </div>

      <BooksMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No accounting rows found." />
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  accountTypeOptions,
  ledgerTypeOptions,
  optionLabel,
  partyTypeOptions,
  readArray,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'Accounting - Garmetix Books' })

type AccountingTab = 'ledgerGroups' | 'ledgers' | 'parties' | 'bankAccounts' | 'trialBalance' | 'ledgerSync'

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const activeTab = ref<AccountingTab>('ledgers')
const ledgerGroups = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const banks = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const trialBalance = ref<ApiRecord[]>([])
const ledgerSync = ref<ApiRecord | null>(null)

const tabs = [
  { key: 'ledgers' as const, label: 'Ledgers', icon: 'i-lucide-book-open', description: 'Chart of accounts with protected internal party/bank flags hidden.' },
  { key: 'ledgerGroups' as const, label: 'Ledger Groups', icon: 'i-lucide-folder-tree', description: 'Indian accounting ledger groups and categories.' },
  { key: 'parties' as const, label: 'Parties', icon: 'i-lucide-contact-round', description: 'Party masters with ledger link status.' },
  { key: 'bankAccounts' as const, label: 'Bank Accounts', icon: 'i-lucide-landmark', description: 'Bank accounts with ledger link status.' },
  { key: 'trialBalance' as const, label: 'Trial Balance', icon: 'i-lucide-scale', description: 'Read-only trial balance from posted journals.' },
  { key: 'ledgerSync' as const, label: 'Ledger Sync', icon: 'i-lucide-link', description: 'Party and bank ledger synchronization issues.' }
]
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])
const groupName = (id: unknown) => readText(ledgerGroups.value.find(item => item.id === id), ['name'])
const bankName = (id: unknown) => readText(banks.value.find(item => item.id === id), ['name'])
const ledgerExists = (id: unknown) => Boolean(id && ledgers.value.some(item => item.id === id))
const syncIssues = computed(() => readArray(ledgerSync.value, ['issues']))
const syncLabel = computed(() => {
  const count = readNumber(ledgerSync.value, ['issueCount'])
  if (!ledgerSync.value) return 'Sync checking'
  return count > 0 ? `${count} sync issue(s)` : 'Ledger sync clear'
})
const syncTone = computed<'success' | 'warning' | 'neutral'>(() => {
  if (!ledgerSync.value) return 'neutral'
  return readNumber(ledgerSync.value, ['issueCount']) > 0 ? 'warning' : 'success'
})
const cards = computed(() => [
  { label: 'Ledger Groups', value: ledgerGroups.value.length, detail: 'Indian accounting groups' },
  { label: 'Ledgers', value: ledgers.value.length, detail: 'Chart of accounts' },
  { label: 'Parties', value: parties.value.length, detail: 'Customer, vendor, employee and other parties' },
  { label: 'Bank Balance', value: formatIndianMoney(bankAccounts.value.reduce((sum, item) => sum + readNumber(item, ['closingBalance', 'openingBalance']), 0)), detail: 'All bank accounts' }
])
const tableRows = computed<Record<AccountingTab, ApiRecord[]>>(() => ({
  ledgerGroups: ledgerGroups.value.map(item => ({
    name: readText(item, ['name']),
    category: readText(item, ['category']),
    description: readText(item, ['description'])
  })),
  ledgers: ledgers.value.map(item => ({
    name: readText(item, ['name']),
    group: groupName(item.ledgerGroupId),
    type: optionLabel(ledgerTypeOptions, item.ledgerType),
    opening: formatIndianMoney(readNumber(item, ['openingBalance'])),
    status: readText(item, ['active'], 'Active')
  })),
  parties: parties.value.map(item => ({
    name: readText(item, ['name']),
    category: optionLabel(partyTypeOptions, item.category),
    phone: readText(item, ['phone']),
    tax: readText(item, ['gstin', 'pan']),
    ledger: ledgerExists(item.ledgerId) ? 'Linked' : 'Missing'
  })),
  bankAccounts: bankAccounts.value.map(item => ({
    holder: readText(item, ['accountHolderName']),
    account: readText(item, ['accountNumber']),
    bank: bankName(item.bankId),
    type: optionLabel(accountTypeOptions, item.accountType),
    balance: formatIndianMoney(readNumber(item, ['closingBalance', 'openingBalance'])),
    ledger: ledgerExists(item.ledgerId) ? 'Linked' : 'Missing',
    status: item.active === false ? 'Inactive' : 'Active'
  })),
  trialBalance: trialBalance.value.map(item => ({
    ledger: readText(item, ['ledgerName']),
    group: readText(item, ['ledgerGroup']),
    debit: formatIndianMoney(readNumber(item, ['debit'])),
    credit: formatIndianMoney(readNumber(item, ['credit'])),
    closingDebit: formatIndianMoney(readNumber(item, ['closingDebit'])),
    closingCredit: formatIndianMoney(readNumber(item, ['closingCredit']))
  })),
  ledgerSync: syncIssues.value.map(item => ({
    area: readText(item, ['area']),
    entity: readText(item, ['entityName']),
    severity: readText(item, ['severity']),
    issue: readText(item, ['message']),
    action: readText(item, ['fixAction'])
  }))
}))
const columns: Record<AccountingTab, Array<{ key: string, label: string }>> = {
  ledgerGroups: [
    { key: 'name', label: 'Group' },
    { key: 'category', label: 'Category' },
    { key: 'description', label: 'Description' }
  ],
  ledgers: [
    { key: 'name', label: 'Ledger' },
    { key: 'group', label: 'Group' },
    { key: 'type', label: 'Type' },
    { key: 'opening', label: 'Opening' },
    { key: 'status', label: 'Status' }
  ],
  parties: [
    { key: 'name', label: 'Party' },
    { key: 'category', label: 'Category' },
    { key: 'phone', label: 'Phone' },
    { key: 'tax', label: 'GST/PAN' },
    { key: 'ledger', label: 'Ledger Link' }
  ],
  bankAccounts: [
    { key: 'holder', label: 'Holder' },
    { key: 'account', label: 'Account' },
    { key: 'bank', label: 'Bank' },
    { key: 'type', label: 'Type' },
    { key: 'balance', label: 'Balance' },
    { key: 'ledger', label: 'Ledger Link' },
    { key: 'status', label: 'Status' }
  ],
  trialBalance: [
    { key: 'ledger', label: 'Ledger' },
    { key: 'group', label: 'Group' },
    { key: 'debit', label: 'Debit' },
    { key: 'credit', label: 'Credit' },
    { key: 'closingDebit', label: 'Closing Debit' },
    { key: 'closingCredit', label: 'Closing Credit' }
  ],
  ledgerSync: [
    { key: 'area', label: 'Area' },
    { key: 'entity', label: 'Party / Bank' },
    { key: 'severity', label: 'Severity' },
    { key: 'issue', label: 'Issue' },
    { key: 'action', label: 'Action' }
  ]
}
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
    const [groupData, ledgerData, partyData, bankData, bankAccountData, trialData, syncData] = await Promise.allSettled([
      get<unknown>('ledger-groups'),
      get<unknown>('ledgers'),
      get<unknown>('parties'),
      get<unknown>('banks'),
      get<unknown>('bank-accounts'),
      get<unknown>('accounting/trial-balance'),
      get<unknown>('accounting/ledger-sync/status')
    ])
    if (groupData.status === 'fulfilled') ledgerGroups.value = toRows(groupData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (bankData.status === 'fulfilled') banks.value = toRows(bankData.value)
    if (bankAccountData.status === 'fulfilled') bankAccounts.value = toRows(bankAccountData.value)
    if (trialData.status === 'fulfilled') trialBalance.value = toRows(trialData.value)
    if (syncData.status === 'fulfilled' && syncData.value && typeof syncData.value === 'object') ledgerSync.value = syncData.value as ApiRecord
    const failed = [groupData, ledgerData, partyData, bankData, bankAccountData, trialData, syncData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} accounting master request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load accounting masters.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

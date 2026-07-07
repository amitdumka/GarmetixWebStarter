<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-book-open-check" class="size-4" /> Books master data</p>
          <h2 class="garmetix-dashboard-title">Accounting Masters</h2>
          <p class="garmetix-dashboard-subtitle">
            Ledger groups, ledgers, parties, bank accounts, ledger sync and trial balance with guarded repair and statement review.
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
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search master data" class="sm:w-72" />
          <UButton
            v-if="activeTab === 'ledgerSync'"
            icon="i-lucide-wrench"
            color="warning"
            variant="soft"
            :loading="repairing"
            @click="repairLedgerSync"
          >
            Repair Sync
          </UButton>
          <UButton v-if="activeTab === 'ledgers'" icon="i-lucide-plus" color="primary" variant="solid" @click="startCreateLedger">
            New Ledger
          </UButton>
          <UButton v-if="activeTab === 'parties'" icon="i-lucide-plus" color="primary" variant="solid" @click="startCreateParty">
            New Party
          </UButton>
        </div>
      </div>

      <BooksMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No accounting rows found.">
        <template v-if="activeTab === 'ledgers' || activeTab === 'parties'" #actions="{ row }">
          <div class="flex items-center gap-1">
            <UButton
              icon="i-lucide-pencil"
              size="xs"
              color="neutral"
              variant="ghost"
              @click="activeTab === 'ledgers' ? startEditLedger(findLedgerById(row.id)) : startEditParty(findPartyById(row.id))"
            />
            <UButton
              icon="i-lucide-trash-2"
              size="xs"
              color="error"
              variant="ghost"
              @click="activeTab === 'ledgers' ? deleteLedger(findLedgerById(row.id)) : deleteParty(findPartyById(row.id))"
            />
          </div>
        </template>
      </BooksMasterTable>
    </section>

    <UModal v-model:open="ledgerFormOpen" :title="ledgerFormMode === 'edit' ? 'Edit Ledger' : 'New Ledger'">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveLedger">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Name</span>
            <UInput v-model="ledgerForm.name" placeholder="Ledger name" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Ledger Group</span>
            <USelect v-model="ledgerForm.ledgerGroupId" :items="ledgerGroupSelectItems" placeholder="Select group" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Ledger Type</span>
            <USelect v-model="ledgerForm.ledgerType" :items="ledgerTypeSelectItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Opening Date</span>
            <UInput v-model="ledgerForm.openingDate" type="date" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Opening Balance</span>
            <UInput v-model="ledgerForm.openingBalance" type="number" min="0" step="0.01" placeholder="0.00" />
          </label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="ledgerSaving">
              {{ ledgerFormMode === 'edit' ? 'Update Ledger' : 'Save Ledger' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="partyFormOpen" :title="partyFormMode === 'edit' ? 'Edit Party' : 'New Party'">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveParty">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Name</span>
            <UInput v-model="partyForm.name" placeholder="Party name" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Category</span>
            <USelect v-model="partyForm.category" :items="partyTypeSelectItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Phone</span>
            <UInput v-model="partyForm.phone" placeholder="Phone" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Email</span>
            <UInput v-model="partyForm.emailId" placeholder="Email" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">GSTIN</span>
            <UInput v-model="partyForm.gstin" placeholder="GSTIN" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">PAN</span>
            <UInput v-model="partyForm.pan" placeholder="PAN" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Address</span>
            <UTextarea v-model="partyForm.address" :rows="2" placeholder="Address" />
          </label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="partySaving">
              {{ partyFormMode === 'edit' ? 'Update Party' : 'Save Party' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Ledger Statement</h3>
          <p class="garmetix-panel-subtitle">Audit posted entries for a selected ledger without exposing internal party flags.</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="selectedLedgerId" :items="ledgerOptions" placeholder="Select ledger" class="sm:w-72" />
          <UButton icon="i-lucide-file-search" color="neutral" variant="soft" :loading="statementLoading" @click="loadLedgerStatement">View</UButton>
        </div>
      </div>

      <BooksMasterTable :columns="ledgerStatementColumns" :rows="ledgerStatementRows" empty-text="Select a ledger to view posted entries." />
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

interface LedgerForm {
  id: string
  name: string
  ledgerGroupId: string
  ledgerType: number
  openingDate: string
  openingBalance: number | string
}

interface PartyForm {
  id: string
  name: string
  category: number
  phone: string
  emailId: string
  gstin: string
  pan: string
  address: string
}

function emptyLedgerForm(): LedgerForm {
  return { id: '', name: '', ledgerGroupId: '', ledgerType: 4, openingDate: new Date().toISOString().slice(0, 10), openingBalance: 0 }
}

function emptyPartyForm(): PartyForm {
  return { id: '', name: '', category: 0, phone: '', emailId: '', gstin: '', pan: '', address: '' }
}

useHead({ title: 'Accounting - Garmetix Books' })

type AccountingTab = 'ledgerGroups' | 'ledgers' | 'parties' | 'bankAccounts' | 'trialBalance' | 'ledgerSync'

const { get, post, put, del } = useBooksApiClient()
const loading = ref(true)
const repairing = ref(false)
const statementLoading = ref(false)
const error = ref('')
const search = ref('')
const activeTab = ref<AccountingTab>('ledgers')
const selectedLedgerId = ref('')
const ledgerGroups = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const banks = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const trialBalance = ref<ApiRecord[]>([])
const ledgerSync = ref<ApiRecord | null>(null)
const ledgerStatement = ref<ApiRecord[]>([])
const setupStatus = ref<ApiRecord | null>(null)
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])

const ledgerFormOpen = ref(false)
const ledgerFormMode = ref<'create' | 'edit'>('create')
const ledgerSaving = ref(false)
const ledgerForm = reactive<LedgerForm>(emptyLedgerForm())
const partyFormOpen = ref(false)
const partyFormMode = ref<'create' | 'edit'>('create')
const partySaving = ref(false)
const partyForm = reactive<PartyForm>(emptyPartyForm())

const ledgerGroupSelectItems = computed(() => ledgerGroups.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))
const ledgerTypeSelectItems = ledgerTypeOptions.map(item => ({ label: item.label, value: item.value }))
const partyTypeSelectItems = partyTypeOptions.map(item => ({ label: item.label, value: item.value }))

function resolveCompanyId() {
  const companyId = readText(setupStatus.value, ['companyId'], '')
    || readText(stores.value[0], ['companyId'], '')
    || readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving accounting masters.')
  return companyId
}

function startCreateLedger() {
  ledgerFormMode.value = 'create'
  Object.assign(ledgerForm, emptyLedgerForm())
  ledgerForm.ledgerGroupId = ledgerGroupSelectItems.value[0]?.value || ''
  ledgerFormOpen.value = true
}

function startEditLedger(ledger: ApiRecord) {
  if (!ledger) return
  ledgerFormMode.value = 'edit'
  Object.assign(ledgerForm, {
    id: readText(ledger, ['id'], ''),
    name: readText(ledger, ['name'], ''),
    ledgerGroupId: readText(ledger, ['ledgerGroupId'], ''),
    ledgerType: Number(ledger.ledgerType ?? 4),
    openingDate: String(ledger.openingDate ?? '').slice(0, 10) || new Date().toISOString().slice(0, 10),
    openingBalance: readNumber(ledger, ['openingBalance'])
  })
  ledgerFormOpen.value = true
}

async function saveLedger() {
  ledgerSaving.value = true
  error.value = ''
  try {
    const companyId = resolveCompanyId()
    if (!ledgerForm.name.trim()) throw new Error('Enter ledger name.')
    if (!ledgerForm.ledgerGroupId) throw new Error('Select ledger group.')

    const payload = {
      companyId,
      name: ledgerForm.name.trim(),
      ledgerGroupId: ledgerForm.ledgerGroupId,
      ledgerType: Number(ledgerForm.ledgerType),
      openingDate: new Date(ledgerForm.openingDate).toISOString(),
      openingBalance: Number(ledgerForm.openingBalance || 0),
      isParty: false
    }

    if (ledgerFormMode.value === 'edit' && ledgerForm.id) {
      await put<unknown>(`ledgers/${ledgerForm.id}`, payload)
    } else {
      await post<unknown>('ledgers', payload)
    }

    ledgerFormOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save ledger.'
  } finally {
    ledgerSaving.value = false
  }
}

async function deleteLedger(ledger: ApiRecord) {
  if (!ledger) return
  const id = readText(ledger, ['id'], '')
  if (!id) return
  if (!window.confirm(`Delete ledger "${readText(ledger, ['name'])}"?`)) return

  error.value = ''
  try {
    await del<unknown>(`ledgers/${id}`)
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete ledger.'
  }
}

function startCreateParty() {
  partyFormMode.value = 'create'
  Object.assign(partyForm, emptyPartyForm())
  partyFormOpen.value = true
}

function startEditParty(party: ApiRecord) {
  if (!party) return
  partyFormMode.value = 'edit'
  Object.assign(partyForm, {
    id: readText(party, ['id'], ''),
    name: readText(party, ['name'], ''),
    category: Number(party.category ?? 0),
    phone: readText(party, ['phone'], ''),
    emailId: readText(party, ['emailId', 'email'], ''),
    gstin: readText(party, ['gstin'], ''),
    pan: readText(party, ['pan'], ''),
    address: readText(party, ['address'], '')
  })
  partyFormOpen.value = true
}

async function saveParty() {
  partySaving.value = true
  error.value = ''
  try {
    const companyId = resolveCompanyId()
    if (!partyForm.name.trim()) throw new Error('Enter party name.')

    const payload = {
      companyId,
      name: partyForm.name.trim(),
      address: partyForm.address.trim() || null,
      emailId: partyForm.emailId.trim() || null,
      phone: partyForm.phone.trim() || null,
      gstin: partyForm.gstin.trim().toUpperCase() || null,
      pan: partyForm.pan.trim().toUpperCase() || null,
      category: Number(partyForm.category)
    }

    if (partyFormMode.value === 'edit' && partyForm.id) {
      await put<unknown>(`parties/${partyForm.id}`, payload)
    } else {
      await post<unknown>('parties', payload)
    }

    partyFormOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save party.'
  } finally {
    partySaving.value = false
  }
}

async function deleteParty(party: ApiRecord) {
  if (!party) return
  const id = readText(party, ['id'], '')
  if (!id) return
  if (!window.confirm(`Delete party "${readText(party, ['name'])}"?`)) return

  error.value = ''
  try {
    await del<unknown>(`parties/${id}`)
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete party.'
  }
}

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
const findLedgerById = (id: unknown) => ledgers.value.find(item => readText(item, ['id'], '') === id) ?? null
const findPartyById = (id: unknown) => parties.value.find(item => readText(item, ['id'], '') === id) ?? null
const ledgerOptions = computed(() => ledgers.value.map(item => ({ value: readText(item, ['id'], ''), label: readText(item, ['name']) })).filter(item => item.value))
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
    id: readText(item, ['id'], ''),
    name: readText(item, ['name']),
    group: groupName(item.ledgerGroupId),
    type: optionLabel(ledgerTypeOptions, item.ledgerType),
    opening: formatIndianMoney(readNumber(item, ['openingBalance'])),
    status: readText(item, ['active'], 'Active')
  })),
  parties: parties.value.map(item => ({
    id: readText(item, ['id'], ''),
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
const ledgerStatementColumns = [
  { key: 'date', label: 'Date' },
  { key: 'entry', label: 'Entry' },
  { key: 'source', label: 'Source' },
  { key: 'particulars', label: 'Particulars' },
  { key: 'debit', label: 'Debit' },
  { key: 'credit', label: 'Credit' },
  { key: 'balance', label: 'Balance' }
]
const ledgerStatementRows = computed(() => ledgerStatement.value.map(item => ({
  date: readText(item, ['onDate']),
  entry: readText(item, ['entryNumber', 'referenceNumber']),
  source: readText(item, ['sourceType']),
  particulars: readText(item, ['particulars']),
  debit: formatIndianMoney(readNumber(item, ['debit'])),
  credit: formatIndianMoney(readNumber(item, ['credit'])),
  balance: `${formatIndianMoney(readNumber(item, ['balance']))} ${readText(item, ['balanceType'], '')}`.trim()
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [setupData, companyData, storeData, groupData, ledgerData, partyData, bankData, bankAccountData, trialData, syncData] = await Promise.allSettled([
      get<unknown>('setup/status'),
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('ledger-groups'),
      get<unknown>('ledgers'),
      get<unknown>('parties'),
      get<unknown>('banks'),
      get<unknown>('bank-accounts'),
      get<unknown>('accounting/trial-balance'),
      get<unknown>('accounting/ledger-sync/status')
    ])
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (groupData.status === 'fulfilled') ledgerGroups.value = toRows(groupData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (bankData.status === 'fulfilled') banks.value = toRows(bankData.value)
    if (bankAccountData.status === 'fulfilled') bankAccounts.value = toRows(bankAccountData.value)
    if (trialData.status === 'fulfilled') trialBalance.value = toRows(trialData.value)
    if (syncData.status === 'fulfilled' && syncData.value && typeof syncData.value === 'object') ledgerSync.value = syncData.value as ApiRecord
    if (!selectedLedgerId.value && ledgers.value.length) selectedLedgerId.value = readText(ledgers.value[0], ['id'], '')
    const failed = [groupData, ledgerData, partyData, bankData, bankAccountData, trialData, syncData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} accounting master request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load accounting masters.'
  } finally {
    loading.value = false
  }
}

async function repairLedgerSync() {
  const phrase = window.prompt('Type REPAIR LEDGER SYNC to create missing party and bank ledgers.')
  if (phrase !== 'REPAIR LEDGER SYNC') return

  repairing.value = true
  error.value = ''
  try {
    await post<unknown>('accounting/ledger-sync/repair', {})
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to repair ledger sync.'
  } finally {
    repairing.value = false
  }
}

async function loadLedgerStatement() {
  if (!selectedLedgerId.value) {
    error.value = 'Select ledger before loading statement.'
    return
  }

  statementLoading.value = true
  error.value = ''
  try {
    ledgerStatement.value = toRows(await get<unknown>(`accounting/ledger-statement/${selectedLedgerId.value}`))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load ledger statement.'
  } finally {
    statementLoading.value = false
  }
}

onMounted(refresh)
</script>

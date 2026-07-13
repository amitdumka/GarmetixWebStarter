<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-list-tree" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Chart Of Accounts</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton icon="i-lucide-file-search" color="neutral" variant="soft" :loading="seedLoading" @click="openSeedPreview">Seed Preview</UButton>
      </div>
    </div>

    <UAlert v-if="message" :icon="messageIcon" :color="messageTone" variant="subtle" :title="messageTitle" :description="message" />

    <div class="final-accounts-grid">
      <UCard v-for="card in metricCards" :key="card.label" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div>
            <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
            <p class="text-2xl font-semibold text-highlighted">{{ card.value }}</p>
          </div>
          <UIcon :name="card.icon" class="size-5 text-primary" />
        </div>
      </UCard>
    </div>

    <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-xl" />

    <div v-if="activeTab === 'accounts'" class="grid gap-4 xl:grid-cols-[minmax(20rem,24rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveAccount">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ selectedAccountId ? 'Edit Account' : 'New Account' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetAccountForm">Clear</UButton>
          </div>

          <UFormField label="Group" name="accountGroupId">
            <USelectMenu v-model="accountForm.accountGroupId" :items="groupSelectItems" value-key="value" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Code" name="code">
              <UInput v-model="accountForm.code" icon="i-lucide-hash" required />
            </UFormField>
            <UFormField label="Opening balance" name="openingBalance">
              <UInput v-model.number="accountForm.openingBalance" type="number" step="0.01" icon="i-lucide-indian-rupee" />
            </UFormField>
          </div>

          <UFormField label="Name" name="name">
            <UInput v-model="accountForm.name" icon="i-lucide-book-open" required />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Type" name="accountType">
              <USelect v-model="accountForm.accountType" :items="accountTypes" />
            </UFormField>
            <UFormField label="Natural balance" name="naturalBalance">
              <USelect v-model="accountForm.naturalBalance" :items="naturalBalances" />
            </UFormField>
          </div>

          <UFormField label="Parent account" name="parentAccountId">
            <USelectMenu v-model="accountForm.parentAccountId" :items="accountParentOptions" value-key="value" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <USwitch v-model="accountForm.isControlAccount" label="Control account" />
            <USwitch v-model="accountForm.isActive" label="Active" />
          </div>

          <UFormField label="Description" name="description">
            <UTextarea v-model="accountForm.description" :rows="3" />
          </UFormField>

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ selectedAccountId ? 'Save Account' : 'Create Account' }}</UButton>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UInput v-model="accountSearch" icon="i-lucide-search" placeholder="Search accounts" class="w-full sm:w-64" />
          <USelect v-model="accountTypeFilter" :items="accountTypeFilterItems" class="w-full sm:w-48" />
        </div>
        <UTable :data="filteredAccounts" :columns="accountColumns" :loading="loading" class="w-full">
          <template #code-cell="{ row }">
            <span class="font-mono text-sm">{{ row.original.code }}</span>
          </template>
          <template #accountType-cell="{ row }">
            <UBadge color="neutral" variant="subtle">{{ row.original.accountType }}</UBadge>
          </template>
          <template #naturalBalance-cell="{ row }">
            <UBadge :color="row.original.naturalBalance === 'Debit' ? 'success' : 'warning'" variant="subtle">{{ row.original.naturalBalance }}</UBadge>
          </template>
          <template #isActive-cell="{ row }">
            <UBadge :color="row.original.isActive ? 'success' : 'error'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="Edit Account">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="editAccount(row.original)" />
              </UTooltip>
              <UTooltip text="Delete Account">
                <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" :disabled="row.original.isSystem" @click="deleteAccount(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-else-if="activeTab === 'groups'" class="grid gap-4 xl:grid-cols-[minmax(20rem,24rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveGroup">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ selectedGroupId ? 'Edit Group' : 'New Group' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetGroupForm">Clear</UButton>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Code" name="code">
              <UInput v-model="groupForm.code" icon="i-lucide-hash" required />
            </UFormField>
            <UFormField label="Sort" name="sortOrder">
              <UInput v-model.number="groupForm.sortOrder" type="number" icon="i-lucide-arrow-down-narrow-wide" />
            </UFormField>
          </div>

          <UFormField label="Name" name="name">
            <UInput v-model="groupForm.name" icon="i-lucide-folder-tree" required />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Type" name="accountType">
              <USelect v-model="groupForm.accountType" :items="accountTypes" />
            </UFormField>
            <UFormField label="Natural balance" name="naturalBalance">
              <USelect v-model="groupForm.naturalBalance" :items="naturalBalances" />
            </UFormField>
          </div>

          <UFormField label="Parent group" name="parentGroupId">
            <USelectMenu v-model="groupForm.parentGroupId" :items="groupParentOptions" value-key="value" class="w-full" />
          </UFormField>

          <USwitch v-model="groupForm.isActive" label="Active" />

          <UFormField label="Description" name="description">
            <UTextarea v-model="groupForm.description" :rows="3" />
          </UFormField>

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ selectedGroupId ? 'Save Group' : 'Create Group' }}</UButton>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <UTable :data="groups" :columns="groupColumns" :loading="loading" class="w-full">
          <template #code-cell="{ row }">
            <span class="font-mono text-sm">{{ row.original.code }}</span>
          </template>
          <template #naturalBalance-cell="{ row }">
            <UBadge :color="row.original.naturalBalance === 'Debit' ? 'success' : 'warning'" variant="subtle">{{ row.original.naturalBalance }}</UBadge>
          </template>
          <template #isActive-cell="{ row }">
            <UBadge :color="row.original.isActive ? 'success' : 'error'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="Edit Group">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="editGroup(row.original)" />
              </UTooltip>
              <UTooltip text="Delete Group">
                <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" :disabled="row.original.isSystem" @click="deleteGroup(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-else class="grid gap-4 xl:grid-cols-[minmax(20rem,24rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveMapping">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ selectedMappingId ? 'Edit Mapping' : 'New Mapping' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetMappingForm">Clear</UButton>
          </div>

          <UFormField label="Source" name="sourceType">
            <USelect v-model="mappingForm.sourceType" :items="mappingSources" />
          </UFormField>

          <UFormField label="Mapping key" name="mappingKey">
            <UInput v-model="mappingForm.mappingKey" icon="i-lucide-key-round" required />
          </UFormField>

          <UFormField label="Display name" name="displayName">
            <UInput v-model="mappingForm.displayName" icon="i-lucide-tag" required />
          </UFormField>

          <UFormField label="Account" name="accountId">
            <USelectMenu v-model="mappingForm.accountId" :items="accountSelectItems" value-key="value" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <USwitch v-model="mappingForm.isRequired" label="Required" />
            <USwitch v-model="mappingForm.isActive" label="Active" />
          </div>

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="mappingForm.notes" :rows="3" />
          </UFormField>

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ selectedMappingId ? 'Save Mapping' : 'Create Mapping' }}</UButton>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <UTable :data="mappings" :columns="mappingColumns" :loading="loading" class="w-full">
          <template #mappingKey-cell="{ row }">
            <span class="font-mono text-sm">{{ row.original.mappingKey }}</span>
          </template>
          <template #isActive-cell="{ row }">
            <UBadge :color="row.original.isActive ? 'success' : 'error'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="Edit Mapping">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="editMapping(row.original)" />
              </UTooltip>
              <UTooltip text="Delete Mapping">
                <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" :disabled="row.original.isSystem" @click="deleteMapping(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <USlideover v-model:open="seedOpen" title="Seed Preview" :ui="{ content: 'sm:max-w-2xl' }">
      <template #body>
        <div class="space-y-4">
          <UAlert v-if="seedPreview?.issues.length" icon="i-lucide-info" color="neutral" variant="subtle" title="Seed status" :description="seedPreview.issues.map(item => item.message).join(' ')" />

          <UCard :ui="{ body: 'p-0' }">
            <UTable :data="seedPreview?.groups || []" :columns="seedGroupColumns" />
          </UCard>

          <UCard :ui="{ body: 'p-0' }">
            <UTable :data="seedPreview?.accounts || []" :columns="seedAccountColumns" />
          </UCard>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  type FinalAccountsAccount,
  type FinalAccountsAccountGroup,
  type FinalAccountsAccountGroupPayload,
  type FinalAccountsAccountMapping,
  type FinalAccountsAccountMappingPayload,
  type FinalAccountsAccountPayload,
  type FinalAccountsAccountType,
  type FinalAccountsMappingSourceType,
  type FinalAccountsNaturalBalance,
  type FinalAccountsSeedPreview,
  type FinalAccountsValidationSummary,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'Chart Of Accounts' })

type SelectItem = { label: string; value: string }
type Tone = 'success' | 'error' | 'neutral'

const api = useFinalAccountsApiClient()
const activeTab = ref('accounts')
const loading = ref(false)
const saving = ref(false)
const seedLoading = ref(false)
const seedOpen = ref(false)
const message = ref('')
const messageTone = ref<Tone>('neutral')
const groups = ref<FinalAccountsAccountGroup[]>([])
const accounts = ref<FinalAccountsAccount[]>([])
const mappings = ref<FinalAccountsAccountMapping[]>([])
const validation = ref<FinalAccountsValidationSummary | null>(null)
const seedPreview = ref<FinalAccountsSeedPreview | null>(null)
const selectedGroupId = ref('')
const selectedAccountId = ref('')
const selectedMappingId = ref('')
const accountSearch = ref('')
const accountTypeFilter = ref('All')

const accountTypes: FinalAccountsAccountType[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense', 'ContraAsset', 'ContraLiability']
const naturalBalances: FinalAccountsNaturalBalance[] = ['Debit', 'Credit']
const mappingSources: FinalAccountsMappingSourceType[] = ['Sales', 'Purchase', 'Inventory', 'Gst', 'Payroll', 'CashBank', 'Customer', 'Vendor', 'Adjustment']
const accountTypeFilterItems = ['All', ...accountTypes]
const tabs = [
  { label: 'Accounts', value: 'accounts', icon: 'i-lucide-book-open' },
  { label: 'Groups', value: 'groups', icon: 'i-lucide-folder-tree' },
  { label: 'Mappings', value: 'mappings', icon: 'i-lucide-route' }
]

const groupForm = reactive({
  parentGroupId: '',
  code: '',
  name: '',
  accountType: 'Asset' as FinalAccountsAccountType,
  naturalBalance: 'Debit' as FinalAccountsNaturalBalance,
  sortOrder: 0,
  isActive: true,
  description: ''
})

const accountForm = reactive({
  accountGroupId: '',
  parentAccountId: '',
  code: '',
  name: '',
  accountType: 'Asset' as FinalAccountsAccountType,
  naturalBalance: 'Debit' as FinalAccountsNaturalBalance,
  openingBalance: 0,
  isControlAccount: false,
  isActive: true,
  description: '',
  sortOrder: 0
})

const mappingForm = reactive({
  sourceType: 'Sales' as FinalAccountsMappingSourceType,
  mappingKey: '',
  displayName: '',
  accountId: '',
  isRequired: true,
  isActive: true,
  notes: ''
})

const messageTitle = computed(() => messageTone.value === 'success' ? 'Saved' : messageTone.value === 'error' ? 'Chart unavailable' : 'Chart status')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const metricCards = computed(() => [
  { label: 'Groups', value: validation.value?.groupCount ?? groups.value.length, icon: 'i-lucide-folder-tree' },
  { label: 'Accounts', value: validation.value?.accountCount ?? accounts.value.length, icon: 'i-lucide-book-open' },
  { label: 'Mappings', value: validation.value?.mappingCount ?? mappings.value.length, icon: 'i-lucide-route' },
  { label: 'Issues', value: validation.value?.issues.length ?? 0, icon: 'i-lucide-shield-alert' }
])
const groupSelectItems = computed<SelectItem[]>(() => groups.value.map(group => ({ label: `${group.code} - ${group.name}`, value: group.id })))
const groupParentOptions = computed<SelectItem[]>(() => [
  { label: 'No parent', value: '' },
  ...groups.value.filter(group => group.id !== selectedGroupId.value).map(group => ({ label: `${group.code} - ${group.name}`, value: group.id }))
])
const accountSelectItems = computed<SelectItem[]>(() => accounts.value.map(account => ({ label: `${account.code} - ${account.name}`, value: account.id })))
const accountParentOptions = computed<SelectItem[]>(() => [
  { label: 'No parent', value: '' },
  ...accounts.value.filter(account => account.id !== selectedAccountId.value).map(account => ({ label: `${account.code} - ${account.name}`, value: account.id }))
])
const filteredAccounts = computed(() => {
  const term = accountSearch.value.trim().toLowerCase()
  return accounts.value
    .filter(account => accountTypeFilter.value === 'All' || account.accountType === accountTypeFilter.value)
    .filter(account => !term
      || account.code.toLowerCase().includes(term)
      || account.name.toLowerCase().includes(term)
      || (account.accountGroupName || groupLabel(account.accountGroupId)).toLowerCase().includes(term))
    .map(account => ({
      ...account,
      group: account.accountGroupName || groupLabel(account.accountGroupId)
    }))
})

const groupColumns = [
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'accountType', header: 'Type' },
  { accessorKey: 'naturalBalance', header: 'Balance' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const accountColumns = [
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'group', header: 'Group' },
  { accessorKey: 'accountType', header: 'Type' },
  { accessorKey: 'naturalBalance', header: 'Balance' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const mappingColumns = [
  { accessorKey: 'sourceType', header: 'Source' },
  { accessorKey: 'mappingKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'accountCode', header: 'Account' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const seedGroupColumns = [
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'name', header: 'Group' },
  { accessorKey: 'accountType', header: 'Type' },
  { accessorKey: 'parentCode', header: 'Parent' }
]
const seedAccountColumns = [
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'name', header: 'Account' },
  { accessorKey: 'groupCode', header: 'Group' },
  { accessorKey: 'accountType', header: 'Type' }
]

watch(() => groupForm.accountType, value => {
  groupForm.naturalBalance = expectedBalance(value)
})

watch(() => accountForm.accountGroupId, value => {
  const group = groups.value.find(item => item.id === value)
  if (group) {
    accountForm.accountType = group.accountType
    accountForm.naturalBalance = group.naturalBalance
  }
})

async function loadAll() {
  loading.value = true
  message.value = ''
  try {
    const [groupRows, accountRows, mappingRows, validationRow] = await Promise.all([
      api.get<FinalAccountsAccountGroup[]>('account-groups'),
      api.get<FinalAccountsAccount[]>('accounts'),
      api.get<FinalAccountsAccountMapping[]>('account-mappings'),
      api.get<FinalAccountsValidationSummary>('validation/summary')
    ])
    groups.value = groupRows
    accounts.value = accountRows
    mappings.value = mappingRows
    validation.value = validationRow
    if (!accountForm.accountGroupId && groups.value[0]) {
      accountForm.accountGroupId = groups.value[0].id
    }
    if (!mappingForm.accountId && accounts.value[0]) {
      mappingForm.accountId = accounts.value[0].id
    }
  } catch (err) {
    showError(err, 'Chart of accounts could not be loaded.')
  } finally {
    loading.value = false
  }
}

async function saveGroup() {
  await saveEntity(
    () => selectedGroupId.value
      ? api.put<FinalAccountsAccountGroup>(`account-groups/${selectedGroupId.value}`, groupPayload())
      : api.post<FinalAccountsAccountGroup>('account-groups', groupPayload()),
    resetGroupForm,
    'Account group saved.'
  )
}

async function saveAccount() {
  await saveEntity(
    () => selectedAccountId.value
      ? api.put<FinalAccountsAccount>(`accounts/${selectedAccountId.value}`, accountPayload())
      : api.post<FinalAccountsAccount>('accounts', accountPayload()),
    resetAccountForm,
    'Account saved.'
  )
}

async function saveMapping() {
  await saveEntity(
    () => selectedMappingId.value
      ? api.put<FinalAccountsAccountMapping>(`account-mappings/${selectedMappingId.value}`, mappingPayload())
      : api.post<FinalAccountsAccountMapping>('account-mappings', mappingPayload()),
    resetMappingForm,
    'Account mapping saved.'
  )
}

async function saveEntity(action: () => Promise<unknown>, reset: () => void, successMessage: string) {
  saving.value = true
  message.value = ''
  try {
    await action()
    messageTone.value = 'success'
    message.value = successMessage
    reset()
    await loadAll()
  } catch (err) {
    showError(err, 'Save failed.')
  } finally {
    saving.value = false
  }
}

function editGroup(row: FinalAccountsAccountGroup) {
  selectedGroupId.value = row.id
  groupForm.parentGroupId = row.parentGroupId || ''
  groupForm.code = row.code
  groupForm.name = row.name
  groupForm.accountType = row.accountType
  groupForm.naturalBalance = row.naturalBalance
  groupForm.sortOrder = row.sortOrder
  groupForm.isActive = row.isActive
  groupForm.description = row.description || ''
  activeTab.value = 'groups'
}

function editAccount(row: FinalAccountsAccount) {
  selectedAccountId.value = row.id
  accountForm.accountGroupId = row.accountGroupId
  accountForm.parentAccountId = row.parentAccountId || ''
  accountForm.code = row.code
  accountForm.name = row.name
  accountForm.accountType = row.accountType
  accountForm.naturalBalance = row.naturalBalance
  accountForm.openingBalance = row.openingBalance
  accountForm.isControlAccount = row.isControlAccount
  accountForm.isActive = row.isActive
  accountForm.description = row.description || ''
  accountForm.sortOrder = row.sortOrder
  activeTab.value = 'accounts'
}

function editMapping(row: FinalAccountsAccountMapping) {
  selectedMappingId.value = row.id
  mappingForm.sourceType = row.sourceType
  mappingForm.mappingKey = row.mappingKey
  mappingForm.displayName = row.displayName
  mappingForm.accountId = row.accountId
  mappingForm.isRequired = row.isRequired
  mappingForm.isActive = row.isActive
  mappingForm.notes = row.notes || ''
  activeTab.value = 'mappings'
}

async function deleteGroup(row: FinalAccountsAccountGroup) {
  await deleteEntity(`account-groups/${row.id}`, `Delete group ${row.code}?`, 'Account group deleted.')
}

async function deleteAccount(row: FinalAccountsAccount) {
  await deleteEntity(`accounts/${row.id}`, `Delete account ${row.code}?`, 'Account deleted.')
}

async function deleteMapping(row: FinalAccountsAccountMapping) {
  await deleteEntity(`account-mappings/${row.id}`, `Delete mapping ${row.mappingKey}?`, 'Account mapping deleted.')
}

async function deleteEntity(path: string, confirmation: string, successMessage: string) {
  if (!window.confirm(confirmation)) return
  loading.value = true
  message.value = ''
  try {
    await api.remove(path)
    messageTone.value = 'success'
    message.value = successMessage
    await loadAll()
  } catch (err) {
    showError(err, 'Delete failed.')
  } finally {
    loading.value = false
  }
}

async function openSeedPreview() {
  seedLoading.value = true
  try {
    seedPreview.value = await api.get<FinalAccountsSeedPreview>('coa/seed-preview')
    seedOpen.value = true
  } catch (err) {
    showError(err, 'Seed preview could not be loaded.')
  } finally {
    seedLoading.value = false
  }
}

function groupPayload(): FinalAccountsAccountGroupPayload {
  return {
    parentGroupId: blankToNull(groupForm.parentGroupId),
    code: groupForm.code,
    name: groupForm.name,
    accountType: groupForm.accountType,
    naturalBalance: groupForm.naturalBalance,
    sortOrder: Number(groupForm.sortOrder) || 0,
    isActive: groupForm.isActive,
    description: blankToNull(groupForm.description)
  }
}

function accountPayload(): FinalAccountsAccountPayload {
  return {
    accountGroupId: accountForm.accountGroupId,
    parentAccountId: blankToNull(accountForm.parentAccountId),
    code: accountForm.code,
    name: accountForm.name,
    accountType: accountForm.accountType,
    naturalBalance: accountForm.naturalBalance,
    openingBalance: Number(accountForm.openingBalance) || 0,
    isControlAccount: accountForm.isControlAccount,
    isActive: accountForm.isActive,
    description: blankToNull(accountForm.description),
    sortOrder: Number(accountForm.sortOrder) || 0
  }
}

function mappingPayload(): FinalAccountsAccountMappingPayload {
  return {
    sourceType: mappingForm.sourceType,
    mappingKey: mappingForm.mappingKey,
    displayName: mappingForm.displayName,
    accountId: mappingForm.accountId,
    isRequired: mappingForm.isRequired,
    isActive: mappingForm.isActive,
    notes: blankToNull(mappingForm.notes)
  }
}

function resetGroupForm() {
  selectedGroupId.value = ''
  groupForm.parentGroupId = ''
  groupForm.code = ''
  groupForm.name = ''
  groupForm.accountType = 'Asset'
  groupForm.naturalBalance = 'Debit'
  groupForm.sortOrder = 0
  groupForm.isActive = true
  groupForm.description = ''
}

function resetAccountForm() {
  selectedAccountId.value = ''
  accountForm.accountGroupId = groups.value[0]?.id || ''
  accountForm.parentAccountId = ''
  accountForm.code = ''
  accountForm.name = ''
  accountForm.accountType = groups.value[0]?.accountType || 'Asset'
  accountForm.naturalBalance = groups.value[0]?.naturalBalance || 'Debit'
  accountForm.openingBalance = 0
  accountForm.isControlAccount = false
  accountForm.isActive = true
  accountForm.description = ''
  accountForm.sortOrder = 0
}

function resetMappingForm() {
  selectedMappingId.value = ''
  mappingForm.sourceType = 'Sales'
  mappingForm.mappingKey = ''
  mappingForm.displayName = ''
  mappingForm.accountId = accounts.value[0]?.id || ''
  mappingForm.isRequired = true
  mappingForm.isActive = true
  mappingForm.notes = ''
}

function expectedBalance(type: FinalAccountsAccountType): FinalAccountsNaturalBalance {
  return ['Asset', 'Expense', 'ContraLiability'].includes(type) ? 'Debit' : 'Credit'
}

function groupLabel(id: string) {
  const group = groups.value.find(item => item.id === id)
  return group ? `${group.code} - ${group.name}` : '-'
}

function blankToNull(value: string | null | undefined) {
  const trimmed = String(value || '').trim()
  return trimmed ? trimmed : null
}

function showError(err: unknown, fallback: string) {
  messageTone.value = 'error'
  message.value = err instanceof Error ? err.message : fallback
}

onMounted(loadAll)
</script>

<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-landmark" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Accounts Hub</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-arrow-left-right" color="neutral" variant="soft" @click="openTransfer">Transfer</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Account</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load accounts" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Net worth (accounts)</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(netWorth) }}</p>
          </div>
          <UIcon name="i-lucide-wallet" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard v-for="item in typeSummary" :key="item.type" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">{{ item.type }} ({{ item.count }})</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(item.total) }}</p>
          </div>
          <UIcon :name="typeIcon(item.type)" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="accounts" :columns="accountColumns" :loading="loading" class="w-full">
        <template #accountType-cell="{ row }">
          <UBadge color="neutral" variant="subtle">{{ row.original.accountType }}</UBadge>
        </template>
        <template #detail-cell="{ row }">
          <span class="text-sm text-muted">{{ accountDetailLine(row.original) }}</span>
        </template>
        <template #currentBalance-cell="{ row }">
          <span class="font-mono text-sm" :class="row.original.currentBalance < 0 ? 'text-error' : ''">{{ formatCurrency(row.original.currentBalance) }}</span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UTooltip text="Ledger">
              <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" @click="navigateTo(`/accounts/${row.original.id}`)" />
            </UTooltip>
            <UTooltip text="Edit">
              <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEdit(row.original)" />
            </UTooltip>
            <UTooltip text="Delete">
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="deleteAccount(row.original)" />
            </UTooltip>
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Account' : 'New Account'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitAccount">
          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-tag" required class="w-full" />
          </UFormField>

          <UFormField label="Account type" name="accountType">
            <USelect v-model="form.accountType" :items="accountTypes" class="w-full" />
          </UFormField>

          <div v-if="form.accountType === 'Bank'" class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Bank name" name="bankName">
              <UInput v-model="form.bankName" class="w-full" />
            </UFormField>
            <UFormField label="IFSC" name="ifsc">
              <UInput v-model="form.ifsc" class="w-full" />
            </UFormField>
            <UFormField label="Account number (masked)" name="accountNumberMasked" class="sm:col-span-2">
              <UInput v-model="form.accountNumberMasked" placeholder="XXXX1234" class="w-full" />
            </UFormField>
          </div>

          <div v-if="form.accountType === 'CreditCard'" class="grid gap-3 sm:grid-cols-3">
            <UFormField label="Credit limit" name="creditLimit">
              <UInput v-model.number="form.creditLimit" type="number" step="0.01" class="w-full" />
            </UFormField>
            <UFormField label="Statement day" name="statementDayOfMonth">
              <UInput v-model.number="form.statementDayOfMonth" type="number" min="1" max="31" class="w-full" />
            </UFormField>
            <UFormField label="Due day" name="dueDayOfMonth">
              <UInput v-model.number="form.dueDayOfMonth" type="number" min="1" max="31" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField :label="editingId ? 'Opening balance' : 'Opening balance'" name="openingBalance">
              <UInput v-model.number="form.openingBalance" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="Currency" name="currency">
              <UInput v-model="form.currency" class="w-full" />
            </UFormField>
          </div>

          <USwitch v-model="form.isActive" label="Active" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save Account' : 'Create Account' }}</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="transferOpen" title="Transfer Between Accounts" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitTransfer">
          <UFormField label="From account" name="fromAccountId">
            <USelectMenu v-model="transferForm.fromAccountId" :items="accountSelectItems" value-key="value" class="w-full" />
          </UFormField>
          <UFormField label="To account" name="toAccountId">
            <USelectMenu v-model="transferForm.toAccountId" :items="accountSelectItems" value-key="value" class="w-full" />
          </UFormField>
          <UFormField label="Amount" name="amount">
            <UInput v-model.number="transferForm.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UFormField label="Date" name="transactionDate">
            <UInput v-model="transferForm.transactionDate" type="date" class="w-full" />
          </UFormField>
          <UFormField label="Narration" name="narration">
            <UInput v-model="transferForm.narration" class="w-full" />
          </UFormField>

          <UAlert v-if="transferError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="transferError" />

          <UButton type="submit" icon="i-lucide-arrow-left-right" :loading="transferring" block>Transfer</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaAccount, type SwalekhaAccountPayload, type SwalekhaAccountType, type SwalekhaTransferPayload, type SwalekhaTransferResult } from '../../utils/swalekha-api'

useHead({ title: 'Accounts Hub - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const accounts = ref<SwalekhaAccount[]>([])

const accountTypes: SwalekhaAccountType[] = ['Bank', 'Cash', 'CreditCard']

const accountColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'accountType', header: 'Type' },
  { accessorKey: 'detail', header: 'Detail' },
  { accessorKey: 'currentBalance', header: 'Balance' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const netWorth = computed(() => accounts.value.reduce((sum, account) => sum + account.currentBalance, 0))
const typeSummary = computed(() => accountTypes.map(type => {
  const rows = accounts.value.filter(account => account.accountType === type)
  return { type, count: rows.length, total: rows.reduce((sum, account) => sum + account.currentBalance, 0) }
}).filter(item => item.count > 0))

const accountSelectItems = computed(() => accounts.value.map(account => ({ label: `${account.name} (${formatCurrency(account.currentBalance)})`, value: account.id })))

function typeIcon(type: string) {
  return type === 'Bank' ? 'i-lucide-landmark' : type === 'CreditCard' ? 'i-lucide-credit-card' : 'i-lucide-banknote'
}

function accountDetailLine(account: SwalekhaAccount) {
  if (account.accountType === 'Bank') return [account.bankName, account.accountNumberMasked].filter(Boolean).join(' - ') || '-'
  if (account.accountType === 'CreditCard') return account.creditLimit ? `Limit ${formatCurrency(account.creditLimit)}` : '-'
  return '-'
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    accounts.value = await api.get<SwalekhaAccount[]>('accounts?includeInactive=true')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load accounts.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

// Create/edit form
const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaAccountPayload>(defaultForm())

function defaultForm(): SwalekhaAccountPayload {
  return {
    name: '',
    accountType: 'Bank',
    bankName: '',
    accountNumberMasked: '',
    ifsc: '',
    creditLimit: null,
    statementDayOfMonth: null,
    dueDayOfMonth: null,
    openingBalance: 0,
    currency: 'INR',
    isActive: true,
    notes: ''
  }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(account: SwalekhaAccount) {
  editingId.value = account.id
  formError.value = ''
  Object.assign(form, {
    name: account.name,
    accountType: account.accountType,
    bankName: account.bankName || '',
    accountNumberMasked: account.accountNumberMasked || '',
    ifsc: account.ifsc || '',
    creditLimit: account.creditLimit ?? null,
    statementDayOfMonth: account.statementDayOfMonth ?? null,
    dueDayOfMonth: account.dueDayOfMonth ?? null,
    openingBalance: account.openingBalance,
    currency: account.currency,
    isActive: account.isActive,
    notes: account.notes || ''
  })
  formOpen.value = true
}

async function submitAccount() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`accounts/${editingId.value}`, form)
    } else {
      await api.post('accounts', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the account.'
  } finally {
    saving.value = false
  }
}

async function deleteAccount(account: SwalekhaAccount) {
  if (!confirm(`Delete account "${account.name}"? Its transaction history is kept for the record.`)) return
  try {
    await api.del(`accounts/${account.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the account.'
  }
}

// Transfer form
const transferOpen = ref(false)
const transferring = ref(false)
const transferError = ref('')
const transferForm = reactive<SwalekhaTransferPayload>({
  fromAccountId: '',
  toAccountId: '',
  amount: 0,
  transactionDate: new Date().toISOString().slice(0, 10),
  narration: ''
})

function openTransfer() {
  transferError.value = ''
  transferForm.fromAccountId = accounts.value[0]?.id || ''
  transferForm.toAccountId = accounts.value[1]?.id || ''
  transferForm.amount = 0
  transferForm.transactionDate = new Date().toISOString().slice(0, 10)
  transferForm.narration = ''
  transferOpen.value = true
}

async function submitTransfer() {
  transferring.value = true
  transferError.value = ''
  try {
    await api.post<SwalekhaTransferResult>('transfers', transferForm)
    transferOpen.value = false
    await refresh()
  } catch (err) {
    transferError.value = err instanceof Error ? err.message : 'Could not complete the transfer.'
  } finally {
    transferring.value = false
  }
}
</script>

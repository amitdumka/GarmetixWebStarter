<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" size="sm" to="/accounts">Accounts Hub</UButton>
        <h1 class="garmetix-dashboard-title mt-1">{{ account?.name || 'Account' }}</h1>
        <p v-if="account" class="text-sm text-muted">{{ account.accountType }} - {{ formatCurrency(account.currentBalance) }}</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load account" :description="error" />

    <UCard v-if="account">
      <div class="swalekha-grid">
        <div>
          <p class="text-xs font-medium uppercase text-muted">Current balance</p>
          <p class="text-lg font-semibold" :class="account.currentBalance < 0 ? 'text-error' : 'text-highlighted'">{{ formatCurrency(account.currentBalance) }}</p>
        </div>
        <div>
          <p class="text-xs font-medium uppercase text-muted">Opening balance</p>
          <p class="text-lg font-semibold text-highlighted">{{ formatCurrency(account.openingBalance) }}</p>
        </div>
        <div v-if="account.accountType === 'Bank'">
          <p class="text-xs font-medium uppercase text-muted">Bank</p>
          <p class="text-sm text-highlighted">{{ [account.bankName, account.accountNumberMasked].filter(Boolean).join(' - ') || '-' }}</p>
        </div>
        <div v-if="account.accountType === 'CreditCard'">
          <p class="text-xs font-medium uppercase text-muted">Credit limit</p>
          <p class="text-sm text-highlighted">{{ account.creditLimit ? formatCurrency(account.creditLimit) : '-' }}</p>
        </div>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="text-sm font-semibold">Add Transaction</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-5 sm:items-end" @submit.prevent="submitTransaction">
        <UFormField label="Type" name="transactionType" class="sm:col-span-1">
          <USelect v-model="txnForm.transactionType" :items="['Deposit', 'Withdrawal']" class="w-full" />
        </UFormField>
        <UFormField label="Amount" name="amount" class="sm:col-span-1">
          <UInput v-model.number="txnForm.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UFormField label="Date" name="transactionDate" class="sm:col-span-1">
          <UInput v-model="txnForm.transactionDate" type="date" class="w-full" />
        </UFormField>
        <UFormField label="Narration" name="narration" class="sm:col-span-1">
          <UInput v-model="txnForm.narration" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-plus" :loading="txnSaving" class="sm:col-span-1">Add</UButton>
      </form>
      <UAlert v-if="txnError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="txnError" />
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="transactions" :columns="transactionColumns" :loading="loading" class="w-full">
        <template #transactionDate-cell="{ row }">
          {{ formatDate(row.original.transactionDate) }}
        </template>
        <template #transactionType-cell="{ row }">
          <UBadge :color="transactionColor(row.original.transactionType)" variant="subtle">{{ row.original.transactionType }}</UBadge>
        </template>
        <template #counterAccountName-cell="{ row }">
          <span class="text-sm text-muted">{{ row.original.counterAccountName || '-' }}</span>
        </template>
        <template #amount-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span>
        </template>
        <template #runningBalance-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.runningBalance) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteTransaction(row.original)" />
        </template>
      </UTable>
      <div class="flex items-center justify-between gap-3 p-4">
        <p class="text-xs text-muted">{{ totalCount }} transactions</p>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-chevron-left" color="neutral" variant="soft" size="sm" :disabled="page <= 1" @click="changePage(page - 1)" />
          <span class="text-xs text-muted">Page {{ page }}</span>
          <UButton icon="i-lucide-chevron-right" color="neutral" variant="soft" size="sm" :disabled="page * pageSize >= totalCount" @click="changePage(page + 1)" />
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaAccount, type SwalekhaTransaction, type SwalekhaTransactionList, type SwalekhaTransactionPayload } from '../../utils/swalekha-api'

const route = useRoute()
const accountId = computed(() => String(route.params.id))
const api = useSwalekhaApiClient()

useHead({ title: 'Account Ledger - Swalekha' })

const loading = ref(false)
const error = ref('')
const account = ref<SwalekhaAccount | null>(null)
const transactions = ref<SwalekhaTransaction[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)

const transactionColumns = [
  { accessorKey: 'transactionDate', header: 'Date' },
  { accessorKey: 'transactionType', header: 'Type' },
  { accessorKey: 'narration', header: 'Narration' },
  { accessorKey: 'counterAccountName', header: 'Counter Account' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'runningBalance', header: 'Balance' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function transactionColor(type: string) {
  if (type === 'Deposit' || type === 'TransferIn') return 'success'
  return 'error'
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function loadAccount() {
  account.value = await api.get<SwalekhaAccount>(`accounts/${accountId.value}`)
}

async function loadTransactions() {
  const result = await api.get<SwalekhaTransactionList>(`accounts/${accountId.value}/transactions?page=${page.value}&pageSize=${pageSize.value}`)
  transactions.value = result.rows
  totalCount.value = result.totalCount
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await Promise.all([loadAccount(), loadTransactions()])
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load this account.'
  } finally {
    loading.value = false
  }
}

function changePage(next: number) {
  if (next < 1) return
  page.value = next
  loadTransactions()
}

onMounted(refreshAll)

const txnSaving = ref(false)
const txnError = ref('')
const txnForm = reactive<SwalekhaTransactionPayload>({
  amount: 0,
  transactionDate: new Date().toISOString().slice(0, 10),
  narration: '',
  transactionType: 'Deposit'
})

async function submitTransaction() {
  txnSaving.value = true
  txnError.value = ''
  try {
    await api.post(`accounts/${accountId.value}/transactions`, txnForm)
    txnForm.amount = 0
    txnForm.narration = ''
    page.value = 1
    await refreshAll()
  } catch (err) {
    txnError.value = err instanceof Error ? err.message : 'Could not add the transaction.'
  } finally {
    txnSaving.value = false
  }
}

async function deleteTransaction(entry: SwalekhaTransaction) {
  if (!confirm('Delete this transaction? The account balance will be reversed accordingly.')) return
  try {
    await api.del(`accounts/${accountId.value}/transactions/${entry.id}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the transaction.'
  }
}
</script>

<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" size="sm" to="/investments/mutual-funds">Mutual Funds</UButton>
        <h1 class="garmetix-dashboard-title mt-1">{{ fund?.schemeName || 'Fund' }}</h1>
        <p v-if="fund" class="text-sm text-muted">{{ fund.amc || 'No AMC set' }}<span v-if="fund.folioNumber"> - Folio {{ fund.folioNumber }}</span></p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UBadge v-if="fund" :color="fund.investmentMode === 'SIP' ? 'secondary' : 'primary'" variant="subtle">{{ fund.investmentMode }}</UBadge>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load this fund" :description="error" />

    <div class="swalekha-grid" v-if="returns">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Units Held</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ returns.currentUnits.toFixed(3) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Current Value</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ returns.currentValue != null ? formatCurrency(returns.currentValue) : 'Set NAV' }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Absolute Return</p>
        <p class="truncate text-lg font-semibold" :class="(returns.absoluteReturn ?? 0) >= 0 ? 'text-success' : 'text-error'">
          {{ returns.absoluteReturn != null ? formatCurrency(returns.absoluteReturn) : '-' }}
        </p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">XIRR</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ returns.xirrAvailable && returns.xirr != null ? `${returns.xirr.toFixed(2)}%` : '-' }}</p>
      </UCard>
    </div>
    <UAlert v-if="returns && !returns.xirrAvailable && returns.xirrNote" icon="i-lucide-info" color="neutral" variant="subtle" :description="returns.xirrNote" />

    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <h2 class="text-sm font-semibold">Current NAV</h2>
          <span class="text-sm text-muted">{{ fund?.currentNav != null ? formatCurrency(fund.currentNav) : 'Not set' }}<span v-if="fund?.currentNavUpdatedAt"> - updated {{ formatDate(fund.currentNavUpdatedAt) }}</span></span>
        </div>
      </template>
      <form class="flex flex-wrap items-end gap-3" @submit.prevent="submitNav">
        <UFormField label="Update NAV" name="currentNav" class="flex-1 min-w-40">
          <UInput v-model.number="navForm.currentNav" type="number" step="0.0001" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-save" :loading="navSaving">Update</UButton>
      </form>
      <UAlert v-if="navError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="navError" />
    </UCard>

    <UCard>
      <template #header>
        <h2 class="text-sm font-semibold">Record Transaction</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-6 sm:items-end" @submit.prevent="submitTransaction">
        <UFormField label="Type" name="transactionType" class="sm:col-span-1">
          <USelect v-model="txForm.transactionType" :items="['Purchase', 'SipInstallment', 'Redemption']" class="w-full" />
        </UFormField>
        <UFormField v-if="txForm.transactionType !== 'Redemption'" label="Amount" name="amount" class="sm:col-span-1">
          <UInput v-model.number="txForm.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UFormField v-else label="Units to Redeem" name="units" class="sm:col-span-1">
          <UInput v-model.number="txForm.units" type="number" step="0.001" class="w-full" />
        </UFormField>
        <UFormField label="NAV" name="navAtTransaction" class="sm:col-span-1">
          <UInput v-model.number="txForm.navAtTransaction" type="number" step="0.0001" class="w-full" />
        </UFormField>
        <UFormField label="Date" name="transactionDate" class="sm:col-span-1">
          <UInput v-model="txForm.transactionDate" type="date" class="w-full" />
        </UFormField>
        <UFormField label="Narration" name="narration" class="sm:col-span-1">
          <UInput v-model="txForm.narration" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-plus" :loading="txSaving" class="sm:col-span-1">Add</UButton>
      </form>
      <UAlert v-if="txError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="txError" />
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="transactions" :columns="transactionColumns" :loading="loading" class="w-full">
        <template #transactionDate-cell="{ row }">{{ formatDate(row.original.transactionDate) }}</template>
        <template #transactionType-cell="{ row }">
          <UBadge :color="row.original.transactionType === 'Redemption' ? 'error' : 'success'" variant="subtle">{{ row.original.transactionType }}</UBadge>
        </template>
        <template #units-cell="{ row }"><span class="font-mono text-sm">{{ row.original.units.toFixed(3) }}</span></template>
        <template #navAtTransaction-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.navAtTransaction) }}</span></template>
        <template #amount-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span></template>
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
import {
  useSwalekhaApiClient,
  type SwalekhaMutualFund,
  type SwalekhaMutualFundReturns,
  type SwalekhaMutualFundTransaction,
  type SwalekhaMutualFundTransactionList,
  type SwalekhaMutualFundTransactionPayload,
  type SwalekhaUpdateNavPayload
} from '../../../utils/swalekha-api'

const route = useRoute()
const fundId = computed(() => String(route.params.id))
const api = useSwalekhaApiClient()

useHead({ title: 'Mutual Fund - Swalekha' })

const loading = ref(false)
const error = ref('')
const fund = ref<SwalekhaMutualFund | null>(null)
const returns = ref<SwalekhaMutualFundReturns | null>(null)
const transactions = ref<SwalekhaMutualFundTransaction[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)

const transactionColumns = [
  { accessorKey: 'transactionDate', header: 'Date' },
  { accessorKey: 'transactionType', header: 'Type' },
  { accessorKey: 'units', header: 'Units' },
  { accessorKey: 'navAtTransaction', header: 'NAV' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'narration', header: 'Narration' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function loadFund() {
  fund.value = await api.get<SwalekhaMutualFund>(`mutual-funds/${fundId.value}`)
  navForm.currentNav = fund.value.currentNav || 0
}

async function loadReturns() {
  returns.value = await api.get<SwalekhaMutualFundReturns>(`mutual-funds/${fundId.value}/returns`)
}

async function loadTransactions() {
  const result = await api.get<SwalekhaMutualFundTransactionList>(`mutual-funds/${fundId.value}/transactions?page=${page.value}&pageSize=${pageSize.value}`)
  transactions.value = result.rows
  totalCount.value = result.totalCount
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await loadFund()
    await Promise.all([loadReturns(), loadTransactions()])
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load this fund.'
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

const navSaving = ref(false)
const navError = ref('')
const navForm = reactive<SwalekhaUpdateNavPayload>({ currentNav: 0 })

async function submitNav() {
  navSaving.value = true
  navError.value = ''
  try {
    await api.put(`mutual-funds/${fundId.value}/nav`, navForm)
    await refreshAll()
  } catch (err) {
    navError.value = err instanceof Error ? err.message : 'Could not update the NAV.'
  } finally {
    navSaving.value = false
  }
}

const txSaving = ref(false)
const txError = ref('')
const txForm = reactive<SwalekhaMutualFundTransactionPayload>(defaultTxForm())

function defaultTxForm(): SwalekhaMutualFundTransactionPayload {
  return { transactionType: 'Purchase', transactionDate: new Date().toISOString().substring(0, 10), amount: null, units: null, navAtTransaction: 0, narration: '' }
}

async function submitTransaction() {
  txSaving.value = true
  txError.value = ''
  try {
    await api.post(`mutual-funds/${fundId.value}/transactions`, txForm)
    Object.assign(txForm, defaultTxForm())
    page.value = 1
    await refreshAll()
  } catch (err) {
    txError.value = err instanceof Error ? err.message : 'Could not record the transaction.'
  } finally {
    txSaving.value = false
  }
}

async function deleteTransaction(transaction: SwalekhaMutualFundTransaction) {
  if (!confirm('Delete this transaction? This reverses its effect on units, invested amount, and the linked account balance.')) return
  try {
    await api.del(`mutual-funds/${fundId.value}/transactions/${transaction.id}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the transaction.'
  }
}
</script>

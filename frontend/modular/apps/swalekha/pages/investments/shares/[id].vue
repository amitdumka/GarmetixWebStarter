<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" size="sm" to="/investments/shares">Shares</UButton>
        <h1 class="garmetix-dashboard-title mt-1">{{ holding?.symbol || 'Holding' }}</h1>
        <p v-if="holding" class="text-sm text-muted">{{ holding.companyName || 'No company name set' }}<span v-if="holding.exchange"> - {{ holding.exchange }}</span></p>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load this holding" :description="error" />

    <div class="swalekha-grid" v-if="holding">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Quantity Held</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ holding.currentQuantity }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Current Value</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ holding.currentValue != null ? formatCurrency(holding.currentValue) : 'Set price' }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Unrealized P&amp;L</p>
        <p class="truncate text-lg font-semibold" :class="(holding.unrealizedPnL ?? 0) >= 0 ? 'text-success' : 'text-error'">
          {{ holding.unrealizedPnL != null ? formatCurrency(holding.unrealizedPnL) : '-' }}
        </p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Realized P&amp;L</p>
        <p class="truncate text-lg font-semibold" :class="holding.realizedPnL >= 0 ? 'text-success' : 'text-error'">{{ formatCurrency(holding.realizedPnL) }}</p>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between">
          <h2 class="text-sm font-semibold">Current Price</h2>
          <span class="text-sm text-muted">{{ holding?.currentPrice != null ? formatCurrency(holding.currentPrice) : 'Not set' }}<span v-if="holding?.currentPriceUpdatedAt"> - updated {{ formatDate(holding.currentPriceUpdatedAt) }}</span></span>
        </div>
      </template>
      <form class="flex flex-wrap items-end gap-3" @submit.prevent="submitPrice">
        <UFormField label="Update Price" name="currentPrice" class="flex-1 min-w-40">
          <UInput v-model.number="priceForm.currentPrice" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-save" :loading="priceSaving">Update</UButton>
      </form>
      <UAlert v-if="priceError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="priceError" />
    </UCard>

    <UCard>
      <template #header>
        <h2 class="text-sm font-semibold">Record Transaction</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-6 sm:items-end" @submit.prevent="submitTransaction">
        <UFormField label="Type" name="transactionType" class="sm:col-span-1">
          <USelect v-model="txForm.transactionType" :items="['Buy', 'Sell']" class="w-full" />
        </UFormField>
        <UFormField label="Quantity" name="quantity" class="sm:col-span-1">
          <UInput v-model.number="txForm.quantity" type="number" step="1" class="w-full" />
        </UFormField>
        <UFormField label="Price/Share" name="pricePerShare" class="sm:col-span-1">
          <UInput v-model.number="txForm.pricePerShare" type="number" step="0.01" class="w-full" />
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
          <UBadge :color="row.original.transactionType === 'Sell' ? 'error' : 'success'" variant="subtle">{{ row.original.transactionType }}</UBadge>
        </template>
        <template #pricePerShare-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.pricePerShare) }}</span></template>
        <template #amount-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span></template>
        <template #realizedPnLOnSale-cell="{ row }">
          <span v-if="row.original.realizedPnLOnSale != null" class="font-mono text-sm" :class="row.original.realizedPnLOnSale >= 0 ? 'text-success' : 'text-error'">
            {{ formatCurrency(row.original.realizedPnLOnSale) }}
          </span>
          <span v-else class="text-sm text-muted">-</span>
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
import {
  useSwalekhaApiClient,
  type SwalekhaShareHolding,
  type SwalekhaShareTransaction,
  type SwalekhaShareTransactionList,
  type SwalekhaShareTransactionPayload,
  type SwalekhaUpdateSharePricePayload
} from '../../../utils/swalekha-api'

const route = useRoute()
const holdingId = computed(() => String(route.params.id))
const api = useSwalekhaApiClient()

useHead({ title: 'Share Holding - Swalekha' })

const loading = ref(false)
const error = ref('')
const holding = ref<SwalekhaShareHolding | null>(null)
const transactions = ref<SwalekhaShareTransaction[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)

const transactionColumns = [
  { accessorKey: 'transactionDate', header: 'Date' },
  { accessorKey: 'transactionType', header: 'Type' },
  { accessorKey: 'quantity', header: 'Qty' },
  { accessorKey: 'pricePerShare', header: 'Price' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'realizedPnLOnSale', header: 'Realized P&L' },
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

async function loadHolding() {
  holding.value = await api.get<SwalekhaShareHolding>(`shares/${holdingId.value}`)
  priceForm.currentPrice = holding.value.currentPrice || 0
}

async function loadTransactions() {
  const result = await api.get<SwalekhaShareTransactionList>(`shares/${holdingId.value}/transactions?page=${page.value}&pageSize=${pageSize.value}`)
  transactions.value = result.rows
  totalCount.value = result.totalCount
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await loadHolding()
    await loadTransactions()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load this holding.'
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

const priceSaving = ref(false)
const priceError = ref('')
const priceForm = reactive<SwalekhaUpdateSharePricePayload>({ currentPrice: 0 })

async function submitPrice() {
  priceSaving.value = true
  priceError.value = ''
  try {
    await api.put(`shares/${holdingId.value}/price`, priceForm)
    await refreshAll()
  } catch (err) {
    priceError.value = err instanceof Error ? err.message : 'Could not update the price.'
  } finally {
    priceSaving.value = false
  }
}

const txSaving = ref(false)
const txError = ref('')
const txForm = reactive<SwalekhaShareTransactionPayload>(defaultTxForm())

function defaultTxForm(): SwalekhaShareTransactionPayload {
  return { transactionType: 'Buy', transactionDate: new Date().toISOString().substring(0, 10), quantity: 0, pricePerShare: 0, narration: '' }
}

async function submitTransaction() {
  txSaving.value = true
  txError.value = ''
  try {
    await api.post(`shares/${holdingId.value}/transactions`, txForm)
    Object.assign(txForm, defaultTxForm())
    page.value = 1
    await refreshAll()
  } catch (err) {
    txError.value = err instanceof Error ? err.message : 'Could not record the transaction.'
  } finally {
    txSaving.value = false
  }
}

async function deleteTransaction(transaction: SwalekhaShareTransaction) {
  if (!confirm('Delete this transaction? This reverses its effect on quantity, invested amount, realized P&L, and the linked account balance.')) return
  try {
    await api.del(`shares/${holdingId.value}/transactions/${transaction.id}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the transaction.'
  }
}
</script>

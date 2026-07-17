<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-line-chart" class="size-4" /> Investments</p>
        <h1 class="garmetix-dashboard-title">Shares &amp; Stocks</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Holding</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load holdings" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Total Invested</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalInvested) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Current Value</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalCurrentValue) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Unrealized P&amp;L</p>
        <p class="truncate text-lg font-semibold" :class="totalUnrealized >= 0 ? 'text-success' : 'text-error'">{{ formatCurrency(totalUnrealized) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Realized P&amp;L</p>
        <p class="truncate text-lg font-semibold" :class="totalRealized >= 0 ? 'text-success' : 'text-error'">{{ formatCurrency(totalRealized) }}</p>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="holdings" :columns="holdingColumns" :loading="loading" class="w-full">
        <template #currentQuantity-cell="{ row }"><span class="font-mono text-sm">{{ row.original.currentQuantity }}</span></template>
        <template #currentValue-cell="{ row }"><span class="font-mono text-sm">{{ row.original.currentValue != null ? formatCurrency(row.original.currentValue) : '-' }}</span></template>
        <template #unrealizedPnLPercent-cell="{ row }">
          <span v-if="row.original.unrealizedPnLPercent != null" class="font-mono text-sm" :class="row.original.unrealizedPnLPercent >= 0 ? 'text-success' : 'text-error'">
            {{ row.original.unrealizedPnLPercent.toFixed(2) }}%
          </span>
          <span v-else class="text-sm text-muted">-</span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" title="Transactions & P&L" @click="navigateTo(`/investments/shares/${row.original.id}`)" />
            <UButton icon="i-lucide-refresh-cw" color="primary" variant="ghost" size="sm" title="Update Price" @click="openPrice(row.original)" />
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteHolding(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Holding' : 'New Holding'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitHolding">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Symbol" name="symbol">
              <UInput v-model="form.symbol" required class="w-full" />
            </UFormField>
            <UFormField label="Company Name" name="companyName">
              <UInput v-model="form.companyName" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Exchange" name="exchange">
              <UInput v-model="form.exchange" placeholder="NSE, BSE..." class="w-full" />
            </UFormField>
            <UFormField label="Broker" name="broker">
              <UInput v-model="form.broker" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Demat Account" name="dematAccount">
            <UInput v-model="form.dematAccount" class="w-full" />
          </UFormField>

          <UFormField label="Linked Account" name="accountId" description="Debited on Buy, credited on Sell">
            <USelectMenu v-model="form.accountId" :items="accountOptions" value-key="value" placeholder="Not linked" class="w-full" />
          </UFormField>

          <USwitch v-model="form.isActive" label="Active" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Holding' }}</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="priceOpen" :title="`Update Price - ${priceTarget?.symbol || ''}`" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitPrice">
          <UFormField label="Current Price" name="currentPrice">
            <UInput v-model.number="priceForm.currentPrice" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UAlert v-if="priceError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="priceError" />
          <UButton type="submit" icon="i-lucide-save" :loading="priceSaving" block>Update Price</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaShareHolding,
  type SwalekhaShareHoldingPayload,
  type SwalekhaUpdateSharePricePayload
} from '../../../utils/swalekha-api'

useHead({ title: 'Shares - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const holdings = ref<SwalekhaShareHolding[]>([])
const accounts = ref<SwalekhaAccount[]>([])

const holdingColumns = [
  { accessorKey: 'symbol', header: 'Symbol' },
  { accessorKey: 'companyName', header: 'Company' },
  { accessorKey: 'currentQuantity', header: 'Qty' },
  { accessorKey: 'currentValue', header: 'Current Value' },
  { accessorKey: 'unrealizedPnLPercent', header: 'Unrealized' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const accountOptions = computed(() => [
  { label: 'Not linked', value: null },
  ...accounts.value.filter(a => a.isActive).map(a => ({ label: `${a.name} (${a.accountType})`, value: a.id }))
])

const totalInvested = computed(() => holdings.value.reduce((sum, h) => sum + h.totalInvested, 0))
const totalCurrentValue = computed(() => holdings.value.reduce((sum, h) => sum + (h.currentValue ?? h.totalInvested), 0))
const totalUnrealized = computed(() => totalCurrentValue.value - totalInvested.value)
const totalRealized = computed(() => holdings.value.reduce((sum, h) => sum + h.realizedPnL, 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [holdingsResult, accountsResult] = await Promise.all([
      api.get<SwalekhaShareHolding[]>('shares?includeInactive=true'),
      api.get<SwalekhaAccount[]>('accounts')
    ])
    holdings.value = holdingsResult
    accounts.value = accountsResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load holdings.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaShareHoldingPayload>(defaultForm())

function defaultForm(): SwalekhaShareHoldingPayload {
  return { symbol: '', companyName: '', exchange: '', dematAccount: '', broker: '', accountId: null, isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(holding: SwalekhaShareHolding) {
  editingId.value = holding.id
  formError.value = ''
  Object.assign(form, {
    symbol: holding.symbol,
    companyName: holding.companyName || '',
    exchange: holding.exchange || '',
    dematAccount: holding.dematAccount || '',
    broker: holding.broker || '',
    accountId: holding.accountId || null,
    isActive: holding.isActive,
    notes: holding.notes || ''
  })
  formOpen.value = true
}

async function submitHolding() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`shares/${editingId.value}`, form)
    } else {
      await api.post('shares', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the holding.'
  } finally {
    saving.value = false
  }
}

async function deleteHolding(holding: SwalekhaShareHolding) {
  if (!confirm(`Delete "${holding.symbol}"? Its transaction history is kept for the record.`)) return
  try {
    await api.del(`shares/${holding.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the holding.'
  }
}

const priceOpen = ref(false)
const priceSaving = ref(false)
const priceError = ref('')
const priceTarget = ref<SwalekhaShareHolding | null>(null)
const priceForm = reactive<SwalekhaUpdateSharePricePayload>({ currentPrice: 0 })

function openPrice(holding: SwalekhaShareHolding) {
  priceTarget.value = holding
  priceError.value = ''
  priceForm.currentPrice = holding.currentPrice || 0
  priceOpen.value = true
}

async function submitPrice() {
  if (!priceTarget.value) return
  priceSaving.value = true
  priceError.value = ''
  try {
    await api.put(`shares/${priceTarget.value.id}/price`, priceForm)
    priceOpen.value = false
    await refresh()
  } catch (err) {
    priceError.value = err instanceof Error ? err.message : 'Could not update the price.'
  } finally {
    priceSaving.value = false
  }
}
</script>

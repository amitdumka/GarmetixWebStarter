<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-pie-chart" class="size-4" /> Investments</p>
        <h1 class="garmetix-dashboard-title">Mutual Funds &amp; SIPs</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Fund</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load mutual funds" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Total Invested</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalInvested) }}</p>
          </div>
          <UIcon name="i-lucide-wallet" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Current Value</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalCurrentValue) }}</p>
          </div>
          <UIcon name="i-lucide-trending-up" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Overall Return</p>
            <p class="truncate text-lg font-semibold" :class="totalReturn >= 0 ? 'text-success' : 'text-error'">{{ formatCurrency(totalReturn) }}</p>
          </div>
          <UIcon name="i-lucide-scale" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">SIPs Due This Month</p>
            <p class="truncate text-lg font-semibold text-warning">{{ funds.filter(f => f.sipDueThisMonth).length }}</p>
          </div>
          <UIcon name="i-lucide-calendar-clock" class="size-5 shrink-0 text-warning" />
        </div>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="funds" :columns="fundColumns" :loading="loading" class="w-full">
        <template #investmentMode-cell="{ row }">
          <div class="flex items-center gap-1.5">
            <UBadge :color="row.original.investmentMode === 'SIP' ? 'secondary' : 'primary'" variant="subtle">{{ row.original.investmentMode }}</UBadge>
            <UBadge v-if="row.original.sipDueThisMonth" color="warning" variant="subtle" size="xs">SIP Due</UBadge>
          </div>
        </template>
        <template #currentUnits-cell="{ row }">
          <span class="font-mono text-sm">{{ row.original.currentUnits.toFixed(3) }}</span>
        </template>
        <template #currentValue-cell="{ row }">
          <span class="font-mono text-sm">{{ row.original.currentValue != null ? formatCurrency(row.original.currentValue) : '-' }}</span>
        </template>
        <template #returnPercent-cell="{ row }">
          <span v-if="row.original.returnPercent != null" class="font-mono text-sm" :class="row.original.returnPercent >= 0 ? 'text-success' : 'text-error'">
            {{ row.original.returnPercent.toFixed(2) }}%
          </span>
          <span v-else class="text-sm text-muted">-</span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" title="Transactions & Returns" @click="navigateTo(`/investments/mutual-funds/${row.original.id}`)" />
            <UButton icon="i-lucide-refresh-cw" color="primary" variant="ghost" size="sm" title="Update NAV" @click="openNav(row.original)" />
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteFund(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Fund' : 'New Fund'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitFund">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Scheme Name" name="schemeName">
              <UInput v-model="form.schemeName" required class="w-full" />
            </UFormField>
            <UFormField label="AMC" name="amc">
              <UInput v-model="form.amc" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Folio Number" name="folioNumber">
            <UInput v-model="form.folioNumber" class="w-full" />
          </UFormField>

          <UFormField label="Linked Account" name="accountId" description="Debited on Purchase/SIP, credited on Redemption">
            <USelectMenu v-model="form.accountId" :items="accountOptions" value-key="value" placeholder="Not linked" class="w-full" />
          </UFormField>

          <UFormField label="Investment Mode" name="investmentMode">
            <USelect v-model="form.investmentMode" :items="['Lumpsum', 'SIP']" class="w-full" />
          </UFormField>

          <div v-if="form.investmentMode === 'SIP'" class="grid gap-3 sm:grid-cols-2">
            <UFormField label="SIP Amount" name="sipAmount">
              <UInput v-model.number="form.sipAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="SIP Day Of Month" name="sipDayOfMonth">
              <UInput v-model.number="form.sipDayOfMonth" type="number" min="1" max="28" class="w-full" />
            </UFormField>
          </div>

          <USwitch v-model="form.isActive" label="Active" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Fund' }}</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="navOpen" :title="`Update NAV - ${navTarget?.schemeName || ''}`" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitNav">
          <UFormField label="Current NAV" name="currentNav">
            <UInput v-model.number="navForm.currentNav" type="number" step="0.0001" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UAlert v-if="navError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="navError" />
          <UButton type="submit" icon="i-lucide-save" :loading="navSaving" block>Update NAV</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaMutualFund,
  type SwalekhaMutualFundPayload,
  type SwalekhaUpdateNavPayload
} from '../../../utils/swalekha-api'

useHead({ title: 'Mutual Funds - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const funds = ref<SwalekhaMutualFund[]>([])
const accounts = ref<SwalekhaAccount[]>([])

const fundColumns = [
  { accessorKey: 'schemeName', header: 'Scheme' },
  { accessorKey: 'amc', header: 'AMC' },
  { accessorKey: 'investmentMode', header: 'Mode' },
  { accessorKey: 'currentUnits', header: 'Units' },
  { accessorKey: 'currentValue', header: 'Current Value' },
  { accessorKey: 'returnPercent', header: 'Return' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const accountOptions = computed(() => [
  { label: 'Not linked', value: null },
  ...accounts.value.filter(a => a.isActive).map(a => ({ label: `${a.name} (${a.accountType})`, value: a.id }))
])

const totalInvested = computed(() => funds.value.reduce((sum, f) => sum + f.totalInvested, 0))
const totalCurrentValue = computed(() => funds.value.reduce((sum, f) => sum + (f.currentValue ?? f.totalInvested), 0))
const totalReturn = computed(() => totalCurrentValue.value - totalInvested.value)

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [fundsResult, accountsResult] = await Promise.all([
      api.get<SwalekhaMutualFund[]>('mutual-funds?includeInactive=true'),
      api.get<SwalekhaAccount[]>('accounts')
    ])
    funds.value = fundsResult
    accounts.value = accountsResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load mutual funds.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaMutualFundPayload>(defaultForm())

function defaultForm(): SwalekhaMutualFundPayload {
  return { schemeName: '', amc: '', folioNumber: '', accountId: null, investmentMode: 'Lumpsum', sipAmount: null, sipDayOfMonth: null, isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(fund: SwalekhaMutualFund) {
  editingId.value = fund.id
  formError.value = ''
  Object.assign(form, {
    schemeName: fund.schemeName,
    amc: fund.amc || '',
    folioNumber: fund.folioNumber || '',
    accountId: fund.accountId || null,
    investmentMode: fund.investmentMode,
    sipAmount: fund.sipAmount ?? null,
    sipDayOfMonth: fund.sipDayOfMonth ?? null,
    isActive: fund.isActive,
    notes: fund.notes || ''
  })
  formOpen.value = true
}

async function submitFund() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`mutual-funds/${editingId.value}`, form)
    } else {
      await api.post('mutual-funds', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the fund.'
  } finally {
    saving.value = false
  }
}

async function deleteFund(fund: SwalekhaMutualFund) {
  if (!confirm(`Delete "${fund.schemeName}"? Its transaction history is kept for the record.`)) return
  try {
    await api.del(`mutual-funds/${fund.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the fund.'
  }
}

const navOpen = ref(false)
const navSaving = ref(false)
const navError = ref('')
const navTarget = ref<SwalekhaMutualFund | null>(null)
const navForm = reactive<SwalekhaUpdateNavPayload>({ currentNav: 0 })

function openNav(fund: SwalekhaMutualFund) {
  navTarget.value = fund
  navError.value = ''
  navForm.currentNav = fund.currentNav || 0
  navOpen.value = true
}

async function submitNav() {
  if (!navTarget.value) return
  navSaving.value = true
  navError.value = ''
  try {
    await api.put(`mutual-funds/${navTarget.value.id}/nav`, navForm)
    navOpen.value = false
    await refresh()
  } catch (err) {
    navError.value = err instanceof Error ? err.message : 'Could not update the NAV.'
  } finally {
    navSaving.value = false
  }
}
</script>

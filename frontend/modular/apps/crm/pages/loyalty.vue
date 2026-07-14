<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-gift" class="size-4" /> CRM loyalty</p>
          <h2 class="garmetix-dashboard-title">Loyalty Program</h2>
          <p class="garmetix-dashboard-subtitle">Configure store loyalty, inspect customer point balance and post guarded manual adjustments.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-users-round" @click="router.push('/customers')">Customers</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-wallet-cards" @click="router.push('/customers/dues-reconciliation')">Dues Reco</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label"><UIcon :name="metric.icon" class="mr-1 inline size-4" /> {{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.value }}</p>
        <p class="garmetix-metric-caption">{{ metric.detail }}</p>
      </div>
    </section>

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-3">
      <UFormField label="Company">
        <USelect v-model="selectedCompanyId" :items="companyOptions" placeholder="Select company" @update:model-value="onCompanyChanged" />
      </UFormField>
      <UFormField label="Store">
        <USelect v-model="selectedStoreId" :items="storeOptions" placeholder="Select store" @update:model-value="loadProgram" />
      </UFormField>
      <UFormField label="Customer">
        <USelect v-model="selectedCustomerId" :items="customerOptions" placeholder="Select customer" @update:model-value="loadCustomerLoyalty" />
      </UFormField>
    </div>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(360px,0.75fr)]">
      <div class="garmetix-section-card">
        <div class="mb-4 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <p class="garmetix-kicker"><UIcon name="i-lucide-sliders-horizontal" class="size-4" /> Store rules</p>
            <h3 class="text-xl font-semibold text-highlighted">Program Setup</h3>
            <p class="text-sm text-muted">Saved against the selected store and used by POS invoice loyalty calculation.</p>
          </div>
          <UBadge :color="program.enabled ? 'success' : 'neutral'" variant="subtle">{{ program.enabled ? 'Enabled' : 'Disabled' }}</UBadge>
        </div>

        <div class="grid gap-3 md:grid-cols-2">
          <UFormField label="Program name">
            <UInput v-model="program.name" placeholder="Garmetix Loyalty" />
          </UFormField>
          <div class="rounded-md border border-default p-3">
            <UCheckbox v-model="program.enabled" label="Enable loyalty for this store" />
          </div>
          <UFormField label="Earn points per rupee">
            <UInput v-model.number="program.earnPointsPerRupee" type="number" min="0" step="0.01" />
          </UFormField>
          <UFormField label="Redeem value per point">
            <UInput v-model.number="program.redeemValuePerPoint" type="number" min="0" step="0.01" />
          </UFormField>
          <UFormField label="Minimum bill amount">
            <UInput v-model.number="program.minimumBillAmount" type="number" min="0" step="1" />
          </UFormField>
          <UFormField label="Expiry days">
            <UInput v-model.number="program.expiryDays" type="number" min="0" step="1" placeholder="Optional" />
          </UFormField>
        </div>

        <div class="mt-4 flex flex-wrap justify-end gap-2">
          <UButton color="neutral" variant="ghost" icon="i-lucide-rotate-ccw" :disabled="savingProgram" @click="loadProgram">Reload</UButton>
          <UButton icon="i-lucide-save" :loading="savingProgram" :disabled="!canSaveProgram" @click="saveProgram">Save Program</UButton>
        </div>
      </div>

      <div class="garmetix-section-card">
        <p class="garmetix-kicker"><UIcon name="i-lucide-user-check" class="size-4" /> Customer balance</p>
        <h3 class="text-xl font-semibold text-highlighted">{{ selectedCustomerName }}</h3>
        <p class="text-sm text-muted">{{ selectedCustomerMobile }}</p>

        <div class="mt-4 grid gap-3 sm:grid-cols-2">
          <div class="rounded-md border border-default p-3">
            <p class="text-xs uppercase text-muted">Points</p>
            <p class="text-2xl font-semibold text-highlighted">{{ number(readNumber(summary, ['loyaltyPoints'])) }}</p>
          </div>
          <div class="rounded-md border border-default p-3">
            <p class="text-xs uppercase text-muted">Credit</p>
            <p class="text-2xl font-semibold text-highlighted">{{ money(readNumber(summary, ['creditBalance'])) }}</p>
          </div>
          <div class="rounded-md border border-default p-3">
            <p class="text-xs uppercase text-muted">Total billed</p>
            <p class="text-lg font-semibold text-highlighted">{{ money(readNumber(summary, ['amount', 'totalAmount'])) }}</p>
          </div>
          <div class="rounded-md border border-default p-3">
            <p class="text-xs uppercase text-muted">Bills</p>
            <p class="text-lg font-semibold text-highlighted">{{ number(readNumber(summary, ['billCount'])) }}</p>
          </div>
        </div>

        <UAlert v-if="adjustmentError" class="mt-4" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="adjustmentError" />

        <div class="mt-4 grid gap-3 sm:grid-cols-2">
          <UFormField label="Points in">
            <UInput v-model.number="adjustment.pointsIn" type="number" min="0" step="1" />
          </UFormField>
          <UFormField label="Points out">
            <UInput v-model.number="adjustment.pointsOut" type="number" min="0" step="1" />
          </UFormField>
          <UFormField label="Remarks" class="sm:col-span-2">
            <UTextarea v-model="adjustment.remarks" :rows="3" placeholder="Audit note for this manual loyalty adjustment" />
          </UFormField>
        </div>

        <div class="mt-4 flex justify-end">
          <UButton icon="i-lucide-plus-circle" :loading="savingAdjustment" :disabled="!canAdjust" @click="adjustPoints">Post Adjustment</UButton>
        </div>
      </div>
    </section>

    <div class="garmetix-section-card">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-list-tree" class="size-4" /> Point ledger</p>
          <h3 class="text-xl font-semibold text-highlighted">Loyalty Activity</h3>
        </div>
        <UFormField label="Search ledger">
          <UInput v-model="ledgerSearch" icon="i-lucide-search" placeholder="Search source, remarks or points" class="lg:w-80" />
        </UFormField>
      </div>

      <UAlert v-if="ledgerError" class="mb-4" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="ledgerError" />

      <div class="overflow-x-auto">
        <table class="w-full min-w-[860px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr>
              <th class="border-b border-default p-3">Date</th>
              <th class="border-b border-default p-3">Source</th>
              <th class="border-b border-default p-3 text-right">In</th>
              <th class="border-b border-default p-3 text-right">Out</th>
              <th class="border-b border-default p-3 text-right">Balance</th>
              <th class="border-b border-default p-3">Remarks</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="ledgerLoading"><td colspan="6" class="p-8 text-center text-muted">Loading loyalty ledger...</td></tr>
            <tr v-else-if="!filteredLedger.length"><td colspan="6" class="p-8 text-center text-muted">No loyalty activity for the selected customer.</td></tr>
            <template v-else>
              <tr v-for="row in filteredLedger" :key="String(row.id || `${row.onDate}-${row.sourceNumber}`)">
                <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
                <td class="border-b border-default p-3">
                  <p class="font-medium text-highlighted">{{ readText(row, ['sourceType']) }}</p>
                  <p class="text-xs text-muted">{{ readText(row, ['sourceNumber'], '') }}</p>
                </td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pointsIn'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pointsOut'])) }}</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ number(readNumber(row, ['balanceAfter'])) }}</td>
                <td class="border-b border-default p-3">{{ readText(row, ['remarks']) }}</td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useCrmApiClient } from '../utils/crm-api'

const router = useRouter()
const route = useRoute()
const toast = useToast()
const { get, post } = useCrmApiClient()

const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const customers = ref<ApiRecord[]>([])
const selectedCompanyId = ref('')
const selectedStoreId = ref('')
const selectedCustomerId = ref('')
const summary = ref<ApiRecord | null>(null)
const ledger = ref<ApiRecord[]>([])
const ledgerSearch = ref('')
const loading = ref(false)
const ledgerLoading = ref(false)
const savingProgram = ref(false)
const savingAdjustment = ref(false)
const loadError = ref('')
const ledgerError = ref('')
const adjustmentError = ref('')

const program = reactive({
  enabled: true,
  name: 'Garmetix Loyalty',
  earnPointsPerRupee: 0,
  redeemValuePerPoint: 0,
  minimumBillAmount: 0,
  expiryDays: null as number | null
})

const adjustment = reactive({
  pointsIn: 0,
  pointsOut: 0,
  remarks: ''
})

const companyOptions = computed(() => companies.value.map(item => ({
  label: readText(item, ['name', 'companyName'], 'Company'),
  value: String(item.id || '')
})))

const storeOptions = computed(() => {
  const filtered = stores.value.filter(item => !selectedCompanyId.value || readText(item, ['companyId'], '') === selectedCompanyId.value)
  return filtered.map(item => ({
    label: readText(item, ['name', 'storeName'], 'Store'),
    value: String(item.id || '')
  }))
})

const customerOptions = computed(() => customers.value.map(item => ({
  label: `${readText(item, ['name'], 'Customer')} - ${readText(item, ['mobileNumber'], 'No mobile')}`,
  value: String(item.id || '')
})))

const selectedStore = computed(() => stores.value.find(item => String(item.id || '') === selectedStoreId.value) ?? null)
const selectedCustomer = computed(() => customers.value.find(item => String(item.id || '') === selectedCustomerId.value) ?? null)
const selectedCustomerName = computed(() => readText(summary.value ?? selectedCustomer.value, ['customerName', 'name'], selectedCustomerId.value ? 'Customer' : 'Select a customer'))
const selectedCustomerMobile = computed(() => readText(selectedCustomer.value, ['mobileNumber', 'mobile'], selectedCustomerId.value ? 'Mobile not available' : 'Choose a customer to load balance and ledger.'))

const canSaveProgram = computed(() => Boolean(selectedCompanyId.value && selectedStoreId.value && selectedStoreGroupId()))
const canAdjust = computed(() => Boolean(selectedCustomerId.value && canSaveProgram.value && (Number(adjustment.pointsIn || 0) > 0 || Number(adjustment.pointsOut || 0) > 0)))

const filteredLedger = computed(() => {
  const term = ledgerSearch.value.trim().toLowerCase()
  if (!term) return ledger.value
  return ledger.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

const metrics = computed(() => [
  { label: 'Program', value: program.enabled ? 'Active' : 'Off', detail: selectedStore.value ? readText(selectedStore.value, ['name', 'storeName']) : 'Select store', icon: 'i-lucide-gift' },
  { label: 'Customers', value: number(customers.value.length), detail: 'Customer master rows', icon: 'i-lucide-users-round' },
  { label: 'Outstanding Points', value: number(customers.value.reduce((sum, item) => sum + readNumber(item, ['loyaltyPoints']), 0)), detail: 'Customer point balance', icon: 'i-lucide-coins' },
  { label: 'Customer Credit', value: money(customers.value.reduce((sum, item) => sum + readNumber(item, ['creditBalance']), 0)), detail: 'Advance / credit-note balance', icon: 'i-lucide-wallet-cards' }
])

function selectedStoreGroupId() {
  return readText(selectedStore.value, ['storeGroupId', 'groupId'], '')
}

function money(value: number) {
  return formatIndianMoney(value)
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function applyProgram(row: ApiRecord | null | undefined) {
  program.enabled = row?.enabled === undefined ? true : Boolean(row.enabled)
  program.name = readText(row, ['name'], 'Garmetix Loyalty')
  program.earnPointsPerRupee = readNumber(row, ['earnPointsPerRupee'])
  program.redeemValuePerPoint = readNumber(row, ['redeemValuePerPoint'])
  program.minimumBillAmount = readNumber(row, ['minimumBillAmount'])
  const expiry = readNumber(row, ['expiryDays'])
  program.expiryDays = expiry > 0 ? expiry : null
}

function resetAdjustment() {
  adjustment.pointsIn = 0
  adjustment.pointsOut = 0
  adjustment.remarks = ''
  adjustmentError.value = ''
}

async function loadWorkspace() {
  const [companyData, storeData, customerData] = await Promise.all([
    get<unknown>('companies').catch(() => []),
    get<unknown>('stores').catch(() => []),
    get<unknown>('customers').catch(() => [])
  ])
  companies.value = toRows(companyData)
  stores.value = toRows(storeData)
  customers.value = toRows(customerData)
  selectedCompanyId.value = selectedCompanyId.value || readText(stores.value[0], ['companyId'], '') || readText(companies.value[0], ['id'], '')
  selectedStoreId.value = selectedStoreId.value || readText(stores.value.find(item => readText(item, ['companyId'], '') === selectedCompanyId.value) ?? stores.value[0], ['id'], '')
  selectedCustomerId.value = String(route.query.customerId || selectedCustomerId.value || customers.value[0]?.id || '')
}

async function loadProgram() {
  if (!selectedStoreId.value) {
    applyProgram(null)
    return
  }

  const row = await get<ApiRecord | null>('loyalty/program', { storeId: selectedStoreId.value })
  applyProgram(row)
}

async function loadCustomerLoyalty() {
  summary.value = null
  ledger.value = []
  ledgerError.value = ''
  resetAdjustment()
  if (!selectedCustomerId.value) return

  ledgerLoading.value = true
  try {
    const [summaryRow, ledgerRows] = await Promise.all([
      get<ApiRecord>(`loyalty/customers/${selectedCustomerId.value}`),
      get<ApiRecord[]>(`loyalty/customers/${selectedCustomerId.value}/ledger`)
    ])
    summary.value = summaryRow
    ledger.value = Array.isArray(ledgerRows) ? ledgerRows : toRows(ledgerRows)
  } catch (caught) {
    ledgerError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load customer loyalty.')
    toast.add({ title: 'Could not load loyalty', description: ledgerError.value, color: 'error' })
  } finally {
    ledgerLoading.value = false
  }
}

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    await loadWorkspace()
    await loadProgram()
    await loadCustomerLoyalty()
  } catch (caught) {
    loadError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load loyalty page.')
    toast.add({ title: 'Could not load loyalty page', description: loadError.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function onCompanyChanged() {
  const nextStore = stores.value.find(item => readText(item, ['companyId'], '') === selectedCompanyId.value)
  selectedStoreId.value = readText(nextStore, ['id'], '')
  await loadProgram()
}

async function saveProgram() {
  if (!canSaveProgram.value) return
  savingProgram.value = true
  try {
    const saved = await post<ApiRecord>('loyalty/program', {
      companyId: selectedCompanyId.value,
      storeGroupId: selectedStoreGroupId(),
      storeId: selectedStoreId.value,
      enabled: Boolean(program.enabled),
      name: program.name,
      earnPointsPerRupee: Number(program.earnPointsPerRupee || 0),
      redeemValuePerPoint: Number(program.redeemValuePerPoint || 0),
      minimumBillAmount: Number(program.minimumBillAmount || 0),
      expiryDays: program.expiryDays ? Number(program.expiryDays) : null
    })
    applyProgram(saved)
    toast.add({ title: 'Loyalty program saved', color: 'success' })
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'Could not save loyalty program.')
    toast.add({ title: 'Could not save loyalty program', description: message, color: 'error' })
  } finally {
    savingProgram.value = false
  }
}

async function adjustPoints() {
  if (!canAdjust.value) return
  savingAdjustment.value = true
  adjustmentError.value = ''
  try {
    const saved = await post<ApiRecord>(`loyalty/customers/${selectedCustomerId.value}/adjust`, {
      companyId: selectedCompanyId.value,
      storeGroupId: selectedStoreGroupId(),
      storeId: selectedStoreId.value,
      pointsIn: Number(adjustment.pointsIn || 0),
      pointsOut: Number(adjustment.pointsOut || 0),
      remarks: adjustment.remarks
    })
    summary.value = saved
    toast.add({ title: 'Loyalty adjustment posted', color: 'success' })
    resetAdjustment()
    await loadCustomerLoyalty()
  } catch (caught) {
    adjustmentError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not post loyalty adjustment.')
    toast.add({ title: 'Could not post loyalty adjustment', description: adjustmentError.value, color: 'error' })
  } finally {
    savingAdjustment.value = false
  }
}

onMounted(refresh)
useHead({ title: 'Loyalty - Garmetix CRM' })
</script>

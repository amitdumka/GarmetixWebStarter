<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-users-round" class="size-4" /> CRM customer master</p>
          <h2 class="garmetix-dashboard-title">Customers</h2>
          <p class="garmetix-dashboard-subtitle">Manage customer identity, GST details, store credit and loyalty balances.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-user-plus" @click="router.push('/customers/new')">New Customer</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-wallet-cards" @click="router.push('/customers/dues-reconciliation')">Dues Reco</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-gift" @click="router.push('/loyalty')">Loyalty</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label"><UIcon :name="metric.icon" class="mr-1 inline size-4" /> {{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.value }}</p>
        <p class="garmetix-metric-caption">{{ metric.meta }}</p>
      </div>
    </section>

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_auto]">
      <UFormField label="Search">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search customer, mobile, GSTIN, credit, loyalty" />
      </UFormField>
      <div class="flex flex-wrap items-end gap-2">
        <UBadge color="neutral" variant="soft">{{ filteredCustomers.length }} of {{ customers.length }} customer(s)</UBadge>
        <UButton color="neutral" variant="ghost" icon="i-lucide-x" :disabled="!search" @click="search = ''">Clear</UButton>
      </div>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[980px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Name</th>
            <th class="border-b border-default p-3">Mobile</th>
            <th class="border-b border-default p-3">GSTIN</th>
            <th class="border-b border-default p-3 text-right">Credit</th>
            <th class="border-b border-default p-3 text-right">Loyalty</th>
            <th class="border-b border-default p-3">GST Status</th>
            <th class="border-b border-default p-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="7" class="p-8 text-center text-muted">Loading customers...</td>
          </tr>
          <tr v-else-if="!filteredCustomers.length">
            <td colspan="7" class="p-8 text-center text-muted">{{ search ? 'No matching customers.' : 'No customers yet.' }}</td>
          </tr>
          <tr v-for="customer in filteredCustomers" v-else :key="String(customer.id)">
            <td class="border-b border-default p-3">
              <p class="font-semibold text-highlighted">{{ readText(customer, ['name'], 'Customer') }}</p>
              <p class="text-xs text-muted">{{ readText(customer, ['email', 'city', 'state']) }}</p>
            </td>
            <td class="border-b border-default p-3">{{ readText(customer, ['mobileNumber']) }}</td>
            <td class="border-b border-default p-3">{{ readText(customer, ['gstin', 'gSTIN']) }}</td>
            <td class="border-b border-default p-3 text-right">{{ money(readNumber(customer, ['creditBalance'])) }}</td>
            <td class="border-b border-default p-3 text-right">{{ number(readNumber(customer, ['loyaltyPoints'])) }}</td>
            <td class="border-b border-default p-3">
              <UBadge :color="gstStatus(customer).color" variant="subtle">{{ gstStatus(customer).label }}</UBadge>
            </td>
            <td class="border-b border-default p-3">
              <div class="flex flex-wrap justify-end gap-2">
                <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-pencil" @click="router.push(`/customers/${customer.id}`)">Edit</UButton>
                <UButton size="xs" icon="i-lucide-gift" @click="openLoyalty(customer)">Loyalty</UButton>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="selectedCustomer" class="garmetix-section-card">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-gift" class="size-4" /> Loyalty ledger</p>
          <h3 class="text-xl font-semibold text-highlighted">{{ readText(selectedCustomer, ['name'], 'Customer') }}</h3>
          <p class="text-sm text-muted">
            Balance {{ number(readNumber(selectedCustomer, ['loyaltyPoints'])) }} points | Credit {{ money(readNumber(selectedCustomer, ['creditBalance'])) }}
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-gift" @click="router.push(`/loyalty?customerId=${selectedCustomer.id}`)">Manage Loyalty</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="closeLoyalty">Close</UButton>
        </div>
      </div>

      <UAlert v-if="ledgerError" class="mt-3" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="ledgerError" />
      <div class="mt-4 overflow-x-auto">
        <table class="w-full min-w-[760px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr><th class="border-b border-default p-3">Date</th><th class="border-b border-default p-3">Source</th><th class="border-b border-default p-3 text-right">In</th><th class="border-b border-default p-3 text-right">Out</th><th class="border-b border-default p-3 text-right">Balance</th><th class="border-b border-default p-3">Remarks</th></tr>
          </thead>
          <tbody>
            <tr v-if="ledgerLoading"><td colspan="6" class="p-6 text-center text-muted">Loading loyalty ledger...</td></tr>
            <tr v-else-if="!ledger.length"><td colspan="6" class="p-6 text-center text-muted">No loyalty activity.</td></tr>
            <tr v-for="row in ledger" v-else :key="String(row.id || `${row.onDate}-${row.sourceNumber}`)">
              <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
              <td class="border-b border-default p-3">{{ readText(row, ['sourceType']) }} {{ readText(row, ['sourceNumber'], '') }}</td>
              <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pointsIn'])) }}</td>
              <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pointsOut'])) }}</td>
              <td class="border-b border-default p-3 text-right font-semibold">{{ number(readNumber(row, ['balanceAfter'])) }}</td>
              <td class="border-b border-default p-3">{{ readText(row, ['remarks']) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get } = useCrmApiClient()

const customers = ref<ApiRecord[]>([])
const search = ref('')
const loading = ref(false)
const loadError = ref('')
const selectedCustomer = ref<ApiRecord | null>(null)
const ledger = ref<ApiRecord[]>([])
const ledgerLoading = ref(false)
const ledgerError = ref('')

const filteredCustomers = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return customers.value
  return customers.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

const metrics = computed(() => [
  { label: 'Customers', value: number(customers.value.length), meta: 'Total customer master', icon: 'i-lucide-users-round' },
  { label: 'Loyalty Points', value: number(customers.value.reduce((sum, item) => sum + readNumber(item, ['loyaltyPoints']), 0)), meta: 'Total outstanding points', icon: 'i-lucide-gift' },
  { label: 'Credit Balance', value: money(customers.value.reduce((sum, item) => sum + readNumber(item, ['creditBalance']), 0)), meta: 'Advance / credit-note balance', icon: 'i-lucide-wallet-cards' },
  { label: 'GST Alerts', value: number(customers.value.filter(item => Boolean(item.gstMismatchAlert)).length), meta: 'Mismatch warnings', icon: 'i-lucide-triangle-alert' }
])

function money(value: number) {
  return formatIndianMoney(value)
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function gstStatus(customer: ApiRecord) {
  if (customer.gstMismatchAlert) return { label: 'Mismatch', color: 'warning' as const }
  if (customer.gstVerified) return { label: 'Verified', color: 'success' as const }
  return { label: 'Pending', color: 'neutral' as const }
}

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    customers.value = await get<ApiRecord[]>('customers')
    if (selectedCustomer.value?.id) {
      selectedCustomer.value = customers.value.find(item => item.id === selectedCustomer.value?.id) || selectedCustomer.value
    }
  } catch (caught) {
    loadError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load customers.')
    toast.add({ title: 'Could not load customers', description: loadError.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function openLoyalty(customer: ApiRecord) {
  selectedCustomer.value = customer
  ledgerLoading.value = true
  ledgerError.value = ''
  ledger.value = []
  try {
    ledger.value = await get<ApiRecord[]>(`loyalty/customers/${customer.id}/ledger`)
  } catch (caught) {
    ledgerError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load customer loyalty ledger.')
    toast.add({ title: 'Could not load loyalty ledger', description: ledgerError.value, color: 'error' })
  } finally {
    ledgerLoading.value = false
  }
}

function closeLoyalty() {
  selectedCustomer.value = null
  ledger.value = []
  ledgerError.value = ''
}

onMounted(refresh)
useHead({ title: 'Customers - Garmetix CRM' })
</script>

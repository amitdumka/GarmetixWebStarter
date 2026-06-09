<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const router = useRouter()
const customers = ref<any[]>([])
const search = ref('')
const loading = ref(false)
const selectedCustomer = ref<any | null>(null)
const ledger = ref<any[]>([])
const ledgerLoading = ref(false)

const filteredCustomers = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return customers.value
  return customers.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const metrics = computed(() => [
  { label: 'Customers', value: customers.value.length, meta: 'Total customer master', icon: 'i-lucide-users-round', color: 'primary' },
  { label: 'Loyalty Points', value: customers.value.reduce((sum, item) => sum + Number(item.loyaltyPoints || 0), 0).toFixed(2), meta: 'Total outstanding points', icon: 'i-lucide-gift', color: 'success' },
  { label: 'Credit Balance', value: money(customers.value.reduce((sum, item) => sum + Number(item.creditBalance || 0), 0)), meta: 'Advance / credit note balance', icon: 'i-lucide-wallet-cards', color: 'warning' },
  { label: 'GST Alerts', value: customers.value.filter((item) => item.gstMismatchAlert).length, meta: 'Mismatch warnings', icon: 'i-lucide-triangle-alert', color: 'error' }
])

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    customers.value = await api.list<any>('customers')
    if (selectedCustomer.value) {
      selectedCustomer.value = customers.value.find((item) => item.id === selectedCustomer.value.id) || selectedCustomer.value
    }
  } catch (error) {
    feedback.failed('Could not load customers', error)
  } finally {
    loading.value = false
  }
}

async function openLoyalty(customer: any) {
  selectedCustomer.value = customer
  ledgerLoading.value = true
  try {
    ledger.value = await api.get<any[]>(`loyalty/customers/${customer.id}/ledger`)
  } catch (error) {
    feedback.failed('Could not load customer loyalty ledger', error)
  } finally {
    ledgerLoading.value = false
  }
}

function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Customers" @refresh="refresh">
    <UiModulePageHeader
      title="Customer Module"
      description="Dedicated customer master with GSTIN, store credit, loyalty balance, and customer loyalty ledger. Create/edit opens a dedicated form page."
      icon="i-lucide-user-round"
      primary-label="New Customer"
      primary-icon="i-lucide-plus"
      @primary="router.push('/customers/new')"
    >
      <template #actions>
        <UButton color="neutral" variant="subtle" icon="i-lucide-gift" label="Loyalty Setup" @click="router.push('/loyalty')" />
      </template>
    </UiModulePageHeader>

    <div class="planner-metric-grid mt-4">
      <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
        <div class="planner-metric-body">
          <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
          <div><p>{{ metric.label }}</p><strong>{{ metric.value }}</strong><span>{{ metric.meta }}</span></div>
        </div>
      </UCard>
    </div>

    <UCard class="planner-card mt-4">
      <template #header>
        <div class="planner-card-header">
          <div><h2>Customer Register</h2><p>Search name, mobile, GSTIN, loyalty, or credit balance.</p></div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search customer" />
        </div>
      </template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Name</th><th>Mobile</th><th>GSTIN</th><th>Credit</th><th>Loyalty</th><th>GST Status</th><th></th></tr></thead>
          <tbody>
            <tr v-for="customer in filteredCustomers" :key="customer.id">
              <td>{{ customer.name }}</td>
              <td>{{ customer.mobileNumber }}</td>
              <td>{{ customer.gstin || customer.gSTIN || '-' }}</td>
              <td>{{ money(customer.creditBalance) }}</td>
              <td>{{ Number(customer.loyaltyPoints || 0).toFixed(2) }}</td>
              <td><UBadge :color="customer.gstMismatchAlert ? 'warning' : (customer.gstVerified ? 'success' : 'neutral')" variant="subtle">{{ customer.gstMismatchAlert ? 'Mismatch' : (customer.gstVerified ? 'Verified' : 'Pending') }}</UBadge></td>
              <td class="inline-action-row"><UButton size="xs" label="Edit" variant="subtle" @click="router.push(`/customers/${customer.id}`)" /><UButton size="xs" label="Loyalty" icon="i-lucide-gift" @click="openLoyalty(customer)" /></td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <UCard v-if="selectedCustomer" class="planner-card mt-4">
      <template #header>
        <div class="planner-card-header">
          <div><h2>{{ selectedCustomer.name }} loyalty</h2><p>Balance: {{ Number(selectedCustomer.loyaltyPoints || 0).toFixed(2) }} points · Credit: {{ money(selectedCustomer.creditBalance) }}</p></div>
          <UButton label="Manage Loyalty" icon="i-lucide-gift" @click="router.push(`/loyalty?customerId=${selectedCustomer.id}`)" />
        </div>
      </template>
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Date</th><th>Source</th><th>In</th><th>Out</th><th>Balance</th><th>Remarks</th></tr></thead>
          <tbody>
            <tr v-for="row in ledger" :key="row.id"><td>{{ date(row.onDate) }}</td><td>{{ row.sourceType }} {{ row.sourceNumber || '' }}</td><td>{{ row.pointsIn }}</td><td>{{ row.pointsOut }}</td><td>{{ row.balanceAfter }}</td><td>{{ row.remarks }}</td></tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </AppShell>
</template>

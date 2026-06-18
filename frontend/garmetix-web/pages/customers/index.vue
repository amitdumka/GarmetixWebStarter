<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const router = useRouter()
const customers = ref<any[]>([])
const search = ref('')
const loading = ref(false)
const loadError = ref('')
const selectedCustomer = ref<any | null>(null)
const ledger = ref<any[]>([])
const ledgerLoading = ref(false)
const ledgerError = ref('')

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
  loadError.value = ''
  try {
    customers.value = await api.list<any>('customers')
    if (selectedCustomer.value) {
      selectedCustomer.value = customers.value.find((item) => item.id === selectedCustomer.value.id) || selectedCustomer.value
    }
  } catch (error) {
    loadError.value = error instanceof Error ? error.message : 'Please check the service and try again.'
    feedback.failed('Could not load customers', error)
  } finally {
    loading.value = false
  }
}

async function openLoyalty(customer: any) {
  selectedCustomer.value = customer
  ledgerLoading.value = true
  ledgerError.value = ''
  ledger.value = []
  try {
    ledger.value = await api.get<any[]>(`loyalty/customers/${customer.id}/ledger`)
  } catch (error) {
    ledgerError.value = error instanceof Error ? error.message : 'Please check the service and try again.'
    feedback.failed('Could not load customer loyalty ledger', error)
  } finally {
    ledgerLoading.value = false
  }
}

function closeLoyalty() {
  selectedCustomer.value = null
  ledger.value = []
  ledgerError.value = ''
}

function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Customers" @refresh="refresh">
    <UiModulePageHeader
      title="Customers"
      description="Manage customer identity, GST details, store credit, and loyalty balances."
      icon="i-lucide-user-round"
      primary-label="New Customer"
      primary-icon="i-lucide-plus"
      @primary="router.push('/customers/new')"
    >
      <template #actions>
        <UButton icon="i-lucide-plus" label="New Customer" @click="router.push('/customers/new')" />
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

    <UiRegisterPanel
      class="mt-4"
      title="Customer Register"
      :description="`${filteredCustomers.length} of ${customers.length} customers`"
      :loading="loading"
      :error="loadError"
      :empty="filteredCustomers.length === 0"
      :empty-title="search ? 'No matching customers' : 'No customers yet'"
      :empty-description="search ? 'Try a different name, mobile number, GSTIN, credit, or loyalty value.' : 'Create the first customer to begin this register.'"
      empty-icon="i-lucide-users-round"
      @retry="refresh"
    >
      <template #actions>
        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search customers"
          :loading="loading"
          @refresh="refresh"
        />
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Name</th><th>Mobile</th><th>GSTIN</th><th class="text-right">Credit</th><th class="text-right">Loyalty</th><th>GST Status</th><th class="text-right">Actions</th></tr></thead>
          <tbody>
            <tr v-for="customer in filteredCustomers" :key="customer.id">
              <td class="font-medium">{{ customer.name }}</td>
              <td>{{ customer.mobileNumber || '-' }}</td>
              <td>{{ customer.gstin || customer.gSTIN || '-' }}</td>
              <td class="text-right">{{ money(customer.creditBalance) }}</td>
              <td class="text-right">{{ Number(customer.loyaltyPoints || 0).toFixed(2) }}</td>
              <td><UBadge :color="customer.gstMismatchAlert ? 'warning' : (customer.gstVerified ? 'success' : 'neutral')" variant="subtle">{{ customer.gstMismatchAlert ? 'Mismatch' : (customer.gstVerified ? 'Verified' : 'Pending') }}</UBadge></td>
              <td><div class="inline-action-row justify-end"><UButton size="xs" label="Edit" icon="i-lucide-pencil" variant="subtle" @click="router.push(`/customers/${customer.id}`)" /><UButton size="xs" label="Loyalty" icon="i-lucide-gift" @click="openLoyalty(customer)" /></div></td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>

    <UiRegisterPanel
      v-if="selectedCustomer"
      class="mt-4"
      :title="`${selectedCustomer.name} Loyalty Ledger`"
      :description="`Balance: ${Number(selectedCustomer.loyaltyPoints || 0).toFixed(2)} points | Credit: ${money(selectedCustomer.creditBalance)}`"
      :loading="ledgerLoading"
      :error="ledgerError"
      :empty="ledger.length === 0"
      empty-title="No loyalty activity"
      empty-description="Loyalty earning and redemption entries will appear here."
      empty-icon="i-lucide-gift"
      @retry="openLoyalty(selectedCustomer)"
    >
      <template #actions>
        <div class="inline-action-row justify-end">
          <UButton label="Manage Loyalty" icon="i-lucide-gift" @click="router.push(`/loyalty?customerId=${selectedCustomer.id}`)" />
          <UButton color="neutral" variant="ghost" icon="i-lucide-x" label="Close" @click="closeLoyalty" />
        </div>
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Date</th><th>Source</th><th class="text-right">In</th><th class="text-right">Out</th><th class="text-right">Balance</th><th>Remarks</th></tr></thead>
          <tbody>
            <tr v-for="row in ledger" :key="row.id"><td>{{ date(row.onDate) }}</td><td>{{ row.sourceType }} {{ row.sourceNumber || '' }}</td><td class="text-right">{{ row.pointsIn }}</td><td class="text-right">{{ row.pointsOut }}</td><td class="text-right font-medium">{{ row.balanceAfter }}</td><td>{{ row.remarks || '-' }}</td></tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>

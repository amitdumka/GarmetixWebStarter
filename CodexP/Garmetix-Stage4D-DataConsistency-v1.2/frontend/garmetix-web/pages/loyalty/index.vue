<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const route = useRoute()
const router = useRouter()
const saving = ref(false)
const loading = ref(false)
const program = reactive({ enabled: true, name: 'Garmetix Loyalty', earnPointsPerRupee: 0.01, redeemValuePerPoint: 1, minimumBillAmount: 0, expiryDays: null as number | null })
const customers = ref<any[]>([])
const selectedCustomerId = ref('')
const selectedCustomer = computed(() => customers.value.find((item) => item.id === selectedCustomerId.value))
const ledger = ref<any[]>([])
const summary = ref<any | null>(null)
const adjustment = reactive({ pointsIn: 0, pointsOut: 0, remarks: '' })

const customerOptions = computed(() => [{ value: '', label: 'Select customer' }, ...customers.value.map((item) => ({ value: item.id, label: `${item.name} - ${item.mobileNumber || ''} (${Number(item.loyaltyPoints || 0).toFixed(2)} pts)` }))])

async function refresh() {
  if (!auth.isAuthenticated.value || !workspace.storeId.value) return
  loading.value = true
  try {
    const [data, customerRows] = await Promise.all([
      api.get<any>(`loyalty/program?storeId=${workspace.storeId.value}`),
      api.list<any>('customers')
    ])
    if (data) Object.assign(program, data)
    customers.value = customerRows
    const requestedCustomer = String(route.query.customerId || '')
    if (requestedCustomer && !selectedCustomerId.value) selectedCustomerId.value = requestedCustomer
    if (selectedCustomerId.value) await loadCustomerLoyalty(false)
  } catch (error) {
    feedback.failed('Could not load loyalty data', error)
  } finally {
    loading.value = false
  }
}

async function saveProgram() {
  saving.value = true
  try {
    await api.create<any>('loyalty/program', {
      companyId: workspace.companyId.value,
      storeGroupId: workspace.storeGroupId.value,
      storeId: workspace.storeId.value,
      enabled: program.enabled,
      name: program.name,
      earnPointsPerRupee: Number(program.earnPointsPerRupee || 0),
      redeemValuePerPoint: Number(program.redeemValuePerPoint || 0),
      minimumBillAmount: Number(program.minimumBillAmount || 0),
      expiryDays: program.expiryDays ? Number(program.expiryDays) : null
    })
    feedback.saved('Loyalty program saved')
    await refresh()
  } catch (error) {
    feedback.failed('Could not save loyalty program', error)
  } finally {
    saving.value = false
  }
}

async function loadCustomerLoyalty(updateRoute = true) {
  if (!selectedCustomerId.value) {
    ledger.value = []
    summary.value = null
    return
  }
  try {
    const [summaryRow, ledgerRows] = await Promise.all([
      api.get<any>(`loyalty/customers/${selectedCustomerId.value}`),
      api.get<any[]>(`loyalty/customers/${selectedCustomerId.value}/ledger`)
    ])
    summary.value = summaryRow
    ledger.value = ledgerRows
    if (updateRoute) router.replace({ query: { ...route.query, customerId: selectedCustomerId.value } })
  } catch (error) {
    feedback.failed('Could not load customer loyalty', error)
  }
}

async function adjustPoints() {
  if (!selectedCustomerId.value) {
    feedback.notify('Select customer first', undefined, 'warning')
    return
  }
  saving.value = true
  try {
    await api.create<any>(`loyalty/customers/${selectedCustomerId.value}/adjust`, {
      companyId: workspace.companyId.value,
      storeGroupId: workspace.storeGroupId.value,
      storeId: workspace.storeId.value,
      pointsIn: Number(adjustment.pointsIn || 0),
      pointsOut: Number(adjustment.pointsOut || 0),
      remarks: adjustment.remarks
    })
    feedback.saved('Loyalty points adjusted')
    Object.assign(adjustment, { pointsIn: 0, pointsOut: 0, remarks: '' })
    await refresh()
  } catch (error) {
    feedback.failed('Could not adjust loyalty points', error)
  } finally {
    saving.value = false
  }
}

function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Loyalty Program" @refresh="refresh" @workspace-change="refresh">
    <UiModulePageHeader
      title="Loyalty Program"
      description="Configure store loyalty rules and manage customer loyalty balances from the customer module."
      icon="i-lucide-gift"
      primary-label="Customer Module"
      primary-icon="i-lucide-user-round"
      @primary="router.push('/customers')"
    />

    <div class="page-grid two-column-layout mt-4">
      <UCard class="planner-card">
        <template #header><strong>Store Loyalty Rule</strong></template>
        <UAlert color="primary" variant="subtle" title="Automatic earning" description="When enabled, paid sales invoices earn points for the customer. Sales returns reverse proportional earned points." />
        <div class="form-two-column mt-4">
          <UFormField label="Enabled"><USwitch v-model="program.enabled" /></UFormField>
          <UFormField label="Program name"><UInput v-model="program.name" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Points per rupee"><UInput v-model="program.earnPointsPerRupee" type="number" step="0.01" /></UFormField>
          <UFormField label="Redeem value per point"><UInput v-model="program.redeemValuePerPoint" type="number" step="0.01" /></UFormField>
          <UFormField label="Minimum bill"><UInput v-model="program.minimumBillAmount" type="number" step="0.01" /></UFormField>
        </div>
        <UFormField label="Expiry days"><UInput v-model="program.expiryDays" type="number" placeholder="Optional" /></UFormField>
        <UButton label="Save Loyalty Program" icon="i-lucide-save" :loading="saving || loading" @click="saveProgram" />
      </UCard>

      <UCard class="planner-card">
        <template #header><strong>Customer Loyalty Handling</strong></template>
        <UFormField label="Customer"><USelect v-model="selectedCustomerId" :items="customerOptions" @change="loadCustomerLoyalty" /></UFormField>
        <div v-if="summary" class="planner-metric-grid compact-grid mt-4">
          <UCard><p>Points</p><strong>{{ Number(summary.loyaltyPoints || 0).toFixed(2) }}</strong></UCard>
          <UCard><p>Credit</p><strong>{{ money(summary.creditBalance) }}</strong></UCard>
          <UCard><p>Bill Count</p><strong>{{ summary.billCount }}</strong></UCard>
        </div>
        <div class="form-three-column mt-4">
          <UFormField label="Add points"><UInput v-model="adjustment.pointsIn" type="number" step="0.01" /></UFormField>
          <UFormField label="Redeem/reduce points"><UInput v-model="adjustment.pointsOut" type="number" step="0.01" /></UFormField>
          <UFormField label="Remarks"><UInput v-model="adjustment.remarks" /></UFormField>
        </div>
        <div class="form-actions">
          <UButton color="neutral" variant="subtle" label="Load Ledger" icon="i-lucide-list" @click="loadCustomerLoyalty" />
          <UButton label="Adjust Points" icon="i-lucide-gift" :loading="saving" @click="adjustPoints" />
        </div>
      </UCard>
    </div>

    <UCard v-if="selectedCustomerId" class="planner-card mt-4">
      <template #header><strong>{{ selectedCustomer?.name || 'Customer' }} Loyalty Ledger</strong></template>
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

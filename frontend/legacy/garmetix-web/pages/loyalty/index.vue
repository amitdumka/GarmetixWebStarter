<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const route = useRoute()
const router = useRouter()
const saving = ref(false)
const loading = ref(false)
const loadError = ref('')
const ledgerLoading = ref(false)
const ledgerError = ref('')
const ledgerSearch = ref('')
const program = reactive({ enabled: true, name: 'Garmetix Loyalty', earnPointsPerRupee: 0.01, redeemValuePerPoint: 1, minimumBillAmount: 0, expiryDays: null as number | null })
const customers = ref<any[]>([])
const selectedCustomerId = ref('')
const selectedCustomer = computed(() => customers.value.find((item) => item.id === selectedCustomerId.value))
const ledger = ref<any[]>([])
const summary = ref<any | null>(null)
const adjustment = reactive({ pointsIn: 0, pointsOut: 0, remarks: '' })

const customerOptions = computed(() => customers.value.map((item) => ({
  value: item.id,
  label: `${item.name} - ${item.mobileNumber || ''} (${Number(item.loyaltyPoints || 0).toFixed(2)} pts)`
})))
const filteredLedger = computed(() => {
  const term = ledgerSearch.value.trim().toLowerCase()
  if (!term) return ledger.value
  return ledger.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

async function refresh() {
  if (!auth.isAuthenticated.value) return
  if (!workspace.storeId.value) {
    loadError.value = 'Select a store to load its loyalty program.'
    return
  }
  loading.value = true
  loadError.value = ''
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
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
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
    ledgerError.value = ''
    return
  }
  ledgerLoading.value = true
  ledgerError.value = ''
  try {
    const [summaryRow, ledgerRows] = await Promise.all([
      api.get<any>(`loyalty/customers/${selectedCustomerId.value}`),
      api.get<any[]>(`loyalty/customers/${selectedCustomerId.value}/ledger`)
    ])
    summary.value = summaryRow
    ledger.value = ledgerRows
    if (updateRoute) router.replace({ query: { ...route.query, customerId: selectedCustomerId.value } })
  } catch (error) {
    ledgerError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Could not load customer loyalty', error)
  } finally {
    ledgerLoading.value = false
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

    <UAlert
      v-if="loadError"
      class="mt-4"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      title="Could not load loyalty setup"
      :description="loadError"
      :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
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
        <UFormField label="Customer"><USelect v-model="selectedCustomerId" :items="customerOptions" placeholder="Select customer" @change="loadCustomerLoyalty" /></UFormField>
        <div v-if="summary" class="loyalty-summary-grid mt-4">
          <div><span>Points</span><strong>{{ Number(summary.loyaltyPoints || 0).toFixed(2) }}</strong></div>
          <div><span>Credit</span><strong>{{ money(summary.creditBalance) }}</strong></div>
          <div><span>Bill Count</span><strong>{{ summary.billCount }}</strong></div>
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

    <UiRegisterPanel
      class="mt-4"
      :title="selectedCustomerId ? `${selectedCustomer?.name || 'Customer'} Loyalty Ledger` : 'Customer Loyalty Ledger'"
      :description="selectedCustomerId ? `${filteredLedger.length} of ${ledger.length} entries` : 'Select a customer to review earning, redemption, and adjustment history.'"
      :loading="ledgerLoading"
      :error="ledgerError"
      :empty="!selectedCustomerId || filteredLedger.length === 0"
      :empty-title="!selectedCustomerId ? 'Select a customer' : (ledgerSearch ? 'No matching loyalty entries' : 'No loyalty activity')"
      :empty-description="!selectedCustomerId ? 'Use Customer Loyalty Handling above to choose a customer.' : (ledgerSearch ? 'Try a different source, date, amount, or remark.' : 'Earning, redemption, and adjustment entries will appear here.')"
      empty-icon="i-lucide-gift"
      @retry="loadCustomerLoyalty(false)"
    >
      <template v-if="selectedCustomerId" #actions>
        <UiCrudToolbar
          v-model:search="ledgerSearch"
          search-placeholder="Search loyalty ledger"
          :loading="ledgerLoading"
          @refresh="loadCustomerLoyalty(false)"
        />
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Date</th><th>Source</th><th class="text-right">In</th><th class="text-right">Out</th><th class="text-right">Balance</th><th>Remarks</th></tr></thead>
          <tbody>
            <tr v-for="row in filteredLedger" :key="row.id"><td>{{ date(row.onDate) }}</td><td>{{ row.sourceType }} {{ row.sourceNumber || '' }}</td><td class="text-right">{{ row.pointsIn }}</td><td class="text-right">{{ row.pointsOut }}</td><td class="text-right font-medium">{{ row.balanceAfter }}</td><td>{{ row.remarks || '-' }}</td></tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>

<style scoped>
.loyalty-summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  border: 1px solid var(--ui-border);
}

.loyalty-summary-grid > div {
  display: grid;
  gap: 0.25rem;
  padding: 0.75rem;
  border-right: 1px solid var(--ui-border);
}

.loyalty-summary-grid > div:last-child {
  border-right: 0;
}

.loyalty-summary-grid span {
  color: var(--ui-text-muted);
  font-size: 0.75rem;
}

@media (max-width: 640px) {
  .loyalty-summary-grid {
    grid-template-columns: 1fr;
  }

  .loyalty-summary-grid > div {
    border-right: 0;
    border-bottom: 1px solid var(--ui-border);
  }

  .loyalty-summary-grid > div:last-child {
    border-bottom: 0;
  }
}
</style>

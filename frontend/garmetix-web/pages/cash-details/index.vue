<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const rows = ref<any[]>([])
const history = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const filterFrom = ref('')
const filterTo = ref('')
const filterSource = ref('')
const editId = ref('')
const form = reactive(blankForm())

const activeStoreId = computed(() => workspace.storeId.value || stores.value[0]?.id || '')
const selectedStoreName = computed(() => stores.value.find((store) => store.id === activeStoreId.value)?.name || 'No store selected')
const computedAmount = computed(() => calculateCash(form))
const totalAmount = computed(() => rows.value.reduce((sum, row) => sum + Number(row.amount || 0), 0))

function blankForm() {
  return {
    onDate: localDateValue(),
    amount: null as number | null,
    n2000: 0,
    n500: 0,
    n200: 0,
    n100: 0,
    n50: 0,
    nC20: 0,
    nC10: 0,
    nC5: 0,
    nC2: 0,
    nC1: 0,
    source: 'ManualCashFlow'
  }
}

function localDateValue(date = new Date()) {
  const offset = date.getTimezoneOffset()
  const local = new Date(date.getTime() - offset * 60000)
  return local.toISOString().slice(0, 10)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function calculateCash(source: any) {
  return Number(source.n2000 || 0) * 2000
    + Number(source.n500 || 0) * 500
    + Number(source.n200 || 0) * 200
    + Number(source.n100 || 0) * 100
    + Number(source.n50 || 0) * 50
    + Number(source.nC20 || 0) * 20
    + Number(source.nC10 || 0) * 10
    + Number(source.nC5 || 0) * 5
    + Number(source.nC2 || 0) * 2
    + Number(source.nC1 || 0)
}

function payload() {
  const amount = computedAmount.value > 0 ? computedAmount.value : form.amount
  return {
    storeId: activeStoreId.value,
    onDate: form.onDate,
    amount,
    n2000: Number(form.n2000 || 0),
    n500: Number(form.n500 || 0),
    n200: Number(form.n200 || 0),
    n100: Number(form.n100 || 0),
    n50: Number(form.n50 || 0),
    nC20: Number(form.nC20 || 0),
    nC10: Number(form.nC10 || 0),
    nC5: Number(form.nC5 || 0),
    nC2: Number(form.nC2 || 0),
    nC1: Number(form.nC1 || 0),
    source: form.source || 'ManualCashFlow'
  }
}

async function loadShellData() {
  const [companyRows, storeRows] = await Promise.all([
    api.list<any>('companies'),
    api.list<any>('stores')
  ])
  companies.value = companyRows
  stores.value = storeRows
}

async function refresh() {
  if (!auth.isAuthenticated.value || !activeStoreId.value) return
  loading.value = true
  try {
    const params = new URLSearchParams()
    params.set('storeId', activeStoreId.value)
    if (filterFrom.value) params.set('from', filterFrom.value)
    if (filterTo.value) params.set('to', filterTo.value)
    if (filterSource.value) params.set('source', filterSource.value)
    rows.value = await api.get<any[]>(`cash-details?${params.toString()}`)

    const historyParams = new URLSearchParams()
    historyParams.set('storeId', activeStoreId.value)
    if (filterFrom.value) historyParams.set('from', filterFrom.value)
    if (filterTo.value) historyParams.set('to', filterTo.value)
    history.value = await api.get<any>(`cash-details/history?${historyParams.toString()}`)
  } catch (error) {
    feedback.failed('Cash details load failed', error)
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!activeStoreId.value) {
    feedback.notify('Select store', 'Choose working store from top bar first.', 'warning')
    return
  }

  saving.value = true
  try {
    if (editId.value) {
      await api.update<any>('cash-details', editId.value, payload())
      feedback.notify('Cash detail updated')
    } else {
      await api.create<any>('cash-details', payload())
      feedback.notify('Cash detail added')
    }
    resetForm()
    await refresh()
  } catch (error) {
    feedback.failed('Cash detail save failed', error)
  } finally {
    saving.value = false
  }
}

function edit(row: any) {
  editId.value = row.id
  form.onDate = String(row.onDate || '').slice(0, 10)
  form.amount = row.amount
  form.n2000 = row.n2000 || 0
  form.n500 = row.n500 || 0
  form.n200 = row.n200 || 0
  form.n100 = row.n100 || 0
  form.n50 = row.n50 || 0
  form.nC20 = row.nC20 || 0
  form.nC10 = row.nC10 || 0
  form.nC5 = row.nC5 || 0
  form.nC2 = row.nC2 || 0
  form.nC1 = row.nC1 || 0
  form.source = row.source || 'ManualCashFlow'
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function remove(row: any) {
  if (row.linkedToDayOpening || row.linkedToDayClosing) {
    feedback.notify('Cannot delete linked cash detail', 'This row is linked to Day Opening/Closing. Edit it or reopen/delete close first.', 'warning')
    return
  }

  saving.value = true
  try {
    await api.remove('cash-details', row.id)
    feedback.notify('Cash detail deleted', undefined, 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Cash detail delete failed', error)
  } finally {
    saving.value = false
  }
}

function resetForm() {
  editId.value = ''
  Object.assign(form, blankForm())
}

watch(() => [activeStoreId.value, filterFrom.value, filterTo.value, filterSource.value], () => refresh())

onMounted(async () => {
  auth.restore()
  await loadShellData()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Cash Details"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="cash-details-page">
      <UiModulePageHeader
        title="Cash Details"
        description="Manage cash denomination notes and coin history per store/day. Manual records can be added, edited and deleted. Day opening/closing linked rows can be edited safely."
        icon="i-lucide-coins"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge color="primary" variant="subtle" :label="selectedStoreName" />
        </template>
      </UiModulePageHeader>

      <div class="metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-coins" color="primary" variant="subtle" /><div><p>History rows</p><strong>{{ history?.recordCount || rows.length }}</strong><span>Current filter</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-indian-rupee" color="success" variant="subtle" /><div><p>Total cash</p><strong>{{ money(history?.totalAmount || totalAmount) }}</strong><span>Filtered total</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-history" color="warning" variant="subtle" /><div><p>Average</p><strong>{{ money(history?.averageAmount || 0) }}</strong><span>Per row</span></div></div></UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <h2>{{ editId ? 'Edit Cash Detail' : 'Add Manual Cash Detail' }}</h2>
        </template>
        <div class="form-grid">
          <UFormField label="Date">
            <UInput v-model="form.onDate" type="date" />
          </UFormField>
          <UFormField label="Source">
            <USelectMenu v-model="form.source" :items="['ManualCashFlow', 'DayOpening', 'DayClosing', 'StoreHoliday', 'CashVerification']" />
          </UFormField>
          <UFormField label="Amount override">
            <UInput v-model.number="form.amount" type="number" step="0.01" placeholder="Used when notes total is zero" />
          </UFormField>
          <UFormField label="Calculated notes/coin total">
            <UInput :model-value="money(computedAmount)" readonly />
          </UFormField>
        </div>

        <div class="notes-grid">
          <UInput v-model.number="form.n2000" type="number" placeholder="₹2000" />
          <UInput v-model.number="form.n500" type="number" placeholder="₹500" />
          <UInput v-model.number="form.n200" type="number" placeholder="₹200" />
          <UInput v-model.number="form.n100" type="number" placeholder="₹100" />
          <UInput v-model.number="form.n50" type="number" placeholder="₹50" />
          <UInput v-model.number="form.nC20" type="number" placeholder="₹20" />
          <UInput v-model.number="form.nC10" type="number" placeholder="₹10" />
          <UInput v-model.number="form.nC5" type="number" placeholder="₹5" />
          <UInput v-model.number="form.nC2" type="number" placeholder="₹2" />
          <UInput v-model.number="form.nC1" type="number" placeholder="₹1" />
        </div>

        <template #footer>
          <div class="footer-actions">
            <UButton icon="i-lucide-save" color="primary" :loading="saving" :label="editId ? 'Update cash detail' : 'Add cash detail'" @click="save" />
            <UButton icon="i-lucide-x" color="neutral" variant="subtle" label="Clear" @click="resetForm" />
          </div>
        </template>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <h2>Cash Notes / Coin History</h2>
            <UBadge color="neutral" variant="subtle" :label="`${rows.length} rows`" />
          </div>
        </template>

        <div class="filter-grid">
          <UFormField label="From">
            <UInput v-model="filterFrom" type="date" />
          </UFormField>
          <UFormField label="To">
            <UInput v-model="filterTo" type="date" />
          </UFormField>
          <UFormField label="Source">
            <USelectMenu v-model="filterSource" :items="['', 'ManualCashFlow', 'DayOpening', 'DayClosing', 'StoreHoliday', 'CashVerification']" />
          </UFormField>
        </div>

        <div class="cash-table">
          <div class="cash-row header">
            <span>Date</span><span>Source</span><span>Amount</span><span>Notes / coins</span><span>Linked</span><span>Action</span>
          </div>
          <div v-for="row in rows" :key="row.id" class="cash-row">
            <span>{{ String(row.onDate || '').slice(0, 10) }}</span>
            <span>{{ row.source }}</span>
            <strong>{{ money(row.amount) }}</strong>
            <span class="notes-text">2000×{{ row.n2000 }} 500×{{ row.n500 }} 200×{{ row.n200 }} 100×{{ row.n100 }} 50×{{ row.n50 }} 20×{{ row.nC20 }} 10×{{ row.nC10 }} 5×{{ row.nC5 }} 2×{{ row.nC2 }} 1×{{ row.nC1 }}</span>
            <span>
              <UBadge v-if="row.linkedToDayOpening" color="success" variant="subtle" label="Opening" />
              <UBadge v-if="row.linkedToDayClosing" color="warning" variant="subtle" label="Closing" />
              <UBadge v-if="!row.linkedToDayOpening && !row.linkedToDayClosing" color="neutral" variant="subtle" label="Manual" />
            </span>
            <span class="actions">
              <UButton size="xs" icon="i-lucide-pencil" color="primary" variant="soft" @click="edit(row)" />
              <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="soft" :disabled="row.linkedToDayOpening || row.linkedToDayClosing" @click="remove(row)" />
            </span>
          </div>
          <UAlert v-if="!rows.length && !loading" color="neutral" variant="subtle" icon="i-lucide-info" title="No cash details found" description="Add a manual record or run Day Opening/Closing." />
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.cash-details-page { display: grid; gap: 1rem; }
.metric-grid, .form-grid, .filter-grid, .notes-grid { display: grid; gap: 1rem; }
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.form-grid, .filter-grid { grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); }
.notes-grid { grid-template-columns: repeat(auto-fit, minmax(92px, 1fr)); }
.footer-actions { display: flex; gap: .75rem; flex-wrap: wrap; }
.cash-table { display: grid; gap: .5rem; margin-top: 1rem; overflow-x: auto; }
.cash-row { display: grid; grid-template-columns: 110px 150px 130px minmax(360px, 1fr) 150px 90px; gap: .75rem; align-items: center; min-width: 980px; padding: .75rem; border: 1px solid rgb(var(--color-gray-200)); border-radius: .75rem; }
.cash-row.header { font-weight: 700; color: rgb(var(--color-gray-500)); background: rgb(var(--color-gray-50)); }
.notes-text { font-size: .8rem; color: rgb(var(--color-gray-500)); }
.actions { display: flex; gap: .35rem; }
.dark .cash-row { border-color: rgb(var(--color-gray-800)); }
.dark .cash-row.header { background: rgb(var(--color-gray-900)); }
</style>

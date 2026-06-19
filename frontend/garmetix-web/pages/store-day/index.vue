<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const status = ref<any | null>(null)
const closingResult = ref<any | null>(null)
const selectedDate = ref(localDateValue())
const openingBalance = ref<number | null>(null)
const holidayReason = ref('Store closed / holiday')
const openingCash = reactive(cashBlank())
const closingCash = reactive(cashBlank())

const activeStoreId = computed(() => workspace.storeId.value || stores.value[0]?.id || '')
const cashAmount = computed(() => calculateCash(closingCash))
const openingCashAmount = computed(() => calculateCash(openingCash))
const canUseStoreDay = computed(() => Boolean(activeStoreId.value))

function cashBlank() {
  return {
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
    nC1: 0
  }
}

function toCashPayload(source: any, fallbackAmount?: number | null) {
  const calculated = calculateCash(source)
  return {
    amount: source.amount ?? fallbackAmount ?? calculated,
    n2000: Number(source.n2000 || 0),
    n500: Number(source.n500 || 0),
    n200: Number(source.n200 || 0),
    n100: Number(source.n100 || 0),
    n50: Number(source.n50 || 0),
    nC20: Number(source.nC20 || 0),
    nC10: Number(source.nC10 || 0),
    nC5: Number(source.nC5 || 0),
    nC2: Number(source.nC2 || 0),
    nC1: Number(source.nC1 || 0)
  }
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

function localDateValue(date = new Date()) {
  const offset = date.getTimezoneOffset()
  const local = new Date(date.getTime() - offset * 60000)
  return local.toISOString().slice(0, 10)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
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
  closingResult.value = null
  try {
    status.value = await api.get<any>(`store-day/status?storeId=${activeStoreId.value}&onDate=${selectedDate.value}`)
    openingBalance.value = status.value?.openingBalance ?? status.value?.bookSummary?.openingBalance ?? 0
    feedback.notify('Store day refreshed', status.value?.message || 'Status loaded.', status.value?.entryAllowed ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Store day status failed', error)
  } finally {
    loading.value = false
  }
}

async function openDay() {
  if (!activeStoreId.value) return
  loading.value = true
  try {
    status.value = await api.create<any>('store-day/open', {
      storeId: activeStoreId.value,
      onDate: selectedDate.value,
      openingBalance: openingBalance.value,
      cashDetail: toCashPayload(openingCash, openingBalance.value),
      remarks: 'Day opened from Store Day page'
    })
    feedback.notify('Store day opened')
    await refresh()
  } catch (error) {
    feedback.failed('Store day opening failed', error)
  } finally {
    loading.value = false
  }
}

async function closeDay() {
  if (!activeStoreId.value) return
  loading.value = true
  try {
    closingResult.value = await api.create<any>('store-day/close', {
      storeId: activeStoreId.value,
      onDate: selectedDate.value,
      cashDetail: toCashPayload(closingCash, cashAmount.value || status.value?.bookSummary?.cashInHand),
      useBookCashIfNoCashDetail: true,
      remarks: 'Day closed from Store Day page'
    })
    status.value = closingResult.value?.status || status.value
    feedback.notify('Store day closed and petty cash sheet updated')
  } catch (error) {
    feedback.failed('Store day closing failed', error)
  } finally {
    loading.value = false
  }
}

async function markHoliday() {
  if (!activeStoreId.value) return
  loading.value = true
  try {
    status.value = await api.create<any>('store-day/holiday', {
      storeId: activeStoreId.value,
      onDate: selectedDate.value,
      reason: holidayReason.value
    })
    feedback.notify('Store holiday/closed day marked')
  } catch (error) {
    feedback.failed('Holiday marking failed', error)
  } finally {
    loading.value = false
  }
}

async function reopenDay() {
  if (!activeStoreId.value) return
  loading.value = true
  try {
    status.value = await api.create<any>('store-day/reopen', {
      storeId: activeStoreId.value,
      onDate: selectedDate.value,
      reason: 'Wrong close operation - reopened from Store Day page'
    })
    closingResult.value = null
    feedback.notify('Store day reopened')
  } catch (error) {
    feedback.failed('Store day reopen failed', error)
  } finally {
    loading.value = false
  }
}

async function deleteDayClose() {
  if (!activeStoreId.value) return
  loading.value = true
  try {
    status.value = await api.create<any>('store-day/delete-close', {
      storeId: activeStoreId.value,
      onDate: selectedDate.value,
      reason: 'Wrong close operation - deleted from Store Day page'
    })
    closingResult.value = null
    feedback.notify('Store day close deleted')
  } catch (error) {
    feedback.failed('Delete day close failed', error)
  } finally {
    loading.value = false
  }
}

async function printPettyCash() {
  const id = closingResult.value?.pettyCashSheetId || status.value?.pettyCashSheetId
  if (!id) {
    feedback.notify('No petty cash sheet', 'Close the day or mark holiday first.', 'warning')
    return
  }

  try {
    await documentPrint.printPdf(`petty-cash-sheets/${id}/pdf`)
  } catch (error) {
    feedback.failed('Petty cash print failed', error)
  }
}

watch(() => [activeStoreId.value, selectedDate.value], () => refresh())

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
    title="Store Day Open / Close"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="store-day-page">
      <UiModulePageHeader
        title="Store Day Open / Close"
        description="Open each store day before billing/entry, close with cash denomination details, generate petty cash sheet, or mark store holiday."
        icon="i-lucide-sun-medium"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="status?.entryAllowed ? 'success' : status?.isClosed ? 'error' : 'warning'" :label="status?.message || 'Select store/date'" variant="subtle" />
        </template>
      </UiModulePageHeader>

      <UCard class="planner-card">
        <div class="filter-grid">
          <UFormField label="Working store">
            <UInput :model-value="stores.find((store) => store.id === activeStoreId)?.name || 'No store selected'" readonly />
          </UFormField>
          <UFormField label="Date">
            <UInput v-model="selectedDate" type="date" />
          </UFormField>
        </div>
      </UCard>

      <div class="metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-door-open" :color="status?.isOpened ? 'success' : 'warning'" variant="subtle" /><div><p>Opening</p><strong>{{ status?.isOpened ? 'Done' : 'Pending' }}</strong><span>{{ money(status?.openingBalance || 0) }}</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-door-closed" :color="status?.isClosed ? 'success' : 'warning'" variant="subtle" /><div><p>Closing</p><strong>{{ status?.isClosed ? 'Done' : 'Pending' }}</strong><span>{{ money(status?.physicalClosingBalance || 0) }}</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-book-open-check" color="primary" variant="subtle" /><div><p>Book Cash</p><strong>{{ money(status?.bookSummary?.cashInHand || 0) }}</strong><span>Calculated from books</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-scale" :color="Math.abs(status?.difference || 0) > 0.01 ? 'warning' : 'success'" variant="subtle" /><div><p>Difference</p><strong>{{ money(status?.difference || 0) }}</strong><span>Physical - book</span></div></div></UCard>
      </div>

      <div class="grid-two">
        <UCard class="planner-card">
          <template #header><h2>Day Opening</h2></template>
          <div class="form-grid">
            <UFormField label="Opening balance">
              <UInput v-model.number="openingBalance" type="number" step="0.01" />
            </UFormField>
            <UFormField label="Opening cash amount from notes">
              <UInput :model-value="money(openingCashAmount)" readonly />
            </UFormField>
          </div>
          <div class="notes-grid">
            <UInput v-model.number="openingCash.n2000" type="number" placeholder="₹2000" />
            <UInput v-model.number="openingCash.n500" type="number" placeholder="₹500" />
            <UInput v-model.number="openingCash.n200" type="number" placeholder="₹200" />
            <UInput v-model.number="openingCash.n100" type="number" placeholder="₹100" />
            <UInput v-model.number="openingCash.n50" type="number" placeholder="₹50" />
            <UInput v-model.number="openingCash.nC20" type="number" placeholder="₹20" />
            <UInput v-model.number="openingCash.nC10" type="number" placeholder="₹10" />
            <UInput v-model.number="openingCash.nC5" type="number" placeholder="₹5" />
            <UInput v-model.number="openingCash.nC2" type="number" placeholder="₹2" />
            <UInput v-model.number="openingCash.nC1" type="number" placeholder="₹1" />
          </div>
          <template #footer>
            <UButton icon="i-lucide-door-open" color="success" :loading="loading" :disabled="!canUseStoreDay || status?.isClosed" label="Open / Update Day" @click="openDay" />
          </template>
        </UCard>

        <UCard class="planner-card">
          <template #header><h2>Day Closing</h2></template>
          <div class="form-grid">
            <UFormField label="Book closing cash">
              <UInput :model-value="money(status?.bookSummary?.cashInHand || 0)" readonly />
            </UFormField>
            <UFormField label="Physical cash from notes">
              <UInput :model-value="money(cashAmount)" readonly />
            </UFormField>
          </div>
          <div class="notes-grid">
            <UInput v-model.number="closingCash.n2000" type="number" placeholder="₹2000" />
            <UInput v-model.number="closingCash.n500" type="number" placeholder="₹500" />
            <UInput v-model.number="closingCash.n200" type="number" placeholder="₹200" />
            <UInput v-model.number="closingCash.n100" type="number" placeholder="₹100" />
            <UInput v-model.number="closingCash.n50" type="number" placeholder="₹50" />
            <UInput v-model.number="closingCash.nC20" type="number" placeholder="₹20" />
            <UInput v-model.number="closingCash.nC10" type="number" placeholder="₹10" />
            <UInput v-model.number="closingCash.nC5" type="number" placeholder="₹5" />
            <UInput v-model.number="closingCash.nC2" type="number" placeholder="₹2" />
            <UInput v-model.number="closingCash.nC1" type="number" placeholder="₹1" />
          </div>
          <template #footer>
            <div class="footer-actions">
              <UButton icon="i-lucide-door-closed" color="primary" :loading="loading" :disabled="!status?.isOpened || status?.isClosed" label="Close Day + Save Petty Cash" @click="closeDay" />
              <UButton icon="i-lucide-rotate-ccw" color="warning" variant="soft" :loading="loading" :disabled="!status?.isClosed" label="Reopen Day" @click="reopenDay" />
              <UButton icon="i-lucide-trash-2" color="error" variant="soft" :loading="loading" :disabled="!status?.isClosed" label="Delete Close" @click="deleteDayClose" />
              <UButton icon="i-lucide-printer" color="neutral" variant="subtle" label="Print Petty Cash" @click="printPettyCash" />
            </div>
          </template>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header><h2>Store Closed / Holiday</h2></template>
        <p class="text-sm text-slate-500">For a non-operational day, this carries forward previous closing balance, creates opening and closing, and blocks entries for that date.</p>
        <div class="filter-grid">
          <UFormField label="Reason">
            <UInput v-model="holidayReason" />
          </UFormField>
          <div class="flex items-end">
            <UButton icon="i-lucide-calendar-x-2" color="warning" variant="soft" :loading="loading" label="Mark Holiday / Closed" @click="markHoliday" />
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Book Summary</h2></template>
        <div class="summary-grid">
          <div><span>Sales</span><strong>{{ money(status?.bookSummary?.sales || 0) }}</strong></div>
          <div><span>Receipts</span><strong>{{ money(status?.bookSummary?.receipts || 0) }}</strong></div>
          <div><span>Due Receipts</span><strong>{{ money(status?.bookSummary?.dueReceipts || 0) }}</strong></div>
          <div><span>Expenses</span><strong>{{ money(status?.bookSummary?.expenses || 0) }}</strong></div>
          <div><span>Payments</span><strong>{{ money(status?.bookSummary?.payments || 0) }}</strong></div>
          <div><span>Bank Deposit</span><strong>{{ money(status?.bookSummary?.bankDeposit || 0) }}</strong></div>
          <div><span>Non-cash sale</span><strong>{{ money(status?.bookSummary?.nonCashSale || 0) }}</strong></div>
          <div><span>Cash in hand</span><strong>{{ money(status?.bookSummary?.cashInHand || 0) }}</strong></div>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.store-day-page { display: grid; gap: 1rem; }
.filter-grid, .form-grid, .metric-grid, .grid-two, .notes-grid, .summary-grid {
  display: grid;
  gap: 1rem;
}
.filter-grid, .form-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.grid-two { grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); }
.notes-grid { grid-template-columns: repeat(auto-fit, minmax(92px, 1fr)); }
.summary-grid { grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }
.summary-grid div { padding: .75rem; border: 1px solid rgb(var(--color-gray-200)); border-radius: .75rem; }
.summary-grid span { display: block; color: rgb(var(--color-gray-500)); font-size: .8rem; }
.summary-grid strong { display: block; margin-top: .25rem; }
.footer-actions { display: flex; gap: .75rem; flex-wrap: wrap; }
.dark .summary-grid div { border-color: rgb(var(--color-gray-800)); }
</style>

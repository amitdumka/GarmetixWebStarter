<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const stores = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const testing = ref(false)
const queue = ref<any[]>([])
const queueStats = ref<any[]>([])
const selectedStoreId = ref('')
const statusFilter = ref('Pending')
const statusOptions = [
  { label: 'Pending', value: 'Pending' },
  { label: 'Printing', value: 'Printing' },
  { label: 'Failed', value: 'Failed' },
  { label: 'Printed', value: 'Printed' },
  { label: 'Skipped', value: 'Skipped' },
  { label: 'All', value: 'All' }
]
const testMessage = ref('Garmetix Epson LX-310 dot-matrix test print')
const setting = reactive({
  storeId: '',
  enabled: false,
  printerName: 'EPSON_LX310',
  outputMode: 'BridgeService',
  spoolDirectory: '/app/data/dotmatrix-spool',
  timeZoneId: 'Asia/Kolkata',
  lineWidth: 136,
  printTransactions: true,
  printDayOpeningClosing: true,
  printEditsAndDeletes: true,
  printAttendanceInDaySummary: true,
  printBankUpiSummary: true,
  pollSeconds: 5,
  retryLimit: 10
})

const outputModeOptions = [
  { label: 'Ubuntu host bridge service (recommended)', value: 'BridgeService' },
  { label: 'Disabled', value: 'Disabled' },
  { label: 'Legacy spool file from API container', value: 'SpoolFile' },
  { label: 'Direct lp command from API container (not recommended)', value: 'LpCommand' }
]
const widthOptions = [
  { label: '80 column', value: 80 },
  { label: '136 column / full width', value: 136 }
]

const activeStoreId = computed(() => selectedStoreId.value || workspace.storeId.value || stores.value[0]?.id || '')

onMounted(async () => {
  await loadStores()
  selectedStoreId.value = activeStoreId.value
  await refreshAll()
})

watch(activeStoreId, async (value) => {
  if (!value) return
  selectedStoreId.value = value
  await refreshAll()
})

async function loadStores() {
  stores.value = await api.list<any>('stores')
}

async function refreshAll() {
  if (!activeStoreId.value) return
  loading.value = true
  try {
    await Promise.all([loadSetting(), loadQueue(), loadStats()])
  } finally {
    loading.value = false
  }
}

async function loadSetting() {
  const row = await api.get<any>(`dot-matrix-print/settings?storeId=${activeStoreId.value}`)
  Object.assign(setting, row)
}

async function saveSetting() {
  saving.value = true
  try {
    await api.create<any>('dot-matrix-print/settings', { ...setting, storeId: activeStoreId.value })
    feedback.success('Dot-matrix print settings saved')
    await loadSetting()
  } finally {
    saving.value = false
  }
}

async function sendTestPrint() {
  testing.value = true
  try {
    await api.create<any>('dot-matrix-print/test', { storeId: activeStoreId.value, message: testMessage.value })
    feedback.success('Test print queued')
    await Promise.all([loadQueue(), loadStats()])
  } finally {
    testing.value = false
  }
}

async function loadQueue() {
  if (!activeStoreId.value) return
  const params = new URLSearchParams({ storeId: activeStoreId.value })
  if (statusFilter.value && statusFilter.value !== 'All') params.set('status', statusFilter.value)
  queue.value = await api.get<any[]>(`dot-matrix-print/queue?${params.toString()}`)
}

async function loadStats() {
  if (!activeStoreId.value) return
  const params = new URLSearchParams({ storeId: activeStoreId.value })
  queueStats.value = await api.get<any[]>(`dot-matrix-print/queue/stats?${params.toString()}`)
}

async function retry(row: any) {
  await $fetch(`${useRuntimeConfig().public.apiBase}/dot-matrix-print/queue/${row.id}/retry`, {
    method: 'POST',
    headers: api.authHeaders() as any
  })
  await Promise.all([loadQueue(), loadStats()])
}

async function skip(row: any) {
  await $fetch(`${useRuntimeConfig().public.apiBase}/dot-matrix-print/queue/${row.id}/skip`, {
    method: 'POST',
    headers: api.authHeaders() as any
  })
  await Promise.all([loadQueue(), loadStats()])
}

async function reprint(row: any) {
  await $fetch(`${useRuntimeConfig().public.apiBase}/dot-matrix-print/queue/${row.id}/reprint`, {
    method: 'POST',
    headers: api.authHeaders() as any
  })
  feedback.success('Reprint copy queued')
  await Promise.all([loadQueue(), loadStats()])
}

async function pausePrinting() {
  await api.create<any>('dot-matrix-print/settings/pause', { storeId: activeStoreId.value })
  feedback.success('Dot-matrix printing paused for this store')
  await loadSetting()
}

async function resumePrinting() {
  await api.create<any>('dot-matrix-print/settings/resume', { storeId: activeStoreId.value })
  feedback.success('Dot-matrix printing resumed for this store')
  await loadSetting()
}

async function retryFailed() {
  const result = await $fetch<any>(`${useRuntimeConfig().public.apiBase}/dot-matrix-print/queue/retry-failed`, {
    method: 'POST',
    headers: api.authHeaders() as any,
    body: { storeId: activeStoreId.value }
  })
  feedback.success(`Retry queued for ${result?.count || 0} failed print rows`)
  await Promise.all([loadQueue(), loadStats()])
}

async function resetStuckPrinting() {
  const result = await $fetch<any>(`${useRuntimeConfig().public.apiBase}/dot-matrix-print/queue/reset-stuck-printing`, {
    method: 'POST',
    headers: api.authHeaders() as any,
    body: { storeId: activeStoreId.value }
  })
  feedback.success(`Reset ${result?.count || 0} stuck printing rows`)
  await Promise.all([loadQueue(), loadStats()])
}

async function previewText(row: any) {
  const text = await $fetch<string>(`${useRuntimeConfig().public.apiBase}/dot-matrix-print/queue/${row.id}/text`, {
    headers: api.authHeaders() as any
  })
  const blob = new Blob([text], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  window.open(url, '_blank', 'noopener,noreferrer')
  setTimeout(() => URL.revokeObjectURL(url), 30000)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}
</script>

<template>
  <AppShell title="Dot Matrix Print">
  <section class="dot-matrix-page">
    <UPageHeader
      title="Dot Matrix Audit Journal"
      description="Live transaction register, day opening/closing cash details and full-width daily summary for Epson LX-310 via the Ubuntu host bridge."
    />

    <UCard>
      <div class="toolbar">
        <UFormField label="Store">
          <USelect v-model="selectedStoreId" :items="stores.map(store => ({ label: `${store.name} / ${store.storeCode || ''}`, value: store.id }))" />
        </UFormField>
        <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refreshAll">Refresh</UButton>
      </div>
    </UCard>

    <div class="grid-two">
      <UCard>
        <template #header>
          <strong>Printer Settings</strong>
        </template>
        <div class="form-grid">
          <UFormField label="Enable printing">
            <USwitch v-model="setting.enabled" />
          </UFormField>
          <UFormField label="Printer name">
            <UInput v-model="setting.printerName" placeholder="EPSON_LX310" />
          </UFormField>
          <UFormField label="Output mode">
            <USelect v-model="setting.outputMode" :items="outputModeOptions" />
          </UFormField>
          <UFormField label="Line width">
            <USelect v-model="setting.lineWidth" :items="widthOptions" />
          </UFormField>
          <UFormField label="Spool directory">
            <UInput v-model="setting.spoolDirectory" />
          </UFormField>
          <UFormField label="Timezone">
            <UInput v-model="setting.timeZoneId" />
          </UFormField>
        </div>
        <div class="checks">
          <UCheckbox v-model="setting.printTransactions" label="Print transactions immediately after DB save" />
          <UCheckbox v-model="setting.printDayOpeningClosing" label="Print day opening/closing cash details and summary" />
          <UCheckbox v-model="setting.printEditsAndDeletes" label="Print edited/deleted record audit lines" />
          <UCheckbox v-model="setting.printAttendanceInDaySummary" label="Include attendance details in day summary" />
          <UCheckbox v-model="setting.printBankUpiSummary" label="Include bank / UPI summary" />
        </div>
        <template #footer>
          <div class="toolbar-actions">
            <UButton icon="i-lucide-save" :loading="saving" @click="saveSetting">Save Settings</UButton>
            <UButton v-if="setting.enabled" icon="i-lucide-pause" color="warning" variant="soft" @click="pausePrinting">Pause</UButton>
            <UButton v-else icon="i-lucide-play" color="success" variant="soft" @click="resumePrinting">Resume</UButton>
          </div>
        </template>
      </UCard>

      <UCard>
        <template #header>
          <strong>Test Print</strong>
        </template>
        <p class="muted">Use this after installing the Ubuntu DotMatrix Bridge and adding the Epson LX-310 CUPS queue, default name EPSON_LX310.</p>
        <UTextarea v-model="testMessage" :rows="4" />
        <template #footer>
          <UButton icon="i-lucide-printer" color="primary" :loading="testing" @click="sendTestPrint">Queue Test Print</UButton>
        </template>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <div class="toolbar">
          <div>
            <strong>Print Queue</strong>
            <div class="stat-row">
              <span v-for="stat in queueStats" :key="stat.status" class="stat-pill">{{ stat.status }}: {{ stat.count }}</span>
            </div>
          </div>
          <div class="toolbar-actions">
            <USelect v-model="statusFilter" :items="statusOptions" @change="loadQueue" />
            <UButton icon="i-lucide-rotate-ccw" variant="soft" @click="retryFailed">Retry failed</UButton>
            <UButton icon="i-lucide-wrench" variant="soft" color="neutral" @click="resetStuckPrinting">Reset stuck</UButton>
            <UButton icon="i-lucide-refresh-cw" variant="soft" @click="loadQueue">Reload</UButton>
          </div>
        </div>
      </template>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Date</th>
              <th>Event</th>
              <th>Source</th>
              <th>Number</th>
              <th>Party</th>
              <th class="right">Amount</th>
              <th>Mode</th>
              <th>Status</th>
              <th>Error</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in queue" :key="row.id">
              <td>{{ row.businessDate?.slice?.(0, 10) || row.businessDate }}</td>
              <td>{{ row.eventType }} / {{ row.actionType }}</td>
              <td>{{ row.sourceType }}</td>
              <td>{{ row.sourceNumber }}</td>
              <td>{{ row.partyName }}</td>
              <td class="right">{{ money(row.amount) }}</td>
              <td>{{ row.paymentMode }}</td>
              <td>{{ row.status }} <small v-if="row.retryCount">({{ row.retryCount }})</small></td>
              <td class="error-cell">{{ row.errorMessage }}</td>
              <td class="actions">
                <UButton size="xs" variant="ghost" @click="retry(row)">Retry</UButton>
                <UButton size="xs" variant="ghost" color="primary" @click="reprint(row)">Reprint</UButton>
                <UButton size="xs" variant="ghost" color="neutral" @click="skip(row)">Skip</UButton>
                <UButton size="xs" variant="ghost" color="neutral" @click="previewText(row)">Text</UButton>
              </td>
            </tr>
            <tr v-if="queue.length === 0">
              <td colspan="10" class="muted center">No queue rows for this filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </section>
  </AppShell>
</template>

<style scoped>
.dot-matrix-page { display: grid; gap: 1rem; }
.toolbar { display: flex; align-items: end; justify-content: space-between; gap: 1rem; flex-wrap: wrap; }
.toolbar-actions { display: flex; gap: .5rem; align-items: center; flex-wrap: wrap; }
.stat-row { display: flex; gap: .4rem; flex-wrap: wrap; margin-top: .35rem; }
.stat-pill { font-size: .75rem; background: rgb(241 245 249); color: rgb(71 85 105); border-radius: 999px; padding: .15rem .45rem; }
.grid-two { display: grid; grid-template-columns: minmax(0, 1.2fr) minmax(320px, .8fr); gap: 1rem; }
.form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.checks { display: grid; gap: .5rem; margin-top: 1rem; }
.muted { color: rgb(100 116 139); }
.center { text-align: center; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: .875rem; }
th, td { border-bottom: 1px solid rgb(226 232 240); padding: .55rem; vertical-align: top; }
th { text-align: left; background: rgb(248 250 252); }
.right { text-align: right; }
.error-cell { max-width: 260px; white-space: normal; color: rgb(185 28 28); }
.actions { white-space: nowrap; }
@media (max-width: 900px) { .grid-two, .form-grid { grid-template-columns: 1fr; } }
</style>

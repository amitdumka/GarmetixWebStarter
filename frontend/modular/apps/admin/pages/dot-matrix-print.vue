<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-printer" class="size-4" />
            Epson LX-310 audit journal
          </p>
          <h2 class="garmetix-dashboard-title">Dot Matrix Print</h2>
          <p class="garmetix-dashboard-subtitle">
            Configure the Ubuntu bridge, queue test prints and monitor the dot-matrix print queue from Admin.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-settings" color="neutral" variant="soft" :disabled="!activeStoreId" @click="settingsOpen = true">Settings</UButton>
          <UButton icon="i-lucide-printer" color="primary" variant="solid" :disabled="!activeStoreId" @click="testOpen = true">Test Print</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="grid gap-3 lg:grid-cols-[minmax(260px,360px)_1fr] lg:items-end">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Store</span>
          <USelect v-model="selectedStoreId" :items="storeItems" class="w-full" />
        </label>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-pause" color="warning" variant="soft" :disabled="!setting.enabled || !activeStoreId" @click="pausePrinting">Pause</UButton>
          <UButton icon="i-lucide-play" color="success" variant="soft" :disabled="setting.enabled || !activeStoreId" @click="resumePrinting">Resume</UButton>
          <UButton icon="i-lucide-rotate-ccw" color="neutral" variant="soft" :disabled="!activeStoreId" @click="retryFailed">Retry Failed</UButton>
          <UButton icon="i-lucide-wrench" color="neutral" variant="soft" :disabled="!activeStoreId" @click="resetStuckPrinting">Reset Stuck</UButton>
        </div>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,0.75fr)_minmax(0,1.25fr)]">
      <div class="garmetix-section-card">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Current Settings</h3>
            <p class="garmetix-panel-subtitle">{{ activeStoreLabel }}</p>
          </div>
          <UBadge :color="setting.enabled ? 'success' : 'warning'" variant="subtle">{{ setting.enabled ? 'Enabled' : 'Paused' }}</UBadge>
        </div>
        <dl class="mt-4 grid gap-3 text-sm">
          <div v-for="item in settingRows" :key="item.label" class="border-b border-default pb-2">
            <dt class="text-xs text-muted">{{ item.label }}</dt>
            <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
          </div>
        </dl>
      </div>

      <div class="garmetix-table-panel">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Print Queue</h3>
            <p class="garmetix-panel-subtitle">{{ queue.length }} row(s) for {{ activeStoreLabel }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <USelect v-model="statusFilter" :items="statusOptions" class="w-40" />
            <UButton icon="i-lucide-refresh-cw" size="sm" color="neutral" variant="soft" :loading="queueLoading" @click="loadQueue">Reload</UButton>
          </div>
        </div>

        <div class="overflow-hidden rounded-lg border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[1120px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="px-3 py-2 font-medium">Date</th>
                  <th class="px-3 py-2 font-medium">Event</th>
                  <th class="px-3 py-2 font-medium">Source</th>
                  <th class="px-3 py-2 font-medium">Number</th>
                  <th class="px-3 py-2 font-medium">Party</th>
                  <th class="px-3 py-2 text-right font-medium">Amount</th>
                  <th class="px-3 py-2 font-medium">Mode</th>
                  <th class="px-3 py-2 font-medium">Status</th>
                  <th class="px-3 py-2 font-medium">Error</th>
                  <th class="px-3 py-2 font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="queue.length === 0">
                  <td colspan="10" class="px-3 py-8 text-center text-muted">No queue rows for this filter.</td>
                </tr>
                <tr v-for="row in queue" :key="readText(row, ['id'])" class="bg-default/40">
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDateTime(row.businessDate) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['eventType']) }} / {{ readText(row, ['actionType']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['sourceType']) }}</td>
                  <td class="max-w-36 truncate px-3 py-2">{{ readText(row, ['sourceNumber']) }}</td>
                  <td class="max-w-44 truncate px-3 py-2">{{ readText(row, ['partyName']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(readNumber(row, ['amount'])) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['paymentMode']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">
                    <UBadge :color="statusColor(readText(row, ['status']))" variant="subtle">{{ readText(row, ['status']) }}<span v-if="readNumber(row, ['retryCount'])"> ({{ readNumber(row, ['retryCount']) }})</span></UBadge>
                  </td>
                  <td class="max-w-56 truncate px-3 py-2 text-warning">{{ readText(row, ['errorMessage'], '') }}</td>
                  <td class="px-3 py-2">
                    <div class="flex flex-wrap gap-1">
                      <UButton size="xs" color="neutral" variant="ghost" @click="retry(row)">Retry</UButton>
                      <UButton size="xs" color="primary" variant="ghost" @click="reprint(row)">Reprint</UButton>
                      <UButton size="xs" color="neutral" variant="ghost" @click="skip(row)">Skip</UButton>
                      <UButton size="xs" color="neutral" variant="soft" @click="previewText(row)">Text</UButton>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>

    <UModal v-model:open="settingsOpen" title="Printer Settings" :description="activeStoreLabel" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-4xl' }">
      <template #body>
        <form class="grid gap-4 sm:grid-cols-2" @submit.prevent="saveSetting">
          <label class="flex items-center justify-between gap-3 rounded-lg border border-default p-3 text-sm sm:col-span-2">
            <span>
              <span class="block font-medium">Enable printing</span>
              <span class="text-xs text-muted">Pause this before printer maintenance or paper changes.</span>
            </span>
            <USwitch v-model="setting.enabled" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Printer name</span>
            <UInput v-model="setting.printerName" placeholder="EPSON_LX310" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Output mode</span>
            <USelect v-model="setting.outputMode" :items="outputModeOptions" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Line width</span>
            <USelect v-model="setting.lineWidth" :items="widthOptions" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Poll seconds</span>
            <UInput v-model="setting.pollSeconds" type="number" min="1" step="1" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Retry limit</span>
            <UInput v-model="setting.retryLimit" type="number" min="0" step="1" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Timezone</span>
            <UInput v-model="setting.timeZoneId" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Spool directory</span>
            <UInput v-model="setting.spoolDirectory" />
          </label>

          <div class="grid gap-2 sm:col-span-2">
            <UCheckbox v-model="setting.printTransactions" label="Print transactions immediately after DB save" />
            <UCheckbox v-model="setting.printDayOpeningClosing" label="Print day opening/closing cash details and summary" />
            <UCheckbox v-model="setting.printEditsAndDeletes" label="Print edited/deleted record audit lines" />
            <UCheckbox v-model="setting.printAttendanceInDaySummary" label="Include attendance details in day summary" />
            <UCheckbox v-model="setting.printBankUpiSummary" label="Include bank / UPI summary" />
          </div>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="button" color="neutral" variant="ghost" @click="settingsOpen = false">Cancel</UButton>
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Settings</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="testOpen" title="Queue Test Print" :description="activeStoreLabel" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-xl' }">
      <template #body>
        <div class="space-y-3">
          <UAlert color="neutral" variant="subtle" icon="i-lucide-info" title="Bridge service required" description="Use after the Ubuntu DotMatrix Bridge and Epson LX-310 CUPS queue are installed." />
          <UTextarea v-model="testMessage" :rows="5" />
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="ghost" @click="testOpen = false">Cancel</UButton>
            <UButton icon="i-lucide-printer" color="primary" :loading="testing" @click="sendTestPrint">Queue Test Print</UButton>
          </div>
        </div>
      </template>
    </UModal>

    <USlideover v-model:open="textOpen" title="Printable Text" :description="textPreviewTitle" :ui="{ content: 'w-full sm:max-w-3xl lg:max-w-4xl' }">
      <template #body>
        <pre class="max-h-[70vh] overflow-auto rounded-lg border border-default bg-muted/30 p-4 text-xs leading-relaxed">{{ textPreview || 'No text loaded.' }}</pre>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { createApiUrl } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatDateTime, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Dot Matrix Print - Garmetix Admin' })

const { apiBaseUrl, get, post } = useAdminApiClient()
const loading = ref(false)
const queueLoading = ref(false)
const saving = ref(false)
const testing = ref(false)
const error = ref('')
const message = ref('')
const stores = ref<ApiRecord[]>([])
const queue = ref<ApiRecord[]>([])
const queueStats = ref<ApiRecord[]>([])
const selectedStoreId = ref('')
const statusFilter = ref('Pending')
const settingsOpen = ref(false)
const testOpen = ref(false)
const textOpen = ref(false)
const textPreview = ref('')
const textPreviewTitle = ref('')
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

const statusOptions = [
  { label: 'Pending', value: 'Pending' },
  { label: 'Printing', value: 'Printing' },
  { label: 'Failed', value: 'Failed' },
  { label: 'Printed', value: 'Printed' },
  { label: 'Skipped', value: 'Skipped' },
  { label: 'All', value: 'All' }
]
const outputModeOptions = [
  { label: 'Ubuntu host bridge service', value: 'BridgeService' },
  { label: 'Disabled', value: 'Disabled' },
  { label: 'Legacy spool file', value: 'SpoolFile' },
  { label: 'Direct lp command', value: 'LpCommand' }
]
const widthOptions = [
  { label: '80 column', value: 80 },
  { label: '136 column / full width', value: 136 }
]
const activeStoreId = computed(() => selectedStoreId.value || readText(stores.value[0], ['id'], ''))
const activeStoreLabel = computed(() => {
  const store = stores.value.find(item => readText(item, ['id'], '') === activeStoreId.value)
  return store ? `${readText(store, ['storeName', 'name'], 'Store')} ${readText(store, ['storeCode'], '')}`.trim() : 'No store selected'
})
const storeItems = computed(() => stores.value.map(store => ({
  label: `${readText(store, ['storeName', 'name'], 'Store')} ${readText(store, ['storeCode'], '')}`.trim(),
  value: readText(store, ['id'], '')
})).filter(item => item.value))
const statMap = computed(() => new Map(queueStats.value.map(row => [readText(row, ['status']), readNumber(row, ['count'])])))
const cards = computed(() => [
  { label: 'Pending', value: statMap.value.get('Pending') ?? 0, detail: 'Waiting for bridge service' },
  { label: 'Printing', value: statMap.value.get('Printing') ?? 0, detail: 'Currently in progress' },
  { label: 'Failed', value: statMap.value.get('Failed') ?? 0, detail: 'Needs retry or skip' },
  { label: 'Printed', value: statMap.value.get('Printed') ?? 0, detail: 'Completed rows' },
  { label: 'Skipped', value: statMap.value.get('Skipped') ?? 0, detail: 'Manually skipped' }
])
const settingRows = computed(() => [
  { label: 'Printer', value: setting.printerName },
  { label: 'Output Mode', value: setting.outputMode },
  { label: 'Spool Directory', value: setting.spoolDirectory },
  { label: 'Line Width', value: `${setting.lineWidth} column` },
  { label: 'Poll Seconds', value: `${setting.pollSeconds}s` },
  { label: 'Retry Limit', value: setting.retryLimit },
  { label: 'Transactions', value: setting.printTransactions ? 'Enabled' : 'Disabled' },
  { label: 'Day Opening / Closing', value: setting.printDayOpeningClosing ? 'Enabled' : 'Disabled' }
])

async function loadStores() {
  stores.value = toRows(await get<unknown>('stores'))
  if (!selectedStoreId.value) selectedStoreId.value = readText(stores.value[0], ['id'], '')
}

async function refreshAll() {
  if (!activeStoreId.value) return
  loading.value = true
  error.value = ''
  try {
    await Promise.all([loadSetting(), loadQueue(), loadStats()])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to refresh dot-matrix print status.'
  } finally {
    loading.value = false
  }
}

async function loadSetting() {
  const row = await get<ApiRecord>('dot-matrix-print/settings', { storeId: activeStoreId.value })
  Object.assign(setting, row)
}

async function saveSetting() {
  if (!activeStoreId.value) return
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    await post<ApiRecord>('dot-matrix-print/settings', { ...setting, storeId: activeStoreId.value })
    message.value = 'Dot-matrix print settings saved.'
    settingsOpen.value = false
    await loadSetting()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save dot-matrix settings.'
  } finally {
    saving.value = false
  }
}

async function sendTestPrint() {
  if (!activeStoreId.value) return
  testing.value = true
  error.value = ''
  message.value = ''
  try {
    await post<ApiRecord>('dot-matrix-print/test', { storeId: activeStoreId.value, message: testMessage.value })
    message.value = 'Test print queued.'
    testOpen.value = false
    await Promise.all([loadQueue(), loadStats()])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to queue test print.'
  } finally {
    testing.value = false
  }
}

async function loadQueue() {
  if (!activeStoreId.value) return
  queueLoading.value = true
  try {
    const query: Record<string, string> = { storeId: activeStoreId.value }
    if (statusFilter.value !== 'All') query.status = statusFilter.value
    queue.value = toRows(await get<unknown>('dot-matrix-print/queue', query))
  } finally {
    queueLoading.value = false
  }
}

async function loadStats() {
  if (!activeStoreId.value) return
  queueStats.value = toRows(await get<unknown>('dot-matrix-print/queue/stats', { storeId: activeStoreId.value }))
}

async function retry(row: ApiRecord) {
  await runQueueAction(`dot-matrix-print/queue/${readText(row, ['id'], '')}/retry`, 'Print row moved back to pending.')
}

async function skip(row: ApiRecord) {
  await runQueueAction(`dot-matrix-print/queue/${readText(row, ['id'], '')}/skip`, 'Print row skipped.')
}

async function reprint(row: ApiRecord) {
  await runQueueAction(`dot-matrix-print/queue/${readText(row, ['id'], '')}/reprint`, 'Reprint copy queued.')
}

async function retryFailed() {
  await runQueueAction('dot-matrix-print/queue/retry-failed', 'Failed rows moved back to pending.', { storeId: activeStoreId.value })
}

async function resetStuckPrinting() {
  await runQueueAction('dot-matrix-print/queue/reset-stuck-printing', 'Stuck printing rows reset.', { storeId: activeStoreId.value })
}

async function pausePrinting() {
  await runQueueAction('dot-matrix-print/settings/pause', 'Dot-matrix printing paused for this store.', { storeId: activeStoreId.value })
  await loadSetting()
}

async function resumePrinting() {
  await runQueueAction('dot-matrix-print/settings/resume', 'Dot-matrix printing resumed for this store.', { storeId: activeStoreId.value })
  await loadSetting()
}

async function runQueueAction(path: string, successMessage: string, body?: unknown) {
  if (!path || path.includes('//')) return
  error.value = ''
  message.value = ''
  try {
    await post<ApiRecord>(path, body)
    message.value = successMessage
    await Promise.all([loadQueue(), loadStats()])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Dot-matrix queue action failed.'
  }
}

async function previewText(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  if (!id) return
  error.value = ''
  try {
    const response = await fetch(apiUrl(`dot-matrix-print/queue/${id}/text`), { headers: authHeaders() })
    if (!response.ok) throw new Error(`Unable to load printable text (${response.status}).`)
    textPreview.value = await response.text()
    textPreviewTitle.value = `${readText(row, ['sourceType'])} ${readText(row, ['sourceNumber'])}`
    textOpen.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to preview printable text.'
  }
}

function apiUrl(path: string) {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'http://localhost'
  return new URL(createApiUrl(apiBaseUrl.value, path), origin).toString()
}

function authHeaders() {
  const headers = new Headers()
  const token = getStoredToken(window.localStorage)
  if (token) headers.set('Authorization', `Bearer ${token}`)
  return headers
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function statusColor(status: string) {
  if (status === 'Printed') return 'success'
  if (status === 'Failed') return 'error'
  if (status === 'Printing') return 'primary'
  if (status === 'Skipped') return 'warning'
  return 'neutral'
}

watch(selectedStoreId, refreshAll)
watch(statusFilter, loadQueue)

onMounted(async () => {
  loading.value = true
  try {
    await loadStores()
    await refreshAll()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load dot-matrix print page.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-receipt-text" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Digital Bills</h2>
          <p class="garmetix-dashboard-subtitle">Generate secure invoice links, copy/share them, send WhatsApp, and review customer activity.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-chart-no-axes-combined" @click="router.push('/marketing/digital-bill-analytics')">Analytics</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-megaphone" @click="router.push('/marketing/campaign-audiences')">Audiences</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label"><UIcon :name="metric.icon" class="mr-1 inline size-4" /> {{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.value }}</p>
        <p class="garmetix-metric-caption">{{ metric.detail }}</p>
      </div>
    </section>

    <div class="garmetix-section-card">
      <div class="grid gap-3 lg:grid-cols-[minmax(260px,1fr)_160px_auto]">
        <UFormField label="Search">
          <UInput v-model="filters.q" icon="i-lucide-search" placeholder="Invoice, customer, mobile" @keyup.enter="refresh(true)" />
        </UFormField>
        <UFormField label="Page size">
          <USelect v-model="filters.pageSize" :items="pageSizeOptions" @update:model-value="refresh(true)" />
        </UFormField>
        <div class="flex flex-wrap items-end gap-2">
          <UButton icon="i-lucide-search-check" :loading="loading" @click="refresh(true)">Search</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="clearSearch">Clear</UButton>
        </div>
      </div>

      <div class="mt-4 grid gap-3 lg:grid-cols-[minmax(260px,1fr)_auto]">
        <UFormField label="Generate by invoice ID or number">
          <UInput v-model="generateInvoiceKey" placeholder="Paste sale invoice ID or invoice number" @keyup.enter="generateDigitalBill" />
        </UFormField>
        <div class="flex items-end">
          <UButton class="w-full justify-center" icon="i-lucide-link" :loading="generating" @click="generateDigitalBill">Generate Link</UButton>
        </div>
      </div>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[1200px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Invoice</th>
            <th class="border-b border-default p-3">Customer</th>
            <th class="border-b border-default p-3 text-right">Amount</th>
            <th class="border-b border-default p-3 text-right">Open</th>
            <th class="border-b border-default p-3 text-right">PDF</th>
            <th class="border-b border-default p-3 text-right">Review</th>
            <th class="border-b border-default p-3">WhatsApp</th>
            <th class="border-b border-default p-3">Status</th>
            <th class="border-b border-default p-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="9" class="p-8 text-center text-muted">Loading digital bills...</td>
          </tr>
          <tr v-else-if="!digitalBills.length">
            <td colspan="9" class="p-8 text-center text-muted">No digital bills found. Generate one from an invoice number.</td>
          </tr>
          <template v-else>
            <tr v-for="bill in digitalBills" :key="String(bill.id)">
              <td class="border-b border-default p-3">
                <p class="font-semibold text-highlighted">{{ readText(bill, ['invoiceNumber']) }}</p>
                <p class="text-xs text-muted">{{ formatDate(readText(bill, ['invoiceDate'], '')) }}</p>
              </td>
              <td class="border-b border-default p-3">
                <p class="font-medium text-highlighted">{{ readText(bill, ['customerName']) }}</p>
                <p class="text-xs text-muted">{{ readText(bill, ['customerMobile']) }}</p>
              </td>
              <td class="border-b border-default p-3 text-right">{{ money(readNumber(bill, ['amount'])) }}</td>
              <td class="border-b border-default p-3 text-right">{{ number(readNumber(bill, ['openCount'])) }}</td>
              <td class="border-b border-default p-3 text-right">{{ number(readNumber(bill, ['pdfDownloadCount'])) }}</td>
              <td class="border-b border-default p-3 text-right">{{ number(readNumber(bill, ['reviewClickCount'])) }}</td>
              <td class="border-b border-default p-3"><UBadge :color="whatsAppColor(bill)" variant="subtle">{{ readText(bill, ['whatsAppStatus'], 'Not sent') }}</UBadge></td>
              <td class="border-b border-default p-3"><UBadge :color="bill.isActive === false ? 'neutral' : 'success'" variant="subtle">{{ bill.isActive === false ? 'Disabled' : 'Active' }}</UBadge></td>
              <td class="border-b border-default p-3">
                <div class="flex flex-wrap justify-end gap-2">
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="selectBill(bill)">Activity</UButton>
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-copy" :disabled="!digitalBillUrl(bill)" @click="copyLink(bill)">Copy</UButton>
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-external-link" :disabled="!digitalBillUrl(bill)" @click="openLink(bill)">Open</UButton>
                  <UButton size="xs" icon="i-lucide-send" :loading="actionBusyId === bill.id" @click="sendWhatsApp(bill)">WhatsApp</UButton>
                  <UButton size="xs" color="warning" variant="soft" icon="i-lucide-refresh-ccw" :loading="actionBusyId === bill.id" @click="regenerateToken(bill)">Token</UButton>
                  <UButton size="xs" color="error" variant="soft" icon="i-lucide-ban" :loading="actionBusyId === bill.id" :disabled="bill.isActive === false" @click="disableBill(bill)">Disable</UButton>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
    <div v-if="digitalBills.length" class="flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
      <p>Page {{ filters.page }} of {{ totalPages }} - {{ total }} digital bill(s)</p>
      <div class="flex items-center gap-2">
        <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="filters.page <= 1 || loading" @click="changePage(-1)">Prev</UButton>
        <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="filters.page >= totalPages || loading" @click="changePage(1)">Next</UButton>
      </div>
    </div>

    <div v-if="selectedBill" class="garmetix-section-card">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-activity" class="size-4" /> Activity</p>
          <h3 class="text-xl font-semibold text-highlighted">{{ readText(selectedBill, ['invoiceNumber']) }}</h3>
          <p class="text-sm text-muted">{{ readText(selectedBill, ['customerName']) }} · {{ readText(selectedBill, ['customerMobile']) }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="activityLoading" @click="loadActivity(selectedBill)">Reload Activity</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="selectedBill = null">Close</UButton>
        </div>
      </div>

      <UAlert v-if="activityError" class="mb-4" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="activityError" />

      <section class="mb-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <div v-for="metric in activityMetrics" :key="metric.label" class="rounded-md border border-default p-3">
          <p class="text-xs uppercase text-muted">{{ metric.label }}</p>
          <p class="text-xl font-semibold text-highlighted">{{ metric.value }}</p>
        </div>
      </section>

      <div class="grid gap-4 xl:grid-cols-2">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[620px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr><th class="border-b border-default p-3">At</th><th class="border-b border-default p-3">Type</th><th class="border-b border-default p-3">Detail</th></tr>
            </thead>
            <tbody>
              <tr v-if="activityLoading"><td colspan="3" class="p-6 text-center text-muted">Loading activity...</td></tr>
              <tr v-else-if="!timeline.length"><td colspan="3" class="p-6 text-center text-muted">No activity yet.</td></tr>
              <template v-else>
                <tr v-for="item in timeline" :key="`${readText(item, ['at'])}-${readText(item, ['type'])}-${readText(item, ['title'])}`">
                  <td class="border-b border-default p-3">{{ formatDateTime(readText(item, ['at'], '')) }}</td>
                  <td class="border-b border-default p-3">{{ readText(item, ['type']) }}</td>
                  <td class="border-b border-default p-3">
                    <p class="font-medium text-highlighted">{{ readText(item, ['title']) }}</p>
                    <p class="text-xs text-muted">{{ readText(item, ['description', 'detail'], '') }}</p>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[620px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr><th class="border-b border-default p-3">Feedback</th><th class="border-b border-default p-3">Rating</th><th class="border-b border-default p-3">Submitted</th></tr>
            </thead>
            <tbody>
              <tr v-if="activityLoading"><td colspan="3" class="p-6 text-center text-muted">Loading feedback...</td></tr>
              <tr v-else-if="!feedbackRows.length"><td colspan="3" class="p-6 text-center text-muted">No feedback submitted.</td></tr>
              <template v-else>
                <tr v-for="row in feedbackRows" :key="String(row.id)">
                  <td class="border-b border-default p-3">{{ readText(row, ['message'], '-') }}</td>
                  <td class="border-b border-default p-3">{{ number(readNumber(row, ['rating'])) }}</td>
                  <td class="border-b border-default p-3">{{ formatDateTime(readText(row, ['submittedAt'], '')) }}</td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readRecord, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const route = useRoute()
const toast = useToast()
const { get, post } = useCrmApiClient()

const filters = reactive({
  page: 1,
  pageSize: 50,
  q: typeof route.query.q === 'string' ? route.query.q : ''
})
const pageSizeOptions = [25, 50, 100, 200]
const response = ref<ApiRecord | null>(null)
const digitalBills = ref<ApiRecord[]>([])
const selectedBill = ref<ApiRecord | null>(null)
const activity = ref<ApiRecord | null>(null)
const generateInvoiceKey = ref('')
const loading = ref(false)
const generating = ref(false)
const activityLoading = ref(false)
const actionBusyId = ref('')
const loadError = ref('')
const activityError = ref('')

const total = computed(() => readNumber(response.value, ['total']) || digitalBills.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / filters.pageSize)))
const timeline = computed(() => readArray(activity.value, ['timeline', 'events']))
const feedbackRows = computed(() => readArray(activity.value, ['feedback']))
const activityTotals = computed(() => readRecord(activity.value?.totals) ?? {})

const metrics = computed(() => [
  { label: 'Digital Bills', value: number(total.value), detail: 'Matching register rows', icon: 'i-lucide-receipt-text' },
  { label: 'Active', value: number(digitalBills.value.filter(row => row.isActive !== false).length), detail: 'Visible customer links', icon: 'i-lucide-link' },
  { label: 'Opens', value: number(digitalBills.value.reduce((sum, row) => sum + readNumber(row, ['openCount']), 0)), detail: 'Page open count', icon: 'i-lucide-eye' },
  { label: 'PDF', value: number(digitalBills.value.reduce((sum, row) => sum + readNumber(row, ['pdfDownloadCount']), 0)), detail: 'PDF downloads', icon: 'i-lucide-file-down' },
  { label: 'Reviews', value: number(digitalBills.value.reduce((sum, row) => sum + readNumber(row, ['reviewClickCount']), 0)), detail: 'Review clicks', icon: 'i-lucide-star' }
])

const activityMetrics = computed(() => [
  { label: 'Open', value: number(readNumber(activityTotals.value, ['openCount'])) },
  { label: 'PDF', value: number(readNumber(activityTotals.value, ['pdfDownloadCount'])) },
  { label: 'Review', value: number(readNumber(activityTotals.value, ['reviewClickCount'])) },
  { label: 'Feedback', value: number(readNumber(activityTotals.value, ['feedbackCount'])) },
  { label: 'WhatsApp', value: number(readNumber(activityTotals.value, ['whatsAppLogCount'])) },
  { label: 'Failed', value: number(readNumber(activityTotals.value, ['whatsAppFailedCount'])) }
])

function money(value: number) {
  return formatIndianMoney(value)
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date)
}

function digitalBillUrl(row: ApiRecord | null) {
  const path = readText(row, ['publicPath'], '')
  if (!path) return ''
  try {
    return new URL(path, window.location.origin).toString()
  } catch {
    return path
  }
}

function whatsAppColor(row: ApiRecord) {
  const status = readText(row, ['whatsAppStatus'], '').toLowerCase()
  if (status.includes('sent') || status.includes('delivered') || status.includes('read')) return 'success' as const
  if (status.includes('fail')) return 'error' as const
  if (status.includes('pending') || status.includes('manual')) return 'warning' as const
  return 'neutral' as const
}

function clearSearch() {
  filters.q = ''
  filters.page = 1
  refresh()
}

function changePage(delta: number) {
  const next = filters.page + delta
  if (next < 1 || next > totalPages.value) return
  filters.page = next
  refresh()
}

function setRows(value: unknown) {
  const record = readRecord(value)
  response.value = record
  digitalBills.value = toRows(value)
}

async function refresh(resetPage = false) {
  if (resetPage) filters.page = 1
  loading.value = true
  loadError.value = ''
  try {
    const data = await get<unknown>('digital-bills', {
      page: filters.page,
      pageSize: filters.pageSize,
      q: filters.q || undefined
    })
    setRows(data)
  } catch (caught) {
    loadError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load digital bills.')
    toast.add({ title: 'Could not load digital bills', description: loadError.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function generateDigitalBill() {
  const invoiceKey = generateInvoiceKey.value.trim()
  if (!invoiceKey) {
    toast.add({ title: 'Enter invoice ID or number first', color: 'warning' })
    return
  }

  generating.value = true
  try {
    const result = await post<ApiRecord>('digital-bills/sales/generate', { invoiceKey })
    toast.add({ title: readText(result, ['message'], 'Digital bill generated'), description: readText(result, ['publicPath'], ''), color: 'success' })
    generateInvoiceKey.value = ''
    await refresh(true)
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'Could not generate digital bill.')
    toast.add({ title: 'Could not generate digital bill', description: message, color: 'error' })
  } finally {
    generating.value = false
  }
}

async function copyLink(row: ApiRecord) {
  const url = digitalBillUrl(row)
  if (!url) return
  try {
    await navigator.clipboard.writeText(url)
    toast.add({ title: 'Digital bill link copied', description: url, color: 'success' })
  } catch {
    window.prompt('Copy digital bill link', url)
  }
}

function openLink(row: ApiRecord) {
  const url = digitalBillUrl(row)
  if (url) window.open(url, '_blank', 'noopener,noreferrer')
}

async function selectBill(row: ApiRecord) {
  selectedBill.value = row
  await loadActivity(row)
}

async function loadActivity(row: ApiRecord | null) {
  if (!row?.id) return
  activityLoading.value = true
  activityError.value = ''
  activity.value = null
  try {
    activity.value = readRecord(await get<unknown>(`digital-bills/${row.id}/activity`))
  } catch (caught) {
    activityError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load digital bill activity.')
    toast.add({ title: 'Could not load activity', description: activityError.value, color: 'error' })
  } finally {
    activityLoading.value = false
  }
}

async function runAction(row: ApiRecord, action: () => Promise<unknown>, successTitle: string) {
  actionBusyId.value = String(row.id || '')
  try {
    const result = readRecord(await action())
    toast.add({ title: successTitle, description: readText(result, ['message', 'status', 'errorMessage'], ''), color: 'success' })
    await refresh()
    if (selectedBill.value?.id === row.id) {
      const updated = digitalBills.value.find(item => item.id === row.id) ?? row
      selectedBill.value = updated
      await loadActivity(updated)
    }
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'Digital bill action failed.')
    toast.add({ title: 'Digital bill action failed', description: message, color: 'error' })
  } finally {
    actionBusyId.value = ''
  }
}

async function sendWhatsApp(row: ApiRecord) {
  await runAction(row, () => post(`digital-bills/${row.id}/send-whatsapp`, { force: true }), 'WhatsApp send requested')
}

async function regenerateToken(row: ApiRecord) {
  await runAction(row, () => post(`digital-bills/${row.id}/regenerate-token`, {}), 'Digital bill token regenerated')
}

async function disableBill(row: ApiRecord) {
  await runAction(row, () => post(`digital-bills/${row.id}/disable`, { reason: 'Disabled from CRM Digital Bills page' }), 'Digital bill disabled')
}

onMounted(refresh)
useHead({ title: 'Digital Bills - Garmetix CRM' })
</script>

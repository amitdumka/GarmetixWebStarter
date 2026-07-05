<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-users-round" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Campaign Audiences</h2>
          <p class="garmetix-dashboard-subtitle">Segment customers by opens, reviews, feedback, WhatsApp delivery and campaign readiness.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-copy" :disabled="!rows.length" @click="copyMobiles">Copy Mobiles</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-file-down" :disabled="!rows.length" @click="exportCsv">Export CSV</UButton>
          <UButton icon="i-lucide-megaphone" @click="openCampaignCreate">Create Campaign</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />
    <UAlert v-if="audience?.isTruncated" color="warning" variant="subtle" icon="i-lucide-triangle-alert" title="Large audience range truncated" description="Narrow the date or store filter for complete segmentation." />

    <div class="garmetix-section-card grid gap-3 xl:grid-cols-[150px_150px_220px_220px_1fr_auto]">
      <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
      <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
      <UFormField label="Store"><USelect v-model="filters.storeId" :items="storeOptions" /></UFormField>
      <UFormField label="Segment"><USelect v-model="filters.segment" :items="segmentOptions" /></UFormField>
      <UFormField label="Search"><UInput v-model="search" placeholder="Customer, mobile, invoice" @keyup.enter="refresh(true)" /></UFormField>
      <div class="flex items-end"><UButton icon="i-lucide-search" :loading="loading" @click="refresh(true)">Apply</UButton></div>
    </div>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div
        v-for="item in summaries"
        :key="readText(item, ['segment'])"
        class="garmetix-metric-card cursor-pointer"
        :class="{ 'ring-2 ring-primary': readText(item, ['segment']) === filters.segment }"
        @click="applySummary(readText(item, ['segment'], 'all'))"
      >
        <p class="garmetix-metric-label">{{ readText(item, ['label']) }}</p>
        <p class="garmetix-metric-value">{{ number(readNumber(item, ['count'])) }}</p>
        <p class="garmetix-metric-caption">{{ readText(item, ['hint']) }}</p>
      </div>
    </section>

    <div class="garmetix-section-card">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-filter" class="size-4" /> Audience list</p>
          <h3 class="text-xl font-semibold text-highlighted">{{ selectedSummaryLabel }}</h3>
          <p class="text-sm text-muted">{{ number(total) }} customer(s), {{ number(campaignReadyCount) }} ready on this page</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--; refresh()">Prev</UButton>
          <UBadge color="neutral" variant="soft">Page {{ page }} / {{ totalPages }}</UBadge>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++; refresh()">Next</UButton>
        </div>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full min-w-[1320px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr>
              <th class="border-b border-default p-3">Customer</th>
              <th class="border-b border-default p-3">Last Invoice</th>
              <th class="border-b border-default p-3 text-right">Value</th>
              <th class="border-b border-default p-3 text-right">Engagement</th>
              <th class="border-b border-default p-3">WhatsApp</th>
              <th class="border-b border-default p-3">Feedback</th>
              <th class="border-b border-default p-3">Recommendation</th>
              <th class="border-b border-default p-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="8" class="p-8 text-center text-muted">Loading audience...</td></tr>
            <tr v-else-if="!rows.length"><td colspan="8" class="p-8 text-center text-muted">No audience rows for this filter.</td></tr>
            <template v-else>
              <tr v-for="row in rows" :key="readText(row, ['audienceKey'])">
                <td class="border-b border-default p-3">
                  <p class="font-medium text-highlighted">{{ readText(row, ['customerName'], 'Walk-in Customer') }}</p>
                  <p class="text-xs text-muted">{{ readText(row, ['customerMobile'], 'No mobile') }} · {{ readText(row, ['storeName']) }}</p>
                </td>
                <td class="border-b border-default p-3">
                  <p class="font-medium text-highlighted">{{ readText(row, ['lastInvoiceNumber']) }}</p>
                  <p class="text-xs text-muted">{{ formatDate(readText(row, ['lastInvoiceDate'], '')) }} · {{ number(readNumber(row, ['invoiceCount'])) }} bill(s)</p>
                </td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['totalAmount'])) }}</td>
                <td class="border-b border-default p-3 text-right text-xs">
                  Open {{ number(readNumber(row, ['openCount'])) }} · PDF {{ number(readNumber(row, ['pdfDownloadCount'])) }}<br>
                  Review {{ number(readNumber(row, ['reviewClickCount'])) }} · Banner {{ number(readNumber(row, ['bannerClickCount'])) }}
                </td>
                <td class="border-b border-default p-3">
                  <UBadge :color="statusColor(readText(row, ['lastWhatsAppStatus'], ''))" variant="subtle">{{ readText(row, ['lastWhatsAppStatus'], 'NotSent') }}</UBadge>
                  <p class="text-xs text-muted">Sent {{ number(readNumber(row, ['whatsAppSentCount'])) }} · Failed {{ number(readNumber(row, ['whatsAppFailedCount'])) }}</p>
                </td>
                <td class="border-b border-default p-3">
                  <p>Last {{ ratingLabel(readNumber(row, ['lastFeedbackRating'])) }}</p>
                  <p class="text-xs text-muted">Worst {{ ratingLabel(readNumber(row, ['worstFeedbackRating'])) }} · Count {{ number(readNumber(row, ['feedbackCount'])) }}</p>
                </td>
                <td class="border-b border-default p-3">
                  <p class="max-w-sm">{{ readText(row, ['recommendation']) }}</p>
                  <p class="text-xs text-muted">Last activity: {{ formatDateTime(readText(row, ['lastActivityAt'], '')) }}</p>
                </td>
                <td class="border-b border-default p-3">
                  <div class="flex flex-wrap justify-end gap-2">
                    <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-external-link" :disabled="!readText(row, ['lastPublicPath'], '')" @click="openPublicBill(row)">Bill</UButton>
                    <UButton size="xs" icon="i-lucide-activity" @click="openActivity(row)">Activity</UButton>
                  </div>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readRecord, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get } = useCrmApiClient()
const ALL_STORES_VALUE = '__ALL_STORES__'
const today = new Date()
const start = new Date(Date.now() - 29 * 24 * 60 * 60 * 1000)

const segmentOptions = [
  { label: 'All customers', value: 'all' },
  { label: 'Opened bill', value: 'opened' },
  { label: 'Not opened', value: 'notOpened' },
  { label: 'PDF downloaded', value: 'pdfDownloaded' },
  { label: 'Review clicked', value: 'reviewClicked' },
  { label: 'Review pending', value: 'reviewPending' },
  { label: 'Feedback submitted', value: 'feedbackSubmitted' },
  { label: 'Low feedback', value: 'lowFeedback' },
  { label: 'WhatsApp failed', value: 'whatsappFailed' },
  { label: 'WhatsApp pending', value: 'whatsappPending' },
  { label: 'No mobile', value: 'noMobile' }
]

const filters = reactive({
  fromDate: toDateInput(start),
  toDate: toDateInput(today),
  storeId: ALL_STORES_VALUE,
  segment: 'all'
})
const stores = ref<ApiRecord[]>([])
const audience = ref<ApiRecord | null>(null)
const search = ref('')
const page = ref(1)
const pageSize = ref(50)
const loading = ref(false)
const loadError = ref('')

const rows = computed(() => readArray(audience.value, ['items']))
const summaries = computed(() => readArray(audience.value, ['summaries']))
const total = computed(() => readNumber(audience.value, ['total']))
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const selectedSummaryLabel = computed(() => readText(summaries.value.find(item => readText(item, ['segment']) === filters.segment), ['label'], 'Audience list'))
const campaignReadyCount = computed(() => rows.value.filter(row => readText(row, ['customerMobile'], '') && readNumber(row, ['whatsAppFailedCount']) === 0 && !(readNumber(row, ['worstFeedbackRating']) > 0 && readNumber(row, ['worstFeedbackRating']) <= 3)).length)
const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map(item => ({ label: `${readText(item, ['name', 'storeName', 'storeCode'], 'Store')} (${readText(item, ['storeCode'], 'store')})`, value: readText(item, ['id'], '') }))
])

function toDateInput(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function money(value: number) {
  return formatIndianMoney(value)
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString('en-IN')
}

function ratingLabel(value: number) {
  return value ? `${number(value)}/5` : '-'
}

function statusColor(status: string) {
  if (['Sent', 'Delivered', 'Read'].includes(status)) return 'success' as const
  if (['Failed', 'Skipped', 'RetryLimitReached'].includes(status)) return 'error' as const
  if (['Queued', 'ManualPending', 'NotConfigured', 'NotSent', 'Pending'].includes(status)) return 'warning' as const
  return 'neutral' as const
}

async function refresh(resetPage = false) {
  if (resetPage) page.value = 1
  loading.value = true
  loadError.value = ''
  try {
    const [storeData, audienceData] = await Promise.all([
      get<unknown>('stores').catch(() => []),
      get<unknown>('digital-bill-audiences', {
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        segment: filters.segment,
        page: page.value,
        pageSize: pageSize.value,
        storeId: filters.storeId !== ALL_STORES_VALUE ? filters.storeId : undefined,
        q: search.value.trim() || undefined
      })
    ])
    stores.value = toRows(storeData)
    audience.value = readRecord(audienceData)
  } catch (caught) {
    loadError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Campaign audience load failed.')
    toast.add({ title: 'Audience load failed', description: loadError.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

function applySummary(segment: string) {
  filters.segment = segment || 'all'
  refresh(true)
}

async function copyMobiles() {
  const mobiles = [...new Set(rows.value.map(row => readText(row, ['customerMobile'], '').trim()).filter(Boolean))]
  if (!mobiles.length) {
    toast.add({ title: 'No mobile numbers to copy', color: 'warning' })
    return
  }
  const text = mobiles.join('\n')
  try {
    await navigator.clipboard.writeText(text)
    toast.add({ title: 'Mobile list copied', description: `${mobiles.length} mobile number(s) copied.`, color: 'success' })
  } catch {
    window.prompt('Copy customer mobile list', text)
  }
}

function csvCell(value: unknown) {
  const text = String(value ?? '').replace(/\r?\n/g, ' ').trim()
  return `"${text.replace(/"/g, '""')}"`
}

function exportCsv() {
  const header = ['Customer Name', 'Mobile', 'Store', 'Last Invoice', 'Last Invoice Date', 'Invoice Count', 'Total Amount', 'Opens', 'PDF Downloads', 'Review Clicks', 'Feedback Count', 'Banner Clicks', 'WhatsApp Sent', 'WhatsApp Failed', 'Last WhatsApp Status', 'Recommendation', 'Public Bill Path']
  const lines = [header.map(csvCell).join(',')]
  for (const row of rows.value) {
    lines.push([
      readText(row, ['customerName']),
      readText(row, ['customerMobile']),
      readText(row, ['storeName']),
      readText(row, ['lastInvoiceNumber']),
      formatDate(readText(row, ['lastInvoiceDate'], '')),
      readNumber(row, ['invoiceCount']),
      readNumber(row, ['totalAmount']),
      readNumber(row, ['openCount']),
      readNumber(row, ['pdfDownloadCount']),
      readNumber(row, ['reviewClickCount']),
      readNumber(row, ['feedbackCount']),
      readNumber(row, ['bannerClickCount']),
      readNumber(row, ['whatsAppSentCount']),
      readNumber(row, ['whatsAppFailedCount']),
      readText(row, ['lastWhatsAppStatus']),
      readText(row, ['recommendation']),
      readText(row, ['lastPublicPath'])
    ].map(csvCell).join(','))
  }
  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `digital-bill-audience-${filters.segment}-${filters.fromDate}-to-${filters.toDate}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

function openPublicBill(row: ApiRecord) {
  const path = readText(row, ['lastPublicPath'], '')
  if (path) window.open(new URL(path, window.location.origin).toString(), '_blank', 'noopener,noreferrer')
}

function openActivity(row: ApiRecord) {
  const query = readText(row, ['lastInvoiceNumber'], '')
  router.push({ path: '/marketing/digital-bills', query: query ? { q: query } : undefined })
}

function openCampaignCreate() {
  router.push({
    path: '/marketing/campaigns',
    query: {
      segment: filters.segment,
      fromDate: filters.fromDate,
      toDate: filters.toDate,
      storeId: filters.storeId === ALL_STORES_VALUE ? '' : filters.storeId,
      searchText: search.value
    }
  })
}

onMounted(refresh)
useHead({ title: 'Campaign Audiences - Garmetix CRM' })
</script>

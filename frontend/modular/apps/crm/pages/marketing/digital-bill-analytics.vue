<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-chart-no-axes-combined" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Digital Bill Analytics</h2>
          <p class="garmetix-dashboard-subtitle">Measure bill opens, PDF downloads, reviews, feedback, WhatsApp delivery and banner engagement.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-receipt-text" @click="router.push('/marketing/digital-bills')">Digital Bills</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-users-round" @click="router.push('/marketing/campaign-audiences')">Audiences</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />

    <div class="garmetix-section-card grid gap-3 md:grid-cols-[160px_160px_minmax(220px,1fr)_auto]">
      <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
      <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
      <UFormField label="Store"><USelect v-model="filters.storeId" :items="storeOptions" /></UFormField>
      <div class="flex items-end"><UButton icon="i-lucide-search" :loading="loading" @click="refresh">Apply</UButton></div>
    </div>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label"><UIcon :name="metric.icon" class="mr-1 inline size-4" /> {{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.value }}</p>
        <p class="garmetix-metric-caption">{{ metric.detail }}</p>
      </div>
    </section>

    <div class="garmetix-section-card">
      <div class="mb-4 flex items-start justify-between gap-3">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-days" class="size-4" /> Daily trend</p>
          <h3 class="text-xl font-semibold text-highlighted">Engagement By Day</h3>
          <p class="text-sm text-muted">{{ analytics ? `${formatDate(readText(analytics, ['fromDate'], ''))} to ${formatDate(readText(analytics, ['toDate'], ''))}` : 'No analytics loaded' }}</p>
        </div>
        <UBadge color="neutral" variant="soft">{{ dailyRows.length }} day row(s)</UBadge>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full min-w-[1120px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr>
              <th class="border-b border-default p-3">Date</th>
              <th class="border-b border-default p-3 text-right">Bills</th>
              <th class="border-b border-default p-3 text-right">Opens</th>
              <th class="border-b border-default p-3 text-right">PDF</th>
              <th class="border-b border-default p-3 text-right">Reviews</th>
              <th class="border-b border-default p-3 text-right">Feedback</th>
              <th class="border-b border-default p-3 text-right">Banner</th>
              <th class="border-b border-default p-3 text-right">WA Sent</th>
              <th class="border-b border-default p-3 text-right">WA Delivered</th>
              <th class="border-b border-default p-3 text-right">WA Read</th>
              <th class="border-b border-default p-3 text-right">WA Failed</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="11" class="p-8 text-center text-muted">Loading analytics...</td></tr>
            <tr v-else-if="!dailyRows.length"><td colspan="11" class="p-8 text-center text-muted">No daily activity for the selected period.</td></tr>
            <template v-else>
              <tr v-for="row in dailyRows" :key="readText(row, ['date'])">
                <td class="border-b border-default p-3">{{ formatDate(readText(row, ['date'], '')) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['bills'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['opens'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['pdfDownloads'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['reviewClicks'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['feedbacks'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['bannerClicks'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['whatsAppSent'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['whatsAppDelivered'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['whatsAppRead'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['whatsAppFailed'])) }}</td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readNumber, readRecord, readText, toRows, type ApiRecord, useCrmApiClient, formatDate } from '../../utils/crm-api'
import { stripServerUrl } from '@garmetix/shared-utils'

const router = useRouter()
const toast = useToast()
const { get } = useCrmApiClient()
const ALL_STORES_VALUE = '__ALL_STORES__'
const today = new Date()
const start = new Date(Date.now() - 29 * 24 * 60 * 60 * 1000)

const filters = reactive({
  fromDate: toDateInput(start),
  toDate: toDateInput(today),
  storeId: ALL_STORES_VALUE
})
const stores = ref<ApiRecord[]>([])
const analytics = ref<ApiRecord | null>(null)
const loading = ref(false)
const loadError = ref('')

const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map(item => ({ label: `${readText(item, ['name', 'storeName', 'storeCode'], 'Store')} (${readText(item, ['storeCode'], 'store')})`, value: readText(item, ['id'], '') }))
])

const dailyRows = computed(() => readArray(analytics.value, ['daily']))
const metrics = computed(() => [
  { label: 'Digital Bills', value: number(readNumber(analytics.value, ['totalDigitalBills'])), detail: `${number(readNumber(analytics.value, ['activeDigitalBills']))} active`, icon: 'i-lucide-receipt-text' },
  { label: 'Opened', value: number(readNumber(analytics.value, ['openCount'])), detail: 'Customer page views', icon: 'i-lucide-eye' },
  { label: 'PDF Downloads', value: number(readNumber(analytics.value, ['pdfDownloadCount'])), detail: 'Customer downloads', icon: 'i-lucide-file-down' },
  { label: 'Review Clicks', value: number(readNumber(analytics.value, ['reviewClickCount'])), detail: 'Google review button', icon: 'i-lucide-star' },
  { label: 'Feedback', value: number(readNumber(analytics.value, ['feedbackCount'])), detail: 'Private responses', icon: 'i-lucide-message-square-text' },
  { label: 'Banner Clicks', value: number(readNumber(analytics.value, ['bannerClickCount'])), detail: 'Ad engagement', icon: 'i-lucide-mouse-pointer-click' },
  { label: 'WhatsApp Sent', value: number(readNumber(analytics.value, ['whatsAppSentCount'])), detail: `${number(readNumber(analytics.value, ['whatsAppDeliveredCount']))} delivered`, icon: 'i-lucide-send' },
  { label: 'WhatsApp Failed', value: number(readNumber(analytics.value, ['whatsAppFailedCount'])), detail: `${number(readNumber(analytics.value, ['whatsAppReadCount']))} read`, icon: 'i-lucide-triangle-alert' }
])

function toDateInput(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const [storeData, analyticsData] = await Promise.all([
      get<unknown>('stores').catch(() => []),
      get<unknown>('digital-bill-analytics', {
        fromDate: filters.fromDate,
        toDate: filters.toDate,
        storeId: filters.storeId !== ALL_STORES_VALUE ? filters.storeId : undefined
      })
    ])
    stores.value = toRows(storeData)
    analytics.value = readRecord(analyticsData)
  } catch (caught) {
    loadError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Digital bill analytics load failed.')
    toast.add({ title: 'Analytics load failed', description: loadError.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
useHead({ title: 'Digital Bill Analytics - Garmetix CRM' })
</script>

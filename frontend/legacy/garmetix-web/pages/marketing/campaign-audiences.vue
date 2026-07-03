<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const stores = ref<any[]>([])
const audience = ref<any | null>(null)
const loading = ref(false)
const loadError = ref('')
const search = ref('')
const page = ref(1)
const pageSize = ref(50)
const ALL_STORES_VALUE = '__ALL_STORES__'

const filters = reactive({
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE,
  segment: 'all'
})

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

const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id }))
])

const rows = computed(() => audience.value?.items || [])
const summaries = computed(() => audience.value?.summaries || [])
const selectedSummary = computed(() => summaries.value.find((item: any) => item.segment === filters.segment))
const total = computed(() => Number(audience.value?.total || 0))
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const campaignReadyCount = computed(() => rows.value.filter((row: any) => row.customerMobile && row.whatsAppFailedCount === 0 && !(row.worstFeedbackRating && row.worstFeedbackRating <= 3)).length)

async function refresh(resetPage = false) {
  if (resetPage) page.value = 1
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({
      fromDate: filters.fromDate,
      toDate: filters.toDate,
      segment: filters.segment,
      page: String(page.value),
      pageSize: String(pageSize.value)
    })
    if (filters.storeId && filters.storeId !== ALL_STORES_VALUE) query.set('storeId', filters.storeId)
    if (search.value.trim()) query.set('q', search.value.trim())
    const [storeRows, response] = await Promise.all([
      api.list<any>('stores'),
      api.get<any>(`digital-bill-audiences?${query}`)
    ])
    stores.value = storeRows || []
    audience.value = response
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check the API service and Digital Bill CRM database migration.', 'Campaign audience load failed')
  } finally {
    loading.value = false
  }
}

function applySummary(segment: string) {
  filters.segment = segment
  refresh(true)
}

async function copyMobiles() {
  const mobiles = rows.value
    .map((row: any) => String(row.customerMobile || '').trim())
    .filter(Boolean)
  if (mobiles.length === 0) {
    feedback.notify('No mobile numbers to copy', 'Selected audience rows do not have customer mobile numbers.', 'warning')
    return
  }
  const text = [...new Set(mobiles)].join('\n')
  try {
    await navigator.clipboard?.writeText(text)
    feedback.notify('Mobile list copied', `${mobiles.length} customer mobile numbers copied.`, 'success')
  } catch {
    window.prompt('Copy customer mobile list', text)
  }
}

function csvCell(value: unknown) {
  const text = String(value ?? '').replace(/\r?\n/g, ' ').trim()
  return `"${text.replace(/"/g, '""')}"`
}

function exportCsv() {
  const header = [
    'Customer Name', 'Mobile', 'Store', 'Last Invoice', 'Last Invoice Date', 'Invoice Count', 'Total Amount', 'Opens', 'PDF Downloads', 'Review Clicks', 'Feedback Count', 'Banner Clicks', 'WhatsApp Sent', 'WhatsApp Failed', 'Last WhatsApp Status', 'Last Feedback Rating', 'Worst Feedback Rating', 'Recommendation', 'Public Bill Path'
  ]
  const lines = [header.map(csvCell).join(',')]
  for (const row of rows.value) {
    lines.push([
      row.customerName,
      row.customerMobile,
      row.storeName,
      row.lastInvoiceNumber,
      date(row.lastInvoiceDate),
      row.invoiceCount,
      row.totalAmount,
      row.openCount,
      row.pdfDownloadCount,
      row.reviewClickCount,
      row.feedbackCount,
      row.bannerClickCount,
      row.whatsAppSentCount,
      row.whatsAppFailedCount,
      row.lastWhatsAppStatus,
      row.lastFeedbackRating ?? '',
      row.worstFeedbackRating ?? '',
      row.recommendation,
      row.lastPublicPath
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

function openActivity(row: any) {
  if (!row?.lastDigitalBillId) return
  navigateTo({ path: '/marketing/digital-bills', query: { activityId: row.lastDigitalBillId } })
}

function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function dateTime(value: string) { return value ? new Date(value).toLocaleString('en-IN') : '-' }
function ratingLabel(value: number | null | undefined) { return value ? `${value}/5` : '-' }
function statusColor(status: string) {
  if (['Sent', 'Delivered', 'Read'].includes(status)) return 'success'
  if (['Failed', 'Skipped', 'RetryLimitReached'].includes(status)) return 'error'
  if (['Queued', 'ManualPending', 'NotConfigured', 'NotSent'].includes(status)) return 'warning'
  return 'neutral'
}

onMounted(() => refresh())
</script>

<template>
  <AppShell title="Campaign Audiences" @refresh="() => refresh()">
    <UiModulePageHeader
      title="Campaign Audiences"
      description="Segment Digital Bill customers by opens, review clicks, feedback rating, WhatsApp delivery, and campaign readiness."
      icon="i-lucide-users-round"
    >
      <template #actions>
        <div class="inline-action-row">
          <UButton icon="i-lucide-copy" label="Copy Mobiles" variant="subtle" :disabled="rows.length === 0" @click="copyMobiles" />
          <UButton icon="i-lucide-file-down" label="Export CSV" variant="subtle" :disabled="rows.length === 0" @click="exportCsv" />
          <UButton icon="i-lucide-megaphone" label="Create Campaign" color="primary" :to="{ path: '/marketing/campaigns', query: { segment: filters.segment, fromDate: filters.fromDate, toDate: filters.toDate, storeId: filters.storeId === ALL_STORES_VALUE ? '' : filters.storeId, searchText: search } }" />
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh()" />
        </div>
      </template>
    </UiModulePageHeader>

    <UAlert
      v-if="audience?.isTruncated"
      class="mt-4"
      color="warning"
      variant="soft"
      icon="i-lucide-triangle-alert"
      title="Large audience range truncated"
      description="The API grouped the latest 5,000 digital bill rows for this period. Narrow the date/store filter for complete segmentation."
    />

    <UCard class="mt-4">
      <div class="grid gap-3 xl:grid-cols-[150px_150px_220px_220px_1fr_auto]">
        <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
        <UFormField label="Store"><USelect v-model="filters.storeId" :items="storeOptions" /></UFormField>
        <UFormField label="Segment"><USelect v-model="filters.segment" :items="segmentOptions" /></UFormField>
        <UFormField label="Search"><UInput v-model="search" placeholder="Customer, mobile, invoice" @keyup.enter="refresh(true)" /></UFormField>
        <div class="flex items-end"><UButton icon="i-lucide-search" label="Apply" :loading="loading" @click="refresh(true)" /></div>
      </div>
    </UCard>

    <div class="planner-metric-grid mt-4">
      <UCard v-for="item in summaries" :key="item.segment" class="planner-metric-card cursor-pointer" :class="{ 'ring-2 ring-primary': item.segment === filters.segment }" @click="applySummary(item.segment)">
        <div class="planner-metric-body">
          <UAvatar :icon="item.segment === 'all' ? 'i-lucide-users-round' : 'i-lucide-filter'" :color="item.segment === filters.segment ? 'primary' : 'neutral'" variant="subtle" />
          <div><p>{{ item.label }}</p><strong>{{ item.count }}</strong><span>{{ item.hint }}</span></div>
        </div>
      </UCard>
    </div>

    <UiRegisterPanel
      class="mt-4"
      title="Audience list"
      :description="selectedSummary ? `${selectedSummary.label} · ${total} customers · ${campaignReadyCount} ready on this page` : `${total} customers`"
      :loading="loading"
      :error="loadError"
      :empty="rows.length === 0"
      empty-title="No audience rows"
      empty-description="Generate digital bills or widen the filters to build campaign audiences."
      empty-icon="i-lucide-users-round"
      @retry="refresh"
    >
      <template #actions>
        <div class="inline-action-row">
          <UButton size="xs" icon="i-lucide-chevron-left" label="Prev" variant="subtle" :disabled="page <= 1" @click="page--; refresh()" />
          <UBadge color="neutral" variant="subtle">Page {{ page }} / {{ totalPages }}</UBadge>
          <UButton size="xs" icon="i-lucide-chevron-right" label="Next" variant="subtle" :disabled="page >= totalPages" @click="page++; refresh()" />
        </div>
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead>
            <tr>
              <th>Customer</th>
              <th>Last invoice</th>
              <th class="text-right">Value</th>
              <th class="text-right">Engagement</th>
              <th>WhatsApp</th>
              <th>Feedback</th>
              <th>Recommendation</th>
              <th class="text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in rows" :key="row.audienceKey">
              <td>
                <div class="font-medium">{{ row.customerName || 'Walk-in Customer' }}</div>
                <div class="text-xs text-muted">{{ row.customerMobile || 'No mobile' }} · {{ row.storeName }}</div>
              </td>
              <td>
                <div class="font-medium">{{ row.lastInvoiceNumber }}</div>
                <div class="text-xs text-muted">{{ date(row.lastInvoiceDate) }} · {{ row.invoiceCount }} bill(s)</div>
              </td>
              <td class="text-right">{{ money(row.totalAmount) }}</td>
              <td class="text-right text-xs">
                Open {{ row.openCount }} · PDF {{ row.pdfDownloadCount }}<br>
                Review {{ row.reviewClickCount }} · Banner {{ row.bannerClickCount }}
              </td>
              <td>
                <UBadge :color="statusColor(row.lastWhatsAppStatus)" variant="subtle">{{ row.lastWhatsAppStatus || 'NotSent' }}</UBadge>
                <div class="text-xs text-muted">Sent {{ row.whatsAppSentCount }} · Failed {{ row.whatsAppFailedCount }}</div>
              </td>
              <td>
                <div class="text-sm">Last {{ ratingLabel(row.lastFeedbackRating) }}</div>
                <div class="text-xs text-muted">Worst {{ ratingLabel(row.worstFeedbackRating) }} · Count {{ row.feedbackCount }}</div>
              </td>
              <td>
                <div class="max-w-sm text-sm">{{ row.recommendation }}</div>
                <div class="text-xs text-muted">Last activity: {{ dateTime(row.lastActivityAt) }}</div>
              </td>
              <td>
                <div class="inline-action-row justify-end">
                  <UButton size="xs" label="Bill" icon="i-lucide-external-link" :to="row.lastPublicPath" target="_blank" />
                  <UButton size="xs" label="Activity" icon="i-lucide-activity" color="info" variant="subtle" @click="openActivity(row)" />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>

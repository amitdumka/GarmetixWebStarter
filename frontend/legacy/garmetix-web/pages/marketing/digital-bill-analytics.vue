<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const stores = ref<any[]>([])
const analytics = ref<any | null>(null)
const loading = ref(false)
const loadError = ref('')
const ALL_STORES_VALUE = '__ALL_STORES__'

const filters = reactive({
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE
})

const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id }))
])
const metrics = computed(() => {
  const row = analytics.value || {}
  return [
    { label: 'Digital bills', value: row.totalDigitalBills || 0, hint: `${row.activeDigitalBills || 0} active`, icon: 'i-lucide-receipt-text', color: 'primary' },
    { label: 'Opened', value: row.openCount || 0, hint: 'Customer page views', icon: 'i-lucide-eye', color: 'success' },
    { label: 'PDF downloads', value: row.pdfDownloadCount || 0, hint: 'Customer downloads', icon: 'i-lucide-file-down', color: 'info' },
    { label: 'Review clicks', value: row.reviewClickCount || 0, hint: 'Google review button', icon: 'i-lucide-star', color: 'warning' },
    { label: 'Feedback', value: row.feedbackCount || 0, hint: 'Private responses', icon: 'i-lucide-message-square-text', color: 'neutral' },
    { label: 'Banner clicks', value: row.bannerClickCount || 0, hint: 'Ad engagement', icon: 'i-lucide-mouse-pointer-click', color: 'warning' },
    { label: 'WhatsApp sent', value: row.whatsAppSentCount || 0, hint: `${row.whatsAppDeliveredCount || 0} delivered`, icon: 'i-lucide-send', color: 'success' },
    { label: 'WhatsApp failed', value: row.whatsAppFailedCount || 0, hint: `${row.whatsAppReadCount || 0} read`, icon: 'i-lucide-triangle-alert', color: 'error' }
  ]
})

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({ fromDate: filters.fromDate, toDate: filters.toDate })
    if (filters.storeId && filters.storeId !== ALL_STORES_VALUE) query.set('storeId', filters.storeId)
    const [storeRows, analyticsRow] = await Promise.all([
      api.list<any>('stores'),
      api.get<any>(`digital-bill-analytics?${query}`)
    ])
    stores.value = storeRows || []
    analytics.value = analyticsRow
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'Digital bill analytics load failed')
  } finally {
    loading.value = false
  }
}

function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }

onMounted(refresh)
</script>

<template>
  <AppShell title="Digital Bill Analytics" @refresh="refresh">
    <UiModulePageHeader title="Digital Bill Analytics" description="Measure digital bill opens, PDF downloads, review clicks, feedback, WhatsApp delivery, and ad banner performance." icon="i-lucide-chart-no-axes-combined" />

    <UCard class="mt-4">
      <div class="grid gap-3 md:grid-cols-[160px_160px_220px_auto]">
        <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
        <UFormField label="Store"><USelect v-model="filters.storeId" :items="storeOptions" /></UFormField>
        <div class="flex items-end"><UButton icon="i-lucide-search" :loading="loading" label="Apply" @click="refresh" /></div>
      </div>
    </UCard>

    <UiRegisterPanel class="mt-4" title="Digital bill performance" :description="analytics ? `${date(analytics.fromDate)} → ${date(analytics.toDate)}` : 'No analytics loaded'" :loading="loading" :error="loadError" :empty="!analytics" empty-title="No analytics" empty-description="Generate and open digital bills to populate this dashboard." empty-icon="i-lucide-chart-no-axes-combined" @retry="refresh">
      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div><p>{{ metric.label }}</p><strong>{{ metric.value }}</strong><span>{{ metric.hint }}</span></div>
          </div>
        </UCard>
      </div>
    </UiRegisterPanel>

    <UiRegisterPanel v-if="analytics" class="mt-4" title="Daily trend" :description="`${analytics.daily?.length || 0} day rows`" :loading="loading" :error="loadError" :empty="(analytics.daily || []).length === 0" empty-title="No daily rows" empty-description="No activity for the selected period." empty-icon="i-lucide-calendar-days" @retry="refresh">
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Date</th><th class="text-right">Bills</th><th class="text-right">Opens</th><th class="text-right">PDF</th><th class="text-right">Reviews</th><th class="text-right">Feedback</th><th class="text-right">Banner</th><th class="text-right">WA Sent</th><th class="text-right">WA Delivered</th><th class="text-right">WA Read</th><th class="text-right">WA Failed</th></tr></thead>
          <tbody>
            <tr v-for="row in analytics.daily" :key="row.date">
              <td>{{ date(row.date) }}</td>
              <td class="text-right">{{ row.bills }}</td>
              <td class="text-right">{{ row.opens }}</td>
              <td class="text-right">{{ row.pdfDownloads }}</td>
              <td class="text-right">{{ row.reviewClicks }}</td>
              <td class="text-right">{{ row.feedbacks }}</td>
              <td class="text-right">{{ row.bannerClicks }}</td>
              <td class="text-right">{{ row.whatsAppSent }}</td>
              <td class="text-right">{{ row.whatsAppDelivered }}</td>
              <td class="text-right">{{ row.whatsAppRead }}</td>
              <td class="text-right">{{ row.whatsAppFailed }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>

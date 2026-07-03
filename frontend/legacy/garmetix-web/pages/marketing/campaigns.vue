<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const route = useRoute()
const stores = ref<any[]>([])
const campaigns = ref<any | null>(null)
const selected = ref<any | null>(null)
const preview = ref<any | null>(null)
const selectedRoi = ref<any | null>(null)
const lastSendResult = ref<any | null>(null)
const roiLoading = ref(false)
const roiDays = ref(30)
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const ALL_STORES_VALUE = '__ALL_STORES__'
const ALL_STATUS_VALUE = '__ALL_STATUS__'

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

const statusOptions = [
  { label: 'All statuses', value: ALL_STATUS_VALUE },
  { label: 'Draft', value: 'Draft' },
  { label: 'Queued', value: 'Queued' },
  { label: 'Completed', value: 'Completed' },
  { label: 'Cancelled', value: 'Cancelled' }
]

const channelOptions = [
  { label: 'WhatsApp manual/copy', value: 'WhatsAppManual' },
  { label: 'SMS manual/copy', value: 'SmsManual' },
  { label: 'Approved WhatsApp marketing template', value: 'WhatsAppMarketingTemplate' }
]

const filter = reactive({
  status: ALL_STATUS_VALUE,
  q: '',
  page: 1,
  pageSize: 50
})

const form = reactive({
  name: '',
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE,
  segment: 'reviewPending',
  searchText: '',
  channel: 'WhatsAppManual',
  templateName: '',
  messageTitle: 'Review reminder',
  messageBody: 'Hello {{customerName}}, thank you for shopping with us. Your bill {{invoiceNumber}} is available here: {{publicUrl}}. Please share your honest feedback/review.',
  offerUrl: '',
  notes: '',
  queueNow: false,
  limit: 1000
})

const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id }))
])
const rows = computed(() => campaigns.value?.items || [])
const total = computed(() => Number(campaigns.value?.total || 0))
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(filter.pageSize || 50))))
const selectedRecipients = computed(() => selected.value?.recipients || [])
const selectedCampaign = computed(() => selected.value?.campaign || null)

async function refresh(resetPage = false) {
  if (resetPage) filter.page = 1
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({
      page: String(filter.page),
      pageSize: String(filter.pageSize)
    })
    if (filter.status && filter.status !== ALL_STATUS_VALUE) query.set('status', filter.status)
    if (filter.q.trim()) query.set('q', filter.q.trim())
    const [storeRows, campaignRows] = await Promise.all([
      api.list<any>('stores'),
      api.get<any>(`digital-bill-campaigns?${query}`)
    ])
    stores.value = storeRows || []
    campaigns.value = campaignRows
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check Digital Bill Campaign database migration/schema repair.', 'Campaigns load failed')
  } finally {
    loading.value = false
  }
}

function hydrateFromQuery() {
  const q = route.query
  if (typeof q.segment === 'string') form.segment = q.segment
  if (typeof q.fromDate === 'string') form.fromDate = q.fromDate
  if (typeof q.toDate === 'string') form.toDate = q.toDate
  if (typeof q.storeId === 'string' && q.storeId) form.storeId = q.storeId
  if (typeof q.searchText === 'string') form.searchText = q.searchText
}

function requestBody(extra: Record<string, unknown> = {}) {
  return {
    name: form.name,
    fromDate: form.fromDate,
    toDate: form.toDate,
    storeId: form.storeId === ALL_STORES_VALUE ? null : form.storeId,
    segment: form.segment,
    searchText: form.searchText || null,
    channel: form.channel,
    templateName: form.templateName || null,
    messageTitle: form.messageTitle,
    messageBody: form.messageBody,
    offerUrl: form.offerUrl || null,
    notes: form.notes || null,
    queueNow: form.queueNow,
    limit: Number(form.limit || 1000),
    ...extra
  }
}

async function previewCampaign() {
  saving.value = true
  try {
    preview.value = await api.create<any>('digital-bill-campaigns/preview', requestBody())
    feedback.notify('Preview ready', `${preview.value.recipientsWithMobile} recipients with mobile numbers found.`, 'success')
  } catch (error) {
    feedback.notify('Preview failed', feedback.errorMessage(error, 'Could not preview this campaign.'), 'error')
  } finally {
    saving.value = false
  }
}

async function createCampaign() {
  saving.value = true
  try {
    const detail = await api.create<any>('digital-bill-campaigns', requestBody())
    selected.value = detail
    preview.value = null
    feedback.notify('Campaign created', `${detail.campaign.recipientCount} recipients prepared.`, 'success')
    await refresh(true)
  } catch (error) {
    feedback.notify('Campaign create failed', feedback.errorMessage(error, 'Could not create campaign.'), 'error')
  } finally {
    saving.value = false
  }
}

async function openCampaign(row: any) {
  try {
    selected.value = await api.get<any>(`digital-bill-campaigns/${row.id}`)
    lastSendResult.value = null
    await loadRoi(row.id)
  } catch (error) {
    feedback.notify('Open campaign failed', feedback.errorMessage(error, 'Could not open campaign.'), 'error')
  }
}

async function loadRoi(id?: string) {
  const campaignId = id || selectedCampaign.value?.id
  if (!campaignId) return
  roiLoading.value = true
  try {
    selectedRoi.value = await api.get<any>(`digital-bill-campaigns/${campaignId}/roi?days=${Number(roiDays.value || 30)}`)
  } catch (error) {
    selectedRoi.value = null
    feedback.notify('ROI load failed', feedback.errorMessage(error, 'Could not calculate campaign ROI.'), 'error')
  } finally {
    roiLoading.value = false
  }
}


async function sendViaWhatsAppTemplate(row?: any) {
  const campaign = row || selectedCampaign.value
  if (!campaign?.id) return
  if (!campaign.templateName) {
    feedback.notify('Template required', 'Set an approved Meta WhatsApp marketing template name before API sending. Manual copy/export is still available.', 'warning')
    return
  }
  saving.value = true
  try {
    lastSendResult.value = await api.create<any>(`digital-bill-campaigns/${campaign.id}/send-whatsapp`, { force: false, limit: 500 })
    selected.value = await api.get<any>(`digital-bill-campaigns/${campaign.id}`)
    await loadRoi(campaign.id)
    await refresh()
    feedback.notify('WhatsApp send batch completed', `Sent ${lastSendResult.value.sent}, manual ${lastSendResult.value.manualPending}, failed ${lastSendResult.value.failed}, skipped ${lastSendResult.value.skipped}.`, lastSendResult.value.failed ? 'warning' : 'success')
  } catch (error) {
    feedback.notify('WhatsApp campaign send failed', feedback.errorMessage(error, 'Could not send campaign with WhatsApp template.'), 'error')
  } finally {
    saving.value = false
  }
}

async function updateStatus(row: any, action: 'queue' | 'mark-sent' | 'cancel') {
  try {
    selected.value = await api.create<any>(`digital-bill-campaigns/${row.id}/${action}`, {})
    await loadRoi(row.id)
    await refresh()
    feedback.notify('Campaign updated', `Campaign status changed to ${selected.value.campaign.status}.`, 'success')
  } catch (error) {
    feedback.notify('Campaign update failed', feedback.errorMessage(error, 'Could not update campaign.'), 'error')
  }
}

async function copyMessages() {
  const text = selectedRecipients.value
    .filter((item: any) => item.customerMobile)
    .map((item: any) => `${item.customerMobile}\t${item.customerName}\t${item.messageBody}`)
    .join('\n')
  if (!text) {
    feedback.notify('No messages to copy', 'This campaign has no recipients with mobile numbers.', 'warning')
    return
  }
  try {
    await navigator.clipboard?.writeText(text)
    feedback.notify('Campaign messages copied', `${selectedRecipients.value.length} recipient rows copied.`, 'success')
  } catch {
    window.prompt('Copy campaign messages', text)
  }
}

function csvCell(value: unknown) {
  const text = String(value ?? '').replace(/\r?\n/g, ' ').trim()
  return `"${text.replace(/"/g, '""')}"`
}

function exportRecipients() {
  const header = ['Mobile', 'Customer', 'Store', 'Invoice', 'Bill Link', 'Status', 'Message']
  const lines = [header.map(csvCell).join(',')]
  for (const row of selectedRecipients.value) {
    lines.push([row.customerMobile, row.customerName, row.storeName, row.invoiceNumber, row.publicPath, row.status, row.messageBody].map(csvCell).join(','))
  }
  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `digital-bill-campaign-${selectedCampaign.value?.name || 'recipients'}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

function roiForRecipient(id: string) {
  return selectedRoi.value?.recipients?.find((item: any) => item.recipientId === id) || null
}

function statusColor(status: string) {
  if (status === 'Completed' || status === 'Sent') return 'success'
  if (status === 'Queued' || status === 'ManualPending') return 'info'
  if (status === 'Cancelled' || status === 'Failed') return 'error'
  return 'warning'
}
function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function percent(value: number) { return `${Number(value || 0).toFixed(2)}%` }
function date(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function dateTime(value: string) { return value ? new Date(value).toLocaleString('en-IN') : '-' }

onMounted(() => {
  hydrateFromQuery()
  refresh()
})
</script>

<template>
  <AppShell title="Digital Bill Campaigns" @refresh="() => refresh()">
    <UiModulePageHeader
      title="Digital Bill Campaigns"
      description="Create campaign drafts from Digital Bill audience segments, prepare WhatsApp/SMS message lists, and track campaign status."
      icon="i-lucide-megaphone"
    >
      <template #actions>
        <div class="inline-action-row">
          <UButton icon="i-lucide-users-round" label="Audiences" variant="subtle" to="/marketing/campaign-audiences" />
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh()" />
        </div>
      </template>
    </UiModulePageHeader>

    <UAlert
      class="mt-4"
      color="info"
      variant="soft"
      icon="i-lucide-info"
      title="Safe campaign mode"
      description="This stage prepares campaign recipients and manual/queued message logs. Use approved WhatsApp marketing templates before enabling provider-based bulk sending."
    />

    <div class="grid gap-4 xl:grid-cols-[420px_1fr] mt-4">
      <UCard>
        <template #header><div class="font-semibold">Create campaign</div></template>
        <div class="space-y-3">
          <UFormField label="Campaign name"><UInput v-model="form.name" placeholder="June review reminder" /></UFormField>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="From"><UInput v-model="form.fromDate" type="date" /></UFormField>
            <UFormField label="To"><UInput v-model="form.toDate" type="date" /></UFormField>
          </div>
          <UFormField label="Store"><USelect v-model="form.storeId" :items="storeOptions" /></UFormField>
          <UFormField label="Segment"><USelect v-model="form.segment" :items="segmentOptions" /></UFormField>
          <UFormField label="Search filter"><UInput v-model="form.searchText" placeholder="Optional customer/mobile/invoice filter" /></UFormField>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Channel"><USelect v-model="form.channel" :items="channelOptions" /></UFormField>
            <UFormField label="Limit"><UInput v-model.number="form.limit" type="number" min="1" max="5000" /></UFormField>
          </div>
          <UFormField label="Template name"><UInput v-model="form.templateName" placeholder="Optional approved template name" /></UFormField>
          <UFormField label="Message title"><UInput v-model="form.messageTitle" /></UFormField>
          <UFormField label="Message body">
            <UTextarea v-model="form.messageBody" :rows="6" />
            <p class="mt-1 text-xs text-muted">Variables: <code>&#123;&#123;customerName&#125;&#125;</code>, <code>&#123;&#123;storeName&#125;&#125;</code>, <code>&#123;&#123;invoiceNumber&#125;&#125;</code>, <code>&#123;&#123;publicUrl&#125;&#125;</code>, <code>&#123;&#123;amount&#125;&#125;</code>, <code>&#123;&#123;offerUrl&#125;&#125;</code></p>
          </UFormField>
          <UFormField label="Offer URL"><UInput v-model="form.offerUrl" placeholder="Optional offer/banner link" /></UFormField>
          <UFormField label="Notes"><UTextarea v-model="form.notes" :rows="2" /></UFormField>
          <UCheckbox v-model="form.queueNow" label="Queue as manual pending immediately" />
          <div class="inline-action-row justify-end">
            <UButton icon="i-lucide-eye" label="Preview" color="neutral" variant="subtle" :loading="saving" @click="previewCampaign" />
            <UButton icon="i-lucide-plus" label="Create Campaign" :loading="saving" @click="createCampaign" />
          </div>
        </div>
      </UCard>

      <div class="space-y-4">
        <UCard v-if="preview">
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div class="font-semibold">Preview</div>
              <UBadge color="info" variant="subtle">{{ preview.recipientsWithMobile }} ready / {{ preview.totalCandidates }} candidates</UBadge>
            </div>
          </template>
          <div class="grid gap-3 md:grid-cols-4">
            <div><p class="text-xs text-muted">Segment</p><strong>{{ preview.segment }}</strong></div>
            <div><p class="text-xs text-muted">With mobile</p><strong>{{ preview.recipientsWithMobile }}</strong></div>
            <div><p class="text-xs text-muted">Skipped no mobile</p><strong>{{ preview.skippedNoMobile }}</strong></div>
            <div><p class="text-xs text-muted">Limit</p><strong>{{ preview.limitApplied }}</strong></div>
          </div>
          <div class="planner-table-wrap mt-3">
            <table class="planner-table">
              <thead><tr><th>Customer</th><th>Invoice</th><th>Message</th></tr></thead>
              <tbody>
                <tr v-for="row in preview.sampleRecipients" :key="`${row.digitalInvoiceId}-${row.customerMobile}`">
                  <td>{{ row.customerName }}<div class="text-xs text-muted">{{ row.customerMobile }}</div></td>
                  <td>{{ row.invoiceNumber }}<div class="text-xs text-muted">{{ row.publicPath }}</div></td>
                  <td class="text-sm">{{ row.messageBody }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>

        <UiRegisterPanel
          title="Campaigns"
          :description="`${total} campaign(s)`"
          :loading="loading"
          :error="loadError"
          :empty="rows.length === 0"
          empty-title="No campaigns"
          empty-description="Create your first campaign from a Digital Bill audience segment."
          empty-icon="i-lucide-megaphone"
          @retry="refresh"
        >
          <template #actions>
            <div class="inline-action-row">
              <USelect v-model="filter.status" :items="statusOptions" class="w-40" @update:model-value="refresh(true)" />
              <UInput v-model="filter.q" placeholder="Search campaign" class="w-48" @keyup.enter="refresh(true)" />
              <UButton size="xs" icon="i-lucide-chevron-left" label="Prev" variant="subtle" :disabled="filter.page <= 1" @click="filter.page--; refresh()" />
              <UBadge color="neutral" variant="subtle">{{ filter.page }} / {{ totalPages }}</UBadge>
              <UButton size="xs" icon="i-lucide-chevron-right" label="Next" variant="subtle" :disabled="filter.page >= totalPages" @click="filter.page++; refresh()" />
            </div>
          </template>
          <div class="planner-table-wrap">
            <table class="planner-table">
              <thead>
                <tr><th>Campaign</th><th>Segment</th><th>Recipients</th><th>Status</th><th>Dates</th><th class="text-right">Actions</th></tr>
              </thead>
              <tbody>
                <tr v-for="row in rows" :key="row.id">
                  <td><div class="font-medium">{{ row.name }}</div><div class="text-xs text-muted">{{ row.messageTitle }}</div></td>
                  <td><UBadge color="neutral" variant="subtle">{{ row.segment }}</UBadge><div class="text-xs text-muted">{{ row.storeName || 'All stores' }}</div></td>
                  <td><div class="font-medium">{{ row.recipientCount }}</div><div class="text-xs text-muted">Sent {{ row.sentCount }} · Failed {{ row.failedCount }}</div></td>
                  <td><UBadge :color="statusColor(row.status)" variant="subtle">{{ row.status }}</UBadge></td>
                  <td><div class="text-sm">{{ date(row.fromDate) }} → {{ date(row.toDate) }}</div><div class="text-xs text-muted">Created {{ dateTime(row.createdAt) }}</div></td>
                  <td>
                    <div class="inline-action-row justify-end">
                      <UButton size="xs" label="Open" icon="i-lucide-eye" variant="subtle" @click="openCampaign(row)" />
                      <UButton v-if="row.status === 'Draft'" size="xs" label="Queue" icon="i-lucide-send" color="info" variant="subtle" @click="updateStatus(row, 'queue')" />
                      <UButton v-if="row.templateName && (row.status === 'Draft' || row.status === 'Queued')" size="xs" label="Send template" icon="i-lucide-send" color="success" variant="subtle" @click="sendViaWhatsAppTemplate(row)" />
                      <UButton v-if="row.status === 'Queued'" size="xs" label="Mark sent" icon="i-lucide-check" color="success" variant="subtle" @click="updateStatus(row, 'mark-sent')" />
                      <UButton v-if="row.status !== 'Completed' && row.status !== 'Cancelled'" size="xs" label="Cancel" icon="i-lucide-x" color="error" variant="ghost" @click="updateStatus(row, 'cancel')" />
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </UiRegisterPanel>
      </div>
    </div>

    <UCard v-if="selectedCampaign" class="mt-4">
      <template #header>
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div class="font-semibold">{{ selectedCampaign.name }}</div>
            <div class="text-xs text-muted">{{ selectedCampaign.segment }} · {{ selectedCampaign.status }} · {{ selectedCampaign.recipientCount }} recipients</div>
          </div>
          <div class="inline-action-row">
            <UInput v-model.number="roiDays" type="number" min="1" max="365" class="w-24" />
            <UButton icon="i-lucide-chart-no-axes-combined" label="Refresh ROI" variant="subtle" :loading="roiLoading" @click="loadRoi()" />
            <UButton icon="i-lucide-send" label="Send WhatsApp Template" color="success" variant="subtle" :loading="saving" :disabled="!selectedCampaign.templateName" @click="sendViaWhatsAppTemplate()" />
            <UButton icon="i-lucide-copy" label="Copy Messages" variant="subtle" @click="copyMessages" />
            <UButton icon="i-lucide-file-down" label="Export Recipients" variant="subtle" @click="exportRecipients" />
          </div>
        </div>
      </template>

      <UAlert
        v-if="lastSendResult"
        class="mb-4"
        :color="lastSendResult.failed ? 'warning' : 'success'"
        variant="soft"
        icon="i-lucide-send"
        title="WhatsApp marketing template batch result"
        :description="`Attempted ${lastSendResult.attempted}. Sent ${lastSendResult.sent}, manual ${lastSendResult.manualPending}, failed ${lastSendResult.failed}, skipped ${lastSendResult.skipped}. ${lastSendResult.message}`"
      />

      <div class="grid gap-3 md:grid-cols-5">
        <div><p class="text-xs text-muted">Recipients</p><strong>{{ selectedCampaign.recipientCount }}</strong></div>
        <div><p class="text-xs text-muted">Prepared</p><strong>{{ selectedCampaign.preparedCount }}</strong></div>
        <div><p class="text-xs text-muted">Sent</p><strong>{{ selectedCampaign.sentCount }}</strong></div>
        <div><p class="text-xs text-muted">Opens before campaign</p><strong>{{ selectedCampaign.openCountAtCreate }}</strong></div>
        <div><p class="text-xs text-muted">Review clicks before</p><strong>{{ selectedCampaign.reviewClickCountAtCreate }}</strong></div>
      </div>

      <UAlert
        v-if="selectedRoi"
        class="mt-4"
        color="success"
        variant="soft"
        icon="i-lucide-chart-no-axes-combined"
        title="Campaign ROI tracking"
        :description="`Attribution window ${date(selectedRoi.attributionFrom)} → ${date(selectedRoi.attributionTo)}. Repeat sales exclude the original source invoices used to build this campaign.`"
      />

      <div v-if="selectedRoi" class="grid gap-3 md:grid-cols-4 lg:grid-cols-8 mt-4">
        <div><p class="text-xs text-muted">Engaged</p><strong>{{ selectedRoi.engagedRecipientCount }}</strong><div class="text-xs text-muted">{{ percent(selectedRoi.engagementRate) }}</div></div>
        <div><p class="text-xs text-muted">Opened after</p><strong>{{ selectedRoi.openedAfterCount }}</strong></div>
        <div><p class="text-xs text-muted">Review clicks</p><strong>{{ selectedRoi.reviewClickedAfterCount }}</strong></div>
        <div><p class="text-xs text-muted">Banner clicks</p><strong>{{ selectedRoi.bannerClickedAfterCount }}</strong></div>
        <div><p class="text-xs text-muted">Feedback</p><strong>{{ selectedRoi.feedbackSubmittedAfterCount }}</strong></div>
        <div><p class="text-xs text-muted">Repeat customers</p><strong>{{ selectedRoi.repeatCustomerCount }}</strong><div class="text-xs text-muted">{{ percent(selectedRoi.repeatPurchaseRate) }}</div></div>
        <div><p class="text-xs text-muted">Repeat bills</p><strong>{{ selectedRoi.repeatInvoiceCount }}</strong></div>
        <div><p class="text-xs text-muted">Repeat sales</p><strong>{{ money(selectedRoi.repeatSalesAmount) }}</strong></div>
      </div>
      <div v-if="selectedRoi?.repeatSales?.length" class="mt-4">
        <div class="mb-2 font-semibold">Repeat sales after campaign</div>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Invoice</th><th>Customer</th><th>Store</th><th class="text-right">Amount</th></tr></thead>
            <tbody>
              <tr v-for="row in selectedRoi.repeatSales" :key="row.invoiceId">
                <td>{{ row.invoiceNumber }}<div class="text-xs text-muted">{{ dateTime(row.invoiceDate) }}</div></td>
                <td>{{ row.customerName }}<div class="text-xs text-muted">{{ row.customerMobile }}</div></td>
                <td>{{ row.storeName }}</td>
                <td class="text-right font-medium">{{ money(row.billAmount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="planner-table-wrap mt-4">
        <table class="planner-table">
          <thead><tr><th>Customer</th><th>Invoice</th><th>Status</th><th v-if="selectedRoi">ROI after campaign</th><th>Message</th></tr></thead>
          <tbody>
            <tr v-for="row in selectedRecipients" :key="row.id">
              <td>{{ row.customerName }}<div class="text-xs text-muted">{{ row.customerMobile }} · {{ row.storeName }}</div></td>
              <td>{{ row.invoiceNumber }}<div class="text-xs text-muted">{{ money(row.lastInvoiceAmount) }} · {{ row.publicPath }}</div></td>
              <td><UBadge :color="statusColor(row.status)" variant="subtle">{{ row.status }}</UBadge></td>
              <td v-if="selectedRoi" class="text-sm">
                <template v-if="roiForRecipient(row.id)">
                  Opens {{ roiForRecipient(row.id).openedAfterCount }} · Reviews {{ roiForRecipient(row.id).reviewClickedAfterCount }} · Repeat {{ roiForRecipient(row.id).repeatInvoiceCount }} / {{ money(roiForRecipient(row.id).repeatSalesAmount) }}
                </template>
                <span v-else class="text-muted">-</span>
              </td>
              <td class="text-sm">{{ row.messageBody }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </AppShell>
</template>

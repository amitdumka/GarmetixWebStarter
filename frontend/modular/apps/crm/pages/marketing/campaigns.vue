<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-megaphone" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Campaigns</h2>
          <p class="garmetix-dashboard-subtitle">Create, preview, queue, send, close and measure digital bill campaigns.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-users-round" @click="router.push('/marketing/campaign-audiences')">Audiences</UButton>
          <UButton icon="i-lucide-plus" @click="openCreate">New Campaign</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="garmetix-section-card grid gap-3 md:grid-cols-[180px_1fr_auto]">
      <UFormField label="Status">
        <USelect v-model="filters.status" :items="statusOptions" />
      </UFormField>
      <UFormField label="Search">
        <UInput v-model="filters.q" placeholder="Campaign name or title" @keyup.enter="refresh(true)" />
      </UFormField>
      <div class="flex items-end gap-2">
        <UButton icon="i-lucide-search" :loading="loading" @click="refresh(true)">Apply</UButton>
      </div>
    </div>

    <section class="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Campaigns</p>
        <p class="garmetix-metric-value">{{ number(total) }}</p>
        <p class="garmetix-metric-caption">Filtered register total</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Recipients</p>
        <p class="garmetix-metric-value">{{ number(summary.recipients) }}</p>
        <p class="garmetix-metric-caption">Visible page audience</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Sent</p>
        <p class="garmetix-metric-value">{{ number(summary.sent) }}</p>
        <p class="garmetix-metric-caption">Visible page sent count</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Failed</p>
        <p class="garmetix-metric-value">{{ number(summary.failed) }}</p>
        <p class="garmetix-metric-caption">Visible page failed count</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Prepared</p>
        <p class="garmetix-metric-value">{{ number(summary.prepared) }}</p>
        <p class="garmetix-metric-caption">Ready or manual pending</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.15fr)_minmax(420px,0.85fr)]">
      <div class="garmetix-section-card">
        <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p class="garmetix-kicker"><UIcon name="i-lucide-list" class="size-4" /> Campaign register</p>
            <h3 class="text-xl font-semibold text-highlighted">Digital bill campaigns</h3>
            <p class="text-sm text-muted">Page {{ page }} / {{ totalPages }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--; refresh()">Prev</UButton>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++; refresh()">Next</UButton>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[1120px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Campaign</th>
                <th class="border-b border-default p-3">Segment</th>
                <th class="border-b border-default p-3">Window</th>
                <th class="border-b border-default p-3 text-right">Recipients</th>
                <th class="border-b border-default p-3 text-right">Delivery</th>
                <th class="border-b border-default p-3">Status</th>
                <th class="border-b border-default p-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="loading"><td colspan="7" class="p-8 text-center text-muted">Loading campaigns...</td></tr>
              <tr v-else-if="!campaigns.length"><td colspan="7" class="p-8 text-center text-muted">No campaigns found.</td></tr>
              <template v-else>
                <tr
                  v-for="row in campaigns"
                  :key="readId(row)"
                  :class="{ 'bg-muted/30': selectedId === readId(row) }"
                >
                  <td class="border-b border-default p-3">
                    <button type="button" class="text-left font-semibold text-highlighted hover:text-primary" @click="selectCampaign(row)">
                      {{ readText(row, ['name']) }}
                    </button>
                    <p class="text-xs text-muted">{{ readText(row, ['messageTitle']) }} | {{ readText(row, ['storeName'], 'All stores') }}</p>
                  </td>
                  <td class="border-b border-default p-3">{{ segmentLabel(readText(row, ['segment'], 'all')) }}</td>
                  <td class="border-b border-default p-3">
                    <p>{{ formatDate(readText(row, ['fromDate'], '')) }}</p>
                    <p class="text-xs text-muted">to {{ formatDate(readText(row, ['toDate'], '')) }}</p>
                  </td>
                  <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['recipientCount'])) }}</td>
                  <td class="border-b border-default p-3 text-right text-xs">
                    Sent {{ number(readNumber(row, ['sentCount'])) }}<br>
                    Failed {{ number(readNumber(row, ['failedCount'])) }}
                  </td>
                  <td class="border-b border-default p-3">
                    <UBadge :color="statusColor(readText(row, ['status'], ''))" variant="subtle">{{ readText(row, ['status']) }}</UBadge>
                    <p class="mt-1 text-xs text-muted">{{ readText(row, ['channel']) }}</p>
                  </td>
                  <td class="border-b border-default p-3">
                    <div class="flex flex-wrap justify-end gap-2">
                      <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="selectCampaign(row)">View</UButton>
                      <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chart-no-axes-combined" @click="loadRoi(row)">ROI</UButton>
                      <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-copy" @click="copyCampaignMessages(row)">Copy</UButton>
                    </div>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </div>

      <aside class="grid gap-4">
        <div class="garmetix-section-card">
          <div class="mb-4 flex items-start justify-between gap-3">
            <div>
              <p class="garmetix-kicker"><UIcon name="i-lucide-panel-right" class="size-4" /> Detail</p>
              <h3 class="text-xl font-semibold text-highlighted">{{ detailTitle }}</h3>
              <p class="text-sm text-muted">{{ detailSubtitle }}</p>
            </div>
            <UBadge :color="statusColor(readText(selectedCampaign, ['status'], ''))" variant="subtle">{{ readText(selectedCampaign, ['status'], 'No selection') }}</UBadge>
          </div>

          <div v-if="!selectedCampaign" class="rounded-md border border-dashed border-default p-6 text-sm text-muted">
            Select a campaign from the register or create a new campaign.
          </div>
          <template v-else>
            <div class="grid gap-3 sm:grid-cols-2">
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Recipients</p>
                <p class="text-2xl font-semibold text-highlighted">{{ number(readNumber(selectedCampaign, ['recipientCount'])) }}</p>
              </div>
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Prepared</p>
                <p class="text-2xl font-semibold text-highlighted">{{ number(readNumber(selectedCampaign, ['preparedCount'])) }}</p>
              </div>
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Sent</p>
                <p class="text-2xl font-semibold text-highlighted">{{ number(readNumber(selectedCampaign, ['sentCount'])) }}</p>
              </div>
              <div class="rounded-md border border-default p-3">
                <p class="text-xs uppercase text-muted">Failed</p>
                <p class="text-2xl font-semibold text-highlighted">{{ number(readNumber(selectedCampaign, ['failedCount'])) }}</p>
              </div>
            </div>

            <div class="mt-4 rounded-md border border-default p-3">
              <p class="text-xs uppercase text-muted">Message</p>
              <p class="mt-1 font-semibold text-highlighted">{{ readText(selectedCampaign, ['messageTitle']) }}</p>
              <p class="mt-2 whitespace-pre-wrap text-sm text-muted">{{ readText(selectedCampaign, ['messageBody']) }}</p>
            </div>

            <div class="mt-4 flex flex-wrap gap-2">
              <UButton size="sm" icon="i-lucide-list-checks" :loading="actionLoading === 'queue'" :disabled="!canQueue" @click="queueCampaign">Queue</UButton>
              <UButton size="sm" color="success" icon="i-lucide-send" :loading="actionLoading === 'send'" :disabled="!canSend" @click="sendCampaign">Send WhatsApp</UButton>
              <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-check-check" :loading="actionLoading === 'mark'" :disabled="!canMarkSent" @click="markSent">Mark Sent</UButton>
              <UButton size="sm" color="error" variant="soft" icon="i-lucide-ban" :loading="actionLoading === 'cancel'" :disabled="!canCancel" @click="cancelCampaign">Cancel</UButton>
            </div>
          </template>
        </div>

        <div v-if="sendResult" class="garmetix-section-card">
          <p class="garmetix-kicker"><UIcon name="i-lucide-send" class="size-4" /> Last send result</p>
          <h3 class="text-lg font-semibold text-highlighted">{{ readText(sendResult, ['message']) }}</h3>
          <div class="mt-3 grid grid-cols-2 gap-2 text-sm">
            <p>Attempted: <span class="font-semibold">{{ number(readNumber(sendResult, ['attempted'])) }}</span></p>
            <p>Sent: <span class="font-semibold">{{ number(readNumber(sendResult, ['sent'])) }}</span></p>
            <p>Manual: <span class="font-semibold">{{ number(readNumber(sendResult, ['manualPending'])) }}</span></p>
            <p>Failed: <span class="font-semibold">{{ number(readNumber(sendResult, ['failed'])) }}</span></p>
          </div>
        </div>

        <div class="garmetix-section-card">
          <div class="mb-3 flex items-center justify-between gap-3">
            <p class="garmetix-kicker"><UIcon name="i-lucide-users" class="size-4" /> Recipients</p>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-copy" :disabled="!recipients.length" @click="copySelectedMessages">Copy messages</UButton>
          </div>
          <div class="max-h-[430px] overflow-auto">
            <div v-if="detailLoading" class="p-4 text-sm text-muted">Loading campaign detail...</div>
            <div v-else-if="!recipients.length" class="p-4 text-sm text-muted">No recipients loaded.</div>
            <template v-else>
              <div v-for="recipient in recipients" :key="readId(recipient)" class="border-b border-default py-3 last:border-b-0">
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <p class="font-semibold text-highlighted">{{ readText(recipient, ['customerName']) }}</p>
                    <p class="text-xs text-muted">{{ readText(recipient, ['customerMobile']) }} | {{ readText(recipient, ['invoiceNumber']) }}</p>
                  </div>
                  <UBadge :color="statusColor(readText(recipient, ['status'], ''))" variant="subtle">{{ readText(recipient, ['status']) }}</UBadge>
                </div>
                <p class="mt-2 line-clamp-2 text-xs text-muted">{{ readText(recipient, ['messageBody']) }}</p>
                <p v-if="readText(recipient, ['errorMessage'], '')" class="mt-1 text-xs text-error">{{ readText(recipient, ['errorMessage']) }}</p>
              </div>
            </template>
          </div>
        </div>
      </aside>
    </section>

    <section v-if="roi" class="garmetix-section-card">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-chart-no-axes-combined" class="size-4" /> Campaign ROI</p>
          <h3 class="text-xl font-semibold text-highlighted">{{ readText(roi, ['campaignName']) }}</h3>
          <p class="text-sm text-muted">{{ formatDate(readText(roi, ['attributionFrom'], '')) }} to {{ formatDate(readText(roi, ['attributionTo'], '')) }}</p>
        </div>
        <UButton color="neutral" variant="soft" icon="i-lucide-x" @click="roi = null">Close ROI</UButton>
      </div>
      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Engagement</p><p class="text-2xl font-semibold">{{ number(readNumber(roi, ['engagementRate'])) }}%</p></div>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Repeat rate</p><p class="text-2xl font-semibold">{{ number(readNumber(roi, ['repeatPurchaseRate'])) }}%</p></div>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Repeat sales</p><p class="text-2xl font-semibold">{{ money(readNumber(roi, ['repeatSalesAmount'])) }}</p></div>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Repeat bills</p><p class="text-2xl font-semibold">{{ number(readNumber(roi, ['repeatInvoiceCount'])) }}</p></div>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Opened</p><p class="text-2xl font-semibold">{{ number(readNumber(roi, ['openedAfterCount'])) }}</p></div>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Feedback</p><p class="text-2xl font-semibold">{{ number(readNumber(roi, ['feedbackSubmittedAfterCount'])) }}</p></div>
      </div>
    </section>

    <UModal v-model:open="createOpen" title="Create Campaign" description="Preview the audience before creating the campaign.">
      <template #body>
        <div class="grid gap-4">
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Campaign name">
              <UInput v-model="form.name" placeholder="Festival reminder" />
            </UFormField>
            <UFormField label="Segment">
              <USelect v-model="form.segment" :items="segmentOptions" />
            </UFormField>
            <UFormField label="From">
              <UInput v-model="form.fromDate" type="date" />
            </UFormField>
            <UFormField label="To">
              <UInput v-model="form.toDate" type="date" />
            </UFormField>
            <UFormField label="Store">
              <USelect v-model="form.storeId" :items="storeOptions" />
            </UFormField>
            <UFormField label="Channel">
              <USelect v-model="form.channel" :items="channelOptions" />
            </UFormField>
            <UFormField label="Template name">
              <UInput v-model="form.templateName" placeholder="approved_template_name" />
            </UFormField>
            <UFormField label="Limit">
              <UInput v-model.number="form.limit" type="number" min="1" max="5000" />
            </UFormField>
            <UFormField label="Search" class="md:col-span-2">
              <UInput v-model="form.searchText" placeholder="Optional customer/mobile/invoice filter" />
            </UFormField>
            <UFormField label="Title" class="md:col-span-2">
              <UInput v-model="form.messageTitle" placeholder="Special offer" />
            </UFormField>
            <UFormField label="Offer URL" class="md:col-span-2">
              <UInput v-model="form.offerUrl" placeholder="Optional landing page or offer URL" />
            </UFormField>
            <UFormField label="Message body" class="md:col-span-2">
              <UTextarea v-model="form.messageBody" :rows="5" />
            </UFormField>
            <UFormField label="Notes" class="md:col-span-2">
              <UTextarea v-model="form.notes" :rows="2" />
            </UFormField>
          </div>

          <div class="flex flex-wrap items-center gap-3">
            <UCheckbox v-model="form.queueNow" label="Queue immediately after create" />
            <UButton color="neutral" variant="soft" icon="i-lucide-eye" :loading="previewLoading" @click="previewCampaign">Preview</UButton>
            <UButton icon="i-lucide-save" :loading="creating" @click="createCampaign">Create Campaign</UButton>
          </div>

          <UAlert v-if="previewError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="previewError" />

          <div v-if="preview" class="rounded-md border border-default p-3">
            <div class="mb-3 grid gap-2 text-sm md:grid-cols-4">
              <p>Total: <span class="font-semibold">{{ number(readNumber(preview, ['totalCandidates'])) }}</span></p>
              <p>With mobile: <span class="font-semibold">{{ number(readNumber(preview, ['recipientsWithMobile'])) }}</span></p>
              <p>No mobile: <span class="font-semibold">{{ number(readNumber(preview, ['skippedNoMobile'])) }}</span></p>
              <p>Limit: <span class="font-semibold">{{ number(readNumber(preview, ['limitApplied'])) }}</span></p>
            </div>
            <div class="max-h-64 overflow-auto">
              <div v-for="recipient in previewRecipients" :key="readText(recipient, ['audienceKey', 'customerMobile'])" class="border-b border-default py-2 text-sm last:border-b-0">
                <p class="font-semibold text-highlighted">{{ readText(recipient, ['customerName']) }} - {{ readText(recipient, ['customerMobile']) }}</p>
                <p class="text-xs text-muted">{{ readText(recipient, ['invoiceNumber']) }} | {{ readText(recipient, ['messageBody']) }}</p>
              </div>
            </div>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readArray, readId, readNumber, readRecord, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const route = useRoute()
const toast = useToast()
const { get, post } = useCrmApiClient()

const ALL_VALUE = '__ALL__'
const today = new Date()
const start = new Date(Date.now() - 29 * 24 * 60 * 60 * 1000)

const statusOptions = [
  { label: 'All', value: 'all' },
  { label: 'Draft', value: 'Draft' },
  { label: 'Queued', value: 'Queued' },
  { label: 'Completed', value: 'Completed' },
  { label: 'Cancelled', value: 'Cancelled' }
]
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
const channelOptions = [
  { label: 'Manual copy/export', value: 'Manual' },
  { label: 'WhatsApp provider', value: 'WhatsAppProvider' },
  { label: 'WhatsApp marketing template', value: 'WhatsAppMarketingTemplate' }
]

const filters = reactive({ status: 'all', q: '' })
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const loading = ref(false)
const error = ref('')
const campaigns = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const selectedId = ref('')
const detail = ref<ApiRecord | null>(null)
const detailLoading = ref(false)
const actionLoading = ref('')
const sendResult = ref<ApiRecord | null>(null)
const roi = ref<ApiRecord | null>(null)
const createOpen = ref(false)
const preview = ref<ApiRecord | null>(null)
const previewLoading = ref(false)
const previewError = ref('')
const creating = ref(false)

const form = reactive({
  name: '',
  segment: 'all',
  fromDate: toDateInput(start),
  toDate: toDateInput(today),
  storeId: ALL_VALUE,
  searchText: '',
  channel: 'Manual',
  templateName: '',
  messageTitle: 'Digital bill campaign',
  messageBody: 'Hello {{customerName}}, thank you for shopping with us. Your bill {{invoiceNumber}} is ready here: {{publicUrl}}',
  offerUrl: '',
  notes: '',
  scheduledAt: '',
  queueNow: false,
  limit: 1000
})

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))
const selectedCampaign = computed(() => readRecord(detail.value, ['campaign']))
const recipients = computed(() => readArray(detail.value, ['recipients']))
const previewRecipients = computed(() => readArray(preview.value, ['sampleRecipients']))
const detailTitle = computed(() => selectedCampaign.value ? readText(selectedCampaign.value, ['name']) : 'No campaign selected')
const detailSubtitle = computed(() => selectedCampaign.value ? `${segmentLabel(readText(selectedCampaign.value, ['segment'], 'all'))} | ${readText(selectedCampaign.value, ['channel'])}` : 'Select a register row to review recipients and actions.')
const canQueue = computed(() => ['Draft'].includes(readText(selectedCampaign.value, ['status'], '')))
const canSend = computed(() => ['Queued', 'Draft'].includes(readText(selectedCampaign.value, ['status'], '')))
const canMarkSent = computed(() => ['Queued', 'Draft'].includes(readText(selectedCampaign.value, ['status'], '')))
const canCancel = computed(() => selectedCampaign.value && !['Completed', 'Cancelled'].includes(readText(selectedCampaign.value, ['status'], '')))
const storeOptions = computed(() => [
  { label: 'All stores / workspace', value: ALL_VALUE },
  ...stores.value.map(row => ({ label: `${readText(row, ['name', 'storeName'], 'Store')} (${readText(row, ['storeCode'], 'store')})`, value: readId(row) }))
])
const summary = computed(() => campaigns.value.reduce((acc, row) => {
  acc.recipients += readNumber(row, ['recipientCount'])
  acc.prepared += readNumber(row, ['preparedCount'])
  acc.sent += readNumber(row, ['sentCount'])
  acc.failed += readNumber(row, ['failedCount'])
  return acc
}, { recipients: 0, prepared: 0, sent: 0, failed: 0 }))

function toDateInput(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function money(value: number) {
  return formatIndianMoney(value)
}

function segmentLabel(value: string) {
  return segmentOptions.find(item => item.value === value)?.label ?? value
}

function statusColor(status: string) {
  if (['Completed', 'Sent', 'Delivered', 'Read'].includes(status)) return 'success' as const
  if (['Cancelled', 'Failed', 'Skipped'].includes(status)) return 'error' as const
  if (['Queued', 'ManualPending', 'Prepared', 'Draft'].includes(status)) return 'warning' as const
  return 'neutral' as const
}

function normalizeStoreId(value: string) {
  return value && value !== ALL_VALUE ? value : null
}

function buildCreatePayload() {
  return {
    name: form.name.trim(),
    segment: form.segment,
    fromDate: form.fromDate || null,
    toDate: form.toDate || null,
    storeId: normalizeStoreId(form.storeId),
    searchText: form.searchText.trim() || null,
    channel: form.channel,
    templateName: form.templateName.trim() || null,
    messageTitle: form.messageTitle.trim() || 'Digital bill campaign',
    messageBody: form.messageBody.trim(),
    offerUrl: form.offerUrl.trim() || null,
    notes: form.notes.trim() || null,
    scheduledAt: form.scheduledAt || null,
    queueNow: form.queueNow,
    limit: Number(form.limit || 1000)
  }
}

async function refresh(resetPage = false) {
  if (resetPage) page.value = 1
  loading.value = true
  error.value = ''
  try {
    const [campaignData, storeData] = await Promise.all([
      get<unknown>('digital-bill-campaigns', {
        page: page.value,
        pageSize: pageSize.value,
        status: filters.status,
        q: filters.q.trim() || undefined
      }),
      get<unknown>('stores').catch(() => [])
    ])
    const record = readRecord(campaignData)
    campaigns.value = toRows(campaignData)
    total.value = readNumber(record, ['total'])
    stores.value = toRows(storeData)
    if (!selectedId.value && campaigns.value.length) await selectCampaign(campaigns.value[0])
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Campaign load failed.')
    toast.add({ title: 'Campaign load failed', description: error.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

function hydrateFromQuery() {
  form.segment = String(route.query.segment || form.segment || 'all')
  form.fromDate = String(route.query.fromDate || form.fromDate)
  form.toDate = String(route.query.toDate || form.toDate)
  form.storeId = String(route.query.storeId || '') || ALL_VALUE
  form.searchText = String(route.query.searchText || '')
  if (route.query.segment || route.query.searchText) createOpen.value = true
}

function openCreate() {
  preview.value = null
  previewError.value = ''
  createOpen.value = true
}

async function selectCampaign(row: ApiRecord, clearSendResult = true) {
  const id = readId(row)
  if (!id) return
  selectedId.value = id
  detailLoading.value = true
  if (clearSendResult) sendResult.value = null
  try {
    detail.value = readRecord(await get<unknown>(`digital-bill-campaigns/${id}`))
  } catch (caught) {
    toast.add({ title: 'Could not load campaign detail', description: stripServerUrl(caught instanceof Error ? caught.message : 'Campaign detail failed.'), color: 'error' })
  } finally {
    detailLoading.value = false
  }
}

async function previewCampaign() {
  previewLoading.value = true
  previewError.value = ''
  preview.value = null
  try {
    preview.value = readRecord(await post<unknown>('digital-bill-campaigns/preview', buildCreatePayload()))
  } catch (caught) {
    previewError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Campaign preview failed.')
    toast.add({ title: 'Preview failed', description: previewError.value, color: 'error' })
  } finally {
    previewLoading.value = false
  }
}

async function createCampaign() {
  if (!form.name.trim() || !form.messageBody.trim()) {
    toast.add({ title: 'Campaign name and message are required', color: 'warning' })
    return
  }
  creating.value = true
  previewError.value = ''
  try {
    const created = readRecord(await post<unknown>('digital-bill-campaigns', buildCreatePayload()))
    createOpen.value = false
    detail.value = created
    selectedId.value = readId(readRecord(created, ['campaign']))
    toast.add({ title: 'Campaign created', description: readText(readRecord(created, ['campaign']), ['name'], 'Campaign saved.'), color: 'success' })
    await refresh(true)
    if (selectedId.value) {
      const row = campaigns.value.find(item => readId(item) === selectedId.value)
      if (row) await selectCampaign(row, action !== 'send')
    }
  } catch (caught) {
    previewError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Campaign create failed.')
    toast.add({ title: 'Create failed', description: previewError.value, color: 'error' })
  } finally {
    creating.value = false
  }
}

async function lifecycle(action: 'queue' | 'send' | 'mark' | 'cancel') {
  const campaign = selectedCampaign.value
  const id = readId(campaign)
  if (!id) return
  const message = {
    queue: 'Queue this campaign for manual or WhatsApp sending?',
    send: 'Send this campaign using the configured WhatsApp provider or template?',
    mark: 'Mark all non-sent recipients as sent and complete this campaign?',
    cancel: 'Cancel this campaign? Non-sent recipients will be cancelled.'
  }[action]
  if (!window.confirm(message)) return
  actionLoading.value = action
  try {
    let result: unknown
    if (action === 'queue') result = await post<unknown>(`digital-bill-campaigns/${id}/queue`, { reason: 'Queued from modular CRM.' })
    if (action === 'send') result = await post<unknown>(`digital-bill-campaigns/${id}/send-whatsapp`, { force: false, limit: 500 })
    if (action === 'mark') result = await post<unknown>(`digital-bill-campaigns/${id}/mark-sent`, { reason: 'Marked sent from modular CRM.' })
    if (action === 'cancel') result = await post<unknown>(`digital-bill-campaigns/${id}/cancel`, { reason: 'Cancelled from modular CRM.' })

    if (action === 'send') {
      sendResult.value = readRecord(result)
      toast.add({ title: 'Send completed', description: readText(sendResult.value, ['message'], 'Campaign send processed.'), color: 'success' })
    } else {
      detail.value = readRecord(result)
      toast.add({ title: 'Campaign updated', color: 'success' })
    }
    await refresh()
    if (selectedId.value) {
      const row = campaigns.value.find(item => readId(item) === selectedId.value)
      if (row) await selectCampaign(row)
    }
  } catch (caught) {
    toast.add({ title: 'Campaign action failed', description: stripServerUrl(caught instanceof Error ? caught.message : 'Campaign action failed.'), color: 'error' })
  } finally {
    actionLoading.value = ''
  }
}

const queueCampaign = () => lifecycle('queue')
const sendCampaign = () => lifecycle('send')
const markSent = () => lifecycle('mark')
const cancelCampaign = () => lifecycle('cancel')

async function loadRoi(row: ApiRecord) {
  const id = readId(row)
  if (!id) return
  try {
    roi.value = readRecord(await get<unknown>(`digital-bill-campaigns/${id}/roi`, { days: 30 }))
    await selectCampaign(row)
  } catch (caught) {
    toast.add({ title: 'ROI load failed', description: stripServerUrl(caught instanceof Error ? caught.message : 'Campaign ROI failed.'), color: 'error' })
  }
}

function recipientMessageLines(rows: ApiRecord[]) {
  return rows.map(row => `${readText(row, ['customerMobile'], '')}\t${readText(row, ['customerName'], '')}\t${readText(row, ['messageBody'], '')}`).join('\n')
}

async function copySelectedMessages() {
  await copyText(recipientMessageLines(recipients.value), 'Campaign recipient messages copied.')
}

async function copyCampaignMessages(row: ApiRecord) {
  await selectCampaign(row)
  await copySelectedMessages()
}

async function copyText(text: string, success: string) {
  if (!text.trim()) {
    toast.add({ title: 'Nothing to copy', color: 'warning' })
    return
  }
  try {
    await navigator.clipboard.writeText(text)
    toast.add({ title: success, color: 'success' })
  } catch {
    window.prompt('Copy campaign data', text)
  }
}

onMounted(async () => {
  hydrateFromQuery()
  await refresh()
})

useHead({ title: 'Campaigns - Garmetix CRM' })
</script>

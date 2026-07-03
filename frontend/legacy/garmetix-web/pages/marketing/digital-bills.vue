<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const route = useRoute()
const digitalBills = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const search = ref('')
const page = ref(1)
const pageSize = 50
const total = ref(0)
const invoiceId = ref(String(route.query.invoiceId || ''))
const activityOpen = ref(false)
const activityLoading = ref(false)
const activityError = ref('')
const selectedActivity = ref<any | null>(null)

const metrics = computed(() => [
  { label: 'Digital bills', value: total.value, icon: 'i-lucide-receipt-text', color: 'primary' },
  { label: 'Opened', value: digitalBills.value.reduce((sum, row) => sum + Number(row.openCount || 0), 0), icon: 'i-lucide-eye', color: 'success' },
  { label: 'PDF downloads', value: digitalBills.value.reduce((sum, row) => sum + Number(row.pdfDownloadCount || 0), 0), icon: 'i-lucide-file-down', color: 'info' },
  { label: 'Review clicks', value: digitalBills.value.reduce((sum, row) => sum + Number(row.reviewClickCount || 0), 0), icon: 'i-lucide-star', color: 'warning' }
])

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (search.value.trim()) query.set('q', search.value.trim())
    const response = await api.get<any>(`digital-bills?${query}`)
    digitalBills.value = response.items || []
    total.value = Number(response.total || digitalBills.value.length)
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'Digital bills load failed')
  } finally {
    loading.value = false
  }
}

async function generateForInvoice() {
  const id = invoiceId.value.trim()
  if (!id) {
    feedback.notify('Enter a sale invoice ID first.', undefined, 'warning')
    return
  }
  loading.value = true
  try {
    const response = await api.create<any>('digital-bills/sales/generate', { invoiceKey: id })
    feedback.notify(response.message || 'Digital bill generated.', response.publicPath, 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Could not generate digital bill', error)
  } finally {
    loading.value = false
  }
}

async function copyLink(row: any) {
  const link = `${window.location.origin}${row.publicPath}`
  await navigator.clipboard?.writeText(link)
  feedback.notify('Digital bill link copied', link, 'success')
}

async function disable(row: any) {
  if (!confirm(`Disable digital bill ${row.invoiceNumber}?`)) return
  try {
    await api.create<any>(`digital-bills/${row.id}/disable`, { reason: 'Disabled from Digital Bills page' })
    feedback.notify('Digital bill disabled', undefined, 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Could not disable digital bill', error)
  }
}

async function regenerate(row: any) {
  if (!confirm(`Regenerate public token for ${row.invoiceNumber}? Old link will stop working.`)) return
  try {
    await api.create<any>(`digital-bills/${row.id}/regenerate-token`, {})
    feedback.notify('Digital bill token regenerated', undefined, 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Could not regenerate token', error)
  }
}

async function sendWhatsApp(row: any) {
  loading.value = true
  try {
    const response = await api.create<any>(`digital-bills/${row.id}/send-whatsapp`, { force: true })
    feedback.notify(`WhatsApp status: ${response.status}`, response.errorMessage || response.messageBody, response.status === 'Sent' ? 'success' : 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Could not send WhatsApp digital bill', error)
  } finally {
    loading.value = false
  }
}

async function openActivity(row: any) {
  if (!row?.id) return
  activityOpen.value = true
  activityLoading.value = true
  activityError.value = ''
  selectedActivity.value = null
  try {
    selectedActivity.value = await api.get<any>(`digital-bills/${row.id}/activity`)
  } catch (error) {
    activityError.value = feedback.errorMessage(error, 'Could not load customer activity for this digital bill.', 'Activity load failed')
  } finally {
    activityLoading.value = false
  }
}

async function openActivityById(id: string) {
  if (!id) return
  await openActivity({ id })
}

function eventColor(type: string) {
  if (['Opened', 'DigitalBillCreated'].includes(type)) return 'primary'
  if (['PdfDownloaded', 'FeedbackSubmitted'].includes(type)) return 'success'
  if (['ReviewClicked', 'InstagramClicked', 'FacebookClicked', 'BannerClicked'].includes(type)) return 'warning'
  if (['WhatsAppSent', 'WhatsAppDelivered', 'WhatsAppRead'].includes(type)) return 'info'
  if (['Failed', 'WhatsAppFailed'].includes(type)) return 'error'
  return 'neutral'
}

function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function date(value: string) { return value ? new Date(value).toLocaleString('en-IN') : '-' }

onMounted(async () => {
  await refresh()
  const activityId = String(route.query.activityId || '')
  if (activityId) await openActivityById(activityId)
})
</script>

<template>
  <AppShell title="Digital Bills" @refresh="refresh">
    <UiModulePageHeader
      title="Digital Bills"
      description="Generate secure customer invoice links, copy/share them, and track opens, PDF downloads, feedback, and review clicks."
      icon="i-lucide-receipt-text"
      primary-label="Generate"
      primary-icon="i-lucide-link"
      @primary="generateForInvoice"
    />

    <div class="planner-metric-grid mt-4">
      <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
        <div class="planner-metric-body">
          <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
          <div><p>{{ metric.label }}</p><strong>{{ metric.value }}</strong><span>Current filtered page</span></div>
        </div>
      </UCard>
    </div>

    <UCard class="mt-4">
      <template #header><strong>Generate digital bill from sale invoice</strong></template>
      <div class="grid gap-3 lg:grid-cols-[1fr_auto]">
        <UInput v-model="invoiceId" placeholder="Paste sale invoice ID or invoice number, e.g. S-20260625-0001" @keyup.enter="generateForInvoice" />
        <UButton icon="i-lucide-link" :loading="loading" label="Generate / Ensure Link" @click="generateForInvoice" />
      </div>
      <p class="mt-2 text-xs text-muted">When WhatsApp Settings has auto-send enabled, this link is generated and queued/sent automatically after sale invoice finalization.</p>
    </UCard>

    <UiRegisterPanel
      class="mt-4"
      title="Digital bill register"
      :description="`${digitalBills.length} loaded of ${total}`"
      :loading="loading"
      :error="loadError"
      :empty="digitalBills.length === 0"
      empty-title="No digital bills yet"
      empty-description="Generate a digital bill from a sale invoice first."
      empty-icon="i-lucide-receipt-text"
      @retry="refresh"
    >
      <template #actions>
        <UiCrudToolbar v-model:search="search" search-placeholder="Search invoice/customer/mobile" :loading="loading" @refresh="refresh" />
        <UButton icon="i-lucide-search" label="Apply" @click="refresh" />
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Invoice</th><th>Customer</th><th>Date</th><th class="text-right">Amount</th><th>Link</th><th>WhatsApp</th><th class="text-right">Activity</th><th class="text-right">Actions</th></tr></thead>
          <tbody>
            <tr v-for="row in digitalBills" :key="row.id">
              <td><div class="font-medium">{{ row.invoiceNumber }}</div><div class="text-xs text-muted">{{ row.customerMobile || '-' }}</div></td>
              <td>{{ row.customerName || 'Walk-in Customer' }}</td>
              <td>{{ date(row.invoiceDate) }}</td>
              <td class="text-right">{{ money(row.amount) }}</td>
              <td><UBadge :color="row.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.isActive ? 'Active' : 'Disabled' }}</UBadge></td>
              <td><UBadge color="neutral" variant="subtle">{{ row.whatsAppStatus || 'NotSent' }}</UBadge></td>
              <td class="text-right text-xs">Open {{ row.openCount }} · PDF {{ row.pdfDownloadCount }} · Review {{ row.reviewClickCount }}</td>
              <td>
                <div class="inline-action-row justify-end">
                  <UButton size="xs" label="Open" icon="i-lucide-external-link" :to="row.publicPath" target="_blank" />
                  <UButton size="xs" label="Copy" icon="i-lucide-copy" variant="subtle" @click="copyLink(row)" />
                  <UButton size="xs" label="Activity" icon="i-lucide-activity" color="info" variant="subtle" @click="openActivity(row)" />
                  <UButton size="xs" label="WhatsApp" icon="i-lucide-send" color="primary" variant="subtle" @click="sendWhatsApp(row)" />
                  <UButton size="xs" label="Regenerate" icon="i-lucide-refresh-cw" color="warning" variant="subtle" @click="regenerate(row)" />
                  <UButton size="xs" label="Disable" icon="i-lucide-ban" color="error" variant="subtle" @click="disable(row)" />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>

    <UModal v-model:open="activityOpen" title="Digital Bill Activity" :ui="{ content: 'max-w-4xl' }">
      <template #body>
        <UAlert v-if="activityError" color="error" variant="soft" title="Activity load failed" :description="activityError" />
        <div v-else-if="activityLoading" class="space-y-3">
          <USkeleton class="h-20 w-full" />
          <USkeleton class="h-40 w-full" />
        </div>
        <div v-else-if="selectedActivity" class="space-y-4">
          <div class="planner-metric-grid">
            <UCard class="planner-metric-card">
              <div class="planner-metric-body"><UAvatar icon="i-lucide-eye" color="primary" variant="subtle" /><div><p>Opened</p><strong>{{ selectedActivity.totals?.openCount || 0 }}</strong><span>Public invoice views</span></div></div>
            </UCard>
            <UCard class="planner-metric-card">
              <div class="planner-metric-body"><UAvatar icon="i-lucide-file-down" color="success" variant="subtle" /><div><p>PDF</p><strong>{{ selectedActivity.totals?.pdfDownloadCount || 0 }}</strong><span>Downloads</span></div></div>
            </UCard>
            <UCard class="planner-metric-card">
              <div class="planner-metric-body"><UAvatar icon="i-lucide-star" color="warning" variant="subtle" /><div><p>Review</p><strong>{{ selectedActivity.totals?.reviewClickCount || 0 }}</strong><span>Google clicks</span></div></div>
            </UCard>
            <UCard class="planner-metric-card">
              <div class="planner-metric-body"><UAvatar icon="i-lucide-message-circle" color="info" variant="subtle" /><div><p>WhatsApp</p><strong>{{ selectedActivity.totals?.whatsAppLogCount || 0 }}</strong><span>Message logs</span></div></div>
            </UCard>
          </div>

          <UCard>
            <template #header>
              <div class="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <strong>{{ selectedActivity.summary?.invoiceNumber }}</strong>
                  <p class="text-xs text-muted">{{ selectedActivity.summary?.customerName || 'Walk-in Customer' }} · {{ selectedActivity.summary?.customerMobile || '-' }}</p>
                </div>
                <div class="inline-action-row">
                  <UButton size="xs" label="Open Bill" icon="i-lucide-external-link" :to="selectedActivity.summary?.publicPath" target="_blank" />
                  <UButton size="xs" label="Copy Link" icon="i-lucide-copy" variant="subtle" @click="copyLink(selectedActivity.summary)" />
                </div>
              </div>
            </template>

            <div class="space-y-3">
              <div v-for="entry in selectedActivity.timeline || []" :key="`${entry.at}-${entry.type}-${entry.title}`" class="rounded-lg border border-default p-3">
                <div class="flex flex-wrap items-center justify-between gap-2">
                  <div class="flex items-center gap-2">
                    <UBadge :color="eventColor(entry.type)" variant="subtle">{{ entry.type }}</UBadge>
                    <strong>{{ entry.title }}</strong>
                  </div>
                  <span class="text-xs text-muted">{{ date(entry.at) }}</span>
                </div>
                <p class="mt-1 text-sm text-muted">{{ entry.description }}</p>
                <p v-if="entry.targetUrl" class="mt-1 text-xs text-muted break-all">Target: {{ entry.targetUrl }}</p>
                <p v-if="entry.detail" class="mt-1 text-xs text-muted break-all">{{ entry.detail }}</p>
              </div>
              <UiCrudEmptyState v-if="!selectedActivity.timeline?.length" title="No activity yet" description="Customer events will appear here after the invoice link is opened." icon="i-lucide-activity" />
            </div>
          </UCard>

          <div class="grid gap-4 lg:grid-cols-2">
            <UCard>
              <template #header><strong>WhatsApp logs</strong></template>
              <div class="space-y-3">
                <div v-for="log in selectedActivity.whatsAppLogs || []" :key="log.id" class="rounded-lg border border-default p-3 text-sm">
                  <div class="flex items-center justify-between gap-2"><UBadge color="neutral" variant="subtle">{{ log.status }}</UBadge><span class="text-xs text-muted">{{ date(log.createdAt) }}</span></div>
                  <p class="mt-1 text-muted">{{ log.provider }} · retry {{ log.retryCount || 0 }}</p>
                  <p v-if="log.errorMessage" class="mt-1 text-error">{{ log.errorMessage }}</p>
                </div>
                <UiCrudEmptyState v-if="!selectedActivity.whatsAppLogs?.length" title="No WhatsApp log" description="Send or auto-send WhatsApp to see status history." icon="i-lucide-send" />
              </div>
            </UCard>

            <UCard>
              <template #header><strong>Feedback</strong></template>
              <div class="space-y-3">
                <div v-for="item in selectedActivity.feedback || []" :key="item.id" class="rounded-lg border border-default p-3 text-sm">
                  <div class="flex items-center justify-between gap-2"><UBadge color="warning" variant="subtle">{{ item.rating }}/5</UBadge><span class="text-xs text-muted">{{ date(item.submittedAt) }}</span></div>
                  <p class="mt-1">{{ item.message || 'No message given.' }}</p>
                </div>
                <UiCrudEmptyState v-if="!selectedActivity.feedback?.length" title="No feedback yet" description="Customer feedback from the public invoice page appears here." icon="i-lucide-message-square" />
              </div>
            </UCard>
          </div>
        </div>
      </template>
    </UModal>

  </AppShell>
</template>

<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const logs = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const page = ref(1)
const pageSize = 50
const total = ref(0)
const search = ref('')
const status = ref('all')

const statusOptions = ['all', 'Sent', 'Delivered', 'Read', 'Queued', 'ManualPending', 'Failed', 'Skipped', 'NotConfigured', 'Disabled', 'RetryLimitReached', 'WebhookReceived'].map((item) => ({ label: item, value: item }))
const metrics = computed(() => [
  { label: 'Logs', value: total.value, icon: 'i-lucide-list-checks', color: 'primary' },
  { label: 'Sent', value: logs.value.filter((item) => ['Sent', 'Delivered', 'Read'].includes(item.status)).length, icon: 'i-lucide-check-circle', color: 'success' },
  { label: 'Manual pending', value: logs.value.filter((item) => item.status === 'ManualPending').length, icon: 'i-lucide-hand', color: 'warning' },
  { label: 'Failed/skipped', value: logs.value.filter((item) => ['Failed', 'Skipped', 'NotConfigured', 'RetryLimitReached'].includes(item.status)).length, icon: 'i-lucide-triangle-alert', color: 'error' }
])

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (search.value.trim()) query.set('q', search.value.trim())
    if (status.value !== 'all') query.set('status', status.value)
    const response = await api.get<any>(`digital-bill-whatsapp-logs?${query}`)
    logs.value = response.items || []
    total.value = Number(response.total || logs.value.length)
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'WhatsApp logs load failed')
  } finally {
    loading.value = false
  }
}

async function retry(row: any) {
  loading.value = true
  try {
    const response = await api.create<any>(`digital-bill-whatsapp-logs/${row.id}/retry`, {})
    feedback.notify(`Retry result: ${response.status}`, response.errorMessage || response.messageBody, response.status === 'Sent' ? 'success' : 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Could not retry WhatsApp log', error)
  } finally {
    loading.value = false
  }
}

async function copyMessage(row: any) {
  await navigator.clipboard?.writeText(row.messageBody || '')
  feedback.notify('Message copied', undefined, 'success')
}

function date(value: string) { return value ? new Date(value).toLocaleString('en-IN') : '-' }
function statusColor(value: string) {
  if (['Sent', 'Delivered', 'Read'].includes(value)) return 'success'
  if (['Failed', 'Skipped', 'RetryLimitReached'].includes(value)) return 'error'
  if (['ManualPending', 'NotConfigured', 'Disabled'].includes(value)) return 'warning'
  return 'neutral'
}

onMounted(refresh)
</script>

<template>
  <AppShell title="WhatsApp Logs" @refresh="refresh">
    <UiModulePageHeader title="WhatsApp Logs" description="Track Digital Bill CRM WhatsApp sends, manual pending messages, provider errors, and retries." icon="i-lucide-message-square-text" />

    <div class="planner-metric-grid mt-4">
      <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
        <div class="planner-metric-body">
          <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
          <div><p>{{ metric.label }}</p><strong>{{ metric.value }}</strong><span>Current filtered page</span></div>
        </div>
      </UCard>
    </div>

    <UiRegisterPanel class="mt-4" title="WhatsApp message register" :description="`${logs.length} loaded of ${total}`" :loading="loading" :error="loadError" :empty="logs.length === 0" empty-title="No WhatsApp logs" empty-description="Send or test a WhatsApp digital bill first." empty-icon="i-lucide-message-circle" @retry="refresh">
      <template #actions>
        <UiCrudToolbar v-model:search="search" search-placeholder="Search mobile/message/error" :loading="loading" @refresh="refresh" />
        <USelect v-model="status" :items="statusOptions" class="w-48" />
        <UButton icon="i-lucide-search" label="Apply" @click="refresh" />
      </template>

      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead><tr><th>Invoice</th><th>Mobile</th><th>Provider</th><th>Status</th><th>Message</th><th>Date</th><th class="text-right">Actions</th></tr></thead>
          <tbody>
            <tr v-for="row in logs" :key="row.id">
              <td>{{ row.invoiceNumber || '-' }}</td>
              <td>{{ row.customerMobile || '-' }}</td>
              <td>{{ row.provider }}</td>
              <td><UBadge :color="statusColor(row.status)" variant="subtle">{{ row.status }}</UBadge><div v-if="row.errorMessage" class="mt-1 max-w-xs text-xs text-error">{{ row.errorMessage }}</div></td>
              <td class="max-w-md truncate">{{ row.messageBody }}</td>
              <td>{{ date(row.createdAt) }}</td>
              <td>
                <div class="inline-action-row justify-end">
                  <UButton size="xs" icon="i-lucide-copy" label="Copy" variant="subtle" @click="copyMessage(row)" />
                  <UButton size="xs" icon="i-lucide-refresh-cw" label="Retry" color="warning" variant="subtle" @click="retry(row)" />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>

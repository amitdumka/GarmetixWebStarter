<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-message-square-text" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">WhatsApp Logs</h2>
          <p class="garmetix-dashboard-subtitle">Invoice and campaign WhatsApp send attempts, provider responses and retry handoff.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-settings" @click="router.push('/marketing/whatsapp-settings')">Settings</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label"><UIcon :name="metric.icon" class="mr-1 inline size-4" /> {{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.value }}</p>
        <p class="garmetix-metric-caption">{{ metric.meta }}</p>
      </div>
    </section>

    <div class="garmetix-section-card grid gap-3 xl:grid-cols-[minmax(240px,1fr)_180px_130px_auto]">
      <UFormField label="Search">
        <UInput v-model="filters.q" icon="i-lucide-search" placeholder="Search mobile, provider, message or error" @keyup.enter="refresh" />
      </UFormField>
      <UFormField label="Status">
        <USelect v-model="filters.status" :items="statusItems" />
      </UFormField>
      <UFormField label="Rows">
        <USelect v-model="filters.pageSize" :items="pageSizeItems" />
      </UFormField>
      <div class="flex flex-wrap items-end gap-2">
        <UButton icon="i-lucide-search-check" :loading="loading" @click="runSearch">Search</UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-x" :disabled="!hasFilters" @click="clearFilters">Clear</UButton>
      </div>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[1240px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Mobile</th>
            <th class="border-b border-default p-3">Status</th>
            <th class="border-b border-default p-3">Provider</th>
            <th class="border-b border-default p-3">Invoice</th>
            <th class="border-b border-default p-3">Message</th>
            <th class="border-b border-default p-3">Error</th>
            <th class="border-b border-default p-3">Timeline</th>
            <th class="border-b border-default p-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="8" class="p-8 text-center text-muted">Loading WhatsApp logs...</td></tr>
          <tr v-else-if="!rows.length"><td colspan="8" class="p-8 text-center text-muted">No WhatsApp logs found.</td></tr>
          <template v-else>
            <tr v-for="row in rows" :key="String(row.id)">
              <td class="border-b border-default p-3">{{ readText(row, ['customerMobile']) }}</td>
              <td class="border-b border-default p-3"><UBadge :color="statusColor(readText(row, ['status']))" variant="subtle">{{ readText(row, ['status']) }}</UBadge></td>
              <td class="border-b border-default p-3">
                <p class="font-medium text-highlighted">{{ readText(row, ['provider']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['templateName'], 'No template') }}</p>
              </td>
              <td class="border-b border-default p-3">{{ readText(row, ['invoiceNumber']) }}</td>
              <td class="border-b border-default p-3">
                <p class="line-clamp-2 max-w-sm">{{ readText(row, ['messageBody']) }}</p>
              </td>
              <td class="border-b border-default p-3">
                <p class="line-clamp-2 max-w-xs text-error">{{ readText(row, ['errorMessage'], '') }}</p>
              </td>
              <td class="border-b border-default p-3 text-xs text-muted">
                <p>Created: {{ formatDate(readText(row, ['createdAt'], '')) }}</p>
                <p v-if="readText(row, ['sentAt'], '')">Sent: {{ formatDate(readText(row, ['sentAt'], '')) }}</p>
                <p v-if="readText(row, ['deliveredAt'], '')">Delivered: {{ formatDate(readText(row, ['deliveredAt'], '')) }}</p>
                <p v-if="readText(row, ['readAt'], '')">Read: {{ formatDate(readText(row, ['readAt'], '')) }}</p>
              </td>
              <td class="border-b border-default p-3">
                <div class="flex justify-end gap-2">
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-receipt-text" :disabled="!readText(row, ['invoiceNumber'], '')" @click="openBill(row)">Bill</UButton>
                  <UButton size="xs" icon="i-lucide-rotate-cw" :loading="retryingId === row.id" :disabled="!canRetry(row)" @click="retry(row)">Retry</UButton>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <div class="garmetix-section-card flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <p class="text-sm text-muted">Page {{ number(page) }} of {{ number(totalPages) }} | Total {{ number(total) }} log(s)</p>
      <div class="flex flex-wrap gap-2">
        <UButton color="neutral" variant="soft" icon="i-lucide-chevrons-left" :disabled="page <= 1" @click="goPage(1)">First</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="goPage(page - 1)">Previous</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="goPage(page + 1)">Next</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-chevrons-right" :disabled="page >= totalPages" @click="goPage(totalPages)">Last</UButton>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get, post } = useCrmApiClient()

const rows = ref<ApiRecord[]>([])
const total = ref(0)
const page = ref(1)
const loading = ref(false)
const error = ref('')
const retryingId = ref<unknown>(null)

const filters = reactive({
  q: '',
  status: 'all',
  pageSize: 50
})

const statusItems = [
  { label: 'All status', value: 'all' },
  { label: 'Queued', value: 'Queued' },
  { label: 'Sent', value: 'Sent' },
  { label: 'Delivered', value: 'Delivered' },
  { label: 'Read', value: 'Read' },
  { label: 'Failed', value: 'Failed' },
  { label: 'Skipped', value: 'Skipped' }
]

const pageSizeItems = [
  { label: '25', value: 25 },
  { label: '50', value: 50 },
  { label: '100', value: 100 },
  { label: '200', value: 200 }
]

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(filters.pageSize || 50))))
const hasFilters = computed(() => Boolean(filters.q.trim()) || filters.status !== 'all')
const failedCount = computed(() => rows.value.filter(row => canRetry(row)).length)

const metrics = computed(() => [
  { label: 'Visible Logs', value: number(rows.value.length), meta: `Page ${number(page.value)}`, icon: 'i-lucide-list' },
  { label: 'Total Logs', value: number(total.value), meta: 'Server filtered total', icon: 'i-lucide-database' },
  { label: 'Retry Ready', value: number(failedCount.value), meta: 'Failed/skipped visible rows', icon: 'i-lucide-rotate-cw' },
  { label: 'Page Size', value: number(Number(filters.pageSize)), meta: 'Rows requested', icon: 'i-lucide-rows-3' }
])

function number(value: number) {
  return new Intl.NumberFormat('en-IN').format(Number(value || 0))
}

function statusColor(status: string) {
  const normalized = status.toLowerCase()
  if (normalized.includes('fail') || normalized.includes('skip') || normalized.includes('limit')) return 'error' as const
  if (normalized.includes('sent') || normalized.includes('deliver') || normalized.includes('read')) return 'success' as const
  if (normalized.includes('queue')) return 'warning' as const
  return 'neutral' as const
}

function canRetry(row: ApiRecord) {
  const status = readText(row, ['status'], '').toLowerCase()
  return Boolean(readText(row, ['id'], '')) && (status.includes('fail') || status.includes('skip') || status.includes('queued'))
}

function clearFilters() {
  filters.q = ''
  filters.status = 'all'
  page.value = 1
  refresh()
}

function runSearch() {
  page.value = 1
  refresh()
}

function goPage(nextPage: number) {
  page.value = Math.min(Math.max(1, nextPage), totalPages.value)
  refresh()
}

function openBill(row: ApiRecord) {
  router.push(`/marketing/digital-bills?q=${encodeURIComponent(readText(row, ['invoiceNumber'], ''))}`)
}

async function retry(row: ApiRecord) {
  retryingId.value = row.id
  try {
    const result = await post<ApiRecord>(`digital-bill-whatsapp-logs/${row.id}/retry`, {})
    toast.add({ title: 'WhatsApp retry submitted', description: readText(result, ['status']), color: 'success' })
    await refresh()
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'Could not retry WhatsApp log.')
    toast.add({ title: 'Could not retry WhatsApp log', description: message, color: 'error' })
  } finally {
    retryingId.value = null
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const data = await get<ApiRecord>('digital-bill-whatsapp-logs', {
      page: page.value,
      pageSize: filters.pageSize,
      status: filters.status,
      q: filters.q
    })
    rows.value = toRows(data)
    total.value = readNumber(data, ['total'])
    page.value = readNumber(data, ['page']) || page.value
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load WhatsApp logs.')
    toast.add({ title: 'Could not load WhatsApp logs', description: error.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

watch(() => filters.pageSize, () => {
  page.value = 1
  refresh()
})

onMounted(refresh)
useHead({ title: 'WhatsApp Logs - Garmetix CRM' })
</script>

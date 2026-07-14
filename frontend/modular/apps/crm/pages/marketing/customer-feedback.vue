<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-message-square-heart" class="size-4" /> Digital CRM</p>
          <h2 class="garmetix-dashboard-title">Customer Feedback</h2>
          <p class="garmetix-dashboard-subtitle">Private customer feedback submitted from public digital bill pages.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-megaphone" @click="router.push('/marketing/campaign-audiences?segment=lowFeedback')">Low Feedback Audience</UButton>
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

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(240px,1fr)_180px_auto]">
      <UFormField label="Search">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search customer, mobile, invoice or message" />
      </UFormField>
      <UFormField label="Rating">
        <USelect v-model="ratingFilter" :items="ratingItems" />
      </UFormField>
      <div class="flex flex-wrap items-end gap-2">
        <UBadge color="neutral" variant="soft">{{ filteredRows.length }} feedback row(s)</UBadge>
        <UButton color="neutral" variant="ghost" icon="i-lucide-x" :disabled="!hasFilters" @click="clearFilters">Clear</UButton>
      </div>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[1040px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Customer</th>
            <th class="border-b border-default p-3">Invoice</th>
            <th class="border-b border-default p-3 text-right">Rating</th>
            <th class="border-b border-default p-3">Message</th>
            <th class="border-b border-default p-3">Submitted</th>
            <th class="border-b border-default p-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="6" class="p-8 text-center text-muted">Loading feedback...</td></tr>
          <tr v-else-if="!filteredRows.length"><td colspan="6" class="p-8 text-center text-muted">No feedback found.</td></tr>
          <template v-else>
            <tr v-for="row in filteredRows" :key="String(row.id)">
              <td class="border-b border-default p-3">
                <p class="font-semibold text-highlighted">{{ readText(row, ['customerName'], 'Customer') }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['customerMobile'], '') }}</p>
              </td>
              <td class="border-b border-default p-3">{{ readText(row, ['invoiceNumber']) }}</td>
              <td class="border-b border-default p-3 text-right">
                <UBadge :color="ratingColor(readNumber(row, ['rating']))" variant="subtle">{{ readNumber(row, ['rating']) }}/5</UBadge>
              </td>
              <td class="border-b border-default p-3">{{ readText(row, ['message'], 'No message') }}</td>
              <td class="border-b border-default p-3">{{ formatDate(readText(row, ['submittedAt'], '')) }}</td>
              <td class="border-b border-default p-3">
                <div class="flex justify-end gap-2">
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-receipt-text" @click="openBill(row)">Bill</UButton>
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-copy" @click="copyMobile(row)">Copy Mobile</UButton>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get } = useCrmApiClient()

const rows = ref<ApiRecord[]>([])
const loading = ref(false)
const error = ref('')
const search = ref('')
const ratingFilter = ref('all')

const ratingItems = [
  { label: 'All ratings', value: 'all' },
  { label: 'Low: 1-3', value: 'low' },
  { label: 'Good: 4-5', value: 'good' },
  { label: '5 star only', value: 'five' }
]

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rows.value.filter((row) => {
    if (term && !JSON.stringify(row).toLowerCase().includes(term)) return false
    const rating = readNumber(row, ['rating'])
    if (ratingFilter.value === 'low' && rating > 3) return false
    if (ratingFilter.value === 'good' && rating < 4) return false
    if (ratingFilter.value === 'five' && rating !== 5) return false
    return true
  })
})

const hasFilters = computed(() => Boolean(search.value.trim()) || ratingFilter.value !== 'all')
const lowRows = computed(() => rows.value.filter(row => readNumber(row, ['rating']) > 0 && readNumber(row, ['rating']) <= 3))
const averageRating = computed(() => {
  if (!rows.value.length) return 0
  return rows.value.reduce((sum, row) => sum + readNumber(row, ['rating']), 0) / rows.value.length
})

const metrics = computed(() => [
  { label: 'Feedback', value: number(rows.value.length), meta: 'Latest responses', icon: 'i-lucide-message-square-heart' },
  { label: 'Low Rating', value: number(lowRows.value.length), meta: 'Needs owner follow-up', icon: 'i-lucide-triangle-alert' },
  { label: 'Average', value: averageRating.value.toFixed(1), meta: 'Average customer score', icon: 'i-lucide-star' },
  { label: 'Filtered', value: number(filteredRows.value.length), meta: 'Visible after filters', icon: 'i-lucide-filter' }
])

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function ratingColor(value: number) {
  if (value <= 2) return 'error' as const
  if (value <= 3) return 'warning' as const
  if (value >= 5) return 'success' as const
  return 'primary' as const
}

function clearFilters() {
  search.value = ''
  ratingFilter.value = 'all'
}

function openBill(row: ApiRecord) {
  router.push(`/marketing/digital-bills?q=${encodeURIComponent(readText(row, ['invoiceNumber'], ''))}`)
}

async function copyMobile(row: ApiRecord) {
  const mobile = readText(row, ['customerMobile'], '')
  if (navigator?.clipboard && mobile) await navigator.clipboard.writeText(mobile)
  toast.add({ title: 'Mobile copied', description: mobile, color: 'success' })
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    rows.value = toRows(await get<unknown>('digital-bills/feedback'))
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load feedback.')
    toast.add({ title: 'Could not load feedback', description: error.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
useHead({ title: 'Customer Feedback - Garmetix CRM' })
</script>

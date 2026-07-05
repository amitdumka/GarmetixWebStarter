<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon :name="icon" class="size-4" /> {{ eyebrow }}</p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ description }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge color="success" variant="subtle">Read-only foundation</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section v-if="summaryCards.length" class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.caption }}</p>
      </div>
    </section>

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_auto]">
      <UFormField label="Search" :name="`${title}-search`">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search current rows" />
      </UFormField>
      <div class="flex items-end">
        <UBadge color="neutral" variant="soft">{{ filteredRows.length }} row(s)</UBadge>
      </div>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[900px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">{{ tableTitle }}</th>
            <th v-for="column in columns" :key="column.label" class="border-b border-default p-3">{{ column.label }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!filteredRows.length">
            <td :colspan="columns.length + 1" class="p-8 text-center text-muted">No rows returned yet.</td>
          </tr>
          <tr v-for="(row, index) in filteredRows.slice(0, take)" :key="rowKey(row, index)">
            <td class="border-b border-default p-3">
              <p class="font-semibold text-highlighted">{{ rowTitle(row, index) }}</p>
              <p class="text-xs text-muted">{{ rowSubtitle(row) }}</p>
            </td>
            <td v-for="column in columns" :key="column.label" class="border-b border-default p-3">
              {{ columnValue(row, column) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useCrmApiClient } from '../utils/crm-api'

type ColumnType = 'text' | 'money' | 'number' | 'date'

const props = withDefaults(defineProps<{
  title: string
  description: string
  endpoint: string
  eyebrow?: string
  icon?: string
  tableTitle?: string
  rowKeys?: string[]
  titleKeys?: string[]
  subtitleKeys?: string[]
  columns?: Array<{ label: string, keys: string[], type?: ColumnType }>
  summary?: Array<{ label: string, keys: string[], type?: ColumnType, caption?: string }>
  query?: Record<string, string | number | boolean | null | undefined>
  rowArrayKeys?: string[]
  take?: number
}>(), {
  eyebrow: 'CRM read model',
  icon: 'i-lucide-users',
  tableTitle: 'Record',
  rowKeys: () => ['id', 'customerId', 'digitalInvoiceId', 'invoiceNumber', 'name', 'mobileNumber'],
  titleKeys: () => ['name', 'customerName', 'invoiceNumber', 'campaignName', 'messageTitle', 'label', 'storeName'],
  subtitleKeys: () => ['mobileNumber', 'customerMobile', 'gstin', 'publicPath', 'status', 'whatsAppStatus'],
  columns: () => [],
  summary: () => [],
  query: () => ({}),
  rowArrayKeys: () => ['items', 'rows', 'data', 'results'],
  take: 50
})

const { get } = useCrmApiClient()
const loading = ref(false)
const error = ref('')
const data = ref<unknown>(null)
const search = ref('')

const rows = computed(() => toRows(data.value, props.rowArrayKeys))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return rows.value
  return rows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const summaryCards = computed(() => props.summary.map(item => ({
  label: item.label,
  value: formatField(data.value as ApiRecord | null, item.keys, item.type),
  caption: item.caption ?? ''
})))

function rowKey(row: ApiRecord, index: number) {
  return readText(row, props.rowKeys, String(index))
}

function rowTitle(row: ApiRecord, index: number) {
  return readText(row, props.titleKeys, `Row ${index + 1}`)
}

function rowSubtitle(row: ApiRecord) {
  return readText(row, props.subtitleKeys)
}

function columnValue(row: ApiRecord, column: { keys: string[], type?: ColumnType }) {
  return formatField(row, column.keys, column.type)
}

function formatField(source: ApiRecord | null | undefined, keys: string[], type: ColumnType = 'text') {
  if (type === 'money') return formatIndianMoney(readNumber(source, keys))
  if (type === 'number') return new Intl.NumberFormat('en-IN').format(readNumber(source, keys))
  if (type === 'date') return formatDate(readText(source, keys, ''))
  return readText(source, keys)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<unknown>(props.endpoint, props.query)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to load ${props.title}.`
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

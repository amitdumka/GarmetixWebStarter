<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Back Office read model</p>
          <h2 class="mt-1 text-2xl font-semibold">{{ title }}</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">{{ description }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge color="success" variant="subtle">Read-only</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section v-if="summaryCards.length" class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in summaryCards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.caption }}</p>
      </div>
    </section>

    <div class="border border-default bg-muted/10">
      <div class="flex flex-col gap-3 border-b border-default px-4 py-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h3 class="text-base font-semibold">{{ tableTitle }}</h3>
          <p class="text-xs text-muted">{{ rows.length }} row(s) loaded from {{ endpoint }}</p>
        </div>
        <UBadge color="neutral" variant="soft">{{ rows.length }}</UBadge>
      </div>

      <div v-if="rows.length" class="divide-y divide-default">
        <div v-for="(row, index) in rows.slice(0, take)" :key="rowKey(row, index)" class="grid gap-3 px-4 py-3 lg:grid-cols-[minmax(0,1.2fr)_repeat(3,minmax(0,0.65fr))]">
          <div class="min-w-0">
            <p class="truncate text-sm font-medium">{{ rowTitle(row, index) }}</p>
            <p class="mt-1 truncate text-xs text-muted">{{ rowSubtitle(row) }}</p>
          </div>
          <div v-for="column in columns" :key="column.label" class="min-w-0">
            <p class="text-xs text-muted">{{ column.label }}</p>
            <p class="mt-1 truncate text-sm font-semibold">{{ columnValue(row, column) }}</p>
          </div>
        </div>
      </div>

      <p v-else class="px-4 py-6 text-sm text-muted">No rows returned yet.</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../utils/main-api'

type ColumnType = 'text' | 'money' | 'number' | 'date'

const props = withDefaults(defineProps<{
  title: string
  description: string
  endpoint: string
  tableTitle?: string
  rowKeys?: string[]
  titleKeys?: string[]
  subtitleKeys?: string[]
  columns?: Array<{ label: string, keys: string[], type?: ColumnType }>
  summary?: Array<{ label: string, keys: string[], type?: ColumnType, caption?: string }>
  query?: Record<string, string | number | boolean | null | undefined>
  take?: number
}>(), {
  tableTitle: 'Rows',
  rowKeys: () => ['id', 'stockId', 'productId', 'invoiceNumber', 'inwardNumber', 'barcode'],
  titleKeys: () => ['invoiceNumber', 'inwardNumber', 'productName', 'name', 'customerName', 'vendorName', 'label'],
  subtitleKeys: () => ['customerMobileNumber', 'mobileNumber', 'vendorGstin', 'barcode', 'categoryName', 'status', 'invoiceStatus'],
  columns: () => [],
  summary: () => [],
  query: () => ({}),
  take: 25
})

const { get } = useMainApiClient()
const loading = ref(false)
const error = ref('')
const data = ref<unknown>(null)

const rows = computed(() => toRows(data.value))
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

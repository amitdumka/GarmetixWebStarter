<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-brain-circuit" class="size-4" />
            AI Sense read model
          </p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ description }}</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="readText(metric, ['label'])" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ readText(metric, ['label']) }}</p>
        <p class="garmetix-metric-value">{{ readText(metric, ['displayValue'], formatValue(readNumber(metric, ['value']))) }}</p>
        <p class="garmetix-metric-caption">{{ readText(metric, ['caption']) }}</p>
      </div>
      <div v-if="!metrics.length" class="garmetix-metric-card">
        <p class="garmetix-metric-label">Endpoint</p>
        <p class="garmetix-metric-value text-lg">Ready</p>
        <p class="garmetix-metric-caption">No metrics returned yet.</p>
      </div>
    </div>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Rows</h3>
        <div v-if="rows.length" class="garmetix-list-stack">
          <div v-for="(row, index) in rows.slice(0, 12)" :key="rowKey(row, index)" class="garmetix-row-card">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="truncate text-sm font-medium">{{ rowTitle(row, index) }}</p>
                <p class="mt-1 text-xs text-muted">{{ rowSubtitle(row) }}</p>
              </div>
              <p class="shrink-0 text-sm font-semibold">{{ rowAmount(row) }}</p>
            </div>
          </div>
        </div>
        <p v-else class="mt-3 text-sm text-muted">No detail rows returned.</p>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Signals</h3>
        <div v-if="signals.length" class="garmetix-list-stack">
          <div v-for="(signal, index) in signals.slice(0, 12)" :key="rowKey(signal, index)" class="garmetix-row-card">
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="text-sm font-medium">{{ readText(signal, ['label', 'ageBucket', 'status'], `Signal ${index + 1}`) }}</p>
                <p class="text-xs text-muted">{{ readText(signal, ['description', 'status', 'caption']) }}</p>
              </div>
              <p class="text-sm font-semibold">{{ readText(signal, ['value'], formatValue(readNumber(signal, ['dueAmount', 'amount']))) }}</p>
            </div>
          </div>
        </div>
        <p v-else class="mt-3 text-sm text-muted">No risk signals returned.</p>
      </div>
    </section>

    <div class="garmetix-section-card">
      <h3 class="garmetix-panel-title">Trend</h3>
      <div v-if="trend.length" class="garmetix-trend-grid">
        <div v-for="point in trend.slice(-12)" :key="readText(point, ['label', 'date'])" class="garmetix-row-card">
          <p class="text-xs text-muted">{{ readText(point, ['label', 'date']) }}</p>
          <p class="mt-1 text-sm font-semibold">{{ formatValue(readNumber(point, ['sales'])) }}</p>
          <p class="text-xs text-muted">Profit {{ formatValue(readNumber(point, ['profit'])) }}</p>
        </div>
      </div>
      <p v-else class="mt-3 text-sm text-muted">No trend points returned.</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, type ApiRecord, useAiApiClient } from '../utils/ai-api'

const props = defineProps({
  title: {
    type: String,
    required: true
  },
  description: {
    type: String,
    required: true
  },
  endpoint: {
    type: String,
    required: true
  }
})

const { get } = useAiApiClient()
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)

const metrics = computed(() => readArray(data.value, ['metrics']))
const rows = computed(() => readArray(data.value, ['rows', 'storeGroups']))
const signals = computed(() => readArray(data.value, ['signals']))
const trend = computed(() => readArray(data.value, ['trend']))

function formatValue(value: number) {
  return formatIndianMoney(value)
}

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id', 'storeId', 'storeGroupId', 'partyId'], String(index))
}

function rowTitle(row: ApiRecord, index: number) {
  return readText(row, ['storeName', 'storeGroupName', 'partyName', 'title', 'label'], `Row ${index + 1}`)
}

function rowSubtitle(row: ApiRecord) {
  return readText(row, ['subtitle', 'ageBucket', 'sellThroughSignal', 'contact', 'status'])
}

function rowAmount(row: ApiRecord) {
  const value = readNumber(row, ['amount', 'sales', 'purchase', 'grossMargin', 'stockValue', 'dueAmount', 'salesMonth', 'purchaseMonth'])
  return formatValue(value)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<ApiRecord>(props.endpoint)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to load ${props.title}.`
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

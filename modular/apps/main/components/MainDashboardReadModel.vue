<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Back Office read model</p>
          <h2 class="mt-1 text-2xl font-semibold">{{ effectiveTitle }}</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">{{ effectiveDescription }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge color="success" variant="subtle">Read-only</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="readText(metric, ['label'])" class="border border-default bg-muted/20 p-4">
        <div class="flex items-center justify-between gap-3">
          <p class="truncate text-sm text-muted">{{ readText(metric, ['label']) }}</p>
          <UBadge size="xs" :color="metricColor(metric)" variant="soft">
            <UIcon :name="readText(metric, ['icon'], 'i-lucide-activity')" class="size-3" />
          </UBadge>
        </div>
        <p class="mt-2 text-2xl font-semibold">{{ readText(metric, ['displayValue'], formatValue(readNumber(metric, ['value']))) }}</p>
        <p class="mt-1 line-clamp-2 text-xs text-muted">{{ readText(metric, ['caption']) }}</p>
      </div>
      <div v-if="!metrics.length" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">Endpoint</p>
        <p class="mt-2 text-lg font-semibold">No metrics</p>
        <p class="mt-1 text-xs text-muted">The endpoint responded but did not return metric cards.</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(0,0.8fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="text-base font-semibold">{{ effectivePrimaryTitle }}</h3>
        <div v-if="primaryRows.length" class="mt-3 space-y-2">
          <div v-for="(row, index) in primaryRows.slice(0, 10)" :key="rowKey(row, index)" class="border border-default bg-default/40 p-3">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="truncate text-sm font-medium">{{ rowTitle(row, index) }}</p>
                <p class="mt-1 text-xs text-muted">{{ rowSubtitle(row) }}</p>
              </div>
              <p class="shrink-0 text-sm font-semibold">{{ rowAmount(row) }}</p>
            </div>
          </div>
        </div>
        <p v-else class="mt-3 text-sm text-muted">No rows returned.</p>
      </div>

      <div class="space-y-4">
        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">{{ effectiveSecondaryTitle }}</h3>
          <div v-if="secondaryRows.length" class="mt-3 space-y-2">
            <div v-for="(row, index) in secondaryRows.slice(0, 8)" :key="rowKey(row, index)" class="border border-default bg-default/40 p-3">
              <div class="flex items-center justify-between gap-3">
                <div class="min-w-0">
                  <p class="truncate text-sm font-medium">{{ rowTitle(row, index) }}</p>
                  <p class="truncate text-xs text-muted">{{ rowSubtitle(row) }}</p>
                </div>
                <p class="shrink-0 text-sm font-semibold">{{ rowAmount(row) }}</p>
              </div>
            </div>
          </div>
          <p v-else class="mt-3 text-sm text-muted">No secondary rows returned.</p>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Signals</h3>
          <div v-if="signals.length" class="mt-3 space-y-2">
            <div v-for="(signal, index) in signals.slice(0, 8)" :key="rowKey(signal, index)" class="border border-default bg-default/40 p-3">
              <div class="flex items-start justify-between gap-3">
                <div>
                  <p class="text-sm font-medium">{{ readText(signal, ['label', 'status'], `Signal ${index + 1}`) }}</p>
                  <p class="text-xs text-muted">{{ readText(signal, ['description', 'caption', 'status']) }}</p>
                </div>
                <UBadge size="xs" :color="metricColor(signal)" variant="soft">{{ readText(signal, ['value', 'status']) }}</UBadge>
              </div>
            </div>
          </div>
          <p v-else class="mt-3 text-sm text-muted">No signals returned.</p>
        </div>
      </div>
    </section>

    <div class="border border-default bg-muted/10 p-4">
      <h3 class="text-base font-semibold">Trend</h3>
      <div v-if="trend.length" class="mt-3 grid gap-2 md:grid-cols-3 xl:grid-cols-6">
        <div v-for="point in trend.slice(-12)" :key="readText(point, ['label', 'date'])" class="border border-default bg-default/40 p-3">
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
import { readArray, readNumber, readText, type ApiRecord, useMainApiClient } from '../utils/main-api'

const props = withDefaults(defineProps<{
  title?: string
  description?: string
  endpoint?: string
  primaryKeys?: string[]
  secondaryKeys?: string[]
  signalKeys?: string[]
  trendKeys?: string[]
  primaryTitle?: string
  secondaryTitle?: string
}>(), {
  primaryKeys: () => ['recentSales', 'recentActivities', 'stores', 'storeGroups', 'workQueue'],
  secondaryKeys: () => ['recentPurchases', 'stockAlerts', 'quickActions', 'adminQueue'],
  signalKeys: () => ['healthSignals', 'signals'],
  trendKeys: () => ['trend', 'salesTrend'],
  primaryTitle: 'Primary Rows',
  secondaryTitle: 'Secondary Rows'
})

const route = useRoute()
const { get } = useMainApiClient()
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)

const fallbackConfig = computed(() => {
  const path = route.path.replace(/\/+$/, '') || '/'
  if (path === '/dashboard/todays') {
    return {
      title: "Today's Dashboard",
      description: 'Daily Back Office view for store activity, collections, stock movement and pending reviews.',
      endpoint: 'dashboard/todays',
      primaryKeys: ['recentActivities'],
      secondaryKeys: ['quickActions'],
      signalKeys: ['attendance'],
      trendKeys: ['salesTrend'],
      primaryTitle: 'Recent Activity',
      secondaryTitle: 'Quick Actions'
    }
  }

  if (path === '/dashboard/store-manager') {
    return {
      title: 'Store Manager Dashboard',
      description: 'Manager route for store checklist, team activity and operations exceptions.',
      endpoint: 'dashboard/store-manager',
      primaryKeys: ['recentSales', 'workQueue'],
      secondaryKeys: ['stockAlerts', 'quickActions'],
      signalKeys: ['healthSignals', 'signals'],
      trendKeys: ['trend', 'salesTrend'],
      primaryTitle: 'Recent Sales',
      secondaryTitle: 'Stock Alerts'
    }
  }

  return {
    title: path === '/reports' ? 'Reports Dashboard' : 'Back Office Dashboard',
    description: path === '/reports'
      ? 'Business reporting summary for store performance, sales, purchases, dues and operational signals.'
      : 'Lean landing page for store operations, purchase, inventory, customers and reports. Heavy POS, HR, Books, AI Sense and Admin screens stay in their own modular apps.',
    endpoint: 'dashboard/business',
    primaryKeys: ['stores', 'storeGroups'],
    secondaryKeys: ['recentSales', 'recentPurchases', 'quickActions'],
    signalKeys: ['healthSignals', 'signals'],
    trendKeys: ['trend', 'salesTrend'],
    primaryTitle: 'Store Performance',
    secondaryTitle: 'Recent Activity'
  }
})

const effectiveTitle = computed(() => props.title || fallbackConfig.value.title)
const effectiveDescription = computed(() => props.description || fallbackConfig.value.description)
const effectiveEndpoint = computed(() => props.endpoint || fallbackConfig.value.endpoint)
const effectivePrimaryKeys = computed(() => props.primaryKeys?.length ? props.primaryKeys : fallbackConfig.value.primaryKeys)
const effectiveSecondaryKeys = computed(() => props.secondaryKeys?.length ? props.secondaryKeys : fallbackConfig.value.secondaryKeys)
const effectiveSignalKeys = computed(() => props.signalKeys?.length ? props.signalKeys : fallbackConfig.value.signalKeys)
const effectiveTrendKeys = computed(() => props.trendKeys?.length ? props.trendKeys : fallbackConfig.value.trendKeys)
const effectivePrimaryTitle = computed(() => props.primaryTitle || fallbackConfig.value.primaryTitle)
const effectiveSecondaryTitle = computed(() => props.secondaryTitle || fallbackConfig.value.secondaryTitle)

const metrics = computed(() => readArray(data.value, ['metrics']))
const primaryRows = computed(() => readArray(data.value, effectivePrimaryKeys.value))
const secondaryRows = computed(() => readArray(data.value, effectiveSecondaryKeys.value))
const signals = computed(() => readArray(data.value, effectiveSignalKeys.value))
const trend = computed(() => readArray(data.value, effectiveTrendKeys.value))

function formatValue(value: number) {
  return formatIndianMoney(value)
}

function metricColor(row: ApiRecord) {
  const color = readText(row, ['color'], 'neutral')
  return ['primary', 'success', 'warning', 'error', 'neutral'].includes(color) ? color as 'primary' | 'success' | 'warning' | 'error' | 'neutral' : 'neutral'
}

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id', 'storeId', 'storeGroupId', 'resourceId', 'partyId', 'label'], String(index))
}

function rowTitle(row: ApiRecord, index: number) {
  return readText(row, ['title', 'storeName', 'storeGroupName', 'label', 'partyName'], `Row ${index + 1}`)
}

function rowSubtitle(row: ApiRecord) {
  return readText(row, ['subtitle', 'status', 'caption', 'description', 'contact'])
}

function rowAmount(row: ApiRecord) {
  const explicit = readText(row, ['amount'], '')
  if (explicit && Number.isNaN(Number(explicit))) return explicit
  const value = readNumber(row, ['amount', 'salesMonth', 'sales', 'purchaseMonth', 'stockValue', 'dueAmount', 'currentStockQty'])
  return formatValue(value)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<ApiRecord>(effectiveEndpoint.value)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to load ${effectiveTitle.value}.`
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

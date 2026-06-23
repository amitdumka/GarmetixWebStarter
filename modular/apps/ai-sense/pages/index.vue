<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">AI Sense command center</p>
          <h2 class="mt-1 text-2xl font-semibold">Business Intelligence</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            A lean analytics workspace for owner, admin, accountant, and power-user views. This stage reads existing dashboard and stock APIs.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/dashboard/business" icon="i-lucide-chart-no-axes-combined">Business</UButton>
          <UButton to="/stock-reports" color="neutral" variant="soft" icon="i-lucide-package-search">Stock Risk</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Cash Flow Signals</h3>
          <UBadge :color="loading ? 'warning' : 'primary'" variant="subtle">{{ loading ? 'Loading' : 'Live API' }}</UBadge>
        </div>
        <div class="grid gap-2 sm:grid-cols-2">
          <div v-for="item in cashCards" :key="item.label" class="border border-default bg-default/40 p-3">
            <p class="text-xs text-muted">{{ item.label }}</p>
            <p class="mt-1 text-base font-semibold">{{ item.value }}</p>
          </div>
        </div>
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Route Coverage</h3>
          <UButton to="/ai-sense/sales-analysis" size="sm" color="neutral" variant="ghost" icon="i-lucide-arrow-right">Open</UButton>
        </div>
        <div class="grid gap-2 sm:grid-cols-2">
          <UButton v-for="item in quickLinks" :key="item.href" :to="item.href" :icon="item.icon" color="neutral" variant="soft" class="justify-start">
            {{ item.label }}
          </UButton>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, type ApiRecord, useAiApiClient } from '../utils/ai-api'

useHead({ title: 'AI Sense - Garmetix AI Sense' })

const { get } = useAiApiClient()
const loading = ref(true)
const error = ref('')
const dashboard = ref<ApiRecord | null>(null)
const stock = ref<ApiRecord | null>(null)
const cash = computed(() => dashboard.value?.cashPaymentSummary as ApiRecord | undefined)
const cards = computed(() => [
  { label: 'Sales', value: formatIndianMoney(readNumber(dashboard.value, ['salesMonth', 'sales', 'totalSales'])), detail: 'Business dashboard sales signal' },
  { label: 'Purchase', value: formatIndianMoney(readNumber(dashboard.value, ['purchaseMonth', 'purchase', 'totalPurchase'])), detail: 'Purchase volume signal' },
  { label: 'Stock Value', value: formatIndianMoney(readNumber(dashboard.value, ['stockValue', 'totalStockValue'])), detail: 'Current stock valuation' },
  { label: 'Low Stock', value: readNumber(stock.value, ['lowStockCount', 'lowStockItems']), detail: 'Inventory stock report risk rows' }
])
const cashCards = computed(() => [
  { label: 'Cash In', value: formatIndianMoney(readNumber(cash.value, ['cashIn'])) },
  { label: 'Cash Out', value: formatIndianMoney(readNumber(cash.value, ['cashOut'])) },
  { label: 'Bank In', value: formatIndianMoney(readNumber(cash.value, ['bankIn'])) },
  { label: 'Net Cash', value: formatIndianMoney(readNumber(cash.value, ['netCash'])) }
])
const quickLinks = [
  { label: 'Sales Analysis', href: '/ai-sense/sales-analysis', icon: 'i-lucide-trending-up' },
  { label: 'Purchase Analysis', href: '/ai-sense/purchase-analysis', icon: 'i-lucide-chart-column-increasing' },
  { label: 'Profit Analysis', href: '/ai-sense/profit-analysis', icon: 'i-lucide-chart-pie' },
  { label: 'Daily Summary', href: '/ai-sense/daily-summary', icon: 'i-lucide-calendar-days' }
]

onMounted(async () => {
  loading.value = true
  error.value = ''
  try {
    const [businessData, stockData] = await Promise.all([
      get<ApiRecord>('api/dashboard/business'),
      get<ApiRecord>('api/inventory/stock-reports/summary', { lowStockThreshold: 5 })
    ])
    dashboard.value = businessData
    stock.value = stockData
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load AI Sense dashboard.'
  } finally {
    loading.value = false
  }
})
</script>

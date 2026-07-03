<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-chart-no-axes-combined" class="size-4" />
            Business dashboard
          </p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ subtitle }}</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </div>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Store Performance</h3>
            <p class="garmetix-panel-subtitle">{{ stores.length }} store row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[760px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Store</th>
                <th class="px-3 py-2">Sales</th>
                <th class="px-3 py-2">Purchase</th>
                <th class="px-3 py-2">Stock</th>
                <th class="px-3 py-2">Invoices</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in stores" :key="readText(row, ['storeId', 'storeName'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(row, ['storeName', 'name']) }}</td>
                <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['salesMonth', 'sales'])) }}</td>
                <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['purchaseMonth', 'purchase'])) }}</td>
                <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['stockValue'])) }}</td>
                <td class="px-3 py-2">{{ readNumber(row, ['invoiceCount']) }}</td>
              </tr>
              <tr v-if="!stores.length">
                <td class="px-3 py-6 text-center text-muted" colspan="5">No store performance rows returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Due Signals</h3>
            <p class="garmetix-panel-subtitle">Customer, vendor and movement follow-up</p>
          </div>
        </div>
        <div class="garmetix-list-stack p-4">
          <div v-for="item in dueCards" :key="item.label" class="garmetix-row-card flex items-center justify-between gap-3">
            <div>
              <p class="text-sm font-medium">{{ item.label }}</p>
              <p class="text-xs text-muted">{{ item.detail }}</p>
            </div>
            <p class="text-base font-semibold">{{ item.value }}</p>
          </div>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, type ApiRecord, useAiApiClient } from '../../utils/ai-api'

useHead({ title: 'Business Dashboard - Garmetix AI Sense' })

const { get } = useAiApiClient()
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)
const stores = computed(() => readArray(data.value, ['stores', 'Stores']))
const scope = computed(() => data.value?.scope as ApiRecord | undefined)
const period = computed(() => data.value?.period as ApiRecord | undefined)
const title = computed(() => readText(scope.value, ['companyName'], 'Company Dashboard'))
const subtitle = computed(() => readText(period.value, ['label'], 'Company, store-group and store performance overview.'))
const cards = computed(() => [
  { label: 'Sales', value: formatIndianMoney(readNumber(data.value, ['salesMonth', 'sales', 'totalSales'])), detail: 'Selected period sales' },
  { label: 'Purchase', value: formatIndianMoney(readNumber(data.value, ['purchaseMonth', 'purchase', 'totalPurchase'])), detail: 'Selected period purchase' },
  { label: 'Stock Value', value: formatIndianMoney(readNumber(data.value, ['stockValue', 'totalStockValue'])), detail: 'Current stock valuation' },
  { label: 'Invoices', value: readNumber(data.value, ['invoiceCount', 'invoices']), detail: 'Selected period invoice count' }
])
const dueCards = computed(() => [
  { label: 'Customer Dues', value: readArray(data.value, ['customerDues']).length, detail: 'Rows needing customer follow-up' },
  { label: 'Vendor Dues', value: readArray(data.value, ['vendorDues']).length, detail: 'Rows needing vendor payment review' },
  { label: 'Recent Sales', value: readArray(data.value, ['recentSales']).length, detail: 'Recent sales rows returned' },
  { label: 'Recent Purchases', value: readArray(data.value, ['recentPurchases']).length, detail: 'Recent purchase rows returned' }
])

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<ApiRecord>('api/dashboard/business')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load business dashboard.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

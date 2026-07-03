<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="text-sm text-muted">Inventory intelligence</p>
          <h2 class="mt-1 text-2xl font-semibold">Stock Risk</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Low-stock and valuation signals from the existing stock report API.</p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="load">
          <UFormField label="Low stock threshold" name="threshold">
            <UInput v-model.number="threshold" type="number" min="0" />
          </UFormField>
          <UButton type="submit" icon="i-lucide-refresh-cw" :loading="loading">Load</UButton>
        </form>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </div>

    <div class="border border-default bg-muted/10 p-4">
      <h3 class="text-base font-semibold">Stock Report Payload</h3>
      <pre class="mt-3 max-h-[520px] overflow-auto border border-default bg-default/40 p-3 text-xs">{{ formattedSummary }}</pre>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, type ApiRecord, useAiApiClient } from '../utils/ai-api'

useHead({ title: 'Stock Risk - Garmetix AI Sense' })

const { get } = useAiApiClient()
const threshold = ref(5)
const loading = ref(false)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const cards = computed(() => [
  { label: 'Products', value: readNumber(summary.value, ['productCount', 'products']), detail: 'Products covered by report' },
  { label: 'Low Stock', value: readNumber(summary.value, ['lowStockCount', 'lowStockItems']), detail: 'Rows below threshold' },
  { label: 'Stock Value', value: formatIndianMoney(readNumber(summary.value, ['stockValue', 'totalStockValue'])), detail: 'Current valuation signal' },
  { label: 'Mismatch', value: readNumber(summary.value, ['projectionMismatchCount', 'mismatchCount']), detail: 'Rows needing reconciliation' }
])
const formattedSummary = computed(() => summary.value ? JSON.stringify(summary.value, null, 2) : 'No stock summary loaded.')

async function load() {
  loading.value = true
  error.value = ''
  try {
    summary.value = await get<ApiRecord>('api/inventory/stock-reports/summary', { lowStockThreshold: threshold.value })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stock report summary.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

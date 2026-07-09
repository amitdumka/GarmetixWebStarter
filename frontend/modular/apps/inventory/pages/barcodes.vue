<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scan-barcode" class="size-4" /> Barcode printing</p>
          <h2 class="garmetix-dashboard-title">Barcodes And Labels</h2>
          <p class="garmetix-dashboard-subtitle">Search for stock and print price tag labels.</p>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />
    <UAlert v-if="warnings.length" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="warnings.join(' ')" />

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.6fr)_minmax(320px,0.8fr)]">
      <div class="garmetix-section-card">
        <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Search Stock</h3>
            <p class="garmetix-panel-subtitle">{{ products.length }} result(s)</p>
          </div>
          <div class="flex gap-2">
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search by name or barcode..." class="sm:w-72" @keyup.enter="searchProducts" />
            <UButton icon="i-lucide-search" color="primary" :loading="loading" @click="searchProducts">Search</UButton>
          </div>
        </div>

        <AdminMasterTable :columns="searchColumns" :rows="searchRows" empty-text="Search for a product to begin.">
          <template #actions="{ row }">
            <UButton icon="i-lucide-plus" size="xs" color="neutral" variant="soft" @click="addToQueue(row)">Add</UButton>
          </template>
        </AdminMasterTable>
      </div>

      <aside class="garmetix-detail-panel">
        <div class="flex items-center justify-between gap-3">
          <h3 class="garmetix-panel-title">Print Queue</h3>
          <UBadge color="neutral" variant="subtle">{{ queue.length }} item(s)</UBadge>
        </div>

        <div v-if="queue.length === 0" class="mt-6 text-center text-sm text-muted">
          No products added to the print queue.
        </div>

        <div v-else class="mt-4 space-y-3">
          <div v-for="(item, index) in queue" :key="`${item.stockId}-${index}`" class="flex items-center justify-between gap-2 rounded-lg border border-default p-2">
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-medium">{{ item.productName }}</p>
              <p class="truncate text-xs text-muted">{{ item.barcode }} - {{ formatIndianMoney(item.mrp) }}</p>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              <UInput v-model.number="item.copies" type="number" min="1" class="w-16" size="sm" />
              <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="removeFromQueue(index)" />
            </div>
          </div>

          <div class="space-y-3 border-t border-default pt-3">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Label size</span>
              <USelect v-model="labelSize" :items="labelSizeItems" class="w-full" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Print mode</span>
              <USelect v-model="printMode" :items="printModeItems" class="w-full" />
            </label>
            <UButton icon="i-lucide-printer" color="primary" block :loading="printing" @click="printLabels">
              Print {{ totalCopies }} Label(s)
            </UButton>
          </div>
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Barcodes - Garmetix Inventory' })

interface QueueItem {
  stockId: string
  productId: string
  productName: string
  barcode: string
  hsnCode: string
  brand: string
  mrp: number
  copies: number
}

const { get, post } = useAdminApiClient()
const search = ref('')
const loading = ref(false)
const printing = ref(false)
const error = ref('')
const message = ref('')
const warnings = ref<string[]>([])
const products = ref<ApiRecord[]>([])
const queue = ref<QueueItem[]>([])
const labelSize = ref('50x30')
const printMode = ref('browser')

const labelSizeItems = [
  { label: '50x30 mm', value: '50x30' },
  { label: '50x25 mm', value: '50x25' }
]
const printModeItems = [
  { label: 'Browser Print', value: 'browser' },
  { label: 'TSPL Download', value: 'tspl' }
]

const searchRows = computed(() => products.value.map(item => ({
  id: readText(item, ['stockId'], ''),
  name: readText(item, ['productName']),
  barcode: readText(item, ['barcode']),
  mrp: formatIndianMoney(readNumber(item, ['mrp']))
})))
const searchColumns = [
  { key: 'name', label: 'Product Name' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'mrp', label: 'MRP' }
]

const totalCopies = computed(() => queue.value.reduce((sum, item) => sum + (Number(item.copies) || 1), 0))

async function searchProducts() {
  if (!search.value.trim()) return
  loading.value = true
  error.value = ''
  try {
    products.value = toRows(await get<unknown>('price-tags/search', { query: search.value.trim(), take: 50 }))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Search failed.'
  } finally {
    loading.value = false
  }
}

function addToQueue(row: { id: string, name: string, barcode: string, mrp: string }) {
  const source = products.value.find(item => readText(item, ['stockId'], '') === row.id)
  if (!source) return

  const existing = queue.value.find(item => item.stockId === row.id)
  if (existing) {
    existing.copies += 1
    return
  }

  queue.value.push({
    stockId: readText(source, ['stockId'], ''),
    productId: readText(source, ['productId'], ''),
    productName: readText(source, ['productName']),
    barcode: readText(source, ['barcode']),
    hsnCode: readText(source, ['hsnCode'], ''),
    brand: readText(source, ['brand'], ''),
    mrp: readNumber(source, ['mrp']),
    copies: 1
  })
  message.value = `${readText(source, ['productName'])} added to print queue.`
}

function removeFromQueue(index: number) {
  queue.value.splice(index, 1)
}

async function printLabels() {
  if (queue.value.length === 0) return
  printing.value = true
  error.value = ''
  message.value = ''
  warnings.value = []
  try {
    const payload = {
      labelSize: labelSize.value,
      printerLanguage: printMode.value,
      storeName: 'Garmetix',
      rows: queue.value.map(item => ({
        stockId: item.stockId || null,
        productId: item.productId || null,
        productName: item.productName,
        barcode: item.barcode,
        hsnCode: item.hsnCode || null,
        brand: item.brand || null,
        mrp: item.mrp,
        copies: Number(item.copies) || 1
      }))
    }

    const res = await post<ApiRecord>('price-tags/prepare', payload)
    warnings.value = (res.warnings as string[] | undefined) ?? []
    message.value = `Prepared ${readNumber(res, ['labelCount'])} label(s).`

    if (printMode.value === 'tspl') {
      const commands = readText(res, ['thermalCommands'], '')
      if (commands) {
        const blob = new Blob([commands], { type: 'text/plain' })
        const url = URL.createObjectURL(blob)
        const anchor = document.createElement('a')
        anchor.href = url
        anchor.download = 'tags.prn'
        anchor.click()
        URL.revokeObjectURL(url)
      }
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Print failed.'
  } finally {
    printing.value = false
  }
}
</script>

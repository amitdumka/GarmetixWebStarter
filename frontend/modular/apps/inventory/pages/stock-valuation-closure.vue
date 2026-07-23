<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-shield-check" class="size-4" /> Compliance & closure</p>
          <h2 class="garmetix-dashboard-title">Stock Valuation Closure</h2>
          <p class="garmetix-dashboard-subtitle">Acceptance checklist confirming stock quantity, cost and MRP valuation is complete and reconciled with the movement ledger.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Run</UButton>
          <UButton icon="i-lucide-download" color="neutral" variant="soft" :loading="exporting" @click="exportEvidence">CSV Evidence</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-triangle-alert" :description="error">
      <template #actions>
        <UButton size="xs" color="error" variant="soft" @click="load">Try again</UButton>
      </template>
    </UAlert>

    <section class="garmetix-section-card">
      <div class="flex flex-wrap items-end gap-3">
        <label class="space-y-1 text-sm">
          <span class="text-muted">As of date</span>
          <UInput v-model="asOfDate" type="date" class="w-48" />
        </label>
        <UButton icon="i-lucide-filter" color="primary" variant="soft" :loading="loading" @click="load">Apply</UButton>
      </div>
    </section>

    <section v-if="report" class="grid gap-3 sm:grid-cols-3">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Closure Status</p>
        <UBadge :color="statusColor(report.status)" variant="soft" size="lg" class="mt-1">{{ report.status }}</UBadge>
        <p class="mt-2 text-xs text-muted">As of {{ formatShortDate(report.asOf) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Critical Issues</p>
        <p class="mt-1 text-2xl font-semibold" :class="report.criticalIssues > 0 ? 'text-error' : ''">{{ report.criticalIssues }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Warnings</p>
        <p class="mt-1 text-2xl font-semibold">{{ report.warningIssues }}</p>
      </UCard>
    </section>

    <section v-if="metricsRows.length" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <UCard v-for="metric in metricsRows" :key="metric.label" :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">{{ metric.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ metric.display }}</p>
        <p v-if="metric.description" class="mt-1 text-xs text-muted">{{ metric.description }}</p>
      </UCard>
    </section>

    <section v-if="issueRows.length" class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Issues</h3>
      <AdminMasterTable :columns="issueColumns" :rows="issueRows" empty-text="No issues found.">
        <template #actions="{ row }">
          <a v-if="row.actionPath" :href="row.actionPath" class="text-primary text-xs underline">Open</a>
        </template>
      </AdminMasterTable>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Stock Valuation Rows</h3>
      <AdminMasterTable :columns="rowColumns" :rows="pagedRows" empty-text="No stock rows found." />
      <div v-if="rows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedRows.length }} of {{ rows.length }} row(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="rowsPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="rowsPage <= 1" @click="rowsPage--">Prev</UButton>
          <span>{{ rowsPage }} / {{ rowsTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="rowsPage >= rowsTotalPages" @click="rowsPage++">Next</UButton>
        </div>
      </div>
    </section>

    <section v-if="summaryRows.length" class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Stock-Type Summary</h3>
      <AdminMasterTable :columns="summaryColumns" :rows="summaryRows" empty-text="No summary rows." />
    </section>

    <section v-if="report" class="grid gap-3 lg:grid-cols-3">
      <UCard :ui="{ body: 'p-4' }">
        <h4 class="mb-2 text-sm font-semibold">Closeout Checklist</h4>
        <ul class="list-inside list-disc space-y-1 text-sm text-muted">
          <li v-for="(item, index) in report.closeoutChecklist" :key="index">{{ item }}</li>
        </ul>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <h4 class="mb-2 text-sm font-semibold">Known Limitations</h4>
        <ul class="list-inside list-disc space-y-1 text-sm text-muted">
          <li v-for="(item, index) in report.knownLimitations" :key="index">{{ item }}</li>
        </ul>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <h4 class="mb-2 text-sm font-semibold">Next Modules</h4>
        <ul class="list-inside list-disc space-y-1 text-sm text-muted">
          <li v-for="(item, index) in report.nextModuleCandidates" :key="index">{{ item }}</li>
        </ul>
      </UCard>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { pageSizeOptions, paginateRows, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Stock Valuation Closure - Garmetix Inventory' })

const { get, download } = useAdminApiClient()

const loading = ref(true)
const exporting = ref(false)
const error = ref('')
const report = ref<ApiRecord | null>(null)
const asOfDate = ref('')

const rowsPage = ref(1)
const rowsPageSize = ref<number>(pageSizeOptions[0].value)

const issueColumns = [
  { key: 'severity', label: 'Severity' },
  { key: 'title', label: 'Title' },
  { key: 'message', label: 'Message' }
]
const issueRows = computed(() => toRows(report.value?.issues).map(item => ({
  severity: readText(item, ['severity']),
  title: readText(item, ['title']),
  message: readText(item, ['message']),
  actionPath: readText(item, ['actionPath'], '')
})))

const rowColumns = [
  { key: 'productName', label: 'Product' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'currentQty', label: 'Current Qty' },
  { key: 'cost', label: 'Cost' },
  { key: 'mrp', label: 'MRP' },
  { key: 'costValue', label: 'Cost Value' },
  { key: 'movementRows', label: 'Movement Rows' },
  { key: 'status', label: 'Status' }
]
const rows = computed(() => toRows(report.value?.rows).map(item => ({
  productName: readText(item, ['productName']),
  barcode: readText(item, ['barcode']),
  currentQty: readNumber(item, ['currentQty']),
  cost: formatIndianMoney(readNumber(item, ['costPrice'])),
  mrp: formatIndianMoney(readNumber(item, ['mrp'])),
  costValue: formatIndianMoney(readNumber(item, ['costValue'])),
  movementRows: readNumber(item, ['movementRows']),
  status: readText(item, ['status'])
})))
const rowsTotalPages = computed(() => Math.max(1, Math.ceil(rows.value.length / Number(rowsPageSize.value || 25))))
const pagedRows = computed(() => paginateRows(rows.value, rowsPage.value, rowsPageSize.value))
watch(rowsPageSize, () => { rowsPage.value = 1 })
watch(rowsTotalPages, (value) => { if (rowsPage.value > value) rowsPage.value = value })

const summaryColumns = [
  { key: 'stockType', label: 'Stock Type' },
  { key: 'rows', label: 'Rows' },
  { key: 'quantity', label: 'Quantity' },
  { key: 'costValue', label: 'Cost Value' },
  { key: 'mrpValue', label: 'MRP Value' }
]
const summaryRows = computed(() => toRows(report.value?.summary).map(item => ({
  stockType: readText(item, ['stockType']),
  rows: readNumber(item, ['rows']),
  quantity: readNumber(item, ['quantity']),
  costValue: formatIndianMoney(readNumber(item, ['costValue'])),
  mrpValue: formatIndianMoney(readNumber(item, ['mrpValue']))
})))

const metricsRows = computed(() => toRows(report.value?.metrics).map(item => {
  const hasCount = item.count !== null && item.count !== undefined
  const label = readText(item, ['label'])
  const isMoney = !hasCount && /value/i.test(label)
  return {
    label,
    description: readText(item, ['description'], ''),
    display: hasCount
      ? String(readNumber(item, ['count']))
      : isMoney
        ? formatIndianMoney(readNumber(item, ['amount']))
        : String(readNumber(item, ['amount']))
  }
}))

function statusColor(value: string) {
  const normalized = value.toLowerCase()
  if (normalized.includes('not complete') || normalized.includes('critical') || normalized.includes('negative')) return 'error'
  if (normalized.includes('warning')) return 'warning'
  if (normalized.includes('complete')) return 'success'
  return 'neutral'
}

function formatShortDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    report.value = await get<ApiRecord>('inventory/stock-valuation-closure', {
      asOf: asOfDate.value || undefined
    })
    rowsPage.value = 1
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stock valuation closure report.'
  } finally {
    loading.value = false
  }
}

async function exportEvidence() {
  exporting.value = true
  try {
    await download('inventory/stock-valuation-closure/evidence.csv', { asOf: asOfDate.value || undefined }, 'garmetix-stock-valuation-closure.csv')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export evidence CSV.'
  } finally {
    exporting.value = false
  }
}

onMounted(load)
</script>

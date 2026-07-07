<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker"><UIcon name="i-lucide-shield-check" class="size-4" /> Post-import checks</p>
          <h2 class="garmetix-dashboard-title">Accounting/GST Validation</h2>
          <p class="garmetix-dashboard-subtitle">
            Cross-check accounting and GST totals for sales, purchase, payments and stock after import, with a closeout checklist for the selected period.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge :color="statusColor" variant="subtle">{{ report ? readText(report, ['status']) : 'Not run' }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="runValidation">Run Validation</UButton>
          <UButton icon="i-lucide-file-down" color="neutral" variant="soft" :loading="csvLoading" @click="downloadEvidence">CSV Evidence</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <div class="grid gap-3 xl:grid-cols-12">
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">From Date</span>
          <UInput v-model="filters.from" type="date" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">To Date</span>
          <UInput v-model="filters.to" type="date" />
        </label>
        <div class="flex items-end xl:col-span-2">
          <UButton icon="i-lucide-check" color="primary" variant="soft" :loading="loading" @click="runValidation">Apply</UButton>
        </div>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Final Status</p>
        <p class="garmetix-metric-value">{{ report ? readText(report, ['status']) : '-' }}</p>
        <p class="garmetix-metric-caption">{{ criticalIssues.length }} critical, {{ warningIssues.length }} warning</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Critical Issues</p>
        <p class="garmetix-metric-value">{{ criticalIssues.length }}</p>
        <p class="garmetix-metric-caption">Blocking closeout</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Warnings</p>
        <p class="garmetix-metric-value">{{ warningIssues.length }}</p>
        <p class="garmetix-metric-caption">Review before filing</p>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.amount ? money(metric.amount) : numberValue(metric.count) }}</p>
        <p class="garmetix-metric-caption">{{ metric.detail }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Validation Checks</h3>
      <BooksMasterTable :columns="checkColumns" :rows="checkRows" empty-text="No validation checks returned." />
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Issues ({{ issues.length }})</h3>
      <ul v-if="issues.length" class="space-y-2">
        <li v-for="(issue, index) in issues" :key="index" class="border-b border-default pb-2 text-sm">
          <p class="font-medium">{{ readText(issue, ['area']) }}: {{ readText(issue, ['title']) }}</p>
          <p class="text-muted">
            <span v-if="readText(issue, ['reference']) !== '-'">{{ readText(issue, ['reference']) }} - </span>{{ readText(issue, ['description']) }}
          </p>
        </li>
      </ul>
      <p v-else class="text-sm text-muted">No issues found for this period.</p>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">GST Snapshot</h3>
        <BooksMasterTable :columns="gstColumns" :rows="gstRowsDisplay" empty-text="No GST rows found." />
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Payment Reconciliation</h3>
        <BooksMasterTable :columns="paymentColumns" :rows="paymentRowsDisplay" empty-text="No payment rows found." />
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-3">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Closeout Checklist</h3>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="(item, index) in closeoutChecklist" :key="index" class="border-b border-default pb-2">{{ item }}</li>
          <li v-if="closeoutChecklist.length === 0">No checklist items available.</li>
        </ul>
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Known Limitations</h3>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="(item, index) in knownLimitations" :key="index" class="border-b border-default pb-2">{{ item }}</li>
          <li v-if="knownLimitations.length === 0">No known limitations listed.</li>
        </ul>
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Next Module Candidates</h3>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="(item, index) in nextModuleCandidates" :key="index" class="border-b border-default pb-2">{{ item }}</li>
          <li v-if="nextModuleCandidates.length === 0">No candidates listed.</li>
        </ul>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Accounting/GST Validation - Garmetix Books' })

const { download, get } = useBooksApiClient()
const loading = ref(true)
const csvLoading = ref(false)
const error = ref('')
const report = ref<ApiRecord | null>(null)
const filters = reactive({ from: firstDayOfMonth(), to: lastDayOfMonth() })

const checkColumns = [
  { key: 'status', label: 'Status' },
  { key: 'check', label: 'Check' },
  { key: 'expected', label: 'Expected' },
  { key: 'actual', label: 'Actual' },
  { key: 'difference', label: 'Difference' },
  { key: 'description', label: 'Description' }
]
const gstColumns = [
  { key: 'direction', label: 'Direction' },
  { key: 'rate', label: 'Rate' },
  { key: 'taxable', label: 'Taxable' },
  { key: 'tax', label: 'Tax' },
  { key: 'lines', label: 'Lines' }
]
const paymentColumns = [
  { key: 'direction', label: 'Direction' },
  { key: 'mode', label: 'Mode' },
  { key: 'rows', label: 'Rows' },
  { key: 'amount', label: 'Amount' },
  { key: 'missingBank', label: 'Missing Bank' }
]

const metrics = computed(() => readArray(report.value, ['metrics']).map(item => ({
  label: readText(item, ['label']),
  count: readNumber(item, ['count']),
  amount: readNumber(item, ['amount']),
  detail: readText(item, ['detail'])
})))
const checks = computed(() => readArray(report.value, ['checks']))
const issues = computed(() => readArray(report.value, ['issues']))
const criticalIssues = computed(() => issues.value.filter(item => readText(item, ['severity']) === 'Critical'))
const warningIssues = computed(() => issues.value.filter(item => readText(item, ['severity']) === 'Warning'))
const gstRowsDisplay = computed(() => readArray(report.value, ['gstRows']).map(row => ({
  direction: readText(row, ['direction']),
  rate: readText(row, ['taxRate']),
  taxable: money(row.taxableValue),
  tax: money(row.taxAmount),
  lines: readText(row, ['lineCount'])
})))
const paymentRowsDisplay = computed(() => readArray(report.value, ['paymentRows']).map(row => ({
  direction: readText(row, ['direction']),
  mode: readText(row, ['paymentMode']),
  rows: readText(row, ['rowCount']),
  amount: money(row.amount),
  missingBank: readText(row, ['missingBankMappingCount'])
})))
const closeoutChecklist = computed(() => readArray(report.value, ['closeoutChecklist']).map(item => String(item)))
const knownLimitations = computed(() => readArray(report.value, ['knownLimitations']).map(item => String(item)))
const nextModuleCandidates = computed(() => readArray(report.value, ['nextModuleCandidates']).map(item => String(item)))
const checkRows = computed(() => checks.value.map(item => ({
  status: readText(item, ['status']),
  check: `${readText(item, ['title'])} (${readText(item, ['code'])})`,
  expected: numberOrDash(item.expectedValue),
  actual: numberOrDash(item.actualValue),
  difference: numberOrDash(item.difference),
  description: readText(item, ['description'])
})))
const statusColor = computed(() => {
  const status = report.value ? readText(report.value, ['status']) : ''
  if (status === 'Complete') return 'success'
  if (status === 'Not Complete') return 'error'
  return 'neutral'
})

function localDateOnly(date: Date) {
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10)
}

function firstDayOfMonth() {
  const date = new Date()
  return localDateOnly(new Date(date.getFullYear(), date.getMonth(), 1))
}

function lastDayOfMonth() {
  const date = new Date()
  return localDateOnly(new Date(date.getFullYear(), date.getMonth() + 1, 0))
}

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function numberValue(value: unknown) {
  return readNumber({ value }, ['value'])
}

function numberOrDash(value: unknown) {
  return value === null || value === undefined ? '-' : money(value)
}

async function runValidation() {
  loading.value = true
  error.value = ''
  try {
    report.value = await get<ApiRecord>('post-import-validation/accounting-gst', { from: filters.from, to: filters.to })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to run accounting/GST validation.'
  } finally {
    loading.value = false
  }
}

async function downloadEvidence() {
  csvLoading.value = true
  error.value = ''
  try {
    await download('post-import-validation/accounting-gst.csv', { from: filters.from, to: filters.to }, `accounting-gst-post-import-validation-${filters.from}-${filters.to}.csv`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download validation evidence CSV.'
  } finally {
    csvLoading.value = false
  }
}

onMounted(runValidation)
</script>

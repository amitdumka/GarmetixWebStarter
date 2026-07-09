<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-shield-check" class="size-4" /> Purchase closeout</p>
          <h2 class="garmetix-dashboard-title">Vendor Payable Reconciliation</h2>
          <p class="garmetix-dashboard-subtitle">
            Cross-checks vendor master balances against purchase invoice, payment, debit-note and settlement evidence for the selected close date.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UBadge :color="statusColor" variant="subtle">{{ report ? readText(report, ['status']) : 'Not run' }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="runReport">Run Reconciliation</UButton>
          <UButton icon="i-lucide-file-down" color="neutral" variant="soft" :loading="csvLoading" @click="downloadEvidence">CSV Evidence</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <div class="grid gap-3 sm:grid-cols-3">
        <label class="space-y-1 text-sm">
          <span class="text-muted">From Date</span>
          <UInput v-model="filters.from" type="date" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">To Date</span>
          <UInput v-model="filters.to" type="date" />
        </label>
        <div class="flex items-end">
          <UButton icon="i-lucide-check" color="primary" variant="soft" :loading="loading" @click="runReport">Apply</UButton>
        </div>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Final Status</p>
        <p class="garmetix-metric-value">{{ report ? readText(report, ['status']) : '-' }}</p>
        <p class="garmetix-metric-caption">{{ readNumber(report, ['criticalIssues']) }} critical, {{ readNumber(report, ['warningIssues']) }} warning</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Expected Payable</p>
        <p class="garmetix-metric-value">{{ money(readNumber(report, ['totalExpectedPayable'])) }}</p>
        <p class="garmetix-metric-caption">Outstanding minus open debit notes</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Open Debit Notes</p>
        <p class="garmetix-metric-value">{{ money(readNumber(report, ['totalOpenDebitNotes'])) }}</p>
        <p class="garmetix-metric-caption">Available to adjust/settle</p>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="metric in metrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.amount !== null ? money(metric.amount) : metric.count }}</p>
        <p class="garmetix-metric-caption">{{ metric.description }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Payment / Settlement Sources</h3>
      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[720px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2 font-medium">Source</th>
                <th class="px-3 py-2 text-right font-medium">Total Rows</th>
                <th class="px-3 py-2 text-right font-medium">Total Amount</th>
                <th class="px-3 py-2 text-right font-medium">Period Rows</th>
                <th class="px-3 py-2 text-right font-medium">Period Amount</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="!paymentSourceRows.length">
                <td colspan="5" class="px-3 py-8 text-center text-muted">No payment source rows found.</td>
              </tr>
              <tr v-for="(row, index) in paymentSourceRows" :key="index">
                <td class="px-3 py-2">{{ readText(row, ['source']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['totalRows']) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['totalAmount'])) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['periodRows']) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['periodAmount'])) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Issues ({{ issues.length }})</h3>
      <ul v-if="issues.length" class="space-y-2">
        <li v-for="(issue, index) in issues" :key="index" class="border-b border-default pb-2 text-sm">
          <p class="font-medium">
            <UBadge size="xs" :color="readText(issue, ['severity']) === 'Critical' ? 'error' : 'warning'" variant="subtle">{{ readText(issue, ['severity']) }}</UBadge>
            {{ readText(issue, ['vendorName']) }} - {{ readText(issue, ['code']) }}
          </p>
          <p class="text-muted">{{ readText(issue, ['message']) }}</p>
        </li>
      </ul>
      <p v-else class="text-sm text-muted">No issues found for this period.</p>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex items-center justify-between">
        <h3 class="garmetix-panel-title">Vendor Evidence</h3>
        <p class="garmetix-panel-subtitle">{{ pagedEvidence.length }} of {{ evidenceRows.length }} vendor(s)</p>
      </div>
      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1100px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2 font-medium">Vendor</th>
                <th class="px-3 py-2 text-right font-medium">Invoices</th>
                <th class="px-3 py-2 text-right font-medium">Invoice Total</th>
                <th class="px-3 py-2 text-right font-medium">Outstanding</th>
                <th class="px-3 py-2 text-right font-medium">Master Balance</th>
                <th class="px-3 py-2 text-right font-medium">Open DN</th>
                <th class="px-3 py-2 text-right font-medium">Expected Payable</th>
                <th class="px-3 py-2 text-right font-medium">Balance Diff</th>
                <th class="px-3 py-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="!pagedEvidence.length">
                <td colspan="9" class="px-3 py-8 text-center text-muted">No vendor evidence rows found.</td>
              </tr>
              <tr v-for="row in pagedEvidence" :key="readText(row, ['vendorId'])">
                <td class="max-w-48 truncate px-3 py-2 font-medium">{{ readText(row, ['vendorName']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['invoiceCount']) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['invoiceTotal'])) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['invoiceOutstanding'])) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['masterBalance'])) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['openDebitNoteAmount'])) }}</td>
                <td class="px-3 py-2 text-right font-medium">{{ money(readNumber(row, ['expectedPayableBalance'])) }}</td>
                <td class="px-3 py-2 text-right" :class="Math.abs(readNumber(row, ['balanceDifference'])) > 1 ? 'text-error' : ''">{{ money(readNumber(row, ['balanceDifference'])) }}</td>
                <td class="px-3 py-2"><UBadge :color="evidenceStatusColor(row.status)" variant="subtle">{{ readText(row, ['status']) }}</UBadge></td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <div v-if="evidenceRows.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ evidencePage }} of {{ evidenceTotalPages }}</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="evidencePage <= 1" @click="evidencePage = Math.max(1, evidencePage - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="evidencePage >= evidenceTotalPages" @click="evidencePage = Math.min(evidenceTotalPages, evidencePage + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-3">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Closeout Checklist</h3>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="(item, index) in closeoutChecklist" :key="index" class="border-b border-default pb-2">{{ item }}</li>
          <li v-if="!closeoutChecklist.length">No checklist items available.</li>
        </ul>
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Operator Rules</h3>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="(item, index) in operatorRules" :key="index" class="border-b border-default pb-2">{{ item }}</li>
          <li v-if="!operatorRules.length">No operator rules listed.</li>
        </ul>
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Known Limitations</h3>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="(item, index) in knownLimitations" :key="index" class="border-b border-default pb-2">{{ item }}</li>
          <li v-if="!knownLimitations.length">No known limitations listed.</li>
        </ul>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Vendor Payable Reconciliation - Garmetix Back Office' })

const { download, get } = useMainApiClient()
const loading = ref(true)
const csvLoading = ref(false)
const error = ref('')
const report = ref<ApiRecord | null>(null)
const evidencePage = ref(1)
const evidencePageSize = 25

function localDateOnly(date: Date) {
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10)
}
function firstDayOfMonth() {
  const date = new Date()
  return localDateOnly(new Date(date.getFullYear(), date.getMonth(), 1))
}
function today() {
  return localDateOnly(new Date())
}

const filters = reactive({ from: firstDayOfMonth(), to: today() })

const metrics = computed(() => readArray(report.value, ['metrics']).map(item => ({
  label: readText(item, ['label']),
  count: readNumber(item, ['count']),
  amount: item.amount === null || item.amount === undefined ? null : readNumber(item, ['amount']),
  description: readText(item, ['description'])
})))
const issues = computed(() => readArray(report.value, ['issues']))
const evidenceRows = computed(() => readArray(report.value, ['evidenceRows']))
const paymentSourceRows = computed(() => readArray(report.value, ['paymentSources']))
const closeoutChecklist = computed(() => readArray(report.value, ['closeoutChecklist']).map(item => String(item)))
const operatorRules = computed(() => readArray(report.value, ['operatorRules']).map(item => String(item)))
const knownLimitations = computed(() => readArray(report.value, ['knownLimitations']).map(item => String(item)))
const evidenceTotalPages = computed(() => Math.max(1, Math.ceil(evidenceRows.value.length / evidencePageSize)))
const pagedEvidence = computed(() => {
  const start = (evidencePage.value - 1) * evidencePageSize
  return evidenceRows.value.slice(start, start + evidencePageSize)
})
const statusColor = computed(() => {
  const status = report.value ? readText(report.value, ['status']) : ''
  if (status === 'Complete') return 'success' as const
  if (status === 'Not Complete') return 'error' as const
  return 'neutral' as const
})

watch(evidenceRows, () => { evidencePage.value = 1 })

function money(value: unknown) { return formatIndianMoney(readNumber({ value }, ['value'])) }
function evidenceStatusColor(status: unknown) {
  const value = String(status ?? '')
  if (value === 'Critical') return 'error' as const
  if (value === 'Warning') return 'warning' as const
  return 'success' as const
}

async function runReport() {
  loading.value = true
  error.value = ''
  try {
    report.value = await get<ApiRecord>('purchase/vendor-payable-reconciliation', { from: filters.from, to: filters.to })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to run vendor payable reconciliation.'
  } finally {
    loading.value = false
  }
}

async function downloadEvidence() {
  csvLoading.value = true
  error.value = ''
  try {
    await download('purchase/vendor-payable-reconciliation/evidence.csv', { from: filters.from, to: filters.to }, `vendor-payable-reconciliation-${filters.from}-${filters.to}.csv`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download reconciliation evidence CSV.'
  } finally {
    csvLoading.value = false
  }
}

onMounted(runReport)
</script>

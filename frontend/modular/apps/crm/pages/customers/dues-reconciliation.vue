<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-wallet-cards" class="size-4" /> CRM audit</p>
          <h2 class="garmetix-dashboard-title">Customer Dues Reconciliation</h2>
          <p class="garmetix-dashboard-subtitle">Compare customer master credit with advances, credit notes, invoice dues and payment evidence.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="runReport">Run Report</UButton>
          <UButton icon="i-lucide-download" :loading="exporting" :disabled="!report" @click="exportCsv">Export CSV</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-users-round" @click="router.push('/customers')">Customers</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="loadError" />

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-5">
      <UFormField label="Company" class="lg:col-span-1">
        <USelect v-model="selectedCompanyId" :items="companyOptions" placeholder="All companies" @update:model-value="onCompanyChanged" />
      </UFormField>
      <UFormField label="Store" class="lg:col-span-1">
        <USelect v-model="selectedStoreId" :items="storeOptions" placeholder="All stores" />
      </UFormField>
      <UFormField label="From">
        <UInput v-model="filters.from" type="date" />
      </UFormField>
      <UFormField label="To">
        <UInput v-model="filters.to" type="date" />
      </UFormField>
      <div class="flex items-end">
        <UButton class="w-full justify-center" icon="i-lucide-search-check" :loading="loading" @click="runReport">Reconcile</UButton>
      </div>
    </div>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div v-for="metric in summaryMetrics" :key="metric.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label"><UIcon :name="metric.icon" class="mr-1 inline size-4" /> {{ metric.label }}</p>
        <p class="garmetix-metric-value">{{ metric.value }}</p>
        <p class="garmetix-metric-caption">{{ metric.detail }}</p>
      </div>
    </section>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,0.75fr)_minmax(0,1fr)]">
      <div class="garmetix-section-card">
        <div class="mb-4 flex items-start justify-between gap-3">
          <div>
            <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> Closeout</p>
            <h3 class="text-xl font-semibold text-highlighted">Audit Checklist</h3>
          </div>
          <UBadge :color="statusColor" variant="subtle">{{ reportStatus }}</UBadge>
        </div>

        <div class="space-y-4">
          <div>
            <p class="mb-2 text-sm font-semibold text-highlighted">Closeout checklist</p>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in closeoutChecklist" :key="item" class="flex gap-2"><UIcon name="i-lucide-check-circle-2" class="mt-0.5 size-4 text-success" /> <span>{{ item }}</span></li>
            </ul>
          </div>
          <div>
            <p class="mb-2 text-sm font-semibold text-highlighted">Operator rules</p>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in operatorRules" :key="item" class="flex gap-2"><UIcon name="i-lucide-shield-check" class="mt-0.5 size-4 text-primary" /> <span>{{ item }}</span></li>
            </ul>
          </div>
          <div v-if="knownLimitations.length">
            <p class="mb-2 text-sm font-semibold text-highlighted">Known limitations</p>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in knownLimitations" :key="item" class="flex gap-2"><UIcon name="i-lucide-info" class="mt-0.5 size-4 text-warning" /> <span>{{ item }}</span></li>
            </ul>
          </div>
        </div>
      </div>

      <div class="garmetix-section-card">
        <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p class="garmetix-kicker"><UIcon name="i-lucide-triangle-alert" class="size-4" /> Exceptions</p>
            <h3 class="text-xl font-semibold text-highlighted">Issues</h3>
          </div>
          <UInput v-model="issueSearch" icon="i-lucide-search" placeholder="Search issue, customer or code" class="lg:w-80" />
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[760px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Severity</th>
                <th class="border-b border-default p-3">Customer</th>
                <th class="border-b border-default p-3">Code</th>
                <th class="border-b border-default p-3">Message</th>
                <th class="border-b border-default p-3 text-right">Amount</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="loading"><td colspan="5" class="p-8 text-center text-muted">Loading reconciliation...</td></tr>
              <tr v-else-if="!filteredIssues.length"><td colspan="5" class="p-8 text-center text-muted">No issues found for this scope.</td></tr>
              <template v-else>
                <tr v-for="issue in filteredIssues" :key="`${readText(issue, ['customerId'])}-${readText(issue, ['code'])}-${readText(issue, ['message'])}`">
                  <td class="border-b border-default p-3"><UBadge :color="issueColor(issue)" variant="subtle">{{ readText(issue, ['severity']) }}</UBadge></td>
                  <td class="border-b border-default p-3">
                    <p class="font-medium text-highlighted">{{ readText(issue, ['customerName']) }}</p>
                    <p class="text-xs text-muted">{{ readText(issue, ['mobileNumber'], '') }}</p>
                  </td>
                  <td class="border-b border-default p-3">{{ readText(issue, ['code']) }}</td>
                  <td class="border-b border-default p-3">{{ readText(issue, ['message']) }}</td>
                  <td class="border-b border-default p-3 text-right">{{ money(readNumber(issue, ['amount'])) }}</td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div class="garmetix-section-card">
      <div class="mb-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-database" class="size-4" /> Evidence</p>
          <h3 class="text-xl font-semibold text-highlighted">Customer Evidence Rows</h3>
        </div>
        <UInput v-model="evidenceSearch" icon="i-lucide-search" placeholder="Search customer, mobile, status" class="lg:w-96" />
      </div>

      <div class="overflow-x-auto">
        <table class="w-full min-w-[1320px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr>
              <th class="border-b border-default p-3">Customer</th>
              <th class="border-b border-default p-3 text-right">Invoices</th>
              <th class="border-b border-default p-3 text-right">Invoice Total</th>
              <th class="border-b border-default p-3 text-right">Paid</th>
              <th class="border-b border-default p-3 text-right">Due</th>
              <th class="border-b border-default p-3 text-right">Master Credit</th>
              <th class="border-b border-default p-3 text-right">Advance Open</th>
              <th class="border-b border-default p-3 text-right">Credit Note Open</th>
              <th class="border-b border-default p-3 text-right">Expected Credit</th>
              <th class="border-b border-default p-3 text-right">Difference</th>
              <th class="border-b border-default p-3">Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="11" class="p-8 text-center text-muted">Loading evidence...</td></tr>
            <tr v-else-if="!filteredEvidence.length"><td colspan="11" class="p-8 text-center text-muted">No evidence rows for this scope.</td></tr>
            <template v-else>
              <tr v-for="row in filteredEvidence" :key="readText(row, ['customerId'])">
                <td class="border-b border-default p-3">
                  <p class="font-medium text-highlighted">{{ readText(row, ['customerName']) }}</p>
                  <p class="text-xs text-muted">{{ readText(row, ['mobileNumber'], '') }}</p>
                </td>
                <td class="border-b border-default p-3 text-right">{{ number(readNumber(row, ['invoiceCount'])) }} / {{ number(readNumber(row, ['openInvoiceCount'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['invoiceTotal'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['invoicePaid', 'paymentRowsTotal'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['invoiceDue'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['customerMasterCreditBalance'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['advanceOpen'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['creditNoteOpen'])) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(readNumber(row, ['expectedCreditBalance'])) }}</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ money(readNumber(row, ['creditBalanceDifference'])) }}</td>
                <td class="border-b border-default p-3"><UBadge :color="rowStatusColor(row)" variant="subtle">{{ readText(row, ['status']) }}</UBadge></td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </div>

    <div class="garmetix-section-card">
      <div class="mb-4 flex items-center justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-list-checks" class="size-4" /> Source summary</p>
          <h3 class="text-xl font-semibold text-highlighted">Credit Sources</h3>
        </div>
        <UBadge color="neutral" variant="soft">{{ creditSources.length }} source(s)</UBadge>
      </div>

      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div v-for="source in creditSources" :key="readText(source, ['source'])" class="rounded-md border border-default p-3">
          <p class="font-semibold text-highlighted">{{ readText(source, ['source']) }}</p>
          <p class="text-sm text-muted">{{ number(readNumber(source, ['rowCount'])) }} row(s), {{ number(readNumber(source, ['issueCount'])) }} issue(s)</p>
          <div class="mt-3 grid gap-2 text-sm">
            <div class="flex justify-between"><span class="text-muted">Amount</span><span>{{ money(readNumber(source, ['amount'])) }}</span></div>
            <div class="flex justify-between"><span class="text-muted">Adjusted</span><span>{{ money(readNumber(source, ['adjustedAmount'])) }}</span></div>
            <div class="flex justify-between"><span class="text-muted">Available</span><span class="font-semibold">{{ money(readNumber(source, ['availableAmount'])) }}</span></div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, stripServerUrl } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const router = useRouter()
const toast = useToast()
const { get, getBlob } = useCrmApiClient()

const today = new Date()
const firstDay = new Date(today.getFullYear(), today.getMonth(), 1)

const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const selectedCompanyId = ref('')
const selectedStoreId = ref('')
const report = ref<ApiRecord | null>(null)
const loading = ref(false)
const exporting = ref(false)
const loadError = ref('')
const issueSearch = ref('')
const evidenceSearch = ref('')

const filters = reactive({
  from: toDateInput(firstDay),
  to: toDateInput(today)
})

const companyOptions = computed(() => [{ label: 'All companies', value: '' }, ...companies.value.map(item => ({
  label: readText(item, ['name', 'companyName'], 'Company'),
  value: String(item.id || '')
}))])

const storeOptions = computed(() => {
  const visibleStores = stores.value.filter(item => !selectedCompanyId.value || readText(item, ['companyId'], '') === selectedCompanyId.value)
  return [{ label: 'All stores', value: '' }, ...visibleStores.map(item => ({
    label: readText(item, ['name', 'storeName'], 'Store'),
    value: String(item.id || '')
  }))]
})

const selectedStore = computed(() => stores.value.find(item => String(item.id || '') === selectedStoreId.value) ?? null)
const reportStatus = computed(() => readText(report.value, ['status'], report.value ? 'Ready' : 'Not run'))
const criticalIssues = computed(() => readNumber(report.value, ['criticalIssues']))
const warningIssues = computed(() => readNumber(report.value, ['warningIssues']))
const evidenceRows = computed(() => readArray(report.value, ['evidenceRows']))
const issues = computed(() => readArray(report.value, ['issues']))
const creditSources = computed(() => readArray(report.value, ['creditSources']))
const closeoutChecklist = computed(() => readTextArray(report.value, 'closeoutChecklist'))
const operatorRules = computed(() => readTextArray(report.value, 'operatorRules'))
const knownLimitations = computed(() => readTextArray(report.value, 'knownLimitations'))

const statusColor = computed(() => {
  const status = reportStatus.value.toLowerCase()
  if (criticalIssues.value > 0 || status.includes('critical')) return 'error' as const
  if (warningIssues.value > 0 || status.includes('warning')) return 'warning' as const
  if (report.value) return 'success' as const
  return 'neutral' as const
})

const summaryMetrics = computed(() => [
  { label: 'Status', value: reportStatus.value, detail: `${number(criticalIssues.value)} critical, ${number(warningIssues.value)} warning`, icon: 'i-lucide-activity' },
  { label: 'Total Due', value: money(readNumber(report.value, ['totalDue'])), detail: 'Open invoice due', icon: 'i-lucide-receipt-indian-rupee' },
  { label: 'Master Credit', value: money(readNumber(report.value, ['totalMasterCreditBalance'])), detail: 'Customer master balance', icon: 'i-lucide-wallet-cards' },
  { label: 'Expected Credit', value: money(readNumber(report.value, ['totalExpectedCreditBalance'])), detail: 'Advance + credit-note evidence', icon: 'i-lucide-scale' },
  { label: 'Evidence Rows', value: number(evidenceRows.value.length), detail: scopeLabel.value, icon: 'i-lucide-database' }
])

const scopeLabel = computed(() => {
  if (selectedStore.value) return readText(selectedStore.value, ['name', 'storeName'], 'Selected store')
  if (selectedCompanyId.value) return readText(companies.value.find(item => String(item.id || '') === selectedCompanyId.value), ['name', 'companyName'], 'Selected company')
  return 'All accessible scope'
})

const filteredIssues = computed(() => {
  const term = issueSearch.value.trim().toLowerCase()
  if (!term) return issues.value
  return issues.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

const filteredEvidence = computed(() => {
  const term = evidenceSearch.value.trim().toLowerCase()
  if (!term) return evidenceRows.value
  return evidenceRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

function toDateInput(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function readTextArray(source: ApiRecord | null | undefined, key: string) {
  const value = source?.[key]
  return Array.isArray(value) ? value.map(item => String(item)) : []
}

function money(value: number) {
  return formatIndianMoney(value)
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function selectedStoreGroupId() {
  return readText(selectedStore.value, ['storeGroupId', 'groupId'], '')
}

function queryParams() {
  return {
    companyId: selectedCompanyId.value || undefined,
    storeGroupId: selectedStoreId.value ? selectedStoreGroupId() : undefined,
    storeId: selectedStoreId.value || undefined,
    from: filters.from,
    to: filters.to
  }
}

function issueColor(issue: ApiRecord) {
  const severity = readText(issue, ['severity'], '').toLowerCase()
  if (severity.includes('critical') || severity.includes('error')) return 'error' as const
  if (severity.includes('warn')) return 'warning' as const
  return 'neutral' as const
}

function rowStatusColor(row: ApiRecord) {
  const status = readText(row, ['status'], '').toLowerCase()
  if (status.includes('critical') || status.includes('error') || status.includes('mismatch')) return 'error' as const
  if (status.includes('warning')) return 'warning' as const
  if (status.includes('ok') || status.includes('match')) return 'success' as const
  return 'neutral' as const
}

async function loadWorkspace() {
  const [companyData, storeData] = await Promise.all([
    get<unknown>('companies').catch(() => []),
    get<unknown>('stores').catch(() => [])
  ])
  companies.value = toRows(companyData)
  stores.value = toRows(storeData)
  selectedCompanyId.value = selectedCompanyId.value || readText(stores.value[0], ['companyId'], '') || readText(companies.value[0], ['id'], '')
  selectedStoreId.value = selectedStoreId.value || readText(stores.value.find(item => readText(item, ['companyId'], '') === selectedCompanyId.value) ?? stores.value[0], ['id'], '')
}

function onCompanyChanged() {
  const nextStore = stores.value.find(item => readText(item, ['companyId'], '') === selectedCompanyId.value)
  selectedStoreId.value = readText(nextStore, ['id'], '')
}

async function runReport() {
  loading.value = true
  loadError.value = ''
  try {
    if (!companies.value.length && !stores.value.length) await loadWorkspace()
    report.value = await get<ApiRecord>('customers/dues-reconciliation', queryParams())
  } catch (caught) {
    loadError.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load customer dues reconciliation.')
    toast.add({ title: 'Could not load dues reconciliation', description: loadError.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function exportCsv() {
  exporting.value = true
  try {
    const blob = await getBlob('customers/dues-reconciliation/evidence.csv', queryParams())
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = `customer-dues-reconciliation-${filters.from}-to-${filters.to}.csv`
    document.body.appendChild(anchor)
    anchor.click()
    anchor.remove()
    URL.revokeObjectURL(url)
    toast.add({ title: 'Dues evidence CSV ready', color: 'success' })
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'Could not export dues evidence CSV.')
    toast.add({ title: 'Could not export CSV', description: message, color: 'error' })
  } finally {
    exporting.value = false
  }
}

onMounted(async () => {
  await loadWorkspace()
  await runReport()
})
useHead({ title: 'Dues Reconciliation - Garmetix CRM' })
</script>

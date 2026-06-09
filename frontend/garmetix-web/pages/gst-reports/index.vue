<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const hsnReport = ref<any | null>(null)
const taxReport = ref<any | null>(null)
const invoiceRegister = ref<any | null>(null)
const today = new Date()
const filters = reactive({
  returnPeriod: `${String(today.getMonth() + 1).padStart(2, '0')}${today.getFullYear()}`,
  direction: 'both'
})

const directionOptions = [
  { label: 'Both', value: 'both' },
  { label: 'Sales / Outward', value: 'sales' },
  { label: 'Purchase / Inward', value: 'purchase' }
]

const hsnRows = computed(() => hsnReport.value?.rows || [])
const taxRows = computed(() => taxReport.value?.rows || [])
const registerRows = computed(() => invoiceRegister.value?.rows || [])

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function number(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function query(includeDirection = true) {
  const params = new URLSearchParams()
  params.set('returnPeriod', filters.returnPeriod)
  if (includeDirection) {
    params.set('direction', filters.direction)
  }
  return params.toString()
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) {
    return
  }
  const [companyRows, storeRows] = await Promise.all([
    api.list<any>('companies'),
    api.list<any>('stores')
  ])
  companies.value = companyRows
  stores.value = storeRows
}

async function refreshAll() {
  await refreshShell()
  await loadReports()
}

async function loadReports() {
  if (!filters.returnPeriod || !/^\d{6}$/.test(filters.returnPeriod)) {
    feedback.notify('Invalid return period', 'Use MMYYYY format, for example 042026.', 'warning')
    return
  }
  loading.value = true
  try {
    const [hsn, tax, register] = await Promise.all([
      api.get<any>(`gst-returns/reports/hsn-summary?${query(true)}`),
      api.get<any>(`gst-returns/reports/tax-summary?${query(false)}`),
      api.get<any>(`gst-returns/reports/invoice-register?${query(true)}`)
    ])
    hsnReport.value = hsn
    taxReport.value = tax
    invoiceRegister.value = register
    feedback.notify('GST reports refreshed', `${hsn.rowCount || 0} HSN rows and ${register.rowCount || 0} register rows loaded.`, 'success')
  } catch (error: any) {
    feedback.fromError(error, 'Unable to load GST reports')
  } finally {
    loading.value = false
  }
}

async function downloadCsv(kind: 'hsn-summary' | 'tax-summary' | 'invoice-register') {
  const includeDirection = kind !== 'tax-summary'
  const path = `${config.public.apiBase}/gst-returns/reports/${kind}/csv?${query(includeDirection)}`
  try {
    const response = await fetch(path, { headers: api.authHeaders() })
    if (!response.ok) {
      throw new Error(await response.text())
    }
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `Garmetix-${kind}-${filters.returnPeriod}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'CSV download failed')
  }
}

onMounted(async () => {
  auth.restore()
  await refreshShell()
  if (auth.isAuthenticated.value) {
    await loadReports()
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refreshShell" />

  <AppShell
    v-else
    title="GST Reports"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
  >
    <section class="gst-report-page">
      <UCard class="planner-card">
        <div class="gst-report-header">
          <div>
            <p class="eyebrow">Stage 4C</p>
            <h1>GST / HSN Reports</h1>
            <p>Book-based HSN summary, GST rate reconciliation, and invoice register using stored invoice item snapshots.</p>
          </div>
          <div class="gst-report-actions">
            <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="loadReports" />
          </div>
        </div>
        <div class="gst-report-filters">
          <UFormField label="Return period (MMYYYY)">
            <UInput v-model="filters.returnPeriod" placeholder="042026" />
          </UFormField>
          <UFormField label="Direction">
            <USelect v-model="filters.direction" :items="directionOptions" />
          </UFormField>
        </div>
      </UCard>

      <div class="gst-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-receipt-text" color="primary" variant="subtle" /><div><p>HSN Rows</p><strong>{{ number(hsnReport?.rowCount || 0) }}</strong><span>Total {{ money(hsnReport?.totalValue || 0) }}</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-arrow-up-right" color="error" variant="subtle" /><div><p>Output Tax</p><strong>{{ money((taxReport?.outputCgstAmount || 0) + (taxReport?.outputSgstAmount || 0) + (taxReport?.outputIgstAmount || 0)) }}</strong><span>Sales taxable {{ money(taxReport?.outputTaxableValue || 0) }}</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-arrow-down-left" color="success" variant="subtle" /><div><p>Input Tax</p><strong>{{ money((taxReport?.inputCgstAmount || 0) + (taxReport?.inputSgstAmount || 0) + (taxReport?.inputIgstAmount || 0)) }}</strong><span>Purchase taxable {{ money(taxReport?.inputTaxableValue || 0) }}</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-scale" color="warning" variant="subtle" /><div><p>Net Payable</p><strong>{{ money(taxReport?.netTaxPayable || 0) }}</strong><span>Output less input</span></div></div></UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header"><div><h2>HSN Summary</h2><p>Uses invoice item HSN snapshot first, then product HSN fallback.</p></div><UButton size="sm" variant="subtle" icon="i-lucide-download" label="CSV" @click="downloadCsv('hsn-summary')" /></div>
        </template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>#</th><th>Type</th><th>HSN</th><th>Description</th><th>UQC</th><th>Rate</th><th>Qty</th><th>Taxable</th><th>CGST</th><th>SGST</th><th>IGST</th><th>Total</th></tr></thead>
            <tbody>
              <tr v-for="row in hsnRows" :key="`${row.direction}-${row.hsnCode}-${row.rate}-${row.serialNumber}`">
                <td>{{ row.serialNumber }}</td><td>{{ row.direction }}</td><td>{{ row.hsnCode }}</td><td>{{ row.description }}</td><td>{{ row.uqc }}</td><td>{{ number(row.rate) }}%</td><td>{{ number(row.quantity) }}</td><td>{{ money(row.taxableValue) }}</td><td>{{ money(row.cgstAmount) }}</td><td>{{ money(row.sgstAmount) }}</td><td>{{ money(row.igstAmount) }}</td><td>{{ money(row.totalValue) }}</td>
              </tr>
              <tr v-if="!hsnRows.length"><td colspan="12" class="empty">No HSN rows found for this period.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header"><div><h2>GST Rate Reconciliation</h2><p>Output tax, input tax, and net payable grouped by GST rate.</p></div><UButton size="sm" variant="subtle" icon="i-lucide-download" label="CSV" @click="downloadCsv('tax-summary')" /></div>
        </template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Rate</th><th>Sales Taxable</th><th>Sales CGST</th><th>Sales SGST</th><th>Sales IGST</th><th>Purchase Taxable</th><th>Purchase CGST</th><th>Purchase SGST</th><th>Purchase IGST</th><th>Net Payable</th></tr></thead>
            <tbody>
              <tr v-for="row in taxRows" :key="row.rate">
                <td>{{ number(row.rate) }}%</td><td>{{ money(row.salesTaxableValue) }}</td><td>{{ money(row.salesCgstAmount) }}</td><td>{{ money(row.salesSgstAmount) }}</td><td>{{ money(row.salesIgstAmount) }}</td><td>{{ money(row.purchaseTaxableValue) }}</td><td>{{ money(row.purchaseCgstAmount) }}</td><td>{{ money(row.purchaseSgstAmount) }}</td><td>{{ money(row.purchaseIgstAmount) }}</td><td>{{ money(row.netTaxPayable) }}</td>
              </tr>
              <tr v-if="!taxRows.length"><td colspan="10" class="empty">No GST rate rows found.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header"><div><h2>Invoice Register</h2><p>Sales and purchase register with GST split totals.</p></div><UButton size="sm" variant="subtle" icon="i-lucide-download" label="CSV" @click="downloadCsv('invoice-register')" /></div>
        </template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Type</th><th>Invoice</th><th>Ref</th><th>Date</th><th>Party</th><th>GSTIN</th><th>Status</th><th>Taxable</th><th>CGST</th><th>SGST</th><th>IGST</th><th>Bill</th></tr></thead>
            <tbody>
              <tr v-for="row in registerRows" :key="`${row.direction}-${row.invoiceNumber}-${row.onDate}`">
                <td>{{ row.direction }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.referenceNumber || '-' }}</td><td>{{ formatDate(row.onDate) }}</td><td>{{ row.partyName }}</td><td>{{ row.partyGstin || '-' }}</td><td>{{ row.invoiceStatus }}</td><td>{{ money(row.taxableValue) }}</td><td>{{ money(row.cgstAmount) }}</td><td>{{ money(row.sgstAmount) }}</td><td>{{ money(row.igstAmount) }}</td><td>{{ money(row.billAmount) }}</td>
              </tr>
              <tr v-if="!registerRows.length"><td colspan="12" class="empty">No invoices found for this period.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.gst-report-page { display: grid; gap: 1rem; }
.gst-report-header, .section-header { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
.gst-report-header h1, .section-header h2 { margin: 0; }
.gst-report-header p, .section-header p { margin: .35rem 0 0; color: rgb(var(--color-gray-500)); }
.eyebrow { text-transform: uppercase; letter-spacing: .12em; font-size: .72rem; font-weight: 700; color: rgb(var(--color-primary-500)); }
.gst-report-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-top: 1rem; }
.gst-metric-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
.table-wrap { overflow: auto; }
table { width: 100%; border-collapse: collapse; min-width: 980px; }
th, td { padding: .65rem .7rem; border-bottom: 1px solid rgb(var(--color-gray-200)); text-align: left; font-size: .86rem; }
th { font-size: .75rem; text-transform: uppercase; letter-spacing: .05em; color: rgb(var(--color-gray-500)); background: rgb(var(--color-gray-50)); }
.empty { text-align: center; color: rgb(var(--color-gray-500)); padding: 1.5rem; }
.dark th { background: rgb(var(--color-gray-900)); }
.dark th, .dark td { border-color: rgb(var(--color-gray-800)); }
@media (max-width: 720px) { .gst-report-header, .section-header { flex-direction: column; } }
</style>

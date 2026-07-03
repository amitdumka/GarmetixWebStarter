<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const gstReviewContact = useGstReviewContact()

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const sendingReports = ref(false)
const reportShareOpen = ref(false)
const reportShareResponse = ref<any | null>(null)
const reportShare = reactive({
  toEmail: '',
  toName: '',
  whatsAppNumber: '',
  note: 'Please review the attached GST book reports.',
  includeHsnSummaryCsv: true,
  includeTaxSummaryCsv: true,
  includeInvoiceRegisterCsv: true
})
const loadError = ref('')
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
const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const reportAttachmentCount = computed(() => [reportShare.includeHsnSummaryCsv, reportShare.includeTaxSummaryCsv, reportShare.includeInvoiceRegisterCsv].filter(Boolean).length)
const canSendReports = computed(() => Boolean(reportShare.toEmail.trim() && reportAttachmentCount.value > 0))
const recentGstShareLogs = computed(() => gstReviewContact.shareLogs.value.slice(0, 5))

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
  if (selectedCompanyId.value) {
    params.set('companyId', selectedCompanyId.value)
  }
  if (includeDirection) {
    params.set('direction', filters.direction)
  }
  return params.toString()
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) {
    return
  }
  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check the service and try again.', 'GST report setup failed')
  }
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
  loadError.value = ''
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
    loadError.value = feedback.errorMessage(error, 'Please check the selected period and try again.', 'Unable to load GST reports')
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

function openReportShare() {
  gstReviewContact.applyTo(reportShare)
  reportShareResponse.value = null
  reportShareOpen.value = true
}
function saveGstReportContact() {
  gstReviewContact.save(reportShare)
  feedback.notify('CA contact saved', 'This Accountant/CA contact will be reused in GST Returns and GST Reports.', 'success')
}


async function sendGstReports() {
  if (!reportShare.toEmail.trim()) {
    feedback.notify('CA email required', 'Enter Accountant/CA email address before sending.', 'warning')
    return
  }
  if (!reportAttachmentCount.value) {
    feedback.notify('Select reports', 'Choose at least one GST report attachment.', 'warning')
    return
  }
  sendingReports.value = true
  try {
    await loadReports()
    if (!confirm(`Confirm sending GST book reports ${filters.returnPeriod} to ${reportShare.toEmail}?`)) {
      return
    }
    gstReviewContact.save(reportShare)
    reportShareResponse.value = await api.create<any>('gst-returns/reports/send-review', {
      companyId: selectedCompanyId.value || null,
      returnPeriod: filters.returnPeriod,
      direction: filters.direction,
      ...reportShare
    } as any)
    gstReviewContact.addLog({
      kind: `GST book reports (${filters.direction})`,
      returnPeriod: filters.returnPeriod,
      toEmail: reportShare.toEmail.trim(),
      toName: reportShare.toName,
      attachmentNames: reportShareResponse.value.attachmentNames || [],
      message: reportShareResponse.value.message
    })
    feedback.notify('GST reports sent', reportShareResponse.value.message || 'GST reports emailed to Accountant/CA.', 'success')
  } catch (error) {
    feedback.failed('GST report send failed', error)
  } finally {
    sendingReports.value = false
  }
}

function openReportWhatsApp() {
  const url = reportShareResponse.value?.whatsAppShareUrl
  if (!url) {
    feedback.notify('Send email first', 'WhatsApp share link is prepared after email delivery.', 'warning')
    return
  }
  window.open(url, '_blank', 'noopener,noreferrer')
}

onMounted(async () => {
  auth.restore()
  gstReviewContact.load()
  gstReviewContact.applyTo(reportShare)
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
            <p class="eyebrow">GST reports</p>
            <h1>GST / HSN Reports</h1>
            <p>Book-based HSN summary, GST rate reconciliation, and invoice register using stored invoice item snapshots.</p>
          </div>
          <div class="gst-report-actions">
            <UButton icon="i-lucide-send" color="primary" label="Send to CA" :loading="sendingReports" @click="openReportShare" />
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

      <UCard class="planner-card gst-report-share-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>GST Report Sharing</h3>
              <p>Review book-based HSN, tax summary and invoice register, then send to Accountant/CA with CSV attachments.</p>
            </div>
            <div class="setup-tabs">
              <UBadge color="primary" variant="subtle">{{ reportAttachmentCount }} files</UBadge>
              <UButton icon="i-lucide-send" color="primary" label="Review & Send" :loading="sendingReports" @click="openReportShare" />
            </div>
          </div>
        </template>
        <UAlert
          :icon="gstReviewContact.isConfigured.value ? 'i-lucide-user-check' : 'i-lucide-user-plus'"
          :color="gstReviewContact.isConfigured.value ? 'success' : 'warning'"
          variant="soft"
          :title="gstReviewContact.isConfigured.value ? 'Default Accountant/CA contact ready' : 'Add default Accountant/CA contact'"
          :description="gstReviewContact.isConfigured.value ? `${gstReviewContact.contact.toName || 'Accountant/CA'} <${gstReviewContact.contact.toEmail}> will auto-fill before report sharing.` : 'Open Review & Send once, enter the CA details, then save as default. It will be reused in GST Returns and GST Reports.'"
        />
        <UAlert
          v-if="reportShareResponse?.emailSent"
          icon="i-lucide-mail-check"
          color="success"
          variant="soft"
          title="GST reports emailed"
          :description="`${reportShareResponse.message} ${reportShareResponse.attachmentNames?.length || 0} files attached.`"
          :actions="[{ label: 'Share on WhatsApp', icon: 'i-lucide-message-circle', onClick: openReportWhatsApp }]"
        />
        <div v-if="recentGstShareLogs.length" class="gst-audit-list compact">
          <div v-for="row in recentGstShareLogs" :key="row.id" class="gst-audit-row">
            <strong>{{ row.kind }} · {{ row.returnPeriod }}</strong>
            <span>{{ row.toEmail }} · {{ new Date(row.sentAt).toLocaleString('en-IN') }}</span>
            <small>{{ row.attachmentNames.length }} attachment(s)</small>
          </div>
        </div>
      </UCard>

      <UiRegisterPanel
        title="HSN Summary"
        description="Uses invoice item HSN snapshot first, then product HSN fallback."
        :loading="loading"
        :error="loadError"
        :empty="hsnRows.length === 0"
        empty-title="No HSN rows found"
        empty-description="Change the return period or direction, then refresh."
        empty-icon="i-lucide-receipt-text"
        @retry="loadReports"
      >
        <template #actions><UButton size="sm" variant="subtle" icon="i-lucide-download" label="CSV" @click="downloadCsv('hsn-summary')" /></template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>#</th><th>Type</th><th>HSN</th><th>Description</th><th>UQC</th><th>Rate</th><th>Qty</th><th>Taxable</th><th>CGST</th><th>SGST</th><th>IGST</th><th>Total</th></tr></thead>
            <tbody>
              <tr v-for="row in hsnRows" :key="`${row.direction}-${row.hsnCode}-${row.rate}-${row.serialNumber}`">
                <td>{{ row.serialNumber }}</td><td>{{ row.direction }}</td><td>{{ row.hsnCode }}</td><td>{{ row.description }}</td><td>{{ row.uqc }}</td><td>{{ number(row.rate) }}%</td><td>{{ number(row.quantity) }}</td><td>{{ money(row.taxableValue) }}</td><td>{{ money(row.cgstAmount) }}</td><td>{{ money(row.sgstAmount) }}</td><td>{{ money(row.igstAmount) }}</td><td>{{ money(row.totalValue) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>

      <UiRegisterPanel
        title="GST Rate Reconciliation"
        description="Output tax, input tax, and net payable grouped by GST rate."
        :loading="loading"
        :error="loadError"
        :empty="taxRows.length === 0"
        empty-title="No GST rate rows found"
        empty-description="Change the return period and refresh the report."
        empty-icon="i-lucide-scale"
        @retry="loadReports"
      >
        <template #actions><UButton size="sm" variant="subtle" icon="i-lucide-download" label="CSV" @click="downloadCsv('tax-summary')" /></template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Rate</th><th>Sales Taxable</th><th>Sales CGST</th><th>Sales SGST</th><th>Sales IGST</th><th>Purchase Taxable</th><th>Purchase CGST</th><th>Purchase SGST</th><th>Purchase IGST</th><th>Net Payable</th></tr></thead>
            <tbody>
              <tr v-for="row in taxRows" :key="row.rate">
                <td>{{ number(row.rate) }}%</td><td>{{ money(row.salesTaxableValue) }}</td><td>{{ money(row.salesCgstAmount) }}</td><td>{{ money(row.salesSgstAmount) }}</td><td>{{ money(row.salesIgstAmount) }}</td><td>{{ money(row.purchaseTaxableValue) }}</td><td>{{ money(row.purchaseCgstAmount) }}</td><td>{{ money(row.purchaseSgstAmount) }}</td><td>{{ money(row.purchaseIgstAmount) }}</td><td>{{ money(row.netTaxPayable) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>

      <UiRegisterPanel
        title="Invoice Register"
        description="Sales and purchase register with GST split totals."
        :loading="loading"
        :error="loadError"
        :empty="registerRows.length === 0"
        empty-title="No invoices found"
        empty-description="Change the return period or direction, then refresh."
        empty-icon="i-lucide-files"
        @retry="loadReports"
      >
        <template #actions><UButton size="sm" variant="subtle" icon="i-lucide-download" label="CSV" @click="downloadCsv('invoice-register')" /></template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Type</th><th>Invoice</th><th>Ref</th><th>Date</th><th>Party</th><th>GSTIN</th><th>Status</th><th>Taxable</th><th>CGST</th><th>SGST</th><th>IGST</th><th>Bill</th></tr></thead>
            <tbody>
              <tr v-for="row in registerRows" :key="`${row.direction}-${row.invoiceNumber}-${row.onDate}`">
                <td>{{ row.direction }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.referenceNumber || '-' }}</td><td>{{ formatDate(row.onDate) }}</td><td>{{ row.partyName }}</td><td>{{ row.partyGstin || '-' }}</td><td>{{ row.invoiceStatus }}</td><td>{{ money(row.taxableValue) }}</td><td>{{ money(row.cgstAmount) }}</td><td>{{ money(row.sgstAmount) }}</td><td>{{ money(row.igstAmount) }}</td><td>{{ money(row.billAmount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiRegisterPanel>

      <UModal v-model:open="reportShareOpen" title="Review and send GST reports to Accountant/CA" :ui="{ content: 'max-w-3xl' }">
        <template #body>
          <div class="modal-stack">
            <UAlert
              icon="i-lucide-info"
              color="primary"
              variant="soft"
              title="Reports from main books"
              description="This will refresh reports from Billing, Purchase and GST accounting data, then email selected CSV files to the Accountant/CA after your confirmation."
            />
            <div class="form-two-column">
              <UFormField label="Accountant/CA Email" required><UInput v-model="reportShare.toEmail" type="email" placeholder="ca@example.com" /></UFormField>
              <UFormField label="Accountant/CA Name"><UInput v-model="reportShare.toName" placeholder="CA / Accountant" /></UFormField>
              <UFormField label="WhatsApp Mobile"><UInput v-model="reportShare.whatsAppNumber" placeholder="91XXXXXXXXXX" /></UFormField>
              <UFormField label="Return Period"><UInput v-model="filters.returnPeriod" placeholder="042026" /></UFormField>
            </div>
            <div class="inline-action-row">
              <UButton icon="i-lucide-save" color="neutral" variant="subtle" label="Save as default CA contact" type="button" @click="saveGstReportContact" />
              <UButton icon="i-lucide-rotate-ccw" color="neutral" variant="ghost" label="Use saved contact" type="button" @click="gstReviewContact.applyTo(reportShare)" />
            </div>
            <UFormField label="Message / note"><UTextarea v-model="reportShare.note" :rows="3" /></UFormField>
            <div class="gst-draft-summary">
              <label><UCheckbox v-model="reportShare.includeHsnSummaryCsv" /> <span>HSN summary CSV</span></label>
              <label><UCheckbox v-model="reportShare.includeTaxSummaryCsv" /> <span>GST tax summary CSV</span></label>
              <label><UCheckbox v-model="reportShare.includeInvoiceRegisterCsv" /> <span>Invoice register CSV</span></label>
            </div>
            <UAlert
              v-if="reportShareResponse?.emailSent"
              icon="i-lucide-message-circle"
              color="success"
              variant="soft"
              title="WhatsApp share ready"
              :description="reportShareResponse.whatsAppText"
              :actions="[{ label: 'Open WhatsApp', icon: 'i-lucide-message-circle', onClick: openReportWhatsApp }]"
            />
          </div>
        </template>
        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="reportShareOpen = false" />
            <UButton icon="i-lucide-send" color="primary" label="Confirm & Send Reports" :loading="sendingReports" :disabled="!canSendReports" @click="sendGstReports" />
          </div>
        </template>
      </UModal>
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
.table-wrap { overflow-x: auto; overflow-y: visible; }
table { width: 100%; border-collapse: collapse; min-width: 980px; }
th, td { padding: .65rem .7rem; border-bottom: 1px solid rgb(var(--color-gray-200)); text-align: left; font-size: .86rem; }
th { font-size: .75rem; text-transform: uppercase; letter-spacing: .05em; color: rgb(var(--color-gray-500)); background: rgb(var(--color-gray-50)); }
.empty { text-align: center; color: rgb(var(--color-gray-500)); padding: 1.5rem; }
.dark th { background: rgb(var(--color-gray-900)); }
.dark th, .dark td { border-color: rgb(var(--color-gray-800)); }
@media (max-width: 720px) { .gst-report-header, .section-header { flex-direction: column; } }
</style>

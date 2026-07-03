<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const today = new Date()
const filters = reactive({
  returnPeriod: `${String(today.getMonth() + 1).padStart(2, '0')}${today.getFullYear()}`,
  direction: 'both'
})
const status = reactive({
  hsnRows: 0,
  invoiceRows: 0,
  outputTax: 0,
  inputTax: 0,
  netTaxPayable: 0,
  dataIssues: 0,
  emailReady: false,
  backupReady: false
})
const acceptanceNote = ref('')
const GST_ACCEPTANCE_KEY = 'garmetix:gst-final-acceptance:v1'
const acceptanceSteps = ref([
  { key: 'sales', label: 'Sales / Billing checked', detail: 'Sales invoices and returns are entered for the GST period.' },
  { key: 'purchase', label: 'Purchase checked', detail: 'Purchase inward, purchase returns and ITC values are entered.' },
  { key: 'books', label: 'Accounting books checked', detail: 'Vouchers, cash vouchers, petty cash and ledgers are updated.' },
  { key: 'gstr1', label: 'GSTR-1 preview/export checked', detail: 'JSON/Excel preview has no blocking validation issues.' },
  { key: 'gstr3b', label: 'GSTR-3B preview/export checked', detail: 'Output tax, input tax and net payable are verified.' },
  { key: 'caEmail', label: 'CA email sent', detail: 'GST package/report sent by Brevo SMTP after confirmation.' },
  { key: 'whatsapp', label: 'WhatsApp share sent', detail: 'WhatsApp review link/text shared with Accountant/CA.' },
  { key: 'backup', label: 'Backup taken before filing', detail: 'Fresh backup created and verified before final filing.' }
])
const acceptanceState = reactive<Record<string, boolean>>({})

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const completedSteps = computed(() => acceptanceSteps.value.filter((step) => acceptanceState[step.key]).length)
const acceptanceReady = computed(() => completedSteps.value === acceptanceSteps.value.length && status.dataIssues === 0 && status.emailReady && status.backupReady)

function apiQuery(includeDirection = true) {
  const params = new URLSearchParams()
  params.set('returnPeriod', filters.returnPeriod)
  if (selectedCompanyId.value) params.set('companyId', selectedCompanyId.value)
  if (includeDirection) params.set('direction', filters.direction)
  return params.toString()
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function loadAcceptance() {
  if (typeof window === 'undefined') return
  try {
    const saved = JSON.parse(window.localStorage.getItem(GST_ACCEPTANCE_KEY) || '{}')
    if (saved.state) Object.assign(acceptanceState, saved.state)
    acceptanceNote.value = saved.note || ''
  } catch {
    // Ignore old browser cache.
  }
}

function saveAcceptance() {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(GST_ACCEPTANCE_KEY, JSON.stringify({
    state: acceptanceState,
    note: acceptanceNote.value,
    returnPeriod: filters.returnPeriod,
    savedAt: new Date().toISOString()
  }))
}

watch(acceptanceState, saveAcceptance, { deep: true })
watch(acceptanceNote, saveAcceptance)
watch(() => filters.returnPeriod, saveAcceptance)

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Workspace options could not be loaded.', 'GST final acceptance setup failed')
  }
}

async function runAcceptanceChecks() {
  if (!/^\d{6}$/.test(filters.returnPeriod)) {
    feedback.notify('Invalid return period', 'Use MMYYYY format, for example 042026.', 'warning')
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    const [hsn, tax, register, consistency, email, backup] = await Promise.all([
      api.get<any>(`gst-returns/reports/hsn-summary?${apiQuery(true)}`),
      api.get<any>(`gst-returns/reports/tax-summary?${apiQuery(false)}`),
      api.get<any>(`gst-returns/reports/invoice-register?${apiQuery(true)}`),
      api.get<any>('data-consistency/summary'),
      api.get<any>('email-diagnostics/status'),
      api.get<any>('backups/maintenance/status')
    ])

    status.hsnRows = hsn?.rowCount || 0
    status.invoiceRows = register?.rowCount || 0
    status.outputTax = Number(tax?.outputCgstAmount || 0) + Number(tax?.outputSgstAmount || 0) + Number(tax?.outputIgstAmount || 0)
    status.inputTax = Number(tax?.inputCgstAmount || 0) + Number(tax?.inputSgstAmount || 0) + Number(tax?.inputIgstAmount || 0)
    status.netTaxPayable = Number(tax?.netTaxPayable || 0)
    status.dataIssues = Number(consistency?.criticalIssues || 0)
    status.emailReady = Boolean(email?.ready || email?.isReady)
    status.backupReady = Boolean(backup?.hasRecentBackup && backup?.directoryWritable)

    feedback.notify('GST acceptance refreshed', 'Book reports, data consistency, email and backup readiness were checked.', 'success')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please review GST reports, data consistency, SMTP and backup setup.', 'GST final acceptance failed')
    feedback.failed('GST final acceptance failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  auth.restore()
  loadAcceptance()
  await refreshShell()
  if (auth.isAuthenticated.value) await runAcceptanceChecks()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="runAcceptanceChecks" />

  <AppShell
    v-else
    title="GST Final Acceptance"
    :companies="companies"
    :stores="stores"
    @refresh="runAcceptanceChecks"
  >
    <section class="gst-final-page">
      <UiModulePageHeader
        title="GST Final Acceptance"
        description="Final GST go-live checklist covering billing, purchase, accounting, GST exports, CA email/WhatsApp sharing and backup readiness."
        icon="i-lucide-badge-check"
        primary-label="Run Checks"
        primary-icon="i-lucide-refresh-cw"
        @primary="runAcceptanceChecks"
      >
        <template #actions>
          <UBadge :color="acceptanceReady ? 'success' : 'warning'" :label="acceptanceReady ? 'Ready for CA/Filing' : `${completedSteps}/${acceptanceSteps.length} accepted`" variant="subtle" />
          <UButton to="/gst-reports" icon="i-lucide-table-properties" variant="subtle" label="GST Reports" />
          <UButton to="/gst-returns" icon="i-lucide-file-json-2" variant="subtle" label="GST Returns" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="GST acceptance checks failed"
        :description="loadError"
      />

      <UCard class="planner-card">
        <div class="filter-grid">
          <UFormField label="Return period (MMYYYY)">
            <UInput v-model="filters.returnPeriod" placeholder="062026" />
          </UFormField>
          <UFormField label="Direction">
            <USelect v-model="filters.direction" :items="[
              { label: 'Both', value: 'both' },
              { label: 'Sales / Outward', value: 'sales' },
              { label: 'Purchase / Inward', value: 'purchase' }
            ]" />
          </UFormField>
        </div>
      </UCard>

      <div class="metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list" color="primary" variant="subtle" /><div><p>Invoice Rows</p><strong>{{ status.invoiceRows }}</strong><span>GST register rows</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-tags" color="neutral" variant="subtle" /><div><p>HSN Rows</p><strong>{{ status.hsnRows }}</strong><span>HSN summary lines</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-arrow-up-right" color="error" variant="subtle" /><div><p>Output Tax</p><strong>{{ money(status.outputTax) }}</strong><span>Sales GST</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-arrow-down-left" color="success" variant="subtle" /><div><p>Input Tax</p><strong>{{ money(status.inputTax) }}</strong><span>Purchase ITC</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-scale" color="warning" variant="subtle" /><div><p>Net Payable</p><strong>{{ money(status.netTaxPayable) }}</strong><span>Output less input</span></div></div></UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header">
            <div>
              <h2>Acceptance Checklist</h2>
              <p>Tick only after manual review. The system checks support the checklist but do not replace CA review.</p>
            </div>
            <UBadge :color="acceptanceReady ? 'success' : 'warning'" :label="acceptanceReady ? 'Complete' : 'Pending'" />
          </div>
        </template>
        <div class="check-grid">
          <label v-for="step in acceptanceSteps" :key="step.key" class="check-card">
            <UCheckbox v-model="acceptanceState[step.key]" />
            <span>
              <strong>{{ step.label }}</strong>
              <small>{{ step.detail }}</small>
            </span>
          </label>
        </div>
        <UFormField class="mt-4" label="Acceptance note / CA confirmation">
          <UTextarea v-model="acceptanceNote" :rows="3" placeholder="Example: Sent to CA on WhatsApp/email, CA confirmed figures, backup file name..." />
        </UFormField>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>System Readiness</h2></template>
        <div class="readiness-grid">
          <UAlert :color="status.dataIssues === 0 ? 'success' : 'error'" :icon="status.dataIssues === 0 ? 'i-lucide-check-circle-2' : 'i-lucide-circle-alert'" title="Data Consistency" :description="status.dataIssues === 0 ? 'No critical data consistency issue detected.' : `${status.dataIssues} critical issue(s) found. Open Data Consistency before filing.`" />
          <UAlert :color="status.emailReady ? 'success' : 'warning'" :icon="status.emailReady ? 'i-lucide-mail-check' : 'i-lucide-mail-warning'" title="Brevo SMTP" :description="status.emailReady ? 'Email diagnostics are ready.' : 'SMTP diagnostics are not ready. Test Brevo email before CA sharing.'" />
          <UAlert :color="status.backupReady ? 'success' : 'warning'" :icon="status.backupReady ? 'i-lucide-database-backup' : 'i-lucide-hard-drive-download'" title="Backup" :description="status.backupReady ? 'Recent writable backup is available.' : 'Create and verify a fresh backup before GST filing.'" />
        </div>
        <template #footer>
          <div class="footer-actions">
            <UButton to="/data-consistency" icon="i-lucide-shield-alert" variant="subtle" label="Data Consistency" />
            <UButton to="/backup-maintenance" icon="i-lucide-database-backup" variant="subtle" label="Backup Maintenance" />
            <UButton to="/production-readiness" icon="i-lucide-shield-check" variant="subtle" label="Production Readiness" />
          </div>
        </template>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.gst-final-page { display: grid; gap: 1rem; }
.filter-grid, .metric-grid, .check-grid, .readiness-grid {
  display: grid;
  gap: 1rem;
}
.filter-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.check-grid { grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
.readiness-grid { grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
.check-card {
  display: flex;
  gap: .75rem;
  border: 1px solid rgb(var(--color-gray-200));
  border-radius: 1rem;
  padding: 1rem;
}
.check-card strong { display: block; }
.check-card small { color: rgb(var(--color-gray-500)); }
.section-header, .footer-actions {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
  flex-wrap: wrap;
}
.dark .check-card { border-color: rgb(var(--color-gray-800)); }
</style>

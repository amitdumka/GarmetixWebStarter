<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker"><UIcon name="i-lucide-badge-check" class="size-4" /> Owner/Admin sign-off</p>
          <h2 class="garmetix-dashboard-title">GST Final Acceptance</h2>
          <p class="garmetix-dashboard-subtitle">
            Manual go/no-go checklist for GST filing readiness, backed by live HSN/tax/invoice-register totals and system readiness checks.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <UBadge :color="acceptanceReady ? 'success' : 'warning'" variant="subtle">
            {{ acceptanceReady ? 'Ready for CA/Filing' : `${completedSteps}/${acceptanceSteps.length} accepted` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="runAcceptanceChecks">Run Checks</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <div class="grid gap-3 xl:grid-cols-12">
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Return Period (MMYYYY)</span>
          <UInput v-model="filters.returnPeriod" placeholder="MMYYYY" />
        </label>
        <label class="space-y-1 text-sm xl:col-span-3">
          <span class="text-muted">Direction</span>
          <USelect v-model="filters.direction" :items="directionItems" />
        </label>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div v-for="card in metricCards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Acceptance Checklist</h3>
      <div class="grid gap-3 md:grid-cols-2">
        <label v-for="step in acceptanceSteps" :key="step.key" class="garmetix-row-card flex items-start gap-3">
          <UCheckbox v-model="acceptanceState[step.key]" />
          <span>
            <span class="block font-medium">{{ step.label }}</span>
            <span class="block text-xs text-muted">{{ step.detail }}</span>
          </span>
        </label>
      </div>
      <label class="mt-4 block space-y-1 text-sm">
        <span class="text-muted">Acceptance Note / CA Confirmation</span>
        <UTextarea v-model="acceptanceNote" :rows="3" />
      </label>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">System Readiness</h3>
      <div class="grid gap-3 md:grid-cols-3">
        <UAlert
          :color="status.dataIssues === 0 ? 'success' : 'error'"
          variant="subtle"
          :icon="status.dataIssues === 0 ? 'i-lucide-circle-check' : 'i-lucide-triangle-alert'"
          :title="'Data Consistency'"
          :description="status.dataIssues === 0 ? 'No critical data consistency issues.' : `${status.dataIssues} critical issue(s) found.`"
        />
        <UAlert
          :color="status.emailReady ? 'success' : 'warning'"
          variant="subtle"
          :icon="status.emailReady ? 'i-lucide-circle-check' : 'i-lucide-triangle-alert'"
          :title="'Email (Brevo SMTP)'"
          :description="status.emailReady ? 'Email delivery is configured and ready.' : 'Email delivery is not confirmed ready.'"
        />
        <UAlert
          :color="status.backupReady ? 'success' : 'warning'"
          variant="subtle"
          :icon="status.backupReady ? 'i-lucide-circle-check' : 'i-lucide-triangle-alert'"
          :title="'Backup'"
          :description="status.backupReady ? 'A recent backup exists and the backup directory is writable.' : 'No recent backup confirmed.'"
        />
      </div>
      <div class="mt-4 flex flex-wrap gap-2">
        <UButton size="sm" color="neutral" variant="soft" to="/data-consistency">Data Consistency</UButton>
        <UButton size="sm" color="neutral" variant="soft" to="/backup-maintenance">Backup Maintenance</UButton>
        <UButton size="sm" color="neutral" variant="soft" to="/production-readiness">Production Readiness</UButton>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readNumber, useAdminApiClient, type ApiRecord } from '../utils/admin-api'

useHead({ title: 'GST Final Acceptance - Garmetix Admin' })

const ACCEPTANCE_KEY = 'garmetix:gst-final-acceptance:v1'

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')

const acceptanceSteps = [
  { key: 'sales', label: 'Sales / Billing checked', detail: 'Sales invoices and returns are entered for the GST period.' },
  { key: 'purchase', label: 'Purchase checked', detail: 'Purchase inward, purchase returns and ITC values are entered.' },
  { key: 'books', label: 'Accounting books checked', detail: 'Vouchers, cash vouchers, petty cash and ledgers are updated.' },
  { key: 'gstr1', label: 'GSTR-1 preview/export checked', detail: 'JSON/Excel preview has no blocking validation issues.' },
  { key: 'gstr3b', label: 'GSTR-3B preview/export checked', detail: 'Output tax, input tax and net payable are verified.' },
  { key: 'caEmail', label: 'CA email sent', detail: 'GST package/report sent to the Accountant/CA after confirmation.' },
  { key: 'whatsapp', label: 'WhatsApp share sent', detail: 'WhatsApp review link/text shared with Accountant/CA.' },
  { key: 'backup', label: 'Backup taken before filing', detail: 'Fresh backup created and verified before final filing.' }
] as const

const directionItems = [
  { label: 'Both', value: 'both' },
  { label: 'Sales (Outward)', value: 'sales' },
  { label: 'Purchase (Inward)', value: 'purchase' }
]

const filters = reactive({ returnPeriod: currentReturnPeriod(), direction: 'both' })
const acceptanceState = reactive<Record<string, boolean>>(Object.fromEntries(acceptanceSteps.map(step => [step.key, false])))
const acceptanceNote = ref('')
const status = reactive({
  invoiceRows: 0,
  hsnRows: 0,
  outputTax: 0,
  inputTax: 0,
  netTaxPayable: 0,
  dataIssues: 0,
  emailReady: false,
  backupReady: false
})

const completedSteps = computed(() => acceptanceSteps.filter(step => acceptanceState[step.key]).length)
const acceptanceReady = computed(() => completedSteps.value === acceptanceSteps.length && status.dataIssues === 0 && status.emailReady && status.backupReady)
const metricCards = computed(() => [
  { label: 'Invoice Rows', value: status.invoiceRows },
  { label: 'HSN Rows', value: status.hsnRows },
  { label: 'Output Tax', value: money(status.outputTax) },
  { label: 'Input Tax', value: money(status.inputTax) },
  { label: 'Net Payable', value: money(status.netTaxPayable) }
])

function currentReturnPeriod() {
  const date = new Date()
  return `${String(date.getMonth() + 1).padStart(2, '0')}${date.getFullYear()}`
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(value || 0)
}

function canUseStorage() {
  return typeof window !== 'undefined' && Boolean(window.localStorage)
}

function loadAcceptance() {
  if (!canUseStorage()) return
  try {
    const raw = window.localStorage.getItem(ACCEPTANCE_KEY)
    if (!raw) return
    const saved = JSON.parse(raw)
    if (saved.state && typeof saved.state === 'object') Object.assign(acceptanceState, saved.state)
    if (typeof saved.note === 'string') acceptanceNote.value = saved.note
    if (typeof saved.returnPeriod === 'string' && saved.returnPeriod) filters.returnPeriod = saved.returnPeriod
  } catch {
    // Ignore bad browser cache and let the operator re-check the acceptance boxes.
  }
}

function saveAcceptance() {
  if (!canUseStorage()) return
  window.localStorage.setItem(ACCEPTANCE_KEY, JSON.stringify({
    state: { ...acceptanceState },
    note: acceptanceNote.value,
    returnPeriod: filters.returnPeriod,
    savedAt: new Date().toISOString()
  }))
}

function apiQuery(includeDirection: boolean) {
  const query: Record<string, string> = { returnPeriod: filters.returnPeriod }
  if (includeDirection) query.direction = filters.direction
  return query
}

async function runAcceptanceChecks() {
  if (!/^\d{6}$/.test(filters.returnPeriod)) {
    error.value = 'Return period must be MMYYYY, for example 072026.'
    return
  }

  loading.value = true
  error.value = ''
  try {
    const [hsnResult, taxResult, registerResult, consistencyResult, emailResult, backupResult] = await Promise.allSettled([
      get<ApiRecord>('gst-returns/reports/hsn-summary', apiQuery(true)),
      get<ApiRecord>('gst-returns/reports/tax-summary', apiQuery(false)),
      get<ApiRecord>('gst-returns/reports/invoice-register', apiQuery(true)),
      get<ApiRecord>('data-consistency/summary'),
      get<ApiRecord>('email-diagnostics/status'),
      get<ApiRecord>('backups/maintenance/status')
    ])

    if (hsnResult.status === 'fulfilled') status.hsnRows = readNumber(hsnResult.value, ['rowCount'])
    if (taxResult.status === 'fulfilled') {
      const tax = taxResult.value
      status.outputTax = readNumber(tax, ['outputCgstAmount']) + readNumber(tax, ['outputSgstAmount']) + readNumber(tax, ['outputIgstAmount'])
      status.inputTax = readNumber(tax, ['inputCgstAmount']) + readNumber(tax, ['inputSgstAmount']) + readNumber(tax, ['inputIgstAmount'])
      status.netTaxPayable = readNumber(tax, ['netTaxPayable'])
    }
    if (registerResult.status === 'fulfilled') status.invoiceRows = readNumber(registerResult.value, ['rowCount'])
    if (consistencyResult.status === 'fulfilled') status.dataIssues = readNumber(consistencyResult.value, ['criticalIssues'])
    if (emailResult.status === 'fulfilled') {
      const email = emailResult.value as ApiRecord
      status.emailReady = Boolean(email.ready ?? email.isReady)
    }
    if (backupResult.status === 'fulfilled') {
      const backup = backupResult.value as ApiRecord
      status.backupReady = Boolean(backup.hasRecentBackup) && Boolean(backup.directoryWritable)
    }

    const failed = [hsnResult, taxResult, registerResult, consistencyResult, emailResult, backupResult].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} readiness request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to run GST final acceptance checks.'
  } finally {
    loading.value = false
  }
}

watch(acceptanceState, saveAcceptance, { deep: true })
watch(acceptanceNote, saveAcceptance)
watch(() => filters.returnPeriod, saveAcceptance)

onMounted(() => {
  loadAcceptance()
  runAcceptanceChecks()
})
</script>

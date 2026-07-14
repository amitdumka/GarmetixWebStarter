<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated

const loading = ref(false)
const savingEvidence = ref(false)
const exportLoading = ref(false)
const loadError = ref('')
const status = ref<any | null>(null)
const evidenceRows = ref<any[]>([])
const evidenceResult = ref<any | null>(null)
const PRINT_ACCEPTANCE_KEY = 'garmetix:print-final-acceptance:v2'
const acceptanceState = reactive<Record<string, { checked: boolean, result: string, remarks: string }>>({})
const evidenceMeta = reactive({
  operatorName: '',
  liveBaseUrl: '',
  browserName: 'Chrome / Edge browser print preview',
  printerName: '',
  note: ''
})

const documents = computed(() => status.value?.documents || [])
const scenarios = computed(() => status.value?.scenarios || [])
const readyCount = computed(() => status.value?.readyCount || 0)
const totalCount = computed(() => status.value?.totalCount || documents.value.length)
const passedScenarioCount = computed(() => scenarios.value.filter((scenario: any) => stateOf(scenario.key).checked && stateOf(scenario.key).result === 'Pass').length)
const requiredScenarioCount = computed(() => scenarios.value.filter((scenario: any) => scenario.required).length || scenarios.value.length)
const allScenariosPassed = computed(() => requiredScenarioCount.value > 0 && passedScenarioCount.value === requiredScenarioCount.value)
const documentReady = computed(() => totalCount.value > 0 && readyCount.value === totalCount.value)
const finalAccepted = computed(() => documentReady.value && allScenariosPassed.value)
const closure = computed(() => status.value?.closure || null)
const closureBlockingIssues = computed(() => closure.value?.blockingIssues || [])
const finalCloseoutChecklist = computed(() => closure.value?.finalCloseoutChecklist || [])
const knownLimitations = computed(() => closure.value?.knownLimitations || [])
const operatorRules = computed(() => closure.value?.operatorRules || [])
const nextModuleCandidates = computed(() => closure.value?.nextModuleCandidates || [])

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

function statusColor(value: string) {
  if (value === 'Ready' || value === 'Accepted') return 'success'
  if (value === 'Needs Review' || value === 'Missing sample') return 'warning'
  return 'primary'
}

function stateOf(key: string) {
  if (!acceptanceState[key]) {
    acceptanceState[key] = { checked: false, result: 'Pending', remarks: '' }
  }
  return acceptanceState[key]
}

function loadAcceptance() {
  if (typeof window === 'undefined') return
  try {
    const saved = JSON.parse(window.localStorage.getItem(PRINT_ACCEPTANCE_KEY) || '{}')
    if (saved.state) Object.assign(acceptanceState, saved.state)
    if (saved.meta) Object.assign(evidenceMeta, saved.meta)
  } catch {
    // Ignore local checklist cache.
  }
}

function saveAcceptance() {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PRINT_ACCEPTANCE_KEY, JSON.stringify({
    state: acceptanceState,
    meta: evidenceMeta,
    savedAt: new Date().toISOString()
  }))
}

watch(acceptanceState, saveAcceptance, { deep: true })
watch(evidenceMeta, saveAcceptance, { deep: true })

function ensureScenarioState() {
  for (const scenario of scenarios.value) {
    stateOf(scenario.key)
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    status.value = await api.get<any>('print-acceptance/status')
    evidenceRows.value = status.value?.recentEvidence || []
    ensureScenarioState()
    feedback.notify('Print acceptance refreshed', 'Sample document availability and evidence history were checked.', 'success')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check admin permission and API service.', 'Print acceptance failed')
    feedback.failed('Print acceptance failed', error)
  } finally {
    loading.value = false
  }
}

async function loadEvidence() {
  try {
    evidenceRows.value = await api.list<any>('print-acceptance/evidence')
  } catch (error) {
    feedback.failed('Print evidence history failed', error)
  }
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

async function downloadEvidenceCsv() {
  if (!import.meta.client) return
  exportLoading.value = true
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/print-acceptance/evidence.csv`, {
      headers: api.authHeaders() as Record<string, string>,
      responseType: 'blob'
    })
    downloadBlob(blob, `garmetix-print-final-acceptance-${new Date().toISOString().slice(0, 10)}.csv`)
    feedback.notify('Print evidence CSV downloaded', 'Attach this CSV with final production handover evidence.', 'success')
  } catch (error) {
    feedback.failed('Print evidence CSV export failed', error)
  } finally {
    exportLoading.value = false
  }
}

function absoluteUrl(endpoint?: string) {
  if (!endpoint) return ''
  return `${config.public.apiBase}${endpoint.replace(/^\/api/, '')}`
}

async function openEndpoint(doc: any) {
  if (!doc?.endpoint) {
    feedback.notify('No sample found', 'Create a sample record first, then refresh print acceptance.', 'warning')
    return
  }

  window.open(absoluteUrl(doc.endpoint), '_blank', 'noopener,noreferrer')
}

function openScenario(scenario: any) {
  if (!scenario?.sampleEndpoint) {
    feedback.notify('No sample found', 'Create a sale or purchase sample first, then refresh print acceptance.', 'warning')
    return
  }

  window.open(absoluteUrl(scenario.sampleEndpoint), '_blank', 'noopener,noreferrer')
}

function markPassed(key: string) {
  const state = stateOf(key)
  state.checked = true
  state.result = 'Pass'
}

function markFailed(key: string) {
  const state = stateOf(key)
  state.checked = true
  state.result = 'Fail'
}

function resetEvidenceDraft() {
  for (const key of Object.keys(acceptanceState)) {
    acceptanceState[key] = { checked: false, result: 'Pending', remarks: '' }
  }
  evidenceMeta.note = ''
  evidenceResult.value = null
}

async function saveFinalEvidence() {
  savingEvidence.value = true
  try {
    const items = scenarios.value.map((scenario: any) => {
      const state = stateOf(scenario.key)
      return {
        key: scenario.key,
        label: scenario.label,
        area: scenario.area,
        paperSize: scenario.paperSize,
        checked: state.checked,
        result: state.checked ? state.result : 'Pending',
        endpoint: scenario.sampleEndpoint,
        remarks: state.remarks
      }
    })

    evidenceResult.value = await api.create<any>('print-acceptance/evidence', {
      operatorName: evidenceMeta.operatorName,
      liveBaseUrl: evidenceMeta.liveBaseUrl,
      browserName: evidenceMeta.browserName,
      printerName: evidenceMeta.printerName,
      note: evidenceMeta.note,
      items
    })

    await refresh()
    feedback.notify('Print evidence saved', `${evidenceResult.value.reference} saved with status ${evidenceResult.value.status}.`, evidenceResult.value.status === 'Accepted' ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Print evidence save failed', error)
  } finally {
    savingEvidence.value = false
  }
}

onMounted(async () => {
  auth.restore()
  loadAcceptance()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Print Final Acceptance"
    @refresh="refresh"
  >
    <section class="print-acceptance-page">
      <UiModulePageHeader
        title="Print Final Acceptance"
        description="Stage 11D-129 final PDF/print acceptance closure with Complete/Not Complete status, CSV evidence export, and live-printer operator rules."
        icon="i-lucide-printer-check"
        primary-label="Run Checks"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="closure?.complete ? 'success' : 'warning'" :label="closure?.status || (finalAccepted ? 'Ready for evidence save' : `${passedScenarioCount}/${requiredScenarioCount} print checks passed`)" variant="subtle" />
          <UButton icon="i-lucide-file-down" :loading="exportLoading" variant="soft" label="Export Evidence CSV" @click="downloadEvidenceCsv" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Print acceptance unavailable"
        :description="loadError"
      />

      <div class="metric-grid">
        <UCard class="planner-metric-card">
          <div class="planner-metric-body"><UAvatar icon="i-lucide-files" color="primary" variant="subtle" /><div><p>Samples Ready</p><strong>{{ readyCount }}/{{ totalCount }}</strong><span>Available source records</span></div></div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body"><UAvatar icon="i-lucide-check-check" :color="allScenariosPassed ? 'success' : 'warning'" variant="subtle" /><div><p>Print Checks</p><strong>{{ passedScenarioCount }}/{{ requiredScenarioCount }}</strong><span>A4/A5 evidence checks</span></div></div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body"><UAvatar icon="i-lucide-history" color="neutral" variant="subtle" /><div><p>Evidence Records</p><strong>{{ evidenceRows.length }}</strong><span>Saved in audit log</span></div></div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body"><UAvatar icon="i-lucide-clipboard-check" :color="closure?.complete ? 'success' : 'warning'" variant="subtle" /><div><p>Closure</p><strong>{{ closure?.status || 'Checking' }}</strong><span>{{ closure ? `${closure.coreSamplesReady}/${closure.coreSamplesRequired} core samples` : 'Backend closure status' }}</span></div></div>
        </UCard>
      </div>

      <UCard v-if="closure" class="planner-card closure-card">
        <template #header>
          <div class="section-header">
            <div>
              <h2>Final print/PDF closure</h2>
              <p>Backend status is based on required sale/purchase samples plus saved Accepted evidence for every required print scenario.</p>
            </div>
            <UBadge :color="closure.complete ? 'success' : 'warning'" :label="closure.status" variant="subtle" />
          </div>
        </template>
        <div class="closure-grid">
          <div class="closure-box"><span>Core samples</span><strong>{{ closure.coreSamplesReady }}/{{ closure.coreSamplesRequired }}</strong></div>
          <div class="closure-box"><span>Latest evidence</span><strong>{{ closure.latestPassedCount }}/{{ closure.requiredEvidenceCount }}</strong></div>
          <div class="closure-box"><span>Last accepted</span><strong>{{ closure.lastAcceptedReference || '-' }}</strong></div>
        </div>
        <UAlert
          v-if="closureBlockingIssues.length"
          class="mt-4"
          color="warning"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Closure blockers"
          :description="closureBlockingIssues.join(' · ')"
        />
        <div class="evidence-columns mt-4">
          <div>
            <h3>Final closeout checklist</h3>
            <ul><li v-for="item in finalCloseoutChecklist" :key="item">{{ item }}</li></ul>
          </div>
          <div>
            <h3>Operator rules</h3>
            <ul><li v-for="item in operatorRules" :key="item">{{ item }}</li></ul>
          </div>
          <div>
            <h3>Known limitations</h3>
            <ul><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul>
          </div>
        </div>
        <div v-if="nextModuleCandidates.length" class="mt-4 next-module-box">
          <strong>Next after print closure:</strong>
          <span>{{ nextModuleCandidates.join(' / ') }}</span>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header">
            <div>
              <h2>Sample Documents</h2>
              <p>Open each available source document before running the final A4/A5 evidence checklist.</p>
            </div>
            <UButton icon="i-lucide-refresh-cw" :loading="loading" variant="subtle" label="Refresh" @click="refresh" />
          </div>
        </template>

        <div class="document-grid">
          <div v-for="doc in documents" :key="doc.key" class="document-card">
            <div class="document-top">
              <div>
                <UBadge :label="doc.area" variant="subtle" />
                <h3>{{ doc.label }}</h3>
                <p>{{ doc.message }}</p>
              </div>
              <UBadge :color="statusColor(doc.status)" :label="doc.status" />
            </div>
            <dl>
              <div><dt>Latest</dt><dd>{{ doc.latestNumber || '-' }}</dd></div>
              <div><dt>Date</dt><dd>{{ formatDate(doc.latestDate) }}</dd></div>
              <div><dt>Count</dt><dd>{{ doc.count }}</dd></div>
            </dl>
            <div class="document-actions">
              <UButton icon="i-lucide-external-link" color="primary" variant="soft" label="Open sample" :disabled="!doc.endpoint" @click="openEndpoint(doc)" />
            </div>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header">
            <div>
              <h2>Final PDF print evidence checklist</h2>
              <p>Use live URL browser print preview or real printer output, then mark each required check as passed or failed.</p>
            </div>
            <UBadge :color="allScenariosPassed ? 'success' : 'warning'" :label="`${passedScenarioCount}/${requiredScenarioCount} required passed`" variant="subtle" />
          </div>
        </template>

        <div class="scenario-grid">
          <div v-for="scenario in scenarios" :key="scenario.key" class="scenario-card">
            <div class="document-top">
              <div>
                <UBadge :label="scenario.paperSize" variant="subtle" />
                <h3>{{ scenario.label }}</h3>
                <p>{{ scenario.expectedEvidence }}</p>
              </div>
              <UBadge :color="stateOf(scenario.key).result === 'Pass' ? 'success' : stateOf(scenario.key).result === 'Fail' ? 'error' : 'warning'" :label="stateOf(scenario.key).result" />
            </div>
            <div class="document-actions">
              <UButton icon="i-lucide-external-link" variant="soft" label="Open PDF" :disabled="!scenario.sampleEndpoint" @click="openScenario(scenario)" />
              <UButton icon="i-lucide-check" color="success" variant="soft" label="Mark pass" @click="markPassed(scenario.key)" />
              <UButton icon="i-lucide-x" color="error" variant="ghost" label="Mark fail" @click="markFailed(scenario.key)" />
            </div>
            <UFormField label="Evidence remarks">
              <UTextarea v-model="stateOf(scenario.key).remarks" :rows="2" placeholder="Example: verified A5 preview, footer not clipped, totals match screen..." />
            </UFormField>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Save backend evidence</h2></template>
        <div class="form-grid">
          <UFormField label="Operator name">
            <UInput v-model="evidenceMeta.operatorName" placeholder="Who verified print/PDF output" />
          </UFormField>
          <UFormField label="Live URL tested">
            <UInput v-model="evidenceMeta.liveBaseUrl" placeholder="https://garmetix.aadwikafashion.in" />
          </UFormField>
          <UFormField label="Browser / device">
            <UInput v-model="evidenceMeta.browserName" />
          </UFormField>
          <UFormField label="Printer / PDF target">
            <UInput v-model="evidenceMeta.printerName" placeholder="Save as PDF / physical printer name" />
          </UFormField>
        </div>
        <UFormField class="mt-4" label="Final acceptance note">
          <UTextarea v-model="evidenceMeta.note" :rows="3" placeholder="Example: Verified Sale A4/A5, Purchase A4/A5, large invoice pagination, amount box, footer, signature and page summary on live server." />
        </UFormField>
        <div class="footer-actions mt-4">
          <UButton icon="i-lucide-save" color="primary" :loading="savingEvidence" label="Save Print Evidence" @click="saveFinalEvidence" />
          <UButton icon="i-lucide-file-down" :loading="exportLoading" variant="soft" label="Export Evidence CSV" @click="downloadEvidenceCsv" />
          <UButton icon="i-lucide-rotate-ccw" variant="ghost" label="Reset Draft" @click="resetEvidenceDraft" />
        </div>
        <UAlert
          v-if="evidenceResult"
          class="mt-4"
          :color="evidenceResult.status === 'Accepted' ? 'success' : 'warning'"
          variant="subtle"
          icon="i-lucide-file-check-2"
          :title="`Evidence ${evidenceResult.reference} saved as ${evidenceResult.status}`"
          :description="`${evidenceResult.passedCount}/${evidenceResult.requiredCount} required checks passed.`"
        />
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Saved evidence history</h2></template>
        <div v-if="!evidenceRows.length" class="empty-state">No backend print acceptance evidence saved yet.</div>
        <div v-else class="evidence-list">
          <div v-for="item in evidenceRows" :key="item.id" class="evidence-row">
            <div>
              <strong>{{ item.reference }}</strong>
              <p>{{ item.operatorName || 'Operator' }} · {{ formatDate(item.occurredAt) }}</p>
              <p v-if="item.liveBaseUrl">{{ item.liveBaseUrl }}</p>
              <p v-if="item.note">{{ item.note }}</p>
            </div>
            <div class="evidence-badges">
              <UBadge :color="statusColor(item.status)" :label="item.status" />
              <UBadge variant="subtle" :label="`${item.passedCount}/${item.requiredCount}`" />
            </div>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Quick navigation</h2></template>
        <div class="footer-actions">
          <UButton to="/billing" icon="i-lucide-receipt-indian-rupee" variant="subtle" label="Billing" />
          <UButton to="/sales-return" icon="i-lucide-rotate-ccw" variant="subtle" label="Sales Return" />
          <UButton to="/vouchers" icon="i-lucide-banknote" variant="subtle" label="Vouchers" />
          <UButton to="/cash-vouchers" icon="i-lucide-wallet-cards" variant="subtle" label="Cash Vouchers" />
          <UButton to="/petty-cash" icon="i-lucide-circle-dollar-sign" variant="subtle" label="Petty Cash" />
          <UButton to="/purchase" icon="i-lucide-package-plus" variant="subtle" label="Purchase" />
          <UButton to="/purchase-return" icon="i-lucide-undo-2" variant="subtle" label="Purchase Return" />
          <UButton to="/non-gst-goods" icon="i-lucide-file-warning" variant="subtle" label="Non-GST Goods" />
          <UButton to="/payroll" icon="i-lucide-badge-indian-rupee" variant="subtle" label="Payroll" />
          <UButton to="/tailoring" icon="i-lucide-scissors" variant="subtle" label="Tailoring" />
          <UButton to="/gst-final-acceptance" icon="i-lucide-badge-check" variant="subtle" label="GST Acceptance" />
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.print-acceptance-page { display: grid; gap: 1rem; }
.metric-grid, .document-grid, .scenario-grid, .form-grid {
  display: grid;
  gap: 1rem;
}
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.document-grid { grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); }
.scenario-grid { grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); }
.form-grid { grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); }
.document-card, .scenario-card, .evidence-row {
  border: 1px solid rgb(var(--color-gray-200));
  border-radius: 1rem;
  padding: 1rem;
  display: grid;
  gap: .85rem;
}
.document-top, .section-header, .footer-actions, .document-actions, .evidence-row, .evidence-badges {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
  flex-wrap: wrap;
}
.document-actions, .evidence-badges { justify-content: flex-start; align-items: center; }
.document-card h3, .scenario-card h3 { margin: .45rem 0 .25rem; }
.document-card p, .scenario-card p, .evidence-row p, .empty-state { color: rgb(var(--color-gray-500)); margin: 0; }
.document-card dl {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: .75rem;
}
.document-card dt { font-size: .72rem; text-transform: uppercase; color: rgb(var(--color-gray-500)); }
.document-card dd { margin: .2rem 0 0; font-weight: 700; overflow-wrap: anywhere; }
.evidence-list { display: grid; gap: .75rem; }
.closure-grid, .evidence-columns {
  display: grid;
  gap: 1rem;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}
.closure-box, .next-module-box {
  border: 1px solid rgb(var(--color-gray-200));
  border-radius: .9rem;
  padding: .85rem;
}
.closure-box span { display: block; color: rgb(var(--color-gray-500)); font-size: .8rem; }
.closure-box strong { display: block; margin-top: .25rem; font-size: 1.15rem; }
.evidence-columns h3 { margin: 0 0 .5rem; }
.evidence-columns ul { margin: 0; padding-left: 1.15rem; color: rgb(var(--color-gray-600)); }
.next-module-box { display: flex; gap: .5rem; flex-wrap: wrap; color: rgb(var(--color-gray-600)); }
.mt-4 { margin-top: 1rem; }
.dark .document-card, .dark .scenario-card, .dark .evidence-row, .dark .closure-box, .dark .next-module-box { border-color: rgb(var(--color-gray-800)); }
.dark .evidence-columns ul, .dark .next-module-box { color: rgb(var(--color-gray-400)); }
</style>

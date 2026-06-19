<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated

const loading = ref(false)
const loadError = ref('')
const status = ref<any | null>(null)
const PRINT_ACCEPTANCE_KEY = 'garmetix:print-final-acceptance:v1'
const acceptanceState = reactive<Record<string, boolean>>({})
const acceptanceNote = ref('')

const documents = computed(() => status.value?.documents || [])
const readyCount = computed(() => status.value?.readyCount || 0)
const totalCount = computed(() => status.value?.totalCount || documents.value.length)
const acceptedCount = computed(() => documents.value.filter((doc: any) => acceptanceState[doc.key]).length)
const allAccepted = computed(() => totalCount.value > 0 && acceptedCount.value === totalCount.value && readyCount.value === totalCount.value)

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function statusColor(value: string) {
  return value === 'Ready' ? 'success' : 'warning'
}

function loadAcceptance() {
  if (typeof window === 'undefined') return
  try {
    const saved = JSON.parse(window.localStorage.getItem(PRINT_ACCEPTANCE_KEY) || '{}')
    if (saved.state) Object.assign(acceptanceState, saved.state)
    acceptanceNote.value = saved.note || ''
  } catch {
    // Ignore local checklist cache.
  }
}

function saveAcceptance() {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PRINT_ACCEPTANCE_KEY, JSON.stringify({
    state: acceptanceState,
    note: acceptanceNote.value,
    savedAt: new Date().toISOString()
  }))
}

watch(acceptanceState, saveAcceptance, { deep: true })
watch(acceptanceNote, saveAcceptance)

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    status.value = await api.get<any>('print-acceptance/status')
    feedback.notify('Print acceptance refreshed', 'Sample document availability was checked.', 'success')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check admin permission and API service.', 'Print acceptance failed')
    feedback.failed('Print acceptance failed', error)
  } finally {
    loading.value = false
  }
}

async function openEndpoint(doc: any) {
  if (!doc?.endpoint) {
    feedback.notify('No sample found', 'Create a sample record first, then refresh print acceptance.', 'warning')
    return
  }

  const url = `${config.public.apiBase}${doc.endpoint.replace(/^\/api/, '')}`
  window.open(url, '_blank', 'noopener,noreferrer')
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
        description="Final print/PDF acceptance for vouchers, cash vouchers, petty cash, purchase inward, tailoring and GST exports before handover."
        icon="i-lucide-printer-check"
        primary-label="Run Checks"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="allAccepted ? 'success' : 'warning'" :label="allAccepted ? 'Prints accepted' : `${acceptedCount}/${totalCount} accepted`" variant="subtle" />
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
          <div class="planner-metric-body"><UAvatar icon="i-lucide-check-check" :color="allAccepted ? 'success' : 'warning'" variant="subtle" /><div><p>Accepted</p><strong>{{ acceptedCount }}</strong><span>Operator verified prints</span></div></div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header">
            <div>
              <h2>Print Documents</h2>
              <p>Open each sample, verify layout, then tick accepted.</p>
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
              <UCheckbox v-model="acceptanceState[doc.key]" label="Print checked and accepted" />
            </div>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Final print checklist</h2></template>
        <div class="check-grid">
          <UAlert color="primary" icon="i-lucide-image" title="Branding" description="Logo, company name, GSTIN, address and contact details are correct." />
          <UAlert color="primary" icon="i-lucide-calendar-check" title="Dates" description="Document date is not one day back and print date is correct." />
          <UAlert color="primary" icon="i-lucide-indian-rupee" title="Amounts" description="Taxable amount, GST/tax, discount, round-off and total amount match screen values." />
          <UAlert color="primary" icon="i-lucide-pen-line" title="Signatures" description="Customer/receiver/prepared-by/signature blocks are visible where required." />
        </div>
        <UFormField class="mt-4" label="Operator print acceptance note">
          <UTextarea v-model="acceptanceNote" :rows="3" placeholder="Example: Verified voucher A5, petty cash print, purchase inward, GST export, operator name/date..." />
        </UFormField>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Quick navigation</h2></template>
        <div class="footer-actions">
          <UButton to="/vouchers" icon="i-lucide-banknote" variant="subtle" label="Vouchers" />
          <UButton to="/cash-vouchers" icon="i-lucide-wallet-cards" variant="subtle" label="Cash Vouchers" />
          <UButton to="/petty-cash" icon="i-lucide-circle-dollar-sign" variant="subtle" label="Petty Cash" />
          <UButton to="/purchase" icon="i-lucide-package-plus" variant="subtle" label="Purchase" />
          <UButton to="/tailoring" icon="i-lucide-scissors" variant="subtle" label="Tailoring" />
          <UButton to="/gst-final-acceptance" icon="i-lucide-badge-check" variant="subtle" label="GST Acceptance" />
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.print-acceptance-page { display: grid; gap: 1rem; }
.metric-grid, .document-grid, .check-grid {
  display: grid;
  gap: 1rem;
}
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.document-grid { grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); }
.check-grid { grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); }
.document-card {
  border: 1px solid rgb(var(--color-gray-200));
  border-radius: 1rem;
  padding: 1rem;
  display: grid;
  gap: .85rem;
}
.document-top, .section-header, .footer-actions, .document-actions {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  align-items: flex-start;
  flex-wrap: wrap;
}
.document-card h3 { margin: .45rem 0 .25rem; }
.document-card p { color: rgb(var(--color-gray-500)); margin: 0; }
.document-card dl {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: .75rem;
}
.document-card dt { font-size: .72rem; text-transform: uppercase; color: rgb(var(--color-gray-500)); }
.document-card dd { margin: .2rem 0 0; font-weight: 700; overflow-wrap: anywhere; }
.dark .document-card { border-color: rgb(var(--color-gray-800)); }
</style>

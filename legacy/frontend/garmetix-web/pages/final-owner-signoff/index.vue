<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const config = useRuntimeConfig()

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const report = ref<any | null>(null)
const loading = ref(false)
const exporting = ref(false)
const loadError = ref('')
const today = new Date()
const filters = reactive({ from: inputDate(new Date(today.getFullYear(), today.getMonth(), 1)), to: inputDate(today), ownerName: '', ownerMobile: '' })

useHead({ title: 'Final Owner Sign-off | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const rows = computed(() => report.value?.signoffRows || [])
const issues = computed(() => report.value?.openIssues || [])
const declarations = computed(() => report.value?.ownerDeclarations || [])
const signatureFields = computed(() => report.value?.signatureFields || [])
const evidenceLinks = computed(() => report.value?.evidenceLinks || [])

function inputDate(date: Date) { return date.toISOString().slice(0, 10) }
function query() {
  const params = new URLSearchParams()
  if (selectedCompanyId.value) params.set('companyId', selectedCompanyId.value)
  if (selectedStoreGroupId.value) params.set('storeGroupId', selectedStoreGroupId.value)
  if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  if (filters.ownerName) params.set('ownerName', filters.ownerName)
  if (filters.ownerMobile) params.set('ownerMobile', filters.ownerMobile)
  return params.toString()
}
function numberValue(value: number | string | null | undefined) { return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0)) }
function formatDate(value: string | null | undefined) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function statusColor(status: string | undefined) {
  if (status === 'Ready to Sign' || status === 'Accepted with evidence' || status === 'Prepared') return 'success'
  if (status === 'Manual sign-off required' || status === 'Warning') return 'warning'
  if (status === 'Not Ready' || status === 'Blocked' || status === 'Critical') return 'error'
  return 'neutral'
}
function statusIcon(status: string | undefined) {
  if (status === 'Ready to Sign' || status === 'Accepted with evidence' || status === 'Prepared') return 'i-lucide-circle-check'
  if (status === 'Manual sign-off required' || status === 'Warning') return 'i-lucide-triangle-alert'
  if (status === 'Not Ready' || status === 'Blocked' || status === 'Critical') return 'i-lucide-circle-alert'
  return 'i-lucide-circle-help'
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  try {
    const [companyRows, storeRows] = await Promise.all([api.list<any>('companies'), api.list<any>('stores')])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error: any) {
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Final Owner Sign-off workspace load failed')
  }
}
async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`final-owner-signoff?${query()}`)
    feedback.notify('Final Owner Sign-off refreshed', `${report.value?.criticalIssues || 0} critical and ${report.value?.warningIssues || 0} warning issue(s).`, report.value?.readyToSign ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Final Owner Sign-off could not be loaded. Check API logs and permissions.', 'Final Owner Sign-off failed')
  } finally { loading.value = false }
}
async function refreshAll() { await refreshShell(); await loadReport() }
async function exportCsv() {
  exporting.value = true
  try {
    const response = await fetch(`${config.public.apiBase}/final-owner-signoff/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `final-owner-signoff-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) { feedback.fromError(error, 'Final Owner Sign-off CSV export failed') }
  finally { exporting.value = false }
}
async function openPrint() {
  try {
    const response = await fetch(`${config.public.apiBase}/final-owner-signoff/print?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const html = await response.text()
    const blob = new Blob([html], { type: 'text/html' })
    const url = URL.createObjectURL(blob)
    window.open(url, '_blank', 'noopener,noreferrer')
    window.setTimeout(() => URL.revokeObjectURL(url), 60000)
  } catch (error: any) { feedback.fromError(error, 'Final Owner Sign-off print view failed') }
}
onMounted(async () => { auth.restore(); if (auth.isAuthenticated.value) await refreshAll() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" @authenticated="refreshAll" />
  <AppShell v-else title="Owner Sign-off" :companies="companies" :stores="stores" @refresh="refreshAll" @workspace-change="loadReport">
    <section class="closure-page">
      <UiModulePageHeader title="Final Owner Sign-off" description="Printable owner declaration and evidence pack after Production Go-Live Master Acceptance is ready." icon="i-lucide-pen-line">
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton icon="i-lucide-printer" label="Print / PDF" variant="subtle" @click="openPrint" />
            <UButton to="/production-go-live-master-acceptance" icon="i-lucide-rocket" label="Go-Live Gate" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Sign-off unavailable" :description="loadError" :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refreshAll }]" />

      <UCard>
        <div class="validation-filters">
          <UFormField label="From date"><UInput v-model="filters.from" type="date" /></UFormField>
          <UFormField label="To date"><UInput v-model="filters.to" type="date" /></UFormField>
          <UFormField label="Owner name"><UInput v-model="filters.ownerName" placeholder="Owner name for printed sign-off" /></UFormField>
          <UFormField label="Owner mobile"><UInput v-model="filters.ownerMobile" placeholder="Optional" /></UFormField>
          <div class="filter-actions"><UButton icon="i-lucide-play" label="Prepare sign-off" :loading="loading" @click="loadReport" /></div>
        </div>
      </UCard>

      <div class="status-grid">
        <UCard class="status-card hero-status"><p class="eyebrow">Sign-off status</p><div class="status-line"><UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" /></div><p class="muted">{{ formatDate(report?.from) }} to {{ formatDate(report?.to) }}</p><p class="muted">{{ report?.version }} / {{ report?.buildCode }}</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Critical</p><strong>{{ numberValue(report?.criticalIssues) }}</strong><p class="muted">Must be zero.</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Warnings</p><strong>{{ numberValue(report?.warningIssues) }}</strong><p class="muted">Owner review.</p></UCard>
      </div>

      <UCard class="section-card"><template #header><h3>Sign-off evidence rows</h3></template><div class="table-wrap"><table><thead><tr><th>Area</th><th>Status</th><th>Evidence</th><th>Owner instruction</th></tr></thead><tbody><tr v-for="row in rows" :key="row.area"><td>{{ row.area }}</td><td><UBadge :label="row.status" :color="statusColor(row.status)" variant="subtle" /></td><td>{{ row.evidence }}</td><td>{{ row.ownerInstruction }}</td></tr></tbody></table></div></UCard>

      <UCard v-if="issues.length" class="section-card"><template #header><h3>Open issue snapshot</h3></template><div class="table-wrap"><table><thead><tr><th>Severity</th><th>Title</th><th>Message</th><th>Open</th></tr></thead><tbody><tr v-for="issue in issues" :key="`${issue.title}-${issue.message}`"><td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td><td>{{ issue.title }}</td><td>{{ issue.message }}</td><td><UButton v-if="issue.actionPath" :to="issue.actionPath" size="xs" variant="ghost" icon="i-lucide-external-link" label="Open" /></td></tr></tbody></table></div></UCard>

      <div class="info-grid">
        <UCard><template #header><h3>Owner declarations</h3></template><ul><li v-for="item in declarations" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Signature fields</h3></template><ul><li v-for="item in signatureFields" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Evidence links</h3></template><ul><li v-for="item in evidenceLinks" :key="item"><NuxtLink :to="item">{{ item }}</NuxtLink></li></ul></UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.closure-page { display: grid; gap: 1rem; }
.header-actions { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; }
.validation-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; align-items: end; }
.filter-actions { display: flex; align-items: center; }
.status-grid { display: grid; grid-template-columns: minmax(260px, 2fr) repeat(2, minmax(140px, 1fr)); gap: 1rem; }
.status-card strong { display: block; font-size: 1.8rem; margin-top: .25rem; }
.hero-status { min-height: 140px; }
.eyebrow { margin: 0; text-transform: uppercase; letter-spacing: .06em; font-size: .72rem; color: rgb(var(--color-gray-500)); }
.muted { color: rgb(var(--color-gray-500)); margin: .25rem 0 0; }
.section-card { overflow: hidden; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: .875rem; }
th, td { padding: .65rem; border-bottom: 1px solid rgb(var(--color-gray-200)); text-align: left; vertical-align: top; }
.info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1rem; }
ul { margin: 0; padding-left: 1.1rem; }
a { color: rgb(var(--color-primary-600)); }
@media (max-width: 780px) { .status-grid { grid-template-columns: 1fr; } }
</style>

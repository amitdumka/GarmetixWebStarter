<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const report = ref<any | null>(null)
const loading = ref(false)
const exporting = ref(false)
const loadError = ref('')

useHead({ title: 'Production Host Build QA | Garmetix' })

const checks = computed(() => report.value?.checks || [])
const closeoutChecklist = computed(() => report.value?.closeoutChecklist || [])
const operatorRules = computed(() => report.value?.operatorRules || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const nextModules = computed(() => report.value?.nextModuleCandidates || [])

function statusColor(status: string | undefined) {
  if (status === 'Ready' || status === 'Pass') return 'success'
  if (status === 'Needs Manual QA' || status === 'Warning') return 'warning'
  if (status === 'Blocked' || status === 'Critical') return 'error'
  return 'neutral'
}

function statusIcon(status: string | undefined) {
  if (status === 'Ready' || status === 'Pass') return 'i-lucide-circle-check'
  if (status === 'Needs Manual QA' || status === 'Warning') return 'i-lucide-triangle-alert'
  if (status === 'Blocked' || status === 'Critical') return 'i-lucide-circle-alert'
  return 'i-lucide-circle-help'
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  return new Date(value).toLocaleString('en-IN')
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error: any) {
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Production host build QA workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>('production-host-build-qa')
    feedback.notify('Production host build QA refreshed', `${report.value?.critical || 0} critical and ${report.value?.warnings || 0} warning check(s).`, report.value?.status === 'Ready' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Production host build QA could not be loaded. Check API logs and permissions.', 'Production host build QA failed')
  } finally {
    loading.value = false
  }
}

async function refreshAll() {
  await refreshShell()
  await loadReport()
}

async function exportCsv() {
  exporting.value = true
  try {
    const response = await fetch(`${config.public.apiBase}/production-host-build-qa/evidence.csv`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = 'production-host-build-qa.csv'
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Production host build QA CSV export failed')
  } finally {
    exporting.value = false
  }
}

onMounted(async () => {
  auth.restore()
  if (auth.isAuthenticated.value) await refreshAll()
})
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" @authenticated="refreshAll" />

  <AppShell
    v-else
    title="Production Host QA"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="production-host-qa-page">
      <UiModulePageHeader
        title="Production host build QA + runtime error fix pack"
        description="Run this on the real Docker host after docker compose build to catch database/schema/runtime blockers and record manual smoke-test evidence before live billing."
        icon="i-lucide-server-cog"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run QA" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/runtime-diagnostics" icon="i-lucide-activity" label="Runtime Diagnostics" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Production host QA is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refreshAll }]"
      />

      <div class="status-grid">
        <UCard class="status-card">
          <p class="eyebrow">Final status</p>
          <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" />
          <p class="muted">Ready only when automatic probes pass and manual host smoke evidence is completed.</p>
        </UCard>
        <UCard class="status-card"><p class="eyebrow">Passed</p><strong>{{ report?.passed || 0 }}</strong><p class="muted">Automatic checks passed.</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Warnings</p><strong>{{ report?.warnings || 0 }}</strong><p class="muted">Mostly manual QA evidence.</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Critical</p><strong>{{ report?.critical || 0 }}</strong><p class="muted">Blocks live use.</p></UCard>
      </div>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Host/runtime checks</h2>
              <p>{{ report?.version }} · {{ report?.stage }} · {{ report?.buildCode }} · {{ report?.environment }} · {{ formatDateTime(report?.generatedAtUtc) }}</p>
            </div>
            <UBadge :label="`${checks.length} check(s)`" color="neutral" variant="subtle" />
          </div>
        </template>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Status</th><th>Code</th><th>Area</th><th>Check</th><th>Detail</th><th>Action</th></tr>
            </thead>
            <tbody>
              <tr v-for="check in checks" :key="check.code">
                <td><UBadge :label="check.status" :color="statusColor(check.status)" variant="subtle" /></td>
                <td>{{ check.code }}</td>
                <td>{{ check.area }}</td>
                <td>{{ check.label }}</td>
                <td>{{ check.detail }}</td>
                <td>{{ check.action }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <div class="info-grid">
        <UCard><template #header><h2>Closeout checklist</h2></template><ul class="check-list"><li v-for="item in closeoutChecklist" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h2>Operator rules</h2></template><ul class="check-list"><li v-for="item in operatorRules" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h2>Known limitations</h2></template><ul class="check-list"><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h2>Next module candidates</h2></template><ul class="check-list"><li v-for="item in nextModules" :key="item">{{ item }}</li></ul></UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.production-host-qa-page { display: flex; flex-direction: column; gap: 1rem; }
.header-actions { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; justify-content: flex-end; }
.status-grid, .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 1rem; }
.status-card strong { display: block; font-size: 1.8rem; line-height: 1; margin: .35rem 0; }
.eyebrow { color: rgb(var(--color-gray-500)); font-size: .78rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.muted { color: rgb(var(--color-gray-500)); font-size: .9rem; margin-top: .5rem; }
.card-header-line { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
.card-header-line h2 { font-size: 1.05rem; font-weight: 700; }
.card-header-line p { color: rgb(var(--color-gray-500)); font-size: .9rem; margin-top: .15rem; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: .86rem; }
th, td { padding: .65rem .75rem; border-bottom: 1px solid rgb(var(--color-gray-200)); vertical-align: top; white-space: nowrap; }
th { text-align: left; color: rgb(var(--color-gray-500)); font-size: .72rem; letter-spacing: .08em; text-transform: uppercase; }
.check-list { display: grid; gap: .55rem; padding-left: 1.1rem; color: rgb(var(--color-gray-600)); font-size: .92rem; }
</style>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-search-check" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">GST Audit</h2>
          <p class="garmetix-dashboard-subtitle">
            Runs the configurable rule catalog against Product/Vendor/Customer masters and Sale/Purchase invoices.
            Findings are informational by default - nothing here blocks billing unless a rule's strict mode is on.
          </p>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Run Audit</h3>
      <div class="grid gap-2 sm:grid-cols-4">
        <USelect v-model="runForm.moduleArea" :items="moduleAreaItems" />
        <UInput v-model="runForm.fromDate" type="date" />
        <UInput v-model="runForm.toDate" type="date" />
        <UButton color="primary" icon="i-lucide-play" :loading="running" @click="runAudit">Run Audit</UButton>
      </div>
      <UAlert
        v-if="lastRunResult"
        class="mt-3"
        color="neutral"
        variant="subtle"
        icon="i-lucide-info"
        :description="`Scanned ${lastRunResult.entitiesScanned}, created ${lastRunResult.findingsCreated} new finding(s).${lastRunResult.notes.length ? ' ' + lastRunResult.notes.join(' ') : ''}`"
      />
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <h3 class="garmetix-panel-title">Findings</h3>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="findingFilters.status" :items="statusFilterItems" @update:model-value="loadFindings" />
          <USelect v-model="findingFilters.severity" :items="severityFilterItems" @update:model-value="loadFindings" />
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loadingFindings" @click="loadFindings">Refresh</UButton>
        </div>
      </div>

      <div class="space-y-2">
        <div v-if="findings.length === 0" class="py-8 text-center text-sm text-muted">No findings match these filters.</div>
        <div v-for="finding in findings" :key="finding.id" class="garmetix-row-card">
          <div class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
            <div class="flex-1">
              <div class="flex flex-wrap items-center gap-2">
                <UBadge :color="severityColor(finding.severity)" variant="subtle">{{ finding.severity }}</UBadge>
                <UBadge color="neutral" variant="subtle">{{ finding.moduleArea }}</UBadge>
                <span class="text-xs text-muted">{{ finding.ruleCode }}</span>
                <UBadge :color="statusColor(finding.status)" variant="subtle">{{ finding.status }}</UBadge>
              </div>
              <p class="mt-1 text-sm">{{ finding.message }}</p>
              <p v-if="finding.expectedValue || finding.actualValue" class="text-xs text-muted">
                Expected: {{ finding.expectedValue ?? '-' }} | Actual: {{ finding.actualValue ?? '-' }}
              </p>
              <p class="text-xs text-muted">{{ formatDate(finding.createdAt) }}</p>
            </div>
            <div v-if="finding.status === 'Open'" class="flex flex-wrap gap-1">
              <UButton size="xs" color="success" variant="soft" icon="i-lucide-check" @click="setStatus(finding, 'mark-fixed')">Mark Fixed</UButton>
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-thumbs-up" @click="setStatus(finding, 'accept')">Accept</UButton>
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye-off" @click="setStatus(finding, 'ignore')">Ignore</UButton>
            </div>
          </div>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Audit Rules</h3>
      <div class="overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[720px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Rule</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Module</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Severity</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Enabled</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Strict Mode</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="rule in rules" :key="rule.id">
              <td class="max-w-72 px-3 py-2">
                <p class="font-medium">{{ rule.ruleName }}</p>
                <p class="text-xs text-muted">{{ rule.ruleCode }}</p>
              </td>
              <td class="whitespace-nowrap px-3 py-2">{{ rule.moduleArea }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <UBadge :color="severityColor(rule.severity)" variant="subtle">{{ rule.severity }}</UBadge>
              </td>
              <td class="whitespace-nowrap px-3 py-2">
                <USwitch :model-value="rule.isEnabled" @update:model-value="value => updateRule(rule, { isEnabled: value, strictMode: rule.strictMode })" />
              </td>
              <td class="whitespace-nowrap px-3 py-2">
                <USwitch :model-value="rule.strictMode" @update:model-value="value => updateRule(rule, { isEnabled: rule.isEnabled, strictMode: value })" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <p class="mt-2 text-xs text-muted">Strict mode is a per-rule flag for future billing-time enforcement - findings themselves never block billing on their own.</p>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDate, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'GST Audit - Garmetix Books' })

interface AuditRule {
  id: string
  ruleCode: string
  ruleName: string
  moduleArea: string
  severity: string
  isEnabled: boolean
  strictMode: boolean
}

interface AuditFinding {
  id: string
  ruleCode: string
  severity: string
  moduleArea: string
  message: string
  expectedValue: string | null
  actualValue: string | null
  status: string
  createdAt: string
}

interface RunResult {
  findingsCreated: number
  entitiesScanned: number
  notes: string[]
}

const { get, post, put } = useBooksApiClient()

const error = ref('')
const message = ref('')
const running = ref(false)
const loadingFindings = ref(false)
const rules = ref<AuditRule[]>([])
const findings = ref<AuditFinding[]>([])
const lastRunResult = ref<RunResult | null>(null)

const moduleAreaItems = [
  { label: 'All Modules', value: '' },
  { label: 'Product', value: 'Product' },
  { label: 'Customer', value: 'Customer' },
  { label: 'Vendor', value: 'Vendor' },
  { label: 'Sale', value: 'Sale' },
  { label: 'Purchase', value: 'Purchase' }
]
const statusFilterItems = [
  { label: 'Open', value: 'Open' },
  { label: 'Fixed', value: 'Fixed' },
  { label: 'Accepted', value: 'Accepted' },
  { label: 'Ignored', value: 'Ignored' },
  { label: 'All Status', value: '' }
]
const severityFilterItems = [
  { label: 'All Severity', value: '' },
  { label: 'Error', value: 'Error' },
  { label: 'Warning', value: 'Warning' },
  { label: 'Info', value: 'Info' }
]

const runForm = reactive({
  moduleArea: '',
  fromDate: new Date(Date.now() - 30 * 24 * 3600 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10)
})
const findingFilters = reactive({ status: 'Open', severity: '' })

function severityColor(severity: string) {
  if (severity === 'Error') return 'error'
  if (severity === 'Warning') return 'warning'
  return 'neutral'
}

function statusColor(status: string) {
  if (status === 'Open') return 'warning'
  if (status === 'Fixed') return 'success'
  return 'neutral'
}

async function loadRules() {
  try {
    rules.value = await get<AuditRule[]>('/gst/audit/rules')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load audit rules.'
  }
}

async function updateRule(rule: AuditRule, patch: { isEnabled: boolean, strictMode: boolean }) {
  try {
    await put(`/gst/audit/rules/${rule.id}`, { isEnabled: patch.isEnabled, strictMode: patch.strictMode, messageTemplate: null })
    rule.isEnabled = patch.isEnabled
    rule.strictMode = patch.strictMode
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the rule.'
  }
}

async function loadFindings() {
  loadingFindings.value = true
  error.value = ''
  try {
    const query: Record<string, string> = {}
    if (findingFilters.status) query.status = findingFilters.status
    if (findingFilters.severity) query.severity = findingFilters.severity
    findings.value = await get<AuditFinding[]>('/gst/audit/findings', query)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load findings.'
  } finally {
    loadingFindings.value = false
  }
}

async function runAudit() {
  running.value = true
  error.value = ''
  message.value = ''
  try {
    lastRunResult.value = await post<RunResult>('/gst/audit/run', {
      moduleArea: runForm.moduleArea || null,
      fromDate: runForm.fromDate,
      toDate: runForm.toDate
    })
    message.value = 'Audit run complete.'
    await loadFindings()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Audit run failed.'
  } finally {
    running.value = false
  }
}

async function setStatus(finding: AuditFinding, action: 'ignore' | 'mark-fixed' | 'accept') {
  try {
    await post(`/gst/audit/findings/${finding.id}/${action}`)
    await loadFindings()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the finding.'
  }
}

onMounted(async () => {
  await loadRules()
  await loadFindings()
})
</script>

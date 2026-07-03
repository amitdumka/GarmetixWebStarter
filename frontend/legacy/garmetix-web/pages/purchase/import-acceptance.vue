<script setup lang="ts">
const api = useGarmetixApi()
const config = useRuntimeConfig()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const loading = ref(false)
const summary = ref<any | null>(null)
const selectedBatchId = ref('')
const report = ref<any | null>(null)
const backupChecklist = ref<any | null>(null)
const parserQa = ref<any | null>(null)
const finalClosure = ref<any | null>(null)
const correctionPlan = ref<any | null>(null)
const loadingReport = ref(false)

function money(value: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function fileSize(bytes: number) {
  if (!bytes) return '0 B'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

function formatDate(value: any) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString('en-IN')
}

function badgeColor(status: string) {
  const value = `${status || ''}`.toLowerCase()
  if (value === 'pass' || value === 'posted' || value === 'readytopost') return 'success'
  if (value === 'warn' || value === 'needsreview' || value === 'info' || value === 'needsretest' || value === 'draftneedscorrection') return 'warning'
  if (value === 'block' || value === 'failed' || value === 'rejected' || value === 'fail' || value === 'correctionrequired') return 'error'
  return 'neutral'
}


async function downloadImportFile(resource: string, fallbackName: string) {
  try {
    const response = await fetch(`${config.public.apiBase}/${resource}`, { headers: api.authHeaders() })
    if (!response.ok) {
      throw new Error(`Download failed (${response.status})`)
    }
    const blob = await response.blob()
    const disposition = response.headers.get('content-disposition') || ''
    const match = disposition.match(/filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i)
    const fileName = decodeURIComponent(match?.[1] || match?.[2] || fallbackName)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Purchase import export downloaded')
  } catch (error) {
    feedback.failed('Could not download purchase import export', error)
  }
}

function downloadBatchAudit(batchId: string) {
  if (!batchId) return
  return downloadImportFile(`purchase-import/batches/${batchId}/audit-json`, `purchase-import-audit-${batchId}.json`)
}

function downloadBatchLines(batchId: string) {
  if (!batchId) return
  return downloadImportFile(`purchase-import/batches/${batchId}/lines-csv`, `purchase-import-lines-${batchId}.csv`)
}

function downloadLearningExport() {
  return downloadImportFile('purchase-import/vendor-profiles/export', 'purchase-import-vendor-learning.json')
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [acceptance, backup, parser, closure] = await Promise.all([
      api.get<any>('purchase-import/acceptance-summary'),
      api.get<any>('purchase-import/backup-restore-checklist'),
      api.get<any>('purchase-import/parser-qa-summary'),
      api.get<any>('purchase-import/final-closure-status')
    ])
    summary.value = acceptance
    backupChecklist.value = backup
    parserQa.value = parser
    finalClosure.value = closure
  } catch (error) {
    feedback.failed('Could not load purchase import acceptance summary', error)
  } finally {
    loading.value = false
  }
}

async function openReport(batchId: string) {
  if (!batchId) return
  selectedBatchId.value = batchId
  loadingReport.value = true
  try {
    report.value = await api.get<any>(`purchase-import/batches/${batchId}/posting-report`)
  } catch (error) {
    feedback.failed('Could not load import posting report', error)
  } finally {
    loadingReport.value = false
  }
}

async function openCorrectionPlan(batchId: string) {
  if (!batchId) return
  try {
    correctionPlan.value = await api.get<any>(`purchase-import/batches/${batchId}/correction-plan`)
  } catch (error) {
    feedback.failed('Could not load correction plan', error)
  }
}

async function markAcceptance(batchId: string, status: 'Pass' | 'Fail' | 'NeedsRetest') {
  if (!batchId) return
  const notes = window.prompt(status === 'Pass' ? 'Acceptance notes (optional)' : 'Reason / correction note')
  if (notes === null && status !== 'Pass') return
  try {
    await $fetch(`${config.public.apiBase}/purchase-import/batches/${batchId}/acceptance`, { method: 'POST', headers: api.authHeaders(), body: { status, notes: notes || null } })
    feedback.notify(`Import marked ${status}`)
    await refresh()
  } catch (error) {
    feedback.failed('Could not update import acceptance', error)
  }
}

async function requestCorrection(batchId: string) {
  if (!batchId) return
  const reason = window.prompt('Why does this posted/draft import need correction?')
  if (!reason) return
  try {
    const safety = await $fetch<any>(`${config.public.apiBase}/purchase-import/batches/${batchId}/request-correction`, { method: 'POST', headers: api.authHeaders(), body: { reason, requestedAction: 'Review purchase inward before stock/accounting correction' } })
    feedback.notify(safety?.isPosted ? 'Posted import flagged for controlled correction' : 'Draft flagged for correction')
    await refresh()
  } catch (error) {
    feedback.failed('Could not request import correction', error)
  }
}

onMounted(refresh)
</script>

<template>
  <AuthLoginPrompt v-if="!isAuthenticated" />
  <AppShell v-else title="Purchase Import Acceptance" @refresh="refresh" @workspace-change="refresh">
    <section class="page-shell space-y-6">
      <UiPageHero
        icon="i-lucide-clipboard-check"
        title="Purchase Import Acceptance"
        subtitle="Final QA dashboard for supplier invoice OCR imports, proof storage, posting readiness, vendor learning and repeat import control."
      >
        <template #actions>
          <UButton color="neutral" variant="subtle" icon="i-lucide-file-scan" label="Import Invoice" to="/purchase/import" />
          <UButton color="neutral" variant="subtle" icon="i-lucide-brain-circuit" label="Learning" to="/purchase/import-profiles" />
          <UButton color="neutral" variant="subtle" icon="i-lucide-download" label="Export learning" @click="downloadLearningExport" />
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UiPageHero>

      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
        <UCard>
          <p class="text-sm text-gray-500">Total imports</p>
          <p class="text-2xl font-semibold">{{ summary?.totalBatches || 0 }}</p>
          <p class="text-xs text-gray-500">Posted {{ summary?.postedBatches || 0 }} · Ready {{ summary?.readyToPostBatches || 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-gray-500">Needs attention</p>
          <p class="text-2xl font-semibold">{{ (summary?.needsReviewBatches || 0) + (summary?.failedOrRejectedBatches || 0) }}</p>
          <p class="text-xs text-gray-500">Review {{ summary?.needsReviewBatches || 0 }} · Failed/rejected {{ summary?.failedOrRejectedBatches || 0 }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-gray-500">Posted this month</p>
          <p class="text-2xl font-semibold">{{ summary?.postedThisMonth || 0 }}</p>
          <p class="text-xs text-gray-500">{{ money(summary?.postedThisMonthBillAmount || 0) }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-gray-500">Proof storage</p>
          <p class="text-2xl font-semibold">{{ fileSize(summary?.totalStorageBytes || 0) }}</p>
          <p class="text-xs text-gray-500">Protected {{ fileSize(summary?.protectedPostedProofBytes || 0) }} · Unposted {{ fileSize(summary?.unpostedStorageBytes || 0) }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-gray-500">Real invoice accepted</p>
          <p class="text-2xl font-semibold">{{ summary?.acceptedPassBatches || 0 }}</p>
          <p class="text-xs text-gray-500">Failed {{ summary?.acceptedFailBatches || 0 }} · Correction {{ summary?.correctionRequiredBatches || 0 }}</p>
        </UCard>
      </div>

      <div class="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
        <UCard>
          <template #header>
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 class="font-semibold">Purchase Import final closure</h2>
                <p class="text-sm text-gray-500">Final Complete / Not Complete status before moving outside Purchase Import.</p>
              </div>
              <UBadge :color="finalClosure?.moduleStatus === 'Complete' ? 'success' : finalClosure?.moduleStatus === 'Not Complete' ? 'error' : 'warning'">{{ finalClosure?.moduleStatus || 'Loading' }}</UBadge>
            </div>
          </template>
          <div class="space-y-4">
            <div class="grid gap-3 md:grid-cols-5">
              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Mode</p><strong>{{ finalClosure?.completionMode || '-' }}</strong></div>
              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Accepted Pass</p><strong>{{ finalClosure?.acceptedPassBatches || 0 }}</strong></div>
              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Open drafts</p><strong>{{ finalClosure?.openDraftBatches || 0 }}</strong></div>
              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Untested posted</p><strong>{{ finalClosure?.untestedPostedBatches || 0 }}</strong></div>
              <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Corrections</p><strong>{{ finalClosure?.correctionRequiredBatches || 0 }}</strong></div>
            </div>
            <div class="grid gap-3 lg:grid-cols-2">
              <div v-for="item in finalClosure?.closeoutChecklist || []" :key="item.key" class="rounded-xl border border-gray-100 p-3 dark:border-gray-800">
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <p class="font-medium">{{ item.label }}</p>
                    <p class="text-sm text-gray-500">{{ item.detail }}</p>
                  </div>
                  <UBadge :color="badgeColor(item.status)">{{ item.status }}</UBadge>
                </div>
              </div>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div>
              <h2 class="font-semibold">Known limitations + next module</h2>
              <p class="text-sm text-gray-500">Keep these visible so Purchase Import can close without hiding remaining live QA.</p>
            </div>
          </template>
          <div class="space-y-4">
            <div>
              <h3 class="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Limitations</h3>
              <ul class="list-disc space-y-2 pl-5 text-sm text-gray-600 dark:text-gray-300">
                <li v-for="item in finalClosure?.knownLimitations || []" :key="item">{{ item }}</li>
              </ul>
            </div>
            <div>
              <h3 class="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Operator rules</h3>
              <ul class="list-disc space-y-2 pl-5 text-sm text-gray-600 dark:text-gray-300">
                <li v-for="item in finalClosure?.operatorRules || []" :key="item">{{ item }}</li>
              </ul>
            </div>
            <div class="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-100">
              <p class="font-medium">Recommended outside module</p>
              <p>{{ finalClosure?.recommendedNextModule || '-' }}</p>
            </div>
            <div>
              <h3 class="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-500">Next candidates</h3>
              <ul class="list-disc space-y-2 pl-5 text-sm text-gray-600 dark:text-gray-300">
                <li v-for="item in finalClosure?.nextModuleCandidates || []" :key="item">{{ item }}</li>
              </ul>
            </div>
          </div>
        </UCard>
      </div>

      <div class="grid gap-6 xl:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="font-semibold">Parser QA by template</h2>
                <p class="text-sm text-gray-500">Use this to confirm Tally/garment/generic parsers are tested on real supplier invoices.</p>
              </div>
              <UBadge :color="(parserQa?.correctionRequiredBatches || 0) > 0 ? 'warning' : 'info'">{{ parserQa?.totalBatches || 0 }} imports</UBadge>
            </div>
          </template>
          <div class="space-y-3">
            <div v-for="item in parserQa?.checklist || []" :key="item.key" class="rounded-xl border border-gray-100 p-3 dark:border-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div>
                  <p class="font-medium">{{ item.label }}</p>
                  <p class="text-sm text-gray-500">{{ item.detail }}</p>
                </div>
                <UBadge :color="badgeColor(item.status)">{{ item.status }}</UBadge>
              </div>
            </div>
            <div class="overflow-x-auto">
              <table class="min-w-full text-sm">
                <thead>
                  <tr class="border-b text-left text-xs uppercase tracking-wide text-gray-500 dark:border-gray-800">
                    <th class="py-2 pr-3">Template</th>
                    <th class="py-2 pr-3 text-right">Imports</th>
                    <th class="py-2 pr-3 text-right">Posted</th>
                    <th class="py-2 pr-3 text-right">Pass</th>
                    <th class="py-2 text-right">Correction</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="row in parserQa?.templates || []" :key="row.parserTemplate" class="border-b dark:border-gray-800">
                    <td class="py-2 pr-3"><UBadge color="info" variant="subtle">{{ row.parserTemplate }}</UBadge></td>
                    <td class="py-2 pr-3 text-right">{{ row.batchCount }}</td>
                    <td class="py-2 pr-3 text-right">{{ row.postedCount }}</td>
                    <td class="py-2 pr-3 text-right">{{ row.acceptedPassCount }}</td>
                    <td class="py-2 text-right">{{ row.correctionRequiredCount }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="font-semibold">Backup / restore proof checklist</h2>
                <p class="text-sm text-gray-500">Supplier PDF/image proofs live outside PostgreSQL and must be restored with the database.</p>
              </div>
              <UBadge :color="backupChecklist?.hasPostedProofs ? 'success' : 'warning'">{{ fileSize(backupChecklist?.totalStorageBytes || 0) }}</UBadge>
            </div>
          </template>
          <div class="space-y-3">
            <div class="rounded-lg bg-gray-50 p-3 text-sm dark:bg-gray-900">
              <p><strong>Storage root:</strong> {{ backupChecklist?.storageRoot || '-' }}</p>
              <p><strong>Docker volume:</strong> {{ backupChecklist?.dockerVolume || '-' }}</p>
              <p><strong>Protected proofs:</strong> {{ fileSize(backupChecklist?.protectedPostedProofBytes || 0) }}</p>
            </div>
            <div v-for="item in backupChecklist?.checklist || []" :key="item.key" class="rounded-xl border border-gray-100 p-3 dark:border-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div>
                  <p class="font-medium">{{ item.label }}</p>
                  <p class="text-sm text-gray-500">{{ item.detail }}</p>
                </div>
                <UBadge :color="badgeColor(item.status)">{{ item.status }}</UBadge>
              </div>
            </div>
            <div class="rounded-lg bg-gray-50 p-3 text-xs dark:bg-gray-900">
              <p class="font-medium">Backup commands</p>
              <p v-for="cmd in backupChecklist?.backupCommands || []" :key="cmd" class="font-mono">{{ cmd }}</p>
            </div>
          </div>
        </UCard>
      </div>

      <div class="grid gap-6 xl:grid-cols-[1fr_1.2fr]">
        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="font-semibold">Acceptance checklist</h2>
                <p class="text-sm text-gray-500">Use this before moving away from purchase import work.</p>
              </div>
              <UBadge :color="summary?.failedOrRejectedBatches || summary?.needsReviewBatches ? 'warning' : 'success'">
                {{ summary?.failedOrRejectedBatches || summary?.needsReviewBatches ? 'Attention' : 'Good' }}
              </UBadge>
            </div>
          </template>
          <div class="space-y-3">
            <div v-for="item in summary?.checklist || []" :key="item.key" class="rounded-xl border border-gray-100 p-3 dark:border-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div>
                  <p class="font-medium">{{ item.label }}</p>
                  <p class="text-sm text-gray-500">{{ item.detail }}</p>
                </div>
                <UBadge :color="badgeColor(item.status)">{{ item.status }}</UBadge>
              </div>
            </div>
            <p v-if="!summary && !loading" class="text-sm text-gray-500">Click Refresh to load acceptance status.</p>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div>
                <h2 class="font-semibold">Status summary</h2>
                <p class="text-sm text-gray-500">Status-wise counts, line counts and storage.</p>
              </div>
            </div>
          </template>
          <div class="overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="border-b text-left text-xs uppercase tracking-wide text-gray-500 dark:border-gray-800">
                  <th class="py-2 pr-3">Status</th>
                  <th class="py-2 pr-3 text-right">Batches</th>
                  <th class="py-2 pr-3 text-right">Lines</th>
                  <th class="py-2 pr-3 text-right">Bill</th>
                  <th class="py-2 text-right">Files</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in summary?.statuses || []" :key="row.status" class="border-b dark:border-gray-800">
                  <td class="py-2 pr-3"><UBadge :color="badgeColor(row.status)">{{ row.status }}</UBadge></td>
                  <td class="py-2 pr-3 text-right">{{ row.count }}</td>
                  <td class="py-2 pr-3 text-right">{{ row.lineCount }}</td>
                  <td class="py-2 pr-3 text-right">{{ money(row.billAmount) }}</td>
                  <td class="py-2 text-right">{{ fileSize(row.fileBytes || 0) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold">Recent supplier imports</h2>
              <p class="text-sm text-gray-500">Open a posting report before posting or when debugging a failed import.</p>
            </div>
            <UButton color="neutral" variant="subtle" icon="i-lucide-file-scan" label="New Import" to="/purchase/import" />
          </div>
        </template>
        <div class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b text-left text-xs uppercase tracking-wide text-gray-500 dark:border-gray-800">
                <th class="py-2 pr-3">Created</th>
                <th class="py-2 pr-3">Supplier</th>
                <th class="py-2 pr-3">Invoice</th>
                <th class="py-2 pr-3">Status</th>
                <th class="py-2 pr-3">Parser</th>
                <th class="py-2 pr-3">Accepted</th>
                <th class="py-2 pr-3">Correction</th>
                <th class="py-2 pr-3 text-right">Lines</th>
                <th class="py-2 pr-3 text-right">Bill</th>
                <th class="py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="batch in summary?.recentBatches || []" :key="batch.id" class="border-b dark:border-gray-800">
                <td class="py-2 pr-3">{{ formatDate(batch.createdAt) }}</td>
                <td class="py-2 pr-3">{{ batch.vendorName || '-' }}</td>
                <td class="py-2 pr-3">{{ batch.supplierInvoiceNumber || '-' }}</td>
                <td class="py-2 pr-3"><UBadge :color="badgeColor(batch.status)">{{ batch.status }}</UBadge></td>
                <td class="py-2 pr-3"><UBadge color="info" variant="subtle">{{ batch.parserTemplate || 'auto' }}</UBadge></td>
                <td class="py-2 pr-3"><UBadge :color="badgeColor(batch.acceptanceStatus || 'Untested')" variant="subtle">{{ batch.acceptanceStatus || 'Untested' }}</UBadge></td>
                <td class="py-2 pr-3"><UBadge :color="badgeColor(batch.correctionStatus || 'None')" variant="subtle">{{ batch.correctionStatus || 'None' }}</UBadge></td>
                <td class="py-2 pr-3 text-right">{{ batch.lineCount || 0 }}</td>
                <td class="py-2 pr-3 text-right">{{ money(batch.billAmount) }}</td>
                <td class="py-2 text-right">
                  <div class="flex justify-end gap-2">
                    <UButton size="xs" variant="subtle" color="neutral" icon="i-lucide-file-search" label="Report" :loading="loadingReport && selectedBatchId === batch.id" @click="openReport(batch.id)" />
                    <UButton size="xs" variant="ghost" color="warning" icon="i-lucide-route" label="Plan" @click="openCorrectionPlan(batch.id)" />
                    <UButton size="xs" variant="ghost" color="success" icon="i-lucide-check-circle" label="Pass" @click="markAcceptance(batch.id, 'Pass')" />
                    <UButton size="xs" variant="ghost" color="warning" icon="i-lucide-rotate-ccw" label="Retest" @click="markAcceptance(batch.id, 'NeedsRetest')" />
                    <UButton size="xs" variant="ghost" color="error" icon="i-lucide-triangle-alert" label="Correction" @click="requestCorrection(batch.id)" />
                    <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-table" label="CSV" @click="downloadBatchLines(batch.id)" />
                    <UButton size="xs" variant="ghost" color="neutral" icon="i-lucide-braces" label="Audit" @click="downloadBatchAudit(batch.id)" />
                    <UButton size="xs" variant="ghost" icon="i-lucide-pencil" label="Open" :to="`/purchase/import?batchId=${batch.id}`" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard v-if="correctionPlan">
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold">Correction / revision plan</h2>
              <p class="text-sm text-gray-500">Safe path for correcting imported drafts or posted purchase inwards.</p>
            </div>
            <UBadge :color="correctionPlan.isPosted ? 'warning' : 'info'">{{ correctionPlan.correctionStatus }}</UBadge>
          </div>
        </template>
        <div class="grid gap-4 lg:grid-cols-2">
          <div class="space-y-2">
            <p class="rounded-lg bg-gray-50 p-3 text-sm dark:bg-gray-900"><strong>Recommended:</strong> {{ correctionPlan.recommendedAction }}</p>
            <div v-for="item in correctionPlan.checklist || []" :key="item.key" class="rounded-lg border border-gray-100 p-3 dark:border-gray-800">
              <div class="flex items-start justify-between gap-3">
                <div><p class="font-medium">{{ item.label }}</p><p class="text-sm text-gray-500">{{ item.detail }}</p></div>
                <UBadge :color="badgeColor(item.status)">{{ item.status }}</UBadge>
              </div>
            </div>
          </div>
          <div class="space-y-2">
            <h3 class="font-medium">Safe steps</h3>
            <ol class="list-decimal space-y-2 pl-5 text-sm text-gray-600 dark:text-gray-300">
              <li v-for="step in correctionPlan.steps || []" :key="step">{{ step }}</li>
            </ol>
            <p v-for="warning in correctionPlan.warnings || []" :key="warning" class="rounded-lg border border-amber-200 bg-amber-50 p-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100">{{ warning }}</p>
          </div>
        </div>
      </UCard>

      <UCard v-if="report">
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold">Posting report</h2>
              <p class="text-sm text-gray-500">Batch {{ report.batchId }}</p>
            </div>
            <div class="flex items-center gap-2">
              <UButton size="xs" color="neutral" variant="subtle" icon="i-lucide-table" label="CSV" @click="downloadBatchLines(report.batchId)" />
              <UButton size="xs" color="neutral" variant="subtle" icon="i-lucide-braces" label="Audit JSON" @click="downloadBatchAudit(report.batchId)" />
              <UBadge :color="report.canPost ? 'success' : 'error'">{{ report.canPost ? 'Ready to post' : 'Blocked' }}</UBadge>
            </div>
          </div>
        </template>
        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Active/Ignored</p><strong>{{ report.activeLineCount }} / {{ report.ignoredLineCount }}</strong></div>
          <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">New/Matched</p><strong>{{ report.newProductCount }} / {{ report.matchedProductCount }}</strong></div>
          <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Missing barcode/GST</p><strong>{{ report.missingBarcodeCount }} / {{ report.missingTaxCount }}</strong></div>
          <div class="rounded-lg bg-gray-50 p-3 dark:bg-gray-900"><p class="text-xs text-gray-500">Grand difference</p><strong>{{ money(report.reconciliation?.billDifference || 0) }}</strong></div>
        </div>
        <div class="mt-4 grid gap-4 lg:grid-cols-2">
          <div class="space-y-2">
            <h3 class="font-medium">Checklist</h3>
            <div v-for="item in report.checklist || []" :key="item.key" class="flex items-start justify-between gap-3 rounded-lg border border-gray-100 p-3 dark:border-gray-800">
              <div><p class="font-medium">{{ item.label }}</p><p class="text-sm text-gray-500">{{ item.detail }}</p></div>
              <UBadge :color="badgeColor(item.status)">{{ item.status }}</UBadge>
            </div>
          </div>
          <div class="space-y-2">
            <h3 class="font-medium">Warnings</h3>
            <div v-if="report.warnings?.length" class="space-y-2">
              <p v-for="warning in report.warnings" :key="warning" class="rounded-lg border border-amber-200 bg-amber-50 p-2 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100">{{ warning }}</p>
            </div>
            <p v-else class="rounded-lg border border-green-200 bg-green-50 p-2 text-sm text-green-800 dark:border-green-900 dark:bg-green-950 dark:text-green-100">No posting warnings.</p>
          </div>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

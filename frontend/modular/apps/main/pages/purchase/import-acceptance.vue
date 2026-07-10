<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> Purchase Import</p>
          <h2 class="garmetix-dashboard-title">Import Acceptance</h2>
          <p class="garmetix-dashboard-subtitle">
            Final QA dashboard for supplier invoice OCR imports, proof storage, posting readiness and repeat-import control.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-file-scan" color="neutral" variant="soft" to="/purchase/import">Import Invoice</UButton>
          <UButton icon="i-lucide-brain-circuit" color="neutral" variant="soft" to="/purchase/import-profiles">Learning</UButton>
          <UButton icon="i-lucide-download" color="neutral" variant="soft" :loading="downloading === 'learning'" @click="downloadLearningExport">Export Learning</UButton>
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Total imports</p>
        <p class="garmetix-metric-value">{{ readNumber(summary, ['totalBatches']) }}</p>
        <p class="garmetix-metric-caption">Posted {{ readNumber(summary, ['postedBatches']) }} - Ready {{ readNumber(summary, ['readyToPostBatches']) }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Needs attention</p>
        <p class="garmetix-metric-value">{{ readNumber(summary, ['needsReviewBatches']) + readNumber(summary, ['failedOrRejectedBatches']) }}</p>
        <p class="garmetix-metric-caption">Review {{ readNumber(summary, ['needsReviewBatches']) }} - Failed/rejected {{ readNumber(summary, ['failedOrRejectedBatches']) }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Posted this month</p>
        <p class="garmetix-metric-value">{{ readNumber(summary, ['postedThisMonth']) }}</p>
        <p class="garmetix-metric-caption">{{ money(readNumber(summary, ['postedThisMonthBillAmount'])) }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Proof storage</p>
        <p class="garmetix-metric-value">{{ fileSize(readNumber(summary, ['totalStorageBytes'])) }}</p>
        <p class="garmetix-metric-caption">Protected {{ fileSize(readNumber(summary, ['protectedPostedProofBytes'])) }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Accepted pass</p>
        <p class="garmetix-metric-value">{{ readNumber(summary, ['acceptedPassBatches']) }}</p>
        <p class="garmetix-metric-caption">Failed {{ readNumber(summary, ['acceptedFailBatches']) }} - Correction {{ readNumber(summary, ['correctionRequiredBatches']) }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
      <div class="garmetix-section-card">
        <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 class="garmetix-panel-title">Final Closure</h3>
            <p class="garmetix-panel-subtitle">Complete / Not Complete status before moving outside Purchase Import.</p>
          </div>
          <UBadge :color="finalClosureColor" variant="subtle">{{ readText(finalClosure, ['moduleStatus'], 'Loading') }}</UBadge>
        </div>
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Mode</p><p class="garmetix-metric-value text-base">{{ readText(finalClosure, ['completionMode']) }}</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Accepted Pass</p><p class="garmetix-metric-value">{{ readNumber(finalClosure, ['acceptedPassBatches']) }}</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Open drafts</p><p class="garmetix-metric-value">{{ readNumber(finalClosure, ['openDraftBatches']) }}</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Untested posted</p><p class="garmetix-metric-value">{{ readNumber(finalClosure, ['untestedPostedBatches']) }}</p></div>
          <div class="garmetix-metric-card"><p class="garmetix-metric-label">Corrections</p><p class="garmetix-metric-value">{{ readNumber(finalClosure, ['correctionRequiredBatches']) }}</p></div>
        </div>
        <ul class="mt-3 space-y-2">
          <li v-for="item in readArray(finalClosure, ['closeoutChecklist'])" :key="readText(item, ['key'])" class="flex items-start justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
            <UBadge size="xs" :color="badgeColor(item.status)" variant="subtle">{{ readText(item, ['status']) }}</UBadge>
          </li>
        </ul>
      </div>

      <div class="garmetix-section-card space-y-3">
        <h3 class="garmetix-panel-title">Known Limitations &amp; Next Module</h3>
        <div>
          <h4 class="mb-1 text-xs font-semibold uppercase text-muted">Limitations</h4>
          <ul class="list-disc space-y-1 pl-5 text-sm text-muted">
            <li v-for="item in readArray(finalClosure, ['knownLimitations'])" :key="String(item)">{{ item }}</li>
          </ul>
        </div>
        <div>
          <h4 class="mb-1 text-xs font-semibold uppercase text-muted">Operator rules</h4>
          <ul class="list-disc space-y-1 pl-5 text-sm text-muted">
            <li v-for="item in readArray(finalClosure, ['operatorRules'])" :key="String(item)">{{ item }}</li>
          </ul>
        </div>
        <UAlert color="info" variant="subtle" title="Recommended outside module" :description="readText(finalClosure, ['recommendedNextModule'])" />
        <div>
          <h4 class="mb-1 text-xs font-semibold uppercase text-muted">Next candidates</h4>
          <ul class="list-disc space-y-1 pl-5 text-sm text-muted">
            <li v-for="item in readArray(finalClosure, ['nextModuleCandidates'])" :key="String(item)">{{ item }}</li>
          </ul>
        </div>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between gap-2">
          <div>
            <h3 class="garmetix-panel-title">Parser QA By Template</h3>
            <p class="garmetix-panel-subtitle">Confirm Tally/garment/generic parsers are tested on real supplier invoices.</p>
          </div>
          <UBadge :color="readNumber(parserQa, ['correctionRequiredBatches']) > 0 ? 'warning' : 'info'" variant="subtle">{{ readNumber(parserQa, ['totalBatches']) }} imports</UBadge>
        </div>
        <ul class="mb-3 space-y-2">
          <li v-for="item in readArray(parserQa, ['checklist'])" :key="readText(item, ['key'])" class="flex items-start justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
            <UBadge size="xs" :color="badgeColor(item.status)" variant="subtle">{{ readText(item, ['status']) }}</UBadge>
          </li>
        </ul>
        <div class="overflow-hidden rounded-lg border border-default">
          <table class="w-full text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr><th class="px-2 py-2">Template</th><th class="px-2 py-2 text-right">Imports</th><th class="px-2 py-2 text-right">Posted</th><th class="px-2 py-2 text-right">Pass</th><th class="px-2 py-2 text-right">Correction</th></tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="row in readArray(parserQa, ['templates'])" :key="readText(row, ['parserTemplate'])">
                <td class="px-2 py-2"><UBadge size="xs" color="info" variant="subtle">{{ readText(row, ['parserTemplate']) }}</UBadge></td>
                <td class="px-2 py-2 text-right">{{ readNumber(row, ['batchCount']) }}</td>
                <td class="px-2 py-2 text-right">{{ readNumber(row, ['postedCount']) }}</td>
                <td class="px-2 py-2 text-right">{{ readNumber(row, ['acceptedPassCount']) }}</td>
                <td class="px-2 py-2 text-right">{{ readNumber(row, ['correctionRequiredCount']) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between gap-2">
          <div>
            <h3 class="garmetix-panel-title">Backup / Restore Proof Checklist</h3>
            <p class="garmetix-panel-subtitle">Supplier PDF/image proofs live outside PostgreSQL and must be restored with the database.</p>
          </div>
          <UBadge :color="readBool(backupChecklist, 'hasPostedProofs') ? 'success' : 'warning'" variant="subtle">{{ fileSize(readNumber(backupChecklist, ['totalStorageBytes'])) }}</UBadge>
        </div>
        <div class="mb-3 rounded-md bg-muted/30 p-3 text-sm">
          <p><span class="font-medium">Storage root:</span> {{ readText(backupChecklist, ['storageRoot']) }}</p>
          <p><span class="font-medium">Docker volume:</span> {{ readText(backupChecklist, ['dockerVolume']) }}</p>
          <p><span class="font-medium">Protected proofs:</span> {{ fileSize(readNumber(backupChecklist, ['protectedPostedProofBytes'])) }}</p>
        </div>
        <ul class="mb-3 space-y-2">
          <li v-for="item in readArray(backupChecklist, ['checklist'])" :key="readText(item, ['key'])" class="flex items-start justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
            <UBadge size="xs" :color="badgeColor(item.status)" variant="subtle">{{ readText(item, ['status']) }}</UBadge>
          </li>
        </ul>
        <div class="rounded-md bg-muted/30 p-3 text-xs">
          <p class="mb-1 font-medium">Backup commands</p>
          <p v-for="cmd in readArray(backupChecklist, ['backupCommands'])" :key="String(cmd)" class="font-mono">{{ cmd }}</p>
        </div>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[1fr_1.2fr]">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between gap-2">
          <h3 class="garmetix-panel-title">Acceptance Checklist</h3>
          <UBadge :color="(readNumber(summary, ['failedOrRejectedBatches']) || readNumber(summary, ['needsReviewBatches'])) ? 'warning' : 'success'" variant="subtle">
            {{ (readNumber(summary, ['failedOrRejectedBatches']) || readNumber(summary, ['needsReviewBatches'])) ? 'Attention' : 'Good' }}
          </UBadge>
        </div>
        <ul class="space-y-2">
          <li v-for="item in readArray(summary, ['checklist'])" :key="readText(item, ['key'])" class="flex items-start justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
            <UBadge size="xs" :color="badgeColor(item.status)" variant="subtle">{{ readText(item, ['status']) }}</UBadge>
          </li>
        </ul>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Status Summary</h3>
        <div class="overflow-hidden rounded-lg border border-default">
          <table class="w-full text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr><th class="px-2 py-2">Status</th><th class="px-2 py-2 text-right">Batches</th><th class="px-2 py-2 text-right">Lines</th><th class="px-2 py-2 text-right">Bill</th><th class="px-2 py-2 text-right">Files</th></tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="row in readArray(summary, ['statuses'])" :key="readText(row, ['status'])">
                <td class="px-2 py-2"><UBadge size="xs" :color="badgeColor(row.status)" variant="subtle">{{ readText(row, ['status']) }}</UBadge></td>
                <td class="px-2 py-2 text-right">{{ readNumber(row, ['count']) }}</td>
                <td class="px-2 py-2 text-right">{{ readNumber(row, ['lineCount']) }}</td>
                <td class="px-2 py-2 text-right">{{ money(readNumber(row, ['billAmount'])) }}</td>
                <td class="px-2 py-2 text-right">{{ fileSize(readNumber(row, ['fileBytes'])) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 class="garmetix-panel-title">Recent Supplier Imports</h3>
          <p class="garmetix-panel-subtitle">Open a posting report before posting or when debugging a failed import.</p>
        </div>
        <UButton icon="i-lucide-file-scan" color="neutral" variant="soft" to="/purchase/import">New Import</UButton>
      </div>
      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1200px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-2 py-2">Created</th><th class="px-2 py-2">Supplier</th><th class="px-2 py-2">Invoice</th>
                <th class="px-2 py-2">Status</th><th class="px-2 py-2">Parser</th><th class="px-2 py-2">Accepted</th>
                <th class="px-2 py-2">Correction</th><th class="px-2 py-2 text-right">Lines</th><th class="px-2 py-2 text-right">Bill</th>
                <th class="px-2 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="!recentBatches.length"><td colspan="10" class="px-3 py-8 text-center text-muted">No import batches yet.</td></tr>
              <tr v-for="batch in recentBatches" :key="readText(batch, ['id'])">
                <td class="px-2 py-2 whitespace-nowrap">{{ formatDate(batch.createdAt) }}</td>
                <td class="px-2 py-2 max-w-36 truncate">{{ readText(batch, ['vendorName']) }}</td>
                <td class="px-2 py-2 max-w-32 truncate">{{ readText(batch, ['supplierInvoiceNumber']) }}</td>
                <td class="px-2 py-2"><UBadge size="xs" :color="badgeColor(batch.status)" variant="subtle">{{ readText(batch, ['status']) }}</UBadge></td>
                <td class="px-2 py-2"><UBadge size="xs" color="info" variant="subtle">{{ readText(batch, ['parserTemplate'], 'auto') }}</UBadge></td>
                <td class="px-2 py-2"><UBadge size="xs" :color="badgeColor(readText(batch, ['acceptanceStatus'], 'Untested'))" variant="subtle">{{ readText(batch, ['acceptanceStatus'], 'Untested') }}</UBadge></td>
                <td class="px-2 py-2"><UBadge size="xs" :color="badgeColor(readText(batch, ['correctionStatus'], 'None'))" variant="subtle">{{ readText(batch, ['correctionStatus'], 'None') }}</UBadge></td>
                <td class="px-2 py-2 text-right">{{ readNumber(batch, ['lineCount']) }}</td>
                <td class="px-2 py-2 text-right">{{ money(readNumber(batch, ['billAmount'])) }}</td>
                <td class="px-2 py-2">
                  <div class="flex flex-wrap justify-end gap-1">
                    <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-file-search" :loading="loadingReport === readText(batch, ['id'])" @click="openReport(readText(batch, ['id']))" />
                    <UButton size="xs" color="warning" variant="ghost" icon="i-lucide-route" @click="openCorrectionPlan(readText(batch, ['id']))" />
                    <UButton size="xs" color="success" variant="ghost" icon="i-lucide-check-circle" @click="markAcceptance(readText(batch, ['id']), 'Pass')" />
                    <UButton size="xs" color="warning" variant="ghost" icon="i-lucide-rotate-ccw" @click="markAcceptance(readText(batch, ['id']), 'NeedsRetest')" />
                    <UButton size="xs" color="error" variant="ghost" icon="i-lucide-triangle-alert" @click="requestCorrection(readText(batch, ['id']))" />
                    <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-table" :loading="downloading === readText(batch, ['id']) + '-csv'" @click="downloadBatchLines(readText(batch, ['id']))" />
                    <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-braces" :loading="downloading === readText(batch, ['id']) + '-audit'" @click="downloadBatchAudit(readText(batch, ['id']))" />
                    <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-pencil" :to="`/purchase/import?batchId=${readText(batch, ['id'])}`" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section v-if="correctionPlan" class="garmetix-section-card">
      <div class="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 class="garmetix-panel-title">Correction / Revision Plan</h3>
          <p class="garmetix-panel-subtitle">Safe path for correcting imported drafts or posted purchase inwards.</p>
        </div>
        <UBadge :color="readBool(correctionPlan, 'isPosted') ? 'warning' : 'info'" variant="subtle">{{ readText(correctionPlan, ['correctionStatus']) }}</UBadge>
      </div>
      <div class="grid gap-4 lg:grid-cols-2">
        <div class="space-y-2">
          <UAlert color="neutral" variant="subtle" title="Recommended" :description="readText(correctionPlan, ['recommendedAction'])" />
          <div v-for="item in readArray(correctionPlan, ['checklist'])" :key="readText(item, ['key'])" class="flex items-start justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
            <UBadge size="xs" :color="badgeColor(item.status)" variant="subtle">{{ readText(item, ['status']) }}</UBadge>
          </div>
        </div>
        <div class="space-y-2">
          <h4 class="text-sm font-medium">Safe steps</h4>
          <ol class="list-decimal space-y-1 pl-5 text-sm text-muted">
            <li v-for="step in readArray(correctionPlan, ['steps'])" :key="String(step)">{{ step }}</li>
          </ol>
          <UAlert v-for="warning in readArray(correctionPlan, ['warnings'])" :key="String(warning)" color="warning" variant="subtle" :description="String(warning)" />
        </div>
      </div>
    </section>

    <section v-if="postingReport" class="garmetix-section-card">
      <div class="mb-3 flex items-center justify-between gap-2">
        <div>
          <h3 class="garmetix-panel-title">Posting Report</h3>
          <p class="garmetix-panel-subtitle">Batch {{ readText(postingReport, ['batchId']) }}</p>
        </div>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-table" @click="downloadBatchLines(readText(postingReport, ['batchId']))">CSV</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-braces" @click="downloadBatchAudit(readText(postingReport, ['batchId']))">Audit JSON</UButton>
          <UBadge :color="readBool(postingReport, 'canPost') ? 'success' : 'error'" variant="subtle">{{ readBool(postingReport, 'canPost') ? 'Ready to post' : 'Blocked' }}</UBadge>
        </div>
      </div>
      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <div class="garmetix-metric-card"><p class="garmetix-metric-label">Active/Ignored</p><p class="garmetix-metric-value text-base">{{ readNumber(postingReport, ['activeLineCount']) }} / {{ readNumber(postingReport, ['ignoredLineCount']) }}</p></div>
        <div class="garmetix-metric-card"><p class="garmetix-metric-label">New/Matched</p><p class="garmetix-metric-value text-base">{{ readNumber(postingReport, ['newProductCount']) }} / {{ readNumber(postingReport, ['matchedProductCount']) }}</p></div>
        <div class="garmetix-metric-card"><p class="garmetix-metric-label">Missing barcode/GST</p><p class="garmetix-metric-value text-base">{{ readNumber(postingReport, ['missingBarcodeCount']) }} / {{ readNumber(postingReport, ['missingTaxCount']) }}</p></div>
        <div class="garmetix-metric-card"><p class="garmetix-metric-label">Grand difference</p><p class="garmetix-metric-value text-base">{{ money(readNumber(reconciliation, ['billDifference'])) }}</p></div>
      </div>
      <div class="mt-4 grid gap-4 lg:grid-cols-2">
        <div class="space-y-2">
          <h4 class="text-sm font-medium">Checklist</h4>
          <div v-for="item in readArray(postingReport, ['checklist'])" :key="readText(item, ['key'])" class="flex items-start justify-between gap-2 rounded-md border border-default p-2 text-sm">
            <span><span class="font-medium">{{ readText(item, ['label']) }}:</span> {{ readText(item, ['detail']) }}</span>
            <UBadge size="xs" :color="badgeColor(item.status)" variant="subtle">{{ readText(item, ['status']) }}</UBadge>
          </div>
        </div>
        <div class="space-y-2">
          <h4 class="text-sm font-medium">Warnings</h4>
          <UAlert v-for="warning in readArray(postingReport, ['warnings'])" :key="String(warning)" color="warning" variant="subtle" :description="String(warning)" />
          <p v-if="!readArray(postingReport, ['warnings']).length" class="rounded-md border border-success/40 bg-success/10 p-2 text-sm text-success">No posting warnings.</p>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Import Acceptance - Garmetix Back Office' })

const { download, get, post } = useMainApiClient()

const loading = ref(false)
const loadingReport = ref('')
const downloading = ref('')
const error = ref('')
const message = ref('')

const summary = ref<ApiRecord | null>(null)
const backupChecklist = ref<ApiRecord | null>(null)
const parserQa = ref<ApiRecord | null>(null)
const finalClosure = ref<ApiRecord | null>(null)
const correctionPlan = ref<ApiRecord | null>(null)
const postingReport = ref<ApiRecord | null>(null)

const recentBatches = computed(() => readArray(summary.value, ['recentBatches']))
const reconciliation = computed(() => (postingReport.value as ApiRecord | null)?.reconciliation as ApiRecord ?? {})
const finalClosureColor = computed(() => {
  const status = readText(finalClosure.value, ['moduleStatus'], '')
  if (status === 'Complete') return 'success' as const
  if (status === 'Not Complete') return 'error' as const
  return 'warning' as const
})

function readBool(source: ApiRecord | null, key: string) { return Boolean(source?.[key]) }
function money(value: number) { return formatIndianMoney(value) }
function fileSize(bytes: number) {
  if (!bytes) return '0 B'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}
function badgeColor(status: unknown) {
  const value = String(status ?? '').toLowerCase()
  if (['pass', 'posted', 'readytopost'].includes(value)) return 'success' as const
  if (['warn', 'needsreview', 'info', 'needsretest', 'draftneedscorrection'].includes(value)) return 'warning' as const
  if (['block', 'failed', 'rejected', 'fail', 'correctionrequired'].includes(value)) return 'error' as const
  return 'neutral' as const
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [acceptance, backup, parser, closure] = await Promise.all([
      get<ApiRecord>('purchase-import/acceptance-summary'),
      get<ApiRecord>('purchase-import/backup-restore-checklist'),
      get<ApiRecord>('purchase-import/parser-qa-summary'),
      get<ApiRecord>('purchase-import/final-closure-status')
    ])
    summary.value = acceptance
    backupChecklist.value = backup
    parserQa.value = parser
    finalClosure.value = closure
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase import acceptance summary.'
  } finally {
    loading.value = false
  }
}

async function openReport(batchId: string) {
  if (!batchId) return
  loadingReport.value = batchId
  error.value = ''
  try {
    postingReport.value = await get<ApiRecord>(`purchase-import/batches/${batchId}/posting-report`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load import posting report.'
  } finally {
    loadingReport.value = ''
  }
}

async function openCorrectionPlan(batchId: string) {
  if (!batchId) return
  error.value = ''
  try {
    correctionPlan.value = await get<ApiRecord>(`purchase-import/batches/${batchId}/correction-plan`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load correction plan.'
  }
}

async function markAcceptance(batchId: string, status: 'Pass' | 'Fail' | 'NeedsRetest') {
  if (!batchId) return
  const notes = window.prompt(status === 'Pass' ? 'Acceptance notes (optional)' : 'Reason / correction note')
  if (notes === null && status !== 'Pass') return
  error.value = ''
  try {
    await post<unknown>(`purchase-import/batches/${batchId}/acceptance`, { status, notes: notes || null })
    message.value = `Import marked ${status}.`
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update import acceptance.'
  }
}

async function requestCorrection(batchId: string) {
  if (!batchId) return
  const reason = window.prompt('Why does this posted/draft import need correction?')
  if (!reason) return
  error.value = ''
  try {
    const safety = await post<ApiRecord>(`purchase-import/batches/${batchId}/request-correction`, { reason, requestedAction: 'Review purchase inward before stock/accounting correction' })
    message.value = readBool(safety, 'isPosted') ? 'Posted import flagged for controlled correction.' : 'Draft flagged for correction.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to request import correction.'
  }
}

async function downloadBatchLines(batchId: string) {
  if (!batchId) return
  downloading.value = `${batchId}-csv`
  error.value = ''
  try {
    await download(`purchase-import/batches/${batchId}/lines-csv`, {}, `purchase-import-lines-${batchId}.csv`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download lines CSV.'
  } finally {
    downloading.value = ''
  }
}

async function downloadBatchAudit(batchId: string) {
  if (!batchId) return
  downloading.value = `${batchId}-audit`
  error.value = ''
  try {
    await download(`purchase-import/batches/${batchId}/audit-json`, {}, `purchase-import-audit-${batchId}.json`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download audit JSON.'
  } finally {
    downloading.value = ''
  }
}

async function downloadLearningExport() {
  downloading.value = 'learning'
  error.value = ''
  try {
    await download('purchase-import/vendor-profiles/export', {}, 'purchase-import-vendor-learning.json')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download vendor learning export.'
  } finally {
    downloading.value = ''
  }
}

onMounted(refresh)
</script>

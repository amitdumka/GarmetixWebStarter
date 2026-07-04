<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-image-check" class="size-4" /> Photo proof</p>
          <h2 class="garmetix-dashboard-title">Photo Review</h2>
          <p class="garmetix-dashboard-subtitle">
            Review kiosk photo proof rows before payroll close. Regularization can be created from suspicious or missing proof.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-monitor-dot" color="primary" variant="soft" to="/attendance/kiosk-monitor">Kiosk Monitor</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-6">
      <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div class="grid gap-3 sm:grid-cols-2">
          <UFormField label="Status">
            <USelect v-model="statusFilter" :items="statusOptions" />
          </UFormField>
          <UFormField label="Rows">
            <UInput v-model="take" type="number" min="1" max="500" />
          </UFormField>
        </div>
        <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="loading" @click="load">Load Proofs</UButton>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
      <div class="garmetix-table-panel overflow-hidden">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Photo Proof Queue</h3>
            <p class="garmetix-panel-subtitle">{{ rows.length }} row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[1080px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Captured</th>
                <th class="px-3 py-2">Verification</th>
                <th class="px-3 py-2">Review</th>
                <th class="px-3 py-2">Client Punch</th>
                <th class="px-3 py-2">Retention</th>
                <th class="px-3 py-2">Remarks</th>
                <th class="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in rows" :key="readText(row, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ employeeName(readText(row, ['employeeId'], '')) }}<br><span class="text-xs text-muted">{{ readText(row, ['employeeId']) }}</span></td>
                <td class="px-3 py-2">{{ dateText(readText(row, ['capturedAtUtc'], '')) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['verificationStatus']) }}</td>
                <td class="px-3 py-2"><UBadge :color="reviewTone(row)" variant="subtle">{{ readText(row, ['reviewStatus']) }}</UBadge></td>
                <td class="px-3 py-2">{{ readText(row, ['clientPunchId']) }}</td>
                <td class="px-3 py-2">{{ dateText(readText(row, ['retentionUntilUtc'], '')) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['reviewRemarks', 'remarks']) }}</td>
                <td class="px-3 py-2">
                  <div class="flex justify-end gap-1">
                    <UButton size="xs" icon="i-lucide-eye" color="neutral" variant="soft" @click="selectRow(row)">Review</UButton>
                    <UButton size="xs" icon="i-lucide-file-warning" color="warning" variant="soft" @click="createRegularization(row)">Regularize</UButton>
                  </div>
                </td>
              </tr>
              <tr v-if="!rows.length">
                <td class="px-3 py-6 text-center text-muted" colspan="8">No photo proof rows returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Review Decision</h3>
            <p class="garmetix-panel-subtitle">{{ selected ? employeeName(readText(selected, ['employeeId'], '')) : 'Select a proof row' }}</p>
          </div>
        </div>
        <div class="grid gap-3">
          <UFormField label="Decision">
            <USelect v-model="review.decision" :items="decisionOptions" />
          </UFormField>
          <UFormField label="Reason">
            <UInput v-model="review.reason" placeholder="Clear, blurred, wrong employee..." />
          </UFormField>
          <UFormField label="Remarks">
            <UTextarea v-model="review.remarks" autoresize />
          </UFormField>
          <UCheckbox v-model="review.createRegularizationRequest" label="Create regularization request" />
          <UFormField label="Type REVIEW to enable">
            <UInput v-model="confirmText" placeholder="REVIEW" />
          </UFormField>
          <UButton icon="i-lucide-check-check" :disabled="!canReview" :loading="reviewing" @click="submitReview">Save Review</UButton>
          <div v-if="selected" class="rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <p><span class="text-muted">Proof path:</span> {{ readText(selected, ['proofPath', 'photoProofPath']) }}</p>
            <p><span class="text-muted">Reviewed by:</span> {{ readText(selected, ['reviewedBy']) }}</p>
            <p><span class="text-muted">Reviewed at:</span> {{ dateText(readText(selected, ['reviewedAtUtc'], '')) }}</p>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Photo Review - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const reviewing = ref(false)
const message = ref('')
const messageTone = ref<'info' | 'success' | 'warning' | 'error'>('info')
const rows = ref<ApiRecord[]>([])
const employees = ref<ApiRecord[]>([])
const summary = ref<ApiRecord | null>(null)
const selected = ref<ApiRecord | null>(null)
const statusFilter = ref('PendingReview')
const take = ref(100)
const confirmText = ref('')
const review = reactive({
  decision: 'Approved',
  reason: '',
  remarks: '',
  createRegularizationRequest: false
})

const statusOptions = ['PendingReview', 'Approved', 'Rejected', 'Flagged', 'NeedsRegularization', 'PhotoProofOnly'].map(value => ({ label: value, value }))
const decisionOptions = ['Approved', 'Rejected', 'Flagged', 'NeedsRegularization'].map(value => ({ label: value, value }))
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const canReview = computed(() => Boolean(selected.value && confirmText.value.trim().toUpperCase() === 'REVIEW') && !reviewing.value)
const summaryCards = computed(() => [
  { label: 'Pending', value: readNumber(summary.value, ['pending']) },
  { label: 'Approved', value: readNumber(summary.value, ['approved']) },
  { label: 'Rejected', value: readNumber(summary.value, ['rejected']) },
  { label: 'Flagged', value: readNumber(summary.value, ['flagged']) },
  { label: 'Regularize', value: readNumber(summary.value, ['needsRegularization']) },
  { label: 'Expiring', value: readNumber(summary.value, ['expiringSoon']) }
])

function showMessage(text: string, tone: typeof messageTone.value = 'success') {
  message.value = text
  messageTone.value = tone
}

function employeeName(employeeId: string) {
  const employee = employees.value.find(item => readText(item, ['id'], '') === employeeId)
  if (!employee) return employeeId || '-'
  const name = readText(employee, ['staffName', 'fullName', 'name'], '')
  if (name && name !== '-') return name
  return `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || readText(employee, ['employeeCode'], employeeId)
}

function dateText(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('en-IN')
}

function reviewTone(row: ApiRecord) {
  const status = readText(row, ['reviewStatus'], '').toLowerCase()
  if (status.includes('approved')) return 'success'
  if (status.includes('reject')) return 'error'
  if (status.includes('regular')) return 'warning'
  if (status.includes('flag')) return 'warning'
  return 'neutral'
}

function selectRow(row: ApiRecord) {
  selected.value = row
  review.decision = readText(row, ['reviewStatus'], 'Approved') === 'PendingReview' ? 'Approved' : readText(row, ['reviewStatus'], 'Approved')
  review.reason = readText(row, ['reviewReason'], '') === '-' ? '' : readText(row, ['reviewReason'], '')
  review.remarks = readText(row, ['reviewRemarks'], '') === '-' ? '' : readText(row, ['reviewRemarks'], '')
  review.createRegularizationRequest = review.decision === 'NeedsRegularization'
  confirmText.value = ''
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [proofRows, summaryRow, employeeRows] = await Promise.all([
      get<ApiRecord[]>('api/attendance/photo-proofs', { take: Number(take.value || 100), status: statusFilter.value || undefined }),
      get<ApiRecord>('api/attendance/photo-proofs/review-summary'),
      get<ApiRecord[]>('api/employees')
    ])
    rows.value = Array.isArray(proofRows) ? proofRows : []
    summary.value = summaryRow
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    if (selected.value) {
      selected.value = rows.value.find(row => readText(row, ['id'], '') === readText(selected.value, ['id'], '')) ?? null
    }
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Unable to load photo proof review.', 'error')
  } finally {
    loading.value = false
  }
}

async function submitReview() {
  const id = readText(selected.value, ['id'], '')
  if (!id || !canReview.value) return
  reviewing.value = true
  try {
    await post<ApiRecord>(`api/attendance/photo-proofs/${id}/review`, {
      decision: review.decision,
      reason: review.reason || null,
      remarks: review.remarks || null,
      createRegularizationRequest: review.createRegularizationRequest
    })
    showMessage('Photo proof review saved.')
    confirmText.value = ''
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not save photo proof review.', 'error')
  } finally {
    reviewing.value = false
  }
}

async function createRegularization(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  if (!id || !window.confirm(`Create regularization from photo proof for ${employeeName(readText(row, ['employeeId'], ''))}?`)) return
  try {
    await post<ApiRecord>(`api/attendance/photo-proofs/${id}/regularization`, {
      remarks: 'Created from modular HR photo review page.'
    })
    showMessage('Regularization request created from photo proof.')
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not create regularization from proof.', 'error')
  }
}

onMounted(load)
</script>

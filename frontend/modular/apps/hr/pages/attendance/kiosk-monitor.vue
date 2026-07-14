<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-monitor-dot" class="size-4" /> Kiosk evidence</p>
          <h2 class="garmetix-dashboard-title">Kiosk Monitor</h2>
          <p class="garmetix-dashboard-subtitle">
            Review kiosk photo proof summary and offline sync batch evidence from the attendance API.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Photo Proofs</h3>
            <p class="garmetix-panel-subtitle">{{ photoProofs.length }} row(s)</p>
          </div>
          <UButton size="sm" variant="soft" icon="i-lucide-image" to="/attendance/photo-review">Review</UButton>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[760px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Device</th>
                <th class="px-3 py-2">Captured</th>
                <th class="px-3 py-2">Status</th>
                <th class="px-3 py-2">Review</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in photoProofs" :key="readText(row, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(row, ['employeeName', 'staffName', 'employeeCode']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['deviceCode', 'deviceName']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['capturedAtUtc', 'createdAtUtc']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['verificationStatus', 'status']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['reviewStatus', 'decision']) }}</td>
              </tr>
              <tr v-if="!photoProofs.length">
                <td class="px-3 py-6 text-center text-muted" colspan="5">No photo proof rows.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Sync Batches</h3>
            <p class="garmetix-panel-subtitle">{{ syncBatches.length }} offline sync row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[720px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Device</th>
                <th class="px-3 py-2">Received</th>
                <th class="px-3 py-2">Accepted</th>
                <th class="px-3 py-2">Duplicate</th>
                <th class="px-3 py-2">Failed</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in syncBatches" :key="readText(row, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(row, ['deviceCode', 'deviceName', 'deviceId']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['receivedAtUtc', 'createdAtUtc']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['acceptedCount', 'accepted']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['duplicateCount', 'duplicate']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['failedCount', 'failed']) }}</td>
              </tr>
              <tr v-if="!syncBatches.length">
                <td class="px-3 py-6 text-center text-muted" colspan="5">No sync batch rows.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Kiosk Monitor - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const photoProofs = ref<ApiRecord[]>([])
const syncBatches = ref<ApiRecord[]>([])

const cards = computed(() => [
  { label: 'Pending', value: readNumber(summary.value, ['pending']) },
  { label: 'Approved', value: readNumber(summary.value, ['approved']) },
  { label: 'Rejected', value: readNumber(summary.value, ['rejected']) },
  { label: 'Flagged', value: readNumber(summary.value, ['flagged']) },
  { label: 'Regularization', value: readNumber(summary.value, ['needsRegularization']) },
  { label: 'Sync Batches', value: syncBatches.value.length }
])

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [summaryResponse, proofsResponse, batchesResponse] = await Promise.all([
      get<ApiRecord>('api/attendance/photo-proofs/review-summary'),
      get<ApiRecord[]>('api/attendance/photo-proofs'),
      get<ApiRecord[]>('api/attendance/sync-batches')
    ])
    summary.value = summaryResponse
    photoProofs.value = Array.isArray(proofsResponse) ? proofsResponse : []
    syncBatches.value = Array.isArray(batchesResponse) ? batchesResponse : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load kiosk monitor.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

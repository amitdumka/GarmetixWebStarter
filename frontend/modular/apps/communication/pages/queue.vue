<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-list-checks" class="size-4" />
            Communication & Mail
          </p>
          <h2 class="garmetix-dashboard-title">Queue & Log</h2>
          <p class="garmetix-dashboard-subtitle">Every outbound email's status, attempts and delivery timeline.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <div class="flex flex-wrap items-center gap-2">
      <USelect v-model="statusFilter" :items="statusOptions" placeholder="All statuses" class="w-48" @update:model-value="refresh" />
      <UInput v-model="search" placeholder="Search subject..." icon="i-lucide-search" @keyup.enter="refresh" />
      <UInput v-model="correlationId" placeholder="Correlation ID" class="w-48" @keyup.enter="refresh" />
    </div>

    <section class="garmetix-section-card">
      <CommunicationMasterTable :columns="columns" :rows="rows" empty-text="No queued emails yet.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" @click="openDetail(row.id)">Open</UButton>
            <UButton v-if="row.canRetry" size="xs" color="neutral" variant="soft" icon="i-lucide-rotate-cw" @click="doAction(row.id, 'retry')">Retry</UButton>
            <UButton v-if="row.canCancel" size="xs" color="error" variant="soft" icon="i-lucide-circle-x" @click="doAction(row.id, 'cancel')">Cancel</UButton>
            <UButton v-if="row.status === 'DeadLetter'" size="xs" color="success" variant="soft" icon="i-lucide-undo-2" @click="doAction(row.id, 'restore')">Restore</UButton>
          </div>
        </template>
      </CommunicationMasterTable>
      <div class="mt-3 flex items-center justify-between text-sm text-muted">
        <span>{{ totalCount }} item(s)</span>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" :disabled="page <= 1" @click="page--; refresh()">Prev</UButton>
          <span>Page {{ page }}</span>
          <UButton size="xs" color="neutral" variant="soft" :disabled="items.length < pageSize" @click="page++; refresh()">Next</UButton>
        </div>
      </div>
    </section>

    <USlideover v-model:open="detailOpen" title="Queue Item Detail">
      <template #body>
        <div v-if="detail" class="space-y-4">
          <div class="garmetix-row-card">
            <p class="text-sm font-medium">{{ detail.subject }}</p>
            <p class="text-xs text-muted">Status: {{ detail.status }} | Attempts: {{ detail.attemptCount }}/{{ detail.maxAttempts }}</p>
            <p v-if="detail.lastErrorMessage" class="mt-1 text-xs text-error">{{ detail.lastErrorMessage }}</p>
            <p class="mt-1 text-xs text-muted">To: {{ detail.recipientEmails.join(', ') }}</p>
          </div>

          <div>
            <p class="garmetix-panel-subtitle mb-2">Delivery Timeline</p>
            <ul class="space-y-1 text-sm">
              <li v-for="(ev, idx) in detail.events" :key="idx" class="flex items-center gap-2">
                <UIcon name="i-lucide-circle-dot" class="size-3 text-primary" />
                {{ ev.eventType }} - {{ formatDateTime(ev.occurredAtUtc) }}
              </li>
              <li v-if="detail.events.length === 0" class="text-muted">No delivery events recorded yet.</li>
            </ul>
          </div>

          <div>
            <p class="garmetix-panel-subtitle mb-2">Attempts</p>
            <ul class="space-y-1 text-sm">
              <li v-for="attempt in detail.attempts" :key="attempt.attemptNumber" class="flex items-center gap-2">
                <UIcon :name="attempt.wasSuccess ? 'i-lucide-circle-check' : 'i-lucide-circle-x'" :class="attempt.wasSuccess ? 'text-success' : 'text-error'" class="size-4" />
                #{{ attempt.attemptNumber }} - {{ formatDateTime(attempt.startedAtUtc) }}
                <span v-if="attempt.errorMessage" class="text-xs text-muted">({{ attempt.errorMessage }})</span>
              </li>
              <li v-if="detail.attempts.length === 0" class="text-muted">No attempts recorded yet.</li>
            </ul>
          </div>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import CommunicationMasterTable from '../components/CommunicationMasterTable.vue'
import { formatDateTime, useCommunicationApiClient } from '../utils/communication-api'

useHead({ title: 'Queue & Log - Garmetix Communication & Mail' })

interface QueueItemSummary {
  id: string
  status: string
  subject: string
  sourceModule: string | null
  sourceType: string | null
  correlationId: string
  providerMessageId: string | null
  attemptCount: number
  lastErrorCode: string | null
  createdAt: string
  recipientEmail: string | null
}

interface QueueItemDetail {
  id: string
  status: string
  subject: string
  attemptCount: number
  maxAttempts: number
  lastErrorMessage: string | null
  recipientEmails: string[]
  attempts: { attemptNumber: number, startedAtUtc: string, wasSuccess: boolean, errorMessage: string | null }[]
  events: { eventType: string, occurredAtUtc: string }[]
}

const { get, post } = useCommunicationApiClient()

const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const correlationId = ref('')
const statusFilter = ref<string | undefined>(undefined)
const statusOptions = ['Draft', 'Pending', 'Scheduled', 'Processing', 'Sent', 'Delivered', 'Deferred', 'Bounced', 'Complained', 'Rejected', 'Cancelled', 'Failed', 'DeadLetter']
const page = ref(1)
const pageSize = 25
const totalCount = ref(0)
const items = ref<QueueItemSummary[]>([])

const columns = [
  { key: 'subject', label: 'Subject' },
  { key: 'recipientEmail', label: 'To' },
  { key: 'status', label: 'Status' },
  { key: 'attemptCount', label: 'Attempts' },
  { key: 'createdAt', label: 'Created' }
]
const rows = computed(() => items.value.map(i => ({
  id: i.id,
  subject: i.subject,
  recipientEmail: i.recipientEmail || '-',
  status: i.status,
  attemptCount: i.attemptCount,
  createdAt: formatDateTime(i.createdAt),
  canRetry: ['Failed', 'DeadLetter', 'Rejected'].includes(i.status),
  canCancel: ['Draft', 'Pending', 'Scheduled', 'Processing'].includes(i.status)
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const response = await get<{ totalCount: number, items: QueueItemSummary[] }>('/communication/queue', {
      status: statusFilter.value || undefined,
      search: search.value || undefined,
      correlationId: correlationId.value || undefined,
      page: page.value,
      pageSize
    })
    items.value = response?.items ?? []
    totalCount.value = response?.totalCount ?? 0
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the queue.'
  } finally {
    loading.value = false
  }
}

const detailOpen = ref(false)
const detail = ref<QueueItemDetail | null>(null)

async function openDetail(id: string) {
  error.value = ''
  try {
    detail.value = await get<QueueItemDetail>(`/communication/queue/${id}`)
    detailOpen.value = true
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the queue item.'
  }
}

async function doAction(id: string, action: 'retry' | 'cancel' | 'restore') {
  error.value = ''
  try {
    await post(`/communication/queue/${id}/${action}`)
    message.value = `Queue item ${action === 'restore' ? 'restored' : action + 'd'}.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : `Failed to ${action} the queue item.`
  }
}

onMounted(refresh)
</script>

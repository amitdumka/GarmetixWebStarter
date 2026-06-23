<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Attendance correction queue</p>
          <h2 class="mt-1 text-2xl font-semibold">Regularization</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review missed punch and correction requests. Approve or reject records with an audit remark.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="overflow-hidden border border-default bg-muted/10">
      <div class="overflow-auto">
        <table class="w-full min-w-[1040px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Request</th>
              <th class="px-3 py-2">Punch</th>
              <th class="px-3 py-2">Reason</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in rows" :key="rowKey(row, index)" class="border-t border-default align-top">
              <td class="px-3 py-2">
                <p class="font-medium">{{ readText(row, ['employeeName', 'employee', 'employeeId']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['requestedBy', 'createdBy']) }}</p>
              </td>
              <td class="px-3 py-2">{{ readText(row, ['requestType']) }}</td>
              <td class="px-3 py-2">
                <p>{{ readText(row, ['requestedPunchType']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['requestedLocalPunchTime', 'requestedPunchTimeUtc']) }}</p>
              </td>
              <td class="px-3 py-2">{{ readText(row, ['reason']) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="statusTone(row)" variant="subtle">{{ readText(row, ['status']) }}</UBadge>
              </td>
              <td class="px-3 py-2">
                <div class="flex min-w-64 flex-col gap-2">
                  <UInput v-model="remarks[rowKey(row, index)]" size="xs" placeholder="Manager remark" />
                  <div class="flex flex-wrap gap-2">
                    <UButton size="xs" color="success" variant="soft" :disabled="!isPending(row)" :loading="decidingId === readText(row, ['id'], '')" @click="decide(row, true)">Approve</UButton>
                    <UButton size="xs" color="error" variant="soft" :disabled="!isPending(row)" :loading="decidingId === readText(row, ['id'], '')" @click="decide(row, false)">Reject</UButton>
                  </div>
                </div>
              </td>
            </tr>
            <tr v-if="!rows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="6">No regularization requests returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Regularization - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const decidingId = ref('')
const rows = ref<ApiRecord[]>([])
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const remarks = reactive<Record<string, string>>({})

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const cards = computed(() => [
  { label: 'Requests', value: rows.value.length },
  { label: 'Pending', value: rows.value.filter(isPending).length },
  { label: 'Approved', value: rows.value.filter(row => readText(row, ['status'], '').toLowerCase() === 'approved').length },
  { label: 'Rejected', value: rows.value.filter(row => readText(row, ['status'], '').toLowerCase() === 'rejected').length }
])

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id'], String(index))
}

function isPending(row: ApiRecord) {
  return readText(row, ['status'], '').toLowerCase() === 'pending'
}

function statusTone(row: ApiRecord) {
  const status = readText(row, ['status'], '').toLowerCase()
  if (status === 'approved') return 'success'
  if (status === 'rejected') return 'error'
  if (status === 'pending') return 'warning'
  return 'neutral'
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const response = await get<ApiRecord[]>('api/attendance/regularization')
    rows.value = Array.isArray(response) ? response : []
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load regularization requests.'
  } finally {
    loading.value = false
  }
}

async function decide(row: ApiRecord, approved: boolean) {
  const id = readText(row, ['id'], '')
  if (!id) {
    messageTone.value = 'warning'
    message.value = 'This request has no saved id.'
    return
  }

  decidingId.value = id
  message.value = ''
  try {
    await post<ApiRecord>(`api/attendance/regularization/${id}/${approved ? 'approve' : 'reject'}`, {
      remarks: remarks[id] || `${approved ? 'Approved' : 'Rejected'} from modular HR app.`
    })
    messageTone.value = 'success'
    message.value = `Regularization request ${approved ? 'approved' : 'rejected'}.`
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to update regularization request.'
  } finally {
    decidingId.value = ''
  }
}

onMounted(load)
</script>

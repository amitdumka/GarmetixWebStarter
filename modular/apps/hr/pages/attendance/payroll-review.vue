<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="text-sm text-muted">Attendance payroll control</p>
          <h2 class="mt-1 text-2xl font-semibold">Payroll Review</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Rebuilds review rows from monthly attendance and marks rows for payroll. It does not create salary slips or salary payments.
          </p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="load">
          <UFormField label="Year" name="year">
            <UInput v-model.number="year" type="number" min="2020" max="2100" />
          </UFormField>
          <UFormField label="Month" name="month">
            <UInput v-model.number="month" type="number" min="1" max="12" />
          </UFormField>
          <UButton type="submit" icon="i-lucide-refresh-cw" :loading="loading">Load</UButton>
          <UButton color="primary" variant="soft" icon="i-lucide-wand-sparkles" :loading="rebuilding" @click="rebuild">Rebuild</UButton>
        </form>
      </div>
    </div>

    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Safe review action"
      description="This screen only prepares attendance numbers for payroll review. Salary slips, salary payments, and accounting vouchers are not posted here."
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="overflow-hidden border border-default bg-muted/10">
      <div class="overflow-auto">
        <table class="w-full min-w-[1120px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Present</th>
              <th class="px-3 py-2">Absent</th>
              <th class="px-3 py-2">Half</th>
              <th class="px-3 py-2">Late</th>
              <th class="px-3 py-2">Payable</th>
              <th class="px-3 py-2">Deduction</th>
              <th class="px-3 py-2">OT</th>
              <th class="px-3 py-2">Est. Gross</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in rows" :key="rowKey(row, index)" class="border-t border-default align-top">
              <td class="px-3 py-2">
                <p class="font-medium">{{ readText(row, ['employeeName', 'employee']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['employeeCode', 'code']) }}</p>
              </td>
              <td class="px-3 py-2">{{ readNumber(row, ['presentDays']) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['absentDays']) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['halfDays']) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['lateDays']) }}</td>
              <td class="px-3 py-2 font-semibold">{{ readNumber(row, ['payableDays']) }}</td>
              <td class="px-3 py-2 font-semibold">{{ readNumber(row, ['deductionDays']) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['overtimeMinutes']) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['estimatedGrossPay'])) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="reviewTone(row)" variant="subtle">{{ readText(row, ['reviewStatus']) }}</UBadge>
              </td>
              <td class="px-3 py-2">
                <div class="flex min-w-64 flex-col gap-2">
                  <UInput v-model="notes[rowKey(row, index)]" size="xs" placeholder="Review note" />
                  <div class="flex flex-wrap gap-2">
                    <UButton size="xs" variant="soft" :loading="markingId === readText(row, ['id'], '')" @click="mark(row, 'Reviewed')">Reviewed</UButton>
                    <UButton size="xs" color="success" variant="soft" :loading="markingId === readText(row, ['id'], '')" @click="mark(row, 'ApprovedForPayroll')">Approve</UButton>
                    <UButton size="xs" color="warning" variant="soft" :loading="markingId === readText(row, ['id'], '')" @click="mark(row, 'OnHold')">Hold</UButton>
                  </div>
                </div>
              </td>
            </tr>
            <tr v-if="!rows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="11">No review rows found. Rebuild after monthly attendance is calculated.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { currentYearMonth, readArray, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Payroll Review - Garmetix HR' })

const { get, post } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const rebuilding = ref(false)
const markingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const review = ref<ApiRecord | null>(null)
const notes = reactive<Record<string, string>>({})

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const rows = computed(() => readArray(review.value, ['rows', 'Rows']))
const cards = computed(() => [
  { label: 'Employees', value: readNumber(review.value, ['employees', 'employeeCount']) },
  { label: 'Payable Days', value: readNumber(review.value, ['payableDays']) },
  { label: 'Deduction Days', value: readNumber(review.value, ['deductionDays']) },
  { label: 'Late Days', value: readNumber(review.value, ['lateDays']) },
  { label: 'OT Minutes', value: readNumber(review.value, ['overtimeMinutes']) },
  { label: 'Reviewed', value: readNumber(review.value, ['reviewedRows']) }
])

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id'], String(index))
}

function reviewTone(row: ApiRecord) {
  const status = readText(row, ['reviewStatus'], '').toLowerCase()
  if (status.includes('approved')) return 'success'
  if (status.includes('hold')) return 'warning'
  if (status.includes('review')) return 'primary'
  return 'neutral'
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    review.value = await get<ApiRecord>('api/attendance/payroll-review', { year: year.value, month: month.value })
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load payroll review.'
  } finally {
    loading.value = false
  }
}

async function rebuild() {
  rebuilding.value = true
  message.value = ''
  try {
    review.value = await post<ApiRecord>('api/attendance/payroll-review/rebuild', { year: year.value, month: month.value })
    messageTone.value = 'success'
    message.value = 'Attendance payroll review rebuilt. Salary slips and payments were not posted.'
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to rebuild payroll review.'
  } finally {
    rebuilding.value = false
  }
}

async function mark(row: ApiRecord, status: string) {
  const id = readText(row, ['id'], '')
  if (!id) {
    messageTone.value = 'warning'
    message.value = 'This review row has no saved id. Rebuild review first.'
    return
  }

  markingId.value = id
  message.value = ''
  try {
    await post<ApiRecord>(`api/attendance/payroll-review/${id}/mark-reviewed`, {
      reviewStatus: status,
      notes: notes[id] || `Marked ${status} from modular HR app.`
    })
    messageTone.value = 'success'
    message.value = `${readText(row, ['employeeName', 'employee'])} marked ${status}.`
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to mark payroll review row.'
  } finally {
    markingId.value = ''
  }
}

onMounted(load)
</script>

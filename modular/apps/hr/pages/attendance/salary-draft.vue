<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="text-sm text-muted">Attendance salary preparation</p>
          <h2 class="mt-1 text-2xl font-semibold">Salary Draft</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Build preview salary draft rows from reviewed attendance. This page does not generate final salary slips.
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
      title="Preview-only salary draft"
      description="Rows can be rebuilt and marked ready, but final payslip and salary payment generation remain disabled in this modular stage."
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
              <th class="px-3 py-2">Payable</th>
              <th class="px-3 py-2">Deduction Days</th>
              <th class="px-3 py-2">Gross</th>
              <th class="px-3 py-2">Benefits</th>
              <th class="px-3 py-2">Deductions</th>
              <th class="px-3 py-2">Net Preview</th>
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
              <td class="px-3 py-2">{{ readNumber(row, ['payableDays']) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['deductionDays']) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['attendanceGrossPreview'])) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(benefits(row)) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(deductions(row)) }}</td>
              <td class="px-3 py-2 font-semibold">{{ formatIndianMoney(readNumber(row, ['netPayPreview'])) }}</td>
              <td class="px-3 py-2 space-y-1">
                <UBadge :color="draftTone(row)" variant="subtle">{{ readText(row, ['draftStatus']) }}</UBadge>
                <UBadge v-if="readText(row, ['payrollPostStatus'], '').toLowerCase().includes('generated')" color="success" variant="subtle">
                  Generated
                </UBadge>
                <p v-if="readText(row, ['generatedSalaryPaySlipId'], '')" class="text-xs text-muted">Payslip linked</p>
              </td>
              <td class="px-3 py-2">
                <div class="flex min-w-56 flex-col gap-2">
                  <UInput v-model="notes[rowKey(row, index)]" size="xs" placeholder="Draft note" />
                  <div class="flex flex-wrap gap-2">
                    <UButton size="xs" color="success" variant="soft" :disabled="isGenerated(row)" :loading="markingId === readText(row, ['id'], '')" @click="mark(row, 'ReadyForPayroll')">Ready</UButton>
                    <UButton size="xs" color="warning" variant="soft" :disabled="isGenerated(row)" :loading="markingId === readText(row, ['id'], '')" @click="mark(row, 'OnHold')">Hold</UButton>
                  </div>
                </div>
              </td>
            </tr>
            <tr v-if="!rows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="9">No salary draft rows found. Mark payroll review rows Reviewed or ApprovedForPayroll, then rebuild drafts.</td>
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

useHead({ title: 'Salary Draft - Garmetix HR' })

const { get, post } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const rebuilding = ref(false)
const markingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const draft = ref<ApiRecord | null>(null)
const notes = reactive<Record<string, string>>({})

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const rows = computed(() => readArray(draft.value, ['rows', 'Rows']))
const cards = computed(() => [
  { label: 'Employees', value: readNumber(draft.value, ['employees']) },
  { label: 'Ready Rows', value: readNumber(draft.value, ['readyRows']) },
  { label: 'Draft Rows', value: readNumber(draft.value, ['draftRows']) },
  { label: 'Gross Preview', value: formatIndianMoney(readNumber(draft.value, ['totalGrossPreview'])) },
  { label: 'Deduction Preview', value: formatIndianMoney(readNumber(draft.value, ['totalDeductionPreview'])) },
  { label: 'Net Preview', value: formatIndianMoney(readNumber(draft.value, ['totalNetPayPreview'])) }
])

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id'], String(index))
}

function benefits(row: ApiRecord) {
  return readNumber(row, ['bonusPreview']) + readNumber(row, ['leaveEncashmentPreview'])
}

function deductions(row: ApiRecord) {
  return readNumber(row, ['salaryAdvanceRecoveryPreview']) +
    readNumber(row, ['pfEmployeePreview']) +
    readNumber(row, ['gratuityPreview']) +
    readNumber(row, ['otherDeductionPreview'])
}

function isGenerated(row: ApiRecord) {
  return readText(row, ['payrollPostStatus'], '').toLowerCase().includes('generated')
}

function draftTone(row: ApiRecord) {
  const status = readText(row, ['draftStatus'], '').toLowerCase()
  if (status.includes('ready')) return 'success'
  if (status.includes('hold')) return 'warning'
  return 'neutral'
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    draft.value = await get<ApiRecord>('api/attendance/salary-slip-drafts', { year: year.value, month: month.value })
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load salary drafts.'
  } finally {
    loading.value = false
  }
}

async function rebuild() {
  rebuilding.value = true
  message.value = ''
  try {
    draft.value = await post<ApiRecord>('api/attendance/salary-slip-drafts/rebuild', { year: year.value, month: month.value })
    messageTone.value = 'success'
    message.value = 'Salary draft preview rebuilt. Final salary slips were not generated.'
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to rebuild salary draft.'
  } finally {
    rebuilding.value = false
  }
}

async function mark(row: ApiRecord, status: string) {
  const id = readText(row, ['id'], '')
  if (!id) {
    messageTone.value = 'warning'
    message.value = 'This draft row has no saved id. Rebuild drafts first.'
    return
  }

  markingId.value = id
  message.value = ''
  try {
    await post<ApiRecord>(`api/attendance/salary-slip-drafts/${id}/mark-ready`, {
      draftStatus: status,
      notes: notes[id] || `Marked ${status} from modular HR app.`
    })
    messageTone.value = 'success'
    message.value = `${readText(row, ['employeeName', 'employee'])} marked ${status}.`
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to update salary draft row.'
  } finally {
    markingId.value = ''
  }
}

onMounted(load)
</script>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-receipt-indian-rupee" class="size-4" /> Attendance salary preparation</p>
          <h2 class="garmetix-dashboard-title">Salary Draft</h2>
          <p class="garmetix-dashboard-subtitle">
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
      description="Rows can be rebuilt and marked ready. Final payslip generation is available only through the guarded action below; salary payment generation is separate."
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel border-warning/30">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Guarded Payslip Generation</h3>
          <p class="garmetix-panel-subtitle">Generates salary payslips from ReadyForPayroll rows. It does not create salary payments or accounting vouchers.</p>
        </div>
        <UBadge color="warning" variant="subtle">{{ payslipPhrase }}</UBadge>
      </div>
      <div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(280px,0.6fr)_auto] lg:items-end">
        <UFormField label="Audit notes">
          <UInput v-model="payslipNotes" placeholder="Approval reference, reviewer name, payroll period note" />
        </UFormField>
        <UFormField label="Type exact phrase">
          <UInput v-model="payslipConfirm" :placeholder="payslipPhrase" />
        </UFormField>
        <UButton icon="i-lucide-file-check-2" color="warning" :disabled="!canGeneratePayslips" :loading="generatingPayslips" @click="generatePayslips">
          Generate Payslips
        </UButton>
      </div>
      <UAlert v-if="payslipMessage" class="mt-3" :color="payslipTone" variant="subtle" :icon="payslipIcon" :description="payslipMessage" />
      <div v-if="payslipResult" class="mt-3 grid gap-3 md:grid-cols-4">
        <div v-for="item in payslipResultCards" :key="item.label" class="garmetix-row-card">
          <p class="text-xs text-muted">{{ item.label }}</p>
          <p class="mt-1 text-base font-semibold">{{ item.value }}</p>
        </div>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Draft Rows</h3>
          <p class="garmetix-panel-subtitle">{{ rows.length }} employee row(s)</p>
        </div>
      </div>
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
const generatingPayslips = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const draft = ref<ApiRecord | null>(null)
const notes = reactive<Record<string, string>>({})
const payslipConfirm = ref('')
const payslipNotes = ref('')
const payslipMessage = ref('')
const payslipTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const payslipResult = ref<ApiRecord | null>(null)

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const payslipIcon = computed(() => payslipTone.value === 'success' ? 'i-lucide-circle-check' : payslipTone.value === 'warning' ? 'i-lucide-triangle-alert' : payslipTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const rows = computed(() => readArray(draft.value, ['rows', 'Rows']))
const salaryMonth = computed(() => `${year.value}${String(month.value).padStart(2, '0')}`)
const payslipPhrase = computed(() => `GENERATE PAYSLIPS ${salaryMonth.value}`)
const readyRows = computed(() => rows.value.filter(row => readText(row, ['draftStatus'], '').toLowerCase() === 'readyforpayroll' && !isGenerated(row)))
const canGeneratePayslips = computed(() => readyRows.value.length > 0 && payslipConfirm.value.trim().toUpperCase() === payslipPhrase.value && payslipNotes.value.trim().length >= 8 && !generatingPayslips.value)
const cards = computed(() => [
  { label: 'Employees', value: readNumber(draft.value, ['employees']) },
  { label: 'Ready Rows', value: readNumber(draft.value, ['readyRows']) },
  { label: 'Draft Rows', value: readNumber(draft.value, ['draftRows']) },
  { label: 'Gross Preview', value: formatIndianMoney(readNumber(draft.value, ['totalGrossPreview'])) },
  { label: 'Deduction Preview', value: formatIndianMoney(readNumber(draft.value, ['totalDeductionPreview'])) },
  { label: 'Net Preview', value: formatIndianMoney(readNumber(draft.value, ['totalNetPayPreview'])) }
])
const payslipResultCards = computed(() => [
  { label: 'Selected Drafts', value: readNumber(payslipResult.value, ['selectedDrafts']) },
  { label: 'Created', value: readNumber(payslipResult.value, ['createdPayslips']) },
  { label: 'Updated', value: readNumber(payslipResult.value, ['updatedPayslips']) },
  { label: 'Net Total', value: formatIndianMoney(readNumber(payslipResult.value, ['totalNet'])) }
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

async function generatePayslips() {
  if (!canGeneratePayslips.value) {
    payslipTone.value = 'warning'
    payslipMessage.value = `Type ${payslipPhrase.value} and enter audit notes before generating payslips.`
    return
  }

  generatingPayslips.value = true
  payslipMessage.value = ''
  payslipResult.value = null
  try {
    const response = await post<ApiRecord>('api/attendance/salary-slip-drafts/generate-payslips', {
      year: year.value,
      month: month.value,
      employeeId: null,
      confirm: true,
      notes: payslipNotes.value
    })
    payslipResult.value = response
    draft.value = response
    payslipTone.value = 'success'
    payslipMessage.value = 'Salary payslips generated. Salary payments and accounting vouchers were not posted.'
    payslipConfirm.value = ''
    await load()
  } catch (caught) {
    payslipTone.value = 'error'
    payslipMessage.value = caught instanceof Error ? caught.message : 'Unable to generate payslips.'
  } finally {
    generatingPayslips.value = false
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

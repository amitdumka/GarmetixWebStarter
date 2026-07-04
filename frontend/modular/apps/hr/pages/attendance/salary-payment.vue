<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-wallet-cards" class="size-4" />
            Salary payment
          </p>
          <h2 class="garmetix-dashboard-title">Salary Payments</h2>
          <p class="garmetix-dashboard-subtitle">
            Preview salary payment amounts including advance deduction, previous due, outstanding amount, and round-off. Final payment generation remains disabled here.
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
        </form>
      </div>
    </div>

    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Preview only"
      description="Preview is safe. Final salary payment generation is available only through the guarded action below and creates accounting posting."
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel border-error/30">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Guarded Salary Payment Generation</h3>
          <p class="garmetix-panel-subtitle">Creates SalaryPayment rows and accounting posting for generated payslips pending payment.</p>
        </div>
        <UBadge color="error" variant="subtle">{{ paymentPhrase }}</UBadge>
      </div>
      <div class="grid gap-3 xl:grid-cols-[160px_180px_minmax(0,1fr)_minmax(300px,0.7fr)_auto] xl:items-end">
        <UFormField label="Payment Mode">
          <USelect v-model="paymentForm.paymentMode" :items="paymentModeOptions" />
        </UFormField>
        <UFormField label="Payment Date">
          <UInput v-model="paymentForm.paymentDate" type="date" />
        </UFormField>
        <UFormField label="Audit notes">
          <UInput v-model="paymentForm.notes" placeholder="Approved by, bank/cash handoff, payroll reference" />
        </UFormField>
        <UFormField label="Type exact phrase">
          <UInput v-model="paymentConfirm" :placeholder="paymentPhrase" />
        </UFormField>
        <UButton icon="i-lucide-wallet-cards" color="error" :disabled="!canGeneratePayments" :loading="generatingPayments" @click="generatePayments">
          Generate Payments
        </UButton>
      </div>
      <UAlert v-if="paymentMessage" class="mt-3" :color="paymentTone" variant="subtle" :icon="paymentIcon" :description="paymentMessage" />
      <div v-if="paymentResult" class="mt-3 grid gap-3 md:grid-cols-4">
        <div v-for="item in paymentResultCards" :key="item.label" class="garmetix-row-card">
          <p class="text-xs text-muted">{{ item.label }}</p>
          <p class="mt-1 text-base font-semibold">{{ item.value }}</p>
        </div>
      </div>
    </div>

    <div v-if="preview" class="garmetix-section-card border-primary/30 bg-primary/5">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Calculated preview</p>
          <h3 class="text-lg font-semibold">{{ previewEmployee }}</h3>
        </div>
        <UBadge color="primary" variant="subtle">Not saved</UBadge>
      </div>
      <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
        <div v-for="item in previewCards" :key="item.label" class="garmetix-row-card bg-default/50">
          <p class="text-xs text-muted">{{ item.label }}</p>
          <p class="mt-1 text-base font-semibold">{{ item.value }}</p>
        </div>
      </div>
    </div>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Ready Candidates</h3>
            <p class="garmetix-panel-subtitle">{{ candidates.length }} candidate row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[820px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Net Preview</th>
                <th class="px-3 py-2">Status</th>
                <th class="px-3 py-2">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(candidate, index) in candidates" :key="readText(candidate, ['id', 'employeeId'], String(index))" class="border-t border-default">
                <td class="px-3 py-2">
                  <p class="font-medium">{{ readText(candidate, ['employeeName', 'employee']) }}</p>
                  <p class="text-xs text-muted">{{ readText(candidate, ['employeeCode', 'code']) }}</p>
                </td>
                <td class="px-3 py-2 font-semibold">{{ formatIndianMoney(readNumber(candidate, ['netPayPreview', 'payableAmount', 'netSalary'])) }}</td>
                <td class="px-3 py-2">{{ readText(candidate, ['paymentPostStatus', 'draftStatus', 'status']) }}</td>
                <td class="px-3 py-2">
                  <UButton
                    size="xs"
                    icon="i-lucide-calculator"
                    :loading="previewingId === readText(candidate, ['employeeId'], '')"
                    :disabled="!candidateEmployeeId(candidate)"
                    @click="previewCandidate(candidate)"
                  >
                    Preview
                  </UButton>
                </td>
              </tr>
              <tr v-if="!candidates.length">
                <td class="px-3 py-6 text-center text-muted" colspan="4">No salary payment candidates returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Existing Payments</h3>
            <p class="garmetix-panel-subtitle">{{ payments.length }} payment row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[760px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Voucher</th>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Month</th>
                <th class="px-3 py-2">Paid</th>
                <th class="px-3 py-2">Status</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(payment, index) in payments" :key="readText(payment, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(payment, ['voucherNumber', 'salaryPaymentNumber', 'number']) }}</td>
                <td class="px-3 py-2">{{ readText(payment, ['employeeName', 'employee']) }}</td>
                <td class="px-3 py-2">{{ readText(payment, ['monthYear', 'salaryMonth', 'month']) }}</td>
                <td class="px-3 py-2 font-semibold">{{ formatIndianMoney(readNumber(payment, ['paidAmount', 'amount'])) }}</td>
                <td class="px-3 py-2">{{ readText(payment, ['status']) }}</td>
              </tr>
              <tr v-if="!payments.length">
                <td class="px-3 py-6 text-center text-muted" colspan="5">No salary payments returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { currentYearMonth, readArray, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Salary Payments - Garmetix HR' })

const { get, post } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const previewingId = ref('')
const generatingPayments = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const payments = ref<ApiRecord[]>([])
const candidateSummary = ref<ApiRecord | null>(null)
const preview = ref<ApiRecord | null>(null)
const previewEmployee = ref('')
const paymentConfirm = ref('')
const paymentMessage = ref('')
const paymentTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const paymentResult = ref<ApiRecord | null>(null)
const paymentForm = reactive({
  paymentMode: 'Cash',
  paymentDate: toLocalDateInput(new Date()),
  notes: ''
})

const candidates = computed(() => readArray(candidateSummary.value, ['rows', 'Rows']))
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const paymentIcon = computed(() => paymentTone.value === 'success' ? 'i-lucide-circle-check' : paymentTone.value === 'warning' ? 'i-lucide-triangle-alert' : paymentTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const paymentModeOptions = ['Cash', 'UPI', 'Card', 'NEFT', 'IMPS', 'RTGS', 'Cheque', 'Others']
const cards = computed(() => {
  const paid = payments.value.reduce((total, item) => total + readNumber(item, ['paidAmount', 'amount']), 0)
  return [
    { label: 'Payments', value: payments.value.length },
    { label: 'Paid Amount', value: formatIndianMoney(paid) },
    { label: 'Ready To Pay', value: readNumber(candidateSummary.value, ['readyToPay', 'ReadyToPay']) },
    { label: 'Paid Drafts', value: readNumber(candidateSummary.value, ['paid', 'Paid']) },
    { label: 'Pending Amount', value: formatIndianMoney(readNumber(candidateSummary.value, ['totalPendingAmount', 'TotalPendingAmount'])) },
    { label: 'Salary Month', value: salaryMonth.value }
  ]
})
const salaryMonth = computed(() => (Number(year.value) * 100) + Number(month.value))
const paymentPhrase = computed(() => `GENERATE SALARY PAYMENTS ${salaryMonth.value}`)
const payableCandidates = computed(() => candidates.value.filter(candidate => readText(candidate, ['payrollPostStatus'], '').toLowerCase() === 'salaryslipgenerated' && readText(candidate, ['paymentPostStatus'], '').toLowerCase() !== 'salarypaymentgenerated' && readText(candidate, ['generatedSalaryPaySlipId'], '') !== '-'))
const canGeneratePayments = computed(() => payableCandidates.value.length > 0 && paymentConfirm.value.trim().toUpperCase() === paymentPhrase.value && paymentForm.notes.trim().length >= 8 && !generatingPayments.value)
const previewCards = computed(() => [
  { label: 'Gross', value: formatIndianMoney(readNumber(preview.value, ['grossSalary'])) },
  { label: 'Base Deduction', value: formatIndianMoney(readNumber(preview.value, ['baseDeductions'])) },
  { label: 'Advance', value: formatIndianMoney(readNumber(preview.value, ['salaryAdvance'])) },
  { label: 'Total Deduction', value: formatIndianMoney(readNumber(preview.value, ['totalDeductions'])) },
  { label: 'Previous Due', value: formatIndianMoney(readNumber(preview.value, ['previousDue'])) },
  { label: 'Net Payable', value: formatIndianMoney(readNumber(preview.value, ['netPayable'])) },
  { label: 'Outstanding', value: formatIndianMoney(readNumber(preview.value, ['outstandingAmount'])) },
  { label: 'Rounded Pay', value: formatIndianMoney(readNumber(preview.value, ['roundedPaidAmount'])) },
  { label: 'Round Off', value: formatIndianMoney(readNumber(preview.value, ['roundOff'])) },
  { label: 'Already Paid', value: formatIndianMoney(readNumber(preview.value, ['alreadyPaid'])) }
])
const paymentResultCards = computed(() => [
  { label: 'Selected Drafts', value: readNumber(paymentResult.value, ['selectedDrafts']) },
  { label: 'Created Payments', value: readNumber(paymentResult.value, ['createdPayments']) },
  { label: 'Total Amount', value: formatIndianMoney(readNumber(paymentResult.value, ['totalAmount'])) },
  { label: 'Payable Before', value: payableCandidates.value.length }
])

function candidateEmployeeId(candidate: ApiRecord) {
  return readText(candidate, ['employeeId', 'EmployeeId'], '')
}

function candidateSalaryPaySlipId(candidate: ApiRecord) {
  return readText(candidate, ['generatedSalaryPaySlipId', 'salaryPaySlipId', 'GeneratedSalaryPaySlipId', 'SalaryPaySlipId'], '')
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [paymentResponse, candidateResponse] = await Promise.all([
      get<ApiRecord[]>('api/salary-payments'),
      get<ApiRecord>('api/attendance/salary-payment-candidates', { year: year.value, month: month.value })
    ])
    payments.value = Array.isArray(paymentResponse) ? paymentResponse : []
    candidateSummary.value = candidateResponse
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load salary payment data.'
  } finally {
    loading.value = false
  }
}

async function previewCandidate(candidate: ApiRecord) {
  const employeeId = candidateEmployeeId(candidate)
  const salaryPaySlipId = candidateSalaryPaySlipId(candidate)
  if (!employeeId) {
    messageTone.value = 'warning'
    message.value = 'This candidate does not have an employee reference.'
    return
  }

  previewingId.value = employeeId
  message.value = ''
  try {
    preview.value = await post<ApiRecord>('api/salary-payments/preview', {
      employeeId,
      salaryMonth: salaryMonth.value,
      salaryPaySlipId: salaryPaySlipId || null,
      paymentId: null
    })
    previewEmployee.value = readText(candidate, ['employeeName', 'employee'])
    messageTone.value = 'success'
    message.value = 'Salary payment preview calculated. Nothing was saved.'
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to preview salary payment.'
  } finally {
    previewingId.value = ''
  }
}

async function generatePayments() {
  if (!canGeneratePayments.value) {
    paymentTone.value = 'warning'
    paymentMessage.value = `Type ${paymentPhrase.value} and enter audit notes before generating salary payments.`
    return
  }

  generatingPayments.value = true
  paymentMessage.value = ''
  paymentResult.value = null
  try {
    const response = await post<ApiRecord>('api/attendance/salary-payments/generate', {
      year: year.value,
      month: month.value,
      employeeId: null,
      confirm: true,
      paymentMode: paymentForm.paymentMode,
      paymentDate: paymentForm.paymentDate,
      notes: paymentForm.notes
    })
    paymentResult.value = response
    candidateSummary.value = response
    paymentTone.value = 'success'
    paymentMessage.value = 'Salary payments generated and accounting posting completed.'
    paymentConfirm.value = ''
    await load()
  } catch (caught) {
    paymentTone.value = 'error'
    paymentMessage.value = caught instanceof Error ? caught.message : 'Unable to generate salary payments.'
  } finally {
    generatingPayments.value = false
  }
}

onMounted(load)
</script>

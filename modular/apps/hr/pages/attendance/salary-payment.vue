<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="text-sm text-muted">Salary payment</p>
          <h2 class="mt-1 text-2xl font-semibold">Salary Payments</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
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
      description="This modular page calculates salary payment previews only. It does not create salary payment vouchers."
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div v-if="preview" class="border border-primary/30 bg-primary/5 p-4">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Calculated preview</p>
          <h3 class="text-lg font-semibold">{{ previewEmployee }}</h3>
        </div>
        <UBadge color="primary" variant="subtle">Not saved</UBadge>
      </div>
      <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
        <div v-for="item in previewCards" :key="item.label" class="border border-default bg-default/50 p-3">
          <p class="text-xs text-muted">{{ item.label }}</p>
          <p class="mt-1 text-base font-semibold">{{ item.value }}</p>
        </div>
      </div>
    </div>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="overflow-hidden border border-default bg-muted/10">
        <div class="border-b border-default p-4">
          <h3 class="text-base font-semibold">Ready Candidates</h3>
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
                    :disabled="!readText(candidate, ['generatedSalaryPaySlipId', 'salaryPaySlipId'], '')"
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

      <div class="overflow-hidden border border-default bg-muted/10">
        <div class="border-b border-default p-4">
          <h3 class="text-base font-semibold">Existing Payments</h3>
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
import { currentYearMonth, readArray, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Salary Payments - Garmetix HR' })

const { get, post } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const previewingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const payments = ref<ApiRecord[]>([])
const candidateSummary = ref<ApiRecord | null>(null)
const preview = ref<ApiRecord | null>(null)
const previewEmployee = ref('')

const candidates = computed(() => readArray(candidateSummary.value, ['rows', 'Rows']))
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
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
const previewCards = computed(() => [
  { label: 'Gross', value: formatIndianMoney(readNumber(preview.value, ['grossSalary'])) },
  { label: 'Base Deduction', value: formatIndianMoney(readNumber(preview.value, ['baseDeductions'])) },
  { label: 'Advance', value: formatIndianMoney(readNumber(preview.value, ['salaryAdvance'])) },
  { label: 'Previous Due', value: formatIndianMoney(readNumber(preview.value, ['previousDue'])) },
  { label: 'Outstanding', value: formatIndianMoney(readNumber(preview.value, ['outstandingAmount'])) },
  { label: 'Rounded Pay', value: formatIndianMoney(readNumber(preview.value, ['roundedPaidAmount'])) },
  { label: 'Round Off', value: formatIndianMoney(readNumber(preview.value, ['roundOff'])) },
  { label: 'Already Paid', value: formatIndianMoney(readNumber(preview.value, ['alreadyPaid'])) }
])

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
  const employeeId = readText(candidate, ['employeeId'], '')
  const salaryPaySlipId = readText(candidate, ['generatedSalaryPaySlipId', 'salaryPaySlipId'], '')
  if (!employeeId || !salaryPaySlipId) {
    messageTone.value = 'warning'
    message.value = 'This candidate does not have a generated salary slip yet.'
    return
  }

  previewingId.value = employeeId
  message.value = ''
  try {
    preview.value = await post<ApiRecord>('api/salary-payments/preview', {
      employeeId,
      salaryMonth: salaryMonth.value,
      salaryPaySlipId,
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

onMounted(load)
</script>

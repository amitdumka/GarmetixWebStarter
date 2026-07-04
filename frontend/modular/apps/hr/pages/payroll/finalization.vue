<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> Payroll month close</p>
          <h2 class="garmetix-dashboard-title">Payroll Finalization</h2>
          <p class="garmetix-dashboard-subtitle">
            Validate attendance, review, salary drafts, payslips, salary payments and month lock before closing payroll.
          </p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="load">
          <UFormField label="Year">
            <UInput v-model.number="year" type="number" min="2020" max="2100" />
          </UFormField>
          <UFormField label="Month">
            <UInput v-model.number="month" type="number" min="1" max="12" />
          </UFormField>
          <UButton type="submit" icon="i-lucide-refresh-cw" :loading="loading">Load</UButton>
          <UButton icon="i-lucide-file-down" color="primary" variant="soft" :disabled="!validation" @click="downloadValidationCsv">Export CSV</UButton>
        </form>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Month-end actions are guarded"
      description="Only use the action panel after attendance, regularization, photo proof, payroll review and salary draft checks are complete."
    />

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <div class="grid gap-3 xl:grid-cols-[minmax(0,1.1fr)_minmax(320px,0.7fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Finalization Checklist</h3>
            <p class="garmetix-panel-subtitle">{{ validationStatus }}</p>
          </div>
          <UBadge :color="validationComplete ? 'success' : 'warning'" variant="subtle">
            {{ validationComplete ? 'Complete' : 'Needs Action' }}
          </UBadge>
        </div>
        <div class="grid gap-2 md:grid-cols-2">
          <div v-for="check in checks" :key="readText(check, ['key', 'title'])" class="garmetix-row-card">
            <div class="flex items-start justify-between gap-3">
              <div>
                <p class="font-medium text-highlighted">{{ readText(check, ['title']) }}</p>
                <p class="mt-1 text-xs text-muted">{{ readText(check, ['detail']) }}</p>
              </div>
              <UBadge :color="readBoolean(check, ['ok']) ? 'success' : 'warning'" variant="subtle">
                {{ readText(check, ['status'], readBoolean(check, ['ok']) ? 'Pass' : 'Needs Action') }}
              </UBadge>
            </div>
          </div>
          <p v-if="!checks.length" class="text-sm text-muted">Load a month to view finalization checks.</p>
        </div>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Guarded Action Panel</h3>
        <p class="garmetix-panel-subtitle mt-1">Type <span class="font-mono">{{ confirmationPhrase }}</span> before posting final month-end changes.</p>
        <div class="mt-3 space-y-3">
          <UFormField label="Confirmation phrase">
            <UInput v-model="confirmationText" :placeholder="confirmationPhrase" />
          </UFormField>
          <UFormField label="Audit notes">
            <UInput v-model="notes" placeholder="Payroll checked by, owner approval, payment reference" />
          </UFormField>
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Payment mode">
              <USelect v-model="paymentMode" :items="paymentModeOptions" />
            </UFormField>
            <UFormField label="Payment date">
              <UInput v-model="paymentDate" type="date" />
            </UFormField>
          </div>
          <div class="grid gap-2 text-sm text-muted md:grid-cols-2">
            <UCheckbox v-model="postSalaryPayments" label="Post salary payments" />
            <UCheckbox v-model="lockMonthAfterFinalize" label="Lock month" />
          </div>
          <div class="grid gap-2">
            <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :disabled="!canRun" :loading="runningAction === 'recalculate'" @click="runRecalculate">Recalculate Attendance</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-list-checks" :disabled="!canRun" :loading="runningAction === 'review'" @click="runReviewRebuild">Rebuild Payroll Review</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-file-pen-line" :disabled="!canRun" :loading="runningAction === 'draft'" @click="runDraftRebuild">Rebuild Salary Drafts</UButton>
            <UButton color="warning" variant="soft" icon="i-lucide-file-check-2" :disabled="!canRun" :loading="runningAction === 'payslip'" @click="runPayslipGeneration">Generate Payslips</UButton>
            <UButton color="warning" variant="soft" icon="i-lucide-badge-indian-rupee" :disabled="!canRun || !postSalaryPayments" :loading="runningAction === 'payment'" @click="runSalaryPaymentGeneration">Post Salary Payments</UButton>
            <UButton color="primary" icon="i-lucide-shield-check" :disabled="!canRun" :loading="runningAction === 'finalize'" @click="runHardenedFinalization">Finalize Month</UButton>
          </div>
        </div>
      </div>
    </div>

    <div class="grid gap-3 xl:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Issues</h3>
            <p class="garmetix-panel-subtitle">{{ issues.length }} blocker/warning row(s).</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[720px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Severity</th>
                <th class="px-3 py-2">Code</th>
                <th class="px-3 py-2">Message</th>
                <th class="px-3 py-2">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(issue, index) in issues" :key="readText(issue, ['code'], String(index))" class="border-t border-default align-top">
                <td class="px-3 py-2">
                  <UBadge :color="readText(issue, ['severity']).toLowerCase() === 'blocker' ? 'error' : 'warning'" variant="subtle">
                    {{ readText(issue, ['severity']) }}
                  </UBadge>
                </td>
                <td class="px-3 py-2 font-mono text-xs">{{ readText(issue, ['code']) }}</td>
                <td class="px-3 py-2">{{ readText(issue, ['message']) }}</td>
                <td class="px-3 py-2">{{ readText(issue, ['action']) }}</td>
              </tr>
              <tr v-if="!issues.length">
                <td class="px-3 py-6 text-center text-muted" colspan="4">No validation issues returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Employee Evidence</h3>
            <p class="garmetix-panel-subtitle">{{ employeeRows.length }} employee validation row(s).</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[920px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Payable</th>
                <th class="px-3 py-2">Review</th>
                <th class="px-3 py-2">Draft</th>
                <th class="px-3 py-2">Payslip</th>
                <th class="px-3 py-2">Payment</th>
                <th class="px-3 py-2">Outstanding</th>
                <th class="px-3 py-2">Status</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in employeeRows" :key="readText(row, ['employeeId'], String(index))" class="border-t border-default align-top">
                <td class="px-3 py-2">
                  <p class="font-medium">{{ readText(row, ['employeeName']) }}</p>
                  <p class="text-xs text-muted">{{ readText(row, ['employeeCode']) }}</p>
                </td>
                <td class="px-3 py-2">{{ readNumber(row, ['payableDays']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['reviewStatus']) }}</td>
                <td class="px-3 py-2">{{ readText(row, ['draftStatus']) }}</td>
                <td class="px-3 py-2">{{ readBoolean(row, ['hasPayslip']) ? 'Yes' : 'No' }}</td>
                <td class="px-3 py-2">{{ readBoolean(row, ['hasSalaryPayment']) ? 'Yes' : 'No' }}</td>
                <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['outstandingAmount'])) }}</td>
                <td class="px-3 py-2">
                  <UBadge :color="readText(row, ['status']).toLowerCase().includes('complete') ? 'success' : 'warning'" variant="subtle">
                    {{ readText(row, ['status']) }}
                  </UBadge>
                </td>
              </tr>
              <tr v-if="!employeeRows.length">
                <td class="px-3 py-6 text-center text-muted" colspan="8">No employee validation rows returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { currentYearMonth, readArray, readBoolean, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Payroll Finalization - Garmetix HR' })

const { get, post, downloadFile } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const validation = ref<ApiRecord | null>(null)
const confirmationText = ref('')
const notes = ref('')
const paymentMode = ref('Cash')
const paymentDate = ref(toLocalDateInput())
const postSalaryPayments = ref(true)
const lockMonthAfterFinalize = ref(true)
const runningAction = ref('')
const paymentModeOptions = ['Cash', 'Card', 'UPI', 'NEFT', 'Cheque']

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const confirmationPhrase = computed(() => `FINALIZE ${year.value}${String(month.value).padStart(2, '0')}`)
const canRun = computed(() => confirmationText.value.trim().toUpperCase() === confirmationPhrase.value && notes.value.trim().length >= 8 && !runningAction.value)
const counts = computed(() => (validation.value?.counts || validation.value?.Counts || {}) as ApiRecord)
const money = computed(() => (validation.value?.money || validation.value?.Money || {}) as ApiRecord)
const checks = computed(() => readArray(validation.value, ['checks', 'Checks']))
const issues = computed(() => readArray(validation.value, ['issues', 'Issues']))
const employeeRows = computed(() => readArray(validation.value, ['employees', 'Employees']))
const validationComplete = computed(() => readBoolean(validation.value, ['complete', 'Complete']))
const validationStatus = computed(() => readText(validation.value, ['status', 'Status'], 'Load validation to see payroll month status.'))
const cards = computed(() => [
  { label: 'Employees', value: readNumber(counts.value, ['activeEmployees', 'ActiveEmployees']) },
  { label: 'Monthly Rows', value: readNumber(counts.value, ['monthlySummaryRows', 'MonthlySummaryRows']) },
  { label: 'Review Approved', value: readNumber(counts.value, ['approvedReviewRows', 'ApprovedReviewRows']) },
  { label: 'Ready Drafts', value: readNumber(counts.value, ['readySalaryDraftRows', 'ReadySalaryDraftRows']) },
  { label: 'Payslips', value: readNumber(counts.value, ['payslips', 'Payslips']) },
  { label: 'Outstanding', value: formatIndianMoney(readNumber(money.value, ['outstanding', 'Outstanding'])) }
])

function resetGate() {
  confirmationText.value = ''
  runningAction.value = ''
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    validation.value = await get<ApiRecord>('api/payroll/real-month-validation', { year: year.value, month: month.value })
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load payroll validation.'
  } finally {
    loading.value = false
  }
}

async function runAction(name: string, action: () => Promise<ApiRecord | null | void>, success: string) {
  if (!canRun.value) {
    messageTone.value = 'warning'
    message.value = `Type ${confirmationPhrase.value} and enter audit notes before running payroll finalization actions.`
    return
  }

  runningAction.value = name
  message.value = ''
  try {
    await action()
    messageTone.value = 'success'
    message.value = success
    resetGate()
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : `Unable to run ${name}.`
    runningAction.value = ''
  }
}

async function runRecalculate() {
  await runAction('recalculate', () => post<ApiRecord>('api/attendance/recalculate', { year: year.value, month: month.value, notes: notes.value }), 'Monthly attendance recalculated.')
}

async function runReviewRebuild() {
  await runAction('review', () => post<ApiRecord>('api/attendance/payroll-review/rebuild', { year: year.value, month: month.value, notes: notes.value }), 'Payroll review rebuilt.')
}

async function runDraftRebuild() {
  await runAction('draft', () => post<ApiRecord>('api/attendance/salary-slip-drafts/rebuild', { year: year.value, month: month.value, notes: notes.value }), 'Salary drafts rebuilt.')
}

async function runPayslipGeneration() {
  await runAction('payslip', () => post<ApiRecord>('api/attendance/salary-slip-drafts/generate-payslips', { year: year.value, month: month.value, confirm: true, notes: notes.value }), 'Payslips generated from ready drafts.')
}

async function runSalaryPaymentGeneration() {
  await runAction('payment', () => post<ApiRecord>('api/attendance/salary-payments/generate', { year: year.value, month: month.value, confirm: true, paymentMode: paymentMode.value, paymentDate: paymentDate.value, notes: notes.value }), 'Salary payments posted from generated payslips.')
}

async function runHardenedFinalization() {
  await runAction('finalize', () => post<ApiRecord>('api/payroll/finalization/finalize-month', {
    year: year.value,
    month: month.value,
    confirm: true,
    postSalaryPayments: postSalaryPayments.value,
    lockMonth: lockMonthAfterFinalize.value,
    paymentMode: paymentMode.value,
    paymentDate: paymentDate.value,
    notes: notes.value
  }), 'Payroll finalized through hardened backend workflow.')
}

async function downloadValidationCsv() {
  try {
    await downloadFile('api/payroll/real-month-validation.csv', `payroll-validation-${year.value}-${String(month.value).padStart(2, '0')}.csv`, { year: year.value, month: month.value })
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to download payroll validation CSV.'
  }
}

onMounted(load)
</script>

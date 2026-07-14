<script setup lang="ts">
const reports = useAttendanceReports()
const api = useGarmetixApi()
const config = useRuntimeConfig()
const feedback = useUiFeedback()

const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const loading = ref(false)
const actionLoading = ref('')
const paymentMode = ref(0)
const postSalaryPayments = ref(true)
const lockAfterFinalization = ref(true)
const overtimeRateMultiplier = ref(1)
const latePenaltyPerDay = ref(0)
const finalizationNote = ref('')
const hardenedResult = ref<any | null>(null)
const summary = ref<any | null>(null)
const review = ref<any | null>(null)
const draft = ref<any | null>(null)
const payments = ref<any | null>(null)
const payslips = ref<any[]>([])
const realMonthValidation = ref<any | null>(null)
const validationExporting = ref(false)

const reviewRows = computed(() => review.value?.rows || [])
const draftRows = computed(() => draft.value?.rows || [])
const paymentRows = computed(() => payments.value?.rows || [])
const approvedReviewRows = computed(() => reviewRows.value.filter((row: any) => row.reviewStatus === 'ApprovedForPayroll' || row.reviewStatus === 'Reviewed'))
const readyDraftRows = computed(() => draftRows.value.filter((row: any) => row.draftStatus === 'ReadyForPayroll'))
const generatedDraftRows = computed(() => draftRows.value.filter((row: any) => row.payrollPostStatus === 'SalarySlipGenerated'))
const pendingPaymentRows = computed(() => paymentRows.value.filter((row: any) => row.generatedSalaryPaySlipId && row.paymentPostStatus !== 'SalaryPaymentGenerated'))
const paidPaymentRows = computed(() => paymentRows.value.filter((row: any) => row.paymentPostStatus === 'SalaryPaymentGenerated'))
const validationChecks = computed(() => realMonthValidation.value?.checks || [])
const validationIssues = computed(() => realMonthValidation.value?.issues || [])
const validationRows = computed(() => realMonthValidation.value?.employees || [])

const steps = computed(() => [
  {
    key: 'attendance',
    title: '1. Monthly attendance recalculated',
    status: summary.value?.employees ? 'Ready' : 'Pending',
    detail: `${summary.value?.employees || 0} employee summaries, ${summary.value?.presentDays || 0} present days, ${summary.value?.halfDays || 0} half-days`,
    ok: Boolean(summary.value?.employees)
  },
  {
    key: 'review',
    title: '2. Payroll review approved',
    status: approvedReviewRows.value.length ? 'Ready' : 'Pending',
    detail: `${approvedReviewRows.value.length}/${reviewRows.value.length} review rows approved/reviewed`,
    ok: reviewRows.value.length > 0 && approvedReviewRows.value.length === reviewRows.value.length
  },
  {
    key: 'drafts',
    title: '3. Salary drafts ready',
    status: readyDraftRows.value.length ? 'Ready' : 'Pending',
    detail: `${readyDraftRows.value.length}/${draftRows.value.length} draft rows ready`,
    ok: draftRows.value.length > 0 && readyDraftRows.value.length === draftRows.value.length
  },
  {
    key: 'payslips',
    title: '4. Payslips generated',
    status: generatedDraftRows.value.length ? 'Generated' : 'Pending',
    detail: `${generatedDraftRows.value.length}/${draftRows.value.length} drafts converted to payslips`,
    ok: draftRows.value.length > 0 && generatedDraftRows.value.length === draftRows.value.length
  },
  {
    key: 'payments',
    title: '5. Salary payments posted',
    status: paidPaymentRows.value.length ? 'Posted' : 'Pending',
    detail: `${paidPaymentRows.value.length} paid, ${pendingPaymentRows.value.length} pending`,
    ok: paymentRows.value.length > 0 && pendingPaymentRows.value.length === 0
  },
  {
    key: 'lock',
    title: '6. Month locked',
    status: summary.value?.locked ? 'Locked' : 'Unlocked',
    detail: summary.value?.locked ? 'Attendance month is locked for payroll.' : 'Lock after payroll payment is verified.',
    ok: Boolean(summary.value?.locked)
  }
])

const allDone = computed(() => steps.value.every((step) => step.ok))

async function refresh() {
  loading.value = true
  try {
    const [summaryResult, reviewResult, draftResult, paymentResult, payslipResult, validationResult] = await Promise.all([
      reports.payrollSummary(year.value, month.value),
      reports.payrollReview(year.value, month.value),
      reports.salarySlipDrafts(year.value, month.value),
      reports.salaryPaymentCandidates(year.value, month.value),
      api.get<any[]>('payroll/payslips/recent?take=500'),
      api.get<any>(`payroll/real-month-validation?year=${year.value}&month=${month.value}`)
    ])
    summary.value = summaryResult
    review.value = reviewResult
    draft.value = draftResult
    payments.value = paymentResult
    realMonthValidation.value = validationResult
    payslips.value = payslipResult.filter((row: any) => {
      const date = new Date(row.payPeriodStart)
      return date.getFullYear() === year.value && date.getMonth() + 1 === month.value
    })
  } catch (error: any) {
    feedback.fromError('Payroll finalization refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function runAction(name: string, handler: () => Promise<void>) {
  actionLoading.value = name
  try {
    await handler()
    await refresh()
  } catch (error: any) {
    feedback.fromError(`Payroll action failed: ${name}`, error)
  } finally {
    actionLoading.value = ''
  }
}

function confirmAction(message: string) {
  return window.confirm(message)
}

async function recalculateAttendance() {
  await runAction('attendance', async () => {
    await reports.recalculate({ year: year.value, month: month.value })
    feedback.success('Attendance recalculated', 'Monthly attendance summary was rebuilt for payroll.')
  })
}

async function rebuildReview() {
  await runAction('review', async () => {
    await reports.rebuildPayrollReview({ year: year.value, month: month.value })
    feedback.success('Payroll review rebuilt', 'Payroll review rows were refreshed from monthly attendance.')
  })
}

async function approveAllReviewRows() {
  if (!reviewRows.value.length) {
    feedback.warning('No review rows', 'Rebuild payroll review first.')
    return
  }
  if (!confirmAction(`Approve ${reviewRows.value.length} payroll review row(s) for ${month.value}/${year.value}?`)) return
  await runAction('approve', async () => {
    for (const row of reviewRows.value) {
      if (row.reviewStatus !== 'ApprovedForPayroll') {
        await reports.markPayrollReview(row.id, {
          reviewStatus: 'ApprovedForPayroll',
          notes: finalizationNote.value || 'Approved from Payroll Finalization wizard.'
        })
      }
    }
    feedback.success('Payroll review approved', 'All visible payroll review rows were approved for payroll.')
  })
}

async function rebuildSalaryDrafts() {
  await runAction('drafts', async () => {
    await reports.rebuildSalarySlipDrafts({ year: year.value, month: month.value })
    feedback.success('Salary drafts rebuilt', 'Salary draft rows were rebuilt from approved attendance review.')
  })
}

async function markAllDraftsReady() {
  if (!draftRows.value.length) {
    feedback.warning('No salary drafts', 'Rebuild salary drafts first.')
    return
  }
  if (!confirmAction(`Mark ${draftRows.value.length} salary draft row(s) ReadyForPayroll?`)) return
  await runAction('ready', async () => {
    for (const row of draftRows.value) {
      if (row.payrollPostStatus !== 'SalarySlipGenerated' && row.draftStatus !== 'ReadyForPayroll') {
        await reports.markSalarySlipDraft(row.id, {
          draftStatus: 'ReadyForPayroll',
          notes: finalizationNote.value || 'Marked ready from Payroll Finalization wizard.'
        })
      }
    }
    feedback.success('Salary drafts ready', 'All open salary drafts were marked ready for payroll.')
  })
}

async function generatePayslips() {
  if (!readyDraftRows.value.length) {
    feedback.warning('No ready drafts', 'Mark salary drafts ReadyForPayroll first.')
    return
  }
  if (!confirmAction(`Generate final salary slips for ${readyDraftRows.value.length} ready row(s)?`)) return
  await runAction('payslips', async () => {
    await reports.generateSalarySlipsFromDrafts({
      year: year.value,
      month: month.value,
      confirm: true,
      notes: finalizationNote.value || 'Generated from Payroll Finalization wizard.'
    })
    feedback.success('Payslips generated', 'Final salary slips were generated from ready drafts.')
  })
}

async function generatePayments() {
  if (!pendingPaymentRows.value.length) {
    feedback.warning('No payment rows pending', 'Generate salary slips first or refresh candidates.')
    return
  }
  if (!confirmAction(`Post salary payments for ${pendingPaymentRows.value.length} payslip(s)? Accounting entries will be created.`)) return
  await runAction('payments', async () => {
    await reports.generateSalaryPaymentsFromDrafts({
      year: year.value,
      month: month.value,
      confirm: true,
      paymentMode: paymentMode.value,
      paymentDate: new Date().toISOString(),
      notes: finalizationNote.value || 'Generated from Payroll Finalization wizard.'
    })
    feedback.success('Salary payments posted', 'Salary payments and accounting postings were generated.')
  })
}

async function lockMonth() {
  if (!allDone.value && !confirmAction('Some payroll finalization checks are not complete. Lock attendance month anyway?')) return
  await runAction('lock', async () => {
    await reports.lockMonth({ year: year.value, month: month.value, locked: true })
    feedback.success('Payroll month locked', 'Attendance month is now locked after payroll finalization.')
  })
}

async function finalizePayrollHardened() {
  const paymentText = postSalaryPayments.value ? 'post salary payment vouchers and accounting entries' : 'generate payslips only without payment posting'
  const lockText = lockAfterFinalization.value ? 'lock the attendance/payroll month' : 'leave the month unlocked'
  if (!confirmAction(`Run Stage 11D-40 hardened payroll finalization for ${month.value}/${year.value}? This will ${paymentText} and ${lockText}.`)) return
  await runAction('finalize', async () => {
    hardenedResult.value = await api.create('payroll/finalization/finalize-month', {
      year: year.value,
      month: month.value,
      confirm: true,
      postSalaryPayments: postSalaryPayments.value,
      lockMonth: lockAfterFinalization.value,
      paymentMode: paymentMode.value,
      paymentDate: new Date().toISOString(),
      overtimeRateMultiplier: Number(overtimeRateMultiplier.value || 0),
      latePenaltyPerDay: Number(latePenaltyPerDay.value || 0),
      notes: finalizationNote.value || 'Stage 11D-40 hardened finalization from Payroll Finalization page.'
    })
    feedback.success('Payroll finalized safely', `Employees finalized: ${hardenedResult.value?.employeesFinalized || 0}`)
  })
}

async function exportRealMonthValidationCsv() {
  validationExporting.value = true
  try {
    const url = `${config.public.apiBase}/payroll/real-month-validation.csv?year=${year.value}&month=${month.value}`
    const response = await fetch(url, { headers: api.authHeaders() as HeadersInit })
    if (!response.ok) {
      throw new Error(await response.text())
    }
    const blob = await response.blob()
    const href = window.URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = href
    link.download = `payroll-real-month-validation-${year.value}-${String(month.value).padStart(2, '0')}.csv`
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(href)
    feedback.success('Validation CSV exported', 'Payroll real-month validation evidence CSV was downloaded.')
  } catch (error: any) {
    feedback.fromError('Could not export payroll validation CSV', error)
  } finally {
    validationExporting.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Payroll Finalization" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Payroll Finalization"
        description="Complete payroll in order: attendance summary, approval, salary draft, payslip, salary payment and monthly lock."
        icon="i-lucide-clipboard-check"
        :loading="loading"
      >
        <template #actions>
          <UInput v-model="year" type="number" class="w-28" />
          <UInput v-model="month" type="number" class="w-24" />
          <UButton label="Refresh" variant="subtle" :loading="loading" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="primary"
        icon="i-lucide-info"
        title="Payroll finalization order"
        description="Use this page after attendance import/shift rules are verified. Stage 11D-40 adds one hardened backend finalization action that runs review, draft, payslip, optional payment posting, adjustment recovery and month lock in one database transaction."
      />


      <UCard v-if="realMonthValidation">
        <template #header>
          <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h3 class="font-semibold">Attendance/Payroll real-month validation</h3>
              <p class="text-sm text-muted">Final QA status for {{ realMonthValidation.monthYear }} before payroll sign-off.</p>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              <UBadge :color="realMonthValidation.complete ? 'success' : 'warning'" variant="subtle">{{ realMonthValidation.status }}</UBadge>
              <UButton size="sm" icon="i-lucide-download" label="Export CSV" variant="soft" :loading="validationExporting" @click="exportRealMonthValidationCsv" />
            </div>
          </div>
        </template>

        <div class="grid gap-3 md:grid-cols-4 xl:grid-cols-8">
          <UCard><p class="text-xs text-muted">Active employees</p><strong>{{ realMonthValidation.counts.activeEmployees }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Monthly rows</p><strong>{{ realMonthValidation.counts.monthlySummaryRows }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Approved review</p><strong>{{ realMonthValidation.counts.approvedReviewRows }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Ready drafts</p><strong>{{ realMonthValidation.counts.readySalaryDraftRows }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Payslips</p><strong>{{ realMonthValidation.counts.payslips }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Payments</p><strong>{{ realMonthValidation.counts.salaryPayments }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Net salary</p><strong>₹{{ realMonthValidation.money.totalNet }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Outstanding</p><strong>₹{{ realMonthValidation.money.outstanding }}</strong></UCard>
        </div>

        <div class="mt-4 grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
          <div class="space-y-3">
            <h4 class="text-sm font-semibold">Closeout checks</h4>
            <div class="grid gap-2 md:grid-cols-2">
              <div v-for="check in validationChecks" :key="check.key" class="rounded border border-default p-3">
                <div class="flex items-center justify-between gap-2">
                  <p class="font-medium">{{ check.title }}</p>
                  <UBadge :color="check.ok ? 'success' : 'warning'" variant="subtle">{{ check.status }}</UBadge>
                </div>
                <p class="mt-1 text-xs text-muted">{{ check.detail }}</p>
              </div>
            </div>
          </div>
          <div class="space-y-3">
            <h4 class="text-sm font-semibold">Blocking / warning issues</h4>
            <div v-if="validationIssues.length" class="space-y-2">
              <UAlert
                v-for="issue in validationIssues"
                :key="issue.code"
                :color="issue.severity === 'Blocker' ? 'error' : 'warning'"
                :title="`${issue.code} (${issue.count})`"
                :description="`${issue.message} ${issue.action}`"
              />
            </div>
            <UAlert v-else color="success" title="No validation issue" description="This month has the evidence needed for payroll closure." />
          </div>
        </div>

        <div class="mt-4 overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead class="text-left text-xs uppercase text-muted">
              <tr>
                <th class="p-2">Employee</th>
                <th class="p-2">Status</th>
                <th class="p-2">Payable</th>
                <th class="p-2">Review</th>
                <th class="p-2">Draft</th>
                <th class="p-2">Payslip</th>
                <th class="p-2">Payment</th>
                <th class="p-2">Locked</th>
                <th class="p-2">Outstanding</th>
                <th class="p-2">Issues</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in validationRows.slice(0, 50)" :key="row.employeeId" class="border-t border-default">
                <td class="p-2">
                  <div class="font-medium">{{ row.employeeName }}</div>
                  <div class="text-xs text-muted">{{ row.employeeCode || row.employeeId }}</div>
                </td>
                <td class="p-2"><UBadge :color="row.status === 'Complete' ? 'success' : 'warning'" variant="subtle">{{ row.status }}</UBadge></td>
                <td class="p-2">{{ row.payableDays }}</td>
                <td class="p-2">{{ row.reviewStatus }}</td>
                <td class="p-2">{{ row.draftStatus }}</td>
                <td class="p-2">{{ row.hasPayslip ? 'Yes' : 'No' }}</td>
                <td class="p-2">{{ row.hasSalaryPayment ? 'Yes' : 'No' }}</td>
                <td class="p-2">{{ row.locked ? 'Yes' : 'No' }}</td>
                <td class="p-2">₹{{ row.outstandingAmount }}</td>
                <td class="p-2 text-xs text-muted">{{ row.issues?.join('; ') || '—' }}</td>
              </tr>
              <tr v-if="!validationRows.length">
                <td colspan="10" class="p-6 text-center text-muted">No employee validation rows found for this month.</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="mt-4 grid gap-4 md:grid-cols-3">
          <div>
            <h4 class="text-sm font-semibold">Final closeout checklist</h4>
            <ul class="mt-2 list-disc space-y-1 pl-5 text-xs text-muted">
              <li v-for="item in realMonthValidation.closeoutChecklist" :key="item">{{ item }}</li>
            </ul>
          </div>
          <div>
            <h4 class="text-sm font-semibold">Known limitations</h4>
            <ul class="mt-2 list-disc space-y-1 pl-5 text-xs text-muted">
              <li v-for="item in realMonthValidation.knownLimitations" :key="item">{{ item }}</li>
            </ul>
          </div>
          <div>
            <h4 class="text-sm font-semibold">Next outside-module candidates</h4>
            <ul class="mt-2 list-disc space-y-1 pl-5 text-xs text-muted">
              <li v-for="item in realMonthValidation.nextModuleCandidates" :key="item">{{ item }}</li>
            </ul>
          </div>
        </div>
      </UCard>

      <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
        <UCard v-for="step in steps" :key="step.key">
          <div class="space-y-2">
            <UBadge :color="step.ok ? 'success' : 'warning'" :label="step.status" />
            <h3 class="font-semibold">{{ step.title }}</h3>
            <p class="text-sm text-muted">{{ step.detail }}</p>
          </div>
        </UCard>
      </div>

      <UCard>
        <div class="grid gap-3 md:grid-cols-4 md:items-end">
          <UFormField label="Payment mode">
            <select v-model.number="paymentMode" class="w-full rounded border bg-transparent px-3 py-2 text-sm">
              <option :value="0">Cash</option>
              <option :value="2">UPI</option>
              <option :value="6">NEFT</option>
              <option :value="5">RTGS</option>
              <option :value="7">Cheque</option>
              <option :value="15">Credit Balance</option>
            </select>
          </UFormField>
          <UFormField label="Finalization note" class="md:col-span-2">
            <UInput v-model="finalizationNote" placeholder="Optional note saved on review/draft/payment actions" />
          </UFormField>
          <UBadge :color="allDone ? 'success' : 'warning'" variant="subtle">
            {{ allDone ? 'Payroll complete' : 'Payroll pending' }}
          </UBadge>
        </div>
      </UCard>

      <UCard>
        <div class="grid gap-3 md:grid-cols-[1fr_auto] md:items-center">
          <div>
            <h3 class="font-semibold">Stage 11D-40 hardened one-click finalization</h3>
            <p class="text-sm text-muted">Runs review approval, salary draft preparation, payslip generation, optional salary payment posting, recovered advance update and month lock in one backend transaction.</p>
          </div>
          <UButton icon="i-lucide-shield-check" color="success" label="Finalize Payroll Safely" :loading="actionLoading === 'finalize'" @click="finalizePayrollHardened" />
        </div>
      </UCard>

      <UCard v-if="hardenedResult">
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <h3 class="font-semibold">Last hardened finalization result</h3>
            <UBadge color="success" variant="subtle">{{ hardenedResult.employeesFinalized }} employees</UBadge>
          </div>
        </template>
        <div class="grid gap-3 md:grid-cols-5">
          <UCard><p class="text-xs text-muted">Payslips</p><strong>{{ hardenedResult.payslipsCreated }} created / {{ hardenedResult.payslipsUpdated }} updated</strong></UCard>
          <UCard><p class="text-xs text-muted">Payments</p><strong>{{ hardenedResult.salaryPaymentsCreated }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Locked rows</p><strong>{{ hardenedResult.lockedRows }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Net salary</p><strong>₹{{ hardenedResult.totalNet }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Paid</p><strong>₹{{ hardenedResult.totalPaid }}</strong></UCard>
        </div>
        <ul class="mt-3 list-disc space-y-1 pl-5 text-sm text-muted">
          <li v-for="log in hardenedResult.stepLogs" :key="log">{{ log }}</li>
        </ul>
      </UCard>

      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <UButton block icon="i-lucide-refresh-cw" label="1 Recalculate Attendance" :loading="actionLoading === 'attendance'" @click="recalculateAttendance" />
        <UButton block icon="i-lucide-list-checks" label="2 Rebuild Review" :loading="actionLoading === 'review'" @click="rebuildReview" />
        <UButton block icon="i-lucide-check-check" label="3 Approve Review Rows" :loading="actionLoading === 'approve'" @click="approveAllReviewRows" />
        <UButton block icon="i-lucide-receipt" label="4 Rebuild Salary Drafts" :loading="actionLoading === 'drafts'" @click="rebuildSalaryDrafts" />
        <UButton block icon="i-lucide-badge-check" label="5 Mark Drafts Ready" :loading="actionLoading === 'ready'" @click="markAllDraftsReady" />
        <UButton block icon="i-lucide-file-text" label="6 Generate Payslips" :loading="actionLoading === 'payslips'" @click="generatePayslips" />
        <UButton block icon="i-lucide-wallet-cards" color="success" label="7 Post Salary Payments" :loading="actionLoading === 'payments'" @click="generatePayments" />
        <UButton block icon="i-lucide-lock" color="warning" label="8 Lock Month" :loading="actionLoading === 'lock'" @click="lockMonth" />
      </div>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h3 class="font-semibold">Payroll status for {{ month }}/{{ year }}</h3>
              <p class="text-sm text-muted">Review row counts before posting salary payments.</p>
            </div>
            <UBadge color="neutral" variant="subtle">Payslips {{ payslips.length }}</UBadge>
          </div>
        </template>
        <div class="grid gap-3 md:grid-cols-5">
          <UCard><p class="text-xs text-muted">Attendance Employees</p><strong>{{ summary?.employees || 0 }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Review Rows</p><strong>{{ reviewRows.length }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Draft Rows</p><strong>{{ draftRows.length }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Generated Slips</p><strong>{{ generatedDraftRows.length }}</strong></UCard>
          <UCard><p class="text-xs text-muted">Pending Payments</p><strong>{{ pendingPaymentRows.length }}</strong></UCard>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

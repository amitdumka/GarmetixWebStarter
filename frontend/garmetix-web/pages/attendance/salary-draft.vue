<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const markingId = ref('')
const generating = ref(false)
const generateNotes = ref('')
const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const draft = ref<any | null>(null)

const rows = computed(() => draft.value?.rows || [])
const readyRows = computed(() => rows.value.filter((row: any) => row.draftStatus === 'ReadyForPayroll' && row.payrollPostStatus !== 'SalarySlipGenerated'))
const generatedRows = computed(() => rows.value.filter((row: any) => row.payrollPostStatus === 'SalarySlipGenerated'))

async function refresh() {
  loading.value = true
  try {
    draft.value = await reports.salarySlipDrafts(year.value, month.value)
  } catch (error: any) {
    feedback.fromError('Attendance salary draft refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function rebuild() {
  loading.value = true
  try {
    draft.value = await reports.rebuildSalarySlipDrafts({ year: year.value, month: month.value })
    feedback.success('Salary draft preview rebuilt', 'Draft rows were refreshed from reviewed attendance payroll rows. Payroll was not posted.')
  } catch (error: any) {
    feedback.fromError('Could not rebuild salary draft preview', error)
  } finally {
    loading.value = false
  }
}

async function mark(row: any, status: string) {
  if (!row?.id) {
    feedback.warning('Preview row not saved', 'Click Rebuild Drafts first, then mark a row ready or on hold.')
    return
  }
  markingId.value = row.id
  try {
    await reports.markSalarySlipDraft(row.id, { draftStatus: status, notes: row.notes || null })
    await refresh()
    feedback.success('Salary draft updated', `${row.employeeName} marked ${status}.`)
  } catch (error: any) {
    feedback.fromError('Could not update salary draft', error)
  } finally {
    markingId.value = ''
  }
}


async function generateConfirmedPayslips() {
  if (!readyRows.value.length) {
    feedback.warning('No ready drafts', 'Mark one or more rows Ready before generating final salary slips.')
    return
  }
  const ok = window.confirm(`Generate final salary slips for ${readyRows.value.length} ReadyForPayroll attendance draft row(s)? This will not create salary payments or accounting vouchers.`)
  if (!ok) return
  generating.value = true
  try {
    const result = await reports.generateSalarySlipsFromDrafts({ year: year.value, month: month.value, confirm: true, notes: generateNotes.value || null })
    draft.value = { ...draft.value, rows: result.rows }
    feedback.success('Salary slips generated', `${result.createdPayslips} created and ${result.updatedPayslips} updated. Payments and vouchers were not posted.`)
  } catch (error: any) {
    feedback.fromError('Could not generate salary slips', error)
  } finally {
    generating.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Attendance Salary Draft" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Attendance Salary Slip Draft & Confirmed Generation"
        description="Stage 9F creates final salary slip records only after explicit confirmation from ReadyForPayroll attendance drafts. Salary payment and accounting voucher posting stay separate."
        icon="i-lucide-receipt-indian-rupee"
        :loading="loading"
      >
        <template #actions>
          <UInput v-model="year" type="number" class="w-28" />
          <UInput v-model="month" type="number" class="w-24" />
          <UButton label="Refresh" variant="subtle" :loading="loading" @click="refresh" />
          <UButton label="Rebuild Drafts" color="primary" :loading="loading" @click="rebuild" />
          <UButton label="Generate Salary Slips" color="success" :loading="generating" :disabled="!readyRows.length" @click="generateConfirmedPayslips" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        icon="i-lucide-shield-alert"
        title="Confirmed generation available"
        description="This page can now generate final SalaryPaySlip records after confirmation. Salary payments and accounting vouchers are still not created here."
      />

      <div class="grid gap-3 md:grid-cols-6">
        <UCard><p class="text-xs text-muted">Employees</p><strong>{{ draft?.employees || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Ready</p><strong>{{ readyRows.length }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Generated</p><strong>{{ generatedRows.length }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Gross Preview</p><strong>₹{{ Number(draft?.totalGrossPreview || 0).toFixed(2) }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Deduction Preview</p><strong>₹{{ Number(draft?.totalDeductionPreview || 0).toFixed(2) }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Net Preview</p><strong>₹{{ Number(draft?.totalNetPayPreview || 0).toFixed(2) }}</strong></UCard>
      </div>

      <UCard>
        <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div>
            <h3 class="font-semibold">Final salary slip confirmation</h3>
            <p class="text-sm text-muted">Only rows marked ReadyForPayroll and not already generated will be converted to final salary slips.</p>
          </div>
          <UInput v-model="generateNotes" placeholder="Optional generation note" class="md:w-80" />
        </div>
      </UCard>

      <UCard>
        <div class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b text-left text-muted">
                <th class="p-2">Employee</th>
                <th class="p-2">Payable</th>
                <th class="p-2">Deduction Days</th>
                <th class="p-2">Gross</th>
                <th class="p-2">Benefits</th>
                <th class="p-2">Deductions</th>
                <th class="p-2">Net Preview</th>
                <th class="p-2">Status</th>
                <th class="p-2">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.employeeId" class="border-b align-top">
                <td class="p-2"><strong>{{ row.employeeName }}</strong><p class="text-xs text-muted">{{ row.employeeCode }}</p></td>
                <td class="p-2">{{ row.payableDays }}</td>
                <td class="p-2">{{ row.deductionDays }}</td>
                <td class="p-2">₹{{ Number(row.attendanceGrossPreview || 0).toFixed(2) }}</td>
                <td class="p-2">₹{{ Number((row.bonusPreview || 0) + (row.leaveEncashmentPreview || 0)).toFixed(2) }}</td>
                <td class="p-2">₹{{ Number((row.salaryAdvanceRecoveryPreview || 0) + (row.pfEmployeePreview || 0) + (row.gratuityPreview || 0) + (row.otherDeductionPreview || 0)).toFixed(2) }}</td>
                <td class="p-2 font-semibold">₹{{ Number(row.netPayPreview || 0).toFixed(2) }}</td>
                <td class="p-2 space-y-1">
                  <UBadge :label="row.draftStatus" />
                  <UBadge v-if="row.payrollPostStatus === 'SalarySlipGenerated'" color="success" label="Salary Slip Generated" />
                  <p v-if="row.generatedSalaryPaySlipId" class="text-xs text-muted">Slip: {{ row.generatedSalaryPaySlipId }}</p>
                </td>
                <td class="p-2 flex gap-2">
                  <UButton size="xs" label="Ready" :disabled="row.payrollPostStatus === 'SalarySlipGenerated'" :loading="markingId === row.id" @click="mark(row, 'ReadyForPayroll')" />
                  <UButton size="xs" label="Hold" color="warning" variant="soft" :disabled="row.payrollPostStatus === 'SalarySlipGenerated'" :loading="markingId === row.id" @click="mark(row, 'OnHold')" />
                </td>
              </tr>
              <tr v-if="!rows.length"><td colspan="9" class="p-4 text-center text-muted">No reviewed attendance payroll rows found. First rebuild Attendance Payroll Review and mark rows Reviewed or ApprovedForPayroll.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

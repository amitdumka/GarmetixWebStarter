<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const markingId = ref('')
const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const draft = ref<any | null>(null)

const rows = computed(() => draft.value?.rows || [])

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

onMounted(refresh)
</script>

<template>
  <AppShell title="Attendance Salary Draft" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Attendance Salary Slip Draft Preview"
        description="Stage 9E prepares salary-slip draft preview from reviewed attendance payroll rows only. No salary slip, payment, accounting voucher, or payroll posting is created automatically."
        icon="i-lucide-receipt-indian-rupee"
        :loading="loading"
      >
        <template #actions>
          <UInput v-model="year" type="number" class="w-28" />
          <UInput v-model="month" type="number" class="w-24" />
          <UButton label="Refresh" variant="subtle" :loading="loading" @click="refresh" />
          <UButton label="Rebuild Drafts" color="primary" :loading="loading" @click="rebuild" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        icon="i-lucide-shield-alert"
        title="Preview only"
        description="This page only calculates a draft preview. Final salary slip creation and accounting/voucher posting remain disabled until a later approved package."
      />

      <div class="grid gap-3 md:grid-cols-5">
        <UCard><p class="text-xs text-muted">Employees</p><strong>{{ draft?.employees || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Ready</p><strong>{{ draft?.readyRows || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Gross Preview</p><strong>₹{{ Number(draft?.totalGrossPreview || 0).toFixed(2) }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Deduction Preview</p><strong>₹{{ Number(draft?.totalDeductionPreview || 0).toFixed(2) }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Net Preview</p><strong>₹{{ Number(draft?.totalNetPayPreview || 0).toFixed(2) }}</strong></UCard>
      </div>

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
                <td class="p-2"><UBadge :label="row.draftStatus" /></td>
                <td class="p-2 flex gap-2">
                  <UButton size="xs" label="Ready" :loading="markingId === row.id" @click="mark(row, 'ReadyForPayroll')" />
                  <UButton size="xs" label="Hold" color="warning" variant="soft" :loading="markingId === row.id" @click="mark(row, 'OnHold')" />
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

<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const rebuilding = ref(false)
const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const review = ref<any | null>(null)
const notes = reactive<Record<string, string>>({})

async function refresh() {
  loading.value = true
  try {
    review.value = await reports.payrollReview(year.value, month.value)
  } catch (error: any) {
    feedback.fromError('Attendance payroll review failed', error)
  } finally {
    loading.value = false
  }
}

async function rebuild() {
  rebuilding.value = true
  try {
    review.value = await reports.rebuildPayrollReview({ year: year.value, month: month.value })
    feedback.success('Attendance payroll review rebuilt', 'Review rows were refreshed from Attendance Monthly Summary. Payroll was not posted.')
  } catch (error: any) {
    feedback.fromError('Could not rebuild attendance payroll review', error)
  } finally {
    rebuilding.value = false
  }
}

async function mark(row: any, status = 'Reviewed') {
  try {
    await reports.markPayrollReview(row.id, { reviewStatus: status, notes: notes[row.id] || `Marked ${status} from Stage 9D attendance payroll review.` })
    await refresh()
    feedback.success('Attendance payroll row updated', `${row.employeeName} marked ${status}.`)
  } catch (error: any) {
    feedback.fromError('Could not mark attendance payroll row', error)
  }
}

const rows = computed(() => review.value?.rows || [])
onMounted(refresh)
</script>

<template>
  <AppShell title="Attendance Payroll Review" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Attendance Payroll Review"
        description="Stage 9D payroll integration foundation: present/absent/late/half-day/overtime review only. No salary auto-deduction or payroll posting yet."
        icon="i-lucide-hand-coins"
        :loading="loading"
      >
        <template #actions>
          <UInput v-model="year" type="number" class="w-28" />
          <UInput v-model="month" type="number" class="w-24" />
          <UButton label="Refresh" :loading="loading" @click="refresh" />
          <UButton label="Rebuild Review" color="primary" variant="soft" :loading="rebuilding" @click="rebuild" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        title="Payroll safety"
        description="This page prepares attendance numbers for payroll review only. It does not change salary slips, salary payments, deductions, PF, gratuity, or posted payroll records."
      />

      <div class="grid gap-3 md:grid-cols-6">
        <UCard><p class="text-xs text-muted">Employees</p><strong>{{ review?.employees || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Payable Days</p><strong>{{ review?.payableDays || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Deduction Days</p><strong>{{ review?.deductionDays || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Late Days</p><strong>{{ review?.lateDays || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">OT Minutes</p><strong>{{ review?.overtimeMinutes || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Reviewed</p><strong>{{ review?.reviewedRows || 0 }}</strong></UCard>
      </div>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h3 class="font-semibold">Employee payroll review rows</h3>
              <p class="text-sm text-muted">Rows are generated from Attendance Monthly Summary. Rebuild after recalculating attendance.</p>
            </div>
          </div>
        </template>
        <div class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead class="text-left text-xs uppercase text-muted">
              <tr>
                <th class="p-2">Employee</th>
                <th class="p-2">Present</th>
                <th class="p-2">Absent</th>
                <th class="p-2">Half</th>
                <th class="p-2">Late</th>
                <th class="p-2">Payable</th>
                <th class="p-2">Deduction</th>
                <th class="p-2">OT</th>
                <th class="p-2">Est. Gross</th>
                <th class="p-2">Status</th>
                <th class="p-2">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.employeeId" class="border-t border-default">
                <td class="p-2">
                  <div class="font-medium">{{ row.employeeName }}</div>
                  <div class="text-xs text-muted">{{ row.employeeCode }}</div>
                </td>
                <td class="p-2">{{ row.presentDays }}</td>
                <td class="p-2">{{ row.absentDays }}</td>
                <td class="p-2">{{ row.halfDays }}</td>
                <td class="p-2">{{ row.lateDays }}</td>
                <td class="p-2 font-medium">{{ row.payableDays }}</td>
                <td class="p-2 font-medium">{{ row.deductionDays }}</td>
                <td class="p-2">{{ row.overtimeMinutes }}</td>
                <td class="p-2">₹{{ row.estimatedGrossPay || 0 }}</td>
                <td class="p-2">
                  <UBadge :color="row.reviewStatus === 'ApprovedForPayroll' ? 'success' : row.reviewStatus === 'OnHold' ? 'warning' : 'neutral'">{{ row.reviewStatus }}</UBadge>
                </td>
                <td class="p-2">
                  <div class="flex flex-col gap-2 min-w-52">
                    <UInput v-model="notes[row.id]" placeholder="Review note" size="xs" />
                    <div class="flex gap-2">
                      <UButton size="xs" label="Reviewed" variant="soft" @click="mark(row, 'Reviewed')" />
                      <UButton size="xs" label="Approve" color="success" variant="soft" @click="mark(row, 'ApprovedForPayroll')" />
                      <UButton size="xs" label="Hold" color="warning" variant="soft" @click="mark(row, 'OnHold')" />
                    </div>
                  </div>
                </td>
              </tr>
              <tr v-if="!rows.length">
                <td colspan="11" class="p-6 text-center text-muted">No payroll review rows yet. Recalculate monthly attendance, then click Rebuild Review.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

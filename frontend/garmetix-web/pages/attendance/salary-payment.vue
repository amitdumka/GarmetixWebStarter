<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const posting = ref(false)
const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const paymentMode = ref(0)
const postingNotes = ref('')
const data = ref<any | null>(null)

const rows = computed(() => data.value?.rows || [])
const pendingRows = computed(() => rows.value.filter((row: any) => row.generatedSalaryPaySlipId && row.paymentPostStatus !== 'SalaryPaymentGenerated'))
const paidRows = computed(() => rows.value.filter((row: any) => row.paymentPostStatus === 'SalaryPaymentGenerated'))

async function refresh() {
  loading.value = true
  try {
    data.value = await reports.salaryPaymentCandidates(year.value, month.value)
  } catch (error: any) {
    feedback.fromError('Attendance salary payment refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function generatePayments() {
  if (!pendingRows.value.length) {
    feedback.warning('No salary payments pending', 'Generate salary slips first, then come here to create salary payment postings.')
    return
  }
  const ok = window.confirm(`Generate salary payment and accounting posting for ${pendingRows.value.length} attendance-generated payslip(s)?`)
  if (!ok) return
  posting.value = true
  try {
    const result = await reports.generateSalaryPaymentsFromDrafts({
      year: year.value,
      month: month.value,
      confirm: true,
      paymentMode: paymentMode.value,
      paymentDate: new Date().toISOString(),
      notes: postingNotes.value || null
    })
    data.value = { ...data.value, rows: result.rows, readyToPay: result.rows.filter((r: any) => r.generatedSalaryPaySlipId && r.paymentPostStatus !== 'SalaryPaymentGenerated').length, paid: result.rows.filter((r: any) => r.paymentPostStatus === 'SalaryPaymentGenerated').length }
    feedback.success('Salary payments generated', `${result.createdPayments} payment(s) generated for ₹${Number(result.totalAmount || 0).toFixed(2)}.`)
  } catch (error: any) {
    feedback.fromError('Could not generate salary payments', error)
  } finally {
    posting.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Attendance Salary Payment" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Attendance Salary Payment Posting"
        description="Stage 9G creates salary payments from attendance-generated salary slips only after explicit confirmation. Accounting posting uses the existing SalaryPayment posting workflow."
        icon="i-lucide-wallet-cards"
        :loading="loading"
      >
        <template #actions>
          <UInput v-model="year" type="number" class="w-28" />
          <UInput v-model="month" type="number" class="w-24" />
          <UButton label="Refresh" variant="subtle" :loading="loading" @click="refresh" />
          <UButton label="Generate Payments" color="success" :loading="posting" :disabled="!pendingRows.length" @click="generatePayments" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        icon="i-lucide-shield-check"
        title="Explicit confirmation required"
        description="Payments are created only for attendance salary drafts that already generated final salary slips. This step posts salary payment accounting through the existing audited payroll workflow."
      />

      <div class="grid gap-3 md:grid-cols-4">
        <UCard><p class="text-xs text-muted">Pending Payment</p><strong>{{ pendingRows.length }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Paid</p><strong>{{ paidRows.length }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Pending Amount</p><strong>₹{{ Number(data?.totalPendingAmount || 0).toFixed(2) }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Month</p><strong>{{ month }}/{{ year }}</strong></UCard>
      </div>

      <UCard>
        <div class="grid gap-3 md:grid-cols-3 md:items-end">
          <UFormField label="Payment Mode">
            <select v-model.number="paymentMode" class="w-full rounded border bg-transparent px-3 py-2 text-sm">
              <option :value="0">Cash</option>
              <option :value="2">UPI</option>
              <option :value="6">NEFT</option>
              <option :value="5">RTGS</option>
              <option :value="7">Cheque</option>
              <option :value="15">Credit Balance</option>
            </select>
          </UFormField>
          <UFormField label="Posting Note">
            <UInput v-model="postingNotes" placeholder="Optional payment note" />
          </UFormField>
          <UButton block label="Generate confirmed salary payments" color="success" :loading="posting" :disabled="!pendingRows.length" @click="generatePayments" />
        </div>
      </UCard>

      <UCard>
        <div class="overflow-x-auto">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b text-left text-muted">
                <th class="p-2">Employee</th>
                <th class="p-2">Payslip</th>
                <th class="p-2">Net Pay</th>
                <th class="p-2">Draft Status</th>
                <th class="p-2">Payroll Status</th>
                <th class="p-2">Payment Status</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="row.draftId" class="border-b">
                <td class="p-2"><strong>{{ row.employeeName }}</strong><p class="text-xs text-muted">{{ row.employeeCode }}</p></td>
                <td class="p-2 text-xs">{{ row.generatedSalaryPaySlipId || 'Not generated' }}</td>
                <td class="p-2 font-semibold">₹{{ Number(row.netPayPreview || 0).toFixed(2) }}</td>
                <td class="p-2"><UBadge :label="row.draftStatus" /></td>
                <td class="p-2"><UBadge :label="row.payrollPostStatus" /></td>
                <td class="p-2">
                  <UBadge v-if="row.paymentPostStatus === 'SalaryPaymentGenerated'" color="success" label="Paid" />
                  <UBadge v-else color="warning" :label="row.paymentPostStatus || 'NotPaid'" />
                  <p v-if="row.generatedSalaryPaymentId" class="mt-1 text-xs text-muted">Payment: {{ row.generatedSalaryPaymentId }}</p>
                </td>
              </tr>
              <tr v-if="!rows.length"><td colspan="6" class="p-4 text-center text-muted">No attendance salary drafts found for this month.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

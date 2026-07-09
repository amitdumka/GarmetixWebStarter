<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-badge-indian-rupee" class="size-4" /> Payroll</p>
          <h2 class="garmetix-dashboard-title">Payroll</h2>
          <p class="garmetix-dashboard-subtitle">
            Salary structures, monthly payslips, salary payment preview and printable payment vouchers.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/payroll/finalization" icon="i-lucide-clipboard-check" color="primary" variant="soft">Finalization</UButton>
          <UButton icon="i-lucide-file-down" color="primary" variant="soft" :disabled="!activeRows.length" @click="exportActive">Export CSV</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.caption }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div class="flex flex-wrap gap-2">
          <UButton
            v-for="tab in tabs"
            :key="tab.key"
            :icon="tab.icon"
            :color="activeTab === tab.key ? 'primary' : 'neutral'"
            :variant="activeTab === tab.key ? 'solid' : 'soft'"
            size="sm"
            @click="activeTab = tab.key"
          >
            {{ tab.label }}
          </UButton>
        </div>
        <div class="flex flex-wrap gap-2">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search payroll..." class="w-72 max-w-full" />
          <UButton v-if="activeTab === 'structures'" size="sm" icon="i-lucide-plus" @click="startStructureCreate">New Structure</UButton>
          <UButton v-if="activeTab === 'payments'" size="sm" icon="i-lucide-plus" @click="startPaymentCreate()">New Payment</UButton>
        </div>
      </div>
    </div>

    <div v-if="activeTab === 'payslips'" class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Payslips</h3>
          <p class="garmetix-panel-subtitle">Generate month, print, download PDF, email link or share WhatsApp message.</p>
        </div>
      </div>
      <div class="mb-4 grid gap-3 lg:grid-cols-[180px_minmax(0,1fr)]">
        <UFormField label="Payroll month">
          <UInput v-model="periodMonth" type="month" />
        </UFormField>
        <div class="flex flex-wrap items-end gap-2">
          <UButton icon="i-lucide-file-plus-2" :loading="generating" @click="generatePayslips">Generate Month</UButton>
          <UBadge color="neutral" variant="subtle">Auto generation can still run on the 1st day from backend scheduler/deployment setup.</UBadge>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[1080px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Month</th>
              <th class="px-3 py-2">Days</th>
              <th class="px-3 py-2">Earnings</th>
              <th class="px-3 py-2">Deductions</th>
              <th class="px-3 py-2">Advance</th>
              <th class="px-3 py-2">Due</th>
              <th class="px-3 py-2">Paid</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2 text-right">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(slip, index) in filteredPayslips" :key="readText(slip, ['id'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ readText(slip, ['employeeName', 'employee']) }}</td>
              <td class="px-3 py-2">{{ readText(slip, ['monthYear', 'month']) }}</td>
              <td class="px-3 py-2">{{ numberText(readNumber(slip, ['billableDays'])) }} / {{ numberText(readNumber(slip, ['workingDays'])) }}</td>
              <td class="px-3 py-2">{{ money(readNumber(slip, ['totalEarnings'])) }}</td>
              <td class="px-3 py-2">{{ money(readNumber(slip, ['totalDeductions'])) }}</td>
              <td class="px-3 py-2">{{ money(readNumber(slip, ['salaryAdvance'])) }}</td>
              <td class="px-3 py-2">{{ money(readNumber(slip, ['dueAmount', 'carryForwardDue'])) }}</td>
              <td class="px-3 py-2">{{ money(readNumber(slip, ['paidAmount'])) }}</td>
              <td class="px-3 py-2"><UBadge color="neutral" variant="subtle">{{ readText(slip, ['status']) }}</UBadge></td>
              <td class="px-3 py-2">
                <div class="flex justify-end gap-1">
                  <UButton size="xs" icon="i-lucide-wallet-cards" variant="soft" @click="startPaymentFromPayslip(slip)">Pay</UButton>
                  <UButton size="xs" icon="i-lucide-download" color="neutral" variant="soft" @click="downloadPayslip(slip)">PDF</UButton>
                  <UButton size="xs" icon="i-lucide-mail" color="neutral" variant="soft" @click="sharePayslipEmail(slip)">Email</UButton>
                  <UButton size="xs" icon="i-lucide-message-circle" color="success" variant="soft" @click="sharePayslipWhatsApp(slip)">WhatsApp</UButton>
                </div>
              </td>
            </tr>
            <tr v-if="!filteredPayslips.length">
              <td class="px-3 py-6 text-center text-muted" colspan="10">No payslips returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <UModal v-model:open="structureFormOpen" :title="editingStructureId ? 'Edit Salary Structure' : 'New Salary Structure'">
      <template #body>
        <div class="grid gap-3">
          <p class="garmetix-panel-subtitle">Maintain earnings, deductions and current salary setup.</p>
          <UFormField label="Employee" required>
            <USelectMenu v-model="structureForm.employeeId" value-key="value" :items="employeeOptions" placeholder="Search employee..." />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="From date" required><UInput v-model="structureForm.fromDate" type="date" /></UFormField>
            <UFormField label="To date"><UInput v-model="structureForm.toDate" type="date" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Basic salary"><UInput v-model="structureForm.basicSalary" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="HRA"><UInput v-model="structureForm.hra" type="number" min="0" step="0.01" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Special allowance"><UInput v-model="structureForm.specialAllowance" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="Conveyance"><UInput v-model="structureForm.conveyanceAllowance" type="number" min="0" step="0.01" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Incentives"><UInput v-model="structureForm.incentives" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="Yearly bonus"><UInput v-model="structureForm.yearlyBonus" type="number" min="0" step="0.01" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Provident fund"><UInput v-model="structureForm.providentFund" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="Gratuity"><UInput v-model="structureForm.gratuity" type="number" min="0" step="0.01" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Professional tax"><UInput v-model="structureForm.professionalTax" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="Other deductions"><UInput v-model="structureForm.deductions" type="number" min="0" step="0.01" /></UFormField>
          </div>
          <div class="grid grid-cols-3 gap-2 rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <div><p class="text-muted">Gross</p><strong>{{ money(structureGross) }}</strong></div>
            <div><p class="text-muted">Deductions</p><strong>{{ money(structureDeductions) }}</strong></div>
            <div><p class="text-muted">Net</p><strong>{{ money(structureNet) }}</strong></div>
          </div>
          <div class="flex flex-wrap justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="resetStructure">Clear</UButton>
            <UButton icon="i-lucide-save" :disabled="!canSaveStructure" :loading="saving" @click="saveStructure">Save Structure</UButton>
          </div>
        </div>
      </template>
    </UModal>

    <div v-if="activeTab === 'structures'" class="grid gap-4">
      <div class="garmetix-table-panel overflow-hidden">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Salary Structures</h3>
            <p class="garmetix-panel-subtitle">{{ filteredStructures.length }} row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[980px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">From</th>
                <th class="px-3 py-2">To</th>
                <th class="px-3 py-2">Gross</th>
                <th class="px-3 py-2">Deductions</th>
                <th class="px-3 py-2">Net</th>
                <th class="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in filteredStructures" :key="readText(item, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ employeeName(readText(item, ['employeeId'], '')) }}</td>
                <td class="px-3 py-2">{{ dateText(readText(item, ['fromDate'], '')) }}</td>
                <td class="px-3 py-2">{{ dateText(readText(item, ['toDate'], '')) }}</td>
                <td class="px-3 py-2">{{ money(grossForStructure(item)) }}</td>
                <td class="px-3 py-2">{{ money(deductionsForStructure(item)) }}</td>
                <td class="px-3 py-2 font-semibold">{{ money(netForStructure(item)) }}</td>
                <td class="px-3 py-2">
                  <div class="flex justify-end gap-1">
                    <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="editStructure(item)">Edit</UButton>
                    <UButton size="xs" icon="i-lucide-wallet-cards" variant="soft" @click="startPaymentCreate(item)">Pay</UButton>
                    <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="soft" @click="deleteStructure(item)">Delete</UButton>
                  </div>
                </td>
              </tr>
              <tr v-if="!filteredStructures.length">
                <td class="px-3 py-6 text-center text-muted" colspan="7">No salary structures returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <USlideover v-model:open="paymentFormOpen" :title="editingPaymentId ? 'Edit Salary Payment' : 'New Salary Payment'">
      <template #body>
        <div class="grid gap-3">
          <p class="garmetix-panel-subtitle">Use preview to reduce salary advance, add previous due and round paid amount.</p>
          <UFormField label="Employee" required>
            <USelectMenu v-model="paymentForm.employeeId" value-key="value" :items="employeeOptions" placeholder="Search employee..." @update:model-value="precalculatePayment(false)" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Voucher number">
              <UInput :model-value="editingPaymentId ? paymentForm.voucherNumber : 'Assigned on save'" disabled />
            </UFormField>
            <UFormField label="Salary month" required>
              <UInput v-model="paymentForm.salaryMonth" type="number" @blur="precalculatePayment(false)" />
            </UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Payment date" required><UInput v-model="paymentForm.onDate" type="date" /></UFormField>
            <UFormField label="Preview">
              <UButton block icon="i-lucide-calculator" color="neutral" variant="soft" :loading="previewing" @click="precalculatePayment(true)">Recalculate</UButton>
            </UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Component"><USelect v-model="paymentForm.salaryComponent" :items="salaryComponentOptions" /></UFormField>
            <UFormField label="Mode"><USelect v-model="paymentForm.paymentMode" :items="paymentModeOptions" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Gross salary"><UInput v-model="paymentForm.grossSalary" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="Deductions"><UInput v-model="paymentForm.totalDeductions" type="number" min="0" step="0.01" /></UFormField>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Net salary"><UInput v-model="paymentForm.netSalary" type="number" min="0" step="0.01" /></UFormField>
            <UFormField label="Paid amount"><UInput v-model="paymentForm.amount" type="number" min="0" step="1" @blur="roundPaymentAmount" /></UFormField>
          </div>
          <UFormField label="Remarks"><UTextarea v-model="paymentForm.remarks" autoresize /></UFormField>
          <div class="grid grid-cols-2 gap-2 rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <div><p class="text-muted">Base deductions</p><strong>{{ money(Number(paymentForm.baseDeductions || 0)) }}</strong></div>
            <div><p class="text-muted">Advance reduced</p><strong>{{ money(Number(paymentForm.salaryAdvance || 0)) }}</strong></div>
            <div><p class="text-muted">Previous due added</p><strong>{{ money(Number(paymentForm.previousDue || 0)) }}</strong></div>
            <div><p class="text-muted">Already paid</p><strong>{{ money(Number(paymentForm.alreadyPaid || 0)) }}</strong></div>
            <div><p class="text-muted">Round off</p><strong>{{ money(Number(paymentForm.roundOff || 0)) }}</strong></div>
            <div><p class="text-muted">Balance</p><strong>{{ money(paymentBalance) }}</strong></div>
          </div>
          <div class="flex flex-wrap justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="resetPayment">Clear</UButton>
            <UButton icon="i-lucide-save" :disabled="!canSavePayment" :loading="saving" @click="savePayment">Save Payment</UButton>
          </div>
        </div>
      </template>
    </USlideover>

    <div v-if="activeTab === 'payments'" class="grid gap-4">
      <div class="garmetix-table-panel overflow-hidden">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Salary Payments</h3>
            <p class="garmetix-panel-subtitle">{{ filteredPayments.length }} row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[1040px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Voucher</th>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Month</th>
                <th class="px-3 py-2">Date</th>
                <th class="px-3 py-2">Mode</th>
                <th class="px-3 py-2">Net</th>
                <th class="px-3 py-2">Paid</th>
                <th class="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(payment, index) in filteredPayments" :key="readText(payment, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(payment, ['voucherNumber'], 'Assigned') }}</td>
                <td class="px-3 py-2">{{ employeeName(readText(payment, ['employeeId'], '')) }}</td>
                <td class="px-3 py-2">{{ readText(payment, ['salaryMonth']) }}</td>
                <td class="px-3 py-2">{{ dateText(readText(payment, ['onDate'], '')) }}</td>
                <td class="px-3 py-2">{{ paymentModeLabel(readNumber(payment, ['paymentMode'])) }}</td>
                <td class="px-3 py-2">{{ money(readNumber(payment, ['netSalary'])) }}</td>
                <td class="px-3 py-2 font-semibold">{{ money(readNumber(payment, ['amount'])) }}</td>
                <td class="px-3 py-2">
                  <div class="flex justify-end gap-1">
                    <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="editPayment(payment)">Edit</UButton>
                    <UButton size="xs" icon="i-lucide-download" color="neutral" variant="soft" @click="downloadPayment(payment)">PDF</UButton>
                    <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="soft" @click="deletePayment(payment)">Delete</UButton>
                  </div>
                </td>
              </tr>
              <tr v-if="!filteredPayments.length">
                <td class="px-3 py-6 text-center text-muted" colspan="8">No salary payments returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, isActiveEmployee } from '@garmetix/shared-utils'
import { currentYearMonth, downloadCsvFile, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../utils/hr-api'

useHead({ title: 'Payroll - Garmetix HR' })

type PayrollTab = 'payslips' | 'structures' | 'payments'

const { get, post, put, del, downloadFile } = useHrApiClient()
const loading = ref(false)
const saving = ref(false)
const generating = ref(false)
const previewing = ref(false)
const activeTab = ref<PayrollTab>('payslips')
const search = ref('')
const message = ref('')
const messageTone = ref<'info' | 'success' | 'warning' | 'error'>('info')
const employees = ref<ApiRecord[]>([])
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const setupStatus = ref<ApiRecord | null>(null)
const payslips = ref<ApiRecord[]>([])
const structures = ref<ApiRecord[]>([])
const payments = ref<ApiRecord[]>([])
const editingStructureId = ref('')
const editingPaymentId = ref('')
const structureFormOpen = ref(false)
const paymentFormOpen = ref(false)
const { year, month } = currentYearMonth()
const periodMonth = ref(`${year}-${String(month).padStart(2, '0')}`)

const tabs = [
  { key: 'payslips' as const, label: 'Payslips', icon: 'i-lucide-file-stack' },
  { key: 'structures' as const, label: 'Salary Structures', icon: 'i-lucide-badge-indian-rupee' },
  { key: 'payments' as const, label: 'Salary Payments', icon: 'i-lucide-wallet-cards' }
]
const salaryComponentOptions = ['NetSalary', 'LastPcs', 'WOWBill', 'SundaySalary', 'Incentive', 'Others', 'Advance', 'PaidLeave', 'SickLeave', 'SalaryAdvance', 'Receipts'].map((label, value) => ({ label, value }))
const paymentModeOptions = ['Cash', 'Card', 'UPI', 'Wallets', 'IMPS', 'RTGS', 'NEFT', 'Cheque', 'DemandDraft', 'CreditNote', 'DebitNote', 'Coupons', 'MixPayments', 'SaleReturn', 'Others', 'CreditBalance'].map((label, value) => ({ label, value }))

const structureForm = reactive(emptyStructure())
const paymentForm = reactive(emptyPayment())

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const employeeOptions = computed(() => employees.value.filter(isActiveEmployee).map(employee => ({
  value: readText(employee, ['id'], ''),
  label: employeeName(readText(employee, ['id'], ''))
})))
const structureGross = computed(() => grossForStructure(structureForm as ApiRecord))
const structureDeductions = computed(() => deductionsForStructure(structureForm as ApiRecord))
const structureNet = computed(() => structureGross.value - structureDeductions.value)
const paymentBalance = computed(() => Math.max(0, Number(paymentForm.outstandingAmount || paymentForm.netSalary || 0) - Number(paymentForm.amount || 0)))
const canSaveStructure = computed(() => Boolean(structureForm.employeeId && structureForm.fromDate) && !saving.value)
const canSavePayment = computed(() => Boolean(paymentForm.employeeId && Number(paymentForm.salaryMonth || 0) > 200001 && paymentForm.onDate && Number(paymentForm.amount || 0) > 0) && !saving.value)
const activeRows = computed(() => activeTab.value === 'payslips' ? filteredPayslips.value : activeTab.value === 'structures' ? filteredStructures.value : filteredPayments.value)
const cards = computed(() => {
  const net = payslips.value.reduce((total, item) => total + readNumber(item, ['netSalary', 'payableAmount']), 0)
  const paid = payments.value.reduce((total, item) => total + readNumber(item, ['amount']), 0)
  const due = payslips.value.reduce((total, item) => total + readNumber(item, ['dueAmount']), 0)
  return [
    { label: 'Payslips', value: payslips.value.length, caption: 'Recent salary slip rows' },
    { label: 'Structures', value: structures.value.length, caption: 'Employee salary setup' },
    { label: 'Paid', value: money(paid), caption: 'Salary payments loaded' },
    { label: 'Outstanding', value: money(Math.max(0, due || net - paid)), caption: 'Payslip due estimate' }
  ]
})
const filteredPayslips = computed(() => filterRows(payslips.value, ['employeeName', 'monthYear', 'status']))
const filteredStructures = computed(() => filterRows(structures.value, ['employeeId', 'fromDate', 'toDate']))
const filteredPayments = computed(() => filterRows(payments.value, ['voucherNumber', 'employeeId', 'salaryMonth', 'remarks']))

function emptyStructure() {
  return {
    employeeId: '',
    fromDate: toLocalDateInput(new Date()),
    toDate: '',
    basicSalary: 0,
    hra: 0,
    specialAllowance: 0,
    conveyanceAllowance: 0,
    incentives: 0,
    providentFund: 0,
    gratuity: 0,
    professionalTax: 0,
    deductions: 0,
    yearlyBonus: 0
  }
}

function emptyPayment() {
  const now = new Date()
  return {
    voucherNumber: '',
    employeeId: '',
    salaryMonth: Number(`${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`),
    onDate: toLocalDateInput(now),
    salaryComponent: 0,
    grossSalary: 0,
    baseDeductions: 0,
    salaryAdvance: 0,
    totalDeductions: 0,
    previousDue: 0,
    netSalary: 0,
    alreadyPaid: 0,
    outstandingAmount: 0,
    roundOff: 0,
    amount: 0,
    paymentMode: 0,
    remarks: '',
    salaryPaySlipId: ''
  }
}

function money(value: number) {
  return formatIndianMoney(Number(value || 0))
}

function numberText(value: number) {
  return Number(value || 0).toFixed(1)
}

function dateText(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString('en-IN')
}

function toApiDate(value: string) {
  return value ? `${value}T00:00:00` : null
}

function employeeName(employeeId: string) {
  const employee = employees.value.find(item => readText(item, ['id'], '') === employeeId)
  if (!employee) return employeeId || '-'
  const name = readText(employee, ['staffName', 'fullName', 'name'], '')
  if (name && name !== '-') return name
  return `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || readText(employee, ['employeeCode'], employeeId)
}

function paymentModeLabel(value: number) {
  return paymentModeOptions[value]?.label ?? String(value)
}

function filterRows(rows: ApiRecord[], keys: string[]) {
  const term = search.value.trim().toLowerCase()
  if (!term) return rows
  return rows.filter(row => keys.some(key => {
    const raw = key === 'employeeId' ? employeeName(readText(row, [key], '')) : readText(row, [key], '')
    return raw.toLowerCase().includes(term)
  }))
}

function grossForStructure(item: ApiRecord) {
  return readNumber(item, ['basicSalary']) + readNumber(item, ['hra', 'HRA']) + readNumber(item, ['specialAllowance']) + readNumber(item, ['conveyanceAllowance']) + readNumber(item, ['incentives'])
}

function deductionsForStructure(item: ApiRecord) {
  return readNumber(item, ['providentFund']) + readNumber(item, ['gratuity']) + readNumber(item, ['professionalTax']) + readNumber(item, ['deductions'])
}

function netForStructure(item: ApiRecord) {
  return grossForStructure(item) - deductionsForStructure(item)
}

function setupIds(includeStore: boolean) {
  const selectedStore = stores.value[0]
  const companyId = readText(setupStatus.value, ['companyId'], '') !== '-' ? readText(setupStatus.value, ['companyId'], '') : readText(selectedStore, ['companyId'], readText(companies.value[0], ['id'], ''))
  const storeGroupId = readText(setupStatus.value, ['storeGroupId'], '') !== '-' ? readText(setupStatus.value, ['storeGroupId'], '') : readText(selectedStore, ['storeGroupId'], '')
  const storeId = readText(setupStatus.value, ['storeId'], '') !== '-' ? readText(setupStatus.value, ['storeId'], '') : readText(selectedStore, ['id'], '')
  if (!companyId || (includeStore && (!storeGroupId || !storeId))) throw new Error('Run quick setup before saving payroll.')
  return { companyId, storeGroupId, storeId }
}

function showMessage(text: string, tone: typeof messageTone.value = 'success') {
  message.value = text
  messageTone.value = tone
}

function resetStructure() {
  Object.assign(structureForm, emptyStructure())
  editingStructureId.value = ''
}

function resetPayment() {
  Object.assign(paymentForm, emptyPayment())
  editingPaymentId.value = ''
}

function startStructureCreate() {
  activeTab.value = 'structures'
  resetStructure()
  structureFormOpen.value = true
}

function editStructure(item: ApiRecord) {
  activeTab.value = 'structures'
  Object.assign(structureForm, {
    employeeId: readText(item, ['employeeId'], ''),
    fromDate: toLocalDateInput(new Date(readText(item, ['fromDate'], new Date().toISOString()))),
    toDate: readText(item, ['toDate'], '') === '-' ? '' : toLocalDateInput(new Date(readText(item, ['toDate'], new Date().toISOString()))),
    basicSalary: readNumber(item, ['basicSalary']),
    hra: readNumber(item, ['hra', 'HRA']),
    specialAllowance: readNumber(item, ['specialAllowance']),
    conveyanceAllowance: readNumber(item, ['conveyanceAllowance']),
    incentives: readNumber(item, ['incentives']),
    providentFund: readNumber(item, ['providentFund']),
    gratuity: readNumber(item, ['gratuity']),
    professionalTax: readNumber(item, ['professionalTax']),
    deductions: readNumber(item, ['deductions']),
    yearlyBonus: readNumber(item, ['yearlyBonus'])
  })
  editingStructureId.value = readText(item, ['id'], '')
  structureFormOpen.value = true
}

function startPaymentCreate(structure?: ApiRecord) {
  activeTab.value = 'payments'
  resetPayment()
  if (structure) {
    paymentForm.employeeId = readText(structure, ['employeeId'], '')
    paymentForm.grossSalary = grossForStructure(structure)
    paymentForm.totalDeductions = deductionsForStructure(structure)
    paymentForm.netSalary = netForStructure(structure)
    paymentForm.amount = Math.round(paymentForm.netSalary)
  }
  paymentFormOpen.value = true
}

function startPaymentFromPayslip(payslip: ApiRecord) {
  activeTab.value = 'payments'
  resetPayment()
  paymentForm.employeeId = readText(payslip, ['employeeId'], '')
  paymentForm.salaryMonth = salaryMonthFromPayslip(payslip)
  paymentForm.grossSalary = readNumber(payslip, ['totalEarnings'])
  paymentForm.baseDeductions = readNumber(payslip, ['totalDeductions'])
  paymentForm.salaryAdvance = readNumber(payslip, ['salaryAdvance'])
  paymentForm.totalDeductions = paymentForm.baseDeductions + paymentForm.salaryAdvance
  paymentForm.previousDue = readNumber(payslip, ['carryForwardDue'])
  paymentForm.netSalary = readNumber(payslip, ['payableAmount', 'netSalary'])
  paymentForm.alreadyPaid = readNumber(payslip, ['paidAmount'])
  paymentForm.outstandingAmount = readNumber(payslip, ['dueAmount'])
  paymentForm.amount = Math.round(readNumber(payslip, ['dueAmount', 'payableAmount', 'netSalary']))
  paymentForm.salaryPaySlipId = readText(payslip, ['id'], '')
  paymentForm.remarks = `Salary payment against payslip ${readText(payslip, ['monthYear'], '')}`.trim()
  paymentFormOpen.value = true
}

function editPayment(payment: ApiRecord) {
  activeTab.value = 'payments'
  Object.assign(paymentForm, emptyPayment(), {
    voucherNumber: readText(payment, ['voucherNumber'], ''),
    employeeId: readText(payment, ['employeeId'], ''),
    salaryMonth: readNumber(payment, ['salaryMonth']),
    onDate: toLocalDateInput(new Date(readText(payment, ['onDate'], new Date().toISOString()))),
    salaryComponent: readNumber(payment, ['salaryComponent']),
    grossSalary: readNumber(payment, ['grossSalary']),
    totalDeductions: readNumber(payment, ['totalDeductions']),
    netSalary: readNumber(payment, ['netSalary']),
    amount: readNumber(payment, ['amount']),
    paymentMode: readNumber(payment, ['paymentMode']),
    remarks: readText(payment, ['remarks'], ''),
    salaryPaySlipId: readText(payment, ['salaryPaySlipId'], '')
  })
  editingPaymentId.value = readText(payment, ['id'], '')
  paymentFormOpen.value = true
}

function salaryMonthFromPayslip(payslip: ApiRecord) {
  const start = readText(payslip, ['payPeriodStart'], '')
  const date = start && start !== '-' ? new Date(start) : new Date()
  return Number(`${date.getFullYear()}${String(date.getMonth() + 1).padStart(2, '0')}`)
}

function roundPaymentAmount() {
  paymentForm.amount = Math.round(Number(paymentForm.amount || 0))
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [setup, companyRows, storeRows, employeeRows, structureRows, paymentRows, payslipRows] = await Promise.all([
      get<ApiRecord>('api/setup/status').catch(() => null),
      get<ApiRecord[]>('api/companies').catch(() => []),
      get<ApiRecord[]>('api/stores').catch(() => []),
      get<ApiRecord[]>('api/employees'),
      get<ApiRecord[]>('api/salary-structures').catch(() => []),
      get<ApiRecord[]>('api/salary-payments'),
      get<ApiRecord[]>('api/payroll/payslips/recent', { take: 250 })
    ])
    setupStatus.value = setup
    companies.value = Array.isArray(companyRows) ? companyRows : []
    stores.value = Array.isArray(storeRows) ? storeRows : []
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    structures.value = Array.isArray(structureRows) ? structureRows : []
    payments.value = Array.isArray(paymentRows) ? paymentRows.sort((a, b) => readText(b, ['onDate'], '').localeCompare(readText(a, ['onDate'], ''))) : []
    payslips.value = Array.isArray(payslipRows) ? payslipRows : []
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Unable to load payroll.', 'error')
  } finally {
    loading.value = false
  }
}

async function saveStructure() {
  if (!canSaveStructure.value) return
  saving.value = true
  try {
    const { companyId } = setupIds(false)
    const payload = {
      employeeId: structureForm.employeeId,
      fromDate: toApiDate(structureForm.fromDate),
      toDate: structureForm.toDate ? toApiDate(structureForm.toDate) : null,
      basicSalary: Number(structureForm.basicSalary || 0),
      hra: Number(structureForm.hra || 0),
      specialAllowance: Number(structureForm.specialAllowance || 0),
      conveyanceAllowance: Number(structureForm.conveyanceAllowance || 0),
      incentives: Number(structureForm.incentives || 0),
      providentFund: Number(structureForm.providentFund || 0),
      gratuity: Number(structureForm.gratuity || 0),
      professionalTax: Number(structureForm.professionalTax || 0),
      deductions: Number(structureForm.deductions || 0),
      yearlyBonus: Number(structureForm.yearlyBonus || 0),
      employee: null,
      companyId
    }
    if (editingStructureId.value) {
      await put<ApiRecord>(`api/salary-structures/${editingStructureId.value}`, payload)
      showMessage('Salary structure updated.')
    } else {
      await post<ApiRecord>('api/salary-structures', payload)
      showMessage('Salary structure saved.')
    }
    resetStructure()
    structureFormOpen.value = false
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not save salary structure.', 'error')
  } finally {
    saving.value = false
  }
}

async function precalculatePayment(showError = true) {
  if (!paymentForm.employeeId || Number(paymentForm.salaryMonth || 0) < 200001) return
  previewing.value = true
  try {
    const preview = await post<ApiRecord>('api/salary-payments/preview', {
      employeeId: paymentForm.employeeId,
      salaryMonth: Number(paymentForm.salaryMonth || 0),
      salaryPaySlipId: paymentForm.salaryPaySlipId || null,
      paymentId: editingPaymentId.value || null
    })
    Object.assign(paymentForm, {
      salaryPaySlipId: readText(preview, ['salaryPaySlipId'], paymentForm.salaryPaySlipId),
      grossSalary: readNumber(preview, ['grossSalary']),
      baseDeductions: readNumber(preview, ['baseDeductions']),
      salaryAdvance: readNumber(preview, ['salaryAdvance']),
      totalDeductions: readNumber(preview, ['totalDeductions']),
      previousDue: readNumber(preview, ['previousDue']),
      netSalary: readNumber(preview, ['netPayable']),
      alreadyPaid: readNumber(preview, ['alreadyPaid']),
      outstandingAmount: readNumber(preview, ['outstandingAmount']),
      amount: readNumber(preview, ['roundedPaidAmount']),
      roundOff: readNumber(preview, ['roundOff'])
    })
  } catch (caught) {
    if (showError) showMessage(caught instanceof Error ? caught.message : 'Could not pre-calculate salary payment.', 'error')
  } finally {
    previewing.value = false
  }
}

async function savePayment() {
  if (!canSavePayment.value) return
  saving.value = true
  try {
    const { companyId, storeGroupId, storeId } = setupIds(true)
    const payload = {
      employeeId: paymentForm.employeeId,
      salaryMonth: Number(paymentForm.salaryMonth || 0),
      onDate: toApiDate(paymentForm.onDate),
      salaryComponent: Number(paymentForm.salaryComponent || 0),
      grossSalary: Number(paymentForm.grossSalary || 0),
      totalDeductions: Number(paymentForm.totalDeductions || 0),
      netSalary: Number(paymentForm.netSalary || 0),
      amount: Math.round(Number(paymentForm.amount || 0)),
      paymentMode: Number(paymentForm.paymentMode || 0),
      remarks: String(paymentForm.remarks || '').trim() || null,
      salaryPaySlipId: paymentForm.salaryPaySlipId || null,
      companyId,
      storeGroupId,
      storeId
    }
    let saved: ApiRecord | null = null
    if (editingPaymentId.value) {
      saved = await put<ApiRecord>(`api/salary-payments/${editingPaymentId.value}`, payload)
      showMessage('Salary payment updated.')
    } else {
      saved = await post<ApiRecord>('api/salary-payments', payload)
      showMessage('Salary payment saved. PDF is ready for print/download.')
    }
    resetPayment()
    paymentFormOpen.value = false
    await load()
    const id = readText(saved, ['id'], '')
    if (id) await downloadPayment(saved)
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not save salary payment.', 'error')
  } finally {
    saving.value = false
  }
}

async function generatePayslips() {
  const [yearText, monthText] = periodMonth.value.split('-')
  const selectedYear = Number(yearText)
  const selectedMonth = Number(monthText)
  if (!selectedYear || !selectedMonth) {
    showMessage('Select payroll month before generating payslips.', 'warning')
    return
  }
  generating.value = true
  try {
    const ids = setupIds(true)
    const response = await post<ApiRecord>('api/payroll/payslips/generate-month', {
      year: selectedYear,
      month: selectedMonth,
      ...ids
    })
    showMessage(`Payslip generation completed. Created ${readNumber(response, ['payslipsCreated'])}, updated ${readNumber(response, ['payslipsUpdated'])}.`)
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not generate payslips.', 'error')
  } finally {
    generating.value = false
  }
}

async function deleteStructure(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id || !window.confirm(`Delete salary structure for ${employeeName(readText(item, ['employeeId'], ''))}?`)) return
  try {
    await del<ApiRecord>(`api/salary-structures/${id}`)
    showMessage('Salary structure deleted.')
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not delete salary structure.', 'error')
  }
}

async function deletePayment(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id || !window.confirm(`Delete salary payment ${readText(item, ['voucherNumber'], id)}?`)) return
  try {
    await del<ApiRecord>(`api/salary-payments/${id}`)
    showMessage('Salary payment deleted.')
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not delete salary payment.', 'error')
  }
}

async function downloadPayslip(slip: ApiRecord) {
  const id = readText(slip, ['id'], '')
  if (!id) return
  await downloadFile(`api/payroll/payslips/${id}/pdf`, `payslip-${readText(slip, ['monthYear'], 'month').replace(/\s+/g, '-')}-${readText(slip, ['employeeName'], 'employee').replace(/\s+/g, '-')}.pdf`)
}

async function downloadPayment(payment: ApiRecord) {
  const id = readText(payment, ['id'], '')
  if (!id) return
  await downloadFile(`api/salary-payments/${id}/pdf`, `salary-payment-${readText(payment, ['voucherNumber'], id).replace(/[\\/]/g, '-')}.pdf`)
}

function sharePayslipEmail(slip: ApiRecord) {
  const employee = readText(slip, ['employeeName'], 'Employee')
  const email = readText(slip, ['employeeEmail'], '')
  const subject = encodeURIComponent(`Garmetix payslip - ${readText(slip, ['monthYear'], '')}`)
  const body = encodeURIComponent(`Dear ${employee},\n\nYour payslip for ${readText(slip, ['monthYear'], '')} is ready in Garmetix.\n\nNet salary: ${money(readNumber(slip, ['netSalary']))}\nPaid: ${money(readNumber(slip, ['paidAmount']))}\nDue: ${money(readNumber(slip, ['dueAmount']))}\n\nRegards,\nGarmetix`)
  window.location.href = `mailto:${email && email !== '-' ? email : ''}?subject=${subject}&body=${body}`
}

function sharePayslipWhatsApp(slip: ApiRecord) {
  const mobile = readText(slip, ['employeeMobile'], '').replace(/\D/g, '')
  const messageText = encodeURIComponent(`Garmetix payslip ready for ${readText(slip, ['monthYear'], '')}. Net: ${money(readNumber(slip, ['netSalary']))}, Paid: ${money(readNumber(slip, ['paidAmount']))}, Due: ${money(readNumber(slip, ['dueAmount']))}.`)
  window.open(`https://wa.me/${mobile.length >= 10 ? mobile : ''}?text=${messageText}`, '_blank', 'noopener,noreferrer')
}

function exportActive() {
  if (activeTab.value === 'payslips') {
    downloadCsvFile('garmetix-payslips.csv', filteredPayslips.value.map(row => ({
      employee: readText(row, ['employeeName']),
      month: readText(row, ['monthYear']),
      earnings: readNumber(row, ['totalEarnings']),
      deductions: readNumber(row, ['totalDeductions']),
      advance: readNumber(row, ['salaryAdvance']),
      paid: readNumber(row, ['paidAmount']),
      due: readNumber(row, ['dueAmount']),
      status: readText(row, ['status'])
    })), [
      { key: 'employee', label: 'Employee' },
      { key: 'month', label: 'Month' },
      { key: 'earnings', label: 'Earnings' },
      { key: 'deductions', label: 'Deductions' },
      { key: 'advance', label: 'Advance' },
      { key: 'paid', label: 'Paid' },
      { key: 'due', label: 'Due' },
      { key: 'status', label: 'Status' }
    ])
    return
  }
  if (activeTab.value === 'structures') {
    downloadCsvFile('garmetix-salary-structures.csv', filteredStructures.value.map(row => ({
      employee: employeeName(readText(row, ['employeeId'], '')),
      fromDate: readText(row, ['fromDate']),
      toDate: readText(row, ['toDate']),
      gross: grossForStructure(row),
      deductions: deductionsForStructure(row),
      net: netForStructure(row)
    })), [
      { key: 'employee', label: 'Employee' },
      { key: 'fromDate', label: 'From' },
      { key: 'toDate', label: 'To' },
      { key: 'gross', label: 'Gross' },
      { key: 'deductions', label: 'Deductions' },
      { key: 'net', label: 'Net' }
    ])
    return
  }
  downloadCsvFile('garmetix-salary-payments.csv', filteredPayments.value.map(row => ({
    voucher: readText(row, ['voucherNumber']),
    employee: employeeName(readText(row, ['employeeId'], '')),
    salaryMonth: readText(row, ['salaryMonth']),
    onDate: readText(row, ['onDate']),
    mode: paymentModeLabel(readNumber(row, ['paymentMode'])),
    netSalary: readNumber(row, ['netSalary']),
    amount: readNumber(row, ['amount'])
  })), [
    { key: 'voucher', label: 'Voucher' },
    { key: 'employee', label: 'Employee' },
    { key: 'salaryMonth', label: 'Month' },
    { key: 'onDate', label: 'Date' },
    { key: 'mode', label: 'Mode' },
    { key: 'netSalary', label: 'Net' },
    { key: 'amount', label: 'Paid' }
  ])
}

onMounted(load)
</script>

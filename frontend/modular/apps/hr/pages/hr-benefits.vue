<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-hand-coins" class="size-4" /> HR payroll inputs</p>
          <h2 class="garmetix-dashboard-title">HR Benefits & Salary Adjustments</h2>
          <p class="garmetix-dashboard-subtitle">
            Manage salary advances, recovery, leave, bonus, leave encashment, PF and gratuity rows before payroll finalization.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="startCreate">New Adjustment</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert v-if="readinessMessages.length" color="primary" variant="subtle" icon="i-lucide-clipboard-check" title="Payroll adjustment readiness" :description="readinessMessages.join(' ')" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">HR Benefits Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} adjustment record(s).</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="typeFilter" :items="typeFilterItems" class="w-44" />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search adjustment" class="w-56" />
        </div>
      </div>

      <div class="overflow-auto">
        <table class="w-full min-w-[1120px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Date</th>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Type</th>
              <th class="px-3 py-2">Month</th>
              <th class="px-3 py-2">Amount</th>
              <th class="px-3 py-2">Recovered</th>
              <th class="px-3 py-2">Leave</th>
              <th class="px-3 py-2">PF</th>
              <th class="px-3 py-2">Gratuity</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2 text-right">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in pagedRows" :key="readText(row, ['id'])" class="border-t border-default align-top">
              <td class="px-3 py-2">{{ formatDate(readText(row, ['onDate'])) }}</td>
              <td class="px-3 py-2">
                <p class="font-medium">{{ readText(row, ['employeeName']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['employeeCode']) }}</p>
              </td>
              <td class="px-3 py-2">{{ typeLabel(readText(row, ['adjustmentType'])) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['salaryMonth']) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['amount'])) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['recoveredAmount'])) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['leaveDays']).toFixed(1) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['pfEmployee']) + readNumber(row, ['pfEmployer'])) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(row, ['gratuityAmount'])) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="readText(row, ['status']) === 'Closed' ? 'success' : 'warning'" variant="subtle">{{ readText(row, ['status']) }}</UBadge>
              </td>
              <td class="px-3 py-2">
                <div class="flex justify-end gap-2">
                  <UButton size="xs" icon="i-lucide-pencil" color="primary" variant="soft" @click="startEdit(row)">Edit</UButton>
                  <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="soft" :loading="deletingId === readText(row, ['id'])" @click="remove(row)">Delete</UButton>
                </div>
              </td>
            </tr>
            <tr v-if="!pagedRows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="11">No HR benefits records found.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="filteredRows.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ page }} of {{ totalPages }}</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++">Next</UButton>
        </div>
      </div>
    </div>

    <UModal v-model:open="formOpen" :title="editingId ? 'Edit Adjustment' : 'New Adjustment'">
      <template #body>
        <form class="space-y-3" @submit.prevent="save">
          <div class="garmetix-panel-header">
            <p class="garmetix-panel-subtitle">These rows are consumed by salary draft, payslip and finalization calculations.</p>
            <UBadge color="primary" variant="subtle">{{ form.adjustmentType }}</UBadge>
          </div>

          <UFormField label="Employee" required>
            <USelectMenu v-model="form.employeeId" value-key="value" :items="employeeOptions" placeholder="Search employee..." />
          </UFormField>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Type">
              <USelect v-model="form.adjustmentType" :items="adjustmentTypeOptions" />
            </UFormField>
            <UFormField label="Status">
              <USelect v-model="form.status" :items="statusOptions" />
            </UFormField>
            <UFormField label="Date">
              <UInput v-model="form.onDate" type="date" />
            </UFormField>
            <UFormField label="Salary month">
              <UInput v-model.number="form.salaryMonth" type="number" placeholder="YYYYMM" />
            </UFormField>
            <UFormField label="Amount">
              <UInput v-model.number="form.amount" type="number" min="0" step="0.01" />
            </UFormField>
            <UFormField label="Recovered amount">
              <UInput v-model.number="form.recoveredAmount" type="number" min="0" step="0.01" />
            </UFormField>
            <UFormField label="Leave days">
              <UInput v-model.number="form.leaveDays" type="number" min="0" step="0.5" />
            </UFormField>
            <UFormField label="Gratuity amount">
              <UInput v-model.number="form.gratuityAmount" type="number" min="0" step="0.01" />
            </UFormField>
            <UFormField label="PF employee">
              <UInput v-model.number="form.pfEmployee" type="number" min="0" step="0.01" />
            </UFormField>
            <UFormField label="PF employer">
              <UInput v-model.number="form.pfEmployer" type="number" min="0" step="0.01" />
            </UFormField>
          </div>

          <UCheckbox v-model="form.recoverFromSalary" label="Recover from salary" />
          <UFormField label="Remarks">
            <UTextarea v-model="form.remarks" :rows="3" />
          </UFormField>

          <div class="flex flex-wrap justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="resetForm">Clear</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="saving" :disabled="!canSave">Save Adjustment</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, isActiveEmployee } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../utils/hr-api'

useHead({ title: 'HR Benefits - Garmetix HR' })

const { get, post, put, del } = useHrApiClient()
const loading = ref(false)
const saving = ref(false)
const deletingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const search = ref('')
const typeFilter = ref('all')
const page = ref(1)
const pageSize = ref(20)
const editingId = ref('')
const formOpen = ref(false)
const employees = ref<ApiRecord[]>([])
const adjustments = ref<ApiRecord[]>([])
const summary = ref<ApiRecord | null>(null)
const form = reactive(emptyForm())

const adjustmentTypeOptions = [
  { value: 'SalaryAdvance', label: 'Salary Advance' },
  { value: 'AdvanceRecovery', label: 'Advance Recovery' },
  { value: 'Leave', label: 'Leave' },
  { value: 'Bonus', label: 'Bonus' },
  { value: 'LeaveEncashment', label: 'Leave Encashment' },
  { value: 'PF', label: 'PF' },
  { value: 'Gratuity', label: 'Gratuity' },
  { value: 'Other', label: 'Other' }
]
const statusOptions = ['Open', 'Approved', 'Recovered', 'Closed']
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const activeEmployees = computed(() => employees.value.filter(isActiveEmployee))
const employeeOptions = computed(() => activeEmployees.value.map(employee => ({
  label: `${readText(employee, ['employeeCode'], 'EMP')} - ${employeeName(employee)}`,
  value: readText(employee, ['id'], '')
})).filter(item => item.value))
const readinessMessages = computed(() => readArray(summary.value, ['readinessMessages']).map(item => String(item)))
const cards = computed(() => [
  { label: 'Open Advances', value: readNumber(summary.value, ['openAdvances']), detail: formatIndianMoney(readNumber(summary.value, ['openAdvanceAmount'])) },
  { label: 'Recovered', value: formatIndianMoney(readNumber(summary.value, ['recoveredAmount'])), detail: 'Advance recovery' },
  { label: 'Leave Days', value: readNumber(summary.value, ['leaveDays']).toFixed(1), detail: 'Leave / encashment rows' },
  { label: 'Bonus', value: formatIndianMoney(readNumber(summary.value, ['bonusAmount'])), detail: 'Bonus provision/payment' },
  { label: 'PF', value: formatIndianMoney(readNumber(summary.value, ['pfEmployee']) + readNumber(summary.value, ['pfEmployer'])), detail: 'Employee + employer' },
  { label: 'Gratuity', value: formatIndianMoney(readNumber(summary.value, ['gratuityAmount'])), detail: 'Provision / settlement' }
])
const typeFilterItems = [{ value: 'all', label: 'All types' }, ...adjustmentTypeOptions]
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return adjustments.value.filter(row => {
    const typeMatches = typeFilter.value === 'all' || readText(row, ['adjustmentType']) === typeFilter.value
    const searchMatches = !term || JSON.stringify(row).toLowerCase().includes(term)
    return typeMatches && searchMatches
  })
})
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / pageSize.value)))
const pagedRows = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return filteredRows.value.slice(start, start + pageSize.value)
})
const canSave = computed(() => Boolean(form.employeeId && form.onDate && form.adjustmentType && form.status))

watch([search, typeFilter], () => { page.value = 1 })

function emptyForm() {
  return {
    employeeId: '',
    adjustmentType: 'SalaryAdvance',
    onDate: toLocalDateInput(),
    salaryMonth: salaryMonthInput(),
    amount: 0,
    leaveDays: 0,
    recoverFromSalary: true,
    recoveredAmount: 0,
    pfEmployee: 0,
    pfEmployer: 0,
    gratuityAmount: 0,
    status: 'Open',
    remarks: ''
  }
}

function employeeName(employee: ApiRecord) {
  return readText(employee, ['staffName', 'fullName'], `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || 'Employee')
}

function salaryMonthInput(date = new Date()) {
  return date.getFullYear() * 100 + date.getMonth() + 1
}

function typeLabel(value: string) {
  return adjustmentTypeOptions.find(item => item.value === value)?.label || value
}

function formatDate(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString()
}

function resetForm() {
  editingId.value = ''
  Object.assign(form, emptyForm())
}

function startCreate() {
  resetForm()
  message.value = ''
  formOpen.value = true
}

function startEdit(row: ApiRecord) {
  editingId.value = readText(row, ['id'], '')
  Object.assign(form, {
    employeeId: readText(row, ['employeeId'], ''),
    adjustmentType: readText(row, ['adjustmentType'], 'SalaryAdvance'),
    onDate: readText(row, ['onDate'], toLocalDateInput()).slice(0, 10),
    salaryMonth: readNumber(row, ['salaryMonth']) || salaryMonthInput(),
    amount: readNumber(row, ['amount']),
    leaveDays: readNumber(row, ['leaveDays']),
    recoverFromSalary: readText(row, ['recoverFromSalary'], 'true') !== 'false',
    recoveredAmount: readNumber(row, ['recoveredAmount']),
    pfEmployee: readNumber(row, ['pfEmployee']),
    pfEmployer: readNumber(row, ['pfEmployer']),
    gratuityAmount: readNumber(row, ['gratuityAmount']),
    status: readText(row, ['status'], 'Open'),
    remarks: readText(row, ['remarks'], '')
  })
  formOpen.value = true
}

function buildPayload() {
  const employee = employees.value.find(item => readText(item, ['id'], '') === form.employeeId)
  return {
    ...form,
    id: editingId.value || undefined,
    onDate: `${form.onDate}T00:00:00`,
    salaryMonth: form.salaryMonth ? Number(form.salaryMonth) : null,
    amount: Number(form.amount || 0),
    leaveDays: Number(form.leaveDays || 0),
    recoveredAmount: Number(form.recoveredAmount || 0),
    pfEmployee: Number(form.pfEmployee || 0),
    pfEmployer: Number(form.pfEmployer || 0),
    gratuityAmount: Number(form.gratuityAmount || 0),
    remarks: form.remarks?.trim() || null,
    companyId: readText(employee, ['companyId'], ''),
    storeGroupId: readText(employee, ['storeGroupId'], ''),
    storeId: readText(employee, ['storeId'], '')
  }
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [employeeRows, adjustmentRows, summaryRow] = await Promise.all([
      get<ApiRecord[]>('api/employees'),
      get<ApiRecord[]>('api/hr-payroll/adjustments'),
      get<ApiRecord>('api/hr-payroll/adjustments/summary')
    ])
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    adjustments.value = Array.isArray(adjustmentRows) ? adjustmentRows : []
    summary.value = summaryRow
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load HR benefits.'
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!canSave.value) return
  saving.value = true
  message.value = ''
  try {
    if (editingId.value) {
      await put<ApiRecord>(`api/hr-payroll/adjustments/${editingId.value}`, buildPayload())
      message.value = 'HR payroll adjustment updated.'
    } else {
      await post<ApiRecord>('api/hr-payroll/adjustments', buildPayload())
      message.value = 'HR payroll adjustment saved.'
    }
    messageTone.value = 'success'
    resetForm()
    formOpen.value = false
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save HR payroll adjustment.'
  } finally {
    saving.value = false
  }
}

async function remove(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  if (!id || !window.confirm(`Delete ${readText(row, ['employeeName'], 'selected')} adjustment?`)) return
  deletingId.value = id
  message.value = ''
  try {
    await del<void>(`api/hr-payroll/adjustments/${id}`)
    messageTone.value = 'success'
    message.value = 'HR payroll adjustment deleted.'
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to delete HR payroll adjustment.'
  } finally {
    deletingId.value = ''
  }
}

onMounted(load)
</script>

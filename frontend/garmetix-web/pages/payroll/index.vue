<script setup lang="ts">
import { BadgeIndianRupee, CreditCard, Pencil, Plus, Trash2 } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const salaryStructures = ref<any[]>([])
const salaryPayments = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const activeTab = ref<'structures' | 'payments'>('structures')
const viewMode = ref<'list' | 'structureForm' | 'paymentForm'>('list')
const payrollMessage = ref('')
const searchText = ref('')
const editingStructureId = ref('')
const editingPaymentId = ref('')

const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' }
]

const salaryComponentOptions = [
  { value: 0, label: 'Net Salary' },
  { value: 1, label: 'Last Pcs' },
  { value: 2, label: 'WOW Bill' },
  { value: 3, label: 'Sunday Salary' },
  { value: 4, label: 'Incentive' },
  { value: 5, label: 'Others' },
  { value: 6, label: 'Advance' },
  { value: 7, label: 'Paid Leave' },
  { value: 8, label: 'Sick Leave' },
  { value: 9, label: 'Salary Advance' },
  { value: 10, label: 'Receipts' }
]

const structureForm = reactive<any>(emptyStructure())
const paymentForm = reactive<any>(emptyPayment())

const filteredStructures = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  return salaryStructures.value.filter((item) => {
    const employee = employeeName(item.employeeId).toLowerCase()
    return !query || employee.includes(query)
  })
})

const filteredPayments = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  return salaryPayments.value.filter((item) => {
    const employee = employeeName(item.employeeId).toLowerCase()
    return !query ||
      employee.includes(query) ||
      String(item.voucherNumber || '').toLowerCase().includes(query)
  })
})

const structureGross = computed(() => {
  return Number(structureForm.basicSalary || 0) +
    Number(structureForm.hra || 0) +
    Number(structureForm.specialAllowance || 0) +
    Number(structureForm.conveyanceAllowance || 0) +
    Number(structureForm.incentives || 0)
})

const structureDeductions = computed(() => {
  return Number(structureForm.providentFund || 0) +
    Number(structureForm.gratuity || 0) +
    Number(structureForm.professionalTax || 0) +
    Number(structureForm.deductions || 0)
})

const structureNet = computed(() => structureGross.value - structureDeductions.value)

function emptyStructure() {
  return {
    employeeId: '',
    fromDate: new Date().toISOString().slice(0, 10),
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
  const today = new Date()
  return {
    employeeId: '',
    voucherNumber: createPayrollVoucherNumber(),
    salaryMonth: Number(`${today.getFullYear()}${String(today.getMonth() + 1).padStart(2, '0')}`),
    onDate: today.toISOString().slice(0, 10),
    salaryComponent: 0,
    grossSalary: 0,
    totalDeductions: 0,
    netSalary: 0,
    amount: 0,
    paymentMode: 0,
    remarks: ''
  }
}

function createPayrollVoucherNumber() {
  const date = new Date()
  const stamp = date.toISOString().slice(0, 10).replaceAll('-', '')
  return `PAY-${stamp}-${String(Date.now()).slice(-4)}`
}

function employeeName(employeeId: string) {
  const employee = employees.value.find((item) => item.id === employeeId)
  return employee ? `${employee.firstName} ${employee.lastName}`.trim() : 'Employee'
}

function paymentModeLabel(value: number) {
  return paymentModeOptions.find((item) => item.value === Number(value))?.label || 'Other'
}

function componentLabel(value: number) {
  return salaryComponentOptions.find((item) => item.value === Number(value))?.label || 'Component'
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, employeeRows, structureRows, paymentRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees'),
      api.list<any>('salary-structures'),
      api.list<any>('salary-payments')
    ])

    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeRows
    salaryStructures.value = structureRows
    salaryPayments.value = paymentRows
  } finally {
    loading.value = false
  }
}

function showStructures() {
  activeTab.value = 'structures'
  viewMode.value = 'list'
  payrollMessage.value = ''
}

function showPayments() {
  activeTab.value = 'payments'
  viewMode.value = 'list'
  payrollMessage.value = ''
}

function startStructureCreate() {
  Object.assign(structureForm, emptyStructure())
  editingStructureId.value = ''
  payrollMessage.value = ''
  activeTab.value = 'structures'
  viewMode.value = 'structureForm'
}

function startStructureEdit(item: any) {
  Object.assign(structureForm, {
    ...item,
    fromDate: String(item.fromDate || new Date().toISOString()).slice(0, 10),
    toDate: item.toDate ? String(item.toDate).slice(0, 10) : '',
    employee: null
  })
  editingStructureId.value = item.id
  payrollMessage.value = ''
  activeTab.value = 'structures'
  viewMode.value = 'structureForm'
}

function startPaymentCreate(structure?: any) {
  Object.assign(paymentForm, emptyPayment())
  if (structure) {
    paymentForm.employeeId = structure.employeeId
    paymentForm.grossSalary = grossForStructure(structure)
    paymentForm.totalDeductions = deductionsForStructure(structure)
    paymentForm.netSalary = netForStructure(structure)
    paymentForm.amount = netForStructure(structure)
  }
  editingPaymentId.value = ''
  payrollMessage.value = ''
  activeTab.value = 'payments'
  viewMode.value = 'paymentForm'
}

function startPaymentEdit(item: any) {
  Object.assign(paymentForm, {
    ...item,
    onDate: String(item.onDate || new Date().toISOString()).slice(0, 10),
    employee: null,
    salaryPaySlip: null
  })
  editingPaymentId.value = item.id
  payrollMessage.value = ''
  activeTab.value = 'payments'
  viewMode.value = 'paymentForm'
}

function grossForStructure(item: any) {
  return Number(item.basicSalary || 0) +
    Number(item.hra || 0) +
    Number(item.specialAllowance || 0) +
    Number(item.conveyanceAllowance || 0) +
    Number(item.incentives || 0)
}

function deductionsForStructure(item: any) {
  return Number(item.providentFund || 0) +
    Number(item.gratuity || 0) +
    Number(item.professionalTax || 0) +
    Number(item.deductions || 0)
}

function netForStructure(item: any) {
  return grossForStructure(item) - deductionsForStructure(item)
}

function setupIds(includeStore: boolean) {
  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || (includeStore && (!storeGroupId || !storeId))) {
    throw new Error('Run quick setup before saving payroll.')
  }

  return { companyId, storeGroupId, storeId }
}

async function saveStructure() {
  payrollMessage.value = ''

  try {
    const { companyId } = setupIds(false)
    const payload = {
      ...structureForm,
      employeeId: structureForm.employeeId,
      fromDate: new Date(structureForm.fromDate).toISOString(),
      toDate: structureForm.toDate ? new Date(structureForm.toDate).toISOString() : null,
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
      await api.update<any>('salary-structures', editingStructureId.value, payload)
      payrollMessage.value = 'Salary structure updated.'
    } else {
      await api.create<any>('salary-structures', payload)
      payrollMessage.value = 'Salary structure saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    payrollMessage.value = error?.data?.message || error?.message || 'Could not save salary structure.'
  }
}

async function savePayment() {
  payrollMessage.value = ''

  try {
    const { companyId, storeGroupId, storeId } = setupIds(true)
    const payload = {
      ...paymentForm,
      employeeId: paymentForm.employeeId,
      voucherNumber: String(paymentForm.voucherNumber || '').trim(),
      salaryMonth: Number(paymentForm.salaryMonth || 0),
      onDate: new Date(paymentForm.onDate).toISOString(),
      salaryComponent: Number(paymentForm.salaryComponent),
      grossSalary: Number(paymentForm.grossSalary || 0),
      totalDeductions: Number(paymentForm.totalDeductions || 0),
      netSalary: Number(paymentForm.netSalary || 0),
      amount: Number(paymentForm.amount || 0),
      paymentMode: Number(paymentForm.paymentMode),
      remarks: String(paymentForm.remarks || '').trim() || null,
      employee: null,
      salaryPaySlip: null,
      companyId,
      storeGroupId,
      storeId
    }

    if (editingPaymentId.value) {
      await api.update<any>('salary-payments', editingPaymentId.value, payload)
      payrollMessage.value = 'Salary payment updated.'
    } else {
      await api.create<any>('salary-payments', payload)
      payrollMessage.value = 'Salary payment saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    payrollMessage.value = error?.data?.message || error?.message || 'Could not save salary payment.'
  }
}

async function deleteStructure(item: any) {
  const confirmed = window.confirm(`Delete salary structure for ${employeeName(item.employeeId)}?`)
  if (!confirmed) {
    return
  }

  await api.remove('salary-structures', item.id)
  payrollMessage.value = 'Salary structure deleted.'
  await refresh()
}

async function deletePayment(item: any) {
  const confirmed = window.confirm(`Delete salary payment ${item.voucherNumber}?`)
  if (!confirmed) {
    return
  }

  await api.remove('salary-payments', item.id)
  payrollMessage.value = 'Salary payment deleted.'
  await refresh()
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Payroll"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Payroll</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${salaryStructures.length} structures` }}</span>
            <button class="button secondary" type="button" @click="showStructures">
              Structures
            </button>
            <button class="button secondary" type="button" @click="showPayments">
              Payments
            </button>
            <button v-if="activeTab === 'structures'" class="button" type="button" @click="startStructureCreate">
              <Plus :size="16" />
              New Structure
            </button>
            <button v-else class="button" type="button" @click="startPaymentCreate()">
              <CreditCard :size="16" />
              New Payment
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list' && activeTab === 'structures'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search salary structures" placeholder="Search employee" />
            <p v-if="payrollMessage" class="inline-message">{{ payrollMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Employee</th>
                <th>From</th>
                <th>Gross</th>
                <th>Deductions</th>
                <th>Net</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in filteredStructures" :key="item.id">
                <td>{{ employeeName(item.employeeId) }}</td>
                <td>{{ new Date(item.fromDate).toLocaleDateString() }}</td>
                <td>{{ grossForStructure(item).toFixed(2) }}</td>
                <td>{{ deductionsForStructure(item).toFixed(2) }}</td>
                <td>{{ netForStructure(item).toFixed(2) }}</td>
                <td><span class="status" :class="item.toDate ? 'warn' : 'ok'">{{ item.toDate ? 'Closed' : 'Current' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startPaymentCreate(item)">
                    <CreditCard :size="16" />
                    Pay
                  </button>
                  <button class="button secondary" type="button" @click="startStructureEdit(item)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteStructure(item)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredStructures.length === 0">
                <td colspan="7">No salary structures</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else-if="viewMode === 'list' && activeTab === 'payments'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search salary payments" placeholder="Search employee or voucher" />
            <p v-if="payrollMessage" class="inline-message">{{ payrollMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Voucher</th>
                <th>Date</th>
                <th>Employee</th>
                <th>Month</th>
                <th>Component</th>
                <th>Mode</th>
                <th>Amount</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in filteredPayments" :key="item.id">
                <td>{{ item.voucherNumber }}</td>
                <td>{{ new Date(item.onDate).toLocaleDateString() }}</td>
                <td>{{ employeeName(item.employeeId) }}</td>
                <td>{{ item.salaryMonth }}</td>
                <td>{{ componentLabel(item.salaryComponent) }}</td>
                <td>{{ paymentModeLabel(item.paymentMode) }}</td>
                <td>{{ Number(item.amount).toFixed(2) }}</td>
                <td>
                  <button class="button secondary" type="button" @click="startPaymentEdit(item)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deletePayment(item)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredPayments.length === 0">
                <td colspan="8">No salary payments</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else-if="viewMode === 'structureForm'" class="form-grid wide-form" @submit.prevent="saveStructure">
          <div class="field">
            <label for="structureEmployee">Employee</label>
            <select id="structureEmployee" v-model="structureForm.employeeId" required>
              <option value="">Select employee</option>
              <option v-for="employee in employees" :key="employee.id" :value="employee.id">
                {{ employee.firstName }} {{ employee.lastName }}
              </option>
            </select>
          </div>
          <div class="field">
            <label for="fromDate">From date</label>
            <input id="fromDate" v-model="structureForm.fromDate" required type="date" />
          </div>
          <div class="field">
            <label for="toDate">To date</label>
            <input id="toDate" v-model="structureForm.toDate" type="date" />
          </div>
          <div class="field">
            <label for="basicSalary">Basic salary</label>
            <input id="basicSalary" v-model="structureForm.basicSalary" min="0" type="number" />
          </div>
          <div class="field">
            <label for="hra">HRA</label>
            <input id="hra" v-model="structureForm.hra" min="0" type="number" />
          </div>
          <div class="field">
            <label for="specialAllowance">Special allowance</label>
            <input id="specialAllowance" v-model="structureForm.specialAllowance" min="0" type="number" />
          </div>
          <div class="field">
            <label for="conveyanceAllowance">Conveyance</label>
            <input id="conveyanceAllowance" v-model="structureForm.conveyanceAllowance" min="0" type="number" />
          </div>
          <div class="field">
            <label for="incentives">Incentives</label>
            <input id="incentives" v-model="structureForm.incentives" min="0" type="number" />
          </div>
          <div class="field">
            <label for="providentFund">Provident fund</label>
            <input id="providentFund" v-model="structureForm.providentFund" min="0" type="number" />
          </div>
          <div class="field">
            <label for="gratuity">Gratuity</label>
            <input id="gratuity" v-model="structureForm.gratuity" min="0" type="number" />
          </div>
          <div class="field">
            <label for="professionalTax">Professional tax</label>
            <input id="professionalTax" v-model="structureForm.professionalTax" min="0" type="number" />
          </div>
          <div class="field">
            <label for="deductions">Deductions</label>
            <input id="deductions" v-model="structureForm.deductions" min="0" type="number" />
          </div>
          <div class="payroll-summary">
            <span>Gross</span><strong>{{ structureGross.toFixed(2) }}</strong>
            <span>Deductions</span><strong>{{ structureDeductions.toFixed(2) }}</strong>
            <span>Net</span><strong>{{ structureNet.toFixed(2) }}</strong>
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <BadgeIndianRupee :size="16" />
              Save Structure
            </button>
          </div>
          <p v-if="payrollMessage" class="setup-message">{{ payrollMessage }}</p>
        </form>

        <form v-else class="form-grid wide-form" @submit.prevent="savePayment">
          <div class="field">
            <label for="paymentEmployee">Employee</label>
            <select id="paymentEmployee" v-model="paymentForm.employeeId" required>
              <option value="">Select employee</option>
              <option v-for="employee in employees" :key="employee.id" :value="employee.id">
                {{ employee.firstName }} {{ employee.lastName }}
              </option>
            </select>
          </div>
          <div class="field">
            <label for="voucherNumber">Voucher number</label>
            <input id="voucherNumber" v-model="paymentForm.voucherNumber" required />
          </div>
          <div class="field">
            <label for="salaryMonth">Salary month</label>
            <input id="salaryMonth" v-model="paymentForm.salaryMonth" required type="number" />
          </div>
          <div class="field">
            <label for="paymentDate">Payment date</label>
            <input id="paymentDate" v-model="paymentForm.onDate" required type="date" />
          </div>
          <div class="field">
            <label for="salaryComponent">Component</label>
            <select id="salaryComponent" v-model="paymentForm.salaryComponent">
              <option v-for="item in salaryComponentOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="paymentMode">Payment mode</label>
            <select id="paymentMode" v-model="paymentForm.paymentMode">
              <option v-for="item in paymentModeOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="grossSalary">Gross salary</label>
            <input id="grossSalary" v-model="paymentForm.grossSalary" min="0" type="number" />
          </div>
          <div class="field">
            <label for="totalDeductions">Deductions</label>
            <input id="totalDeductions" v-model="paymentForm.totalDeductions" min="0" type="number" />
          </div>
          <div class="field">
            <label for="netSalary">Net salary</label>
            <input id="netSalary" v-model="paymentForm.netSalary" min="0" type="number" />
          </div>
          <div class="field">
            <label for="amount">Paid amount</label>
            <input id="amount" v-model="paymentForm.amount" min="0" type="number" />
          </div>
          <div class="field">
            <label for="remarks">Remarks</label>
            <input id="remarks" v-model="paymentForm.remarks" />
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <CreditCard :size="16" />
              Save Payment
            </button>
          </div>
          <p v-if="payrollMessage" class="setup-message">{{ payrollMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>

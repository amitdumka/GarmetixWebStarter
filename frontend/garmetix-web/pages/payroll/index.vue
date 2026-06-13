<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type PayrollTab = 'payslips' | 'structures' | 'payments'
type FormKind = 'payslip' | 'structure' | 'payment'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const monthlyAttendance = ref<any[]>([])
const salaryStructures = ref<any[]>([])
const salaryPayments = ref<any[]>([])
const salaryPaySlips = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const generating = ref(false)
const printLoading = ref(false)
const previewingPayment = ref(false)
const activeTab = ref<PayrollTab>('payslips')
const search = ref('')
const formOpen = ref(false)
const formKind = ref<FormKind>('structure')
const deleteOpen = ref(false)
const printOpen = ref(false)
const pendingDelete = ref<any | null>(null)
const selectedPayslip = ref<any | null>(null)
const printDetail = ref<any | null>(null)
const editingStructureId = ref('')
const editingPaymentId = ref('')
const periodForm = reactive({
  month: previousMonthInput()
})

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

const tabs = [
  { key: 'payslips' as const, label: 'Payslips', icon: 'i-lucide-file-text' },
  { key: 'structures' as const, label: 'Salary Structures', icon: 'i-lucide-badge-indian-rupee' },
  { key: 'payments' as const, label: 'Salary Payments', icon: 'i-lucide-credit-card' }
]

const structureForm = reactive<any>(emptyStructure())
const paymentForm = reactive<any>(emptyPayment())

const employeeOptions = computed(() => employees.value.map((employee) => ({
  value: employee.id,
  label: employeeName(employee.id)
})))

const activeLabel = computed(() => {
  if (activeTab.value === 'payslips') {
    return 'Payslips'
  }

  return activeTab.value === 'structures' ? 'Salary Structures' : 'Salary Payments'
})
const primaryLabel = computed(() => {
  if (activeTab.value === 'payslips') {
    return 'Generate Payslips'
  }

  return activeTab.value === 'structures' ? 'New Structure' : 'New Payment'
})
const primaryIcon = computed(() => {
  if (activeTab.value === 'payslips') {
    return 'i-lucide-file-plus-2'
  }

  return activeTab.value === 'structures' ? 'i-lucide-plus' : 'i-lucide-credit-card'
})
const searchPlaceholder = computed(() => {
  if (activeTab.value === 'payslips') {
    return 'Search employee or month'
  }

  return activeTab.value === 'structures' ? 'Search employee' : 'Search employee or voucher'
})

const structureGross = computed(() => grossForStructure(structureForm))
const structureDeductions = computed(() => deductionsForStructure(structureForm))
const structureNet = computed(() => structureGross.value - structureDeductions.value)
const paymentBalance = computed(() => Math.max(0,
  Number(paymentForm.netSalary || 0) -
  Number(paymentForm.alreadyPaid || 0) -
  Number(paymentForm.amount || 0) +
  Number(paymentForm.roundOff || 0)))
const submitDisabled = computed(() =>
  formKind.value === 'payment' && Number(paymentForm.amount || 0) <= 0)

const payrollSummary = computed(() => {
  return {
    gross: salaryStructures.value.reduce((sum, item) => sum + grossForStructure(item), 0),
    deductions: salaryStructures.value.reduce((sum, item) => sum + deductionsForStructure(item), 0),
    net: salaryStructures.value.reduce((sum, item) => sum + netForStructure(item), 0),
    paid: salaryPayments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0),
    due: salaryPaySlips.value.reduce((sum, item) => sum + Number(item.dueAmount || 0), 0),
    advance: salaryPaySlips.value.reduce((sum, item) => sum + Number(item.salaryAdvance || 0), 0),
    billableDays: monthlyAttendance.value.reduce((sum, item) => sum + Number(item.billableDays || 0), 0)
  }
})

const metrics = computed(() => [
  {
    label: 'Payslips',
    value: salaryPaySlips.value.length,
    meta: 'Generated payroll slips',
    icon: 'i-lucide-file-text',
    color: 'primary'
  },
  {
    label: 'Due Salary',
    value: money(payrollSummary.value.due),
    meta: 'After advance and paid amount',
    icon: 'i-lucide-indian-rupee',
    color: 'warning'
  },
  {
    label: 'Advance',
    value: money(payrollSummary.value.advance),
    meta: 'Reduced from payable',
    icon: 'i-lucide-minus-circle',
    color: 'neutral'
  },
  {
    label: 'Billable Days',
    value: payrollSummary.value.billableDays.toFixed(1),
    meta: 'From monthly attendance',
    icon: 'i-lucide-calendar-days',
    color: 'warning'
  }
])

const payslipRows = computed(() => salaryPaySlips.value.map((item) => ({
  id: item.id,
  monthYear: item.monthYear,
  employee: item.employeeName || employeeName(item.employeeId),
  netSalary: money(Number(item.netSalary || 0)),
  advance: money(Number(item.salaryAdvance || 0)),
  carryForwardDue: money(Number(item.carryForwardDue || 0)),
  paid: money(Number(item.paidAmount || 0)),
  due: money(Number(item.dueAmount || 0)),
  status: item.status || 'Due',
  raw: item
})))

const structureRows = computed(() => salaryStructures.value.map((item) => ({
  id: item.id,
  employee: employeeName(item.employeeId),
  fromDate: formatDate(item.fromDate),
  gross: money(grossForStructure(item)),
  deductions: money(deductionsForStructure(item)),
  net: money(netForStructure(item)),
  status: item.toDate ? 'Closed' : 'Current',
  raw: item
})))

const paymentRows = computed(() => salaryPayments.value.map((item) => ({
  id: item.id,
  voucherNumber: item.voucherNumber || '-',
  onDate: formatDate(item.onDate),
  employee: employeeName(item.employeeId),
  salaryMonth: item.salaryMonth || '-',
  component: componentLabel(item.salaryComponent),
  mode: paymentModeLabel(item.paymentMode),
  amount: money(Number(item.amount || 0)),
  raw: item
})))

const currentRows = computed(() => {
  const rows = activeTab.value === 'payslips'
    ? payslipRows.value
    : activeTab.value === 'structures'
      ? structureRows.value
      : paymentRows.value
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return rows
  }

  return rows.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const structureColumns: TableColumn<any>[] = [
  { accessorKey: 'employee', header: 'Employee' },
  { accessorKey: 'fromDate', header: 'From' },
  { accessorKey: 'gross', header: 'Gross' },
  { accessorKey: 'deductions', header: 'Deductions' },
  { accessorKey: 'net', header: 'Net' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Current' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'primary',
        variant: 'ghost',
        icon: 'i-lucide-credit-card',
        label: 'Pay',
        onClick: () => startPaymentCreate(row.original.raw)
      }),
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startStructureEdit(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete('structure', row.original.raw)
      }) : null
    ].filter(Boolean))
  }
]

const payslipColumns: TableColumn<any>[] = [
  { accessorKey: 'monthYear', header: 'Month' },
  { accessorKey: 'employee', header: 'Employee' },
  { accessorKey: 'netSalary', header: 'Net' },
  { accessorKey: 'advance', header: 'Advance' },
  { accessorKey: 'carryForwardDue', header: 'Old Due' },
  { accessorKey: 'paid', header: 'Paid' },
  { accessorKey: 'due', header: 'Due' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Paid' ? 'success' : row.original.status === 'Partial' ? 'warning' : 'error',
      variant: 'subtle'
    }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'primary',
        variant: 'ghost',
        icon: 'i-lucide-printer',
        label: 'Print',
        onClick: () => openPrintablePayslip(row.original.raw)
      }),
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-mail',
        label: 'Email',
        onClick: () => sharePayslipEmail(row.original.raw)
      }),
      h(UButton, {
        color: 'success',
        variant: 'ghost',
        icon: 'i-lucide-message-circle',
        label: 'WhatsApp',
        onClick: () => sharePayslipWhatsApp(row.original.raw)
      }),
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-credit-card',
        label: 'Pay',
        onClick: () => startPaymentFromPayslip(row.original.raw)
      })
    ])
  }
]

const paymentColumns: TableColumn<any>[] = [
  { accessorKey: 'voucherNumber', header: 'Voucher' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'employee', header: 'Employee' },
  { accessorKey: 'salaryMonth', header: 'Month' },
  { accessorKey: 'component', header: 'Component' },
  { accessorKey: 'mode', header: 'Mode' },
  { accessorKey: 'amount', header: 'Amount' },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'primary',
        variant: 'ghost',
        icon: 'i-lucide-printer',
        label: 'Print',
        onClick: () => printSalaryPayment(row.original.raw)
      }),
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startPaymentEdit(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete('payment', row.original.raw)
      }) : null
    ].filter(Boolean))
  }
]

const activeColumns = computed(() => {
  if (activeTab.value === 'payslips') {
    return payslipColumns
  }

  return activeTab.value === 'structures' ? structureColumns : paymentColumns
})

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
    voucherNumber: '',
    salaryMonth: Number(`${today.getFullYear()}${String(today.getMonth() + 1).padStart(2, '0')}`),
    onDate: toLocalDateInput(today),
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

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, employeeRows, monthlyRows, structureRowsData, paymentRowsData, payslipRowsData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees'),
      api.list<any>('monthly-attendance'),
      api.list<any>('salary-structures'),
      api.list<any>('salary-payments'),
      api.get<any[]>('payroll/payslips/recent?take=250')
    ])

    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeRows
    monthlyAttendance.value = monthlyRows
    salaryStructures.value = structureRowsData
    salaryPayments.value = paymentRowsData.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
    salaryPaySlips.value = payslipRowsData
  } catch (error) {
    feedback.failed('Payroll refresh failed', error)
  } finally {
    loading.value = false
  }
}

function showTab(tab: PayrollTab) {
  activeTab.value = tab
  search.value = ''
}

function startStructureCreate() {
  Object.assign(structureForm, emptyStructure())
  editingStructureId.value = ''
  activeTab.value = 'structures'
  formKind.value = 'structure'
  formOpen.value = true
}

function startStructureEdit(item: any) {
  Object.assign(structureForm, {
    ...item,
    fromDate: toDateInput(item.fromDate),
    toDate: item.toDate ? toDateInput(item.toDate) : '',
    employee: null
  })
  editingStructureId.value = item.id
  activeTab.value = 'structures'
  formKind.value = 'structure'
  formOpen.value = true
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
  activeTab.value = 'payments'
  formKind.value = 'payment'
  formOpen.value = true
}

function startPaymentFromPayslip(payslip: any) {
  Object.assign(paymentForm, emptyPayment())
  paymentForm.employeeId = payslip.employeeId
  paymentForm.salaryMonth = salaryMonthFromDate(payslip.payPeriodStart)
  paymentForm.grossSalary = Number(payslip.totalEarnings || 0)
  paymentForm.baseDeductions = Number(payslip.totalDeductions || 0)
  paymentForm.salaryAdvance = Number(payslip.salaryAdvance || 0)
  paymentForm.totalDeductions = paymentForm.baseDeductions + paymentForm.salaryAdvance
  paymentForm.previousDue = Number(payslip.carryForwardDue || 0)
  paymentForm.netSalary = Number(payslip.payableAmount || payslip.netSalary || 0)
  paymentForm.alreadyPaid = Number(payslip.paidAmount || 0)
  paymentForm.outstandingAmount = Number(payslip.dueAmount || 0)
  paymentForm.amount = Number(payslip.dueAmount || payslip.payableAmount || payslip.netSalary || 0)
  paymentForm.salaryComponent = 0
  paymentForm.remarks = `Salary payment against payslip ${payslip.monthYear}`
  paymentForm.salaryPaySlipId = payslip.id
  editingPaymentId.value = ''
  activeTab.value = 'payments'
  formKind.value = 'payment'
  formOpen.value = true
}

function startPaymentEdit(item: any) {
  Object.assign(paymentForm, {
    ...emptyPayment(),
    ...item,
    onDate: toDateInput(item.onDate),
    employee: null,
    salaryPaySlip: null
  })
  editingPaymentId.value = item.id
  activeTab.value = 'payments'
  formKind.value = 'payment'
  formOpen.value = true
}

let paymentPreviewSequence = 0

async function precalculatePayment(showError = true) {
  const employeeId = String(paymentForm.employeeId || '')
  const salaryMonth = Number(paymentForm.salaryMonth || 0)
  if (!employeeId || salaryMonth < 200001) {
    return
  }

  const requestSequence = ++paymentPreviewSequence
  previewingPayment.value = true
  try {
    const preview = await api.create<any>('salary-payments/preview', {
      employeeId,
      salaryMonth,
      salaryPaySlipId: paymentForm.salaryPaySlipId || null,
      paymentId: editingPaymentId.value || null
    })
    if (requestSequence !== paymentPreviewSequence) {
      return
    }

    Object.assign(paymentForm, {
      salaryPaySlipId: preview.salaryPaySlipId || paymentForm.salaryPaySlipId || '',
      grossSalary: Number(preview.grossSalary || 0),
      baseDeductions: Number(preview.baseDeductions || 0),
      salaryAdvance: Number(preview.salaryAdvance || 0),
      totalDeductions: Number(preview.totalDeductions || 0),
      previousDue: Number(preview.previousDue || 0),
      netSalary: Number(preview.netPayable || 0),
      alreadyPaid: Number(preview.alreadyPaid || 0),
      outstandingAmount: Number(preview.outstandingAmount || 0),
      amount: Number(preview.roundedPaidAmount || 0),
      roundOff: Number(preview.roundOff || 0)
    })
  } catch (error) {
    if (showError) {
      feedback.failed('Could not pre-calculate salary payment', error)
    }
  } finally {
    if (requestSequence === paymentPreviewSequence) {
      previewingPayment.value = false
    }
  }
}

async function saveCurrentForm() {
  if (formKind.value === 'structure') {
    await saveStructure()
  } else {
    await savePayment()
  }
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
  const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
  const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
  const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = workspace.storeId.value || setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || (includeStore && (!storeGroupId || !storeId))) {
    throw new Error('Run quick setup before saving payroll.')
  }

  return { companyId, storeGroupId, storeId }
}

async function saveStructure() {
  saving.value = true
  try {
    const { companyId } = setupIds(false)
    const payload = {
      ...structureForm,
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
      await api.update<any>('salary-structures', editingStructureId.value, payload)
      feedback.updated('Salary structure')
    } else {
      await api.create<any>('salary-structures', payload)
      feedback.saved('Salary structure')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save salary structure', error)
  } finally {
    saving.value = false
  }
}

async function savePayment() {
  saving.value = true
  try {
    const { companyId, storeGroupId, storeId } = setupIds(true)
    const payload = {
      employeeId: paymentForm.employeeId,
      salaryMonth: Number(paymentForm.salaryMonth || 0),
      onDate: toApiDate(paymentForm.onDate),
      salaryComponent: Number(paymentForm.salaryComponent),
      grossSalary: Number(paymentForm.grossSalary || 0),
      totalDeductions: Number(paymentForm.totalDeductions || 0),
      netSalary: Number(paymentForm.netSalary || 0),
      amount: Number(paymentForm.amount || 0),
      paymentMode: Number(paymentForm.paymentMode),
      remarks: String(paymentForm.remarks || '').trim() || null,
      salaryPaySlipId: paymentForm.salaryPaySlipId || null,
      companyId,
      storeGroupId,
      storeId
    }

    let createdPayment: any | null = null
    if (editingPaymentId.value) {
      await api.update<any>('salary-payments', editingPaymentId.value, payload)
      feedback.updated('Salary payment')
    } else {
      createdPayment = await api.create<any>('salary-payments', payload)
      feedback.saved('Salary payment')
    }

    formOpen.value = false
    await refresh()
    if (createdPayment?.id) {
      await printSalaryPayment(createdPayment)
    }
  } catch (error) {
    feedback.failed('Could not save salary payment', error)
  } finally {
    saving.value = false
  }
}

function askDelete(kind: FormKind, item: any) {
  formKind.value = kind
  pendingDelete.value = item
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    if (formKind.value === 'payslip') {
      await api.remove('salary-pay-slips', pendingDelete.value.id)
      feedback.deleted('Payslip')
    } else if (formKind.value === 'structure') {
      await api.remove('salary-structures', pendingDelete.value.id)
      feedback.deleted('Salary structure')
    } else {
      await api.remove('salary-payments', pendingDelete.value.id)
      feedback.deleted('Salary payment')
    }

    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete payroll record', error)
  } finally {
    deleting.value = false
  }
}

function primaryAction() {
  if (activeTab.value === 'payslips') {
    generatePayslips()
    return
  }

  if (activeTab.value === 'structures') {
    startStructureCreate()
  } else {
    startPaymentCreate()
  }
}

async function generatePayslips(silent = false) {
  const [yearText, monthText] = periodForm.month.split('-')
  const year = Number(yearText)
  const month = Number(monthText)

  if (!year || !month) {
    feedback.failed('Select a payroll month')
    return false
  }

  generating.value = true
  try {
    const ids = setupIds(true)
    const result = await api.create<any>('payroll/payslips/generate-month', {
      year,
      month,
      ...ids
    })

    if (!silent) {
      feedback.notify('Payslips generated', `${result.payslipsCreated} created, ${result.payslipsUpdated} updated. Due ${money(Number(result.totalDue || 0))}`)
    }

    await refresh()
    return true
  } catch (error) {
    if (!silent) {
      feedback.failed('Could not generate payslips', error)
    }
    return false
  } finally {
    generating.value = false
  }
}

async function autoGeneratePayrollIfDue() {
  if (!import.meta.client) {
    return
  }

  const today = new Date()
  if (today.getDate() !== 1) {
    return
  }

  const previous = new Date(today.getFullYear(), today.getMonth() - 1, 1)
  const previousMonth = toMonthInput(previous)
  const storageKey = `garmetix.payroll.auto-payslips.${previousMonth}`

  if (localStorage.getItem(storageKey)) {
    return
  }

  periodForm.month = previousMonth
  const generated = await generatePayslips(true)
  if (generated) {
    localStorage.setItem(storageKey, new Date().toISOString())
    feedback.notify('Payslips auto-generated', previous.toLocaleDateString(undefined, { month: 'long', year: 'numeric' }))
  }
}

async function openPrintablePayslip(payslip: any) {
  selectedPayslip.value = payslip
  printDetail.value = null
  printOpen.value = true
  printLoading.value = true
  try {
    printDetail.value = await api.get<any>(`payroll/payslips/${payslip.id}/print`)
  } catch (error) {
    feedback.failed('Could not open payslip', error)
    printOpen.value = false
  } finally {
    printLoading.value = false
  }
}

async function printPayslip() {
  if (!selectedPayslip.value?.id) return
  try {
    await documentPrint.printPdf(`payroll/payslips/${selectedPayslip.value.id}/pdf`)
  } catch (error) {
    feedback.failed('Could not print payslip PDF', error)
  }
}

async function printSalaryPayment(payment: any) {
  if (!payment?.id) return
  try {
    await documentPrint.printPdf(`salary-payments/${payment.id}/pdf`)
  } catch (error) {
    feedback.failed('Could not print salary payment PDF', error)
  }
}

async function downloadPayslip() {
  if (!selectedPayslip.value?.id) return
  try {
    const fileName = `payslip-${selectedPayslip.value.monthYear || selectedPayslip.value.id}.pdf`
    await documentPrint.downloadPdf(`payroll/payslips/${selectedPayslip.value.id}/pdf`, fileName)
    feedback.notify('Payslip PDF downloaded')
  } catch (error) {
    feedback.failed('Could not download payslip PDF', error)
  }
}

function sharePayslipEmail(payslip: any) {
  const subject = `Payslip ${payslip.monthYear} - ${payslip.employeeName}`
  const body = payslipShareText(payslip)
  const to = payslip.employeeEmail || ''
  window.location.href = `mailto:${encodeURIComponent(to)}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`
}

function sharePayslipWhatsApp(payslip: any) {
  const phone = String(payslip.employeeMobile || '').replace(/\D/g, '')
  const target = phone ? `https://wa.me/${phone}` : 'https://wa.me/'
  window.open(`${target}?text=${encodeURIComponent(payslipShareText(payslip))}`, '_blank', 'noopener,noreferrer')
}

function payslipShareText(payslip: any) {
  return [
    `Garmetix payslip for ${payslip.employeeName}`,
    `Month: ${payslip.monthYear}`,
    `Net salary: ${money(Number(payslip.netSalary || 0))}`,
    `Advance reduced: ${money(Number(payslip.salaryAdvance || 0))}`,
    `Carry forward due: ${money(Number(payslip.carryForwardDue || 0))}`,
    `Paid: ${money(Number(payslip.paidAmount || 0))}`,
    `Due: ${money(Number(payslip.dueAmount || 0))}`,
    'Open Garmetix Payroll to print or save the PDF.'
  ].join('\n')
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

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function salaryMonthFromDate(value: string) {
  const date = new Date(value)
  return Number(`${date.getFullYear()}${String(date.getMonth() + 1).padStart(2, '0')}`)
}

function previousMonthInput() {
  const today = new Date()
  return toMonthInput(new Date(today.getFullYear(), today.getMonth() - 1, 1))
}

function toMonthInput(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}`
}

function toDateInput(value: string) {
  return String(value || new Date().toISOString()).slice(0, 10)
}

function toApiDate(value: string) {
  return `${value}T00:00:00`
}

function toLocalDateInput(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(value || 0)
}

onMounted(async () => {
  auth.restore()
  await refresh()
  await autoGeneratePayrollIfDue()
})

watch(
  [() => paymentForm.employeeId, () => paymentForm.salaryMonth],
  ([employeeId, salaryMonth], [previousEmployeeId, previousSalaryMonth]) => {
    if (
      formOpen.value &&
      formKind.value === 'payment' &&
      !editingPaymentId.value &&
      employeeId &&
      salaryMonth &&
      (employeeId !== previousEmployeeId || salaryMonth !== previousSalaryMonth)
    ) {
      void precalculatePayment(false)
    }
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
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Payroll"
        description="Generate payslips, reduce salary advance, carry unpaid due, and manage salary payments."
        icon="i-lucide-badge-indian-rupee"
        :primary-label="primaryLabel"
        :primary-icon="primaryIcon"
        @primary="primaryAction"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.value }}</strong>
              <span>{{ metric.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div class="setup-tabs">
              <UButton
                v-for="tab in tabs"
                :key="tab.key"
                :icon="tab.icon"
                :color="activeTab === tab.key ? 'primary' : 'neutral'"
                :variant="activeTab === tab.key ? 'solid' : 'subtle'"
                :label="tab.label"
                @click="showTab(tab.key)"
              />
            </div>
            <UBadge color="neutral" variant="subtle">{{ currentRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          :search-placeholder="searchPlaceholder"
          :loading="loading"
          refresh-label="Sync"
          :create-label="primaryLabel"
          @refresh="refresh"
          @create="primaryAction"
        />

        <div v-if="activeTab === 'payslips'" class="payroll-generator">
          <UFormField label="Payroll month">
            <UInput v-model="periodForm.month" type="month" />
          </UFormField>
          <UButton
            icon="i-lucide-file-plus-2"
            label="Generate Month"
            :loading="generating"
            @click="generatePayslips()"
          />
          <UBadge color="neutral" variant="subtle">
            Auto runs on the 1st for previous month
          </UBadge>
        </div>

        <UTable
          v-if="currentRows.length"
          :data="currentRows"
          :columns="activeColumns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          :title="`No ${activeLabel.toLowerCase()} found`"
          description="Create salary structures first, then generate payslips for a month."
          icon="i-lucide-badge-indian-rupee"
          :action-label="primaryLabel"
          @action="primaryAction"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="formKind === 'structure' ? (editingStructureId ? 'Edit Salary Structure' : 'New Salary Structure') : (editingPaymentId ? 'Edit Salary Payment' : 'New Salary Payment')"
        :description="formKind === 'structure' ? 'Maintain salary components and deductions.' : 'Record employee salary payment details.'"
        :submit-label="formKind === 'structure' ? 'Save Structure' : 'Save Payment'"
        :submit-disabled="submitDisabled"
        :loading="saving"
        @submit="saveCurrentForm"
      >
        <template v-if="formKind === 'structure'">
          <UFormField label="Employee" required>
            <USelect v-model="structureForm.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="From date" required>
              <UInput v-model="structureForm.fromDate" required type="date" />
            </UFormField>
            <UFormField label="To date">
              <UInput v-model="structureForm.toDate" type="date" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Basic salary">
              <UInput v-model="structureForm.basicSalary" min="0" step="0.01" type="number" />
            </UFormField>
            <UFormField label="HRA">
              <UInput v-model="structureForm.hra" min="0" step="0.01" type="number" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Special allowance">
              <UInput v-model="structureForm.specialAllowance" min="0" step="0.01" type="number" />
            </UFormField>
            <UFormField label="Conveyance">
              <UInput v-model="structureForm.conveyanceAllowance" min="0" step="0.01" type="number" />
            </UFormField>
          </div>
          <UFormField label="Incentives">
            <UInput v-model="structureForm.incentives" min="0" step="0.01" type="number" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Provident fund">
              <UInput v-model="structureForm.providentFund" min="0" step="0.01" type="number" />
            </UFormField>
            <UFormField label="Gratuity">
              <UInput v-model="structureForm.gratuity" min="0" step="0.01" type="number" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Professional tax">
              <UInput v-model="structureForm.professionalTax" min="0" step="0.01" type="number" />
            </UFormField>
            <UFormField label="Deductions">
              <UInput v-model="structureForm.deductions" min="0" step="0.01" type="number" />
            </UFormField>
          </div>
          <UFormField label="Yearly bonus">
            <UInput v-model="structureForm.yearlyBonus" min="0" step="0.01" type="number" />
          </UFormField>
          <div class="payroll-summary">
            <span>Gross</span><strong>{{ money(structureGross) }}</strong>
            <span>Deductions</span><strong>{{ money(structureDeductions) }}</strong>
            <span>Net</span><strong>{{ money(structureNet) }}</strong>
          </div>
        </template>

        <template v-else>
          <UFormField label="Employee" required>
            <USelect v-model="paymentForm.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Voucher number" required>
              <UInput
                :model-value="editingPaymentId ? paymentForm.voucherNumber : 'Assigned on save'"
                disabled
              />
            </UFormField>
            <UFormField label="Salary month" required>
              <UInput v-model="paymentForm.salaryMonth" required type="number" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Payment date" required>
              <UInput v-model="paymentForm.onDate" required type="date" />
            </UFormField>
            <UFormField label="Pre-calculation">
              <UButton
                block
                color="neutral"
                icon="i-lucide-calculator"
                label="Recalculate"
                variant="soft"
                :loading="previewingPayment"
                @click="precalculatePayment()"
              />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Component">
              <USelect v-model="paymentForm.salaryComponent" :items="salaryComponentOptions" />
            </UFormField>
            <UFormField label="Payment mode">
              <USelect v-model="paymentForm.paymentMode" :items="paymentModeOptions" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Gross salary">
              <UInput v-model="paymentForm.grossSalary" min="0" step="0.01" type="number" />
            </UFormField>
            <UFormField label="Deductions including advance">
              <UInput v-model="paymentForm.totalDeductions" min="0" step="0.01" type="number" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Net salary">
              <UInput v-model="paymentForm.netSalary" min="0" step="0.01" type="number" />
            </UFormField>
            <UFormField label="Paid amount">
              <UInput
                v-model="paymentForm.amount"
                min="0"
                step="1"
                type="number"
                @blur="paymentForm.amount = Math.round(Number(paymentForm.amount || 0))"
              />
            </UFormField>
          </div>
          <UFormField label="Remarks">
            <UTextarea v-model="paymentForm.remarks" autoresize />
          </UFormField>
          <div class="payroll-summary">
            <span>Gross</span><strong>{{ money(Number(paymentForm.grossSalary || 0)) }}</strong>
            <span>Base deductions</span><strong>{{ money(Number(paymentForm.baseDeductions || 0)) }}</strong>
            <span>Advance deducted</span><strong>{{ money(Number(paymentForm.salaryAdvance || 0)) }}</strong>
            <span>Previous due added</span><strong>{{ money(Number(paymentForm.previousDue || 0)) }}</strong>
            <span>Net payable</span><strong>{{ money(Number(paymentForm.netSalary || 0)) }}</strong>
            <span>Already paid</span><strong>{{ money(Number(paymentForm.alreadyPaid || 0)) }}</strong>
            <span>Round off</span><strong>{{ money(Number(paymentForm.roundOff || 0)) }}</strong>
            <span>Balance after payment</span><strong>{{ money(paymentBalance) }}</strong>
          </div>
        </template>
      </UiFormSlideover>

      <UModal v-model:open="printOpen" title="Payslip" :ui="{ content: 'max-w-4xl' }">
        <template #body>
          <div v-if="printLoading" class="modal-loading">
            <UIcon name="i-lucide-loader-circle" class="animate-spin" />
            <span>Loading payslip</span>
          </div>

          <div v-else-if="printDetail" class="receipt-print payslip-print">
            <header class="receipt-header">
              <h2>{{ printDetail.companyName }}</h2>
              <p>{{ printDetail.companyAddress }}</p>
              <p>{{ printDetail.storeName || 'Payroll' }}</p>
              <p>Payslip {{ printDetail.summary.monthYear }}</p>
            </header>

            <div class="payslip-identity">
              <div>
                <span>Employee</span>
                <strong>{{ printDetail.summary.employeeName }}</strong>
              </div>
              <div>
                <span>Period</span>
                <strong>{{ formatDate(printDetail.summary.payPeriodStart) }} - {{ formatDate(printDetail.summary.payPeriodEnd) }}</strong>
              </div>
              <div>
                <span>Billable days</span>
                <strong>{{ Number(printDetail.summary.billableDays || 0).toFixed(1) }} / {{ Number(printDetail.summary.workingDays || 0).toFixed(1) }}</strong>
              </div>
              <div>
                <span>Status</span>
                <strong>{{ printDetail.summary.status }}</strong>
              </div>
            </div>

            <div class="payslip-columns">
              <table class="receipt-table">
                <thead>
                  <tr>
                    <th>Earnings</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  <tr><td>Basic salary</td><td>{{ money(Number(printDetail.basicSalary || 0)) }}</td></tr>
                  <tr><td>HRA</td><td>{{ money(Number(printDetail.hra || 0)) }}</td></tr>
                  <tr><td>Special allowance</td><td>{{ money(Number(printDetail.specialAllowance || 0)) }}</td></tr>
                  <tr><td>Conveyance</td><td>{{ money(Number(printDetail.conveyanceAllowance || 0)) }}</td></tr>
                  <tr><td>Incentives</td><td>{{ money(Number(printDetail.incentives || 0)) }}</td></tr>
                  <tr><td>Other earnings</td><td>{{ money(Number(printDetail.otherEarnings || 0)) }}</td></tr>
                </tbody>
              </table>

              <table class="receipt-table">
                <thead>
                  <tr>
                    <th>Deductions</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  <tr><td>Provident fund</td><td>{{ money(Number(printDetail.providentFund || 0)) }}</td></tr>
                  <tr><td>Gratuity</td><td>{{ money(Number(printDetail.gratuity || 0)) }}</td></tr>
                  <tr><td>Professional tax</td><td>{{ money(Number(printDetail.professionalTax || 0)) }}</td></tr>
                  <tr><td>Income tax</td><td>{{ money(Number(printDetail.incomeTax || 0)) }}</td></tr>
                  <tr><td>Deductions</td><td>{{ money(Number(printDetail.deductions || 0)) }}</td></tr>
                  <tr><td>Other deductions</td><td>{{ money(Number(printDetail.otherDeductions || 0)) }}</td></tr>
                </tbody>
              </table>
            </div>

            <div class="receipt-totals payslip-totals">
              <span>Total earnings</span><strong>{{ money(Number(printDetail.summary.totalEarnings || 0)) }}</strong>
              <span>Total deductions</span><strong>{{ money(Number(printDetail.summary.totalDeductions || 0)) }}</strong>
              <span>Net salary</span><strong>{{ money(Number(printDetail.summary.netSalary || 0)) }}</strong>
              <span>Carry forward due</span><strong>{{ money(Number(printDetail.summary.carryForwardDue || 0)) }}</strong>
              <span>Salary advance reduced</span><strong>{{ money(Number(printDetail.summary.salaryAdvance || 0)) }}</strong>
              <span>Paid amount</span><strong>{{ money(Number(printDetail.summary.paidAmount || 0)) }}</strong>
              <span>Due amount</span><strong>{{ money(Number(printDetail.summary.dueAmount || 0)) }}</strong>
            </div>

            <footer class="receipt-footer">
              {{ printDetail.remarks || 'Generated by Garmetix payroll.' }}
            </footer>
          </div>
        </template>

        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Close" @click="printOpen = false" />
            <UButton icon="i-lucide-mail" color="neutral" variant="subtle" label="Email" :disabled="!selectedPayslip" @click="selectedPayslip && sharePayslipEmail(selectedPayslip)" />
            <UButton icon="i-lucide-message-circle" color="success" variant="subtle" label="WhatsApp" :disabled="!selectedPayslip" @click="selectedPayslip && sharePayslipWhatsApp(selectedPayslip)" />
            <UButton icon="i-lucide-download" color="neutral" variant="subtle" label="Download PDF" :disabled="!selectedPayslip" @click="downloadPayslip" />
            <UButton icon="i-lucide-printer" label="Print / PDF" :disabled="!printDetail" @click="printPayslip" />
          </div>
        </template>
      </UModal>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        :title="formKind === 'payslip' ? 'Delete Payslip' : formKind === 'structure' ? 'Delete Salary Structure' : 'Delete Salary Payment'"
        :description="formKind === 'payslip'
          ? `Delete payslip for ${pendingDelete?.employeeName || ''}?`
          : formKind === 'structure'
            ? `Delete salary structure for ${pendingDelete ? employeeName(pendingDelete.employeeId) : ''}?`
            : `Delete salary payment ${pendingDelete?.voucherNumber || ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>

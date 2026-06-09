<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type ReportKind = 'sales' | 'purchase' | 'stock' | 'pettyCash' | 'attendance' | 'payroll'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const UBadge = resolveComponent('UBadge')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const salesInvoices = ref<any[]>([])
const purchaseInvoices = ref<any[]>([])
const products = ref<any[]>([])
const stocks = ref<any[]>([])
const pettyCashSheets = ref<any[]>([])
const employees = ref<any[]>([])
const monthlyAttendance = ref<any[]>([])
const payslips = ref<any[]>([])
const loading = ref(false)
const cacheStatus = ref('No cached snapshot loaded')
const cachedRows = ref<any[] | null>(null)
const search = ref('')
const reportKind = ref<ReportKind>('sales')
const today = new Date()
const filters = reactive({
  fromDate: new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10),
  toDate: today.toISOString().slice(0, 10)
})

const reportTabs = [
  { key: 'sales' as const, label: 'Sales', icon: 'i-lucide-receipt-indian-rupee' },
  { key: 'purchase' as const, label: 'Purchase', icon: 'i-lucide-package-plus' },
  { key: 'stock' as const, label: 'Stock', icon: 'i-lucide-boxes' },
  { key: 'pettyCash' as const, label: 'Petty Cash', icon: 'i-lucide-circle-dollar-sign' },
  { key: 'attendance' as const, label: 'Attendance', icon: 'i-lucide-calendar-days' },
  { key: 'payroll' as const, label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' }
]

const activeLabel = computed(() => reportTabs.find((item) => item.key === reportKind.value)?.label || 'Report')
const activeRows = computed(() => cachedRows.value || reportRows.value)

const salesRows = computed(() => filteredByDate(salesInvoices.value, 'onDate').map((invoice) => ({
  invoice: invoice.invoiceNumber || '-',
  date: formatDate(invoice.onDate),
  party: invoice.customerName || '-',
  amount: money(Number(invoice.billAmount || 0)),
  paid: money(Number(invoice.paidAmount || 0)),
  balance: money(Number(invoice.balanceAmount || 0)),
  status: invoice.invoiceStatus ?? 'Saved',
  rawAmount: Number(invoice.billAmount || 0),
  rawPaid: Number(invoice.paidAmount || 0),
  rawBalance: Number(invoice.balanceAmount || 0)
})))

const purchaseRows = computed(() => filteredByDate(purchaseInvoices.value, 'onDate').map((invoice) => ({
  invoice: invoice.invoiceNumber || '-',
  inward: invoice.inwardNumber || '-',
  date: formatDate(invoice.onDate),
  party: invoice.vendorName || '-',
  amount: money(Number(invoice.billAmount || invoice.totalAmount || invoice.netAmount || 0)),
  paid: money(Number(invoice.paidAmount || 0)),
  freight: money(Number(invoice.frightAmount || 0)),
  status: invoice.invoiceStatus ?? 'Saved',
  rawAmount: Number(invoice.billAmount || invoice.totalAmount || invoice.netAmount || 0),
  rawPaid: Number(invoice.paidAmount || 0),
  rawFreight: Number(invoice.frightAmount || 0)
})))

const stockRows = computed(() => products.value.map((product) => {
  const stock = stockFor(product.id)
  return {
    product: product.name || '-',
    barcode: product.barcode || '-',
    purchased: stock.purchaseQty,
    sold: stock.soldQty,
    current: stock.currentStock,
    mrp: money(Number(product.mrp || stock.mrp || 0)),
    value: money(stock.mrpValue),
    rawValue: stock.mrpValue
  }
}))

const pettyCashRows = computed(() => filteredByDate(pettyCashSheets.value, 'onDate').map((sheet) => ({
  date: formatDate(sheet.onDate),
  store: storeName(sheet.storeId),
  opening: money(Number(sheet.openingBalance || 0)),
  sales: money(Number(sheet.sales || 0)),
  receipts: money(Number(sheet.receipts || 0)),
  expenses: money(Number(sheet.expenses || 0)),
  payments: money(Number(sheet.payments || 0)),
  cash: money(Number(sheet.cashInHand || 0)),
  rawCash: Number(sheet.cashInHand || 0),
  rawSales: Number(sheet.sales || 0),
  rawExpenses: Number(sheet.expenses || 0)
})))

const attendanceRows = computed(() => filteredByDate(monthlyAttendance.value, 'onDate').map((row) => ({
  month: monthLabel(row.onDate),
  employee: employeeName(row.employeeId),
  present: Number(row.present || 0),
  halfDay: Number(row.halfDay || 0),
  absent: Number(row.absent || 0),
  workingDays: Number(row.noOfWorkingDays || 0),
  billableDays: Number(row.billableDays || 0),
  valid: row.valid ? 'Valid' : 'Check'
})))

const payrollRows = computed(() => filteredByDate(payslips.value, 'payPeriodStart').map((row) => ({
  month: row.monthYear || '-',
  employee: row.employeeName || employeeName(row.employeeId),
  net: money(Number(row.netSalary || 0)),
  advance: money(Number(row.salaryAdvance || 0)),
  paid: money(Number(row.paidAmount || 0)),
  due: money(Number(row.dueAmount || 0)),
  status: row.status || 'Due',
  rawNet: Number(row.netSalary || 0),
  rawDue: Number(row.dueAmount || 0)
})))

const reportRows = computed(() => {
  const rows = {
    sales: salesRows.value,
    purchase: purchaseRows.value,
    stock: stockRows.value,
    pettyCash: pettyCashRows.value,
    attendance: attendanceRows.value,
    payroll: payrollRows.value
  }[reportKind.value]
  const term = search.value.trim().toLowerCase()
  return term ? rows.filter((row) => JSON.stringify(row).toLowerCase().includes(term)) : rows
})

const metrics = computed(() => {
  if (reportKind.value === 'sales') {
    return metricSet('Sales', salesRows.value.length, sum(salesRows.value, 'rawAmount'), sum(salesRows.value, 'rawPaid'), sum(salesRows.value, 'rawBalance'))
  }
  if (reportKind.value === 'purchase') {
    return metricSet('Purchase', purchaseRows.value.length, sum(purchaseRows.value, 'rawAmount'), sum(purchaseRows.value, 'rawPaid'), sum(purchaseRows.value, 'rawFreight'))
  }
  if (reportKind.value === 'stock') {
    return [
      metric('Products', stockRows.value.length, 'Stock masters', 'i-lucide-boxes', 'primary'),
      metric('Current Qty', sum(stockRows.value, 'current'), 'Available quantity', 'i-lucide-warehouse', 'success'),
      metric('Sold Qty', sum(stockRows.value, 'sold'), 'Sold quantity', 'i-lucide-shopping-bag', 'neutral'),
      metric('Stock Value', money(sum(stockRows.value, 'rawValue')), 'MRP value', 'i-lucide-indian-rupee', 'warning')
    ]
  }
  if (reportKind.value === 'pettyCash') {
    return metricSet('Cash Sheets', pettyCashRows.value.length, sum(pettyCashRows.value, 'rawSales'), sum(pettyCashRows.value, 'rawExpenses'), sum(pettyCashRows.value, 'rawCash'))
  }
  if (reportKind.value === 'attendance') {
    return [
      metric('Rows', attendanceRows.value.length, 'Monthly attendance', 'i-lucide-calendar-days', 'primary'),
      metric('Present', sum(attendanceRows.value, 'present'), 'Present days', 'i-lucide-user-check', 'success'),
      metric('Absent', sum(attendanceRows.value, 'absent'), 'Absent days', 'i-lucide-user-x', 'warning'),
      metric('Billable', sum(attendanceRows.value, 'billableDays').toFixed(1), 'Payroll days', 'i-lucide-clock', 'neutral')
    ]
  }
  return [
    metric('Payslips', payrollRows.value.length, 'Generated slips', 'i-lucide-file-text', 'primary'),
    metric('Net Salary', money(sum(payrollRows.value, 'rawNet')), 'Net payable', 'i-lucide-indian-rupee', 'success'),
    metric('Due Salary', money(sum(payrollRows.value, 'rawDue')), 'Open dues', 'i-lucide-alert-circle', 'warning'),
    metric('Paid Rows', payrollRows.value.filter((row) => row.status === 'Paid').length, 'Cleared payslips', 'i-lucide-check-circle', 'neutral')
  ]
})

const columns = computed<TableColumn<any>[]>(() => {
  if (reportKind.value === 'sales') {
    return moneyColumns(['invoice', 'date', 'party', 'amount', 'paid', 'balance', 'status'])
  }
  if (reportKind.value === 'purchase') {
    return moneyColumns(['invoice', 'inward', 'date', 'party', 'amount', 'paid', 'freight', 'status'])
  }
  if (reportKind.value === 'stock') {
    return [
      { accessorKey: 'product', header: 'Product' },
      { accessorKey: 'barcode', header: 'Barcode' },
      { accessorKey: 'purchased', header: 'Purchased' },
      { accessorKey: 'sold', header: 'Sold' },
      badgeColumn('current', 'Stock'),
      { accessorKey: 'mrp', header: 'MRP' },
      { accessorKey: 'value', header: 'Value' }
    ]
  }
  if (reportKind.value === 'pettyCash') {
    return moneyColumns(['date', 'store', 'opening', 'sales', 'receipts', 'expenses', 'payments', 'cash'])
  }
  if (reportKind.value === 'attendance') {
    return [
      { accessorKey: 'month', header: 'Month' },
      { accessorKey: 'employee', header: 'Employee' },
      { accessorKey: 'present', header: 'Present' },
      { accessorKey: 'halfDay', header: 'Half' },
      { accessorKey: 'absent', header: 'Absent' },
      { accessorKey: 'workingDays', header: 'Working' },
      { accessorKey: 'billableDays', header: 'Billable' },
      badgeColumn('valid', 'Status')
    ]
  }
  return moneyColumns(['month', 'employee', 'net', 'advance', 'paid', 'due', 'status'])
})

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, storeRows, salesRowsData, purchaseRowsData, productRows, stockRowsData, cashRows, employeeRows, monthlyRows, payslipRowsData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('sales-invoices'),
      api.list<any>('purchase-invoices'),
      api.list<any>('products'),
      api.list<any>('stocks'),
      api.list<any>('petty-cash-sheets'),
      api.list<any>('employees'),
      api.list<any>('monthly-attendance'),
      api.get<any[]>('payroll/payslips/recent?take=500')
    ])

    companies.value = companyRows
    stores.value = storeRows
    salesInvoices.value = salesRowsData
    purchaseInvoices.value = purchaseRowsData
    products.value = productRows
    stocks.value = stockRowsData
    pettyCashSheets.value = cashRows
    employees.value = employeeRows
    monthlyAttendance.value = monthlyRows
    payslips.value = payslipRowsData
  } catch (error) {
    feedback.failed('Reports refresh failed', error)
  } finally {
    loading.value = false
  }
}

function showReport(kind: ReportKind) {
  reportKind.value = kind
  search.value = ''
  clearReportCache()
}

function filteredByDate(rows: any[], field: string) {
  const from = new Date(`${filters.fromDate}T00:00:00`)
  const to = new Date(`${filters.toDate}T23:59:59`)
  return rows.filter((row) => {
    const value = row[field]
    if (!value) {
      return true
    }
    const date = new Date(value)
    return date >= from && date <= to
  })
}

function exportCsv() {
  const rows = activeRows.value
  if (!rows.length) {
    feedback.failed('No report rows to export')
    return
  }

  const keys = columns.value.map((column: any) => column.accessorKey).filter(Boolean)
  const csv = [
    keys.join(','),
    ...rows.map((row: any) => keys.map((key: string) => csvCell(row[key])).join(','))
  ].join('\n')
  downloadBlob(new Blob([csv], { type: 'text/csv;charset=utf-8' }), `Garmetix-${activeLabel.value}-Report.csv`)
  saveReportCache()
  feedback.notify('Report exported', `${activeLabel.value} CSV downloaded.`)
}

function exportExcel() {
  const rows = activeRows.value
  if (!rows.length) {
    feedback.failed('No report rows to export')
    return
  }

  const keys = columns.value.map((column: any) => column.accessorKey).filter(Boolean)
  const html = `<!doctype html><html><head><meta charset="utf-8"></head><body><table border="1"><thead><tr>${keys.map((key: string) => `<th>${escapeHtml(titleCase(key))}</th>`).join('')}</tr></thead><tbody>${rows.map((row: any) => `<tr>${keys.map((key: string) => `<td>${escapeHtml(row[key])}</td>`).join('')}</tr>`).join('')}</tbody></table></body></html>`
  downloadBlob(new Blob([html], { type: 'application/vnd.ms-excel;charset=utf-8' }), `Garmetix-${activeLabel.value}-Report.xls`)
  saveReportCache()
  feedback.notify('Report exported', `${activeLabel.value} Excel downloaded.`)
}

function exportPdf() {
  saveReportCache()
  window.print()
  feedback.notify('Report PDF', 'Use the browser print dialog and choose Save as PDF.')
}

function printReport() {
  saveReportCache()
  window.print()
}

function saveReportCache() {
  const rows = reportRows.value
  const payload = {
    label: activeLabel.value,
    kind: reportKind.value,
    filters: { ...filters },
    search: search.value,
    rows,
    cachedAt: new Date().toISOString()
  }
  localStorage.setItem(reportCacheKey(), JSON.stringify(payload))
  cacheStatus.value = `Cached ${rows.length} rows at ${new Date(payload.cachedAt).toLocaleString()}`
}

function loadReportCache() {
  try {
    const raw = localStorage.getItem(reportCacheKey())
    if (!raw) {
      cachedRows.value = null
      cacheStatus.value = 'No cached snapshot for this report/filter.'
      return
    }
    const payload = JSON.parse(raw)
    cachedRows.value = payload.rows || []
    cacheStatus.value = `Loaded cached ${payload.rows?.length || 0} rows from ${new Date(payload.cachedAt).toLocaleString()}`
  } catch (error) {
    cachedRows.value = null
    feedback.failed('Report cache load failed', error)
  }
}

function clearReportCache() {
  cachedRows.value = null
  cacheStatus.value = 'Live report data active.'
}

function reportCacheKey() {
  return `garmetix.report.${reportKind.value}.${filters.fromDate}.${filters.toDate}.${search.value.trim().toLowerCase()}`
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function escapeHtml(value: any) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;')
}

function moneyColumns(keys: string[]) {
  return keys.map((key) => key === 'status' ? badgeColumn(key, titleCase(key)) : {
    accessorKey: key,
    header: titleCase(key)
  })
}

function badgeColumn(key: string, header: string): TableColumn<any> {
  return {
    accessorKey: key,
    header,
    cell: ({ row }) => h(UBadge, {
      color: badgeColor(row.original[key]),
      variant: 'subtle'
    }, () => String(row.original[key]))
  }
}

function metricSet(label: string, count: number, amount: number, paid: number, balance: number) {
  return [
    metric(label, count, 'Rows in range', 'i-lucide-file-text', 'primary'),
    metric('Amount', money(amount), 'Total value', 'i-lucide-indian-rupee', 'success'),
    metric('Paid / Used', money(paid), 'Paid or expense value', 'i-lucide-credit-card', 'neutral'),
    metric('Balance', money(balance), 'Balance or remaining', 'i-lucide-scale', 'warning')
  ]
}

function metric(label: string, value: any, meta: string, icon: string, color: string) {
  return { label, value, meta, icon, color }
}

function stockFor(productId: string) {
  const productStocks = stocks.value.filter((stock) => stock.productId === productId)
  const purchaseQty = productStocks.reduce((sum, stock) => sum + Number(stock.purchaseQty || 0), 0)
  const soldQty = productStocks.reduce((sum, stock) => sum + Number(stock.soldQty || 0), 0)
  const mrpValue = productStocks.reduce((sum, stock) => sum + ((Number(stock.purchaseQty || 0) - Number(stock.soldQty || 0)) * Number(stock.mrp || 0)), 0)
  return {
    purchaseQty,
    soldQty,
    currentStock: purchaseQty - soldQty,
    mrpValue,
    mrp: productStocks[0]?.mrp || 0
  }
}

function storeName(storeId: string) {
  return stores.value.find((store) => store.id === storeId)?.name || 'Store'
}

function employeeName(employeeId: string) {
  const employee = employees.value.find((item) => item.id === employeeId)
  return employee ? `${employee.firstName} ${employee.lastName}`.trim() : 'Employee'
}

function sum(rows: any[], key: string) {
  return rows.reduce((total, row) => total + Number(row[key] || 0), 0)
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function monthLabel(value: string) {
  return value ? new Date(value).toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) : '-'
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(value || 0)
}

function titleCase(value: string) {
  return value.replace(/([A-Z])/g, ' $1').replace(/^./, (text) => text.toUpperCase())
}

function badgeColor(value: any) {
  const text = String(value || '').toLowerCase()
  if (text.includes('paid') || text.includes('valid') || Number(value) > 0) {
    return 'success'
  }
  if (text.includes('due') || text.includes('check') || Number(value) < 0) {
    return 'warning'
  }
  return 'neutral'
}

function csvCell(value: any) {
  const text = String(value ?? '')
  return text.includes(',') || text.includes('"') || text.includes('\n')
    ? `"${text.replaceAll('"', '""')}"`
    : text
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
    title="Reports"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard report-print">
      <UiModulePageHeader
        title="Reports"
        description="Review sales, purchase, stock, cash, attendance, and payroll reports."
        icon="i-lucide-file-text"
        primary-label="Export CSV"
        primary-icon="i-lucide-download"
        @primary="exportCsv"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton icon="i-lucide-file-spreadsheet" color="success" variant="subtle" label="Excel" @click="exportExcel" />
          <UButton icon="i-lucide-file-down" color="warning" variant="subtle" label="PDF" @click="exportPdf" />
          <UButton icon="i-lucide-printer" color="neutral" variant="subtle" label="Print" @click="printReport" />
        </template>
      </UiModulePageHeader>

      <UCard class="planner-card no-print">
        <div class="report-filter-bar">
          <div class="setup-tabs">
            <UButton
              v-for="tab in reportTabs"
              :key="tab.key"
              :icon="tab.icon"
              :color="reportKind === tab.key ? 'primary' : 'neutral'"
              :variant="reportKind === tab.key ? 'solid' : 'subtle'"
              :label="tab.label"
              @click="showReport(tab.key)"
            />
          </div>
          <div class="report-date-filters">
            <UFormField label="From">
              <UInput v-model="filters.fromDate" type="date" />
            </UFormField>
            <UFormField label="To">
              <UInput v-model="filters.toDate" type="date" />
            </UFormField>
          </div>
        </div>
      </UCard>

      <div class="planner-metric-grid">
        <UCard v-for="metricItem in metrics" :key="metricItem.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metricItem.icon" :color="metricItem.color" variant="subtle" />
            <div>
              <p>{{ metricItem.label }}</p>
              <strong>{{ metricItem.value }}</strong>
              <span>{{ metricItem.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>{{ activeLabel }} Report</h3>
              <p>{{ filters.fromDate }} to {{ filters.toDate }}</p>
            </div>
            <div class="setup-tabs">
              <UBadge color="neutral" variant="subtle">{{ activeRows.length }} rows</UBadge>
              <UBadge v-if="cachedRows" color="warning" variant="subtle">Cached</UBadge>
            </div>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search report rows"
          :loading="loading"
          refresh-label="Sync"
          create-label="Export CSV"
          @refresh="refresh"
          @create="exportCsv"
        />

        <div class="report-cache-bar no-print">
          <span>{{ cacheStatus }}</span>
          <div class="button-row compact">
            <UButton size="sm" icon="i-lucide-save" color="neutral" variant="subtle" label="Cache" @click="saveReportCache" />
            <UButton size="sm" icon="i-lucide-database" color="warning" variant="subtle" label="Load Cache" @click="loadReportCache" />
            <UButton size="sm" icon="i-lucide-rotate-ccw" color="neutral" variant="ghost" label="Live" :disabled="!cachedRows" @click="clearReportCache" />
          </div>
        </div>

        <UTable
          v-if="activeRows.length"
          :data="activeRows"
          :columns="columns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          title="No report data"
          description="Adjust filters or sync data to load this report."
          icon="i-lucide-file-text"
          action-label="Refresh"
          @action="refresh"
        />
      </UCard>
    </section>
  </AppShell>
</template>

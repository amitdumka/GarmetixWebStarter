<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const auth = useAuth()

const loading = ref(false)
const data = ref<any | null>(null)
const preferences = useDashboardPreferences('business-dashboard')
const lastRefreshedAt = ref<string | null>(null)
let autoRefreshTimer: ReturnType<typeof setInterval> | undefined
const companies = ref<any[]>([])
const stores = ref<any[]>([])

const companyDashboardTitle = computed(() => data.value?.scope?.companyName || 'Company Dashboard')
const companyDashboardSubtitle = computed(() => {
  const parts = [data.value?.period?.label || preferences.rangeLabel.value, data.value?.scope?.storeGroupName, data.value?.scope?.storeName].filter(Boolean)
  return parts.length ? parts.join(' · ') : 'Company, store-group and store performance overview.'
})

const scopeQuery = computed(() => {
  const params = new URLSearchParams()
  if (workspace.companyId.value) params.set('companyId', workspace.companyId.value)
  if (workspace.storeGroupId.value) params.set('storeGroupId', workspace.storeGroupId.value)
  if (workspace.storeId.value) params.set('storeId', workspace.storeId.value)
  preferences.toQueryParams(params)
  const query = params.toString()
  return query ? `?${query}` : ''
})

const userRole = computed(() => auth.user.value?.role || auth.user.value?.userType || 'User')

const storeColumns = [
  { key: 'storeName', label: 'Store' },
  { key: 'salesMonth', label: 'Sales', type: 'money' },
  { key: 'purchaseMonth', label: 'Purchase', type: 'money' },
  { key: 'stockValue', label: 'Stock Value', type: 'money' },
  { key: 'invoiceCount', label: 'Invoices', type: 'number' },
  { key: 'currentStockQty', label: 'Stock Qty', type: 'number' }
]


const dueColumns = [
  { key: 'partyName', label: 'Name' },
  { key: 'billCount', label: 'Bills', type: 'number' },
  { key: 'billAmount', label: 'Bill Amount', type: 'money' },
  { key: 'paidAmount', label: 'Paid', type: 'money' },
  { key: 'dueAmount', label: 'Due', type: 'money' },
  { key: 'ageBucket', label: 'Age' }
]

const paymentModeColumns = [
  { key: 'paymentMode', label: 'Mode' },
  { key: 'salesCollection', label: 'Sales Collection', type: 'money' },
  { key: 'purchasePayment', label: 'Purchase Payment', type: 'money' },
  { key: 'voucherReceipt', label: 'Voucher Receipt', type: 'money' },
  { key: 'voucherPayment', label: 'Voucher Payment', type: 'money' },
  { key: 'netAmount', label: 'Net', type: 'money' },
  { key: 'transactionCount', label: 'Rows', type: 'number' }
]

const storeGroupComparisonColumns = [
  { key: 'storeGroupName', label: 'Store Group' },
  { key: 'storeCount', label: 'Stores', type: 'number' },
  { key: 'sales', label: 'Sales', type: 'money' },
  { key: 'purchase', label: 'Purchase', type: 'money' },
  { key: 'customerDue', label: 'Customer Due', type: 'money' },
  { key: 'vendorDue', label: 'Vendor Due', type: 'money' },
  { key: 'cashIn', label: 'In', type: 'money' },
  { key: 'cashOut', label: 'Out', type: 'money' },
  { key: 'netCash', label: 'Net Cash', type: 'money' }
]

const cashBreakdownItems = computed(() => [
  { label: 'Cash In', value: data.value?.cashPaymentSummary?.cashIn || 0, displayValue: money(data.value?.cashPaymentSummary?.cashIn || 0), color: 'success', icon: 'i-lucide-arrow-down-left', caption: 'Cash sale collections and cash receipts.' },
  { label: 'Cash Out', value: data.value?.cashPaymentSummary?.cashOut || 0, displayValue: money(data.value?.cashPaymentSummary?.cashOut || 0), color: 'warning', icon: 'i-lucide-arrow-up-right', caption: 'Cash purchase payments and cash vouchers.' },
  { label: 'Bank / Digital In', value: data.value?.cashPaymentSummary?.bankIn || 0, displayValue: money(data.value?.cashPaymentSummary?.bankIn || 0), color: 'primary', icon: 'i-lucide-landmark', caption: 'Card, UPI, bank and wallet inflows.' },
  { label: 'Bank / Digital Out', value: data.value?.cashPaymentSummary?.bankOut || 0, displayValue: money(data.value?.cashPaymentSummary?.bankOut || 0), color: 'neutral', icon: 'i-lucide-send', caption: 'Bank, UPI and non-cash outflows.' },
  { label: 'Net Cash Flow', value: data.value?.cashPaymentSummary?.netCash || 0, displayValue: money(data.value?.cashPaymentSummary?.netCash || 0), color: (data.value?.cashPaymentSummary?.netCash || 0) >= 0 ? 'success' : 'error', icon: 'i-lucide-wallet', caption: 'Total collections less total payments.' }
])

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
}

const storeGroupColumns = [
  { key: 'storeGroupName', label: 'Store Group' },
  { key: 'storeCount', label: 'Stores', type: 'number' },
  { key: 'salesMonth', label: 'Sales', type: 'money' },
  { key: 'purchaseMonth', label: 'Purchase', type: 'money' },
  { key: 'stockValue', label: 'Stock Value', type: 'money' },
  { key: 'invoiceCount', label: 'Invoices', type: 'number' },
  { key: 'currentStockQty', label: 'Stock Qty', type: 'number' }
]


const exportTables = computed(() => [
  { name: 'Store Performance', rows: data.value?.stores || [], columns: storeColumns },
  { name: 'Store Group Performance', rows: data.value?.storeGroups || [], columns: storeGroupColumns },
  { name: 'Store Group Cash / Due Comparison', rows: data.value?.storeGroupComparison || [], columns: storeGroupComparisonColumns },
  { name: 'Customer Dues', rows: data.value?.customerDues || [], columns: dueColumns },
  { name: 'Vendor Dues', rows: data.value?.vendorDues || [], columns: dueColumns },
  { name: 'Payment Mode Summary', rows: data.value?.cashPaymentSummary?.paymentModes || [], columns: paymentModeColumns },
  { name: 'Recent Sales', rows: data.value?.recentSales || [], columns: [
    { key: 'title', label: 'Invoice' },
    { key: 'description', label: 'Customer / Details' },
    { key: 'amount', label: 'Amount' },
    { key: 'date', label: 'Date' }
  ]},
  { name: 'Recent Purchases', rows: data.value?.recentPurchases || [], columns: [
    { key: 'title', label: 'Invoice' },
    { key: 'description', label: 'Vendor / Details' },
    { key: 'amount', label: 'Amount' },
    { key: 'date', label: 'Date' }
  ]}
])

async function refresh() {
  loading.value = true
  try {
    const [dashboard, companyRows, storeRows] = await Promise.all([
      api.get<any>(`dashboard/business${scopeQuery.value}`),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    data.value = dashboard
    companies.value = companyRows
    stores.value = storeRows
    lastRefreshedAt.value = new Date().toISOString()
  } catch (error) {
    feedback.failed('Business dashboard load failed', error)
  } finally {
    loading.value = false
  }
}

function configureAutoRefresh() {
  if (autoRefreshTimer) {
    clearInterval(autoRefreshTimer)
    autoRefreshTimer = undefined
  }
  if (!preferences.autoRefresh.value) return
  autoRefreshTimer = setInterval(() => {
    if (!loading.value) refresh()
  }, Math.max(30, Number(preferences.refreshIntervalSeconds.value || 60)) * 1000)
}

function applyPreset(value: string) {
  preferences.applyPreset(value as any)
  refresh()
}

function setRangeKey(value: string) {
  preferences.rangeKey.value = value as any
  preferences.save()
}

function setFromDate(value: string) {
  preferences.fromDate.value = value
  preferences.rangeKey.value = 'custom'
  preferences.save()
}

function setToDate(value: string) {
  preferences.toDate.value = value
  preferences.rangeKey.value = 'custom'
  preferences.save()
}


watch([preferences.autoRefresh, preferences.refreshIntervalSeconds], () => {
  preferences.save()
  configureAutoRefresh()
})

onMounted(() => {
  preferences.load()
  refresh()
  configureAutoRefresh()
})

onBeforeUnmount(() => {
  if (autoRefreshTimer) clearInterval(autoRefreshTimer)
})
</script>

<template>
  <AppShell :title="companyDashboardTitle" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <DashboardPageHero
        badge="Company"
        badge-icon="i-lucide-building-2"
        :title="companyDashboardTitle"
        :subtitle="companyDashboardSubtitle"
        :loading="loading"
        business
        @refresh="refresh"
      />

      <DashboardFilterBar
        :range-key="preferences.rangeKey.value"
        @update:range-key="setRangeKey"
        :from-date="preferences.fromDate.value"
        @update:from-date="setFromDate"
        :to-date="preferences.toDate.value"
        @update:to-date="setToDate"
        :auto-refresh="preferences.autoRefresh.value"
        @update:auto-refresh="preferences.autoRefresh.value = $event"
        :refresh-interval-seconds="preferences.refreshIntervalSeconds.value"
        @update:refresh-interval-seconds="preferences.refreshIntervalSeconds.value = $event"
        title="Dashboard period and refresh"
        :preset-options="preferences.presetOptions"
        :refresh-interval-options="preferences.refreshIntervalOptions"
        :last-refreshed-at="lastRefreshedAt"
        :loading="loading"
        @apply-preset="applyPreset"
        @refresh="refresh"
      />

      <DashboardMetricGrid :metrics="data?.metrics || []" :loading="loading" business />

      <DashboardExportActions
        :title="companyDashboardTitle"
        file-name="company-dashboard"
        :data="data"
        :tables="exportTables"
        :loading="loading"
      />

      <div class="dashboard-v3-insight-grid dashboard-v3-breakdown-grid">
        <DashboardBreakdownGrid
          title="Revenue split"
          description="Regular GST sales and separate Non-GST sales across the selected scope."
          :items="data?.revenueBreakdown || []"
        />
        <DashboardBreakdownGrid
          title="Stock valuation split"
          description="On-book and Non-GST stock valuation across permitted stores."
          :items="data?.stockBreakdown || []"
        />
        <DashboardBreakdownGrid
          title="Profit split"
          description="GST and Non-GST operational margin for the selected period."
          :items="data?.profitBreakdown || []"
        />
      </div>

      <div class="dashboard-v3-insight-grid dashboard-v3-breakdown-grid">
        <DashboardBreakdownGrid
          title="Cash and payment summary"
          description="Cash, bank and digital collection/payment movement for the selected period."
          :items="cashBreakdownItems"
        />
        <DashboardDataTable
          title="Customer dues"
          description="Top customer outstanding balances across permitted stores."
          :rows="data?.customerDues || []"
          :columns="dueColumns"
        />
        <DashboardDataTable
          title="Vendor dues"
          description="Top vendor payable balances after recorded purchase payments."
          :rows="data?.vendorDues || []"
          :columns="dueColumns"
        />
      </div>

      <div class="dashboard-v3-insight-grid">
        <DashboardActionGrid
          title="Executive actions"
          description="Role-aware shortcuts for owner, admin and accountant users."
          :actions="data?.quickActions || []"
        />

        <DashboardHealthGrid
          title="Company health"
          description="Company and store-group level signals."
          :signals="data?.healthSignals || []"
        />
      </div>

      <div class="dashboard-v3-grid business">
        <DashboardTrendChart
          title="7-day company trend"
          description="Sales and purchase across permitted stores."
          :points="data?.trend || []"
        />

        <DashboardDataTable
          title="Store performance"
          description="Current month sales, purchase and stock valuation."
          :rows="data?.stores || []"
          :columns="storeColumns"
        />

        <DashboardDataTable
          title="Store group performance"
          description="Group-wise sales, purchase and stock visibility."
          :rows="data?.storeGroups || []"
          :columns="storeGroupColumns"
        />

        <DashboardDataTable
          title="Store group cash / due comparison"
          description="Compare sales, purchases, dues, collections, payments and stock by store group."
          :rows="data?.storeGroupComparison || []"
          :columns="storeGroupComparisonColumns"
        />

        <DashboardDataTable
          title="Payment mode summary"
          description="Sales collections, purchase payments and accounting voucher movement by mode."
          :rows="data?.cashPaymentSummary?.paymentModes || []"
          :columns="paymentModeColumns"
        />

        <DashboardItemList
          title="Admin queue"
          description="Control checkpoints and reports."
          :items="data?.adminQueue || []"
          icon="i-lucide-arrow-up-right"
          item-to="dashboard"
          empty-title="No admin queue"
          empty-description="No pending dashboard control checkpoints for this scope."
        />

        <DashboardItemList
          title="Recent sales"
          description="Latest billing activity."
          :items="data?.recentSales || []"
          icon="i-lucide-receipt-indian-rupee"
          :show-date="true"
          empty-title="No recent sales"
          empty-description="Recent sales will appear after billing activity is recorded."
        />

        <DashboardItemList
          title="Recent purchases"
          description="Latest inward activity."
          :items="data?.recentPurchases || []"
          icon="i-lucide-package-plus"
          :show-date="true"
          empty-title="No recent purchases"
          empty-description="Recent purchases will appear after inward activity is recorded."
        />
      </div>
    </section>
  </AppShell>
</template>

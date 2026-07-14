<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()

const ALL_STORES_VALUE = '__ALL_STORES__'
const loading = ref(false)
const loadError = ref('')
const report = ref<any | null>(null)
const stores = ref<any[]>([])
const applyingInvoiceIds = ref<Set<string>>(new Set())

const filters = reactive({
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE,
  search: '',
  onlyIssues: true,
  page: 1,
  pageSize: 100
})

const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id }))
])

const summaryCards = computed(() => {
  const row = report.value?.summary || {}
  return [
    { label: 'Invoices', value: row.invoiceCount || 0, hint: `${row.itemCount || 0} items reviewed`, icon: 'i-lucide-receipt-text', color: 'primary' },
    { label: 'GST issues', value: row.issueCount || 0, hint: 'Threshold mismatches', icon: 'i-lucide-triangle-alert', color: (row.issueCount || 0) > 0 ? 'warning' : 'success' },
    { label: 'Extra amount', value: money(row.extraAmountToReview || 0), hint: 'Review before posting', icon: 'i-lucide-badge-indian-rupee', color: 'warning' },
    { label: 'Profit / Loss', value: money(row.profitAmount || 0), hint: `${num(row.profitPercentage || 0)}% gross margin`, icon: 'i-lucide-chart-no-axes-combined', color: Number(row.profitAmount || 0) < 0 ? 'error' : 'success' },
    { label: 'Sales excl. GST', value: money(row.taxableAmount || 0), hint: `Cost ${money(row.costAmount || 0)}`, icon: 'i-lucide-banknote', color: 'neutral' },
    { label: 'Tax difference', value: money(row.taxDifferenceAmount || 0), hint: `Expected tax ${money(row.expectedTaxAmount || 0)}`, icon: 'i-lucide-percent', color: Math.abs(Number(row.taxDifferenceAmount || 0)) > 0 ? 'warning' : 'success' }
  ]
})

const issueRows = computed(() => (report.value?.items || []).filter((item: any) => item.status !== 'OK'))
const totalPages = computed(() => Math.max(1, Math.ceil((report.value?.totalItems || 0) / filters.pageSize)))

async function refresh(resetPage = false) {
  if (resetPage) filters.page = 1
  loading.value = true
  loadError.value = ''
  try {
    const query = new URLSearchParams({
      fromDate: filters.fromDate,
      toDate: filters.toDate,
      onlyIssues: String(filters.onlyIssues),
      page: String(filters.page),
      pageSize: String(filters.pageSize)
    })
    if (filters.storeId && filters.storeId !== ALL_STORES_VALUE) query.set('storeId', filters.storeId)
    if (filters.search.trim()) query.set('search', filters.search.trim())
    const [storeRows, result] = await Promise.all([
      api.list<any>('stores'),
      api.get<any>(`sale-review?${query}`)
    ])
    stores.value = storeRows || []
    report.value = result
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check sale invoice records and stock movement cost posting.', 'Sale review load failed')
  } finally {
    loading.value = false
  }
}

function money(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function num(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function date(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function statusColor(status: string) {
  if (status === 'OK') return 'success'
  if (status?.includes('18%')) return 'warning'
  if (status?.includes('5%')) return 'error'
  return 'neutral'
}

function invoiceUrl(id: string) {
  return `/billing?invoiceId=${id}`
}

function copyIssueSummary() {
  const rows = issueRows.value
  if (!rows.length) {
    feedback.notify('No GST issue rows', 'No issue rows are visible for the selected filters.')
    return
  }
  const text = rows.map((item: any) => [
    item.invoiceNumber,
    date(item.onDate),
    item.customerName,
    item.productName,
    item.barcode,
    `Basic ${money(item.unitBasicPrice)}`,
    `GST ${item.taxPercentage}% → expected ${item.expectedTaxPercentage}%`,
    `Extra ${money(item.extraAmountToReview)}`,
    item.suggestedAction
  ].join(' | ')).join('\n')
  navigator.clipboard?.writeText(text)
  feedback.notify('Copied GST issue rows', `${rows.length} row(s) copied for accountant review.`)
}


async function applyOptionB(invoice: any) {
  if (!invoice?.invoiceId) return
  const extra = Number(invoice.extraAmountToReview || 0)
  if (!invoice.issueCount || extra <= 0) {
    feedback.notify('No adjustment needed', 'This invoice has no 18% below-threshold extra amount to post.')
    return
  }
  const ok = window.confirm(`Apply Option B to ${invoice.invoiceNumber}?\n\nThis will correct eligible 18% item(s) to 5%, reduce the invoice close to the corrected total, and create an Extra Amount receipt voucher dated ${date(invoice.onDate)} for approx. ${money(extra)}. This cannot be casually undone.`)
  if (!ok) return

  const next = new Set(applyingInvoiceIds.value)
  next.add(invoice.invoiceId)
  applyingInvoiceIds.value = next
  try {
    const result = await api.create<any>('sale-review/adjustments/apply', {
      invoiceId: invoice.invoiceId,
      invoiceItemIds: null,
      employeeId: null,
      remarks: 'Applied from Sale Review page using Option B',
      dryRun: false
    } as any)
    feedback.notify('Sale Review adjustment posted', result?.message || `Invoice ${invoice.invoiceNumber} corrected and Extra Amount voucher created.`)
    await refresh(true)
  } catch (error) {
    feedback.notify('Sale Review adjustment failed', feedback.errorMessage(error, 'Check employee/accounting setup and try again.', 'Adjustment failed'), 'error')
  } finally {
    const done = new Set(applyingInvoiceIds.value)
    done.delete(invoice.invoiceId)
    applyingInvoiceIds.value = done
  }
}

function isApplying(invoiceId: string) {
  return applyingInvoiceIds.value.has(invoiceId)
}

function exportCsv() {
  const rows = report.value?.items || []
  const header = ['Invoice', 'Date', 'Store', 'Customer', 'Mobile', 'Product', 'Barcode', 'Qty', 'Unit Basic', 'Actual GST', 'Expected GST', 'Tax', 'Expected Tax', 'Extra Amount', 'Cost', 'Profit', 'Profit %', 'Status']
  const lines = rows.map((item: any) => [
    item.invoiceNumber,
    date(item.onDate),
    item.storeName,
    item.customerName,
    item.customerMobileNumber,
    item.productName,
    item.barcode,
    item.quantity,
    item.unitBasicPrice,
    item.taxPercentage,
    item.expectedTaxPercentage,
    item.taxAmount,
    item.expectedTaxAmount,
    item.extraAmountToReview,
    item.costAmount,
    item.profitAmount,
    item.profitPercentage,
    item.status
  ].map((value) => `"${String(value ?? '').replaceAll('"', '""')}"`).join(','))
  const blob = new Blob([[header.join(','), ...lines].join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `sale-review-${filters.fromDate}-to-${filters.toDate}.csv`
  anchor.click()
  URL.revokeObjectURL(url)
}

watch(() => [filters.page, filters.pageSize], () => refresh())
onMounted(() => refresh(true))
</script>

<template>
  <AppShell title="Sale Review" @refresh="refresh(true)">
    <UiModulePageHeader
      title="Sale Review"
      description="Review apparel GST threshold mistakes and see invoice-wise and item-wise profit/loss from sale invoices. 5% applies up to ₹2,499 basic value per item; 18% applies from ₹2,499.01 and above. Confirm final corrections with your accountant before posting adjustments."
      icon="i-lucide-clipboard-check"
    />

    <UAlert
      class="mt-4"
      color="warning"
      variant="subtle"
      icon="i-lucide-info"
      title="Option B adjustment guidance"
      description="Use Apply Option B only after review: eligible 18% below-threshold items are corrected to 5%, invoice total is reduced near the corrected amount, and the difference is posted as an Extra Amount receipt voucher dated as the invoice date with invoice number and barcode references."
    />

    <UCard class="mt-4">
      <div class="grid gap-3 lg:grid-cols-[160px_160px_220px_1fr_140px_auto]">
        <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
        <UFormField label="Store"><USelect v-model="filters.storeId" :items="storeOptions" /></UFormField>
        <UFormField label="Search"><UInput v-model="filters.search" placeholder="Invoice, customer, barcode, product" @keyup.enter="refresh(true)" /></UFormField>
        <UFormField label="Rows"><USelect v-model="filters.pageSize" :items="[{ label: '50', value: 50 }, { label: '100', value: 100 }, { label: '250', value: 250 }, { label: '500', value: 500 }]" /></UFormField>
        <div class="flex items-end gap-2">
          <UCheckbox v-model="filters.onlyIssues" label="Only issues" />
          <UButton icon="i-lucide-search" :loading="loading" label="Apply" @click="refresh(true)" />
        </div>
      </div>
    </UCard>

    <UiRegisterPanel class="mt-4" title="Sale tax and margin summary" :loading="loading" :error="loadError" :empty="!report" empty-title="No sale review loaded" empty-description="Apply filters to load sale review rows." empty-icon="i-lucide-clipboard-check" @retry="refresh(true)">
      <div class="planner-metric-grid">
        <UCard v-for="card in summaryCards" :key="card.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="card.icon" :color="card.color" variant="subtle" />
            <div><p>{{ card.label }}</p><strong>{{ card.value }}</strong><span>{{ card.hint }}</span></div>
          </div>
        </UCard>
      </div>
      <div class="mt-4 flex flex-wrap gap-2">
        <UButton icon="i-lucide-copy" variant="subtle" label="Copy issue summary" @click="copyIssueSummary" />
        <UButton icon="i-lucide-download" variant="subtle" label="Export CSV" @click="exportCsv" />
      </div>
    </UiRegisterPanel>

    <UiRegisterPanel v-if="report" class="mt-4" title="Invoice-wise profit / loss" :description="`${report.invoices?.length || 0} invoices in selected review`" :loading="loading" :error="loadError" :empty="(report.invoices || []).length === 0" empty-title="No invoices" empty-description="No invoices matched the selected filters." empty-icon="i-lucide-receipt-text" @retry="refresh(true)">
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead>
            <tr><th>Invoice</th><th>Date</th><th>Customer</th><th class="text-right">Bill</th><th class="text-right">Taxable</th><th class="text-right">Cost</th><th class="text-right">Profit</th><th class="text-right">Margin</th><th class="text-right">Issues</th><th>Status</th><th>Action</th></tr>
          </thead>
          <tbody>
            <tr v-for="invoice in report.invoices" :key="invoice.invoiceId">
              <td><NuxtLink class="font-semibold text-primary" :to="invoiceUrl(invoice.invoiceId)">{{ invoice.invoiceNumber }}</NuxtLink></td>
              <td>{{ date(invoice.onDate) }}</td>
              <td><div class="font-medium">{{ invoice.customerName }}</div><div class="text-xs text-muted">{{ invoice.customerMobileNumber || '-' }}</div></td>
              <td class="text-right">{{ money(invoice.billAmount) }}</td>
              <td class="text-right">{{ money(invoice.taxableAmount) }}</td>
              <td class="text-right">{{ money(invoice.costAmount) }}</td>
              <td class="text-right" :class="Number(invoice.profitAmount || 0) < 0 ? 'text-error' : 'text-success'">{{ money(invoice.profitAmount) }}</td>
              <td class="text-right">{{ num(invoice.profitPercentage) }}%</td>
              <td class="text-right">{{ invoice.issueCount }}</td>
              <td><UBadge :color="invoice.issueCount ? 'warning' : 'success'" variant="subtle">{{ invoice.status }}</UBadge></td>
              <td>
                <UButton
                  v-if="invoice.issueCount && Number(invoice.extraAmountToReview || 0) > 0"
                  size="xs"
                  color="warning"
                  variant="subtle"
                  icon="i-lucide-wrench"
                  label="Apply Option B"
                  :loading="isApplying(invoice.invoiceId)"
                  @click="applyOptionB(invoice)"
                />
                <span v-else class="text-xs text-muted">No action</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiRegisterPanel>

    <UiRegisterPanel v-if="report" class="mt-4" title="Item-wise GST threshold and profit/loss" :description="`${report.totalItems || 0} item rows. Page ${filters.page} of ${totalPages}`" :loading="loading" :error="loadError" :empty="(report.items || []).length === 0" empty-title="No item rows" empty-description="No sale item rows matched the current filter." empty-icon="i-lucide-list-checks" @retry="refresh(false)">
      <div class="planner-table-wrap">
        <table class="planner-table">
          <thead>
            <tr>
              <th>Invoice / Item</th><th>Customer</th><th class="text-right">Qty</th><th class="text-right">Unit basic</th><th class="text-right">GST</th><th class="text-right">Expected</th><th class="text-right">Extra</th><th class="text-right">Cost</th><th class="text-right">Profit</th><th>Status</th><th>Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in report.items" :key="item.invoiceItemId">
              <td>
                <NuxtLink class="font-semibold text-primary" :to="invoiceUrl(item.invoiceId)">{{ item.invoiceNumber }}</NuxtLink>
                <div class="text-sm">{{ item.productName }}</div>
                <div class="text-xs text-muted">{{ item.barcode }} · HSN {{ item.hsnCode || '-' }}</div>
              </td>
              <td><div>{{ item.customerName }}</div><div class="text-xs text-muted">{{ date(item.onDate) }}</div></td>
              <td class="text-right">{{ num(item.quantity) }}</td>
              <td class="text-right">{{ money(item.unitBasicPrice) }}</td>
              <td class="text-right">{{ num(item.taxPercentage) }}%<div class="text-xs text-muted">{{ money(item.taxAmount) }}</div></td>
              <td class="text-right">{{ num(item.expectedTaxPercentage) }}%<div class="text-xs text-muted">{{ money(item.expectedTaxAmount) }}</div></td>
              <td class="text-right font-semibold text-warning">{{ money(item.extraAmountToReview) }}</td>
              <td class="text-right">{{ money(item.costAmount) }}<div class="text-xs text-muted">@ {{ money(item.costRate) }}</div></td>
              <td class="text-right" :class="Number(item.profitAmount || 0) < 0 ? 'text-error' : 'text-success'">{{ money(item.profitAmount) }}<div class="text-xs text-muted">{{ num(item.profitPercentage) }}%</div></td>
              <td><UBadge :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge></td>
              <td class="max-w-[380px] text-xs text-muted">{{ item.suggestedAction }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" variant="subtle" label="Previous" :disabled="filters.page <= 1" @click="filters.page--" />
        <span class="text-sm text-muted">Page {{ filters.page }} / {{ totalPages }}</span>
        <UButton trailing-icon="i-lucide-chevron-right" variant="subtle" label="Next" :disabled="filters.page >= totalPages" @click="filters.page++" />
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>

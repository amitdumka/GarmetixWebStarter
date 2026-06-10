<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const UBadge = resolveComponent('UBadge')

const loading = ref(false)
const posting = ref(false)
const printOpen = ref(false)
const activeTab = ref('sale')
const options = ref<any>({ stocks: [], vendors: [], customers: [], ledgers: [], recentDocuments: [] })
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const report = ref<any | null>(null)
const printDetail = ref<any | null>(null)

function emptyPurchaseLine() {
  return { productName: '', barcode: '', quantity: 1, rate: 0, discountAmount: 0, costPrice: 0, mrp: 0, storeId: stores.value?.[0]?.id || '' }
}

function emptySaleLine() {
  return { stockId: '', quantity: 1, rate: 0, discountAmount: 0 }
}

const purchaseForm = reactive({ vendorId: '', partyName: '', paymentMode: 0, referenceNumber: '', remarks: '' })
const saleForm = reactive({ customerId: '', partyName: '', paymentMode: 0, referenceNumber: '', remarks: '' })
const purchaseLines = ref<any[]>([emptyPurchaseLine()])
const saleLines = ref<any[]>([emptySaleLine()])
const reportForm = reactive({ from: new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10), to: new Date().toISOString().slice(0, 10) })

const paymentModeOptions = [
  { label: 'Cash', value: 0 },
  { label: 'Card', value: 1 },
  { label: 'UPI', value: 2 },
  { label: 'Wallets', value: 3 },
  { label: 'IMPS', value: 4 },
  { label: 'RTGS', value: 5 },
  { label: 'NEFT', value: 6 },
  { label: 'Cheque', value: 7 },
  { label: 'Others', value: 14 }
]

const stockOptions = computed(() => (options.value.stocks || []).map((item: any) => ({ label: item.label, value: item.stockId })))
const vendorOptions = computed(() => (options.value.vendors || []).map((item: any) => ({ label: item.name, value: item.id })))
const customerOptions = computed(() => (options.value.customers || []).map((item: any) => ({ label: item.name, value: item.id })))
const storeOptions = computed(() => (stores.value || []).map((item: any) => ({ label: item.name, value: item.id })))
const stockMap = computed(() => new Map((options.value.stocks || []).map((item: any) => [item.stockId, item])))

const purchaseTotals = computed(() => summarizeLines(purchaseLines.value))
const saleTotals = computed(() => summarizeLines(saleLines.value))
const recentRows = computed(() => (options.value.recentDocuments || []).map(formatRow))
const reportRows = computed(() => (report.value?.rows || []).map(formatRow))
const stockRows = computed(() => (report.value?.currentStockRows || []).map((item: any) => ({
  ...item,
  currentText: Number(item.currentStock || 0).toFixed(2),
  costText: money(Number(item.costPrice || 0)),
  valueText: money(Number(item.stockValue || 0))
})))

const metrics = computed(() => [
  { label: 'Sale amount', value: money(report.value?.saleAmount || 0), meta: `${report.value?.saleCount || 0} cash memo(s)`, icon: 'i-lucide-trending-up', color: 'success' },
  { label: 'Purchase amount', value: money(report.value?.purchaseAmount || 0), meta: `${report.value?.purchaseCount || 0} purchase memo(s)`, icon: 'i-lucide-package-plus', color: 'warning' },
  { label: 'Gross profit', value: money(report.value?.grossProfit || 0), meta: 'Sale - item cost', icon: 'i-lucide-chart-no-axes-combined', color: 'primary' },
  { label: 'Current stock value', value: money(report.value?.currentStockValue || 0), meta: `${Number(report.value?.currentStockQty || 0).toFixed(2)} qty`, icon: 'i-lucide-warehouse', color: 'neutral' }
])

const documentColumns: TableColumn<any>[] = [
  { accessorKey: 'onDateText', header: 'Date' },
  { accessorKey: 'documentNumber', header: 'No.' },
  {
    accessorKey: 'documentType',
    header: 'Type',
    cell: ({ row }) => h(UBadge, { color: row.original.documentType === 'Sale' ? 'success' : 'warning', variant: 'subtle' }, () => row.original.documentType)
  },
  { accessorKey: 'partyName', header: 'Party' },
  { accessorKey: 'itemCount', header: 'Items' },
  { accessorKey: 'quantity', header: 'Qty' },
  { accessorKey: 'netText', header: 'Net' },
  { accessorKey: 'profitText', header: 'Profit' },
  {
    id: 'actions',
    header: 'Print',
    cell: ({ row }) => h(resolveComponent('UButton'), { size: 'xs', variant: 'outline', icon: 'i-lucide-printer', label: 'Print', onClick: () => openPrint(row.original.id) })
  }
]

const stockColumns: TableColumn<any>[] = [
  { accessorKey: 'productName', header: 'Product' },
  { accessorKey: 'barcode', header: 'Barcode' },
  { accessorKey: 'storeName', header: 'Store' },
  { accessorKey: 'purchaseQty', header: 'Purchased' },
  { accessorKey: 'soldQty', header: 'Sold' },
  { accessorKey: 'currentText', header: 'Current' },
  { accessorKey: 'costText', header: 'Cost' },
  { accessorKey: 'valueText', header: 'Value' }
]

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [companyRows, storeRows, optionRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('non-gst-goods/options')
    ])
    companies.value = companyRows
    stores.value = storeRows
    options.value = optionRows
    purchaseLines.value.forEach((line) => {
      if (!line.storeId && storeRows.length) line.storeId = storeRows[0].id
    })
    await loadReport()
  } catch (error) {
    feedback.failed('Non-GST goods refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function postPurchase() {
  posting.value = true
  try {
    const response = await api.create<any>('non-gst-goods/purchase', {
      vendorId: purchaseForm.vendorId || null,
      partyName: nullIfEmpty(purchaseForm.partyName),
      paymentMode: purchaseForm.paymentMode,
      referenceNumber: nullIfEmpty(purchaseForm.referenceNumber),
      remarks: nullIfEmpty(purchaseForm.remarks),
      items: purchaseLines.value.map((line) => ({
        productName: line.productName,
        barcode: line.barcode,
        quantity: Number(line.quantity || 0),
        rate: Number(line.rate || 0),
        discountAmount: Number(line.discountAmount || 0),
        costPrice: Number(line.costPrice || line.rate || 0),
        mrp: Number(line.mrp || line.rate || 0),
        storeId: line.storeId || null
      }))
    })
    feedback.notify('Non-GST purchase memo posted', response?.documentNumber, 'success')
    Object.assign(purchaseForm, { referenceNumber: '', remarks: '' })
    purchaseLines.value = [emptyPurchaseLine()]
    await openPrint(response.id)
    await refresh()
  } catch (error) {
    feedback.failed('Could not post Non-GST purchase memo', error)
  } finally {
    posting.value = false
  }
}

async function postSale() {
  posting.value = true
  try {
    const response = await api.create<any>('non-gst-goods/sale', {
      customerId: saleForm.customerId || null,
      partyName: nullIfEmpty(saleForm.partyName),
      paymentMode: saleForm.paymentMode,
      referenceNumber: nullIfEmpty(saleForm.referenceNumber),
      remarks: nullIfEmpty(saleForm.remarks),
      items: saleLines.value.map((line) => ({
        stockId: line.stockId || null,
        quantity: Number(line.quantity || 0),
        rate: Number(line.rate || 0),
        discountAmount: Number(line.discountAmount || 0)
      }))
    })
    feedback.notify('Non-GST sale cash memo posted', response?.documentNumber, 'success')
    Object.assign(saleForm, { referenceNumber: '', remarks: '' })
    saleLines.value = [emptySaleLine()]
    await openPrint(response.id)
    await refresh()
  } catch (error) {
    feedback.failed('Could not post Non-GST sale cash memo', error)
  } finally {
    posting.value = false
  }
}

async function loadReport() {
  report.value = await api.get<any>(`non-gst-goods/report?from=${reportForm.from}&to=${reportForm.to}`)
}

async function openPrint(id: string) {
  if (!id) return
  printDetail.value = await api.get<any>(`non-gst-goods/documents/${id}/print`)
  printOpen.value = true
}

function addPurchaseLine() {
  purchaseLines.value.push(emptyPurchaseLine())
}

function removePurchaseLine(index: number) {
  if (purchaseLines.value.length > 1) purchaseLines.value.splice(index, 1)
}

function addSaleLine() {
  saleLines.value.push(emptySaleLine())
}

function removeSaleLine(index: number) {
  if (saleLines.value.length > 1) saleLines.value.splice(index, 1)
}

function useSelectedSaleRate(line: any) {
  const selected = stockMap.value.get(line.stockId)
  line.rate = Number(selected?.mrp || 0)
}

function printCashMemo() {
  window.print()
}

function summarizeLines(lines: any[]) {
  const gross = lines.reduce((sum, line) => sum + Number(line.quantity || 0) * Number(line.rate || 0), 0)
  const discount = lines.reduce((sum, line) => sum + Number(line.discountAmount || 0), 0)
  const qty = lines.reduce((sum, line) => sum + Number(line.quantity || 0), 0)
  return { qty, gross, discount, net: gross - discount }
}

function formatRow(item: any) {
  return {
    ...item,
    onDateText: new Date(item.onDate).toLocaleDateString('en-IN'),
    quantity: Number(item.quantity || 0).toFixed(2),
    netText: money(Number(item.netAmount || 0)),
    profitText: money(Number(item.profitAmount || 0))
  }
}

function nullIfEmpty(value: unknown) {
  const text = String(value || '').trim()
  return text ? text : null
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell v-else title="Non-GST Goods" :companies="companies" :stores="stores" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Non-GST / Out-of-Scope Goods"
        description="Multi-item purchase memos and sale cash memos for legally non-taxable or out-of-scope stock. GST rate remains 0 and all records are separately reported."
        icon="i-lucide-file-warning"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge color="warning" variant="subtle">GST 0%</UBadge>
          <UBadge color="success" variant="subtle">Audited separately</UBadge>
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        variant="subtle"
        icon="i-lucide-shield-alert"
        title="Compliance note"
        description="This module does not hide transactions. It keeps Non-GST stock, purchase memos, sale cash memos, profit and current stock in a separate audited register, while excluding these rows from GST reports."
      />

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
          <div class="planner-card-header">
            <div>
              <h2>Post Non-GST Goods</h2>
              <p>Purchase creates or updates IsOFB stock. Sale can include multiple stock rows in one cash memo.</p>
            </div>
            <UBadge color="primary" variant="subtle">Stage 6E</UBadge>
          </div>
        </template>

        <UTabs v-model="activeTab" :items="[
          { label: 'Sale Cash Memo', value: 'sale', icon: 'i-lucide-shopping-bag' },
          { label: 'Purchase Memo', value: 'purchase', icon: 'i-lucide-package-plus' },
          { label: 'Reports', value: 'report', icon: 'i-lucide-table' }
        ]" />

        <div v-if="activeTab === 'sale'" class="mt-4 space-y-4">
          <div class="form-grid">
            <UFormField label="Customer"><USelectMenu v-model="saleForm.customerId" :items="customerOptions" value-key="value" class="w-full" /></UFormField>
            <UFormField label="Party name"><UInput v-model="saleForm.partyName" /></UFormField>
            <UFormField label="Payment mode"><USelect v-model="saleForm.paymentMode" :items="paymentModeOptions" class="w-full" /></UFormField>
            <UFormField label="Reference"><UInput v-model="saleForm.referenceNumber" /></UFormField>
          </div>

          <div class="planner-table-wrap">
            <table class="planner-mini-table">
              <thead><tr><th>Stock item</th><th>Qty</th><th>Rate</th><th>Discount</th><th>Net</th><th /></tr></thead>
              <tbody>
                <tr v-for="(line, index) in saleLines" :key="index">
                  <td><USelectMenu v-model="line.stockId" :items="stockOptions" value-key="value" class="min-w-80" @update:model-value="() => useSelectedSaleRate(line)" /></td>
                  <td><UInput v-model.number="line.quantity" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.rate" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.discountAmount" type="number" min="0" step="0.01" /></td>
                  <td>{{ money(Number(line.quantity || 0) * Number(line.rate || 0) - Number(line.discountAmount || 0)) }}</td>
                  <td><UButton icon="i-lucide-trash-2" color="error" variant="ghost" :disabled="saleLines.length === 1" @click="removeSaleLine(index)" /></td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="planner-action-row">
            <UButton label="Add item" icon="i-lucide-plus" variant="outline" @click="addSaleLine" />
            <div class="summary-pill">Qty {{ saleTotals.qty.toFixed(2) }} · Net {{ money(saleTotals.net) }}</div>
            <UButton label="Post & Print Cash Memo" icon="i-lucide-printer" :disabled="!canEdit" :loading="posting" @click="postSale" />
          </div>
          <UFormField label="Remarks"><UTextarea v-model="saleForm.remarks" /></UFormField>
        </div>

        <div v-else-if="activeTab === 'purchase'" class="mt-4 space-y-4">
          <div class="form-grid">
            <UFormField label="Vendor"><USelectMenu v-model="purchaseForm.vendorId" :items="vendorOptions" value-key="value" class="w-full" /></UFormField>
            <UFormField label="Party name"><UInput v-model="purchaseForm.partyName" /></UFormField>
            <UFormField label="Payment mode"><USelect v-model="purchaseForm.paymentMode" :items="paymentModeOptions" class="w-full" /></UFormField>
            <UFormField label="Reference"><UInput v-model="purchaseForm.referenceNumber" /></UFormField>
          </div>

          <div class="planner-table-wrap">
            <table class="planner-mini-table">
              <thead><tr><th>Product</th><th>Barcode</th><th>Store</th><th>Qty</th><th>Rate</th><th>Discount</th><th>Cost</th><th>MRP</th><th>Net</th><th /></tr></thead>
              <tbody>
                <tr v-for="(line, index) in purchaseLines" :key="index">
                  <td><UInput v-model="line.productName" /></td>
                  <td><UInput v-model="line.barcode" /></td>
                  <td><USelectMenu v-model="line.storeId" :items="storeOptions" value-key="value" class="min-w-52" /></td>
                  <td><UInput v-model.number="line.quantity" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.rate" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.discountAmount" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.costPrice" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.mrp" type="number" min="0" step="0.01" /></td>
                  <td>{{ money(Number(line.quantity || 0) * Number(line.rate || 0) - Number(line.discountAmount || 0)) }}</td>
                  <td><UButton icon="i-lucide-trash-2" color="error" variant="ghost" :disabled="purchaseLines.length === 1" @click="removePurchaseLine(index)" /></td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="planner-action-row">
            <UButton label="Add item" icon="i-lucide-plus" variant="outline" @click="addPurchaseLine" />
            <div class="summary-pill">Qty {{ purchaseTotals.qty.toFixed(2) }} · Net {{ money(purchaseTotals.net) }}</div>
            <UButton label="Post & Print Purchase Memo" icon="i-lucide-printer" :disabled="!canEdit" :loading="posting" @click="postPurchase" />
          </div>
          <UFormField label="Remarks"><UTextarea v-model="purchaseForm.remarks" /></UFormField>
        </div>

        <div v-else class="mt-4 space-y-4">
          <div class="form-grid">
            <UFormField label="From"><UInput v-model="reportForm.from" type="date" /></UFormField>
            <UFormField label="To"><UInput v-model="reportForm.to" type="date" /></UFormField>
            <div class="flex items-end"><UButton label="Load report" icon="i-lucide-search" @click="loadReport" /></div>
          </div>
          <h3 class="section-title">Sale / Purchase / Profit Report</h3>
          <UTable :columns="documentColumns" :data="reportRows" />
          <h3 class="section-title">Current Non-GST Stock</h3>
          <UTable :columns="stockColumns" :data="stockRows" />
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Recent Non-GST Documents</h2>
              <p>Print purchase memo or sale cash memo from any saved document.</p>
            </div>
          </div>
        </template>
        <UTable :columns="documentColumns" :data="recentRows" />
      </UCard>

      <UModal v-model:open="printOpen" title="Non-GST Memo Print" :ui="{ content: 'max-w-5xl' }">
        <template #body>
          <div v-if="printDetail" class="receipt-print non-gst-print-document">
            <section class="memo-print">
              <div class="memo-header">
                <div>
                  <h2>{{ printDetail.companyName }}</h2>
                  <p>{{ printDetail.storeName }}</p>
                  <p>{{ printDetail.storeAddress }}</p>
                  <p>{{ printDetail.storePhone }} <span v-if="printDetail.storeEmail">· {{ printDetail.storeEmail }}</span></p>
                </div>
                <div class="memo-title">
                  <strong>{{ printDetail.documentType }}</strong>
                  <span>{{ printDetail.documentNumber }}</span>
                  <span>{{ new Date(printDetail.onDate).toLocaleDateString('en-IN') }}</span>
                </div>
              </div>

              <div class="memo-party">
                <span>Party</span><strong>{{ printDetail.partyName }}</strong>
                <span>Payment</span><strong>{{ printDetail.paymentMode }}</strong>
                <span>Reference</span><strong>{{ printDetail.referenceNumber || '-' }}</strong>
              </div>

              <table class="memo-table">
                <thead>
                  <tr><th>#</th><th>Item</th><th>Barcode</th><th>Qty</th><th>Rate</th><th>Gross</th><th>Disc.</th><th>Taxable</th><th>GST%</th><th>Tax</th><th>Amount</th></tr>
                </thead>
                <tbody>
                  <tr v-for="item in printDetail.items" :key="item.serial">
                    <td>{{ item.serial }}</td><td>{{ item.productName }}</td><td>{{ item.barcode }}</td><td>{{ Number(item.quantity).toFixed(2) }}</td><td>{{ money(item.rate) }}</td><td>{{ money(item.grossAmount) }}</td><td>{{ money(item.discountAmount) }}</td><td>{{ money(item.taxableAmount) }}</td><td>{{ Number(item.taxRate).toFixed(2) }}</td><td>{{ money(item.taxAmount) }}</td><td>{{ money(item.amount) }}</td>
                  </tr>
                </tbody>
              </table>

              <div class="memo-tax-note">{{ printDetail.taxNote }}</div>
              <div class="memo-totals">
                <span>Gross</span><strong>{{ money(printDetail.grossAmount) }}</strong>
                <span>Discount</span><strong>{{ money(printDetail.discountAmount) }}</strong>
                <span>Taxable</span><strong>{{ money(printDetail.taxableAmount) }}</strong>
                <span>Tax</span><strong>{{ money(printDetail.taxAmount) }}</strong>
                <span>Net</span><strong>{{ money(printDetail.netAmount) }}</strong>
              </div>
              <p v-if="printDetail.remarks" class="memo-remarks">{{ printDetail.remarks }}</p>
            </section>
          </div>
        </template>
        <template #footer>
          <div class="modal-actions no-print">
            <UButton color="neutral" variant="outline" label="Close" @click="printOpen = false" />
            <UButton icon="i-lucide-printer" label="Print" @click="printCashMemo" />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>

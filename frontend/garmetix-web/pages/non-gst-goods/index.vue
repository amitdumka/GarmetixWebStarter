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
const activeTab = ref('sale')
const options = ref<any>({ stocks: [], vendors: [], customers: [], ledgers: [], recentDocuments: [] })
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const report = ref<any | null>(null)

const purchaseForm = reactive({ vendorId: '', partyName: '', paymentMode: 0, referenceNumber: '', remarks: '', productName: '', barcode: '', quantity: 1, rate: 0, discountAmount: 0, costPrice: 0, mrp: 0, storeId: '' })
const saleForm = reactive({ stockId: '', customerId: '', partyName: '', paymentMode: 0, referenceNumber: '', remarks: '', quantity: 1, rate: 0, discountAmount: 0 })
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
const storeOptions = computed(() => (options.value.stores || stores.value || []).map((item: any) => ({ label: item.name, value: item.id })))
const selectedStock = computed(() => (options.value.stocks || []).find((item: any) => item.stockId === saleForm.stockId))

const recentRows = computed(() => (options.value.recentDocuments || []).map(formatRow))
const reportRows = computed(() => (report.value?.rows || []).map(formatRow))
const metrics = computed(() => [
  { label: 'Non-GST stock rows', value: options.value.stocks?.length || 0, meta: 'Separate from GST billing stock', icon: 'i-lucide-package-search', color: 'primary' },
  { label: 'Sale amount', value: money(report.value?.saleAmount || 0), meta: 'Visible other income', icon: 'i-lucide-trending-up', color: 'success' },
  { label: 'Purchase amount', value: money(report.value?.purchaseAmount || 0), meta: 'Non-GST purchase clearing', icon: 'i-lucide-package-plus', color: 'warning' }
])

const columns: TableColumn<any>[] = [
  { accessorKey: 'onDateText', header: 'Date' },
  { accessorKey: 'documentNumber', header: 'No.' },
  {
    accessorKey: 'documentType',
    header: 'Type',
    cell: ({ row }) => h(UBadge, { color: row.original.documentType === 'Sale' ? 'success' : 'warning', variant: 'subtle' }, () => row.original.documentType)
  },
  { accessorKey: 'partyName', header: 'Party' },
  { accessorKey: 'quantity', header: 'Qty' },
  { accessorKey: 'netText', header: 'Net' },
  { accessorKey: 'remarks', header: 'Remarks' }
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
    options.value = { ...optionRows, stores: storeRows }
    if (!purchaseForm.storeId && storeRows.length) purchaseForm.storeId = storeRows[0].id
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
      items: [{
        productName: purchaseForm.productName,
        barcode: purchaseForm.barcode,
        quantity: Number(purchaseForm.quantity || 0),
        rate: Number(purchaseForm.rate || 0),
        discountAmount: Number(purchaseForm.discountAmount || 0),
        costPrice: Number(purchaseForm.costPrice || purchaseForm.rate || 0),
        mrp: Number(purchaseForm.mrp || purchaseForm.rate || 0),
        storeId: purchaseForm.storeId || null
      }]
    })
    feedback.notify('Non-GST purchase posted', response?.documentNumber, 'success')
    Object.assign(purchaseForm, { productName: '', barcode: '', quantity: 1, rate: 0, discountAmount: 0, costPrice: 0, mrp: 0, referenceNumber: '', remarks: '' })
    await refresh()
  } catch (error) {
    feedback.failed('Could not post Non-GST purchase', error)
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
      items: [{
        stockId: saleForm.stockId,
        quantity: Number(saleForm.quantity || 0),
        rate: Number(saleForm.rate || 0),
        discountAmount: Number(saleForm.discountAmount || 0)
      }]
    })
    feedback.notify('Non-GST sale posted', response?.documentNumber, 'success')
    Object.assign(saleForm, { quantity: 1, rate: 0, discountAmount: 0, referenceNumber: '', remarks: '' })
    await refresh()
  } catch (error) {
    feedback.failed('Could not post Non-GST sale', error)
  } finally {
    posting.value = false
  }
}

async function loadReport() {
  report.value = await api.get<any>(`non-gst-goods/report?from=${reportForm.from}&to=${reportForm.to}`)
}

function useSelectedSaleRate() {
  saleForm.rate = Number(selectedStock.value?.mrp || 0)
}

watch(() => saleForm.stockId, useSelectedSaleRate)

function formatRow(item: any) {
  return {
    ...item,
    onDateText: new Date(item.onDate).toLocaleDateString('en-IN'),
    netText: money(Number(item.netAmount || 0))
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
        description="Visible, audited purchase/sale flow for legally non-taxable or out-of-scope stock. These documents are separate from GST invoices and GST reports."
        icon="i-lucide-file-warning"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge color="warning" variant="subtle">No GST tax calculation</UBadge>
          <UBadge color="success" variant="subtle">Visible in books</UBadge>
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        variant="subtle"
        icon="i-lucide-shield-alert"
        title="Compliance note"
        description="This module does not hide sales or purchases. It records them separately, posts visible accounting entries, excludes them from GST reports only as Non-GST/out-of-scope transactions, and provides a separate report."
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
              <p>Discount is allowed. Tax rate and GST split are always zero.</p>
            </div>
            <UBadge color="primary" variant="subtle">Stage 6D</UBadge>
          </div>
        </template>

        <UTabs v-model="activeTab" :items="[
          { label: 'Sale', value: 'sale', icon: 'i-lucide-shopping-bag' },
          { label: 'Purchase', value: 'purchase', icon: 'i-lucide-package-plus' },
          { label: 'Report', value: 'report', icon: 'i-lucide-table' }
        ]" />

        <div v-if="activeTab === 'sale'" class="form-grid mt-4">
          <UFormField label="Non-GST stock"><USelectMenu v-model="saleForm.stockId" :items="stockOptions" value-key="value" class="w-full" /></UFormField>
          <UFormField label="Customer"><USelectMenu v-model="saleForm.customerId" :items="customerOptions" value-key="value" class="w-full" /></UFormField>
          <UFormField label="Party name"><UInput v-model="saleForm.partyName" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="saleForm.paymentMode" :items="paymentModeOptions" class="w-full" /></UFormField>
          <UFormField label="Quantity"><UInput v-model.number="saleForm.quantity" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Rate"><UInput v-model.number="saleForm.rate" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Discount"><UInput v-model.number="saleForm.discountAmount" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Reference"><UInput v-model="saleForm.referenceNumber" /></UFormField>
          <UFormField label="Remarks" class="md:col-span-2"><UTextarea v-model="saleForm.remarks" /></UFormField>
          <div class="md:col-span-2 flex justify-end">
            <UButton label="Post Non-GST Sale" icon="i-lucide-check" :disabled="!canEdit" :loading="posting" @click="postSale" />
          </div>
        </div>

        <div v-else-if="activeTab === 'purchase'" class="form-grid mt-4">
          <UFormField label="Vendor"><USelectMenu v-model="purchaseForm.vendorId" :items="vendorOptions" value-key="value" class="w-full" /></UFormField>
          <UFormField label="Party name"><UInput v-model="purchaseForm.partyName" /></UFormField>
          <UFormField label="Store"><USelectMenu v-model="purchaseForm.storeId" :items="storeOptions" value-key="value" class="w-full" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="purchaseForm.paymentMode" :items="paymentModeOptions" class="w-full" /></UFormField>
          <UFormField label="Product name"><UInput v-model="purchaseForm.productName" /></UFormField>
          <UFormField label="Barcode"><UInput v-model="purchaseForm.barcode" /></UFormField>
          <UFormField label="Quantity"><UInput v-model.number="purchaseForm.quantity" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Rate"><UInput v-model.number="purchaseForm.rate" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Discount"><UInput v-model.number="purchaseForm.discountAmount" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Cost price"><UInput v-model.number="purchaseForm.costPrice" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="MRP"><UInput v-model.number="purchaseForm.mrp" type="number" min="0" step="0.01" /></UFormField>
          <UFormField label="Reference"><UInput v-model="purchaseForm.referenceNumber" /></UFormField>
          <UFormField label="Remarks" class="md:col-span-2"><UTextarea v-model="purchaseForm.remarks" /></UFormField>
          <div class="md:col-span-2 flex justify-end">
            <UButton label="Post Non-GST Purchase" icon="i-lucide-package-plus" :disabled="!canEdit" :loading="posting" @click="postPurchase" />
          </div>
        </div>

        <div v-else class="mt-4 space-y-4">
          <div class="form-grid">
            <UFormField label="From"><UInput v-model="reportForm.from" type="date" /></UFormField>
            <UFormField label="To"><UInput v-model="reportForm.to" type="date" /></UFormField>
            <div class="flex items-end"><UButton label="Load report" icon="i-lucide-search" @click="loadReport" /></div>
          </div>
          <UTable :columns="columns" :data="reportRows" />
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Recent Non-GST Documents</h2>
              <p>These rows are excluded from GST reports and available in this separate register.</p>
            </div>
          </div>
        </template>
        <UTable :columns="columns" :data="recentRows" />
      </UCard>
    </section>
  </AppShell>
</template>

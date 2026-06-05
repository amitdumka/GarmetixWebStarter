<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const purchaseInvoices = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const setupStatus = ref<any | null>(null)
const search = ref('')
const formOpen = ref(false)

const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 4, label: 'IMPS' },
  { value: 5, label: 'RTGS' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' },
  { value: 8, label: 'Demand Draft' }
]

const purchaseForm = reactive<any>(emptyPurchaseForm())
const purchaseCart = ref<any[]>([])

const productOptions = computed(() => [
  { value: '', label: 'New product' },
  ...products.value.map((product) => ({
    value: product.id,
    label: `${product.name || 'Product'} - ${product.barcode || 'No barcode'}`
  }))
])

const selectedPurchaseProduct = computed(() => products.value.find((item) => item.id === purchaseForm.selectedProductId))
const requiresBankAccount = computed(() => Number(purchaseForm.paidAmount || 0) > 0 && Number(purchaseForm.paymentMode) !== 0)

const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({
  value: account.id,
  label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
})))

const purchaseCartTotal = computed(() => {
  return purchaseCart.value.reduce((sum, item) => sum + lineTotal(item), 0)
})

const payableTotal = computed(() => purchaseCartTotal.value + Number(purchaseForm.frightAmount || 0))

const invoiceSummary = computed(() => {
  return purchaseInvoices.value.reduce((summary, invoice) => {
    summary.billAmount += Number(invoice.billAmount || invoice.totalAmount || invoice.netAmount || 0)
    summary.paidAmount += Number(invoice.paidAmount || 0)
    summary.freightAmount += Number(invoice.frightAmount || 0)
    return summary
  }, {
    billAmount: 0,
    paidAmount: 0,
    freightAmount: 0
  })
})

const metrics = computed(() => [
  {
    label: 'Purchase Invoices',
    value: purchaseInvoices.value.length,
    meta: 'Inward records',
    icon: 'i-lucide-file-text',
    color: 'primary'
  },
  {
    label: 'Purchase Value',
    value: money(invoiceSummary.value.billAmount),
    meta: 'Invoice total',
    icon: 'i-lucide-indian-rupee',
    color: 'success'
  },
  {
    label: 'Paid',
    value: money(invoiceSummary.value.paidAmount),
    meta: 'Supplier payments',
    icon: 'i-lucide-credit-card',
    color: 'neutral'
  },
  {
    label: 'Freight',
    value: money(invoiceSummary.value.freightAmount),
    meta: 'Purchase freight',
    icon: 'i-lucide-truck',
    color: 'warning'
  }
])

const tableRows = computed(() => purchaseInvoices.value.map((invoice) => ({
  id: invoice.id,
  invoiceNumber: invoice.invoiceNumber || '-',
  inwardNumber: invoice.inwardNumber || '-',
  vendorName: invoice.vendorName || invoice.vendor?.name || '-',
  billAmount: money(Number(invoice.billAmount || invoice.totalAmount || invoice.netAmount || 0)),
  paidAmount: money(Number(invoice.paidAmount || 0)),
  status: invoice.invoiceStatus ?? 'Saved',
  raw: invoice
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const columns: TableColumn<any>[] = [
  { accessorKey: 'invoiceNumber', header: 'Invoice' },
  { accessorKey: 'inwardNumber', header: 'Inward' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'billAmount', header: 'Amount' },
  { accessorKey: 'paidAmount', header: 'Paid' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: 'success',
      variant: 'subtle'
    }, () => String(row.original.status))
  }
]

function emptyPurchaseForm() {
  return {
    vendorName: 'Default Supplier',
    vendorMobileNumber: '',
    vendorGstin: '',
    invoiceNumber: '',
    inwardNumber: '',
    paymentMode: 0,
    paidAmount: 0,
    frightAmount: 0,
    selectedProductId: '',
    productName: '',
    barcode: '',
    quantity: 1,
    costPrice: 0,
    mrp: 0,
    discountAmount: 0,
    bankAccountId: null
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, purchaseRows, bankAccountRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.list<any>('purchase-invoices'),
      api.list<any>('bank-accounts')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    purchaseInvoices.value = purchaseRows
    bankAccounts.value = bankAccountRows
  } catch (error) {
    feedback.failed('Purchase refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  Object.assign(purchaseForm, emptyPurchaseForm())
  purchaseCart.value = []
  formOpen.value = true
}

function addPurchaseItem() {
  const selected = selectedPurchaseProduct.value
  const productName = selected?.name || purchaseForm.productName
  const barcode = selected?.barcode || purchaseForm.barcode

  if (!productName || !barcode) {
    feedback.notify('Item missing', 'Select an existing product or enter product name and barcode.', 'warning')
    return
  }

  purchaseCart.value.push({
    productId: selected?.id || null,
    productName,
    barcode,
    quantity: Number(purchaseForm.quantity || 0),
    costPrice: Number(purchaseForm.costPrice || 0),
    mrp: Number(purchaseForm.mrp || selected?.mrp || 0),
    discountAmount: Number(purchaseForm.discountAmount || 0)
  })

  purchaseForm.selectedProductId = ''
  purchaseForm.productName = ''
  purchaseForm.barcode = ''
  purchaseForm.quantity = 1
  purchaseForm.costPrice = 0
  purchaseForm.mrp = 0
  purchaseForm.discountAmount = 0
  purchaseForm.paidAmount = payableTotal.value
}

function removePurchaseItem(index: number) {
  purchaseCart.value.splice(index, 1)
  purchaseForm.paidAmount = payableTotal.value
}

async function submitPurchase() {
  saving.value = true
  try {
    const companyId = setupStatus.value?.companyId || companies.value[0]?.id
    const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
    const storeId = setupStatus.value?.storeId || stores.value[0]?.id

    if (!companyId || !storeGroupId || !storeId) {
      throw new Error('Run quick setup before purchase inward.')
    }

    if (purchaseCart.value.length === 0) {
      throw new Error('Add at least one item to the inward cart.')
    }

    if (requiresBankAccount.value && !purchaseForm.bankAccountId) {
      throw new Error('Select bank account for non-cash payment.')
    }

    const response = await api.create<any>('purchase/inward', {
      companyId,
      storeGroupId,
      storeId,
      vendorName: purchaseForm.vendorName,
      vendorMobileNumber: purchaseForm.vendorMobileNumber,
      vendorGstin: purchaseForm.vendorGstin,
      invoiceNumber: purchaseForm.invoiceNumber,
      inwardNumber: purchaseForm.inwardNumber,
      paymentMode: Number(purchaseForm.paymentMode),
      bankAccountId: requiresBankAccount.value ? purchaseForm.bankAccountId : null,
      paidAmount: Number(purchaseForm.paidAmount || 0),
      frightAmount: Number(purchaseForm.frightAmount || 0),
      items: purchaseCart.value.map((item) => ({
        productId: item.productId,
        productName: item.productName,
        barcode: item.barcode,
        quantity: item.quantity,
        costPrice: item.costPrice,
        mrp: item.mrp,
        discountAmount: item.discountAmount
      }))
    })

    feedback.saved(`Inward ${response.inwardNumber || ''}`.trim())
    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save purchase inward', error)
  } finally {
    saving.value = false
  }
}

function lineTotal(item: any) {
  return Math.max((Number(item.costPrice || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
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
})

watch(() => purchaseForm.paymentMode, () => {
  if (requiresBankAccount.value && !purchaseForm.bankAccountId) {
    purchaseForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => purchaseForm.paidAmount, () => {
  if (requiresBankAccount.value && !purchaseForm.bankAccountId) {
    purchaseForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Purchase"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Purchase Inward"
        description="Record supplier inward invoices, add items, and update stock in one workflow."
        icon="i-lucide-package-plus"
        primary-label="New Inward"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${purchaseInvoices.length} invoices` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" label="New Inward" @click="startCreate" />
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
          <div class="planner-card-header">
            <div>
              <h2>Purchase Register</h2>
              <p>Search supplier invoice, inward number, or vendor.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search invoice, inward, vendor"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Inward"
          @refresh="refresh"
          @create="startCreate"
        />

        <UTable
          v-if="filteredRows.length"
          :data="filteredRows"
          :columns="columns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          title="No purchase invoices found"
          description="Create the first inward invoice to begin stock receiving."
          icon="i-lucide-package-plus"
          action-label="New Inward"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        title="New Purchase Inward"
        description="Create or reuse products and receive stock from a supplier."
        submit-label="Save Inward"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="saving"
        @submit="submitPurchase"
      >
        <UFormField label="Vendor" required>
          <UInput v-model="purchaseForm.vendorName" required />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Vendor mobile">
            <UInput v-model="purchaseForm.vendorMobileNumber" />
          </UFormField>
          <UFormField label="Vendor GSTIN">
            <UInput v-model="purchaseForm.vendorGstin" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Supplier invoice">
            <UInput v-model="purchaseForm.invoiceNumber" />
          </UFormField>
          <UFormField label="Inward number">
            <UInput v-model="purchaseForm.inwardNumber" />
          </UFormField>
        </div>

        <USeparator label="Item" />

        <UFormField label="Existing product">
          <USelect v-model="purchaseForm.selectedProductId" :items="productOptions" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Product name">
            <UInput v-model="purchaseForm.productName" :disabled="Boolean(purchaseForm.selectedProductId)" />
          </UFormField>
          <UFormField label="Barcode">
            <UInput v-model="purchaseForm.barcode" :disabled="Boolean(purchaseForm.selectedProductId)" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Quantity">
            <UInput v-model="purchaseForm.quantity" min="1" type="number" />
          </UFormField>
          <UFormField label="Cost price">
            <UInput v-model="purchaseForm.costPrice" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="MRP">
            <UInput v-model="purchaseForm.mrp" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Unit discount">
            <UInput v-model="purchaseForm.discountAmount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Inward Item" type="button" @click="addPurchaseItem" />

        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Qty</th>
                <th>Cost</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in purchaseCart" :key="`${item.barcode}-${index}`">
                <td>{{ item.productName }}</td>
                <td>{{ item.quantity }}</td>
                <td>{{ money(lineTotal(item)) }}</td>
                <td>
                  <UButton color="error" variant="ghost" icon="i-lucide-x" size="xs" type="button" @click="removePurchaseItem(index)" />
                </td>
              </tr>
              <tr v-if="purchaseCart.length === 0">
                <td colspan="4">No inward items</td>
              </tr>
            </tbody>
          </table>
        </div>

        <USeparator label="Payment" />

        <UFormField label="Payment mode">
          <USelect v-model="purchaseForm.paymentMode" :items="paymentModeOptions" />
        </UFormField>
        <UFormField v-if="requiresBankAccount" label="Bank account" required>
          <USelect v-model="purchaseForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Freight">
            <UInput v-model="purchaseForm.frightAmount" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Paid">
            <UInput v-model="purchaseForm.paidAmount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <div class="payroll-summary">
          <span>Cart total</span><strong>{{ money(purchaseCartTotal) }}</strong>
          <span>Freight</span><strong>{{ money(Number(purchaseForm.frightAmount || 0)) }}</strong>
          <span>Payable</span><strong>{{ money(payableTotal) }}</strong>
        </div>
      </UiFormSlideover>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const invoices = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const selectedReceipt = ref<any | null>(null)
const pendingCancel = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const cancelling = ref(false)
const setupStatus = ref<any | null>(null)
const search = ref('')
const saleOpen = ref(false)
const cancelOpen = ref(false)

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

const saleForm = reactive<any>(emptySaleForm())
const saleCart = ref<any[]>([])

const receiptOpen = computed({
  get: () => Boolean(selectedReceipt.value),
  set: (value: boolean) => {
    if (!value) {
      selectedReceipt.value = null
    }
  }
})

const productOptions = computed(() => [
  { value: '', label: 'Select product' },
  ...products.value.map((product) => ({
    value: product.id,
    label: `${product.name || 'Product'} - ${product.barcode || 'No barcode'}`
  }))
])

const selectedProduct = computed(() => products.value.find((item) => item.id === saleForm.selectedProductId))
const requiresBankAccount = computed(() => Number(saleForm.paidAmount || 0) > 0 && Number(saleForm.paymentMode) !== 0)

const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({
  value: account.id,
  label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
})))

const cartTotal = computed(() => {
  return saleCart.value.reduce((sum, item) => sum + lineTotal(item), 0)
})

const payableTotal = computed(() => Math.max(cartTotal.value - Number(saleForm.billDiscountAmount || 0), 0))

const invoiceSummary = computed(() => {
  return invoices.value.reduce((summary, invoice) => {
    const billAmount = Number(invoice.billAmount || 0)
    const paidAmount = Number(invoice.paidAmount || 0)
    summary.billAmount += billAmount
    summary.paidAmount += paidAmount
    summary.balanceAmount += Number(invoice.balanceAmount || (billAmount - paidAmount))
    if (invoice.invoiceStatus === 'Cancelled') {
      summary.cancelled += 1
    }
    return summary
  }, {
    billAmount: 0,
    paidAmount: 0,
    balanceAmount: 0,
    cancelled: 0
  })
})

const metrics = computed(() => [
  {
    label: 'Invoices',
    value: invoices.value.length,
    meta: `${invoiceSummary.value.cancelled} cancelled`,
    icon: 'i-lucide-receipt-indian-rupee',
    color: 'primary'
  },
  {
    label: 'Sales',
    value: money(invoiceSummary.value.billAmount),
    meta: 'Bill amount',
    icon: 'i-lucide-indian-rupee',
    color: 'success'
  },
  {
    label: 'Paid',
    value: money(invoiceSummary.value.paidAmount),
    meta: 'Collected amount',
    icon: 'i-lucide-credit-card',
    color: 'neutral'
  },
  {
    label: 'Balance',
    value: money(invoiceSummary.value.balanceAmount),
    meta: 'Outstanding',
    icon: 'i-lucide-wallet',
    color: invoiceSummary.value.balanceAmount > 0 ? 'warning' : 'success'
  }
])

const tableRows = computed(() => invoices.value.map((invoice) => ({
  id: invoice.id,
  invoiceNumber: invoice.invoiceNumber || '-',
  onDate: formatDate(invoice.onDate),
  customerName: invoice.customerName || 'Walk-in Customer',
  billAmount: money(Number(invoice.billAmount || 0)),
  paidAmount: money(Number(invoice.paidAmount || 0)),
  balanceAmount: money(Number(invoice.balanceAmount || (Number(invoice.billAmount || 0) - Number(invoice.paidAmount || 0)))),
  invoiceStatus: invoice.invoiceStatus || 'Saved',
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
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'customerName', header: 'Customer' },
  { accessorKey: 'billAmount', header: 'Amount' },
  { accessorKey: 'paidAmount', header: 'Paid' },
  { accessorKey: 'balanceAmount', header: 'Balance' },
  {
    accessorKey: 'invoiceStatus',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.invoiceStatus === 'Cancelled' ? 'error' : 'success',
      variant: 'subtle'
    }, () => row.original.invoiceStatus)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => {
      const invoice = row.original.raw
      const actions = [
        h(UButton, {
          color: 'neutral',
          variant: 'ghost',
          icon: 'i-lucide-file-text',
          label: 'Receipt',
          onClick: () => viewReceipt(invoice.id)
        })
      ]

      if (canDelete.value && invoice.invoiceStatus !== 'Cancelled') {
        actions.push(h(UButton, {
          color: 'error',
          variant: 'ghost',
          icon: 'i-lucide-ban',
          label: 'Cancel',
          onClick: () => askCancel(invoice)
        }))
      }

      return h('div', { class: 'table-action-buttons' }, actions)
    }
  }
]

function emptySaleForm() {
  return {
    customerName: 'Walk-in Customer',
    customerMobileNumber: '',
    paymentMode: 0,
    paidAmount: 0,
    billDiscountAmount: 0,
    selectedProductId: '',
    quantity: 1,
    lineDiscount: 0,
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
    const [companyRows, storeRows, productRows, invoiceRows, bankAccountRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any[]>('billing/sales/recent?take=100'),
      api.list<any>('bank-accounts')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    invoices.value = invoiceRows
    bankAccounts.value = bankAccountRows
  } catch (error) {
    feedback.failed('Billing refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  Object.assign(saleForm, emptySaleForm())
  saleCart.value = []
  saleOpen.value = true
}

function addToCart() {
  if (!selectedProduct.value) {
    feedback.notify('Product missing', 'Select a product before adding to cart.', 'warning')
    return
  }

  saleCart.value.push({
    productId: selectedProduct.value.id,
    name: selectedProduct.value.name,
    barcode: selectedProduct.value.barcode,
    quantity: Number(saleForm.quantity || 0),
    mrp: Number(selectedProduct.value.mrp || 0),
    discountAmount: Number(saleForm.lineDiscount || 0)
  })

  saleForm.selectedProductId = ''
  saleForm.quantity = 1
  saleForm.lineDiscount = 0
  saleForm.paidAmount = payableTotal.value
}

function removeCartItem(index: number) {
  saleCart.value.splice(index, 1)
  saleForm.paidAmount = payableTotal.value
}

async function submitSale() {
  saving.value = true
  try {
    const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
    const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
    const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
    const storeId = workspace.storeId.value || setupStatus.value?.storeId || stores.value[0]?.id

    if (!companyId || !storeGroupId || !storeId) {
      throw new Error('Run quick setup before billing.')
    }

    if (saleCart.value.length === 0) {
      throw new Error('Add at least one item to the bill.')
    }

    if (requiresBankAccount.value && !saleForm.bankAccountId) {
      throw new Error('Select bank account for non-cash payment.')
    }

    const response = await api.create<any>('billing/sales', {
      companyId,
      storeGroupId,
      storeId,
      customerName: saleForm.customerName,
      customerMobileNumber: saleForm.customerMobileNumber,
      paymentMode: Number(saleForm.paymentMode),
      bankAccountId: requiresBankAccount.value ? saleForm.bankAccountId : null,
      paidAmount: Number(saleForm.paidAmount || 0),
      billDiscountAmount: Number(saleForm.billDiscountAmount || 0),
      items: saleCart.value.map((item) => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: item.quantity,
        mrp: item.mrp,
        discountAmount: item.discountAmount
      }))
    })

    feedback.saved(`Invoice ${response.invoiceNumber || ''}`.trim())
    saleOpen.value = false
    await viewReceipt(response.invoiceId)
    await refresh()
  } catch (error) {
    feedback.failed('Could not save invoice', error)
  } finally {
    saving.value = false
  }
}

async function viewReceipt(invoiceId: string) {
  try {
    selectedReceipt.value = await api.get<any>(`billing/sales/${invoiceId}/receipt`)
  } catch (error) {
    feedback.failed('Could not open receipt', error)
  }
}

function askCancel(invoice: any) {
  if (invoice.invoiceStatus === 'Cancelled') {
    return
  }

  pendingCancel.value = invoice
  cancelOpen.value = true
}

async function confirmCancel() {
  if (!pendingCancel.value) {
    return
  }

  cancelling.value = true
  try {
    await api.create<any>(`billing/sales/${pendingCancel.value.id}/cancel`, {
      reason: 'Cancelled from billing page'
    })

    if (selectedReceipt.value?.id === pendingCancel.value.id) {
      selectedReceipt.value = null
    }

    feedback.notify('Invoice cancelled', 'Stock quantities were reversed.', 'warning')
    cancelOpen.value = false
    pendingCancel.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not cancel invoice', error)
  } finally {
    cancelling.value = false
  }
}

function printReceipt() {
  window.print()
}

function lineTotal(item: any) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
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

watch(() => saleForm.paymentMode, () => {
  if (requiresBankAccount.value && !saleForm.bankAccountId) {
    saleForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => saleForm.paidAmount, () => {
  if (requiresBankAccount.value && !saleForm.bankAccountId) {
    saleForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Billing"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Sales Billing"
        description="Create POS invoices, print receipts, and cancel invoices with stock reversal."
        icon="i-lucide-receipt-indian-rupee"
        primary-label="New Invoice"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${invoices.length} invoices` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" label="New Invoice" @click="startCreate" />
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
              <h2>Invoice Register</h2>
              <p>Search invoice number, customer, status, or date.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search invoice or customer"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Invoice"
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
          title="No invoices found"
          description="Create the first sales invoice from the POS billing workflow."
          icon="i-lucide-receipt-indian-rupee"
          action-label="New Invoice"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="saleOpen"
        title="New Sales Invoice"
        description="Select products, add quantities, collect payment, and save the invoice."
        submit-label="Save Invoice"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="saving"
        @submit="submitSale"
      >
        <UFormField label="Customer">
          <UInput v-model="saleForm.customerName" />
        </UFormField>
        <UFormField label="Mobile">
          <UInput v-model="saleForm.customerMobileNumber" />
        </UFormField>

        <USeparator label="Item" />

        <UFormField label="Product">
          <USelect v-model="saleForm.selectedProductId" :items="productOptions" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Quantity">
            <UInput v-model="saleForm.quantity" min="1" type="number" />
          </UFormField>
          <UFormField label="Line discount">
            <UInput v-model="saleForm.lineDiscount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Item" type="button" @click="addToCart" />

        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Qty</th>
                <th>Total</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in saleCart" :key="`${item.productId}-${index}`">
                <td>{{ item.name }}</td>
                <td>{{ item.quantity }}</td>
                <td>{{ money(lineTotal(item)) }}</td>
                <td>
                  <UButton color="error" variant="ghost" icon="i-lucide-x" size="xs" type="button" @click="removeCartItem(index)" />
                </td>
              </tr>
              <tr v-if="saleCart.length === 0">
                <td colspan="4">No items</td>
              </tr>
            </tbody>
          </table>
        </div>

        <USeparator label="Payment" />

        <UFormField label="Payment">
          <USelect v-model="saleForm.paymentMode" :items="paymentModeOptions" />
        </UFormField>
        <UFormField v-if="requiresBankAccount" label="Bank account" required>
          <USelect v-model="saleForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Bill discount">
            <UInput v-model="saleForm.billDiscountAmount" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Paid">
            <UInput v-model="saleForm.paidAmount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <div class="payroll-summary">
          <span>Cart total</span><strong>{{ money(cartTotal) }}</strong>
          <span>Discount</span><strong>{{ money(Number(saleForm.billDiscountAmount || 0)) }}</strong>
          <span>Payable</span><strong>{{ money(payableTotal) }}</strong>
        </div>
      </UiFormSlideover>

      <UModal v-model:open="receiptOpen" title="Invoice Receipt" :ui="{ content: 'max-w-3xl' }">
        <template #body>
          <div v-if="selectedReceipt" class="receipt-print">
            <header class="receipt-header">
              <h2>{{ selectedReceipt.companyName }}</h2>
              <p>{{ selectedReceipt.storeName }}</p>
              <p>Invoice {{ selectedReceipt.invoiceNumber }}</p>
              <p>{{ new Date(selectedReceipt.onDate).toLocaleString() }}</p>
            </header>

            <div class="receipt-customer">
              <span>{{ selectedReceipt.customerName }}</span>
              <span>{{ selectedReceipt.customerMobileNumber }}</span>
            </div>

            <table class="receipt-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Qty</th>
                  <th>MRP</th>
                  <th>Tax</th>
                  <th>Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in selectedReceipt.items" :key="`${item.barcode}-${item.productName}`">
                  <td>{{ item.productName }}</td>
                  <td>{{ item.quantity }}</td>
                  <td>{{ money(Number(item.mrp || 0)) }}</td>
                  <td>{{ money(Number(item.taxAmount || 0)) }}</td>
                  <td>{{ money(Number(item.amount || 0)) }}</td>
                </tr>
              </tbody>
            </table>

            <div class="receipt-totals">
              <span>MRP</span><strong>{{ money(Number(selectedReceipt.mrp || selectedReceipt.MRP || 0)) }}</strong>
              <span>Discount</span><strong>{{ money(Number(selectedReceipt.discountAmount || 0)) }}</strong>
              <span>Tax</span><strong>{{ money(Number(selectedReceipt.taxAmount || 0)) }}</strong>
              <span>Round off</span><strong>{{ money(Number(selectedReceipt.roundOff || 0)) }}</strong>
              <span>Bill amount</span><strong>{{ money(Number(selectedReceipt.billAmount || 0)) }}</strong>
              <span>Paid</span><strong>{{ money(Number(selectedReceipt.paidAmount || 0)) }}</strong>
              <span>Balance</span><strong>{{ money(Number(selectedReceipt.balanceAmount || 0)) }}</strong>
            </div>

            <footer class="receipt-footer">
              Thank you for shopping with us.
            </footer>
          </div>
        </template>

        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Close" @click="receiptOpen = false" />
            <UButton icon="i-lucide-printer" label="Print" @click="printReceipt" />
          </div>
        </template>
      </UModal>

      <UiConfirmDeleteModal
        v-model:open="cancelOpen"
        title="Cancel Invoice"
        :description="`Cancel invoice ${pendingCancel?.invoiceNumber || ''}? Stock will be reversed.`"
        confirm-label="Cancel Invoice"
        :loading="cancelling"
        @confirm="confirmCancel"
      />
    </section>
  </AppShell>
</template>

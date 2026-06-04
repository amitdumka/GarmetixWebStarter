<script setup lang="ts">
import {
  CreditCard,
  FileText,
  PackagePlus,
  Printer,
  ReceiptIndianRupee
} from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const invoices = ref<any[]>([])
const selectedReceipt = ref<any | null>(null)
const loading = ref(false)
const setupStatus = ref<any | null>(null)
const viewMode = ref<'list' | 'create'>('list')
const saleMessage = ref('')

const saleForm = reactive({
  customerName: 'Walk-in Customer',
  customerMobileNumber: '',
  paymentMode: 0,
  paidAmount: 0,
  billDiscountAmount: 0,
  selectedProductId: '',
  quantity: 1,
  lineDiscount: 0
})
const saleCart = ref<any[]>([])

const selectedProduct = computed(() => products.value.find((item) => item.id === saleForm.selectedProductId))
const cartTotal = computed(() => {
  return saleCart.value.reduce((sum, item) => sum + ((item.mrp - item.discountAmount) * item.quantity), 0)
})
const payableTotal = computed(() => Math.max(cartTotal.value - Number(saleForm.billDiscountAmount), 0))

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, invoiceRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any[]>('billing/sales/recent?take=100')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    invoices.value = invoiceRows
  } finally {
    loading.value = false
  }
}

function addToCart() {
  if (!selectedProduct.value) {
    saleMessage.value = 'Select a product before adding to cart.'
    return
  }

  saleCart.value.push({
    productId: selectedProduct.value.id,
    name: selectedProduct.value.name,
    barcode: selectedProduct.value.barcode,
    quantity: Number(saleForm.quantity),
    mrp: Number(selectedProduct.value.mrp || 0),
    discountAmount: Number(saleForm.lineDiscount)
  })

  saleForm.selectedProductId = ''
  saleForm.quantity = 1
  saleForm.lineDiscount = 0
  saleForm.paidAmount = payableTotal.value
  saleMessage.value = ''
}

function removeCartItem(index: number) {
  saleCart.value.splice(index, 1)
  saleForm.paidAmount = payableTotal.value
}

async function submitSale() {
  saleMessage.value = ''

  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    saleMessage.value = 'Run quick setup before billing.'
    return
  }

  if (saleCart.value.length === 0) {
    saleMessage.value = 'Add at least one item to the bill.'
    return
  }

  const response = await api.create<any>('billing/sales', {
    companyId,
    storeGroupId,
    storeId,
    customerName: saleForm.customerName,
    customerMobileNumber: saleForm.customerMobileNumber,
    paymentMode: Number(saleForm.paymentMode),
    paidAmount: Number(saleForm.paidAmount),
    billDiscountAmount: Number(saleForm.billDiscountAmount),
    items: saleCart.value.map((item) => ({
      productId: item.productId,
      barcode: item.barcode,
      quantity: item.quantity,
      mrp: item.mrp,
      discountAmount: item.discountAmount
    }))
  })

  saleCart.value = []
  saleForm.billDiscountAmount = 0
  saleForm.paidAmount = 0
  saleMessage.value = `Invoice ${response.invoiceNumber} saved.`
  viewMode.value = 'list'
  await viewReceipt(response.invoiceId)
  await refresh()
}

async function viewReceipt(invoiceId: string) {
  selectedReceipt.value = await api.get<any>(`billing/sales/${invoiceId}/receipt`)
}

async function cancelInvoice(invoice: any) {
  if (invoice.invoiceStatus === 'Cancelled') {
    return
  }

  const confirmed = window.confirm(`Cancel invoice ${invoice.invoiceNumber}? Stock will be reversed.`)
  if (!confirmed) {
    return
  }

  await api.create<any>(`billing/sales/${invoice.id}/cancel`, {
    reason: 'Cancelled from billing page'
  })

  if (selectedReceipt.value?.id === invoice.id) {
    selectedReceipt.value = null
  }

  await refresh()
}

function closeReceipt() {
  selectedReceipt.value = null
}

function printReceipt() {
  window.print()
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
    title="Billing"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Sales Invoices</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${invoices.length} invoices` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <FileText :size="16" />
              List
            </button>
            <button class="button" type="button" @click="viewMode = 'create'">
              <ReceiptIndianRupee :size="16" />
              New Invoice
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <table class="table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Date</th>
                <th>Customer</th>
                <th>Amount</th>
                <th>Paid</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="invoice in invoices" :key="invoice.id">
                <td>{{ invoice.invoiceNumber }}</td>
                <td>{{ new Date(invoice.onDate).toLocaleDateString() }}</td>
                <td>{{ invoice.customerName }}</td>
                <td>{{ Number(invoice.billAmount).toFixed(2) }}</td>
                <td>{{ Number(invoice.paidAmount).toFixed(2) }}</td>
                <td><span class="status" :class="invoice.invoiceStatus === 'Cancelled' ? 'danger' : 'ok'">{{ invoice.invoiceStatus }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="viewReceipt(invoice.id)">
                    <FileText :size="16" />
                    Receipt
                  </button>
                  <button
                    v-if="invoice.invoiceStatus !== 'Cancelled'"
                    class="button danger-button"
                    type="button"
                    @click="cancelInvoice(invoice)"
                  >
                    Cancel
                  </button>
                </td>
              </tr>
              <tr v-if="invoices.length === 0">
                <td colspan="7">No invoices</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="pos-grid">
          <div class="form-grid">
            <div class="field">
              <label for="customerName">Customer</label>
              <input id="customerName" v-model="saleForm.customerName" />
            </div>
            <div class="field">
              <label for="customerMobile">Mobile</label>
              <input id="customerMobile" v-model="saleForm.customerMobileNumber" />
            </div>
            <div class="field">
              <label for="saleProduct">Product</label>
              <select id="saleProduct" v-model="saleForm.selectedProductId">
                <option value="">Select product</option>
                <option v-for="product in products" :key="product.id" :value="product.id">
                  {{ product.name }} - {{ product.barcode }}
                </option>
              </select>
            </div>
            <div class="field">
              <label for="saleQty">Quantity</label>
              <input id="saleQty" v-model="saleForm.quantity" min="1" type="number" />
            </div>
            <div class="field">
              <label for="lineDiscount">Line discount</label>
              <input id="lineDiscount" v-model="saleForm.lineDiscount" min="0" type="number" />
            </div>
            <button class="button secondary" type="button" @click="addToCart">
              <PackagePlus :size="16" />
              Add Item
            </button>
          </div>

          <div class="pos-cart">
            <table class="table">
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
                  <td>{{ ((item.mrp - item.discountAmount) * item.quantity).toFixed(2) }}</td>
                  <td>
                    <button class="icon-button" type="button" @click="removeCartItem(index)">x</button>
                  </td>
                </tr>
                <tr v-if="saleCart.length === 0">
                  <td colspan="4">No items</td>
                </tr>
              </tbody>
            </table>

            <div class="payment-grid">
              <div class="field">
                <label for="paymentMode">Payment</label>
                <select id="paymentMode" v-model="saleForm.paymentMode">
                  <option :value="0">Cash</option>
                  <option :value="1">Card</option>
                  <option :value="2">UPI</option>
                  <option :value="7">Cheque</option>
                </select>
              </div>
              <div class="field">
                <label for="billDiscount">Bill discount</label>
                <input id="billDiscount" v-model="saleForm.billDiscountAmount" min="0" type="number" />
              </div>
              <div class="field">
                <label for="paidAmount">Paid</label>
                <input id="paidAmount" v-model="saleForm.paidAmount" min="0" type="number" />
              </div>
              <div class="pos-total">
                {{ payableTotal.toFixed(2) }}
              </div>
              <button class="button" type="button" @click="submitSale">
                <CreditCard :size="16" />
                Save Invoice
              </button>
            </div>
            <p v-if="saleMessage" class="setup-message">{{ saleMessage }}</p>
          </div>
        </div>
      </section>
    </section>

    <div v-if="selectedReceipt" class="receipt-overlay">
      <section class="receipt-sheet">
        <div class="receipt-actions">
          <button class="button secondary" type="button" @click="closeReceipt">Close</button>
          <button class="button" type="button" @click="printReceipt">
            <Printer :size="16" />
            Print
          </button>
        </div>

        <div class="receipt-print">
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
                <td>{{ Number(item.mrp).toFixed(2) }}</td>
                <td>{{ Number(item.taxAmount).toFixed(2) }}</td>
                <td>{{ Number(item.amount).toFixed(2) }}</td>
              </tr>
            </tbody>
          </table>

          <div class="receipt-totals">
            <span>MRP</span><strong>{{ Number(selectedReceipt.mrp).toFixed(2) }}</strong>
            <span>Discount</span><strong>{{ Number(selectedReceipt.discountAmount).toFixed(2) }}</strong>
            <span>Tax</span><strong>{{ Number(selectedReceipt.taxAmount).toFixed(2) }}</strong>
            <span>Round off</span><strong>{{ Number(selectedReceipt.roundOff).toFixed(2) }}</strong>
            <span>Bill amount</span><strong>{{ Number(selectedReceipt.billAmount).toFixed(2) }}</strong>
            <span>Paid</span><strong>{{ Number(selectedReceipt.paidAmount).toFixed(2) }}</strong>
            <span>Balance</span><strong>{{ Number(selectedReceipt.balanceAmount).toFixed(2) }}</strong>
          </div>

          <footer class="receipt-footer">
            Thank you for shopping with us.
          </footer>
        </div>
      </section>
    </div>
  </AppShell>
</template>

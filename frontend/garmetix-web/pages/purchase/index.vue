<script setup lang="ts">
import { CreditCard, FileText, PackagePlus } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const purchaseInvoices = ref<any[]>([])
const loading = ref(false)
const setupStatus = ref<any | null>(null)
const viewMode = ref<'list' | 'create'>('list')
const purchaseMessage = ref('')

const purchaseForm = reactive({
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
  discountAmount: 0
})
const purchaseCart = ref<any[]>([])

const selectedPurchaseProduct = computed(() => products.value.find((item) => item.id === purchaseForm.selectedProductId))
const purchaseCartTotal = computed(() => {
  return purchaseCart.value.reduce((sum, item) => sum + Math.max((item.costPrice - item.discountAmount) * item.quantity, 0), 0)
})

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, purchaseRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.list<any>('purchase-invoices')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    purchaseInvoices.value = purchaseRows
  } finally {
    loading.value = false
  }
}

function addPurchaseItem() {
  const selected = selectedPurchaseProduct.value
  const productName = selected?.name || purchaseForm.productName
  const barcode = selected?.barcode || purchaseForm.barcode

  if (!productName || !barcode) {
    purchaseMessage.value = 'Select an existing product or enter product name and barcode.'
    return
  }

  purchaseCart.value.push({
    productId: selected?.id || null,
    productName,
    barcode,
    quantity: Number(purchaseForm.quantity),
    costPrice: Number(purchaseForm.costPrice),
    mrp: Number(purchaseForm.mrp || selected?.mrp || 0),
    discountAmount: Number(purchaseForm.discountAmount)
  })

  purchaseForm.selectedProductId = ''
  purchaseForm.productName = ''
  purchaseForm.barcode = ''
  purchaseForm.quantity = 1
  purchaseForm.costPrice = 0
  purchaseForm.mrp = 0
  purchaseForm.discountAmount = 0
  purchaseForm.paidAmount = purchaseCartTotal.value
  purchaseMessage.value = ''
}

function removePurchaseItem(index: number) {
  purchaseCart.value.splice(index, 1)
  purchaseForm.paidAmount = purchaseCartTotal.value
}

async function submitPurchase() {
  purchaseMessage.value = ''

  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    purchaseMessage.value = 'Run quick setup before purchase inward.'
    return
  }

  if (purchaseCart.value.length === 0) {
    purchaseMessage.value = 'Add at least one item to the inward cart.'
    return
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
    paidAmount: Number(purchaseForm.paidAmount),
    frightAmount: Number(purchaseForm.frightAmount),
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

  purchaseCart.value = []
  purchaseForm.invoiceNumber = ''
  purchaseForm.inwardNumber = ''
  purchaseForm.paidAmount = 0
  purchaseForm.frightAmount = 0
  purchaseMessage.value = `Inward ${response.inwardNumber} saved and stock updated.`
  viewMode.value = 'list'
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
    title="Purchase"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Purchase Invoices</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${purchaseInvoices.length} invoices` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <FileText :size="16" />
              List
            </button>
            <button class="button" type="button" @click="viewMode = 'create'">
              <PackagePlus :size="16" />
              New Inward
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <table class="table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Inward</th>
                <th>Vendor</th>
                <th>Amount</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="invoice in purchaseInvoices" :key="invoice.id">
                <td>{{ invoice.invoiceNumber }}</td>
                <td>{{ invoice.inwardNumber }}</td>
                <td>{{ invoice.vendorName }}</td>
                <td>{{ Number(invoice.billAmount).toFixed(2) }}</td>
                <td><span class="status ok">{{ invoice.invoiceStatus }}</span></td>
              </tr>
              <tr v-if="purchaseInvoices.length === 0">
                <td colspan="5">No purchase invoices</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="pos-grid">
          <div class="form-grid">
            <div class="field">
              <label for="vendorName">Vendor</label>
              <input id="vendorName" v-model="purchaseForm.vendorName" required />
            </div>
            <div class="field">
              <label for="vendorMobile">Vendor mobile</label>
              <input id="vendorMobile" v-model="purchaseForm.vendorMobileNumber" />
            </div>
            <div class="field">
              <label for="vendorGstin">Vendor GSTIN</label>
              <input id="vendorGstin" v-model="purchaseForm.vendorGstin" />
            </div>
            <div class="field">
              <label for="purchaseInvoice">Supplier invoice</label>
              <input id="purchaseInvoice" v-model="purchaseForm.invoiceNumber" />
            </div>
            <div class="field">
              <label for="purchaseProduct">Existing product</label>
              <select id="purchaseProduct" v-model="purchaseForm.selectedProductId">
                <option value="">New product</option>
                <option v-for="product in products" :key="product.id" :value="product.id">
                  {{ product.name }} - {{ product.barcode }}
                </option>
              </select>
            </div>
            <div class="field">
              <label for="purchaseProductName">Product name</label>
              <input id="purchaseProductName" v-model="purchaseForm.productName" :disabled="!!purchaseForm.selectedProductId" />
            </div>
            <div class="field">
              <label for="purchaseBarcode">Barcode</label>
              <input id="purchaseBarcode" v-model="purchaseForm.barcode" :disabled="!!purchaseForm.selectedProductId" />
            </div>
            <div class="field">
              <label for="purchaseQty">Quantity</label>
              <input id="purchaseQty" v-model="purchaseForm.quantity" min="1" type="number" />
            </div>
            <div class="field">
              <label for="purchaseCost">Cost price</label>
              <input id="purchaseCost" v-model="purchaseForm.costPrice" min="0" type="number" />
            </div>
            <div class="field">
              <label for="purchaseMrp">MRP</label>
              <input id="purchaseMrp" v-model="purchaseForm.mrp" min="0" type="number" />
            </div>
            <div class="field">
              <label for="purchaseDiscount">Unit discount</label>
              <input id="purchaseDiscount" v-model="purchaseForm.discountAmount" min="0" type="number" />
            </div>
            <button class="button secondary" type="button" @click="addPurchaseItem">
              <PackagePlus :size="16" />
              Add Inward Item
            </button>
          </div>

          <div class="pos-cart">
            <table class="table">
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
                  <td>{{ Math.max((item.costPrice - item.discountAmount) * item.quantity, 0).toFixed(2) }}</td>
                  <td>
                    <button class="icon-button" type="button" @click="removePurchaseItem(index)">x</button>
                  </td>
                </tr>
                <tr v-if="purchaseCart.length === 0">
                  <td colspan="4">No inward items</td>
                </tr>
              </tbody>
            </table>

            <div class="payment-grid">
              <div class="field">
                <label for="purchasePayment">Payment</label>
                <select id="purchasePayment" v-model="purchaseForm.paymentMode">
                  <option :value="0">Cash</option>
                  <option :value="1">Card</option>
                  <option :value="2">UPI</option>
                  <option :value="7">Cheque</option>
                </select>
              </div>
              <div class="field">
                <label for="frightAmount">Freight</label>
                <input id="frightAmount" v-model="purchaseForm.frightAmount" min="0" type="number" />
              </div>
              <div class="field">
                <label for="purchasePaid">Paid</label>
                <input id="purchasePaid" v-model="purchaseForm.paidAmount" min="0" type="number" />
              </div>
              <div class="pos-total">
                {{ (purchaseCartTotal + Number(purchaseForm.frightAmount)).toFixed(2) }}
              </div>
              <button class="button" type="button" @click="submitPurchase">
                <CreditCard :size="16" />
                Save Inward
              </button>
            </div>
            <p v-if="purchaseMessage" class="setup-message">{{ purchaseMessage }}</p>
          </div>
        </div>
      </section>
    </section>
  </AppShell>
</template>

<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const lookup = ref<any>({ vendors: [], taxes: [], units: [], productTypes: [], productGroups: [], categories: [], subCategories: [] })
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const cart = ref<any[]>([])

const form = reactive<any>({
  vendorId: '',
  vendorName: 'Default Supplier',
  vendorMobileNumber: '',
  vendorGstin: '',
  invoiceNumber: '',
  inwardNumber: '',
  supplierInvoiceDate: todayInputDate(),
  dueDate: todayInputDate(45),
  paymentMode: 0,
  paidAmount: 0,
  frightAmount: 0,
  bankAccountId: null,
  productName: '',
  barcode: '',
  hsnCode: '',
  productUnit: 2,
  productType: 0,
  productGroup: 0,
  quantity: 1,
  costPrice: 0,
  mrp: 0,
  discountAmount: 0,
  taxId: '',
  productCategoryId: '',
  productSubCategoryId: ''
})

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

const vendorOptions = computed(() => [
  { value: '', label: 'New supplier / manual entry' },
  ...(lookup.value.vendors?.map((vendor: any) => ({ value: vendor.id, label: `${vendor.name || 'Supplier'}${vendor.gstin ? ` | ${vendor.gstin}` : ''}` })) || [])
])
const selectedVendor = computed(() => lookup.value.vendors?.find((vendor: any) => vendor.id === form.vendorId) || null)
const unitOptions = computed(() => lookup.value.units || [])
const productTypeOptions = computed(() => lookup.value.productTypes || [])
const productGroupOptions = computed(() => lookup.value.productGroups || [])
const categoryOptions = computed(() => lookup.value.categories?.map((item: any) => ({ value: item.id, label: item.name })) || [])
const subCategoryOptions = computed(() => lookup.value.subCategories?.filter((item: any) => !form.productCategoryId || item.categoryId === form.productCategoryId)?.map((item: any) => ({ value: item.id, label: item.name })) || [])
const taxOptions = computed(() => lookup.value.taxes?.map((item: any) => ({ value: item.id, label: `${item.name || 'GST'} - ${Number(item.rate || 0).toFixed(2)}%` })) || [])
const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({ value: account.id, label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim() })))
const requiresBankAccount = computed(() => Number(form.paidAmount || 0) > 0 && Number(form.paymentMode) !== 0)
const cartTotal = computed(() => cart.value.reduce((sum, item) => sum + lineTotal(item), 0))
const payableTotal = computed(() => cartTotal.value + Number(form.frightAmount || 0))

function todayInputDate(offsetDays = 0) {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  return date.toISOString().slice(0, 10)
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [companyRows, storeRows, lookupRows, bankRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('purchase/lookup-options'),
      api.list<any>('bank-accounts')
    ])
    companies.value = companyRows
    stores.value = storeRows
    lookup.value = lookupRows
    bankAccounts.value = bankRows
  } catch (error) {
    feedback.failed('Purchase inward setup failed', error)
  } finally {
    loading.value = false
  }
}

function applySelectedVendor() {
  if (!selectedVendor.value) return
  form.vendorName = selectedVendor.value.name || form.vendorName
  form.vendorMobileNumber = selectedVendor.value.mobileNumber || ''
  form.vendorGstin = selectedVendor.value.gstin || ''
}

function addItem() {
  if (!form.productName || !form.barcode) {
    feedback.notify('Item missing', 'Enter product name and barcode before adding.', 'warning')
    return
  }
  cart.value.push({
    productId: null,
    productName: form.productName,
    barcode: form.barcode,
    hsnCode: form.hsnCode || '',
    productUnit: Number(form.productUnit ?? 2),
    productType: Number(form.productType ?? 0),
    productGroup: Number(form.productGroup ?? 0),
    quantity: Number(form.quantity || 0),
    costPrice: Number(form.costPrice || 0),
    mrp: Number(form.mrp || 0),
    discountAmount: Number(form.discountAmount || 0),
    taxId: form.taxId || null,
    productCategoryId: form.productCategoryId || null,
    productSubCategoryId: form.productSubCategoryId || null
  })
  form.productName = ''
  form.barcode = ''
  form.hsnCode = ''
  form.quantity = 1
  form.costPrice = 0
  form.mrp = 0
  form.discountAmount = 0
  form.paidAmount = payableTotal.value
}

function removeItem(index: number) {
  cart.value.splice(index, 1)
  form.paidAmount = payableTotal.value
}

async function submitPurchase() {
  saving.value = true
  try {
    const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
    const companyId = workspace.companyId.value || selectedStore?.companyId || companies.value[0]?.id
    const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || stores.value[0]?.storeGroupId
    const storeId = workspace.storeId.value || stores.value[0]?.id
    if (!companyId || !storeGroupId || !storeId) throw new Error('Select company/store before saving inward.')
    if (!cart.value.length) throw new Error('Add at least one item to the inward cart.')
    if (requiresBankAccount.value && !form.bankAccountId) throw new Error('Select bank account for non-cash payment.')

    const response = await api.create<any>('purchase/inward', {
      companyId,
      storeGroupId,
      storeId,
      vendorId: form.vendorId || null,
      vendorName: form.vendorName,
      vendorMobileNumber: form.vendorMobileNumber,
      vendorGstin: form.vendorGstin,
      invoiceNumber: form.invoiceNumber,
      inwardNumber: form.inwardNumber,
      supplierInvoiceDate: form.supplierInvoiceDate,
      dueDate: form.dueDate,
      paymentMode: Number(form.paymentMode),
      paidAmount: Number(form.paidAmount || 0),
      frightAmount: Number(form.frightAmount || 0),
      bankAccountId: form.bankAccountId || null,
      items: cart.value
    })
    feedback.notify('Purchase inward saved', response.invoiceNumber || response.inwardNumber || 'Stock updated.', 'success')
    await navigateTo('/purchase')
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
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

watch(() => form.vendorId, applySelectedVendor)
watch(() => form.paymentMode, () => { if (requiresBankAccount.value && !form.bankAccountId) form.bankAccountId = bankAccounts.value[0]?.id || null })
watch(() => form.paidAmount, () => { if (requiresBankAccount.value && !form.bankAccountId) form.bankAccountId = bankAccounts.value[0]?.id || null })
onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="New Purchase Inward" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="New Purchase Inward"
        description="Full page inward entry for supplier invoice, products, stock receiving and first payment."
        icon="i-lucide-package-plus"
        primary-label="Save Inward"
        primary-icon="i-lucide-save"
        @primary="submitPurchase"
      >
        <template #actions>
          <UButton color="neutral" variant="subtle" icon="i-lucide-arrow-left" label="Back to Purchase" to="/purchase" />
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">{{ loading ? 'Loading' : 'Ready' }}</UBadge>
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-shopping-cart" color="primary" variant="subtle" /><div><p>Items</p><strong>{{ cart.length }}</strong><span>Inward lines</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-indian-rupee" color="success" variant="subtle" /><div><p>Total</p><strong>{{ money(payableTotal) }}</strong><span>Including freight</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-credit-card" color="neutral" variant="subtle" /><div><p>Paid</p><strong>{{ money(Number(form.paidAmount || 0)) }}</strong><span>First payment</span></div></div></UCard>
      </div>

      <UCard class="setup-card">
        <template #header><h2 class="section-title">Supplier and invoice details</h2></template>
        <div class="form-three-column">
          <UFormField label="Existing vendor"><USelect v-model="form.vendorId" :items="vendorOptions" /></UFormField>
          <UFormField label="Vendor" required><UInput v-model="form.vendorName" /></UFormField>
          <UFormField label="Vendor mobile"><UInput v-model="form.vendorMobileNumber" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Vendor GSTIN"><UInput v-model="form.vendorGstin" /></UFormField>
          <UFormField label="Supplier invoice"><UInput v-model="form.invoiceNumber" /></UFormField>
          <UFormField label="Inward number"><UInput v-model="form.inwardNumber" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Supplier invoice date"><UInput v-model="form.supplierInvoiceDate" type="date" /></UFormField>
          <UFormField label="Due date"><UInput v-model="form.dueDate" type="date" /></UFormField>
          <UFormField label="Freight"><UInput v-model="form.frightAmount" type="number" min="0" /></UFormField>
        </div>
      </UCard>

      <UCard class="setup-card">
        <template #header><h2 class="section-title">Add inward item</h2></template>
        <div class="form-four-column">
          <UFormField label="Product name" required><UInput v-model="form.productName" /></UFormField>
          <UFormField label="Barcode" required><UInput v-model="form.barcode" /></UFormField>
          <UFormField label="HSN"><UInput v-model="form.hsnCode" /></UFormField>
          <UFormField label="Unit"><USelect v-model="form.productUnit" :items="unitOptions" /></UFormField>
        </div>
        <div class="form-four-column">
          <UFormField label="Qty"><UInput v-model="form.quantity" type="number" min="0" /></UFormField>
          <UFormField label="Cost"><UInput v-model="form.costPrice" type="number" min="0" /></UFormField>
          <UFormField label="MRP"><UInput v-model="form.mrp" type="number" min="0" /></UFormField>
          <UFormField label="Discount"><UInput v-model="form.discountAmount" type="number" min="0" /></UFormField>
        </div>
        <div class="form-four-column">
          <UFormField label="GST"><USelect v-model="form.taxId" :items="taxOptions" /></UFormField>
          <UFormField label="Category"><USelect v-model="form.productCategoryId" :items="categoryOptions" /></UFormField>
          <UFormField label="Sub category"><USelect v-model="form.productSubCategoryId" :items="subCategoryOptions" /></UFormField>
          <UFormField label="Product type"><USelect v-model="form.productType" :items="productTypeOptions" /></UFormField>
        </div>
        <div class="inline-action-row"><UButton icon="i-lucide-plus" label="Add Item" @click="addItem" /></div>
      </UCard>

      <UiRegisterPanel title="Inward Cart" :description="`${cart.length} items | ${money(cartTotal)}`" :empty="cart.length === 0" empty-title="No items added" empty-description="Add products before saving inward." empty-icon="i-lucide-package-plus">
        <div class="planner-table-wrap">
          <table class="native-table"><thead><tr><th>Product</th><th>Barcode</th><th>Qty</th><th>Cost</th><th>MRP</th><th>Amount</th><th></th></tr></thead><tbody><tr v-for="(item, index) in cart" :key="`${item.barcode}-${index}`"><td>{{ item.productName }}</td><td>{{ item.barcode }}</td><td>{{ item.quantity }}</td><td>{{ money(item.costPrice) }}</td><td>{{ money(item.mrp) }}</td><td>{{ money(lineTotal(item)) }}</td><td><UButton color="error" variant="ghost" icon="i-lucide-trash-2" @click="removeItem(index)" /></td></tr></tbody></table>
        </div>
      </UiRegisterPanel>

      <UCard class="setup-card">
        <template #header><h2 class="section-title">Payment</h2></template>
        <div class="form-three-column">
          <UFormField label="Paid amount"><UInput v-model="form.paidAmount" type="number" min="0" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="form.paymentMode" :items="paymentModeOptions" /></UFormField>
          <UFormField v-if="requiresBankAccount" label="Bank account"><USelect v-model="form.bankAccountId" :items="bankAccountOptions" /></UFormField>
        </div>
        <div class="inline-action-row"><UButton icon="i-lucide-save" :loading="saving" label="Save Purchase Inward" @click="submitPurchase" /></div>
      </UCard>
    </section>
  </AppShell>
</template>

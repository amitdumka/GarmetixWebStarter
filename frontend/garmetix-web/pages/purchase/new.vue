<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const NONE = '__none__'
const MANUAL_VENDOR = '__manual__'

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const lookup = ref<any>({ vendors: [], taxes: [], units: [], productTypes: [], productGroups: [], categories: [], subCategories: [] })
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const productSaving = ref(false)
const cart = ref<any[]>([])
const productSearch = ref('')
const productResults = ref<any[]>([])
const productLookupLoading = ref(false)
const productDialogOpen = ref(false)

const form = reactive<any>({
  vendorId: MANUAL_VENDOR,
  vendorName: 'Default Supplier',
  vendorMobileNumber: '',
  vendorGstin: '',
  invoiceNumber: '',
  supplierInvoiceDate: todayInputDate(),
  dueDate: todayInputDate(45),
  paymentMode: 0,
  paidAmount: 0,
  frightAmount: 0,
  bankAccountId: null,
  productId: null,
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
  taxId: NONE,
  productCategoryId: NONE,
  productSubCategoryId: NONE
})

const newProduct = reactive<any>({
  name: '',
  barcode: '',
  hsnCode: '',
  description: '',
  mrp: 0,
  costPrice: 0,
  unit: 2,
  productType: 0,
  productGroup: 0,
  taxId: NONE,
  productCategoryId: NONE,
  productSubCategoryId: NONE
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
  { value: MANUAL_VENDOR, label: 'New supplier / manual entry' },
  ...(lookup.value.vendors?.map((vendor: any) => ({ value: vendor.id, label: `${vendor.name || 'Supplier'}${vendor.gstin ? ` | ${vendor.gstin}` : ''}` })) || [])
])
const selectedVendor = computed(() => form.vendorId === MANUAL_VENDOR ? null : (lookup.value.vendors?.find((vendor: any) => vendor.id === form.vendorId) || null))
const selectedStore = computed(() => stores.value.find((store) => store.id === workspace.storeId.value) || stores.value[0] || null)
const selectedCompanyId = computed(() => workspace.companyId.value || selectedStore.value?.companyId || companies.value[0]?.id || null)
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || selectedStore.value?.storeGroupId || stores.value[0]?.storeGroupId || null)
const selectedStoreId = computed(() => workspace.storeId.value || stores.value[0]?.id || null)
const safeStoreCode = computed(() => String(selectedStore.value?.storeCode || selectedStore.value?.code || 'STORE').trim().toUpperCase().replace(/[^A-Z0-9_-]/g, '') || 'STORE')
const inwardNumberPreview = computed(() => `${safeStoreCode.value}/${todayInputDate().slice(0, 7).replace('-', '')}/INW/auto`)

const unitOptions = computed(() => lookup.value.units || [])
const productTypeOptions = computed(() => lookup.value.productTypes || [])
const productGroupOptions = computed(() => lookup.value.productGroups || [])
const categoryOptions = computed(() => [
  { value: NONE, label: 'No category' },
  ...(lookup.value.categories?.map((item: any) => ({ value: item.id, label: item.name })) || [])
])
const subCategoryOptions = computed(() => [
  { value: NONE, label: 'No sub category' },
  ...(lookup.value.subCategories
    ?.filter((item: any) => form.productCategoryId === NONE || !form.productCategoryId || item.categoryId === form.productCategoryId)
    ?.map((item: any) => ({ value: item.id, label: item.name })) || [])
])
const newProductSubCategoryOptions = computed(() => [
  { value: NONE, label: 'No sub category' },
  ...(lookup.value.subCategories
    ?.filter((item: any) => newProduct.productCategoryId === NONE || !newProduct.productCategoryId || item.categoryId === newProduct.productCategoryId)
    ?.map((item: any) => ({ value: item.id, label: item.name })) || [])
])
const taxOptions = computed(() => [
  { value: NONE, label: 'Select GST / tax' },
  ...(lookup.value.taxes?.map((item: any) => ({ value: item.id, label: `${item.name || 'GST'} - ${Number(item.rate || item.compositeRate || 0).toFixed(2)}%` })) || [])
])
const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({ value: account.id, label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim() })))
const requiresBankAccount = computed(() => Number(form.paidAmount || 0) > 0 && Number(form.paymentMode) !== 0)
const cartTotal = computed(() => cart.value.reduce((sum, item) => sum + lineTotal(item), 0))
const payableTotal = computed(() => cartTotal.value + Number(form.frightAmount || 0))

function nullableSelect(value: any) {
  return value && value !== NONE ? value : null
}

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
    applyLookupDefaults()
  } catch (error) {
    feedback.failed('Purchase inward setup failed', error)
  } finally {
    loading.value = false
  }
}

function applyLookupDefaults() {
  if (form.taxId === NONE && lookup.value.taxes?.length === 1) form.taxId = lookup.value.taxes[0].id
  if (form.productCategoryId === NONE && lookup.value.categories?.length === 1) form.productCategoryId = lookup.value.categories[0].id
  if (newProduct.taxId === NONE && lookup.value.taxes?.length === 1) newProduct.taxId = lookup.value.taxes[0].id
  if (newProduct.productCategoryId === NONE && lookup.value.categories?.length === 1) newProduct.productCategoryId = lookup.value.categories[0].id
}

function applySelectedVendor() {
  if (!selectedVendor.value) return
  form.vendorName = selectedVendor.value.name || form.vendorName
  form.vendorMobileNumber = selectedVendor.value.mobileNumber || ''
  form.vendorGstin = selectedVendor.value.gstin || ''
}

async function searchProducts() {
  if (!productSearch.value.trim()) {
    productResults.value = []
    return
  }
  productLookupLoading.value = true
  try {
    const query = new URLSearchParams({ query: productSearch.value.trim(), take: '10' })
    if (selectedStoreId.value) query.set('storeId', selectedStoreId.value)
    productResults.value = await api.get<any[]>(`product-lookup?${query.toString()}`)
  } catch (error) {
    feedback.failed('Product lookup failed', error)
  } finally {
    productLookupLoading.value = false
  }
}

async function lookupBarcode() {
  if (!form.barcode?.trim()) return
  productLookupLoading.value = true
  try {
    const query = selectedStoreId.value ? `?storeId=${selectedStoreId.value}` : ''
    const row = await api.get<any>(`product-lookup/barcode/${encodeURIComponent(form.barcode.trim())}${query}`)
    applyProduct(row)
  } catch {
    productResults.value = []
  } finally {
    productLookupLoading.value = false
  }
}

function applyProduct(row: any) {
  form.productId = row.productId || null
  form.productName = row.name || row.productName || form.productName
  form.barcode = row.barcode || form.barcode
  form.hsnCode = row.hsnCode || row.hsn || ''
  form.mrp = Number(row.mrp || form.mrp || 0)
  form.taxId = row.taxId || NONE
  form.productCategoryId = row.productCategoryId || NONE
  form.productSubCategoryId = row.productSubCategoryId || NONE
  const unit = unitOptions.value.find((item: any) => String(item.label || item.value).toLowerCase() === String(row.unit || '').toLowerCase())
  if (unit) form.productUnit = unit.value
  productResults.value = []
  productSearch.value = ''
}

function openNewProductDialog() {
  newProduct.name = form.productName || productSearch.value || ''
  newProduct.barcode = form.barcode || ''
  newProduct.hsnCode = form.hsnCode || ''
  newProduct.description = ''
  newProduct.mrp = Number(form.mrp || 0)
  newProduct.costPrice = Number(form.costPrice || 0)
  newProduct.unit = Number(form.productUnit ?? 2)
  newProduct.productType = Number(form.productType ?? 0)
  newProduct.productGroup = Number(form.productGroup ?? 0)
  newProduct.taxId = form.taxId || NONE
  newProduct.productCategoryId = form.productCategoryId || NONE
  newProduct.productSubCategoryId = form.productSubCategoryId || NONE
  productDialogOpen.value = true
}

async function saveNewProduct() {
  if (!newProduct.name?.trim() || !newProduct.barcode?.trim()) {
    feedback.notify('Product missing', 'Product name and barcode are required.', 'warning')
    return
  }
  if (!selectedCompanyId.value || !selectedStoreGroupId.value || !selectedStoreId.value) {
    feedback.notify('Store missing', 'Select company/store before adding a product.', 'warning')
    return
  }
  productSaving.value = true
  try {
    const response = await api.create<any>('setup/quick-product', {
      name: newProduct.name,
      barcode: newProduct.barcode,
      mrp: Number(newProduct.mrp || 0),
      openingQuantity: 0,
      costPrice: Number(newProduct.costPrice || 0),
      companyId: selectedCompanyId.value,
      storeGroupId: selectedStoreGroupId.value,
      storeId: selectedStoreId.value,
      productCategoryId: nullableSelect(newProduct.productCategoryId),
      productSubCategoryId: nullableSelect(newProduct.productSubCategoryId),
      taxId: nullableSelect(newProduct.taxId),
      descriptions: newProduct.description || null,
      hsnCode: newProduct.hsnCode || null,
      unit: Number(newProduct.unit ?? 2),
      productType: Number(newProduct.productType ?? 0),
      productGroup: Number(newProduct.productGroup ?? 0)
    })
    const product = response.product || {}
    const stock = response.stock || {}
    applyProduct({
      productId: product.id,
      name: product.name,
      barcode: product.barcode,
      hsnCode: product.hsnCode,
      mrp: product.mrp,
      taxId: stock.taxId || nullableSelect(newProduct.taxId),
      productCategoryId: product.productCategoryId || nullableSelect(newProduct.productCategoryId),
      productSubCategoryId: product.productSubCategoryId || nullableSelect(newProduct.productSubCategoryId)
    })
    form.costPrice = Number(newProduct.costPrice || 0)
    form.productUnit = Number(newProduct.unit ?? form.productUnit)
    form.productType = Number(newProduct.productType ?? form.productType)
    form.productGroup = Number(newProduct.productGroup ?? form.productGroup)
    productDialogOpen.value = false
    feedback.notify('Product added', `${product.name || newProduct.name} is ready for this inward.`, 'success')
  } catch (error) {
    feedback.failed('Could not add product', error)
  } finally {
    productSaving.value = false
  }
}

function addItem() {
  if (!form.productName || !form.barcode) {
    feedback.notify('Item missing', 'Select or add a product before adding the inward line.', 'warning')
    return
  }
  cart.value.push({
    productId: form.productId || null,
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
    taxId: nullableSelect(form.taxId),
    productCategoryId: nullableSelect(form.productCategoryId),
    productSubCategoryId: nullableSelect(form.productSubCategoryId)
  })
  form.productId = null
  form.productName = ''
  form.barcode = ''
  form.hsnCode = ''
  form.quantity = 1
  form.costPrice = 0
  form.mrp = 0
  form.discountAmount = 0
  form.taxId = lookup.value.taxes?.length === 1 ? lookup.value.taxes[0].id : NONE
  form.productCategoryId = lookup.value.categories?.length === 1 ? lookup.value.categories[0].id : NONE
  form.productSubCategoryId = NONE
  form.paidAmount = payableTotal.value
}

function removeItem(index: number) {
  cart.value.splice(index, 1)
  form.paidAmount = payableTotal.value
}

async function submitPurchase() {
  saving.value = true
  try {
    const companyId = selectedCompanyId.value
    const storeGroupId = selectedStoreGroupId.value
    const storeId = selectedStoreId.value
    if (!companyId || !storeGroupId || !storeId) throw new Error('Select company/store before saving inward.')
    if (!cart.value.length) throw new Error('Add at least one item to the inward cart.')
    if (requiresBankAccount.value && !form.bankAccountId) throw new Error('Select bank account for non-cash payment.')

    const response = await api.create<any>('purchase/inward', {
      companyId,
      storeGroupId,
      storeId,
      vendorId: form.vendorId === MANUAL_VENDOR ? null : (form.vendorId || null),
      vendorName: form.vendorName,
      vendorMobileNumber: form.vendorMobileNumber,
      vendorGstin: form.vendorGstin,
      invoiceNumber: form.invoiceNumber,
      inwardNumber: null,
      supplierInvoiceDate: form.supplierInvoiceDate,
      dueDate: form.dueDate,
      paymentMode: Number(form.paymentMode),
      paidAmount: Number(form.paidAmount || 0),
      frightAmount: Number(form.frightAmount || 0),
      bankAccountId: form.bankAccountId || null,
      items: cart.value
    })
    feedback.notify('Purchase inward saved', response.inwardNumber || response.invoiceNumber || 'Stock updated.', 'success')
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
watch(productSearch, () => { if (productSearch.value.trim().length >= 2) searchProducts() })
onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="New Purchase Inward" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="New Purchase Inward"
        description="Full page inward entry with auto inward number, product lookup, inline product creation, stock receiving and first payment."
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
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-digit" color="primary" variant="subtle" /><div><p>Inward no.</p><strong>{{ inwardNumberPreview }}</strong><span>Auto generated on save</span></div></div></UCard>
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
          <UFormField label="Inward number"><UInput :model-value="inwardNumberPreview" readonly /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Supplier invoice date"><UInput v-model="form.supplierInvoiceDate" type="date" /></UFormField>
          <UFormField label="Due date"><UInput v-model="form.dueDate" type="date" /></UFormField>
          <UFormField label="Freight"><UInput v-model="form.frightAmount" type="number" min="0" /></UFormField>
        </div>
      </UCard>

      <UCard class="setup-card">
        <template #header>
          <div class="section-heading-row">
            <div>
              <h2 class="section-title">Add inward item</h2>
              <p class="section-description">Search an existing product or create a new product in the popup without leaving this purchase page.</p>
            </div>
            <UButton color="primary" variant="soft" icon="i-lucide-package-plus" label="New Product" @click="openNewProductDialog" />
          </div>
        </template>

        <div class="form-three-column">
          <UFormField label="Search product / barcode"><UInput v-model="productSearch" icon="i-lucide-search" placeholder="Type product, barcode or HSN" /></UFormField>
          <UFormField label="Barcode"><UInput v-model="form.barcode" @blur="lookupBarcode" /></UFormField>
          <UFormField label="Product name" required><UInput v-model="form.productName" /></UFormField>
        </div>

        <div v-if="productResults.length" class="planner-table-wrap mb-4">
          <table class="native-table">
            <thead><tr><th>Product</th><th>Barcode</th><th>Stock</th><th>MRP</th><th></th></tr></thead>
            <tbody>
              <tr v-for="row in productResults" :key="`${row.productId}-${row.barcode}`">
                <td>{{ row.name }}</td>
                <td>{{ row.barcode }}</td>
                <td>{{ row.availableQty }}</td>
                <td>{{ money(Number(row.mrp || 0)) }}</td>
                <td><UButton size="xs" icon="i-lucide-check" label="Use" @click="applyProduct(row)" /></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="form-four-column">
          <UFormField label="HSN"><UInput v-model="form.hsnCode" /></UFormField>
          <UFormField label="Unit"><USelect v-model="form.productUnit" :items="unitOptions" /></UFormField>
          <UFormField label="Qty"><UInput v-model="form.quantity" type="number" min="0" /></UFormField>
          <UFormField label="Cost"><UInput v-model="form.costPrice" type="number" min="0" /></UFormField>
        </div>
        <div class="form-four-column">
          <UFormField label="MRP"><UInput v-model="form.mrp" type="number" min="0" /></UFormField>
          <UFormField label="Discount"><UInput v-model="form.discountAmount" type="number" min="0" /></UFormField>
          <UFormField label="GST"><USelect v-model="form.taxId" :items="taxOptions" /></UFormField>
          <UFormField label="Category"><USelect v-model="form.productCategoryId" :items="categoryOptions" /></UFormField>
        </div>
        <div class="form-four-column">
          <UFormField label="Sub category"><USelect v-model="form.productSubCategoryId" :items="subCategoryOptions" /></UFormField>
          <UFormField label="Product type"><USelect v-model="form.productType" :items="productTypeOptions" /></UFormField>
          <UFormField label="Product group"><USelect v-model="form.productGroup" :items="productGroupOptions" /></UFormField>
          <div class="inline-action-row"><UButton icon="i-lucide-plus" label="Add Item" @click="addItem" /></div>
        </div>
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

    <UModal v-model:open="productDialogOpen" title="Add Product While Purchasing" :ui="{ content: 'sm:max-w-5xl xl:max-w-6xl' }">
      <template #body>
        <div class="form-three-column">
          <UFormField label="Product name" required><UInput v-model="newProduct.name" /></UFormField>
          <UFormField label="Barcode" required><UInput v-model="newProduct.barcode" /></UFormField>
          <UFormField label="HSN"><UInput v-model="newProduct.hsnCode" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="MRP"><UInput v-model="newProduct.mrp" type="number" min="0" /></UFormField>
          <UFormField label="Cost price"><UInput v-model="newProduct.costPrice" type="number" min="0" /></UFormField>
          <UFormField label="GST"><USelect v-model="newProduct.taxId" :items="taxOptions" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Category"><USelect v-model="newProduct.productCategoryId" :items="categoryOptions" /></UFormField>
          <UFormField label="Sub category"><USelect v-model="newProduct.productSubCategoryId" :items="newProductSubCategoryOptions" /></UFormField>
          <UFormField label="Unit"><USelect v-model="newProduct.unit" :items="unitOptions" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Product type"><USelect v-model="newProduct.productType" :items="productTypeOptions" /></UFormField>
          <UFormField label="Product group"><USelect v-model="newProduct.productGroup" :items="productGroupOptions" /></UFormField>
          <UFormField label="Description"><UInput v-model="newProduct.description" /></UFormField>
        </div>
      </template>
      <template #footer>
        <UButton color="neutral" variant="ghost" label="Cancel" @click="productDialogOpen = false" />
        <UButton icon="i-lucide-save" :loading="productSaving" label="Create and use product" @click="saveNewProduct" />
      </template>
    </UModal>
  </AppShell>
</template>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-plus-2" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">New Inward</h2>
          <p class="garmetix-dashboard-subtitle">
            Record a supplier invoice: vendor, items and payment in one entry. Inward number is generated automatically.
          </p>
        </div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="soft" to="/purchase">Back to Register</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert
      v-if="successResult"
      color="success"
      variant="subtle"
      icon="i-lucide-circle-check"
      :title="`Saved ${successResult.invoiceNumber}`"
      :description="`Inward ${successResult.inwardNumber} - Bill ${money(successResult.billAmount)} - ${successResult.itemCount} item(s)`"
    >
      <template #actions>
        <UButton size="xs" color="success" variant="solid" to="/purchase">Go to Register</UButton>
        <UButton size="xs" color="neutral" variant="ghost" @click="resetForNext">Add Another</UButton>
      </template>
    </UAlert>

    <section class="garmetix-section-card grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <UFormField label="Store">
        <USelect v-model="header.storeId" :items="storeItems" placeholder="Select store" @update:model-value="onStoreChanged" />
      </UFormField>
      <UFormField label="Vendor">
        <USelectMenu
          v-model="header.vendorId"
          :items="vendorItems"
          value-key="value"
          searchable
          placeholder="Existing vendor"
          @update:model-value="onVendorSelected"
        />
      </UFormField>
      <UFormField label="Invoice number (blank = auto)">
        <UInput v-model="header.invoiceNumber" placeholder="Supplier invoice no." />
      </UFormField>
      <UFormField label="Inward date">
        <UInput v-model="header.inwardDate" type="date" />
      </UFormField>
      <UFormField label="Supplier invoice date">
        <UInput v-model="header.supplierInvoiceDate" type="date" />
      </UFormField>
      <UFormField label="Due date">
        <UInput v-model="header.dueDate" type="date" />
      </UFormField>
      <UFormField label="Freight amount">
        <UInput v-model.number="header.frightAmount" type="number" min="0" step="0.01" />
      </UFormField>
      <div></div>

      <UFormField label="Vendor name" class="sm:col-span-2">
        <UInput v-model="header.vendorName" placeholder="Vendor / supplier name" />
      </UFormField>
      <UFormField label="Vendor mobile">
        <UInput v-model="header.vendorMobileNumber" placeholder="Mobile number" />
      </UFormField>
      <UFormField label="Vendor GSTIN">
        <UInput v-model="header.vendorGstin" placeholder="GSTIN" />
      </UFormField>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Add Item</h3>
      <div class="grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <UFormField label="Barcode / search" class="lg:col-span-2">
          <UInput v-model="itemForm.barcode" placeholder="Scan or type barcode" @keyup.enter="lookupBarcode" @blur="lookupBarcode" />
        </UFormField>
        <UFormField label="Product name" class="lg:col-span-2">
          <UInput v-model="itemForm.productName" placeholder="Product name" />
        </UFormField>
        <UFormField label="HSN code">
          <UInput v-model="itemForm.hsnCode" placeholder="HSN" />
        </UFormField>
        <UFormField label="Unit">
          <USelect v-model="itemForm.productUnit" :items="unitItems" />
        </UFormField>
        <UFormField label="Category">
          <USelect v-model="itemForm.productCategoryId" :items="categoryItems" />
        </UFormField>
        <UFormField label="Sub-category">
          <USelect v-model="itemForm.productSubCategoryId" :items="subCategoryItems" />
        </UFormField>
        <UFormField label="Tax">
          <USelect v-model="itemForm.taxId" :items="taxItems" />
        </UFormField>
        <UFormField label="Quantity">
          <UInput v-model.number="itemForm.quantity" type="number" min="0" step="0.01" />
        </UFormField>
        <UFormField label="Cost price">
          <UInput v-model.number="itemForm.costPrice" type="number" min="0" step="0.01" />
        </UFormField>
        <UFormField label="MRP">
          <UInput v-model.number="itemForm.mrp" type="number" min="0" step="0.01" />
        </UFormField>
        <UFormField label="Discount / unit">
          <UInput v-model.number="itemForm.discountAmount" type="number" min="0" step="0.01" />
        </UFormField>
        <div class="flex items-end">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" class="w-full" @click="addItem">Add Item</UButton>
        </div>
      </div>
      <p v-if="matchedProductName" class="mt-2 text-xs text-success">Matched existing product: {{ matchedProductName }}</p>
    </section>

    <section class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[900px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Product</th>
            <th class="border-b border-default p-3">Barcode</th>
            <th class="border-b border-default p-3 text-right">Qty</th>
            <th class="border-b border-default p-3 text-right">Cost</th>
            <th class="border-b border-default p-3 text-right">MRP</th>
            <th class="border-b border-default p-3 text-right">Line Total</th>
            <th class="border-b border-default p-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!cart.length">
            <td colspan="7" class="p-8 text-center text-muted">No items added yet.</td>
          </tr>
          <tr v-for="(item, index) in cart" :key="index">
            <td class="border-b border-default p-3">{{ item.productName }}</td>
            <td class="border-b border-default p-3">{{ item.barcode }}</td>
            <td class="border-b border-default p-3 text-right">{{ item.quantity }}</td>
            <td class="border-b border-default p-3 text-right">{{ money(item.costPrice) }}</td>
            <td class="border-b border-default p-3 text-right">{{ money(item.mrp) }}</td>
            <td class="border-b border-default p-3 text-right font-semibold">{{ money(lineTotal(item)) }}</td>
            <td class="border-b border-default p-3 text-right">
              <UButton icon="i-lucide-x" size="xs" color="error" variant="ghost" @click="cart.splice(index, 1)" />
            </td>
          </tr>
        </tbody>
        <tfoot v-if="cart.length">
          <tr>
            <td colspan="5" class="p-3 text-right font-semibold">Total</td>
            <td class="p-3 text-right font-semibold">{{ money(cartTotal) }}</td>
            <td></td>
          </tr>
        </tfoot>
      </table>
    </section>

    <section class="garmetix-section-card grid gap-3 sm:grid-cols-3">
      <UFormField label="Paid amount">
        <UInput v-model.number="header.paidAmount" type="number" min="0" step="0.01" />
      </UFormField>
      <UFormField label="Payment mode">
        <USelect v-model="header.paymentMode" :items="paymentModeItems" />
      </UFormField>
      <UFormField v-if="requiresBank(header.paymentMode)" label="Bank account">
        <USelect v-model="header.bankAccountId" :items="bankAccountItems" />
      </UFormField>
      <div class="sm:col-span-3 flex justify-end">
        <UButton icon="i-lucide-save" color="primary" size="lg" :loading="saving" :disabled="!cart.length" @click="submitInward">
          Save Purchase Inward ({{ money(cartTotal) }})
        </UButton>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { getStoredUser } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

interface CartItem {
  productId: string | null
  productName: string
  barcode: string
  hsnCode: string
  quantity: number
  costPrice: number
  mrp: number
  discountAmount: number
  taxId: string | null
  productCategoryId: string | null
  productSubCategoryId: string | null
  productUnit: number | null
}

useHead({ title: 'New Inward - Garmetix Back Office' })

const { get, post } = useMainApiClient()

const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallets', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 },
  { label: 'Demand Draft', value: 8 }, { label: 'Others', value: 14 }
]

function toInputDate(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}

const error = ref('')
const saving = ref(false)
const matchedProductName = ref('')
const successResult = ref<{ invoiceNumber: string, inwardNumber: string, billAmount: number, itemCount: number } | null>(null)

const stores = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const categories = ref<ApiRecord[]>([])
const subCategories = ref<ApiRecord[]>([])
const taxes = ref<ApiRecord[]>([])
const units = ref<ApiRecord[]>([])

const header = reactive({
  companyId: '',
  storeGroupId: '',
  storeId: '',
  vendorId: '' as string | null,
  vendorName: '',
  vendorMobileNumber: '',
  vendorGstin: '',
  invoiceNumber: '',
  inwardDate: toInputDate(new Date()),
  supplierInvoiceDate: toInputDate(new Date()),
  dueDate: toInputDate(new Date()),
  frightAmount: 0,
  paidAmount: 0,
  paymentMode: 0,
  bankAccountId: ''
})

function emptyItemForm() {
  return {
    barcode: '', productName: '', hsnCode: '', quantity: 1, costPrice: 0, mrp: 0, discountAmount: 0,
    taxId: null as string | null, productCategoryId: null as string | null, productSubCategoryId: null as string | null,
    productUnit: null as number | null, productId: null as string | null
  }
}
const itemForm = reactive(emptyItemForm())
const cart = ref<CartItem[]>([])

const storeItems = computed(() => stores.value.map(item => ({ label: readText(item, ['name'], 'Store'), value: readText(item, ['id'], '') })))
const vendorItems = computed(() => [
  { label: '+ New vendor (type name below)', value: '' },
  ...vendors.value.map(item => ({ label: `${readText(item, ['name'])} (${readText(item, ['gSTIN', 'GSTIN'], 'No GSTIN')})`, value: readText(item, ['id'], '') }))
])
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountName', 'name'], 'Bank account'), value: readText(item, ['id'], '') })))
const categoryItems = computed(() => [{ label: 'Select category', value: null }, ...categories.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))])
const subCategoryItems = computed(() => [{ label: 'Select sub-category', value: null }, ...subCategories.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))])
const taxItems = computed(() => [{ label: 'Select tax', value: null }, ...taxes.value.map(item => ({ label: `${readText(item, ['name'])} (${readNumber(item, ['rate'])}%)`, value: readText(item, ['id'], '') }))])
const unitItems = computed(() => [{ label: 'Default', value: null }, ...units.value.map(item => ({ label: readText(item, ['label']), value: readNumber(item, ['value']) }))])

const cartTotal = computed(() => cart.value.reduce((sum, item) => sum + lineTotal(item), 0))

function money(value: number) { return formatIndianMoney(value) }
function lineTotal(item: CartItem) { return Math.max((item.costPrice - item.discountAmount) * item.quantity, 0) }
function requiresBank(mode: number) { return ![0].includes(mode) }

async function onStoreChanged() {
  const store = stores.value.find(item => readText(item, ['id'], '') === header.storeId)
  header.companyId = readText(store, ['companyId'], header.companyId)
  header.storeGroupId = readText(store, ['storeGroupId'], header.storeGroupId)
}

function onVendorSelected() {
  if (!header.vendorId) return
  const vendor = vendors.value.find(item => readText(item, ['id'], '') === header.vendorId)
  if (!vendor) return
  header.vendorName = readText(vendor, ['name'], '')
  header.vendorMobileNumber = readText(vendor, ['mobileNumber'], '')
  header.vendorGstin = readText(vendor, ['gSTIN', 'GSTIN'], '')
}

async function lookupBarcode() {
  matchedProductName.value = ''
  const code = itemForm.barcode.trim()
  if (!code) return
  try {
    const product = await get<ApiRecord>(`product-lookup/barcode/${encodeURIComponent(code)}`, header.storeId ? { storeId: header.storeId } : undefined)
    if (product) {
      itemForm.productId = readText(product, ['productId'], null as unknown as string) || null
      itemForm.productName = readText(product, ['name'], itemForm.productName)
      itemForm.hsnCode = readText(product, ['hsnCode'], itemForm.hsnCode)
      itemForm.taxId = readText(product, ['taxId'], '') || null
      itemForm.productCategoryId = readText(product, ['productCategoryId'], '') || null
      itemForm.productSubCategoryId = readText(product, ['productSubCategoryId'], '') || null
      itemForm.mrp = readNumber(product, ['mrp']) || itemForm.mrp
      matchedProductName.value = itemForm.productName
    }
  } catch {
    itemForm.productId = null
  }
}

function addItem() {
  error.value = ''
  if (!itemForm.barcode.trim() || !itemForm.productName.trim()) {
    error.value = 'Enter barcode and product name before adding an item.'
    return
  }
  if (!itemForm.quantity || itemForm.quantity <= 0) {
    error.value = 'Enter a valid quantity.'
    return
  }
  cart.value.push({
    productId: itemForm.productId,
    productName: itemForm.productName.trim(),
    barcode: itemForm.barcode.trim(),
    hsnCode: itemForm.hsnCode.trim(),
    quantity: itemForm.quantity,
    costPrice: itemForm.costPrice,
    mrp: itemForm.mrp,
    discountAmount: itemForm.discountAmount,
    taxId: itemForm.taxId,
    productCategoryId: itemForm.productCategoryId,
    productSubCategoryId: itemForm.productSubCategoryId,
    productUnit: itemForm.productUnit
  })
  Object.assign(itemForm, emptyItemForm())
  matchedProductName.value = ''
}

async function refresh() {
  error.value = ''
  try {
    const storedUser = getStoredUser(window.localStorage)
    const [storeData, bankData, lookupData] = await Promise.allSettled([
      get<unknown>('stores'),
      get<unknown>('bank-accounts'),
      get<ApiRecord>('purchase/lookup-options')
    ])
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (bankData.status === 'fulfilled') bankAccounts.value = toRows(bankData.value)
    if (lookupData.status === 'fulfilled') {
      const lookup = lookupData.value
      categories.value = readArrayLocal(lookup, 'categories')
      subCategories.value = readArrayLocal(lookup, 'subCategories')
      taxes.value = readArrayLocal(lookup, 'taxes')
      vendors.value = readArrayLocal(lookup, 'vendors')
      units.value = readArrayLocal(lookup, 'units')
    }

    header.storeId = storedUser?.storeId || readText(stores.value[0], ['id'], '')
    header.companyId = storedUser?.companyId || readText(stores.value[0], ['companyId'], '')
    header.storeGroupId = storedUser?.storeGroupId || readText(stores.value[0], ['storeGroupId'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase setup data.'
  }
}

function readArrayLocal(source: ApiRecord, key: string): ApiRecord[] {
  const value = (source as Record<string, unknown>)[key]
  return Array.isArray(value) ? value as ApiRecord[] : []
}

async function submitInward() {
  error.value = ''
  successResult.value = null
  if (!header.storeId || !header.companyId || !header.storeGroupId) {
    error.value = 'Select a store before saving.'
    return
  }
  if (!header.vendorName.trim()) {
    error.value = 'Enter vendor name.'
    return
  }
  if (!cart.value.length) {
    error.value = 'Add at least one item.'
    return
  }

  saving.value = true
  try {
    const response = await post<ApiRecord>('purchase/inward', {
      companyId: header.companyId,
      storeGroupId: header.storeGroupId,
      storeId: header.storeId,
      vendorId: header.vendorId || null,
      vendorName: header.vendorName.trim(),
      vendorMobileNumber: header.vendorMobileNumber.trim() || null,
      vendorGstin: header.vendorGstin.trim() || null,
      invoiceNumber: header.invoiceNumber.trim() || null,
      inwardDate: header.inwardDate || null,
      supplierInvoiceDate: header.supplierInvoiceDate || null,
      dueDate: header.dueDate || null,
      frightAmount: header.frightAmount || 0,
      paidAmount: header.paidAmount || 0,
      paymentMode: header.paymentMode,
      bankAccountId: header.bankAccountId || null,
      items: cart.value.map(item => ({
        productId: item.productId || null,
        productName: item.productName,
        barcode: item.barcode,
        quantity: item.quantity,
        costPrice: item.costPrice,
        mrp: item.mrp,
        discountAmount: item.discountAmount,
        taxId: item.taxId || null,
        productCategoryId: item.productCategoryId || null,
        productSubCategoryId: item.productSubCategoryId || null,
        hsnCode: item.hsnCode || null,
        productUnit: item.productUnit
      }))
    })
    successResult.value = {
      invoiceNumber: readText(response, ['invoiceNumber'], ''),
      inwardNumber: readText(response, ['inwardNumber'], ''),
      billAmount: readNumber(response, ['billAmount']),
      itemCount: readNumber(response, ['itemCount'])
    }
    cart.value = []
    header.invoiceNumber = ''
    header.paidAmount = 0
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save purchase inward.'
  } finally {
    saving.value = false
  }
}

function resetForNext() {
  successResult.value = null
}

onMounted(refresh)
</script>

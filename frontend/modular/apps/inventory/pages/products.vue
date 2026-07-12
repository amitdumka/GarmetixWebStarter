<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-boxes" class="size-4" /> Product master</p>
          <h2 class="garmetix-dashboard-title">Products</h2>
          <p class="garmetix-dashboard-subtitle">Manage your catalog, stock, and barcodes.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="openCreate">New Product</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="fetchProducts">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Total</p>
        <p class="garmetix-metric-value">{{ total }}</p>
        <p class="garmetix-metric-caption">Products matching filters</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">In Stock</p>
        <p class="garmetix-metric-value">{{ inStockCount }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Out Of Stock</p>
        <p class="garmetix-metric-value">{{ outOfStockCount }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Stock Value</p>
        <p class="garmetix-metric-value">{{ formatIndianMoney(totalMrpValue) }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Product Register</h3>
          <p class="garmetix-panel-subtitle">{{ products.length }} row(s) shown</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search by name or barcode..." class="sm:w-64" @keyup.enter="fetchProducts" />
          <USelect v-model="categoryFilter" :items="categorySelectItems" placeholder="All categories" class="sm:w-48" @update:model-value="fetchProducts" />
          <USelect v-model="brandFilter" :items="brandSelectItems" placeholder="All brands" class="sm:w-48" @update:model-value="fetchProducts" />
        </div>
      </div>

      <AdminMasterTable :columns="columns" :rows="tableRows" empty-text="No products found.">
        <template #actions="{ row }">
          <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="openEdit(findRowById(row.id))" />
        </template>
      </AdminMasterTable>

      <div v-if="total > 0" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <span>Showing {{ ((page - 1) * pageSize) + 1 }} to {{ Math.min(page * pageSize, total) }} of {{ total }} entries</span>
        <div class="flex gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="goToPage(page - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" trailing :disabled="page * pageSize >= total" @click="goToPage(page + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <UModal v-model:open="formOpen" :title="isEditing ? 'Edit Product' : 'New Product'" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-2xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveProduct">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Product name</span>
            <UInput v-model="form.name" placeholder="e.g. Cotton Shirt" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Barcode</span>
            <UInput v-model="form.barcode" placeholder="Auto-generated if empty" class="w-full" />
          </label>
          <label class="relative space-y-1 text-sm">
            <span class="text-muted">HSN code</span>
            <UInput
              v-model="form.hsnCode"
              placeholder="e.g. 6205"
              class="w-full"
              @input="onHsnInput"
              @focus="onHsnInput"
              @blur="hideHsnSuggestionsSoon"
            />
            <p v-if="!form.hsnCode" class="text-xs text-warning">Missing HSN - GST reporting for this product will be flagged.</p>
            <ul
              v-if="hsnSuggestions.length"
              class="absolute z-20 mt-1 w-full max-h-56 overflow-y-auto rounded-md border border-default bg-default shadow-lg"
            >
              <li
                v-for="suggestion in hsnSuggestions"
                :key="suggestion.hsnCode"
                class="cursor-pointer px-3 py-2 text-xs hover:bg-elevated"
                @mousedown.prevent="applyHsnSuggestion(suggestion)"
              >
                <span class="font-medium">{{ suggestion.hsnCode }}</span>
                <span class="text-muted"> - {{ suggestion.description || suggestion.commonTradeDescription || 'No description' }}</span>
                <span v-if="suggestion.defaultGstRate !== null" class="text-muted"> ({{ suggestion.defaultGstRate }}%)</span>
              </li>
            </ul>
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">MRP</span>
            <UInput v-model.number="form.mrp" type="number" min="0" step="0.01" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Cost price</span>
            <UInput v-model.number="form.costPrice" type="number" min="0" step="0.01" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Category</span>
            <USelect v-model="form.productCategoryId" :items="categoryFormItems" placeholder="Select category" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Brand</span>
            <UInput v-model="form.brand" placeholder="e.g. Raymond" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Tax</span>
            <USelect v-model="form.taxId" :items="taxSelectItems" placeholder="Select tax slab" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Opening quantity</span>
            <UInput v-model.number="form.openingQuantity" type="number" min="0" :disabled="isEditing" placeholder="0" class="w-full" />
          </label>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Product</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Products - Garmetix Inventory' })

function emptyForm() {
  return {
    id: '',
    name: '',
    barcode: '',
    hsnCode: '',
    mrp: 0,
    costPrice: 0,
    openingQuantity: 0,
    productCategoryId: '',
    brand: '',
    taxId: ''
  }
}

const { get, post, put } = useAdminApiClient()

interface HsnSuggestion {
  hsnCode: string
  description: string | null
  commonTradeDescription: string | null
  defaultGstRate: number | null
}

const hsnSuggestions = ref<HsnSuggestion[]>([])
let hsnSearchTimer: ReturnType<typeof setTimeout> | null = null

function onHsnInput() {
  if (hsnSearchTimer) clearTimeout(hsnSearchTimer)
  hsnSearchTimer = setTimeout(async () => {
    const query = form.hsnCode.trim()
    if (query.length < 1) {
      hsnSuggestions.value = []
      return
    }
    try {
      hsnSuggestions.value = await post<HsnSuggestion[]>('gst/hsn/search', { query, goodsOnly: true })
    } catch {
      hsnSuggestions.value = []
    }
  }, 250)
}

function hideHsnSuggestionsSoon() {
  setTimeout(() => { hsnSuggestions.value = [] }, 150)
}

function applyHsnSuggestion(suggestion: HsnSuggestion) {
  form.hsnCode = suggestion.hsnCode
  hsnSuggestions.value = []
}

const search = ref('')
const categoryFilter = ref('')
const brandFilter = ref('')
const page = ref(1)
const pageSize = ref(25)
const total = ref(0)
const inStockCount = ref(0)
const outOfStockCount = ref(0)
const totalMrpValue = ref(0)
const products = ref<ApiRecord[]>([])
const loading = ref(false)
const error = ref('')
const message = ref('')

const isEditing = ref(false)
const saving = ref(false)
const formOpen = ref(false)
const form = reactive(emptyForm())

const categories = ref<ApiRecord[]>([])
const taxes = ref<ApiRecord[]>([])
const brands = ref<string[]>([])
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])

const categorySelectItems = computed(() => [
  { label: 'All categories', value: '' },
  ...categories.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
])
const categoryFormItems = computed(() => categories.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })))
const brandSelectItems = computed(() => [
  { label: 'All brands', value: '' },
  ...brands.value.map(name => ({ label: name, value: name }))
])
const taxSelectItems = computed(() => taxes.value.map(item => ({ label: `${readText(item, ['name'])} (${readNumber(item, ['rate'])}%)`, value: readText(item, ['id'], '') })))

const tableRows = computed(() => products.value.map(item => ({
  id: readText(item, ['id'], ''),
  name: readText(item, ['name']),
  barcode: readText(item, ['barcode']),
  category: readText(item, ['categoryName']),
  brand: readText(item, ['brand']),
  mrp: formatIndianMoney(readNumber(item, ['mrp'])),
  stock: readNumber(item, ['currentStock'])
})))
const columns = [
  { key: 'name', label: 'Product Name' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'category', label: 'Category' },
  { key: 'brand', label: 'Brand' },
  { key: 'mrp', label: 'MRP' },
  { key: 'stock', label: 'Stock' }
]

function findRowById(id: unknown) {
  return products.value.find(item => readText(item, ['id'], '') === id) ?? null
}

async function fetchOptions() {
  try {
    const res = await get<ApiRecord>('inventory/product-master/options')
    categories.value = toRows(res.categories)
    taxes.value = toRows(res.taxes)
    brands.value = Array.isArray(res.brands) ? res.brands as string[] : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load product options.'
  }
}

async function fetchProducts() {
  loading.value = true
  error.value = ''
  try {
    const res = await get<ApiRecord>('inventory/product-master/paged', {
      page: page.value,
      pageSize: pageSize.value,
      q: search.value.trim() || undefined,
      categoryId: categoryFilter.value || undefined,
      brand: brandFilter.value || undefined
    })
    products.value = toRows(res.items)
    total.value = readNumber(res, ['total'])
    inStockCount.value = readNumber(res, ['inStockCount'])
    outOfStockCount.value = readNumber(res, ['outOfStockCount'])
    totalMrpValue.value = readNumber(res, ['totalMrpValue'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Failed to load products.'
  } finally {
    loading.value = false
  }
}

function goToPage(next: number) {
  page.value = Math.max(1, next)
  fetchProducts()
}

function resolveScope() {
  const companyId = readText(companies.value[0], ['id'], '')
  const store = stores.value[0]
  const storeId = readText(store, ['id'], '')
  const storeGroupId = readText(store, ['storeGroupId'], '')
  return { companyId, storeGroupId, storeId }
}

function openCreate() {
  Object.assign(form, emptyForm())
  isEditing.value = false
  message.value = ''
  error.value = ''
  formOpen.value = true
}

function openEdit(row: ApiRecord | null) {
  if (!row) return
  Object.assign(form, {
    id: readText(row, ['id'], ''),
    name: readText(row, ['name'], ''),
    barcode: readText(row, ['barcode'], ''),
    hsnCode: readText(row, ['hSNCode', 'hsnCode'], ''),
    mrp: readNumber(row, ['mrp']),
    costPrice: readNumber(row, ['costPrice']),
    openingQuantity: 0,
    productCategoryId: readText(row, ['productCategoryId'], ''),
    brand: readText(row, ['brand'], ''),
    taxId: readText(row, ['taxId'], '')
  })
  isEditing.value = true
  message.value = ''
  error.value = ''
  formOpen.value = true
}

async function saveProduct() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (!form.name.trim()) throw new Error('Enter product name.')
    if (!isEditing.value && !form.barcode.trim()) throw new Error('Enter a barcode.')

    const scope = resolveScope()
    const payload = {
      name: form.name.trim(),
      barcode: form.barcode.trim(),
      hsnCode: form.hsnCode.trim() || null,
      mrp: Number(form.mrp || 0),
      costPrice: Number(form.costPrice || 0),
      openingQuantity: Number(form.openingQuantity || 0),
      productCategoryId: form.productCategoryId || null,
      brand: form.brand.trim() || null,
      taxId: form.taxId || null,
      companyId: scope.companyId || null,
      storeGroupId: scope.storeGroupId || null,
      storeId: scope.storeId || null
    }

    if (isEditing.value && form.id) {
      await put<unknown>(`inventory/product-master/${form.id}`, payload)
      message.value = 'Product updated.'
    } else {
      await post<unknown>('inventory/product-master', payload)
      message.value = 'Product created.'
    }

    formOpen.value = false
    await fetchProducts()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Failed to save product.'
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await Promise.allSettled([
    fetchOptions(),
    fetchProducts(),
    get<unknown>('companies').then(value => { companies.value = toRows(value) }).catch(() => {}),
    get<unknown>('stores').then(value => { stores.value = toRows(value) }).catch(() => {})
  ])
})
</script>

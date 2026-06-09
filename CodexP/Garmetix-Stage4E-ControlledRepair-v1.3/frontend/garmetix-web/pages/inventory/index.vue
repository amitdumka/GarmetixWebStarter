<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const productOptions = ref<any>({
  categories: [],
  subCategories: [],
  taxes: [],
  vendors: [],
  units: [],
  taxTypes: [],
  productTypes: [],
  productGroups: [],
  stockTypes: []
})
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const editMode = ref<'create' | 'edit'>('create')
const pendingDelete = ref<any | null>(null)

const productForm = reactive<any>(emptyProduct())

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const categoryOptions = computed(() => {
  const selectedGroup = Number(productForm.productGroup)
  return (productOptions.value.categories || [])
    .filter((item: any) => item.isActive !== false)
    .filter((item: any) => item.productGroup === null || item.productGroup === undefined || Number(item.productGroup) === selectedGroup)
    .map((item: any) => ({ value: item.id, label: item.name }))
})

const allCategoryOptions = computed(() => (productOptions.value.categories || []).map((item: any) => ({ value: item.id, label: item.name })))

const subCategoryOptions = computed(() => {
  const categoryId = productForm.productCategoryId
  return (productOptions.value.subCategories || [])
    .filter((item: any) => !categoryId || !item.categoryId || item.categoryId === categoryId)
    .map((item: any) => ({ value: item.id, label: item.name }))
})

const taxOptions = computed(() => (productOptions.value.taxes || []).map((item: any) => ({
  value: item.id,
  label: `${item.name || 'GST'} - ${Number(item.rate || 0).toFixed(2)}%`
})))

const unitOptions = computed(() => enumItems(productOptions.value.units))
const taxTypeOptions = computed(() => enumItems(productOptions.value.taxTypes))
const productTypeOptions = computed(() => enumItems(productOptions.value.productTypes))
const productGroupOptions = computed(() => enumItems(productOptions.value.productGroups))
const stockTypeOptions = computed(() => enumItems(productOptions.value.stockTypes))
const vendorOptions = computed(() => [
  { value: '', label: 'No vendor selected' },
  ...(productOptions.value.vendors || []).map((item: any) => ({
    value: item.id,
    label: `${item.name || 'Vendor'}${item.gstin ? ` - ${item.gstin}` : ''}`
  }))
])

const stockSummary = computed(() => products.value.reduce((summary, product) => {
  summary.purchaseQty += Number(product.purchaseQty || 0)
  summary.soldQty += Number(product.soldQty || 0)
  summary.currentStock += Number(product.currentStock || 0)
  summary.stockValue += Number(product.mrp || 0) * Number(product.currentStock || 0)
  return summary
}, {
  purchaseQty: 0,
  soldQty: 0,
  currentStock: 0,
  stockValue: 0
}))

const metrics = computed(() => [
  {
    label: 'Products',
    value: products.value.length,
    meta: 'Product masters',
    icon: 'i-lucide-boxes',
    color: 'primary'
  },
  {
    label: 'Current Stock',
    value: stockSummary.value.currentStock,
    meta: 'Purchased minus sold',
    icon: 'i-lucide-warehouse',
    color: stockSummary.value.currentStock > 0 ? 'success' : 'warning'
  },
  {
    label: 'Sold Qty',
    value: stockSummary.value.soldQty,
    meta: 'Across scoped stores',
    icon: 'i-lucide-shopping-bag',
    color: 'neutral'
  },
  {
    label: 'Stock Value',
    value: money(stockSummary.value.stockValue),
    meta: 'MRP value',
    icon: 'i-lucide-indian-rupee',
    color: 'warning'
  }
])

const tableRows = computed(() => products.value.map((product) => ({
  id: product.id,
  name: product.name || 'Product',
  barcode: product.barcode || '-',
  hsnCode: product.hsnCode || '-',
  productType: labelFor(product.productType, productTypeOptions.value),
  productGroup: labelFor(product.productGroup, productGroupOptions.value),
  category: product.categoryName || labelFor(product.productCategoryId, allCategoryOptions.value) || '-',
  subCategory: product.subCategoryName || '-',
  mrp: money(Number(product.mrp || 0)),
  costPrice: money(Number(product.costPrice || 0)),
  purchased: Number(product.purchaseQty || 0),
  sold: Number(product.soldQty || 0),
  currentStock: Number(product.currentStock || 0),
  stockValue: money(Number(product.mrp || 0) * Number(product.currentStock || 0)),
  raw: product
})))

const columns: TableColumn<any>[] = [
  {
    accessorKey: 'name',
    header: 'Product',
    cell: ({ row }) => h('div', { class: 'min-w-48' }, [
      h('strong', row.original.name),
      h('p', { class: 'text-xs text-muted' }, `${row.original.productGroup} / ${row.original.productType}`)
    ])
  },
  { accessorKey: 'barcode', header: 'Barcode' },
  { accessorKey: 'hsnCode', header: 'HSN' },
  { accessorKey: 'category', header: 'Category' },
  { accessorKey: 'mrp', header: 'MRP' },
  { accessorKey: 'costPrice', header: 'Cost' },
  {
    accessorKey: 'currentStock',
    header: 'Stock',
    cell: ({ row }) => h(UBadge, {
      color: row.original.currentStock > 0 ? 'success' : 'warning',
      variant: 'subtle'
    }, () => String(row.original.currentStock))
  },
  { accessorKey: 'stockValue', header: 'Value' },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      }) : null
    ].filter(Boolean))
  }
]

function emptyProduct() {
  return {
    id: '',
    name: '',
    barcode: '',
    descriptions: '',
    hsnCode: '',
    mrp: 0,
    openingQuantity: 0,
    costPrice: 0,
    taxId: '',
    taxRate: 0,
    unit: 2,
    taxType: 0,
    productType: 3,
    productGroup: 0,
    stockType: 0,
    productCategoryId: '',
    productSubCategoryId: '',
    styleCode: '',
    baseColor: '',
    brand: '',
    vendorId: '',
    companyId: '',
    storeGroupId: '',
    storeId: ''
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, optionRows, productRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('inventory/product-master/options'),
      api.list<any>('inventory/product-master')
    ])

    companies.value = companyRows
    stores.value = storeRows
    productOptions.value = optionRows
    products.value = productRows
  } catch (error) {
    feedback.failed('Inventory refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(productForm, emptyProduct())
  const firstTax = productOptions.value.taxes?.[0]
  const firstCategory = categoryOptions.value[0]
  const firstSubCategory = subCategoryOptions.value[0]
  productForm.taxId = firstTax?.id || ''
  productForm.taxRate = Number(firstTax?.rate || 0)
  productForm.taxType = Number(firstTax?.taxType || 0)
  productForm.productCategoryId = firstCategory?.value || ''
  productForm.productSubCategoryId = firstSubCategory?.value || ''
  formOpen.value = true
}

function startEdit(product: any) {
  editMode.value = 'edit'
  Object.assign(productForm, {
    ...emptyProduct(),
    ...product,
    openingQuantity: Number(product.purchaseQty || 0),
    taxId: product.taxId || '',
    vendorId: product.vendorId || '',
    productCategoryId: product.productCategoryId || '',
    productSubCategoryId: product.productSubCategoryId || '',
    unit: Number(product.unit ?? 2),
    taxType: Number(product.taxType ?? 0),
    productType: Number(product.productType ?? 3),
    productGroup: Number(product.productGroup ?? 0),
    stockType: Number(product.stockType ?? 0)
  })
  formOpen.value = true
}

async function saveProduct() {
  saving.value = true
  try {
    if (editMode.value === 'create') {
      await api.create<any>('inventory/product-master', productPayload())
      feedback.saved('Product')
    } else {
      await api.update<any>('inventory/product-master', productForm.id, productPayload())
      feedback.updated('Product')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save product', error)
  } finally {
    saving.value = false
  }
}

function productPayload() {
  const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value) || stores.value[0]
  const selectedTax = (productOptions.value.taxes || []).find((tax: any) => tax.id === productForm.taxId)
  const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
  const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId
  const storeId = workspace.storeId.value || setupStatus.value?.storeId || selectedStore?.id

  return {
    name: String(productForm.name || '').trim(),
    barcode: String(productForm.barcode || '').trim(),
    descriptions: nullIfEmpty(productForm.descriptions),
    hsnCode: nullIfEmpty(productForm.hsnCode),
    mrp: Number(productForm.mrp || 0),
    openingQuantity: editMode.value === 'create' ? Number(productForm.openingQuantity || 0) : 0,
    costPrice: Number(productForm.costPrice || 0),
    taxId: productForm.taxId || null,
    taxRate: selectedTax ? Number(selectedTax.rate || 0) : Number(productForm.taxRate || 0),
    taxType: selectedTax ? Number(selectedTax.taxType || 0) : Number(productForm.taxType || 0),
    unit: Number(productForm.unit || 0),
    productType: Number(productForm.productType || 0),
    productGroup: Number(productForm.productGroup || 0),
    stockType: Number(productForm.stockType || 0),
    productCategoryId: productForm.productCategoryId || null,
    productSubCategoryId: productForm.productSubCategoryId || null,
    styleCode: nullIfEmpty(productForm.styleCode),
    baseColor: nullIfEmpty(productForm.baseColor),
    brand: nullIfEmpty(productForm.brand),
    vendorId: productForm.vendorId || null,
    companyId,
    storeGroupId,
    storeId
  }
}

function askDelete(product: any) {
  pendingDelete.value = product
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    await api.remove('products', pendingDelete.value.id)
    feedback.deleted('Product')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete product', error)
  } finally {
    deleting.value = false
  }
}

function enumItems(items: any[] = []) {
  return items.map((item) => ({ value: Number(item.value), label: item.label }))
}

function labelFor(value: unknown, items: any[] = []) {
  const match = items.find((item) => String(item.value) === String(value))
  return match?.label || String(value ?? '')
}

function nullIfEmpty(value: unknown) {
  const text = String(value || '').trim()
  return text ? text : null
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(value || 0)
}

watch(() => productForm.productGroup, () => {
  if (editMode.value === 'create' && !categoryOptions.value.some((item) => item.value === productForm.productCategoryId)) {
    productForm.productCategoryId = categoryOptions.value[0]?.value || ''
    productForm.productSubCategoryId = subCategoryOptions.value[0]?.value || ''
  }
})

watch(() => productForm.productCategoryId, () => {
  if (!subCategoryOptions.value.some((item) => item.value === productForm.productSubCategoryId)) {
    productForm.productSubCategoryId = subCategoryOptions.value[0]?.value || ''
  }
})

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Inventory"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Inventory"
        description="Stage 3A Product Master: product type/group, HSN, GST, category, vendor, brand, style, color, and stock defaults."
        icon="i-lucide-boxes"
        primary-label="New Product"
        primary-icon="i-lucide-package-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${products.length} products` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-package-plus" label="New Product" @click="startCreate" />
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
              <h2>Product Master</h2>
              <p>Maintain full garment product metadata before billing, purchase, and GST reporting.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search product, barcode, HSN, category, brand, style"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Product"
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
          title="No products found"
          description="Create the first product with HSN, product type/group, GST, and stock defaults."
          icon="i-lucide-package-search"
          action-label="New Product"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="editMode === 'create' ? 'New Product' : 'Edit Product'"
        description="Maintain product master, classification, GST, vendor, and stock default data."
        :submit-label="editMode === 'create' ? 'Save Product' : 'Update Product'"
        :loading="saving"
        @submit="saveProduct"
      >
        <div class="form-two-column">
          <UFormField label="Product name" required>
            <UInput v-model="productForm.name" required />
          </UFormField>
          <UFormField label="Barcode" required>
            <UInput v-model="productForm.barcode" required />
          </UFormField>
        </div>

        <UFormField label="Description">
          <UTextarea v-model="productForm.descriptions" :rows="2" />
        </UFormField>

        <div class="form-three-column">
          <UFormField label="Product group">
            <USelect v-model="productForm.productGroup" :items="productGroupOptions" />
          </UFormField>
          <UFormField label="Product type">
            <USelect v-model="productForm.productType" :items="productTypeOptions" />
          </UFormField>
          <UFormField label="Stock type">
            <USelect v-model="productForm.stockType" :items="stockTypeOptions" />
          </UFormField>
        </div>

        <div class="form-three-column">
          <UFormField label="Category">
            <USelect v-model="productForm.productCategoryId" :items="categoryOptions" placeholder="Auto General" />
          </UFormField>
          <UFormField label="Sub category">
            <USelect v-model="productForm.productSubCategoryId" :items="subCategoryOptions" placeholder="Auto General" />
          </UFormField>
          <UFormField label="Unit">
            <USelect v-model="productForm.unit" :items="unitOptions" />
          </UFormField>
        </div>

        <div class="form-three-column">
          <UFormField label="MRP">
            <UInput v-model="productForm.mrp" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Cost price">
            <UInput v-model="productForm.costPrice" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField v-if="editMode === 'create'" label="Opening quantity">
            <UInput v-model="productForm.openingQuantity" min="0" step="1" type="number" />
          </UFormField>
          <UFormField v-else label="Current stock">
            <UInput :model-value="productForm.currentStock" disabled />
          </UFormField>
        </div>

        <div class="form-three-column">
          <UFormField label="Tax">
            <USelect v-model="productForm.taxId" :items="taxOptions" placeholder="Auto GST 0" />
          </UFormField>
          <UFormField label="Tax type">
            <USelect v-model="productForm.taxType" :items="taxTypeOptions" />
          </UFormField>
          <UFormField label="HSN code">
            <UInput v-model="productForm.hsnCode" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Brand">
            <UInput v-model="productForm.brand" />
          </UFormField>
          <UFormField label="Vendor / supplier">
            <USelect v-model="productForm.vendorId" :items="vendorOptions" placeholder="Select vendor" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Style code">
            <UInput v-model="productForm.styleCode" />
          </UFormField>
          <UFormField label="Base color">
            <UInput v-model="productForm.baseColor" />
          </UFormField>
        </div>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Product"
        :description="`Delete ${pendingDelete?.name || 'this product'}? Current stock is ${pendingDelete?.currentStock || 0}.`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>

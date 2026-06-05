<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const stocks = ref<any[]>([])
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

const stockSummary = computed(() => {
  return products.value.reduce((summary, product) => {
    const stock = stockFor(product.id)
    summary.purchaseQty += stock.purchaseQty
    summary.soldQty += stock.soldQty
    summary.currentStock += stock.currentStock
    summary.stockValue += stock.mrpValue
    return summary
  }, {
    purchaseQty: 0,
    soldQty: 0,
    currentStock: 0,
    stockValue: 0
  })
})

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
    meta: 'Across all stores',
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

const tableRows = computed(() => products.value.map((product) => {
  const stock = stockFor(product.id)
  return {
    id: product.id,
    name: product.name || 'Product',
    barcode: product.barcode || '-',
    mrp: money(Number(product.mrp || 0)),
    purchased: stock.purchaseQty,
    sold: stock.soldQty,
    currentStock: stock.currentStock,
    stockValue: money(stock.mrpValue),
    raw: product
  }
}))

const columns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Product' },
  { accessorKey: 'barcode', header: 'Barcode' },
  { accessorKey: 'mrp', header: 'MRP' },
  { accessorKey: 'purchased', header: 'Purchased' },
  { accessorKey: 'sold', header: 'Sold' },
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
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }),
      h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      })
    ])
  }
]

function emptyProduct() {
  return {
    id: '',
    name: '',
    productName: '',
    barcode: '',
    mrp: 0,
    openingQuantity: 0,
    taxRate: 0,
    unit: 2,
    taxType: 0,
    productType: 0,
    productCategoryId: '',
    productSubCategoryId: '',
    companyId: '',
    storeGroupId: ''
  }
}

function stockFor(productId: string) {
  const rows = stocks.value.filter((stock) => stock.productId === productId)
  const purchaseQty = rows.reduce((sum, stock) => sum + Number(stock.purchaseQty || 0), 0)
  const soldQty = rows.reduce((sum, stock) => sum + Number(stock.soldQty || 0), 0)
  const currentStock = purchaseQty - soldQty
  const mrpValue = rows.reduce((sum, stock) => sum + (Number(stock.mrp || 0) * (Number(stock.purchaseQty || 0) - Number(stock.soldQty || 0))), 0)

  return { purchaseQty, soldQty, currentStock, mrpValue }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, stockRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.list<any>('stocks')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    stocks.value = stockRows
  } catch (error) {
    feedback.failed('Inventory refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(productForm, emptyProduct())
  formOpen.value = true
}

function startEdit(product: any) {
  editMode.value = 'edit'
  Object.assign(productForm, {
    ...emptyProduct(),
    ...product,
    productName: product.name || '',
    productCategory: null,
    productSubCategory: null,
    stocks: null
  })
  formOpen.value = true
}

async function saveProduct() {
  saving.value = true
  try {
    if (editMode.value === 'create') {
      await createProduct()
      feedback.saved('Product')
    } else {
      await updateProduct()
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

async function createProduct() {
  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    throw new Error('Run quick setup before adding products.')
  }

  await api.create<any>('setup/quick-product', {
    name: String(productForm.productName || productForm.name || '').trim(),
    barcode: String(productForm.barcode || '').trim(),
    mrp: Number(productForm.mrp || 0),
    openingQuantity: Number(productForm.openingQuantity || 0),
    companyId,
    storeGroupId,
    storeId
  })
}

async function updateProduct() {
  await api.update<any>('products', productForm.id, {
    ...productForm,
    name: String(productForm.productName || productForm.name || '').trim(),
    barcode: String(productForm.barcode || '').trim(),
    mrp: Number(productForm.mrp || 0),
    taxRate: Number(productForm.taxRate || 0),
    productCategory: null,
    productSubCategory: null,
    stocks: null
  })
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
        description="Track product masters, stock quantity, sold quantity, and MRP stock value."
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
              <h2>Products</h2>
              <p>Search, edit, and maintain product stock visibility.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search product or barcode"
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
          description="Create the first product to start billing, purchase, and inventory workflows."
          icon="i-lucide-package-search"
          action-label="New Product"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="editMode === 'create' ? 'New Product' : 'Edit Product'"
        description="Maintain product master data and opening stock."
        :submit-label="editMode === 'create' ? 'Save Product' : 'Update Product'"
        :loading="saving"
        @submit="saveProduct"
      >
        <UFormField label="Product name" required>
          <UInput v-model="productForm.productName" required />
        </UFormField>
        <UFormField label="Barcode" required>
          <UInput v-model="productForm.barcode" required />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="MRP">
            <UInput v-model="productForm.mrp" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField v-if="editMode === 'create'" label="Opening quantity">
            <UInput v-model="productForm.openingQuantity" min="0" step="1" type="number" />
          </UFormField>
          <UFormField v-else label="Tax rate">
            <UInput v-model="productForm.taxRate" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Product"
        :description="`Delete ${pendingDelete?.name || 'this product'}? Current stock is ${pendingDelete ? stockFor(pendingDelete.id).currentStock : 0}.`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>

<script setup lang="ts">
import { Boxes, PackagePlus, Pencil, Trash2 } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const stocks = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const viewMode = ref<'list' | 'create' | 'edit'>('list')
const inventoryMessage = ref('')
const searchText = ref('')

const createForm = reactive({
  productName: '',
  barcode: '',
  mrp: 0,
  openingQuantity: 0
})

const editForm = reactive<any>({
  id: '',
  name: '',
  barcode: '',
  mrp: 0,
  taxRate: 0,
  unit: 2,
  taxType: 0,
  productType: 0,
  productCategoryId: '',
  productSubCategoryId: '',
  companyId: '',
  storeGroupId: ''
})

const filteredProducts = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) {
    return products.value
  }

  return products.value.filter((product) => {
    return String(product.name || '').toLowerCase().includes(query) ||
      String(product.barcode || '').toLowerCase().includes(query)
  })
})

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
  } finally {
    loading.value = false
  }
}

async function createProduct() {
  inventoryMessage.value = ''

  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    inventoryMessage.value = 'Run quick setup before adding products.'
    return
  }

  await api.create<any>('setup/quick-product', {
    name: createForm.productName,
    barcode: createForm.barcode,
    mrp: Number(createForm.mrp),
    openingQuantity: Number(createForm.openingQuantity),
    companyId,
    storeGroupId,
    storeId
  })

  createForm.productName = ''
  createForm.barcode = ''
  createForm.mrp = 0
  createForm.openingQuantity = 0
  inventoryMessage.value = 'Product saved.'
  viewMode.value = 'list'
  await refresh()
}

function startEdit(product: any) {
  Object.assign(editForm, {
    ...product,
    productCategory: null,
    productSubCategory: null,
    stocks: null
  })
  viewMode.value = 'edit'
  inventoryMessage.value = ''
}

async function saveEdit() {
  inventoryMessage.value = ''

  await api.update<any>('products', editForm.id, {
    ...editForm,
    name: String(editForm.name || '').trim(),
    barcode: String(editForm.barcode || '').trim(),
    mrp: Number(editForm.mrp || 0),
    taxRate: Number(editForm.taxRate || 0)
  })

  inventoryMessage.value = 'Product updated.'
  viewMode.value = 'list'
  await refresh()
}

async function deleteProduct(product: any) {
  const stock = stockFor(product.id)
  const confirmed = window.confirm(`Delete product ${product.name}? Current stock is ${stock.currentStock}.`)
  if (!confirmed) {
    return
  }

  await api.remove('products', product.id)
  inventoryMessage.value = 'Product deleted.'
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
    title="Inventory"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Products</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${products.length} products` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <Boxes :size="16" />
              List
            </button>
            <button class="button" type="button" @click="viewMode = 'create'">
              <PackagePlus :size="16" />
              New Product
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search products" placeholder="Search product or barcode" />
            <p v-if="inventoryMessage" class="inline-message">{{ inventoryMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Product</th>
                <th>Barcode</th>
                <th>MRP</th>
                <th>Purchased</th>
                <th>Sold</th>
                <th>Stock</th>
                <th>Value</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="product in filteredProducts" :key="product.id">
                <td>{{ product.name }}</td>
                <td>{{ product.barcode }}</td>
                <td>{{ Number(product.mrp).toFixed(2) }}</td>
                <td>{{ stockFor(product.id).purchaseQty }}</td>
                <td>{{ stockFor(product.id).soldQty }}</td>
                <td>
                  <span class="status" :class="stockFor(product.id).currentStock > 0 ? 'ok' : 'warn'">
                    {{ stockFor(product.id).currentStock }}
                  </span>
                </td>
                <td>{{ stockFor(product.id).mrpValue.toFixed(2) }}</td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(product)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteProduct(product)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredProducts.length === 0">
                <td colspan="8">No products</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else-if="viewMode === 'create'" class="form-grid wide-form" @submit.prevent="createProduct">
          <div class="field">
            <label for="productName">Product name</label>
            <input id="productName" v-model="createForm.productName" required />
          </div>
          <div class="field">
            <label for="barcode">Barcode</label>
            <input id="barcode" v-model="createForm.barcode" required />
          </div>
          <div class="field">
            <label for="mrp">MRP</label>
            <input id="mrp" v-model="createForm.mrp" min="0" type="number" />
          </div>
          <div class="field">
            <label for="openingQuantity">Opening quantity</label>
            <input id="openingQuantity" v-model="createForm.openingQuantity" min="0" type="number" />
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <PackagePlus :size="16" />
              Save Product
            </button>
          </div>
          <p v-if="inventoryMessage" class="setup-message">{{ inventoryMessage }}</p>
        </form>

        <form v-else class="form-grid wide-form" @submit.prevent="saveEdit">
          <div class="field">
            <label for="editName">Product name</label>
            <input id="editName" v-model="editForm.name" required />
          </div>
          <div class="field">
            <label for="editBarcode">Barcode</label>
            <input id="editBarcode" v-model="editForm.barcode" required />
          </div>
          <div class="field">
            <label for="editMrp">MRP</label>
            <input id="editMrp" v-model="editForm.mrp" min="0" type="number" />
          </div>
          <div class="field">
            <label for="editTaxRate">Tax rate</label>
            <input id="editTaxRate" v-model="editForm.taxRate" min="0" type="number" />
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <Pencil :size="16" />
              Save Changes
            </button>
          </div>
          <p v-if="inventoryMessage" class="setup-message">{{ inventoryMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>

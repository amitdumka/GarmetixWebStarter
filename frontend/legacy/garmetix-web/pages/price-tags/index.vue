<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const route = useRoute()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])

const search = ref('')
const purchaseSearch = ref('')
const selectedStoreId = ref<string | null>(null)
const inStockOnly = ref(true)
const loading = ref(false)
const preparing = ref(false)
const stockRows = ref<any[]>([])
const purchaseRows = ref<any[]>([])
const selectedRows = ref<any[]>([])
const labelSize = ref<'50x30' | '50x25'>('50x30')
const printMode = ref<'browser' | 'tspl'>('browser')
const storeName = ref('')
const tsplCommands = ref('')
const warnings = ref<string[]>([])
const showPreview = ref(true)
const sourceNotice = ref('')
const autoLoadingSource = ref(false)

const labelSizeOptions = [
  { value: '50x30', label: '50 × 30 mm' },
  { value: '50x25', label: '50 × 25 mm' }
]

const printModeOptions = [
  { value: 'browser', label: 'Browser / driver print' },
  { value: 'tspl', label: 'Download TSPL commands' }
]

const storeOptions = computed(() => [
  { value: '__all__', label: 'All stores' },
  ...stores.value.map((store: any) => ({ value: store.id, label: store.name || 'Store' }))
])

const labelCount = computed(() => selectedRows.value.reduce((sum, row) => sum + normalizedCopies(row), 0))
const totalMrp = computed(() => selectedRows.value.reduce((sum, row) => sum + (Number(row.mrp || 0) * normalizedCopies(row)), 0))
const printableLabels = computed(() => selectedRows.value.flatMap(row => Array.from({ length: normalizedCopies(row) }, (_, index) => ({ ...row, copyNumber: index + 1 }))))
const pageClass = computed(() => labelSize.value === '50x25' ? 'tag-page size-50x25' : 'tag-page size-50x30')
const tagClass = computed(() => labelSize.value === '50x25' ? 'price-tag tag-50x25' : 'price-tag tag-50x30')

onMounted(async () => {
  auth.restore()
  if (isAuthenticated.value) {
    await refresh()
    await autoLoadFromRoute()
  }
})

watch(selectedStoreId, value => {
  storeName.value = stores.value.find((store: any) => store.id === value)?.name || ''
})


async function refresh() {
  loading.value = true
  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
    selectedStoreId.value = workspace.storeId.value || stores.value[0]?.id || '__all__'
    storeName.value = stores.value.find((store: any) => store.id === selectedStoreId.value)?.name || ''
    await loadStock()
  } catch (error) {
    feedback.failed('Price tag page could not be loaded', error)
  } finally {
    loading.value = false
  }
}

async function loadStock() {
  loading.value = true
  try {
    const query = new URLSearchParams()
    if (selectedStoreId.value && selectedStoreId.value !== '__all__') query.set('storeId', selectedStoreId.value)
    if (search.value.trim()) query.set('query', search.value.trim())
    query.set('inStockOnly', String(inStockOnly.value))
    query.set('take', '80')
    stockRows.value = await api.list<any>(`price-tags/search?${query.toString()}`)
  } catch (error) {
    feedback.failed('Price tag product search failed', error)
  } finally {
    loading.value = false
  }
}

async function searchPurchases() {
  loading.value = true
  try {
    const query = new URLSearchParams()
    if (selectedStoreId.value && selectedStoreId.value !== '__all__') query.set('storeId', selectedStoreId.value)
    if (purchaseSearch.value.trim()) query.set('query', purchaseSearch.value.trim())
    query.set('take', '20')
    purchaseRows.value = await api.list<any>(`price-tags/purchase-inwards?${query.toString()}`)
  } catch (error) {
    feedback.failed('Purchase inward search failed', error)
  } finally {
    loading.value = false
  }
}

async function loadPurchaseItems(row: any) {
  await loadPurchaseItemsById(row.id, row.inwardNumber || row.invoiceNumber)
}

async function loadPurchaseItemsById(id: string, label = 'selected inward') {
  loading.value = true
  try {
    const rows = await api.list<any>(`price-tags/purchase-inwards/${id}/items`)
    const mapped = rows.map((item: any) => ({ ...item, copies: Math.max(1, Math.ceil(Number(item.currentStock || item.quantity || 1))) }))
    mergeRows(mapped)
    sourceNotice.value = `${mapped.length} item(s) loaded from purchase inward ${label}.`
    feedback.success(sourceNotice.value)
  } catch (error) {
    feedback.failed('Purchase inward tag items could not be loaded', error)
  } finally {
    loading.value = false
  }
}

async function loadImportBatchItemsById(id: string) {
  loading.value = true
  try {
    const rows = await api.list<any>(`price-tags/import-batches/${id}/items`)
    const mapped = rows.map((item: any) => ({ ...item, copies: Math.max(1, Math.ceil(Number(item.currentStock || item.quantity || 1))) }))
    mergeRows(mapped)
    sourceNotice.value = `${mapped.length} item(s) loaded from posted supplier invoice import.`
    feedback.success(sourceNotice.value)
  } catch (error) {
    feedback.failed('Posted import tag items could not be loaded', error)
  } finally {
    loading.value = false
  }
}

async function autoLoadFromRoute() {
  const importBatchId = Array.isArray(route.query.importBatchId) ? route.query.importBatchId[0] : route.query.importBatchId
  const purchaseInwardId = Array.isArray(route.query.purchaseInwardId) ? route.query.purchaseInwardId[0] : route.query.purchaseInwardId
  if (!importBatchId && !purchaseInwardId) return
  autoLoadingSource.value = true
  try {
    if (importBatchId) {
      await loadImportBatchItemsById(String(importBatchId))
    } else if (purchaseInwardId) {
      await loadPurchaseItemsById(String(purchaseInwardId), 'posted from import')
    }
    if (route.query.size === '50x25' || route.query.size === '50x30') labelSize.value = route.query.size as '50x25' | '50x30'
  } finally {
    autoLoadingSource.value = false
  }
}

function addRow(row: any) {
  const exists = selectedRows.value.find(item => String(item.barcode).toLowerCase() === String(row.barcode).toLowerCase())
  if (exists) {
    exists.copies = normalizedCopies(exists) + 1
    return
  }

  selectedRows.value.push({
    ...row,
    copies: Math.max(1, Math.ceil(Number(row.currentStock || 1)))
  })
}

function mergeRows(rows: any[]) {
  for (const row of rows) addRow(row)
}

function removeRow(index: number) {
  selectedRows.value.splice(index, 1)
}

function clearRows() {
  selectedRows.value = []
  tsplCommands.value = ''
  warnings.value = []
}

function normalizedCopies(row: any) {
  return Math.max(1, Math.min(500, Math.ceil(Number(row.copies || 1))))
}

async function prepareTags() {
  preparing.value = true
  try {
    const result = await api.create<any>('price-tags/prepare', {
      labelSize: labelSize.value,
      printerLanguage: printMode.value,
      storeName: storeName.value,
      rows: selectedRows.value.map(row => ({
        stockId: row.stockId && row.stockId !== '00000000-0000-0000-0000-000000000000' ? row.stockId : null,
        productId: row.productId || null,
        productName: row.productName || row.name || row.barcode,
        barcode: row.barcode,
        hsnCode: row.hsnCode,
        brand: row.brand,
        mrp: Number(row.mrp || 0),
        copies: normalizedCopies(row)
      }))
    })
    warnings.value = result.warnings || []
    tsplCommands.value = result.thermalCommands || ''
    feedback.success(`${result.labelCount || labelCount.value} label(s) prepared.`)
  } catch (error) {
    feedback.failed('Price tags could not be prepared', error)
  } finally {
    preparing.value = false
  }
}

async function printBrowser() {
  await prepareTags()
  await nextTick()
  window.print()
}

async function downloadTspl() {
  printMode.value = 'tspl'
  await prepareTags()
  const blob = new Blob([tsplCommands.value], { type: 'text/plain;charset=utf-8' })
  const link = document.createElement('a')
  link.href = URL.createObjectURL(blob)
  link.download = `garmetix-price-tags-${labelSize.value}-${new Date().toISOString().slice(0, 10)}.tspl.txt`
  link.click()
  URL.revokeObjectURL(link.href)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
}

function shortText(value: string, max = 28) {
  const text = String(value || '').trim()
  return text.length <= max ? text : `${text.slice(0, max - 1)}…`
}

const code128Patterns = [
  '11011001100','11001101100','11001100110','10010011000','10010001100','10001001100','10011001000','10011000100','10001100100','11001001000',
  '11001000100','11000100100','10110011100','10011011100','10011001110','10111001100','10011101100','10011100110','11001110010','11001011100',
  '11001001110','11011100100','11001110100','11101101110','11101001100','11100101100','11100100110','11101100100','11100110100','11100110010',
  '11011011000','11011000110','11000110110','10100011000','10001011000','10001000110','10110001000','10001101000','10001100010','11010001000',
  '11000101000','11000100010','10110111000','10110001110','10001101110','10111011000','10111000110','10001110110','11101110110','11010001110',
  '11000101110','11011101000','11011100010','11011101110','11101011000','11101000110','11100010110','11101101000','11101100010','11100011010',
  '11101111010','11001000010','11110001010','10100110000','10100001100','10010110000','10010000110','10000101100','10000100110','10110010000',
  '10110000100','10011010000','10011000010','10000110100','10000110010','11000010010','11001010000','11110111010','11000010100','10001111010',
  '10100111100','10010111100','10010011110','10111100100','10011110100','10011110010','11110100100','11110010100','11110010010','11011011110',
  '11011110110','11110110110','10101111000','10100011110','10001011110','10111101000','10111100010','11110101000','11110100010','10111011110',
  '10111101110','11101011110','11110101110','11010000100','11010010000','11010011100','1100011101011'
]

function code128Svg(value: string) {
  const text = String(value || '').trim() || '0'
  const codes = [104]
  for (const char of text) {
    const code = char.charCodeAt(0)
    codes.push(code >= 32 && code <= 126 ? code - 32 : 0)
  }
  let checksum = codes[0]
  for (let i = 1; i < codes.length; i++) checksum += codes[i] * i
  codes.push(checksum % 103, 106)
  const pattern = codes.map(code => code128Patterns[code] || '').join('')
  const width = pattern.length
  const height = 48
  let x = 0
  let rects = ''
  for (const bit of pattern) {
    if (bit === '1') rects += `<rect x="${x}" y="0" width="1" height="${height}" />`
    x++
  }
  return `<svg class="barcode-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none" aria-label="${escapeHtml(text)}">${rects}</svg>`
}

function escapeHtml(value: string) {
  return value.replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '\'': '&#39;', '"': '&quot;' }[char] || char))
}
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Price Tag Printing" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
  <div class="space-y-6 print:m-0">
    <div class="screen-only flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <div>
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-white">Price Tag Printing</h1>
        <p class="text-sm text-slate-500 dark:text-slate-400">Generate 50×30 mm and 50×25 mm thermal price tags with barcode, MRP and product name.</p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton color="neutral" variant="outline" icon="i-lucide-eye" :label="showPreview ? 'Hide preview' : 'Show preview'" @click="showPreview = !showPreview" />
        <UButton color="primary" icon="i-lucide-printer" :loading="preparing" :disabled="!selectedRows.length" label="Print tags" @click="printBrowser" />
        <UButton color="neutral" variant="outline" icon="i-lucide-download" :loading="preparing" :disabled="!selectedRows.length" label="Download TSPL" @click="downloadTspl" />
      </div>
    </div>

    <UAlert class="screen-only" color="info" variant="subtle" icon="i-lucide-printer-check" title="Printer support" description="For the USB desktop thermal printer, install it as a normal system printer and use Browser / driver print. For mobile Bluetooth printers, download TSPL and send it from the printer vendor app or a local bridge; direct Bluetooth printing from a web page is not reliable on all devices." />

    <UAlert v-if="sourceNotice" class="screen-only" color="success" variant="subtle" icon="i-lucide-tags" title="Tags loaded from purchase" :description="sourceNotice" />

    <div class="screen-only grid gap-4 xl:grid-cols-[360px_1fr]">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <div>
              <h2 class="font-semibold">Search stock</h2>
              <p class="text-xs text-slate-500">Add existing stock rows to print tags.</p>
            </div>
            <UBadge color="primary" variant="subtle">{{ stockRows.length }}</UBadge>
          </div>
        </template>
        <div class="space-y-3">
          <USelect v-model="selectedStoreId" :items="storeOptions" placeholder="Store" />
          <UInput v-model="search" placeholder="Search product, barcode, HSN" icon="i-lucide-search" @keydown.enter="loadStock" />
          <UCheckbox v-model="inStockOnly" label="In-stock only" />
          <UButton block color="neutral" variant="outline" :loading="loading" label="Search products" @click="loadStock" />
        </div>
        <div class="mt-4 max-h-80 space-y-2 overflow-auto pr-1">
          <button v-for="row in stockRows" :key="row.stockId || row.barcode" class="stock-row" type="button" @click="addRow(row)">
            <span class="font-medium">{{ row.productName }}</span>
            <span class="text-xs text-slate-500">{{ row.barcode }} · {{ money(row.mrp) }} · Stock {{ row.currentStock }}</span>
          </button>
        </div>
      </UCard>

      <div class="space-y-4">
        <UCard>
          <template #header>
            <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 class="font-semibold">Purchase inward to tags</h2>
                <p class="text-xs text-slate-500">Search inward number, supplier invoice number, vendor or GSTIN.</p>
              </div>
              <div class="flex gap-2 md:w-96">
                <UInput v-model="purchaseSearch" class="flex-1" placeholder="e.g. 496 or inward number" @keydown.enter="searchPurchases" />
                <UButton color="neutral" variant="outline" :loading="loading" label="Find" @click="searchPurchases" />
              </div>
            </div>
          </template>
          <div class="grid gap-2 md:grid-cols-2 xl:grid-cols-3">
            <button v-for="row in purchaseRows" :key="row.id" type="button" class="purchase-row" @click="loadPurchaseItems(row)">
              <span class="font-semibold">{{ row.inwardNumber || row.invoiceNumber }}</span>
              <span>{{ row.vendorName }}</span>
              <span class="text-xs text-slate-500">{{ row.supplierInvoiceDate || row.inwardDate }} · {{ money(row.billAmount) }}</span>
            </button>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 class="font-semibold">Selected tags</h2>
                <p class="text-xs text-slate-500">{{ selectedRows.length }} item(s), {{ labelCount }} label(s), MRP total {{ money(totalMrp) }}</p>
              </div>
              <div class="flex flex-wrap gap-2">
                <USelect v-model="labelSize" :items="labelSizeOptions" class="w-36" />
                <USelect v-model="printMode" :items="printModeOptions" class="w-56" />
                <UButton color="neutral" variant="ghost" icon="i-lucide-trash-2" label="Clear" :disabled="!selectedRows.length" @click="clearRows" />
              </div>
            </div>
          </template>

          <div v-if="warnings.length" class="mb-3 space-y-2">
            <UAlert v-for="warning in warnings" :key="warning" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :title="warning" />
          </div>

          <div class="overflow-x-auto">
            <table class="tag-table">
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Barcode</th>
                  <th>MRP</th>
                  <th>Copies</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, index) in selectedRows" :key="`${row.barcode}-${index}`">
                  <td><UInput v-model="row.productName" /></td>
                  <td><UInput v-model="row.barcode" /></td>
                  <td><UInput v-model.number="row.mrp" type="number" min="0" /></td>
                  <td><UInput v-model.number="row.copies" type="number" min="1" max="500" /></td>
                  <td><UButton color="error" variant="ghost" icon="i-lucide-x" @click="removeRow(index)" /></td>
                </tr>
                <tr v-if="!selectedRows.length">
                  <td colspan="5" class="py-8 text-center text-sm text-slate-500">Search stock or load a purchase inward to prepare price tags.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>
      </div>
    </div>

    <div v-if="showPreview" :class="pageClass">
      <div v-for="(label, index) in printableLabels" :key="`${label.barcode}-${index}`" :class="tagClass">
        <div class="tag-product">{{ shortText(label.productName, labelSize === '50x25' ? 24 : 28) }}</div>
        <div class="tag-meta">
          <span>{{ shortText(label.brand || label.categoryName || storeName, 16) }}</span>
          <strong>{{ money(Number(label.mrp || 0)) }}</strong>
        </div>
        <div class="tag-barcode" v-html="code128Svg(label.barcode)" />
        <div class="tag-code">{{ label.barcode }}</div>
      </div>
    </div>
  </div>
  </AppShell>
</template>

<style scoped>
.screen-only { }
.stock-row,
.purchase-row {
  display: flex;
  width: 100%;
  flex-direction: column;
  gap: 0.15rem;
  border: 1px solid rgb(226 232 240);
  border-radius: 0.75rem;
  padding: 0.7rem;
  text-align: left;
  background: white;
}
.dark .stock-row,
.dark .purchase-row { border-color: rgb(51 65 85); background: rgb(15 23 42); }
.stock-row:hover,
.purchase-row:hover { border-color: rgb(59 130 246); }
.tag-table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
.tag-table th,
.tag-table td { border-bottom: 1px solid rgb(226 232 240); padding: 0.5rem; text-align: left; }
.dark .tag-table th,
.dark .tag-table td { border-bottom-color: rgb(51 65 85); }
.tag-page {
  display: flex;
  flex-wrap: wrap;
  gap: 2mm;
  align-items: flex-start;
  padding: 4mm;
  background: rgb(248 250 252);
}
.price-tag {
  box-sizing: border-box;
  width: 50mm;
  border: 0.2mm dashed rgb(148 163 184);
  background: white;
  color: black;
  font-family: Arial, Helvetica, sans-serif;
  overflow: hidden;
  padding: 2mm 2.5mm 1.5mm;
  break-inside: avoid;
}
.tag-50x30 { height: 30mm; }
.tag-50x25 { height: 25mm; padding-top: 1.5mm; }
.tag-product {
  font-size: 9pt;
  font-weight: 700;
  line-height: 1.05;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.tag-meta {
  display: flex;
  justify-content: space-between;
  gap: 1mm;
  margin-top: 0.5mm;
  font-size: 7pt;
}
.tag-meta strong { font-size: 10pt; }
.tag-barcode { height: 9mm; margin-top: 0.5mm; }
.tag-50x25 .tag-barcode { height: 7.5mm; }
:deep(.barcode-svg) { display: block; width: 100%; height: 100%; fill: #000; }
.tag-code { margin-top: 0.2mm; text-align: center; font-size: 7pt; letter-spacing: 0.4pt; }
@media print {
  :global(body) { margin: 0 !important; background: white !important; }
  :global(body *) { visibility: hidden !important; }
  .screen-only { display: none !important; }
  .tag-page,
  .tag-page * { visibility: visible !important; }
  .tag-page {
    display: block;
    position: absolute;
    left: 0;
    top: 0;
    padding: 0;
    background: white;
  }
  .price-tag {
    float: left;
    margin: 0;
    border: 0;
    page-break-inside: avoid;
    break-inside: avoid;
  }
}
@page { margin: 0; size: auto; }
@page label-50x30 { size: 50mm 30mm; margin: 0; }
@page label-50x25 { size: 50mm 25mm; margin: 0; }
</style>

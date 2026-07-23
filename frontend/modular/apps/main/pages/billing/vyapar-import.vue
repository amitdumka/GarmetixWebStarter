<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-spreadsheet" class="size-4" /> Sale</p>
          <h2 class="garmetix-dashboard-title">Vyapar Sale Import</h2>
          <p class="garmetix-dashboard-subtitle">Upload a Vyapar sale-report Excel export, review barcode matches, map payment sources, then confirm import as real sale invoices.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <NuxtLink to="/billing/vyapar-imported"><UButton icon="i-lucide-list" color="neutral" variant="soft">Imported Invoices</UButton></NuxtLink>
          <NuxtLink to="/billing/vyapar-import-batches"><UButton icon="i-lucide-layers" color="neutral" variant="soft">Import Batches</UButton></NuxtLink>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" :close-button="{ icon: 'i-lucide-x' }" @close="error = ''" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" :close-button="{ icon: 'i-lucide-x' }" @close="message = ''" />

    <!-- Step 1: Upload -->
    <section class="garmetix-section-card space-y-3">
      <h3 class="garmetix-panel-title">Step 1 - Upload Vyapar Sale Report</h3>
      <div class="grid gap-3 sm:grid-cols-3">
        <UFormField label="Store">
          <USelect v-model="storeId" :items="storeItems" placeholder="Select store" class="w-full" />
        </UFormField>
        <UFormField label="Vyapar export file (.xlsx / .csv)" class="sm:col-span-2">
          <input
            type="file"
            accept=".xlsx,.csv"
            class="block w-full rounded-md border border-default bg-default p-2 text-sm file:mr-2 file:rounded file:border-0 file:bg-primary file:px-2 file:py-1 file:text-white"
            @change="onFileChange"
          >
        </UFormField>
      </div>
      <UButton icon="i-lucide-upload" color="primary" :loading="previewing" :disabled="!storeId || !uploadFile" @click="loadPreview">Preview Data</UButton>
    </section>

    <template v-if="preview">
      <!-- Step 2: Review -->
      <section class="garmetix-section-card space-y-4">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <h3 class="garmetix-panel-title">Step 2 - Review Barcode Matches</h3>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-download" :disabled="!missingProducts.length" @click="exportMissingBarcodeCsv">Export Missing Barcode CSV</UButton>
        </div>

        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Invoices</p><p class="text-lg font-semibold">{{ readNumber(preview, ['invoiceCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Lines</p><p class="text-lg font-semibold">{{ readNumber(preview, ['lineCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Matched</p><p class="text-lg font-semibold text-success">{{ readNumber(preview, ['matchedLineCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Missing</p><p class="text-lg font-semibold text-error">{{ readNumber(preview, ['missingLineCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Total</p><p class="text-lg font-semibold">{{ money(readNumber(preview, ['invoiceAmountTotal'])) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Fully Matched Invoices</p><p class="text-lg font-semibold">{{ readNumber(preview, ['fullyMatchedInvoiceCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Auto-Hidden (Already Imported)</p><p class="text-lg font-semibold">{{ readNumber(preview, ['autoHiddenExistingInvoiceCount']) }}</p></UCard>
        </div>

        <UAlert v-if="warnings.length" color="warning" variant="subtle" icon="i-lucide-triangle-alert" title="Warnings" :description="warnings.join(' | ')" />

        <div class="garmetix-section-card space-y-2">
          <h4 class="text-sm font-medium">Bulk Apply Barcode Mapping (optional)</h4>
          <p class="text-xs text-muted">Upload a Vyapar item code -> Garmetix barcode mapping file to fill overrides in bulk across every matching line below.</p>
          <div class="flex flex-wrap items-center gap-2">
            <input
              type="file"
              accept=".xlsx,.csv"
              class="block max-w-xs rounded-md border border-default bg-default p-2 text-sm file:mr-2 file:rounded file:border-0 file:bg-primary file:px-2 file:py-1 file:text-white"
              @change="onMappingFileChange"
            >
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-wand-2" :loading="applyingMapping" :disabled="!mappingFile" @click="applyBarcodeMapping">Apply Mapping</UButton>
          </div>
        </div>

        <div class="flex flex-wrap items-end gap-3">
          <UFormField label="Invoice Filter">
            <USelect v-model="invoiceFilter" :items="invoiceFilterItems" class="w-48" />
          </UFormField>
          <UFormField label="Line Filter">
            <USelect v-model="lineFilter" :items="lineFilterItems" class="w-48" />
          </UFormField>
          <UInput v-model="searchTerm" icon="i-lucide-search" placeholder="Search invoice, item, code, barcode, category" class="sm:w-72" />
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[1100px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-2 py-2">Import</th>
                <th class="px-2 py-2">Invoice #</th>
                <th class="px-2 py-2">Item</th>
                <th class="px-2 py-2">Category / Size</th>
                <th class="px-2 py-2">Vyapar Code</th>
                <th class="px-2 py-2">Garmetix Barcode</th>
                <th class="px-2 py-2">Override Barcode</th>
                <th class="px-2 py-2 text-right">Qty</th>
                <th class="px-2 py-2 text-right">Amount</th>
                <th class="px-2 py-2">Status</th>
                <th class="px-2 py-2">Create</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="!pagedLineRows.length"><td colspan="11" class="px-3 py-8 text-center text-muted">No lines match these filters.</td></tr>
              <tr v-for="row in pagedLineRows" :key="row.key" class="align-top">
                <td class="px-2 py-2"><input type="checkbox" v-model="row.raw.importLine"></td>
                <td class="px-2 py-2 tabular-nums">{{ row.sourceInvoiceNumber }}</td>
                <td class="px-2 py-2">{{ row.itemName }}</td>
                <td class="px-2 py-2 text-xs text-muted">{{ row.category }}{{ row.size ? ' / ' + row.size : '' }}</td>
                <td class="px-2 py-2 tabular-nums">{{ row.vyaparItemCode || '-' }}</td>
                <td class="px-2 py-2 tabular-nums">{{ row.garmetixBarcode || '-' }}</td>
                <td class="px-2 py-2"><UInput v-model="row.raw.overrideBarcode" size="xs" placeholder="barcode" class="w-32" /></td>
                <td class="px-2 py-2 text-right tabular-nums">{{ row.quantity }}</td>
                <td class="px-2 py-2 text-right tabular-nums">{{ money(row.lineTotal) }}</td>
                <td class="px-2 py-2"><UBadge size="xs" :color="matchStatusColor(row.matchStatus)" variant="soft">{{ row.matchStatus }}</UBadge></td>
                <td class="px-2 py-2"><input type="checkbox" v-model="row.raw.createProductAndStock"></td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="lineRows.length" class="flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
          <p>Showing {{ pagedLineRows.length }} of {{ lineRows.length }} line(s)</p>
          <div class="flex items-center gap-2">
            <USelect v-model="linesPageSize" :items="pageSizeOptions" class="w-28" />
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="linesPage <= 1" @click="linesPage--">Prev</UButton>
            <span>{{ linesPage }} / {{ linesTotalPages }}</span>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="linesPage >= linesTotalPages" @click="linesPage++">Next</UButton>
          </div>
        </div>
      </section>

      <!-- Step 3: Payment sources -->
      <section v-if="paymentSources.length" class="garmetix-section-card space-y-3">
        <h3 class="garmetix-panel-title">Step 3 - Map Payment Sources To Bank Accounts</h3>
        <p class="text-xs text-muted">Non-cash Vyapar payment sources need a Garmetix bank account so payments post correctly.</p>
        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[700px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr><th class="px-2 py-2">Source</th><th class="px-2 py-2">Mode</th><th class="px-2 py-2 text-right">Total</th><th class="px-2 py-2 text-right">Invoices</th><th class="px-2 py-2">Bank Account</th></tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="source in paymentSources" :key="source.key">
                <td class="px-2 py-2">{{ source.sourceName }}<p class="text-xs text-muted">{{ source.exampleDescription }}</p></td>
                <td class="px-2 py-2">{{ source.paymentMode }}</td>
                <td class="px-2 py-2 text-right tabular-nums">{{ money(source.totalAmount) }}</td>
                <td class="px-2 py-2 text-right tabular-nums">{{ source.invoiceCount }}</td>
                <td class="px-2 py-2">
                  <USelect
                    v-if="source.paymentMode !== 'Cash'"
                    v-model="paymentMappings[source.key]"
                    :items="bankAccountItems"
                    placeholder="Select bank account"
                    class="w-56"
                  />
                  <span v-else class="text-xs text-muted">Cash - no mapping needed</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <UAlert v-if="unmappedNonCashCount > 0" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="`${unmappedNonCashCount} non-cash payment source(s) still unmapped.`" />
      </section>

      <!-- Step 4: Confirm -->
      <section class="garmetix-section-card space-y-3">
        <h3 class="garmetix-panel-title">Step 4 - Confirm Import</h3>
        <div class="grid gap-3 sm:grid-cols-2">
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" v-model="confirmForm.useVyaparInvoiceNumbers"> Use Vyapar invoice numbers</label>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" v-model="confirmForm.createMissingProductsAndStock"> Create missing products and stock</label>
          <label class="flex items-center gap-2 text-sm sm:col-span-2"><input type="checkbox" v-model="confirmForm.allowStockBridgeForInsufficientStock"> Allow stock bridge for insufficient-stock lines</label>
          <UFormField label="Default Salesman">
            <USelect v-model="confirmForm.defaultSalesmanId" :items="salesmanItems" placeholder="Optional" class="w-full" />
          </UFormField>
          <UFormField label="Default Bank Account (fallback)">
            <USelect v-model="confirmForm.defaultBankAccountId" :items="bankAccountItems" placeholder="Optional" class="w-full" />
          </UFormField>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-check-check" color="primary" :disabled="!fullyMatchedInvoices.length" @click="openApproval('fullyMatched')">Import Fully Matched {{ fullyMatchedInvoices.length }}</UButton>
          <UButton icon="i-lucide-circle-check-big" color="neutral" variant="soft" :disabled="!readyInvoices.length" @click="openApproval('ready')">Confirm Import Ready {{ readyInvoices.length }}</UButton>
        </div>
      </section>

      <section v-if="confirmResult" class="garmetix-section-card space-y-2">
        <h3 class="garmetix-panel-title">Import Result</h3>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Invoices Imported</p><p class="text-lg font-semibold">{{ readNumber(confirmResult, ['importedInvoiceCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Lines Imported</p><p class="text-lg font-semibold">{{ readNumber(confirmResult, ['importedLineCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Products Created</p><p class="text-lg font-semibold">{{ readNumber(confirmResult, ['createdProductCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Bill Amount</p><p class="text-lg font-semibold">{{ money(readNumber(confirmResult, ['importedBillAmount'])) }}</p></UCard>
        </div>
        <p v-if="readNumber(confirmResult, ['skippedInvoiceCount'])" class="text-sm text-warning">{{ readNumber(confirmResult, ['skippedInvoiceCount']) }} invoice(s) skipped.</p>
      </section>
    </template>

    <UModal v-model:open="approvalOpen" title="Confirm Import" description="This posts real sale invoices, payments, stock movements and accounting entries. This action cannot be undone from here except via batch Undo.">
      <template #body>
        <div class="grid gap-3">
          <p class="text-sm">Mode: <strong>{{ approvalMode === 'fullyMatched' ? 'Import Fully Matched' : 'Confirm Import Ready' }}</strong></p>
          <p class="text-sm">Invoices to import: <strong>{{ approvalMode === 'fullyMatched' ? fullyMatchedInvoices.length : readyInvoices.length }}</strong></p>
          <p class="text-sm">Create missing products/stock: <strong>{{ confirmForm.createMissingProductsAndStock ? 'Yes' : 'No' }}</strong></p>
          <p class="text-sm">Allow stock bridge: <strong>{{ confirmForm.allowStockBridgeForInsufficientStock ? 'Yes' : 'No' }}</strong></p>
          <div class="flex justify-end gap-2">
            <UButton color="primary" icon="i-lucide-check" :loading="confirming" @click="confirmImport">Approve & Import</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { getStoredUser } from '@garmetix/shared-auth'
import { pageSizeOptions, paginateRows, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Vyapar Sale Import - Garmetix' })

const { get, post, postForm } = useMainApiClient()

const error = ref('')
const message = ref('')
const previewing = ref(false)
const applyingMapping = ref(false)
const confirming = ref(false)

const stores = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const salesmen = ref<ApiRecord[]>([])
const storeId = ref('')
const uploadFile = ref<File | null>(null)
const mappingFile = ref<File | null>(null)
const preview = ref<ApiRecord | null>(null)
const confirmResult = ref<ApiRecord | null>(null)

const invoiceFilter = ref('all')
const lineFilter = ref('all')
const searchTerm = ref('')
const linesPage = ref(1)
const linesPageSize = ref<number>(pageSizeOptions[0].value)

const paymentMappings = reactive<Record<string, string>>({})
const confirmForm = reactive({
  useVyaparInvoiceNumbers: true,
  createMissingProductsAndStock: false,
  allowStockBridgeForInsufficientStock: false,
  defaultSalesmanId: '',
  defaultBankAccountId: ''
})

const approvalOpen = ref(false)
const approvalMode = ref<'fullyMatched' | 'ready'>('fullyMatched')

function money(value: number) {
  return formatIndianMoney(value)
}

const storeItems = computed(() => stores.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })).filter(item => item.value))
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountHolderName', 'bankName'], readText(item, ['name'])), value: readText(item, ['id'], '') })).filter(item => item.value))
const salesmanItems = computed(() => salesmen.value.map(item => ({ label: readText(item, ['name', 'fullName']), value: readText(item, ['id'], '') })).filter(item => item.value))

const invoiceFilterItems = [
  { label: 'All Invoices', value: 'all' },
  { label: 'Ready For Import', value: 'ready' },
  { label: 'Fully Matched', value: 'fullyMatched' },
  { label: 'Needs Review', value: 'review' },
  { label: 'Duplicate', value: 'duplicate' }
]
const lineFilterItems = [
  { label: 'All Lines', value: 'all' },
  { label: 'Matched', value: 'Matched' },
  { label: 'Missing Product/Stock', value: 'MissingProductOrStock' },
  { label: 'Insufficient Stock', value: 'InsufficientStock' },
  { label: 'Needs Review', value: 'review' }
]

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  uploadFile.value = input.files?.[0] ?? null
}
function onMappingFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  mappingFile.value = input.files?.[0] ?? null
}

const selectedStore = computed(() => stores.value.find(item => readText(item, ['id'], '') === storeId.value) || null)

const warnings = computed(() => readArray(preview.value, ['warnings']).map(item => String(item)))
const missingProducts = computed(() => toRows(preview.value?.missingProducts))
const paymentSources = computed(() => toRows(preview.value?.paymentSources).map(item => ({
  key: `${readText(item, ['sourceName'])}::${readText(item, ['paymentMode'])}`,
  sourceName: readText(item, ['sourceName']),
  paymentMode: readText(item, ['paymentMode']),
  totalAmount: readNumber(item, ['totalAmount']),
  invoiceCount: readNumber(item, ['invoiceCount']),
  exampleDescription: readText(item, ['exampleDescription'], '')
})))
const unmappedNonCashCount = computed(() => paymentSources.value.filter(source => source.paymentMode !== 'Cash' && !paymentMappings[source.key]).length)

const invoiceRows = computed(() => toRows(preview.value?.invoices))

const fullyMatchedInvoices = computed(() => invoiceRows.value.filter(inv => inv.fullyMatched && !inv.hiddenBecauseAlreadyImported))
const readyInvoices = computed(() => invoiceRows.value.filter(inv => inv.readyForImport && !inv.hiddenBecauseAlreadyImported))

function invoiceMatchesFilter(inv: ApiRecord) {
  if (invoiceFilter.value === 'all') return true
  if (invoiceFilter.value === 'ready') return Boolean(inv.readyForImport)
  if (invoiceFilter.value === 'fullyMatched') return Boolean(inv.fullyMatched)
  if (invoiceFilter.value === 'duplicate') return Boolean(inv.duplicateInvoice)
  if (invoiceFilter.value === 'review') return !inv.fullyMatched && !inv.duplicateInvoice
  return true
}

const lineRows = computed(() => {
  const term = searchTerm.value.trim().toLowerCase()
  const rows: Array<{ key: string, raw: ApiRecord, sourceInvoiceNumber: string, itemName: string, category: string, size: string, vyaparItemCode: string, garmetixBarcode: string, quantity: number, lineTotal: number, matchStatus: string }> = []
  invoiceRows.value.forEach((inv, invIndex) => {
    if (!invoiceMatchesFilter(inv)) return
    toRows(inv.lines).forEach((line, lineIndex) => {
      const matchStatus = readText(line, ['matchStatus'])
      if (lineFilter.value !== 'all') {
        if (lineFilter.value === 'review' && !line.reviewRequired) return
        if (lineFilter.value !== 'review' && matchStatus !== lineFilter.value) return
      }
      const sourceInvoiceNumber = readText(line, ['sourceInvoiceNumber'], readText(inv, ['sourceInvoiceNumber']))
      const itemName = readText(line, ['itemName'])
      const vyaparItemCode = readText(line, ['vyaparItemCode'], '')
      const garmetixBarcode = readText(line, ['garmetixBarcode'], '')
      const category = readText(line, ['category'], '')
      if (term) {
        const haystack = `${sourceInvoiceNumber} ${itemName} ${vyaparItemCode} ${garmetixBarcode} ${category}`.toLowerCase()
        if (!haystack.includes(term)) return
      }
      rows.push({
        key: `${invIndex}:${lineIndex}`,
        raw: line,
        sourceInvoiceNumber,
        itemName,
        category,
        size: readText(line, ['size'], ''),
        vyaparItemCode,
        garmetixBarcode,
        quantity: readNumber(line, ['quantity']),
        lineTotal: readNumber(line, ['lineTotal']),
        matchStatus
      })
    })
  })
  return rows
})

const linesTotalPages = computed(() => Math.max(1, Math.ceil(lineRows.value.length / Number(linesPageSize.value || 25))))
const pagedLineRows = computed(() => paginateRows(lineRows.value, linesPage.value, linesPageSize.value))
watch(linesPageSize, () => { linesPage.value = 1 })
watch(linesTotalPages, (value) => { if (linesPage.value > value) linesPage.value = value })
watch([invoiceFilter, lineFilter, searchTerm], () => { linesPage.value = 1 })

function matchStatusColor(status: string) {
  if (status === 'Matched') return 'success'
  if (status === 'InsufficientStock') return 'warning'
  return 'error'
}

async function loadOptions() {
  try {
    const storedUser = getStoredUser(window.localStorage)
    const [storeData, bankData] = await Promise.allSettled([
      get<unknown>('stores'),
      get<unknown>('bank-accounts')
    ])
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (bankData.status === 'fulfilled') bankAccounts.value = toRows(bankData.value)
    if (!storeId.value) storeId.value = storedUser?.storeId || readText(stores.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stores/bank accounts.'
  }
}

async function loadSalesmen() {
  if (!selectedStore.value) return
  try {
    const companyId = readText(selectedStore.value, ['companyId'], '')
    const data = await get<ApiRecord>('billing/options', { companyId, storeId: storeId.value, take: 100 })
    salesmen.value = toRows((data as ApiRecord)?.salesmen ?? data)
  } catch {
    salesmen.value = []
  }
}
watch(storeId, loadSalesmen)

async function loadPreview() {
  if (!storeId.value || !uploadFile.value) return
  const store = selectedStore.value
  const companyId = readText(store, ['companyId'], '')
  const storeGroupId = readText(store, ['storeGroupId'], '')
  if (!companyId || !storeGroupId) {
    error.value = 'Selected store is missing company/store-group mapping.'
    return
  }
  previewing.value = true
  error.value = ''
  message.value = ''
  confirmResult.value = null
  try {
    const form = new FormData()
    form.append('file', uploadFile.value)
    const result = await postForm<ApiRecord>(`sale-import/vyapar/preview?companyId=${companyId}&storeGroupId=${storeGroupId}&storeId=${storeId.value}`, form)
    preview.value = result
    message.value = 'Preview loaded. Review matches below before confirming.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to parse the Vyapar file.'
  } finally {
    previewing.value = false
  }
}

async function applyBarcodeMapping() {
  if (!mappingFile.value || !preview.value) return
  applyingMapping.value = true
  error.value = ''
  try {
    const form = new FormData()
    form.append('file', mappingFile.value)
    const result = await postForm<ApiRecord>('sale-import/vyapar/barcode-mapping/preview', form)
    const mappings = toRows(result?.mappings)
    let applied = 0
    invoiceRows.value.forEach(inv => {
      toRows(inv.lines).forEach(line => {
        const code = readText(line, ['vyaparItemCode'], '')
        const name = readText(line, ['itemName'], '').toLowerCase()
        const match = mappings.find(m => (code && readText(m, ['vyaparItemCode'], '') === code) || (name && readText(m, ['itemName'], '').toLowerCase() === name))
        if (match) {
          const barcode = readText(match, ['garmetixBarcode'], '')
          if (barcode) {
            line.overrideBarcode = barcode
            line.importLine = true
            applied += 1
          }
        }
      })
    })
    message.value = `Applied ${applied} barcode override(s) from the mapping file.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to apply barcode mapping.'
  } finally {
    applyingMapping.value = false
  }
}

function exportMissingBarcodeCsv() {
  const header = ['Vyapar Item Code', 'Item Name', 'Category', 'HSN', 'Size', 'Quantity', 'Amount', 'Tax Rate', 'Invoice Count', 'Suggested Barcode']
  const rows = missingProducts.value.map(item => [
    readText(item, ['vyaparItemCode'], ''), readText(item, ['itemName']), readText(item, ['category'], ''), readText(item, ['hsnCode'], ''),
    readText(item, ['size'], ''), String(readNumber(item, ['quantity'])), String(readNumber(item, ['amount'])), String(readNumber(item, ['taxRate'])),
    String(readNumber(item, ['invoiceCount'])), readText(item, ['suggestedBarcode'], '')
  ])
  downloadCsv('vyapar-missing-barcodes.csv', header, rows)
}

function downloadCsv(fileName: string, header: string[], rows: string[][]) {
  const escape = (value: string) => (/[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value)
  const lines = [header, ...rows].map(row => row.map(escape).join(',')).join('\n')
  const blob = new Blob([lines], { type: 'text/csv;charset=utf-8;' })
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(objectUrl)
}

function openApproval(mode: 'fullyMatched' | 'ready') {
  approvalMode.value = mode
  approvalOpen.value = true
}

async function confirmImport() {
  if (!preview.value || !selectedStore.value) return
  confirming.value = true
  error.value = ''
  try {
    const invoices = approvalMode.value === 'fullyMatched' ? fullyMatchedInvoices.value : readyInvoices.value
    const paymentBankMappings = paymentSources.value
      .filter(source => paymentMappings[source.key])
      .map(source => ({ sourceName: source.sourceName, paymentMode: source.paymentMode, bankAccountId: paymentMappings[source.key] }))

    const body = {
      companyId: readText(selectedStore.value, ['companyId'], ''),
      storeGroupId: readText(selectedStore.value, ['storeGroupId'], ''),
      storeId: storeId.value,
      useVyaparInvoiceNumbers: confirmForm.useVyaparInvoiceNumbers,
      createMissingProductsAndStock: confirmForm.createMissingProductsAndStock,
      allowStockBridgeForInsufficientStock: confirmForm.allowStockBridgeForInsufficientStock,
      defaultBankAccountId: confirmForm.defaultBankAccountId || null,
      defaultSalesmanId: confirmForm.defaultSalesmanId || null,
      invoices,
      paymentBankMappings,
      finalApprovalConfirmed: true,
      sourceFileName: readText(preview.value, ['sourceFileName'], '')
    }
    const result = await post<ApiRecord>('sale-import/vyapar/confirm', body)
    confirmResult.value = result
    message.value = 'Import confirmed.'
    approvalOpen.value = false
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to confirm import.'
  } finally {
    confirming.value = false
  }
}

onMounted(async () => {
  await loadOptions()
  await loadSalesmen()
})
</script>

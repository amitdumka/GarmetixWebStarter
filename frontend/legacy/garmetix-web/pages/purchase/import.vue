<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated

const NONE = '__none__'
const MANUAL_VENDOR = '__manual__'

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const lookup = ref<any>({ vendors: [], taxes: [], units: [], productTypes: [], productGroups: [], categories: [], subCategories: [] })
const bankAccounts = ref<any[]>([])
const batches = ref<any[]>([])
const selectedBatch = ref<any | null>(null)
const loading = ref(false)
const uploading = ref(false)
const saving = ref(false)
const posting = ref(false)
const uploadFile = ref<File | null>(null)
const rawText = ref('')
const proofUrl = ref('')
const duplicateOverrideReason = ref('')
const reparseText = ref('')
const extractedText = ref('')
const loadingExtractedText = ref(false)
const productSearches = ref<Record<string, string>>({})
const productMatches = ref<Record<string, any[]>>({})
const matchingLine = ref<string | null>(null)
const bulkFindingSimilar = ref(false)
const reparsing = ref(false)
const overridingDuplicate = ref(false)
const recheckingDuplicate = ref(false)
const distributingDiscount = ref(false)
const generatingBarcodes = ref(false)
const replaceLinesOnReparse = ref(false)
const importFiles = ref<any[]>([])
const splitDialogOpen = ref(false)
const splitLineIndex = ref(-1)
const splitSizeText = ref('')
const splittingLine = ref(false)
const deletingDraftId = ref<string | null>(null)
const cleanupHistoryLoading = ref(false)
const importStorageSummary = ref<any | null>(null)
const loadingStorageSummary = ref(false)
const splitTemplate = ref('custom')
const lastPostedResult = ref<any | null>(null)

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
const gstPriceModeOptions = [
  { value: 'Inclusive', label: 'Cost includes GST' },
  { value: 'Exclusive', label: 'Cost before GST' }
]
const splitTemplates = [
  { value: 'custom', label: 'Custom', labels: [] },
  { value: 'shirt', label: 'Shirt S-XXL', labels: ['S', 'M', 'L', 'XL', 'XXL'] },
  { value: 'jeans', label: 'Jeans 30-40', labels: ['30', '32', '34', '36', '38', '40'] },
  { value: 'kurta', label: 'Kurta 38-46', labels: ['38', '40', '42', '44', '46'] },
  { value: 'free', label: 'Free size', labels: ['Free'] }
]

const selectedStore = computed(() => stores.value.find((store) => store.id === workspace.storeId.value) || stores.value[0] || null)
const selectedCompanyId = computed(() => workspace.companyId.value || selectedStore.value?.companyId || companies.value[0]?.id || null)
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || selectedStore.value?.storeGroupId || null)
const selectedStoreId = computed(() => workspace.storeId.value || selectedStore.value?.id || null)

const vendorOptions = computed(() => [
  { value: MANUAL_VENDOR, label: 'New supplier / manual entry' },
  ...(lookup.value.vendors?.map((vendor: any) => ({ value: vendor.id, label: `${vendor.name || 'Supplier'}${vendor.gstin ? ` | ${vendor.gstin}` : ''}` })) || [])
])
const taxOptions = computed(() => [
  { value: NONE, label: 'Select GST / tax' },
  ...(lookup.value.taxes?.map((item: any) => ({ value: item.id, label: `${item.name || 'GST'} - ${Number(item.rate || item.compositeRate || 0).toFixed(2)}%` })) || [])
])
const categoryOptions = computed(() => [
  { value: NONE, label: 'No category' },
  ...(lookup.value.categories?.map((item: any) => ({ value: item.id, label: item.name })) || [])
])
const subCategoryOptions = computed(() => [
  { value: NONE, label: 'No sub category' },
  ...(lookup.value.subCategories?.map((item: any) => ({ value: item.id, label: item.name })) || [])
])
const unitOptions = computed(() => lookup.value.units || [])
const productTypeOptions = computed(() => lookup.value.productTypes || [])
const productGroupOptions = computed(() => lookup.value.productGroups || [])
const bankAccountOptions = computed(() => [
  { value: NONE, label: 'No bank account' },
  ...bankAccounts.value.map((account) => ({ value: account.id, label: `${account.accountHolderName || 'Bank'} ${account.accountNumber || ''}`.trim() }))
])

const draftWarnings = computed(() => selectedBatch.value?.warnings || [])
const draftLines = computed(() => selectedBatch.value?.lines || [])
const proofIsImage = computed(() => selectedBatch.value?.contentType?.startsWith('image/'))
const lineTotal = (line: any) => {
  const qty = Number(line.quantity || 0)
  const unitCost = Number(line.costPrice || 0)
  const discount = Number(line.lineDiscount || 0) > 0 ? Number(line.lineDiscount || 0) : Number(line.unitDiscount || 0) * qty
  const base = Math.max((unitCost * qty) - discount, 0)
  if (line.gstPriceMode === 'Exclusive') return base + (base * Number(line.taxRate || 0) / 100)
  return base
}
function lineDiscountAmount(line: any) {
  const qty = Number(line.quantity || 0)
  return Number(line.lineDiscount || 0) > 0 ? Number(line.lineDiscount || 0) : Number(line.unitDiscount || 0) * qty
}
function lineGrossAmount(line: any) {
  return Number(line.costPrice || 0) * Number(line.quantity || 0)
}
function lineTaxableAmount(line: any) {
  const net = Math.max(lineGrossAmount(line) - lineDiscountAmount(line), 0)
  const rate = Number(line.taxRate || 0) / 100
  if (line.gstPriceMode === 'Exclusive') return net
  return rate > 0 ? net / (1 + rate) : net
}
function estimatedTax(line: any) {
  const taxable = lineTaxableAmount(line)
  return taxable * Number(line.taxRate || 0) / 100
}

const draftLineTotal = computed(() => draftLines.value.filter((line: any) => !line.ignored).reduce((sum: number, line: any) => sum + lineTotal(line), 0))
const payableTotal = computed(() => draftLineTotal.value + Number(selectedBatch.value?.freightAmount || 0) + Number(selectedBatch.value?.roundOff || 0))
const reconciliation = computed(() => {
  const active = draftLines.value.filter((line: any) => !line.ignored)
  const gross = active.reduce((sum: number, line: any) => sum + lineGrossAmount(line), 0)
  const discount = active.reduce((sum: number, line: any) => sum + lineDiscountAmount(line), 0)
  const taxable = active.reduce((sum: number, line: any) => sum + lineTaxableAmount(line), 0)
  const tax = active.reduce((sum: number, line: any) => sum + estimatedTax(line), 0)
  const lineTotalValue = active.reduce((sum: number, line: any) => sum + lineTotal(line), 0)
  const freight = Number(selectedBatch.value?.freightAmount || 0)
  const roundOff = Number(selectedBatch.value?.roundOff || 0)
  const calculatedGrand = Math.round(lineTotalValue + freight + roundOff)
  const scannedBill = Number(selectedBatch.value?.billAmount || 0)
  const headerDiscount = Number(selectedBatch.value?.discountAmount || 0)
  return {
    gross,
    discount,
    taxable,
    tax,
    lineTotal: lineTotalValue,
    freight,
    roundOff,
    calculatedGrand,
    scannedBill,
    billDifference: scannedBill > 0 ? scannedBill - calculatedGrand : 0,
    headerDiscount,
    headerDiscountDifference: headerDiscount > 0 ? headerDiscount - discount : 0,
    taxableDifference: (gross - discount) - taxable
  }
})
const reviewStats = computed(() => {
  const lines = draftLines.value || []
  const active = lines.filter((line: any) => !line.ignored)
  const barcodeCounts = new Map<string, number>()
  active.forEach((line: any) => {
    const barcode = String(line.barcodeFinal || line.barcodeRaw || '').trim().toUpperCase()
    if (barcode) barcodeCounts.set(barcode, (barcodeCounts.get(barcode) || 0) + 1)
  })
  const duplicateBarcodes = Array.from(barcodeCounts.values()).filter((count) => count > 1).length
  return {
    active: active.length,
    ignored: lines.length - active.length,
    newProducts: active.filter((line: any) => !line.productId).length,
    matchedProducts: active.filter((line: any) => line.productId).length,
    missingBarcode: active.filter((line: any) => !String(line.barcodeFinal || line.barcodeRaw || '').trim()).length,
    missingTax: active.filter((line: any) => !nullableSelect(line.taxId) && Number(line.taxRate || 0) <= 0).length,
    lowConfidence: active.filter((line: any) => Number(line.confidenceScore || 0) > 0 && Number(line.confidenceScore || 0) < 55).length,
    duplicateBarcodes,
    calculatedTotal: payableTotal.value,
    billDifference: Number(selectedBatch.value?.billAmount || 0) > 0 ? Number(selectedBatch.value?.billAmount || 0) - payableTotal.value : 0
  }
})

const postingChecklist = computed(() => {
  const batch = selectedBatch.value
  const active = draftLines.value.filter((line: any) => !line.ignored)
  const requiresBank = Number(batch?.paidAmount || 0) > 0 && Number(batch?.paymentMode || 0) !== 0
  const missingNewProductDefaults = active.filter((line: any) => !line.productId && (
    !String(line.productNameFinal || line.productNameRaw || '').trim() ||
    !String(line.barcodeFinal || line.barcodeRaw || '').trim() ||
    Number(line.costPrice || 0) <= 0 ||
    Number(line.mrp || 0) <= 0 ||
    !nullableSelect(line.productCategoryId) ||
    !nullableSelect(line.productSubCategoryId)
  )).length

  const checks = [
    { key: 'vendor', label: 'Vendor selected/entered', passed: Boolean(String(batch?.vendorNameFinal || batch?.vendorNameRaw || '').trim()), help: 'Vendor name is required before posting.' },
    { key: 'invoice', label: 'Supplier invoice number', passed: Boolean(String(batch?.supplierInvoiceNumber || '').trim()), help: 'Enter supplier invoice number to avoid duplicate inward.' },
    { key: 'date', label: 'Supplier invoice date', passed: Boolean(batch?.supplierInvoiceDate), help: 'Invoice date is required for purchase and due tracking.' },
    { key: 'lines', label: 'At least one active item line', passed: active.length > 0, help: 'Ignored rows are kept for learning only and will not post.' },
    { key: 'barcode', label: 'No missing/duplicate barcode', passed: reviewStats.value.missingBarcode === 0 && reviewStats.value.duplicateBarcodes === 0, help: 'Generate missing barcodes and correct duplicates before posting.' },
    { key: 'tax', label: 'GST/tax assigned', passed: reviewStats.value.missingTax === 0, help: 'Copy defaults or assign GST on each active row.' },
    { key: 'product-defaults', label: 'New product defaults complete', passed: missingNewProductDefaults === 0, help: `${missingNewProductDefaults} new row(s) still need name, barcode, MRP, cost, category and subcategory.` },
    { key: 'total', label: 'Scanned total reconciled', passed: Math.abs(reconciliation.value.billDifference) <= 1, help: `Difference is ${money(reconciliation.value.billDifference)}. Correct discount, GST mode, freight or round-off.` },
    { key: 'discount', label: 'Header discount reconciled', passed: Math.abs(reconciliation.value.headerDiscountDifference) <= 1 || Number(batch?.discountAmount || 0) <= 0, help: `Header discount difference is ${money(reconciliation.value.headerDiscountDifference)}.` },
    { key: 'bank', label: 'Bank selected for non-cash payment', passed: !requiresBank || Boolean(nullableSelect(batch?.bankAccountId)), help: 'Select bank account for UPI/card/cheque/NEFT/RTGS payment.' }
  ]

  return {
    checks,
    failed: checks.filter((item) => !item.passed),
    ready: checks.every((item) => item.passed)
  }
})

function nullableSelect(value: any) {
  return value && value !== NONE && value !== MANUAL_VENDOR ? value : null
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function toInputDate(value: any) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10)
}

function applyVendor() {
  if (!selectedBatch.value || selectedBatch.value.vendorId === MANUAL_VENDOR) return
  const vendor = lookup.value.vendors?.find((item: any) => item.id === selectedBatch.value.vendorId)
  if (!vendor) return
  selectedBatch.value.vendorNameFinal = vendor.name || selectedBatch.value.vendorNameFinal
  selectedBatch.value.vendorGstinFinal = vendor.gstin || selectedBatch.value.vendorGstinFinal
  selectedBatch.value.vendorMobileNumber = vendor.mobileNumber || selectedBatch.value.vendorMobileNumber
}

function onFileSelected(event: Event) {
  uploadFile.value = (event.target as HTMLInputElement).files?.[0] || null
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [companyRows, storeRows, lookupRows, bankRows, importRows, storageRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('purchase/lookup-options'),
      api.list<any>('bank-accounts'),
      api.get<any[]>('purchase-import/batches?take=50'),
      api.get<any>('purchase-import/storage-summary')
    ])
    companies.value = companyRows
    stores.value = storeRows
    lookup.value = lookupRows
    bankAccounts.value = bankRows
    batches.value = importRows
    importStorageSummary.value = storageRows
  } catch (error) {
    feedback.failed('Supplier invoice import setup failed', error)
  } finally {
    loading.value = false
  }
}

async function uploadSupplierInvoice() {
  if (!uploadFile.value) {
    feedback.notify('File missing', 'Choose supplier invoice PDF or image first.', 'warning')
    return
  }
  if (!selectedCompanyId.value || !selectedStoreGroupId.value || !selectedStoreId.value) {
    feedback.notify('Store missing', 'Select company/store before importing supplier invoice.', 'warning')
    return
  }

  uploading.value = true
  try {
    const form = new FormData()
    form.append('file', uploadFile.value)
    form.append('companyId', selectedCompanyId.value)
    form.append('storeGroupId', selectedStoreGroupId.value)
    form.append('storeId', selectedStoreId.value)
    if (rawText.value.trim()) form.append('rawText', rawText.value.trim())

    const draft = await $fetch<any>(`${config.public.apiBase}/purchase-import/uploads`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    api.clearCache()
    selectedBatch.value = normalizeDraft(draft)
    await loadProof(draft.id)
    await loadImportFiles(draft.id)
    await refreshListOnly()
    feedback.notify('Import draft created', 'Review supplier, product, GST and totals before posting inward.', 'success')
  } catch (error) {
    feedback.failed('Could not create supplier invoice import draft', error)
  } finally {
    uploading.value = false
  }
}

async function refreshListOnly() {
  const [importRows, storageRows] = await Promise.all([
    api.get<any[]>('purchase-import/batches?take=50'),
    api.get<any>('purchase-import/storage-summary')
  ])
  batches.value = importRows
  importStorageSummary.value = storageRows
}

async function refreshStorageSummary() {
  loadingStorageSummary.value = true
  try {
    importStorageSummary.value = await api.get<any>('purchase-import/storage-summary')
  } catch (error) {
    feedback.failed('Could not load supplier invoice storage summary', error)
  } finally {
    loadingStorageSummary.value = false
  }
}

async function openDraft(id: string) {
  loading.value = true
  try {
    const draft = await api.get<any>(`purchase-import/batches/${id}`)
    selectedBatch.value = normalizeDraft(draft)
    await loadProof(id)
    await loadImportFiles(id)
  } catch (error) {
    feedback.failed('Could not open import draft', error)
  } finally {
    loading.value = false
  }
}

function normalizeDraft(draft: any) {
  const normalized = { ...draft }
  normalized.vendorId = normalized.vendorId || MANUAL_VENDOR
  normalized.supplierInvoiceDate = toInputDate(normalized.supplierInvoiceDate)
  normalized.dueDate = toInputDate(normalized.dueDate)
  normalized.bankAccountId = normalized.bankAccountId || NONE
  duplicateOverrideReason.value = normalized.duplicateOverrideReason || ''
  extractedText.value = ''
  normalized.lines = (normalized.lines || []).map((line: any, index: number) => ({
    ...line,
    lineNumber: line.lineNumber || index + 1,
    taxId: line.taxId || NONE,
    gstPriceMode: line.gstPriceMode || 'Inclusive',
    productCategoryId: line.productCategoryId || NONE,
    productSubCategoryId: line.productSubCategoryId || NONE,
    unit: Number(line.unit ?? 2),
    productType: Number(line.productType ?? 0),
    productGroup: Number(line.productGroup ?? 0)
  }))
  return normalized
}

async function loadProof(id: string) {
  if (proofUrl.value) {
    URL.revokeObjectURL(proofUrl.value)
    proofUrl.value = ''
  }
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/purchase-import/batches/${id}/proof`, {
      method: 'GET',
      headers: api.authHeaders(),
      responseType: 'blob'
    })
    proofUrl.value = URL.createObjectURL(blob)
  } catch {
    proofUrl.value = ''
  }
}

async function loadImportFiles(id: string) {
  try {
    importFiles.value = await api.get<any[]>(`purchase-import/batches/${id}/files`)
  } catch {
    importFiles.value = []
  }
}

async function openImportFile(file: any) {
  if (!selectedBatch.value?.id || !file?.id) return
  try {
    const blob = await $fetch<Blob>(`${config.public.apiBase}/purchase-import/batches/${selectedBatch.value.id}/files/${file.id}/download`, {
      method: 'GET',
      headers: api.authHeaders(),
      responseType: 'blob'
    })
    const url = URL.createObjectURL(blob)
    window.open(url, '_blank', 'noopener,noreferrer')
    setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch (error) {
    feedback.failed('Could not open import file', error)
  }
}

function fileSize(bytes: number) {
  const value = Number(bytes || 0)
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / 1024 / 1024).toFixed(1)} MB`
}

async function loadExtractedText() {
  if (!selectedBatch.value?.id) return
  loadingExtractedText.value = true
  try {
    extractedText.value = await $fetch<string>(`${config.public.apiBase}/purchase-import/batches/${selectedBatch.value.id}/extracted-text`, {
      method: 'GET',
      headers: api.authHeaders(),
      responseType: 'text'
    })
    if (!extractedText.value.trim()) feedback.notify('No text found', 'OCR/text extraction did not return useful text for this file.', 'warning')
  } catch (error) {
    feedback.failed('Could not load extracted text', error)
  } finally {
    loadingExtractedText.value = false
  }
}

function useExtractedTextForReparse() {
  if (!extractedText.value.trim()) return
  reparseText.value = extractedText.value.trim()
  feedback.notify('Text copied to reparse box', 'You can edit it before reparsing the current draft.', 'success')
}

function addManualLine() {
  if (!selectedBatch.value) return
  selectedBatch.value.lines.push({
    id: null,
    lineNumber: selectedBatch.value.lines.length + 1,
    productId: null,
    productNameFinal: '',
    barcodeFinal: '',
    hsnCode: '',
    unit: 2,
    quantity: 1,
    mrp: 0,
    costPrice: 0,
    unitDiscount: 0,
    lineDiscount: 0,
    taxRate: 0,
    gstPriceMode: 'Inclusive',
    taxId: lookup.value.taxes?.[0]?.id || NONE,
    productCategoryId: lookup.value.categories?.[0]?.id || NONE,
    productSubCategoryId: lookup.value.subCategories?.[0]?.id || NONE,
    productType: 0,
    productGroup: 0,
    ignored: false,
    matchStatus: 'ManualLine',
    reviewRequired: true
  })
}

function removeLine(index: number) {
  if (!selectedBatch.value) return
  selectedBatch.value.lines.splice(index, 1)
  selectedBatch.value.lines.forEach((line: any, rowIndex: number) => { line.lineNumber = rowIndex + 1 })
}

function openSplitLine(line: any, index: number) {
  const qty = Math.max(Math.round(Number(line.quantity || 0)), 1)
  splitLineIndex.value = index
  splitTemplate.value = 'custom'
  splitSizeText.value = Array.from({ length: Math.min(qty, 100) }, () => '').join('\n')
  splitDialogOpen.value = true
  feedback.notify('Enter size/color labels', `Enter ${qty} labels, one per piece, or use a template with size=qty lines such as 38=2.`, 'info')
}

function parseSplitSizeLabels() {
  const labels: string[] = []
  splitSizeText.value
    .split(/[\n,]+/)
    .map((item) => item.trim())
    .filter(Boolean)
    .forEach((item) => {
      const match = item.match(/^(.+?)[=:x×*]\s*(\d+)$/i)
      if (match) {
        const label = match[1].trim()
        const count = Math.min(Math.max(Number(match[2] || 0), 0), 500)
        for (let i = 0; i < count; i++) labels.push(label)
        return
      }
      labels.push(item)
    })
  return labels
}

function applySplitTemplate(templateValue: string) {
  splitTemplate.value = templateValue
  const template = splitTemplates.find((item) => item.value === templateValue)
  const labels = template?.labels || []
  if (!labels.length) return
  const index = splitLineIndex.value
  const line = selectedBatch.value?.lines?.[index]
  const qty = Math.max(Math.round(Number(line?.quantity || 0)), 1)
  if (labels.length === 1) {
    splitSizeText.value = `${labels[0]}=${qty}`
    return
  }
  splitSizeText.value = labels.map((label, idx) => `${label}=${idx < qty % labels.length ? Math.floor(qty / labels.length) + 1 : Math.floor(qty / labels.length)}`)
    .filter((item) => !item.endsWith('=0'))
    .join('\n')
}

function appendVariantName(name: string, label: string) {
  const base = String(name || 'Imported product').trim()
  return base.toLowerCase().includes(label.toLowerCase()) ? base : `${base} - ${label}`
}

async function applySplitBySize() {
  if (!selectedBatch.value) return
  const index = splitLineIndex.value
  const line = selectedBatch.value.lines[index]
  if (!line) return
  const labels = parseSplitSizeLabels()
  const qty = Math.max(Math.round(Number(line.quantity || 0)), 1)
  if (labels.length !== qty) {
    feedback.notify('Size count mismatch', `Line quantity is ${qty}. Enter exactly ${qty} size/color labels, one per piece.`, 'warning')
    return
  }

  splittingLine.value = true
  try {
    const lineDiscount = Number(line.lineDiscount || 0) > 0 ? Number(line.lineDiscount || 0) / labels.length : 0
    const splitLines = labels.map((label, offset) => ({
      ...line,
      id: null,
      lineNumber: index + offset + 1,
      productId: null,
      productNameFinal: appendVariantName(line.productNameFinal || line.productNameRaw || 'Imported product', label),
      barcodeRaw: '',
      barcodeFinal: '',
      quantity: 1,
      lineDiscount: Number(lineDiscount.toFixed(2)),
      matchStatus: 'SplitVariantDraft',
      reviewRequired: true,
      reviewMessage: `Split from scanned invoice line ${index + 1} for size/color ${label}. Match existing product if available, or generate barcode and create new item.`,
      ignored: false
    }))
    selectedBatch.value.lines.splice(index, 1, ...splitLines)
    selectedBatch.value.lines.forEach((item: any, rowIndex: number) => { item.lineNumber = rowIndex + 1 })
    splitDialogOpen.value = false
    splitSizeText.value = ''
    feedback.notify('Line split', `${labels.length} size/color-wise draft items were created. Use Find similar or Generate barcodes before posting.`, 'success')
  } finally {
    splittingLine.value = false
  }
}

function findSimilarForLine(line: any, index: number) {
  const key = lineKey(line, index)
  productSearches.value[key] = line.productNameFinal || line.productNameRaw || line.barcodeFinal || ''
  return searchProductForLine(line, index)
}

async function findSimilarForAllNewLines() {
  if (!selectedBatch.value?.lines?.length) return
  const candidates = selectedBatch.value.lines
    .map((line: any, index: number) => ({ line, index }))
    .filter(({ line }: any) => !line.ignored && !line.productId && String(line.productNameFinal || line.productNameRaw || '').trim())
    .slice(0, 25)
  if (!candidates.length) {
    feedback.notify('No new lines found', 'All visible rows are already matched or ignored.', 'info')
    return
  }
  bulkFindingSimilar.value = true
  let matched = 0
  try {
    for (const { line, index } of candidates) {
      const key = lineKey(line, index)
      const q = String(line.productNameFinal || line.productNameRaw || '').trim()
      productSearches.value[key] = q
      const params = new URLSearchParams({ query: q, take: '5' })
      if (selectedStoreId.value) params.append('storeId', selectedStoreId.value)
      const rows = await api.get<any[]>(`purchase-import/product-matches?${params.toString()}`)
      productMatches.value[key] = rows
      if (rows?.length) matched++
    }
    feedback.notify('Similar product scan complete', `${matched} of ${candidates.length} new lines have possible product matches. Review chips under each line before posting.`, matched ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Could not scan similar products', error)
  } finally {
    bulkFindingSimilar.value = false
  }
}

async function deleteImportDraft(batch: any) {
  if (!batch?.id) return
  if (batch.status === 'Posted') {
    feedback.notify('Posted proof protected', 'Posted supplier invoice imports are purchase proof and cannot be deleted from history.', 'warning')
    return
  }
  if (!confirm('Delete this scanned supplier invoice history and stored proof/audit files? Posted imports are protected, but this unposted/failed draft will be removed.')) return
  deletingDraftId.value = batch.id
  try {
    await api.remove('purchase-import/batches', batch.id)
    if (selectedBatch.value?.id === batch.id) selectedBatch.value = null
    await refreshListOnly()
    feedback.notify('Import history deleted', 'Failed/unposted scanned invoice history and stored files were removed.', 'success')
  } catch (error) {
    feedback.failed('Could not delete import history', error)
  } finally {
    deletingDraftId.value = null
  }
}

async function cleanupFailedHistory() {
  if (!confirm('Delete all failed/rejected unposted supplier invoice import history and stored OCR/proof files? Posted purchase proofs will not be touched.')) return
  cleanupHistoryLoading.value = true
  try {
    const result = await api.create<any>('purchase-import/cleanup-history', {
      olderThanDays: 0,
      deleteFailed: true,
      deleteRejected: true,
      deleteNeedsReview: false,
      deleteReadyToPost: false,
      deleteFiles: true
    })
    if (selectedBatch.value && ['Failed', 'Rejected'].includes(selectedBatch.value.status)) selectedBatch.value = null
    await refreshListOnly()
    feedback.notify('Import history cleanup complete', result?.messages?.[0] || `Deleted ${result?.deletedBatches || 0} draft(s).`, result?.deletedBatches ? 'success' : 'info')
  } catch (error) {
    feedback.failed('Could not clean scanned invoice history', error)
  } finally {
    cleanupHistoryLoading.value = false
  }
}

function ignoreLowConfidenceLines() {
  if (!selectedBatch.value) return
  let ignored = 0
  selectedBatch.value.lines.forEach((line: any) => {
    const confidence = Number(line.confidenceScore || 0)
    if (!line.ignored && confidence > 0 && confidence < 55 && !line.productId) {
      line.ignored = true
      ignored++
    }
  })
  feedback.notify('Low-confidence rows checked', ignored ? `${ignored} weak OCR rows were marked as Ignore.` : 'No low-confidence unmatched rows found.', ignored ? 'warning' : 'success')
}

function restoreIgnoredLines() {
  if (!selectedBatch.value) return
  let restored = 0
  selectedBatch.value.lines.forEach((line: any) => {
    if (line.ignored) {
      line.ignored = false
      restored++
    }
  })
  feedback.notify('Ignored rows restored', restored ? `${restored} rows were restored for review.` : 'No ignored rows found.', restored ? 'success' : 'info')
}

function fillMissingMrpFromCost() {
  if (!selectedBatch.value) return
  let filled = 0
  selectedBatch.value.lines.forEach((line: any) => {
    if (!line.ignored && Number(line.mrp || 0) <= 0 && Number(line.costPrice || 0) > 0) {
      line.mrp = Number(line.costPrice || 0)
      filled++
    }
  })
  feedback.notify('MRP checked', filled ? `${filled} missing MRP values were filled from cost.` : 'No missing MRP values found.', filled ? 'success' : 'info')
}

function copyDefaultsFromFirstReviewedLine() {
  if (!selectedBatch.value) return
  const source = selectedBatch.value.lines.find((line: any) => !line.ignored && (nullableSelect(line.taxId) || line.taxRate > 0 || nullableSelect(line.productCategoryId) || nullableSelect(line.productSubCategoryId)))
  if (!source) {
    feedback.notify('No source line found', 'Fill GST/category/type on one correct line first, then copy defaults.', 'warning')
    return
  }
  let changed = 0
  selectedBatch.value.lines.forEach((line: any) => {
    if (line.ignored || line === source || line.productId) return
    if (!nullableSelect(line.taxId) && nullableSelect(source.taxId)) { line.taxId = source.taxId; line.taxRate = Number(source.taxRate || line.taxRate || 0); changed++ }
    if (!nullableSelect(line.productCategoryId) && nullableSelect(source.productCategoryId)) { line.productCategoryId = source.productCategoryId; changed++ }
    if (!nullableSelect(line.productSubCategoryId) && nullableSelect(source.productSubCategoryId)) { line.productSubCategoryId = source.productSubCategoryId; changed++ }
    if (source.unit !== undefined && source.unit !== null) line.unit = Number(source.unit)
    if (source.productType !== undefined && source.productType !== null) line.productType = Number(source.productType)
    if (source.productGroup !== undefined && source.productGroup !== null) line.productGroup = Number(source.productGroup)
    if (source.gstPriceMode) line.gstPriceMode = source.gstPriceMode
  })
  feedback.notify('Defaults copied', changed ? 'GST/category/type/group defaults were copied to new product draft lines.' : 'No missing default fields were changed.', changed ? 'success' : 'info')
}

function confidenceColor(line: any) {
  const score = Number(line.confidenceScore || 0)
  if (score >= 75) return 'success'
  if (score >= 55) return 'warning'
  return 'error'
}

async function generateBarcode(line: any) {
  if (!selectedBatch.value?.id) {
    feedback.notify('Save draft first', 'Save the supplier invoice draft before generating the final YYMM barcode.', 'warning')
    return
  }
  line.barcodeFinal = ''
  await generateMissingBarcodes()
}

async function generateMissingBarcodes() {
  if (!selectedBatch.value?.id) return
  generatingBarcodes.value = true
  try {
    const saved = await saveDraft()
    if (!saved) return
    const draft = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/generate-missing-barcodes`, { onlyMissing: true })
    selectedBatch.value = normalizeDraft(draft)
    feedback.notify('Barcodes generated', 'Missing supplier invoice barcodes were generated in YYMM + import block + item sequence format, for example 26061001.', 'success')
  } catch (error) {
    feedback.failed('Could not generate missing barcodes', error)
  } finally {
    generatingBarcodes.value = false
  }
}

function applyTaxRate(line: any) {
  const tax = lookup.value.taxes?.find((item: any) => item.id === line.taxId)
  if (tax) line.taxRate = Number(tax.rate || tax.compositeRate || 0)
}

function lineKey(line: any, index: number) {
  return line.id || `new-${index}`
}

async function searchProductForLine(line: any, index: number) {
  const key = lineKey(line, index)
  const q = (productSearches.value[key] || line.barcodeFinal || line.productNameFinal || '').trim()
  if (!q) {
    feedback.notify('Search text missing', 'Enter barcode/product text before matching.', 'warning')
    return
  }
  matchingLine.value = key
  try {
    const params = new URLSearchParams({ query: q, take: '8' })
    if (selectedStoreId.value) params.append('storeId', selectedStoreId.value)
    productMatches.value[key] = await api.get<any[]>(`purchase-import/product-matches?${params.toString()}`)
    if (!productMatches.value[key]?.length) feedback.notify('No product found', 'Try another barcode/name, or keep it as a new product draft.', 'warning')
  } catch (error) {
    feedback.failed('Could not search product matches', error)
  } finally {
    matchingLine.value = null
  }
}

async function applyProductMatch(line: any, index: number, product: any) {
  const key = lineKey(line, index)
  try {
    if (selectedBatch.value?.id && line.id) {
      const draft = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/lines/${line.id}/match-product`, { productId: product.productId })
      selectedBatch.value = normalizeDraft(draft)
      productMatches.value[key] = []
      feedback.notify('Product matched', product.name, 'success')
      return
    }

    line.productId = product.productId
    line.productNameFinal = product.name
    line.barcodeFinal = product.barcode
    line.hsnCode = product.hsnCode || line.hsnCode
    line.unit = Number(product.unit ?? line.unit ?? 2)
    line.mrp = Number(line.mrp || product.mrp || 0)
    line.taxRate = Number(product.taxRate || line.taxRate || 0)
    line.taxId = product.taxId || line.taxId || NONE
    line.productCategoryId = product.productCategoryId || line.productCategoryId || NONE
    line.productSubCategoryId = product.productSubCategoryId || line.productSubCategoryId || NONE
    line.productType = Number(product.productType ?? line.productType ?? 0)
    line.productGroup = Number(product.productGroup ?? line.productGroup ?? 0)
    line.matchStatus = 'MatchedExistingProduct'
    line.reviewRequired = false
    productMatches.value[key] = []
  } catch (error) {
    feedback.failed('Could not apply product match', error)
  }
}

async function overrideDuplicate() {
  if (!selectedBatch.value?.id) return
  if (!duplicateOverrideReason.value.trim()) {
    feedback.notify('Reason required', 'Enter why this duplicate warning is safe to override.', 'warning')
    return
  }
  overridingDuplicate.value = true
  try {
    const draft = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/override-duplicate`, { reason: duplicateOverrideReason.value.trim() })
    selectedBatch.value = normalizeDraft(draft)
    feedback.notify('Duplicate override saved', 'This draft can be posted if no other warning remains.', 'success')
  } catch (error) {
    feedback.failed('Could not override duplicate warning', error)
  } finally {
    overridingDuplicate.value = false
  }
}

async function recheckDuplicate() {
  if (!selectedBatch.value?.id) return
  recheckingDuplicate.value = true
  try {
    const draft = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/recheck-duplicate`, {})
    selectedBatch.value = normalizeDraft(draft)
    feedback.notify('Duplicate check refreshed', draft.duplicatePurchaseInvoiceId ? 'Possible duplicate still exists.' : 'No duplicate supplier invoice found for current vendor/invoice number.', draft.duplicatePurchaseInvoiceId ? 'warning' : 'success')
  } catch (error) {
    feedback.failed('Could not refresh duplicate check', error)
  } finally {
    recheckingDuplicate.value = false
  }
}

async function distributeHeaderDiscount() {
  if (!selectedBatch.value?.id) return
  const amount = Number(selectedBatch.value.discountAmount || 0)
  if (amount <= 0) {
    feedback.notify('Discount missing', 'Enter supplier header discount amount before distributing.', 'warning')
    return
  }
  distributingDiscount.value = true
  try {
    const draft = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/distribute-discount`, { discountAmount: amount })
    selectedBatch.value = normalizeDraft(draft)
    await loadImportFiles(selectedBatch.value.id)
    feedback.notify('Discount distributed', 'Header discount was distributed into item line discounts for posting.', 'success')
  } catch (error) {
    feedback.failed('Could not distribute header discount', error)
  } finally {
    distributingDiscount.value = false
  }
}

async function reparseCurrentDraftText() {
  if (!selectedBatch.value?.id) return
  if (!reparseText.value.trim()) {
    feedback.notify('Text missing', 'Paste supplier invoice OCR/text before reparsing.', 'warning')
    return
  }
  reparsing.value = true
  try {
    const draft = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/reparse-text`, { rawText: reparseText.value.trim(), replaceLines: replaceLinesOnReparse.value })
    selectedBatch.value = normalizeDraft(draft)
    await loadImportFiles(selectedBatch.value.id)
    reparseText.value = ''
    feedback.notify('Draft reparsed', replaceLinesOnReparse.value ? 'Detected lines replaced the existing draft lines.' : 'Newly detected lines were added for review.', 'success')
  } catch (error) {
    feedback.failed('Could not reparse supplier invoice text', error)
  } finally {
    reparsing.value = false
  }
}

async function saveDraft() {
  if (!selectedBatch.value) return
  saving.value = true
  try {
    const payload = {
      vendorId: nullableSelect(selectedBatch.value.vendorId),
      vendorNameFinal: selectedBatch.value.vendorNameFinal,
      vendorGstinFinal: selectedBatch.value.vendorGstinFinal,
      vendorMobileNumber: selectedBatch.value.vendorMobileNumber,
      vendorAddress: selectedBatch.value.vendorAddress,
      supplierInvoiceNumber: selectedBatch.value.supplierInvoiceNumber,
      supplierInvoiceDate: selectedBatch.value.supplierInvoiceDate || null,
      dueDate: selectedBatch.value.dueDate || null,
      freightAmount: Number(selectedBatch.value.freightAmount || 0),
      discountAmount: Number(selectedBatch.value.discountAmount || 0),
      roundOff: Number(selectedBatch.value.roundOff || 0),
      billAmount: Number(selectedBatch.value.billAmount || payableTotal.value || 0),
      paidAmount: Number(selectedBatch.value.paidAmount || 0),
      paymentMode: Number(selectedBatch.value.paymentMode || 0),
      bankAccountId: nullableSelect(selectedBatch.value.bankAccountId),
      importQaNotes: selectedBatch.value.importQaNotes || null,
      lines: selectedBatch.value.lines.map((line: any, index: number) => ({
        id: line.id || null,
        lineNumber: index + 1,
        productId: line.productId || null,
        productNameFinal: line.productNameFinal || line.productNameRaw || '',
        barcodeFinal: line.barcodeFinal || line.barcodeRaw || '',
        hsnCode: line.hsnCode || '',
        unit: Number(line.unit ?? 2),
        quantity: Number(line.quantity || 0),
        mrp: Number(line.mrp || 0),
        costPrice: Number(line.costPrice || 0),
        unitDiscount: Number(line.unitDiscount || 0),
        lineDiscount: Number(line.lineDiscount || 0),
        taxRate: Number(line.taxRate || 0),
        gstPriceMode: line.gstPriceMode || 'Inclusive',
        taxId: nullableSelect(line.taxId),
        productCategoryId: nullableSelect(line.productCategoryId),
        productSubCategoryId: nullableSelect(line.productSubCategoryId),
        productType: Number(line.productType ?? 0),
        productGroup: Number(line.productGroup ?? 0),
        ignored: Boolean(line.ignored)
      }))
    }
    const draft = await api.update<any>('purchase-import/batches', selectedBatch.value.id, payload)
    selectedBatch.value = normalizeDraft(draft)
    await loadImportFiles(selectedBatch.value.id)
    await refreshListOnly()
    feedback.notify('Draft saved', draft.canPost ? 'Draft is ready to post.' : 'Review warnings before posting.', draft.canPost ? 'success' : 'warning')
    return true
  } catch (error) {
    feedback.failed('Could not save import draft', error)
    return false
  } finally {
    saving.value = false
  }
}

async function postDraft() {
  if (!selectedBatch.value) return
  posting.value = true
  try {
    const saved = await saveDraft()
    if (!saved) return
    const result = await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/post`, {})
    lastPostedResult.value = result
    feedback.notify('Purchase inward posted', `${result.inwardNumber || result.invoiceNumber} created and supplier invoice proof linked. Use Print tags to label new stock.`, 'success')
    await openDraft(selectedBatch.value.id)
    await refreshListOnly()
  } catch (error) {
    feedback.failed('Could not post supplier invoice draft', error)
  } finally {
    posting.value = false
  }
}


function openPriceTagsForSelectedImport() {
  const batch = selectedBatch.value
  const postedPurchaseInvoiceId = batch?.postedPurchaseInvoiceId || lastPostedResult.value?.purchaseInvoiceId
  const batchId = batch?.id || lastPostedResult.value?.batchId
  const query = new URLSearchParams()
  if (batchId) query.set('importBatchId', String(batchId))
  if (postedPurchaseInvoiceId) query.set('purchaseInwardId', String(postedPurchaseInvoiceId))
  query.set('size', '50x30')
  navigateTo(`/price-tags?${query.toString()}`)
}

async function rejectDraft() {
  if (!selectedBatch.value) return
  try {
    await api.create<any>(`purchase-import/batches/${selectedBatch.value.id}/reject`, { reason: 'Rejected from import review page.' })
    feedback.notify('Draft rejected', 'Supplier invoice proof is kept, but draft will not be posted.', 'success')
    await openDraft(selectedBatch.value.id)
    await refreshListOnly()
  } catch (error) {
    feedback.failed('Could not reject draft', error)
  }
}

watch(() => selectedBatch.value?.vendorId, applyVendor)
onMounted(async () => { auth.restore(); await refresh() })
onBeforeUnmount(() => { if (proofUrl.value) URL.revokeObjectURL(proofUrl.value) })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Supplier Invoice Import" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Supplier Invoice Import"
        description="Upload supplier invoice PDF/image, keep original proof, review draft lines, then post verified purchase inward with vendor/product/stock/GST update."
        icon="i-lucide-file-scan"
        primary-label="Upload invoice"
        primary-icon="i-lucide-upload"
        @primary="uploadSupplierInvoice"
      >
        <template #actions>
          <UButton color="neutral" variant="subtle" icon="i-lucide-arrow-left" label="Back to Purchase" to="/purchase" />
          <UButton color="neutral" variant="subtle" icon="i-lucide-clipboard-check" label="Acceptance" to="/purchase/import-acceptance" />
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">{{ loading ? 'Loading' : 'Ready' }}</UBadge>
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-inbox" color="primary" variant="subtle" /><div><p>Drafts</p><strong>{{ batches.length }}</strong><span>Recent imports</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list-checks" color="warning" variant="subtle" /><div><p>Review</p><strong>{{ draftWarnings.length }}</strong><span>Open warnings</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-package-plus" color="success" variant="subtle" /><div><p>Lines</p><strong>{{ draftLines.length }}</strong><span>Draft products</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-indian-rupee" color="neutral" variant="subtle" /><div><p>Total</p><strong>{{ money(Number(selectedBatch?.billAmount || payableTotal || 0)) }}</strong><span>Bill amount</span></div></div></UCard>
      </div>

      <div class="grid gap-4 lg:grid-cols-[360px_1fr]">
        <div class="space-y-4">
          <UCard class="setup-card">
            <template #header><h2 class="section-title">Upload supplier bill</h2></template>
            <div class="space-y-3">
              <input type="file" accept=".pdf,.png,.jpg,.jpeg,.webp,.txt,application/pdf,image/*" @change="onFileSelected" />
              <UTextarea v-model="rawText" :rows="4" placeholder="Optional: paste extracted invoice text here. Useful until full OCR provider is configured." />
              <UButton block color="primary" icon="i-lucide-upload" :loading="uploading" label="Create Import Draft" @click="uploadSupplierInvoice" />
              <UAlert color="info" variant="soft" icon="i-lucide-info" title="Safe import rule" description="Upload creates draft only. Stock/accounting updates happen only after you verify and post." />
              <div v-if="selectedBatch && selectedBatch.status !== 'Posted'" class="rounded-xl border border-dashed border-gray-300 p-3 dark:border-gray-700">
                <p class="mb-2 text-sm font-medium">Paste better OCR/text into current draft</p>
                <UTextarea v-model="reparseText" :rows="4" placeholder="Paste extracted text from supplier invoice. Append or replace detected lines after review." />
                <div class="mt-2 flex items-center justify-between gap-2 rounded-lg bg-gray-50 px-2 py-1 dark:bg-gray-900">
                  <span class="text-xs text-gray-500">Replace existing lines instead of appending</span>
                  <USwitch v-model="replaceLinesOnReparse" />
                </div>
                <UButton class="mt-2" block color="neutral" variant="subtle" icon="i-lucide-refresh-cw" :loading="reparsing" label="Reparse Current Draft" @click="reparseCurrentDraftText" />
              </div>
            </div>
          </UCard>

          <UCard class="setup-card">
            <template #header>
              <div class="flex items-center justify-between gap-2">
                <h2 class="section-title">Recent import drafts</h2>
                <UButton size="xs" color="error" variant="subtle" icon="i-lucide-trash-2" :loading="cleanupHistoryLoading" label="Clean failed" @click="cleanupFailedHistory" />
              </div>
            </template>
            <div class="space-y-2">
              <button
                v-for="batch in batches"
                :key="batch.id"
                type="button"
                class="w-full rounded-xl border p-3 text-left transition hover:bg-gray-50 dark:hover:bg-gray-900"
                :class="selectedBatch?.id === batch.id ? 'border-primary-400 bg-primary-50/70 dark:bg-primary-950/30' : 'border-gray-200 dark:border-gray-800'"
                @click="openDraft(batch.id)"
              >
                <div class="flex items-center justify-between gap-2">
                  <strong class="truncate">{{ batch.vendorName || batch.sourceFileName }}</strong>
                  <div class="flex items-center gap-1">
                    <UBadge size="xs" :color="batch.status === 'Posted' ? 'success' : batch.status === 'Rejected' ? 'neutral' : 'warning'" variant="subtle">{{ batch.status }}</UBadge>
                    <UButton
                      v-if="batch.status !== 'Posted'"
                      size="xs"
                      color="error"
                      variant="ghost"
                      icon="i-lucide-trash-2"
                      :loading="deletingDraftId === batch.id"
                      @click.stop="deleteImportDraft(batch)"
                    />
                  </div>
                </div>
                <p class="text-xs text-gray-500">{{ batch.supplierInvoiceNumber || 'No invoice no.' }} · {{ money(batch.billAmount || 0) }}</p>
              </button>
              <p v-if="!batches.length" class="text-sm text-gray-500">No supplier invoice import drafts yet.</p>
            </div>
          </UCard>


          <UCard class="setup-card">
            <template #header>
              <div class="flex items-center justify-between gap-2">
                <h2 class="section-title">Import storage</h2>
                <UButton size="xs" color="neutral" variant="subtle" icon="i-lucide-refresh-cw" :loading="loadingStorageSummary" label="Refresh" @click="refreshStorageSummary" />
              </div>
            </template>
            <div v-if="importStorageSummary" class="space-y-3 text-sm">
              <div class="grid grid-cols-2 gap-2">
                <div class="rounded-lg bg-gray-50 p-2 dark:bg-gray-900"><p class="text-xs text-gray-500">Total files</p><strong>{{ importStorageSummary.totalFiles }}</strong></div>
                <div class="rounded-lg bg-gray-50 p-2 dark:bg-gray-900"><p class="text-xs text-gray-500">Storage</p><strong>{{ fileSize(importStorageSummary.totalFileBytes || 0) }}</strong></div>
                <div class="rounded-lg bg-gray-50 p-2 dark:bg-gray-900"><p class="text-xs text-gray-500">Posted proof</p><strong>{{ fileSize(importStorageSummary.postedProofBytes || 0) }}</strong></div>
                <div class="rounded-lg bg-gray-50 p-2 dark:bg-gray-900"><p class="text-xs text-gray-500">Unposted files</p><strong>{{ fileSize(importStorageSummary.unpostedFileBytes || 0) }}</strong></div>
              </div>
              <div class="space-y-1">
                <div v-for="row in importStorageSummary.statuses || []" :key="row.status" class="flex items-center justify-between rounded-lg border border-gray-100 px-2 py-1 dark:border-gray-800">
                  <span>{{ row.status }}</span>
                  <span class="text-xs text-gray-500">{{ row.batchCount }} draft · {{ row.lineCount }} lines · {{ fileSize(row.fileBytes || 0) }}</span>
                </div>
              </div>
              <UAlert color="info" variant="soft" icon="i-lucide-shield-check" title="Proof retention" description="Posted supplier invoice proofs are protected. Cleanup only removes unposted failed/rejected history unless you choose broader cleanup later." />
            </div>
            <p v-else class="text-sm text-gray-500">Storage summary will appear after refresh.</p>
          </UCard>
        </div>

        <div v-if="selectedBatch" class="space-y-4">
          <UCard class="setup-card">
            <template #header>
              <div class="flex flex-wrap items-center justify-between gap-2">
                <h2 class="section-title">Review draft</h2>
                <div class="flex flex-wrap gap-2">
                  <UBadge :color="selectedBatch.canPost ? 'success' : 'warning'" variant="subtle">{{ selectedBatch.status }}</UBadge>
                  <UBadge color="info" variant="subtle">{{ selectedBatch.ocrProvider || 'OCR' }}</UBadge>
                  <UBadge color="neutral" variant="subtle">{{ Number(selectedBatch.confidenceScore || 0).toFixed(0) }}% confidence</UBadge>
                  <UButton color="neutral" variant="subtle" icon="i-lucide-save" :loading="saving" label="Save Draft" @click="saveDraft" />
                  <UButton color="primary" icon="i-lucide-check-circle-2" :loading="posting" :disabled="selectedBatch.status === 'Posted' || selectedBatch.status === 'Rejected' || !postingChecklist.ready" label="Post Inward" @click="postDraft" />
                  <UButton v-if="selectedBatch.status === 'Posted' || selectedBatch.postedPurchaseInvoiceId" color="success" variant="subtle" icon="i-lucide-tags" label="Print tags" @click="openPriceTagsForSelectedImport" />
                  <UButton color="error" variant="subtle" icon="i-lucide-x-circle" :disabled="selectedBatch.status === 'Posted'" label="Reject" @click="rejectDraft" />
                </div>
              </div>
            </template>

            <UAlert
              v-if="selectedBatch.status === 'Posted' || selectedBatch.postedPurchaseInvoiceId"
              class="mb-4"
              color="success"
              variant="soft"
              icon="i-lucide-tags"
              title="Purchase inward posted"
              description="Use Print tags to load this purchase inward directly into the 50×30 / 50×25 price-tag printing page."
            />

            <div v-if="draftWarnings.length" class="mb-4 space-y-2">
              <UAlert v-for="warning in draftWarnings" :key="warning" color="warning" variant="soft" icon="i-lucide-alert-triangle" :description="warning" />
            </div>

            <div v-if="selectedBatch.duplicatePurchaseInvoiceId && !selectedBatch.duplicateOverrideReason" class="mb-4 rounded-2xl border border-amber-300 bg-amber-50 p-3 dark:border-amber-800 dark:bg-amber-950/30">
              <h3 class="font-semibold text-amber-900 dark:text-amber-100">Duplicate invoice warning</h3>
              <p class="text-sm text-amber-800 dark:text-amber-200">A purchase invoice with same supplier/invoice number may already exist. Admin/edit users can override only after checking proof.</p>
              <div class="mt-3 flex flex-col gap-2 sm:flex-row">
                <UTextarea v-model="duplicateOverrideReason" class="flex-1" :rows="2" placeholder="Reason, e.g. supplier reused invoice number but date/amount/proof is different" />
                <UButton color="neutral" variant="subtle" icon="i-lucide-refresh-cw" :loading="recheckingDuplicate" label="Recheck" @click="recheckDuplicate" />
                <UButton color="warning" icon="i-lucide-shield-check" :loading="overridingDuplicate" label="Override" @click="overrideDuplicate" />
              </div>
            </div>
            <UAlert v-else-if="selectedBatch.duplicateOverrideReason" class="mb-4" color="success" variant="soft" icon="i-lucide-shield-check" title="Duplicate override saved" :description="selectedBatch.duplicateOverrideReason" />
            <UAlert class="mb-4" color="info" variant="soft" icon="i-lucide-scan-text" title="Extraction status" :description="`${selectedBatch.ocrStatus || 'Pending'} via ${selectedBatch.ocrProvider || 'OCR'}. Parser: ${selectedBatch.parserTemplate || 'auto'}${selectedBatch.parserTemplateReason ? ' — ' + selectedBatch.parserTemplateReason : ''}. Use extracted text below when the parser needs another attempt.`" />

            <div class="mb-4 rounded-2xl border border-blue-100 bg-blue-50 p-3 dark:border-blue-900 dark:bg-blue-950/30">
              <div class="mb-2 flex items-center gap-2 text-sm font-semibold text-blue-900 dark:text-blue-100">
                <UIcon name="i-lucide-file-check-2" />
                <span>Import QA notes</span>
              </div>
              <UTextarea v-model="selectedBatch.importQaNotes" :rows="2" placeholder="Optional notes after checking parser/template accuracy for this supplier invoice" />
              <p class="mt-1 text-xs text-blue-700 dark:text-blue-200">Saved with draft and included in audit JSON/export for future invoice QA.</p>
            </div>

            <div class="mb-4 rounded-2xl border border-gray-200 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-950">
              <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
                <h3 class="text-sm font-semibold">Ready-to-post checklist</h3>
                <UBadge :color="postingChecklist.ready ? 'success' : 'warning'" variant="subtle">{{ postingChecklist.ready ? 'Ready' : `${postingChecklist.failed.length} issue(s)` }}</UBadge>
              </div>
              <div class="grid gap-2 text-xs md:grid-cols-2 xl:grid-cols-3">
                <div
                  v-for="check in postingChecklist.checks"
                  :key="check.key"
                  class="rounded-xl border px-3 py-2"
                  :class="check.passed ? 'border-green-200 bg-green-50 text-green-800 dark:border-green-900 dark:bg-green-950/30 dark:text-green-100' : 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-100'"
                >
                  <div class="flex items-center gap-2 font-medium">
                    <UIcon :name="check.passed ? 'i-lucide-check-circle-2' : 'i-lucide-alert-triangle'" />
                    <span>{{ check.label }}</span>
                  </div>
                  <p v-if="!check.passed" class="mt-1 opacity-80">{{ check.help }}</p>
                </div>
              </div>
            </div>

            <div class="grid gap-4 lg:grid-cols-2">
              <div class="space-y-3">
                <h3 class="font-semibold">Supplier invoice proof</h3>
                <div class="rounded-2xl border border-gray-200 bg-gray-50 p-2 dark:border-gray-800 dark:bg-gray-950">
                  <img v-if="proofUrl && proofIsImage" :src="proofUrl" class="max-h-[520px] w-full rounded-xl object-contain" alt="Supplier invoice proof" />
                  <iframe v-else-if="proofUrl" :src="proofUrl" class="h-[520px] w-full rounded-xl bg-white" />
                  <div v-else class="p-6 text-center text-sm text-gray-500">Proof preview unavailable. Original file is still stored on server.</div>
                </div>
                <div class="flex flex-wrap gap-2">
                  <UButton v-if="proofUrl" :href="proofUrl" target="_blank" color="neutral" variant="subtle" icon="i-lucide-external-link" label="Open proof" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-scan-text" :loading="loadingExtractedText" label="Load extracted text" @click="loadExtractedText" />
                  <UButton v-if="extractedText" color="primary" variant="soft" icon="i-lucide-copy-check" label="Use for reparse" @click="useExtractedTextForReparse" />
                </div>
                <UTextarea v-if="extractedText" v-model="extractedText" :rows="7" readonly class="font-mono text-xs" />
                <div v-if="importFiles.length" class="rounded-xl border border-gray-200 p-3 dark:border-gray-800">
                  <p class="mb-2 text-sm font-semibold">Stored proof / audit files</p>
                  <div class="space-y-2">
                    <button
                      v-for="file in importFiles"
                      :key="file.id"
                      type="button"
                      class="flex w-full items-center justify-between gap-3 rounded-lg px-2 py-1 text-left text-sm hover:bg-gray-50 dark:hover:bg-gray-900"
                      @click="openImportFile(file)"
                    >
                      <span class="truncate"><strong>{{ file.fileKind }}</strong> · {{ file.originalFileName }}</span>
                      <span class="shrink-0 text-xs text-gray-500">{{ fileSize(file.fileSizeBytes) }}</span>
                    </button>
                  </div>
                </div>
              </div>

              <div class="space-y-3">
                <h3 class="font-semibold">Supplier and bill details</h3>
                <div class="form-two-column">
                  <UFormField label="Existing vendor"><USelect v-model="selectedBatch.vendorId" :items="vendorOptions" /></UFormField>
                  <UFormField label="Vendor name" required><UInput v-model="selectedBatch.vendorNameFinal" /></UFormField>
                </div>
                <div class="form-two-column">
                  <UFormField label="GSTIN"><UInput v-model="selectedBatch.vendorGstinFinal" /></UFormField>
                  <UFormField label="Mobile"><UInput v-model="selectedBatch.vendorMobileNumber" /></UFormField>
                </div>
                <UFormField label="Address"><UTextarea v-model="selectedBatch.vendorAddress" :rows="2" /></UFormField>
                <div class="form-two-column">
                  <UFormField label="Supplier invoice no." required><UInput v-model="selectedBatch.supplierInvoiceNumber" /></UFormField>
                  <UFormField label="Invoice date"><UInput v-model="selectedBatch.supplierInvoiceDate" type="date" /></UFormField>
                </div>
                <div class="form-two-column">
                  <UFormField label="Due date"><UInput v-model="selectedBatch.dueDate" type="date" /></UFormField>
                  <UFormField label="Freight"><UInput v-model="selectedBatch.freightAmount" type="number" min="0" /></UFormField>
                </div>
                <div class="form-two-column">
                  <UFormField label="Header discount"><UInput v-model="selectedBatch.discountAmount" type="number" min="0" /></UFormField>
                  <UFormField label="Discount action">
                    <UButton block color="neutral" variant="subtle" icon="i-lucide-scissors" :loading="distributingDiscount" :disabled="Number(selectedBatch.discountAmount || 0) <= 0 || selectedBatch.status === 'Posted'" label="Distribute to lines" @click="distributeHeaderDiscount" />
                  </UFormField>
                </div>
                <div class="form-two-column">
                  <UFormField label="Paid amount"><UInput v-model="selectedBatch.paidAmount" type="number" min="0" /></UFormField>
                  <UFormField label="Payment mode"><USelect v-model="selectedBatch.paymentMode" :items="paymentModeOptions" /></UFormField>
                </div>
                <UFormField label="Bank account"><USelect v-model="selectedBatch.bankAccountId" :items="bankAccountOptions" /></UFormField>
                <div class="form-two-column">
                  <UFormField label="Round off"><UInput v-model="selectedBatch.roundOff" type="number" /></UFormField>
                  <UFormField label="Bill amount"><UInput v-model="selectedBatch.billAmount" type="number" min="0" /></UFormField>
                </div>
              </div>
            </div>
          </UCard>

          <UCard class="setup-card">
            <template #header>
              <div class="flex items-center justify-between gap-2">
                <h2 class="section-title">Product draft lines</h2>
                <div class="flex flex-wrap gap-2">
                  <UButton color="neutral" variant="subtle" icon="i-lucide-barcode" :loading="generatingBarcodes" label="Generate missing barcodes" @click="generateMissingBarcodes" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-search-check" :loading="bulkFindingSimilar" label="Find similar all" @click="findSimilarForAllNewLines" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-copy-check" label="Copy defaults" @click="copyDefaultsFromFirstReviewedLine" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-indian-rupee" label="Fill MRP" @click="fillMissingMrpFromCost" />
                  <UButton color="warning" variant="subtle" icon="i-lucide-filter-x" label="Ignore low-confidence" @click="ignoreLowConfidenceLines" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-undo-2" label="Restore ignored" @click="restoreIgnoredLines" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Line" @click="addManualLine" />
                </div>
              </div>
            </template>

            <div class="space-y-3">
              <div class="grid gap-2 md:grid-cols-4">
                <UAlert color="info" variant="soft" icon="i-lucide-list-checks" :title="`${reviewStats.active} active / ${reviewStats.ignored} ignored`" description="Active rows will be posted. Ignored rows are kept for learning only." />
                <UAlert color="primary" variant="soft" icon="i-lucide-package-plus" :title="`${reviewStats.newProducts} new / ${reviewStats.matchedProducts} matched`" description="Map existing products, or let new draft products be created during posting." />
                <UAlert :color="reviewStats.missingBarcode || reviewStats.missingTax ? 'warning' : 'success'" variant="soft" icon="i-lucide-shield-check" :title="`${reviewStats.missingBarcode} barcode / ${reviewStats.missingTax} GST missing`" description="Use generate barcode and copy defaults before posting." />
                <UAlert :color="Math.abs(reviewStats.billDifference) > 1 || reviewStats.duplicateBarcodes ? 'warning' : 'success'" variant="soft" icon="i-lucide-calculator" :title="`Diff ${money(reviewStats.billDifference)}`" :description="reviewStats.duplicateBarcodes ? `${reviewStats.duplicateBarcodes} duplicate barcode group(s)` : `Calc ${money(reviewStats.calculatedTotal)}`" />
              </div>
              <div class="rounded-2xl border border-gray-200 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-950">
                <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
                  <p class="text-sm font-semibold">Posting reconciliation</p>
                  <UBadge :color="Math.abs(reconciliation.billDifference) <= 1 && Math.abs(reconciliation.headerDiscountDifference) <= 1 ? 'success' : 'warning'" variant="subtle">{{ Math.abs(reconciliation.billDifference) <= 1 ? 'Bill matched' : 'Review total' }}</UBadge>
                </div>
                <div class="grid gap-2 text-xs md:grid-cols-4">
                  <div><span class="text-gray-500">Gross</span><strong class="block">{{ money(reconciliation.gross) }}</strong></div>
                  <div><span class="text-gray-500">Line discount</span><strong class="block">{{ money(reconciliation.discount) }}</strong></div>
                  <div><span class="text-gray-500">Taxable</span><strong class="block">{{ money(reconciliation.taxable) }}</strong></div>
                  <div><span class="text-gray-500">Tax</span><strong class="block">{{ money(reconciliation.tax) }}</strong></div>
                  <div><span class="text-gray-500">Line total</span><strong class="block">{{ money(reconciliation.lineTotal) }}</strong></div>
                  <div><span class="text-gray-500">Freight + round off</span><strong class="block">{{ money(reconciliation.freight + reconciliation.roundOff) }}</strong></div>
                  <div><span class="text-gray-500">Calculated grand</span><strong class="block">{{ money(reconciliation.calculatedGrand) }}</strong></div>
                  <div><span class="text-gray-500">Scanned grand diff</span><strong class="block" :class="Math.abs(reconciliation.billDifference) > 1 ? 'text-amber-600' : 'text-green-600'">{{ money(reconciliation.billDifference) }}</strong></div>
                </div>
                <p v-if="Math.abs(reconciliation.headerDiscountDifference) > 1" class="mt-2 text-xs text-amber-600">Header discount difference: {{ money(reconciliation.headerDiscountDifference) }}. Use Distribute to lines or correct line discounts before posting.</p>
              </div>
              <div v-for="(line, index) in selectedBatch.lines" :key="line.id || index" class="rounded-2xl border border-gray-200 p-3 dark:border-gray-800">
                <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
                  <div class="flex items-center gap-2">
                    <UBadge color="neutral" variant="subtle">#{{ index + 1 }}</UBadge>
                    <UBadge :color="line.reviewRequired ? 'warning' : 'success'" variant="subtle">{{ line.matchStatus || 'Draft' }}</UBadge>
                    <UBadge :color="confidenceColor(line)" variant="subtle">{{ Number(line.confidenceScore || 0).toFixed(0) }}% OCR</UBadge>
                  </div>
                  <div class="flex flex-wrap gap-2">
                    <UButton color="neutral" variant="subtle" size="xs" icon="i-lucide-split" label="Split qty" @click="openSplitLine(line, index)" />
                    <UButton color="neutral" variant="subtle" size="xs" icon="i-lucide-search-check" label="Find similar" @click="findSimilarForLine(line, index)" />
                    <UButton color="neutral" variant="subtle" size="xs" icon="i-lucide-barcode" label="Generate barcode" @click="generateBarcode(line)" />
                    <USwitch v-model="line.ignored" label="Ignore" />
                    <UButton color="error" variant="ghost" size="xs" icon="i-lucide-trash-2" @click="removeLine(index)" />
                  </div>
                </div>
                <UAlert
                  v-if="line.reviewMessage"
                  class="mb-3"
                  :color="line.reviewRequired ? 'warning' : 'info'"
                  variant="soft"
                  icon="i-lucide-message-square-warning"
                  :description="line.reviewMessage"
                />
                <div class="form-four-column">
                  <UFormField label="Product name"><UInput v-model="line.productNameFinal" /></UFormField>
                  <UFormField label="Barcode"><UInput v-model="line.barcodeFinal" /></UFormField>
                  <UFormField label="HSN"><UInput v-model="line.hsnCode" /></UFormField>
                  <UFormField label="GST"><USelect v-model="line.taxId" :items="taxOptions" @update:model-value="applyTaxRate(line)" /></UFormField>
                </div>
                <div class="mt-3 rounded-xl border border-gray-100 bg-gray-50 p-2 dark:border-gray-800 dark:bg-gray-950">
                  <div class="flex flex-col gap-2 md:flex-row">
                    <UInput v-model="productSearches[lineKey(line, index)]" class="flex-1" placeholder="Search existing product by barcode/name/HSN" />
                    <UButton color="neutral" variant="subtle" icon="i-lucide-search" :loading="matchingLine === lineKey(line, index)" label="Find product" @click="searchProductForLine(line, index)" />
                  </div>
                  <div v-if="productMatches[lineKey(line, index)]?.length" class="mt-2 flex flex-wrap gap-2">
                    <UButton
                      v-for="product in productMatches[lineKey(line, index)]"
                      :key="product.productId"
                      size="xs"
                      color="primary"
                      variant="soft"
                      icon="i-lucide-link"
                      :label="product.matchLabel"
                      @click="applyProductMatch(line, index, product)"
                    />
                  </div>
                </div>
                <div class="form-four-column mt-3">
                  <UFormField label="Qty"><UInput v-model="line.quantity" type="number" min="0" /></UFormField>
                  <UFormField label="Cost"><UInput v-model="line.costPrice" type="number" min="0" /></UFormField>
                  <UFormField label="GST mode"><USelect v-model="line.gstPriceMode" :items="gstPriceModeOptions" /></UFormField>
                  <UFormField label="MRP"><UInput v-model="line.mrp" type="number" min="0" /></UFormField>
                </div>
                <div class="form-four-column mt-3">
                  <UFormField label="Unit discount"><UInput v-model="line.unitDiscount" type="number" min="0" /></UFormField>
                  <UFormField label="Line discount"><UInput v-model="line.lineDiscount" type="number" min="0" /></UFormField>
                  <UFormField label="Estimated tax"><UInput :model-value="money(estimatedTax(line))" readonly /></UFormField>
                  <UFormField label="Line total"><UInput :model-value="money(lineTotal(line))" readonly /></UFormField>
                </div>
                <div class="form-four-column mt-3">
                  <UFormField label="Unit"><USelect v-model="line.unit" :items="unitOptions" /></UFormField>
                  <UFormField label="Category"><USelect v-model="line.productCategoryId" :items="categoryOptions" /></UFormField>
                  <UFormField label="Sub category"><USelect v-model="line.productSubCategoryId" :items="subCategoryOptions" /></UFormField>
                  <UFormField label="Product id"><UInput :model-value="line.productId ? 'Existing product' : 'New product draft'" readonly /></UFormField>
                </div>
                <div class="form-two-column mt-3">
                  <UFormField label="Product type"><USelect v-model="line.productType" :items="productTypeOptions" /></UFormField>
                  <UFormField label="Product group"><USelect v-model="line.productGroup" :items="productGroupOptions" /></UFormField>
                </div>
              </div>
            </div>
          </UCard>
        </div>

        <UCard v-else class="setup-card">
          <div class="p-10 text-center text-gray-500">
            <UIcon name="i-lucide-file-scan" class="mx-auto mb-3 h-10 w-10" />
            <p>Upload a supplier invoice or select a recent draft to start review.</p>
          </div>
        </UCard>
      </div>
    </section>
      <UModal v-model:open="splitDialogOpen" title="Split invoice line by size/color" :ui="{ content: 'max-w-xl' }">
        <template #body>
          <div class="space-y-3">
            <UAlert
              color="info"
              variant="soft"
              icon="i-lucide-info"
              title="Create size-wise product draft lines"
              description="Enter one size/color label per piece. The selected invoice line will be replaced by quantity-1 lines. Each split line can be matched with an existing product or posted as a new product with generated barcode."
            />
            <div class="flex flex-wrap gap-2">
              <UButton
                v-for="template in splitTemplates"
                :key="template.value"
                size="xs"
                :color="splitTemplate === template.value ? 'primary' : 'neutral'"
                variant="subtle"
                :label="template.label"
                @click="applySplitTemplate(template.value)"
              />
            </div>
            <UTextarea v-model="splitSizeText" :rows="8" :placeholder="'Example:\n38=2\n40=2\n42=1\nBlue-M=3\nBlue-L=3'" />
            <p class="text-xs text-gray-500">Use one label per piece, or size=count lines like 38=2. Expanded count must equal the scanned line quantity.</p>
          </div>
        </template>
        <template #footer>
          <div class="flex w-full justify-end gap-2">
            <UButton color="neutral" variant="subtle" label="Cancel" @click="splitDialogOpen = false" />
            <UButton color="primary" icon="i-lucide-split" :loading="splittingLine" label="Create split lines" @click="applySplitBySize" />
          </div>
        </template>
      </UModal>

  </AppShell>
</template>

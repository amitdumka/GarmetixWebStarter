<script setup lang="ts">
type GstForm = 'gstr1' | 'gstr3b'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const config = useRuntimeConfig()

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const activeForm = ref<GstForm>('gstr1')
const preview = ref<any | null>(null)
const schemaReview = ref<any | null>(null)
const schemaReviewLoading = ref(false)
const loading = ref(false)
const today = new Date()
const currentPeriod = `${String(today.getMonth() + 1).padStart(2, '0')}${today.getFullYear()}`

const header = reactive({
  gstin: '',
  returnPeriod: currentPeriod,
  grossTurnover: 0,
  currentTurnover: 0,
  legalName: '',
  tradeName: ''
})

const b2bRows = ref<any[]>([
  newB2BRow()
])
const b2cRows = ref<any[]>([
  newB2CRow()
])
const hsnRows = ref<any[]>([
  newHsnRow()
])
const documentRows = ref<any[]>([
  newDocumentRow()
])
const nilRows = ref<any[]>([
  newNilRow()
])

const supplies = reactive({
  outwardTaxableValue: 0,
  outwardIntegratedTax: 0,
  outwardCentralTax: 0,
  outwardStateTax: 0,
  outwardCess: 0,
  zeroRatedTaxableValue: 0,
  zeroRatedIntegratedTax: 0,
  nilExemptTaxableValue: 0,
  nonGstTaxableValue: 0,
  reverseChargeTaxableValue: 0,
  reverseChargeIntegratedTax: 0,
  reverseChargeCentralTax: 0,
  reverseChargeStateTax: 0,
  reverseChargeCess: 0
})

const interStateSupplies = reactive({
  unregisteredTaxableValue: 0,
  unregisteredIntegratedTax: 0,
  compositionTaxableValue: 0,
  compositionIntegratedTax: 0,
  uinTaxableValue: 0,
  uinIntegratedTax: 0
})

const itc = reactive({
  importGoodsIntegratedTax: 0,
  importGoodsCess: 0,
  importServicesIntegratedTax: 0,
  reverseChargeIntegratedTax: 0,
  reverseChargeCentralTax: 0,
  reverseChargeStateTax: 0,
  reverseChargeCess: 0,
  isdIntegratedTax: 0,
  isdCentralTax: 0,
  isdStateTax: 0,
  isdCess: 0,
  otherIntegratedTax: 0,
  otherCentralTax: 0,
  otherStateTax: 0,
  otherCess: 0,
  reversalRule42IntegratedTax: 0,
  reversalRule42CentralTax: 0,
  reversalRule42StateTax: 0,
  reversalRule42Cess: 0,
  reversalOtherIntegratedTax: 0,
  reversalOtherCentralTax: 0,
  reversalOtherStateTax: 0,
  reversalOtherCess: 0,
  ineligibleIntegratedTax: 0,
  ineligibleCentralTax: 0,
  ineligibleStateTax: 0,
  ineligibleCess: 0
})

const inwardSupplies = reactive({
  compositionTaxableValue: 0,
  compositionIntegratedTax: 0,
  compositionCentralTax: 0,
  compositionStateTax: 0,
  nilRatedTaxableValue: 0,
  nilRatedIntegratedTax: 0,
  nilRatedCentralTax: 0,
  nilRatedStateTax: 0,
  nonGstTaxableValue: 0
})

const interestLateFee = reactive({
  integratedTaxInterest: 0,
  centralTaxInterest: 0,
  stateTaxInterest: 0,
  cessInterest: 0,
  centralLateFee: 0,
  stateLateFee: 0
})

const activeLabel = computed(() => activeForm.value === 'gstr1' ? 'GSTR-1' : 'GSTR-3B')
const previewIssues = computed(() => preview.value?.issues || [])
const schemaWarnings = computed(() => schemaReview.value?.productionWarnings || [])
const activeSchemaItems = computed(() => activeForm.value === 'gstr1' ? schemaReview.value?.gstr1 || [] : schemaReview.value?.gstr3B || [])

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows

    const company = companyRows[0]
    if (company && !header.legalName) {
      header.legalName = company.name || company.companyName || ''
      header.tradeName = company.tradeName || company.shortName || header.legalName
      header.gstin = company.gstNumber || company.gstin || company.gstIn || ''
    }

    await loadSchemaReview()
  } catch (error) {
    feedback.failed('GST Returns refresh failed', error)
  }
}

function newB2BRow() {
  return {
    recipientGstin: '',
    recipientName: '',
    invoiceNumber: '',
    invoiceDate: today.toISOString().slice(0, 10),
    placeOfSupply: '',
    reverseCharge: 'N',
    invoiceType: 'R',
    invoiceValue: 0,
    rate: 5,
    taxableValue: 0,
    integratedTax: 0,
    centralTax: 0,
    stateTax: 0,
    cess: 0,
    eCommerceGstin: ''
  }
}

function newB2CRow() {
  return {
    type: 'INTRA',
    placeOfSupply: '',
    eCommerceGstin: '',
    rate: 5,
    taxableValue: 0,
    integratedTax: 0,
    centralTax: 0,
    stateTax: 0,
    cess: 0
  }
}

function newHsnRow() {
  return {
    serialNumber: 1,
    hsnCode: '',
    description: '',
    uqc: 'PCS',
    totalQuantity: 0,
    totalValue: 0,
    taxableValue: 0,
    integratedTax: 0,
    centralTax: 0,
    stateTax: 0,
    cess: 0
  }
}

function newDocumentRow() {
  return {
    serialNumber: 1,
    natureOfDocument: 'Invoices for outward supply',
    fromSerialNumber: '',
    toSerialNumber: '',
    totalNumber: 0,
    cancelledNumber: 0
  }
}

function newNilRow() {
  return {
    description: 'Inter-state supplies to registered persons',
    nilRated: 0,
    exempted: 0,
    nonGst: 0
  }
}

function addRow(rows: any[], factory: () => any) {
  const row = factory()
  if ('serialNumber' in row) {
    row.serialNumber = rows.length + 1
  }
  rows.push(row)
}

function removeRow(rows: any[], index: number) {
  if (rows.length <= 1) {
    Object.assign(rows[0], rows[0].recipientGstin !== undefined ? newB2BRow() : rows[0].type !== undefined ? newB2CRow() : rows[0].hsnCode !== undefined ? newHsnRow() : rows[0].natureOfDocument !== undefined ? newDocumentRow() : newNilRow())
    return
  }
  rows.splice(index, 1)
}

function buildGstr1Payload() {
  return {
    header: { ...header },
    b2BInvoices: b2bRows.value.map(normalizeDateRow),
    b2CSummaries: b2cRows.value,
    hsnSummaries: hsnRows.value,
    documentsIssued: documentRows.value,
    nilRatedSupplies: nilRows.value
  }
}

function buildGstr3BPayload() {
  return {
    header: { ...header },
    supplies,
    interStateSupplies,
    itc,
    inwardSupplies,
    interestLateFee
  }
}

function normalizeDateRow(row: any) {
  return {
    ...row,
    invoiceDate: row.invoiceDate ? new Date(`${row.invoiceDate}T00:00:00`).toISOString() : new Date().toISOString()
  }
}

async function loadSchemaReview() {
  schemaReviewLoading.value = true
  try {
    schemaReview.value = await api.get<any>('gst-returns/schema-review')
  } catch (error) {
    feedback.failed('GST schema review failed', error)
  } finally {
    schemaReviewLoading.value = false
  }
}

async function downloadSchemaReview() {
  schemaReviewLoading.value = true
  try {
    await downloadGetFile('gst-returns/schema-review/excel', 'Garmetix-GST-Schema-Review.xlsx')
    feedback.notify('GST review checklist ready', 'Schema review checklist downloaded.')
  } catch (error) {
    feedback.failed('GST schema review download failed', error)
  } finally {
    schemaReviewLoading.value = false
  }
}

async function previewReturn() {
  loading.value = true
  preview.value = null
  try {
    preview.value = activeForm.value === 'gstr1'
      ? await api.create<any>('gst-returns/gstr1/preview', buildGstr1Payload() as any)
      : await api.create<any>('gst-returns/gstr3b/preview', buildGstr3BPayload() as any)
  } catch (error) {
    feedback.failed('GST preview failed', error)
  } finally {
    loading.value = false
  }
}

async function downloadReturn(format: 'json' | 'excel') {
  loading.value = true
  try {
    const endpoint = activeForm.value === 'gstr1'
      ? `gst-returns/gstr1/${format}`
      : `gst-returns/gstr3b/${format}`
    const payload = activeForm.value === 'gstr1' ? buildGstr1Payload() : buildGstr3BPayload()
    await downloadFile(endpoint, payload)
    feedback.notify('GST export ready', `${activeLabel.value} ${format.toUpperCase()} downloaded.`)
  } catch (error) {
    feedback.failed('GST export failed', error)
  } finally {
    loading.value = false
  }
}

async function downloadGetFile(resource: string, fallback: string) {
  const response = await fetch(`${config.public.apiBase}/${resource}`, {
    headers: api.authHeaders()
  })

  if (!response.ok) {
    const errorBody = await response.json().catch(() => null)
    throw new Error(errorBody?.message || `Download failed with status ${response.status}`)
  }

  const blob = await response.blob()
  const disposition = response.headers.get('content-disposition') || ''
  const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
  const fileName = match ? decodeURIComponent(match[1]) : fallback
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

async function downloadFile(resource: string, body: any) {
  const response = await fetch(`${config.public.apiBase}/${resource}`, {
    method: 'POST',
    headers: {
      ...api.authHeaders(),
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(body)
  })

  if (!response.ok) {
    const errorBody = await response.json().catch(() => null)
    throw new Error(errorBody?.message || `Export failed with status ${response.status}`)
  }

  const blob = await response.blob()
  const disposition = response.headers.get('content-disposition') || ''
  const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
  const fallback = `Garmetix-${activeLabel.value}-${header.returnPeriod}.${resource.endsWith('json') ? 'json' : 'xlsx'}`
  const fileName = match ? decodeURIComponent(match[1]) : fallback
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(value || 0)
}

function numeric(value: any) {
  return Number(value || 0)
}

const gstr1Totals = computed(() => {
  const taxable = b2bRows.value.reduce((sum, row) => sum + numeric(row.taxableValue), 0) + b2cRows.value.reduce((sum, row) => sum + numeric(row.taxableValue), 0)
  const tax = ['integratedTax', 'centralTax', 'stateTax', 'cess'].reduce((total, key) => total + b2bRows.value.reduce((sum, row) => sum + numeric(row[key]), 0) + b2cRows.value.reduce((sum, row) => sum + numeric(row[key]), 0), 0)
  return { taxable, tax }
})

const gstr3BTotals = computed(() => ({
  taxable: numeric(supplies.outwardTaxableValue) + numeric(supplies.zeroRatedTaxableValue) + numeric(supplies.nilExemptTaxableValue) + numeric(supplies.nonGstTaxableValue) + numeric(supplies.reverseChargeTaxableValue),
  tax: numeric(supplies.outwardIntegratedTax) + numeric(supplies.outwardCentralTax) + numeric(supplies.outwardStateTax) + numeric(supplies.outwardCess)
}))

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="GST Returns"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard gst-return-page">
      <UiModulePageHeader
        title="GST Returns"
        description="Urgent standalone module for manual GSTR-1 and GSTR-3B JSON/Excel generation. Billing and Purchase auto-linking will be added later."
        icon="i-lucide-file-json-2"
        primary-label="Preview"
        primary-icon="i-lucide-eye"
        @primary="previewReturn"
      >
        <template #actions>
          <UBadge color="error" variant="subtle">Urgent</UBadge>
          <UButton icon="i-lucide-shield-check" color="warning" variant="subtle" label="Review" :loading="schemaReviewLoading" @click="downloadSchemaReview" />
          <UButton icon="i-lucide-file-json" color="primary" variant="subtle" label="JSON" :loading="loading" @click="downloadReturn('json')" />
          <UButton icon="i-lucide-file-spreadsheet" color="success" variant="subtle" label="Excel" :loading="loading" @click="downloadReturn('excel')" />
        </template>
      </UiModulePageHeader>

      <UAlert
        icon="i-lucide-info"
        color="warning"
        variant="soft"
        title="Separate manual GST module"
        description="This screen does not read Billing or Purchase data yet. Enter GST return values manually, generate export files, then verify them before portal upload/filing."
      />

      <UCard v-if="schemaReview" class="planner-card gst-schema-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Portal / Offline Utility Readiness</h3>
              <p>{{ schemaReview.reviewedOnUtc }} — manual portal validation still required before production filing.</p>
            </div>
            <div class="setup-tabs">
              <UBadge color="warning" variant="subtle">Review required</UBadge>
              <UButton size="sm" icon="i-lucide-download" label="Checklist Excel" :loading="schemaReviewLoading" @click="downloadSchemaReview" />
            </div>
          </div>
        </template>

        <div class="gst-schema-grid">
          <div class="gst-schema-list">
            <strong>{{ activeLabel }} mapping</strong>
            <div v-for="item in activeSchemaItems" :key="`${item.section}-${item.exportKey}`" class="gst-schema-row">
              <span>{{ item.section }}</span>
              <code>{{ item.exportKey }}</code>
              <UBadge color="success" variant="subtle">{{ item.status }}</UBadge>
            </div>
          </div>
          <div class="gst-schema-list warning-list">
            <strong>Production warnings</strong>
            <p v-for="warning in schemaWarnings" :key="warning">{{ warning }}</p>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Return Header</h3>
              <p>GSTIN and return period are required for JSON/Excel generation.</p>
            </div>
            <div class="setup-tabs">
              <UButton label="GSTR-1" icon="i-lucide-receipt-text" :color="activeForm === 'gstr1' ? 'primary' : 'neutral'" :variant="activeForm === 'gstr1' ? 'solid' : 'subtle'" @click="activeForm = 'gstr1'; preview = null" />
              <UButton label="GSTR-3B" icon="i-lucide-file-check-2" :color="activeForm === 'gstr3b' ? 'primary' : 'neutral'" :variant="activeForm === 'gstr3b' ? 'solid' : 'subtle'" @click="activeForm = 'gstr3b'; preview = null" />
            </div>
          </div>
        </template>

        <div class="form-grid four">
          <UFormField label="GSTIN">
            <UInput v-model="header.gstin" placeholder="22AAAAA0000A1Z5" />
          </UFormField>
          <UFormField label="Return Period (MMYYYY)">
            <UInput v-model="header.returnPeriod" placeholder="042026" />
          </UFormField>
          <UFormField label="Legal Name">
            <UInput v-model="header.legalName" />
          </UFormField>
          <UFormField label="Trade Name">
            <UInput v-model="header.tradeName" />
          </UFormField>
          <UFormField label="Gross Turnover">
            <UInput v-model.number="header.grossTurnover" type="number" step="0.01" />
          </UFormField>
          <UFormField label="Current Turnover">
            <UInput v-model.number="header.currentTurnover" type="number" step="0.01" />
          </UFormField>
        </div>
      </UCard>

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-file-check-2" color="primary" variant="subtle" />
            <div><p>Active Form</p><strong>{{ activeLabel }}</strong><span>Manual export</span></div>
          </div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-indian-rupee" color="success" variant="subtle" />
            <div><p>Taxable Value</p><strong>{{ money(activeForm === 'gstr1' ? gstr1Totals.taxable : gstr3BTotals.taxable) }}</strong><span>Current entry</span></div>
          </div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-percent" color="warning" variant="subtle" />
            <div><p>Total Tax</p><strong>{{ money(activeForm === 'gstr1' ? gstr1Totals.tax : gstr3BTotals.tax) }}</strong><span>IGST + CGST + SGST + Cess</span></div>
          </div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-alert-triangle" :color="previewIssues.length ? 'error' : 'success'" variant="subtle" />
            <div><p>Validation</p><strong>{{ previewIssues.length }}</strong><span>{{ previewIssues.length ? 'Issues found' : 'No preview issues' }}</span></div>
          </div>
        </UCard>
      </div>

      <UCard v-if="preview" class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>{{ preview.form }} Preview</h3>
              <p>{{ preview.gstin }} / {{ preview.returnPeriod }} / {{ preview.rowCount }} rows</p>
            </div>
            <UBadge :color="previewIssues.length ? 'error' : 'success'" variant="subtle">{{ previewIssues.length ? 'Fix required' : 'Ready' }}</UBadge>
          </div>
        </template>
        <div v-if="previewIssues.length" class="gst-issue-list">
          <div v-for="issue in previewIssues" :key="`${issue.field}-${issue.message}`" class="gst-issue-row">
            <strong>{{ issue.field }}</strong>
            <span>{{ issue.message }}</span>
          </div>
        </div>
        <div v-else class="empty-state compact">
          <p>Preview passed. You can download JSON or Excel.</p>
        </div>
      </UCard>

      <template v-if="activeForm === 'gstr1'">
        <UCard class="planner-card">
          <template #header>
            <div class="setup-list-header">
              <div><h3>GSTR-1 B2B Invoices</h3><p>Manual recipient-wise outward invoice details.</p></div>
              <UButton icon="i-lucide-plus" label="Add B2B" @click="addRow(b2bRows, newB2BRow)" />
            </div>
          </template>
          <div class="gst-entry-list">
            <div v-for="(row, index) in b2bRows" :key="index" class="gst-entry-card">
              <div class="gst-entry-title"><strong>B2B #{{ index + 1 }}</strong><UButton icon="i-lucide-trash-2" color="error" variant="ghost" @click="removeRow(b2bRows, index)" /></div>
              <div class="form-grid four compact-grid">
                <UFormField label="Recipient GSTIN"><UInput v-model="row.recipientGstin" /></UFormField>
                <UFormField label="Recipient Name"><UInput v-model="row.recipientName" /></UFormField>
                <UFormField label="Invoice No"><UInput v-model="row.invoiceNumber" /></UFormField>
                <UFormField label="Invoice Date"><UInput v-model="row.invoiceDate" type="date" /></UFormField>
                <UFormField label="POS"><UInput v-model="row.placeOfSupply" placeholder="20-Jharkhand" /></UFormField>
                <UFormField label="RCHRG"><USelect v-model="row.reverseCharge" :items="['N', 'Y']" /></UFormField>
                <UFormField label="Invoice Type"><UInput v-model="row.invoiceType" /></UFormField>
                <UFormField label="Invoice Value"><UInput v-model.number="row.invoiceValue" type="number" step="0.01" /></UFormField>
                <UFormField label="Rate"><UInput v-model.number="row.rate" type="number" step="0.01" /></UFormField>
                <UFormField label="Taxable"><UInput v-model.number="row.taxableValue" type="number" step="0.01" /></UFormField>
                <UFormField label="IGST"><UInput v-model.number="row.integratedTax" type="number" step="0.01" /></UFormField>
                <UFormField label="CGST"><UInput v-model.number="row.centralTax" type="number" step="0.01" /></UFormField>
                <UFormField label="SGST"><UInput v-model.number="row.stateTax" type="number" step="0.01" /></UFormField>
                <UFormField label="Cess"><UInput v-model.number="row.cess" type="number" step="0.01" /></UFormField>
                <UFormField label="E-Commerce GSTIN"><UInput v-model="row.eCommerceGstin" /></UFormField>
              </div>
            </div>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header>
            <div class="setup-list-header"><div><h3>GSTR-1 B2C Summary</h3><p>Manual small/large B2C tax-rate summaries.</p></div><UButton icon="i-lucide-plus" label="Add B2C" @click="addRow(b2cRows, newB2CRow)" /></div>
          </template>
          <div class="gst-entry-list compact">
            <div v-for="(row, index) in b2cRows" :key="index" class="gst-entry-card">
              <div class="gst-entry-title"><strong>B2C #{{ index + 1 }}</strong><UButton icon="i-lucide-trash-2" color="error" variant="ghost" @click="removeRow(b2cRows, index)" /></div>
              <div class="form-grid four compact-grid">
                <UFormField label="Type"><USelect v-model="row.type" :items="['INTRA', 'INTER']" /></UFormField>
                <UFormField label="POS"><UInput v-model="row.placeOfSupply" /></UFormField>
                <UFormField label="Rate"><UInput v-model.number="row.rate" type="number" step="0.01" /></UFormField>
                <UFormField label="Taxable"><UInput v-model.number="row.taxableValue" type="number" step="0.01" /></UFormField>
                <UFormField label="IGST"><UInput v-model.number="row.integratedTax" type="number" step="0.01" /></UFormField>
                <UFormField label="CGST"><UInput v-model.number="row.centralTax" type="number" step="0.01" /></UFormField>
                <UFormField label="SGST"><UInput v-model.number="row.stateTax" type="number" step="0.01" /></UFormField>
                <UFormField label="Cess"><UInput v-model.number="row.cess" type="number" step="0.01" /></UFormField>
              </div>
            </div>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header>
            <div class="setup-list-header"><div><h3>HSN, Documents, Nil/Exempt</h3><p>Supporting sheets for GSTR-1 Excel/JSON.</p></div></div>
          </template>
          <div class="gst-subsection">
            <div class="setup-list-header small"><strong>HSN Summary</strong><UButton size="sm" icon="i-lucide-plus" label="Add HSN" @click="addRow(hsnRows, newHsnRow)" /></div>
            <div v-for="(row, index) in hsnRows" :key="`hsn-${index}`" class="form-grid four compact-grid gst-line">
              <UInput v-model="row.hsnCode" placeholder="HSN" />
              <UInput v-model="row.description" placeholder="Description" />
              <UInput v-model="row.uqc" placeholder="UQC" />
              <UInput v-model.number="row.totalQuantity" type="number" step="0.01" placeholder="Qty" />
              <UInput v-model.number="row.totalValue" type="number" step="0.01" placeholder="Total value" />
              <UInput v-model.number="row.taxableValue" type="number" step="0.01" placeholder="Taxable" />
              <UInput v-model.number="row.integratedTax" type="number" step="0.01" placeholder="IGST" />
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" @click="removeRow(hsnRows, index)" />
            </div>
          </div>
          <div class="gst-subsection">
            <div class="setup-list-header small"><strong>Documents Issued</strong><UButton size="sm" icon="i-lucide-plus" label="Add Document" @click="addRow(documentRows, newDocumentRow)" /></div>
            <div v-for="(row, index) in documentRows" :key="`doc-${index}`" class="form-grid four compact-grid gst-line">
              <UInput v-model="row.natureOfDocument" placeholder="Nature" />
              <UInput v-model="row.fromSerialNumber" placeholder="From" />
              <UInput v-model="row.toSerialNumber" placeholder="To" />
              <UInput v-model.number="row.totalNumber" type="number" placeholder="Total" />
              <UInput v-model.number="row.cancelledNumber" type="number" placeholder="Cancelled" />
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" @click="removeRow(documentRows, index)" />
            </div>
          </div>
          <div class="gst-subsection">
            <div class="setup-list-header small"><strong>Nil / Exempt / Non-GST</strong><UButton size="sm" icon="i-lucide-plus" label="Add Row" @click="addRow(nilRows, newNilRow)" /></div>
            <div v-for="(row, index) in nilRows" :key="`nil-${index}`" class="form-grid four compact-grid gst-line">
              <UInput v-model="row.description" placeholder="Description" />
              <UInput v-model.number="row.nilRated" type="number" step="0.01" placeholder="Nil rated" />
              <UInput v-model.number="row.exempted" type="number" step="0.01" placeholder="Exempted" />
              <UInput v-model.number="row.nonGst" type="number" step="0.01" placeholder="Non-GST" />
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" @click="removeRow(nilRows, index)" />
            </div>
          </div>
        </UCard>
      </template>

      <template v-else>
        <UCard class="planner-card">
          <template #header><div><h3>GSTR-3B 3.1 Supplies</h3><p>Manual outward and reverse-charge summary.</p></div></template>
          <div class="form-grid four compact-grid">
            <UFormField label="Outward taxable"><UInput v-model.number="supplies.outwardTaxableValue" type="number" step="0.01" /></UFormField>
            <UFormField label="Outward IGST"><UInput v-model.number="supplies.outwardIntegratedTax" type="number" step="0.01" /></UFormField>
            <UFormField label="Outward CGST"><UInput v-model.number="supplies.outwardCentralTax" type="number" step="0.01" /></UFormField>
            <UFormField label="Outward SGST"><UInput v-model.number="supplies.outwardStateTax" type="number" step="0.01" /></UFormField>
            <UFormField label="Outward Cess"><UInput v-model.number="supplies.outwardCess" type="number" step="0.01" /></UFormField>
            <UFormField label="Zero rated taxable"><UInput v-model.number="supplies.zeroRatedTaxableValue" type="number" step="0.01" /></UFormField>
            <UFormField label="Zero rated IGST"><UInput v-model.number="supplies.zeroRatedIntegratedTax" type="number" step="0.01" /></UFormField>
            <UFormField label="Nil/exempt taxable"><UInput v-model.number="supplies.nilExemptTaxableValue" type="number" step="0.01" /></UFormField>
            <UFormField label="Non-GST taxable"><UInput v-model.number="supplies.nonGstTaxableValue" type="number" step="0.01" /></UFormField>
            <UFormField label="RCM taxable"><UInput v-model.number="supplies.reverseChargeTaxableValue" type="number" step="0.01" /></UFormField>
            <UFormField label="RCM IGST"><UInput v-model.number="supplies.reverseChargeIntegratedTax" type="number" step="0.01" /></UFormField>
            <UFormField label="RCM CGST"><UInput v-model.number="supplies.reverseChargeCentralTax" type="number" step="0.01" /></UFormField>
            <UFormField label="RCM SGST"><UInput v-model.number="supplies.reverseChargeStateTax" type="number" step="0.01" /></UFormField>
            <UFormField label="RCM Cess"><UInput v-model.number="supplies.reverseChargeCess" type="number" step="0.01" /></UFormField>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header><div><h3>GSTR-3B ITC / Interstate / Inward / Fee</h3><p>Manual input credit and other return sections.</p></div></template>
          <div class="gst-subsection">
            <strong>ITC Available</strong>
            <div class="form-grid four compact-grid">
              <UFormField label="Import goods IGST"><UInput v-model.number="itc.importGoodsIntegratedTax" type="number" step="0.01" /></UFormField>
              <UFormField label="Import goods Cess"><UInput v-model.number="itc.importGoodsCess" type="number" step="0.01" /></UFormField>
              <UFormField label="Import services IGST"><UInput v-model.number="itc.importServicesIntegratedTax" type="number" step="0.01" /></UFormField>
              <UFormField label="Other ITC IGST"><UInput v-model.number="itc.otherIntegratedTax" type="number" step="0.01" /></UFormField>
              <UFormField label="Other ITC CGST"><UInput v-model.number="itc.otherCentralTax" type="number" step="0.01" /></UFormField>
              <UFormField label="Other ITC SGST"><UInput v-model.number="itc.otherStateTax" type="number" step="0.01" /></UFormField>
              <UFormField label="Other ITC Cess"><UInput v-model.number="itc.otherCess" type="number" step="0.01" /></UFormField>
              <UFormField label="Ineligible IGST"><UInput v-model.number="itc.ineligibleIntegratedTax" type="number" step="0.01" /></UFormField>
            </div>
          </div>
          <div class="gst-subsection">
            <strong>Interstate Supplies</strong>
            <div class="form-grid four compact-grid">
              <UFormField label="Unregistered taxable"><UInput v-model.number="interStateSupplies.unregisteredTaxableValue" type="number" step="0.01" /></UFormField>
              <UFormField label="Unregistered IGST"><UInput v-model.number="interStateSupplies.unregisteredIntegratedTax" type="number" step="0.01" /></UFormField>
              <UFormField label="Composition taxable"><UInput v-model.number="interStateSupplies.compositionTaxableValue" type="number" step="0.01" /></UFormField>
              <UFormField label="Composition IGST"><UInput v-model.number="interStateSupplies.compositionIntegratedTax" type="number" step="0.01" /></UFormField>
              <UFormField label="UIN taxable"><UInput v-model.number="interStateSupplies.uinTaxableValue" type="number" step="0.01" /></UFormField>
              <UFormField label="UIN IGST"><UInput v-model.number="interStateSupplies.uinIntegratedTax" type="number" step="0.01" /></UFormField>
            </div>
          </div>
          <div class="gst-subsection">
            <strong>Inward Supplies and Late Fee</strong>
            <div class="form-grid four compact-grid">
              <UFormField label="Composition taxable"><UInput v-model.number="inwardSupplies.compositionTaxableValue" type="number" step="0.01" /></UFormField>
              <UFormField label="Nil rated taxable"><UInput v-model.number="inwardSupplies.nilRatedTaxableValue" type="number" step="0.01" /></UFormField>
              <UFormField label="Non-GST taxable"><UInput v-model.number="inwardSupplies.nonGstTaxableValue" type="number" step="0.01" /></UFormField>
              <UFormField label="Interest IGST"><UInput v-model.number="interestLateFee.integratedTaxInterest" type="number" step="0.01" /></UFormField>
              <UFormField label="Interest CGST"><UInput v-model.number="interestLateFee.centralTaxInterest" type="number" step="0.01" /></UFormField>
              <UFormField label="Interest SGST"><UInput v-model.number="interestLateFee.stateTaxInterest" type="number" step="0.01" /></UFormField>
              <UFormField label="Late fee CGST"><UInput v-model.number="interestLateFee.centralLateFee" type="number" step="0.01" /></UFormField>
              <UFormField label="Late fee SGST"><UInput v-model.number="interestLateFee.stateLateFee" type="number" step="0.01" /></UFormField>
            </div>
          </div>
        </UCard>
      </template>
    </section>
  </AppShell>
</template>

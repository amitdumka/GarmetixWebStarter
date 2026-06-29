<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="text-sm text-muted">GST registers</p>
          <h2 class="mt-1 text-2xl font-semibold">GST Reports</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review HSN summary, tax rate summary and invoice register for CA reconciliation. CSV downloads are available, while email/WhatsApp review sending remains disabled here.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="returnPeriod" placeholder="MMYYYY" class="sm:w-32" />
          <USelect v-model="direction" :items="directionItems" class="sm:w-40" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <div class="flex flex-wrap gap-2">
      <UButton
        v-for="tab in tabs"
        :key="tab.key"
        :icon="tab.icon"
        size="sm"
        color="neutral"
        :variant="activeTab === tab.key ? 'soft' : 'ghost'"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </UButton>
    </div>

    <section class="border border-default bg-muted/10 p-4">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="text-base font-semibold">{{ currentTab.label }}</h3>
          <p class="text-xs text-muted">{{ currentTab.description }}</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search GST report" class="sm:w-72" />
          <UButton icon="i-lucide-file-down" color="primary" variant="soft" :loading="downloadLoading" @click="downloadCsv">CSV</UButton>
        </div>
      </div>
      <BooksMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No GST report rows found." />
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  formatDate,
  readArray,
  readNumber,
  readText,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'GST Reports - Garmetix Books' })

type GstReportTab = 'hsn' | 'tax' | 'invoice'

const { download, get } = useBooksApiClient()
const loading = ref(true)
const downloadLoading = ref(false)
const error = ref('')
const search = ref('')
const returnPeriod = ref(currentReturnPeriod())
const direction = ref('both')
const activeTab = ref<GstReportTab>('tax')
const hsnReport = ref<ApiRecord | null>(null)
const taxReport = ref<ApiRecord | null>(null)
const invoiceReport = ref<ApiRecord | null>(null)

const directionItems = [
  { label: 'Both', value: 'both' },
  { label: 'Sales', value: 'sales' },
  { label: 'Purchase', value: 'purchase' }
]
const tabs = [
  { key: 'tax' as const, label: 'Tax Summary', icon: 'i-lucide-scale', description: 'Output, input and net payable by GST rate.' },
  { key: 'hsn' as const, label: 'HSN Summary', icon: 'i-lucide-list-tree', description: 'HSN/UQC quantity, taxable value and tax summary.' },
  { key: 'invoice' as const, label: 'Invoice Register', icon: 'i-lucide-receipt-text', description: 'GST invoice register for sales and purchase documents.' }
]
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])
const cards = computed(() => [
  { label: 'Tax Rows', value: readNumber(taxReport.value, ['rowCount']), detail: 'GST rate summary rows' },
  { label: 'Output Taxable', value: money(readNumber(taxReport.value, ['outputTaxableValue'])), detail: returnPeriod.value },
  { label: 'Input Taxable', value: money(readNumber(taxReport.value, ['inputTaxableValue'])), detail: returnPeriod.value },
  { label: 'Net Payable', value: money(readNumber(taxReport.value, ['netTaxPayable'])), detail: 'Sales tax minus input tax' },
  { label: 'HSN Taxable', value: money(readNumber(hsnReport.value, ['totalTaxableValue'])), detail: `${readNumber(hsnReport.value, ['rowCount'])} HSN row(s)` },
  { label: 'HSN Tax', value: money(readNumber(hsnReport.value, ['totalTaxAmount'])), detail: 'CGST, SGST and IGST total' }
])
const reportRows = computed<Record<GstReportTab, ApiRecord[]>>(() => ({
  tax: readArray(taxReport.value, ['rows']).map(row => ({
    rate: readText(row, ['rate']),
    salesTaxable: money(row.salesTaxableValue),
    salesTax: money(readNumber(row, ['salesCgstAmount']) + readNumber(row, ['salesSgstAmount']) + readNumber(row, ['salesIgstAmount'])),
    purchaseTaxable: money(row.purchaseTaxableValue),
    purchaseTax: money(readNumber(row, ['purchaseCgstAmount']) + readNumber(row, ['purchaseSgstAmount']) + readNumber(row, ['purchaseIgstAmount'])),
    net: money(row.netTaxPayable)
  })),
  hsn: readArray(hsnReport.value, ['rows']).map(row => ({
    direction: readText(row, ['direction']),
    hsn: readText(row, ['hsnCode']),
    description: readText(row, ['description']),
    uqc: readText(row, ['uqc']),
    rate: readText(row, ['rate']),
    quantity: readText(row, ['quantity']),
    taxable: money(row.taxableValue),
    tax: money(row.taxAmount),
    total: money(row.totalValue)
  })),
  invoice: readArray(invoiceReport.value, ['rows']).map(row => ({
    direction: readText(row, ['direction']),
    invoice: readText(row, ['invoiceNumber']),
    date: formatDate(row.onDate),
    party: readText(row, ['partyName']),
    gstin: readText(row, ['partyGstin']),
    status: readText(row, ['invoiceStatus']),
    taxable: money(row.taxableValue),
    tax: money(row.taxAmount),
    bill: money(row.billAmount)
  }))
}))
const columns: Record<GstReportTab, Array<{ key: string, label: string }>> = {
  tax: [
    { key: 'rate', label: 'Rate' },
    { key: 'salesTaxable', label: 'Sales Taxable' },
    { key: 'salesTax', label: 'Sales Tax' },
    { key: 'purchaseTaxable', label: 'Purchase Taxable' },
    { key: 'purchaseTax', label: 'Purchase Tax' },
    { key: 'net', label: 'Net' }
  ],
  hsn: [
    { key: 'direction', label: 'Direction' },
    { key: 'hsn', label: 'HSN' },
    { key: 'description', label: 'Description' },
    { key: 'uqc', label: 'UQC' },
    { key: 'rate', label: 'Rate' },
    { key: 'quantity', label: 'Quantity' },
    { key: 'taxable', label: 'Taxable' },
    { key: 'tax', label: 'Tax' },
    { key: 'total', label: 'Total' }
  ],
  invoice: [
    { key: 'direction', label: 'Direction' },
    { key: 'invoice', label: 'Invoice' },
    { key: 'date', label: 'Date' },
    { key: 'party', label: 'Party' },
    { key: 'gstin', label: 'GSTIN' },
    { key: 'status', label: 'Status' },
    { key: 'taxable', label: 'Taxable' },
    { key: 'tax', label: 'Tax' },
    { key: 'bill', label: 'Bill' }
  ]
}
const currentColumns = computed(() => columns[activeTab.value])
const currentRows = computed(() => reportRows.value[activeTab.value])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return currentRows.value
  return currentRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

function currentReturnPeriod() {
  const date = new Date()
  return `${String(date.getMonth() + 1).padStart(2, '0')}${date.getFullYear()}`
}

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [hsnData, taxData, invoiceData] = await Promise.allSettled([
      get<unknown>('gst-returns/reports/hsn-summary', { returnPeriod: returnPeriod.value, direction: direction.value }),
      get<unknown>('gst-returns/reports/tax-summary', { returnPeriod: returnPeriod.value }),
      get<unknown>('gst-returns/reports/invoice-register', { returnPeriod: returnPeriod.value, direction: direction.value })
    ])
    if (hsnData.status === 'fulfilled' && hsnData.value && typeof hsnData.value === 'object') hsnReport.value = hsnData.value as ApiRecord
    if (taxData.status === 'fulfilled' && taxData.value && typeof taxData.value === 'object') taxReport.value = taxData.value as ApiRecord
    if (invoiceData.status === 'fulfilled' && invoiceData.value && typeof invoiceData.value === 'object') invoiceReport.value = invoiceData.value as ApiRecord
    const failed = [hsnData, taxData, invoiceData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} GST report request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST reports.'
  } finally {
    loading.value = false
  }
}

async function downloadCsv() {
  downloadLoading.value = true
  error.value = ''
  try {
    const path = activeTab.value === 'hsn'
      ? 'gst-returns/reports/hsn-summary/csv'
      : activeTab.value === 'invoice'
        ? 'gst-returns/reports/invoice-register/csv'
        : 'gst-returns/reports/tax-summary/csv'
    const query = activeTab.value === 'tax'
      ? { returnPeriod: returnPeriod.value }
      : { returnPeriod: returnPeriod.value, direction: direction.value }
    await download(path, query, `gst-${activeTab.value}-${returnPeriod.value}.csv`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download GST report CSV.'
  } finally {
    downloadLoading.value = false
  }
}

onMounted(refresh)
</script>

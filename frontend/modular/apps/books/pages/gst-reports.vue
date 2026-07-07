<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-table-properties" class="size-4" />
            GST registers
          </p>
          <h2 class="garmetix-dashboard-title">GST Reports</h2>
          <p class="garmetix-dashboard-subtitle">
            Review HSN summary, tax rate summary and invoice register for CA reconciliation. CSV downloads are available, while email/WhatsApp review sending remains disabled here.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="returnPeriod" placeholder="MMYYYY" class="sm:w-32" />
          <USelect v-model="direction" :items="directionItems" class="sm:w-40" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-send" color="primary" variant="soft" @click="openReportShare">Send to CA</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
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

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ currentTab.label }}</h3>
          <p class="text-xs text-muted">{{ currentTab.description }}</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search GST report" class="sm:w-72" />
          <UButton icon="i-lucide-file-down" color="primary" variant="soft" :loading="downloadLoading" @click="downloadCsv">CSV</UButton>
        </div>
      </div>
      <BooksMasterTable :columns="currentColumns" :rows="filteredRows" empty-text="No GST report rows found." />
    </section>

    <section v-if="recentGstShareLogs.length" class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Recent CA Shares</h3>
      <ul class="space-y-2 text-sm">
        <li v-for="log in recentGstShareLogs" :key="log.id" class="border-b border-default pb-2">
          <p class="font-medium">{{ log.kind }} - {{ log.returnPeriod }}</p>
          <p class="text-muted">{{ log.toEmail }} - {{ formatDateTime(log.sentAt) }}</p>
          <p class="text-xs text-muted">{{ log.attachmentNames.length }} attachment(s)</p>
        </li>
      </ul>
    </section>

    <UModal v-model:open="reportShareOpen">
      <template #content>
        <div class="grid gap-4 p-5">
          <div>
            <h3 class="text-base font-semibold">Send GST Book Reports to CA</h3>
            <p class="mt-1 text-sm text-muted">HSN summary, tax summary and invoice register CSVs for the selected period.</p>
          </div>
          <UFormField label="Accountant/CA Email">
            <UInput v-model="reportShare.toEmail" type="email" placeholder="ca@example.com" />
          </UFormField>
          <UFormField label="Accountant/CA Name">
            <UInput v-model="reportShare.toName" />
          </UFormField>
          <UFormField label="WhatsApp Mobile">
            <UInput v-model="reportShare.whatsAppNumber" />
          </UFormField>
          <UFormField label="Return Period">
            <UInput v-model="returnPeriod" placeholder="MMYYYY" />
          </UFormField>
          <UFormField label="Message">
            <UTextarea v-model="reportShare.note" :rows="2" />
          </UFormField>
          <div class="grid grid-cols-3 gap-2 text-sm">
            <label class="flex items-center gap-2"><USwitch v-model="reportShare.includeHsnSummaryCsv" /> HSN CSV</label>
            <label class="flex items-center gap-2"><USwitch v-model="reportShare.includeTaxSummaryCsv" /> Tax Summary CSV</label>
            <label class="flex items-center gap-2"><USwitch v-model="reportShare.includeInvoiceRegisterCsv" /> Invoice Register CSV</label>
          </div>
          <div class="flex flex-wrap justify-between gap-2">
            <div class="flex flex-wrap gap-2">
              <UButton size="sm" color="neutral" variant="soft" @click="gstReviewContact.save(reportShare)">Save as default CA contact</UButton>
              <UButton size="sm" color="neutral" variant="soft" @click="gstReviewContact.applyTo(reportShare)">Use saved contact</UButton>
            </div>
            <UButton
              v-if="reportShareResponse?.whatsAppShareUrl"
              size="sm"
              color="success"
              variant="soft"
              icon="i-lucide-message-circle"
              @click="openReportWhatsApp"
            >
              Open WhatsApp Share
            </UButton>
          </div>
          <UAlert v-if="reportShareResponse" color="success" variant="subtle" icon="i-lucide-mail-check" :description="readText(reportShareResponse, ['message'])" />
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="reportShareOpen = false">Close</UButton>
            <UButton color="primary" icon="i-lucide-send" :disabled="!canSendReports" :loading="sendingReports" @click="sendGstReports">Confirm &amp; Send Email</UButton>
          </div>
        </div>
      </template>
    </UModal>
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
import { useGstReviewContact } from '../composables/useGstReviewContact'

useHead({ title: 'GST Reports - Garmetix Books' })

type GstReportTab = 'hsn' | 'tax' | 'invoice'

const { download, get, post } = useBooksApiClient()
const gstReviewContact = useGstReviewContact()
const loading = ref(true)
const downloadLoading = ref(false)
const sendingReports = ref(false)
const error = ref('')
const search = ref('')
const returnPeriod = ref(currentReturnPeriod())
const direction = ref('both')
const activeTab = ref<GstReportTab>('tax')
const hsnReport = ref<ApiRecord | null>(null)
const taxReport = ref<ApiRecord | null>(null)
const invoiceReport = ref<ApiRecord | null>(null)
const reportShareOpen = ref(false)
const reportShareResponse = ref<ApiRecord | null>(null)
const reportShare = reactive({
  toEmail: '',
  toName: '',
  whatsAppNumber: '',
  note: 'Please review the attached GST book reports.',
  includeHsnSummaryCsv: true,
  includeTaxSummaryCsv: true,
  includeInvoiceRegisterCsv: true
})

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
const reportAttachmentCount = computed(() => [
  reportShare.includeHsnSummaryCsv,
  reportShare.includeTaxSummaryCsv,
  reportShare.includeInvoiceRegisterCsv
].filter(Boolean).length)
const canSendReports = computed(() => Boolean(reportShare.toEmail.trim() && reportAttachmentCount.value > 0))
const recentGstShareLogs = computed(() => gstReviewContact.shareLogs.value.slice(0, 5))

function currentReturnPeriod() {
  const date = new Date()
  return `${String(date.getMonth() + 1).padStart(2, '0')}${date.getFullYear()}`
}

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
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

function openReportShare() {
  gstReviewContact.applyTo(reportShare)
  reportShareResponse.value = null
  reportShareOpen.value = true
}

async function sendGstReports() {
  if (!reportShare.toEmail.trim()) {
    error.value = 'Accountant/CA email is required.'
    return
  }
  if (reportAttachmentCount.value === 0) {
    error.value = 'Select at least one report to send.'
    return
  }

  sendingReports.value = true
  error.value = ''
  try {
    await refresh()
    if (!window.confirm(`Confirm sending GST book reports ${returnPeriod.value} to ${reportShare.toEmail}?`)) return

    gstReviewContact.save(reportShare)
    reportShareResponse.value = await post<ApiRecord>('gst-returns/reports/send-review', {
      companyId: null,
      returnPeriod: returnPeriod.value,
      direction: direction.value,
      ...reportShare
    })
    gstReviewContact.addLog({
      kind: `GST book reports (${direction.value})`,
      returnPeriod: returnPeriod.value,
      toEmail: reportShare.toEmail.trim(),
      toName: reportShare.toName,
      attachmentNames: readArray(reportShareResponse.value, ['attachmentNames']).map(item => String(item)),
      message: readText(reportShareResponse.value, ['message'])
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'GST report send failed.'
  } finally {
    sendingReports.value = false
  }
}

function openReportWhatsApp() {
  const url = readText(reportShareResponse.value, ['whatsAppShareUrl'], '')
  if (!url) {
    error.value = 'Send the email first, then share on WhatsApp.'
    return
  }
  window.open(url, '_blank')
}

onMounted(refresh)
</script>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-package-plus" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">Purchase Register</h2>
          <p class="garmetix-dashboard-subtitle">
            Supplier invoices, goods intake and vendor payment status. Use New Inward for a full entry with items.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-file-plus-2" color="primary" variant="solid" to="/purchase/new">New Inward</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="applyFilters">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-4">
      <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(200px,1fr)_140px_150px_150px_130px_auto]">
      <UFormField label="Search invoice, inward or vendor">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Invoice, inward no., vendor" @keyup.enter="applyFilters" />
      </UFormField>
      <UFormField label="Status">
        <USelect v-model="statusFilter" :items="statusOptions" />
      </UFormField>
      <UFormField label="Date mode">
        <USelect v-model="dateMode" :items="dateModeOptions" />
      </UFormField>
      <UFormField label="Date">
        <USelect v-model="datePreset" :items="datePresetOptions" />
      </UFormField>
      <UFormField label="Rows">
        <USelect v-model="pageSize" :items="pageSizeOptions" />
      </UFormField>
      <div class="flex items-end gap-2">
        <UButton color="neutral" variant="soft" icon="i-lucide-list-filter" :loading="loading" @click="applyFilters">Find</UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="clearSearch">Clear</UButton>
      </div>
      <template v-if="datePreset === 'custom'">
        <UFormField label="From">
          <UInput v-model="customFromDate" type="date" />
        </UFormField>
        <UFormField label="To">
          <UInput v-model="customToDate" type="date" />
        </UFormField>
      </template>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[1080px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Invoice / Inward</th>
            <th class="border-b border-default p-3">Date</th>
            <th class="border-b border-default p-3">Vendor</th>
            <th class="border-b border-default p-3 text-right">Bill</th>
            <th class="border-b border-default p-3 text-right">Paid</th>
            <th class="border-b border-default p-3 text-right">Balance</th>
            <th class="border-b border-default p-3">Status</th>
            <th class="border-b border-default p-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!invoices.length">
            <td colspan="8" class="p-8 text-center text-muted">No purchase invoices matched the current filters.</td>
          </tr>
          <tr v-for="invoice in invoices" :key="readText(invoice, ['id'])" class="align-top">
            <td class="border-b border-default p-3">
              <p class="font-semibold text-highlighted">{{ readText(invoice, ['invoiceNumber']) }}</p>
              <p class="text-xs text-muted">Inward {{ readText(invoice, ['inwardNumber']) }}</p>
            </td>
            <td class="border-b border-default p-3">{{ formatDate(readText(invoice, ['inwardDate', 'onDate'], '')) }}</td>
            <td class="border-b border-default p-3">
              <p class="max-w-48 truncate">{{ readText(invoice, ['vendorName']) }}</p>
              <p class="text-xs text-muted">{{ readText(invoice, ['vendorGstin']) }}</p>
            </td>
            <td class="border-b border-default p-3 text-right font-semibold">{{ money(readNumber(invoice, ['billAmount'])) }}</td>
            <td class="border-b border-default p-3 text-right">{{ money(readNumber(invoice, ['paidAmount'])) }}</td>
            <td class="border-b border-default p-3 text-right" :class="readNumber(invoice, ['balanceAmount']) > 0 ? 'text-warning' : ''">
              {{ money(readNumber(invoice, ['balanceAmount'])) }}
            </td>
            <td class="border-b border-default p-3">
              <UBadge size="xs" :color="statusColor(invoice)" variant="soft">{{ readText(invoice, ['invoiceStatus'], 'Pending') }}</UBadge>
            </td>
            <td class="border-b border-default p-3 text-right">
              <div class="flex flex-wrap justify-end gap-1">
                <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-eye" @click="viewInvoice(invoice)" />
                <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-pencil" :disabled="isCancelled(invoice)" @click="startEdit(invoice)" />
                <UButton size="xs" color="primary" variant="ghost" icon="i-lucide-hand-coins" :disabled="isCancelled(invoice) || readNumber(invoice, ['balanceAmount']) <= 0" @click="startPay(invoice)" />
                <UButton size="xs" color="warning" variant="ghost" icon="i-lucide-ban" :disabled="isCancelled(invoice)" @click="startCancel(invoice)" />
                <UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" @click="startDelete(invoice)" />
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p class="text-sm text-muted">
        Showing {{ pageStart }}-{{ pageEnd }} of {{ total }} invoice(s)
      </p>
      <div class="flex flex-wrap items-center gap-2">
        <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
        <UBadge color="neutral" variant="outline">Page {{ page }} / {{ totalPages }}</UBadge>
        <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
      </div>
    </div>

    <USlideover v-model:open="viewOpen" title="Purchase Receipt" :description="readText(selectedInvoice, ['invoiceNumber'])">
      <template #body>
        <div v-if="receiptLoading" class="py-8 text-center text-sm text-muted">Loading receipt...</div>
        <div v-else-if="receipt" class="space-y-5">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div><p class="text-xs text-muted">Vendor</p><p class="font-medium">{{ readText(receipt, ['vendorName']) }}</p></div>
            <div><p class="text-xs text-muted">GSTIN</p><p class="font-medium">{{ readText(receipt, ['vendorGstin'], '-') }}</p></div>
            <div><p class="text-xs text-muted">Bill Amount</p><p class="font-medium">{{ money(readNumber(receipt, ['billAmount'])) }}</p></div>
            <div><p class="text-xs text-muted">Balance</p><p class="font-medium">{{ money(readNumber(receipt, ['balanceAmount'])) }}</p></div>
          </div>

          <div>
            <h4 class="mb-2 text-sm font-semibold">Items ({{ readArray(receipt, ['items']).length }})</h4>
            <div class="max-h-64 space-y-2 overflow-auto pr-1">
              <div v-for="(item, index) in readArray(receipt, ['items'])" :key="index" class="rounded-md border border-default p-3 text-sm">
                <div class="flex justify-between gap-3">
                  <div class="min-w-0">
                    <p class="truncate font-medium">{{ readText(item, ['productName']) }}</p>
                    <p class="text-xs text-muted">{{ readText(item, ['barcode']) }} | Qty {{ readNumber(item, ['quantity']) }}</p>
                  </div>
                  <p class="font-semibold">{{ money(readNumber(item, ['amount'])) }}</p>
                </div>
              </div>
            </div>
          </div>

          <div>
            <h4 class="mb-2 text-sm font-semibold">Payments ({{ paymentRows.length }})</h4>
            <div v-if="!paymentRows.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">No payments recorded yet.</div>
            <div v-else class="space-y-2">
              <div v-for="(row, index) in paymentRows" :key="index" class="flex items-center justify-between rounded-md border border-default p-3 text-sm">
                <div>
                  <p class="font-medium">{{ row.mode }} <span class="text-xs text-muted">{{ row.reference }}</span></p>
                  <p class="text-xs text-muted">{{ row.date }}</p>
                </div>
                <p class="font-semibold">{{ row.amount }}</p>
              </div>
            </div>
          </div>

          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-file-down" size="sm" color="primary" variant="soft" :loading="downloading" @click="downloadInvoicePdf">Download PDF</UButton>
          </div>
        </div>
      </template>
    </USlideover>

    <UModal v-model:open="editOpen" title="Edit Purchase Invoice" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-2xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveEdit">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Invoice number</span>
            <UInput v-model="editForm.invoiceNumber" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Inward number</span>
            <UInput v-model="editForm.inwardNumber" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Inward date</span>
            <UInput v-model="editForm.inwardDate" type="date" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Supplier invoice date</span>
            <UInput v-model="editForm.supplierInvoiceDate" type="date" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Due date</span>
            <UInput v-model="editForm.dueDate" type="date" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Vendor name</span>
            <UInput v-model="editForm.vendorName" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Vendor GSTIN</span>
            <UInput v-model="editForm.vendorGstin" />
          </label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Update Invoice</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="payOpen" title="Record Vendor Payment" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="grid gap-3" @submit.prevent="savePayment">
          <p class="text-sm text-muted">
            {{ readText(selectedInvoice, ['invoiceNumber']) }} - Balance {{ money(readNumber(selectedInvoice, ['balanceAmount'])) }}
          </p>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Amount</span>
            <UInput v-model.number="payForm.amount" type="number" min="0" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Payment mode</span>
            <USelect v-model="payForm.paymentMode" :items="paymentModeItems" />
          </label>
          <label v-if="requiresBank(payForm.paymentMode)" class="space-y-1 text-sm">
            <span class="text-muted">Bank account</span>
            <USelect v-model="payForm.bankAccountId" :items="bankAccountItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Reference / slip number</span>
            <UInput v-model="payForm.slipNumber" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Remarks</span>
            <UTextarea v-model="payForm.remarks" :rows="2" />
          </label>
          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-hand-coins" color="primary" :loading="saving">Record Payment</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="cancelOpen" title="Cancel purchase invoice" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <p class="text-sm text-muted">
          Cancel <span class="font-medium text-highlighted">{{ readText(selectedInvoice, ['invoiceNumber']) }}</span>? This reverses stock and posts a purchase return.
        </p>
        <label class="mt-3 block space-y-1 text-sm">
          <span class="text-muted">Reason</span>
          <UTextarea v-model="cancelReason" :rows="2" placeholder="Reason for cancellation" />
        </label>
        <div class="mt-4 flex justify-end gap-2">
          <UButton color="neutral" variant="soft" @click="cancelOpen = false">Close</UButton>
          <UButton color="warning" variant="solid" icon="i-lucide-ban" :loading="saving" @click="confirmCancel">Cancel Invoice</UButton>
        </div>
      </template>
    </UModal>

    <UModal v-model:open="deleteOpen" title="Delete purchase invoice" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <p class="text-sm text-muted">
          Delete <span class="font-medium text-highlighted">{{ readText(selectedInvoice, ['invoiceNumber']) }}</span>? This cannot be undone.
        </p>
        <div class="mt-4 flex justify-end gap-2">
          <UButton color="neutral" variant="soft" @click="deleteOpen = false">Cancel</UButton>
          <UButton color="error" variant="solid" icon="i-lucide-trash-2" :loading="saving" @click="confirmDelete">Delete</UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Purchase - Garmetix Back Office' })

const { get, post, put, del, download } = useMainApiClient()

const statusOptions = [
  { value: 'all', label: 'All status' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Paid', label: 'Paid' },
  { value: 'PartiallyPaid', label: 'Partially paid' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Refunded', label: 'Refunded' },
  { value: 'Overdue', label: 'Overdue' }
]
const dateModeOptions = [
  { value: 'inward', label: 'Inward date' },
  { value: 'entry', label: 'Entry date' }
]
const datePresetOptions = [
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'custom', label: 'Custom' }
]
const pageSizeOptions = [
  { value: 25, label: '25' },
  { value: 50, label: '50' },
  { value: 100, label: '100' },
  { value: 200, label: '200' }
]
const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallets', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 },
  { label: 'Demand Draft', value: 8 }, { label: 'Others', value: 14 }
]

const loading = ref(false)
const saving = ref(false)
const receiptLoading = ref(false)
const downloading = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const statusFilter = ref('all')
const dateMode = ref('inward')
const datePreset = ref('month')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const serverSummary = reactive({ billAmount: 0, paidAmount: 0, freightAmount: 0, cancelledCount: 0 })
const invoices = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const selectedInvoice = ref<ApiRecord | null>(null)
const receipt = ref<ApiRecord | null>(null)

const viewOpen = ref(false)
const editOpen = ref(false)
const payOpen = ref(false)
const cancelOpen = ref(false)
const deleteOpen = ref(false)
const cancelReason = ref('')

const editForm = reactive({ invoiceNumber: '', inwardNumber: '', inwardDate: '', supplierInvoiceDate: '', dueDate: '', vendorName: '', vendorGstin: '' })
const payForm = reactive({ amount: 0, paymentMode: 0, bankAccountId: '', slipNumber: '', remarks: '' })

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const pageStart = computed(() => total.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(total.value, page.value * Number(pageSize.value || 50)))
const summaryCards = computed(() => [
  { label: 'Invoices', value: String(total.value), detail: 'Matching register filters' },
  { label: 'Bill amount', value: money(serverSummary.billAmount), detail: 'Server register total' },
  { label: 'Freight', value: money(serverSummary.freightAmount), detail: 'Freight billed in range' },
  { label: 'Cancelled', value: String(serverSummary.cancelledCount), detail: 'Matching filters' }
])
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountName', 'name'], 'Bank account'), value: readText(item, ['id'], '') })))
const paymentRows = computed(() => readArray(receipt.value, ['payments']).map(item => ({
  date: formatDate(item.onDate),
  amount: money(readNumber(item, ['amount'])),
  mode: readText(item, ['paymentMode']),
  reference: readText(item, ['referenceNumber'])
})))

function money(value: number) { return formatIndianMoney(value) }
function toInputDate(value: Date) {
  return `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`
}
function isCancelled(invoice: ApiRecord) { return readText(invoice, ['invoiceStatus'], '').toLowerCase() === 'cancelled' }
function statusColor(invoice: ApiRecord) {
  const value = readText(invoice, ['invoiceStatus'], '').toLowerCase()
  if (value.includes('cancel')) return 'error' as const
  if (value.includes('due') || value.includes('partial') || value.includes('pending')) return 'warning' as const
  return 'success' as const
}
function requiresBank(mode: number) { return ![0].includes(mode) }

function dateRangeFromPreset(): { from?: string, to?: string } {
  const now = new Date()
  if (datePreset.value === 'custom') return { from: customFromDate.value, to: customToDate.value }
  if (datePreset.value === 'last-month') {
    const start = new Date(now.getFullYear(), now.getMonth() - 1, 1)
    const end = new Date(now.getFullYear(), now.getMonth(), 0)
    return { from: toInputDate(start), to: toInputDate(end) }
  }
  if (datePreset.value === 'year') {
    return { from: toInputDate(new Date(now.getFullYear(), 0, 1)), to: toInputDate(new Date(now.getFullYear(), 11, 31)) }
  }
  return { from: toInputDate(new Date(now.getFullYear(), now.getMonth(), 1)), to: toInputDate(new Date(now.getFullYear(), now.getMonth() + 1, 0)) }
}

function buildQuery() {
  const range = dateRangeFromPreset()
  const query: Record<string, string | number> = { page: page.value, pageSize: pageSize.value, dateMode: dateMode.value }
  if (statusFilter.value !== 'all') query.status = statusFilter.value
  if (search.value.trim()) query.q = search.value.trim()
  if (range.from) query.from = range.from
  if (range.to) query.to = range.to
  return query
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord>('purchase/invoices', buildQuery())
    const rows = toRows(response, ['items'])
    invoices.value = rows
    total.value = readNumber(response, ['total']) || rows.length
    serverSummary.billAmount = readNumber(response, ['billAmount'])
    serverSummary.paidAmount = readNumber(response, ['paidAmount'])
    serverSummary.freightAmount = readNumber(response, ['freightAmount'])
    serverSummary.cancelledCount = readNumber(response, ['cancelledCount'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase invoices.'
  } finally {
    loading.value = false
  }
}

async function loadBankAccounts() {
  try {
    const data = await get<unknown>('bank-accounts')
    bankAccounts.value = toRows(data)
  } catch {
    bankAccounts.value = []
  }
}

async function applyFilters() {
  page.value = 1
  await refresh()
}

async function goToPage(nextPage: number) {
  page.value = Math.min(Math.max(1, nextPage), totalPages.value)
  await refresh()
}

async function clearSearch() {
  search.value = ''
  statusFilter.value = 'all'
  datePreset.value = 'month'
  page.value = 1
  await refresh()
}

async function viewInvoice(invoice: ApiRecord) {
  selectedInvoice.value = invoice
  viewOpen.value = true
  receipt.value = null
  receiptLoading.value = true
  const id = readText(invoice, ['id'], '')
  try {
    receipt.value = await get<ApiRecord>(`purchase/invoices/${id}/receipt`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase receipt.'
  } finally {
    receiptLoading.value = false
  }
}

async function downloadInvoicePdf() {
  const id = readText(selectedInvoice.value, ['id'], '')
  if (!id) return
  downloading.value = true
  error.value = ''
  try {
    await download(`purchase/invoices/${id}/pdf`, { format: 'a4' }, `${readText(selectedInvoice.value, ['invoiceNumber'], 'purchase-invoice')}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download purchase invoice.'
  } finally {
    downloading.value = false
  }
}

function startEdit(invoice: ApiRecord) {
  selectedInvoice.value = invoice
  Object.assign(editForm, {
    invoiceNumber: readText(invoice, ['invoiceNumber'], ''),
    inwardNumber: readText(invoice, ['inwardNumber'], ''),
    inwardDate: String(invoice.inwardDate || '').slice(0, 10),
    supplierInvoiceDate: String(invoice.supplierInvoiceDate || '').slice(0, 10),
    dueDate: String(invoice.dueDate || '').slice(0, 10),
    vendorName: readText(invoice, ['vendorName'], ''),
    vendorGstin: readText(invoice, ['vendorGstin'], '')
  })
  error.value = ''
  editOpen.value = true
}

async function saveEdit() {
  const id = readText(selectedInvoice.value, ['id'], '')
  if (!id) return
  saving.value = true
  error.value = ''
  try {
    await put<unknown>(`purchase/invoices/${id}`, {
      invoiceNumber: editForm.invoiceNumber || null,
      inwardNumber: editForm.inwardNumber || null,
      inwardDate: editForm.inwardDate || null,
      supplierInvoiceDate: editForm.supplierInvoiceDate || null,
      dueDate: editForm.dueDate || null,
      vendorName: editForm.vendorName || null,
      vendorGstin: editForm.vendorGstin || null
    })
    message.value = 'Purchase invoice updated.'
    editOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update purchase invoice.'
  } finally {
    saving.value = false
  }
}

function startPay(invoice: ApiRecord) {
  selectedInvoice.value = invoice
  Object.assign(payForm, { amount: readNumber(invoice, ['balanceAmount']), paymentMode: 0, bankAccountId: '', slipNumber: '', remarks: '' })
  error.value = ''
  payOpen.value = true
}

async function savePayment() {
  const id = readText(selectedInvoice.value, ['id'], '')
  if (!id) return
  if (!payForm.amount || payForm.amount <= 0) { error.value = 'Enter a valid amount.'; return }
  saving.value = true
  error.value = ''
  try {
    await post<unknown>(`purchase/invoices/${id}/payment-voucher`, {
      amount: payForm.amount,
      paymentMode: payForm.paymentMode,
      bankAccountId: payForm.bankAccountId || null,
      slipNumber: payForm.slipNumber || null,
      remarks: payForm.remarks || null
    })
    message.value = 'Vendor payment recorded.'
    payOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to record vendor payment.'
  } finally {
    saving.value = false
  }
}

function startCancel(invoice: ApiRecord) {
  selectedInvoice.value = invoice
  cancelReason.value = ''
  error.value = ''
  cancelOpen.value = true
}

async function confirmCancel() {
  const id = readText(selectedInvoice.value, ['id'], '')
  if (!id) return
  saving.value = true
  error.value = ''
  try {
    await post<unknown>(`purchase/invoices/${id}/cancel`, { reason: cancelReason.value || null })
    message.value = 'Purchase invoice cancelled.'
    cancelOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to cancel purchase invoice.'
  } finally {
    saving.value = false
  }
}

function startDelete(invoice: ApiRecord) {
  selectedInvoice.value = invoice
  error.value = ''
  deleteOpen.value = true
}

async function confirmDelete() {
  const id = readText(selectedInvoice.value, ['id'], '')
  if (!id) return
  saving.value = true
  error.value = ''
  try {
    await del<unknown>(`purchase/invoices/${id}`)
    message.value = 'Purchase invoice deleted.'
    deleteOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete purchase invoice.'
  } finally {
    saving.value = false
  }
}

watch([statusFilter, dateMode, datePreset, pageSize], () => { void applyFilters() })
watch([customFromDate, customToDate], () => { if (datePreset.value === 'custom') void applyFilters() })

onMounted(() => { void refresh(); void loadBankAccounts() })
</script>

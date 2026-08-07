<template>
  <section class="garmetix-page-stack" :aria-busy="loading || Boolean(receiptLoading)">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-undo-2" class="size-4" /> Return / exchange register</p>
          <h2 class="garmetix-dashboard-title">Sale Return / Exchange Register</h2>
          <p class="garmetix-dashboard-subtitle">
            Every completed return and exchange from this counter, with the original invoice and credit note linked.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/returns" icon="i-lucide-undo-2" color="neutral" variant="soft">New Return</UButton>
          <UButton to="/exchange" icon="i-lucide-repeat-2" color="neutral" variant="soft">New Exchange</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <PosToast :message="message" :color="messageTone" :icon="messageIcon" />

    <section>
      <div class="space-y-4">
        <div class="grid gap-3 md:grid-cols-4">
          <div v-for="item in summaryItems" :key="item.label" class="garmetix-metric-card">
            <p class="garmetix-metric-label">{{ item.label }}</p>
            <p class="garmetix-metric-value">{{ item.value }}</p>
            <p class="garmetix-metric-caption">{{ item.detail }}</p>
          </div>
        </div>

        <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_150px_150px_150px_auto]">
          <UFormField label="Search invoice, customer or mobile" name="returnsRegisterSearch">
            <UInput v-model="search" icon="i-lucide-search" placeholder="Invoice, customer, mobile" @keyup.enter="applyFilters" />
          </UFormField>
          <UFormField label="Type" name="returnsRegisterKind">
            <USelect v-model="kindFilter" :items="kindOptions" />
          </UFormField>
          <UFormField label="Date" name="returnsRegisterDatePreset">
            <USelect v-model="datePreset" :items="datePresetOptions" />
          </UFormField>
          <UFormField label="Rows" name="returnsRegisterPageSize">
            <USelect v-model="pageSize" :items="pageSizeOptions" />
          </UFormField>
          <div class="flex items-end gap-2">
            <UButton color="neutral" variant="soft" icon="i-lucide-list-filter" :loading="loading" @click="applyFilters">Find</UButton>
            <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="clearSearch">Clear</UButton>
          </div>
          <template v-if="datePreset === 'custom'">
            <UFormField label="From" name="returnsRegisterFromDate">
              <UInput v-model="customFromDate" type="date" />
            </UFormField>
            <UFormField label="To" name="returnsRegisterToDate">
              <UInput v-model="customToDate" type="date" />
            </UFormField>
          </template>
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[1080px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Invoice</th>
                <th class="border-b border-default p-3">Date</th>
                <th class="border-b border-default p-3">Type</th>
                <th class="border-b border-default p-3">Original Invoice</th>
                <th class="border-b border-default p-3">Customer</th>
                <th class="border-b border-default p-3">Credit Note</th>
                <th class="border-b border-default p-3 text-right">Amount</th>
                <th class="border-b border-default p-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!rows.length">
                <td colspan="8" class="p-8 text-center text-muted">No sale returns or exchanges matched the current filters.</td>
              </tr>
              <tr v-for="row in rows" :key="row.id" :class="selectedRow?.id === row.id ? 'bg-primary/5' : ''">
                <td class="border-b border-default p-3">
                  <button class="text-left font-semibold text-highlighted hover:underline" type="button" @click="selectRow(row)">
                    {{ row.invoiceNumber || 'Invoice' }}
                  </button>
                  <div class="mt-1 flex flex-wrap gap-1">
                    <UBadge size="xs" :color="statusColor(row.invoiceStatus)" variant="soft">{{ row.invoiceStatus || 'Saved' }}</UBadge>
                  </div>
                </td>
                <td class="border-b border-default p-3">{{ formatDateTime(row.onDate) }}</td>
                <td class="border-b border-default p-3">
                  <UBadge size="xs" :color="row.kind === 'Return' ? 'warning' : 'info'" variant="soft">{{ row.kind }}</UBadge>
                </td>
                <td class="border-b border-default p-3">{{ row.originalInvoiceNumber || '-' }}</td>
                <td class="border-b border-default p-3">
                  <span>{{ row.customerName || 'Walk-in Customer' }}</span>
                  <small class="block text-muted">{{ row.customerMobileNumber || '-' }}</small>
                </td>
                <td class="border-b border-default p-3">{{ row.creditNoteNumber || '-' }}</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ money(row.billAmount) }}</td>
                <td class="border-b border-default p-3 text-right">
                  <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-eye" :loading="receiptLoading === row.id" @click="selectRow(row)">View</UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p class="text-sm text-muted">
            Showing {{ pageStart }}-{{ pageEnd }} of {{ total }} record(s)
            <span v-if="serverDateRange">for {{ serverDateRange }}</span>
          </p>
          <div class="flex flex-wrap items-center gap-2">
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevrons-left" :disabled="page <= 1 || loading" @click="goToPage(1)">First</UButton>
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
            <UBadge color="neutral" variant="outline">Page {{ page }} / {{ totalPages }}</UBadge>
            <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
            <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevrons-right" :disabled="page >= totalPages || loading" @click="goToPage(totalPages)">Last</UButton>
          </div>
        </div>
      </div>
    </section>

    <USlideover v-model:open="detailOpen" title="Return / Exchange Detail" :description="selectedRow?.invoiceNumber || ''">
      <template #body>
        <div v-if="selectedRow" class="space-y-5">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p class="text-xs text-muted">Type</p>
              <p class="font-medium">{{ selectedRow.kind }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Original Invoice</p>
              <p class="font-medium">{{ selectedRow.originalInvoiceNumber || '-' }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Customer</p>
              <p class="font-medium">{{ selectedRow.customerName || 'Walk-in Customer' }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Credit Note</p>
              <p class="font-medium">{{ selectedRow.creditNoteNumber || '-' }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Amount</p>
              <p class="font-medium">{{ money(selectedRow.billAmount) }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Paid</p>
              <p class="font-medium">{{ money(selectedRow.paidAmount) }}</p>
            </div>
          </div>

          <div>
            <div class="mb-2 flex items-center justify-between">
              <h4 class="text-sm font-semibold">Items</h4>
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-refresh-cw" :loading="receiptLoading === selectedRow.id" @click="loadReceipt(selectedRow)">Reload</UButton>
            </div>
            <div class="max-h-72 space-y-2 overflow-auto pr-1">
              <div v-if="!receiptItems.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">
                No receipt items loaded.
              </div>
              <div v-for="(item, index) in receiptItems" :key="item.id || index" class="rounded-md border border-default p-3 text-sm">
                <div class="flex justify-between gap-3">
                  <div class="min-w-0">
                    <p class="truncate font-medium">{{ item.productName || item.name || 'Item' }}</p>
                    <p class="text-xs text-muted">{{ item.barcode || '-' }} | Qty {{ Number(item.quantity || 0) }}</p>
                  </div>
                  <p class="font-semibold">{{ money(lineValue(item)) }}</p>
                </div>
              </div>
            </div>
          </div>

          <div class="flex flex-wrap gap-2">
            <UButton color="neutral" variant="soft" icon="i-lucide-printer" :loading="printingId === selectedRow.id" @click="printInvoice(selectedRow)">Print</UButton>
          </div>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { normalizePosDocumentSearch, openBillingInvoicePdf } from '../utils/pos-documents'

useHead({ title: 'Sale Return / Exchange Register - Garmetix POS' })

interface RegisterRow {
  id: string
  invoiceNumber?: string
  onDate?: string
  kind?: string
  invoiceStatus?: string
  billAmount?: number
  paidAmount?: number
  originalInvoiceId?: string
  originalInvoiceNumber?: string
  customerName?: string
  customerMobileNumber?: string
  creditNoteNumber?: string
  reason?: string
}

interface PagedRegisterResponse {
  items?: RegisterRow[]
  total?: number
  page?: number
  pageSize?: number
  fromDate?: string
  toDate?: string
  returnCount?: number
  exchangeCount?: number
  totalCreditAmount?: number
}

const kindOptions = [
  { value: 'all', label: 'All types' },
  { value: 'Return', label: 'Return only' },
  { value: 'Exchange', label: 'Exchange only' }
]
const datePresetOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
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

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const receiptLoading = ref('')
const printingId = ref('')
const search = ref('')
const kindFilter = ref('all')
const datePreset = ref('month')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const serverFromDate = ref('')
const serverToDate = ref('')
const serverSummary = ref({ returnCount: 0, exchangeCount: 0, totalCreditAmount: 0 })
const rows = ref<RegisterRow[]>([])
const selectedRow = ref<RegisterRow | null>(null)
const detailOpen = ref(false)
const receiptItems = ref<any[]>([])
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const pageStart = computed(() => total.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(total.value, page.value * Number(pageSize.value || 50)))
const serverDateRange = computed(() => {
  if (!serverFromDate.value || !serverToDate.value) return ''
  return `${formatDate(serverFromDate.value)} to ${formatDate(serverToDate.value)}`
})
const summaryItems = computed(() => [
  { label: 'Total records', value: String(total.value), detail: 'Matching register filters' },
  { label: 'Returns', value: String(serverSummary.value.returnCount), detail: 'In current filter window' },
  { label: 'Exchanges', value: String(serverSummary.value.exchangeCount), detail: 'In current filter window' },
  { label: 'Credit issued', value: money(serverSummary.value.totalCreditAmount), detail: 'Sum of return amounts' }
])

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function toInputDate(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatDate(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function statusColor(status: string | null | undefined) {
  const value = String(status || '').toLowerCase()
  if (value.includes('cancel') || value.includes('void')) return 'error'
  if (value.includes('due') || value.includes('partial') || value.includes('pending')) return 'warning'
  return 'success'
}

function lineValue(item: any) {
  return Number(item.total || item.lineTotal || item.amount || 0)
    || (Number(item.quantity || 0) * Number(item.mrp || item.rate || 0)) - Number(item.discountAmount || 0)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function buildQuery() {
  const query = new URLSearchParams()
  query.set('page', String(page.value))
  query.set('pageSize', String(pageSize.value))
  query.set('datePreset', datePreset.value)
  if (kindFilter.value !== 'all') query.set('kind', kindFilter.value)
  const term = normalizePosDocumentSearch(search.value).trim()
  if (term) query.set('q', term)
  if (datePreset.value === 'custom') {
    if (customFromDate.value) query.set('from', customFromDate.value)
    if (customToDate.value) query.set('to', customToDate.value)
  }
  return query
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required to load the return/exchange register.')
    return
  }

  loading.value = true
  try {
    const response = await api.value.get<PagedRegisterResponse>(`billing/sales/returns-register?${buildQuery().toString()}`)
    const items = Array.isArray(response?.items) ? response.items : []
    rows.value = items
    total.value = Number(response?.total ?? items.length)
    if (response?.page) page.value = Number(response.page)
    if (response?.pageSize) pageSize.value = Number(response.pageSize)
    serverFromDate.value = response?.fromDate || ''
    serverToDate.value = response?.toDate || ''
    serverSummary.value = {
      returnCount: Number(response?.returnCount || 0),
      exchangeCount: Number(response?.exchangeCount || 0),
      totalCreditAmount: Number(response?.totalCreditAmount || 0)
    }
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load the return/exchange register.')
  } finally {
    loading.value = false
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

async function selectRow(row: RegisterRow) {
  selectedRow.value = row
  detailOpen.value = true
  await loadReceipt(row)
}

async function loadReceipt(row: RegisterRow | null) {
  if (!row?.id || receiptLoading.value) return
  receiptLoading.value = row.id
  try {
    const receipt = await api.value.get<any>(`billing/sales/${row.id}/receipt`)
    receiptItems.value = Array.isArray(receipt?.items) ? receipt.items : []
  } catch (error) {
    receiptItems.value = []
    showMessage('warning', error instanceof Error ? error.message : 'Could not load receipt items.')
  } finally {
    receiptLoading.value = ''
  }
}

async function printInvoice(row: RegisterRow) {
  if (!row?.id || printingId.value) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before printing.')
    return
  }

  printingId.value = row.id
  try {
    await openBillingInvoicePdf({
      apiBaseUrl: apiBaseUrl.value,
      invoiceId: row.id,
      token,
      reprint: true
    })
    showMessage('success', `${row.invoiceNumber || 'Document'} opened for printing.`)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not print.')
  } finally {
    printingId.value = ''
  }
}

async function clearSearch() {
  search.value = ''
  kindFilter.value = 'all'
  datePreset.value = 'month'
  page.value = 1
  message.value = ''
  await refresh()
}

watch([kindFilter, datePreset, pageSize], () => { void applyFilters() })
watch([customFromDate, customToDate], () => { if (datePreset.value === 'custom') void applyFilters() })

onMounted(() => {
  void refresh()
})
</script>

<template>
  <section class="garmetix-page-stack" :aria-busy="loading">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-repeat-2" class="size-4" /> Customer exchange counter</p>
          <h2 class="garmetix-dashboard-title">Sales Exchange</h2>
          <p class="garmetix-dashboard-subtitle">
            Scan the invoice QR or search for the original invoice, then press Exchange to open the exchange form on its own page.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-list-checks" to="/returns-register">Return / exchange register</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <PosToast :message="message" :color="messageTone" :icon="messageIcon" />

    <section>
      <div class="space-y-4">
        <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_150px_140px_auto]">
          <UFormField label="Invoice number / QR code / customer" name="invoiceSearch">
            <UInput
              ref="invoiceSearchInput"
              v-model="invoiceSearch"
              icon="i-lucide-search"
              placeholder="Scan QR, enter invoice number, mobile, or customer name"
              autofocus
              data-pos-exchange-search
              @keyup.enter="selectBestMatch"
            />
          </UFormField>
          <UFormField label="Status" name="exchangeStatusFilter">
            <USelect v-model="statusFilter" :items="statusOptions" />
          </UFormField>
          <UFormField label="Date" name="exchangeDatePreset">
            <USelect v-model="datePreset" :items="datePresetOptions" />
          </UFormField>
          <div class="flex items-end gap-2">
            <UButton icon="i-lucide-search" :loading="loading" @click="selectBestMatch">Find</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-scan-line" @click="focusInvoiceSearch">Scan</UButton>
          </div>
          <template v-if="datePreset === 'custom'">
            <UFormField label="From" name="exchangeFromDate">
              <UInput v-model="customFromDate" type="date" />
            </UFormField>
            <UFormField label="To" name="exchangeToDate">
              <UInput v-model="customToDate" type="date" />
            </UFormField>
          </template>
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[780px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Invoice</th>
                <th class="border-b border-default p-3">Date</th>
                <th class="border-b border-default p-3">Customer</th>
                <th class="border-b border-default p-3 text-right">Amount</th>
                <th class="border-b border-default p-3">Status</th>
                <th class="border-b border-default p-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!filteredInvoices.length">
                <td colspan="6" class="p-6 text-center text-muted">No exchangeable invoice matched the search.</td>
              </tr>
              <tr v-for="invoice in filteredInvoices" :key="invoice.id">
                <td class="border-b border-default p-3 font-semibold">{{ invoice.invoiceNumber || '-' }}</td>
                <td class="border-b border-default p-3">{{ formatDate(invoice.onDate) }}</td>
                <td class="border-b border-default p-3">
                  <span>{{ invoice.customerName || 'Walk-in Customer' }}</span>
                  <small class="block text-muted">{{ invoice.customerMobileNumber || '-' }}</small>
                </td>
                <td class="border-b border-default p-3 text-right">{{ money(invoice.billAmount) }}</td>
                <td class="border-b border-default p-3"><UBadge color="success" variant="soft">{{ invoice.invoiceStatus || 'Saved' }}</UBadge></td>
                <td class="border-b border-default p-3 text-right">
                  <UButton size="sm" icon="i-lucide-repeat-2" :to="exchangeFormLink(invoice)">Exchange</UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p class="text-sm text-muted">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalInvoices }} invoice(s)</p>
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
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { normalizePosDocumentSearch, textMatchesDocumentSearch } from '../utils/pos-documents'

useHead({ title: 'Sales Exchange - Garmetix POS' })

interface RecentInvoice {
  id: string
  invoiceNumber: string
  onDate: string
  customerName: string
  customerMobileNumber: string
  billAmount: number
  paidAmount: number
  balanceAmount: number
  invoiceStatus: string
  paymentMode: string
}

interface PagedSaleInvoicesResponse {
  items?: RecentInvoice[]
  total?: number
  page?: number
  pageSize?: number
}

const statusOptions = [
  { value: 'all', label: 'All sales' },
  { value: 'Paid', label: 'Paid' },
  { value: 'PartiallyPaid', label: 'Partially paid' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Overdue', label: 'Overdue' }
]

const datePresetOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'custom', label: 'Custom' }
]

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const invoiceSearch = ref('')
const invoiceSearchInput = ref<any>(null)
const invoices = ref<RecentInvoice[]>([])
const statusFilter = ref('all')
const datePreset = ref('today')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const filteredInvoices = computed(() => {
  const term = normalizePosDocumentSearch(invoiceSearch.value).toLowerCase()
  return invoices.value
    .filter(invoice => !['cancelled', 'refunded'].includes(String(invoice.invoiceStatus || '').toLowerCase()))
    .filter(invoice => !String(invoice.invoiceNumber || '').toUpperCase().startsWith('SR-'))
    .filter((invoice) => {
      if (!term) return true
      return [invoice.id, invoice.invoiceNumber, invoice.customerName, invoice.customerMobileNumber, invoice.invoiceStatus]
        .some(value => textMatchesDocumentSearch(value, term))
    })
})
const totalInvoices = computed(() => total.value || filteredInvoices.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalInvoices.value / Number(pageSize.value || 50))))
const pageStart = computed(() => totalInvoices.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(totalInvoices.value, page.value * Number(pageSize.value || 50)))

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
  return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat('en-IN').format(date)
}

// The exchange form lives on its own page; the list only picks the original invoice.
function exchangeFormLink(invoice: RecentInvoice) {
  return { path: '/exchange-new', query: { invoiceId: invoice.id } }
}

function buildInvoiceQuery() {
  const query = new URLSearchParams()
  query.set('page', String(page.value))
  query.set('pageSize', String(pageSize.value))
  query.set('datePreset', datePreset.value)
  if (statusFilter.value !== 'all') query.set('status', statusFilter.value)
  const term = normalizePosDocumentSearch(invoiceSearch.value).trim()
  if (term) query.set('q', term)
  if (datePreset.value === 'custom') {
    if (customFromDate.value) query.set('from', customFromDate.value)
    if (customToDate.value) query.set('to', customToDate.value)
  }
  return query
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function focusInvoiceSearch() {
  if (!import.meta.client) return
  void nextTick(() => {
    const input = invoiceSearchInput.value?.inputRef
      || invoiceSearchInput.value?.$el?.querySelector?.('input')
      || document.querySelector<HTMLInputElement>('[data-pos-exchange-search]')
    input?.focus?.()
    input?.select?.()
  })
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before loading sales exchanges.')
    return
  }

  loading.value = true
  try {
    const invoiceResponse = await api.value.get<PagedSaleInvoicesResponse>(`billing/sales?${buildInvoiceQuery().toString()}`)
    invoices.value = Array.isArray(invoiceResponse?.items) ? invoiceResponse.items : []
    total.value = Number(invoiceResponse?.total ?? invoices.value.length)
    if (invoiceResponse?.page) page.value = Number(invoiceResponse.page)
    if (invoiceResponse?.pageSize) pageSize.value = Number(invoiceResponse.pageSize)
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load exchangeable invoices.')
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

async function selectBestMatch() {
  invoiceSearch.value = normalizePosDocumentSearch(invoiceSearch.value)
  if (!filteredInvoices.value.length) {
    showMessage('warning', 'No invoice matched the search.')
    return
  }
  await navigateTo(exchangeFormLink(filteredInvoices.value[0]))
}

function handleExchangeShortcut(event: KeyboardEvent) {
  if (event.defaultPrevented) return
  if (event.key === 'F2') {
    event.preventDefault()
    focusInvoiceSearch()
  }
  if (event.key === 'Escape' && invoiceSearch.value) {
    event.preventDefault()
    invoiceSearch.value = ''
    focusInvoiceSearch()
  }
}

watch([statusFilter, datePreset, pageSize], () => {
  void applyFilters()
})

watch([customFromDate, customToDate], () => {
  if (datePreset.value === 'custom') void applyFilters()
})

onMounted(() => {
  window.addEventListener('keydown', handleExchangeShortcut)
  void refresh()
  focusInvoiceSearch()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleExchangeShortcut)
})
</script>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-rotate-ccw" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">Purchase Return</h2>
          <p class="garmetix-dashboard-subtitle">
            Return supplier stock against a purchase invoice, and review posted return/debit-note history.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-shield-check" color="neutral" variant="soft" to="/purchase-return/advanced-settlement">Settlement QA</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Find Purchase Invoice To Return</h3>
      <div class="flex flex-col gap-2 sm:flex-row">
        <UInput v-model="invoiceSearch" icon="i-lucide-search" placeholder="Invoice number, inward number or vendor" class="flex-1" @keyup.enter="searchInvoices" />
        <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="searching" @click="searchInvoices">Search</UButton>
      </div>
      <div v-if="invoiceResults.length" class="mt-3 space-y-2">
        <div
          v-for="invoice in invoiceResults"
          :key="readText(invoice, ['id'])"
          class="flex flex-wrap items-center justify-between gap-2 rounded-md border border-default p-3 text-sm"
        >
          <div>
            <p class="font-medium">{{ readText(invoice, ['invoiceNumber']) }} - {{ readText(invoice, ['vendorName']) }}</p>
            <p class="text-xs text-muted">Bill {{ money(readNumber(invoice, ['billAmount'])) }} - {{ formatDate(invoice.inwardDate) }}</p>
          </div>
          <UButton size="xs" color="primary" variant="soft" icon="i-lucide-rotate-ccw" :disabled="isCancelled(invoice)" @click="loadReturnable(invoice)">Return Items</UButton>
        </div>
      </div>
    </section>

    <section v-if="returnable" class="garmetix-section-card">
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 class="garmetix-panel-title">Return Items - {{ readText(returnable, ['invoiceNumber']) }}</h3>
          <p class="garmetix-panel-subtitle">{{ readText(returnable, ['vendorName']) }} - Balance {{ money(readNumber(returnable, ['balanceAmount'])) }}</p>
        </div>
        <div class="flex gap-2">
          <UButton size="xs" color="neutral" variant="soft" @click="returnAllAvailable">Return All Available</UButton>
          <UButton size="xs" color="neutral" variant="ghost" @click="clearReturnQuantities">Clear</UButton>
        </div>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[820px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2 font-medium">Product</th>
                <th class="px-3 py-2 text-right font-medium">Purchased</th>
                <th class="px-3 py-2 text-right font-medium">Already Returned</th>
                <th class="px-3 py-2 text-right font-medium">Returnable</th>
                <th class="px-3 py-2 text-right font-medium">Return Qty</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="item in returnableItems" :key="readText(item, ['itemId'])">
                <td class="px-3 py-2">{{ readText(item, ['productName']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['purchasedQuantity']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['alreadyReturnedQuantity']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['returnableQuantity']) }}</td>
                <td class="px-3 py-2 text-right">
                  <UInput
                    v-model.number="returnQuantities[readText(item, ['itemId'])]"
                    type="number"
                    min="0"
                    :max="readNumber(item, ['returnableQuantity'])"
                    step="0.01"
                    class="w-28 ml-auto"
                  />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="mt-3 grid gap-3 sm:grid-cols-3">
        <UFormField label="Return date">
          <UInput v-model="returnDate" type="date" />
        </UFormField>
        <UFormField label="Reason" class="sm:col-span-2">
          <UInput v-model="returnReason" placeholder="Reason for return" />
        </UFormField>
      </div>
      <div class="mt-3 flex justify-end gap-2">
        <UButton color="neutral" variant="soft" @click="returnable = null">Cancel</UButton>
        <UButton color="primary" variant="solid" icon="i-lucide-rotate-ccw" :loading="submitting" @click="submitReturn">Submit Return</UButton>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Purchase Return Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredReturns.length }} of {{ returns.length }} return(s)</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="statusFilter" :items="statusFilterItems" class="sm:w-40" />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search return, vendor, debit note" class="sm:w-72" />
        </div>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[960px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Return No.</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Original Invoice</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Amount</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Debit Note</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Settlement</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="pagedReturns.length === 0">
                <td colspan="8" class="px-3 py-8 text-center text-muted">No purchase returns found.</td>
              </tr>
              <tr v-for="item in pagedReturns" :key="readText(item, ['id'])" class="bg-default/40">
                <td class="whitespace-nowrap px-3 py-2">{{ formatDate(item.onDate) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['returnNumber']) }}</td>
                <td class="max-w-44 truncate px-3 py-2">{{ readText(item, ['vendorName']) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['originalInvoiceNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                <td class="max-w-36 truncate px-3 py-2">{{ readText(item, ['debitNoteNumber']) }}</td>
                <td class="px-3 py-2"><UBadge :color="settlementColor(item.settlementStatus)" variant="subtle">{{ readText(item, ['settlementStatus']) }}</UBadge></td>
                <td class="px-3 py-2">
                  <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" @click="viewReturn(item)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="filteredReturns.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ page }} of {{ totalPages }} - {{ filteredReturns.length }} return(s)</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page = Math.max(1, page - 1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page = Math.min(totalPages, page + 1)">Next</UButton>
        </div>
      </div>
    </section>

    <USlideover v-model:open="detailOpen" title="Purchase Return Detail" :description="readText(selectedReturn, ['returnNumber'])">
      <template #body>
        <div v-if="detailLoading" class="py-8 text-center text-sm text-muted">Loading return detail...</div>
        <div v-else-if="selectedReturn" class="space-y-4">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div><p class="text-xs text-muted">Vendor</p><p class="font-medium">{{ readText(selectedReturn, ['vendorName']) }}</p></div>
            <div><p class="text-xs text-muted">Original invoice</p><p class="font-medium">{{ readText(selectedReturn, ['originalInvoiceNumber']) }}</p></div>
            <div><p class="text-xs text-muted">Return amount</p><p class="font-medium">{{ money(readNumber(selectedReturn, ['returnAmount'])) }}</p></div>
            <div><p class="text-xs text-muted">Debit note</p><p class="font-medium">{{ readText(selectedReturn, ['debitNoteNumber']) }}</p></div>
          </div>
          <div>
            <h4 class="mb-2 text-sm font-semibold">Items</h4>
            <div class="space-y-2">
              <div v-for="(item, index) in readArray(selectedReturn, ['items'])" :key="index" class="rounded-md border border-default p-3 text-sm">
                <div class="flex justify-between gap-3">
                  <p class="min-w-0 truncate font-medium">{{ readText(item, ['productName']) }}</p>
                  <p class="font-semibold">{{ money(readNumber(item, ['returnAmount'])) }}</p>
                </div>
                <p class="text-xs text-muted">Qty {{ readNumber(item, ['returnedQuantity']) }} - {{ readText(item, ['reason'], 'No reason noted') }}</p>
              </div>
            </div>
          </div>
          <UButton icon="i-lucide-file-down" size="sm" color="primary" variant="soft" :loading="downloading" @click="downloadReturnPdf">Download PDF</UButton>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Purchase Return - Garmetix Back Office' })

const { get, post, download } = useMainApiClient()

const statusFilterItems = [
  { label: 'All Settlements', value: 'all' },
  { label: 'Open', value: 'Open' },
  { label: 'Partially Settled', value: 'PartiallySettled' },
  { label: 'Settled', value: 'Settled' }
]

const loading = ref(true)
const searching = ref(false)
const submitting = ref(false)
const detailLoading = ref(false)
const downloading = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const statusFilter = ref('all')
const invoiceSearch = ref('')
const page = ref(1)
const pageSize = 25

const returns = ref<ApiRecord[]>([])
const invoiceResults = ref<ApiRecord[]>([])
const returnable = ref<ApiRecord | null>(null)
const returnQuantities = reactive<Record<string, number>>({})
const returnReason = ref('')
const returnDate = ref(new Date().toISOString().slice(0, 10))
const selectedReturn = ref<ApiRecord | null>(null)
const detailOpen = ref(false)

const returnableItems = computed(() => readArray(returnable.value, ['items']))
const filteredReturns = computed(() => {
  const term = search.value.trim().toLowerCase()
  return returns.value.filter(item => {
    const statusMatches = statusFilter.value === 'all' || readText(item, ['settlementStatus']) === statusFilter.value
    const textMatches = !term || [
      readText(item, ['returnNumber']),
      readText(item, ['vendorName']),
      readText(item, ['originalInvoiceNumber']),
      readText(item, ['debitNoteNumber'])
    ].join(' ').toLowerCase().includes(term)
    return statusMatches && textMatches
  })
})
const totalPages = computed(() => Math.max(1, Math.ceil(filteredReturns.value.length / pageSize)))
const pagedReturns = computed(() => {
  const start = (page.value - 1) * pageSize
  return filteredReturns.value.slice(start, start + pageSize)
})

watch([search, statusFilter], () => { page.value = 1 })

function money(value: unknown) { return formatIndianMoney(readNumber({ value }, ['value'])) }
function isCancelled(invoice: ApiRecord) { return readText(invoice, ['invoiceStatus'], '').toLowerCase() === 'cancelled' }
function settlementColor(status: unknown) {
  const value = String(status ?? '').toLowerCase()
  if (value.includes('settled') && !value.includes('partial')) return 'success' as const
  if (value.includes('partial')) return 'warning' as const
  return 'neutral' as const
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const data = await get<unknown>('purchase/returns/recent', { take: 150 })
    returns.value = toRows(data)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase returns.'
  } finally {
    loading.value = false
  }
}

async function searchInvoices() {
  searching.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord>('purchase/invoices', { q: invoiceSearch.value.trim() || undefined, pageSize: 15, page: 1 })
    invoiceResults.value = toRows(response, ['items'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to search purchase invoices.'
  } finally {
    searching.value = false
  }
}

async function loadReturnable(invoice: ApiRecord) {
  error.value = ''
  const id = readText(invoice, ['id'], '')
  if (!id) return
  try {
    returnable.value = await get<ApiRecord>(`purchase/invoices/${id}/returnable`)
    Object.keys(returnQuantities).forEach(key => delete returnQuantities[key])
    returnReason.value = ''
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load returnable items.'
  }
}

function returnAllAvailable() {
  for (const item of returnableItems.value) {
    const id = readText(item, ['itemId'], '')
    if (id) returnQuantities[id] = readNumber(item, ['returnableQuantity'])
  }
}

function clearReturnQuantities() {
  Object.keys(returnQuantities).forEach(key => { returnQuantities[key] = 0 })
}

async function submitReturn() {
  const invoiceId = readText(returnable.value, ['id'], '')
  if (!invoiceId) return
  const items = Object.entries(returnQuantities)
    .filter(([, qty]) => Number(qty) > 0)
    .map(([itemId, qty]) => ({ itemId, quantity: Number(qty) }))
  if (!items.length) {
    error.value = 'Enter at least one return quantity.'
    return
  }
  submitting.value = true
  error.value = ''
  try {
    await post<unknown>(`purchase/invoices/${invoiceId}/partial-return`, {
      items,
      reason: returnReason.value || null,
      returnDate: returnDate.value || null
    })
    message.value = 'Purchase return posted.'
    returnable.value = null
    invoiceResults.value = []
    invoiceSearch.value = ''
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to submit purchase return.'
  } finally {
    submitting.value = false
  }
}

async function viewReturn(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  selectedReturn.value = item
  detailOpen.value = true
  detailLoading.value = true
  error.value = ''
  try {
    selectedReturn.value = await get<ApiRecord>(`purchase/returns/${id}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load purchase return detail.'
  } finally {
    detailLoading.value = false
  }
}

async function downloadReturnPdf() {
  const id = readText(selectedReturn.value, ['id'], '')
  if (!id) return
  downloading.value = true
  error.value = ''
  try {
    await download(`purchase/returns/${id}/pdf`, {}, `${readText(selectedReturn.value, ['returnNumber'], 'purchase-return')}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download purchase return.'
  } finally {
    downloading.value = false
  }
}

onMounted(refresh)
</script>

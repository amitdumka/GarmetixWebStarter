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
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="focusNewReturn">New Return</UButton>
          <UButton icon="i-lucide-shield-check" color="neutral" variant="soft" to="/purchase-return/advanced-settlement">Settlement QA</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section ref="newReturnSectionRef" class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">New Return - Find Purchase Invoice</h3>
      <p class="garmetix-panel-subtitle mb-3">Search for the original purchase invoice to return items against - this is how a new purchase return is added.</p>
      <div class="flex flex-col gap-2 sm:flex-row">
        <UInput ref="invoiceSearchInputRef" v-model="invoiceSearch" icon="i-lucide-search" placeholder="Invoice number, inward number or vendor" class="flex-1" @keyup.enter="searchInvoices" />
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

      <div class="mb-3 flex flex-col gap-2 sm:flex-row">
        <UInput
          v-model="barcodeScan"
          icon="i-lucide-scan-barcode"
          placeholder="Scan or type barcode to jump to a line"
          class="flex-1"
          @keyup.enter="scanBarcode"
        />
        <UButton icon="i-lucide-search" color="neutral" variant="soft" @click="scanBarcode">Find Line</UButton>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[960px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2 text-right font-medium">#</th>
                <th class="px-3 py-2 font-medium">Product</th>
                <th class="px-3 py-2 text-right font-medium">Purchased</th>
                <th class="px-3 py-2 text-right font-medium">Already Returned</th>
                <th class="px-3 py-2 text-right font-medium">Returnable</th>
                <th class="px-3 py-2 text-right font-medium">Current Stock</th>
                <th class="px-3 py-2 text-right font-medium">Return Qty</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr
                v-for="item in returnableItems"
                :key="readText(item, ['itemId'])"
                :ref="el => setLineRef(readText(item, ['itemId']), el)"
                :class="highlightedItemId === readText(item, ['itemId']) ? 'bg-primary/10' : ''"
              >
                <td class="px-3 py-2 text-right text-muted">{{ readNumber(item, ['rowNumber']) }}</td>
                <td class="px-3 py-2">{{ readText(item, ['productName']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['purchasedQuantity']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['alreadyReturnedQuantity']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['returnableQuantity']) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(item, ['currentStockQuantity']) }}</td>
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

      <div class="mt-3 grid gap-3 sm:grid-cols-2">
        <UFormField label="Transport details" class="sm:col-span-2">
          <UTextarea v-model="transportDetails" :rows="2" placeholder="Courier/transporter, LR number, vehicle, etc." />
        </UFormField>
        <UFormField label="Freight amount">
          <UInput v-model.number="freightAmount" type="number" min="0" step="0.01" />
        </UFormField>
        <UFormField label="Freight borne by">
          <USelect v-model="freightBearer" :items="freightBearerItems" @update:model-value="onFreightBearerChange" />
        </UFormField>
        <UFormField v-if="freightBearer === 'InHouse'" label="Freight issued by" class="sm:col-span-2">
          <USelect v-model="freightEmployeeId" :items="employeeItems" placeholder="Select employee" />
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
                <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="pagedReturns.length === 0">
                <td colspan="9" class="px-3 py-8 text-center text-muted">No purchase returns found.</td>
              </tr>
              <tr v-for="item in pagedReturns" :key="readText(item, ['id'])" class="bg-default/40">
                <td class="whitespace-nowrap px-3 py-2">{{ formatDate(item.onDate) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['returnNumber']) }}</td>
                <td class="max-w-44 truncate px-3 py-2">{{ readText(item, ['vendorName']) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['originalInvoiceNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                <td class="max-w-36 truncate px-3 py-2">{{ readText(item, ['debitNoteNumber']) }}</td>
                <td class="px-3 py-2"><UBadge :color="settlementColor(item.settlementStatus)" variant="subtle">{{ readText(item, ['settlementStatus']) }}</UBadge></td>
                <td class="px-3 py-2"><UBadge :color="readText(item, ['status']) === 'Cancelled' ? 'neutral' : 'success'" variant="subtle">{{ readText(item, ['status'], 'Posted') }}</UBadge></td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap justify-end gap-1">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" @click="viewReturn(item)" />
                    <UButton
                      v-if="canModifyReturn(item)"
                      icon="i-lucide-pencil"
                      size="xs"
                      color="neutral"
                      variant="ghost"
                      :loading="editingReturnId === readText(item, ['id'])"
                      @click="startEditReturn(item)"
                    />
                    <UButton
                      v-if="canModifyReturn(item)"
                      icon="i-lucide-trash-2"
                      size="xs"
                      color="error"
                      variant="ghost"
                      :loading="deletingReturnId === readText(item, ['id'])"
                      @click="askDeleteReturn(item)"
                    />
                  </div>
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
          <UBadge v-if="readText(selectedReturn, ['status'], 'Posted') === 'Cancelled'" color="neutral" variant="subtle">Cancelled - reversed, no longer active</UBadge>
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div><p class="text-xs text-muted">Vendor</p><p class="font-medium">{{ readText(selectedReturn, ['vendorName']) }}</p></div>
            <div><p class="text-xs text-muted">Original invoice</p><p class="font-medium">{{ readText(selectedReturn, ['originalInvoiceNumber']) }} ({{ formatDate(selectedReturn?.originalInvoiceDate) }}, {{ readNumber(selectedReturn, ['daysOld']) }}d old)</p></div>
            <div><p class="text-xs text-muted">Return amount</p><p class="font-medium">{{ money(readNumber(selectedReturn, ['returnAmount'])) }}</p></div>
            <div><p class="text-xs text-muted">Debit note</p><p class="font-medium">{{ readText(selectedReturn, ['debitNoteNumber']) }}</p></div>
          </div>
          <div v-if="readText(selectedReturn, ['transportDetails'], '') || readNumber(selectedReturn, ['freightAmount'])" class="rounded-md border border-default p-3 text-sm">
            <p v-if="readText(selectedReturn, ['transportDetails'], '')" class="text-muted">Transport: {{ readText(selectedReturn, ['transportDetails']) }}</p>
            <p v-if="readNumber(selectedReturn, ['freightAmount'])">
              Freight: {{ money(readNumber(selectedReturn, ['freightAmount'])) }} -
              {{ readText(selectedReturn, ['freightBearer']) === 'Vendor' ? 'billed to vendor (in debit note)' : `in-house expense${readText(selectedReturn, ['freightExpenseVoucherNumber'], '') ? ' - voucher ' + readText(selectedReturn, ['freightExpenseVoucherNumber']) : ''}` }}
            </p>
          </div>
          <div>
            <h4 class="mb-2 text-sm font-semibold">Items</h4>
            <div class="space-y-2">
              <div v-for="(item, index) in readArray(selectedReturn, ['items'])" :key="index" class="rounded-md border border-default p-3 text-sm">
                <div class="flex justify-between gap-3">
                  <p class="min-w-0 truncate font-medium">#{{ readNumber(item, ['rowNumber']) }} {{ readText(item, ['productName']) }}</p>
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
import { formatIndianMoney, isActiveEmployee } from '@garmetix/shared-utils'
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
const editingReturnId = ref('')
const deletingReturnId = ref('')

const returns = ref<ApiRecord[]>([])
const invoiceResults = ref<ApiRecord[]>([])
const returnable = ref<ApiRecord | null>(null)
const returnQuantities = reactive<Record<string, number>>({})
const returnReason = ref('')
const returnDate = ref(new Date().toISOString().slice(0, 10))
const selectedReturn = ref<ApiRecord | null>(null)
const detailOpen = ref(false)

const barcodeScan = ref('')
const highlightedItemId = ref('')
const lineRefs: Record<string, HTMLElement> = {}
const newReturnSectionRef = ref<HTMLElement | null>(null)
const invoiceSearchInputRef = ref<{ inputRef?: HTMLInputElement } | HTMLElement | null>(null)

const transportDetails = ref('')
const freightAmount = ref(0)
const freightBearer = ref<'' | 'Vendor' | 'InHouse'>('')
const freightEmployeeId = ref('')
const employees = ref<ApiRecord[]>([])
const employeesLoaded = ref(false)

const freightBearerItems = [
  { label: 'No freight', value: '' },
  { label: 'Vendor (billed to debit note)', value: 'Vendor' },
  { label: 'In-house (our expense)', value: 'InHouse' }
]
const employeeItems = computed(() => employees.value
  .filter(item => isActiveEmployee(item))
  .map(item => ({ label: readText(item, ['name'], 'Employee'), value: readText(item, ['id'], '') })))

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

function focusNewReturn() {
  newReturnSectionRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  const el = invoiceSearchInputRef.value
  const input = el && 'inputRef' in el ? el.inputRef : (el as HTMLElement | null)
  if (input instanceof HTMLInputElement) input.focus()
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
    barcodeScan.value = ''
    highlightedItemId.value = ''
    transportDetails.value = ''
    freightAmount.value = 0
    freightBearer.value = ''
    freightEmployeeId.value = ''
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load returnable items.'
  }
}

function setLineRef(itemId: string, el: unknown) {
  if (el instanceof HTMLElement) lineRefs[itemId] = el
}

async function loadEmployees() {
  if (employeesLoaded.value) return
  try {
    const data = await get<unknown>('employees')
    employees.value = toRows(data)
    employeesLoaded.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load employees.'
  }
}

function onFreightBearerChange(value: string) {
  if (value === 'InHouse') void loadEmployees()
}

async function scanBarcode() {
  const code = barcodeScan.value.trim()
  if (!code) return
  error.value = ''
  const match = returnableItems.value.find(item => readText(item, ['barcode'], '').toLowerCase() === code.toLowerCase())
  if (!match) {
    error.value = `No returnable line found for barcode ${code}.`
    return
  }
  const itemId = readText(match, ['itemId'], '')
  highlightedItemId.value = itemId
  if (!returnQuantities[itemId]) returnQuantities[itemId] = 1
  lineRefs[itemId]?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  barcodeScan.value = ''
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
  if (freightAmount.value > 0 && !freightBearer.value) {
    error.value = 'Select who bears the freight cost before adding a freight amount.'
    return
  }
  if (freightAmount.value > 0 && freightBearer.value === 'InHouse' && !freightEmployeeId.value) {
    error.value = 'Select who is issuing the in-house freight expense.'
    return
  }
  submitting.value = true
  error.value = ''
  try {
    await post<unknown>(`purchase/invoices/${invoiceId}/partial-return`, {
      items,
      reason: returnReason.value || null,
      returnDate: returnDate.value || null,
      transportDetails: transportDetails.value.trim() || null,
      freightAmount: freightAmount.value > 0 ? freightAmount.value : null,
      freightBearer: freightAmount.value > 0 ? freightBearer.value : null,
      freightEmployeeId: freightAmount.value > 0 && freightBearer.value === 'InHouse' ? freightEmployeeId.value : null
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

function canModifyReturn(item: ApiRecord) {
  return readText(item, ['status'], 'Posted') !== 'Cancelled' && readNumber(item, ['settledAmount']) <= 0
}

async function startEditReturn(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  if (!window.confirm(`Editing will immediately reverse posted return ${readText(item, ['returnNumber'])} (undo its stock movement, void its debit note, reverse any GST ITC/freight postings) so you can re-enter it with corrected quantities. The original return cannot be restored once reversed. Continue?`)) return

  editingReturnId.value = id
  error.value = ''
  try {
    const oldDetail = await get<ApiRecord>(`purchase/returns/${id}`)
    const invoiceId = readText(oldDetail, ['purchaseInvoiceId'], '')
    if (!invoiceId) throw new Error('Original purchase invoice reference was not found on this return.')

    await post<unknown>(`purchase/returns/${id}/reverse`, { reason: 'Reversed for edit', hardDelete: false })
    message.value = `Return ${readText(oldDetail, ['returnNumber'])} reversed. Review and submit the corrected return below.`

    await loadReturnable({ id: invoiceId })
    for (const oldItem of readArray(oldDetail, ['items'])) {
      const originalItemId = readText(oldItem, ['purchaseInvoiceItemId'], '')
      if (originalItemId) returnQuantities[originalItemId] = readNumber(oldItem, ['returnedQuantity'])
    }
    returnReason.value = readText(oldDetail, ['reason'], '')
    transportDetails.value = readText(oldDetail, ['transportDetails'], '')
    freightAmount.value = readNumber(oldDetail, ['freightAmount'])
    const oldBearer = readText(oldDetail, ['freightBearer'], '')
    freightBearer.value = oldBearer === 'Vendor' || oldBearer === 'InHouse' ? oldBearer : ''
    if (freightBearer.value === 'InHouse') await loadEmployees()

    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to start editing this return.'
  } finally {
    editingReturnId.value = ''
  }
}

function askDeleteReturn(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  const phrase = window.prompt(`This permanently reverses purchase return ${readText(item, ['returnNumber'])} - undoing its stock movement, voiding its debit note ${readText(item, ['debitNoteNumber'])}, and reversing any GST ITC/freight postings - then removes it. This cannot be undone. Type DELETE RETURN to confirm.`)
  if (phrase !== 'DELETE RETURN') return
  void deleteReturn(id, readText(item, ['returnNumber'], ''))
}

async function deleteReturn(id: string, returnNumber: string) {
  deletingReturnId.value = id
  error.value = ''
  try {
    await post<unknown>(`purchase/returns/${id}/reverse`, { reason: 'Deleted by operator', hardDelete: true })
    message.value = `Purchase return ${returnNumber} reversed and deleted.`
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete purchase return.'
  } finally {
    deletingReturnId.value = ''
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

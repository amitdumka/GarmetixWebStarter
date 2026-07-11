<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-package-minus" class="size-4" /> Purchase</p>
          <h2 class="garmetix-dashboard-title">New Goods Return</h2>
          <p class="garmetix-dashboard-subtitle">
            Pick a vendor, then scan a barcode or search a product name to pull rate, quantity and discount straight
            from that vendor's purchase history - across any of their invoices - and post one consolidated return.
          </p>
        </div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="soft" to="/purchase-return">Back to Purchase Return</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <UFormField label="Vendor" class="lg:col-span-2">
        <USelectMenu
          v-model="selectedVendorId"
          :items="vendorItems"
          value-key="value"
          searchable
          placeholder="Select vendor"
          @update:model-value="onVendorSelected"
        />
      </UFormField>
      <UFormField label="Return Date">
        <UInput v-model="returnDate" type="date" />
      </UFormField>
      <div></div>

      <UFormField label="Barcode / product name" class="lg:col-span-2">
        <UInput
          v-model="searchTerm"
          icon="i-lucide-scan-barcode"
          placeholder="Scan barcode or type product name"
          :disabled="!selectedVendorId"
          @keyup.enter="runSearch"
        />
      </UFormField>
      <div class="flex items-end">
        <UButton icon="i-lucide-search" color="primary" variant="soft" class="w-full" :loading="searching" :disabled="!selectedVendorId" @click="runSearch">
          Search Purchase History
        </UButton>
      </div>
      <div></div>
    </section>

    <section v-if="searchAttempted" class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Matching Purchase History</h3>
      <div v-if="searchResults.length === 0" class="rounded-md border border-dashed border-default p-6 text-center text-sm text-muted">
        No returnable items matched "{{ searchTerm }}" for this vendor in your current store.
      </div>
      <div v-else class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1000px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2 font-medium">Product</th>
                <th class="px-3 py-2 font-medium">Invoice</th>
                <th class="px-3 py-2 text-right font-medium">Purchased</th>
                <th class="px-3 py-2 text-right font-medium">Returned</th>
                <th class="px-3 py-2 text-right font-medium">Returnable</th>
                <th class="px-3 py-2 text-right font-medium">Current Stock</th>
                <th class="px-3 py-2 text-right font-medium">Rate</th>
                <th class="px-3 py-2 text-right font-medium">Tax %</th>
                <th class="px-3 py-2 text-right font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="row in searchResults" :key="readText(row, ['purchaseInvoiceItemId'])">
                <td class="px-3 py-2">
                  <p class="font-medium">{{ readText(row, ['productName']) }}</p>
                  <p class="text-xs text-muted">{{ readText(row, ['barcode']) }} | {{ readText(row, ['hsnCode'], 'No HSN') }}</p>
                </td>
                <td class="px-3 py-2">
                  <p>{{ readText(row, ['invoiceNumber']) }}</p>
                  <p class="text-xs text-muted">{{ formatDate(row.invoiceDate) }} | Row #{{ readNumber(row, ['rowNumber']) }}</p>
                </td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['purchasedQuantity']).toFixed(2) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['alreadyReturnedQuantity']).toFixed(2) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['returnableQuantity']).toFixed(2) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['currentStockQuantity']).toFixed(2) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(row, ['unitAmount'])) }}</td>
                <td class="px-3 py-2 text-right">{{ readNumber(row, ['taxPercentage']).toFixed(2) }}</td>
                <td class="px-3 py-2 text-right">
                  <UButton
                    size="xs"
                    color="primary"
                    variant="soft"
                    icon="i-lucide-plus"
                    :disabled="readNumber(row, ['returnableQuantity']) <= 0 || isInCart(row)"
                    @click="addToCart(row)"
                  >
                    {{ isInCart(row) ? 'Added' : 'Add' }}
                  </UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[980px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Product</th>
            <th class="border-b border-default p-3">Invoice</th>
            <th class="border-b border-default p-3 text-right">Return Qty</th>
            <th class="border-b border-default p-3 text-right">Rate</th>
            <th class="border-b border-default p-3 text-right">Tax %</th>
            <th class="border-b border-default p-3 text-right">Line Total</th>
            <th class="border-b border-default p-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!cart.length">
            <td colspan="7" class="p-8 text-center text-muted">No items added yet. Search the vendor's purchase history above.</td>
          </tr>
          <tr v-for="(item, index) in cart" :key="item.purchaseInvoiceItemId">
            <td class="border-b border-default p-3">
              <p class="font-medium">{{ item.productName }}</p>
              <p class="text-xs text-muted">{{ item.barcode }}</p>
            </td>
            <td class="border-b border-default p-3">{{ item.invoiceNumber }}</td>
            <td class="border-b border-default p-3 text-right">
              <UInput
                v-model.number="item.returnQuantity"
                type="number"
                min="0"
                :max="item.returnableQuantity"
                step="0.01"
                class="w-28 ml-auto"
              />
            </td>
            <td class="border-b border-default p-3 text-right">{{ money(item.unitAmount) }}</td>
            <td class="border-b border-default p-3 text-right">{{ item.taxPercentage.toFixed(2) }}</td>
            <td class="border-b border-default p-3 text-right font-semibold">{{ money(lineTotal(item)) }}</td>
            <td class="border-b border-default p-3 text-right">
              <UButton icon="i-lucide-x" size="xs" color="error" variant="ghost" @click="cart.splice(index, 1)" />
            </td>
          </tr>
        </tbody>
        <tfoot v-if="cart.length">
          <tr>
            <td colspan="5" class="p-3 text-right font-semibold">Total</td>
            <td class="p-3 text-right font-semibold">{{ money(cartTotal) }}</td>
            <td></td>
          </tr>
        </tfoot>
      </table>
    </section>

    <section class="garmetix-section-card grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <UFormField label="Reason" class="lg:col-span-2">
        <UTextarea v-model="reason" :rows="2" />
      </UFormField>
      <UFormField label="Transport Details">
        <UInput v-model="transportDetails" placeholder="Courier, LR number, vehicle etc." />
      </UFormField>
      <div></div>

      <UFormField label="Freight Amount">
        <UInput v-model.number="freightAmount" type="number" min="0" step="0.01" />
      </UFormField>
      <UFormField label="Freight Borne By">
        <USelect v-model="freightBearer" :items="freightBearerItems" :disabled="!freightAmount" />
      </UFormField>
      <UFormField v-if="freightAmount > 0 && freightBearer === 'InHouse'" label="Issued By (Employee)">
        <USelect v-model="freightEmployeeId" :items="employeeItems" @focus="ensureEmployeesLoaded" />
      </UFormField>
      <div class="flex items-end text-xs text-muted">
        <p v-if="freightAmount > 0 && freightBearer === 'Vendor'">
          Billed to vendor: freight {{ money(freightAmount) }} + 5% GST {{ money(freightGstAmount) }} = <strong class="text-highlighted">{{ money(freightAmount + freightGstAmount) }}</strong> added to the debit note.
        </p>
        <p v-else-if="freightAmount > 0 && freightBearer === 'InHouse'">
          Booked as an in-house Expense voucher at the actual amount {{ money(freightAmount) }}, no GST added.
        </p>
      </div>

      <div class="sm:col-span-2 lg:col-span-4 flex justify-end">
        <UButton icon="i-lucide-save" color="primary" size="lg" :loading="saving" :disabled="!cart.length" @click="submitGoodsReturn">
          Save Goods Return ({{ money(cartTotal) }})
        </UButton>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Goods Returns</h3>
          <p class="garmetix-panel-subtitle">{{ goodsReturns.length }} return(s) created from this page</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="registerLoading" @click="refreshRegister">Refresh</UButton>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1080px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Return No.</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Invoices</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Qty</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Amount</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="goodsReturns.length === 0">
                <td colspan="8" class="px-3 py-8 text-center text-muted">No goods returns created from this page yet.</td>
              </tr>
              <tr v-for="item in goodsReturns" :key="readText(item, ['id'])" class="bg-default/40">
                <td class="max-w-40 truncate px-3 py-2 font-medium">{{ readText(item, ['returnNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ formatDate(item.onDate) }}</td>
                <td class="max-w-44 truncate px-3 py-2">{{ readText(item, ['vendorName']) }}</td>
                <td class="max-w-48 truncate px-3 py-2">{{ readText(item, ['originalInvoiceNumber']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ readNumber(item, ['quantity']).toFixed(2) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                <td class="px-3 py-2"><UBadge :color="readText(item, ['status'], 'Posted') === 'Posted' ? 'success' : 'neutral'" variant="subtle">{{ readText(item, ['status'], 'Posted') }}</UBadge></td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap justify-end gap-1">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" @click="openDetail(item)" />
                    <UButton
                      icon="i-lucide-pencil"
                      size="xs"
                      color="warning"
                      variant="ghost"
                      :disabled="readText(item, ['status'], 'Posted') !== 'Posted' || readNumber(item, ['settledAmount']) > 0"
                      @click="editReturn(item)"
                    />
                    <UButton
                      icon="i-lucide-trash-2"
                      size="xs"
                      color="error"
                      variant="ghost"
                      :disabled="readText(item, ['status'], 'Posted') !== 'Posted' || readNumber(item, ['settledAmount']) > 0"
                      @click="deleteReturn(item)"
                    />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <UModal v-model:open="detailOpen" title="Goods Return Details" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-4xl' }">
      <template #body>
        <div v-if="detailLoading" class="py-10 text-center text-sm text-muted">Loading goods return...</div>
        <div v-else-if="selectedReturn" class="space-y-4">
          <UAlert
            color="neutral"
            variant="subtle"
            icon="i-lucide-file-check-2"
            :title="readText(selectedReturn, ['returnNumber'])"
            :description="`${readText(selectedReturn, ['vendorName'])} | Invoices: ${(readArray(selectedReturn, ['originalInvoiceNumbers']).length ? readArray(selectedReturn, ['originalInvoiceNumbers']).join(', ') : readText(selectedReturn, ['originalInvoiceNumber']))} | Debit note ${readText(selectedReturn, ['debitNoteNumber'], 'not linked')}`"
          />

          <div class="grid grid-cols-2 gap-3 rounded-lg border border-default bg-muted/20 p-3 text-sm sm:grid-cols-4">
            <div><p class="text-muted">Return date</p><p class="font-semibold">{{ formatDate(selectedReturn?.onDate) }}</p></div>
            <div><p class="text-muted">Status</p><p class="font-semibold">{{ readText(selectedReturn, ['status'], 'Posted') }}</p></div>
            <div><p class="text-muted">Quantity</p><p class="font-semibold">{{ readNumber(selectedReturn, ['quantity']).toFixed(2) }}</p></div>
            <div><p class="text-muted">Return amount</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['returnAmount'])) }}</p></div>
            <div><p class="text-muted">Freight</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['freightAmount'])) }}</p></div>
            <div><p class="text-muted">Freight GST</p><p class="font-semibold">{{ money(readNumber(selectedReturn, ['freightTaxAmount'])) }}</p></div>
            <div><p class="text-muted">Freight borne by</p><p class="font-semibold">{{ readText(selectedReturn, ['freightBearer'], 'Not applicable') }}</p></div>
            <div><p class="text-muted">Freight voucher</p><p class="font-semibold">{{ readText(selectedReturn, ['freightExpenseVoucherNumber'], 'Not applicable') }}</p></div>
          </div>

          <div class="overflow-hidden rounded-lg border border-default">
            <div class="overflow-x-auto">
              <table class="w-full min-w-[720px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted">
                  <tr>
                    <th class="px-3 py-2 font-medium">Item</th>
                    <th class="px-3 py-2 text-right font-medium">Returned</th>
                    <th class="px-3 py-2 text-right font-medium">Rate</th>
                    <th class="px-3 py-2 text-right font-medium">Tax</th>
                    <th class="px-3 py-2 text-right font-medium">Amount</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="item in readArray(selectedReturn, ['items'])" :key="readText(item, ['id'])">
                    <td class="px-3 py-2">
                      <p class="font-medium">{{ readText(item, ['productName']) }}</p>
                      <p class="text-xs text-muted">{{ readText(item, ['barcode']) }}</p>
                    </td>
                    <td class="px-3 py-2 text-right">{{ readNumber(item, ['returnedQuantity']).toFixed(2) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['unitRate'])) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['taxAmount'])) }}</td>
                    <td class="px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <UAlert color="info" variant="subtle" title="Return reason" :description="readText(selectedReturn, ['reason'], 'No reason recorded.')" />
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end">
          <UButton color="neutral" variant="outline" @click="detailOpen = false">Close</UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, isActiveEmployee } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

interface CartItem {
  purchaseInvoiceItemId: string
  purchaseInvoiceId: string
  invoiceNumber: string
  productName: string
  barcode: string
  unitAmount: number
  taxPercentage: number
  returnableQuantity: number
  returnQuantity: number
}

useHead({ title: 'New Goods Return - Garmetix Back Office' })

const { get, post } = useMainApiClient()

const freightBearerItems = [
  { label: 'Select who bears freight', value: null },
  { label: 'Vendor (added to debit note + 5% GST)', value: 'Vendor' },
  { label: 'In-house (Expense voucher, no GST)', value: 'InHouse' }
]

function localDateInput(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

const error = ref('')
const message = ref('')
const saving = ref(false)
const searching = ref(false)
const searchAttempted = ref(false)
const registerLoading = ref(false)
const detailLoading = ref(false)

const vendors = ref<ApiRecord[]>([])
const employees = ref<ApiRecord[]>([])
const employeesLoaded = ref(false)
const goodsReturns = ref<ApiRecord[]>([])

const selectedVendorId = ref<string | null>(null)
const searchTerm = ref('')
const searchResults = ref<ApiRecord[]>([])
const cart = ref<CartItem[]>([])

const reason = ref('Goods return')
const returnDate = ref(localDateInput())
const transportDetails = ref('')
const freightAmount = ref(0)
const freightBearer = ref<string | null>(null)
const freightEmployeeId = ref<string | null>(null)

const detailOpen = ref(false)
const selectedReturn = ref<ApiRecord | null>(null)

const vendorItems = computed(() => vendors.value.map(item => ({
  label: `${readText(item, ['name'])} (${readText(item, ['gSTIN', 'GSTIN'], 'No GSTIN')})`,
  value: readText(item, ['id'], '')
})))
const employeeItems = computed(() => [
  { label: 'Select employee', value: null },
  ...employees.value.filter(isActiveEmployee).map(item => ({ label: readText(item, ['name'], 'Employee'), value: readText(item, ['id'], '') }))
])

const cartTotal = computed(() => cart.value.reduce((sum, item) => sum + lineTotal(item), 0))
const freightGstAmount = computed(() => freightBearer.value === 'Vendor' ? Math.round(freightAmount.value * 0.05 * 100) / 100 : 0)

function money(value: number) { return formatIndianMoney(value) }
function lineTotal(item: CartItem) { return Math.max(item.returnQuantity, 0) * item.unitAmount }
function isInCart(row: ApiRecord) {
  const id = readText(row, ['purchaseInvoiceItemId'], '')
  return cart.value.some(item => item.purchaseInvoiceItemId === id)
}

function onVendorSelected() {
  searchResults.value = []
  searchAttempted.value = false
  searchTerm.value = ''
}

async function runSearch() {
  if (!selectedVendorId.value || !searchTerm.value.trim()) return
  searching.value = true
  error.value = ''
  searchAttempted.value = true
  try {
    const response = await get<unknown>(`purchase/vendors/${selectedVendorId.value}/returnable-items`, { query: searchTerm.value.trim() })
    searchResults.value = toRows(response)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not search vendor purchase history.'
    searchResults.value = []
  } finally {
    searching.value = false
  }
}

function addToCart(row: ApiRecord, quantity?: number) {
  if (isInCart(row)) return
  const returnable = readNumber(row, ['returnableQuantity'])
  const requested = quantity ?? Math.min(1, returnable)
  cart.value.push({
    purchaseInvoiceItemId: readText(row, ['purchaseInvoiceItemId'], ''),
    purchaseInvoiceId: readText(row, ['purchaseInvoiceId'], ''),
    invoiceNumber: readText(row, ['invoiceNumber'], ''),
    productName: readText(row, ['productName'], ''),
    barcode: readText(row, ['barcode'], ''),
    unitAmount: readNumber(row, ['unitAmount']),
    taxPercentage: readNumber(row, ['taxPercentage']),
    returnableQuantity: returnable,
    returnQuantity: Math.min(Math.max(requested, 0), returnable)
  })
}

async function ensureEmployeesLoaded() {
  if (employeesLoaded.value) return
  employeesLoaded.value = true
  try {
    const response = await get<unknown>('employees')
    employees.value = toRows(response)
  } catch {
    employees.value = []
  }
}

function resetForm() {
  cart.value = []
  searchResults.value = []
  searchAttempted.value = false
  searchTerm.value = ''
  reason.value = 'Goods return'
  returnDate.value = localDateInput()
  transportDetails.value = ''
  freightAmount.value = 0
  freightBearer.value = null
  freightEmployeeId.value = null
}

async function refresh() {
  error.value = ''
  try {
    const lookup = await get<ApiRecord>('purchase/lookup-options')
    vendors.value = readArray(lookup, ['vendors'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendors.'
  }
  await refreshRegister()
}

async function refreshRegister() {
  registerLoading.value = true
  try {
    const response = await get<unknown>('purchase/returns/recent')
    goodsReturns.value = toRows(response).filter(item => readText(item, ['returnKind'], '') === 'GoodsReturn')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load goods returns.'
  } finally {
    registerLoading.value = false
  }
}

async function submitGoodsReturn() {
  error.value = ''
  message.value = ''
  if (!selectedVendorId.value) {
    error.value = 'Select a vendor before saving.'
    return
  }
  if (!cart.value.length) {
    error.value = 'Add at least one item.'
    return
  }
  if (cart.value.some(item => !item.returnQuantity || item.returnQuantity <= 0)) {
    error.value = 'Enter a valid return quantity for every item.'
    return
  }
  if (freightAmount.value > 0 && !freightBearer.value) {
    error.value = 'Select who bears the freight cost.'
    return
  }
  if (freightAmount.value > 0 && freightBearer.value === 'InHouse' && !freightEmployeeId.value) {
    error.value = 'Select the employee issuing the in-house freight expense.'
    return
  }

  saving.value = true
  try {
    const response = await post<ApiRecord>(`purchase/vendors/${selectedVendorId.value}/goods-return`, {
      items: cart.value.map(item => ({ itemId: item.purchaseInvoiceItemId, quantity: item.returnQuantity })),
      reason: reason.value || 'Goods return',
      returnDate: returnDate.value ? `${returnDate.value}T00:00:00` : null,
      transportDetails: transportDetails.value.trim() || null,
      freightAmount: freightAmount.value || 0,
      freightBearer: freightAmount.value > 0 ? freightBearer.value : null,
      freightEmployeeId: freightAmount.value > 0 && freightBearer.value === 'InHouse' ? freightEmployeeId.value : null
    })
    message.value = `${readText(response, ['returnNumber'], 'Goods return')} posted with debit note ${readText(response, ['debitNoteNumber'], '')} for ${money(readNumber(response, ['returnAmount']))}.`
    resetForm()
    await refreshRegister()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not create goods return.'
  } finally {
    saving.value = false
  }
}

async function openDetail(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  selectedReturn.value = item
  detailOpen.value = true
  detailLoading.value = true
  error.value = ''
  try {
    selectedReturn.value = await get<ApiRecord>(`purchase/returns/${id}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not load goods return.'
    detailOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

async function editReturn(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  if (!window.confirm('Editing a Goods Return fully reverses its stock movement, debit note, GST ITC reversal and any freight voucher, then reopens this form pre-filled so you can correct and resubmit as a fresh return. Continue?')) {
    return
  }
  error.value = ''
  try {
    const detail = await get<ApiRecord>(`purchase/returns/${id}`)
    const oldItems = readArray(detail, ['items'])
    const vendorId = readText(detail, ['vendorId'], '')

    await post(`purchase/returns/${id}/reverse`, { reason: 'Edited via New Goods Return page', hardDelete: false })
    message.value = `${readText(detail, ['returnNumber'])} reversed. Review the re-loaded items below and save to post the corrected return.`
    await refreshRegister()

    resetForm()
    selectedVendorId.value = vendorId

    for (const oldItem of oldItems) {
      const barcode = readText(oldItem, ['barcode'], '')
      const invoiceItemId = readText(oldItem, ['purchaseInvoiceItemId'], '')
      if (!barcode || !invoiceItemId) continue
      try {
        const matchResponse = await get<unknown>(`purchase/vendors/${vendorId}/returnable-items`, { query: barcode })
        const match = toRows(matchResponse).find(row => readText(row, ['purchaseInvoiceItemId'], '') === invoiceItemId)
        if (match) {
          addToCart(match, Math.min(readNumber(oldItem, ['returnedQuantity']), readNumber(match, ['returnableQuantity'])))
        }
      } catch {
        // Skip items that can no longer be located; operator can re-add manually.
      }
    }

    reason.value = readText(detail, ['reason'], 'Goods return')
    transportDetails.value = readText(detail, ['transportDetails'], '')
    freightAmount.value = readNumber(detail, ['freightAmount'])
    freightBearer.value = readText(detail, ['freightBearer'], '') || null
    if (freightBearer.value === 'InHouse') await ensureEmployeesLoaded()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not reverse this return for editing.'
  }
}

async function deleteReturn(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  const confirmation = window.prompt(`This permanently deletes ${readText(item, ['returnNumber'])} after reversing its stock, debit note, GST ITC and any freight voucher. Type DELETE RETURN to confirm.`)
  if (confirmation !== 'DELETE RETURN') return
  error.value = ''
  try {
    await post(`purchase/returns/${id}/reverse`, { reason: 'Deleted via New Goods Return page', hardDelete: true })
    message.value = `${readText(item, ['returnNumber'])} was reversed and permanently deleted.`
    await refreshRegister()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not delete this return.'
  }
}

onMounted(refresh)
</script>

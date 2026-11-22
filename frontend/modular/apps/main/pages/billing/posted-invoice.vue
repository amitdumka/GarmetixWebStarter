<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-plus" class="size-4" /> Back-dated sale entry</p>
          <h2 class="garmetix-dashboard-title">Posted Invoice Entry</h2>
          <p class="garmetix-dashboard-subtitle">
            Enter a sale that was missed on its real day. The invoice, stock movement, payments and accounting are all posted on the date you choose.
          </p>
        </div>
        <UButton color="neutral" variant="soft" icon="i-lucide-list" @click="router.push('/billing')">Sale Invoices</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="created" color="success" variant="subtle" icon="i-lucide-circle-check"
      :title="`Invoice ${created.invoiceNumber} posted`"
      :description="`Dated ${createdDate}. Bill ${money(created.billAmount)}, paid ${money(created.paidAmount)}. You can find it in Sale Invoices.`" />

    <div class="garmetix-section-card grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <UFormField label="Store">
        <USelect v-model="form.storeId" :items="storeItems" class="w-full" />
      </UFormField>
      <UFormField label="Sale date" hint="Date the sale really happened">
        <UInput v-model="form.saleDate" type="date" :max="today" class="w-full" />
      </UFormField>
      <UFormField label="Salesman">
        <USelect v-model="form.salesmanId" :items="salesmanItems" placeholder="Select salesman" class="w-full" />
      </UFormField>
      <UFormField label="Customer mobile" hint="Leave blank for walk-in">
        <UInput v-model="form.customerMobile" placeholder="10 digit mobile" class="w-full" @blur="lookupCustomer" @keydown.enter.prevent="lookupCustomer" />
      </UFormField>
      <UFormField label="Customer name">
        <UInput v-model="form.customerName" class="w-full" />
      </UFormField>
      <UFormField label="Customer GSTIN (optional)">
        <UInput v-model="form.customerGstin" class="w-full" />
      </UFormField>
      <div v-if="customerNote" class="md:col-span-2 text-sm text-muted self-end">{{ customerNote }}</div>
    </div>

    <div class="garmetix-section-card grid gap-3">
      <div class="flex flex-wrap items-end gap-2">
        <UFormField label="Add item (barcode or name)" class="min-w-[260px] flex-1">
          <UInput v-model="itemSearch" placeholder="Scan barcode or type product name" class="w-full" @keydown.enter.prevent="addFromSearch" @input="suggest" />
        </UFormField>
        <UButton icon="i-lucide-plus" :loading="adding" @click="addFromSearch">Add</UButton>
      </div>
      <div v-if="suggestions.length" class="flex flex-wrap gap-2">
        <UButton v-for="item in suggestions" :key="item.productId + item.barcode" size="xs" color="neutral" variant="soft" @click="addItem(item)">
          {{ item.name }} | {{ item.barcode }} | {{ money(Number(item.mrp || 0)) }} | stock {{ item.availableQty }}
        </UButton>
      </div>

      <div class="overflow-x-auto">
        <table class="w-full min-w-[760px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr><th class="border-b border-default p-2">Item</th><th class="border-b border-default p-2 w-24">Qty</th><th class="border-b border-default p-2 w-28">MRP</th><th class="border-b border-default p-2 w-28">Discount / unit</th><th class="border-b border-default p-2 text-right">Line</th><th class="border-b border-default p-2 w-12"></th></tr>
          </thead>
          <tbody>
            <tr v-if="!lines.length"><td colspan="6" class="p-6 text-center text-muted">No items yet. Scan or search to add.</td></tr>
            <tr v-for="(line, index) in lines" :key="line.key">
              <td class="border-b border-default p-2"><p class="font-medium">{{ line.name }}</p><p class="text-xs text-muted">{{ line.barcode }} | in stock {{ line.availableQty }}</p></td>
              <td class="border-b border-default p-2"><UInput v-model.number="line.quantity" type="number" min="0" step="1" size="sm" /></td>
              <td class="border-b border-default p-2"><UInput v-model.number="line.mrp" type="number" min="0" step="0.01" size="sm" /></td>
              <td class="border-b border-default p-2"><UInput v-model.number="line.discountAmount" type="number" min="0" step="0.01" size="sm" /></td>
              <td class="border-b border-default p-2 text-right">{{ money((Number(line.mrp) - Number(line.discountAmount)) * Number(line.quantity)) }}</td>
              <td class="border-b border-default p-2"><UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" @click="lines.splice(index, 1)" /></td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="grid gap-3 sm:grid-cols-3">
        <UFormField label="Bill discount (whole bill)"><UInput v-model.number="form.billDiscount" type="number" min="0" step="0.01" class="w-full" /></UFormField>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Bill amount</p><p class="text-xl font-semibold">{{ money(billAmount) }}</p></div>
        <div class="rounded-md border border-default p-3"><p class="text-xs uppercase text-muted">Balance after payments</p><p class="text-xl font-semibold" :class="balance < 0 ? 'text-error' : balance > 0 ? 'text-warning' : ''">{{ money(balance) }}</p></div>
      </div>
    </div>

    <div class="garmetix-section-card grid gap-2">
      <div class="flex items-center justify-between"><h4 class="text-sm font-semibold">Payments</h4><UButton size="xs" icon="i-lucide-plus" color="neutral" variant="soft" @click="addPayment">Add payment</UButton></div>
      <div class="overflow-x-auto">
        <table class="w-full min-w-[640px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr><th class="border-b border-default p-2 w-36">Mode</th><th class="border-b border-default p-2 w-28">Amount</th><th class="border-b border-default p-2">Bank account</th><th class="border-b border-default p-2">Reference</th><th class="border-b border-default p-2 w-12"></th></tr>
          </thead>
          <tbody>
            <tr v-if="!payments.length"><td colspan="5" class="p-4 text-center text-muted">No payment - the bill will be left unpaid (credit sale).</td></tr>
            <tr v-for="(payment, index) in payments" :key="payment.key">
              <td class="border-b border-default p-2"><USelect v-model="payment.paymentMode" :items="paymentModeItems" size="sm" class="w-full" /></td>
              <td class="border-b border-default p-2"><UInput v-model.number="payment.amount" type="number" min="0" step="0.01" size="sm" /></td>
              <td class="border-b border-default p-2"><USelect v-if="payment.paymentMode !== 0" v-model="payment.bankAccountId" :items="bankItems" placeholder="Select account" size="sm" class="w-full" /><span v-else class="text-xs text-muted">Cash in hand</span></td>
              <td class="border-b border-default p-2"><UInput v-model="payment.referenceNumber" size="sm" /></td>
              <td class="border-b border-default p-2"><UButton size="xs" color="error" variant="ghost" icon="i-lucide-trash-2" @click="payments.splice(index, 1)" /></td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="flex flex-wrap items-center gap-3 pt-2">
        <UCheckbox v-model="confirmed" :label="`I confirm this sale really happened on ${form.saleDate || 'the chosen date'} and was not billed before.`" />
        <UButton icon="i-lucide-save" :loading="saving" :disabled="!canSave" @click="submit">Post Invoice</UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-rotate-ccw" @click="reset">Clear</UButton>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Posted Invoice Entry - Garmetix Back Office' })

const router = useRouter()
const { get, post } = useMainApiClient()
const money = (value: number) => formatIndianMoney(value)

type Line = { key: string, productId: string, barcode: string, name: string, quantity: number, mrp: number, discountAmount: number, availableQty: number }
type Payment = { key: string, paymentMode: number, amount: number, bankAccountId: string, referenceNumber: string }

const today = new Date().toISOString().slice(0, 10)
const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10)
const stores = ref<ApiRecord[]>([])
const salesmen = ref<ApiRecord[]>([])
const banks = ref<ApiRecord[]>([])
const lines = ref<Line[]>([])
const payments = ref<Payment[]>([])
const suggestions = ref<ApiRecord[]>([])
const itemSearch = ref('')
const adding = ref(false)
const saving = ref(false)
const confirmed = ref(false)
const error = ref('')
const customerNote = ref('')
const created = ref<{ invoiceNumber: string, billAmount: number, paidAmount: number } | null>(null)
const createdDate = ref('')
const form = reactive({ storeId: '', saleDate: yesterday, salesmanId: '', customerMobile: '', customerName: '', customerGstin: '', billDiscount: 0 })
let keySeed = 0
const nextKey = () => `k${++keySeed}`

const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallet', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 }
]
const storeItems = computed(() => stores.value.map(item => ({ label: readText(item, ['name', 'storeCode'], 'Store'), value: String(item.id) })))
const salesmanItems = computed(() => salesmen.value.map(item => ({ label: readText(item, ['name'], 'Salesman'), value: String(item.id) })))
const bankItems = computed(() => banks.value.map(item => ({ label: `${readText(item, ['accountHolderName', 'name'], 'Bank account')} - ${readText(item, ['accountNumber'], '')}`, value: String(item.id) })))
const selectedStore = computed(() => stores.value.find(item => String(item.id) === form.storeId) || null)

const grossMrp = computed(() => lines.value.reduce((sum, line) => sum + Number(line.mrp || 0) * Number(line.quantity || 0), 0))
const itemDiscount = computed(() => lines.value.reduce((sum, line) => sum + Number(line.discountAmount || 0) * Number(line.quantity || 0), 0))
const billAmount = computed(() => Math.round(grossMrp.value - itemDiscount.value - Number(form.billDiscount || 0)))
const paidTotal = computed(() => payments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0))
const balance = computed(() => billAmount.value - paidTotal.value)
const canSave = computed(() => Boolean(form.storeId && form.saleDate && form.salesmanId)
  && lines.value.length > 0
  && lines.value.every(line => Number(line.quantity) > 0 && Number(line.mrp) >= 0 && Number(line.discountAmount) >= 0)
  && balance.value >= 0 && billAmount.value > 0
  && payments.value.every(item => Number(item.amount) > 0 && (item.paymentMode === 0 || Boolean(item.bankAccountId)))
  && confirmed.value && !saving.value)

function addPayment() {
  payments.value.push({ key: nextKey(), paymentMode: 0, amount: Math.max(balance.value, 0), bankAccountId: '', referenceNumber: '' })
}

async function loadStoreData() {
  if (!form.storeId) return
  const [options, bankRows] = await Promise.all([
    get<ApiRecord>(`billing/options?storeId=${form.storeId}&take=1`).catch(() => ({ salesmen: [] })),
    get<unknown>('bank-accounts').catch(() => [])
  ])
  salesmen.value = (options.salesmen as ApiRecord[]) || []
  banks.value = toRows(bankRows)
  if (!salesmen.value.some(item => String(item.id) === form.salesmanId)) form.salesmanId = salesmen.value[0] ? String(salesmen.value[0].id) : ''
}

async function lookupCustomer() {
  const mobile = form.customerMobile.trim()
  customerNote.value = ''
  if (!/^\d{10}$/.test(mobile)) return
  const company = readText(selectedStore.value, ['companyId'], '')
  const rows = toRows(await get<unknown>(`billing/customers/search?q=${encodeURIComponent(mobile)}&take=5${company ? `&companyId=${company}` : ''}`).catch(() => []))
  const match = rows.find(item => readText(item, ['mobileNumber'], '') === mobile)
  if (match) {
    form.customerName = readText(match, ['name'], form.customerName)
    if (!form.customerGstin) form.customerGstin = readText(match, ['gstin'], '')
    customerNote.value = `Existing customer: ${readText(match, ['name'])} (${readNumber(match, ['billCount'])} bill(s)).`
  } else {
    customerNote.value = 'New customer - will be created from the name above.'
  }
}

let suggestTimer: ReturnType<typeof setTimeout> | undefined
function suggest() {
  clearTimeout(suggestTimer)
  const term = itemSearch.value.trim()
  if (term.length < 3 || !form.storeId) { suggestions.value = []; return }
  suggestTimer = setTimeout(async () => {
    suggestions.value = toRows(await get<unknown>(`product-lookup?query=${encodeURIComponent(term)}&storeId=${form.storeId}&take=8`).catch(() => []))
  }, 300)
}

function addItem(item: ApiRecord) {
  const barcode = readText(item, ['barcode'], '')
  const existing = lines.value.find(line => line.barcode === barcode)
  if (existing) {
    existing.quantity = Number(existing.quantity || 0) + 1
  } else {
    lines.value.push({ key: nextKey(), productId: readText(item, ['productId']), barcode, name: readText(item, ['name'], barcode), quantity: 1, mrp: readNumber(item, ['mrp']), discountAmount: 0, availableQty: readNumber(item, ['availableQty']) })
  }
  itemSearch.value = ''
  suggestions.value = []
}

async function addFromSearch() {
  const term = itemSearch.value.trim()
  if (!term || !form.storeId) return
  adding.value = true
  error.value = ''
  try {
    try {
      addItem(await get<ApiRecord>(`product-lookup/barcode/${encodeURIComponent(term)}?storeId=${form.storeId}`))
    } catch {
      const rows = toRows(await get<unknown>(`product-lookup?query=${encodeURIComponent(term)}&storeId=${form.storeId}&take=8`))
      if (rows.length === 1) addItem(rows[0]!)
      else if (rows.length) suggestions.value = rows
      else error.value = `No product found for "${term}".`
    }
  } finally {
    adding.value = false
  }
}

function reset() {
  lines.value = []
  payments.value = []
  Object.assign(form, { customerMobile: '', customerName: '', customerGstin: '', billDiscount: 0 })
  customerNote.value = ''
  confirmed.value = false
}

async function submit() {
  if (!canSave.value || !selectedStore.value) return
  saving.value = true
  error.value = ''
  created.value = null
  try {
    const store = selectedStore.value
    const result = await post<ApiRecord>('billing/sales', {
      companyId: readText(store, ['companyId']),
      storeGroupId: readText(store, ['storeGroupId']),
      storeId: form.storeId,
      customerName: form.customerName || null,
      customerMobileNumber: form.customerMobile || null,
      customerGstin: form.customerGstin || null,
      paymentMode: payments.value[0]?.paymentMode ?? 0,
      bankAccountId: null,
      paidAmount: paidTotal.value,
      billDiscountAmount: Number(form.billDiscount || 0),
      salesmanId: form.salesmanId || null,
      saleDate: form.saleDate,
      items: lines.value.map(line => ({ productId: line.productId, barcode: line.barcode, quantity: Number(line.quantity), mrp: Number(line.mrp), discountAmount: Number(line.discountAmount || 0) })),
      payments: payments.value.map(item => ({
        paymentMode: Number(item.paymentMode),
        amount: Number(item.amount),
        bankAccountId: Number(item.paymentMode) === 0 ? null : item.bankAccountId || null,
        referenceNumber: item.referenceNumber || null,
        gatewayReference: null,
        settlementStatus: null,
        adjustmentSourceType: null,
        adjustmentSourceId: null
      }))
    })
    created.value = { invoiceNumber: readText(result, ['invoiceNumber']), billAmount: readNumber(result, ['billAmount']), paidAmount: readNumber(result, ['paidAmount']) }
    createdDate.value = form.saleDate
    reset()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not post the invoice.'
  } finally {
    saving.value = false
  }
}

watch(() => form.storeId, loadStoreData)

onMounted(async () => {
  stores.value = toRows(await get<unknown>('stores').catch(() => []))
  if (!form.storeId && stores.value[0]) form.storeId = String(stores.value[0].id)
})
</script>

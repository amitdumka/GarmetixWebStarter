<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-hand-coins" class="size-4" /> Purchase settlement</p>
          <h2 class="garmetix-dashboard-title">Vendor Payments</h2>
          <p class="garmetix-dashboard-subtitle">
            Record and review purchase invoice payments and vendor advances, with linked vouchers and print handoff.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Payment</UButton>
          <USelect v-model="kindFilter" :items="kindFilterItems" class="w-44" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="applyFilters">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.55fr)_minmax(340px,0.85fr)]">
      <div class="garmetix-table-panel">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Payments</h3>
            <p class="garmetix-panel-subtitle">Page {{ page }} of {{ totalPages }} - {{ total }} row(s)</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search vendor payments" class="lg:w-72" @keyup.enter="applyFilters" />
        </div>

        <div class="overflow-hidden rounded-lg border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[960px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Kind</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Mode</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Reference</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Amount</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="payments.length === 0">
                  <td colspan="8" class="px-3 py-8 text-center text-muted">No vendor payments found.</td>
                </tr>
                <tr
                  v-for="payment in payments"
                  :key="paymentKey(payment)"
                  class="bg-default/40"
                  :class="selectedPaymentId === readText(payment, ['id'], '') ? 'outline outline-1 outline-primary/60' : ''"
                >
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDate(payment.onDate) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ readText(payment, ['vendorName']) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ readText(payment, ['purchaseInvoiceNumber']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(payment, ['paymentKind']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(payment, ['paymentMode']) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ readText(payment, ['referenceNumber']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ money(payment.amount) }}</td>
                  <td class="px-3 py-2">
                    <div class="flex flex-wrap items-center gap-1">
                      <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" @click="selectPayment(payment)" />
                      <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(payment)" />
                      <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(payment)" />
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div v-if="total" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
          <p>Showing {{ payments.length }} of {{ total }} payment(s)</p>
          <div class="flex items-center gap-2">
            <USelect v-model="pageSize" :items="pageSizeOptions" class="w-28" @update:model-value="applyFilters" />
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
          </div>
        </div>
      </div>

      <aside class="garmetix-detail-panel">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Payment Detail</h3>
            <p class="garmetix-panel-subtitle">{{ selectedPaymentTitle }}</p>
          </div>
          <UBadge :color="selectedPayment ? 'success' : 'neutral'" variant="subtle">{{ selectedPayment ? readText(selectedPayment, ['paymentKind']) : 'None' }}</UBadge>
        </div>

        <div v-if="selectedPayment" class="mt-4 space-y-4">
          <BooksMasterTable :columns="detailColumns" :rows="detailRows" empty-text="No payment detail rows found." />

          <div class="flex flex-wrap gap-2">
            <UButton
              v-if="selectedPayment.voucherId"
              icon="i-lucide-file-down"
              size="sm"
              color="primary"
              variant="soft"
              :loading="downloadLoading === 'voucher'"
              @click="downloadVoucher"
            >
              Voucher PDF
            </UButton>
            <UButton
              v-if="selectedPayment.purchaseInvoiceId"
              icon="i-lucide-receipt-text"
              size="sm"
              color="neutral"
              variant="soft"
              :loading="downloadLoading === 'invoice'"
              @click="downloadInvoice"
            >
              Invoice PDF
            </UButton>
            <UBadge v-if="!selectedPayment.voucherId && !selectedPayment.purchaseInvoiceId" color="neutral" variant="subtle">No PDF link</UBadge>
          </div>
        </div>

        <div v-else class="mt-8 text-center text-sm text-muted">
          Select a vendor payment to review references and print handoff.
        </div>
      </aside>
    </section>

    <UModal v-model:open="createOpen" title="New Vendor Payment" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-xl' }">
      <template #body>
        <div class="mb-3 flex gap-2">
          <UButton size="sm" :variant="createKind === 'invoice' ? 'solid' : 'soft'" color="neutral" @click="createKind = 'invoice'">Against Invoice</UButton>
          <UButton size="sm" :variant="createKind === 'advance' ? 'solid' : 'soft'" color="neutral" @click="createKind = 'advance'">Vendor Advance</UButton>
        </div>

        <form class="grid gap-3" @submit.prevent="saveCreate">
          <template v-if="createKind === 'invoice'">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Search purchase invoice</span>
              <div class="flex gap-2">
                <UInput v-model="invoiceSearch" placeholder="Invoice number" class="flex-1" @keyup.enter="searchInvoices" />
                <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="invoiceSearching" type="button" @click="searchInvoices">Find</UButton>
              </div>
            </label>
            <div v-if="invoiceResults.length" class="max-h-40 space-y-1 overflow-auto">
              <button
                v-for="invoice in invoiceResults"
                :key="readText(invoice, ['id'])"
                type="button"
                class="flex w-full items-center justify-between rounded-md border border-default p-2 text-left text-sm hover:bg-muted/40"
                :class="createForm.purchaseInvoiceId === readText(invoice, ['id']) ? 'outline outline-1 outline-primary/60' : ''"
                @click="selectInvoiceForPayment(invoice)"
              >
                <span>{{ readText(invoice, ['invoiceNumber']) }} - {{ readText(invoice, ['vendorName']) }}</span>
                <span class="text-xs text-muted">Balance {{ money(readNumber(invoice, ['balanceAmount'])) }}</span>
              </button>
            </div>
          </template>
          <template v-else>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Vendor</span>
              <USelectMenu v-model="createForm.vendorId" :items="vendorItems" value-key="value" searchable placeholder="Select vendor" />
            </label>
          </template>

          <label class="space-y-1 text-sm">
            <span class="text-muted">Amount</span>
            <UInput v-model.number="createForm.amount" type="number" min="0" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Payment mode</span>
            <USelect v-model="createForm.paymentMode" :items="paymentModeItems" />
          </label>
          <label v-if="createForm.paymentMode !== 0" class="space-y-1 text-sm">
            <span class="text-muted">Bank account</span>
            <USelect v-model="createForm.bankAccountId" :items="bankAccountItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Reference / slip number</span>
            <UInput v-model="createForm.slipNumber" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Remarks</span>
            <UTextarea v-model="createForm.remarks" :rows="2" />
          </label>

          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Payment</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="editOpen" title="Edit Vendor Payment" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="grid gap-3" @submit.prevent="saveEdit">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Date</span>
            <UInput v-model="editForm.onDate" type="date" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Amount</span>
            <UInput v-model.number="editForm.amount" type="number" min="0" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Payment mode</span>
            <USelect v-model="editForm.paymentMode" :items="paymentModeItems" />
          </label>
          <label v-if="editForm.paymentMode !== 0" class="space-y-1 text-sm">
            <span class="text-muted">Bank account</span>
            <USelect v-model="editForm.bankAccountId" :items="bankAccountItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Reference number</span>
            <UInput v-model="editForm.referenceNumber" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Remarks</span>
            <UTextarea v-model="editForm.remarks" :rows="2" />
          </label>
          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Update Payment</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="deleteOpen" title="Delete vendor payment" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <p class="text-sm text-muted">Delete this vendor payment? This cannot be undone.</p>
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
import {
  formatDate,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'Vendor Payments - Garmetix Books' })

const { download, get, post, put, del } = useBooksApiClient()
const loading = ref(true)
const saving = ref(false)
const invoiceSearching = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const kindFilter = ref('all')
const downloadLoading = ref('')
const payments = ref<ApiRecord[]>([])
const vouchers = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const selectedPaymentId = ref('')
const selectedPayment = ref<ApiRecord | null>(null)
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)

const createOpen = ref(false)
const editOpen = ref(false)
const deleteOpen = ref(false)
const createKind = ref<'invoice' | 'advance'>('invoice')
const invoiceSearch = ref('')
const invoiceResults = ref<ApiRecord[]>([])
const deleteTarget = ref<ApiRecord | null>(null)

const createForm = reactive({ purchaseInvoiceId: '', vendorId: '', amount: 0, paymentMode: 0, bankAccountId: '', slipNumber: '', remarks: '' })
const editForm = reactive({ id: '', onDate: '', amount: 0, paymentMode: 0, bankAccountId: '', referenceNumber: '', remarks: '' })

const kindFilterItems = [
  { label: 'All Kinds', value: 'all' },
  { label: 'Invoice', value: 'Invoice' },
  { label: 'Advance', value: 'Advance' }
]
const pageSizeOptions = [
  { label: '25 / page', value: 25 },
  { label: '50 / page', value: 50 },
  { label: '100 / page', value: 100 }
]
const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallets', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 },
  { label: 'Demand Draft', value: 8 }, { label: 'Others', value: 14 }
]
const vendorItems = computed(() => vendors.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })))
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountName', 'name'], 'Bank account'), value: readText(item, ['id'], '') })))

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const cards = computed(() => {
  const invoiceTotal = payments.value
    .filter(item => readText(item, ['paymentKind']) === 'Invoice')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)
  const advanceTotal = payments.value
    .filter(item => readText(item, ['paymentKind']) === 'Advance')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)
  return [
    { label: 'Payments', value: String(total.value), detail: 'Matching register filters' },
    { label: 'Invoice Paid', value: money(invoiceTotal), detail: 'This page - purchase invoice payments' },
    { label: 'Advances', value: money(advanceTotal), detail: 'This page - vendor advance payments' },
    { label: 'Voucher Links', value: payments.value.filter(item => item.voucherId).length, detail: 'Printable voucher references' }
  ]
})
const selectedPaymentTitle = computed(() => selectedPayment.value
  ? `${readText(selectedPayment.value, ['vendorName'])} - ${money(selectedPayment.value.amount)}`
  : 'Select a payment')
const detailRows = computed(() => {
  const payment = selectedPayment.value
  if (!payment) return []
  return [
    { label: 'Date', value: formatDate(payment.onDate) },
    { label: 'Vendor', value: readText(payment, ['vendorName']) },
    { label: 'Purchase Invoice', value: readText(payment, ['purchaseInvoiceNumber']) },
    { label: 'Payment Kind', value: readText(payment, ['paymentKind']) },
    { label: 'Amount', value: money(payment.amount) },
    { label: 'Payment Mode', value: readText(payment, ['paymentMode']) },
    { label: 'Reference', value: readText(payment, ['referenceNumber']) },
    { label: 'Voucher', value: voucherNumber(payment.voucherId) },
    { label: 'Remarks', value: readText(payment, ['remarks']) }
  ]
})
const detailColumns = [
  { key: 'label', label: 'Field' },
  { key: 'value', label: 'Value' }
]

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function paymentKey(payment: ApiRecord) {
  return readText(payment, ['id', 'referenceNumber'])
}

function voucherNumber(id: unknown) {
  return readText(vouchers.value.find(item => item.id === id), ['voucherNumber'], id ? 'Linked' : '-')
}

function selectPayment(payment: ApiRecord) {
  selectedPaymentId.value = readText(payment, ['id'], '')
  selectedPayment.value = payment
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [paymentData, voucherData, lookupData, bankData] = await Promise.allSettled([
      get<ApiRecord>('purchase/payments', {
        page: page.value,
        pageSize: pageSize.value,
        q: search.value.trim() || undefined,
        paymentMode: undefined
      }),
      get<unknown>('vouchers'),
      get<ApiRecord>('purchase/lookup-options'),
      get<unknown>('bank-accounts')
    ])
    if (paymentData.status === 'fulfilled') {
      const rows = toRows(paymentData.value, ['items'])
      payments.value = kindFilter.value === 'all' ? rows : rows.filter(item => readText(item, ['paymentKind']) === kindFilter.value)
      total.value = readNumber(paymentData.value, ['total']) || rows.length
    }
    if (voucherData.status === 'fulfilled') vouchers.value = toRows(voucherData.value)
    if (lookupData.status === 'fulfilled') vendors.value = toRows(lookupData.value, ['vendors'])
    if (bankData.status === 'fulfilled') bankAccounts.value = toRows(bankData.value)
    if (!selectedPaymentId.value && payments.value.length > 0) selectPayment(payments.value[0])

    const failed = [paymentData, voucherData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} vendor payment request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendor payments.'
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

async function downloadVoucher() {
  const id = selectedPayment.value?.voucherId
  if (!id) return
  downloadLoading.value = 'voucher'
  error.value = ''
  try {
    await download(`vouchers/${id}/pdf`, { signatures: true }, `${voucherNumber(id)}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download vendor payment voucher.'
  } finally {
    downloadLoading.value = ''
  }
}

async function downloadInvoice() {
  const id = selectedPayment.value?.purchaseInvoiceId
  if (!id) return
  downloadLoading.value = 'invoice'
  error.value = ''
  try {
    await download(`purchase/invoices/${id}/pdf`, { format: 'a4' }, `${readText(selectedPayment.value, ['purchaseInvoiceNumber'], 'purchase-invoice')}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download purchase invoice.'
  } finally {
    downloadLoading.value = ''
  }
}

function startCreate() {
  createKind.value = 'invoice'
  Object.assign(createForm, { purchaseInvoiceId: '', vendorId: '', amount: 0, paymentMode: 0, bankAccountId: '', slipNumber: '', remarks: '' })
  invoiceSearch.value = ''
  invoiceResults.value = []
  error.value = ''
  createOpen.value = true
}

async function searchInvoices() {
  invoiceSearching.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord>('purchase/invoices', { q: invoiceSearch.value.trim() || undefined, pageSize: 10, page: 1, status: 'all' })
    invoiceResults.value = toRows(response, ['items'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to search purchase invoices.'
  } finally {
    invoiceSearching.value = false
  }
}

function selectInvoiceForPayment(invoice: ApiRecord) {
  createForm.purchaseInvoiceId = readText(invoice, ['id'], '')
  createForm.amount = readNumber(invoice, ['balanceAmount'])
}

async function saveCreate() {
  error.value = ''
  if (!createForm.amount || createForm.amount <= 0) { error.value = 'Enter a valid amount.'; return }
  saving.value = true
  try {
    if (createKind.value === 'invoice') {
      if (!createForm.purchaseInvoiceId) throw new Error('Select a purchase invoice.')
      await post<unknown>(`purchase/invoices/${createForm.purchaseInvoiceId}/payment-voucher`, {
        amount: createForm.amount,
        paymentMode: createForm.paymentMode,
        bankAccountId: createForm.bankAccountId || null,
        slipNumber: createForm.slipNumber || null,
        remarks: createForm.remarks || null
      })
    } else {
      if (!createForm.vendorId) throw new Error('Select a vendor.')
      await post<unknown>('purchase/payments/advance', {
        vendorId: createForm.vendorId,
        amount: createForm.amount,
        paymentMode: createForm.paymentMode,
        bankAccountId: createForm.bankAccountId || null,
        slipNumber: createForm.slipNumber || null,
        remarks: createForm.remarks || null
      })
    }
    message.value = 'Vendor payment saved.'
    createOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save vendor payment.'
  } finally {
    saving.value = false
  }
}

function startEdit(payment: ApiRecord) {
  Object.assign(editForm, {
    id: readText(payment, ['id'], ''),
    onDate: String(payment.onDate || '').slice(0, 10),
    amount: readNumber(payment, ['amount']),
    paymentMode: readNumber(payment, ['paymentModeValue']),
    bankAccountId: readText(payment, ['bankAccountId'], ''),
    referenceNumber: readText(payment, ['referenceNumber'], ''),
    remarks: readText(payment, ['remarks'], '')
  })
  error.value = ''
  editOpen.value = true
}

async function saveEdit() {
  if (!editForm.id) return
  saving.value = true
  error.value = ''
  try {
    await put<unknown>(`purchase/payments/${editForm.id}`, {
      onDate: editForm.onDate || null,
      amount: editForm.amount,
      paymentMode: editForm.paymentMode,
      bankAccountId: editForm.bankAccountId || null,
      referenceNumber: editForm.referenceNumber || null,
      remarks: editForm.remarks || null
    })
    message.value = 'Vendor payment updated.'
    editOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update vendor payment.'
  } finally {
    saving.value = false
  }
}

function askDelete(payment: ApiRecord) {
  deleteTarget.value = payment
  deleteOpen.value = true
}

async function confirmDelete() {
  const id = readText(deleteTarget.value, ['id'], '')
  if (!id) return
  saving.value = true
  error.value = ''
  try {
    await del<unknown>(`purchase/payments/${id}`)
    message.value = 'Vendor payment deleted.'
    deleteOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete vendor payment.'
  } finally {
    saving.value = false
  }
}

watch(kindFilter, () => { void applyFilters() })

onMounted(refresh)
</script>

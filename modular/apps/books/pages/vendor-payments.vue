<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Purchase settlement</p>
          <h2 class="mt-1 text-2xl font-semibold">Vendor Payment Review</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review recent purchase payments, advances, linked vouchers and purchase invoice print handoff. Payment creation stays in the controlled purchase workflow.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="kindFilter" :items="kindFilterItems" class="w-44" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UBadge color="primary" variant="subtle">Read only</UBadge>
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.55fr)_minmax(340px,0.85fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="text-base font-semibold">Recent Payments</h3>
            <p class="text-xs text-muted">{{ filteredPayments.length }} row(s) shown</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search vendor payments" class="lg:w-72" />
        </div>

        <div class="overflow-hidden border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[920px] text-left text-sm">
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
                <tr v-if="filteredPayments.length === 0">
                  <td colspan="8" class="px-3 py-8 text-center text-muted">No vendor payments found.</td>
                </tr>
                <tr
                  v-for="payment in filteredPayments"
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
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" @click="selectPayment(payment)">View</UButton>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <aside class="border border-default bg-muted/10 p-4">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">Payment Detail</h3>
            <p class="text-xs text-muted">{{ selectedPaymentTitle }}</p>
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

const { download, get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const kindFilter = ref('all')
const downloadLoading = ref('')
const payments = ref<ApiRecord[]>([])
const vouchers = ref<ApiRecord[]>([])
const selectedPaymentId = ref('')
const selectedPayment = ref<ApiRecord | null>(null)

const kindFilterItems = [
  { label: 'All Kinds', value: 'all' },
  { label: 'Invoice', value: 'Invoice' },
  { label: 'Advance', value: 'Advance' }
]
const cards = computed(() => {
  const invoiceTotal = payments.value
    .filter(item => readText(item, ['paymentKind']) === 'Invoice')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)
  const advanceTotal = payments.value
    .filter(item => readText(item, ['paymentKind']) === 'Advance')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)
  return [
    { label: 'Payments', value: payments.value.length, detail: 'Recent payment rows' },
    { label: 'Invoice Paid', value: money(invoiceTotal), detail: 'Purchase invoice payments' },
    { label: 'Advances', value: money(advanceTotal), detail: 'Vendor advance payments' },
    { label: 'Voucher Links', value: payments.value.filter(item => item.voucherId).length, detail: 'Printable voucher references' }
  ]
})
const filteredPayments = computed(() => {
  const term = search.value.trim().toLowerCase()
  return payments.value.filter(item => {
    const kindMatches = kindFilter.value === 'all' || readText(item, ['paymentKind']) === kindFilter.value
    const textMatches = !term || [
      readText(item, ['vendorName']),
      readText(item, ['purchaseInvoiceNumber']),
      readText(item, ['paymentKind']),
      readText(item, ['paymentMode']),
      readText(item, ['referenceNumber']),
      readText(item, ['remarks']),
      voucherNumber(item.voucherId)
    ].join(' ').toLowerCase().includes(term)
    return kindMatches && textMatches
  })
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
    const [paymentData, voucherData] = await Promise.allSettled([
      get<unknown>('purchase/payments/recent', { take: 150 }),
      get<unknown>('vouchers')
    ])
    if (paymentData.status === 'fulfilled') payments.value = toRows(paymentData.value)
    if (voucherData.status === 'fulfilled') vouchers.value = toRows(voucherData.value)
    if (!selectedPaymentId.value && payments.value.length > 0) selectPayment(payments.value[0])
    const failed = [paymentData, voucherData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} vendor payment request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendor payments.'
  } finally {
    loading.value = false
  }
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

onMounted(refresh)
</script>

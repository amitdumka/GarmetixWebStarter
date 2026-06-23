<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Debit note settlement</p>
          <h2 class="mt-1 text-2xl font-semibold">Vendor Settlement Review</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review debit-note settlements, invoice allocations, refund voucher links, bank references and journal handoff. Settlement posting remains in the controlled purchase return flow.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="statusFilter" :items="statusFilterItems" class="w-44" />
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.55fr)_minmax(360px,0.85fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="text-base font-semibold">Recent Settlements</h3>
            <p class="text-xs text-muted">{{ filteredSettlements.length }} row(s) shown</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search vendor settlements" class="lg:w-72" />
        </div>

        <div class="overflow-hidden border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[960px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Settlement</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Debit Note</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Type</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Adjusted</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Refund</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="filteredSettlements.length === 0">
                  <td colspan="9" class="px-3 py-8 text-center text-muted">No vendor settlements found.</td>
                </tr>
                <tr
                  v-for="settlement in filteredSettlements"
                  :key="settlementKey(settlement)"
                  class="bg-default/40"
                  :class="selectedSettlementId === readText(settlement, ['id'], '') ? 'outline outline-1 outline-primary/60' : ''"
                >
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDate(settlement.onDate) }}</td>
                  <td class="max-w-44 truncate px-3 py-2">{{ readText(settlement, ['settlementNumber']) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ readText(settlement, ['vendorName']) }}</td>
                  <td class="max-w-44 truncate px-3 py-2">{{ readText(settlement, ['debitNoteNumber']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(settlement, ['settlementType']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(settlement.adjustedAmount) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(settlement.refundAmount) }}</td>
                  <td class="px-3 py-2">
                    <UBadge :color="settlementStatusColor(settlement.status)" variant="subtle">{{ readText(settlement, ['status']) }}</UBadge>
                  </td>
                  <td class="px-3 py-2">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" :loading="detailLoading && selectedSettlementId === readText(settlement, ['id'], '')" @click="selectSettlement(settlement)">
                      View
                    </UButton>
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
            <h3 class="text-base font-semibold">Settlement Detail</h3>
            <p class="text-xs text-muted">{{ selectedSettlementTitle }}</p>
          </div>
          <UBadge :color="selectedSettlement ? 'success' : 'neutral'" variant="subtle">{{ selectedSettlement ? readText(selectedSettlement, ['settlementType']) : 'None' }}</UBadge>
        </div>

        <div v-if="selectedSettlement" class="mt-4 space-y-4">
          <BooksMasterTable :columns="detailColumns" :rows="detailRows" empty-text="No settlement detail rows found." />

          <div>
            <h4 class="mb-2 text-sm font-semibold">Allocations</h4>
            <BooksMasterTable :columns="allocationColumns" :rows="allocationRows" empty-text="No invoice allocations found." />
          </div>

          <div class="flex flex-wrap gap-2">
            <UButton
              v-if="selectedSettlement.voucherId"
              icon="i-lucide-file-down"
              size="sm"
              color="primary"
              variant="soft"
              :loading="downloadLoading"
              @click="downloadVoucher"
            >
              Refund Voucher PDF
            </UButton>
            <UBadge v-if="selectedSettlement.bankTransactionId" color="neutral" variant="subtle">Bank transaction linked</UBadge>
            <UBadge v-if="!selectedSettlement.voucherId" color="neutral" variant="subtle">No refund voucher</UBadge>
          </div>
        </div>

        <div v-else class="mt-8 text-center text-sm text-muted">
          Select a settlement to review allocations and posting references.
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  formatDate,
  readArray,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'Vendor Settlements - Garmetix Books' })

type BadgeColor = 'success' | 'warning' | 'neutral'

const { download, get } = useBooksApiClient()
const loading = ref(true)
const detailLoading = ref(false)
const downloadLoading = ref(false)
const error = ref('')
const search = ref('')
const statusFilter = ref('all')
const settlements = ref<ApiRecord[]>([])
const selectedSettlementId = ref('')
const selectedSettlement = ref<ApiRecord | null>(null)

const statusFilterItems = computed(() => {
  const statuses = Array.from(new Set(settlements.value.map(item => readText(item, ['status'])).filter(item => item !== '-')))
  return [
    { label: 'All Status', value: 'all' },
    ...statuses.map(item => ({ label: item, value: item }))
  ]
})
const cards = computed(() => {
  const adjusted = settlements.value.reduce((sum, item) => sum + readNumber(item, ['adjustedAmount']), 0)
  const refunds = settlements.value.reduce((sum, item) => sum + readNumber(item, ['refundAmount']), 0)
  return [
    { label: 'Settlements', value: settlements.value.length, detail: 'Recent settlement rows' },
    { label: 'Adjusted', value: money(adjusted), detail: 'Debit note adjusted amount' },
    { label: 'Refunds', value: money(refunds), detail: 'Vendor refund amount' },
    { label: 'Voucher Links', value: settlements.value.filter(item => item.voucherId).length, detail: 'Refund voucher references' }
  ]
})
const filteredSettlements = computed(() => {
  const term = search.value.trim().toLowerCase()
  return settlements.value.filter(item => {
    const statusMatches = statusFilter.value === 'all' || readText(item, ['status']) === statusFilter.value
    const textMatches = !term || [
      readText(item, ['settlementNumber']),
      readText(item, ['vendorName']),
      readText(item, ['returnNumber']),
      readText(item, ['debitNoteNumber']),
      readText(item, ['settlementType']),
      readText(item, ['paymentMode']),
      readText(item, ['referenceNumber']),
      readText(item, ['remarks'])
    ].join(' ').toLowerCase().includes(term)
    return statusMatches && textMatches
  })
})
const selectedSettlementTitle = computed(() => selectedSettlement.value
  ? `${readText(selectedSettlement.value, ['settlementNumber'])} - ${readText(selectedSettlement.value, ['vendorName'])}`
  : 'Select a settlement')
const detailRows = computed(() => {
  const settlement = selectedSettlement.value
  if (!settlement) return []
  return [
    { label: 'Date', value: formatDate(settlement.onDate) },
    { label: 'Vendor', value: readText(settlement, ['vendorName']) },
    { label: 'Purchase Return', value: readText(settlement, ['returnNumber']) },
    { label: 'Debit Note', value: readText(settlement, ['debitNoteNumber']) },
    { label: 'Settlement Type', value: readText(settlement, ['settlementType']) },
    { label: 'Adjusted Amount', value: money(settlement.adjustedAmount) },
    { label: 'Refund Amount', value: money(settlement.refundAmount) },
    { label: 'Total Amount', value: money(settlement.totalAmount) },
    { label: 'Payment Mode', value: readText(settlement, ['paymentMode']) },
    { label: 'Bank Account', value: readText(settlement, ['bankAccountName']) },
    { label: 'Reference', value: readText(settlement, ['referenceNumber']) },
    { label: 'Voucher', value: readText(settlement, ['voucherNumber'], settlement.voucherId ? 'Linked' : '-') },
    { label: 'Journal', value: readText(settlement, ['journalEntryNumber'], settlement.journalEntryId ? 'Linked' : '-') },
    { label: 'Status', value: readText(settlement, ['status']) },
    { label: 'Remarks', value: readText(settlement, ['remarks']) }
  ]
})
const allocationRows = computed(() => readArray(selectedSettlement.value, ['allocations']).map(item => ({
  invoice: readText(item, ['purchaseInvoiceNumber']),
  amount: money(item.amount)
})))
const detailColumns = [
  { key: 'label', label: 'Field' },
  { key: 'value', label: 'Value' }
]
const allocationColumns = [
  { key: 'invoice', label: 'Purchase Invoice' },
  { key: 'amount', label: 'Allocated Amount' }
]

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function settlementKey(settlement: ApiRecord) {
  return readText(settlement, ['id', 'settlementNumber'])
}

function settlementStatusColor(status: unknown): BadgeColor {
  const value = String(status ?? '').toLowerCase()
  if (value.includes('settled') || value.includes('posted') || value.includes('complete')) return 'success'
  if (value.includes('partial') || value.includes('pending') || value.includes('open')) return 'warning'
  return 'neutral'
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const data = await get<unknown>('purchase/vendor-settlements/recent', { take: 150 })
    settlements.value = toRows(data)
    if (!selectedSettlementId.value && settlements.value.length > 0) await selectSettlement(settlements.value[0])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendor settlements.'
  } finally {
    loading.value = false
  }
}

async function selectSettlement(settlement: ApiRecord) {
  const id = readText(settlement, ['id'], '')
  selectedSettlementId.value = id
  selectedSettlement.value = settlement
  if (!id) return

  detailLoading.value = true
  error.value = ''
  try {
    const detail = await get<unknown>(`purchase/vendor-settlements/${id}`)
    if (detail && typeof detail === 'object') selectedSettlement.value = detail as ApiRecord
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendor settlement detail.'
  } finally {
    detailLoading.value = false
  }
}

async function downloadVoucher() {
  const id = selectedSettlement.value?.voucherId
  if (!id) return
  downloadLoading.value = true
  error.value = ''
  try {
    await download(`vouchers/${id}/pdf`, { signatures: true }, `${readText(selectedSettlement.value, ['voucherNumber'], 'vendor-refund-voucher')}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download refund voucher.'
  } finally {
    downloadLoading.value = false
  }
}

onMounted(refresh)
</script>

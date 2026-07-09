<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-check-2" class="size-4" /> Debit note settlement</p>
          <h2 class="garmetix-dashboard-title">Vendor Settlement Review</h2>
          <p class="garmetix-dashboard-subtitle">
            Review debit-note settlements, invoice allocations, refund voucher links, bank references and journal handoff. Settlement posting remains in the controlled purchase return flow.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <USelect v-model="statusFilter" :items="statusFilterItems" class="w-44" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Debit Notes Available For Settlement</h3>
          <p class="garmetix-panel-subtitle">{{ availableReturns.length }} debit note(s) with settleable balance</p>
        </div>
      </div>
      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[820px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2 font-medium">Debit Note</th>
                <th class="px-3 py-2 font-medium">Vendor</th>
                <th class="px-3 py-2 font-medium">Original Invoice</th>
                <th class="px-3 py-2 text-right font-medium">Return Amount</th>
                <th class="px-3 py-2 text-right font-medium">Available</th>
                <th class="px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="!availableReturns.length">
                <td colspan="6" class="px-3 py-8 text-center text-muted">No debit notes are awaiting settlement.</td>
              </tr>
              <tr v-for="item in availableReturns" :key="readText(item, ['id'])">
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['debitNoteNumber']) }}</td>
                <td class="max-w-44 truncate px-3 py-2">{{ readText(item, ['vendorName']) }}</td>
                <td class="max-w-40 truncate px-3 py-2">{{ readText(item, ['originalInvoiceNumber']) }}</td>
                <td class="px-3 py-2 text-right">{{ money(readNumber(item, ['returnAmount'])) }}</td>
                <td class="px-3 py-2 text-right font-medium">{{ money(readNumber(item, ['availableSettlementAmount'])) }}</td>
                <td class="px-3 py-2">
                  <UButton size="xs" color="primary" variant="soft" icon="i-lucide-scale" :loading="settleLoading === readText(item, ['id'])" @click="startSettle(item)">Settle</UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.55fr)_minmax(360px,0.85fr)]">
      <div class="garmetix-table-panel">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Recent Settlements</h3>
            <p class="garmetix-panel-subtitle">{{ filteredSettlements.length }} row(s) shown</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search vendor settlements" class="lg:w-72" />
        </div>

        <div class="overflow-hidden rounded-lg border border-default">
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

      <aside class="garmetix-detail-panel">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Settlement Detail</h3>
            <p class="garmetix-panel-subtitle">{{ selectedSettlementTitle }}</p>
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

    <UModal v-model:open="settleOpen" title="Settle Debit Note" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-3xl' }">
      <template #body>
        <div v-if="settleOptions" class="space-y-4">
          <p class="text-sm text-muted">
            {{ readText(settleOptions, ['debitNoteNumber']) }} - {{ readText(settleOptions, ['vendorName']) }} - Available {{ money(readNumber(settleOptions, ['availableAmount'])) }}
          </p>

          <div class="flex justify-end gap-2">
            <UButton size="xs" color="neutral" variant="soft" type="button" @click="allocateAvailable">Allocate Available</UButton>
            <UButton size="xs" color="neutral" variant="ghost" type="button" @click="clearAllocations">Clear</UButton>
          </div>

          <div class="overflow-hidden rounded-lg border border-default">
            <div class="overflow-x-auto">
              <table class="w-full min-w-[560px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted">
                  <tr>
                    <th class="px-3 py-2 font-medium">Invoice</th>
                    <th class="px-3 py-2 text-right font-medium">Outstanding</th>
                    <th class="px-3 py-2 text-right font-medium">Allocate</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="invoice in readArray(settleOptions, ['outstandingInvoices'])" :key="readText(invoice, ['purchaseInvoiceId'])">
                    <td class="px-3 py-2">{{ readText(invoice, ['invoiceNumber']) }}</td>
                    <td class="px-3 py-2 text-right">{{ money(readNumber(invoice, ['outstandingAmount'])) }}</td>
                    <td class="px-3 py-2 text-right">
                      <UInput
                        v-model.number="allocations[readText(invoice, ['purchaseInvoiceId'])]"
                        type="number"
                        min="0"
                        :max="readNumber(invoice, ['outstandingAmount'])"
                        step="0.01"
                        class="w-32 ml-auto"
                      />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Refund amount (unallocated goes to vendor)</span>
              <UInput v-model.number="settleForm.refundAmount" type="number" min="0" step="0.01" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Refund mode</span>
              <USelect v-model="settleForm.paymentMode" :items="paymentModeItems" />
            </label>
            <label v-if="settleForm.refundAmount > 0 && settleForm.paymentMode !== 0" class="space-y-1 text-sm">
              <span class="text-muted">Bank account</span>
              <USelect v-model="settleForm.bankAccountId" :items="bankAccountItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Reference</span>
              <UInput v-model="settleForm.referenceNumber" />
            </label>
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Remarks</span>
              <UTextarea v-model="settleForm.remarks" :rows="2" />
            </label>
          </div>

          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="settleOpen = false">Cancel</UButton>
            <UButton color="primary" variant="solid" icon="i-lucide-scale" :loading="settling" @click="submitSettle">Post Settlement</UButton>
          </div>
        </div>
      </template>
    </UModal>
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

const { download, get, post } = useBooksApiClient()
const loading = ref(true)
const detailLoading = ref(false)
const downloadLoading = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const statusFilter = ref('all')
const settlements = ref<ApiRecord[]>([])
const returns = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const selectedSettlementId = ref('')
const selectedSettlement = ref<ApiRecord | null>(null)

const settleOpen = ref(false)
const settleLoading = ref('')
const settling = ref(false)
const settleOptions = ref<ApiRecord | null>(null)
const allocations = reactive<Record<string, number>>({})
const settleForm = reactive({ refundAmount: 0, paymentMode: 0, bankAccountId: '', referenceNumber: '', remarks: '' })

const paymentModeItems = [
  { label: 'Cash', value: 0 }, { label: 'Card', value: 1 }, { label: 'UPI', value: 2 }, { label: 'Wallets', value: 3 },
  { label: 'IMPS', value: 4 }, { label: 'RTGS', value: 5 }, { label: 'NEFT', value: 6 }, { label: 'Cheque', value: 7 },
  { label: 'Demand Draft', value: 8 }, { label: 'Others', value: 14 }
]
const bankAccountItems = computed(() => bankAccounts.value.map(item => ({ label: readText(item, ['accountName', 'name'], 'Bank account'), value: readText(item, ['id'], '') })))
const availableReturns = computed(() => returns.value.filter(item => readNumber(item, ['availableSettlementAmount']) > 0))

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
    const [settlementData, returnData, bankData] = await Promise.allSettled([
      get<unknown>('purchase/vendor-settlements/recent', { take: 150 }),
      get<unknown>('purchase/returns/recent', { take: 150 }),
      get<unknown>('bank-accounts')
    ])
    if (settlementData.status === 'fulfilled') settlements.value = toRows(settlementData.value)
    if (returnData.status === 'fulfilled') returns.value = toRows(returnData.value)
    if (bankData.status === 'fulfilled') bankAccounts.value = toRows(bankData.value)
    if (!selectedSettlementId.value && settlements.value.length > 0) await selectSettlement(settlements.value[0])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vendor settlements.'
  } finally {
    loading.value = false
  }
}

async function startSettle(item: ApiRecord) {
  const id = readText(item, ['id'], '')
  if (!id) return
  settleLoading.value = id
  error.value = ''
  try {
    settleOptions.value = await get<ApiRecord>(`purchase/returns/${id}/settlement-options`)
    Object.keys(allocations).forEach(key => delete allocations[key])
    Object.assign(settleForm, { refundAmount: 0, paymentMode: 0, bankAccountId: '', referenceNumber: '', remarks: '' })
    settleOpen.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load settlement options.'
  } finally {
    settleLoading.value = ''
  }
}

function allocateAvailable() {
  let remaining = readNumber(settleOptions.value, ['availableAmount'])
  for (const invoice of readArray(settleOptions.value, ['outstandingInvoices'])) {
    if (remaining <= 0) break
    const id = readText(invoice, ['purchaseInvoiceId'], '')
    const outstanding = readNumber(invoice, ['outstandingAmount'])
    const amount = Math.min(outstanding, remaining)
    if (id) allocations[id] = amount
    remaining -= amount
  }
}

function clearAllocations() {
  Object.keys(allocations).forEach(key => { allocations[key] = 0 })
}

async function submitSettle() {
  const id = readText(settleOptions.value, ['purchaseReturnId'], '')
  if (!id) return
  const allocationList = Object.entries(allocations)
    .filter(([, amount]) => Number(amount) > 0)
    .map(([purchaseInvoiceId, amount]) => ({ purchaseInvoiceId, amount: Number(amount) }))
  const allocatedTotal = allocationList.reduce((sum, item) => sum + item.amount, 0)
  const available = readNumber(settleOptions.value, ['availableAmount'])
  if (allocatedTotal + settleForm.refundAmount > available + 0.01) {
    error.value = 'Allocations plus refund exceed the available settlement amount.'
    return
  }
  settling.value = true
  error.value = ''
  try {
    await post<unknown>(`purchase/returns/${id}/settle`, {
      refundAmount: settleForm.refundAmount || 0,
      paymentMode: settleForm.refundAmount > 0 ? settleForm.paymentMode : null,
      bankAccountId: settleForm.refundAmount > 0 ? (settleForm.bankAccountId || null) : null,
      referenceNumber: settleForm.referenceNumber || null,
      remarks: settleForm.remarks || null,
      allocations: allocationList
    })
    message.value = 'Vendor settlement posted.'
    settleOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to post vendor settlement.'
  } finally {
    settling.value = false
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

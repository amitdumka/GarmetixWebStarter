<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Accounting documents</p>
          <h2 class="mt-1 text-2xl font-semibold">Voucher Review</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Read-only payment, receipt and expense vouchers with ledger, party, bank and employee references. Voucher creation and posting actions stay in the legacy flow for now.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.6fr)_minmax(340px,0.8fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <h3 class="text-base font-semibold">Voucher List</h3>
            <p class="text-xs text-muted">{{ filteredVouchers.length }} row(s) shown</p>
          </div>
          <div class="flex flex-col gap-2 sm:flex-row">
            <USelect v-model="voucherTypeFilter" :items="voucherTypeFilterItems" class="sm:w-44" />
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search vouchers" class="sm:w-72" />
          </div>
        </div>

        <div class="overflow-hidden border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[920px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Voucher</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Type</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Party</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Ledger</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Mode</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Amount</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="filteredVouchers.length === 0">
                  <td colspan="8" class="px-3 py-8 text-center text-muted">No vouchers found.</td>
                </tr>
                <tr
                  v-for="voucher in filteredVouchers"
                  :key="voucherKey(voucher)"
                  class="bg-default/40"
                  :class="selectedVoucherId === readText(voucher, ['id'], '') ? 'outline outline-1 outline-primary/60' : ''"
                >
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDate(voucher.onDate) }}</td>
                  <td class="px-3 py-2">
                    <p class="font-medium">{{ readText(voucher, ['voucherNumber']) }}</p>
                    <p class="text-xs text-muted">{{ readText(voucher, ['slipNumber'], 'No slip') }}</p>
                  </td>
                  <td class="whitespace-nowrap px-3 py-2">{{ voucherTypeLabel(voucher.voucherType) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ voucherPartyName(voucher) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ ledgerName(voucher.ledgerId) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ paymentModeLabel(voucher.paymentMode) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ formatIndianMoney(readNumber(voucher, ['amount'])) }}</td>
                  <td class="px-3 py-2">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" :loading="detailLoading && selectedVoucherId === readText(voucher, ['id'], '')" @click="selectVoucher(voucher)">
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
            <h3 class="text-base font-semibold">Voucher Details</h3>
            <p class="text-xs text-muted">{{ selectedVoucherNumber }}</p>
          </div>
          <UBadge :color="selectedVoucher ? 'success' : 'neutral'" variant="subtle">{{ selectedVoucher ? voucherTypeLabel(selectedVoucher.voucherType) : 'None' }}</UBadge>
        </div>

        <div v-if="selectedVoucher" class="mt-4 space-y-3">
          <dl class="grid gap-3 text-sm">
            <div v-for="item in selectedDetails" :key="item.label" class="border-b border-default pb-2">
              <dt class="text-xs text-muted">{{ item.label }}</dt>
              <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
            </div>
          </dl>

          <div class="flex flex-wrap gap-2 pt-1">
            <UButton icon="i-lucide-file-down" size="sm" color="primary" variant="soft" :loading="downloadLoading === 'a4'" @click="downloadVoucher('a4')">A4 PDF</UButton>
            <UButton icon="i-lucide-receipt-text" size="sm" color="neutral" variant="soft" :loading="downloadLoading === 'a5'" @click="downloadVoucher('a5')">A5 PDF</UButton>
            <UButton icon="i-lucide-printer" size="sm" color="neutral" variant="ghost" :loading="downloadLoading === 'reprint'" @click="downloadVoucher('reprint')">Reprint</UButton>
          </div>
        </div>

        <div v-else class="mt-8 text-center text-sm text-muted">
          Select a voucher to view details and PDF options.
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  formatDate,
  optionLabel,
  paymentModeOptions,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient,
  voucherTypeOptions
} from '../utils/books-api'

useHead({ title: 'Vouchers - Garmetix Books' })

const { download, get } = useBooksApiClient()
const loading = ref(true)
const detailLoading = ref(false)
const error = ref('')
const search = ref('')
const voucherTypeFilter = ref('all')
const selectedVoucherId = ref('')
const selectedVoucher = ref<ApiRecord | null>(null)
const downloadLoading = ref('')
const vouchers = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const banks = ref<ApiRecord[]>([])
const bankAccounts = ref<ApiRecord[]>([])
const employees = ref<ApiRecord[]>([])

const voucherTypeFilterItems = [
  { label: 'All Types', value: 'all' },
  ...voucherTypeOptions.map(item => ({ label: item.label, value: item.label }))
]

const ledgerName = (id: unknown) => readText(ledgers.value.find(item => item.id === id), ['name'])
const partyName = (id: unknown) => readText(parties.value.find(item => item.id === id), ['name'])
const employeeName = (id: unknown) => readText(employees.value.find(item => item.id === id), ['staffName', 'name', 'firstName'])
const bankName = (id: unknown) => readText(banks.value.find(item => item.id === id), ['name'])
const bankAccountLabel = (id: unknown) => {
  const account = bankAccounts.value.find(item => item.id === id)
  if (!account) return '-'
  return `${bankName(account.bankId)} - ${readText(account, ['accountHolderName'], 'Account')} - ${readText(account, ['accountNumber'])}`
}
const voucherTypeLabel = (value: unknown) => optionLabel(voucherTypeOptions, value)
const paymentModeLabel = (value: unknown) => optionLabel(paymentModeOptions, value)
const voucherPartyName = (voucher: ApiRecord) => readText(voucher, ['partyName'], partyName(voucher.partyId))
const voucherKey = (voucher: ApiRecord) => readText(voucher, ['id', 'voucherNumber'])

const cards = computed(() => {
  const paymentTotal = vouchers.value
    .filter(item => voucherTypeLabel(item.voucherType) === 'Payment')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)
  const receiptTotal = vouchers.value
    .filter(item => voucherTypeLabel(item.voucherType) === 'Receipt')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)
  const expenseTotal = vouchers.value
    .filter(item => voucherTypeLabel(item.voucherType) === 'Expense')
    .reduce((sum, item) => sum + readNumber(item, ['amount']), 0)

  return [
    { label: 'Vouchers', value: vouchers.value.length, detail: 'Accounting vouchers' },
    { label: 'Payments', value: formatIndianMoney(paymentTotal), detail: 'Payment voucher total' },
    { label: 'Receipts', value: formatIndianMoney(receiptTotal), detail: 'Receipt voucher total' },
    { label: 'Expenses', value: formatIndianMoney(expenseTotal), detail: 'Expense voucher total' }
  ]
})
const filteredVouchers = computed(() => {
  const term = search.value.trim().toLowerCase()
  return vouchers.value.filter(item => {
    const typeMatches = voucherTypeFilter.value === 'all' || voucherTypeLabel(item.voucherType) === voucherTypeFilter.value
    const textMatches = !term || [
      readText(item, ['voucherNumber']),
      readText(item, ['slipNumber']),
      readText(item, ['partyName']),
      readText(item, ['particulars']),
      readText(item, ['remarks']),
      ledgerName(item.ledgerId),
      paymentModeLabel(item.paymentMode)
    ].join(' ').toLowerCase().includes(term)
    return typeMatches && textMatches
  })
})
const selectedVoucherNumber = computed(() => selectedVoucher.value ? readText(selectedVoucher.value, ['voucherNumber']) : 'Select a voucher')
const selectedDetails = computed(() => {
  const voucher = selectedVoucher.value
  if (!voucher) return []
  return [
    { label: 'Date', value: formatDate(voucher.onDate) },
    { label: 'Party', value: voucherPartyName(voucher) },
    { label: 'Particulars', value: readText(voucher, ['particulars']) },
    { label: 'Amount', value: formatIndianMoney(readNumber(voucher, ['amount'])) },
    { label: 'Ledger', value: ledgerName(voucher.ledgerId) },
    { label: 'Payment Mode', value: paymentModeLabel(voucher.paymentMode) },
    { label: 'Payment Details', value: readText(voucher, ['paymentDetails']) },
    { label: 'Bank Account', value: bankAccountLabel(voucher.accountNumber) },
    { label: 'Issued By', value: employeeName(voucher.employeeId) },
    { label: 'Remarks', value: readText(voucher, ['remarks']) }
  ]
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [voucherData, ledgerData, partyData, bankData, bankAccountData, employeeData] = await Promise.allSettled([
      get<unknown>('vouchers'),
      get<unknown>('ledgers'),
      get<unknown>('parties'),
      get<unknown>('banks'),
      get<unknown>('bank-accounts'),
      get<unknown>('employees')
    ])

    if (voucherData.status === 'fulfilled') vouchers.value = toRows(voucherData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (bankData.status === 'fulfilled') banks.value = toRows(bankData.value)
    if (bankAccountData.status === 'fulfilled') bankAccounts.value = toRows(bankAccountData.value)
    if (employeeData.status === 'fulfilled') employees.value = toRows(employeeData.value)

    if (!selectedVoucherId.value && vouchers.value.length > 0) {
      await selectVoucher(vouchers.value[0])
    } else if (selectedVoucherId.value && !vouchers.value.some(item => readText(item, ['id'], '') === selectedVoucherId.value)) {
      selectedVoucherId.value = ''
      selectedVoucher.value = null
    }

    const failed = [voucherData, ledgerData, partyData, bankData, bankAccountData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} voucher reference request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load vouchers.'
  } finally {
    loading.value = false
  }
}

async function selectVoucher(voucher: ApiRecord) {
  const id = readText(voucher, ['id'], '')
  selectedVoucherId.value = id
  selectedVoucher.value = voucher
  if (!id) return

  detailLoading.value = true
  try {
    const detail = await get<unknown>(`vouchers/${id}`)
    if (detail && typeof detail === 'object') selectedVoucher.value = detail as ApiRecord
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load voucher details.'
  } finally {
    detailLoading.value = false
  }
}

async function downloadVoucher(mode: 'a4' | 'a5' | 'reprint') {
  const id = selectedVoucherId.value
  if (!id) return

  downloadLoading.value = mode
  error.value = ''
  try {
    const query = mode === 'a5'
      ? { format: 'a5-one', signatures: true }
      : mode === 'reprint'
        ? { reprint: true, signatures: true }
        : { signatures: true }
    await download(`vouchers/${id}/pdf`, query, `${selectedVoucherNumber.value}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download voucher PDF.'
  } finally {
    downloadLoading.value = ''
  }
}

onMounted(refresh)
</script>

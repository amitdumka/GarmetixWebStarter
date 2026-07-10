<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-wallet" class="size-4" /> Daily cash control</p>
          <h2 class="garmetix-dashboard-title">Petty Cash</h2>
          <p class="garmetix-dashboard-subtitle">
            Record daily cash in/out, review saved sheets against the calculated summary, and print or download the A5 cash sheet.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="selectedStoreId" :items="storeOptions" class="sm:w-64" />
          <UInput v-model="reviewDate" type="date" class="sm:w-44" />
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Sheet</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.4fr)_minmax(360px,0.9fr)]">
      <div class="garmetix-table-panel">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Cash Register</h3>
            <p class="garmetix-panel-subtitle">{{ filteredSheets.length }} row(s) shown for {{ selectedStoreName }}</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search sheet rows" class="lg:w-72" />
        </div>

        <div class="overflow-hidden rounded-lg border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[960px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Store</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Opening</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Income</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Outflow</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Cash In Hand</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="filteredSheets.length === 0">
                  <td colspan="8" class="px-3 py-8 text-center text-muted">No petty cash sheets found.</td>
                </tr>
                <tr
                  v-for="sheet in filteredSheets"
                  :key="sheetKey(sheet)"
                  class="bg-default/40"
                  :class="selectedSheetId === readText(sheet, ['id'], '') ? 'outline outline-1 outline-primary/60' : ''"
                >
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDate(sheet.onDate) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ storeName(sheet.storeId) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(sheet.openingBalance) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(sheetIncome(sheet)) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(sheetOutflow(sheet)) }}</td>
                  <td class="whitespace-nowrap px-3 py-2 text-right font-medium">{{ money(sheet.cashInHand) }}</td>
                  <td class="px-3 py-2">
                    <UBadge :color="sheetStatus(sheet).color" variant="subtle">{{ sheetStatus(sheet).label }}</UBadge>
                  </td>
                  <td class="px-3 py-2">
                    <div class="flex flex-wrap justify-end gap-1">
                      <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" :loading="detailLoading && selectedSheetId === readText(sheet, ['id'], '')" @click="selectSheet(sheet)" />
                      <UButton icon="i-lucide-printer" size="xs" color="neutral" variant="ghost" @click="openPrint(sheet)" />
                      <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(sheet)" />
                      <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(sheet)" />
                    </div>
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
            <h3 class="garmetix-panel-title">Selected Sheet</h3>
            <p class="garmetix-panel-subtitle">{{ selectedSheetTitle }}</p>
          </div>
          <UBadge :color="mismatchRows.length ? 'warning' : 'success'" variant="subtle">
            {{ mismatchRows.length ? `${mismatchRows.length} mismatch` : 'Balanced' }}
          </UBadge>
        </div>

        <div v-if="selectedSheet" class="mt-4 space-y-4">
          <BooksMasterTable :columns="detailColumns" :rows="detailRows" empty-text="No petty cash detail rows found." />

          <div v-if="mismatchRows.length" class="rounded-lg border border-warning/40 bg-warning/10 p-3">
            <h4 class="text-sm font-semibold">Calculated Mismatch</h4>
            <BooksMasterTable :columns="mismatchColumns" :rows="mismatchRows" empty-text="No mismatch rows." />
          </div>

          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-printer" size="sm" color="neutral" variant="soft" @click="openPrint(selectedSheet)">Print</UButton>
            <UButton icon="i-lucide-receipt-text" size="sm" color="primary" variant="soft" :loading="downloadLoading" @click="downloadSelectedSheet">A5 PDF</UButton>
            <UBadge color="neutral" variant="subtle">{{ readText(selectedSheet, ['createdBy'], 'System') }}</UBadge>
          </div>
        </div>

        <div v-else class="mt-8 text-center text-sm text-muted">
          Select a sheet to review totals and print readiness.
        </div>
      </aside>
    </section>

    <section class="grid gap-4 xl:grid-cols-3">
      <div class="garmetix-section-card xl:col-span-2">
        <div class="mb-3 flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Calculated Daily Summary</h3>
            <p class="garmetix-panel-subtitle">{{ prepareSummary }}</p>
          </div>
          <UBadge :color="prepareLoading ? 'warning' : 'primary'" variant="subtle">{{ prepareLoading ? 'Loading' : 'Calculated' }}</UBadge>
        </div>
        <BooksMasterTable :columns="prepareColumns" :rows="prepareRows" empty-text="Select a store and date to calculate petty cash." />
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Calculation Notes</h3>
        <ul class="mt-3 space-y-2 text-sm text-muted">
          <li v-for="note in calculationNotes" :key="note" class="border-b border-default pb-2">{{ note }}</li>
          <li v-if="calculationNotes.length === 0">No calculation notes available.</li>
        </ul>
      </div>
    </section>

    <UModal
      v-model:open="formOpen"
      :title="editMode === 'create' ? 'New Petty Cash Sheet' : 'Edit Petty Cash Sheet'"
      description="Enter daily cash in, cash out, and calculated cash in hand."
      :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-3xl lg:max-w-4xl' }"
    >
      <template #body>
        <form class="space-y-3" @submit.prevent="saveSheet">
          <UAlert
            v-if="formPreparation"
            color="info"
            variant="subtle"
            icon="i-lucide-calculator"
            title="Pre-calculated from transactions"
            :description="`${readText(formPreparation, ['openingBalanceSource'])}. Verify and adjust any value before saving.`"
          />
          <div class="flex justify-end">
            <UButton
              v-if="editMode === 'create'"
              size="xs"
              icon="i-lucide-refresh-cw"
              color="neutral"
              variant="outline"
              :loading="calculating"
              @click="prepareFormSheet"
            >
              Recalculate
            </UButton>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Store" required>
              <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" />
            </UFormField>
            <UFormField label="Date" required>
              <UInput v-model="form.onDate" type="date" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Opening balance"><UInput v-model.number="form.openingBalance" type="number" step="0.01" /></UFormField>
            <UFormField label="Cash sales"><UInput v-model.number="form.sales" type="number" step="0.01" /></UFormField>
            <UFormField label="Receipts"><UInput v-model.number="form.receipts" type="number" step="0.01" /></UFormField>
            <UFormField label="Due receipts"><UInput v-model.number="form.dueReceipts" type="number" step="0.01" /></UFormField>
            <UFormField label="Bank withdrawal"><UInput v-model.number="form.bankWithdrawal" type="number" step="0.01" /></UFormField>
            <UFormField label="Expenses"><UInput v-model.number="form.expenses" type="number" step="0.01" /></UFormField>
            <UFormField label="Payments"><UInput v-model.number="form.payments" type="number" step="0.01" /></UFormField>
            <UFormField label="Customer due"><UInput v-model.number="form.customerDue" type="number" step="0.01" /></UFormField>
            <UFormField label="Bank deposit"><UInput v-model.number="form.bankDeposit" type="number" step="0.01" /></UFormField>
            <UFormField label="Non-cash sale"><UInput v-model.number="form.nonCashSale" type="number" step="0.01" /></UFormField>
            <UFormField label="Cash in hand" class="sm:col-span-2"><UInput v-model.number="form.cashInHand" type="number" step="0.01" readonly /></UFormField>
          </div>

          <div class="grid grid-cols-3 gap-2 rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <div><p class="text-muted">Total in</p><p class="font-semibold">{{ money(totalIn) }}</p></div>
            <div><p class="text-muted">Total out</p><p class="font-semibold">{{ money(totalOut) }}</p></div>
            <div><p class="text-muted">Calculated cash</p><p class="font-semibold">{{ money(calculatedCash) }}</p></div>
          </div>

          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">
              {{ editMode === 'create' ? 'Save Sheet' : 'Update Sheet' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="printOpen" title="Petty Cash Sheet" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-3xl' }">
      <template #body>
        <UAlert
          v-if="reconciliationDifferences.length"
          color="warning"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Transaction values do not match"
          description="The sheet was saved and an owner alert was added to Message Logs."
          class="mb-3"
        />

        <div v-if="printSheetData" class="rounded-lg border border-default bg-white p-4 text-neutral-900">
          <div class="mb-3 flex items-start justify-between border-b-2 border-emerald-700 pb-2">
            <div>
              <p class="font-bold">Garmetix</p>
              <p class="text-sm">{{ storeName(printSheetData.storeId) }}</p>
            </div>
            <div class="text-right">
              <p class="text-lg font-semibold">Petty Cash Sheet</p>
              <p class="text-sm">{{ formatDate(printSheetData.onDate) }}</p>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <section class="rounded border border-neutral-300">
              <h3 class="bg-emerald-700 px-3 py-1.5 text-sm font-semibold text-white">Income / Cash In</h3>
              <div v-for="[label, value] in printIncomeRows" :key="String(label)" class="flex justify-between border-t border-neutral-200 px-3 py-1.5 text-sm">
                <span>{{ label }}</span><strong>{{ money(value) }}</strong>
              </div>
              <div class="flex justify-between bg-emerald-50 px-3 py-1.5 text-sm font-semibold">
                <span>Total Income</span><strong>{{ money(totalPrintIncome) }}</strong>
              </div>
            </section>
            <section class="rounded border border-neutral-300">
              <h3 class="bg-orange-700 px-3 py-1.5 text-sm font-semibold text-white">Payment / Cash Out</h3>
              <div v-for="[label, value] in printPaymentRows" :key="String(label)" class="flex justify-between border-t border-neutral-200 px-3 py-1.5 text-sm">
                <span>{{ label }}</span><strong>{{ money(value) }}</strong>
              </div>
              <div class="flex justify-between bg-orange-50 px-3 py-1.5 text-sm font-semibold">
                <span>Total Payment</span><strong>{{ money(totalPrintPayment) }}</strong>
              </div>
            </section>
          </div>

          <div class="mt-3 flex justify-between rounded bg-sky-800 px-3 py-2 font-semibold text-white">
            <span>Cash in hand</span><strong>{{ money(readNumber(printSheetData, ['cashInHand'])) }}</strong>
          </div>
          <div class="mt-6 grid grid-cols-3 gap-6 text-center text-xs text-neutral-600">
            <span class="border-t border-neutral-400 pt-1">Prepared by</span>
            <span class="border-t border-neutral-400 pt-1">Verified by</span>
            <span class="border-t border-neutral-400 pt-1">Owner / Authorised Signatory</span>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-2">
          <UButton color="neutral" variant="outline" @click="printOpen = false">Close</UButton>
          <UButton color="neutral" variant="subtle" icon="i-lucide-download" :loading="downloadLoading" @click="downloadPrintSheet">Download PDF</UButton>
          <UButton icon="i-lucide-printer" color="primary" @click="printSheetA5">Print A5</UButton>
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

useHead({ title: 'Petty Cash - Garmetix Books' })

type BadgeColor = 'success' | 'warning' | 'neutral'

const cashFields = [
  { key: 'openingBalance', label: 'Opening Balance', group: 'Opening' },
  { key: 'sales', label: 'Sales', group: 'Income' },
  { key: 'receipts', label: 'Receipts', group: 'Income' },
  { key: 'dueReceipts', label: 'Due Receipts', group: 'Income' },
  { key: 'bankWithdrawal', label: 'Bank Withdrawal', group: 'Income' },
  { key: 'expenses', label: 'Expenses', group: 'Payment' },
  { key: 'payments', label: 'Payments', group: 'Payment' },
  { key: 'customerDue', label: 'Customer Due', group: 'Adjustment' },
  { key: 'bankDeposit', label: 'Bank Deposit', group: 'Payment' },
  { key: 'nonCashSale', label: 'Non-Cash Sale', group: 'Adjustment' },
  { key: 'cashInHand', label: 'Cash In Hand', group: 'Closing' }
] as const

interface SheetForm {
  id: string
  storeId: string
  onDate: string
  openingBalance: number
  sales: number
  receipts: number
  dueReceipts: number
  bankWithdrawal: number
  expenses: number
  payments: number
  customerDue: number
  bankDeposit: number
  nonCashSale: number
  cashInHand: number
}

function emptySheetForm(): SheetForm {
  return {
    id: '', storeId: '', onDate: localDateInput(new Date()),
    openingBalance: 0, sales: 0, receipts: 0, dueReceipts: 0, bankWithdrawal: 0,
    expenses: 0, payments: 0, customerDue: 0, bankDeposit: 0, nonCashSale: 0, cashInHand: 0
  }
}

const { del, download, get, openBlob, post, put } = useBooksApiClient()
const loading = ref(true)
const detailLoading = ref(false)
const prepareLoading = ref(false)
const downloadLoading = ref(false)
const saving = ref(false)
const calculating = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const selectedStoreId = ref('')
const reviewDate = ref(localDateInput(new Date()))
const selectedSheetId = ref('')
const stores = ref<ApiRecord[]>([])
const sheets = ref<ApiRecord[]>([])
const selectedSheet = ref<ApiRecord | null>(null)
const preparation = ref<ApiRecord | null>(null)

const editMode = ref<'create' | 'edit'>('create')
const formOpen = ref(false)
const form = reactive<SheetForm>(emptySheetForm())
const formPreparation = ref<ApiRecord | null>(null)

const printOpen = ref(false)
const printSheetData = ref<ApiRecord | null>(null)
const reconciliationDifferences = ref<ApiRecord[]>([])

const storeOptions = computed(() => {
  const rows = stores.value.map(item => ({ label: storeName(item.id), value: String(item.id) }))
  return rows.length ? rows : [{ label: 'No stores', value: '' }]
})
const selectedStoreName = computed(() => storeName(selectedStoreId.value))
const selectedSheetTitle = computed(() => selectedSheet.value ? `${storeName(selectedSheet.value.storeId)} - ${formatDate(selectedSheet.value.onDate)}` : 'Select a sheet')
const latestSheet = computed(() => sheets.value[0] ?? null)
const matchingPreparedSheet = computed(() => {
  if (!selectedSheet.value || !preparation.value) return null
  const selectedDate = localDateInput(selectedSheet.value.onDate)
  const preparedDate = localDateInput(preparation.value.onDate)
  return selectedDate === preparedDate && String(selectedSheet.value.storeId) === String(preparation.value.storeId)
    ? preparation.value
    : null
})
const cards = computed(() => [
  { label: 'Sheets', value: sheets.value.length, detail: 'Saved petty cash sheets' },
  { label: 'Last Cash In Hand', value: latestSheet.value ? money(latestSheet.value.cashInHand) : formatIndianMoney(0), detail: latestSheet.value ? formatDate(latestSheet.value.onDate) : 'No saved sheet' },
  { label: 'Calculated Cash', value: preparation.value ? money(preparation.value.cashInHand) : formatIndianMoney(0), detail: selectedStoreName.value },
  { label: 'Mismatches', value: mismatchRows.value.length, detail: 'Selected sheet vs calculated' }
])
const filteredSheets = computed(() => {
  const term = search.value.trim().toLowerCase()
  return sheets.value.filter(item => {
    const storeMatches = !selectedStoreId.value || String(item.storeId) === selectedStoreId.value
    const textMatches = !term || [
      storeName(item.storeId),
      formatDate(item.onDate),
      readText(item, ['createdBy'])
    ].join(' ').toLowerCase().includes(term)
    return storeMatches && textMatches
  })
})
const prepareSummary = computed(() => {
  if (!preparation.value) return 'No calculated summary loaded.'
  return `${selectedStoreName.value} - ${formatDate(preparation.value.onDate)} - ${readText(preparation.value, ['openingBalanceSource'])}`
})
const calculationNotes = computed(() => readArray(preparation.value, ['calculationNotes']).map(item => String(item)))
const detailRows = computed(() => selectedSheet.value ? cashFields.map(field => ({
  group: field.group,
  label: field.label,
  saved: money(selectedSheet.value?.[field.key]),
  calculated: matchingPreparedSheet.value ? money(matchingPreparedSheet.value[field.key]) : '-'
})) : [])
const prepareRows = computed(() => preparation.value ? cashFields.map(field => ({
  group: field.group,
  label: field.label,
  amount: money(preparation.value?.[field.key])
})) : [])
const mismatchRows = computed(() => {
  if (!selectedSheet.value || !matchingPreparedSheet.value) return []
  return cashFields
    .map(field => {
      const saved = readNumber(selectedSheet.value, [field.key])
      const calculated = readNumber(matchingPreparedSheet.value, [field.key])
      return {
        field: field.label,
        saved: money(saved),
        calculated: money(calculated),
        difference: money(saved - calculated),
        mismatch: Math.abs(saved - calculated) > 0.01
      }
    })
    .filter(item => item.mismatch)
})

const totalIn = computed(() => form.openingBalance + form.sales + form.receipts + form.dueReceipts + form.bankWithdrawal)
const totalOut = computed(() => form.expenses + form.payments + form.customerDue + form.bankDeposit + form.nonCashSale)
const calculatedCash = computed(() => totalIn.value - totalOut.value)

const printIncomeRows = computed(() => printSheetData.value ? [
  ['Opening balance', readNumber(printSheetData.value, ['openingBalance'])],
  ['Cash sales', readNumber(printSheetData.value, ['sales'])],
  ['Receipts', readNumber(printSheetData.value, ['receipts'])],
  ['Due receipts', readNumber(printSheetData.value, ['dueReceipts'])],
  ['Bank withdrawal', readNumber(printSheetData.value, ['bankWithdrawal'])]
] as Array<[string, number]> : [])
const printPaymentRows = computed(() => printSheetData.value ? [
  ['Expenses', readNumber(printSheetData.value, ['expenses'])],
  ['Payments', readNumber(printSheetData.value, ['payments'])],
  ['Customer due', readNumber(printSheetData.value, ['customerDue'])],
  ['Bank deposit', readNumber(printSheetData.value, ['bankDeposit'])],
  ['Non-cash sale', readNumber(printSheetData.value, ['nonCashSale'])]
] as Array<[string, number]> : [])
const totalPrintIncome = computed(() => printIncomeRows.value.reduce((sum, [, value]) => sum + value, 0))
const totalPrintPayment = computed(() => printPaymentRows.value.reduce((sum, [, value]) => sum + value, 0))

const detailColumns = [
  { key: 'group', label: 'Section' },
  { key: 'label', label: 'Particular' },
  { key: 'saved', label: 'Saved' },
  { key: 'calculated', label: 'Calculated' }
]
const prepareColumns = [
  { key: 'group', label: 'Section' },
  { key: 'label', label: 'Particular' },
  { key: 'amount', label: 'Calculated Amount' }
]
const mismatchColumns = [
  { key: 'field', label: 'Field' },
  { key: 'saved', label: 'Saved' },
  { key: 'calculated', label: 'Calculated' },
  { key: 'difference', label: 'Difference' }
]

function localDateInput(value: unknown) {
  const date = value instanceof Date ? value : new Date(String(value || ''))
  if (Number.isNaN(date.getTime())) return ''
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function storeName(id: unknown) {
  return readText(stores.value.find(item => item.id === id), ['name', 'storeName'], 'Store')
}

function sheetKey(sheet: ApiRecord) {
  return readText(sheet, ['id', 'onDate'])
}

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function sheetIncome(sheet: ApiRecord) {
  return readNumber(sheet, ['sales']) + readNumber(sheet, ['receipts']) + readNumber(sheet, ['dueReceipts']) + readNumber(sheet, ['bankWithdrawal'])
}

function sheetOutflow(sheet: ApiRecord) {
  return readNumber(sheet, ['expenses']) + readNumber(sheet, ['payments']) + readNumber(sheet, ['customerDue']) + readNumber(sheet, ['bankDeposit']) + readNumber(sheet, ['nonCashSale'])
}

function sheetStatus(sheet: ApiRecord): { label: string, color: BadgeColor } {
  if (!matchingPreparedSheet.value || readText(sheet, ['id'], '') !== selectedSheetId.value) return { label: 'Saved', color: 'neutral' }
  return mismatchRows.value.length ? { label: 'Needs Review', color: 'warning' } : { label: 'Balanced', color: 'success' }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [storeData, sheetData] = await Promise.allSettled([
      get<unknown>('stores'),
      get<unknown>('petty-cash-sheets')
    ])
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (sheetData.status === 'fulfilled') sheets.value = toRows(sheetData.value).sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
    if (!selectedStoreId.value) {
      selectedStoreId.value = String(latestSheet.value?.storeId ?? stores.value[0]?.id ?? '')
    }
    if (!selectedSheetId.value && filteredSheets.value.length > 0) {
      await selectSheet(filteredSheets.value[0])
    }
    await loadPreparation()
    const failed = [storeData, sheetData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} petty cash request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load petty cash review.'
  } finally {
    loading.value = false
  }
}

async function selectSheet(sheet: ApiRecord) {
  const id = readText(sheet, ['id'], '')
  selectedSheetId.value = id
  selectedSheet.value = sheet
  if (sheet.storeId) selectedStoreId.value = String(sheet.storeId)
  if (sheet.onDate) reviewDate.value = localDateInput(sheet.onDate)
  if (!id) return

  detailLoading.value = true
  try {
    const detail = await get<unknown>(`petty-cash-sheets/${id}`)
    if (detail && typeof detail === 'object') selectedSheet.value = detail as ApiRecord
    await loadPreparation()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load petty cash sheet.'
  } finally {
    detailLoading.value = false
  }
}

async function loadPreparation() {
  if (!selectedStoreId.value || !reviewDate.value) {
    preparation.value = null
    return
  }

  prepareLoading.value = true
  try {
    const result = await get<unknown>('petty-cash-sheets/prepare', {
      storeId: selectedStoreId.value,
      onDate: reviewDate.value
    })
    preparation.value = result && typeof result === 'object' ? result as ApiRecord : null
  } catch (caught) {
    preparation.value = null
    error.value = caught instanceof Error ? caught.message : 'Unable to calculate petty cash summary.'
  } finally {
    prepareLoading.value = false
  }
}

async function downloadSelectedSheet() {
  const id = selectedSheetId.value
  if (!id) return
  downloadLoading.value = true
  error.value = ''
  try {
    await download(`petty-cash-sheets/${id}/pdf`, undefined, `${selectedSheetTitle.value}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download petty cash PDF.'
  } finally {
    downloadLoading.value = false
  }
}

async function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptySheetForm())
  form.storeId = selectedStoreId.value || stores.value[0]?.id as string || ''
  formPreparation.value = null
  message.value = ''
  error.value = ''
  formOpen.value = true
  await prepareFormSheet()
}

function startEdit(sheet: ApiRecord) {
  editMode.value = 'edit'
  Object.assign(form, emptySheetForm(), {
    id: readText(sheet, ['id'], ''),
    storeId: readText(sheet, ['storeId'], ''),
    onDate: localDateInput(sheet.onDate),
    openingBalance: readNumber(sheet, ['openingBalance']),
    sales: readNumber(sheet, ['sales']),
    receipts: readNumber(sheet, ['receipts']),
    dueReceipts: readNumber(sheet, ['dueReceipts']),
    bankWithdrawal: readNumber(sheet, ['bankWithdrawal']),
    expenses: readNumber(sheet, ['expenses']),
    payments: readNumber(sheet, ['payments']),
    customerDue: readNumber(sheet, ['customerDue']),
    bankDeposit: readNumber(sheet, ['bankDeposit']),
    nonCashSale: readNumber(sheet, ['nonCashSale']),
    cashInHand: readNumber(sheet, ['cashInHand'])
  })
  formPreparation.value = null
  message.value = ''
  error.value = ''
  formOpen.value = true
}

async function prepareFormSheet() {
  if (!form.storeId || !form.onDate || editMode.value !== 'create') return
  calculating.value = true
  try {
    const result = await get<ApiRecord>('petty-cash-sheets/prepare', { storeId: form.storeId, onDate: form.onDate })
    formPreparation.value = result
    Object.assign(form, {
      openingBalance: readNumber(result, ['openingBalance']),
      sales: readNumber(result, ['sales']),
      receipts: readNumber(result, ['receipts']),
      dueReceipts: readNumber(result, ['dueReceipts']),
      bankWithdrawal: readNumber(result, ['bankWithdrawal']),
      expenses: readNumber(result, ['expenses']),
      payments: readNumber(result, ['payments']),
      customerDue: readNumber(result, ['customerDue']),
      bankDeposit: readNumber(result, ['bankDeposit']),
      nonCashSale: readNumber(result, ['nonCashSale'])
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not calculate the petty cash sheet.'
  } finally {
    calculating.value = false
  }
}

function roundMoney(value: number) {
  return Math.round((Number(value || 0) + Number.EPSILON) * 100) / 100
}

async function saveSheet() {
  if (!form.storeId) { error.value = 'Select store before saving petty cash.'; return }
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    const creating = editMode.value === 'create'
    const payload: Record<string, unknown> = {
      storeId: form.storeId,
      onDate: `${form.onDate}T00:00:00`,
      openingBalance: form.openingBalance,
      sales: form.sales,
      receipts: form.receipts,
      dueReceipts: form.dueReceipts,
      bankWithdrawal: form.bankWithdrawal,
      expenses: form.expenses,
      payments: form.payments,
      customerDue: form.customerDue,
      bankDeposit: form.bankDeposit,
      nonCashSale: form.nonCashSale,
      cashInHand: roundMoney(calculatedCash.value),
      createdBy: 'AutoAdmin'
    }

    let result: ApiRecord
    if (!creating && form.id) {
      result = await put<ApiRecord>(`petty-cash-sheets/${form.id}`, { ...payload, id: form.id })
      message.value = 'Petty cash sheet updated.'
    } else {
      result = await post<ApiRecord>('petty-cash-sheets', payload)
      message.value = 'Petty cash sheet saved.'
    }

    reconciliationDifferences.value = readArray(result, ['differences'])
    printSheetData.value = (result?.sheet as ApiRecord) ?? payload
    formOpen.value = false
    await refresh()
    printOpen.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not save petty cash sheet.'
  } finally {
    saving.value = false
  }
}

function openPrint(sheet: ApiRecord | null) {
  if (!sheet) return
  printSheetData.value = sheet
  reconciliationDifferences.value = []
  printOpen.value = true
}

async function downloadPrintSheet() {
  const id = readText(printSheetData.value, ['id'], '')
  if (!id) return
  downloadLoading.value = true
  error.value = ''
  try {
    await download(`petty-cash-sheets/${id}/pdf`, undefined, `petty-cash-${formatDate(printSheetData.value?.onDate).replace(/\//g, '-')}.pdf`)
    message.value = 'Petty cash PDF downloaded.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download petty cash PDF.'
  } finally {
    downloadLoading.value = false
  }
}

async function printSheetA5() {
  const id = readText(printSheetData.value, ['id'], '')
  if (!id) return
  error.value = ''
  try {
    await openBlob(`petty-cash-sheets/${id}/pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to open petty cash PDF.'
  }
}

function askDelete(sheet: ApiRecord) {
  const id = readText(sheet, ['id'], '')
  if (!id) return
  if (!window.confirm(`Delete petty cash sheet for ${formatDate(sheet.onDate)}?`)) return
  confirmDelete(id)
}

async function confirmDelete(id: string) {
  error.value = ''
  message.value = ''
  try {
    await del<unknown>(`petty-cash-sheets/${id}`)
    message.value = 'Petty cash sheet deleted.'
    if (selectedSheetId.value === id) {
      selectedSheetId.value = ''
      selectedSheet.value = null
    }
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete petty cash sheet.'
  }
}

watch([selectedStoreId, reviewDate], () => {
  void loadPreparation()
})

watch(() => [form.storeId, form.onDate], () => {
  if (formOpen.value && editMode.value === 'create') void prepareFormSheet()
})

watch(calculatedCash, (value) => {
  form.cashInHand = roundMoney(value)
})

onMounted(refresh)
</script>

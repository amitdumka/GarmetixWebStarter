<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="text-sm text-muted">Daily cash control</p>
          <h2 class="mt-1 text-2xl font-semibold">Petty Cash Review</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review saved petty cash sheets against the calculated daily cash summary. Saving, editing and owner alerts stay in the existing controlled workflow.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="selectedStoreId" :items="storeOptions" class="sm:w-64" />
          <UInput v-model="reviewDate" type="date" class="sm:w-44" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.4fr)_minmax(360px,0.9fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="text-base font-semibold">Saved Sheets</h3>
            <p class="text-xs text-muted">{{ filteredSheets.length }} row(s) shown for {{ selectedStoreName }}</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search sheet rows" class="lg:w-72" />
        </div>

        <div class="overflow-hidden border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[880px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Store</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Opening</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Income</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Outflow</th>
                  <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Cash In Hand</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
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
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" :loading="detailLoading && selectedSheetId === readText(sheet, ['id'], '')" @click="selectSheet(sheet)">
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
            <h3 class="text-base font-semibold">Selected Sheet</h3>
            <p class="text-xs text-muted">{{ selectedSheetTitle }}</p>
          </div>
          <UBadge :color="mismatchRows.length ? 'warning' : 'success'" variant="subtle">
            {{ mismatchRows.length ? `${mismatchRows.length} mismatch` : 'Balanced' }}
          </UBadge>
        </div>

        <div v-if="selectedSheet" class="mt-4 space-y-4">
          <BooksMasterTable :columns="detailColumns" :rows="detailRows" empty-text="No petty cash detail rows found." />

          <div v-if="mismatchRows.length" class="border border-warning/40 bg-warning/10 p-3">
            <h4 class="text-sm font-semibold">Calculated Mismatch</h4>
            <BooksMasterTable :columns="mismatchColumns" :rows="mismatchRows" empty-text="No mismatch rows." />
          </div>

          <div class="flex flex-wrap gap-2">
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
      <div class="border border-default bg-muted/10 p-4 xl:col-span-2">
        <div class="mb-3 flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">Calculated Daily Summary</h3>
            <p class="text-xs text-muted">{{ prepareSummary }}</p>
          </div>
          <UBadge :color="prepareLoading ? 'warning' : 'primary'" variant="subtle">{{ prepareLoading ? 'Loading' : 'Calculated' }}</UBadge>
        </div>
        <BooksMasterTable :columns="prepareColumns" :rows="prepareRows" empty-text="Select a store and date to calculate petty cash." />
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <h3 class="text-base font-semibold">Calculation Notes</h3>
        <ul class="mt-3 space-y-2 text-sm text-muted">
          <li v-for="note in calculationNotes" :key="note" class="border-b border-default pb-2">{{ note }}</li>
          <li v-if="calculationNotes.length === 0">No calculation notes available.</li>
        </ul>
      </div>
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

const { download, get } = useBooksApiClient()
const loading = ref(true)
const detailLoading = ref(false)
const prepareLoading = ref(false)
const downloadLoading = ref(false)
const error = ref('')
const search = ref('')
const selectedStoreId = ref('')
const reviewDate = ref(localDateInput(new Date()))
const selectedSheetId = ref('')
const stores = ref<ApiRecord[]>([])
const sheets = ref<ApiRecord[]>([])
const selectedSheet = ref<ApiRecord | null>(null)
const preparation = ref<ApiRecord | null>(null)

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
    if (sheetData.status === 'fulfilled') sheets.value = toRows(sheetData.value)
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

watch([selectedStoreId, reviewDate], () => {
  void loadPreparation()
})

onMounted(refresh)
</script>

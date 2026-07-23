<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-cog" class="size-4" /> Sale</p>
          <h2 class="garmetix-dashboard-title">Invoice Books Adjustment</h2>
          <p class="garmetix-dashboard-subtitle">
            Move a Sale invoice's GST filing period without changing the invoice itself. Book Date defaults to the
            Sale Date; GST Returns (GSTR-1/3B/HSN summary/invoice register) file the invoice under its Book Date,
            not its Sale Date. Payments and accounting always keep using the real Sale Date.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" :close-button="{ icon: 'i-lucide-x' }" @close="error = ''" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" :close-button="{ icon: 'i-lucide-x' }" @close="message = ''" />

    <section class="garmetix-section-card">
      <div class="flex flex-wrap items-end gap-3">
        <UFormField label="Month">
          <USelect v-model="month" :items="monthOptions" class="w-40" />
        </UFormField>
        <UFormField label="Year">
          <USelect v-model="year" :items="yearOptions" class="w-28" />
        </UFormField>
        <UFormField label="Store" v-if="storeItems.length > 1">
          <USelect v-model="storeId" :items="storeItems" class="w-48" />
        </UFormField>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search invoice, customer, mobile" class="sm:w-72" />
        <UButton icon="i-lucide-filter" color="primary" variant="soft" :loading="loading" @click="() => { page = 1; load() }">Apply</UButton>
      </div>
    </section>

    <section class="grid gap-3 sm:grid-cols-3">
      <UCard :ui="{ body: 'p-4' }"><p class="text-xs text-muted">Invoices This Month</p><p class="text-xl font-semibold">{{ total }}</p></UCard>
      <UCard :ui="{ body: 'p-4' }"><p class="text-xs text-muted">Book Date Adjusted</p><p class="text-xl font-semibold" :class="adjustedCount > 0 ? 'text-warning' : ''">{{ adjustedCount }}</p></UCard>
      <UCard :ui="{ body: 'p-4' }"><p class="text-xs text-muted">Showing</p><p class="text-xl font-semibold">Page {{ page }} / {{ totalPages }}</p></UCard>
    </section>

    <section class="garmetix-section-card">
      <div class="garmetix-table-panel overflow-x-auto">
        <table class="w-full min-w-[1000px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Invoice</th>
              <th class="px-3 py-2">Store</th>
              <th class="px-3 py-2">Sale Date</th>
              <th class="px-3 py-2">Customer</th>
              <th class="px-3 py-2 text-right">Amount</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Book Date (GST filing period)</th>
              <th class="px-3 py-2">Last Adjusted</th>
              <th class="px-3 py-2 text-right">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="!rows.length"><td colspan="9" class="px-3 py-8 text-center text-muted">No sale invoices found for this month.</td></tr>
            <tr v-for="row in rows" :key="row.id">
              <td class="px-3 py-2 tabular-nums">{{ row.invoiceNumber }}<UBadge v-if="row.returnInvoice" size="xs" color="warning" variant="soft" class="ml-1">Return</UBadge></td>
              <td class="px-3 py-2">{{ row.storeName }}</td>
              <td class="px-3 py-2">{{ formatDate(row.onDate) }}</td>
              <td class="px-3 py-2">{{ row.customerName }}<p class="text-xs text-muted">{{ row.customerMobileNumber }}</p></td>
              <td class="px-3 py-2 text-right tabular-nums">{{ money(row.billAmount) }}</td>
              <td class="px-3 py-2"><UBadge size="xs" :color="statusColor(row.invoiceStatus)" variant="soft">{{ row.invoiceStatus }}</UBadge></td>
              <td class="px-3 py-2">
                <div class="flex items-center gap-2">
                  <UInput v-model="row.editBookDate" type="date" class="w-40" />
                  <UBadge v-if="row.isBookDateAdjusted" size="xs" color="warning" variant="soft">Adjusted</UBadge>
                </div>
              </td>
              <td class="px-3 py-2 text-xs text-muted">
                <template v-if="row.bookDateUpdatedAt">{{ formatDate(row.bookDateUpdatedAt) }} by {{ row.bookDateUpdatedBy || '-' }}</template>
                <template v-else>-</template>
              </td>
              <td class="px-3 py-2 text-right">
                <UButton
                  size="xs" color="primary" variant="soft" icon="i-lucide-save"
                  :disabled="row.editBookDate === row.bookDate.slice(0, 10)"
                  :loading="row.saving"
                  @click="saveBookDate(row)"
                >Save</UButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ rows.length }} of {{ total }} invoice(s)</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" :loading="loading" @click="() => { page--; load() }">Prev</UButton>
          <span>{{ page }} / {{ totalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" :loading="loading" @click="() => { page++; load() }">Next</UButton>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Invoice Books Adjustment - Garmetix' })

const { get, put } = useMainApiClient()

type BookAdjustmentRow = {
  id: string
  invoiceNumber: string
  onDate: string
  bookDate: string
  editBookDate: string
  isBookDateAdjusted: boolean
  storeName: string
  customerName: string
  customerMobileNumber: string
  invoiceStatus: string
  billAmount: number
  returnInvoice: boolean
  bookDateUpdatedAt: string | null
  bookDateUpdatedBy: string | null
  saving: boolean
}

const loading = ref(false)
const error = ref('')
const message = ref('')

const today = new Date()
const month = ref(today.getMonth() + 1)
const year = ref(today.getFullYear())
const storeId = ref('all')
const search = ref('')
const page = ref(1)
const pageSize = 50
const total = ref(0)
const adjustedCount = ref(0)
const rows = ref<BookAdjustmentRow[]>([])
const stores = ref<ApiRecord[]>([])

const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const monthOptions = monthNames.map((name, index) => ({ label: name, value: index + 1 }))
const yearOptions = Array.from({ length: 6 }, (_, index) => today.getFullYear() - 3 + index).map(y => ({ label: String(y), value: y }))

const storeItems = computed(() => [
  { label: 'All Stores', value: 'all' },
  ...stores.value.map(store => ({ label: readText(store, ['name'], 'Store'), value: readText(store, ['id'], '') }))
])

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))

function money(value: number) {
  return formatIndianMoney(value)
}
function statusColor(status: string) {
  const normalized = (status || '').toLowerCase()
  if (normalized.includes('cancel')) return 'error'
  if (normalized.includes('paid')) return 'success'
  return 'neutral'
}

async function loadStores() {
  try {
    const result = await get<unknown>('stores')
    stores.value = toRows(result)
  } catch {
    stores.value = []
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const result = await get<ApiRecord>('sale/book-adjustment', {
      year: year.value,
      month: month.value,
      storeId: storeId.value !== 'all' ? storeId.value : undefined,
      search: search.value.trim() || undefined,
      page: page.value,
      pageSize
    })
    total.value = readNumber(result, ['totalCount'])
    adjustedCount.value = readNumber(result, ['adjustedCount'])
    const items = toRows((result as ApiRecord)?.items)
    rows.value = items.map(item => {
      const bookDate = readText(item, ['bookDate'], '')
      return {
        id: readText(item, ['id'], ''),
        invoiceNumber: readText(item, ['invoiceNumber']),
        onDate: readText(item, ['onDate'], ''),
        bookDate,
        editBookDate: bookDate.slice(0, 10),
        isBookDateAdjusted: Boolean(item.isBookDateAdjusted),
        storeName: readText(item, ['storeName'], 'Store'),
        customerName: readText(item, ['customerName'], 'Walk-in Customer'),
        customerMobileNumber: readText(item, ['customerMobileNumber'], ''),
        invoiceStatus: readText(item, ['invoiceStatus']),
        billAmount: readNumber(item, ['billAmount']),
        returnInvoice: Boolean(item.returnInvoice),
        bookDateUpdatedAt: item.bookDateUpdatedAt ? String(item.bookDateUpdatedAt) : null,
        bookDateUpdatedBy: item.bookDateUpdatedBy ? String(item.bookDateUpdatedBy) : null,
        saving: false
      }
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load invoices.'
  } finally {
    loading.value = false
  }
}

async function saveBookDate(row: BookAdjustmentRow) {
  if (!row.editBookDate) return
  row.saving = true
  error.value = ''
  message.value = ''
  try {
    const updated = await put<ApiRecord>(`sale/book-adjustment/${row.id}`, { bookDate: row.editBookDate })
    const bookDate = readText(updated, ['bookDate'], row.editBookDate)
    row.bookDate = bookDate
    row.editBookDate = bookDate.slice(0, 10)
    row.isBookDateAdjusted = Boolean(updated?.isBookDateAdjusted)
    row.bookDateUpdatedAt = updated?.bookDateUpdatedAt ? String(updated.bookDateUpdatedAt) : null
    row.bookDateUpdatedBy = updated?.bookDateUpdatedBy ? String(updated.bookDateUpdatedBy) : null
    adjustedCount.value = rows.value.filter(item => item.isBookDateAdjusted).length
    message.value = `Book date updated for invoice ${row.invoiceNumber}.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to update book date.'
  } finally {
    row.saving = false
  }
}

onMounted(async () => {
  await loadStores()
  await load()
})
</script>

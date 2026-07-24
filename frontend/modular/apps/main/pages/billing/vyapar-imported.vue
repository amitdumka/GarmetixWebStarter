<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-spreadsheet" class="size-4" /> Sale</p>
          <h2 class="garmetix-dashboard-title">Vyapar Imported Invoices</h2>
          <p class="garmetix-dashboard-subtitle">Sale invoices already imported from Vyapar exports.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <NuxtLink to="/billing/vyapar-import"><UButton icon="i-lucide-upload" color="primary" variant="soft">New Import</UButton></NuxtLink>
          <NuxtLink to="/billing/vyapar-import-batches"><UButton icon="i-lucide-layers" color="neutral" variant="soft">Import Batches</UButton></NuxtLink>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" :close-button="{ icon: 'i-lucide-x' }" @close="message = ''" />

    <section class="garmetix-section-card">
      <div class="flex flex-wrap items-end gap-3">
        <UFormField label="From">
          <UInput v-model="fromDate" type="date" class="w-40" />
        </UFormField>
        <UFormField label="To">
          <UInput v-model="toDate" type="date" class="w-40" />
        </UFormField>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search invoice, customer, mobile" class="sm:w-72" />
        <UButton icon="i-lucide-filter" color="primary" variant="soft" :loading="loading" @click="() => { page = 1; load() }">Apply</UButton>
      </div>
    </section>

    <section v-if="selectedIds.size" class="garmetix-section-card flex flex-wrap items-center justify-between gap-2">
      <p class="text-sm">{{ selectedIds.size }} invoice(s) selected for undo.</p>
      <UButton icon="i-lucide-undo-2" color="error" variant="soft" @click="openBulkUndo">Undo Selected</UButton>
    </section>

    <section class="grid gap-3 sm:grid-cols-3">
      <UCard :ui="{ body: 'p-4' }"><p class="text-xs text-muted">Imported Bill Amount</p><p class="text-xl font-semibold">{{ money(billTotal) }}</p></UCard>
      <UCard :ui="{ body: 'p-4' }"><p class="text-xs text-muted">Paid</p><p class="text-xl font-semibold text-success">{{ money(paidTotal) }}</p></UCard>
      <UCard :ui="{ body: 'p-4' }"><p class="text-xs text-muted">Balance</p><p class="text-xl font-semibold" :class="balanceTotal > 0 ? 'text-warning' : ''">{{ money(balanceTotal) }}</p></UCard>
    </section>

    <section class="garmetix-section-card">
      <div class="garmetix-table-panel overflow-x-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2"><input type="checkbox" :checked="allOnPageSelected" @change="toggleSelectAllOnPage"></th>
              <th class="px-3 py-2">Invoice</th>
              <th class="px-3 py-2">Vyapar Source</th>
              <th class="px-3 py-2">Date</th>
              <th class="px-3 py-2">Customer</th>
              <th class="px-3 py-2 text-right">Amount</th>
              <th class="px-3 py-2 text-right">Paid</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Remarks</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="!rows.length"><td colspan="9" class="px-3 py-8 text-center text-muted">No imported invoices found.</td></tr>
            <tr v-for="row in rows" :key="row.id">
              <td class="px-3 py-2">
                <input v-if="!row.invoiceStatus.toLowerCase().includes('cancel')" type="checkbox" :checked="selectedIds.has(row.id)" @change="toggleSelect(row.id)">
              </td>
              <td class="px-3 py-2 tabular-nums">{{ row.invoiceNumber }}</td>
              <td class="px-3 py-2 tabular-nums">{{ row.sourceInvoiceNumber }}</td>
              <td class="px-3 py-2">{{ formatDate(row.invoiceDate) }}</td>
              <td class="px-3 py-2">{{ row.customerName }}<p class="text-xs text-muted">{{ row.customerMobileNumber }}</p></td>
              <td class="px-3 py-2 text-right tabular-nums">{{ money(row.billAmount) }}</td>
              <td class="px-3 py-2 text-right tabular-nums">{{ money(row.paidAmount) }}</td>
              <td class="px-3 py-2"><UBadge size="xs" :color="statusColor(row.invoiceStatus)" variant="soft">{{ row.invoiceStatus }}</UBadge></td>
              <td class="px-3 py-2 text-xs text-muted">{{ row.remarks }}</td>
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

    <UModal v-model:open="bulkUndoOpen" title="Undo Selected Invoices" description="Each selected invoice is individually cancelled - stock, payment and accounting are reversed for every one of them. This cannot be undone from here.">
      <template #body>
        <div class="grid gap-3">
          <p class="text-sm">Invoices to undo: <strong>{{ selectedIds.size }}</strong></p>
          <p class="text-sm">Type <strong>UNDO SELECTED</strong> to confirm.</p>
          <UInput v-model="bulkUndoConfirmText" placeholder="UNDO SELECTED" />
          <div v-if="bulkUndoResults.length" class="max-h-48 overflow-y-auto text-xs">
            <p v-for="result in bulkUndoResults" :key="result.id" :class="result.ok ? 'text-success' : 'text-error'">
              {{ result.invoiceNumber }}: {{ result.ok ? 'Undone' : result.message }}
            </p>
          </div>
          <div class="flex justify-end gap-2">
            <UButton color="error" icon="i-lucide-undo-2" :disabled="bulkUndoConfirmText !== 'UNDO SELECTED'" :loading="bulkUndoing" @click="confirmBulkUndo">Undo Selected</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { getStoredUser } from '@garmetix/shared-auth'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Vyapar Imported Invoices - Garmetix' })

const { get, del } = useMainApiClient()

const loading = ref(false)
const error = ref('')
const message = ref('')
const fromDate = ref(startOfMonth())
const toDate = ref(todayIso())
const search = ref('')
const page = ref(1)
const pageSize = 50
const total = ref(0)
const billTotal = ref(0)
const paidTotal = ref(0)
const balanceTotal = ref(0)
const items = ref<ApiRecord[]>([])

const selectedIds = reactive(new Set<string>())
const bulkUndoOpen = ref(false)
const bulkUndoConfirmText = ref('')
const bulkUndoing = ref(false)
const bulkUndoResults = ref<Array<{ id: string, invoiceNumber: string, ok: boolean, message: string }>>([])

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}
function startOfMonth() {
  const date = new Date()
  return new Date(date.getFullYear(), date.getMonth(), 1).toISOString().slice(0, 10)
}

function money(value: number) {
  return formatIndianMoney(value)
}
function statusColor(status: string) {
  const normalized = (status || '').toLowerCase()
  if (normalized.includes('cancel')) return 'error'
  if (normalized.includes('paid') || normalized === 'active') return 'success'
  return 'neutral'
}

const rows = computed(() => items.value.map(item => ({
  id: readText(item, ['id'], ''),
  invoiceNumber: readText(item, ['invoiceNumber']),
  sourceInvoiceNumber: readText(item, ['sourceInvoiceNumber'], '-'),
  invoiceDate: item.invoiceDate,
  customerName: readText(item, ['customerName']),
  customerMobileNumber: readText(item, ['customerMobileNumber'], ''),
  billAmount: readNumber(item, ['billAmount']),
  paidAmount: readNumber(item, ['paidAmount']),
  invoiceStatus: readText(item, ['invoiceStatus']),
  remarks: readText(item, ['remarks'], '')
})))
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize)))
const selectableRows = computed(() => rows.value.filter(row => !row.invoiceStatus.toLowerCase().includes('cancel')))
const allOnPageSelected = computed(() => selectableRows.value.length > 0 && selectableRows.value.every(row => selectedIds.has(row.id)))

function toggleSelect(id: string) {
  if (selectedIds.has(id)) selectedIds.delete(id)
  else selectedIds.add(id)
}
function toggleSelectAllOnPage() {
  if (allOnPageSelected.value) {
    selectableRows.value.forEach(row => selectedIds.delete(row.id))
  } else {
    selectableRows.value.forEach(row => selectedIds.add(row.id))
  }
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const storedUser = getStoredUser(window.localStorage)
    const result = await get<ApiRecord>('sale-import/vyapar/imported', {
      companyId: storedUser?.companyId || undefined,
      storeId: storedUser?.storeId || undefined,
      from: fromDate.value || undefined,
      to: toDate.value || undefined,
      q: search.value.trim() || undefined,
      page: page.value,
      pageSize
    })
    items.value = toRows(result?.items ?? result)
    total.value = readNumber(result, ['total'])
    billTotal.value = readNumber(result, ['billAmountTotal'])
    paidTotal.value = readNumber(result, ['paidAmountTotal'])
    balanceTotal.value = readNumber(result, ['balanceAmountTotal'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load imported invoices.'
  } finally {
    loading.value = false
  }
}

function openBulkUndo() {
  if (!selectedIds.size) return
  bulkUndoConfirmText.value = ''
  bulkUndoResults.value = []
  bulkUndoOpen.value = true
}

async function confirmBulkUndo() {
  if (bulkUndoConfirmText.value !== 'UNDO SELECTED') return
  bulkUndoing.value = true
  error.value = ''
  const targets = rows.value.filter(row => selectedIds.has(row.id))
  const results: Array<{ id: string, invoiceNumber: string, ok: boolean, message: string }> = []
  for (const row of targets) {
    try {
      await del<unknown>(`billing/sales/${row.id}`)
      results.push({ id: row.id, invoiceNumber: row.invoiceNumber, ok: true, message: '' })
      selectedIds.delete(row.id)
    } catch (caught) {
      results.push({ id: row.id, invoiceNumber: row.invoiceNumber, ok: false, message: caught instanceof Error ? caught.message : 'Failed' })
    }
    bulkUndoResults.value = [...results]
  }
  bulkUndoing.value = false
  const okCount = results.filter(item => item.ok).length
  message.value = `${okCount} of ${results.length} invoice(s) undone.`
  await load()
}

onMounted(load)
</script>

<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-qr-code" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">E-Invoice & E-Way Bill</h2>
          <p class="garmetix-dashboard-subtitle">
            Placeholder tracking for e-invoice (IRN) and e-way bill lifecycle state. The provider registry (GST API Setup) already
            supports NIC/e-way bill provider types and every EINVOICE_*/EWAYBILL_* feature - live generation against the government
            portals is not implemented yet, so every "Generate" action here honestly reports "not configured" or "not implemented yet"
            rather than pretending to succeed.
          </p>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <!-- E-Invoice -->
    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">E-Invoice (IRN)</h3>
          <p class="text-xs text-muted">B2B Sale invoices and their IRN generation status.</p>
        </div>
        <UBadge :color="einvoiceReport?.providerConfigured ? 'success' : 'warning'" variant="subtle">
          {{ einvoiceReport?.providerConfigured ? 'Provider configured' : 'No provider configured' }}
        </UBadge>
      </div>

      <div class="grid gap-3 lg:grid-cols-[160px_160px_1fr_140px_auto]">
        <UFormField label="From"><UInput v-model="einvoiceFilters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="einvoiceFilters.toDate" type="date" /></UFormField>
        <UFormField label="Search"><UInput v-model="einvoiceFilters.search" placeholder="Invoice, customer" @keyup.enter="refreshEinvoice(true)" /></UFormField>
        <UFormField label="Rows"><USelect v-model="einvoiceFilters.pageSize" :items="pageSizeItems" /></UFormField>
        <div class="flex items-end gap-2">
          <UCheckbox v-model="einvoiceFilters.onlyPending" label="Only pending" />
          <UButton icon="i-lucide-search" :loading="einvoiceLoading" label="Apply" @click="refreshEinvoice(true)" />
        </div>
      </div>

      <div class="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">B2B invoices</p>
          <p class="garmetix-metric-value">{{ einvoiceReport?.totalRows ?? 0 }}</p>
          <p class="garmetix-metric-caption">In the selected range</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Pending</p>
          <p class="garmetix-metric-value">{{ einvoiceReport?.pendingCount ?? 0 }}</p>
          <p class="garmetix-metric-caption">No Generated IRN yet</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Generated</p>
          <p class="garmetix-metric-value">{{ einvoiceReport?.generatedCount ?? 0 }}</p>
          <p class="garmetix-metric-caption">Has a Generated IRN record</p>
        </div>
      </div>

      <div v-if="einvoiceReport" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[980px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Customer</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Bill</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">IRN</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="row in einvoiceReport.rows" :key="row.invoiceId">
              <td class="whitespace-nowrap px-3 py-2 font-medium text-primary">{{ row.invoiceNumber }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(row.onDate) }}</td>
              <td class="px-3 py-2">
                <div>{{ row.customerName }}</div>
                <div class="text-xs text-muted">{{ row.customerGSTIN || 'No GSTIN' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(row.billAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <UBadge :color="row.status === 'Generated' ? 'success' : 'neutral'" variant="subtle">{{ row.status }}</UBadge>
                <div v-if="row.errorMessage" class="mt-1 max-w-[260px] text-xs text-muted">{{ row.errorMessage }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-xs">{{ row.irn || '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <UButton size="xs" color="neutral" variant="subtle" :loading="isGeneratingEinvoice(row.invoiceId)" @click="generateEinvoice(row)">Generate</UButton>
              </td>
            </tr>
            <tr v-if="!einvoiceReport.rows.length">
              <td colspan="7" class="px-3 py-6 text-center text-sm text-muted">No B2B sale invoices matched the selected filters.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="einvoiceReport" class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" size="sm" variant="subtle" label="Previous" :disabled="einvoiceFilters.page <= 1" @click="changeEinvoicePage(einvoiceFilters.page - 1)" />
        <span class="text-sm text-muted">Page {{ einvoiceFilters.page }} / {{ einvoiceTotalPages }}</span>
        <UButton trailing-icon="i-lucide-chevron-right" size="sm" variant="subtle" label="Next" :disabled="einvoiceFilters.page >= einvoiceTotalPages" @click="changeEinvoicePage(einvoiceFilters.page + 1)" />
      </div>
    </section>

    <!-- E-Way Bill -->
    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">E-Way Bill</h3>
          <p class="text-xs text-muted">Sale and Purchase invoices above ₹50,000 (common nationwide default threshold - confirm your state's actual rule) and their e-way bill status.</p>
        </div>
        <UBadge :color="ewaybillReport?.providerConfigured ? 'success' : 'warning'" variant="subtle">
          {{ ewaybillReport?.providerConfigured ? 'Provider configured' : 'No provider configured' }}
        </UBadge>
      </div>

      <div class="grid gap-3 lg:grid-cols-[160px_160px_160px_1fr_140px_auto]">
        <UFormField label="From"><UInput v-model="ewaybillFilters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="ewaybillFilters.toDate" type="date" /></UFormField>
        <UFormField label="Direction">
          <USelect v-model="ewaybillFilters.direction" :items="directionItems" />
        </UFormField>
        <UFormField label="Search"><UInput v-model="ewaybillFilters.search" placeholder="Invoice, party" @keyup.enter="refreshEwaybill(true)" /></UFormField>
        <UFormField label="Rows"><USelect v-model="ewaybillFilters.pageSize" :items="pageSizeItems" /></UFormField>
        <div class="flex items-end gap-2">
          <UCheckbox v-model="ewaybillFilters.onlyPending" label="Only pending" />
          <UButton icon="i-lucide-search" :loading="ewaybillLoading" label="Apply" @click="refreshEwaybill(true)" />
        </div>
      </div>

      <div class="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Invoices</p>
          <p class="garmetix-metric-value">{{ ewaybillReport?.totalRows ?? 0 }}</p>
          <p class="garmetix-metric-caption">In the selected range</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Pending (above threshold)</p>
          <p class="garmetix-metric-value">{{ ewaybillReport?.pendingCount ?? 0 }}</p>
          <p class="garmetix-metric-caption">No Generated e-way bill yet</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Generated</p>
          <p class="garmetix-metric-value">{{ ewaybillReport?.generatedCount ?? 0 }}</p>
          <p class="garmetix-metric-caption">Has a Generated e-way bill record</p>
        </div>
      </div>

      <div v-if="ewaybillReport" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[1020px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Direction</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Party</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Bill</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="row in ewaybillReport.rows" :key="`${row.direction}-${row.invoiceId}`">
              <td class="whitespace-nowrap px-3 py-2"><UBadge :color="row.direction === 'Sales' ? 'primary' : 'neutral'" variant="subtle">{{ row.direction }}</UBadge></td>
              <td class="whitespace-nowrap px-3 py-2 font-medium text-primary">{{ row.invoiceNumber }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(row.onDate) }}</td>
              <td class="px-3 py-2">
                <div>{{ row.partyName }}</div>
                <div class="text-xs text-muted">{{ row.partyGSTIN || 'No GSTIN' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">
                {{ money(row.billAmount) }}
                <UBadge v-if="row.isAboveThreshold" color="warning" variant="subtle" size="xs" class="ml-1">Above threshold</UBadge>
              </td>
              <td class="whitespace-nowrap px-3 py-2">
                <UBadge :color="row.status === 'Generated' ? 'success' : 'neutral'" variant="subtle">{{ row.status }}</UBadge>
                <div v-if="row.errorMessage" class="mt-1 max-w-[240px] text-xs text-muted">{{ row.errorMessage }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2">
                <UButton size="xs" color="neutral" variant="subtle" :loading="isGeneratingEwaybill(row)" @click="generateEwaybill(row)">Generate</UButton>
              </td>
            </tr>
            <tr v-if="!ewaybillReport.rows.length">
              <td colspan="7" class="px-3 py-6 text-center text-sm text-muted">No invoices matched the selected filters.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="ewaybillReport" class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" size="sm" variant="subtle" label="Previous" :disabled="ewaybillFilters.page <= 1" @click="changeEwaybillPage(ewaybillFilters.page - 1)" />
        <span class="text-sm text-muted">Page {{ ewaybillFilters.page }} / {{ ewaybillTotalPages }}</span>
        <UButton trailing-icon="i-lucide-chevron-right" size="sm" variant="subtle" label="Next" :disabled="ewaybillFilters.page >= ewaybillTotalPages" @click="changeEwaybillPage(ewaybillFilters.page + 1)" />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDate, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'E-Invoice & E-Way Bill - Garmetix Books' })

interface EinvoiceRow {
  invoiceId: string
  invoiceNumber: string
  onDate: string
  customerName: string
  customerGSTIN: string | null
  saleInvoiceType: string
  billAmount: number
  status: string
  irn: string | null
  ackNumber: string | null
  ackDate: string | null
  errorMessage: string | null
  lastAttemptAt: string | null
}

interface EinvoiceReport {
  totalRows: number
  pendingCount: number
  generatedCount: number
  providerConfigured: boolean
  rows: EinvoiceRow[]
}

interface EwaybillRow {
  direction: string
  invoiceId: string
  invoiceNumber: string
  onDate: string
  partyName: string
  partyGSTIN: string | null
  billAmount: number
  isAboveThreshold: boolean
  status: string
  ewbNumber: string | null
  ewbDate: string | null
  validUpto: string | null
  errorMessage: string | null
  lastAttemptAt: string | null
}

interface EwaybillReport {
  totalRows: number
  pendingCount: number
  generatedCount: number
  providerConfigured: boolean
  rows: EwaybillRow[]
}

const { get, post } = useBooksApiClient()

const error = ref('')
const message = ref('')

const pageSizeItems = [
  { label: '50', value: 50 },
  { label: '100', value: 100 },
  { label: '250', value: 250 }
]

const directionItems = [
  { label: 'Sale + Purchase', value: 'both' },
  { label: 'Sale only', value: 'sales' },
  { label: 'Purchase only', value: 'purchase' }
]

const defaultFrom = new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
const defaultTo = new Date().toISOString().slice(0, 10)

// E-Invoice
const einvoiceLoading = ref(false)
const einvoiceReport = ref<EinvoiceReport | null>(null)
const generatingEinvoiceIds = ref<Set<string>>(new Set())
const einvoiceFilters = reactive({ fromDate: defaultFrom, toDate: defaultTo, search: '', onlyPending: false, page: 1, pageSize: 100 })
const einvoiceTotalPages = computed(() => Math.max(1, Math.ceil((einvoiceReport.value?.totalRows || 0) / einvoiceFilters.pageSize)))

function isGeneratingEinvoice(invoiceId: string) {
  return generatingEinvoiceIds.value.has(invoiceId)
}

async function refreshEinvoice(resetPage = false) {
  if (resetPage) einvoiceFilters.page = 1
  einvoiceLoading.value = true
  error.value = ''
  try {
    const query = {
      fromDate: einvoiceFilters.fromDate,
      toDate: einvoiceFilters.toDate,
      search: einvoiceFilters.search.trim() || undefined,
      onlyPending: einvoiceFilters.onlyPending,
      page: einvoiceFilters.page,
      pageSize: einvoiceFilters.pageSize
    }
    einvoiceReport.value = await get<EinvoiceReport>('/gst/einvoice', query as Record<string, string | number | boolean | null | undefined>)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the E-Invoice list.'
  } finally {
    einvoiceLoading.value = false
  }
}

function changeEinvoicePage(page: number) {
  einvoiceFilters.page = Math.max(1, page)
  refreshEinvoice()
}

async function generateEinvoice(row: EinvoiceRow) {
  const next = new Set(generatingEinvoiceIds.value)
  next.add(row.invoiceId)
  generatingEinvoiceIds.value = next
  error.value = ''
  message.value = ''
  try {
    const result = await post<{ message?: string }>(`/gst/einvoice/generate/${row.invoiceId}`, {})
    message.value = result?.message || 'E-invoice generation attempted.'
    await refreshEinvoice()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'E-invoice generation attempt failed.'
  } finally {
    const done = new Set(generatingEinvoiceIds.value)
    done.delete(row.invoiceId)
    generatingEinvoiceIds.value = done
  }
}

// E-Way Bill
const ewaybillLoading = ref(false)
const ewaybillReport = ref<EwaybillReport | null>(null)
const generatingEwaybillKeys = ref<Set<string>>(new Set())
const ewaybillFilters = reactive({ fromDate: defaultFrom, toDate: defaultTo, direction: 'both', search: '', onlyPending: false, page: 1, pageSize: 100 })
const ewaybillTotalPages = computed(() => Math.max(1, Math.ceil((ewaybillReport.value?.totalRows || 0) / ewaybillFilters.pageSize)))

function ewaybillKey(row: EwaybillRow) {
  return `${row.direction}-${row.invoiceId}`
}

function isGeneratingEwaybill(row: EwaybillRow) {
  return generatingEwaybillKeys.value.has(ewaybillKey(row))
}

async function refreshEwaybill(resetPage = false) {
  if (resetPage) ewaybillFilters.page = 1
  ewaybillLoading.value = true
  error.value = ''
  try {
    const query = {
      fromDate: ewaybillFilters.fromDate,
      toDate: ewaybillFilters.toDate,
      direction: ewaybillFilters.direction,
      search: ewaybillFilters.search.trim() || undefined,
      onlyPending: ewaybillFilters.onlyPending,
      page: ewaybillFilters.page,
      pageSize: ewaybillFilters.pageSize
    }
    ewaybillReport.value = await get<EwaybillReport>('/gst/ewaybill', query as Record<string, string | number | boolean | null | undefined>)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the E-Way Bill list.'
  } finally {
    ewaybillLoading.value = false
  }
}

function changeEwaybillPage(page: number) {
  ewaybillFilters.page = Math.max(1, page)
  refreshEwaybill()
}

async function generateEwaybill(row: EwaybillRow) {
  const key = ewaybillKey(row)
  const next = new Set(generatingEwaybillKeys.value)
  next.add(key)
  generatingEwaybillKeys.value = next
  error.value = ''
  message.value = ''
  try {
    const path = row.direction === 'Sales' ? `/gst/ewaybill/generate/sale/${row.invoiceId}` : `/gst/ewaybill/generate/purchase/${row.invoiceId}`
    const result = await post<{ message?: string }>(path, {})
    message.value = result?.message || 'E-way bill generation attempted.'
    await refreshEwaybill()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'E-way bill generation attempt failed.'
  } finally {
    const done = new Set(generatingEwaybillKeys.value)
    done.delete(key)
    generatingEwaybillKeys.value = done
  }
}

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

onMounted(async () => {
  await Promise.all([refreshEinvoice(true), refreshEwaybill(true)])
})
</script>

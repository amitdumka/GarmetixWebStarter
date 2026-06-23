<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Books command center</p>
          <h2 class="mt-1 text-2xl font-semibold">Accounting, GST And Audit</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            A lean accounting workspace for accountant and CA workflows. This foundation keeps write actions disabled while route ownership is split from the main back office.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/vouchers" icon="i-lucide-file-signature">Vouchers</UButton>
          <UButton to="/gst-reports" color="neutral" variant="soft" icon="i-lucide-chart-column">GST Reports</UButton>
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

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Accounting Coverage</h3>
          <UBadge :color="loading ? 'warning' : 'primary'" variant="subtle">{{ loading ? 'Loading' : 'Ready' }}</UBadge>
        </div>
        <div class="grid gap-2 sm:grid-cols-2">
          <UButton v-for="item in quickLinks" :key="item.href" :to="item.href" :icon="item.icon" color="neutral" variant="soft" class="justify-start">
            {{ item.label }}
          </UButton>
        </div>
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Recent Accounting Rows</h3>
          <UButton to="/accounting" size="sm" color="neutral" variant="ghost" icon="i-lucide-arrow-right">Open</UButton>
        </div>
        <div v-if="recentRows.length" class="space-y-2">
          <div v-for="row in recentRows.slice(0, 5)" :key="readText(row, ['id', 'voucherNumber', 'name'])" class="flex items-center justify-between gap-3 border border-default bg-default/40 p-3">
            <div class="min-w-0">
              <p class="truncate text-sm font-medium">{{ readText(row, ['voucherNumber', 'ledgerName', 'name', 'partyName']) }}</p>
              <p class="text-xs text-muted">{{ readText(row, ['onDate', 'date', 'createdAtUtc', 'type']) }}</p>
            </div>
            <p class="shrink-0 text-sm font-semibold">{{ formatIndianMoney(readNumber(row, ['amount', 'balance', 'totalAmount'])) }}</p>
          </div>
        </div>
        <p v-else class="text-sm text-muted">Live rows will appear here after the next endpoint-specific Books slice.</p>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Books - Garmetix Books' })

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const vouchers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const pettyCash = ref<ApiRecord[]>([])
const gstRows = ref<ApiRecord[]>([])

const cards = computed(() => [
  { label: 'Vouchers', value: vouchers.value.length, detail: 'Voucher rows returned by the API' },
  { label: 'Parties', value: parties.value.length, detail: 'Party master rows returned by the API' },
  { label: 'Cash Sheets', value: pettyCash.value.length, detail: 'Petty cash sheets returned by the API' },
  { label: 'GST Rows', value: gstRows.value.length, detail: 'GST report rows returned by the API' }
])
const recentRows = computed(() => [...vouchers.value, ...pettyCash.value, ...parties.value])
const quickLinks = [
  { label: 'Accounting', href: '/accounting', icon: 'i-lucide-book-open-check' },
  { label: 'Parties', href: '/parties', icon: 'i-lucide-contact-round' },
  { label: 'Petty Cash', href: '/petty-cash', icon: 'i-lucide-wallet' },
  { label: 'Vouchers', href: '/vouchers', icon: 'i-lucide-file-signature' },
  { label: 'Debit Notes', href: '/debit-notes', icon: 'i-lucide-file-minus-2' },
  { label: 'Credit Notes', href: '/credit-notes', icon: 'i-lucide-file-plus-2' },
  { label: 'GST Returns', href: '/gst-returns', icon: 'i-lucide-file-check-2' },
  { label: 'GST Reports', href: '/gst-reports', icon: 'i-lucide-chart-column' }
]

onMounted(async () => {
  loading.value = true
  error.value = ''
  try {
    const [voucherData, partyData, pettyData, gstData] = await Promise.allSettled([
      get<unknown>('vouchers'),
      get<unknown>('parties'),
      get<unknown>('petty-cash-sheets'),
      get<unknown>('gst/reports')
    ])
    if (voucherData.status === 'fulfilled') vouchers.value = toRows(voucherData.value)
    if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
    if (pettyData.status === 'fulfilled') pettyCash.value = toRows(pettyData.value)
    if (gstData.status === 'fulfilled') gstRows.value = toRows(gstData.value)
    const failed = [voucherData, partyData, pettyData, gstData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} Books summaries could not be loaded yet. Route shell remains available.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load Books dashboard.'
  } finally {
    loading.value = false
  }
})
</script>

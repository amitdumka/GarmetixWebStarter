<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scan-barcode" class="size-4" /> POS counter</p>
          <h2 class="garmetix-dashboard-title">Counter dashboard</h2>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/sale" icon="i-lucide-scan-barcode">Open Sale</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-calculator" @click="rateCalculatorOpen = true">Rate Calculator</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <RateCalculatorModal v-model:open="rateCalculatorOpen" />

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="widget in saleWidgets" :key="widget.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ widget.label }}</p>
        <p class="garmetix-metric-value">{{ widget.value }}</p>
        <p class="garmetix-metric-caption">{{ widget.detail }}</p>
      </div>
    </section>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <UButton v-for="action in quickActions" :key="action.to" :to="action.to" :icon="action.icon" color="neutral" variant="soft" class="justify-start">
        {{ action.label }}
      </UButton>
    </section>

    <section class="grid gap-4 lg:grid-cols-3">
      <div v-for="item in statusItems" :key="item.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ item.label }}</p>
        <p class="garmetix-metric-value">{{ item.value }}</p>
        <p class="garmetix-metric-caption">{{ item.detail }}</p>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'

useHead({ title: 'POS Counter - Garmetix POS' })

interface SaleInvoiceRow {
  billAmount?: number
  invoiceStatus?: string
  paymentMode?: string
}

interface PagedSaleInvoicesResponse {
  items?: SaleInvoiceRow[]
}

const RETURN_STATUSES = new Set(['Refunded', 'PartiallyRefunded'])

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const loading = ref(false)
const error = ref('')
const todaysRows = ref<SaleInvoiceRow[]>([])
const rateCalculatorOpen = ref(false)

const quickActions = [
  { label: 'Day Open', to: '/day-open', icon: 'i-lucide-sunrise' },
  { label: 'New Sale', to: '/sale', icon: 'i-lucide-scan-barcode' },
  { label: 'Held Bills', to: '/hold-bills', icon: 'i-lucide-pause-circle' },
  { label: 'Day Close', to: '/day-close', icon: 'i-lucide-sunset' }
]

const statusItems = [
  { label: 'Billing mode', value: 'Counter ready', detail: 'Fullscreen counter layout is active for faster billing.' },
  { label: 'Print mode', value: 'Invoice ready', detail: 'Saved invoices can be reprinted from history or print queue.' },
  { label: 'CRM signal', value: 'Digital bills', detail: 'Sales history shows Digital Bill link and WhatsApp status.' }
]

const saleRows = computed(() => todaysRows.value.filter(row => String(row.invoiceStatus || '') !== 'Cancelled'))
const soldRows = computed(() => saleRows.value.filter(row => !RETURN_STATUSES.has(String(row.invoiceStatus || ''))))
const returnRows = computed(() => saleRows.value.filter(row => RETURN_STATUSES.has(String(row.invoiceStatus || ''))))
const totalSale = computed(() => sumAmount(soldRows.value))
const totalReturn = computed(() => sumAmount(returnRows.value))
const cashSale = computed(() => sumAmount(soldRows.value.filter(row => row.paymentMode === 'Cash')))
const upiSale = computed(() => sumAmount(soldRows.value.filter(row => row.paymentMode === 'UPI')))
const cardSale = computed(() => sumAmount(soldRows.value.filter(row => row.paymentMode === 'Card')))
const otherSale = computed(() => sumAmount(soldRows.value.filter(row => !['Cash', 'UPI', 'Card'].includes(String(row.paymentMode || '')))))

const saleWidgets = computed(() => [
  { label: "Today's Total Sale", value: money(totalSale.value), detail: `${soldRows.value.length} invoice(s)` },
  { label: 'Total Sale Return', value: money(totalReturn.value), detail: `${returnRows.value.length} return(s)` },
  { label: 'Cash Sale', value: money(cashSale.value), detail: 'Payment mode: Cash' },
  { label: 'UPI Sale', value: money(upiSale.value), detail: 'Payment mode: UPI' },
  { label: 'Card Sale', value: money(cardSale.value), detail: 'Payment mode: Card' },
  { label: 'Other Sale', value: money(otherSale.value), detail: 'Wallets, IMPS, RTGS, NEFT, cheque, DD' }
])

function sumAmount(rows: SaleInvoiceRow[]) {
  return rows.reduce((sum, row) => sum + Number(row.billAmount || 0), 0)
}

function money(value: number) {
  return formatIndianMoney(value)
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) return

  loading.value = true
  error.value = ''
  try {
    const response = await api.value.get<PagedSaleInvoicesResponse>('billing/sales?datePreset=today&pageSize=200&page=1')
    todaysRows.value = Array.isArray(response?.items) ? response.items : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : "Could not load today's sales."
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

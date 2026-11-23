<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-range" class="size-4" /> Sales and expense review</p>
          <h2 class="garmetix-dashboard-title">Sales & Expense Calendar</h2>
          <p class="garmetix-dashboard-subtitle">Every day of the month with cash sale, non-cash sale and expenses, and the month summary below. Pick a store, a month and a year.</p>
        </div>
        <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" :disabled="!storeId" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="garmetix-section-card grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(220px,1.4fr)_160px_120px_auto]">
      <UFormField label="Store"><USelect v-model="storeId" :items="storeItems" class="w-full" /></UFormField>
      <UFormField label="Month"><USelect v-model="month" :items="monthItems" class="w-full" /></UFormField>
      <UFormField label="Year"><USelect v-model="year" :items="yearItems" class="w-full" /></UFormField>
      <div class="flex flex-wrap items-end gap-2">
        <UButton color="neutral" variant="soft" icon="i-lucide-chevron-left" @click="shiftMonth(-1)">Prev</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-calendar-check" @click="goToday">This month</UButton>
        <UButton color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" @click="shiftMonth(1)">Next</UButton>
      </div>
    </div>

    <div v-if="!storeId" class="garmetix-section-card p-8 text-center text-muted">Select a store to see its sales and expenses.</div>

    <template v-else>
      <div class="garmetix-section-card grid gap-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 class="text-lg font-semibold text-highlighted">{{ storeName }}</h3>
            <p class="text-sm text-muted">{{ monthLabel }} {{ year }}</p>
          </div>
          <div class="flex flex-wrap gap-2 text-xs">
            <span class="inline-flex items-center gap-1 rounded-md border border-emerald-500/40 bg-emerald-500/10 px-2 py-1 text-emerald-400">Cash sale</span>
            <span class="inline-flex items-center gap-1 rounded-md border border-blue-500/40 bg-blue-500/10 px-2 py-1 text-blue-400">Non-cash sale</span>
            <span class="inline-flex items-center gap-1 rounded-md border border-red-500/40 bg-red-500/10 px-2 py-1 text-red-400">Expenses and payments</span>
            <span class="inline-flex items-center gap-1 rounded-md border border-yellow-500/40 bg-yellow-500/10 px-2 py-1 text-yellow-400">Bank deposit</span>
          </div>
        </div>

        <div v-if="loading" class="p-8 text-center text-muted">Loading...</div>
        <div v-else class="overflow-x-auto">
          <div class="grid min-w-[860px] grid-cols-7 gap-1">
            <div v-for="(name, index) in weekdayNames" :key="name" class="p-2 text-center text-xs font-semibold uppercase" :class="index === 0 ? 'text-blue-400' : 'text-muted'">{{ name }}</div>
            <div v-for="n in leadingBlanks" :key="`b${n}`" />
            <div
              v-for="day in days"
              :key="day.date"
              class="min-h-[112px] rounded-md border p-2 text-xs"
              :class="day.date === todayIso ? 'border-primary' : (hasActivity(day) ? 'border-default' : 'border-dashed border-default opacity-70')"
              :title="dayTitle(day)"
            >
              <div class="flex items-start justify-between">
                <span class="text-base font-semibold text-highlighted">{{ day.day }}</span>
                <span v-if="day.bills" class="text-[10px] text-muted">{{ day.bills }} bill{{ day.bills === 1 ? '' : 's' }}</span>
              </div>
              <div class="mt-1 grid gap-0.5">
                <p v-if="day.sale" class="flex justify-between font-semibold text-highlighted"><span>Sale</span><span>{{ compact(day.sale) }}</span></p>
                <p v-if="day.cashSale" class="flex justify-between text-emerald-400"><span>Cash</span><span>{{ compact(day.cashSale) }}</span></p>
                <p v-if="day.nonCashSale" class="flex justify-between text-blue-400"><span>Non-cash</span><span>{{ compact(day.nonCashSale) }}</span></p>
                <p v-if="day.totalExpense" class="flex justify-between text-red-400"><span>Expense</span><span>{{ compact(day.totalExpense) }}</span></p>
                <p v-if="day.bankDeposit" class="flex justify-between text-yellow-400"><span>Deposit</span><span>{{ compact(day.bankDeposit) }}</span></p>
              </div>
            </div>
          </div>
        </div>
      </div>

      <section v-if="summary" class="grid gap-3 md:grid-cols-3 xl:grid-cols-4">
        <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value" :class="card.cls">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.caption }}</p>
        </div>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Sales & Expense Calendar - Garmetix' })

type Day = { date: string, day: number, bills: number, sale: number, cashSale: number, nonCashSale: number, creditSale: number, returns: number, expenses: number, payments: number, salary: number, totalExpense: number, bankDeposit: number }

const { get } = useMainApiClient()
const now = new Date()
const year = ref(now.getFullYear())
const month = ref(now.getMonth() + 1)
const storeId = ref('')
const stores = ref<ApiRecord[]>([])
const loading = ref(false)
const error = ref('')
const calendar = ref<ApiRecord | null>(null)

const todayIso = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
const weekdayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const monthItems = monthNames.map((label, index) => ({ label, value: index + 1 }))
const yearItems = Array.from({ length: 8 }, (_, index) => now.getFullYear() - 6 + index).map(value => ({ label: String(value), value }))
const monthLabel = computed(() => monthNames[month.value - 1])
const storeItems = computed(() => stores.value.map(item => ({ label: readText(item, ['name', 'storeCode'], 'Store'), value: String(item.id) })))
const storeName = computed(() => readText((calendar.value?.store as ApiRecord) || null, ['name'], storeItems.value.find(item => item.value === storeId.value)?.label || ''))

const days = computed<Day[]>(() => ((calendar.value?.days as Day[]) || []))
const summary = computed(() => (calendar.value?.summary as ApiRecord) || null)
const leadingBlanks = computed(() => Number(calendar.value?.firstDayOfWeek ?? 0))

function compact(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 }).format(value)
}

function hasActivity(day: Day) {
  return Boolean(day.sale || day.totalExpense || day.bankDeposit || day.returns)
}

function dayTitle(day: Day) {
  const money = (value: number) => formatIndianMoney(value)
  return [
    `${day.date}: ${day.bills} bill(s)`,
    `Sale ${money(day.sale)} (cash ${money(day.cashSale)}, non-cash ${money(day.nonCashSale)}, credit ${money(day.creditSale)})`,
    day.returns ? `Sales returns ${money(day.returns)}` : '',
    `Expenses ${money(day.expenses)}, payments ${money(day.payments)}, salary ${money(day.salary)}`,
    `Bank deposit ${money(day.bankDeposit)}`
  ].filter(Boolean).join('\n')
}

const summaryCards = computed(() => {
  const s = summary.value
  if (!s) return []
  const n = (key: string) => readNumber(s, [key])
  const money = (key: string) => formatIndianMoney(n(key))
  const counted = n('daysCounted')
  const averageCaption = counted === n('daysInMonth') ? `over ${counted} days` : `over ${counted} days so far`
  return [
    { label: 'Total sale', value: money('totalSale'), caption: `${n('billCount')} bills on ${n('daysWithSale')} day(s)`, cls: 'text-highlighted' },
    { label: 'Cash sale', value: money('cashSale'), caption: 'Received in cash', cls: 'text-emerald-400' },
    { label: 'Non-cash sale', value: money('nonCashSale'), caption: 'Card, UPI, bank and others', cls: 'text-blue-400' },
    { label: 'Credit (unpaid)', value: money('creditSale'), caption: 'Billed but not yet paid', cls: n('creditSale') ? 'text-orange-400' : '' },
    { label: 'Total expense', value: money('totalExpense'), caption: 'Expenses + payments + salary', cls: 'text-red-400' },
    { label: 'Expenses', value: money('expenses'), caption: 'Expense vouchers', cls: '' },
    { label: 'Payments', value: money('payments'), caption: 'Payment vouchers', cls: '' },
    { label: 'Salary paid', value: money('salary'), caption: 'Salary payments', cls: '' },
    { label: 'Average sale / day', value: money('averageSalePerDay'), caption: averageCaption, cls: '' },
    { label: 'Average expense / day', value: money('averageExpensePerDay'), caption: averageCaption, cls: '' },
    { label: 'Bank deposit', value: money('bankDeposit'), caption: 'Cash deposited in bank this month', cls: 'text-yellow-400' },
    { label: 'Sales returns', value: money('salesReturns'), caption: 'Shown apart, not deducted from sale', cls: n('salesReturns') ? 'text-orange-400' : '' }
  ]
})

async function loadStores() {
  try {
    stores.value = toRows(await get<unknown>('stores'))
    if (!storeId.value && stores.value[0]) storeId.value = String(stores.value[0].id)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not load stores.'
  }
}

async function load() {
  if (!storeId.value) return
  loading.value = true
  error.value = ''
  try {
    calendar.value = await get<ApiRecord>('accounting/sales-expense-calendar', { storeId: storeId.value, year: year.value, month: month.value })
  } catch (caught) {
    calendar.value = null
    error.value = caught instanceof Error ? caught.message : 'Could not load the calendar.'
  } finally {
    loading.value = false
  }
}

function shiftMonth(delta: number) {
  const date = new Date(year.value, month.value - 1 + delta, 1)
  year.value = date.getFullYear()
  month.value = date.getMonth() + 1
}

function goToday() {
  year.value = now.getFullYear()
  month.value = now.getMonth() + 1
}

watch([storeId, year, month], load)
onMounted(loadStores)
</script>

<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-layout-dashboard" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Dashboard</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
        <UButton icon="i-lucide-download" color="neutral" variant="soft" @click="exportCsv">Export Net Worth (CSV)</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load dashboard data" :description="error" />
    <UAlert
      v-else-if="health && !health.ok"
      icon="i-lucide-triangle-alert"
      color="warning"
      variant="subtle"
      title="Database unreachable"
      :description="`swalekha_db - ${health.database}`"
    />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Net Worth</p>
        <p class="truncate text-2xl font-bold" :class="dashboard && dashboard.netWorth >= 0 ? 'text-success' : 'text-error'">{{ formatCurrency(dashboard?.netWorth ?? 0) }}</p>
        <p class="text-xs text-muted">{{ formatCurrency(dashboard?.totalAssets ?? 0) }} assets - {{ formatCurrency(dashboard?.totalLiabilities ?? 0) }} liabilities</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Owed to you</p>
            <p class="truncate text-lg font-semibold text-success">{{ formatCurrency(owedToYou) }}</p>
          </div>
          <UButton icon="i-lucide-users" color="neutral" variant="ghost" size="sm" to="/contacts" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Spent (visible)</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(expenseSummary?.totalVisible ?? 0) }}</p>
          </div>
          <UButton icon="i-lucide-receipt" color="neutral" variant="ghost" size="sm" to="/expenses" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Income</p>
            <p class="truncate text-lg font-semibold text-success">{{ formatCurrency(totalIncome) }}</p>
          </div>
          <UButton icon="i-lucide-wallet" color="neutral" variant="ghost" size="sm" to="/income" />
        </div>
      </UCard>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <UCard>
        <template #header>
          <p class="font-semibold text-highlighted">Assets</p>
        </template>
        <ul class="space-y-2">
          <li v-for="row in dashboard?.assetsBreakdown ?? []" :key="row.category" class="flex items-center justify-between gap-2 text-sm">
            <span class="text-muted">{{ row.category }}<span v-if="row.note" class="text-xs"> ({{ row.note }})</span></span>
            <span class="font-mono">{{ formatCurrency(row.value) }}</span>
          </li>
        </ul>
      </UCard>
      <UCard>
        <template #header>
          <p class="font-semibold text-highlighted">Liabilities</p>
        </template>
        <ul v-if="(dashboard?.liabilitiesBreakdown ?? []).length" class="space-y-2">
          <li v-for="row in dashboard?.liabilitiesBreakdown ?? []" :key="row.category" class="flex items-center justify-between gap-2 text-sm">
            <span class="text-muted">{{ row.category }}</span>
            <span class="font-mono text-error">{{ formatCurrency(row.value) }}</span>
          </li>
        </ul>
        <p v-else class="text-sm text-muted">No outstanding liabilities.</p>
      </UCard>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <UCard :ui="{ body: 'p-0' }">
        <template #header>
          <p class="font-semibold text-highlighted">Upcoming Dues (Next 30 Days)</p>
        </template>
        <div v-if="(dashboard?.upcomingDues ?? []).length === 0" class="p-4 text-sm text-muted">Nothing due in the next 30 days.</div>
        <ul v-else class="divide-y divide-default">
          <li v-for="due in dashboard?.upcomingDues ?? []" :key="`${due.eventType}-${due.sourceId}-${due.date}`" class="flex items-center justify-between gap-3 p-3">
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-highlighted">{{ due.title }}</p>
              <p class="text-xs text-muted">{{ formatDate(due.date) }}</p>
            </div>
            <div class="flex items-center gap-2 text-right">
              <span v-if="due.amount != null" class="font-mono text-sm">{{ formatCurrency(due.amount) }}</span>
              <UBadge color="warning" variant="subtle" size="xs">{{ due.eventType }}</UBadge>
            </div>
          </li>
        </ul>
      </UCard>

      <UCard :ui="{ body: 'p-0' }">
        <template #header>
          <div class="flex items-center justify-between">
            <p class="font-semibold text-highlighted">Today's Appointments</p>
            <UButton to="/calendar" icon="i-lucide-arrow-right" color="primary" variant="ghost" size="sm">Calendar</UButton>
          </div>
        </template>
        <div v-if="(dashboard?.todayAppointments ?? []).length === 0" class="p-4 text-sm text-muted">Nothing on the calendar today.</div>
        <ul v-else class="divide-y divide-default">
          <li v-for="appt in dashboard?.todayAppointments ?? []" :key="appt.id" class="p-3">
            <p class="text-sm font-medium text-highlighted">{{ appt.title }}</p>
            <p class="text-xs text-muted">{{ appt.isAllDay ? 'All day' : formatTime(appt.startAt) }}<span v-if="appt.location"> - {{ appt.location }}</span></p>
          </li>
        </ul>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Expense Breakdown</p>
      </template>
      <div v-if="!expenseSummary || expenseSummary.byCategory.length === 0" class="text-sm text-muted">No expenses recorded yet.</div>
      <div v-else class="grid gap-4 sm:grid-cols-2">
        <div>
          <p class="mb-2 text-xs font-medium uppercase text-muted">By Sheet Type</p>
          <ul class="space-y-1.5">
            <li v-for="row in expenseSummary.bySheetType" :key="row.key" class="flex items-center justify-between gap-2 text-sm">
              <span class="text-muted">{{ row.key }} ({{ row.count }})</span>
              <span class="font-mono">{{ formatCurrency(row.total) }}</span>
            </li>
          </ul>
        </div>
        <div>
          <p class="mb-2 text-xs font-medium uppercase text-muted">By Category</p>
          <ul class="space-y-1.5">
            <li v-for="row in expenseSummary.byCategory" :key="row.key" class="flex items-center justify-between gap-2 text-sm">
              <span class="text-muted">{{ row.key }} ({{ row.count }})</span>
              <span class="font-mono">{{ formatCurrency(row.total) }}</span>
            </li>
          </ul>
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { getStoredToken } from '@garmetix/shared-auth'
import {
  useSwalekhaApiClient,
  type SwalekhaContact,
  type SwalekhaDashboard,
  type SwalekhaExpenseSummary,
  type SwalekhaHealth,
  type SwalekhaIncomeList
} from '../utils/swalekha-api'

useHead({ title: 'Dashboard - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const health = ref<SwalekhaHealth | null>(null)
const contacts = ref<SwalekhaContact[]>([])
const expenseSummary = ref<SwalekhaExpenseSummary | null>(null)
const totalIncome = ref(0)
const dashboard = ref<SwalekhaDashboard | null>(null)

const owedToYou = computed(() => contacts.value.filter(c => c.balance > 0).reduce((sum, c) => sum + c.balance, 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

function formatTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { timeStyle: 'short' }).format(date)
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    const [healthData, contactRows, summary, incomeList, dashboardData] = await Promise.all([
      api.get<SwalekhaHealth>('health'),
      api.get<SwalekhaContact[]>('contacts'),
      api.get<SwalekhaExpenseSummary>('expenses/summary'),
      api.get<SwalekhaIncomeList>('income?pageSize=1'),
      api.get<SwalekhaDashboard>('dashboard')
    ])
    health.value = healthData
    contacts.value = contactRows
    expenseSummary.value = summary
    totalIncome.value = incomeList.totalAmount
    dashboard.value = dashboardData
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not reach the Swalekha API.'
  } finally {
    loading.value = false
  }
}

onMounted(refreshAll)

async function exportCsv() {
  try {
    const token = getStoredToken(window.localStorage)
    const response = await fetch(`${api.apiBaseUrl.value}/swalekha/dashboard/export`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    })
    if (!response.ok) throw new Error('Export failed.')
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `swalekha-net-worth-${new Date().toISOString().slice(0, 10)}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not export the net worth summary.'
  }
}
</script>

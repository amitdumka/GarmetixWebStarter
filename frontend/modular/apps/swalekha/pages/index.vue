<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-layout-dashboard" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Dashboard</h1>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
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
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Net worth (accounts)</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(netWorth) }}</p>
          </div>
          <UButton icon="i-lucide-landmark" color="neutral" variant="ghost" size="sm" to="/accounts" />
        </div>
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
            <p class="text-xs font-medium uppercase text-muted">You owe</p>
            <p class="truncate text-lg font-semibold text-error">{{ formatCurrency(youOwe) }}</p>
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
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Bills due this month</p>
            <p class="truncate text-lg font-semibold" :class="dueBillCount > 0 ? 'text-warning' : 'text-highlighted'">{{ dueBillCount }}</p>
          </div>
          <UButton icon="i-lucide-calendar-clock" color="neutral" variant="ghost" size="sm" to="/recurring-bills" />
        </div>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <h2 class="text-sm font-semibold">Coming Up (Next Stages)</h2>
      </template>
      <div class="swalekha-grid">
        <div v-for="item in pillarCards" :key="item.label" class="flex items-center gap-3">
          <UIcon :name="item.icon" class="size-5 shrink-0 text-primary" />
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">{{ item.label }}</p>
            <p class="truncate text-sm text-highlighted">{{ item.value }}</p>
          </div>
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaAccount, type SwalekhaContact, type SwalekhaExpenseSummary, type SwalekhaHealth, type SwalekhaIncomeList, type SwalekhaRecurringBill } from '../utils/swalekha-api'

useHead({ title: 'Dashboard - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const health = ref<SwalekhaHealth | null>(null)
const accounts = ref<SwalekhaAccount[]>([])
const contacts = ref<SwalekhaContact[]>([])
const expenseSummary = ref<SwalekhaExpenseSummary | null>(null)
const totalIncome = ref(0)
const dueBillCount = ref(0)

const pillarCards = [
  { label: 'PersonalFin_06-08', value: 'Investments (FD/RD, Mutual Funds, Shares)', icon: 'i-lucide-trending-up' },
  { label: 'PersonalFin_09', value: 'Loans', icon: 'i-lucide-hand-coins' },
  { label: 'PersonalFin_10', value: 'Insurance', icon: 'i-lucide-shield-check' },
  { label: 'PersonalFin_11', value: 'Diary, Notes, Calendar, Contacts', icon: 'i-lucide-notebook-pen' },
  { label: 'PersonalFin_12', value: 'Net Worth & Reports', icon: 'i-lucide-chart-pie' }
]

const netWorth = computed(() => accounts.value.reduce((sum, account) => sum + account.currentBalance, 0))
const owedToYou = computed(() => contacts.value.filter(c => c.balance > 0).reduce((sum, c) => sum + c.balance, 0))
const youOwe = computed(() => contacts.value.filter(c => c.balance < 0).reduce((sum, c) => sum + Math.abs(c.balance), 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    const [healthData, accountRows, contactRows, summary, incomeList, bills] = await Promise.all([
      api.get<SwalekhaHealth>('health'),
      api.get<SwalekhaAccount[]>('accounts'),
      api.get<SwalekhaContact[]>('contacts'),
      api.get<SwalekhaExpenseSummary>('expenses/summary'),
      api.get<SwalekhaIncomeList>('income?pageSize=1'),
      api.get<SwalekhaRecurringBill[]>('recurring-bills')
    ])
    health.value = healthData
    accounts.value = accountRows
    contacts.value = contactRows
    expenseSummary.value = summary
    totalIncome.value = incomeList.totalAmount
    dueBillCount.value = bills.filter(bill => bill.dueThisMonth).length
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not reach the Swalekha API.'
  } finally {
    loading.value = false
  }
}

onMounted(refreshAll)
</script>

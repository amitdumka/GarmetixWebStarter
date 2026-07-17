<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-trending-up" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Investments</h1>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load investments" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Active FD Principal</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(activeFdPrincipal) }}</p>
          </div>
          <UIcon name="i-lucide-landmark" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Active RD Committed / Month</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(activeRdMonthly) }}</p>
          </div>
          <UIcon name="i-lucide-repeat" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Upcoming Maturities (90 days)</p>
            <p class="truncate text-lg font-semibold text-warning">{{ upcomingMaturities.length }}</p>
          </div>
          <UIcon name="i-lucide-calendar-clock" class="size-5 shrink-0 text-warning" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Mutual Fund Current Value</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(mutualFundCurrentValue) }}</p>
          </div>
          <UIcon name="i-lucide-pie-chart" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
    </div>

    <div class="grid gap-4 sm:grid-cols-3">
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <p class="font-semibold text-highlighted">Fixed Deposits</p>
            <UButton to="/investments/fixed-deposits" icon="i-lucide-arrow-right" color="primary" variant="ghost" size="sm">Manage</UButton>
          </div>
        </template>
        <p class="text-sm text-muted">{{ fixedDeposits.filter(d => !d.isClosed).length }} active, {{ fixedDeposits.filter(d => d.isClosed).length }} matured.</p>
      </UCard>
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <p class="font-semibold text-highlighted">Recurring Deposits</p>
            <UButton to="/investments/recurring-deposits" icon="i-lucide-arrow-right" color="primary" variant="ghost" size="sm">Manage</UButton>
          </div>
        </template>
        <p class="text-sm text-muted">{{ recurringDeposits.filter(d => !d.isClosed).length }} active, {{ recurringDeposits.filter(d => d.isClosed).length }} matured.</p>
      </UCard>
      <UCard>
        <template #header>
          <div class="flex items-center justify-between">
            <p class="font-semibold text-highlighted">Mutual Funds &amp; SIPs</p>
            <UButton to="/investments/mutual-funds" icon="i-lucide-arrow-right" color="primary" variant="ghost" size="sm">Manage</UButton>
          </div>
        </template>
        <p class="text-sm text-muted">{{ mutualFunds.filter(f => f.isActive).length }} active fund(s){{ mutualFundsSipDue ? `, ${mutualFundsSipDue} SIP(s) due this month` : '' }}.</p>
      </UCard>
    </div>

    <UCard v-if="upcomingMaturities.length" :ui="{ body: 'p-0' }">
      <template #header>
        <p class="font-semibold text-highlighted">Upcoming Maturities</p>
      </template>
      <UTable :data="upcomingMaturities" :columns="maturityColumns" class="w-full">
        <template #type-cell="{ row }">
          <UBadge :color="row.original.type === 'FD' ? 'primary' : 'secondary'" variant="subtle">{{ row.original.type }}</UBadge>
        </template>
        <template #maturityAmount-cell="{ row }">
          <span class="font-mono text-sm">{{ row.original.maturityAmount != null ? formatCurrency(row.original.maturityAmount) : '-' }}</span>
        </template>
      </UTable>
    </UCard>

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Coming Up (Next Stages)</p>
      </template>
      <div class="grid gap-2 sm:grid-cols-2">
        <div class="rounded-md border border-default p-3 text-sm">
          <p class="font-medium text-highlighted">PersonalFin_10</p>
          <p class="text-muted">Shares/Stocks, PPF/EPF/NPS/Gold</p>
        </div>
        <div class="rounded-md border border-default p-3 text-sm">
          <p class="font-medium text-highlighted">PersonalFin_11</p>
          <p class="text-muted">Loans Taken &amp; Given</p>
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaFixedDeposit, type SwalekhaMutualFund, type SwalekhaRecurringDeposit } from '../../utils/swalekha-api'

useHead({ title: 'Investments - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const fixedDeposits = ref<SwalekhaFixedDeposit[]>([])
const recurringDeposits = ref<SwalekhaRecurringDeposit[]>([])
const mutualFunds = ref<SwalekhaMutualFund[]>([])

const maturityColumns = [
  { accessorKey: 'type', header: 'Type' },
  { accessorKey: 'bankName', header: 'Bank' },
  { accessorKey: 'maturityDate', header: 'Maturity Date' },
  { accessorKey: 'maturityAmount', header: 'Expected Amount' }
]

const activeFdPrincipal = computed(() => fixedDeposits.value.filter(d => !d.isClosed).reduce((sum, d) => sum + d.principalAmount, 0))
const activeRdMonthly = computed(() => recurringDeposits.value.filter(d => !d.isClosed).reduce((sum, d) => sum + d.monthlyInstallment, 0))
const mutualFundCurrentValue = computed(() => mutualFunds.value.reduce((sum, f) => sum + (f.currentValue ?? f.totalInvested), 0))
const mutualFundsSipDue = computed(() => mutualFunds.value.filter(f => f.sipDueThisMonth).length)

const upcomingMaturities = computed(() => {
  const cutoff = new Date()
  cutoff.setDate(cutoff.getDate() + 90)
  const fdRows = fixedDeposits.value
    .filter(d => !d.isClosed && new Date(d.maturityDate) <= cutoff)
    .map(d => ({ type: 'FD', bankName: d.bankName, maturityDate: formatDate(d.maturityDate), maturityAmount: d.maturityAmount, sortDate: d.maturityDate }))
  const rdRows = recurringDeposits.value
    .filter(d => !d.isClosed && new Date(d.maturityDate) <= cutoff)
    .map(d => ({ type: 'RD', bankName: d.bankName, maturityDate: formatDate(d.maturityDate), maturityAmount: d.maturityAmount, sortDate: d.maturityDate }))
  return [...fdRows, ...rdRows].sort((a, b) => a.sortDate.localeCompare(b.sortDate))
})

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('en-IN')
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [fdResult, rdResult, mfResult] = await Promise.all([
      api.get<SwalekhaFixedDeposit[]>('fixed-deposits?includeClosed=true'),
      api.get<SwalekhaRecurringDeposit[]>('recurring-deposits?includeClosed=true'),
      api.get<SwalekhaMutualFund[]>('mutual-funds?includeInactive=true')
    ])
    fixedDeposits.value = fdResult
    recurringDeposits.value = rdResult
    mutualFunds.value = mfResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load investments.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

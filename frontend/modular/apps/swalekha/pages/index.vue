<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-book-heart" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Personal &amp; Personal Finance</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-landmark" @click="navigateTo('/accounts')">Accounts Hub</UButton>
        <UButton icon="i-lucide-users" @click="navigateTo('/contacts')">Contacts</UButton>
        <UButton icon="i-lucide-receipt" @click="navigateTo('/expenses')">Expenses</UButton>
        <UButton icon="i-lucide-wallet" @click="navigateTo('/income')">Income</UButton>
        <UButton icon="i-lucide-calendar-clock" @click="navigateTo('/recurring-bills')">Bills</UButton>
        <UButton icon="i-lucide-plane" @click="navigateTo('/trips')">Trips</UButton>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshHealth">Refresh</UButton>
      </div>
    </div>

    <UAlert
      v-if="health"
      :icon="health.ok ? 'i-lucide-circle-check' : 'i-lucide-triangle-alert'"
      :color="health.ok ? 'success' : 'warning'"
      variant="subtle"
      :title="health.ok ? 'Database connected' : 'Database unreachable'"
      :description="`swalekha_db - ${health.database}, checked as ${health.ownerName}.`"
    />
    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Health check failed" :description="error" />

    <div class="swalekha-grid">
      <UCard v-for="item in pillarCards" :key="item.label" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">{{ item.label }}</p>
            <p class="truncate text-sm text-highlighted">{{ item.value }}</p>
          </div>
          <UIcon :name="item.icon" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
    </div>

    <p class="text-xs text-muted">
      PersonalFin_02-05 (Accounts Hub, Contacts + Person Ledger, Expense &amp; Income, Travel Expense Sheets) are live - see the buttons above. Every feature below arrives in its own later stage.
    </p>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaHealth } from '../utils/swalekha-api'

useHead({ title: 'Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const health = ref<SwalekhaHealth | null>(null)

const pillarCards = [
  { label: 'PersonalFin_06-08', value: 'Investments (FD/RD, Mutual Funds, Shares)', icon: 'i-lucide-trending-up' },
  { label: 'PersonalFin_09', value: 'Loans', icon: 'i-lucide-hand-coins' },
  { label: 'PersonalFin_10', value: 'Insurance', icon: 'i-lucide-shield-check' },
  { label: 'PersonalFin_11', value: 'Diary, Notes, Calendar, Contacts', icon: 'i-lucide-notebook-pen' },
  { label: 'PersonalFin_12', value: 'Net Worth & Reports', icon: 'i-lucide-chart-pie' }
]

async function refreshHealth() {
  loading.value = true
  error.value = ''
  try {
    health.value = await api.get<SwalekhaHealth>('health')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not reach the Swalekha API.'
  } finally {
    loading.value = false
  }
}

onMounted(refreshHealth)
</script>

<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-landmark" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Status</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshStatus">Refresh</UButton>
        <UButton to="/chart-of-accounts" icon="i-lucide-list-tree" color="neutral" variant="soft">Chart</UButton>
        <UButton to="/fiscal-periods" icon="i-lucide-calendar-range" color="neutral" variant="soft">Periods</UButton>
        <UButton to="/general-ledger" icon="i-lucide-book-open-check" color="neutral" variant="soft">Ledger</UButton>
        <UButton to="/posting-rules" icon="i-lucide-route" color="neutral" variant="soft">Rules</UButton>
        <UButton to="/setup" icon="i-lucide-sliders-horizontal">Setup</UButton>
      </div>
    </div>

    <UAlert
      v-if="status"
      :icon="status.enabled ? 'i-lucide-circle-check' : 'i-lucide-lock-keyhole'"
      :color="status.enabled ? 'success' : 'warning'"
      variant="subtle"
      :title="status.enabled ? 'Enabled' : 'Disabled'"
      :description="status.message"
    />

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Status unavailable" :description="error" />

    <div class="final-accounts-grid">
      <UCard v-for="item in statusCards" :key="item.label" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">{{ item.label }}</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ item.value }}</p>
          </div>
          <UIcon :name="item.icon" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatFinalAccountsDate, type FinalAccountsSettings, useFinalAccountsApiClient } from '../utils/final-accounts-api'

useHead({ title: 'Final Accounts Status' })

const api = useFinalAccountsApiClient()
const loading = ref(false)
const error = ref('')
const status = ref<FinalAccountsSettings | null>(null)

const statusCards = computed(() => {
  const row = status.value
  return [
    { label: 'Feature', value: row?.featureKey || 'FINAL_ACCOUNTS', icon: 'i-lucide-flag' },
    { label: 'API', value: row?.apiRoot || '/api/final-accounts', icon: 'i-lucide-plug' },
    { label: 'Route', value: row?.routeRoot || '/final-accounts', icon: 'i-lucide-route' },
    { label: 'Posting', value: row?.postingMode || 'ManualSync', icon: 'i-lucide-git-branch' },
    { label: 'Template', value: row?.statementTemplate || 'GarmentRetail.v1', icon: 'i-lucide-file-spreadsheet' },
    { label: 'Checked', value: formatFinalAccountsDate(row?.checkedAtUtc), icon: 'i-lucide-clock-3' }
  ]
})

async function refreshStatus() {
  loading.value = true
  error.value = ''
  try {
    status.value = await api.get<FinalAccountsSettings>('status')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Final Accounts status could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(refreshStatus)
</script>

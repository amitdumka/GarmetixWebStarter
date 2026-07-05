<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scan-barcode" class="size-4" /> POS counter</p>
          <h2 class="garmetix-dashboard-title">Counter dashboard</h2>
          <p class="garmetix-dashboard-subtitle">
            Open the counter, review sales, manage held bills, and close the store day from the lightweight POS app.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/sale" icon="i-lucide-scan-barcode">Open Sale</UButton>
          <UButton to="/history" color="neutral" variant="soft" icon="i-lucide-history">Sales History</UButton>
        </div>
      </div>
    </div>

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
useHead({ title: 'POS Counter - Garmetix POS' })

const quickActions = [
  { label: 'Day Open', to: '/day-open', icon: 'i-lucide-sunrise' },
  { label: 'New Sale', to: '/sale', icon: 'i-lucide-scan-barcode' },
  { label: 'Sales History', to: '/history', icon: 'i-lucide-history' },
  { label: 'Held Bills', to: '/hold-bills', icon: 'i-lucide-pause-circle' },
  { label: 'Day Close', to: '/day-close', icon: 'i-lucide-sunset' }
]

const statusItems = [
  { label: 'Billing mode', value: 'Counter ready', detail: 'Fullscreen counter layout is active for faster billing.' },
  { label: 'Print mode', value: 'Invoice ready', detail: 'Saved invoices can be reprinted from history or print queue.' },
  { label: 'CRM signal', value: 'Digital bills', detail: 'Sales history shows Digital Bill link and WhatsApp status.' }
]
</script>

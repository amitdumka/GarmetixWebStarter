<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scan-barcode" class="size-4" /> POS counter</p>
          <h2 class="garmetix-dashboard-title">Counter dashboard</h2>
          <p class="garmetix-dashboard-subtitle">
            Stage 12B.1 prepares the small POS app routes before moving the legacy sale invoice workflow.
          </p>
        </div>
        <UButton to="/sale" icon="i-lucide-scan-barcode">Open Sale</UButton>
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
  { label: 'Held Bills', to: '/hold-bills', icon: 'i-lucide-pause-circle' },
  { label: 'Day Close', to: '/day-close', icon: 'i-lucide-sunset' }
]

const statusItems = [
  { label: 'Billing mode', value: 'GST / Non-GST split', detail: 'Final endpoints will reuse the existing unified API.' },
  { label: 'Print mode', value: 'Invoice ready', detail: 'Print queue route is prepared for generated invoices.' },
  { label: 'Migration state', value: 'Foundation', detail: 'Legacy billing remains untouched until POS parity is ready.' }
]
</script>

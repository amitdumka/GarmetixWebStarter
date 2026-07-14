<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-landmark" class="size-4" />
            Books route
          </p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ description }}</p>
        </div>
        <UBadge :color="badgeColor" variant="subtle">{{ badge }}</UBadge>
      </div>
    </div>

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="item in items" :key="item" class="garmetix-metric-card">
        <p class="text-sm font-medium">{{ item }}</p>
        <p class="garmetix-metric-caption">This screen is reserved in Books and will be connected after the accounting endpoint contract is reviewed.</p>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  title: string
  description: string
  badge?: string
  badgeColor?: 'primary' | 'success' | 'warning' | 'error' | 'neutral'
  items?: string[]
}>(), {
  badge: 'Read-only foundation',
  badgeColor: 'warning',
  items: () => ['Ledger impact', 'Print or export', 'Audit trail']
})
</script>

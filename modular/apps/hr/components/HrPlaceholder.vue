<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-users-round" class="size-4" />
            HR module route
          </p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ description }}</p>
        </div>
        <UBadge color="warning" variant="subtle">{{ badge }}</UBadge>
      </div>
    </div>

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="item in items" :key="item" class="garmetix-metric-card">
        <p class="text-sm font-medium">{{ item }}</p>
        <p class="garmetix-metric-caption">Connection and write actions are scheduled after the first HR shell build is stable.</p>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
withDefaults(defineProps<{
  title: string
  description: string
  badge?: string
  items?: string[]
}>(), {
  badge: 'Next HR slice',
  items: () => ['Read model', 'API action', 'Audit trail']
})
</script>

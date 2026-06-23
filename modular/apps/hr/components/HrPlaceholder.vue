<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">HR module route</p>
          <h2 class="mt-1 text-2xl font-semibold">{{ title }}</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">{{ description }}</p>
        </div>
        <UBadge color="warning" variant="subtle">{{ badge }}</UBadge>
      </div>
    </div>

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="item in items" :key="item" class="border border-default bg-muted/20 p-4">
        <p class="text-sm font-medium">{{ item }}</p>
        <p class="mt-1 text-xs text-muted">Connection and write actions are scheduled after the first HR shell build is stable.</p>
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

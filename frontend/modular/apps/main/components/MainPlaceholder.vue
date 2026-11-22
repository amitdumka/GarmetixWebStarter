<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Back Office route</p>
          <h2 class="mt-1 text-2xl font-semibold">{{ title }}</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">{{ description }}</p>
        </div>
        <UBadge :color="badgeColor" variant="subtle">{{ badge }}</UBadge>
      </div>
    </div>

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="item in items" :key="item" class="border border-default bg-muted/20 p-4">
        <p class="text-sm font-medium">{{ item }}</p>
        <p class="mt-1 text-xs text-muted">This screen is reserved in Back Office and will be connected after the legacy page contract is reviewed.</p>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
// Runtime props on purpose: type-only defineProps<{...}>() compiles to zero props in this production build.
defineProps({
  title: { type: String, required: true },
  description: { type: String, required: true },
  badge: { type: String, default: 'Route shell' },
  badgeColor: { type: String as () => 'primary' | 'success' | 'warning' | 'error' | 'neutral', default: 'warning' },
  items: { type: Array as () => string[], default: () => ['Legacy parity', 'API read model', 'Role permission check'] }
})
</script>

<script setup lang="ts">
const props = withDefaults(defineProps<{
  metrics?: any[]
  business?: boolean
  loading?: boolean
}>(), {
  metrics: () => [],
  business: false,
  loading: false
})
</script>

<template>
  <div class="dashboard-v3-metric-grid" :class="{ business: props.business }">
    <template v-if="props.loading && !props.metrics.length">
      <UCard v-for="index in 6" :key="index" class="dashboard-v3-metric-card dashboard-v3-skeleton-card">
        <USkeleton class="h-3 w-20" />
        <USkeleton class="mt-3 h-7 w-28" />
        <USkeleton class="mt-3 h-3 w-24" />
      </UCard>
    </template>
    <UCard v-for="metric in props.metrics" :key="metric.label" class="dashboard-v3-metric-card">
      <div class="dashboard-v3-metric-body">
        <div>
          <p>{{ metric.label }}</p>
          <strong>{{ metric.displayValue }}</strong>
          <span>{{ metric.caption }}</span>
        </div>
        <UBadge :color="metric.color || 'neutral'" variant="subtle" :icon="metric.icon || 'i-lucide-circle'" />
      </div>
    </UCard>
  </div>
</template>

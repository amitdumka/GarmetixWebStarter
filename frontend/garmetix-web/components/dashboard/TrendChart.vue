<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  badge?: string
  points?: any[]
  salesLabel?: string
  purchaseLabel?: string
}>(), {
  description: '',
  badge: 'Daily',
  points: () => [],
  salesLabel: 'Sales',
  purchaseLabel: 'Purchase'
})

function trendHeight(value: number, key: 'sales' | 'purchase') {
  const max = Math.max(1, ...props.points.map((row: any) => Number(row[key] || 0)))
  return `${Math.max(6, Math.round((Number(value || 0) / max) * 100))}%`
}
</script>

<template>
  <UCard class="dashboard-v3-card dashboard-v3-wide">
    <template #header>
      <div class="dashboard-v3-card-header">
        <div>
          <h2>{{ props.title }}</h2>
          <p>{{ props.description }}</p>
        </div>
        <UBadge color="neutral" variant="subtle">{{ props.badge }}</UBadge>
      </div>
    </template>
    <div v-if="props.points.length" class="dashboard-v3-chart">
      <div v-for="point in props.points" :key="point.label" class="dashboard-v3-chart-day">
        <div class="dashboard-v3-chart-bars">
          <span class="sales" :style="{ height: trendHeight(point.sales, 'sales') }" />
          <span class="purchase" :style="{ height: trendHeight(point.purchase, 'purchase') }" />
        </div>
        <small>{{ point.label }}</small>
      </div>
    </div>
    <UiCrudEmptyState
      v-else
      title="No trend data"
      description="The trend chart will appear after sales or purchase activity is available."
      icon="i-lucide-chart-no-axes-combined"
    />
    <div class="dashboard-v3-legend">
      <span><i class="sales" /> {{ props.salesLabel }}</span>
      <span><i class="purchase" /> {{ props.purchaseLabel }}</span>
    </div>
  </UCard>
</template>

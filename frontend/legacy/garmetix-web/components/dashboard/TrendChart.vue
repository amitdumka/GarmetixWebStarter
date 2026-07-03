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

function trendHeight(value: number, key: 'sales' | 'purchase' | 'profit' | 'nonGstSales') {
  const max = Math.max(1, ...props.points.map((row: any) => Math.abs(Number(row[key] || 0))))
  return `${Math.max(6, Math.round((Math.abs(Number(value || 0)) / max) * 100))}%`
}

const hasProfit = computed(() => props.points.some((row: any) => row.profit !== undefined || row.nonGstSales !== undefined))
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
        <div class="dashboard-v3-chart-bars" :class="{ extended: hasProfit }">
          <span class="sales" :style="{ height: trendHeight(point.sales, 'sales') }" title="GST / regular sales" />
          <span class="purchase" :style="{ height: trendHeight(point.purchase, 'purchase') }" title="Purchase" />
          <span v-if="hasProfit" class="profit" :class="{ negative: Number(point.profit || 0) < 0 }" :style="{ height: trendHeight(point.profit, 'profit') }" title="Profit" />
          <span v-if="hasProfit" class="non-gst" :style="{ height: trendHeight(point.nonGstSales, 'nonGstSales') }" title="Non-GST sales" />
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
      <span v-if="hasProfit"><i class="profit" /> Profit</span>
      <span v-if="hasProfit"><i class="non-gst" /> Non-GST sales</span>
    </div>
  </UCard>
</template>

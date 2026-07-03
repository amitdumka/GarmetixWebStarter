<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  items?: any[]
}>(), {
  description: '',
  items: () => []
})

const total = computed(() => props.items.reduce((sum, item) => sum + Math.abs(Number(item.value || 0)), 0))

function barWidth(value: number) {
  if (!total.value) return '0%'
  return `${Math.max(6, Math.round((Math.abs(Number(value || 0)) / total.value) * 100))}%`
}

function badgeColor(color: string) {
  if (['primary', 'success', 'warning', 'error', 'neutral'].includes(color)) return color as any
  return 'neutral'
}
</script>

<template>
  <UCard class="dashboard-v3-card dashboard-breakdown-card">
    <template #header>
      <div class="dashboard-v3-card-header">
        <div>
          <h2>{{ title }}</h2>
          <p>{{ description }}</p>
        </div>
      </div>
    </template>

    <div v-if="items.length" class="dashboard-breakdown-list">
      <div v-for="item in items" :key="item.label" class="dashboard-breakdown-row">
        <div class="dashboard-breakdown-row-header">
          <span>
            <UBadge :color="badgeColor(item.color)" variant="subtle" :icon="item.icon || 'i-lucide-circle'" />
            <strong>{{ item.label }}</strong>
          </span>
          <strong>{{ item.displayValue }}</strong>
        </div>
        <div class="dashboard-breakdown-bar" :data-color="item.color || 'neutral'">
          <i :style="{ width: barWidth(item.value) }" />
        </div>
        <small>{{ item.caption }}</small>
      </div>
    </div>

    <UiCrudEmptyState
      v-else
      title="No breakdown data"
      description="Breakdowns will appear after activity exists for this dashboard period."
      icon="i-lucide-chart-pie"
    />
  </UCard>
</template>

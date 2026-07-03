<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  signals?: any[]
}>(), {
  description: '',
  signals: () => []
})
</script>

<template>
  <UCard class="dashboard-v3-card">
    <template #header>
      <div class="dashboard-v3-card-header">
        <div>
          <h2>{{ props.title }}</h2>
          <p>{{ props.description }}</p>
        </div>
      </div>
    </template>
    <div class="dashboard-v3-health-grid">
      <div v-for="signal in props.signals" :key="signal.label" class="dashboard-v3-health-card">
        <UBadge :color="signal.color || 'neutral'" variant="subtle" :icon="signal.icon || 'i-lucide-activity'">
          {{ signal.status }}
        </UBadge>
        <strong>{{ signal.value }}</strong>
        <span>{{ signal.label }}</span>
        <small>{{ signal.description }}</small>
      </div>
      <UiCrudEmptyState
        v-if="!props.signals.length"
        class="dashboard-v3-full-row"
        title="No health signals"
        description="Signals will appear after activity is available."
        icon="i-lucide-activity"
      />
    </div>
  </UCard>
</template>

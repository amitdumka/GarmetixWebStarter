<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  actions?: any[]
}>(), {
  description: '',
  actions: () => []
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
    <div class="dashboard-v3-action-grid">
      <NuxtLink v-for="action in props.actions" :key="action.to" :to="action.to" class="dashboard-v3-action-card">
        <UIcon :name="action.icon || 'i-lucide-arrow-up-right'" class="h-5 w-5" />
        <span>
          <strong>{{ action.label }}</strong>
          <small>{{ action.description }}</small>
        </span>
        <UBadge v-if="action.attention" color="warning" variant="subtle">Check</UBadge>
      </NuxtLink>
      <UiCrudEmptyState
        v-if="!props.actions.length"
        class="dashboard-v3-full-row"
        title="No quick actions"
        description="Actions will appear here when available for your role and workspace."
        icon="i-lucide-route"
      />
    </div>
  </UCard>
</template>

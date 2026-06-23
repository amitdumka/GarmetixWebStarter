<script setup lang="ts">
withDefaults(defineProps<{
  title: string
  description?: string
  loading?: boolean
  error?: string
  empty?: boolean
  emptyTitle?: string
  emptyDescription?: string
  emptyIcon?: string
}>(), {
  description: '',
  error: '',
  emptyTitle: 'No records found',
  emptyDescription: 'Records will appear here when they are available.',
  emptyIcon: 'i-lucide-inbox'
})

const emit = defineEmits<{
  retry: []
}>()
</script>

<template>
  <UCard class="planner-card register-panel">
    <template #header>
      <div class="register-panel-header">
        <div class="min-w-0">
          <h2>{{ title }}</h2>
          <p v-if="description">{{ description }}</p>
        </div>
        <div v-if="$slots.actions" class="register-panel-actions">
          <slot name="actions" />
        </div>
      </div>
    </template>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      icon="i-lucide-circle-alert"
      title="Could not load this register"
      :description="error"
      :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: () => emit('retry') }]"
    />

    <div v-else-if="loading" class="register-panel-loading" aria-live="polite" aria-label="Loading records">
      <USkeleton v-for="row in 6" :key="row" class="h-11 w-full" />
    </div>

    <UiCrudEmptyState
      v-else-if="empty"
      :title="emptyTitle"
      :description="emptyDescription"
      :icon="emptyIcon"
    />

    <slot v-else />
  </UCard>
</template>

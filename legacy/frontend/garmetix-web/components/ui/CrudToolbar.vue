<script setup lang="ts">
defineProps<{
  searchPlaceholder?: string
  refreshLabel?: string
  createLabel?: string
  loading?: boolean
}>()

const search = defineModel<string>('search', { default: '' })

const emit = defineEmits<{
  refresh: []
  create: []
}>()
</script>

<template>
  <div class="crud-toolbar">
    <UInput
      v-model="search"
      icon="i-lucide-search"
      :placeholder="searchPlaceholder || 'Search'"
      class="crud-search"
    />

    <div class="crud-actions">
      <slot name="filters" />
      <UButton
        color="neutral"
        variant="subtle"
        icon="i-lucide-refresh-cw"
        :loading="loading"
        :label="refreshLabel || 'Refresh'"
        @click="emit('refresh')"
      />
      <UButton
        v-if="createLabel"
        icon="i-lucide-plus"
        :label="createLabel"
        @click="emit('create')"
      />
    </div>
  </div>
</template>

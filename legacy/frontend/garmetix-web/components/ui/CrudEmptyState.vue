<script setup lang="ts">
defineProps<{
  title: string
  description?: string
  icon?: string
  actionLabel?: string
}>()

const emit = defineEmits<{
  action: []
}>()
</script>

<template>
  <UEmpty
    :icon="icon || 'i-lucide-inbox'"
    :title="title"
    :description="description"
  >
    <template v-if="actionLabel" #actions>
      <UButton icon="i-lucide-plus" :label="actionLabel" @click="emit('action')" />
    </template>
  </UEmpty>
</template>

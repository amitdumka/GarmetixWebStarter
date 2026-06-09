<script setup lang="ts">
defineProps<{
  title: string
  description?: string
  icon?: string
  primaryLabel?: string
  primaryIcon?: string
}>()

const emit = defineEmits<{
  primary: []
}>()
</script>

<template>
  <div class="module-page-header">
    <div class="module-page-title">
      <UAvatar v-if="icon" :icon="icon" size="lg" color="primary" variant="subtle" />
      <div>
        <h2>{{ title }}</h2>
        <p v-if="description">{{ description }}</p>
      </div>
    </div>

    <slot name="actions">
      <UButton
        v-if="primaryLabel"
        :icon="primaryIcon || 'i-lucide-plus'"
        :label="primaryLabel"
        @click="emit('primary')"
      />
    </slot>
  </div>
</template>

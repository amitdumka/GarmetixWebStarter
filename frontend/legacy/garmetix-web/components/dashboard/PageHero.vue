<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  subtitle?: string
  badge?: string
  badgeIcon?: string
  badgeColor?: string
  loading?: boolean
  business?: boolean
}>(), {
  subtitle: '',
  badge: '',
  badgeIcon: 'i-lucide-layout-dashboard',
  badgeColor: 'primary',
  loading: false,
  business: false
})

const emit = defineEmits<{ refresh: [] }>()
</script>

<template>
  <div class="dashboard-v3-hero" :class="{ business: props.business }">
    <div>
      <UBadge v-if="props.badge" :color="props.badgeColor" variant="subtle" :icon="props.badgeIcon">
        {{ props.badge }}
      </UBadge>
      <h1>{{ props.title }}</h1>
      <p>{{ props.subtitle }}</p>
    </div>
    <div class="dashboard-v3-hero-actions">
      <UBadge :color="props.loading ? 'warning' : 'success'" variant="subtle">
        {{ props.loading ? 'Loading' : 'Live' }}
      </UBadge>
      <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="props.loading" @click="emit('refresh')" />
    </div>
  </div>
</template>

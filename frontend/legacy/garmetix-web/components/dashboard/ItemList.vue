<script setup lang="ts">
const props = withDefaults(defineProps<{
  title: string
  description?: string
  items?: any[]
  icon?: string
  itemTo?: string
  emptyTitle?: string
  emptyDescription?: string
  showDate?: boolean
  amountPrefix?: string
}>(), {
  description: '',
  items: () => [],
  icon: 'i-lucide-arrow-up-right',
  itemTo: '',
  emptyTitle: 'No records',
  emptyDescription: 'Records will appear here when available.',
  showDate: false,
  amountPrefix: ''
})

function itemRoute(item: any) {
  return `/${item.resource || props.itemTo}`
}

function dateTime(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

function statusColor(status: string) {
  const value = String(status || '').toLowerCase()
  if (value.includes('ready') || value.includes('paid') || value.includes('done') || value.includes('dashboard')) return 'success'
  if (value.includes('required') || value.includes('low') || value.includes('report') || value.includes('control')) return 'warning'
  if (value.includes('cancel') || value.includes('fail')) return 'error'
  return 'neutral'
}
</script>

<template>
  <UCard class="dashboard-v3-card">
    <template #header>
      <div class="dashboard-v3-card-header">
        <div>
          <h2>{{ props.title }}</h2>
          <p>{{ props.description }}</p>
        </div>
        <slot name="action" />
      </div>
    </template>
    <div class="dashboard-v3-list">
      <template v-for="item in props.items" :key="item.resourceId || item.title">
        <NuxtLink v-if="props.itemTo" :to="itemRoute(item)" class="dashboard-v3-list-item">
          <UIcon :name="props.icon" class="h-4 w-4" />
          <span>
            <strong>{{ item.title }}</strong>
            <small>
              {{ item.subtitle }}
              <template v-if="props.showDate"> · {{ dateTime(item.onDate) }}</template>
            </small>
          </span>
          <UBadge v-if="item.status" :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge>
          <strong v-else-if="item.amount !== undefined">{{ props.amountPrefix }}{{ item.amount }}</strong>
        </NuxtLink>
        <div v-else class="dashboard-v3-list-item">
          <UIcon :name="props.icon" class="h-4 w-4" />
          <span>
            <strong>{{ item.title }}</strong>
            <small>
              {{ item.subtitle }}
              <template v-if="props.showDate"> · {{ dateTime(item.onDate) }}</template>
            </small>
          </span>
          <UBadge v-if="item.status" :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge>
          <strong v-else-if="item.amount !== undefined">{{ props.amountPrefix }}{{ item.amount }}</strong>
        </div>
      </template>
      <UiCrudEmptyState v-if="!props.items.length" :title="props.emptyTitle" :description="props.emptyDescription" icon="i-lucide-circle-check" />
    </div>
  </UCard>
</template>
